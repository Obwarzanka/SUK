"""
Generator układu torowego (plan schematyczny) z OpenStreetMap / OpenRailwayMap – PROPOZYCJA do poprawek w edytorze.

Dane OSM (c) współtwórcy OpenStreetMap, licencja ODbL (https://www.openstreetmap.org/copyright).

Wejście: plik konfiguracji JSON (stacje, arkusze planu, pliki Overpass) – nazwy stacji tylko w konfiguracji.
Wyjście: plik układu w formacie station.json symulatora – tylko układ torowy (bez zobrazowania, bez szlaków):
  tory (długości z geometrii, nr toru i linii, elektryfikacja, kategoria), zwrotnice z numerami (krzyżowe – połówki),
  sygnalizatory z kierunkiem, kozły, wykolejnice, perony, przejazdy, odcinki (wstępne), posterunki, obrazy.

Plan wg Ig-10 § 3: wzdłuż osi arkusza skala 1:2000 (1,25 j./m), w poprzek 1:500 (5 j./m) – rzeczywista geometria
w dwóch skalach, jak plany schematyczne. Między stacjami arkusza szlak skrócony (Ig-10 § 1 ust. 8).

Użycie: python gen_uklad.py <konfiguracja.json> <wynik.json> [raport.txt]
"""
import json
import math
import os
import statistics
import sys
from collections import defaultdict

UX = 1.25      # j. planu na 1 m wzdłuż (1:2000 przy 2,5 j./mm)
UY = 5.0       # j. planu na 1 m w poprzek (1:500)
DY_FULL = 70.0  # do tej odległości od osi – pełna skala poprzeczna, dalej (rozchodzące się linie) – ściśnięta


def load_osm(paths, base):
    N, W = {}, {}
    for p in paths:
        d = json.load(open(os.path.join(base, p), encoding='utf-8'))
        for e in d['elements']:
            if e['type'] == 'node':
                N[e['id']] = e
            elif e['type'] == 'way':
                W[e['id']] = e
    return N, W


def main(cfg_path, out_path, rep_path=None):
    cfg = json.load(open(cfg_path, encoding='utf-8'))
    base = os.path.dirname(os.path.abspath(cfg_path))
    N, W = load_osm(cfg['osm'], base)
    rep = []
    R = lambda *a: rep.append(' '.join(str(x) for x in a))

    # ---------------- rzut metryczny
    lats = [n['lat'] for n in N.values()]
    lat0 = (min(lats) + max(lats)) / 2
    lon0 = min(n['lon'] for n in N.values())
    KX, KY = math.cos(math.radians(lat0)) * 111320, 110540

    def m(nid):
        p = N[nid]
        return ((p['lon'] - lon0) * KX, (p['lat'] - lat0) * KY)

    # ---------------- stacje (punkt stacji z OSM po nazwie)
    st = {}
    for s in cfg['stations']:
        cand = [n for n in N.values() if n.get('tags', {}).get('name') == s['name'] and n.get('tags', {}).get('railway') in ('station', 'halt')]
        if not cand:
            cand = [n for n in N.values() if n.get('tags', {}).get('name', '').startswith(s['name']) and n.get('tags', {}).get('railway') in ('station', 'halt', 'stop')]
        if not cand:
            R('BRAK stacji w OSM:', s['name']); continue
        st[s['code']] = dict(s, xy=m(cand[0]['id']))
    R('stacje:', ', '.join(f"{c} ({v['name']})" for c, v in st.items()))

    rails = [w for w in W.values() if w.get('tags', {}).get('railway') == 'rail' and all(n in N for n in w['nodes'])]

    # ---------------- arkusze: oś odniesienia i rzut (s – wzdłuż, d – w poprzek)
    sheets = []
    for sh in cfg['sheets']:
        codes = [c for c in sh['stations'] if c in st]
        if not codes:
            continue
        centers = [st[c]['xy'] for c in codes]
        reach = max(st[c]['radius'] for c in codes) + cfg.get('stub', 250)

        def near_any(p):
            return any(math.dist(p, c) <= st[k]['radius'] + cfg.get('stub', 250) for k, c in zip(codes, centers))
        # punkty osi: tor 1 linii odniesienia, inaczej tory główne (usage=main) przy stacjach arkusza
        refpts = []
        for w in rails:
            t = w['tags']
            if sh.get('refLine') and t.get('ref') != sh['refLine']:
                continue
            if not sh.get('refLine') and t.get('usage') != 'main':
                continue
            for n in w['nodes']:
                p = m(n)
                if sh.get('refLine') or near_any(p):
                    refpts.append(p)
        if sh.get('refLine'):
            # tylko między skrajnymi stacjami arkusza (z zapasem)
            cx = sum(c[0] for c in centers) / len(centers); cy = sum(c[1] for c in centers) / len(centers)
            refpts = [p for p in refpts if min(math.dist(p, c) for c in centers) < reach or _between(p, centers)]
        if len(refpts) < 2:
            refpts = [c for c in centers] + [(centers[0][0] + 100, centers[0][1])]
        # kierunek główny (PCA), zwrot z zachodu na wschód
        cx = sum(p[0] for p in refpts) / len(refpts); cy = sum(p[1] for p in refpts) / len(refpts)
        sxx = sum((p[0] - cx) ** 2 for p in refpts); syy = sum((p[1] - cy) ** 2 for p in refpts); sxy = sum((p[0] - cx) * (p[1] - cy) for p in refpts)
        a = 0.5 * math.atan2(2 * sxy, sxx - syy)
        ux, uy = math.cos(a), math.sin(a)
        if ux < 0:
            ux, uy = -ux, -uy
        pts = sorted(set(refpts), key=lambda p: (p[0] - cx) * ux + (p[1] - cy) * uy)
        # wygładzona linia osi: średnie w przedziałach co 100 m wzdłuż kierunku głównego, potem średnia krocząca
        bins = defaultdict(list)
        for p in pts:
            bins[int(((p[0] - cx) * ux + (p[1] - cy) * uy) // 100)].append(p)
        ref = [(sum(q[0] for q in v) / len(v), sum(q[1] for q in v) / len(v)) for k, v in sorted(bins.items())]
        if len(ref) > 4:
            ref = [ref[0]] + [((ref[i - 1][0] + 2 * ref[i][0] + ref[i + 1][0]) / 4, (ref[i - 1][1] + 2 * ref[i][1] + ref[i + 1][1]) / 4) for i in range(1, len(ref) - 1)] + [ref[-1]]
        cum = [0.0]
        for i in range(1, len(ref)):
            cum.append(cum[-1] + math.dist(ref[i - 1], ref[i]))
        sheets.append(dict(sh, codes=codes, centers=centers, ref=ref, cum=cum))
        R(f"arkusz {sh['name']}: oś {cum[-1]:.0f} m, punktów osi {len(ref)}")

    def proj(sheet, p):
        ref, cum = sheet['ref'], sheet['cum']
        best = None
        for i in range(len(ref) - 1):
            (x1, y1), (x2, y2) = ref[i], ref[i + 1]
            dx, dy = x2 - x1, y2 - y1; L2 = dx * dx + dy * dy
            if L2 == 0:
                continue
            t = ((p[0] - x1) * dx + (p[1] - y1) * dy) / L2
            if i > 0: t = max(t, 0.0)
            if i < len(ref) - 2: t = min(t, 1.0)
            px, py = x1 + t * dx, y1 + t * dy
            dd = math.hypot(p[0] - px, p[1] - py)
            if best is None or dd < best[0]:
                side = 1 if (dx * (p[1] - y1) - dy * (p[0] - x1)) > 0 else -1
                best = (dd, cum[i] + t * math.sqrt(L2), side * dd)
        return best[1], best[2]

    # ---------------- wybór torów: obszary stacji (+ zapas) i korytarz osi między stacjami arkusza
    def sheet_of(p):
        for sh in sheets:
            for k, c in zip(sh['codes'], sh['centers']):
                if math.dist(p, c) <= st[k]['radius'] + cfg.get('stub', 250):
                    return sh
            if len(sh['codes']) > 1:
                s, d = proj(sh, p)
                ss = [proj(sh, c)[0] for c in sh['centers']]
                if abs(d) <= sh.get('corridor', 70) and min(ss) <= s <= max(ss):
                    return sh
        return None

    SH = {}
    for w in rails:
        for n in w['nodes']:
            if n not in SH:
                SH[n] = sheet_of(m(n))
    pieces = []
    for w in rails:
        cur = []
        for n in w['nodes']:
            if SH.get(n) is not None and (not cur or SH[cur[-1]] is SH[n]):
                cur.append(n)
            else:
                if len(cur) > 1: pieces.append((w, cur))
                cur = [n] if SH.get(n) is not None else []
        if len(cur) > 1: pieces.append((w, cur))

    # ---------------- węzły kluczowe i krawędzie
    use = defaultdict(int)
    for w, ns in pieces:
        for n in ns: use[n] += 1
    tagr = lambda n: N[n].get('tags', {}).get('railway', '')
    key = set()
    for w, ns in pieces:
        key.add(ns[0]); key.add(ns[-1])
    for n, c in use.items():
        if c > 1 or tagr(n) in ('switch', 'buffer_stop', 'derail', 'railway_crossing'):
            key.add(n)
        if tagr(n) == 'signal':
            key.add(n)
    raw = []
    for w, ns in pieces:
        t = w.get('tags', {})
        seg = [ns[0]]
        for n in ns[1:]:
            seg.append(n)
            if n in key:
                raw.append(dict(nodes=seg, t=t, way=w['id'])); seg = [n]
    R('odcinki dróg OSM:', len(pieces), 'krawędzie:', len(raw))

    # ---------------- posterunek węzła i rzut planu
    def post_of(p):
        return min(st, key=lambda k: math.dist(p, st[k]['xy']) / st[k]['radius'])

    def in_station(p):
        return any(math.dist(p, v['xy']) <= v['radius'] for v in st.values())

    # skrócenie szlaku między stacjami arkusza: przedziały stacji [s−R, s+R], przerwy ściśnięte do szlakMax
    for sh in sheets:
        iv = sorted((proj(sh, st[k]['xy'])[0] - st[k]['radius'], proj(sh, st[k]['xy'])[0] + st[k]['radius']) for k in sh['codes'])
        merged = []
        for a, b in iv:
            if merged and a <= merged[-1][1]: merged[-1] = (merged[-1][0], max(merged[-1][1], b))
            else: merged.append((a, b))
        sh['iv'] = merged

    def smap(sh, s):
        iv = sh['iv']; out = 0.0; prev = None
        for a, b in iv:
            if prev is None:
                if s < a: return s - a
                prev = a
            gap = a - prev
            if s < a:
                return out + (s - prev) / gap * min(gap, cfg.get('szlakMax', 250)) if gap > 0 else out
            out += min(gap, cfg.get('szlakMax', 250)) if prev != a else 0
            if s <= b: return out + (s - a)
            out += b - a; prev = b
        return out + (s - prev)

    # arkusze obok siebie (osobne obrazy)
    x_off = 0.0
    for sh in sheets:
        ss = [smap(sh, proj(sh, m(n))[0]) for n in SH if SH[n] is sh]
        sh['s0'] = min(ss) if ss else 0; sh['s1'] = max(ss) if ss else 0
        sh['x0'] = x_off; x_off += (sh['s1'] - sh['s0']) * UX + 500

    def plan(n):
        sh = SH[n]; s, d = proj(sh, m(n))
        dd = d if abs(d) <= DY_FULL else math.copysign(DY_FULL + (abs(d) - DY_FULL) * 0.25, d)
        return (round(sh['x0'] + (smap(sh, s) - sh['s0']) * UX, 1), round(600 - dd * UY, 1))

    P = {}
    for e in raw:
        for n in e['nodes']:
            if n not in P: P[n] = plan(n)

    # ---------------- węzły modelu
    nodes, edges, switches, signals, derailers, track_ends = {}, [], {}, {}, {}, {}
    nid_of = {}
    used_keys = set()

    def uniq(k):
        base, i = k, 2
        while k in used_keys:
            k = f"{base}_{i}"; i += 1
        used_keys.add(k); return k

    def node_id(n):
        if n in nid_of: return nid_of[n]
        p = m(n); post = post_of(p); t = N[n].get('tags', {}); r = tagr(n)
        ref = (t.get('ref') or t.get('railway:ref') or '').strip()
        pre = {'switch': 'sw', 'signal': 'sg', 'buffer_stop': 'k', 'derail': 'wk'}.get(r, 'n')
        nid = uniq(f"{post}:{pre}{ref if ref and pre != 'n' else n}")
        nid_of[n] = nid
        nodes[nid] = dict(x=P[n][0], y=P[n][1], kind=r if r in ('switch', 'buffer_stop', 'derail') else '', ref=ref, post=post)
        return nid

    def length(ns):
        return sum(math.dist(m(ns[i]), m(ns[i + 1])) for i in range(len(ns) - 1))

    def cat(t):
        if t.get('usage') == 'main' and not t.get('service'): return 'gz'
        if t.get('usage') in ('main', 'branch') and t.get('service') in (None, '', 'crossover'): return 'gd'
        return None

    for i, e in enumerate(raw):
        ns, t = e['nodes'], e['t']
        a, b = node_id(ns[0]), node_id(ns[-1])
        if a == b: continue
        ed = dict(id=f"e{i}", a=a, b=b, osm=list(ns), pts=[list(P[n]) for n in ns], len=round(max(1.0, length(ns)), 1),
                  track=t.get('railway:track_ref', ''), line=t.get('ref', ''), sec='')
        if t.get('electrified') in ('contact_line', 'yes'): ed['elec'] = True
        c = cat(t)
        if c: ed['cat'] = c
        try:
            v = float(str(t.get('maxspeed', '')).split()[0]); ed['vmax'] = v
        except (ValueError, IndexError):
            pass
        if not in_station(m(ns[0])) and not in_station(m(ns[-1])): ed['szlak'] = True
        edges.append(ed)

    def inc(nid):
        return [e for e in edges if nid in (e['a'], e['b'])]

    def leg_vec(e, nid):
        pts = e['pts'] if e['a'] == nid else e['pts'][::-1]
        q = pts[min(len(pts) - 1, 1)]; p0 = pts[0]
        dx, dy = q[0] - p0[0], q[1] - p0[1]; L = math.hypot(dx, dy) or 1
        return dx / L, dy / L

    # ---------------- skrzyżowania torów (stopień 4 bez zwrotnicy) – dwa niezależne węzły
    for nid in list(nodes):
        es = inc(nid)
        if len(es) == 4 and nodes[nid]['kind'] != 'switch':
            v = [leg_vec(e, nid) for e in es]
            best = min(((i, j) for i in range(4) for j in range(i + 1, 4)), key=lambda ij: v[ij[0]][0] * v[ij[1]][0] + v[ij[0]][1] * v[ij[1]][1])
            nb = uniq(nid + 'x'); nodes[nb] = dict(nodes[nid])
            for k in best:
                e = es[k]
                if e['a'] == nid: e['a'] = nb
                else: e['b'] = nb
            R('skrzyżowanie torów rozdzielone:', nid)

    # ---------------- rozjazdy krzyżowe (zwrotnica stopnia 4) – dwie połówki „ab” / „cd”
    for nid in list(nodes):
        es = inc(nid)
        if nodes[nid]['kind'] == 'switch' and len(es) == 4:
            v = nodes[nid]
            west = sorted(es, key=lambda e: nodes[e['b'] if e['a'] == nid else e['a']]['x'])[:2]
            east = [e for e in es if e not in west]
            nb = uniq(nid + 'cd'); nodes[nb] = dict(v, x=round(v['x'] + 8, 1), ref=v['ref'] + 'cd'); v['ref'] = v['ref'] + 'ab'
            for e in east:
                if e['a'] == nid: e['a'] = nb; e['pts'][0] = [nodes[nb]['x'], nodes[nb]['y']]
                else: e['b'] = nb; e['pts'][-1] = [nodes[nb]['x'], nodes[nb]['y']]
            edges.append(dict(id=f"x{len(edges)}", a=nid, b=nb, pts=[[v['x'], v['y']], [nodes[nb]['x'], nodes[nb]['y']]], len=6.0, track='', line='', sec=''))

    # ---------------- schematyzacja jak na planach schematycznych (Semaforek, Ig-10 zał. 7):
    # ciągi torów (od zwrotnicy do zwrotnicy, dalej prosto przez rozjazd) dostają poziome pasy wg kolejności torów
    # w terenie; zmiana pasa tylko krótkim skosem przy rozjeździe. X – kilometraż (bez zmian).
    LANE = 4.75 * UY            # rozstaw torów na planie (1:500)
    adj = defaultdict(list)
    for e in edges: adj[e['a']].append(e); adj[e['b']].append(e)

    def leg(e, nid):
        pts = e['pts'] if e['a'] == nid else e['pts'][::-1]
        q = pts[1] if len(pts) > 1 else pts[0]
        dx, dy = q[0] - pts[0][0], q[1] - pts[0][1]; L = math.hypot(dx, dy) or 1
        return dx / L, dy / L
    # ciągi: łączenie w węzłach stopnia 2 oraz przez rozjazd: ostrze z bardziej prostym odgałęzieniem
    par = {e['id']: e['id'] for e in edges}
    def f(x):
        while par[x] != x: par[x] = par[par[x]]; x = par[x]
        return x
    for nid, es in adj.items():
        if len(es) == 2:
            par[f(es[0]['id'])] = f(es[1]['id'])
        elif len(es) == 3:
            v = [leg(e, nid) for e in es]
            i, j = min(((i, j) for i in range(3) for j in range(i + 1, 3)), key=lambda ij: v[ij[0]][0] * v[ij[1]][0] + v[ij[0]][1] * v[ij[1]][1])
            par[f(es[i]['id'])] = f(es[j]['id'])
    chains = defaultdict(list)
    for e in edges: chains[f(e['id'])].append(e)
    cof = {e['id']: f(e['id']) for e in edges}
    # 1) wygładzenie każdego ciągu: y(x) – średnia krocząca ok. 80 m (usuwa ząbki z punktów OSM, zostawia łuki)
    WIN = 80 * UX
    for c, es in chains.items():
        allp = sorted({(q[0], q[1]) for e in es for q in e['pts']})
        if len(allp) < 3: continue
        xs = [p[0] for p in allp]; ys = [p[1] for p in allp]
        def sm(x):
            num = den = 0.0
            for xx, yy in zip(xs, ys):
                w = max(0.0, 1 - abs(xx - x) / WIN)
                num += w * yy; den += w
            return num / den if den else x
        for e in es:
            e['pts'] = [[q[0], round(sm(q[0]), 1)] for q in e['pts']]
    for nid, es in adj.items():                      # węzeł – średnia z końców torów (po wygładzeniu)
        ys = [(e['pts'][0] if e['a'] == nid else e['pts'][-1])[1] for e in es]
        nodes[nid]['y'] = round(sum(ys) / len(ys), 1)
    info = {}
    for c, es in chains.items():
        ys = [q[1] for e in es for q in e['pts']]; xs = [q[0] for e in es for q in e['pts']]
        info[c] = dict(x0=min(xs), x1=max(xs), y=statistics.median(ys), spread=max(ys) - min(ys), len=sum(e['len'] for e in es))
    # 2) pasy: ciągi prawie proste (odchylenie < 6 m) w całości na pasie; pasy – mediany grupowane co 1,5 m
    FLAT = 6.0 * UY
    flatc = [c for c in info if info[c]['spread'] <= FLAT]
    meds = sorted((info[c]['y'], info[c]['len'], c) for c in flatc)
    Yc, cur = {}, []
    def close(cur):
        if not cur: return
        v = sum(y * l for y, l, _ in cur) / sum(l for _, l, _ in cur)
        for _, _, c in cur: Yc[c] = round(v, 1)
    for y, l, c in meds:
        if cur and y - cur[0][0] > 1.5 * UY: close(cur); cur = []
        cur.append((y, l, c))
    close(cur)
    LN = sorted(set(Yc.values()))
    def snap(y):
        if not LN: return y
        i = min(range(len(LN)), key=lambda k: abs(LN[k] - y))
        return LN[i] if abs(LN[i] - y) <= 1.6 * UY else y
    for nid, es in adj.items():
        cs = [cof[e['id']] for e in es]
        on = [c for c in cs if c in Yc]
        if on:
            best = max(set(on), key=lambda c: (cs.count(c), info[c]['len']))
            nodes[nid]['y'] = Yc[best]
        else:
            nodes[nid]['y'] = round(snap(nodes[nid]['y']), 1)
    def dp(pts, eps):
        if len(pts) < 3: return pts
        (x1, y1), (x2, y2) = pts[0], pts[-1]
        L = math.hypot(x2 - x1, y2 - y1) or 1e-9
        dmax, k = -1, 0
        for i in range(1, len(pts) - 1):
            d = abs((x2 - x1) * (y1 - pts[i][1]) - (x1 - pts[i][0]) * (y2 - y1)) / L
            if d > dmax: dmax, k = d, i
        if dmax <= eps: return [pts[0], pts[-1]]
        return dp(pts[:k + 1], eps)[:-1] + dp(pts[k:], eps)
    # 3) rysunek: tor na pasie – poziomo, skos ~1:3 przy końcu na innym pasie; krótkie łączniki (< 80 m) – prosto;
    #    pozostałe (łuki, rozchodzące się linie) – wygładzona geometria, uproszczona z małą tolerancją
    nflat = 0
    for e in edges:
        c = cof[e['id']]
        A = [nodes[e['a']]['x'], nodes[e['a']]['y']]; B = [nodes[e['b']]['x'], nodes[e['b']]['y']]
        if c in Yc:
            y = Yc[c]
            if A[1] == y and B[1] == y: e['pts'] = [A, B]; nflat += 1; continue
            sgn = 1 if B[0] > A[0] else -1; span = abs(B[0] - A[0])
            pts = [A]
            if A[1] != y and span > 1: pts.append([round(A[0] + sgn * min(0.45 * span, abs(A[1] - y) * 3), 1), y])
            if B[1] != y and span > 1: pts.append([round(B[0] - sgn * min(0.45 * span, abs(B[1] - y) * 3), 1), y])
            pts.append(B); e['pts'] = pts; continue
        if e['len'] < 80: e['pts'] = [A, B]; continue
        pts = [A] + [[q[0], round(snap(q[1]), 1)] for q in e['pts'][1:-1]] + [B]
        out = [pts[0]]
        for i in range(1, len(pts) - 1):
            if pts[i][1] == out[-1][1] == pts[i + 1][1]: continue
            out.append(pts[i])
        out.append(pts[-1])
        e['pts'] = dp(out, 0.6 * UY)
    R('ciągi torów:', len(chains), '– na pasach:', len(Yc), '– pasy:', len(LN), '– tory poziome:', nflat, 'z', len(edges))

    # ---------------- geometria z planu schematycznego (kotwice): zwrotnice i końce torów mają położenie w OSM
    # i na planie (Semaforek). Z kotwic – gładkie przekształcenie mapa → plan (thin-plate spline); nim cała geometria
    # torów z OSM (łuki i rozstaw jak na planie, kotwice dokładnie w miejscach z planu).
    PXS = cfg.get('pxScale', 0.85)            # jednostki planu na 1 piksel planu Semaforka
    rev = {x: k for k, x in nid_of.items()}
    for apath in cfg.get('anchors', []):
        A = json.load(open(os.path.join(base, apath), encoding='utf-8'))
        post = A['post']
        deg = defaultdict(int)
        for e in edges: deg[e['a']] += 1; deg[e['b']] += 1
        src, dst, anc_nodes = [], [], []
        for nid, v in nodes.items():
            if v['post'] != post or nid not in rev: continue
            q = None
            if v['kind'] == 'switch' and v.get('ref') in A['switch']: q = A['switch'][v['ref']]
            elif ':sg' in nid and v.get('ref') in A.get('signal', {}): q = A['signal'][v['ref']]
            elif deg[nid] == 1:
                tr = next((e['track'] for e in edges if nid in (e['a'], e['b']) and e['track']), '')
                q = A.get('end', {}).get(tr)
            if q: src.append(m(rev[nid])); dst.append(tuple(q)); anc_nodes.append(nid)
        if len(src) < 4:
            R(f'kotwice {post}: za mało ({len(src)})'); continue
        warp = _tps(src, dst)
        err = max(math.dist(warp(p), q) for p, q in zip(src, dst))
        # środek posterunku zostaje na miejscu (przesunięcie), tory dalej na wschód przesunięte o przyrost szerokości
        pn = [n for n, v in nodes.items() if v['post'] == post and n in rev]
        newp = {n: warp(m(rev[n])) for n in pn}
        ox = statistics.median(nodes[n]['x'] for n in pn) - statistics.median(newp[n][0] for n in pn) * PXS
        oy = statistics.median(nodes[n]['y'] for n in pn) - statistics.median(newp[n][1] for n in pn) * PXS
        old_max = max(nodes[n]['x'] for n in pn)
        for n in pn: nodes[n]['x'] = round(ox + newp[n][0] * PXS, 1); nodes[n]['y'] = round(oy + newp[n][1] * PXS, 1)
        shift = max(nodes[n]['x'] for n in pn) - old_max
        sh_post = next(sh for sh in sheets if post in sh['codes'])
        shifted = set()
        for n, v in nodes.items():
            if v['post'] != post and v['x'] > old_max and SH.get(rev.get(n, rev.get(n[:-2]) if n.endswith('cd') else None)) is sh_post:
                v['x'] = round(v['x'] + shift, 1); shifted.add(n)
        # przyciąganie do linii planu: punkt toru po przekształceniu przesuwany w poprzek (± SN px) do najbliższej
        # ciemnej linii planu (blisko – nie przeskoczy na sąsiedni tor), przesunięcia wygładzone
        SN = cfg.get('snapPx', 10)
        Mk = json.load(open(os.path.join(base, A['mask']), encoding='utf-8')) if A.get('mask') else None
        rows = [int(r, 16) for r in Mk['rows']] if Mk else []
        def dark(x, y):
            x, y = int(round(x)), int(round(y))
            return Mk and 0 <= x < Mk['w'] and 0 <= y < Mk['h'] and (rows[y] >> x) & 1
        def snap_line(pl, fixed_a, fixed_b):
            # próbkowanie co 4 px
            sp = [pl[0]]
            for i in range(len(pl) - 1):
                L = math.dist(pl[i], pl[i + 1]); k = max(1, int(L // 4))
                for j in range(1, k + 1): sp.append((pl[i][0] + (pl[i + 1][0] - pl[i][0]) * j / k, pl[i][1] + (pl[i + 1][1] - pl[i][1]) * j / k))
            if not Mk or len(sp) < 3: return sp
            off = []
            for i, p in enumerate(sp):
                a, b = sp[max(0, i - 2)], sp[min(len(sp) - 1, i + 2)]
                dx, dy = b[0] - a[0], b[1] - a[1]; L = math.hypot(dx, dy) or 1; nx, ny = -dy / L, dx / L
                best = None
                for t in sorted(range(-SN, SN + 1), key=abs):
                    if dark(p[0] + nx * t, p[1] + ny * t): best = t; break
                off.append((best, nx, ny))
            # brak linii (napis w przerwie) – przesunięcie z sąsiadów; wygładzenie średnią kroczącą
            vals = [o[0] for o in off]
            known = [i for i, v in enumerate(vals) if v is not None]
            for i in range(len(vals)):
                if vals[i] is None:
                    l = max((k for k in known if k < i), default=None); r = min((k for k in known if k > i), default=None)
                    vals[i] = (vals[l] if l is not None else 0) if r is None else (vals[r] if l is None else vals[l] + (vals[r] - vals[l]) * (i - l) / (r - l))
            sm = [sum(vals[max(0, i - 4):i + 5]) / len(vals[max(0, i - 4):i + 5]) for i in range(len(vals))]
            n = len(sp)
            out = []
            for i, (p, (_, nx, ny)) in enumerate(zip(sp, off)):
                w = 1.0
                if fixed_a: w = min(w, i / 4)
                if fixed_b: w = min(w, (n - 1 - i) / 4)
                out.append((p[0] + nx * sm[i] * w, p[1] + ny * sm[i] * w))
            return out
        anchored = set(anc_nodes)
        # tor między kolejnymi kotwicami – gładka krzywa Hermite'a przez punkty z planu (styczne jak w Catmull-Rom:
        # kierunek od poprzedniej do następnej kotwicy na tym samym torze); węzły pośrednie wg długości z OSM.
        # Odcinki bez kotwicy na końcu (dalej szlak) – przekształcenie TPS.
        adjp = defaultdict(list)
        for e in edges: adjp[e['a']].append(e); adjp[e['b']].append(e)
        PX = {n: warp(m(rev[n])) for n in anchored}
        def other(e, n): return e['b'] if e['a'] == n else e['a']
        def walk(u, e):
            """Od węzła u torem e przez węzły bez kotwicy (stopień 2) – do kotwicy albo końca: (węzły, tory)."""
            seq, es, cur = [u], [], u
            while True:
                nxt = other(e, cur); es.append(e); seq.append(nxt); cur = nxt
                if cur in anchored or nodes[cur]['post'] != post or len(adjp[cur]) != 2: return seq, es
                e = next(x for x in adjp[cur] if x is not e)
        def vec(a, b):
            d = (b[0] - a[0], b[1] - a[1]); L = math.hypot(*d) or 1; return d[0] / L, d[1] / L
        def through(u, e_in):
            """Kotwica „przed” u na przedłużeniu toru (najprostsza kontynuacja przez u)."""
            vin = vec(nodes[other(e_in, u)]['x'] and m(rev.get(other(e_in, u), rev[u])) or m(rev[u]), m(rev[u]))
            best, bd = None, -2
            for x in adjp[u]:
                if x is e_in: continue
                o = other(x, u)
                if o not in rev: continue
                v = vec(m(rev[u]), m(rev[o])); d = v[0] * vin[0] + v[1] * vin[1]
                if d > bd: bd, best = d, x
            if best is None or bd < 0.5: return None
            seq, _ = walk(u, best)
            return seq[-1] if seq[-1] in anchored else None
        def lim2(T, a, b):
            ch = math.dist(a, b); L = math.hypot(*T) or 1
            return (T[0] * min(1, ch / L), T[1] * min(1, ch / L))
        def herm(p0, p1, t0, t1, t):
            h1 = 2 * t ** 3 - 3 * t ** 2 + 1; h2 = -2 * t ** 3 + 3 * t ** 2; h3 = t ** 3 - 2 * t ** 2 + t; h4 = t ** 3 - t ** 2
            return (h1 * p0[0] + h2 * p1[0] + h3 * t0[0] + h4 * t1[0], h1 * p0[1] + h2 * p1[1] + h3 * t0[1] + h4 * t1[1])
        # piksele planu na metr (z kotwic sąsiednich: cięciwa / długość toru z OSM)
        rat = []
        for e in edges:
            if e['a'] in anchored and e['b'] in anchored and e['len'] > 20: rat.append(math.dist(PX[e['a']], PX[e['b']]) / e['len'])
        KPX = statistics.median(rat) if rat else 1.5
        nw = 0; done = set()
        for u in anchored:
            for e0 in adjp[u]:
                if e0['id'] in done: continue
                seq, es = walk(u, e0)
                for x in es: done.add(x['id'])
                v = seq[-1]
                cum = [0.0]
                for x in es: cum.append(cum[-1] + x['len'])
                if v in anchored:
                    P0, P1 = PX[u], PX[v]
                    pu = through(u, es[0]); nv = through(v, es[-1])
                    T0 = ((P1[0] - PX[pu][0]) / 2, (P1[1] - PX[pu][1]) / 2) if pu else (P1[0] - P0[0], P1[1] - P0[1])
                    T1 = ((PX[nv][0] - P0[0]) / 2, (PX[nv][1] - P0[1]) / 2) if nv else (P1[0] - P0[0], P1[1] - P0[1])
                    # długość stycznych nie większa niż cięciwa (bez pętli)
                    ch = math.dist(P0, P1)
                    def lim(T): L = math.hypot(*T) or 1; return (T[0] * min(1, ch / L), T[1] * min(1, ch / L))
                    T0, T1 = lim(T0), lim(T1)
                    # punkty pośrednie łuku z planu (via, wg toru) leżące między kotwicami
                    trs = [x['track'] for x in es if x['track']]
                    tr = max(set(trs), key=trs.count) if trs else ''
                    lo, hi = sorted((P0[0], P1[0]))
                    vias = sorted([tuple(q) for q in A.get('via', {}).get(tr, []) if lo + 3 < q[0] < hi - 3], key=lambda q: q[0], reverse=P1[0] < P0[0])
                    K = [P0] + vias + [P1]
                    TT = [T0] + [((K[i + 1][0] - K[i - 1][0]) / 2, (K[i + 1][1] - K[i - 1][1]) / 2) for i in range(1, len(K) - 1)] + [T1]
                    if vias:
                        TT[0] = lim2(TT[0], K[0], K[1]); TT[-1] = lim2(TT[-1], K[-2], K[-1])
                    ccum = [0.0]
                    for i in range(len(K) - 1): ccum.append(ccum[-1] + math.dist(K[i], K[i + 1]))
                    def curve(t, K=K, TT=TT, ccum=ccum):
                        s_ = t * ccum[-1]
                        i = max(0, min(len(K) - 2, next((j for j in range(len(K) - 1) if s_ <= ccum[j + 1]), len(K) - 2)))
                        seg = (ccum[i + 1] - ccum[i]) or 1
                        sc = seg / (math.dist(K[0], K[-1]) or 1) if len(K) == 2 else 1
                        return herm(K[i], K[i + 1], TT[i], TT[i + 1], (s_ - ccum[i]) / seg)
                else:
                    curve = None
                for i, x in enumerate(es):
                    if curve:
                        ts = [cum[i] / cum[-1] + (cum[i + 1] - cum[i]) / cum[-1] * k / 8 for k in range(9)] if cum[-1] else [0, 1]
                        pl = [curve(t) for t in ts]
                    else:
                        # koniec otwarty (dalej szlak): poziomo od kotwicy, długość wg OSM w skali planu
                        P0 = PX[u]
                        sgn = 1 if proj(sh_post, m(rev[seq[-1]]))[0] >= proj(sh_post, m(rev[u]))[0] else -1
                        pl = [(P0[0] + sgn * (cum[i] + (cum[i + 1] - cum[i]) * k / 4) * KPX, P0[1]) for k in range(5)]
                    pts = [[round(ox + w[0] * PXS, 1), round(oy + w[1] * PXS, 1)] for w in pl]
                    if x['a'] != seq[i]: pts = pts[::-1]
                    for nn, q in ((x['a'], pts[0]), (x['b'], pts[-1])):
                        if nn not in anchored and nodes[nn]['post'] == post: nodes[nn]['x'], nodes[nn]['y'] = q
                    x['_pl'] = pts; nw += 1
        for e in edges:
            pts = e.pop('_pl', None)
            if pts is None:
                pts = [[round(q[0] + (shift if e['a'] in shifted and e['b'] in shifted else 0), 1), q[1]] for q in e['pts']]
            pts[0] = [nodes[e['a']]['x'], nodes[e['a']]['y']]; pts[-1] = [nodes[e['b']]['x'], nodes[e['b']]['y']]
            e['pts'] = _dp(pts, 0.5)
        R(f'plan {post}: kotwic {len(src)}, największa odchyłka w kotwicy {err:.2f} px, torów przeniesionych {nw}')

    # ---------------- zwrotnice
    for nid, v in nodes.items():
        if v['kind'] != 'switch': continue
        es = inc(nid)
        if len(es) != 3:
            R('zwrotnica pominięta (stopień', len(es), '):', nid); v['kind'] = ''; continue
        lab = v['ref'] or '?'
        osm = [k for k, x in nid_of.items() if x == nid or x + 'cd' == nid]
        t = N[osm[0]].get('tags', {}) if osm else {}
        sw = dict(num=uniq(f"{v['post']}:{lab}"), node=nid, label=lab, post=v['post'], sec='')
        if t.get('railway:switch:electric') == 'no' or t.get('railway:switch:local_operated') == 'yes': sw['manual'] = True
        switches[sw['num']] = sw
        if not v['ref']: R('zwrotnica bez numeru w OSM:', nid)

    # ---------------- sygnalizatory
    skipped = defaultdict(int)
    way_of = defaultdict(list)
    for e in raw:
        for k, n in enumerate(e['nodes']):
            way_of[n].append((e, k))
    for n, nid in nid_of.items():
        t = N[n].get('tags', {})
        if t.get('railway') != 'signal': continue
        ref = (t.get('ref') or '').strip()
        mk = 'railway:signal:combined' if t.get('railway:signal:combined') else 'railway:signal:main' if t.get('railway:signal:main') else None
        func = t.get(mk + ':function', '') if mk else ''
        states = t.get(mk + ':states', '') if mk else ''
        if mk:
            if func == 'block' or (func == 'intermediate' and not in_station(m(n))):
                skipped['odstępowy SBL / na szlaku (szlak dodaj w edytorze)'] += 1; continue
            typ = 'wjazdowy' if func == 'entry' else 'wyjazdowy'   # exit; intermediate w stacji – drogowskazowy (jak wyjazdowy)
        elif t.get('railway:signal:shunting'): typ = 'tm'
        elif t.get('railway:signal:distant'): typ = 'to'
        elif t.get('railway:signal:main_repeated') or t.get('railway:signal:repeated'): typ = 'sp'
        else:
            skipped['inny (wskaźnik, W…)'] += 1; continue
        # kierunek: ważny w kierunku drogi (forward) albo przeciwnie; na planie +1 – na wschód
        dirn = t.get('railway:signal:direction', 'forward')
        e, k = way_of[n][0]
        ns = e['nodes']; a = ns[max(0, k - 1)]; b = ns[min(len(ns) - 1, k + 1)]
        dx = P[b][0] - P[a][0]
        d = (1 if dx >= 0 else -1) * (1 if dirn != 'backward' else -1)
        post = nodes[nid]['post']; lab = ref or nid.split(':')[-1]
        s = dict(name=uniq(f"{post}:{lab}"), node=nid, dir=d, type=typ, label=lab, post=post)
        if typ == 'wyjazdowy' and mk and 'ms2' not in states.lower(): s['shunt'] = False   # bez Ms2 wg OSM
        if typ == 'tm' and lab.lower().startswith('tm'): s['label'] = lab[2:] or lab
        if typ == 'wyjazdowy' and e['t'].get('railway:track_ref'): s['track'] = e['t']['railway:track_ref']
        signals[s['name']] = s
        if not ref: R('sygnalizator bez oznaczenia w OSM:', nid, typ)
    for k, c in skipped.items(): R('pominięte sygnalizatory –', k + ':', c)

    # ---------------- kozły (koniec toru) i wykolejnice
    deg = defaultdict(int)
    for e in edges: deg[e['a']] += 1; deg[e['b']] += 1
    for nid, v in nodes.items():
        if v['kind'] == 'buffer_stop' and deg[nid] == 1:
            tr = next((e['track'] for e in inc(nid) if e['track']), '')
            track_ends[nid] = dict(id=f"{v['post']}:K{tr or nid.split(':')[-1][-4:]}", label='koniec toru ' + tr, disp=tr + 'k' if tr else 'k', marker=True)
        if v['kind'] == 'derail' and v['ref']:
            es = inc(nid)
            derailers[uniq(f"{v['post']}:{v['ref']}")] = dict(name=f"{v['post']}:{v['ref']}", node=nid, label=v['ref'], post=v['post'], sec='')

    # ---------------- odcinki (wstępne): granice na sygnalizatorach, końcach, zmianie posterunku; zwrotnice wewnątrz
    sig_nodes = {s['node'] for s in signals.values() if s['type'] in ('wjazdowy', 'wyjazdowy', 'tm')}
    parent = {e['id']: e['id'] for e in edges}

    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]; x = parent[x]
        return x
    bynode = defaultdict(list)
    for e in edges: bynode[e['a']].append(e); bynode[e['b']].append(e)
    for nid, es in bynode.items():
        if nid in sig_nodes or len(es) < 2 or nodes[nid]['kind'] == 'switch': continue
        for e in es[1:]:
            if e['a'].split(':')[0] == es[0]['a'].split(':')[0]:
                parent[find(e['id'])] = find(es[0]['id'])
    groups = defaultdict(list)
    for e in edges: groups[find(e['id'])].append(e)
    names = set()
    for g, es in groups.items():
        post = nodes[es[0]['a']]['post']
        sws = sorted({sw['label'] for sw in switches.values() if any(sw['node'] in (e['a'], e['b']) for e in es)}, key=lambda x: (len(x), x))
        trs = [e['track'] for e in es if e['track']]
        tr = max(set(trs), key=trs.count) if trs else ''
        if sws: nm = 'z' + sws[0] + ('-' + sws[-1] if len(sws) > 1 else '')
        elif any(e.get('szlak') for e in es): nm = 'sz' + (tr or 'x')
        elif tr: nm = 'JT' + tr if all(in_station(m(k)) for k in [n for n, x in nid_of.items() if x in (es[0]['a'], es[0]['b'])][:1]) else 'it' + tr
        else: nm = 'it'
        base, i = f"{post}:{nm}", 2
        sec = base
        while sec in names:
            sec = f"{base}{chr(ord('a') + i - 2)}"; i += 1
        names.add(sec)
        for e in es: e['sec'] = sec
    for sw in switches.values():
        sw['sec'] = next((e['sec'] for e in edges if sw['node'] in (e['a'], e['b'])), '')
    for dr in derailers.values():
        dr['sec'] = next((e['sec'] for e in edges if dr['node'] in (e['a'], e['b'])), '')

    # ---------------- perony i przejazdy
    platforms = []
    anchored_posts = {json.load(open(os.path.join(os.path.dirname(os.path.abspath(cfg_path)), ap), encoding='utf-8'))['post'] for ap in cfg.get('anchors', [])}
    kept = [m(n) for n in P]
    for w in W.values():
        t = w.get('tags', {})
        if t.get('railway') != 'platform': continue
        ns = [n for n in w['nodes'] if n in N and SH.get(n) is None]
        ns = [n for n in w['nodes'] if n in N]
        sh = sheet_of(m(ns[0])) if ns else None
        if sh is None or not any(in_station(m(n)) for n in ns): continue
        if post_of(m(ns[0])) in anchored_posts: continue   # posterunek z planu – perony z planu, nie z rzutu OSM
        cxy = (sum(m(n)[0] for n in ns) / len(ns), sum(m(n)[1] for n in ns) / len(ns))
        if min(math.dist(cxy, q) for q in kept) > 25: continue   # nie przy torach (np. przystanek tramwajowy)
        pp = []
        for n in ns:
            s, d = proj(sh, m(n)); dd = d if abs(d) <= DY_FULL else math.copysign(DY_FULL + (abs(d) - DY_FULL) * 0.25, d)
            pp.append((sh['x0'] + (smap(sh, s) - sh['s0']) * UX, 600 - dd * UY))
        x0, x1 = min(p[0] for p in pp), max(p[0] for p in pp); y0, y1 = min(p[1] for p in pp), max(p[1] for p in pp)
        if x1 - x0 < 5: continue
        nm = t.get('ref') and f"per. {t['ref']}" or t.get('name') or 'peron'
        platforms.append(dict(name=nm, x=round(x0, 1), y=round(y0, 1), w=round(x1 - x0, 1), h=round(max(4, y1 - y0), 1)))
    crossings = []
    lc = [n for n in N if N[n].get('tags', {}).get('railway') == 'level_crossing' and n in P]
    lc.sort(key=lambda n: P[n][0])
    grp = []
    for n in lc:
        if grp and abs(P[n][0] - P[grp[-1][-1]][0]) < 12: grp[-1].append(n)
        else: grp.append([n])
    for g in grp:
        xs = [P[n][0] for n in g]; ys = [P[n][1] for n in g]
        crossings.append(dict(name='przejazd', kind='Y', x=round(sum(xs) / len(xs), 1), y0=round(min(ys) - 10, 1), y1=round(max(ys) + 10, 1)))

    # ---------------- obrazy (arkusze), posterunki, zapis
    pages = []
    for sh in sheets:
        x1 = sh['x0'] + (sh['s1'] - sh['s0']) * UX
        pages.append(dict(id=sh['codes'][0], name=sh['name'], x0=round(sh['x0'] - 100, 1), x1=round(x1 + 100, 1), posts=sh['codes']))
    out = dict(name=cfg.get('name', 'Układ z OSM'), code=cfg.get('code', 'LCS'), width=round(x_off, 1), height=1200,
               source='Układ torowy wygenerowany z OpenStreetMap (© współtwórcy OpenStreetMap, ODbL) – propozycja do poprawek. tools/gen_uklad.py',
               pages=pages, posts={c: v['name'] for c, v in st.items()},
               nodes={k: {kk: vv for kk, vv in v.items() if kk != 'ref' or vv} for k, v in nodes.items() if deg[k] > 0},
               edges=[{k: v for k, v in e.items() if k != 'osm'} for e in edges], switches=switches, signals=signals, derailers=derailers, blocks={}, boundaries={}, farEnds={},
               trackEnds=track_ends, platforms=platforms, labels=[], crossings=crossings, dashed=[], ports={}, routeRules=[], bumpers={},
               detection='lo')
    json.dump(out, open(out_path, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    by = defaultdict(lambda: defaultdict(int))
    for v in out['nodes'].values(): by[v['post']]['węzły'] += 1
    for s in switches.values(): by[s['post']]['zwrotnice'] += 1
    for s in signals.values(): by[s['post']][s['type']] += 1
    for k, v in by.items(): R(k, dict(v))
    R('razem: węzły', len(out['nodes']), 'tory', len(edges), 'zwrotnice', len(switches), 'sygnalizatory', len(signals),
      'odcinki', len({e['sec'] for e in edges}), 'perony', len(platforms), 'przejazdy', len(crossings), 'kozły', len(track_ends), 'wykolejnice', len(derailers))
    txt = '\n'.join(rep)
    print(txt)
    if rep_path: open(rep_path, 'w', encoding='utf-8').write(txt)


def _tps(src, dst, lam=1e-6):
    """Thin-plate spline: gładkie przekształcenie R² → R² przechodzące przez pary punktów (src → dst)."""
    n = len(src)
    cx = sum(p[0] for p in src) / n; cy = sum(p[1] for p in src) / n
    sc = max(1.0, max(math.dist(p, (cx, cy)) for p in src))
    P = [((p[0] - cx) / sc, (p[1] - cy) / sc) for p in src]
    def U(r): return r * r * math.log(r) if r > 1e-12 else 0.0
    N = n + 3
    K = [[0.0] * N for _ in range(N)]
    for i in range(n):
        for j in range(n):
            K[i][j] = U(math.dist(P[i], P[j])) + (lam if i == j else 0.0)
        K[i][n] = K[n][i] = 1.0
        K[i][n + 1] = K[n + 1][i] = P[i][0]
        K[i][n + 2] = K[n + 2][i] = P[i][1]
    def solve(Mx, b):
        M = [row[:] + [b[i]] for i, row in enumerate(Mx)]
        for c in range(N):
            piv = max(range(c, N), key=lambda r: abs(M[r][c])); M[c], M[piv] = M[piv], M[c]
            if abs(M[c][c]) < 1e-14: continue
            for r in range(N):
                if r != c and M[r][c]:
                    f = M[r][c] / M[c][c]
                    for k in range(c, N + 1): M[r][k] -= f * M[c][k]
        return [M[i][N] / M[i][i] if abs(M[i][i]) > 1e-14 else 0.0 for i in range(N)]
    wx = solve(K, [q[0] for q in dst] + [0, 0, 0]); wy = solve(K, [q[1] for q in dst] + [0, 0, 0])
    def f(p):
        x, y = (p[0] - cx) / sc, (p[1] - cy) / sc
        u = [U(math.dist((x, y), P[i])) for i in range(n)]
        return (wx[n] + wx[n + 1] * x + wx[n + 2] * y + sum(wx[i] * u[i] for i in range(n)),
                wy[n] + wy[n + 1] * x + wy[n + 2] * y + sum(wy[i] * u[i] for i in range(n)))
    return f


def _dp(pts, eps):
    """Douglas–Peucker: łamana uproszczona do prostych odcinków (tolerancja eps)."""
    if len(pts) < 3: return pts
    (x1, y1), (x2, y2) = pts[0], pts[-1]
    L = math.hypot(x2 - x1, y2 - y1) or 1e-9
    dmax, k = -1, 0
    for i in range(1, len(pts) - 1):
        d = abs((x2 - x1) * (y1 - pts[i][1]) - (x1 - pts[i][0]) * (y2 - y1)) / L
        if d > dmax: dmax, k = d, i
    if dmax <= eps: return [pts[0], pts[-1]]
    return _dp(pts[:k + 1], eps)[:-1] + _dp(pts[k:], eps)


def _between(p, centers):
    """Punkt w pasie między skrajnymi stacjami (rzut na odcinek łączący pierwszą i ostatnią)."""
    (x1, y1), (x2, y2) = centers[0], centers[-1]
    dx, dy = x2 - x1, y2 - y1; L2 = dx * dx + dy * dy
    if L2 == 0: return False
    t = ((p[0] - x1) * dx + (p[1] - y1) * dy) / L2
    return 0 <= t <= 1 and abs(dx * (p[1] - y1) - dy * (p[0] - x1)) / math.sqrt(L2) < 400


if __name__ == '__main__':
    main(*sys.argv[1:])
