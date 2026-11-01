# Generator pliku app/Lcs.EdytorUT/Dane/symbole_ig10.json – znaki rozjazdów wg Ig-10, zał. 8 (s. 18–24).
# Wymiary w mm arkusza; x – wzdłuż toru (rozjazd: od początku rozjazdu PR w stronę KR), y – w stronę odgałęzienia.
# Plik JSON można potem poprawiać ręcznie (wtedy nie uruchamiać generatora ponownie).
import json, os

L, H = 8.0, 2.0          # klin rozjazdu zwyczajnego: długość, wysokość na końcu
T = 1.0                  # kreska początku rozjazdu: ±1 mm (kreska a = 2 mm)
A, A0, HK = 8.0, 2.5, 1.6  # rozjazd krzyżowy / skrzyżowanie: odcinek a = 8 mm od środka, klin od 2,5 mm, wysokość na końcu

def line(*pts, w=0.18): return {'t': 'line', 'pts': [list(p) for p in pts], 'w': w}
def poly(*pts, fill='ink', w=0.18): return {'t': 'poly', 'pts': [list(p) for p in pts], 'fill': fill, 'w': w}
def text(x, y, h=1.8, s='{nr}'): return {'t': 'text', 'at': [x, y], 'h': h, 'text': s}

def hatch(x0, x1, y0f, y1f, step=1.0):
    """kreski pionowe w klinie: od y0f(x) do y1f(x), co step mm"""
    res, x = [], x0 + step
    while x < x1 + 1e-9:
        res.append(line((x, y0f(x)), (x, y1f(x))))
        x += step
    return res

tick = line((0, -T), (0, T))
nr_rz = text(-2.5, 2.4)
G, X0 = 0.5, 2.0         # odstęp klina od strony otwartej; odgałęzienie y = x / 4 na podglądzie odchodzi o G przy x = 2
branch = lambda x: x / 4

S = []
def sym(id, name, ig, anchor, prims): S.append({'id': id, 'name': name, 'ig10': ig, 'anchor': anchor, 'prims': prims})

# Klin w kącie między torem a odgałęzieniem (Ig-10 zał. 8 s. 18, plan przykładowy zał. 7, Semaforek), po stronie kierunku
# zamkniętego. Na planie edytor liczy go z geometrii rozjazdu (UtView.DrawWedge); tu – podgląd na odgałęzieniu 1:4.
ANCH_RZ = 'początek rozjazdu (PR) – kreska; klin w kącie między torem a odgałęzieniem (na planie liczony z geometrii rozjazdu, długość do 12 mm), po stronie kierunku zamkniętego, odstęp 0,5 mm od strony otwartej; x w stronę KR, y w stronę odgałęzienia'
sym('rz_uzal_wprost', 'Rozjazd zwyczajny ze zwrotnicą uzależnioną – zasadniczo na tor główny', 'zał. 8, s. 18', ANCH_RZ,
    [tick, poly((X0, G), (L, branch(L)), (L, G), w=0), nr_rz])
sym('rz_uzal_zwrot', 'Rozjazd zwyczajny ze zwrotnicą uzależnioną – zasadniczo na tor odgałęźny', 'zał. 8, s. 18', ANCH_RZ,
    [tick, poly((X0, 0), (L, branch(L) - G), (L, 0), w=0), nr_rz])
sym('rz_nieuzal', 'Rozjazd zwyczajny ze zwrotnicą nieuzależnioną', 'zał. 8, s. 19', ANCH_RZ,
    [tick, poly((X0, G), (L, branch(L)), (L, G), fill='paper')] + hatch(X0, L - 1, lambda x: G, branch) + [nr_rz])
sym('rz_nieczynny', 'Rozjazd zwyczajny nieoddany do użytku', 'zał. 8, s. 19', ANCH_RZ,
    [tick, poly((X0, G), (L, branch(L)), (L, G), fill='paper'), nr_rz])

ANCH_RK = 'punkt matematyczny (przecięcie osi), x wzdłuż toru A, y w stronę toru B na końcu +x'
kwedge = lambda x: HK * (x - A0) / (A - A0)
center = line((0, -T), (0, T))
nr_rk = text(0, 3.0)
def ends(fill_r, fill_l, hatch_r=False, hatch_l=False, zwrot=False):
    lo = 0.5 if zwrot else 0.0
    pr = [poly((A0, 0), (A, lo), (A, HK), fill=fill_r)]
    pl = [poly((-A0, 0), (-A, -lo), (-A, -HK), fill=fill_l)]
    if hatch_r: pr += hatch(A0, A, lambda x: 0, kwedge)
    if hatch_l: pl += [line((-p['pts'][0][0], -p['pts'][0][1]), (-p['pts'][1][0], -p['pts'][1][1])) for p in hatch(A0, A, lambda x: 0, kwedge)]
    return pr + pl
sym('rkpd_uzal_wprost', 'Rozjazd podwójny krzyżowy ze zwrotnicami uzależnionymi – zasadniczo na tor główny', 'zał. 8, s. 21', ANCH_RK,
    [center] + ends('ink', 'ink') + [nr_rk])
sym('rkpd_uzal_zwrot', 'Rozjazd podwójny krzyżowy ze zwrotnicami uzależnionymi – zasadniczo na tor odgałęźny', 'zał. 8, s. 21', ANCH_RK,
    [center] + ends('ink', 'ink', zwrot=True) + [nr_rk])
sym('rkpd_nieuzal', 'Rozjazd podwójny krzyżowy ze zwrotnicami nieuzależnionymi', 'zał. 8, s. 22', ANCH_RK,
    [center] + ends('paper', 'paper', True, True) + [nr_rk])
sym('rkpd_jedna_para', 'Rozjazd podwójny krzyżowy z jedną parą zwrotnic uzależnioną', 'zał. 8, s. 23', ANCH_RK,
    [center] + ends('ink', 'paper', False, True) + [nr_rk])
sym('rkpd_nieczynny', 'Rozjazd podwójny krzyżowy nieoddany do użytku', 'zał. 8, s. 23', ANCH_RK,
    [center] + ends('paper', 'paper') + [nr_rk])
sym('rkp', 'Rozjazd pojedynczy krzyżowy', 'zał. 8, s. 22', ANCH_RK,
    [center] + ends('ink', 'ink') + [nr_rk])
sym('st', 'Skrzyżowanie torów kolejowych', 'zał. 8, s. 24', ANCH_RK,
    [center] + ends('', '', True, True) + [nr_rk])

# Obiekty inżynieryjne (Ig-10 zał. 8 s. 41–44, 63; PLAN_EDYTOR_UT.md § 5m). Tabelki podają tylko grubość linii 0,18 mm i kreskowanie
# (kreska 1 mm, odstęp 1 mm); bok 3 mm od osi, skrzydełka 7 × 3 mm itd. odczytane z rysunków (UtView.Wym). Edytor rysuje je po osi toru
# między km od–do (wzdłuż) albo wzdłuż osi przeszkody (poprzeczne) – tu podgląd na odcinku 20 mm.
def dashed(x0, y0, x1, y1, a=1.0, b=1.0):
    import math
    L = math.hypot(x1 - x0, y1 - y0); res, t = [], 0.0
    while t < L - 1e-9:
        e = min(L, t + a); res.append(line((x0 + (x1 - x0) * t / L, y0 + (y1 - y0) * t / L), (x0 + (x1 - x0) * e / L, y0 + (y1 - y0) * e / L))); t = e + b
    return res
OL, BOK, SD, SO = 20.0, 3.0, 7.0, 3.0
def wings(x0, x1, h, dl, od):
    return [line((x0, h), (x0 - dl, h + od)), line((x0, -h), (x0 - dl, -h - od)), line((x1, h), (x1 + dl, h + od)), line((x1, -h), (x1 + dl, -h - od))]
axis = line((-10, 0), (OL + 10, 0))
ANCH_OW = 'obiekt wzdłuż toru: x – km od (0) do km do (tu 20 mm), y – w poprzek osi toru; boki ±3 mm od osi (przy kilku torach – od skrajnych)'
ANCH_OP = 'obiekt poprzeczny: oś przeszkody pionowo (x = 0 – oś toru); tu tor poziomo, przeszkoda ±10 mm'
sym('ob_most', 'Most', 'zał. 8, s. 41', ANCH_OW,
    [axis, line((0, BOK), (OL, BOK)), line((0, -BOK), (OL, -BOK)), line((0, BOK), (0, -BOK)), line((OL, BOK), (OL, -BOK))] + wings(0, OL, BOK, SD, SO))
sym('ob_tunel', 'Linia kolejowa w tunelu', 'zał. 8, s. 42', ANCH_OW,
    [line((-10, 0), (0, 0)), line((OL, 0), (OL + 10, 0))] + dashed(0, 0, OL, 0) + dashed(0, BOK, OL, BOK) + dashed(0, -BOK, OL, -BOK) +
    [line((0, BOK), (0, -BOK)), line((OL, BOK), (OL, -BOK))] + wings(0, OL, BOK, SD, SO))
W5, WD, WO = 5.0, 3.4, 1.4
def wiad(x0, x1):
    return [line((x0, W5), (x1, W5)), line((x0, -W5), (x1, -W5)), line((x0, W5), (x0, -W5)), line((x1, W5), (x1, -W5)),
            line((x0, W5), (x0 - WO, W5 + WD)), line((x0, -W5), (x0 - WO, -W5 - WD)), line((x1, W5), (x1 + WO, W5 + WD)), line((x1, -W5), (x1 + WO, -W5 - WD))]
sym('ob_wiadukt', 'Wiadukt kolejowy nad drogą (linia kolejowa w poziomie górnym)', 'zał. 8, s. 43', ANCH_OW + '; krawędzie drogi kreskowane',
    [axis] + wiad(6, 14) + dashed(7, -9, 7, 9) + dashed(13, -9, 13, 9))
sym('ob_wiadukt_tor', 'Skrzyżowanie dwóch torów w różnych poziomach (wiadukt nad torem)', 'zał. 8, s. 44', ANCH_OW + '; tor dolny przerywany pod obiektem',
    [line((-10, 0), (OL + 10, 0), w=0.5)] + wiad(6, 14) + [line((10, -12), (10, -W5))] + dashed(10, -W5, 10, W5) + [line((10, W5), (10, 12))])
sym('ob_wiadukt_drog', 'Wiadukt drogowy nad torami (linia kolejowa w poziomie dolnym)', 'zał. 8, s. 44', ANCH_OP + '; tor pod obiektem przerywany',
    [line((-10, 0), (-3.5, 0)), line((3.5, 0), (10, 0))] + dashed(-3.5, 0, 3.5, 0) +
    [line((-3.5, -5.3), (3.5, -5.3)), line((-3.5, 5.3), (3.5, 5.3)), line((-3.5, -5.3), (-3.5, 5.3)), line((3.5, -5.3), (3.5, 5.3)),
     line((-3.5, 5.3), (-4.9, 8.7)), line((3.5, 5.3), (4.9, 8.7)), line((-3.5, -5.3), (-4.9, -8.7)), line((3.5, -5.3), (4.9, -8.7))] +
    dashed(-2.5, -10, -2.5, 10) + dashed(2.5, -10, 2.5, 10))
def tpod(name, ig, extra=()):
    return [line((-10, 0), (10, 0))] + dashed(-BOK, -10, -BOK, 10) + dashed(BOK, -10, BOK, 10) + [line((-BOK, -10), (BOK, -10)), line((-BOK, 10), (BOK, 10)),
            line((-BOK, 10), (-BOK - SO, 10 + SD)), line((BOK, 10), (BOK + SO, 10 + SD)), line((-BOK, -10), (-BOK - SO, -10 - SD)), line((BOK, -10), (BOK + SO, -10 - SD))] + list(extra)
sym('ob_tunel_pod', 'Tunel pod linią kolejową', 'zał. 8, s. 43', ANCH_OP, tpod('', ''))
sym('ob_przejscie_pod', 'Przejście piesze pod torami kolejowymi', 'zał. 8, s. 42', ANCH_OP, tpod('', ''))
sym('ob_przepust', 'Przepust', 'zał. 8, s. 43', ANCH_OP,
    [line((-10, 0), (10, 0))] + dashed(0, -7, 0, 7) + [line((0, 7), (-3, 8.5)), line((0, 7), (3, 8.5)), line((0, -7), (-3, -8.5)), line((0, -7), (3, -8.5))])
sym('ob_kladka', 'Przejście piesze nad torami kolejowymi (kładka)', 'zał. 8, s. 42', ANCH_OP,
    [line((-10, 0), (10, 0)), line((-BOK, -10), (-BOK, 10)), line((BOK, -10), (BOK, 10)), line((-BOK, -10), (BOK, -10)), line((-BOK, 10), (BOK, 10))] +
    [line((-BOK, y), (BOK, y)) for i in range(1, 5) for y in (-10 + 0.8 * i, 10 - 0.8 * i)])
def mur(n):
    res = [line((0, 3), (40, 3)), line((0, 4), (40, 4))]
    for g in range(4):
        for j in range(n): x = 5 + 10 * g + 0.5 * j; res.append(line((x, 3), (x + 0.5, 4)))
    return res
sym('ob_sciana', 'Ściana oporowa', 'zał. 8, s. 63', 'wzdłuż toru po stronie ściany, w odległości od osi z panelu; grupy 3 kresek co 10 mm', [line((-5, 0), (45, 0))] + mur(3))
sym('ob_mur', 'Mur oporowy', 'zał. 8, s. 63', 'wzdłuż toru po stronie muru, w odległości od osi z panelu; grupy 2 kresek co 10 mm', [line((-5, 0), (45, 0))] + mur(2))

# Nastawnie (s. 35–39, warstwa „budynek”): obrys 0,5 mm, piętrowa – druga ściana 1 mm wewnątrz; kreskowanie co 1 mm: mechaniczna – skośne,
# elektryczna – kratka skośna, komputerowa – kratka prostopadła, ręczna – bez; belka 1 mm grubości 2 mm od ściany od strony toru,
# kropka średnicy 1 mm 1 mm nad belką. Na podglądzie: tor y = 0, budynek 20 × 10 mm (piętrowa 20 × 12) w odległości 3 mm.
def clip(x0, y0, x1, y1, px, py, dx, dy):
    """odcinek prostej (px, py) + t (dx, dy) w prostokącie (Liang-Barsky); None – poza"""
    t0, t1 = -1e9, 1e9
    for p, q in ((-dx, px - x0), (dx, x1 - px), (-dy, py - y0), (dy, y1 - py)):
        if abs(p) < 1e-12:
            if q < 0: return None
            continue
        t = q / p
        if p < 0: t0 = max(t0, t)
        else: t1 = min(t1, t)
    if t1 - t0 < 1e-6: return None
    return ((round(px + t0 * dx, 3), round(py + t0 * dy, 3)), (round(px + t1 * dx, 3), round(py + t1 * dy, 3)))
def rect(x0, y0, x1, y1, w): return {'t': 'poly', 'pts': [[x0, y0], [x1, y0], [x1, y1], [x0, y1]], 'fill': None, 'w': w}
def nastawnia(piet, urz):
    X0, X1, Y0 = 0.0, 20.0, 3.0; Y1 = Y0 + (12.0 if piet else 10.0)
    res = [line((-2, 0), (22, 0)), rect(X0, Y0, X1, Y1, 0.5)]
    a0, b0, a1, b1 = X0, Y0, X1, Y1
    if piet:
        a0, b0, a1, b1 = X0 + 1, Y0 + 1, X1 - 1, Y1 - 1; res.append(rect(a0, b0, a1, b1, 0.5))
    dirs = {'r': [], 'm': [(1, 1)], 'e': [(1, 1), (1, -1)], 'k': [(1, 0), (0, 1)]}[urz]
    cx, cy = (a0 + a1) / 2, (b0 + b1) / 2
    for dx, dy in dirs:
        n = (dx * dx + dy * dy) ** 0.5; dx, dy = dx / n, dy / n; nx, ny = -dy, dx
        for i in range(-30, 31):
            s = clip(a0, b0, a1, b1, cx + nx * i, cy + ny * i, dx, dy)
            if s: res.append(line(s[0], s[1]))
    bl = (a1 - a0) * 0.6; yb = b0 + 2
    res.append(poly((cx - bl / 2 - 0.25, yb - 0.25), (cx + bl / 2 + 0.25, yb - 0.25), (cx + bl / 2 + 0.25, yb + 1.25), (cx - bl / 2 - 0.25, yb + 1.25), fill='paper', w=0))
    res.append(poly((cx - bl / 2, yb), (cx + bl / 2, yb), (cx + bl / 2, yb + 1), (cx - bl / 2, yb + 1), w=0))
    res.append({'t': 'circle', 'at': [cx, yb + 2.5], 'r': 0.75, 'fill': 'paper', 'w': 0})
    res.append({'t': 'circle', 'at': [cx, yb + 2.5], 'r': 0.5, 'fill': 'ink', 'w': 0})
    return res
ANCH_NA = 'budynek z boku toru: km od–do = długość, odległość ściany od osi toru i głębokość z panelu; belka od strony toru; znak nie mniejszy niż 10 × 6 mm (piętrowa 10 × 8 mm)'
for piet, pn in ((False, 'parterowa'), (True, 'piętrowa')):
    for urz, un, ig in (('r', 'ręczna', (35, 37)), ('m', 'mechaniczna', (36, 37)), ('e', 'elektryczna', (36, 38)), ('k', 'komputerowa', (38, 39))):
        sym(f"ob_nastawnia_{'p' if piet else 'n'}_{urz}", f'Nastawnia {pn} {un}', f'zał. 8, s. {ig[1] if piet else ig[0]}', ANCH_NA, nastawnia(piet, urz))

out = os.path.join(os.path.dirname(__file__), '..', 'app', 'Lcs.EdytorUT', 'Dane', 'symbole_ig10.json')
def one(x): return json.dumps(x, ensure_ascii=False, separators=(', ', ': '))
NL = chr(10)
parts = []
for d in S:
    head = ("," + NL).join(f'  "{k}": {one(d[k])}' for k in ("id", "name", "ig10", "anchor"))
    prims = ("," + NL).join("    " + one(p) for p in d["prims"])
    parts.append("{" + NL + head + "," + NL + '  "prims": [' + NL + prims + NL + "  ]" + NL + "}")
with open(out, "w", encoding="utf-8") as f: f.write("[" + NL + ("," + NL).join(parts) + NL + "]" + NL)
print(len(S), "symboli ->", os.path.abspath(out))
