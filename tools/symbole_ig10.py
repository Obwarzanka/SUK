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
nr_rz = text(-4.0, 2.4)
wedge = lambda x: H * x / L

S = []
def sym(id, name, ig, anchor, prims): S.append({'id': id, 'name': name, 'ig10': ig, 'anchor': anchor, 'prims': prims})

ANCH_RZ = 'początek rozjazdu (PR), x w stronę KR, y w stronę odgałęzienia'
sym('rz_uzal_wprost', 'Rozjazd zwyczajny ze zwrotnicą uzależnioną – zasadniczo na tor główny', 'zał. 8, s. 18', ANCH_RZ,
    [tick, poly((0, 0), (L, 0), (L, H)), nr_rz])
sym('rz_uzal_zwrot', 'Rozjazd zwyczajny ze zwrotnicą uzależnioną – zasadniczo na tor odgałęźny', 'zał. 8, s. 18', ANCH_RZ,
    [tick, poly((0, 0), (L, 0.7), (L, H)), nr_rz])
sym('rz_nieuzal', 'Rozjazd zwyczajny ze zwrotnicą nieuzależnioną', 'zał. 8, s. 19', ANCH_RZ,
    [tick, poly((0, 0), (L, 0), (L, H), fill='paper')] + hatch(2.0, L, lambda x: 0, wedge) + [nr_rz])
sym('rz_nieczynny', 'Rozjazd zwyczajny nieoddany do użytku', 'zał. 8, s. 19', ANCH_RZ,
    [tick, poly((0, 0), (L, 0), (L, H), fill='paper'), nr_rz])

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
