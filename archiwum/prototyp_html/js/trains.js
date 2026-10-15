'use strict';
/*
 * Ruch pociągów, symulacja posterunków sąsiednich (blokady), scenariusze i usterki.
 */
(function (S) {
  var L = S.LAYOUT;
  var CAT = {
    'ŁKA': { len: 90, vmax: 100, color: '#7fd4ff', dwell: 40 },
    'KD': { len: 90, vmax: 100, color: '#7fd4ff', dwell: 40 },
    'PR': { len: 130, vmax: 100, color: '#9cf09c', dwell: 45 },
    'IC': { len: 260, vmax: 120, color: '#ffd27f', dwell: 60 },
    'TOW': { len: 480, vmax: 70, color: '#d0b0ff', dwell: 0 }
  };
  var ACC = 0.5, BRAKE = 0.8, V_STATION = 60, V_SZ = 20, V_SHUNT = 25;
  S.CAT = CAT;

  var T = S.trains = { list: [], queue: [], scenario: null, seq: 1 };

  function segDir(from, eid) { return S.edgeDir(eid, from); }
  function total(tr) { return tr.segs.reduce(function (a, s) { return a + s.len; }, 0); }
  function stationEdge(eid) { return eid && L.sections[L.edges[eid].sec].kind !== 'szlak'; }

  function permits(name, tr) {
    var s = S.st.signals[name];
    if (s.nodata) return false;
    if (s.aspect === 'train') return true;
    if (s.aspect === 'sz') return true;
    if (s.aspect === 'shunt') return !!tr.shunt;
    return false;
  }
  function sigsAt(nid, dir) {
    return (L.nodes[nid].sigs || []).filter(function (n) { return L.signals[n].dir === dir; });
  }

  // ------------------------------------------------------------------ spawn
  function spawn(entry) {
    var b = L.blocks[entry.from], inward = -b.outward, c = CAT[entry.cat] || CAT['ŁKA'];
    var tr = {
      id: 'T' + (T.seq++), num: entry.num, cat: entry.cat, len: c.len, vmax: c.vmax / 3.6, v: c.vmax / 3.6 * 0.8,
      segs: [{ e: null, from: null, to: b.far, len: c.len + 5, dir: inward }], head: c.len + 5,
      stop: !!entry.stop, dwell: entry.dwell != null ? entry.dwell : c.dwell, depAt: entry.dep != null ? entry.dep : null,
      term: entry.term || null, to: entry.to, from: entry.from, track: entry.track, shunt: false,
      state: 'run', szLimit: false, planned: entry.t, entered: null, waited: 0, color: c.color
    };
    T.list.push(tr);
    S.log('RUCH', 'Pociąg ' + tr.num + ' (' + tr.cat + ') wyprawiony z posterunku ' + b.nbr + ' na szlak ' + entry.from);
    return tr;
  }

  // ------------------------------------------------------------------ ruch
  function lookahead(tr) {
    var last = tr.segs[tr.segs.length - 1];
    if (last.sink) return { d: Infinity };
    var dist = total(tr) - tr.head, node = last.to, fromE = last.e, dir = last.dir, prevE = last.e;
    for (var k = 0; k < 40; k++) {
      var sg = sigsAt(node, dir);
      if (tr.stop && !tr.dwellDone && prevE && L.edges[prevE].track && (L.edges[prevE].platform || tr.term) && sg.length) return { d: dist - 15, dwell: true };
      for (var i = 0; i < sg.length; i++) {
        if (!permits(sg[i], tr)) return { d: dist - 8, sig: sg[i] };
      }
      if (tr.shunt && L.trackEnds[node]) return { d: dist - 10, end: true };
      if (L.farEnds[node] && dir === L.blocks[L.farEnds[node].szlak].outward) return { d: Infinity };
      var nx = S.nextByState(node, dir, fromE);
      if (!nx) return { d: dist - 5, end: true };
      dist += L.edges[nx.edge].len; fromE = nx.edge; prevE = nx.edge; node = S.other(nx.edge, node);
      if (dist > 2500) break;
    }
    return { d: Infinity };
  }

  function extend(tr) {
    var last = tr.segs[tr.segs.length - 1], st = S.st;
    if (last.sink) return false;
    var node = last.to, dir = last.dir;
    // przejechanie węzła – kontrola sygnałów
    sigsAt(node, dir).forEach(function (n) {
      var s = st.signals[n];
      if (L.signals[n].type === 'odstepowy') return;
      if (!permits(n, tr)) { S.alarm('Pociąg ' + tr.num + ' minął sygnalizator ' + n + ' wskazujący sygnał „Stój”!', 'spad' + tr.id + n); S.st.stats.spad = (S.st.stats.spad || 0) + 1; }
      tr.szLimit = s.aspect === 'sz';
      if (L.signals[n].type === 'wjazdowy' && !tr.entered) { tr.entered = st.t; S.st.stats.trainsIn++; }
    });
    if (L.farEnds[node] && dir === L.blocks[L.farEnds[node].szlak].outward) {
      tr.segs.push({ e: null, from: node, to: null, len: 1e6, dir: dir, sink: true, szlak: L.farEnds[node].szlak });
      return true;
    }
    var nx = S.nextByState(node, dir, last.e);
    if (!nx) return false;
    if (nx.req) {
      var w = st.switches[nx.req.num];
      var trailing = L.switches[nx.req.num].tipDir === dir;
      if (trailing && (w.pos !== nx.req.pos || w.moving)) {
        w.rozpr = true; w.damaged = true; w.pos = nx.req.pos; w.moving = null;
        S.alarm('ROZPRUCIE zwrotnicy ' + nx.req.num + ' przez pociąg ' + tr.num + ' – zamknij rozjazd dla ruchu (§22 ust. 2)');
      }
    }
    var der = Object.keys(L.derailers).filter(function (d) { return L.derailers[d].node === node; })[0];
    if (der && st.derailers[der].on) { S.alarm('WYKOLEJENIE pociągu/składu ' + tr.num + ' na wykolejnicy ' + der + '!'); tr.derailed = true; }
    tr.segs.push({ e: nx.edge, from: node, to: S.other(nx.edge, node), len: L.edges[nx.edge].len, dir: dir });
    return true;
  }

  function reverse(tr) {
    var tail = tr.head - tr.len, pos = 0, kept = [];
    tr.segs.forEach(function (s) { if (s.e) kept.push(s); else if (!kept.length) tail -= s.len; pos += s.len; });
    var tot = kept.reduce(function (a, s) { return a + s.len; }, 0);
    tr.segs = kept.reverse().map(function (s) { return { e: s.e, from: s.to, to: s.from, len: s.len, dir: -s.dir }; });
    tr.head = tot - Math.max(0, tail);
    tr.v = 0; tr.szLimit = false;
  }

  function moveTrain(tr, dt) {
    var st = S.st;
    if (tr.derailed) { tr.v = 0; return; }
    if (tr.state === 'dwell') {
      tr.v = 0;
      if (st.t >= tr.dwellUntil) {
        tr.dwellDone = true; tr.state = 'run';
        if (tr.term && tr.term.reverse) {
          reverse(tr); var old = tr.num; tr.num = tr.term.newNum; tr.to = tr.term.to; tr.stop = false; tr.depAt = st.t;
          S.log('RUCH', 'Pociąg ' + old + ' zakończył bieg – zmiana czoła, odjazd jako ' + tr.num + ' w kierunku szlaku ' + tr.to);
          tr.term = null; tr.dwellDone = true;
        } else if (tr.term && tr.term.odstaw) {
          tr.shunt = true; tr.num = tr.num + 'M';
          S.log('RUCH', 'Skład ' + tr.num + ' gotowy do odstawienia – przebieg manewrowy na tor wyciągowy / zaplecze');
        } else {
          S.log('RUCH', 'Pociąg ' + tr.num + ' gotowy do odjazdu (szlak docelowy: ' + tr.to + ')');
        }
        tr.readySince = st.t;
      }
      return;
    }
    var la = lookahead(tr);
    var vmax = Math.min(tr.vmax, (stationEdge(tr.segs[tr.segs.length - 1].e) ? V_STATION : 999) / 3.6);
    if (tr.szLimit) vmax = Math.min(vmax, V_SZ / 3.6);
    if (tr.shunt) vmax = Math.min(vmax, V_SHUNT / 3.6);
    var vt = isFinite(la.d) ? Math.sqrt(2 * BRAKE * Math.max(0, la.d)) : vmax;
    vt = Math.min(vt, vmax);
    if (la.d < 0.5) vt = 0;
    tr.v = Math.min(tr.v + ACC * dt, vt);
    if (tr.v < 0.05 && la.d < 1) {
      tr.v = 0;
      if (la.dwell) { tr.state = 'dwell'; tr.dwellUntil = Math.max(st.t + tr.dwell, tr.depAt || 0); S.log('RUCH', 'Pociąg ' + tr.num + ' – postój na torze ' + trackOf(tr)); return; }
      if (la.sig) { tr.waited += dt; st.stats.delaySum += dt; tr.waitingAt = la.sig; }
      if (la.end && tr.shunt) { tr.parkedAt = tr.parkedAt || st.t; if (st.t - tr.parkedAt > 20) { tr.gone = true; S.log('RUCH', 'Skład ' + tr.num + ' odstawiony – zjazd do zaplecza'); } }
      return;
    }
    tr.waitingAt = null;
    var mv = tr.v * dt;
    if (isFinite(la.d)) mv = Math.min(mv, Math.max(0, la.d));
    tr.head += mv;
    var guard = 0;
    while (tr.head > total(tr) && guard++ < 10) {
      if (!extend(tr)) { tr.head = total(tr); tr.v = 0; break; }
    }
    // obcięcie ogona
    var tail = tr.head - tr.len;
    while (tr.segs.length > 1 && tr.segs[0].len < tail) { tail -= tr.segs[0].len; tr.head -= tr.segs[0].len; tr.segs.shift(); }
    var last = tr.segs[tr.segs.length - 1];
    if (last.sink && tr.head - tr.len > total(tr) - last.len) { tr.gone = true; leave(tr, last.szlak); }
  }

  function trackOf(tr) {
    for (var i = tr.segs.length - 1; i >= 0; i--) { var e = tr.segs[i].e; if (e && L.edges[e].track) return L.edges[e].track; }
    return '?';
  }

  function leave(tr, szlak) {
    var st = S.st;
    st.stats.trainsOut++;
    S.log('RUCH', 'Pociąg ' + tr.num + ' opuścił obszar ŁW – szlak ' + szlak + ' (' + L.blocks[szlak].nbr + ')');
    if (tr.to && tr.to !== szlak) { st.stats.wrongDir++; S.remark('Pociąg ' + tr.num + ' wyprawiony na szlak ' + szlak + ' zamiast ' + tr.to + ' – sprawdź plan ruchu'); }
  }

  function occupancy() {
    var st = S.st;
    Object.keys(st.sections).forEach(function (k) { st.sections[k].prevTrainOcc = st.sections[k].trainOcc; st.sections[k].trainOcc = false; });
    T.list.forEach(function (tr) {
      var pos = 0, tail = tr.head - tr.len;
      tr.segs.forEach(function (s) {
        var a = pos, b = pos + s.len; pos = b;
        if (!s.e) return;
        if (Math.min(b, tr.head) - Math.max(a, tail) > 0) st.sections[L.edges[s.e].sec].trainOcc = true;
      });
    });
    Object.keys(st.sections).forEach(function (k) {
      var s = st.sections[k];
      if (s.fault === 'await') { if (s.trainOcc) s.sawTrain = true; else if (s.sawTrain) { s.fault = null; s.sawTrain = false; S.log('ZDARZENIE', 'Odcinek ' + k + ' – pierwszy przejazd zakończony, kontrola niezajętości przywrócona'); } }
    });
  }

  // ------------------------------------------------------------------ posterunki sąsiednie
  function onBlock(id) {
    var b = L.blocks[id];
    return T.list.some(function (tr) { return tr.segs.some(function (s) { return s.e && b.secs.indexOf(L.edges[s.e].sec) >= 0; }) || (tr.segs[0].e === null && tr.segs[0].to === b.far && tr.head - tr.len < tr.segs[0].len); });
  }
  T.onBlock = onBlock;

  function neighbours() {
    var st = S.st;
    Object.keys(st.blocks).forEach(function (id) {
      var b = L.blocks[id], bs = st.blocks[id];
      if (bs.changing && bs.nbrSilent && !bs.changing.emergency) {
        bs.changing.until = st.t + 1;
        if (!bs.changing.silentAt) bs.changing.silentAt = st.t;
        if (st.t - bs.changing.silentAt > 15) { bs.changing = null; S.alarm('Blokada ' + id + ': brak odpowiedzi posterunku ' + b.nbr + ' – normalna zmiana kierunku niemożliwa (§20 ust. 5)'); }
      }
      if (b.type === 'psb' && bs.req && !bs.changing) {
        var pend = T.queue.some(function (q) { return q.from === id && q.t <= st.t + 30 && !q.done; });
        if (st.t - bs.req.at > S.CFG.nbrDelay) {
          if (!S.secOcc(b.secs[0]) && !pend && !onBlock(id)) { bs.changing = { to: 'local', until: st.t + 1 }; S.log('RUCH', b.nbr + ': oddano pozwolenie na szlak ' + id); }
          else if (!bs.req.refused) { bs.req.refused = true; S.log('RUCH', b.nbr + ': odmowa oddania pozwolenia – pociąg w drodze / szlak zajęty'); bs.req = null; }
        }
      }
    });
    // wyprawianie pociągów z posterunków sąsiednich
    T.queue.forEach(function (q) {
      if (q.done || q.t > st.t) return;
      var b = L.blocks[q.from], bs = st.blocks[q.from], inward = -b.outward;
      var ok;
      if (b.type === 'sbl') {
        ok = bs.dir === inward && !bs.changing && !S.secOcc(b.secs[1]) && !onBlockFar(q.from);
        if (bs.dir !== inward && !bs.nbrReq && !bs.changing) { bs.nbrReq = true; S.alarm(b.nbr + ': żądanie zmiany kierunku blokady ' + q.from + ' dla pociągu ' + q.num + ' (polecenie BK ' + q.from + ')', 'req' + q.from); }
      } else {
        ok = bs.perm === 'remote' && !bs.changing && !S.secOcc(b.secs[0]) && !onBlock(q.from);
        if (bs.perm === 'local' && !bs.nbrReq) { bs.nbrReq = true; S.alarm(b.nbr + ': prośba o pozwolenie na szlak ' + q.from + ' dla pociągu ' + q.num + ' (polecenie BPZ ' + q.from + ')', 'req' + q.from); }
      }
      if (ok) { q.done = true; bs.nbrReq = false; spawn(q); }
    });
  }
  function onBlockFar(id) {
    var b = L.blocks[id];
    return T.list.some(function (tr) { return tr.segs[0].e === null && tr.segs[0].to === b.far && tr.head - tr.len < tr.segs[0].len + 50; });
  }

  // ------------------------------------------------------------------ usterki (panel instruktora)
  var FAULTS = {
    secOcc: { name: 'Zajętość odcinka bez pojazdu (usterka obwodu / licznika osi)', target: 'sec', apply: function (id) { S.st.sections[id].fault = 'occ'; }, clear: function (id) { S.st.sections[id].fault = 'reset'; S.alarm('Odcinek ' + id + ': usterka ustąpiła – wymagane potwierdzenie zerowania (ZER ' + id + ')'); } },
    swNoCtrl: { name: 'Zwrotnica – niespodziewany brak kontroli położenia', target: 'sw', apply: function (n) { S.st.switches[n].noCtrl = true; S.alarm('Zwrotnica ' + n + ': niespodziewany brak kontroli położenia'); }, clear: function (n) { S.st.switches[n].noCtrl = false; } },
    swFrozen: { name: 'Zwrotnica – nie przestawia się (zamarznięcie / przeszkoda)', target: 'sw', apply: function (n) { S.st.switches[n].frozen = true; }, clear: function (n) { S.st.switches[n].frozen = false; } },
    swRozpr: { name: 'Zwrotnica – sygnalizacja rozprucia (utrata kontroli)', target: 'sw', apply: function (n) { S.st.switches[n].rozpr = true; S.alarm('Zwrotnica ' + n + ': sygnalizacja rozprucia'); }, clear: function (n) { S.st.switches[n].rozpr = false; S.st.switches[n].damaged = false; S.st.switches[n].noCtrl = false; } },
    sigLamp: { name: 'Semafor – nie wyświetla sygnału zezwalającego', target: 'sig', apply: function (n) { S.st.signals[n].lampFault = true; }, clear: function (n) { S.st.signals[n].lampFault = false; S.st.signals[n].failAlarm = false; } },
    sigNodata: { name: 'Semafor – brak komunikacji z obiektem', target: 'sig', apply: function (n) { S.st.signals[n].nodata = true; S.alarm('Sygnalizator ' + n + ': brak komunikacji z obiektem'); }, clear: function (n) { S.st.signals[n].nodata = false; } },
    mains: { name: 'Zanik zasilania sieciowego (agregat startuje samoczynnie)', target: null, apply: function () { var p = S.st.power; p.mains = false; p.outageAt = S.st.t; S.alarm('Zanik zasilania sieciowego – praca z UPS (§23)'); }, clear: function () { var p = S.st.power; p.mains = true; p.gen = false; p.outageAt = null; S.log('ZDARZENIE', 'Przywrócono zasilanie sieciowe, agregat wyłączony'); } },
    mainsGenFail: { name: 'Zanik zasilania + agregat nie startuje samoczynnie', target: null, apply: function () { var p = S.st.power; p.mains = false; p.genFail = true; p.outageAt = S.st.t; S.alarm('Zanik zasilania sieciowego – agregat nie uruchomił się (polecenie AGR / wezwij automatyka, §23 ust. 2)'); }, clear: function () { FAULTS.mains.clear(); S.st.power.genFail = false; } },
    nbrSilent: { name: 'Blokada – brak odpowiedzi posterunku sąsiedniego', target: 'block', apply: function (id) { S.st.blocks[id].nbrSilent = true; }, clear: function (id) { S.st.blocks[id].nbrSilent = false; } }
  };
  S.FAULTS = FAULTS;
  S.applyFault = function (type, target, clear) {
    var f = FAULTS[type]; if (!f) return;
    (clear ? f.clear : f.apply)(target);
    S.log('INSTRUKTOR', (clear ? 'Usunięto usterkę: ' : 'Wprowadzono usterkę: ') + f.name + (target ? ' [' + target + ']' : ''));
  };

  // ------------------------------------------------------------------ scenariusze
  var SC = S.SCENARIOS = {
    podstawowy: {
      name: 'Ruch podstawowy (bez usterek)',
      desc: 'Rozkład ok. 25 minut: ŁKA, IC, towarowe; zmiana czoła i odstawienie składu.',
      trains: [
        { t: 5, num: '19111', cat: 'ŁKA', from: 'WF1', to: 'WA1', stop: true, track: '1', dep: 150 },
        { t: 30, num: '18100', cat: 'IC', from: 'WA2', to: 'WF2', stop: true, track: '2', dep: 200 },
        { t: 90, num: '64410', cat: 'TOW', from: 'WD', to: 'WO', stop: false, track: '7' },
        { t: 200, num: '91120', cat: 'ŁKA', from: 'WA2', to: 'WA1', stop: true, track: '3/4', dep: 420, term: { reverse: true, newNum: '19121', to: 'WA1' } },
        { t: 260, num: '91017', cat: 'ŁKA', from: 'WA2', to: null, stop: true, track: '8', term: { odstaw: true } },
        { t: 330, num: '46021', cat: 'TOW', from: 'WO', to: 'WD', stop: false, track: '7' },
        { t: 420, num: '81102', cat: 'IC', from: 'WF1', to: 'WA1', stop: true, track: '1/3', dep: 600 },
        { t: 520, num: '19213', cat: 'PR', from: 'WF1', to: 'WA1', stop: true, track: '1/3', dep: 700 },
        { t: 600, num: '19210', cat: 'PR', from: 'WA2', to: 'WF2', stop: true, track: '2/4', dep: 800 }
      ],
      faults: []
    },
    usterki: {
      name: 'Przeszkody w działaniu urządzeń (Ie-20 rozdz. 5)',
      desc: 'Jak „podstawowy”, plus: semafor S1 nie otwiera się (→ sygnał zastępczy), zakłócenie licznika osi z13, zwrotnica 23 bez kontroli, zanik zasilania, brak odpowiedzi Andrzejowa.',
      trains: null,
      faults: [
        { t: 1, type: 'sigLamp', target: 'S1' },
        { t: 140, type: 'secOcc', target: 'z13' },
        { t: 240, type: 'secOcc', target: 'z13', clear: true },
        { t: 300, type: 'swNoCtrl', target: '23' },
        { t: 380, type: 'mains' },
        { t: 470, type: 'mains', clear: true },
        { t: 500, type: 'nbrSilent', target: 'WA2' }
      ]
    },
    zima: {
      name: 'Zima – mróz i opady śniegu (§4 ust. 5)',
      desc: 'Zwrotnice 5 i 25 zamarzają po 120 s; ćwicz profilaktyczne przestawianie i postępowanie wg §24 ust. 6.',
      trains: null,
      faults: [{ t: 120, type: 'swFrozen', target: '5' }, { t: 120, type: 'swFrozen', target: '25' }, { t: 400, type: 'swFrozen', target: '5', clear: true }]
    }
  };

  T.load = function (key) {
    var sc = SC[key] || SC.podstawowy;
    T.scenario = key; T.list = []; T.seq = 1;
    T.queue = JSON.parse(JSON.stringify(sc.trains || SC.podstawowy.trains)).map(function (q) { q.done = false; return q; });
    T.faultQueue = JSON.parse(JSON.stringify(sc.faults || []));
  };

  T.tick = function (dt) {
    neighbours();
    (T.faultQueue || []).forEach(function (f) { if (!f.done && f.t <= S.st.t) { f.done = true; S.applyFault(f.type, f.target, f.clear); } });
    T.list.forEach(function (tr) { moveTrain(tr, dt); });
    T.list = T.list.filter(function (tr) { return !tr.gone; });
    occupancy();
  };

  T.spawnManual = function (from, cat, num, to, stop) {
    spawn({ t: S.st.t, num: num || String(90000 + T.seq), cat: cat || 'ŁKA', from: from, to: to || null, stop: !!stop });
  };
  T.enqueueManual = function (from, cat, num, to, stop) {
    T.queue.push({ t: S.st.t, num: num || String(90000 + T.seq++), cat: cat || 'ŁKA', from: from, to: to || null, stop: !!stop, done: false });
  };

  /* Pozycja czoła pociągu do rysowania. */
  T.headPos = function (tr) {
    var pos = 0;
    for (var i = 0; i < tr.segs.length; i++) {
      var s = tr.segs[i];
      if (tr.head <= pos + s.len || i === tr.segs.length - 1) {
        if (!s.e) { var n = L.nodes[s.to || s.from]; return n ? { x: n.x, y: n.y, dir: s.dir } : null; }
        var f = Math.max(0, Math.min(1, (tr.head - pos) / s.len)), a = L.nodes[s.from], b = L.nodes[s.to];
        return { x: a.x + (b.x - a.x) * f, y: a.y + (b.y - a.y) * f, dir: s.dir };
      }
      pos += s.len;
    }
    return null;
  };
})(window.SIM);
