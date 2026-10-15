'use strict';
/*
 * Zobrazowanie wg Załącznika 4 do Ie-20 (wyciąg z Ie-104): czarne tło, barwy:
 * szary – stan podstawowy, czerwony – zajętość, zielony – przebieg pociągowy,
 * żółty – przebieg manewrowy / droga ochronna, różowy (magenta) – zwalnianie czasowe / zamknięcie,
 * biały – brak danych / brak kontroli, ciemnoczerwony – oczekiwanie na pierwszy przejazd.
 */
(function (S) {
  var L = S.LAYOUT;
  var NS = 'http://www.w3.org/2000/svg';
  var C = S.COLORS = {
    grey: '#c0c0c0', red: '#ff0000', green: '#00ff00', yellow: '#ffff00', magenta: '#ff00ff', white: '#ffffff',
    cyan: '#00ffff', paleCyan: '#00c0c0', darkRed: '#800000', orange: '#ffc000', azure: '#00c0ff', dim: '#3a3a3a'
  };
  var R = S.render = { els: {} };

  function el(tag, attrs, parent) {
    var e = document.createElementNS(NS, tag);
    for (var k in attrs) e.setAttribute(k, attrs[k]);
    if (parent) parent.appendChild(e);
    return e;
  }
  R.el = el;

  function legOf(eid) {
    // czy krawędź jest gałęzią zwrotnicy; zwraca {num, leg:'+'|'-', node}
    for (var n in L.switches) { var s = L.switches[n]; if (s.plus === eid) return { num: n, leg: '+', node: s.node }; if (s.minus === eid) return { num: n, leg: '-', node: s.node }; }
    return null;
  }

  R.build = function (svg) {
    R.svg = svg;
    svg.innerHTML = '';
    el('rect', { x: 0, y: 0, width: 1800, height: 580, fill: '#000' }, svg);
    var gStatic = el('g', {}, svg);
    L.platforms.forEach(function (p) {
      el('rect', { x: p.x, y: p.y + 6, width: p.w, height: p.h - 12, fill: '#1c1c1c', stroke: '#555', 'stroke-width': 1 }, gStatic);
      var t = el('text', { x: p.x + p.w / 2, y: p.y + p.h / 2 + 4, 'text-anchor': 'middle', class: 'lbl-platform' }, gStatic); t.textContent = p.name;
    });
    L.labels.forEach(function (l) { var t = el('text', { x: l.x, y: l.y, 'text-anchor': l.anchor || 'start', class: l.cls }, gStatic); t.textContent = l.text; });

    // krawędzie
    var gEdges = el('g', {}, svg);
    R.edges = {};
    Object.keys(L.edges).forEach(function (eid) {
      var e = L.edges[eid], a = L.nodes[e.a], b = L.nodes[e.b];
      var g = el('g', { class: 'edge', 'data-sec': e.sec }, gEdges);
      var hit = el('line', { x1: a.x, y1: a.y, x2: b.x, y2: b.y, class: 'hit' }, g);
      var main = el('line', { x1: a.x, y1: a.y, x2: b.x, y2: b.y, stroke: C.grey, 'stroke-width': 4, 'stroke-linecap': 'butt' }, g);
      var inner = el('line', { x1: a.x, y1: a.y, x2: b.x, y2: b.y, stroke: '#000', 'stroke-width': 1.6, visibility: 'hidden' }, g);
      hit.addEventListener('contextmenu', function (ev) { ev.preventDefault(); S.ui.ctxSection(e.sec, ev); });
      hit.addEventListener('mousemove', function () { S.ui.hint(sectionHint(e.sec)); });
      R.edges[eid] = { g: g, main: main, inner: inner, a: a, b: b, leg: legOf(eid) };
    });

    // izolacje (znaczniki granic odcinków) – krótkie kreski poprzeczne
    var gIso = el('g', {}, svg);
    Object.keys(L.nodes).forEach(function (nid) {
      var n = L.nodes[nid]; if (n.sw || n.edges.length !== 2) return;
      var s1 = L.edges[n.edges[0]].sec, s2 = L.edges[n.edges[1]].sec; if (s1 === s2) return;
      el('line', { x1: n.x, y1: n.y - 5, x2: n.x, y2: n.y + 5, stroke: '#666', 'stroke-width': 1 }, gIso);
    });

    // zwrotnice
    var gSw = el('g', {}, svg);
    R.sw = {};
    Object.keys(L.switches).forEach(function (num) {
      var s = L.switches[num], n = L.nodes[s.node];
      var mo = L.nodes[S.other(s.minus, s.node)];
      var below = mo.y < n.y; // gałąź zwrotna w górę -> etykieta pod torem
      var g = el('g', { class: 'sw' }, gSw);
      var mark = el('rect', { x: n.x - 5, y: n.y - 5, width: 10, height: 10, fill: 'none', visibility: 'hidden' }, g);
      var lbl = el('text', { x: n.x - s.tipDir * 2, y: below ? n.y + 17 : n.y - 9, 'text-anchor': 'middle', class: 'lbl-sw' }, g); lbl.textContent = num;
      var sel = el('rect', { x: n.x - 11, y: (below ? n.y + 6 : n.y - 20), width: 22, height: 15, fill: 'none', stroke: C.green, 'stroke-width': 1.5, visibility: 'hidden' }, g);
      var hit = el('circle', { cx: n.x, cy: n.y, r: 11, class: 'hit-c' }, g);
      var hit2 = el('rect', { x: n.x - 11, y: (below ? n.y + 6 : n.y - 20), width: 22, height: 15, class: 'hit-c' }, g);
      [hit, hit2].forEach(function (h) {
        h.addEventListener('contextmenu', function (ev) { ev.preventDefault(); S.ui.ctxSwitch(num, ev); });
        h.addEventListener('click', function (ev) { S.ui.ctxSwitch(num, ev); });
        h.addEventListener('mousemove', function () { S.ui.hint(switchHint(num)); });
      });
      R.sw[num] = { mark: mark, lbl: lbl, sel: sel };
    });

    // wykolejnice
    R.der = {};
    Object.keys(L.derailers).forEach(function (name) {
      var d = L.derailers[name], n = L.nodes[d.node];
      var g = el('g', {}, gSw);
      var bar = el('line', { x1: n.x - 6, y1: n.y - 8, x2: n.x + 6, y2: n.y - 8, stroke: C.grey, 'stroke-width': 3 }, g);
      var t = el('text', { x: n.x, y: n.y - 13, 'text-anchor': 'middle', class: 'lbl-sw' }, g); t.textContent = name;
      var hit = el('rect', { x: n.x - 16, y: n.y - 26, width: 32, height: 24, class: 'hit-c' }, g);
      hit.addEventListener('contextmenu', function (ev) { ev.preventDefault(); S.ui.ctxDerailer(name, ev); });
      hit.addEventListener('click', function (ev) { S.ui.ctxDerailer(name, ev); });
      hit.addEventListener('mousemove', function () { var st = S.st.derailers[name]; S.ui.hint('Wykolejnica ' + name + ': ' + (st.on ? 'założona na tor' : 'zdjęta z toru') + (st.lock ? ', utwierdzona w przebiegu' : '')); });
      R.der[name] = { bar: bar, n: n };
    });

    // kozły oporowe (końce przebiegów manewrowych)
    Object.keys(L.trackEnds).forEach(function (nid) {
      var n = L.nodes[nid], te = L.trackEnds[nid];
      var g = el('g', { class: 'clickable' }, gSw);
      el('line', { x1: n.x, y1: n.y - 7, x2: n.x, y2: n.y + 7, stroke: C.grey, 'stroke-width': 3 }, g);
      var t = el('text', { x: n.x + (n.x < 900 ? -6 : 6), y: n.y + 4, 'text-anchor': n.x < 900 ? 'end' : 'start', class: 'lbl-sig' }, g); t.textContent = te.id;
      var hit = el('rect', { x: n.x - 30, y: n.y - 10, width: 60, height: 20, class: 'hit-c' }, g);
      hit.addEventListener('click', function (ev) { S.ui.clickDest(te.id, ev); });
      hit.addEventListener('mousemove', function () { S.ui.hint('Koniec toru ' + te.id + ' – ' + te.label + ' (cel przebiegu manewrowego)'); });
    });

    // semafory
    var gSig = el('g', {}, svg);
    R.sig = {};
    Object.keys(L.signals).forEach(function (name) {
      var s = L.signals[name], n = L.nodes[s.node], d = s.dir;
      var off = d > 0 ? 14 : -14, x = n.x, y = n.y + off;
      var g = el('g', { class: 'sig' }, gSig);
      var bg = el('rect', { x: x - 12, y: y - 9, width: 24, height: 18, fill: 'none', visibility: 'hidden' }, g);
      el('line', { x1: x, y1: n.y + (d > 0 ? 3 : -3), x2: x, y2: y + (d > 0 ? -6 : 6), stroke: '#777', 'stroke-width': 1 }, g);
      var shape;
      if (s.type === 'odstepowy') {
        shape = el('polygon', { points: tri(x, y, d, 5), fill: C.grey }, g);
      } else if (s.type === 'tm') {
        shape = el('polyline', { points: chev(x, y, d, 6), fill: 'none', stroke: C.grey, 'stroke-width': 2.4 }, g);
      } else {
        shape = el('g', {}, g);
        el('polygon', { points: tri(x + d * 3, y, d, 6) }, shape);
        el('polyline', { points: chev(x - d * 5, y, d, 6), fill: 'none', 'stroke-width': 2.4 }, shape);
      }
      var sel = el('rect', { x: x - 13, y: y - 10, width: 26, height: 20, fill: 'none', stroke: C.green, 'stroke-width': 1.5, visibility: 'hidden' }, g);
      var lbl = null;
      if (s.type !== 'odstepowy') {
        lbl = el('text', { x: x - d * 16, y: y + 4, 'text-anchor': d > 0 ? 'end' : 'start', class: 'lbl-sig' }, g); lbl.textContent = name;
        var hit = el('rect', { x: x - 14 - (d > 0 ? 30 : 0), y: y - 11, width: 58, height: 22, class: 'hit-c' }, g);
        hit.addEventListener('click', function (ev) { S.ui.clickSignal(name, ev); });
        hit.addEventListener('contextmenu', function (ev) { ev.preventDefault(); S.ui.ctxSignal(name, ev); });
        hit.addEventListener('mousemove', function () { S.ui.hint(signalHint(name)); });
      }
      R.sig[name] = { g: g, shape: shape, bg: bg, sel: sel, lbl: lbl, type: s.type };
    });

    // blokady liniowe
    var gBl = el('g', {}, svg);
    R.bl = {};
    Object.keys(L.blocks).forEach(function (id) {
      var b = L.blocks[id], bn = L.nodes[b.boundary], fn = L.nodes[b.far];
      var cx = (bn.x + fn.x) / 2 + (b.type === 'psb' ? 0 : (b.side === 'W' ? 25 : -25)), y = bn.y + (b.tor === 2 ? -22 : 24);
      if (b.type === 'psb') y = bn.y + 24;
      var g = el('g', { class: 'clickable' }, gBl);
      var box = el('rect', { x: cx - 52, y: y - 11, width: 104, height: 18, rx: 2, fill: '#000', stroke: '#555', 'stroke-width': 1 }, g);
      var t = el('text', { x: cx, y: y + 3, 'text-anchor': 'middle', class: 'lbl-block' }, g);
      g.addEventListener('click', function (ev) { S.ui.clickDest(id, ev); });
      g.addEventListener('contextmenu', function (ev) { ev.preventDefault(); S.ui.ctxBlock(id, ev); });
      g.addEventListener('mousemove', function () { S.ui.hint(blockHint(id)); });
      R.bl[id] = { box: box, t: t };
    });

    R.gTrains = el('g', {}, svg);
  };

  function tri(x, y, d, s) { return [(x + d * s), y, (x - d * s), (y - s), (x - d * s), (y + s)].join(','); }
  function chev(x, y, d, s) { return [(x - d * s / 2), (y - s), (x + d * s / 2), y, (x - d * s / 2), (y + s)].join(','); }

  // ------------------------------------------------------------------ podpowiedzi kontekstowe
  function sectionHint(sec) {
    var s = S.st.sections[sec], t = 'Odcinek ' + sec + ': ';
    if (s.fault === 'reset') t += 'wymagane potwierdzenie zerowania (ZER ' + sec + ')';
    else if (s.fault === 'await') t += 'oczekiwanie na pierwszy przejazd';
    else if (s.fault === 'occ') t += 'zajęty (usterka?)';
    else if (s.trainOcc) t += 'zajęty';
    else t += 'wolny';
    if (s.lockType) t += ', utwierdzony (' + ({ train: 'przebieg pociągowy', shunt: 'przebieg manewrowy', overlap: 'droga ochronna' })[s.lockType] + ')';
    if (s.timeRel) t += ', zwalniany czasowo';
    if (s.closed) t += ', ZAMKNIĘTY dla ruchu';
    return t + '  ·  PPM: polecenia';
  }
  function switchHint(n) {
    var w = S.st.switches[n], t = 'Zwrotnica ' + n + ': położenie „' + w.pos + '”';
    if (w.moving) t += ', przestawianie do „' + w.moving.target + '”';
    if (w.lock) t += ', utwierdzona w przebiegu';
    if (w.stopped) t += ', ZASTOPOWANA';
    if (w.rozpr) t += ', ROZPRUCIE';
    if (w.noCtrl) t += ', BRAK KONTROLI';
    return t + '  ·  klik/PPM: polecenia';
  }
  function signalHint(n) {
    var s = S.st.signals[n], d = L.signals[n];
    var asp = { stop: '„Stój”', train: 'zezwalający (pociągowy)', shunt: 'zezwalający (manewrowy)', sz: 'zastępczy Sz' }[s.aspect];
    var t = d.type.charAt(0).toUpperCase() + d.type.slice(1) + ' ' + n + ': ' + asp;
    if (s.aspect === 'sz') t += ' (jeszcze ' + Math.max(0, Math.round(s.szUntil - S.st.t)) + ' s)';
    if (s.closed) t += ', zamknięty indywidualnie';
    if (s.route) t += ', początek przebiegu';
    return t + '  ·  LPM: wybór początku/końca przebiegu, PPM: polecenia';
  }
  function blockHint(id) {
    var b = L.blocks[id], bs = S.st.blocks[id];
    if (b.type === 'sbl') return 'Blokada samoczynna ' + id + ' (tor ' + b.tor + ', ' + b.nbr + '): kierunek ' + S.dirText(b, bs.dir) + (bs.changing ? ', trwa zmiana' : '') + '  ·  LPM: cel przebiegu, PPM: BK / BKA';
    return 'Blokada półsamoczynna ' + id + ' (' + b.nbr + '): pozwolenie ' + (bs.perm === 'local' ? 'na ŁW' : 'na posterunku sąsiednim') + '  ·  LPM: cel przebiegu, PPM: BPZ / BZP';
  }

  // ------------------------------------------------------------------ odświeżanie
  function setBlink(elm, cls) {
    var cur = elm.getAttribute('class') || '';
    var base = cur.replace(/\bblink-\S+/g, '').trim();
    var nw = cls ? (base + ' ' + cls).trim() : base;
    if (nw !== cur) elm.setAttribute('class', nw);
  }
  function setAttr(elm, k, v) { if (elm.getAttribute(k) !== String(v)) elm.setAttribute(k, v); }

  function secColor(sc) {
    var s = S.st.sections[sc];
    if (s.nodata) return [C.white];
    if (s.fault === 'reset') return [C.red, 'blink-rg'];
    if (s.fault === 'await') return [C.darkRed];
    if (s.trainOcc || s.fault === 'occ') return [C.red];
    if (s.timeRel) return [C.magenta];
    if (s.lockType === 'train') return [C.green];
    if (s.lockType === 'shunt' || s.lockType === 'overlap') return [C.yellow];
    return [C.grey];
  }

  R.update = function () {
    var st = S.st;
    var secCache = {};
    Object.keys(R.edges).forEach(function (eid) {
      var r = R.edges[eid], e = L.edges[eid];
      var col = secCache[e.sec] || (secCache[e.sec] = secColor(e.sec));
      var color = col[0], blink = col[1], gap = false;
      if (r.leg) {
        var w = st.switches[r.leg.num];
        var active = !w.moving && !w.noCtrl && w.pos === r.leg.leg;
        if (!active) { color = w.nodata ? C.white : C.grey; blink = null; gap = true; }
      }
      // skrócenie gałęzi nieaktywnej przy zwrotnicy
      var a = r.a, b = r.b, x1 = a.x, y1 = a.y, x2 = b.x, y2 = b.y;
      if (gap) {
        var sn = L.nodes[r.leg.node], len = Math.hypot(x2 - x1, y2 - y1), k = 9 / len;
        if (sn.id === a.id) { x1 += (x2 - x1) * k; y1 += (y2 - y1) * k; } else { x2 -= (x2 - x1) * k; y2 -= (y2 - y1) * k; }
      }
      setAttr(r.main, 'x1', x1.toFixed(1)); setAttr(r.main, 'y1', y1.toFixed(1)); setAttr(r.main, 'x2', x2.toFixed(1)); setAttr(r.main, 'y2', y2.toFixed(1));
      setAttr(r.main, 'stroke', color); setBlink(r.main, blink);
      var closed = st.sections[e.sec].closed;
      setAttr(r.main, 'stroke-width', closed ? 6 : 4);
      setAttr(r.inner, 'visibility', closed ? 'visible' : 'hidden');
      if (closed) { setAttr(r.inner, 'x1', x1); setAttr(r.inner, 'y1', y1); setAttr(r.inner, 'x2', x2); setAttr(r.inner, 'y2', y2); }
    });

    Object.keys(R.sw).forEach(function (num) {
      var r = R.sw[num], w = st.switches[num], col = null, blink = null;
      if (w.nodata) col = C.white;
      else if (w.rozpr) { col = C.red; blink = 'blink-fill'; }
      else if (w.noCtrl || (w.moving && w.frozen && st.t - w.moving.started > S.CFG.swTimeout)) { col = C.white; blink = 'blink-fill'; }
      else if (w.stopped) col = C.magenta;
      else if (w.local) col = C.cyan;
      setAttr(r.mark, 'visibility', col ? 'visible' : 'hidden');
      if (col) setAttr(r.mark, 'fill', col);
      setBlink(r.mark, blink);
      setAttr(r.lbl, 'fill', w.lock ? C.white : (w.stopped ? C.magenta : '#9a9a9a'));
      setAttr(r.sel, 'visibility', S.ui && S.ui.selSwitch === num ? 'visible' : 'hidden');
    });

    Object.keys(R.der).forEach(function (name) {
      var r = R.der[name], d = st.derailers[name], n = r.n;
      var yy = d.on ? n.y - 4 : n.y - 10;
      setAttr(r.bar, 'y1', yy); setAttr(r.bar, 'y2', yy);
      setAttr(r.bar, 'stroke', d.moving ? C.white : (d.lock ? C.yellow : (d.on ? C.grey : '#777')));
    });

    Object.keys(R.sig).forEach(function (name) {
      var r = R.sig[name], s = st.signals[name], col = C.grey, blink = null;
      if (r.type === 'odstepowy') { col = s.dark ? C.dim : (s.aspect === 'train' ? C.green : C.red); }
      else if (s.nodata) col = C.white;
      else if (s.aspect === 'sz') { col = C.white; blink = 'blink-sz'; }
      else if (s.aspect === 'train') col = C.green;
      else if (s.aspect === 'shunt') col = C.yellow;
      else if (s.lampFault && s.failAlarm) { col = C.red; blink = 'blink-sz'; }
      else if (s.closed) col = C.magenta;
      else if (s.route || s.routeEnd) col = C.red;
      if (r.shape.tagName === 'g') {
        Array.prototype.forEach.call(r.shape.childNodes, function (c) { if (c.tagName === 'polygon') setAttr(c, 'fill', col); else setAttr(c, 'stroke', col); });
      } else if (r.shape.tagName === 'polyline') setAttr(r.shape, 'stroke', col);
      else setAttr(r.shape, 'fill', col);
      setBlink(r.shape, blink);
      var pend = st.pending && st.pending.arg === name;
      setAttr(r.bg, 'visibility', pend ? 'visible' : 'hidden'); if (pend) setAttr(r.bg, 'fill', '#a00000');
      setAttr(r.sel, 'visibility', S.ui && S.ui.selStart === name ? 'visible' : 'hidden');
    });

    Object.keys(R.bl).forEach(function (id) {
      var r = R.bl[id], b = L.blocks[id], bs = st.blocks[id], txt, stroke = '#555', fill = C.grey, blink = null;
      var occ = b.secs.some(S.secOcc);
      if (b.type === 'sbl') {
        var arrow = bs.dir > 0 ? '▶' : '◀';
        txt = id + '  ' + (bs.dir > 0 ? '' : arrow + ' ') + (bs.dir === b.outward ? 'odjazd' : 'przyjazd') + (bs.dir > 0 ? ' ' + arrow : '');
        fill = occ ? C.red : (bs.dir === b.outward ? C.green : C.grey);
      } else {
        txt = id + '  Poz: ' + (bs.perm === 'local' ? 'ŁW' : b.nbr.split(' ')[1]);
        fill = occ ? C.red : (bs.perm === 'local' ? C.green : C.grey);
      }
      if (bs.changing) { blink = 'blink-txt'; fill = C.white; }
      if (bs.nbrReq) { stroke = C.orange; blink = 'blink-txt'; }
      r.t.textContent = txt; setAttr(r.t, 'fill', fill); setAttr(r.box, 'stroke', stroke); setBlink(r.t, blink);
      setAttr(r.box, 'stroke-width', S.ui && S.ui.selStart ? 2 : 1);
    });

    // numery pociągów (Ie-104 p. 23)
    var g = R.gTrains; while (g.firstChild) g.removeChild(g.firstChild);
    S.trains.list.forEach(function (tr) {
      var p = S.trains.headPos(tr); if (!p) return;
      var w = 8 * String(tr.num).length + 8, y = p.y - 24;
      var tx = Math.max(w / 2 + 2, Math.min(1798 - w / 2, p.x - p.dir * (w / 2 + 4)));
      var gg = el('g', {}, g);
      el('rect', { x: tx - w / 2, y: y - 9, width: w, height: 15, fill: '#000', stroke: tr.color, 'stroke-width': 1 }, gg);
      var t = el('text', { x: tx, y: y + 3, 'text-anchor': 'middle', class: 'lbl-train', fill: tr.color }, gg);
      t.textContent = tr.num + (tr.state === 'dwell' ? ' ⏸' : '');
      el('polygon', { points: tri(p.x, p.y, p.dir, 4), fill: tr.color }, gg);
    });
  };
})(window.SIM);
