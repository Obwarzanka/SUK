"""
Jednorazowa konwersja zrzutu pulpitu SimRail (pulpit_*.json z moda SimRailMost) na PROJEKT układu SUK (station.json).

Wynik to propozycja do poprawienia przez właściciela, nie gotowy układ:
- geometria = siatka pulpitu EBI: komórka SX × SY px (jak na ekranie gry – 120 kolumn × 32 wiersze, komórka 1:2), długości umowne;
- topologia wyprowadzona z sąsiedztwa komórek: narożniki (obrót 0: dół+prawo, 1: dół+lewo, 2: góra+lewo, 3: góra+prawo),
  zwrotnica = tor poziomy + odgałęzienie do pionowego łącznika w sąsiedniej (albo tej samej) kolumnie;
  isLeft = zwrotnica lewa względem jazdy od iglicy, isUp = odgałęzienie w górę rysunku;
- odcinki: railNetworkName elementu (np. l1_kol_it5 → KO:it5), granice tam, gdzie zmienia się nazwa;
- semafory, wykolejnice, kozły, perony, napisy; BEZ szlaków / blokad liniowych (końce torów zostają końcami),
  BEZ tablicy przebiegów i zależności (projektuje człowiek).
Obok powstaje mapa <wynik>.simrail.json (nazwa SimRail → identyfikator SUK) dla okna „SimRail” w SUK.

Użycie: python -I pulpit_do_suk.py pulpit_….json wynik.json [KOD_POSTERUNKU]
"""
import json, sys, collections, re
sys.stdout.reconfigure(encoding="utf-8")

SX, SY = 24, 32        # px na komórkę w układzie SUK (EBI ma 16 × 32; w poziomie luźniej, bo symbole SUK mają stały rozmiar)
SIATKA = SX            # dawna nazwa (odległości poziome)
METRY_NA_KOMORKE = 15  # umowna długość komórki
wej, wyj = sys.argv[1], sys.argv[2]
POST = sys.argv[3] if len(sys.argv) > 3 else None

d = json.load(open(wej, encoding="utf-8"))
nazwa_post = d.get("posterunek") or "Posterunek"
if POST is None:
    POST = next((e["pola"].get("prefix", "").upper() for e in d["elementy"] if e["klasa"] == "EBI300ScreenStatus"), "") or "PO"
raport = []

# ------------------------------------------------------------------ 1. sklejenie zakładek wirtualnych
el = d["elementy"]
def nazwa(p): return p.get("railNetworkName") or ""
zak = sorted({e["pola"].get("virtualTab", 0) for e in el})

# Zakładki zachodzą na siebie: środek stacji (tory stacyjne) jest na obu, narysowany w różnej długości – to nie jest
# przesunięcie. Szukamy cięcia: kolumny c0 w poprzedniej zakładce i c1 w następnej, w których są WYŁĄCZNIE tory poziome
# o tych samych nazwach w tych samych wierszach (równoległe tory stacyjne). Z poprzedniej bierzemy wszystko do c0,
# z następnej od c1 (przesunięte tak, że c1 = c0). Wybieramy parę, przy której żaden obiekt nie ginie i żaden się nie dubluje.
def komorki(p):
    if p.get("cells"): return [tuple(c) for c in p["cells"]]
    return [tuple(p["position"])] if p.get("position") else []
po_zak = {t: [(e["klasa"], dict(e["pola"])) for e in el if e["pola"].get("virtualTab", 0) == t] for t in zak}

def kolumny_czyste(lista):
    zaj = collections.defaultdict(list)
    for k, p in lista:
        for (x, y) in komorki(p): zaj[(x, y)].append((k, nazwa(p), p.get("isHorizontal", True)))
    kol = collections.defaultdict(dict); brudne = set()
    for (x, y), l in zaj.items():
        if len(l) != 1 or l[0][0] != "EBI300StraightTrack" or not l[0][2]: brudne.add(x)
        else: kol[x][y] = l[0][1]
    return {x: w for x, w in kol.items() if x not in brudne and len(w) >= 3}

def nazwy_do(lista, granica, lewa):
    s = set()
    for k, p in lista:
        n = nazwa(p)
        if not n: continue
        xs = [c[0] for c in komorki(p)]
        if xs and ((min(xs) <= granica) if lewa else (max(xs) >= granica)): s.add((k, n))
    return s

przes = {zak[0]: 0}; ciecie = {}
for t in zak[1:]:
    a, b = po_zak[t - 1], po_zak[t]
    ka, kb = kolumny_czyste(a), kolumny_czyste(b)
    wszystkie = {(k, nazwa(p)) for k, p in a + b if nazwa(p)}
    najl = None
    for c0, w0 in ka.items():
        for c1, w1 in kb.items():
            wspolne = set(w0) & set(w1)
            if len(wspolne) < 3 or any(w0[y] != w1[y] for y in wspolne): continue
            # wiersz tylko po jednej stronie cięcia: dobrze, jeśli ten sam tor (nazwa) jest po drugiej stronie gdzie indziej
            # (połączy się przez wypełnienie toru o tej samej nazwie); tor bez nazwy po jednej stronie to niezgodność
            niezg = sum(1 for y in set(w0) ^ set(w1) if not (w0.get(y) or w1.get(y)))
            na, nb = nazwy_do(a, c0, True), nazwy_do(b, c1, False)
            przez = {("EBI300StraightTrack", n) for n in list(w0.values()) + list(w1.values()) if n}
            strata = len(wszystkie - (na | nb)); dubel = len((na & nb) - przez)
            ocena = (strata + dubel + niezg, -len(wspolne), abs(c0 - 60))
            if najl is None or ocena < najl[0]: najl = (ocena, c0, c1, strata, dubel, niezg)
    if najl is None:
        raport.append(f"zakładka {t}: nie znaleziono cięcia – zakładka pominięta"); continue
    _, c0, c1, strata, dubel, niezg = najl
    ciecie[t] = (c0, c1)
    przes[t] = przes[t - 1] + c0 - c1
    raport.append(f"zakładka {t}: cięcie w kolumnie {c0} (poprzednia) = {c1} (ta), przesunięcie {c0 - c1}; "
                  f"obiektów utraconych {strata}, zdublowanych {dubel}, torów bez nazwy tylko po jednej stronie {niezg}")

elementy = []
for t in zak:
    if t != zak[0] and t not in ciecie: continue
    lo = ciecie[t][1] if t in ciecie else -10**9            # od c1 tej zakładki
    hi = ciecie[t + 1][0] if t + 1 in ciecie else 10**9      # do c0 przed następną
    ox = przes[t]
    for k, p in po_zak[t]:
        p = dict(p)
        if p.get("cells"):
            cs = [c for c in p["cells"] if lo <= c[0] <= hi]
            if not cs: continue
            p["cells"] = [[c[0] + ox, c[1]] for c in cs]; p["position"] = p["cells"][0]
        elif p.get("position"):
            if not (lo <= p["position"][0] <= hi): continue
            p["position"] = [p["position"][0] + ox, p["position"][1]]
        elementy.append((k, p))

proste = {}   # (nazwa, y, poziomy) -> zbiór x (albo y dla pionowych)
inne = []
for k, p in elementy:
    if k == "EBI300StraightTrack" and p.get("cells"):
        poz = p.get("isHorizontal", True)
        n = nazwa(p) or f"_bez_nazwy_{len(proste)}"
        if poz:
            key = (n, p["cells"][0][1], True)
            proste.setdefault(key, {"pola": p, "kom": set()})["kom"].update(c[0] for c in p["cells"])
        else:
            key = (n, p["cells"][0][0], False)
            proste.setdefault(key, {"pola": p, "kom": set()})["kom"].update(c[1] for c in p["cells"])
    else:
        inne.append((k, p))
# usunięcie duplikatów innych elementów (te same: klasa, nazwa, pozycja)
widziane = set(); inne2 = []
for k, p in inne:
    key = (k, nazwa(p), tuple(p.get("position") or ()), p.get("visibleName"))
    if key in widziane: continue
    widziane.add(key); inne2.append((k, p))
inne = inne2

# ------------------------------------------------------------------ 2. komórki warstw
H = {}  # (x,y) -> nazwa odcinka
V = {}
C = {}  # narożniki (x,y) -> (rot, nazwa)
for (n, stala, poz), v in proste.items():
    # ten sam tor na obu zakładkach: wypełnij przerwę między częściami (ciągły tor)
    zakres = range(min(v["kom"]), max(v["kom"]) + 1) if n and not n.startswith("_bez") else v["kom"]
    for z in zakres:
        if poz: H.setdefault((z, stala), n)
        else: V.setdefault((stala, z), n)
J = {}; sygnaly = []; wykolejnice = []; kozly = []; perony = []; napisy = []; skrzyz = []; blokady = []; nastawnie = []; przejazdy_ebi = []
for k, p in inne:
    pos = tuple(p.get("position") or (0, 0))
    if k == "EBI300CornerTrack": C[pos] = (p.get("rotation", 0) % 4, nazwa(p))
    elif k == "EBI300HorizontalJunction": J[pos] = p
    elif k == "EBI300Signal": sygnaly.append(p)
    elif k == "EBI300Derailer": wykolejnice.append(p)
    elif k == "EBI300StopGuard": kozly.append(p)
    elif k == "EBI300Platform": perony.append(p)
    elif k == "EBI300RichText": napisy.append(p)
    elif k == "EBI300LineBlock": blokady.append(p)
    elif k == "EBI300ControlHouse": nastawnie.append(p)
    elif k == "EBI300RoadCrossing": przejazdy_ebi.append(p)
    elif k == "EBI300CrossingTrack": skrzyz.append(p); H.setdefault(pos, nazwa(p))
for pos, p in J.items():
    if pos not in H: H[pos] = nazwa(p)

# Semafory stoją często w pustej komórce między końcem toru a początkiem następnego (przerwa na rysunku = złącze).
# Wypełniamy przerwę (do 3 komórek w każdą stronę) odcinkiem toru ZA semaforem w kierunku jazdy wstecz,
# tak żeby granica odcinków (i węzeł semafora) wypadła tuż przed następnym torem.
wypelnione = 0
for p in sygnaly:
    x, y = p["position"]; kier = 1 if p.get("direction") == "Right" else -1
    if (x, y) in H: continue
    def najblizszy(krok):
        for i in range(1, 4):
            if (x + krok * i, y) in H: return i
        return None
    za, przed = najblizszy(-kier), najblizszy(kier)   # za = tor, z którego jedzie pociąg do semafora
    if za is None and przed is None: continue
    sec = H[(x - kier * za, y)] if za is not None else H[(x + kier * przed, y)]
    if za is not None:
        for i in range(0, za): H.setdefault((x - kier * i, y), sec)
    if przed is not None:
        for i in range(1, przed): H.setdefault((x + kier * i, y), sec)
        H.setdefault((x, y), sec)
    wypelnione += 1
# Luka jednej komórki tuż obok semafora stojącego na torze (złącze izolowane rysowane jako przerwa) – też ciągły tor;
# luka należy do toru po drugiej stronie, więc granica odcinków wypada przy semaforze.
for p in sygnaly:
    x, y = p["position"]
    if (x, y) not in H: continue
    for strona in (-1, 1):
        g = (x + strona, y); za = (x + 2 * strona, y)
        if g not in H and g not in V and g not in C and za in H:
            H[g] = H[za]; wypelnione += 1
raport.append(f"przerwy przy semaforach wypełnione: {wypelnione}")

PORTY = {0: "SE", 1: "SW", 2: "NW", 3: "NE"}
DX = {"E": (1, 0), "W": (-1, 0), "N": (0, -1), "S": (0, 1)}
PRZ = {"E": "W", "W": "E", "N": "S", "S": "N"}

graf = collections.defaultdict(set)
def lacz(a, b): graf[a].add(b); graf[b].add(a)
for (x, y) in H:
    graf[("H", x, y)]
    if (x + 1, y) in H: lacz(("H", x, y), ("H", x + 1, y))
for (x, y) in V:
    graf[("V", x, y)]
    if (x, y + 1) in V: lacz(("V", x, y), ("V", x, y + 1))
for (x, y), (rot, n) in C.items():
    graf[("C", x, y)]
    for port in PORTY[rot]:
        ddx, ddy = DX[port]; s = (x + ddx, y + ddy)
        if port in "EW":
            if s in H: lacz(("C", x, y), ("H", *s))
            elif s in C and PRZ[port] in PORTY[C[s][0]]: lacz(("C", x, y), ("C", *s))
        else:
            if s in V: lacz(("C", x, y), ("V", *s))
            elif s in C and PRZ[port] in PORTY[C[s][0]]: lacz(("C", x, y), ("C", *s))

# zwrotnice: odgałęzienie do pionowego łącznika
zwrotnice = {}  # wierzchołek H -> dane
zajete_v = set()
def param(p):
    dy = -1 if p.get("isUp") else 1
    t = 1 if bool(p.get("isLeft")) == bool(p.get("isUp")) else -1   # +1: iglica po lewej (jazda na wschód)
    return dy, t
# 1) rozjazd w tor sąsiedni bez łącznika: ukos wprost do zwrotnicy dwa wiersze dalej z odgałęzieniem w przeciwną stronę
for (x, y), p in sorted(J.items()):
    if ("H", x, y) in zwrotnice: continue
    dy, t = param(p)
    if any((xb, y + dy) in V or (xb, y + dy) in C for xb in (x - 1, x, x + 1)): continue   # jest łącznik – krok 2
    for ddx in sorted(range(-4, 5), key=lambda v: (abs(v), -v * t)):
        q = (x + ddx, y + 2 * dy)
        if q in J and ("H", *q) not in zwrotnice and param(J[q])[0] == -dy:
            lacz(("H", x, y), ("H", *q))
            zwrotnice[("H", x, y)] = {"p": p, "galaz": ("H", *q), "t": t, "dy": dy}
            zwrotnice[("H", *q)] = {"p": J[q], "galaz": ("H", x, y), "t": param(J[q])[1], "dy": -dy}
            break
# 2) odgałęzienie do pionowego łącznika
for (x, y), p in sorted(J.items()):
    if ("H", x, y) in zwrotnice: continue
    dy, t = param(p)
    kolejnosc = [x + t, x, x - t]
    cel = None
    for xb in kolejnosc:
        for kand in (("V", xb, y), ("V", xb, y + dy), ("C", xb, y + dy)):
            if kand in graf and kand not in zajete_v and (kand[0] != "V" or (xb, y + dy) in V or (xb, y + dy) in C):
                cel = kand; break
        if cel: break
    if cel is None:
        def zn(xx, yy):
            s = ("H" if (xx, yy) in H else "") + ("V" if (xx, yy) in V else "") + (f"C{C[(xx, yy)][0]}" if (xx, yy) in C else "") + ("J" if (xx, yy) in J else "")
            return s or "."
        okno = " | ".join(" ".join(f"{zn(xx, yy):>3}" for xx in range(x - 2, x + 3)) for yy in range(y - 2, y + 3))
        raport.append(f"zwrotnica {p.get('visibleName')} ({x},{y}) isLeft={p.get('isLeft')} isUp={p.get('isUp')}: nie znaleziono łącznika – pominięta  [{okno}]"); continue
    zajete_v.add(cel)
    # łącznik zaczyna się w komórce zwrotnicy (ten sam punkt): pomijamy tę komórkę łącznika i łączymy zwrotnicę z następną,
    # inaczej powstaje tor o zerowej długości, z którego SUK nie odczyta kierunku (iglica / nogi pomylone)
    if cel == ("V", x, y) and graf.get(cel) == {("V", x, y + dy)}:
        nast = ("V", x, y + dy)
        graf[nast].discard(cel); del graf[cel]
        cel = nast
    lacz(("H", x, y), cel)
    zwrotnice[("H", x, y)] = {"p": p, "galaz": cel, "t": t, "dy": dy}

# ------------------------------------------------------------------ 3. odcinki wierzchołków
def odc(v):
    k, x, y = v
    if k == "H": return H[(x, y)]
    if k == "V": return V[(x, y)]
    return C[(x, y)][1]
def krotka(n):
    n = re.sub(r"^l\d+_[a-z]+_", "", n) if re.match(r"^l\d+_[a-z]+_i[tz]", n) else n
    return n
def sec_id(n): return f"{POST}:{krotka(n)}"

def px(v): return [round((v[1] + 0.5) * SX, 1), round((v[2] + 0.5) * SY, 1)]

# ------------------------------------------------------------------ 4. węzły: rozgałęzienia, końce, zwrotnice, granice odcinków, semafory
wezly = {}      # id -> {x,y,kind,ref,post}
licz = [0]
def nowy(x, y, kind="", ref=""):
    licz[0] += 1; i = f"{POST}:n{licz[0]}"
    wezly[i] = {"x": round(x, 1), "y": round(y, 1), "kind": kind, "ref": ref, "post": POST}
    return i
wezel_v = {}   # wierzchołek grafu -> id węzła
# samotne komórki bez nazwy (przerywana linia podziału pulpitu, ozdobniki) – to nie tory
samotne = [v for v, s in graf.items() if not s and odc(v).startswith("_bez")]
for v in samotne: del graf[v]
raport.append(f"pominięte samotne komórki bez nazwy (linia podziału pulpitu): {len(samotne)}")
for v, s in graf.items():
    if len(s) != 2 or v in zwrotnice:
        wezel_v[v] = nowy(*px(v))
# granice odcinków na krawędziach grafu (węzeł w połowie)
granice = {}   # frozenset(a,b) -> id
for a, s in list(graf.items()):
    for b in sorted(s):
        if a < b and odc(a) != odc(b):
            pa, pb = px(a), px(b)
            granice[frozenset((a, b))] = nowy((pa[0] + pb[0]) / 2, (pa[1] + pb[1]) / 2)

# semafory: na granicy komórki w kierunku jazdy (Right → x+0.5)
TYPY = {"MainSignal": "wjazdowy", "Default": "wyjazdowy", "DefaultWithShuntingSignal": "wyjazdowy", "ShuntingSignal": "tm",
        "RepeaterSignal": "sp", "NeighborhoodSignalRepeater": "sp", "ShuntingEndPoint": "pk", "RouteEndPoint": "pk", "DistanceSignal": "to"}
sygn_out = {}; nazwy_sygn = set()
def wstaw_na_krawedzi(a, b):
    """Węzeł na połączeniu a–b (istniejąca granica albo nowa) – zwraca id."""
    key = frozenset((a, b))
    if key not in granice:
        pa, pb = px(a), px(b)
        granice[key] = nowy((pa[0] + pb[0]) / 2, (pa[1] + pb[1]) / 2)
    return granice[key]
for p in sygnaly:
    x, y = p["position"]; kier = 1 if p.get("direction") == "Right" else -1
    a = ("H", x, y); b = ("H", x + kier, y)
    if a not in graf:
        raport.append(f"semafor {p.get('visibleName')} ({x},{y}): brak toru w komórce – pominięty"); continue
    if b in graf.get(a, ()): wid = wstaw_na_krawedzi(a, b)
    else:
        if a not in wezel_v: wezel_v[a] = nowy(*px(a))
        wid = wezel_v[a]
    vis = p.get("visibleName") or nazwa(p)
    nm = f"{POST}:{vis}"
    if nm in nazwy_sygn: nm = f"{POST}:{vis}_{nazwa(p)}"
    nazwy_sygn.add(nm)
    typ = TYPY.get(p.get("signalType", ""), "tm")
    s = {"name": nm, "node": wid, "dir": kier, "type": typ, "label": vis, "post": POST}
    if typ == "wyjazdowy" and p.get("signalType") == "Default": s["shunt"] = False
    sygn_out[nm] = s; sygn_out[nm]["_simrail"] = nazwa(p)

# wykolejnice: węzeł w komórce toru
wyk_out = {}
for p in wykolejnice:
    x, y = p["position"]; a = ("H", x, y)
    if a not in graf: raport.append(f"wykolejnica {p.get('visibleName')}: brak toru – pominięta"); continue
    if a not in wezel_v: wezel_v[a] = nowy(*px(a))
    wid = wezel_v[a]; wezly[wid]["kind"] = "derail"; wezly[wid]["ref"] = p.get("visibleName", "")
    nm = f"{POST}:{p.get('visibleName') or nazwa(p)}"
    wyk_out[nm] = {"name": nm, "node": wid, "label": p.get("visibleName", ""), "post": POST, "sec": sec_id(odc(a)), "_simrail": nazwa(p)}

# ------------------------------------------------------------------ 5. krawędzie: łańcuchy między węzłami
def wezel_lub_none(v): return wezel_v.get(v)
krawedzie = []; uzyte = set()
def dodaj(a_id, b_id, pts, sec, ncell):
    krawedzie.append({"a": a_id, "b": b_id, "pts": pts, "sec": sec_id(sec), "len": round(max(5, ncell * METRY_NA_KOMORKE), 1),
                      "track": "", "line": "", "id": f"e{len(krawedzie)}"})
# łańcuch idzie od węzła przez wierzchołki stopnia 2; przecina się na granicach (granice = węzły w połowie połączenia)
for start, nid in list(wezel_v.items()):
    for nast in sorted(graf[start]):
        if frozenset((start, nast)) in uzyte: continue
        a_id = nid; pts = [px(start)]; sec = odc(start); poprz = start; cur = nast; ncell = 0
        while True:
            uzyte.add(frozenset((poprz, cur)))
            g = granice.get(frozenset((poprz, cur)))
            if g:
                gp = [wezly[g]["x"], wezly[g]["y"]]
                pts.append(gp); dodaj(a_id, g, pts, sec, ncell + 0.5)
                a_id = g; pts = [gp]; sec = odc(cur); ncell = 0.5
            # jak w EBI: narożnik i komórka łącznika w wierszu toru nie są punktem załamania – łącznik wchodzi w tor ukosem
            if cur in wezel_v or not (cur[0] == "C" or (cur[0] == "V" and (cur[1], cur[2]) in H)):
                pts.append(px(cur))
            ncell += 1
            if cur in wezel_v:
                dodaj(a_id, wezel_v[cur], pts, sec, ncell); break
            dalej = [n for n in sorted(graf[cur]) if n != poprz]
            if not dalej: dodaj(a_id, nowy(*px(cur)), pts, sec, ncell); break
            poprz, cur = cur, dalej[0]
# ładniejsze odgałęzienie zwrotnicy: pierwszy punkt odchylony w stronę odgałęzienia (SUK liczy iglicę i (−) z kątów)
for v, z in zwrotnice.items():
    nid = wezel_v[v]
    wezly[nid]["kind"] = "switch"; wezly[nid]["ref"] = z["p"].get("visibleName", "")
    for e in krawedzie:
        for koniec in ("a", "b"):
            if e[koniec] != nid: continue
            pts = e["pts"] if koniec == "a" else e["pts"][::-1]
            if len(pts) > 1 and abs(pts[1][1] - pts[0][1]) < 0.01 and abs(pts[1][0] - pts[0][0]) > 0.01: continue  # nogi poziome
            if z["galaz"][0] == "H": continue   # ukos zwrotnica–zwrotnica (rozjazd w tor sąsiedni): prosta linia jak w EBI
            ox, oy = pts[0]
            # odgałęzienie wychodzi ukosem samo z siebie (łącznik w sąsiedniej kolumnie); tylko łącznik w tej samej kolumnie
            # (pionowo w dół / w górę) dostaje lekkie odchylenie, żeby SUK odróżnił nogę (−) od iglicy – bez widocznego haczyka
            if len(pts) > 1 and abs(pts[1][0] - ox) > 0.01: continue
            ddx = z["galaz"][1] - v[1]
            lean = (1 if ddx > 0 else -1) if ddx != 0 else z["t"]
            if lean != z["t"]: zmienione_t = globals().setdefault("zmienione_t", []); zmienione_t.append(z["p"].get("visibleName"))
            pts.insert(1, [round(ox + lean * SX * 0.2, 1), round(oy + z["dy"] * SY * 0.5, 1)])
            if koniec == "b": e["pts"] = pts[::-1]

# (orientacja torów A→B – niżej, po ułożeniu geometrii odgałęzień)

raport.append(f"odgałęzienia odchylone wbrew isLeft/isUp (łącznik po drugiej stronie): {globals().get('zmienione_t', [])}")

# ------------------------------------------------------------------ 5b. orientacja torów A→B (plik z "orient": "ab")
# Ta sama reguła stron węzła co w SUK (TrackOrientation): dwa tory – przeciwne strony (chyba że kąt ostry), zwrotnica –
# para najbardziej zgodnych kierunków (nogi) po jednej stronie, iglica po drugiej. Tory w spójnej części orientujemy
# tak, by przejazd przez węzeł zawsze przechodził z jednej strony na drugą; potem całą część tak, by A→B szło w prawo.
# Rysunek EBI może zawracać (w prawo, w dół, w lewo) – w trybie "ab" to nie jest sprzeczność, liczy się tylko topologia.
przy = collections.defaultdict(list)
for i, e in enumerate(krawedzie): przy[e["a"]].append(i); przy[e["b"]].append(i)
def stycz(i, n):
    e = krawedzie[i]; pts = e["pts"] if e["a"] == n else e["pts"][::-1]
    for q in pts[1:]:
        dx, dy = q[0] - pts[0][0], q[1] - pts[0][1]; l = (dx * dx + dy * dy) ** 0.5
        if l > 1e-6: return (dx / l, dy / l)
    return (1.0, 0.0)
_strony = {}
def strony(n):
    if n in _strony: return _strony[n]
    l = przy[n]; t = {i: stycz(i, n) for i in l}; c = {}
    dot = lambda a, b: t[a][0] * t[b][0] + t[a][1] * t[b][1]
    if len(l) == 2: c[l[0]] = 1; c[l[1]] = 1 if dot(l[0], l[1]) > 0.1 else -1
    elif len(l) == 3:
        i, j, k = max(((0, 1, 2), (0, 2, 1), (1, 2, 0)), key=lambda q: dot(l[q[0]], l[q[1]]))
        c[l[i]] = 1; c[l[j]] = 1; c[l[k]] = -1
    else:
        for i in l: c[i] = 1
    _strony[n] = c; return c
def cur(i, n): return 1 if krawedzie[i]["a"] == n else -1
def orientuj(nadpisane=None):
    nadpisane = nadpisane or {}
    o = {}; sprzeczne = set(); skladowe = []
    for i0 in range(len(krawedzie)):
        if i0 in o: continue
        skladowa = [i0]; o[i0] = 1; kol = collections.deque([i0])
        while kol:
            e = kol.popleft()
            for n in (krawedzie[e]["a"], krawedzie[e]["b"]):
                sd = nadpisane.get(n) or strony(n); de = o[e] * cur(e, n)
                for f in przy[n]:
                    if f == e: continue
                    want = de if sd[f] == sd[e] else -de
                    of = want * cur(f, n)
                    if f not in o: o[f] = of; kol.append(f); skladowa.append(f)
                    elif o[f] != of: sprzeczne.add(n)
        skladowe.append(skladowa)
    return o, sprzeczne, skladowe

# Gdy orientacja jest sprzeczna, zwykle winna jest pojedyncza zwrotnica, której iglicę źle odczytano z rysunku
# (odgałęzienie w tej samej kolumnie, ukos do drugiej zwrotnicy). Szukamy zachłannie innego toru jako iglicy, dopóki ubywa
# sprzeczności, a potem lekko odchylamy pierwszy odcinek nóg (4 px), żeby SUK odczytał z rysunku to samo.
o, sprzeczne, skladowe = orientuj()
wezly_zwr = {wezel_v[v] for v in zwrotnice}
nadp = {}; poprawione_zwr = []
while sprzeczne:
    best = None
    for n in sorted(wezly_zwr):
        if n in nadp or len(przy[n]) != 3: continue
        for tip in przy[n]:
            alt = {i: (-1 if i == tip else 1) for i in przy[n]}
            if alt == strony(n) or {i: -v for i, v in alt.items()} == strony(n): continue
            _, s2, _ = orientuj({**nadp, n: alt})
            if len(s2) < len(sprzeczne) and (best is None or len(s2) < best[0]): best = (len(s2), n, alt, tip)
    if not best: break
    nadp[best[1]] = best[2]; o, sprzeczne, skladowe = orientuj(nadp)
    poprawione_zwr.append(wezly[best[1]]["ref"])
for n, alt in nadp.items():
    tip = [i for i, v in alt.items() if v < 0][0]
    tx = stycz(tip, n)[0]; s = -1 if tx > 0 else 1          # nogi w stronę przeciwną do iglicy
    for k, i in enumerate(i for i in przy[n] if i != tip):
        e = krawedzie[i]; pts = e["pts"] if e["a"] == n else e["pts"][::-1]
        pts.insert(1, [round(pts[0][0] + s * 4, 1), round(pts[0][1] + (2 if k else -2), 1)])
        if e["a"] != n: e["pts"] = pts[::-1]
    _strony.pop(n, None)
if nadp:
    raport.append(f"zwrotnice z poprawioną iglicą (spójność topologii): {poprawione_zwr}")
    o, sprzeczne, skladowe = orientuj()
for skladowa in skladowe:
    suma = sum(o[k] * (wezly[krawedzie[k]["b"]]["x"] - wezly[krawedzie[k]["a"]]["x"]) for k in skladowa)
    if suma < 0:
        for k in skladowa: o[k] = -o[k]
for i, e in enumerate(krawedzie):
    if o[i] < 0: e["a"], e["b"] = e["b"], e["a"]; e["pts"] = e["pts"][::-1]
raport.append(f"orientacja A→B: odwrócono {sum(1 for v in o.values() if v < 0)} torów, węzły sprzeczne (pętle, trójkąty): {len(sprzeczne)}"
              + (f" – {sorted(sprzeczne)[:12]}" if sprzeczne else ""))
# semafor: jawny tor „przed” (ahead) – ten, który od węzła biegnie w kierunku jazdy semafora na rysunku
for s in sygn_out.values():
    n = s["node"]; kier = s["dir"]
    kand = sorted(przy[n], key=lambda i: -stycz(i, n)[0] * kier)
    if not kand: continue
    ah = kand[0]; s["ahead"] = krawedzie[ah]["id"]; s["dir"] = 1 if krawedzie[ah]["a"] == n else -1

# ------------------------------------------------------------------ 5c. szlaki (blokady liniowe pulpitu SimRail)
# Blokada liniowa EBI stoi w tym samym wierszu co semafor wjazdowy. Szlak = łańcuch torów od semafora wjazdowego
# (granica) w stronę od stacji do końca toru (koniec szlaku), bez zwrotnic. SHL – samoczynna blokada liniowa (sbl,
# semafory odstępowe na granicach odcinków szlaku), Eac – samoczynna jednoodstępowa z licznikami osi (sbl, bez semaforów
# odstępowych), Eap – półsamoczynna (pol). Sąsiad i linia – z napisu na pulpicie
# („Rogów LK1”); tor – z numeru blokady (1R → 1). Kierunek zasadniczy: ruch prawostronny – na parze torów górny tor
# rysunku służy jeździe na zachód, dolny na wschód; szlak pojedynczy – wyjazd ze stacji.
TYPY_BLOKAD = {"SHL": "sbl", "Eac": "sbl", "Eap": "pol"}
blocks_out = {}; boundaries_out = {}; farends_out = {}
wjazdowe = {(round((wezly[s["node"]]["x"]) / SX - 0.5), round(wezly[s["node"]]["y"] / SY - 0.5)): k
            for k, s in sygn_out.items() if s["type"] == "wjazdowy"}
def napis_dla(xw, yw):
    best = None
    for p in napisy:
        t = p.get("visibleName") or ""
        if "LK" not in t: continue
        dd = abs(p["position"][1] - 0.5 - yw) * 3 + abs(p["position"][0] - xw) / 20   # napis stoi wiersz pod torem
        if best is None or dd < best[0]: best = (dd, t)
    if not best: return "", ""
    t = best[1].strip(); i = t.find("LK")
    return t[:i].strip(), t[i + 2:].strip()
szlaki_tmp = []
for lb in blokady:
    bx, by = lb["position"]; nm = lb.get("visibleName") or nazwa(lb)
    kand = [(abs(x - bx), k) for (x, y), k in wjazdowe.items() if y == by and abs(x - bx) <= 6]
    if not kand: raport.append(f"szlak {nm}: brak semafora wjazdowego w wierszu {by} – pominięty"); continue
    sig = sygn_out[min(kand)[1]]; n0 = sig["node"]
    poza = [i for i in przy[n0] if krawedzie[i]["id"] != sig.get("ahead")]
    if len(poza) != 1: raport.append(f"szlak {nm}: przy semaforze {sig['label']} {len(poza)} torów na zewnątrz – pominięty"); continue
    # marsz od granicy na zewnątrz
    lancuch = []; n = n0; i = poza[0]; ok = True; odwiedzone = set()
    while True:
        if i in odwiedzone: ok = False; break
        odwiedzone.add(i); e = krawedzie[i]; lancuch.append((i, n)); n = e["b"] if e["a"] == n else e["a"]
        if wezly[n]["kind"] == "switch": ok = False; break
        dalej = [j for j in przy[n] if j != i]
        if not dalej: break
        if len(dalej) > 1: ok = False; break
        i = dalej[0]
    if not ok: raport.append(f"szlak {nm}: za semaforem {sig['label']} jest zwrotnica albo rozgałęzienie – pominięty"); continue
    far = n
    secs = []
    for i, _ in lancuch:
        if not secs or secs[-1] != krawedzie[i]["sec"]: secs.append(krawedzie[i]["sec"])
    w_lewo = wezly[far]["x"] < wezly[n0]["x"]
    nbr, linia = napis_dla(wezly[far]["x"] / SX, wezly[far]["y"] / SY)
    tor = "".join(ch for ch in nm if ch.isdigit()) or "1"
    pierwszy = krawedzie[lancuch[0][0]]
    szlaki_tmp.append({"id": nm, "typ": TYPY_BLOKAD.get(lb.get("lineBlockType"), "sbl"), "lbt": lb.get("lineBlockType"), "sig": sig, "n0": n0, "far": far,
                       "lancuch": lancuch, "secsWE": secs[::-1] if w_lewo else secs, "w_lewo": w_lewo, "nbr": nbr, "linia": linia,
                       "tor": tor, "outward": 1 if pierwszy["a"] == n0 else -1, "y": wezly[n0]["y"]})
# kierunek zasadniczy: pary torów po tej samej stronie stacji, sąsiednie wiersze
for s in szlaki_tmp:
    para = [t for t in szlaki_tmp if t is not s and t["w_lewo"] == s["w_lewo"] and abs(t["y"] - s["y"]) <= 4.5 * SY and t["linia"] == s["linia"]]
    if para: s["defDir"] = -1 if s["y"] < para[0]["y"] else 1
    else: s["defDir"] = -1 if s["w_lewo"] else 1
for s in szlaki_tmp:
    bid = s["id"]
    blocks_out[bid] = {"id": bid, "type": s["typ"], "nbr": s["nbr"] or "sąsiad", "internal": False,
                       "ends": [{"boundary": s["n0"], "outward": s["outward"], "entrySig": s["sig"]["name"], "post": POST}],
                       "far": s["far"], "secsWE": s["secsWE"], "defDir": s["defDir"], "tor": s["tor"], "line": s["linia"], "label": bid}
    boundaries_out[s["n0"]] = {"szlak": bid}
    farends_out[s["far"]] = {"szlak": bid, "nbr": s["nbr"] or "sąsiad"}
    for i, _ in s["lancuch"]:
        e = krawedzie[i]; e["szlak"] = True; e["track"] = s["tor"]; e["line"] = s["linia"]
    sig = s["sig"]; sig["szlak"] = bid
    # semafory odstępowe SBL: para (oba kierunki) na każdej granicy odcinków szlaku
    if s["typ"] == "sbl" and s["lbt"] != "Eac":
        k = 0
        for (i, n), (j, m) in zip(s["lancuch"], s["lancuch"][1:]):
            if krawedzie[i]["sec"] == krawedzie[j]["sec"]: continue
            k += 1
            for d_ in (1, -1):
                nm_ = f"{POST}:{bid}/{k}{'+' if d_ > 0 else '-'}"
                sygn_out[nm_] = {"name": nm_, "node": m, "dir": d_, "type": "odstepowy", "szlak": bid, "label": f"{bid}{k}", "post": POST}
raport.append(f"szlaki: {len(blocks_out)} z {len(blokady)} blokad – " +
              ", ".join(f"{b['id']} ({b['type']}, {len(b['secsWE'])} odc., {b['nbr']} LK{b['line']}, kier. {b['defDir']:+d})" for b in blocks_out.values()))

# ------------------------------------------------------------------ 6. zwrotnice, kozły, końce
sw_out = {}
for v, z in zwrotnice.items():
    nid = wezel_v[v]; vis = z["p"].get("visibleName") or nazwa(z["p"])
    nm = f"{POST}:{vis}"
    sw_out[nm] = {"num": nm, "node": nid, "label": vis, "sec": sec_id(odc(v)), "post": POST, "_simrail": nazwa(z["p"])}
    if z["p"].get("defaultInMinus"): sw_out[nm]["zas"] = "-"
bumpers = {}
stopien = collections.Counter()
for e in krawedzie: stopien[e["a"]] += 1; stopien[e["b"]] += 1
for p in kozly:
    x, y = p["position"]; best = None
    for nid, w in wezly.items():
        if stopien[nid] == 1:
            dd = abs(w["x"] - (x + 0.5) * SX) / SX + abs(w["y"] - (y + 0.5) * SY) / SY
            if dd <= 1.6 and (best is None or dd < best[0]): best = (dd, nid)
    if best: bumpers[best[1]] = {}

# ------------------------------------------------------------------ 7. perony, napisy, strona
# perony: wymiary w pikselach ekranu EBI (komórka 16 × 32 px), pozycja = komórka, wyśrodkowane na wierszu
platf = []
for p in perony:
    w_ = max(SX * 2, p.get("width", 64) / 16 * SX); h_ = max(SY * 0.6, p.get("height", 20) / 32 * SY)   # px EBI → komórki
    platf.append({"name": p.get("visibleName", ""), "x": round(p["position"][0] * SX, 1), "y": round((p["position"][1] + 0.5) * SY - h_ / 2, 1),
                  "w": round(w_, 1), "h": round(h_, 1)})
lab = [{"x": round(p["position"][0] * SX + p.get("leftMargin", 0) / 16 * SX * 0.0, 1), "y": round((p["position"][1] + 0.5) * SY, 1),
        "text": p.get("visibleName", ""), "anchor": "start"} for p in napisy if p.get("visibleName")]
# nastawnia (symbol posterunku, np. „Kl”) – jako napis
for p in nastawnie:
    if p.get("visibleName"):
        lab.append({"x": round((p["position"][0] + 0.5) * SX, 1), "y": round((p["position"][1] + 0.5) * SY, 1), "text": p["visibleName"].strip('"').join("„”"), "anchor": "middle"})
# przejazdy kolejowo-drogowe: pionowa linia przez `height` wierszy, opis = kilometr (SUK: crossings, symbol Y)
przejazdy = [{"name": p.get("visibleName", ""), "kind": "Y", "x": round((p["position"][0] + 0.5) * SX, 1),
              "y0": round(p["position"][1] * SY, 1), "y1": round((p["position"][1] + max(1, p.get("height", 3))) * SY, 1)} for p in przejazdy_ebi]
maxx = max(w["x"] for w in wezly.values()) + SX * 4
maxy = max(w["y"] for w in wezly.values()) + SY * 2

mapa = {}
for k_, (n, stala, poz) in enumerate(proste):
    if not n.startswith("_bez"): mapa[n] = sec_id(n)
for (rot, n) in C.values(): mapa[n] = sec_id(n)
for s in list(sygn_out.values()) + list(wyk_out.values()) + list(sw_out.values()):
    if s.get("_simrail"): mapa[s["_simrail"]] = s.get("name") or s.get("num")
for s in list(sygn_out.values()) + list(wyk_out.values()) + list(sw_out.values()): s.pop("_simrail", None)

# Tory stacyjne (l1_kol_it…): numer toru na pulpicie (widoczna nazwa z EBI, np. „5”; bez niej – „5a”) i oznaczenie toru
# stacyjnego w SUK (sections.tor) – numer w osi toru i okienko numeru pociągu.
widoczne = {}
for (n, _, poz), v in proste.items():
    if v["pola"].get("visibleName"): widoczne.setdefault(n, v["pola"]["visibleName"])
sekcje_out = {}
for e in krawedzie:
    n = e["sec"].split(":", 1)[1]
    pelna = next((k for k in widoczne if sec_id(k) == e["sec"]), None)
    if re.match(r"^it\d", n):
        e["track"] = widoczne.get(pelna) or n[2:]
        sekcje_out[e["sec"]] = {"tor": True}

# Jeden obraz całej stacji – jak pulpit w grze (obie zakładki wirtualne obok siebie)
strony_pulpitu = [{"id": POST, "name": nazwa_post, "x0": -SX * 4, "x1": round(maxx), "posts": [POST]}]

wynik = {
    "name": f"{nazwa_post} (z pulpitu SimRail – projekt do poprawienia)", "code": POST, "orient": "ab", "sections": sekcje_out,
    "width": round(maxx), "height": round(maxy),
    "pages": strony_pulpitu,
    "posts": {POST: nazwa_post},
    "source": f"Konwersja pulpitu SimRail ({d.get('ekran')}, {d.get('czasPc')}) skryptem pulpit_do_suk.py – geometria z siatki EBI, długości umowne.",
    "nodes": wezly, "edges": krawedzie, "switches": sw_out, "signals": {k: v for k, v in sygn_out.items()}, "derailers": wyk_out,
    "blocks": blocks_out, "boundaries": boundaries_out, "farEnds": farends_out, "trackEnds": {}, "platforms": platf, "labels": lab, "crossings": przejazdy,
    "dashed": [], "ports": {}, "routeRules": [], "bumpers": bumpers,
}
json.dump(wynik, open(wyj, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
plik_mapy = re.sub(r"\.json$", "", wyj) + ".simrail.json"
json.dump({"opis": f"Mapa z konwersji pulpitu SimRail ({nazwa_post}) – sprawdź i popraw.", "mapa": dict(sorted(mapa.items()))},
          open(plik_mapy, "w", encoding="utf-8"), ensure_ascii=False, indent=2)

st = collections.Counter(len(s) for s in graf.values())
print(f"posterunek {nazwa_post} ({POST}); węzłów {len(wezly)}, krawędzi {len(krawedzie)}, zwrotnic {len(sw_out)} z {len(J)}, "
      f"semaforów {len(sygn_out)} z {len(sygnaly)}, wykolejnic {len(wyk_out)}, kozłów {len(bumpers)} z {len(kozly)}, odcinków {len({e['sec'] for e in krawedzie})}")
print("stopnie wierzchołków grafu komórek:", dict(st))
print("mapa:", len(mapa), "pozycji →", plik_mapy)
for r in raport: print(" -", r)

# --- diagnostyka: DEBUG_OKNO="x0,x1,y0,y1" drukuje siatkę po sklejeniu (H/V/C<obrót>/J<L|P><G|D>)
import os
if os.environ.get("DEBUG_OKNO"):
    x0_, x1_, y0_, y1_ = map(int, os.environ["DEBUG_OKNO"].split(","))
    print("      " + "".join(f"{x:>7}" for x in range(x0_, x1_ + 1)))
    for yy in range(y0_, y1_ + 1):
        w = []
        for xx in range(x0_, x1_ + 1):
            s = ("H" if (xx, yy) in H else "") + ("V" if (xx, yy) in V else "") + (f"C{C[(xx, yy)][0]}" if (xx, yy) in C else "")
            if (xx, yy) in J: s += "J" + ("L" if J[(xx, yy)].get("isLeft") else "P") + ("G" if J[(xx, yy)].get("isUp") else "D")
            if ("H", xx, yy) in zwrotnice: g = zwrotnice[("H", xx, yy)]["galaz"]; s += f">{g[1]-xx:+d}{g[2]-yy:+d}"
            w.append(s or ".")
        print(f"{yy:>5} " + "".join(f"{s:>7}" for s in w))
if os.environ.get("DEBUG_SAMOTNE"):
    sam = [v for v, s in graf.items() if not s]
    print("samotne komórki:", len(sam))
    print(collections.Counter((v[0], odc(v)[:12]) for v in sam).most_common(15))
if os.environ.get("DEBUG_ZWR"):
    for v, z in sorted(zwrotnice.items()):
        if z["p"].get("visibleName") in os.environ["DEBUG_ZWR"].split(","):
            print(f"{z['p'].get('visibleName'):>6} {v[1:]} isLeft={z['p'].get('isLeft')} isUp={z['p'].get('isUp')} -> {z['galaz']}  t={z['t']} sasiedzi={sorted(graf.get(v, ()))}")
