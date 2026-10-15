"""
Konwerter: dane OpenStreetMap (Overpass JSON) -> schemat torowy dla symulatora LCS.

Dane OSM (c) współtwórcy OpenStreetMap, licencja ODbL.

Kroki:
 1. tory railway=rail w obszarze stacji, podział dróg na krawędzie w węzłach kluczowych
    (rozjazdy, kozły, wykolejnice, styki dróg),
 2. obrót do osi stacji (linia 17), skala skażona: oś X rozciągnięta w głowicach,
    oś Y mocno powiększona (rozsunięcie torów),
 3. zapis JSON + podgląd PNG.

Użycie:  python osm2layout.py <overpass.json> <out.json> [preview.png]
"""
import json
import math
import sys

BBOX = dict(lon0=19.503, lon1=19.582, lat0=51.7555, lat1=51.7715)
SKIP_NAMES = ('Kolej Dużych',)
CORRIDOR = 95.0  # m – szerokość pasu wokół osi stacji


def main(src, dst, png=None):
    d = json.load(open(src, encoding='utf-8'))
    N = {e['id']: e for e in d['elements'] if e['type'] == 'node'}
    ways = [e for e in d['elements'] if e['type'] == 'way'
            and e.get('tags', {}).get('railway') == 'rail'
            and not any(s in e['tags'].get('name', '') for s in SKIP_NAMES)]
    plats = [e for e in d['elements'] if e['type'] == 'way' and e.get('tags', {}).get('railway') == 'platform']

    # linia odniesienia: tor nr 1 linii 17 (rzut krzywoliniowy – prostuje łuk stacji)
    lat0 = (BBOX['lat0'] + BBOX['lat1']) / 2
    kx0 = math.cos(math.radians(lat0)) * 111320
    def m0(n):
        p = N[n]
        return ((p['lon'] - BBOX['lon0']) * kx0, (p['lat'] - BBOX['lat0']) * 110540)
    refn = [n for w in ways if w['tags'].get('ref') == '17' and w['tags'].get('railway:track_ref') == '1' for n in w['nodes'] if n in N]
    rp = [m0(n) for n in refn]
    cx = sum(p[0] for p in rp) / len(rp); cy = sum(p[1] for p in rp) / len(rp)
    a0 = math.atan2(sum((p[0] - cx) * (p[1] - cy) for p in rp), sum((p[0] - cx) ** 2 for p in rp))
    ux, uy = math.cos(a0), math.sin(a0)
    rp = sorted(set(rp), key=lambda p: (p[0] - cx) * ux + (p[1] - cy) * uy)
    ref = [rp[0]]
    for p in rp[1:]:
        if math.dist(p, ref[-1]) > 15: ref.append(p)
    cum = [0.0]
    for i in range(1, len(ref)): cum.append(cum[-1] + math.dist(ref[i - 1], ref[i]))
    PROJ = {}
    def proj(n):
        if n in PROJ: return PROJ[n]
        x, y = m0(n); best = None
        for i in range(len(ref) - 1):
            (x1, y1), (x2, y2) = ref[i], ref[i + 1]
            dx, dy = x2 - x1, y2 - y1; L2 = dx * dx + dy * dy
            t = max(0.0, min(1.0, ((x - x1) * dx + (y - y1) * dy) / L2))
            px, py = x1 + t * dx, y1 + t * dy
            dd = math.hypot(x - px, y - py)
            if best is None or dd < best[0]:
                side = 1 if (dx * (y - y1) - dy * (x - x1)) > 0 else -1
                best = (dd, cum[i] + t * math.sqrt(L2), side * dd, t, i)
        r = best
        # poza końcami linii odniesienia – ekstrapolacja wzdłuż stycznej
        PROJ[n] = (r[1], r[2])
        return PROJ[n]
    def inside(n):
        p = N[n]
        if not (BBOX['lon0'] <= p['lon'] <= BBOX['lon1'] and BBOX['lat0'] <= p['lat'] <= BBOX['lat1']): return False
        s_, d_ = proj(n)
        return abs(d_) < CORRIDOR and 1.0 < s_ < cum[-1] - 1.0

    # przycięcie dróg do obszaru (zachowujemy ciągłe fragmenty wewnątrz)
    pieces = []
    for w in ways:
        cur = []
        for n in w['nodes']:
            if n in N and inside(n):
                cur.append(n)
            else:
                if len(cur) > 1:
                    pieces.append((w, cur, True))
                cur = []
        if len(cur) > 1:
            pieces.append((w, cur, True))

    use = {}
    for w, nodes, _ in pieces:
        for i, n in enumerate(nodes):
            use[n] = use.get(n, 0) + (1 if 0 < i < len(nodes) - 1 else 1)
    key = set()
    for w, nodes, _ in pieces:
        key.add(nodes[0]); key.add(nodes[-1])
    for n, c in use.items():
        t = N[n].get('tags', {}).get('railway')
        if c > 1 or t in ('switch', 'buffer_stop', 'derail', 'railway_crossing'):
            key.add(n)

    edges = []
    for w, nodes, _ in pieces:
        t = w.get('tags', {})
        seg = [nodes[0]]
        for n in nodes[1:]:
            seg.append(n)
            if n in key:
                edges.append(dict(nodes=seg, track=t.get('railway:track_ref', ''), line=t.get('ref', ''),
                                  service=t.get('service', ''), usage=t.get('usage', ''), way=w['id'],
                                  maxspeed=t.get('maxspeed', '')))
                seg = [n]

    # współrzędne metryczne
    lat0 = (BBOX['lat0'] + BBOX['lat1']) / 2
    kx = math.cos(math.radians(lat0)) * 111320
    ky = 110540

    def xy(n):
        p = N[n]
        return ((p['lon'] - BBOX['lon0']) * kx, (p['lat'] - BBOX['lat0']) * ky)

    def rot(n):
        return proj(n)

    used = sorted({n for e in edges for n in e['nodes']})
    R = {n: rot(n) for n in used}
    xs = [R[n][0] for n in key if n in R]
    xmin, xmax = min(xs), max(xs)

    # skala skażona X: gęstość węzłów kluczowych -> szersze głowice
    BIN = 25.0
    nb = int((xmax - xmin) / BIN) + 2
    hist = [0] * nb
    for n in key:
        if n in R:
            hist[int((R[n][0] - xmin) / BIN)] += 1
    w = [1.0 + 2.2 * min(h, 4) for h in hist]
    cum = [0.0]
    for v in w:
        cum.append(cum[-1] + v)
    W_OUT, MARGIN = 2300.0, 60.0

    def X(x):
        f = (x - xmin) / BIN
        i = max(0, min(nb - 1, int(f)))
        v = cum[i] + (f - i) * w[i]
        return MARGIN + v / cum[-1] * (W_OUT - 2 * MARGIN)

    SY = 6.2

    def Y(y):
        return -y * SY

    P = {n: (round(X(R[n][0]), 1), round(Y(R[n][1]), 1)) for n in used}
    ys = [p[1] for p in P.values()]
    ymin = min(ys)
    P = {n: (p[0], round(p[1] - ymin + 50, 1)) for n, p in P.items()}

    out_nodes = {}
    for n in used:
        t = N[n].get('tags', {})
        out_nodes[str(n)] = dict(x=P[n][0], y=P[n][1], kind=t.get('railway', ''), ref=t.get('ref', ''),
                                 key=n in key, lat=N[n]['lat'], lon=N[n]['lon'])
    out_edges = []
    for i, e in enumerate(edges):
        out_edges.append(dict(id='e%d' % i, a=str(e['nodes'][0]), b=str(e['nodes'][-1]),
                              pts=[P[n] for n in e['nodes']],
                              len=round(sum(math.dist(xy(e['nodes'][j]), xy(e['nodes'][j + 1])) for j in range(len(e['nodes']) - 1)), 1),
                              track=e['track'], line=e['line'], service=e['service'], usage=e['usage'], maxspeed=e['maxspeed']))
    out_plat = []
    for p in plats:
        ns = [n for n in p['nodes'] if n in N and inside(n)]
        if len(ns) < 3:
            continue
        pp = []
        for n in ns:
            x, y = rot(n)
            pp.append((round(X(x), 1), round(Y(y) - ymin + 50, 1)))
        out_plat.append(dict(ref=p.get('tags', {}).get('ref', ''), name=p.get('tags', {}).get('name', ''), pts=pp))

    json.dump(dict(source='OpenStreetMap (ODbL), Overpass API', bbox=BBOX, nodes=out_nodes, edges=out_edges, platforms=out_plat),
              open(dst, 'w', encoding='utf-8'), ensure_ascii=False, indent=0)
    print('węzły', len(out_nodes), 'krawędzie', len(out_edges), 'perony', len(out_plat))

    if png:
        from PIL import Image, ImageDraw
        H = int(max(p[1] for p in P.values()) + 60)
        img = Image.new('RGB', (int(W_OUT), H), (0, 0, 0))
        dr = ImageDraw.Draw(img)
        for pl in out_plat:
            dr.polygon(pl['pts'], outline=(80, 80, 80))
        for e in out_edges:
            col = (255, 255, 255) if e['usage'] == 'main' else (150, 150, 150)
            dr.line(e['pts'], fill=col, width=3)
            if e['track'] and len(e['pts']) > 1:
                m = e['pts'][len(e['pts']) // 2]
                dr.text((m[0], m[1] - 12), e['track'], fill=(110, 110, 255))
        for n, v in out_nodes.items():
            if v['kind'] in ('switch', 'derail', 'buffer_stop'):
                c = {'switch': (0, 255, 0), 'derail': (255, 0, 255), 'buffer_stop': (255, 160, 0)}[v['kind']]
                dr.ellipse([v['x'] - 2, v['y'] - 2, v['x'] + 2, v['y'] + 2], fill=c)
                if v['ref']:
                    dr.text((v['x'] + 2, v['y'] + 2), v['ref'], fill=c)
        img.save(png)


if __name__ == '__main__':
    main(*sys.argv[1:])
