"""
Eksport zrzutu pulpitu SimRail (pulpit_*.json z moda SimRailMost) do ZOBRAZOWANIA SUK w wersji 2 (*.zob.json) – do otwarcia
i dalszej pracy w Edytorze zobrazowania (EdytorZobrazowania2.exe).

- Każda zakładka wirtualna pulpitu EBI = osobny obraz 1920 × 1080 j. (1 j. = 1 px ekranu gry; komórka EBI = 16 × 32 j.).
- Rysunek: węzły (rozjazdy, końce, załamania łączników) i odcinki toru; narożniki i wejścia łączników pod 45°.
- Elementy: zwrotnica (numer), semafor / tarcza / powtarzacz / PK (nazwa, kierunek), wykolejnica, kozioł, peron, przejazd,
  numer toru, przerwa izolacyjna (granica obwodów), strzałka blokady, napisy (sąsiedzi, nastawnia).
- Przypięcia do UT – puste: UT (geometrię w PUWG 1992) buduje się w Edytorze UT, potem „Przypnij do UT” (F8) w edytorze zobrazowania.

Rysunek pochodzi z gry SimRail – tylko do własnego użytku, nie rozpowszechniać.
Użycie: python -I pulpit_do_zob.py pulpit_….json wynik.zob.json [plik_ut.ut.json]
"""
import json, sys, collections, uuid

sys.stdout.reconfigure(encoding="utf-8")
CX, CY = 16, 32
ARGS = [a for a in sys.argv[1:] if not a.startswith("--")]
wej, wyj = ARGS[0], ARGS[1]
UT = ARGS[2] if len(ARGS) > 2 else "koluszki.ut.json"   # ścieżka względem pliku zobrazowania
d = json.load(open(wej, encoding="utf-8"))
raport = []

def nid(): return uuid.uuid4().hex[:8]
def nazwa(p): return p.get("railNetworkName") or ""

TYPY_SYG = {"MainSignal": ("semafor", "wjazdowy"), "Default": ("semafor", "pociagowy"), "DefaultWithShuntingSignal": ("semafor", "manewrowy"),
            "ShuntingSignal": ("tarcza", None), "RepeaterSignal": ("powtarzacz", "sp"), "NeighborhoodSignalRepeater": ("powtarzacz", "sp"),
            "DistanceSignal": ("powtarzacz", "to"), "ShuntingEndPoint": ("pk", None), "RouteEndPoint": ("pk", None),
            "TrainEndPoint": ("pk", None)}      # koniec przebiegu pociągowego (Łazy 320k/322k – „▯” w grze)
PORTY = {0: "SE", 1: "SW", 2: "NW", 3: "NE"}
DX = {"E": (1, 0), "W": (-1, 0), "N": (0, -1), "S": (0, 1)}
PRZ = {"E": "W", "W": "E", "N": "S", "S": "N"}


def obraz_z_zakladki(el, nazwa_obrazu):
    # ------------------------------------------------ komórki
    H, V, C, J = {}, {}, {}, {}
    sygnaly, wyk, kozly, perony, napisy, przejazdy, blokady, nastawnie, numery, pipy = [], [], [], [], [], [], [], [], [], []
    for k, p in el:
        pos = tuple(p.get("position") or (0, 0))
        if k == "EBI300StraightTrack" and p.get("cells"):
            n = nazwa(p) or "_bez_" + nid()
            for c in p["cells"]:
                (H if p.get("isHorizontal", True) else V).setdefault(tuple(c), n)
            if p.get("isHorizontal", True) and p.get("visibleName"): numery.append(p)
        elif k == "EBI300CornerTrack": C[pos] = (p.get("rotation", 0) % 4, nazwa(p))
        elif k == "EBI300HorizontalJunction": J[pos] = p
        elif k == "EBI300CrossingTrack": H.setdefault(pos, nazwa(p))
        elif k == "EBI300Signal": sygnaly.append(p)
        elif k == "EBI300Derailer": wyk.append(p)
        elif k == "EBI300StopGuard": kozly.append(p)
        elif k == "EBI300Platform": perony.append(p)
        elif k == "EBI300RichText": napisy.append(p)
        elif k == "EBI300RoadCrossing": przejazdy.append(p)
        elif k == "EBI300LineBlock": blokady.append(p)
        elif k == "EBI300ControlHouse": nastawnie.append(p)
        elif k == "SRPIP": pipy.append(p)
    for pos, p in J.items(): H.setdefault(pos, nazwa(p))
    # przerwy przy semaforach (sygnał w pustej komórce albo luka obok semafora) – ciągły tor
    for p in sygnaly:
        x, y = p["position"]; kier = 1 if p.get("direction") == "Right" else -1
        if (x, y) not in H:
            for krok in (-kier, kier):
                for i in range(1, 4):
                    if (x + krok * i, y) in H:
                        for j in range(0, i): H.setdefault((x + krok * j, y), H[(x + krok * i, y)])
                        break
        # luka do 4 komórek po obu stronach semafora (np. semafor wjazdowy – strzałka blokady – szlak): tor ciągły
        if (x, y) in H:
            for s in (-1, 1):
                for i in range(2, 6):
                    if (x + s * i, y) in H:
                        luka = [(x + s * j, y) for j in range(1, i)]
                        if all(g not in H and g not in V and g not in C for g in luka):
                            for g in luka: H[g] = H[(x + s * i, y)]
                        break

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
    zwr = {}
    def param(p): return (-1 if p.get("isUp") else 1), (1 if bool(p.get("isLeft")) == bool(p.get("isUp")) else -1)
    def bez_lacznika(x, y, dy): return not any((xb, y + dy) in V or (xb, y + dy) in C for xb in (x - 1, x, x + 1))
    # najpierw pary zgodne kierunkiem (ramię jednej zwrotnicy wskazuje drugą i odwrotnie) – też rozjazd krzyżowy („X”: dwie pary,
    # których ukosy się krzyżują, np. 70–74cd i 73–69ab); potem dawne dobieranie najbliższej
    for (x, y), p in sorted(J.items()):
        if ("H", x, y) in zwr: continue
        dy, t = param(p)
        if not bez_lacznika(x, y, dy): continue
        for ddx in sorted((v for v in range(-6, 7) if v * t > 0), key=abs):
            q = (x + ddx, y + 2 * dy)
            if q in J and ("H", *q) not in zwr and param(J[q]) == (-dy, -t) and bez_lacznika(q[0], q[1], -dy):
                lacz(("H", x, y), ("H", *q)); zwr[("H", x, y)] = p; zwr[("H", *q)] = J[q]; break
    for (x, y), p in sorted(J.items()):
        if ("H", x, y) in zwr: continue
        dy, t = param(p)
        if any((xb, y + dy) in V or (xb, y + dy) in C for xb in (x - 1, x, x + 1)): continue
        for ddx in sorted(range(-4, 5), key=lambda v: (abs(v), -v * t)):
            q = (x + ddx, y + 2 * dy)
            if q in J and ("H", *q) not in zwr and param(J[q])[0] == -dy:
                lacz(("H", x, y), ("H", *q)); zwr[("H", x, y)] = p; zwr[("H", *q)] = J[q]; break
    zajete = set()
    for (x, y), p in sorted(J.items()):
        if ("H", x, y) in zwr: continue
        dy, t = param(p); cel = None
        for xb in (x + t, x, x - t):
            for kand in (("V", xb, y), ("V", xb, y + dy), ("C", xb, y + dy)):
                if kand in graf and kand not in zajete and (kand[0] != "V" or (xb, y + dy) in V or (xb, y + dy) in C):
                    cel = kand; break
            if cel: break
        if cel is None: raport.append(f"{nazwa_obrazu}: zwrotnica {p.get('visibleName')} bez łącznika"); continue
        zajete.add(cel); lacz(("H", x, y), cel); zwr[("H", x, y)] = p
    for v in [v for v, s in graf.items() if not s]: del graf[v]    # samotne komórki (linie podziału)

    def odc(v):
        k, x, y = v
        return H.get((x, y)) if k == "H" else V.get((x, y)) if k == "V" else C[(x, y)][1]
    # ------------------------------------------------ punkty rysunku [j.]
    def srodek(v): return (v[1] * CX + CX // 2, v[2] * CY + CY // 2)
    def punkt(v, poprz, nast):
        """Punkt rysunku wierzchołka w łańcuchu: narożnik i komórka łącznika w wierszu toru – ścięcie 45° (16 j.)."""
        cx, cy = srodek(v)
        sasiedzi = [s for s in (poprz, nast) if s is not None]
        pion = [s for s in sasiedzi if s[0] in "VC" and s[1] == v[1] and s[2] != v[2]]
        if v[0] == "C" or (v[0] == "V" and (v[1], v[2]) in H):
            if pion:
                s = pion[0]; return (cx, cy + (CX if s[2] > v[2] else -CX))
        return (cx, cy)

    def punkty_komorki(v, poprz, nast):
        """Narożnik toru jak w grze (małe zaokrąglenie): poziomo do 8 j. przed środkiem komórki, ukos 45°, pion od 8 j. za nim.
        Pozostałe komórki – jak punkt()."""
        if v[0] != "C" or poprz is None or nast is None: return [punkt(v, poprz, nast)]
        cx, cy = srodek(v)
        def xy(s): return tuple(wezly[s[1]]["p"]) if s[0] == "B" else srodek(s)
        def pionowy(s): return s[0] != "B" and s[1] == v[1] and s[2] != v[2]
        if pionowy(poprz) == pionowy(nast): return [punkt(v, poprz, nast)]
        poz, pio = (nast, poprz) if pionowy(poprz) else (poprz, nast)
        px = xy(poz)[0]; py = xy(pio)[1]
        a = (cx + (8 if px > cx else -8), cy); b = (cx, cy + (8 if py > cy else -8))
        return [a, b] if poz is poprz else [b, a]

    # ------------------------------------------------ węzły: stopień ≠ 2, zwrotnice
    wezly = {}; wez_v = {}
    for v, s in graf.items():
        if len(s) != 2 or v in zwr:
            i = nid(); wez_v[v] = i; wezly[i] = {"id": i, "p": list(srodek(v))}
    odcinki = []; uzyte = set(); kom_odc = {}; lancuchy = {}   # komórka → (łańcuch, indeks punktu)
    obwod_odc = {}; granice = []                                # odcinek rysunku → obwód SimRail; węzły granic obwodów
    def zakoncz(lancuch, sid, koniec, obwod):
        pts = [q for i in range(1, len(lancuch) - 1)
               for q in punkty_komorki(lancuch[i], lancuch[i - 1] if i > 0 else None, lancuch[i + 1] if i + 1 < len(lancuch) else None)]
        pelne0 = [tuple(wezly[sid]["p"])] + pts + [tuple(wezly[koniec]["p"])]
        # ukos zwrotnica–zwrotnica (dwa wiersze toru): pion – 45° – pion, żeby odcinki szły co 45° jak w edytorze
        pelne = [pelne0[0]]
        for (ax, ay), (bx, by) in zip(pelne0, pelne0[1:]):
            dx, dy = bx - ax, by - ay
            if dx and dy and abs(dx) != abs(dy) and abs(dx) < abs(dy):
                reszta = abs(dy) - abs(dx); pol = (reszta // 2) // 8 * 8; sy = 1 if dy > 0 else -1
                pelne += [(ax, ay + sy * pol), (ax + dx, ay + sy * (pol + abs(dx)))]
            pelne.append((bx, by))
        zal = []
        for i in range(1, len(pelne) - 1):
            (ax, ay), (bx, by), (qx, qy) = pelne[i - 1], pelne[i], pelne[i + 1]
            if (bx - ax) * (qy - by) != (by - ay) * (qx - bx): zal.append([bx, by])
        # edytor: odcinek = prosta między dwoma węzłami – każde załamanie jest węzłem
        oid = nid(); punkty = [sid]
        for (bx, by) in zal:
            w = nid(); wezly[w] = {"id": w, "p": [bx, by]}; punkty.append(w)
        punkty.append(koniec)
        kawalki = []
        for u, w in zip(punkty, punkty[1:]):
            if wezly[u]["p"] == wezly[w]["p"]: continue
            kid = nid(); kawalki.append(kid)
            odcinki.append({"id": kid, "a": u, "b": w, "pkt": [], "styl": "ciagly"}); obwod_odc[kid] = obwod
        lancuchy[oid] = {"id": oid, "_lancuch": lancuch, "_pelne": pelne, "_kawalki": kawalki}
        for i, v in enumerate(lancuch):
            if v[0] != "B": kom_odc.setdefault((v[1], v[2], v[0]), (oid, i))
    def obwod_lancucha(lancuch):
        """Obwód odcinka rysunku: najczęstszy wśród komórek łańcucha poza komórkami zwrotnic (łącznik ma własny obwód)."""
        c = collections.Counter(odc(v) for v in lancuch if v[0] != "B" and v not in zwr)
        if not c: c = collections.Counter(odc(v) for v in lancuch if v[0] != "B")
        return c.most_common(1)[0][0] if c else None
    for start, sid in list(wez_v.items()):
        for nast in sorted(graf[start]):
            if frozenset((start, nast)) in uzyte: continue
            lancuch = [start]; sid_cur = sid; poprz, cur = start, nast
            while True:
                uzyte.add(frozenset((poprz, cur)))
                if odc(poprz) != odc(cur) and poprz[0] == "H" and cur[0] == "H" and poprz not in zwr and cur not in zwr:
                    # granica obwodów (złącze / licznik osi): węzeł w połowie między komórkami, osobny odcinek rysunku na obwód
                    (ax, ay), (bx, by) = punkt(poprz, None, None), punkt(cur, None, None)
                    b = nid(); wezly[b] = {"id": b, "p": [(ax + bx) // 2 // 8 * 8, (ay + by) // 2 // 8 * 8]}
                    granice.append((b, poprz, cur))
                    zakoncz(lancuch + [("B", b, 0)], sid_cur, b, obwod_lancucha(lancuch))
                    lancuch = [("B", b, 0)]; sid_cur = b
                lancuch.append(cur)
                if cur in wez_v: koniec = wez_v[cur]; break
                dalej = [n for n in sorted(graf[cur]) if n != poprz]
                if not dalej:
                    koniec = nid(); wez_v[cur] = koniec; wezly[koniec] = {"id": koniec, "p": list(srodek(cur))}; break
                poprz, cur = cur, dalej[0]
            zakoncz(lancuch, sid_cur, koniec, obwod_lancucha(lancuch))

    po_odc = {o["id"]: o for o in odcinki}
    def miejsce(o, x, y, w_prawo=None, warstwa="H"):
        """Kawałek odcinka (prosta) najbliższy środkowi komórki (x, y) łańcucha o: {na, t[, kier]}."""
        qx, qy = srodek((warstwa, x, y)); best = None
        for kid in o["_kawalki"]:
            k = po_odc[kid]; ax, ay = wezly[k["a"]]["p"]; bx, by = wezly[k["b"]]["p"]
            dx, dy = bx - ax, by - ay; l2 = dx * dx + dy * dy
            t = 0.0 if l2 == 0 else max(0.0, min(1.0, ((qx - ax) * dx + (qy - ay) * dy) / l2))
            odl = (ax + t * dx - qx) ** 2 + (ay + t * dy - qy) ** 2
            if best is None or odl < best[0]: best = (odl, kid, t, dx)
        if best is None: return None
        r = {"na": best[1], "t": round(best[2], 4)}
        if w_prawo is not None: r["kier"] = 1 if (best[3] >= 0) == w_prawo else -1
        return r
    def t_na(o, x, y, warstwa="H"):
        m = miejsce(o, x, y, None, warstwa); return m and m["t"]
    def odcinek_komorki(x, y, w="H"):
        r = kom_odc.get((x, y, w))
        return (lancuchy[r[0]], r[1]) if r else (None, None)

    elementy = []
    def el_(typ, **k):
        e = {"id": nid(), "typ": typ, "t": 0, "kier": 1, "opisy": []}; e.update({a: b for a, b in k.items() if b is not None}); elementy.append(e); return e
    # zwrotnice
    for v, p in zwr.items():
        if v not in wez_v: continue
        dy, _ = param(p)
        el_("zwrotnica", _sr=nazwa(p), na=wez_v[v], styl="plusBok" if p.get("defaultInMinus") else None,
            opisy=[{"rola": "numer", "tekst": p.get("visibleName", ""),
                    # przesunięcie opisu z pulpitu gry (Łazy 1073cd −30 / 1071ab +20 – bez niego opisy sąsiednich zwrotnic nachodzą)
                    "d": [-8 + round(int(p.get("objectNamePositionHorizontal") or 0) * 0.8 / 4) * 4, 13 if dy < 0 else -13]}])
    # sygnalizatory
    nsyg = 0
    nazwy_syg = {nazwa(p).lower() for p in sygnaly}
    for p in sygnaly:
        x, y = p["position"]; o, _ = odcinek_komorki(x, y)
        if o is None: raport.append(f"{nazwa_obrazu}: sygnalizator {p.get('visibleName')} poza torem"); continue
        typ, styl = TYPY_SYG.get(p.get("signalType", ""), ("tarcza", None))
        # punkt końcowy na blokadzie liniowej (1803_KO_U1bl – „PK 1P” przed wjazdowym): bez symbolu, koniec wyjazdu na szlak pokazuje
        # trójkąt końca przy semaforze wjazdowym (przebiegi gry kończą się na „…kps”) – uwaga właściciela 2026-11-21
        # … chyba że blokada nie ma semafora (Łazy t13544kbl – 224ŁA): wtedy PK zostaje, jak trójkąt w grze
        if typ == "pk" and nazwa(p).lower().endswith("bl") and nazwa(p)[:-2].lower() in nazwy_syg: continue
        w_prawo = p.get("direction") == "Right"
        el_(typ, _sr=nazwa(p), **miejsce(o, x, y, w_prawo), styl=styl,
            opisy=[{"rola": "nazwa", "tekst": p.get("visibleName", ""), "d": [0, 17 if w_prawo else -16]}]); nsyg += 1
    for p in wyk:
        x, y = p["position"]; o, _ = odcinek_komorki(x, y)
        if o: el_("wykolejnica", _sr=nazwa(p), **miejsce(o, x, y), opisy=[{"rola": "nazwa", "tekst": p.get("visibleName", ""), "d": [0, -16]}])
    for p in kozly:
        x, y = p["position"]; best = None
        for v, i in wez_v.items():
            if len(graf.get(v, ())) == 1 and v[2] == y and abs(v[1] - x) <= 2 and (best is None or abs(v[1] - x) < best[0]): best = (abs(v[1] - x), i)
        if best: el_("koziol", na=best[1], p=list(wezly[best[1]]["p"]))
    for p in perony:
        # wymiary EBI → j. pulpitu zmierzone na zrzucie gry (Koluszki: 85 × 57 → 160 × 88, środek 2,5 wiersza pod pozycją)
        w = max(8, round(int(p.get("width", 64)) * 1.9 / 8) * 8); h = max(8, round(int(p.get("height", 24)) * 1.55 / 8) * 8)
        x0 = p["position"][0] * CX - 4; y0 = p["position"][1] * CY + 56 - h // 2
        el_("peron", p=[x0 - x0 % 8, y0 - y0 % 8], rozm=[w, h],
            opisy=[{"rola": "numer", "tekst": p.get("visibleName", ""), "d": [w // 2, h // 2]}])
    for p in pipy:
        # okno numeru pociągu (szare pole przy końcu szlaku)
        el_("polePip", p=[p["position"][0] * CX, p["position"][1] * CY + 32], rozm=[80, 64])
    for p in przejazdy:
        x, y = p["position"]; h = max(1, int(p.get("height", 3)))
        el_("przejazd", _sr=nazwa(p), p=[x * CX + CX // 2, y * CY + CY // 2], rozm=[0, h * CY], opisy=[{"rola": "numer", "tekst": p.get("visibleName", ""), "d": [0, -16]}])
    for p in napisy:
        if not p.get("visibleName"): continue
        # w grze tekst zaczyna się w komórce (wyrównanie do lewej), w edytorze p = środek napisu
        tekst = p["visibleName"].strip()
        el_("napis", p=[p["position"][0] * CX + len(tekst) * 3 + 4, p["position"][1] * CY + CY // 2], styl="sasiad" if "LK" in tekst else None,
            opisy=[{"rola": "numer", "tekst": tekst, "d": [0, 0]}])
    for p in nastawnie:
        el_("napis", p=[p["position"][0] * CX + CX // 2, p["position"][1] * CY + CY // 2],
            opisy=[{"rola": "numer", "tekst": p.get("visibleName", "").strip('"').join("„”"), "d": [0, 0]}])
    for p in blokady:
        x, y = p["position"]
        for xx in (x, x - 1, x + 1, x - 2, x + 2):
            o, _ = odcinek_komorki(xx, y)
            if o: el_("strzalkaBlokady", _sr=nazwa(p), **miejsce(o, xx, y, bool(p.get("rightArrival"))),
                      opisy=[{"rola": "nazwa", "tekst": (p.get("visibleName") or "").strip(), "d": [0, -20]}]); break
    # numery torów – środek toru
    for p in numery:
        cs = p["cells"]; x, y = cs[len(cs) // 2]
        o, _ = odcinek_komorki(x, y)
        if o: el_("numerToru", **miejsce(o, x, y), p=[x * CX + CX // 2, y * CY + CY // 2], opisy=[{"rola": "numer", "tekst": p["visibleName"], "d": [0, 0]}])
    # przerwy izolacyjne: na węzłach granic obwodów (bez miejsc przy sygnalizatorach – tam granicę pokazuje sygnalizator)
    zajete_m = {(p["position"][0], p["position"][1]) for p in sygnaly}
    nprz = 0
    # pulpit EBI w grze nie rysuje złączy izolowanych (tor ciągły) – przerwy tylko na życzenie (--przerwy); obwody i tak dzielą węzły granic
    for (bw, a, c) in (granice if "--przerwy" in sys.argv else []):
        if (a[1], a[2]) in zajete_m or (c[1], c[2]) in zajete_m: continue
        k = next((o for o in odcinki if o["a"] == bw), None) or next((o for o in odcinki if o["b"] == bw), None)
        if k: el_("przerwa", na=k["id"], t=0.0 if k["a"] == bw else 1.0); nprz += 1
    # ostatnia kontrola: odcinek nie co 45° → pion – 45° – pion (albo poziom – 45° – poziom); elementy na nim – na środkowy kawałek
    nowe = []; zamiana = {}
    for o in odcinki:
        (ax, ay), (bx, by) = wezly[o["a"]]["p"], wezly[o["b"]]["p"]
        dx, dy = bx - ax, by - ay
        if not dx or not dy or abs(dx) == abs(dy): nowe.append(o); continue
        sx, sy = (1 if dx > 0 else -1), (1 if dy > 0 else -1)
        if abs(dx) < abs(dy):
            pol = ((abs(dy) - abs(dx)) // 2) // 8 * 8
            p1, p2 = (ax, ay + sy * pol), (bx, ay + sy * (pol + abs(dx)))
        else:
            pol = ((abs(dx) - abs(dy)) // 2) // 8 * 8
            p1, p2 = (ax + sx * pol, ay), (ax + sx * (pol + abs(dy)), by)
        w1, w2 = nid(), nid(); wezly[w1] = {"id": w1, "p": list(p1)}; wezly[w2] = {"id": w2, "p": list(p2)}
        k1, k2, k3 = nid(), nid(), nid()
        nowe += [{"id": k1, "a": o["a"], "b": w1, "pkt": [], "styl": "ciagly"}, {"id": k2, "a": w1, "b": w2, "pkt": [], "styl": "ciagly"},
                 {"id": k3, "a": w2, "b": o["b"], "pkt": [], "styl": "ciagly"}]
        zamiana[o["id"]] = k2
        for k in (k1, k2, k3): obwod_odc[k] = obwod_odc.get(o["id"])
    odcinki[:] = nowe
    for e in elementy:
        if e.get("na") in zamiana: e["na"] = zamiana[e["na"]]; e["t"] = 0.5
    # łącznik jak „]” na pulpicie w grze (wzór właściciela – 123/126 poprawione ręcznie w edytorze): pion z krótkimi ostrymi końcówkami.
    # Ukos rozjazd → pion skrócony z 16 do 8 j.: rozjazd 8 j. bliżej pionu, załamanie 8 j. od toru (resztę ukosu chowa przerwa rozjazdu).
    nhak = 0
    przy = collections.defaultdict(list)
    for o in odcinki: przy[o["a"]].append(o); przy[o["b"]].append(o)
    def drugi(o, n): return o["b"] if o["a"] == n else o["a"]
    for v in zwr:
        n = wez_v.get(v)
        if n is None: continue
        nx, ny = wezly[n]["p"]
        for o in przy[n]:
            k = drugi(o, n); kx, ky = wezly[k]["p"]
            if abs(kx - nx) != 16 or abs(ky - ny) != 16 or len(przy[k]) != 2: continue
            dal = next(q for q in przy[k] if q is not o); m = drugi(dal, k)
            if wezly[m]["p"][0] != kx or abs(wezly[m]["p"][1] - ky) < 8: continue          # za załamaniem musi być pion
            sx = 1 if kx > nx else -1; sy = 1 if ky > ny else -1
            if any(w["id"] != n and w["p"][1] == ny and min(nx, nx + sx * 8) <= w["p"][0] <= max(nx, nx + sx * 8) for w in wezly.values()): continue
            wezly[n]["p"] = [nx + sx * 8, ny]; wezly[k]["p"] = [kx, ny + sy * 8]
            nhak += 1; break
    # pion prosto nad / pod rozjazdem (np. 3, 128, 151) – w grze też mały haczyk: rozjazd 8 j. wstecz od pionu, załamanie 8 j. od toru
    for v, p in zwr.items():
        n = wez_v.get(v)
        if n is None: continue
        _, t = param(p); nx, ny = wezly[n]["p"]
        pion = [o for o in przy[n] if wezly[drugi(o, n)]["p"][0] == nx and wezly[drugi(o, n)]["p"][1] != ny]
        if len(pion) != 1: continue
        o = pion[0]; m = drugi(o, n); my = wezly[m]["p"][1]
        if abs(my - ny) <= 16: continue
        dy = 1 if my > ny else -1; nowy_x = nx - t * 8
        if any(w["id"] != n and w["p"][1] == ny and min(nowy_x, nx) <= w["p"][0] <= max(nowy_x, nx) for w in wezly.values()): continue
        k = nid(); wezly[k] = {"id": k, "p": [nx, ny + dy * 8]}
        wezly[n]["p"] = [nowy_x, ny]
        if o["a"] == n: o["a"] = k
        else: o["b"] = k
        kid = nid(); odcinki.append({"id": kid, "a": n, "b": k, "pkt": [], "styl": "ciagly"}); obwod_odc[kid] = obwod_odc.get(o["id"])
        nhak += 1
    raport.append(f"{nazwa_obrazu}: łączników „]” z krótkimi końcówkami {nhak}")
    # strzałka blokady nie może wchodzić na semafor wjazdowy (w grze: semafor, odstęp, strzałka) – co najmniej 40 j. od semafora,
    # przesunięta w stronę szlaku
    def poz(e):
        k = next((o for o in odcinki if o["id"] == e["na"]), None)
        if k is None: return None
        (ax, ay), (bx, by) = wezly[k["a"]]["p"], wezly[k["b"]]["p"]
        return ax + (bx - ax) * e["t"], ay + (by - ay) * e["t"]
    sem = [(poz(e), e) for e in elementy if e["typ"] == "semafor" and e.get("na")]
    nstrz = 0
    for e in elementy:
        if e["typ"] != "strzalkaBlokady" or not e.get("na"): continue
        q = poz(e)
        if q is None: continue
        blisko = [s for s, _ in sem if s and abs(s[1] - q[1]) < 1 and abs(s[0] - q[0]) < 40]
        if not blisko: continue
        sx = blisko[0][0]; cel = sx + (40 if q[0] >= sx else -40)
        for o in odcinki:
            (ax, ay), (bx, by) = wezly[o["a"]]["p"], wezly[o["b"]]["p"]
            if ay == by == q[1] and min(ax, bx) <= cel <= max(ax, bx) and ax != bx:
                e["na"] = o["id"]; e["t"] = round((cel - ax) / (bx - ax), 4); nstrz += 1; break
    raport.append(f"{nazwa_obrazu}: strzałek blokady odsuniętych od semafora {nstrz}")
    # odgałęzienie zaczynające się krótkim poziomym kawałkiem (≤ 8 j.) i dopiero potem ukosem (np. noga „X” przy 107, 117):
    # rozjazd ma wtedy trzy ramiona poziome i edytor robi przerwę na złym (na wprost). Rozjazd przesuwa się na koniec kawałka,
    # odgałęzienie wychodzi z niego od razu ukosem – jak 70/73/69ab/74cd.
    nscal = 0
    for v in list(zwr):
        n = wez_v.get(v)
        if n is None: continue
        for _ in range(2):
            przy_n = [o for o in odcinki if n in (o["a"], o["b"])]
            nx, ny = wezly[n]["p"]; zrob = None
            if any(wezly[q["b"] if q["a"] == n else q["a"]]["p"][1] != ny for q in przy_n): break   # ma już ramię nie-poziome
            for o in przy_n:
                k = o["b"] if o["a"] == n else o["a"]; kx, ky = wezly[k]["p"]
                if ky != ny or not (0 < abs(kx - nx) <= 8): continue
                przy_k = [q for q in odcinki if k in (q["a"], q["b"])]
                if len(przy_k) != 2: continue
                dal = przy_k[0] if przy_k[1] is o else przy_k[1]; m = dal["b"] if dal["a"] == k else dal["a"]
                mx, my = wezly[m]["p"]
                if abs(mx - kx) != abs(my - ky) or my == ky: continue                        # dalej musi być ukos 45°
                # tor na wprost po tej samej stronie musi być dłuższy niż przesunięcie
                if any(wezly[q["b"] if q["a"] == n else q["a"]]["p"][1] == ny and q is not o and
                       0 < (wezly[q["b"] if q["a"] == n else q["a"]]["p"][0] - nx) * (kx - nx) <= abs(kx - nx) * abs(kx - nx) for q in przy_n): continue
                zrob = (o, k, dal); break
            if not zrob: break
            o, k, dal = zrob
            wezly[n]["p"] = list(wezly[k]["p"])
            if dal["a"] == k: dal["a"] = n
            else: dal["b"] = n
            odcinki.remove(o); del wezly[k]
            for e in elementy:
                if e.get("na") == o["id"]: e["na"] = dal["id"]; e["t"] = 0.0 if dal["a"] == n else 1.0
                elif e.get("na") == k: e["na"] = n
            nscal += 1
    raport.append(f"{nazwa_obrazu}: odgałęzień wyprostowanych przy rozjeździe (bez poziomego kawałka) {nscal}")
    # granica obwodów pod sygnalizatorem: węzeł granicy stoi w połowie między komórkami, sygnalizator – w środku swojej komórki,
    # więc tor jednego obwodu „wyjeżdżał” za semafor o 8–26 j. (właściciel 2026-11-21, Koluszki C101–C103). Węzeł granicy do 40 j.
    # od sygnalizatora na tej samej prostej przesuwa się pod sygnalizator (na siatkę 8 j.), elementy na obu odcinkach zostają w miejscu.
    przy_w = collections.defaultdict(list)
    for o in odcinki: przy_w[o["a"]].append(o); przy_w[o["b"]].append(o)
    def obw(o):
        v = obwod_odc.get(o["id"]); return v if v and not str(v).startswith("_bez_") else None
    granica = {n for n, l in przy_w.items() if len(l) == 2 and obw(l[0]) and obw(l[1]) and obw(l[0]) != obw(l[1])}
    po_id = {o["id"]: o for o in odcinki}
    def pkt_el(e):
        o = po_id.get(e.get("na"))
        if o is None or "t" not in e: return None
        (ax, ay), (bx, by) = wezly[o["a"]]["p"], wezly[o["b"]]["p"]
        return ax + (bx - ax) * e["t"], ay + (by - ay) * e["t"]
    kand = []
    for e in elementy:
        if e["typ"] not in ("semafor", "tarcza", "powtarzacz") or (q := pkt_el(e)) is None: continue
        o0 = po_id[e["na"]]
        for start in (o0["a"], o0["b"]):
            n, cur = start, o0; d = abs(wezly[n]["p"][0] - q[0]) + abs(wezly[n]["p"][1] - q[1])
            while d <= 40:
                if n in granica: kand.append((d, e, n)); break
                if len(przy_w[n]) != 2: break
                nx_ = przy_w[n][0] if przy_w[n][1] is cur else przy_w[n][1]; m_ = drugi(nx_, n)
                d += abs(wezly[m_]["p"][0] - wezly[n]["p"][0]) + abs(wezly[m_]["p"][1] - wezly[n]["p"][1]); n, cur = m_, nx_
    uz_w, uz_e, ngran = set(), set(), 0
    for d, e, n in sorted(kand, key=lambda k: k[0]):
        if n in uz_w or e["id"] in uz_e or d < 0.5: continue
        e1, e2 = przy_w[n]; A, C, N = wezly[drugi(e1, n)]["p"], wezly[drugi(e2, n)]["p"], wezly[n]["p"]
        k = 0 if A[1] == N[1] == C[1] else 1 if A[0] == N[0] == C[0] else None
        if k is None: continue
        q = pkt_el(e)
        nowy = list(N); nowy[k] = int(round(q[k] / 8) * 8)
        if not (min(A[k], C[k]) < nowy[k] < max(A[k], C[k]) and min(A[k], C[k]) < q[k] < max(A[k], C[k])): continue
        inne = [(x, pkt_el(x)) for x in elementy if x is not e and x.get("na") in (e1["id"], e2["id"]) and "t" in x]
        wezly[n]["p"] = nowy
        for x, (px, py) in inne:
            for o in (e1, e2):
                (ax, ay), (bx, by) = wezly[o["a"]]["p"], wezly[o["b"]]["p"]
                if min((ax, ay)[k], (bx, by)[k]) - 1e-6 <= (px, py)[k] <= max((ax, ay)[k], (bx, by)[k]) + 1e-6:
                    L = abs(bx - ax) + abs(by - ay)
                    x["na"] = o["id"]; x["t"] = 0 if L == 0 else round((abs(px - ax) + abs(py - ay)) / L, 4); break
        e["t"] = 0.0 if po_id[e["na"]]["a"] == n else 1.0
        uz_w.add(n); uz_e.add(e["id"]); ngran += 1
    raport.append(f"{nazwa_obrazu}: granic obwodów przesuniętych pod sygnalizator {ngran}")
    # tor bez obwodu (bez kontroli niezajętości) – w grze linią przerywaną (np. tory za PK, „- - 124 - -”)
    # (nie przy rozjeździe – krótki kawałek przy zwrotnicy bez obwodu w danych gry, np. przy 107, gra rysuje ciągły)
    wezly_zwr = {wez_v[v] for v in zwr if v in wez_v}
    for o in odcinki:
        ob = obwod_odc.get(o["id"])
        if (not ob or str(ob).startswith("_bez_")) and o["a"] not in wezly_zwr and o["b"] not in wezly_zwr: o["styl"] = "bezKontroli"
    raport.append(f"{nazwa_obrazu}: węzłów {len(wezly)}, odcinków {len(odcinki)}, zwrotnic {sum(1 for e in elementy if e['typ'] == 'zwrotnica')}, "
                  f"sygnalizatorów {nsyg}, przerw {nprz}, elementów razem {len(elementy)}")
    mapa_el = {e["id"]: e.pop("_sr") for e in elementy if e.get("_sr")}
    for e in elementy: e.pop("_sr", None)
    mapa_odc = {k: v for k, v in obwod_odc.items() if v and not str(v).startswith("_bez_") and any(o["id"] == k for o in odcinki)}
    MAPY.append({"elementy": mapa_el, "odcinki": mapa_odc})
    return {"id": nid(), "nazwa": nazwa_obrazu, "rodzaj": "szczegolowy", "posterunki": [], "szer": 1920, "wys": 1080,
            "opisyOdcinkow": "nazadanie", "rzedy": [], "strefy": [{"rodzaj": "polecenia", "prost": [1440, 0, 480, 160]}],
            "wezly": list(wezly.values()), "odcinki": odcinki, "elementy": elementy, "przypiecia": []}


def komorki(p):
    if p.get("cells"): return [tuple(c) for c in p["cells"]]
    return [tuple(p["position"])] if p.get("position") else []


def sklej_zakladki(po_zak, zak):
    """Zakładki wirtualne EBI obok siebie – jeden pulpit jak w grze (to samo cięcie co w pulpit_do_suk.py):
    kolumna c0 poprzedniej = kolumna c1 następnej, w obu wyłącznie te same tory poziome; żaden obiekt nie ginie ani nie dubluje."""
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
            n = nazwa(p); xs = [c[0] for c in komorki(p)]
            if n and xs and ((min(xs) <= granica) if lewa else (max(xs) >= granica)): s.add((k, n))
        return s
    przes = {zak[0]: 0}; ciecie = {}
    for ip, t in enumerate(zak[1:]):
        tp = zak[ip]; a, b = po_zak[tp], po_zak[t]
        ka, kb = kolumny_czyste(a), kolumny_czyste(b)
        wszystkie = {(k, nazwa(p)) for k, p in a + b if nazwa(p)}
        najl = None
        for c0, w0 in ka.items():
            for c1, w1 in kb.items():
                wspolne = set(w0) & set(w1)
                if len(wspolne) < 3 or any(w0[y] != w1[y] for y in wspolne): continue
                niezg = sum(1 for y in set(w0) ^ set(w1) if not (w0.get(y) or w1.get(y)))
                na, nb = nazwy_do(a, c0, True), nazwy_do(b, c1, False)
                przez = {("EBI300StraightTrack", n) for n in list(w0.values()) + list(w1.values()) if n}
                strata = len(wszystkie - (na | nb)); dubel = len((na & nb) - przez)
                ocena = (strata + dubel + niezg, -len(wspolne), abs(c0 - 60))
                if najl is None or ocena < najl[0]: najl = (ocena, c0, c1, strata, dubel, niezg)
        if najl is None: raport.append(f"zakładka {t}: nie znaleziono cięcia – pominięta"); continue
        _, c0, c1, strata, dubel, niezg = najl
        ciecie[t] = (c0, c1); przes[t] = przes[tp] + c0 - c1
        raport.append(f"zakładka {t}: cięcie {c0} = {c1}, przesunięcie {c0 - c1}; utraconych {strata}, zdublowanych {dubel}, niezgodnych {niezg}")
    wynik = []; perony_wziete = set()
    for i, t in enumerate(zak):
        if i and t not in ciecie: continue
        lo = ciecie[t][1] if t in ciecie else -10**9
        nast = zak[i + 1] if i + 1 < len(zak) else None
        hi = ciecie[nast][0] if nast in ciecie else 10**9
        ox = przes[t]
        for k, p in po_zak[t]:
            p = dict(p)
            if p.get("cells"):
                cs = [c for c in p["cells"] if lo <= c[0] <= hi]
                if not cs: continue
                p["cells"] = [[c[0] + ox, c[1]] for c in cs]; p["position"] = p["cells"][0]
            elif p.get("position"):
                # perony leżą w grze na granicy zakładek – bierze się je z pierwszej zakładki w całości (jak na pulpicie w grze)
                if k != "EBI300Platform" and not (lo <= p["position"][0] <= hi): continue
                p["position"] = [p["position"][0] + ox, p["position"][1]]
            if k == "EBI300Platform":
                if p.get("visibleName") in perony_wziete: continue
                perony_wziete.add(p.get("visibleName"))
            wynik.append((k, p))
    # ten sam tor (nazwa, wiersz/kolumna) z obu zakładek – jeden ciągły tor; pozostałe obiekty bez duplikatów
    proste = {}; inne = []; widziane = set()
    for k, p in wynik:
        if k == "EBI300StraightTrack" and p.get("cells") and nazwa(p):
            poz = p.get("isHorizontal", True); c0 = p["cells"][0]
            key = (nazwa(p), c0[1] if poz else c0[0], poz)
            if key in proste:
                q = proste[key]; q["cells"] = q["cells"] + p["cells"]
                if p.get("visibleName") and not q.get("visibleName"): q["visibleName"] = p["visibleName"]
            else: proste[key] = dict(p); inne.append((k, proste[key]))
            continue
        key = (k, nazwa(p), tuple(p.get("position") or ()), p.get("visibleName"), tuple(map(tuple, p.get("cells") or ())))
        if key in widziane: continue
        widziane.add(key); inne.append((k, p))
    for (n, stala, poz), p in proste.items():
        zs = sorted({c[0] if poz else c[1] for c in p["cells"]})
        p["cells"] = [[z, stala] if poz else [stala, z] for z in range(zs[0], zs[-1] + 1)]; p["position"] = p["cells"][0]
    return inne


MAPY = []
posterunek = d.get("posterunek") or "Posterunek"
zak = sorted({e["pola"].get("virtualTab", 0) for e in d["elementy"]})
po_zak = {t: [(e["klasa"], e["pola"]) for e in d["elementy"] if e["pola"].get("virtualTab", 0) == t] for t in zak}
obrazy = []
if "--zakladki" in sys.argv or len(zak) == 1:
    for i, t in enumerate(zak):
        obrazy.append(obraz_z_zakladki(po_zak[t], posterunek if len(zak) == 1 else f"{posterunek} {i + 1}"))
else:
    # domyślnie: cały pulpit na jednym obrazie – jak w grze (wszystkie zakładki wirtualne sklejone)
    ob = obraz_z_zakladki(sklej_zakladki(po_zak, zak), posterunek)
    xs = [w["p"][0] for w in ob["wezly"]] + [e["p"][0] + (e.get("rozm") or [0])[0] for e in ob["elementy"] if e.get("p")]
    ys = [w["p"][1] for w in ob["wezly"]] + [e["p"][1] + (e.get("rozm") or [0, 0])[1] for e in ob["elementy"] if e.get("p")]
    ob["szer"] = max(1920, (max(xs) + 2 * CX + 7) // 8 * 8)
    ob["wys"] = max(1080, (max(ys) + CY + 7) // 8 * 8)
    ob["strefy"] = [{"rodzaj": "polecenia", "prost": [ob["szer"] - 480, 0, 480, 160]}]
    # jak w grze: nad rysunkiem pas nagłówka (nazwa posterunku) – cały rysunek o wiersz niżej, nazwa nad środkiem stacji
    for w_ in ob["wezly"]: w_["p"][1] += CY
    for e in ob["elementy"]:
        if e.get("p"): e["p"][1] += CY
    peron = [e for e in ob["elementy"] if e["typ"] == "peron"]
    xt = (peron[0]["p"][0] + peron[0]["rozm"][0] // 2) if peron else ob["szer"] // 2
    ob["elementy"].append({"id": nid(), "typ": "napis", "t": 0, "kier": 1, "p": [xt - xt % 8, 112], "styl": "tytul",
                           "opisy": [{"rola": "numer", "tekst": posterunek.upper(), "d": [0, 0]}]})
    obrazy.append(ob)
wynik = {"version": 2, "ut": UT, "siatka": 8, "obrazy": obrazy}
json.dump(wynik, open(wyj, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
# mapa ID SimRail: element / odcinek rysunku → railNetworkName (do pulpitu zdalnego; edytor jej nie zna i nie zmienia)
mapa = {"opis": "ID SimRail elementów i odcinków zobrazowania (obraz → id rysunku → railNetworkName). Tworzy pulpit_do_zob.py.",
        "posterunek": posterunek, "kod": next((e["pola"].get("prefix", "").upper() for e in d["elementy"] if e["klasa"] == "EBI300ScreenStatus"), "") or "PO",
        "obrazy": {o["id"]: m for o, m in zip(obrazy, MAPY)},
        # rodzaj blokady liniowej (SHL / Eac / Eap) – zob_do_suk.py ustawia wg niego typ szlaku SUK (SBL / PBL)
        "blokady": dict(sorted((e["pola"]["railNetworkName"], e["pola"]["lineBlockType"]) for e in d["elementy"]
                               if e["klasa"] == "EBI300LineBlock" and e["pola"].get("railNetworkName") and e["pola"].get("lineBlockType")))}
plik_mapy = wyj[:-len(".zob.json")] + ".simrail.json" if wyj.endswith(".zob.json") else wyj + ".simrail.json"
json.dump(mapa, open(plik_mapy, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("mapa ID SimRail:", plik_mapy, "–", sum(len(m["elementy"]) for m in MAPY), "elementów,", sum(len(m["odcinki"]) for m in MAPY), "odcinków")
for r in raport: print(r)
print("zapisano", wyj)
