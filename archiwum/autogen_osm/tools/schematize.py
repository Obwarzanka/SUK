"""Etap 2: prostowanie schematu – stałe poziomy torów jak na planie schematycznym / pulpicie LCS.
Użycie: python schematize.py <layout.json> <out.json> [preview.png]

Poziomy (y) torów: kolejność wg planu schematycznego st. Łódź Widzew:
  zaplecze ŁKA (21–28) | 200 | 8 | 6 | 4 | 204 | 2 | 1 | 3 | 5 | 7 | grupa towarowa (1xx, 31x)
Linie 458 i 541 – para torów pod torami głównymi linii 17, poza obszarem stacji.
"""
import json, sys, statistics, bisect

STEP = 30
STATION = {'8': 300, '6': 330, '4': 360, '204': 392, '204a': 392, '2': 430, '1': 460, '3': 520, '5': 550, '7': 610, '200': 270, '7b': 640}
DEPOT = {'21': 240, '22': 216, '23': 192, '24': 168, '25': 144, '26': 120, '27': 96, '28': 72}
SIDE_LINES = {('458', '2'): 490, ('458', '1'): 520, ('541', '2'): 490, ('541', '1'): 520}


def lane_rule(e):
    t, ln = e['track'], e['line']
    if e['len'] < 35:
        return None
    if ln in ('458', '541') and (ln, t) in SIDE_LINES:
        return SIDE_LINES[(ln, t)]
    if ln in ('16', '540') and t in ('1', '2') and ln != '' and not (ln == '16' and t == '4'):
        return None
    if t in STATION:
        return STATION[t]
    if t in DEPOT:
        return DEPOT[t]
    return None


def main(src, dst, png=None):
    L = json.load(open(src, encoding='utf-8'))
    N, E = L['nodes'], L['edges']
    oy = {n: v['y'] for n, v in N.items()}
    for e in E:
        e['omed'] = statistics.median(p[1] for p in e['pts'])
        e['lane'] = lane_rule(e)
        xs = [p[0] for p in e['pts']]
        e['x0'], e['x1'] = min(xs), max(xs)
    lane_edges = [e for e in E if e['lane'] is not None]

    inc = {}
    for e in E:
        inc.setdefault(e['a'], []).append(e); inc.setdefault(e['b'], []).append(e)

    def interp(x, y):
        pairs = sorted({(round(e['omed'], 1), e['lane']) for e in lane_edges if e['x0'] - 160 <= x <= e['x1'] + 160})
        if not pairs:
            return y
        # zachowanie monotoniczności
        mono = []
        for p in pairs:
            if not mono or (p[0] > mono[-1][0] and p[1] > mono[-1][1]):
                mono.append(p)
        ys = [p[0] for p in mono]
        i = bisect.bisect_left(ys, y)
        if i == 0:
            return mono[0][1] - (mono[0][0] - y)
        if i >= len(mono):
            return mono[-1][1] + (y - mono[-1][0])
        (a0, b0), (a1, b1) = mono[i - 1], mono[i]
        return b0 + (y - a0) / (a1 - a0) * (b1 - b0)

    newy = {}
    for n, v in N.items():
        es = [e for e in inc.get(n, []) if e['lane'] is not None]
        if es:
            best = min(es, key=lambda e: abs(e['omed'] - oy[n]))
            if abs(best['omed'] - oy[n]) < 22:
                newy[n] = best['lane']; continue
        newy[n] = round(interp(v['x'], oy[n]) / 6) * 6
    for n in N:
        N[n]['y'] = newy[n]

    for e in E:
        A, B = N[e['a']], N[e['b']]
        ax, ay, bx, by = A['x'], A['y'], B['x'], B['y']
        if e['lane'] is None:
            e['pts'] = [[ax, ay], [bx, by]]
            continue
        ln = e['lane']; sgn = 1 if bx >= ax else -1
        da, db = abs(ay - ln), abs(by - ln)
        span = abs(bx - ax)
        if da + db > span * 0.9:
            f = span * 0.9 / max(1e-6, da + db); da *= f; db *= f
        pts = [[ax, ay]]
        if abs(ay - ln) > 0.5: pts.append([ax + sgn * da, ln])
        if abs(by - ln) > 0.5: pts.append([bx - sgn * db, ln])
        pts.append([bx, by])
        e['pts'] = pts
    for e in E:
        for k in ('omed', 'lane', 'x0', 'x1'):
            e.pop(k, None)
    miny = min(v['y'] for v in N.values())
    for v in N.values():
        v['y'] = v['y'] - miny + 40
    for e in E:
        e['pts'] = [[p[0], p[1] - miny + 40] for p in e['pts']]
    json.dump(L, open(dst, 'w', encoding='utf-8'), ensure_ascii=False, indent=0)
    if png:
        from PIL import Image, ImageDraw
        H = int(max(n['y'] for n in N.values()) + 60); W = int(max(n['x'] for n in N.values()) + 60)
        img = Image.new('RGB', (W, H)); dr = ImageDraw.Draw(img)
        for e in E:
            dr.line([tuple(p) for p in e['pts']], fill=(255, 255, 255) if e['usage'] == 'main' else (160, 160, 160), width=3)
            if e['track'] and len(e['pts']) > 1:
                m = e['pts'][len(e['pts']) // 2]; dr.text((m[0], m[1] - 12), e['track'], fill=(110, 110, 255))
        for n in N.values():
            if n['kind'] in ('switch', 'derail', 'buffer_stop'):
                c = {'switch': (0, 255, 0), 'derail': (255, 0, 255), 'buffer_stop': (255, 160, 0)}[n['kind']]
                dr.ellipse([n['x'] - 2, n['y'] - 2, n['x'] + 2, n['y'] + 2], fill=c)
                if n['ref']: dr.text((n['x'] + 2, n['y'] + 2), n['ref'], fill=c)
        img.save(png)


if __name__ == '__main__':
    main(*sys.argv[1:])
