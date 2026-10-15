'use strict';
/*
 * Model topologiczny stacji Łódź Widzew (ŁW) – układ UPROSZCZONY.
 *
 * Środek stacji (tory 1–8, perony 1–3, nazwy semaforów L/S/M/R) odwzorowano wg
 * „Schematu nowego układu torowego i peronów na stacji Łódź Widzew” (PKP PLK, 2013).
 * Głowice rozjazdowe, numeracja zwrotnic, nazwy semaforów wjazdowych (A,B,C / X,Y,Z),
 * długości odstępów i typy blokad są UMOWNE – do weryfikacji z regulaminem technicznym
 * posterunku i planem schematycznym stacji.
 *
 * Konwencja: oś x rośnie na wschód (w stronę Andrzejowa/Koluszek). dir=+1 – ruch na wschód.
 */
window.SIM = window.SIM || {};
(function (S) {

  function Builder() {
    this.nodes = {}; this.edges = {}; this.sections = {};
    this.signals = {}; this.switches = {}; this.derailers = {};
    this.boundaries = {};   // węzeł -> {szlak} – granica stacji (koniec przebiegu wyjazdowego)
    this.farEnds = {};      // węzeł -> {szlak, nbr} – sąsiedni posterunek (źródło/ujście pociągów)
    this.trackEnds = {};    // węzeł -> {id} – kozioł oporowy (koniec przebiegu manewrowego)
    this.blocks = {};       // szlak -> definicja blokady liniowej
    this.platforms = []; this.labels = [];
  }
  Builder.prototype.node = function (id, x, y, o) {
    this.nodes[id] = Object.assign({ id: id, x: x, y: y, edges: [] }, o || {});
  };
  Builder.prototype.edge = function (a, b, sec, o) {
    if (!this.nodes[a] || !this.nodes[b]) throw new Error('Brak węzła ' + a + '/' + b);
    var id = a + '~' + b;
    var e = Object.assign({ id: id, a: a, b: b, sec: sec }, o || {});
    var na = this.nodes[a], nb = this.nodes[b];
    e.px = Math.hypot(na.x - nb.x, na.y - nb.y);
    e.len = e.len || Math.round(e.px * 1.6); // metry
    this.edges[id] = e; na.edges.push(id); nb.edges.push(id);
    if (!this.sections[sec]) this.sections[sec] = { id: sec, edges: [], kind: o && o.kind || 'tor' };
    this.sections[sec].edges.push(id);
  };
  Builder.prototype.chain = function (ids, sec, o) {
    for (var i = 0; i < ids.length - 1; i++) this.edge(ids[i], ids[i + 1], sec, o);
  };
  Builder.prototype.sw = function (node, num) { this.switches[num] = { num: num, node: node, sec: 'z' + num }; this.nodes[node].sw = num; };
  Builder.prototype.sig = function (name, node, dir, type, o) {
    this.signals[name] = Object.assign({ name: name, node: node, dir: dir, type: type }, o || {});
    (this.nodes[node].sigs = this.nodes[node].sigs || []).push(name);
  };

  // Ustalenie geometrii zwrotnic: ostrze = strona z jedną krawędzią; „+” = gałąź bliższa poziomowi.
  Builder.prototype.finalize = function () {
    var self = this;
    Object.keys(this.switches).forEach(function (num) {
      var sw = self.switches[num], n = self.nodes[sw.node];
      if (n.edges.length !== 3) throw new Error('Zwrotnica ' + num + ' ma ' + n.edges.length + ' krawędzi');
      var side = n.edges.map(function (eid) { var o = self.other(eid, n.id); return Math.sign(self.nodes[o].x - n.x); });
      var east = side.filter(function (s) { return s > 0; }).length;
      var tipSide = east === 1 ? 1 : -1;
      var tipIdx = side.indexOf(tipSide);
      sw.tip = n.edges[tipIdx];
      var legs = n.edges.filter(function (_, i) { return i !== tipIdx; });
      var slope = function (eid) { var o = self.nodes[self.other(eid, n.id)]; return Math.abs((o.y - n.y) / (o.x - n.x)); };
      legs.sort(function (a, b) { return slope(a) - slope(b); });
      sw.plus = legs[0]; sw.minus = legs[1];
      sw.tipDir = tipSide; // po której stronie (x) leży ostrze
    });
    Object.keys(this.signals).forEach(function (name) {
      var s = self.signals[name];
      var back = self.nodes[s.node].edges.filter(function (eid) { var o = self.nodes[self.other(eid, s.node)]; return Math.sign(o.x - self.nodes[s.node].x) === -s.dir; });
      s.approachSec = back.length === 1 ? self.edges[back[0]].sec : null;
    });
    return this;
  };
  Builder.prototype.other = function (eid, nid) { var e = this.edges[eid]; return e.a === nid ? e.b : e.a; };

  // ---------------------------------------------------------------------------------------
  var TRACK_Y = { 8: 120, 6: 160, 4: 200, 2: 260, 1: 300, 3: 360, 5: 400, 7: 460 };
  var X_STW = 740, X_STE = 1060;

  function half(B, side) {
    var W = side === 'W';
    var X = function (x) { return W ? x : 1800 - x; };
    var P = function (id) { return (W ? 'W_' : 'E_') + id; };
    var swn = function (n) { return W ? n : n + 20; };
    var z = function (n) { return 'z' + swn(n); };
    var sz = W ? { t1: 'WF1', t2: 'WF2', s: 'WD' } : { t1: 'WA1', t2: 'WA2', s: 'WO' };
    var nb = W ? { t: 'Łódź Fabryczna', s: 'Łódź Dąbrowa / Chojny (l. 540)' } : { t: 'Łódź Andrzejów', s: 'Łódź Olechów (l. 541)' };
    var inward = W ? 1 : -1;
    var N = function (id, x, y, o) { B.node(P(id), X(x), y, o); };
    var E = function (a, b, sec, o) { B.edge(P(a), P(b), sec, o); };

    // szlaki
    N('F2far', 40, 260); N('F2o', 170, 260); N('B', 300, 260);
    N('F1far', 40, 300); N('F1o', 170, 300); N('A', 300, 300);
    N('Dfar', 40, 460); N('C', 300, 460);
    // głowica
    N('n1', 350, 260); N('m12', 375, 280); N('n2', 400, 300); N('mt1a', 420, 300); N('mt2a', 420, 260);
    N('n3', 440, 300); N('m34', 465, 280); N('n4', 490, 260); N('mt2b', 510, 260); N('mt1b', 485, 300);
    N('n5', 530, 260); N('n11', 530, 300);
    N('mn5', 560, 230); N('n7', 590, 200); N('m79', 610, 180); N('n9', 630, 160); N('m910', 650, 140); N('n10', 670, 120);
    N('tm', 630, 120); N('wk', 520, 120); N('kz', 400, 120);
    N('m1113', 560, 330); N('n13', 590, 360); N('m1315', 610, 380); N('n15', 630, 400); N('m1517', 660, 430); N('n17', 690, 460);
    N('mC', 600, 460);

    var od = { kind: 'szlak' };
    E('F2far', 'F2o', sz.t2 + '-2', Object.assign({ len: 1900 }, od)); E('F2o', 'B', sz.t2 + '-1', Object.assign({ len: 1700 }, od));
    E('F1far', 'F1o', sz.t1 + '-2', Object.assign({ len: 1900 }, od)); E('F1o', 'A', sz.t1 + '-1', Object.assign({ len: 1700 }, od));
    E('Dfar', 'C', sz.s, Object.assign({ len: 3200 }, od));

    var zw = { kind: 'zwr' };
    E('B', 'n1', z(1), zw); E('n1', 'mt2a', z(1), zw); E('n1', 'm12', z(1), zw);
    E('m12', 'n2', z(2), zw); E('A', 'n2', z(2), zw); E('n2', 'mt1a', z(2), zw);
    E('mt1a', 'n3', z(3), zw); E('n3', 'mt1b', z(3), zw); E('n3', 'm34', z(3), zw);
    E('m34', 'n4', z(4), zw); E('mt2a', 'n4', z(4), zw); E('n4', 'mt2b', z(4), zw);
    E('mt2b', 'n5', z(5), zw); E('n5', 'p2', z(5), zw); E('n5', 'mn5', z(5), zw);
    E('mn5', 'n7', z(7), zw); E('n7', 'p4', z(7), zw); E('n7', 'm79', z(7), zw);
    E('m79', 'n9', z(9), zw); E('n9', 'p6', z(9), zw); E('n9', 'm910', z(9), zw);
    E('m910', 'n10', z(10), zw); E('n10', 'p8', z(10), zw); E('tm', 'n10', z(10), zw);
    var wyc = W ? 'W8' : 'E8';
    E('wk', 'tm', wyc); E('kz', 'wk', wyc);
    E('mt1b', 'n11', z(11), zw); E('n11', 'p1', z(11), zw); E('n11', 'm1113', z(11), zw);
    E('m1113', 'n13', z(13), zw); E('n13', 'p3', z(13), zw); E('n13', 'm1315', z(13), zw);
    E('m1315', 'n15', z(15), zw); E('n15', 'p5', z(15), zw); E('n15', 'm1517', z(15), zw);
    E('m1517', 'n17', z(17), zw); E('n17', 'p7', z(17), zw); E('mC', 'n17', z(17), zw);
    E('C', 'mC', W ? 'C7' : 'X7');

    [1, 2, 3, 4, 5, 7, 9, 10, 11, 13, 15, 17].forEach(function (n) { B.sw(P('n' + n), swn(n)); });
    B.derailers[W ? 'Wk23' : 'Wk43'] = { name: W ? 'Wk23' : 'Wk43', node: P('wk'), sec: wyc };

    // semafory wjazdowe (umowne nazwy), tarcza manewrowa, odstępowe
    B.sig(W ? 'A' : 'Y', P('A'), inward, 'wjazdowy', { szlak: sz.t1 });
    B.sig(W ? 'B' : 'Z', P('B'), inward, 'wjazdowy', { szlak: sz.t2 });
    B.sig(W ? 'C' : 'X', P('C'), inward, 'wjazdowy', { szlak: sz.s });
    B.sig(W ? 'Tm14' : 'Tm34', P('tm'), inward, 'tm');
    [['F1o', sz.t1], ['F2o', sz.t2]].forEach(function (p) {
      B.sig(p[1] + '/o+', P(p[0]), 1, 'odstepowy', { szlak: p[1], auto: true });
      B.sig(p[1] + '/o-', P(p[0]), -1, 'odstepowy', { szlak: p[1], auto: true });
    });

    B.boundaries[P('A')] = { szlak: sz.t1 }; B.boundaries[P('B')] = { szlak: sz.t2 }; B.boundaries[P('C')] = { szlak: sz.s };
    B.farEnds[P('F1far')] = { szlak: sz.t1, nbr: nb.t }; B.farEnds[P('F2far')] = { szlak: sz.t2, nbr: nb.t }; B.farEnds[P('Dfar')] = { szlak: sz.s, nbr: nb.s };
    B.trackEnds[P('kz')] = { id: W ? 'KZW' : 'KZE', label: W ? 'tor wyciągowy / zaplecze' : 'tor do zaplecza ŁKA' };

    var outward = -inward;
    B.blocks[sz.t1] = { id: sz.t1, type: 'sbl', nbr: nb.t, side: side, outward: outward, secs: [sz.t1 + '-1', sz.t1 + '-2'], defDir: W ? 1 : 1, entrySig: W ? 'A' : 'Y', boundary: P('A'), far: P('F1far'), tor: 1 };
    B.blocks[sz.t2] = { id: sz.t2, type: 'sbl', nbr: nb.t, side: side, outward: outward, secs: [sz.t2 + '-1', sz.t2 + '-2'], defDir: -1, entrySig: W ? 'B' : 'Z', boundary: P('B'), far: P('F2far'), tor: 2 };
    B.blocks[sz.s] = { id: sz.s, type: 'psb', nbr: nb.s, side: side, outward: outward, secs: [sz.s], defPerm: W ? 'local' : 'remote', entrySig: W ? 'C' : 'X', boundary: P('C'), far: P('Dfar'), tor: 1 };

    B.labels.push({ x: X(40), y: 236, text: nb.t, anchor: W ? 'start' : 'end', cls: 'lbl-nbr' });
    B.labels.push({ x: X(40), y: 440, text: nb.s, anchor: W ? 'start' : 'end', cls: 'lbl-nbr' });
    B.labels.push({ x: X(400), y: 106, text: B.trackEnds[P('kz')].label, anchor: W ? 'start' : 'end', cls: 'lbl-small' });
  }

  function build() {
    var B = new Builder();
    [8, 6, 4, 2, 1, 3, 5, 7].forEach(function (k) {
      B.node('W_p' + k, X_STW, TRACK_Y[k]); B.node('E_p' + k, X_STE, TRACK_Y[k]);
    });
    half(B, 'W'); half(B, 'E');
    var westNames = { 8: 'M8', 6: 'M6', 4: 'M4', 2: 'L2', 1: 'L1', 3: 'L3', 5: 'L5', 7: 'L7' };
    var eastNames = { 8: 'R8', 6: 'R6', 4: 'R4', 2: 'S2', 1: 'S1', 3: 'S3', 5: 'S5', 7: 'S7' };
    [8, 6, 4, 2, 1, 3, 5, 7].forEach(function (k) {
      B.edge('W_p' + k, 'E_p' + k, 'T' + k, { kind: 'stacyjny', track: k, platform: [1, 2, 3, 4, 5, 7].indexOf(k) >= 0 });
      B.sig(westNames[k], 'W_p' + k, -1, 'wyjazdowy');
      B.sig(eastNames[k], 'E_p' + k, 1, 'wyjazdowy');
      B.labels.push({ x: 900, y: TRACK_Y[k] - 5, text: String(k), anchor: 'middle', cls: 'lbl-track' });
    });
    B.platforms.push({ x: 780, y: 214, w: 240, h: 32, name: 'Peron 3' });
    B.platforms.push({ x: 780, y: 314, w: 240, h: 32, name: 'Peron 2' });
    B.platforms.push({ x: 780, y: 414, w: 240, h: 32, name: 'Peron 1' });
    B.labels.push({ x: 900, y: 520, text: 'ŁÓDŹ WIDZEW  (ŁW)', anchor: 'middle', cls: 'lbl-station' });
    B.labels.push({ x: 900, y: 540, text: 'układ uproszczony – środek wg schematu PKP PLK 2013, głowice umowne', anchor: 'middle', cls: 'lbl-small' });
    B.finalize();
    B.trackY = TRACK_Y;
    return B;
  }

  S.LAYOUT = build();
  S.Builder = Builder;
})(window.SIM);
