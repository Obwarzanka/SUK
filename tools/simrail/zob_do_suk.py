"""
Zobrazowanie SUK (*.zob.json, wersja 2) + mapa ID SimRail (*.simrail.json z pulpit_do_zob.py) → układ SUK (station.json)
do PULPITU ZDALNEGO SimRail – bez UT. Topologię i zależności ma gra (jak pulpit EBI w SimRail: rysunek + ID obiektów);
tu powstaje tylko to, czego potrzebuje pulpit SUK: tory obrazu z obwodami, zwrotnice, sygnalizatory, szlaki, perony, napisy.

- Każdy obraz = strona pulpitu (obok siebie). Ten sam obiekt gry na dwóch obrazach: drugi dostaje nazwę z „~2” (SUK łączy je
  z jednym ID gry – stan i polecenia działają na obu).
- Odcinek rysunku → obwód gry z mapy. Odcinek dorysowany/podzielony w edytorze (nowe ID) dostaje obwód odcinka z gry, na którym
  leży (mapa pamięta położenie), albo obwód sąsiadów, gdy po obu końcach jest ten sam; inaczej – własny odcinek SUK (bez stanu z gry).
- Element bez ID gry (narysowany od nowa w edytorze) dostaje obiekt gry po numerze z opisu – ten sam rodzaj i numer co element
  z mapy (np. nowa zwrotnica „70” = z1949). Czego nie da się przypisać – lista w <wynik>.nieprzypisane.txt.
- Mapa dla okna „SimRail” / pulpitu zdalnego: <wynik>.simrail.json (ID gry → obiekt SUK).

- Rejestr posterunków (posterunki.json w katalogu stacji lub wyżej, albo --rejestr): kod, nazwa w grze i posterunki układu
  (pole „uklad” = nazwa pliku zobrazowania bez .zob.json). Bez rejestru – kod i nazwa z mapy (z pulpitu gry).
- Przypięcie „-” w mapie (edytor zobrazowania, zakładka Gra: „bez obiektu gry”) – element/odcinek celowo bez obiektu gry:
  bez podpowiedzi po numerze / położeniu / sąsiadach i bez wpisu w nieprzypisane.txt.
- Stan przypięć do edytora: <zobrazowanie>.przypiecia_gry.json obok zobrazowania (każdy element i odcinek: ID gry, skąd, kandydaci).

Użycie: python -I zob_do_suk.py koluszki.zob.json wynik.json [--zrzuty <katalog UserData/SimRailMost>] [--rejestr posterunki.json]
        python -I zob_do_suk.py koluszki.zob.json --tylko-przypiecia [--zrzuty …]   (tylko stan przypięć, bez pulpitu SUK)
"""
import json, sys, collections, re, os, shutil, csv, datetime

sys.stdout.reconfigure(encoding="utf-8")
def _opcja(n):
    if n in sys.argv:
        i = sys.argv.index(n); v = sys.argv[i + 1] if i + 1 < len(sys.argv) else None; del sys.argv[i:i + 2]; return v
    return None
ZRZUTY = _opcja("--zrzuty"); REJESTR = _opcja("--rejestr")
TYLKO_PRZYP = "--tylko-przypiecia" in sys.argv
ARGS = [a for a in sys.argv[1:] if not a.startswith("--")]
zob_plik = ARGS[0]; wyj = ARGS[1] if len(ARGS) > 1 else None
if not wyj and not TYLKO_PRZYP: raise SystemExit("Użycie: zob_do_suk.py <zobrazowanie.zob.json> <wynik.json> | --tylko-przypiecia")
BEZ = "-"              # przypięcie „bez obiektu gry” (świadomie)
SKALA_X = 1.5          # obraz EBI jest ciasny w poziomie (komórka 16 j.), symbole SUK mają stały rozmiar
ODSTEP = 200           # odstęp między obrazami na pulpicie [j.]
zob = json.load(open(zob_plik, encoding="utf-8"))
mp = zob_plik[:-len(".zob.json")] + ".simrail.json" if zob_plik.endswith(".zob.json") else zob_plik + ".simrail.json"
mapa = json.load(open(mp, encoding="utf-8")) if os.path.exists(mp) else {"obrazy": {}, "kod": "PO", "posterunek": "Posterunek"}
POST = mapa.get("kod") or "PO"; NAZWA = mapa.get("posterunek") or "Posterunek"
raport = []
UKLAD = os.path.basename(zob_plik)[:-len(".zob.json")] if zob_plik.endswith(".zob.json") else os.path.splitext(os.path.basename(zob_plik))[0]
def rejestr():
    """posterunki.json: podany, albo w katalogu zobrazowania i do 3 poziomów wyżej."""
    if REJESTR: return REJESTR if os.path.exists(REJESTR) else None
    kat = os.path.dirname(os.path.abspath(zob_plik))
    for _ in range(4):
        p = os.path.join(kat, "posterunki.json")
        if os.path.exists(p): return p
        kat = os.path.dirname(kat)
    return None
POSTERUNKI_REJ = []
_rp = rejestr()
if _rp:
    _wp = [p for p in json.load(open(_rp, encoding="utf-8")).get("posterunki", []) if (p.get("uklad") or "").lower() == UKLAD.lower()]
    if _wp:
        POST = (_wp[0].get("kod") or POST).upper(); NAZWA = _wp[0].get("nazwaGry") or NAZWA
        POSTERUNKI_REJ = [p["nazwaGry"] for p in _wp if p.get("nazwaGry")]
        if _wp[0].get("skalaX"):          # gęsty pulpit (np. Łazy – cały ekran EBI): 1.0 = proporcje jak w grze
            SKALA_X = float(_wp[0]["skalaX"]); raport.append(f"skala pozioma z rejestru: ×{SKALA_X}")
        raport.append(f"rejestr {os.path.basename(_rp)}: układ {UKLAD} → kod {POST}, posterunki gry {POSTERUNKI_REJ}")
        if len({(p.get("kod") or "").upper() for p in _wp}) > 1:
            raport.append(f"uwaga: układ {UKLAD} ma kilka kodów w rejestrze – pulpit dostaje kod pierwszego ({POST})")
    else: raport.append(f"rejestr {os.path.basename(_rp)}: brak układu {UKLAD} – kod i nazwa z mapy ({POST}, {NAZWA})")

def krotka(n):
    return re.sub(r"^l\d+_[a-z]+_", "", n) if re.match(r"^l\d+_[a-z]+_i[tz]", n) else n
def sec_id(n): return f"{POST}:{krotka(n)}"

TYP_SUK = {("semafor", "wjazdowy"): "wjazdowy", ("semafor", "wjazdowyBezKonca"): "wjazdowy", ("semafor", "manewrowy"): "wyjazdowy",
           ("semafor", "pociagowy"): "wyjazdowy", ("semafor", None): "wyjazdowy", ("tarcza", None): "tm", ("powtarzacz", "sp"): "sp",
           ("powtarzacz", "to"): "to", ("powtarzacz", None): "sp", ("pk", None): "pk"}

wezly, krawedzie, switches, signals, derailers, bumpers = {}, [], {}, {}, {}, {}
platforms, labels, crossings, pages, sekcje = [], [], [], [], {}
strzalki_bl = []                                  # (prefiks obrazu, odcinek obrazu) strzałek blokady liniowej
przerwy, okna_pip, bez_konca = set(), [], []    # węzły przerw z edytora, okna numeru pociągu (PIP), wjazdowe bez trójkąta końca
mapa_suk = {}                      # ID gry → id SUK (pierwsze wystąpienie)
uzyte_nazwy = collections.Counter()
def unikalna(baza, rodzaj):
    """Nazwa w słowniku danego rodzaju (zwrotnice, sygnalizatory, wykolejnice osobno); powtórka = ten sam obiekt na drugim obrazie."""
    uzyte_nazwy[(rodzaj, baza)] += 1
    n = uzyte_nazwy[(rodzaj, baza)]
    return baza if n == 1 else f"{baza}~{n}"

RODZ_EL = {"zwrotnica": "zwr", "semafor": "syg", "tarcza": "syg", "powtarzacz": "syg", "pk": "syg", "wykolejnica": "wyk"}
def opis_el(e): return next((d["tekst"] for d in e.get("opisy", []) if d.get("tekst")), "")
# Mapa pamięta też numery elementów i położenie odcinków z gry („numery”, „geom”) – po usunięciu/przerysowaniu w edytorze
# (nowe ID) przypisanie odtwarza się po numerze i po położeniu. Starsza mapa bez nich: uzupełnienie raz, z bieżącego rysunku.
uzup = False
for o in zob.get("obrazy", []):
    mo = mapa.get("obrazy", {}).get(o["id"])
    if not mo or ("numery" in mo and "geom" in mo): continue
    Wp = {w["id"]: w["p"] for w in o.get("wezly", [])}
    nr = collections.defaultdict(set)
    for e in o.get("elementy", []):
        if e["typ"] in RODZ_EL and opis_el(e) and mo.get("elementy", {}).get(e["id"]): nr[f"{RODZ_EL[e['typ']]}:{opis_el(e)}"].add(mo["elementy"][e["id"]])
    mo["numery"] = {k: sorted(v) for k, v in sorted(nr.items())}
    mo["geom"] = {s["id"]: Wp[s["a"]] + Wp[s["b"]] for s in o.get("odcinki", []) if mo.get("odcinki", {}).get(s["id"])}
    uzup = True
if uzup and os.path.exists(mp):
    json.dump(mapa, open(mp, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    raport.append(f"mapa uzupełniona o numery i położenie odcinków: {mp}")
# numer → ID gry (wszystkie obrazy); numer niejednoznaczny – bez podpowiedzi
po_numerze = collections.defaultdict(set)
for mo in mapa.get("obrazy", {}).values():
    for k, v in mo.get("numery", {}).items(): po_numerze[tuple(k.split(":", 1))].update(v if isinstance(v, list) else [v])
def na_odcinku(p, g, tol=2.5):
    """Punkt p leży na odcinku g=[x1,y1,x2,y2] (z tolerancją)."""
    x1, y1, x2, y2 = g; dx, dy = x2 - x1, y2 - y1; l2 = dx * dx + dy * dy
    if l2 == 0: return abs(p[0] - x1) <= tol and abs(p[1] - y1) <= tol
    t = ((p[0] - x1) * dx + (p[1] - y1) * dy) / l2
    if t < -0.01 or t > 1.01: return False
    return abs((p[0] - x1) * dy - (p[1] - y1) * dx) / l2 ** 0.5 <= tol
nieprzypisane, przyp_numer, przyp_obwod = [], [], 0
przypiecia_gry, nieumieszczone, uzyte_gry = {}, set(), set()

def katalogi_szukania():
    """Gdzie szukać danych z gry (zrzuty pulpitu, obiekty_serwer_*.csv, przebiegi_gry.json): --zrzuty, potem katalog
    zobrazowania i do 4 poziomów wyżej (stacje/<układ>/edytor → UserData/SimRailMost)."""
    k = [ZRZUTY] if ZRZUTY else []
    kat = os.path.dirname(os.path.abspath(zob_plik))
    for _ in range(5): k.append(kat); kat = os.path.dirname(kat)
    return [x for i, x in enumerate(k) if x and os.path.isdir(x) and x not in k[:i]]
def zrzuty_pulpitu():
    """Zrzuty pulpitu gry (pulpit_<posterunek>_*.json z moda) – katalogi_szukania(), najnowszy pierwszy."""
    zrzuty = []
    # mod zamienia w nazwie pliku znaki inne niż litery i cyfry na „_” (Łódź Widzew → pulpit_Łódź_Widzew_…)
    nazwy = {f"pulpit_{n}_" for nz in [NAZWA] + POSTERUNKI_REJ for n in (nz, re.sub(r"[^\w]", "_", nz))}
    for k in katalogi_szukania():
        if not os.path.isdir(k): continue
        zrzuty += [os.path.join(k, f) for f in os.listdir(k) if any(f.startswith(n) for n in nazwy) and f.endswith(".json")]
    return sorted(set(zrzuty), key=os.path.getmtime, reverse=True)
# Tor fizyczny → obwód. Nowsze zrzuty mają w railNetworkName elementu „tor;obwód” (np. „t40367;l1_kol_iz153”), starsze –
# sam tor („t40367”). Mod podaje zajętość obu, ale tor fizyczny jest zajęty tylko pod pociągiem, a pulpit EBI barwi cały
# obwód – mapa z samymi torami pokazywała więc zajętość przesuniętą względem gry. Odcinek dostaje obwód; tor leżący
# na dwóch obwodach – ten, na którym ma więcej komórek pulpitu (jak w pulpit_do_zob.py).
def tory_na_obwody():
    for z in zrzuty_pulpitu():
        try: el = json.load(open(z, encoding="utf-8")).get("elementy", [])
        except (OSError, ValueError): continue
        licz = collections.defaultdict(collections.Counter)
        for e in el:
            n = (e.get("pola") or {}).get("railNetworkName") or ""
            if ";" not in n: continue
            tor, obw = n.split(";", 1)
            licz[tor.strip()][obw.strip()] += len(e["pola"].get("cells") or []) or 1
        if licz:
            raport.append(f"obwody torów z zrzutu {os.path.basename(z)}: {len(licz)} torów")
            return {t: c.most_common(1)[0][0] for t, c in licz.items()}
    return {}
tor_obwod = tory_na_obwody()
# 2026-11-22: tor, który gra podaje jako własny element pulpitu (EBI300TrackRenderer w obiekty_serwer_*.csv), zostaje przy
# swoim stanie. Pulpit gry barwi każdy kawałek toru osobno: obwód zajęty zapala tylko kawałki na ułożonej gałęzi
# (zajętości_serwer: pociąg przez 9 na bok – iz9/13 i t39974 zajęte, t39928 13→C102 wolny). Przepięcie na obwód
# barwiło na SUK tory poza drogą pociągu (13–C102 czerwony przy jeździe 15→9→8).
def tory_z_wlasnym_stanem():
    for z in zrzuty_pulpitu():
        kat = os.path.dirname(z)
        csvy = sorted((f for f in os.listdir(kat) if f.startswith("obiekty_serwer_") and f.endswith(".csv")), reverse=True)
        for f in csvy:
            try:
                with open(os.path.join(kat, f), encoding="utf-8-sig") as p:
                    w = {r["obiekt"] for r in csv.DictReader(p, delimiter=";")
                         if r.get("stacja") in [NAZWA] + POSTERUNKI_REJ and r.get("klasa") == "EBI300TrackRenderer"}
            except (OSError, KeyError, csv.Error): continue
            if w:
                raport.append(f"tory z własnym stanem w grze (bez przepinania na obwód): {len(w)} z {f}")
                return w
        break
    return set()
_wlasne = tory_z_wlasnym_stanem()
tor_obwod = {t: o for t, o in tor_obwod.items() if t not in _wlasne}
def obwod_gry(n):
    """Nazwa obwodu gry dla nazwy z mapy: „tor;obwód” → obwód, sam tor → jego obwód ze zrzutu, inaczej bez zmian."""
    if not n: return n
    if ";" in n:
        # tor z własnym stanem w grze zostaje sobą (jak sam tor niżej) – inaczej SUK barwi cały obwód, a nie gałąź drogi
        tor, obw = (x.strip() for x in n.split(";", 1))
        return tor if tor in _wlasne else obw
    return tor_obwod.get(n, n)
na_obwod = 0

x_off = 0
for oi, o in enumerate(zob.get("obrazy", [])):
    if not o.get("wezly"): continue
    m0 = mapa.get("obrazy", {}).get(o["id"], {})
    m = {"elementy": dict(m0.get("elementy", {})), "odcinki": dict(m0.get("odcinki", {}))}
    on = o.get("nazwa") or f"obraz {oi + 1}"
    # stan przypięć do edytora (zakładka Gra): skąd ID gry, kandydaci
    st_el, st_odc = {}, {}
    przypiecia_gry[o["id"]] = {"elementy": st_el, "odcinki": st_odc}
    # „-” – świadomie bez obiektu gry: bez podpowiedzi, bez wpisu w nieprzypisanych
    bez_el = {k for k, v in m["elementy"].items() if v == BEZ}; bez_odc = {k for k, v in m["odcinki"].items() if v == BEZ}
    for k in bez_el: del m["elementy"][k]
    for k in bez_odc: del m["odcinki"][k]
    ids_el = {e["id"] for e in o["elementy"]}; ids_odc = {s["id"] for s in o["odcinki"]}
    for eid, g in m["elementy"].items():
        if eid in ids_el: st_el[eid] = {"gra": g, "stan": "mapa"}
    for sid, g in m["odcinki"].items():
        if sid in ids_odc: st_odc[sid] = {"gra": g, "stan": "mapa"}
    for k in bez_el & ids_el: st_el[k] = {"gra": None, "stan": "bez"}
    for k in bez_odc & ids_odc: st_odc[k] = {"gra": None, "stan": "bez"}
    # elementy bez ID gry → po numerze
    for e in o["elementy"]:
        if e["typ"] not in RODZ_EL or m["elementy"].get(e["id"]) or e["id"] in bez_el: continue
        nr = opis_el(e); kand = po_numerze.get((RODZ_EL[e["typ"]], nr), set())
        if len(kand) == 1:
            m["elementy"][e["id"]] = next(iter(kand)); przyp_numer.append(f"{on}: {e['typ']} {nr} → {m['elementy'][e['id']]}")
            st_el[e["id"]] = {"gra": m["elementy"][e["id"]], "stan": "numer"}
        else:
            nieprzypisane.append(f"{on}: {e['typ']} „{nr or '(bez numeru)'}” (id {e['id']})" +
                                 (f" – numer niejednoznaczny: {sorted(kand)}" if kand else " – brak obiektu gry o tym numerze"))
            st_el[e["id"]] = {"gra": None, "stan": "niejednoznaczny" if kand else "brak", "kandydaci": sorted(kand)}
    # odcinki bez obwodu → 1) leży na odcinku z gry (podział, przerysowanie w tym samym miejscu)
    Wp = {w["id"]: w["p"] for w in o["wezly"]}
    for s in o["odcinki"]:
        if m["odcinki"].get(s["id"]) or s["id"] in bez_odc: continue
        for sid, g in m0.get("geom", {}).items():
            if m["odcinki"].get(sid) and na_odcinku(Wp[s["a"]], g) and na_odcinku(Wp[s["b"]], g):
                m["odcinki"][s["id"]] = m["odcinki"][sid]; przyp_obwod += 1
                st_odc[s["id"]] = {"gra": m["odcinki"][sid], "stan": "polozenie"}; break
    # 2) obwód sąsiadów (ten sam po obu końcach), powtarzane dla łańcuchów nowych odcinków
    przy_w = collections.defaultdict(list)
    for s in o["odcinki"]: przy_w[s["a"]].append(s["id"]); przy_w[s["b"]].append(s["id"])
    zmiana = True
    while zmiana:
        zmiana = False
        for s in o["odcinki"]:
            if m["odcinki"].get(s["id"]) or s["id"] in bez_odc: continue
            obw_a = {m["odcinki"][x] for x in przy_w[s["a"]] if x != s["id"] and m["odcinki"].get(x)}
            obw_b = {m["odcinki"][x] for x in przy_w[s["b"]] if x != s["id"] and m["odcinki"].get(x)}
            wsp = obw_a & obw_b
            if len(wsp) == 1:
                m["odcinki"][s["id"]] = next(iter(wsp)); przyp_obwod += 1; zmiana = True
                st_odc[s["id"]] = {"gra": m["odcinki"][s["id"]], "stan": "sasiedzi"}
    for s in o["odcinki"]:
        if not m["odcinki"].get(s["id"]) and s["id"] not in bez_odc:
            nieprzypisane.append(f"{on}: odcinek toru {s['id']} {Wp[s['a']]}–{Wp[s['b']]} – bez obwodu gry (bez zajętości)")
            # kandydaci: obwody sąsiednich odcinków (po jednym końcu)
            kand = sorted({m["odcinki"][x] for w_ in (s["a"], s["b"]) for x in przy_w[w_] if x != s["id"] and m["odcinki"].get(x)})
            st_odc[s["id"]] = {"gra": None, "stan": "brak", "kandydaci": kand}
    # obiekty gry z mapy (pulpit gry), których nie ma już na rysunku – usunięte w edytorze
    uzyte_gry.update(m["elementy"].values()); uzyte_gry.update(m["odcinki"].values())
    for k, g in list(m0.get("elementy", {}).items()) + list(m0.get("odcinki", {}).items()):
        if g and g != BEZ and k not in ids_el and k not in ids_odc: nieumieszczone.add(g)
    pref = f"{POST}:o{oi + 1}_"
    def P(p): return (round(p[0] * SKALA_X + x_off, 1), float(p[1]))
    W = {w["id"]: w for w in o["wezly"]}
    for w in o["wezly"]: wezly[pref + w["id"]] = {"x": P(w["p"])[0], "y": P(w["p"])[1], "kind": "", "ref": "", "post": POST}
    # rozciągnięcie ×SKALA_X psuje krótkie ukosy 45° (końcówki łączników „]”, narożniki): 8×8 → 12×8, końcówka wystaje
    # poza przerwę rozjazdu („∫” zamiast „]”). Węzeł po stronie poziomej przesuwa się tak, żeby ukos znów był 45° –
    # pion zostaje na swoim miejscu, rozjazd / początek narożnika przesuwa się wzdłuż toru o kilka j.
    przy_o = collections.defaultdict(list)
    for s in o["odcinki"]: przy_o[s["a"]].append(s); przy_o[s["b"]].append(s)
    def drugi_w(s, n): return s["b"] if s["a"] == n else s["a"]
    for s in o["odcinki"]:
        (ax, ay), (bx, by) = Wp[s["a"]], Wp[s["b"]]
        if not (ax != bx and abs(bx - ax) == abs(by - ay) <= 16): continue
        for u, v in ((s["a"], s["b"]), (s["b"], s["a"])):
            ux, uy = Wp[u]; vx, vy = Wp[v]
            pion_u = any(Wp[drugi_w(q, u)][0] == ux and Wp[drugi_w(q, u)][1] != uy for q in przy_o[u] if q is not s)
            poziom_v = all(Wp[drugi_w(q, v)][1] == vy for q in przy_o[v] if q is not s)
            if pion_u and poziom_v:
                wezly[pref + v]["x"] = round(wezly[pref + u]["x"] + (vx - ux), 1); break
    # elementy na odcinkach – podział odcinka w miejscach symboli (sygnalizator, wykolejnica = węzeł toru)
    na_odc = collections.defaultdict(list)
    for e in o["elementy"]:
        if e.get("na") and e["typ"] in ("semafor", "tarcza", "powtarzacz", "pk", "wykolejnica", "przerwa"): na_odc[e["na"]].append(e)
    el_wezel = {}
    for s in o["odcinki"]:
        obw0 = m["odcinki"].get(s["id"]); obw = obwod_gry(obw0)
        if obw != (obw0 or "").split(";", 1)[0].strip(): na_obwod += 1
        sec = sec_id(obw) if obw else f"{POST}:_{s['id']}"
        if obw: mapa_suk.setdefault(obw, sec)
        a, b = pref + s["a"], pref + s["b"]
        pa, pb = (wezly[a]["x"], wezly[a]["y"]), (wezly[b]["x"], wezly[b]["y"])
        punkty = [(0.0, a)]
        for e in sorted(na_odc.get(s["id"], []), key=lambda e: e.get("t", 0)):
            t = min(max(e.get("t", 0), 0.0), 1.0)
            if t <= 0.001: el_wezel[e["id"]] = (a, s, e); continue
            if t >= 0.999: el_wezel[e["id"]] = (b, s, e); continue
            nidw = f"{pref}e{e['id']}"
            wezly[nidw] = {"x": round(pa[0] + (pb[0] - pa[0]) * t, 1), "y": round(pa[1] + (pb[1] - pa[1]) * t, 1), "kind": "", "ref": "", "post": POST}
            punkty.append((t, nidw)); el_wezel[e["id"]] = (nidw, s, e)
        punkty.append((1.0, b))
        for (t0, u), (t1, v) in zip(punkty, punkty[1:]):
            if u == v: continue
            pu, pv = (wezly[u]["x"], wezly[u]["y"]), (wezly[v]["x"], wezly[v]["y"])
            dl = ((pv[0] - pu[0]) ** 2 + (pv[1] - pu[1]) ** 2) ** 0.5
            krawedzie.append({"a": u, "b": v, "pts": [list(pu), list(pv)], "sec": sec, "len": round(max(5.0, dl / 2), 1), "track": "", "line": "",
                              "id": f"e{len(krawedzie)}", "_seg": s["id"], "_kier": (pv[0] - pu[0], pv[1] - pu[1])})
    # zwrotnice
    for e in o["elementy"]:
        if e["typ"] != "zwrotnica" or not e.get("na"): continue
        lab = next((d["tekst"] for d in e.get("opisy", []) if d.get("tekst")), e["id"])
        num = unikalna(f"{POST}:{lab}", "zwr"); nw = pref + e["na"]
        wezly[nw]["kind"] = "switch"; wezly[nw]["ref"] = lab
        switches[num] = {"num": num, "node": nw, "label": lab, "post": POST}
        if e.get("styl") == "plusBok": switches[num]["zas"] = "-"
        if m["elementy"].get(e["id"]): mapa_suk.setdefault(m["elementy"][e["id"]], num)
    # sygnalizatory, wykolejnice
    for eid, (nw, s, e) in el_wezel.items():
        lab = next((d["tekst"] for d in e.get("opisy", []) if d.get("tekst")), "")
        # przerwa izolacyjna z edytora: pulpit rysuje przerwę w torze tylko tam (lista „gaps”), jak edytor; inne granice obwodów – ciągle
        if e["typ"] == "przerwa": przerwy.add(nw); continue
        if e["typ"] == "wykolejnica":
            nm = unikalna(f"{POST}:{lab or eid}", "wyk"); wezly[nw]["kind"] = "derail"; wezly[nw]["ref"] = lab
            derailers[nm] = {"name": nm, "node": nw, "label": lab, "post": POST}
        else:
            nm = unikalna(f"{POST}:{lab or eid}", "syg")
            typ = TYP_SUK.get((e["typ"], e.get("styl")), TYP_SUK.get((e["typ"], None), "tm"))
            # kierunek na rysunku: kier +1 – od A do B odcinka obrazu
            ax, ay = W[s["a"]]["p"]; bx, by = W[s["b"]]["p"]
            w_prawo = ((bx - ax) >= 0) == (e.get("kier", 1) > 0)
            signals[nm] = {"name": nm, "node": nw, "_w_prawo": w_prawo, "type": typ, "label": lab, "post": POST}
            if e.get("styl") == "pociagowy": signals[nm]["shunt"] = False
            if e.get("styl") == "wjazdowyBezKonca": bez_konca.append(nm)      # bez trójkąta końca przebiegu wyjazdowego (jak w edytorze)
        if m["elementy"].get(eid): mapa_suk.setdefault(m["elementy"][eid], nm)
    for e in o["elementy"]:
        if e["typ"] == "strzalkaBlokady" and e.get("na"): strzalki_bl.append((pref, e["na"], m["elementy"].get(e["id"]) or "", opis_el(e)))
    # kozły, perony, napisy, przejazdy, numery torów
    for e in o["elementy"]:
        t = e["typ"]; tekst = next((d["tekst"] for d in e.get("opisy", []) if d.get("tekst")), "")
        if t == "koziol" and e.get("na") and pref + e["na"] in wezly: bumpers[pref + e["na"]] = {}
        elif t == "peron" and e.get("p"):
            r = e.get("rozm") or [64, 24]
            platforms.append({"name": tekst, "x": P(e["p"])[0], "y": e["p"][1], "w": round(r[0] * SKALA_X, 1), "h": r[1]})
        elif t == "napis" and e.get("p") and tekst:
            labels.append({"x": P(e["p"])[0], "y": e["p"][1], "text": tekst, "anchor": "middle" if tekst.startswith("„") else "start"})
        elif t == "polePip" and e.get("p"):
            r = e.get("rozm") or [88, 68]
            okna_pip.append({"x": P(e["p"])[0], "y": float(e["p"][1]), "w": round(r[0] * SKALA_X, 1), "h": float(r[1])})
        elif t == "przejazd" and e.get("p"):
            r = e.get("rozm") or [0, 64]
            crossings.append({"name": tekst, "kind": "Y", "x": P(e["p"])[0], "y0": e["p"][1] - 16, "y1": e["p"][1] - 16 + r[1]})
        elif t == "numerToru" and e.get("na") and tekst:
            for k in krawedzie:
                if k["_seg"] == e["na"]:
                    k["track"] = tekst; sekcje[k["sec"]] = {"tor": True}
                    for k2 in krawedzie:
                        if k2["sec"] == k["sec"]: k2["track"] = tekst
                    break
    szer = max([w["p"][0] for w in o["wezly"]] + [o.get("szer", 1920)]) * SKALA_X
    pages.append({"id": f"{POST}{oi + 1}", "name": o.get("nazwa") or NAZWA, "x0": round(x_off - 40), "x1": round(x_off + szer + 40), "posts": [POST]})
    x_off += szer + ODSTEP

# ------------------------------------------------ stan przypięć do edytora zobrazowania (zakładka Gra)
nieumieszczone -= uzyte_gry
plik_p = zob_plik[:-len(".zob.json")] + ".przypiecia_gry.json" if zob_plik.endswith(".zob.json") else zob_plik + ".przypiecia_gry.json"
licz = collections.Counter(v["stan"] for o in przypiecia_gry.values() for d_ in o.values() for v in d_.values())
json.dump({"opis": "Stan przypięć elementów i odcinków zobrazowania do obiektów SimRail (zob_do_suk.py). Stan: mapa – z mapy, "
                   "numer – po numerze z opisu, polozenie – leży na odcinku z gry, sasiedzi – obwód sąsiadów, bez – świadomie bez "
                   "obiektu gry, brak / niejednoznaczny – do przypięcia. Plik tylko do odczytu – przypięcia zapisuje się w mapie.",
           "utworzono": datetime.datetime.now().isoformat(timespec="seconds"), "kod": POST, "posterunek": NAZWA,
           "liczby": dict(sorted(licz.items())), "obrazy": przypiecia_gry, "nieumieszczone": sorted(nieumieszczone)},
          open(plik_p, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
if TYLKO_PRZYP:
    print(f"{NAZWA}: przypięcia {dict(sorted(licz.items()))}, nieumieszczone z gry {len(nieumieszczone)} → {plik_p}")
    sys.exit(0)

# ------------------------------------------------ orientacja torów A→B (jak w pulpit_do_suk.py: strony węzła, naprawa iglic)
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
ciecia = set()   # węzły, przez które orientacja się nie przenosi (szew toru zawracającego na rysunku)
def orientuj(nadp=None):
    nadp = nadp or {}; o = {}; spr = set(); skl = []
    for i0 in range(len(krawedzie)):
        if i0 in o: continue
        s_ = [i0]; o[i0] = 1; kol = collections.deque([i0])
        while kol:
            e = kol.popleft()
            for n in (krawedzie[e]["a"], krawedzie[e]["b"]):
                if n in ciecia: continue
                sd = nadp.get(n) or strony(n); de = o[e] * cur(e, n)
                for f in przy[n]:
                    if f == e: continue
                    of = (de if sd[f] == sd[e] else -de) * cur(f, n)
                    if f not in o: o[f] = of; kol.append(f); s_.append(f)
                    elif o[f] != of: spr.add(n)
        skl.append(s_)
    return o, spr, skl
# Iglica zwrotnicy z rysunku: para torów na wprost (przeciwne kierunki) i odgałęzienie; gdy odgałęzienie schodzi pionowo,
# idziemy wzdłuż niego, aż skręci w bok – iglica jest po przeciwnej stronie. Nogi dostają lekkie odchylenie (4 px), żeby SUK
# odczytał z geometrii to samo (iglica = tor odosobniony).
def bok_galezi(n, i):
    e = krawedzie[i]; x0 = wezly[n]["x"]; m = e["b"] if e["a"] == n else e["a"]; poprz = i
    for _ in range(40):
        if abs(wezly[m]["x"] - x0) > 0.5: return 1 if wezly[m]["x"] > x0 else -1
        dalej = [j for j in przy[m] if j != poprz]
        if len(dalej) != 1: return 0
        poprz = dalej[0]; e = krawedzie[poprz]; m = e["b"] if e["a"] == m else e["a"]
    return 0
ustalone = 0; z_rysunku = set()          # węzły zwrotnic z iglicą odczytaną z rysunku – naprawa orientacji ich nie rusza
for sw in switches.values():
    n = sw["node"]; l = przy[n]
    if len(l) != 3: continue
    t = {i: stycz(i, n) for i in l}
    pary = [(i, j) for i in l for j in l if i < j and t[i][0] * t[j][0] + t[i][1] * t[j][1] < -0.9]
    if len(pary) != 1: continue
    i, j = pary[0]; galaz = next(k for k in l if k not in (i, j))
    bok = bok_galezi(n, galaz)
    if bok == 0: continue
    tip = i if t[i][0] * bok < 0 else j
    for k_, kk in enumerate(k for k in l if k != tip):
        e = krawedzie[kk]; pts = e["pts"] if e["a"] == n else e["pts"][::-1]
        if abs(pts[1][0] - pts[0][0]) < 0.01:   # tylko odcinek pionowy (bez kierunku w bok) dostaje odchylenie
            # odchylenie niewidoczne na pulpicie (1 j. w bok na 2 j. – liczy się kierunek, nie wielkość)
            pts.insert(1, [round(pts[0][0] + bok * 1, 1), round(pts[0][1] + (2 if pts[1][1] > pts[0][1] else -2), 1)])
            if e["a"] != n: e["pts"] = pts[::-1]
    _strony.pop(n, None); ustalone += 1; z_rysunku.add(n)
raport.append(f"iglice ustalone z rysunku: {ustalone}")
o_, spr, skl = orientuj()
# Naprawa iglic tylko dla zwrotnic, których iglicy nie odczytano z rysunku. Sprzeczność bywa też prawdziwa – tor złożony
# na pulpicie w „[” (Koluszki: 4s → G4, 3s → P1): zawraca na rysunku, a przestawienie iglicy dowolnej zwrotnicy
# (dawniej 153 i 154) tylko przenosi ją w złe miejsce i psuje rysunek. Taka sprzeczność zostaje na torze (SUK ją znosi).
wezly_zwr = {s["node"] for s in switches.values()} - z_rysunku
nadp = {}; popr = []
while spr:
    best = None
    for n in sorted(wezly_zwr):
        if n in nadp or len(przy[n]) != 3: continue
        for tip in przy[n]:
            alt = {i: (-1 if i == tip else 1) for i in przy[n]}
            if alt == strony(n) or {i: -v for i, v in alt.items()} == strony(n): continue
            _, s2, _ = orientuj({**nadp, n: alt})
            if len(s2) < len(spr) and (best is None or len(s2) < best[0]): best = (len(s2), n, alt)
    if not best: break
    nadp[best[1]] = best[2]; o_, spr, skl = orientuj(nadp); popr.append(wezly[best[1]]["ref"])
for n, alt in nadp.items():
    tip = [i for i, v in alt.items() if v < 0][0]; tx = stycz(tip, n)[0]; sgn = -1 if tx > 0 else 1
    for k, i in enumerate(i for i in przy[n] if i != tip):
        e = krawedzie[i]; pts = e["pts"] if e["a"] == n else e["pts"][::-1]
        pts.insert(1, [round(pts[0][0] + sgn * 1, 1), round(pts[0][1] + (0.5 if k else -0.5), 1)])   # niewidoczne na pulpicie
        if e["a"] != n: e["pts"] = pts[::-1]
    _strony.pop(n, None)
if nadp: o_, spr, skl = orientuj()
# Sprzeczność, która została, wypada tam, gdzie zeszły się fronty przeszukiwania – np. w węźle semafora (Koluszki: ISpG4
# na 4s – SUK liczył mu kierunek z krawędzi i rysował odwrotnie). Szew przenosimy na pusty węzeł tego samego toru, najlepiej
# narożnik „[” (tam tor naprawdę zawraca na rysunku): bez semafora, wykolejnicy, kozła i granicy obwodów.
zajete_w = {v["node"] for d_ in (signals, derailers, bumpers) for v in d_.values() if isinstance(v, dict) and v.get("node")}
zajete_w |= {n for n, w in wezly.items() if w.get("kind")} | przerwy
def szew_dla(n0):
    widziane, kol, najl = {n0}, collections.deque([n0]), None
    while kol:
        n = kol.popleft()
        if len(przy[n]) != 2: continue
        i, j = przy[n]
        if krawedzie[i]["sec"] == krawedzie[j]["sec"] and n not in zajete_w:
            ti, tj = stycz(i, n), stycz(j, n)
            if ti[0] * tj[0] + ti[1] * tj[1] > -0.9: return n          # narożnik
            if najl is None: najl = n
        for k in (i, j):
            m = krawedzie[k]["b"] if krawedzie[k]["a"] == n else krawedzie[k]["a"]
            if m not in widziane and len(widziane) < 400: widziane.add(m); kol.append(m)
    return najl
# SUK przy wczytaniu orientuje tory sam (TrackOrientation) i szew wybiera po swojemu – dlatego tor rozcinamy: narożnik
# dostaje drugi węzeł w tym samym punkcie (rysunek bez zmian), pętla kierunków się otwiera, sprzeczności nie ma nigdzie.
rozciete = []
for n in sorted(spr):
    c = szew_dla(n)
    if not c or c in ciecia: continue
    ciecia.add(c); c2 = c + "_r"; wezly[c2] = dict(wezly[c]); j = przy[c][1]
    if krawedzie[j]["a"] == c: krawedzie[j]["a"] = c2
    else: krawedzie[j]["b"] = c2
    przy[c].remove(j); przy[c2].append(j); _strony.pop(c, None); rozciete.append(krawedzie[j]["sec"])
if ciecia:
    ciecia.clear(); o_, spr2, skl = orientuj()
    raport.append(f"tor zawracający na rysunku rozcięty w narożniku: {sorted(set(rozciete))}, zostało sprzecznych węzłów {len(spr2)}")
    spr = spr2
for s_ in skl:
    if sum(o_[k] * (wezly[krawedzie[k]["b"]]["x"] - wezly[krawedzie[k]["a"]]["x"]) for k in s_) < 0:
        for k in s_: o_[k] = -o_[k]
for i, e in enumerate(krawedzie):
    if o_[i] < 0: e["a"], e["b"] = e["b"], e["a"]; e["pts"] = e["pts"][::-1]
raport.append(f"orientacja: węzłów sprzecznych {len(spr)}" + (f", poprawione iglice {popr}" if popr else "")
              + (f" (tor zawraca na rysunku: {sorted({krawedzie[i]['sec'] for n in spr for i in przy[n]})})" if spr else ""))
raport.append(f"odcinki przepięte z toru fizycznego na obwód gry: {na_obwod}")
# sygnalizator: tor przed nim (ahead) – w stronę wskazaną na rysunku
for s in signals.values():
    n = s["node"]; kier = 1 if s.pop("_w_prawo") else -1
    kand = sorted(przy[n], key=lambda i: -stycz(i, n)[0] * kier)
    if kand: ah = kand[0]; s["ahead"] = krawedzie[ah]["id"]; s["dir"] = 1 if krawedzie[ah]["a"] == n else -1
    else: s["dir"] = kier

# ------------------------------------------------ rodzaj blokady liniowej z pulpitu gry
# EBI300LineBlock.lineBlockType: SHL – SBL (samoczynna, semafory odstępowe), Eac – SBL jednoodstępowa z licznikami osi,
# Eap – PBL (półsamoczynna: blok początkowy / końcowy). Nazwa blokady w grze = ID semafora wjazdowego + „bl” (1803_KO_Abl).
# Źródło: mapa („blokady” – pulpit_do_zob.py), a w starszej mapie – najnowszy zrzut pulpitu gry (pulpit_<posterunek>_*.json
# zapisuje mod SimRailMost) w katalogu zobrazowania lub wyżej; odczytane typy dopisuje się do mapy.
TYPY_BLOKAD = {"shl": "sbl", "eac": "sbl", "eap": "pol"}
def blokady_gry():
    if mapa.get("blokady"): return {k.lower(): v for k, v in mapa["blokady"].items()}
    for z in zrzuty_pulpitu():
        try: el = json.load(open(z, encoding="utf-8")).get("elementy", [])
        except (OSError, ValueError): continue
        t = {e["pola"]["railNetworkName"]: e["pola"]["lineBlockType"] for e in el
             if e.get("klasa") == "EBI300LineBlock" and e["pola"].get("railNetworkName") and e["pola"].get("lineBlockType")}
        if not t: continue
        raport.append(f"rodzaje blokad liniowych z zrzutu {os.path.basename(z)}: {len(t)}")
        if os.path.exists(mp):
            mapa["blokady"] = dict(sorted(t.items()))
            json.dump(mapa, open(mp, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
        return {k.lower(): v for k, v in t.items()}
    raport.append("rodzaje blokad liniowych: brak w mapie i brak zrzutu pulpitu gry – wszystkie szlaki jako SBL")
    return {}
typ_bl = blokady_gry()
gra_syg = {}
for g, sk in mapa_suk.items(): gra_syg.setdefault(sk, g)       # SUK → ID gry

# ------------------------------------------------ szlaki: semafor wjazdowy → koniec toru (bez zwrotnic)
blocks, boundaries, farends, blk_pkt = {}, {}, {}, {}
# szlak bez semafora z blokadą w grze (Łazy 224ŁA – blokada t13544kbl): od strzałki blokady w stronę stacji do pierwszego
# sygnalizatora na torze (bez zwrotnic po drodze) – ten sygnalizator zaczyna szlak jak wjazdowy
syg_wezla = {s["node"]: nm for nm, s in signals.items()}
z_strzalek = {}                                   # sygnalizator SUK → blokada gry ze strzałki
nazwa_bl = {bl.lower(): opis for _, _, bl, opis in strzalki_bl if bl and opis}   # blokada gry → nazwa z pulpitu (224ŁA)
for pref_, seg, bl_gry, _ in strzalki_bl:
    ks = [i for i, e in enumerate(krawedzie) if e.get("_seg") == seg and e["a"].startswith(pref_)]
    if not ks: continue
    for start in (krawedzie[ks[0]]["a"], krawedzie[ks[0]]["b"]):
        n, i, odw = start, ks[0], {ks[0]}
        while n not in syg_wezla and wezly[n]["kind"] != "switch":
            dalej = [j for j in przy[n] if j != i and j not in odw]
            if len(dalej) != 1: n = None; break
            i = dalej[0]; odw.add(i); e = krawedzie[i]; n = e["b"] if e["a"] == n else e["a"]
        if n in syg_wezla: z_strzalek.setdefault(syg_wezla[n], (bl_gry, i))   # i – krawędź od strony strzałki
for nm, s in signals.items():
    # semafor wjazdowy albo inny sygnalizator z blokadą liniową w grze (Łazy: P3/P7 typu Stop, Q123/Q125 semafory z manewrową)
    if "~" in nm or s["type"] != "wjazdowy" and (gra_syg.get(nm) or "").lower() + "bl" not in typ_bl and nm not in z_strzalek: continue
    n0 = s["node"]; poza = [i for i in przy[n0] if krawedzie[i]["id"] != s.get("ahead")]
    if nm in z_strzalek and (s["type"] == "pk" or len(poza) != 1): poza = [z_strzalek[nm][1]]        # PK blokady bez semafora (224ŁA): w stronę strzałki
    if len(poza) != 1: continue
    lan = []; n = n0; i = poza[0]; ok = True; odw = set()
    while True:
        if i in odw: ok = False; break
        odw.add(i); e = krawedzie[i]; lan.append(i); n = e["b"] if e["a"] == n else e["a"]
        if wezly[n]["kind"] == "switch": ok = False; break
        dalej = [j for j in przy[n] if j != i]
        if not dalej:
            # przerwa na rysunku tuż za semaforem (Rogów 2P/A: kikut pod strzałką, dalej 946 osobno) – tor szlaku
            # ciągnie się dalej w tej samej linii: mostek do najbliższego wolnego końca w kierunku od semafora
            kx = 1 if wezly[n]["x"] >= wezly[n0]["x"] else -1
            kand = [(abs(w["x"] - wezly[n]["x"]), m) for m, w in wezly.items()
                    if m != n and len(przy.get(m, ())) == 1 and abs(w["y"] - wezly[n]["y"]) <= 1
                    and 0 < (w["x"] - wezly[n]["x"]) * kx <= 120 and not krawedzie[przy[m][0]].get("szlak") and przy[m][0] not in odw]
            if not kand: break
            m = min(kand)[1]; dalsza = krawedzie[przy[m][0]]
            for j in lan:          # kikut bez obwodu gry przed przerwą należy do pierwszego odstępu za nią
                if krawedzie[j]["sec"].startswith(f"{POST}:_"): krawedzie[j]["sec"] = dalsza["sec"]
            pn, pm = [wezly[n]["x"], wezly[n]["y"]], [wezly[m]["x"], wezly[m]["y"]]
            krawedzie.append({"a": n, "b": m, "pts": [pn, pm], "sec": dalsza["sec"], "len": round(max(5.0, abs(pm[0] - pn[0]) / 2), 1),
                              "track": "", "line": "", "id": f"e{len(krawedzie)}", "_seg": dalsza.get("_seg"), "_kier": (pm[0] - pn[0], 0.0)})
            j = len(krawedzie) - 1; przy[n].append(j); przy[m].append(j)
            raport.append(f"szlak sz{s['label']}: mostek przez przerwę na rysunku {pn} → {pm} ({dalsza['sec']})")
            dalej = [j]
        if len(dalej) > 1: ok = False; break
        i = dalej[0]
    if not ok: continue
    secs = []
    for i in lan:
        if not secs or secs[-1] != krawedzie[i]["sec"]: secs.append(krawedzie[i]["sec"])
    w_lewo = wezly[n]["x"] < wezly[n0]["x"]
    bid = f"sz{s['label']}"
    lbt = typ_bl.get((gra_syg.get(nm) or "").lower() + "bl") or typ_bl.get(z_strzalek.get(nm, ("",))[0].lower())
    typ = TYPY_BLOKAD.get((lbt or "").lower(), "sbl")
    if typ_bl and not lbt: raport.append(f"szlak {bid}: brak blokady {gra_syg.get(nm)}bl w grze – SBL")
    elif lbt and lbt.lower() not in TYPY_BLOKAD: raport.append(f"szlak {bid}: nieznany rodzaj blokady {lbt} – SBL")
    nazwa_szl = nazwa_bl.get((gra_syg.get(nm) or "").lower() + "bl") or nazwa_bl.get(z_strzalek.get(nm, ("",))[0].lower())
    blocks[bid] = {"id": bid, "type": typ, "nbr": "sąsiad", "internal": False,
                   "ends": [{"boundary": n0, "outward": 1 if krawedzie[lan[0]]["a"] == n0 else -1, "entrySig": nm, "post": POST,
                             **({"label": nazwa_szl} if nazwa_szl else {})}],
                   "far": n, "secsWE": secs[::-1] if w_lewo else secs, "defDir": -1 if w_lewo else 1, "tor": "1", "line": "", "label": bid}
    boundaries[n0] = {"szlak": bid}; farends[n] = {"szlak": bid, "nbr": "sąsiad"}
    for i in lan: krawedzie[i]["szlak"] = True
    s["szlak"] = bid
    blk_pkt[bid] = [p for i in lan for p in krawedzie[i]["pts"]]

# dalsze odstępy szlaku: pulpit EBI w grze rysuje je jako osobne kreski w kolumnie przy końcu szlaku (Koluszki: x 12–60, nad A i pod B),
# niepołączone z torem. Kreska (tor bez sąsiadów) należy do szlaku o najbliższym końcu (far) – jej obwód dochodzi do odstępów szlaku
# (zajętość szlaku, okno numeru pociągu PIP); najbliższa końcowi toru = pierwszy odstęp za nim
kreski = collections.defaultdict(list)
for e in krawedzie:
    if len(przy[e["a"]]) != 1 or len(przy[e["b"]]) != 1 or e.get("szlak"): continue
    sx, sy = (e["pts"][0][0] + e["pts"][-1][0]) / 2, (e["pts"][0][1] + e["pts"][-1][1]) / 2
    odl = [((wezly[b["far"]]["x"] - sx) ** 2 + (wezly[b["far"]]["y"] - sy) ** 2, bid) for bid, b in blocks.items()]
    if odl and min(odl)[0] <= 200 ** 2: kreski[min(odl)[1]].append((min(odl)[0], e["sec"]))
for bid, kk in kreski.items():
    dalej = [s for _, s in sorted(kk) if s not in blocks[bid]["secsWE"]]
    b = blocks[bid]; b["secsWE"] = (dalej[::-1] + b["secsWE"]) if b["defDir"] < 0 else (b["secsWE"] + dalej)
    raport.append(f"szlak {bid}: dalsze odstępy z kresek pulpitu gry {dalej}")

# granice odstępów SBL na szlaku: przerwa w torze tam, gdzie zmienia się obwód (jak w EBI gry), także bez przerw z edytora.
# Na stacji przerwy nadal tylko z edytora (uwaga właściciela 2026-11-21: odstępy szlaku do Rokicin i Gałkówka zlewały się w jeden).
przerwy_szlak = set()
for n, l in przy.items():
    sz = [krawedzie[i] for i in l if krawedzie[i].get("szlak")]
    if len(l) == 2 and len(sz) >= 1 and len({krawedzie[i]["sec"] for i in l}) == 2 and not wezly[n].get("kind"): przerwy_szlak.add(n)
przerwy |= przerwy_szlak
raport.append(f"granice odstępów na szlakach (przerwy automatyczne): {len(przerwy_szlak)}")

# okna numeru pociągu (PIP): szlak najbliższy środkowi okna na rysunku (jak przypinanie w edytorze – do szlaku obok)
train_fields = []
for ok in okna_pip:
    cx, cy = ok["x"] + ok["w"] / 2, ok["y"] + ok["h"] / 2
    odl = [(min((p[0] - cx) ** 2 + (p[1] - cy) ** 2 for p in pk), bid) for bid, pk in blk_pkt.items() if pk]
    if not odl: nieprzypisane.append(f"okno numeru pociągu ({ok['x']:.0f}, {ok['y']:.0f}) – brak szlaku na pulpicie"); continue
    train_fields.append({"block": min(odl)[1], **ok})
raport.append(f"przerwy z edytora: {len(przerwy - przerwy_szlak)}, okna numeru pociągu: {len(train_fields)} z {len(okna_pip)}")

# ------------------------------------------------ katalog przebiegów gry obok wyniku
# Przebiegi, których SUK sam nie wyliczy (tor złożony na rysunku w „[” jest rozcięty w narożniku: R1 → S3, U1 → P1 przez tor 3s),
# pulpit zdalny dopisuje z katalogu gry (przebiegi_gry.json – mod SimRailMost, polecenie RouteCatalog). Ostatni katalog z katalogu
# zobrazowania lub do 3 poziomów wyżej kopiujemy jako <wynik>.przebiegi_gry.json – pulpit ma te przebiegi od razu, także gdy gra jest w menu.
def katalog_gry():
    for k in katalogi_szukania():
        p = os.path.join(k, "przebiegi_gry.json")
        if os.path.exists(p): return p
    return None
kg = katalog_gry(); plik_kg = re.sub(r"\.json$", "", wyj) + ".przebiegi_gry.json"
if kg and (not os.path.exists(plik_kg) or os.path.getmtime(kg) > os.path.getmtime(plik_kg)):
    shutil.copyfile(kg, plik_kg)
    raport.append(f"katalog przebiegów gry skopiowany: {kg} → {plik_kg}")
elif not kg: raport.append("katalog przebiegów gry (przebiegi_gry.json): brak – pulpit zdalny pobierze go z gry")

for e in krawedzie: e.pop("_seg", None); e.pop("_kier", None)
maxx = max(w["x"] for w in wezly.values()) + 40; maxy = max(w["y"] for w in wezly.values()) + 60
wynik = {"name": f"{NAZWA} (zobrazowanie z SimRail – pulpit zdalny)", "code": POST, "orient": "ab", "sections": sekcje,
         "width": round(maxx), "height": round(maxy), "pages": pages, "posts": {POST: NAZWA},
         "source": f"zob_do_suk.py z {os.path.basename(zob_plik)} – rysunek z edytora zobrazowania, topologia i zależności w grze",
         "nodes": wezly, "edges": krawedzie, "switches": switches, "signals": signals, "derailers": derailers,
         "blocks": blocks, "boundaries": boundaries, "farEnds": farends, "trackEnds": {}, "platforms": platforms, "labels": labels,
         "crossings": crossings, "dashed": [], "ports": {}, "routeRules": [], "bumpers": bumpers,
         # jak na obrazie z edytora: przerwy w torze tylko w tych węzłach (i pod sygnalizatorami), okna PIP, wjazdowe bez trójkąta końca
         "gaps": sorted(przerwy & set(wezly)), "trainFields": train_fields, "noEndMark": bez_konca}
# tor bez obwodu w grze (pusty railNetworkName – gra rysuje go kreskami, Łazy 301→301k, 347k, tory 355/356…): linia przerywana
for e in krawedzie:
    if e["sec"].startswith(f"{POST}:_"): e["noDetect"] = True
json.dump(wynik, open(wyj, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
plik_m = re.sub(r"\.json$", "", wyj) + ".simrail.json"
# posterunki gry przypisane do układu (okno „SimRail” w SUK): zachować z poprzedniej mapy, inaczej – posterunek ze zrzutu pulpitu
posterunki = [NAZWA] if mapa.get("posterunek") else []
if POSTERUNKI_REJ: posterunki = POSTERUNKI_REJ        # rejestr posterunków ma pierwszeństwo
elif os.path.exists(plik_m):
    try: posterunki = json.load(open(plik_m, encoding="utf-8")).get("posterunki") or posterunki
    except (OSError, ValueError): pass
json.dump({"opis": f"Mapa ID gry → SUK z zobrazowania {os.path.basename(zob_plik)}.", "posterunki": posterunki,
           "mapa": dict(sorted(mapa_suk.items()))},
          open(plik_m, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
print(f"{NAZWA}: obrazów {len(pages)}, węzłów {len(wezly)}, torów {len(krawedzie)}, zwrotnic {len(switches)}, sygnalizatorów {len(signals)}, "
      f"szlaków {len(blocks)}, mapa {len(mapa_suk)} ID gry → {plik_m}")
raport.append(f"przypisane po numerze: {len(przyp_numer)}, obwód z położenia/sąsiadów: {przyp_obwod}, nieprzypisane: {len(nieprzypisane)}")
for r in przyp_numer: raport.append("  po numerze: " + r)
plik_n = re.sub(r"\.json$", "", wyj) + ".nieprzypisane.txt"
open(plik_n, "w", encoding="utf-8").write(
    "Elementy zobrazowania bez obiektu gry (nie będą sterowane/pokazywane z SimRail):\n" + "".join(f"{x}\n" for x in nieprzypisane))
for r in raport: print(" -", r)
if nieprzypisane: print(f" - lista nieprzypisanych: {plik_n}")
