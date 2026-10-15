"""
Etap 3: model stacji dla aplikacji LcsWidzew (semafory, odcinki, szlaki, perony).

Topologia, numery zwrotnic, torów i wykolejnic pochodzą z OpenStreetMap (ODbL).
UZUPEŁNIONE ZAŁOŻENIA (do weryfikacji z regulaminem technicznym ŁW):
 - semafory wyjazdowe L/S/M/R wg schematu PKP PLK (2013), położone ok. 1/10 długości toru od jego końca,
 - semafory wjazdowe – nazwy umowne (A..G od zachodu, W..Z od wschodu),
 - typy blokad liniowych, długości szlaków, granice odcinków izolowanych.

Użycie: python build_station.py <widzew_schem.json> <out station.json> [preview.png]
"""
import json, math, sys, copy

# tory stacyjne: (krawędź OSM z końcem zachodnim, krawędź z końcem wschodnim, nazwa zach., nazwa wsch.)
STATION_TRACKS = {
    '1': ('e134', 'e144', 'L1', 'S1'), '2': ('e146', 'e89', 'L2', 'S2'), '3': ('e12', 'e94', 'L3', 'S3'),
    '4': ('e97', 'e98', 'M4', 'R4'), '5': ('e93', 'e8', 'L5', 'S5'), '6': ('e80', 'e80', 'M6', 'R6'),
    '7': ('e7', 'e6', 'L7', 'S7'), '8': ('e15', 'e15', 'M8', 'R8'),
}
NEIGHBOURS = {('W', '17'): 'Łódź Fabryczna', ('W', '458'): 'Łódź Fabryczna (l. 458)', ('W', '16'): 'Łódź Stoki',
              ('W', '540'): 'Łódź Dąbrowa', ('E', '17'): 'Łódź Andrzejów', ('E', '541'): 'Łódź Olechów'}
PLATFORMS = [('Peron 3', '4', '2'), ('Peron 2', '1', '3'), ('Peron 1', '5', '7')]


class G:
    def __init__(self, L):
        self.N = {k: dict(v, id=k) for k, v in L['nodes'].items()}
        self.E = {e['id']: e for e in L['edges']}
        self.seq = 0

    def inc(self, n):
        return [e for e in self.E.values() if n in (e['a'], e['b'])]

    def other(self, e, n):
        return e['b'] if e['a'] == n else e['a']

    def plen(self, pts):
        return sum(math.dist(pts[i], pts[i + 1]) for i in range(len(pts) - 1))

    def split(self, eid, frac_from_a, nid=None):
        """Dzieli krawędź w punkcie o ułamku długości schematycznej liczonej od końca a."""
        e = self.E.pop(eid)
        pts = e['pts']; total = self.plen(pts); target = total * frac_from_a
        acc = 0.0
        for i in range(len(pts) - 1):
            d = math.dist(pts[i], pts[i + 1])
            if acc + d >= target:
                t = (target - acc) / d if d else 0
                p = [round(pts[i][0] + (pts[i + 1][0] - pts[i][0]) * t, 1), round(pts[i][1] + (pts[i + 1][1] - pts[i][1]) * t, 1)]
                pa, pb = pts[:i + 1] + [p], [p] + pts[i + 1:]
                break
            acc += d
        self.seq += 1
        nid = nid or 'n%d' % self.seq
        self.N[nid] = dict(id=nid, x=p[0], y=p[1], kind='', ref='', key=True)
        e1 = dict(e, id=e['id'] + 'a', b=nid, pts=pa, len=round(e['len'] * frac_from_a, 1))
        e2 = dict(e, id=e['id'] + 'b', a=nid, pts=pb, len=round(e['len'] * (1 - frac_from_a), 1))
        self.E[e1['id']] = e1; self.E[e2['id']] = e2
        return nid, e1['id'], e2['id']

    def end_at(self, eid, side):
        """Węzeł krawędzi po stronie 'W' (mniejsze x) lub 'E'."""
        e = self.E[eid]; a, b = self.N[e['a']], self.N[e['b']]
        if side == 'W':
            return e['a'] if a['x'] <= b['x'] else e['b']
        return e['a'] if a['x'] > b['x'] else e['b']

    def frac_near(self, eid, node, px):
        e = self.E[eid]; total = self.plen(e['pts'])
        f = min(0.45, px / total)
        return f if e['a'] == node else 1 - f


def main(src, dst, png=None):
    L = json.load(open(src, encoding='utf-8'))
    g = G(L)
    signals, blocks, far_ends, boundaries, track_ends = {}, {}, {}, {}, {}

    # --- rozjazdy krzyżowe (węzeł stopnia 4) -> dwie połówki
    for n in list(g.N):
        v = g.N[n]
        if v['kind'] != 'switch':
            continue
        es = g.inc(n)
        if len(es) != 4:
            continue
        west = [e for e in es if g.N[g.other(e, n)]['x'] < v['x']]
        east = [e for e in es if e not in west]
        if len(west) != 2:
            print('rozjazd krzyżowy – nietypowy', v['ref']); continue
        nb = n + 'cd'
        g.N[nb] = dict(v, id=nb, x=v['x'] + 8, ref=v['ref'] + 'cd')
        v['ref'] = v['ref'] + 'ab'
        for e in east:
            if e['a'] == n:
                e['a'] = nb; e['pts'][0] = [g.N[nb]['x'], g.N[nb]['y']]
            else:
                e['b'] = nb; e['pts'][-1] = [g.N[nb]['x'], g.N[nb]['y']]
        g.E['x' + n] = dict(id='x' + n, a=n, b=nb, pts=[[v['x'], v['y']], [v['x'] + 8, v['y']]], len=6.0, track='', line='', service='', usage='', maxspeed='', dslip=True)

    # --- semafory wyjazdowe
    for tr, (ew, ee, nw, ne) in STATION_TRACKS.items():
        node_w = g.end_at(ew, 'W')
        f = g.frac_near(ew, node_w, 26)
        sid, e1, e2 = g.split(ew, f, 'sig_' + nw)
        signals[nw] = dict(name=nw, node=sid, dir=-1, type='wyjazdowy', track=tr)
        if ee == ew:
            ee = e2 if g.N[g.E[e2]['b']]['x'] > g.N[g.E[e2]['a']]['x'] or True else e1
            ee = [x for x in (e1, e2) if g.end_at(x, 'E') != sid][0]
        node_e = g.end_at(ee, 'E')
        f = g.frac_near(ee, node_e, 26)
        sid2, _, _ = g.split(ee, f, 'sig_' + ne)
        signals[ne] = dict(name=ne, node=sid2, dir=1, type='wyjazdowy', track=tr)

    # --- granice: szlaki i końce torów
    deg = {}
    for e in g.E.values():
        deg[e['a']] = deg.get(e['a'], 0) + 1; deg[e['b']] = deg.get(e['b'], 0) + 1
    ends = [n for n, d in deg.items() if d == 1]
    bnd = []
    for n in ends:
        e = g.inc(n)[0]
        if g.N[n]['kind'] != 'buffer_stop' and e['line'] in ('16', '17', '458', '540', '541'):
            side = 'W' if g.N[n]['x'] < 1150 else 'E'
            bnd.append((side, g.N[n]['y'], n, e))
        else:
            track_ends[n] = dict(id='K' + (e['track'] or n[-3:]), label='koniec toru ' + (e['track'] or ''))
    west = sorted([b for b in bnd if b[0] == 'W'], key=lambda b: b[1])
    east = sorted([b for b in bnd if b[0] == 'E'], key=lambda b: b[1])
    names = {id(b): c for b, c in zip(west, 'ABCDEFGH')}
    names.update({id(b): c for b, c in zip(east, 'ZYXWV')})
    for b in west + east:
        side, _, n, e = b
        letter = names[id(b)]
        inward = 1 if side == 'W' else -1
        line, tr = e['line'], e['track'] or '1'
        szlak = ('W' if side == 'W' else 'E') + line + '_' + tr
        nbr = NEIGHBOURS.get((side, line), 'posterunek sąsiedni')
        inner = g.other(e, n)
        f = g.frac_near(e['id'], inner, 34)
        sid, e1, e2 = g.split(e['id'], f, 'sig_' + letter)
        signals[letter] = dict(name=letter, node=sid, dir=inward, type='wjazdowy', szlak=szlak)
        outer = [x for x in (e1, e2) if n in (g.E[x]['a'], g.E[x]['b'])][0]
        single = line == '16'
        if single:
            g.E[outer]['len'] = 3000.0
            g.E[outer]['sec'] = szlak
            secs = [szlak]
        else:
            o_mid, oa, ob = g.split(outer, 0.5, 'od_' + szlak)
            near = [x for x in (oa, ob) if sid in (g.E[x]['a'], g.E[x]['b'])][0]
            farE = [x for x in (oa, ob) if x != near][0]
            g.E[near]['len'] = 1600.0; g.E[farE]['len'] = 1800.0
            g.E[near]['sec'] = szlak + '-1'; g.E[farE]['sec'] = szlak + '-2'
            secs = [szlak + '-1', szlak + '-2']
            signals[szlak + '/o+'] = dict(name=szlak + '/o+', node=o_mid, dir=1, type='odstepowy', szlak=szlak, auto=True)
            signals[szlak + '/o-'] = dict(name=szlak + '/o-', node=o_mid, dir=-1, type='odstepowy', szlak=szlak, auto=True)
        boundaries[sid] = dict(szlak=szlak)
        far_ends[n] = dict(szlak=szlak, nbr=nbr)
        default_dir = (1 if tr == '1' else -1)  # ruch prawostronny: tor 1 na wschód, tor 2 na zachód
        blocks[szlak] = dict(id=szlak, type='psb' if single else 'sbl', nbr=nbr, side=side, outward=-inward, secs=secs,
                             defDir=default_dir, defPerm='local', entrySig=letter, boundary=sid, far=n, tor=tr, line=line)

    # --- odcinki zwrotnicowe: dzielenie długich krawędzi przy zwrotnicach
    sw_nodes = {n for n, v in g.N.items() if v['kind'] == 'switch'}
    for eid in list(g.E):
        e = g.E.get(eid)
        if not e or e.get('sec'):
            continue
        a_sw, b_sw = e['a'] in sw_nodes, e['b'] in sw_nodes
        if a_sw and b_sw and not e.get('dslip'):
            g.split(eid, 0.5)
        elif (a_sw or b_sw) and e['len'] > 140:
            n = e['a'] if a_sw else e['b']
            g.split(eid, g.frac_near(eid, n, 22))
    # przypisanie odcinków
    def swname(n):
        r = g.N[n]['ref']
        return r[:-2] if r.endswith(('ab', 'cd')) else r
    for e in g.E.values():
        if e.get('sec'):
            continue
        for n in (e['a'], e['b']):
            if n in sw_nodes and (e.get('dslip') or e['len'] <= 140 or True):
                if n in sw_nodes and any(n == x for x in (e['a'], e['b'])):
                    pass
        cand = [n for n in (e['a'], e['b']) if n in sw_nodes]
        if e.get('dslip'):
            e['sec'] = 'z' + swname(e['a'])
        elif cand and (e['len'] <= 140 or len(cand) == 1):
            # krawędź przy zwrotnicy (po podziale – krótki kawałek)
            n = cand[0]
            other_n = g.other(e, n)
            if e['len'] <= 140 or other_n.startswith('n'):
                e['sec'] = 'z' + swname(n)
    # pozostałe: łączenie w odcinki torowe (granice: zwrotnice, semafory, końce)
    sig_nodes = {s['node'] for s in signals.values()}
    parent = {}
    def find(x):
        while parent.get(x, x) != x:
            x = parent[x]
        return x
    rest = [e for e in g.E.values() if not e.get('sec')]
    for e in rest:
        parent[e['id']] = e['id']
    bynode = {}
    for e in rest:
        bynode.setdefault(e['a'], []).append(e['id']); bynode.setdefault(e['b'], []).append(e['id'])
    for n, es in bynode.items():
        if n in sw_nodes or n in sig_nodes:
            continue
        for x in es[1:]:
            parent[find(x)] = find(es[0])
    groups = {}
    for e in rest:
        groups.setdefault(find(e['id']), []).append(e)
    used = {}
    for k, es in groups.items():
        tr = max((x['track'] for x in es), key=lambda t: sum(1 for x in es if x['track'] == t)) or 'x'
        base = 'it' + tr
        used[base] = used.get(base, 0) + 1
        name = base + ('' if used[base] == 1 else chr(ord('a') + used[base] - 2))
        for x in es:
            x['sec'] = name
    # nazwy odcinków torowych na torach stacyjnych: „JT1” itd. – dla krawędzi między semaforami
    for tr, (ew, ee, nw, ne) in STATION_TRACKS.items():
        a, b = signals[nw]['node'], signals[ne]['node']
        for e in g.E.values():
            if e['sec'].startswith('it') and e['track'] == tr and min(g.N[e['a']]['x'], g.N[e['b']]['x']) >= g.N[a]['x'] - 1 and max(g.N[e['a']]['x'], g.N[e['b']]['x']) <= g.N[b]['x'] + 1:
                old = e['sec']
                for e2 in g.E.values():
                    if e2['sec'] == old:
                        e2['sec'] = 'JT' + tr
                break

    # wykolejnice
    derailers = {}
    for n, v in g.N.items():
        if v['kind'] == 'derail' and v['ref']:
            es = g.inc(n)
            derailers[v['ref']] = dict(name=v['ref'], node=n, sec=es[0]['sec'] if es else '')

    switches = {}
    for n in sw_nodes:
        r = g.N[n]['ref']
        if not r:
            continue
        if len(g.inc(n)) != 3:
            print('pominięto zwrotnicę', r, 'stopień', len(g.inc(n)))
            continue
        switches[r] = dict(num=r, node=n, sec='z' + swname(n), manual=not r.rstrip('abcd').isdigit() or int(r.rstrip('abcd')) >= 100)

    # perony – prostokąty między torami (na odcinkach JT)
    plats = []
    def lane_y(tr):
        es = [e for e in g.E.values() if e['sec'] == 'JT' + tr]
        ys = sorted(p[1] for e in es for p in e['pts'])
        return ys[len(ys) // 2]
    for name, t1, t2 in PLATFORMS:
        y1, y2 = sorted((lane_y(t1), lane_y(t2)))
        x1 = 1110; x2 = 1430
        plats.append(dict(name=name, x=x1, y=y1 + 7, w=x2 - x1, h=y2 - y1 - 14))

    labels = []
    for n, f in far_ends.items():
        v = g.N[n]
        labels.append(dict(x=v['x'] + (6 if v['x'] < 1150 else -6), y=v['y'] - 8, text=f['nbr'], anchor='start' if v['x'] < 1150 else 'end', cls='nbr'))

    for e in g.E.values():
        e['pts'] = [[round(p[0], 1), round(p[1], 1)] for p in e['pts']]
    nodes = {n: dict(x=v['x'], y=v['y'], kind=v['kind'], ref=v['ref']) for n, v in g.N.items() if any(n in (e['a'], e['b']) for e in g.E.values())}
    out = dict(name='Łódź Widzew', code='ŁW', source='Topologia: © współtwórcy OpenStreetMap (ODbL). Semafory, odcinki i blokady – założenia (do weryfikacji).',
               nodes=nodes, edges=list(g.E.values()), switches=switches, signals=signals, derailers=derailers, blocks=blocks,
               boundaries=boundaries, farEnds=far_ends, trackEnds=track_ends, platforms=plats, labels=labels)
    json.dump(out, open(dst, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    secs = sorted({e['sec'] for e in g.E.values()})
    print('węzły', len(nodes), 'krawędzie', len(g.E), 'zwrotnice', len(switches), 'semafory', len(signals), 'odcinki', len(secs), 'szlaki', list(blocks))

    if png:
        from PIL import Image, ImageDraw
        import hashlib
        W = int(max(v['x'] for v in nodes.values()) + 60); H = int(max(v['y'] for v in nodes.values()) + 60)
        img = Image.new('RGB', (W, H)); dr = ImageDraw.Draw(img)
        for p in plats:
            dr.rectangle([p['x'], p['y'], p['x'] + p['w'], p['y'] + p['h']], outline=(120, 120, 120))
        for e in g.E.values():
            h = int(hashlib.md5(e['sec'].encode()).hexdigest()[:6], 16)
            col = (80 + (h & 0x7f), 80 + ((h >> 8) & 0x7f), 80 + ((h >> 16) & 0x7f))
            dr.line([tuple(p) for p in e['pts']], fill=col, width=4)
        for s in signals.values():
            v = nodes[s['node']]
            c = (255, 0, 0) if s['type'] == 'wjazdowy' else ((0, 255, 0) if s['type'] == 'wyjazdowy' else (90, 90, 90))
            dr.polygon([(v['x'] + 7 * s['dir'], v['y'] + 9 * s['dir']), (v['x'] - 5 * s['dir'], v['y'] + 4 * s['dir']), (v['x'] - 5 * s['dir'], v['y'] + 14 * s['dir'])], fill=c)
            if s['type'] != 'odstepowy':
                dr.text((v['x'] - 10, v['y'] + (14 if s['dir'] > 0 else -24)), s['name'], fill=c)
        for r, s in switches.items():
            v = nodes[s['node']]; dr.text((v['x'] + 2, v['y'] + 3), r, fill=(0, 200, 0) if not s['manual'] else (100, 100, 100))
        img.save(png)


if __name__ == '__main__':
    main(*sys.argv[1:])
