"""
Obrazy szczegółowe LCS Łódź Widzew – generator modelu (Data/station.json).

Posterunki:  LW – Łódź Widzew (obraz 2),  LF – Łódź Fabryczna, LN – Łódź Niciarniana (obraz 1).
Źródła: plany schematyczne Semaforek (Zrodla/Semaforek_Lodz_Widzew.png, ..._Lodz_Fabryczna_2016.png),
wzór pulpitu (Zrodla/SimRail_LCS_Widzew_wzor.webp), wytyczne zobrazowania Ie-104.

Konwencja: x globalne rośnie na wschód (obraz LF: x ∈ [-2000, 0), obraz LW: x ∈ [0, 2000)), dir=+1 – na wschód.
Połączenia między pasami rysowane pionowo z fazami 8 px; węzeł „wcześniejszej” zwrotnicy o 8 px na zachód.

Użycie: python lcs_panel.py <out station.json> [podgląd.png]
"""
import json, math, sys

nodes, edges, signals, switches, derailers, track_ends = {}, [], {}, {}, {}, {}
labels, crossings, dashed, platforms, BLOCKS = [], [], [], [], {}
CUR = {'p': 'LW', 'ox': 0}
POSTS = {'LW': 'Łódź Widzew', 'LF': 'Łódź Fabryczna', 'LN': 'Łódź Niciarniana'}
PAGES = [dict(id='LF', name='Łódź Fabryczna + Łódź Niciarniana', x0=-2000, x1=0, posts=['LF', 'LN']),
         dict(id='LW', name='Łódź Widzew', x0=0, x1=2000, posts=['LW'])]


def post(p, ox):
    CUR['p'] = p; CUR['ox'] = ox


def K(n):
    return CUR['p'] + ':' + n


def X(x):
    return x + CUR['ox']


def N(nid, x, y, kind='', ref=''):
    k = K(nid)
    if k in nodes:
        raise ValueError('dubel węzła ' + k)
    nodes[k] = dict(x=X(x), y=y, kind=kind, ref=ref, post=CUR['p'])
    return k


def bend_pts(a, b, bend_at=None):
    xa, ya = nodes[a]['x'], nodes[a]['y']; xb, yb = nodes[b]['x'], nodes[b]['y']
    if abs(ya - yb) < 0.5:
        return [[xa, ya], [xb, yb]]
    s = 1 if yb > ya else -1
    xm = (xa + xb) / 2 if bend_at is None else X(bend_at)
    c = 8.0 if bend_at is not None else min(8.0, abs(xb - xa) / 2)
    pts = [[xa, ya], [xm - c, ya], [xm, ya + s * c], [xm, yb - s * c], [xm + c, yb], [xb, yb]]
    out = []
    for p in pts:
        if not out or abs(out[-1][0] - p[0]) > 0.01 or abs(out[-1][1] - p[1]) > 0.01:
            out.append(p)
    return out


def E(a, b, bend_at=None, **kw):
    e = dict(a=a, b=b, pts=bend_pts(a, b, bend_at), **kw)
    edges.append(e)
    return e


def chain(ids):
    for i in range(len(ids) - 1):
        E(ids[i], ids[i + 1])


def SW(num, x, y, label=None):
    nid = N('sw' + num, x, y, kind='switch', ref=num)
    base = num[:-2] if num.endswith(('ab', 'cd')) else num
    switches[K(num)] = dict(num=K(num), node=nid, label=label or num, sec=K('z' + base), post=CUR['p'])
    return nid


def SG(name, x, y, d, typ, label=None, **kw):
    nid = N('sg' + name, x, y)
    if 'repeats' in kw and kw['repeats']:
        kw['repeats'] = K(kw['repeats'])
    signals[K(name)] = dict(name=K(name), node=nid, dir=d, type=typ, label=label or name, post=CUR['p'], **kw)
    return nid


def TM(n, x, y, d):
    return SG('Tm' + str(n), x, y, d, 'tm', label=str(n))


def P(nid, x, y):
    return N(nid, x, y)


def BUF(bid, x, y, label, marker=True):
    nid = N('buf' + bid, x, y)
    track_ends[nid] = dict(id=K(bid), label=label, disp=bid, marker=marker)
    return nid


def WK(name, x, y):
    nid = N('wk' + name, x, y, kind='derail', ref=name)
    derailers[K(name)] = dict(name=K(name), node=nid, label=name, post=CUR['p'])
    return nid


def CONN(a, b):
    E(K('sw' + a), K('sw' + b), conn=True)


def SWS(lst, lanes):
    for num, x, lane in lst:
        SW(num, x, lanes[lane])


# ---------------------------------------------------------------- szlaki
def _sbl_signals(bid, bnodes, lab_e, lab_w):
    for j, nd in enumerate(bnodes):
        pst = nodes[nd]['post']
        for d, labs, sfx in ((1, lab_e, '+'), (-1, lab_w, '-')):
            nm = '%s/%d%s' % (bid, j + 1, sfx)
            signals[nm] = dict(name=nm, node=nd, dir=d, type='odstepowy', szlak=bid, label=labs[j] if j < len(labs) else '', post=pst)


def _szlak_edges(bid, seq, bnodes, bend_at=None):
    secs_we, k = [], 0
    for i in range(len(seq) - 1):
        a, b = seq[i], seq[i + 1]
        sec = '%s-%d' % (bid, k + 1)
        if sec not in secs_we:
            secs_we.append(sec)
        pts = [[nodes[a]['x'], nodes[a]['y']], [nodes[b]['x'], nodes[b]['y']]]
        if abs(nodes[a]['y'] - nodes[b]['y']) > 0.5:
            s = 1 if nodes[b]['y'] > nodes[a]['y'] else -1
            pts = [pts[0], [bend_at - 8, nodes[a]['y']], [bend_at, nodes[a]['y'] + s * 8], [bend_at, nodes[b]['y'] - s * 8], [bend_at + 8, nodes[b]['y']], pts[1]]
        edges.append(dict(a=a, b=b, pts=pts, sec=sec, szlak=True))
        if b in bnodes:
            k += 1
    return secs_we


def ext_szlak(bid, y, side, nbr, line, tor, entry, default_dir, it_label, to=None, sps=(), bx=None, lab_e=(), lab_w=(), sbl=True):
    """Szlak zewnętrzny – od posterunku spoza obszaru LCS do semafora wjazdowego.
    sbl=True – blokada samoczynna z odstępami, sbl=False – blokada liniowa, szlak jednoodstępowy (bez sygnalizatorów odstępowych)."""
    if side == 'W':
        far = P('far' + bid, 20, y)
        bx = (bx or (75, 160)) if sbl else ()
        bnodes = [P('od%s_%d' % (bid, i), x, y) for i, x in enumerate(bx)]
        ent = SG(entry, 290, y, 1, 'wjazdowy', szlak=bid)
        items = [(32, SG(to, 32, y, 1, 'to', repeats=entry))] if to else []
        items += [(x, n) for x, n in zip(bx, bnodes)] + [(x, SG(nm, x, y, 1, 'sp', repeats=entry)) for nm, x in sps]
        seq = [far] + [n for x, n in sorted(items)] + [ent]
        outward = -1
    else:
        far = P('far' + bid, 1985, y)
        bx = (bx or (1840, 1915)) if sbl else ()
        bnodes = [P('od%s_%d' % (bid, i), x, y) for i, x in enumerate(bx)]
        ent = SG(entry, 1760, y, -1, 'wjazdowy', szlak=bid)
        items = [(x, n) for x, n in zip(bx, bnodes)] + ([(1952, SG(to, 1952, y, -1, 'to', repeats=entry))] if to else [])
        seq = [ent] + [n for x, n in sorted(items)] + [far]
        outward = 1
    secs_we = _szlak_edges(bid, seq, bnodes)
    _sbl_signals(bid, bnodes, lab_e, lab_w)
    BLOCKS[bid] = dict(id=bid, type='sbl' if sbl else 'blok', nbr=nbr, internal=False, ends=[dict(boundary=ent, outward=outward, entrySig=K(entry), post=CUR['p'])],
                       far=far, secsWE=secs_we, defDir=default_dir, tor=tor, line=line, label=it_label)
    return ent


def int_szlak(bid, west, east, west_sig, east_sig, bx, default_dir, it_label, line, tor, lab_e=(), lab_w=(), bend_at=None, nbr_w='', nbr_e=''):
    """Szlak wewnętrzny – oba końce w obszarze LCS (blokada obsługiwana z obu posterunków)."""
    y_w, y_e = nodes[west]['y'], nodes[east]['y']
    bnodes = []
    for i, x in enumerate(bx):
        nid = 'od:%s_%d' % (bid, i)
        nodes[nid] = dict(x=x, y=y_w if (bend_at is None or x < bend_at) else y_e, kind='', ref='', post='LF' if x < 0 else 'LW')
        bnodes.append(nid)
    secs_we = _szlak_edges(bid, [west] + bnodes + [east], bnodes, bend_at)
    _sbl_signals(bid, bnodes, lab_e, lab_w)
    BLOCKS[bid] = dict(id=bid, type='sbl', nbr=nbr_w + ' – ' + nbr_e, internal=True,
                       ends=[dict(boundary=west, outward=1, entrySig=west_sig, post=nodes[west]['post']),
                             dict(boundary=east, outward=-1, entrySig=east_sig, post=nodes[east]['post'])],
                       far=None, secsWE=secs_we, defDir=default_dir, tor=tor, line=line, label=it_label, nbrW=nbr_w, nbrE=nbr_e)


# =====================================================================================================
#  ŁÓDŹ WIDZEW (LW) – obraz 2
# =====================================================================================================
post('LW', 0)
Y = dict(L104=248, L102=315, LB=383, L8=450, L6=516, L4=583, L2=683, L1=783, L3=883, L5=983, L7=1083, E3=983, E5=1083)
SWS([('6', 417, 'L5'), ('7', 433, 'L3'), ('8', 467, 'L3'), ('9', 483, 'L1'), ('10', 517, 'L1'), ('11', 533, 'L2'),
     ('12', 582, 'L2'), ('13', 598, 'L1'), ('14', 632, 'L2'), ('17cd', 648, 'L4'), ('17ab', 682, 'L4'), ('18cd', 698, 'L6'),
     ('18ab', 735, 'L6'), ('21', 751, 'L8'), ('24', 790, 'LB'), ('15', 647, 'L1'), ('16', 663, 'L3'), ('19', 717, 'L3'),
     ('20', 733, 'L5'), ('27', 782, 'L5'), ('28', 798, 'L7'), ('22', 785, 'L6'), ('23', 801, 'L4'), ('25', 834, 'L4'),
     ('26', 850, 'L2'), ('28N', 950, 'L104'),
     ('43', 1233, 'L6'), ('45', 1265, 'L4'), ('42', 1234, 'L104'), ('47', 1284, 'LB'), ('48', 1315, 'L4'),
     ('49', 1347, 'L4'), ('50', 1363, 'L2'), ('51', 1395, 'L2'), ('53', 1411, 'L1'), ('52', 1399, 'E5'), ('54', 1415, 'E3'),
     ('56', 1450, 'L1'), ('57', 1466, 'E3'), ('58', 1516, 'E3'), ('59', 1532, 'L1'), ('62', 1566, 'L1'), ('63', 1582, 'L2'),
     ('60', 1600, 'E3'), ('61', 1616, 'E5'), ('64', 1618, 'L2'), ('65', 1634, 'L4'), ('44', 1217, 'L7'), ('46', 1233, 'L5')], Y)
switches[K('28N')]['label'] = '28'
for a, b in [('6', '7'), ('8', '9'), ('10', '11'), ('12', '13'), ('14', '17cd'), ('17ab', '18cd'), ('18ab', '21'), ('21', '24'),
             ('15', '16'), ('19', '20'), ('27', '28'), ('22', '23'), ('25', '26'),
             ('49', '50'), ('51', '53'), ('52', '54'), ('56', '57'), ('58', '59'), ('62', '63'), ('60', '61'), ('64', '65'), ('44', '46')]:
    CONN(a, b)

wA = ext_szlak('1M', Y['L4'], 'W', 'Łódź Marysin', '16', '1', 'A', 0, 'it1M', to='ToA', lab_e=('1M1', '1M3'), lab_w=('1M2', '1M4'))
wD = SG('D', 290, Y['L2'], 1, 'wjazdowy', szlak='2F')
wE = SG('E', 290, Y['L1'], 1, 'wjazdowy', szlak='1F')
wG = ext_szlak('1C', Y['L3'], 'W', 'Łódź Chojny', '540', '1', 'G', -1, 'it1C', to='ToG', sps=[('IIISpG', 180), ('IISpG', 210), ('ISpG', 240)], bx=(62, 125), lab_e=('1C1', '1C3'), lab_w=('1C2', '1C4'))
wH = ext_szlak('2C', Y['L5'], 'W', 'Łódź Chojny', '540', '2', 'H', 1, 'it2C', to='ToH', sps=[('IIISpH', 180), ('IISpH', 210), ('ISpH', 240)], bx=(62, 125), lab_e=('2C1', '2C3'), lab_w=('2C2', '2C4'))
eW = ext_szlak('2A', Y['L2'], 'E', 'Łódź Andrzejów', '17', '2', 'W', -1, 'it82', lab_e=('82N', '102N'), lab_w=('82', '102'))
eV = ext_szlak('1A', Y['L1'], 'E', 'Łódź Andrzejów', '17', '1', 'V', 1, 'it81', lab_e=('67', '81'), lab_w=('67N', '81N'))
eU = ext_szlak('2O', Y['E3'], 'E', 'Łódź Olechów', '541', '2', 'U', -1, 'it20', to='ToU', sbl=False)
eT = ext_szlak('1O', Y['E5'], 'E', 'Łódź Olechów', '541', '1', 'T', 1, 'it10', to='ToT', sbl=False)

for t, lane in [('8', 'L8'), ('6', 'L6'), ('4', 'L4'), ('2', 'L2'), ('1', 'L1'), ('3', 'L3'), ('5', 'L5'), ('7', 'L7')]:
    SG({'8': 'M8', '6': 'M6', '4': 'M4'}.get(t, 'L' + t), 900, Y[lane], -1, 'wyjazdowy', track=t)
    SG({'8': 'R8', '6': 'R6', '4': 'R4'}.get(t, 'S' + t), 1160, Y[lane], 1, 'wyjazdowy', track=t)
SG('N104', 1010, Y['L104'], -1, 'wyjazdowy', track='104'); SG('P104', 1160, Y['L104'], 1, 'wyjazdowy', track='104')
SG('N102', 1010, Y['L102'], -1, 'wyjazdowy', track='102'); SG('P102', 1160, Y['L102'], 1, 'wyjazdowy', track='102')
SG('ISpS3', 1107, Y['L3'], 1, 'sp', repeats='S3'); SG('ISpS7', 1107, Y['L7'], 1, 'sp', repeats='S7')
for n, x, lane, d in [(1, 392, 'L5', 1), (2, 392, 'L3', 1), (3, 475, 'L5', -1), (4, 415, 'L1', 1), (5, 507, 'L3', -1), (6, 415, 'L2', 1),
                      (7, 675, 'L2', -1), (8, 559, 'L4', 1), (9, 625, 'L3', 1), (10, 625, 'L6', 1), (11, 709, 'L1', -1), (12, 675, 'L5', 1),
                      (13, 759, 'L3', -1), (14, 806, 'L2', 1), (15, 909, 'LB', -1), (16, 843, 'L104', -1), (17, 675, 'L7', 1), (18, 925, 'L104', 1),
                      (100, 872, 'LB', 1), (21, 1157, 'LB', 1), (136, 1192, 'LB', -1), (22, 1442, 'L4', -1), (23, 1442, 'L2', -1),
                      (24, 1442, 'E5', -1), (28, 1574, 'E5', 1), (29, 1540, 'L2', 1), (30, 1658, 'E3', -1), (31, 1658, 'E5', -1),
                      (32, 1658, 'L1', -1), (33, 1569, 'L4', 1), (34, 1658, 'L2', -1), (35, 1658, 'L4', -1)]:
    TM(n, x, Y[lane], d)
for nm, x, lane in [('Wk18', 659, 'L6'), ('Wk28', 742, 'L7'), ('Wk24', 842, 'LB'), ('Wk47', 1242, 'LB'), ('Wk49', 1405, 'L4')]:
    WK(nm, x, Y[lane])
BUF('10k', 535, Y['L6'], 'koniec toru 200'); BUF('17k', 600, Y['L7'], 'tory sekcji eksploatacji'); BUF('35k', 1748, Y['L4'], 'koniec toru 204a')
BUF('7b', 1247, Y['L7'], 'tor 7b', marker=False); BUF('CSW', 960, Y['LB'], 'baza ŁKA (nastawnia CS)', marker=False); BUF('CSE', 1120, Y['LB'], 'baza ŁKA (nastawnia CS)', marker=False)
P('c104', 820, Y['L104']); P('c102', 966, Y['L102'])

chain([wA, K('sgTm8'), K('sw17cd'), K('sw17ab'), K('sw23'), K('sw25'), K('sgM4'), K('sgR4'), K('sw45'), K('sw48'), K('sw49'), K('wkWk49'), K('sgTm22'), K('sgTm33'), K('sw65'), K('sgTm35'), K('buf35k')])
chain([wD, K('sgTm6'), K('sw11'), K('sw12'), K('sw14'), K('sgTm7'), K('sgTm14'), K('sw26'), K('sgL2'), K('sgS2'), K('sw50'), K('sw51'), K('sgTm23'), K('sgTm29'), K('sw63'), K('sw64'), K('sgTm34'), eW])
chain([wE, K('sgTm4'), K('sw9'), K('sw10'), K('sw13'), K('sw15'), K('sgTm11'), K('sgL1'), K('sgS1'), K('sw53'), K('sw56'), K('sw59'), K('sw62'), K('sgTm32'), eV])
chain([wG, K('sgTm2'), K('sw7'), K('sw8'), K('sgTm5'), K('sgTm9'), K('sw16'), K('sw19'), K('sgTm13'), K('sgL3'), K('sgISpS3'), K('sgS3')])
E(K('sgS3'), K('sw54'), bend_at=1355)
chain([K('sw54'), K('sw57'), K('sw58'), K('sw60'), K('sgTm30'), eU])
chain([wH, K('sgTm1'), K('sw6'), K('sgTm3'), K('sgTm12'), K('sw20'), K('sw27'), K('sgL5'), K('sgS5'), K('sw46')])
E(K('sw46'), K('sw52'), bend_at=1300)
chain([K('sw52'), K('sgTm24'), K('sgTm28'), K('sw61'), K('sgTm31'), eT])
chain([K('buf17k'), K('sgTm17'), K('wkWk28'), K('sw28'), K('sgL7'), K('sgISpS7'), K('sgS7'), K('sw44'), K('buf7b')])
chain([K('buf10k'), K('sgTm10'), K('wkWk18'), K('sw18cd'), K('sw18ab'), K('sw22'), K('sgM6'), K('sgR6'), K('sw43')])
E(K('sw43'), K('sw45'), bend_at=1249)
chain([K('sw21'), K('sgM8'), K('sgR8')])
E(K('sgR8'), K('sw43'), bend_at=1217)
chain([K('sw24'), K('wkWk24'), K('sgTm100'), K('sgTm15'), K('bufCSW')])
chain([K('bufCSE'), K('sgTm21'), K('sgTm136'), K('wkWk47'), K('sw47')])
E(K('sw47'), K('sw48'), bend_at=1300)
E(K('sw24'), K('c104'), bend_at=809)
chain([K('c104'), K('sgTm16'), K('sgTm18'), K('sw28N'), K('sgN104'), K('sgP104'), K('sw42')])
E(K('sw42'), K('sw47'), bend_at=1262)
E(K('sw28N'), K('c102'), bend_at=958)
chain([K('c102'), K('sgN102'), K('sgP102')])
E(K('sgP102'), K('sw42'), bend_at=1226)

labels += [dict(x=6, y=540, text='Łódź Marysin LK16', anchor='start'), dict(x=6, y=940, text='Łódź Chojny LK540', anchor='start'),
           dict(x=1994, y=622, text='Łódź Andrzejów LK17', anchor='end'), dict(x=1994, y=922, text='Łódź Olechów LK541', anchor='end'),
           dict(x=1040, y=360, text='baza „ŁKA” – nastawnia CS', anchor='middle'), dict(x=510, y=1060, text='„ŁW”', anchor='middle')]
platforms += [dict(name='Peron 3', x=925, y=603, w=170, h=58), dict(name='Peron 2', x=925, y=803, w=170, h=58), dict(name='Peron 1', x=925, y=1003, w=170, h=58)]
crossings += [dict(name='przejazd km 5,077', x=875, y0=230, y1=266), dict(name='przejazd km 7,712', x=1875, y0=665, y1=1100)]
dashed += [dict(x0=930, x1=1150, y=Y['LB']), dict(x0=540, x1=600, y=Y['L7'])]

# =====================================================================================================
#  ŁÓDŹ FABRYCZNA (LF) – obraz 1 (część zachodnia)
# =====================================================================================================
post('LF', -2000)
F = dict(T6=383, T4=483, T2=583, T1=683, T3=783, T5=883, T7=983, T9=1083, L=583, Kk=683, J=783, H=883)
SWS([('21', 680, 'T4'), ('22', 715, 'T4'), ('24', 731, 'T2'), ('27', 780, 'T2'), ('28', 796, 'T1'), ('23', 680, 'T3'), ('25', 715, 'T3'),
     ('26', 731, 'T1'), ('29', 830, 'H'), ('31', 846, 'J'), ('30', 870, 'Kk'), ('32', 886, 'J'), ('33', 930, 'J'), ('35', 946, 'Kk'),
     ('34', 970, 'J'), ('36', 986, 'H'), ('37', 1020, 'Kk'), ('38', 1036, 'L')], F)
for a, b in [('22', '24'), ('27', '28'), ('25', '26'), ('29', '31'), ('30', '32'), ('33', '35'), ('34', '36'), ('37', '38')]:
    CONN(a, b)
for t in ['6', '4', '2', '1', '3', '5', '7', '9']:
    y = F['T' + t]
    BUF('k' + t, 120, y, 'kozioł toru ' + t, marker=False)
    SG('E' + t, 160, y, -1, 'wyjazdowy', track=t)
    SG('G' + t, 600, y, 1, 'wyjazdowy', track=t)
for n, x, lane, d in [(21, 695, 'T2', 1), (22, 695, 'T1', 1), (23, 815, 'L', -1), (24, 820, 'Kk', -1), (25, 818, 'H', 1), (26, 815, 'J', 1),
                      (27, 850, 'Kk', 1), (28, 862, 'J', -1), (29, 905, 'Kk', -1), (30, 910, 'J', 1), (31, 955, 'H', 1), (32, 1000, 'J', -1),
                      (33, 1000, 'Kk', 1), (34, 1004, 'H', -1), (35, 1060, 'Kk', -1), (36, 1060, 'L', -1)]:
    TM(n, x, F[lane], d)
BUF('4a', 760, F['T4'], 'tor 4a'); BUF('3a', 760, F['T3'], 'tor 3a')
fL = SG('L', 1100, F['L'], -1, 'wjazdowy', szlak='2T'); fK = SG('K', 1100, F['Kk'], -1, 'wjazdowy', szlak='1T')
fJ = SG('J', 1100, F['J'], -1, 'wjazdowy', szlak='102T'); fH = SG('H', 1100, F['H'], -1, 'wjazdowy', szlak='101T')
chain([K('bufk6'), K('sgE6'), K('sgG6')]); E(K('sgG6'), K('sw21'), bend_at=650)
chain([K('bufk4'), K('sgE4'), K('sgG4'), K('sw21'), K('sw22'), K('buf4a')])
chain([K('bufk2'), K('sgE2'), K('sgG2'), K('sgTm21'), K('sw24'), K('sw27'), K('sgTm23'), K('sw38'), K('sgTm36'), fL])
chain([K('bufk1'), K('sgE1'), K('sgG1'), K('sgTm22'), K('sw26'), K('sw28'), K('sgTm24'), K('sgTm27'), K('sw30'), K('sgTm29'), K('sw35'), K('sgTm33'), K('sw37'), K('sgTm35'), fK])
chain([K('bufk3'), K('sgE3'), K('sgG3'), K('sw23'), K('sw25'), K('buf3a')])
chain([K('bufk5'), K('sgE5'), K('sgG5')]); E(K('sgG5'), K('sw23'), bend_at=650)
chain([K('bufk7'), K('sgE7'), K('sgG7')]); E(K('sgG7'), K('sgTm26'), bend_at=795)
chain([K('sgTm26'), K('sw31'), K('sgTm28'), K('sw32'), K('sgTm30'), K('sw33'), K('sw34'), K('sgTm32'), fJ])
chain([K('bufk9'), K('sgE9'), K('sgG9')]); E(K('sgG9'), K('sgTm25'), bend_at=805)
chain([K('sgTm25'), K('sw29'), K('sgTm31'), K('sw36'), K('sgTm34'), fH])
labels += [dict(x=-1660, y=1125, text='„ŁF”', anchor='middle'), dict(x=-1950, y=330, text='stacja podziemna', anchor='start')]
platforms += [dict(name='Peron 1', x=-1770, y=403, w=320, h=60), dict(name='Peron 2', x=-1770, y=603, w=320, h=60),
              dict(name='Peron 3', x=-1770, y=803, w=320, h=60), dict(name='Peron 4', x=-1770, y=1003, w=320, h=60)]

# =====================================================================================================
#  ŁÓDŹ NICIARNIANA (LN) – posterunek odgałęźny, obraz 1 (część wschodnia)
# =====================================================================================================
post('LN', -2000)
Nn = dict(L=683, Kk=783, J=883, H=983)
SWS([('1', 1508, 'J'), ('2', 1560, 'J'), ('3', 1576, 'Kk'), ('4', 1640, 'Kk'), ('5', 1656, 'L')], Nn)
for a, b in [('2', '3'), ('4', '5')]:
    CONN(a, b)
nB2 = SG('B2', 1440, Nn['L'], 1, 'wyjazdowy', szlak='2T'); nB1 = SG('B1', 1440, Nn['Kk'], 1, 'wyjazdowy', szlak='1T')
nB102 = SG('B102', 1440, Nn['J'], 1, 'wyjazdowy', szlak='102T'); nB101 = SG('B101', 1440, Nn['H'], 1, 'wyjazdowy', szlak='101T')
nC2 = SG('C2', 1760, Nn['L'], -1, 'wyjazdowy', szlak='2F'); nC1 = SG('C1', 1760, Nn['Kk'], -1, 'wyjazdowy', szlak='1F')
P('h1', 1492, Nn['H'])
BUF('11k', 1620, Nn['J'], 'tor 11')
chain([nB2, K('sw5'), nC2]); chain([nB1, K('sw3'), K('sw4'), nC1]); chain([nB102, K('sw1'), K('sw2'), K('buf11k')]); chain([nB101, K('h1')])
E(K('h1'), K('sw1'))
platforms += [dict(name='Niciarniana', x=-535, y=702, w=130, h=62)]
labels += [dict(x=-560, y=470, text='Łódź Niciarniana (podg)', anchor='middle'), dict(x=-740, y=1125, text='tunel – LK17 / LK458', anchor='middle')]

# =====================================================================================================
#  SZLAKI WEWNĘTRZNE
# =====================================================================================================
post('LF', -2000)
int_szlak('2T', K('sgL'), 'LN:sgB2', K('L'), 'LN:B2', (-830, -650), -1, 'tunel 2', '17', '2', lab_e=('26RN', '2T3'), lab_w=('2T2', '26R'), bend_at=-740, nbr_w='Łódź Fabryczna', nbr_e='Łódź Niciarniana')
int_szlak('1T', K('sgK'), 'LN:sgB1', K('K'), 'LN:B1', (-830, -650), 1, 'tunel 1', '17', '1', lab_e=('21R', '1T3'), lab_w=('1T2', '21RN'), bend_at=-740, nbr_w='Łódź Fabryczna', nbr_e='Łódź Niciarniana')
int_szlak('102T', K('sgJ'), 'LN:sgB102', K('J'), 'LN:B102', (-830, -650), -1, 'tunel 102', '458', '102', lab_e=('26DN', 'T3'), lab_w=('T2', '26D'), bend_at=-740, nbr_w='Łódź Fabryczna', nbr_e='Łódź Niciarniana')
int_szlak('101T', K('sgH'), 'LN:sgB101', K('H'), 'LN:B101', (-830, -650), 1, 'tunel 101', '458', '101', lab_e=('21D', 'T5'), lab_w=('T4', '21DN'), bend_at=-740, nbr_w='Łódź Fabryczna', nbr_e='Łódź Niciarniana')
int_szlak('2F', 'LN:sgC2', 'LW:sgD', 'LN:C2', 'LW:D', (-120, 110), -1, 'it2F', '17', '2', lab_e=('2F1', '2F3'), lab_w=('2F2', '2F4'), nbr_w='Łódź Niciarniana', nbr_e='Łódź Widzew')
int_szlak('1F', 'LN:sgC1', 'LW:sgE', 'LN:C1', 'LW:E', (-120, 110), 1, 'it1F', '17', '1', lab_e=('1F1', '1F3'), lab_w=('1F2', '1F4'), nbr_w='Łódź Niciarniana', nbr_e='Łódź Widzew')

# =====================================================================================================
#  ODCINKI (kontrola niezajętości)
# =====================================================================================================
swnode = {v['node']: k for k, v in switches.items()}
signode = {v['node'] for v in signals.values() if v['type'] in ('wjazdowy', 'wyjazdowy', 'tm')}
seq = [0]


def plen(pts):
    return sum(math.dist(pts[i], pts[i + 1]) for i in range(len(pts) - 1))


def split(e, f):
    pts = e['pts']; total = plen(pts); target = total * f; acc = 0
    for i in range(len(pts) - 1):
        d = math.dist(pts[i], pts[i + 1])
        if acc + d >= target:
            t = (target - acc) / d if d else 0
            p = [round(pts[i][0] + (pts[i + 1][0] - pts[i][0]) * t, 1), round(pts[i][1] + (pts[i + 1][1] - pts[i][1]) * t, 1)]
            pa, pb = pts[:i + 1] + [p], [p] + pts[i + 1:]
            break
        acc += d
    seq[0] += 1
    nid = 'j:%d' % seq[0]
    nodes[nid] = dict(x=p[0], y=p[1], kind='', ref='', post=nodes[e['a']]['post'])
    return dict(e, b=nid, pts=pa), dict(e, a=nid, pts=pb)


def sec_of(nid):
    return switches[swnode[nid]]['sec']


out = []
for e in edges:
    if e.get('sec'):
        out.append(e); continue
    a_sw, b_sw = e['a'] in swnode, e['b'] in swnode
    L = plen(e['pts'])
    if a_sw and b_sw:
        if sec_of(e['a']) == sec_of(e['b']):
            e['sec'] = sec_of(e['a']); out.append(e)
        else:
            e1, e2 = split(e, 0.5); e1['sec'] = sec_of(e['a']); e2['sec'] = sec_of(e['b']); out += [e1, e2]
    elif a_sw or b_sw:
        s = e['a'] if a_sw else e['b']
        other = e['b'] if a_sw else e['a']
        if L > 46 or (other in track_ends and L > 28):
            f = 18 / L if a_sw else 1 - 18 / L
            e1, e2 = split(e, f)
            (e1 if a_sw else e2)['sec'] = sec_of(s)
            out += [e1, e2]
        else:
            e['sec'] = sec_of(s); out.append(e)
    else:
        out.append(e)
edges = out
rest = [e for e in edges if not e.get('sec')]
parent = {id(e): id(e) for e in rest}


def find(x):
    while parent[x] != x:
        parent[x] = parent[parent[x]]; x = parent[x]
    return x


bynode = {}
for e in rest:
    bynode.setdefault(e['a'], []).append(id(e)); bynode.setdefault(e['b'], []).append(id(e))
for n, es in bynode.items():
    if n in swnode or n in signode:
        continue
    for x in es[1:]:
        parent[find(x)] = find(es[0])
groups = {}
for e in rest:
    groups.setdefault(find(id(e)), []).append(e)
station_tracks = {}
for k, v in signals.items():
    if v['type'] == 'wyjazdowy' and v.get('track'):
        station_tracks.setdefault((v['post'], v['track']), []).append(v['node'])
cnt = {}
for g, es in groups.items():
    ns = {e['a'] for e in es} | {e['b'] for e in es}
    p = nodes[es[0]['a']]['post']
    name = None
    for (pp, t), sn in station_tracks.items():
        if pp == p and len(sn) == 2 and sn[0] in ns and sn[1] in ns:
            name = '%s:JT%s' % (p, t)
            for e in es:
                e['track'] = t
    bufs = [n for n in ns if n in track_ends]
    if not name and bufs:
        name = '%s:it%s' % (p, track_ends[bufs[0]]['disp'])
    if not name:
        cnt[p] = cnt.get(p, 0) + 1
        name = '%s:it%d' % (p, cnt[p])
    for e in es:
        e['sec'] = name

for e in edges:
    L = plen(e['pts'])
    e['len'] = round(L * (9 if e.get('szlak') else 1.6), 1) if L > 0 else 6
    e['len'] = max(6, e['len'])
    e.setdefault('track', ''); e.setdefault('line', '')
for i, e in enumerate(edges):
    e['id'] = 'e%d' % i

boundaries = {}
for k, b in BLOCKS.items():
    for end in b['ends']:
        boundaries[end['boundary']] = dict(szlak=k)
far_ends = {b['far']: dict(szlak=k, nbr=b['nbr']) for k, b in BLOCKS.items() if b['far']}
for v in signals.values():
    v.setdefault('label', v['name'].split(':')[-1])

out = dict(name='LCS Łódź Widzew', code='LCS', width=2000, height=1125, pages=PAGES, posts=POSTS,
           source='Układy: plany schematyczne Semaforek (stan 01.2017) i wzór pulpitu; zależności – założenia do weryfikacji.',
           nodes=nodes, edges=edges, switches=switches, signals=signals,
           derailers={k: dict(v, sec=next((e['sec'] for e in edges if v['node'] in (e['a'], e['b'])), '')) for k, v in derailers.items()},
           blocks=BLOCKS, boundaries=boundaries, farEnds=far_ends, trackEnds=track_ends, platforms=platforms, labels=labels,
           crossings=crossings, dashed=dashed)

if __name__ == '__main__':
    json.dump(out, open(sys.argv[1], 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('węzły', len(nodes), 'krawędzie', len(edges), 'zwrotnice', len(switches), 'sygnalizatory', len(signals), 'odcinki', len({e['sec'] for e in edges}), 'szlaki', list(BLOCKS))
    bad = [k for k, v in switches.items() if sum(1 for e in edges if v['node'] in (e['a'], e['b'])) != 3]
    print('zwrotnice ze złą liczbą krawędzi:', bad)
    print('krawędzie pionowe:', [e['id'] for e in edges if abs(nodes[e['a']]['x'] - nodes[e['b']]['x']) < 0.5])
    if len(sys.argv) > 2:
        from PIL import Image, ImageDraw
        img = Image.new('RGB', (4000, 1125)); dr = ImageDraw.Draw(img)
        for e in edges:
            dr.line([(p[0] + 2000, p[1]) for p in e['pts']], fill=(170, 170, 170), width=4)
        for k, v in switches.items():
            n = nodes[v['node']]; dr.text((n['x'] + 2000 - 6, n['y'] + 6), v['label'], fill=(255, 255, 255))
        for k, v in signals.items():
            n = nodes[v['node']]; c = {'wjazdowy': (255, 200, 0), 'wyjazdowy': (255, 200, 0), 'tm': (0, 200, 200)}.get(v['type'], (120, 120, 120))
            x = n['x'] + 2000
            dr.polygon([(x + 7 * v['dir'], n['y']), (x - 5 * v['dir'], n['y'] - 6), (x - 5 * v['dir'], n['y'] + 6)], fill=c)
            dr.text((x - 8, n['y'] + (10 if v['dir'] > 0 else -20)), v['label'], fill=c)
        img.save(sys.argv[2])
