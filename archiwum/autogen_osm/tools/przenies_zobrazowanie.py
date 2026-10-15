"""
Przeniesienie zobrazowania (obrazów pulpitu) ze starego układu na nowy układ torowy – PROPOZYCJA do poprawek w edytorze.

Dopasowanie (kotwice): zwrotnice po numerze w obrębie posterunku, sygnalizatory po nazwie, końce torów (kozły) po numerze
toru. Sygnalizatory starego układu, których brak w nowym (np. z planu Semaforka nieodczytane), są przenoszone na nowy
układ: położenie ze starego układu przekształcone na nowy plan, wstawione na najbliższy tor (podział toru).
Zobrazowanie: kotwice dokładnie w miejscach ze starego pulpitu, pozostałe węzły i tory – przekształcenie
nowy plan → stary pulpit (thin-plate spline z kotwic, osobno dla każdego obrazu). Napisy, perony, przejazdy i obrazy
pulpitu – ze starego zobrazowania. Czego nie da się umieścić – „Nieumieszczone na obrazie” w edytorze.

Użycie: python przenies_zobrazowanie.py <stary.json> <nowy_uklad.json> <wynik.json> [raport.txt]
"""
import json
import math
import os
import sys
from collections import defaultdict

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))   # tryb -I: wspólne funkcje z gen_uklad.py
from gen_uklad import _tps


def main(old_path, new_path, out_path, rep_path=None):
    O = json.load(open(old_path, encoding='utf-8'))
    N = json.load(open(new_path, encoding='utf-8'))
    rep = []
    R = lambda *a: rep.append(' '.join(str(x) for x in a))
    OD = O.get('display') or {'nodes': {k: [v['x'], v['y']] for k, v in O['nodes'].items()},
                              'edges': {e['id']: e['pts'] for e in O['edges']}}
    deg = lambda D: (lambda c: c)(defaultdict(int, {k: 0 for k in D['nodes']}))
    def degree(D):
        c = defaultdict(int)
        for e in D['edges']: c[e['a']] += 1; c[e['b']] += 1
        return c
    dO, dN = degree(O), degree(N)

    # ---------------- kotwice: stary węzeł ↔ nowy węzeł
    pairs = {}
    osw = {(s['post'], s['label']): s['node'] for s in O['switches'].values()}
    for s in N['switches'].values():
        k = (s['post'], s['label'])
        if k in osw: pairs[osw[k]] = s['node']
    for name, s in N['signals'].items():
        if name in O['signals']: pairs[O['signals'][name]['node']] = s['node']
    def end_track(D, nid, d):
        if d[nid] != 1: return None
        return next((e['track'] for e in D['edges'] if nid in (e['a'], e['b']) and e['track']), None)
    oends = defaultdict(list)
    for nid in O['nodes']:
        t = end_track(O, nid, dO)
        if t: oends[(O['nodes'][nid].get('post', ''), t)].append(nid)
    for nid in N['nodes']:
        t = end_track(N, nid, dN)
        if t and len(oends.get((N['nodes'][nid]['post'], t), [])) == 1: pairs[oends[(N['nodes'][nid]['post'], t)][0]] = nid
    R('kotwice (zwrotnice, sygnalizatory, końce torów):', len(pairs))

    def post_of_old(n): return O['nodes'][n].get('post') or n.split(':')[0]
    def post_of_new(n): return N['nodes'][n].get('post') or n.split(':')[0]

    # ---------------- przekształcenie stary układ → nowy plan (na posterunek, min. 4 kotwice)
    by_post = defaultdict(list)
    for o, n in pairs.items():
        if o in O['nodes'] and n in N['nodes']: by_post[post_of_new(n)].append((o, n))
    L2N = {}
    for post, pp in by_post.items():
        if len(pp) >= 4:
            L2N[post] = _tps([(O['nodes'][o]['x'], O['nodes'][o]['y']) for o, _ in pp], [(N['nodes'][n]['x'], N['nodes'][n]['y']) for _, n in pp], 1e-3)

    # ---------------- przeniesienie brakujących sygnalizatorów na nowy układ
    def seglen(a, b): return math.dist(a, b)
    def proj_on(pts, p):
        best = None; acc = 0.0; tot = sum(seglen(pts[i], pts[i + 1]) for i in range(len(pts) - 1)) or 1
        for i in range(len(pts) - 1):
            (x1, y1), (x2, y2) = pts[i], pts[i + 1]; dx, dy = x2 - x1, y2 - y1; L2 = dx * dx + dy * dy
            t = 0 if L2 == 0 else max(0, min(1, ((p[0] - x1) * dx + (p[1] - y1) * dy) / L2))
            q = (x1 + t * dx, y1 + t * dy); d = math.dist(p, q)
            if best is None or d < best[0]: best = (d, i, q, (acc + t * math.sqrt(L2)) / tot)
            acc += math.sqrt(L2)
        return best
    otrack = {}
    for e in O['edges']:
        for n in (e['a'], e['b']): otrack.setdefault(n, e['track'])
    moved, skipped = 0, defaultdict(int)
    for name, s in O['signals'].items():
        if name in N['signals']: continue
        if s['type'] == 'odstepowy': skipped['odstępowe SBL (szlaki – nie przenoszone)'] += 1; continue
        post = s.get('post') or name.split(':')[0]
        if post not in L2N: skipped['brak przekształcenia dla posterunku ' + post] += 1; continue
        on = O['nodes'][s['node']]; p = L2N[post]((on['x'], on['y']))
        tr = otrack.get(s['node'], '')
        cand = []
        for e in N['edges']:
            if post_of_new(e['a']) != post and post_of_new(e['b']) != post: continue
            b = proj_on(e['pts'], p)
            pen = 0 if (tr and e['track'] == tr) else 15
            cand.append((b[0] + pen, b, e))
        if not cand: skipped['brak toru'] += 1; continue
        d, b, e = min(cand, key=lambda c: c[0])
        if d > 80: skipped['za daleko od toru (> 80)'] += 1; continue
        # podział toru w punkcie rzutu
        f = min(0.95, max(0.05, b[3]))
        nid = f"{post}:sg{s.get('label', name.split(':')[-1])}"
        while nid in N['nodes']: nid += '_'
        q = [round(b[2][0], 1), round(b[2][1], 1)]
        N['nodes'][nid] = dict(x=q[0], y=q[1], kind='', post=post)
        i = b[1]
        e2 = dict(e, id=e['id'] + 's' + str(moved), a=nid, pts=[q] + e['pts'][i + 1:], len=round(e['len'] * (1 - f), 1))
        e['b'], e['pts'], e['len'] = nid, e['pts'][:i + 1] + [q], round(e['len'] * f, 1)
        N['edges'].append(e2)
        ns = dict(s); ns['node'] = nid
        N['signals'][name] = ns
        pairs[s['node']] = nid; moved += 1
    R('sygnalizatory przeniesione ze starego układu:', moved)
    for k, v in skipped.items(): R('  pominięte –', k + ':', v)

    # ---------------- zobrazowanie: kotwice ze starego pulpitu, reszta – przekształcenie nowy plan → stary pulpit
    page_of_post = {p: pg['id'] for pg in O['pages'] for p in pg['posts']}
    dpos = {}
    anc = defaultdict(list)
    for o, n in pairs.items():
        if n in N['nodes'] and o in OD['nodes'] and post_of_new(n) in page_of_post:
            dpos[n] = tuple(OD['nodes'][o]); anc[page_of_post[post_of_new(n)]].append(n)
    # łańcuchy nowego układu między kotwicami; kształt ze starego pulpitu (droga między tymi samymi kotwicami
    # w starym układzie), węzły pośrednie wg długości. Bez odpowiednika na starym pulpicie – nieumieszczone.
    rp = {n: o for o, n in pairs.items()}
    oadj = defaultdict(list)
    for e in O['edges']: oadj[e['a']].append(e); oadj[e['b']].append(e)
    nadj = defaultdict(list)
    for e in N['edges']: nadj[e['a']].append(e); nadj[e['b']].append(e)
    oanch = set(pairs)
    def oother(e, n): return e['b'] if e['a'] == n else e['a']
    def old_path(o1, o2):
        """Najkrótsza droga w starym układzie z o1 do o2 (przez węzły bez kotwic) – lista (tor, od węzła)."""
        import heapq
        dist = {o1: 0.0}; prev = {}; hq = [(0.0, o1)]
        while hq:
            d, u = heapq.heappop(hq)
            if u == o2: break
            if d > dist.get(u, 1e18) or (u != o1 and u in oanch): continue
            for e in oadj[u]:
                v = oother(e, u); nd = d + e['len']
                if nd < dist.get(v, 1e18): dist[v] = nd; prev[v] = (e, u); heapq.heappush(hq, (nd, v))
        if o2 not in prev: return None
        out, cur = [], o2
        while cur != o1:
            e, u = prev[cur]; out.append((e, u)); cur = u
        return out[::-1]
    def disp_poly(path):
        pts = []
        for e, u in path:
            q = OD['edges'].get(e['id'])
            if not q: return None
            q = q if e['a'] == u else q[::-1]
            pts += q if not pts else q[1:]
        return pts
    def at(pl, f):
        L = [0.0]
        for i in range(len(pl) - 1): L.append(L[-1] + math.dist(pl[i], pl[i + 1]))
        s_ = f * L[-1]
        for i in range(len(pl) - 1):
            if s_ <= L[i + 1] or i == len(pl) - 2:
                t = (s_ - L[i]) / ((L[i + 1] - L[i]) or 1)
                return [round(pl[i][0] + t * (pl[i + 1][0] - pl[i][0]), 1), round(pl[i][1] + t * (pl[i + 1][1] - pl[i][1]), 1)], i
        return pl[-1], len(pl) - 2
    disp_nodes = {n: [round(p[0], 1), round(p[1], 1)] for n, p in dpos.items()}
    placed = len(disp_nodes)
    disp_edges = {}
    nanch = set(dpos)
    done = set()
    for u in list(nanch):
        for e0 in nadj[u]:
            if e0['id'] in done: continue
            seq, es, cur, e = [u], [], u, e0
            while True:
                nxt = e['b'] if e['a'] == cur else e['a']; es.append(e); seq.append(nxt); cur = nxt
                if cur in nanch or len(nadj[cur]) != 2: break
                e = next(x for x in nadj[cur] if x is not e)
            for x in es: done.add(x['id'])
            v = seq[-1]
            if v not in nanch: continue
            path = old_path(rp[u], rp[v])
            pl = disp_poly(path) if path else None
            if not pl: pl = [disp_nodes[u], disp_nodes[v]]
            cum = [0.0]
            for x in es: cum.append(cum[-1] + x['len'])
            tot = cum[-1] or 1
            cuts = []
            for i in range(len(es) + 1):
                p, k = at(pl, cum[i] / tot); cuts.append((p, k))
                if seq[i] not in disp_nodes: disp_nodes[seq[i]] = p
            for i, x in enumerate(es):
                (pa, ia), (pb, ib) = cuts[i], cuts[i + 1]
                poly = [disp_nodes[seq[i]]] + [list(q) for q in pl[ia + 1:ib + 1]] + [disp_nodes[seq[i + 1]]]
                disp_edges[x['id']] = poly if x['a'] == seq[i] else poly[::-1]
    R(f'zobrazowanie: węzły {len(disp_nodes)} z {len(N["nodes"])} (dokładnie ze starego pulpitu {placed}), tory {len(disp_edges)} z {len(N["edges"])}')

    N['display'] = dict(nodes=disp_nodes, edges=disp_edges)
    for k in ('labels', 'platforms', 'crossings'):
        N['display'][k] = (O.get('display') or {}).get(k, O.get(k, []))
    N['pages'] = O['pages'] + [p for p in N['pages'] if not any(pp in page_of_post for pp in p['posts'])]
    N['posts'] = dict(N['posts'], **O['posts'])
    N['name'] = N.get('name', '') + ' + zobrazowanie ze starego układu'
    N['source'] = N.get('source', '') + ' Zobrazowanie przeniesione: tools/przenies_zobrazowanie.py.'
    json.dump(N, open(out_path, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    txt = '\n'.join(rep); print(txt)
    if rep_path: open(rep_path, 'w', encoding='utf-8').write(txt)


if __name__ == '__main__':
    main(*sys.argv[1:])
