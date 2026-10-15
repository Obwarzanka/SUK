'use strict';
/*
 * Warstwa zależnościowa (uproszczona) + interpretacja poleceń.
 * Odwzorowuje zasady Ie-20: polecenia zwykłe / specjalne (dwuetapowe) / techniczne (§13),
 * przestawianie zwrotnic (§15), przebiegi (§16–18), blokady (§19–20), przeszkody (§21–27).
 */
(function (S) {
  var L = S.LAYOUT;
  var CFG = S.CFG = {
    swTime: 5,           // s – czas przestawiania zwrotnicy
    swTimeout: 12,       // s – alarm „przestawianie trwa zbyt długo”
    szTime: 90,          // s – sygnał zastępczy (§17 ust. 7 pkt 4)
    specialConfirm: 30,  // s – czas na potwierdzenie polecenia specjalnego
    timeRelease: 60,     // s – czasowe zwolnienie przebiegu
    blockChange: 4,      // s – zmiana kierunku blokady
    nbrDelay: 6          // s – reakcja dyżurnego posterunku sąsiedniego
  };

  // ------------------------------------------------------------------ topologia
  function other(eid, nid) { var e = L.edges[eid]; return e.a === nid ? e.b : e.a; }
  function edgeDir(eid, from) { return Math.sign(L.nodes[other(eid, from)].x - L.nodes[from].x); }
  S.other = other; S.edgeDir = edgeDir;

  /* Krawędzie wychodzące z węzła w kierunku dir, przy wejściu krawędzią fromEdge.
     Zwraca listę {edge, req:{num,pos}|null}. */
  function forward(nid, dir, fromEdge) {
    var n = L.nodes[nid], out = [];
    if (n.sw) {
      var sw = L.switches[n.sw];
      if (sw.tipDir === dir) {           // jazda od strony iglic do ostrza (zjazd „na ostrze”)
        if (fromEdge === sw.plus) out.push({ edge: sw.tip, req: { num: sw.num, pos: '+' } });
        else if (fromEdge === sw.minus) out.push({ edge: sw.tip, req: { num: sw.num, pos: '-' } });
        else if (fromEdge == null) out.push({ edge: sw.tip, req: null });
      } else {                           // jazda „z ostrza” – wybór gałęzi
        out.push({ edge: sw.plus, req: { num: sw.num, pos: '+' } });
        out.push({ edge: sw.minus, req: { num: sw.num, pos: '-' } });
      }
      return out;
    }
    n.edges.forEach(function (eid) { if (eid !== fromEdge && edgeDir(eid, nid) === dir) out.push({ edge: eid, req: null }); });
    return out;
  }
  S.forward = forward;

  /* Następna krawędź przy bieżącym położeniu zwrotnic (dla pociągu). */
  function nextByState(nid, dir, fromEdge) {
    var opts = forward(nid, dir, fromEdge);
    if (!opts.length) return null;
    var n = L.nodes[nid];
    if (n.sw && opts.length === 2) {
      var pos = S.st.switches[n.sw].pos;
      return opts[pos === '+' ? 0 : 1];
    }
    return opts[0];
  }
  S.nextByState = nextByState;

  // ------------------------------------------------------------------ tablica przebiegów
  function trainSig(s) { return s && (s.type === 'wjazdowy' || s.type === 'wyjazdowy'); }
  function shuntSig(s) { return s && (s.type === 'wyjazdowy' || s.type === 'tm'); }

  function searchFrom(startName) {
    var sig = L.signals[startName], res = [];
    function dfs(nid, fromEdge, edges, reqs, depth) {
      if (depth > 40) return;
      var opts = forward(nid, sig.dir, fromEdge);
      opts.forEach(function (o) {
        var e2 = edges.concat([o.edge]), r2 = o.req ? reqs.concat([o.req]) : reqs;
        var nn = other(o.edge, nid), node = L.nodes[nn];
        var endSig = (node.sigs || []).map(function (n) { return L.signals[n]; }).filter(function (s) { return s.dir === sig.dir && s.type !== 'odstepowy'; })[0];
        if (L.boundaries[nn]) { res.push({ end: L.boundaries[nn].szlak, endKind: 'szlak', edges: e2, reqs: r2, endNode: nn }); return; }
        if (endSig) { res.push({ end: endSig.name, endKind: 'sig', edges: e2, reqs: r2, endNode: nn }); return; }
        if (L.trackEnds[nn]) { res.push({ end: L.trackEnds[nn].id, endKind: 'kz', edges: e2, reqs: r2, endNode: nn }); return; }
        dfs(nn, o.edge, e2, r2, depth + 1);
      });
    }
    dfs(sig.node, null, [], [], 0);
    return res;
  }

  function buildRouteTable() {
    var table = {};
    Object.keys(L.signals).forEach(function (name) {
      var s = L.signals[name];
      if (s.type === 'odstepowy') return;
      var cands = searchFrom(name), best = {};
      cands.forEach(function (c) {
        var endS = L.signals[c.end];
        var types = [];
        if (trainSig(s) && (c.endKind === 'szlak' ? s.type === 'wyjazdowy' : (c.endKind === 'sig' && endS.type === 'wyjazdowy' && s.type === 'wjazdowy'))) types.push('P');
        if (shuntSig(s) && c.endKind !== 'szlak' && !(c.endKind === 'sig' && endS.type === 'wjazdowy')) types.push('M');
        types.forEach(function (t) {
          var key = t + ':' + c.end;
          var minus = c.reqs.filter(function (r) { return r.pos === '-'; }).length;
          if (!best[key] || minus < best[key].minus) best[key] = Object.assign({}, c, { type: t, minus: minus });
        });
      });
      Object.keys(best).forEach(function (k) {
        var r = best[k];
        var secs = [];
        r.edges.forEach(function (eid) { var sc = L.edges[eid].sec; if (secs[secs.length - 1] !== sc) secs.push(sc); });
        var ders = Object.keys(L.derailers).filter(function (d) { return secs.indexOf(L.derailers[d].sec) >= 0; });
        var ov = null;
        if (r.type === 'P' && r.endKind === 'sig') {
          var nx = nextByStaticAny(r.endNode, s.dir);
          if (nx) ov = L.edges[nx].sec;
        }
        var id = (r.type === 'P' ? 'P ' : 'M ') + name + '→' + r.end;
        (table[name] = table[name] || []).push({ id: id, start: name, end: r.end, endKind: r.endKind, type: r.type, edges: r.edges, secs: secs, sw: r.reqs, ders: ders, overlap: ov, endNode: r.endNode });
      });
    });
    return table;
  }
  function nextByStaticAny(nid, dir) { var o = forward(nid, dir, null); return o.length ? o[0].edge : null; }

  // ------------------------------------------------------------------ stan
  function initState() {
    var st = {
      t: 0, startClock: 6 * 3600,
      sections: {}, switches: {}, signals: {}, derailers: {}, blocks: {}, routes: {},
      power: { mains: true, ups: true, gen: false, genAuto: true, genFail: false, nn: true, outageAt: null },
      pending: null, selected: null, alarms: [], log: [], book: [], remarks: [], user: null, postStatus: 'obsluga',
      stats: { cmds: 0, special: 0, rejected: 0, trainsIn: 0, trainsOut: 0, wrongDir: 0, delaySum: 0 },
      seq: 1
    };
    Object.keys(L.sections).forEach(function (id) { st.sections[id] = { id: id, occ: false, trainOcc: false, fault: null, lock: null, lockType: null, timeRel: null, closed: false, wasOcc: false, nodata: false }; });
    Object.keys(L.switches).forEach(function (n) { st.switches[n] = { num: n, pos: '+', moving: null, stopped: false, lock: null, rozpr: false, noCtrl: false, frozen: false, damaged: false, nodata: false, local: false, ovLock: null }; });
    Object.keys(L.signals).forEach(function (n) { st.signals[n] = { name: n, aspect: 'stop', route: null, closed: false, lampFault: false, szUntil: 0, nodata: false, failAlarm: false }; });
    Object.keys(L.derailers).forEach(function (n) { st.derailers[n] = { name: n, on: true, moving: null, lock: null }; });
    Object.keys(L.blocks).forEach(function (id) {
      var b = L.blocks[id];
      st.blocks[id] = b.type === 'sbl' ? { id: id, dir: b.defDir, req: null, changing: null, nbrReq: false } : { id: id, perm: b.defPerm, req: null, changing: null, nbrReq: false };
    });
    return st;
  }

  // ------------------------------------------------------------------ dziennik / alarmy
  function clock(t) {
    var s = Math.floor(S.st.startClock + (t == null ? S.st.t : t));
    var h = Math.floor(s / 3600) % 24, m = Math.floor(s / 60) % 60, sec = s % 60;
    return (h < 10 ? '0' : '') + h + ':' + (m < 10 ? '0' : '') + m + ':' + (sec < 10 ? '0' : '') + sec;
  }
  S.clock = clock;
  function log(kind, text) {
    S.st.log.push({ t: S.st.t, time: clock(), kind: kind, text: text, user: S.st.user ? S.st.user.name : '-' });
    if (S.ui) S.ui.onLog();
  }
  function alarm(text, key) {
    key = key || text;
    if (S.st.alarms.some(function (a) { return a.key === key && !a.ack; })) return;
    S.st.alarms.push({ key: key, text: text, time: clock(), ack: false });
    log('ALARM', text);
    if (S.ui) S.ui.beep();
  }
  function remark(text) { S.st.remarks.push({ time: clock(), text: text }); log('UWAGA', text); }
  S.log = log; S.alarm = alarm; S.remark = remark;

  // ------------------------------------------------------------------ predykaty stanu
  function secOcc(id) { var s = S.st.sections[id]; return s.trainOcc || s.fault === 'occ' || s.fault === 'reset' || s.fault === 'await'; }
  function swControlled(n) { var w = S.st.switches[n]; return !w.moving && !w.noCtrl && !w.rozpr && !w.nodata && !w.local; }
  S.secOcc = secOcc; S.swControlled = swControlled;

  function blockAllowsExit(szlak) {
    var b = L.blocks[szlak], bs = S.st.blocks[szlak];
    if (b.type === 'sbl') return bs.dir === b.outward && !secOcc(b.secs[0]) && !bs.changing;
    return bs.perm === 'local' && !secOcc(b.secs[0]) && !bs.changing;
  }
  S.blockAllowsExit = blockAllowsExit;

  // ------------------------------------------------------------------ przebiegi
  function findRoute(start, end, type) {
    var list = S.routeTable[start] || [];
    return list.filter(function (r) { return r.end === end && (!type || r.type === type); })[0] || null;
  }
  S.findRoute = findRoute;

  function routeObstacles(rt, forSetting) {
    var st = S.st, why = [];
    var sig = st.signals[rt.start];
    if (sig.closed) why.push('sygnalizator ' + rt.start + ' zamknięty indywidualnie');
    if (forSetting && sig.route) why.push('sygnalizator ' + rt.start + ' jest już początkiem przebiegu');
    rt.secs.forEach(function (sc, i) {
      var s = st.sections[sc];
      var last = i === rt.secs.length - 1;
      if (s.closed) why.push('odcinek ' + sc + ' zamknięty');
      if (s.lock && s.lock !== rt.uid) why.push('odcinek ' + sc + ' utwierdzony w innym przebiegu');
      if (secOcc(sc) && !(rt.type === 'M' && last)) why.push('odcinek ' + sc + ' zajęty');
    });
    rt.sw.forEach(function (r) {
      var w = st.switches[r.num];
      if (w.nodata) why.push('zwr. ' + r.num + ' – brak danych');
      if (w.rozpr) why.push('zwr. ' + r.num + ' – rozprucie');
      if (w.local) why.push('zwr. ' + r.num + ' – przekazana do nastawiania lokalnego');
      var curTarget = w.moving ? w.moving.target : w.pos;
      if (curTarget !== r.pos) {
        if (w.lock && w.lock !== rt.uid) why.push('zwr. ' + r.num + ' utwierdzona w innym przebiegu');
        if (w.ovLock && w.ovLock !== rt.uid) why.push('zwr. ' + r.num + ' w drodze ochronnej');
        if (w.stopped) why.push('zwr. ' + r.num + ' zastopowana');
        if (!st.power.nn) why.push('wyłączone napięcie nastawcze');
        if (secOcc(L.switches[r.num].sec) && forSetting) why.push('zwr. ' + r.num + ' – odcinek zajęty');
      }
    });
    if (rt.overlap) {
      var o = st.sections[rt.overlap];
      if (secOcc(rt.overlap)) why.push('droga ochronna ' + rt.overlap + ' zajęta');
      if (o.lock && o.lock !== rt.uid && o.lockType !== 'overlap') why.push('droga ochronna ' + rt.overlap + ' utwierdzona w innym przebiegu');
    }
    if (rt.endKind === 'szlak' && !blockAllowsExit(rt.end)) why.push('blokada liniowa ' + rt.end + ' nie zezwala na wyprawienie');
    return why;
  }

  function setRoute(rt) {
    var st = S.st, uid = 'R' + (st.seq++);
    var r = Object.assign({}, rt, { uid: uid, state: 'setting', rel: 0, wasOcc: {}, passed: false, t0: st.t });
    var why = routeObstacles(r, true);
    if (why.length) return { ok: false, why: why };
    r.secs.forEach(function (sc) { var s = st.sections[sc]; s.lock = uid; s.lockType = r.type === 'P' ? 'train' : 'shunt'; s.wasOcc = false; });
    if (r.overlap) { var o = st.sections[r.overlap]; if (!o.lock) { o.lock = uid; o.lockType = 'overlap'; } }
    r.sw.forEach(function (q) { var w = st.switches[q.num]; w.lock = uid; if (w.pos !== q.pos || (w.moving && w.moving.target !== q.pos)) moveSwitch(q.num, q.pos, true); });
    r.ders.forEach(function (d) { st.derailers[d].lock = uid; st.derailers[d].on = false; });
    st.routes[uid] = r;
    st.signals[r.start].route = uid;
    if (r.endKind === 'sig') st.signals[r.end].routeEnd = uid;
    return { ok: true, route: r };
  }

  function signalConditions(r) {
    var st = S.st, why = routeObstacles(r, false);
    r.sw.forEach(function (q) { var w = st.switches[q.num]; if (w.pos !== q.pos || !swControlled(q.num)) why.push('zwr. ' + q.num + ' bez kontroli w położeniu ' + q.pos); });
    r.ders.forEach(function (d) { if (st.derailers[d].on || st.derailers[d].moving) why.push('wykolejnica ' + d + ' założona'); });
    return why;
  }

  function releaseRoute(uid, reason) {
    var st = S.st, r = st.routes[uid]; if (!r) return;
    r.secs.forEach(function (sc) { var s = st.sections[sc]; if (s.lock === uid) { s.lock = null; s.lockType = null; s.timeRel = null; } });
    if (r.overlap) { var o = st.sections[r.overlap]; if (o.lock === uid) { o.lock = null; o.lockType = null; } }
    r.sw.forEach(function (q) { if (st.switches[q.num].lock === uid) st.switches[q.num].lock = null; });
    r.ders.forEach(function (d) { if (st.derailers[d].lock === uid) { st.derailers[d].lock = null; st.derailers[d].on = true; } });
    var sg = st.signals[r.start]; if (sg.route === uid) { sg.route = null; if (sg.aspect !== 'sz') sg.aspect = 'stop'; }
    if (r.endKind === 'sig' && st.signals[r.end].routeEnd === uid) st.signals[r.end].routeEnd = null;
    delete st.routes[uid];
    if (reason) log('ZDARZENIE', 'Zwolniono przebieg ' + r.id + ' (' + reason + ')');
  }
  S.releaseRoute = releaseRoute;

  function updateRoutes() {
    var st = S.st;
    Object.keys(st.routes).forEach(function (uid) {
      var r = st.routes[uid], sg = st.signals[r.start];
      if (r.timeRelUntil) {
        if (st.t >= r.timeRelUntil) releaseRoute(uid, 'zwolnienie czasowe');
        return;
      }
      // najechanie pierwszej sekcji za semaforem – samoczynne „Stój” (§17 ust. 2 pkt 3)
      if (!r.passed && st.sections[r.secs[0]].trainOcc && !(r.type === 'M' && r.secs.length === 1)) {
        if (sg.aspect === 'train' || sg.aspect === 'shunt') sg.aspect = 'stop';
        r.passed = true; r.state = 'passed';
      }
      // otwarcie semafora
      if (!r.passed) {
        var why = signalConditions(r);
        if (r.state === 'setting' && !why.length) {
          if (sg.lampFault && r.type === 'P') {
            if (!sg.failAlarm) { alarm('Semafor ' + r.start + ': nie wyświetlił sygnału zezwalającego (usterka obwodów sygnalizatora) – §26 ust. 1'); sg.failAlarm = true; }
          } else if (sg.nodata) {
            /* brak danych – semafor nie zostanie otwarty */
          } else {
            r.state = 'set'; sg.aspect = r.type === 'P' ? 'train' : 'shunt';
            log('ZDARZENIE', 'Semafor ' + r.start + ' – sygnał zezwalający (' + r.id + ')');
          }
        } else if (r.state === 'set' && why.length && sg.aspect !== 'stop') {
          sg.aspect = 'stop'; r.state = 'setting';
          alarm('Semafor ' + r.start + ' zamknięty samoczynnie: ' + why[0]);
        } else if (r.state === 'setting' && st.t - r.t0 > 20 && !r.slowAlarm) {
          r.slowAlarm = true; alarm('Przebieg ' + r.id + ' nie może zostać nastawiony: ' + (why[0] || 'brak kontroli'));
        }
      }
      // zwalnianie sekcyjne (§18 ust. 3)
      if (r.passed) {
        while (r.rel < r.secs.length) {
          var sc = r.secs[r.rel], s = st.sections[sc], last = r.rel === r.secs.length - 1;
          if (st.sections[sc].trainOcc) r.wasOcc[sc] = true;
          if (r.wasOcc[sc] && (!st.sections[sc].trainOcc || last)) {
            if (s.lock === uid) { s.lock = null; s.lockType = null; }
            r.sw.forEach(function (q) { if (L.switches[q.num].sec === sc && st.switches[q.num].lock === uid) st.switches[q.num].lock = null; });
            r.ders.forEach(function (d) { if (L.derailers[d].sec === sc && st.derailers[d].lock === uid) { st.derailers[d].lock = null; st.derailers[d].on = true; } });
            r.rel++;
          } else break;
        }
        // sekcje dalsze też mogą zostać zajęte zanim poprzednia się zwolni
        for (var i = r.rel; i < r.secs.length; i++) if (st.sections[r.secs[i]].trainOcc) r.wasOcc[r.secs[i]] = true;
        if (r.rel >= r.secs.length) {
          if (!r.doneAt) r.doneAt = st.t;
          if (st.t - r.doneAt > 8) releaseRoute(uid, null);
        }
      }
    });
  }

  // ------------------------------------------------------------------ zwrotnice
  function moveSwitch(num, pos, byRoute) {
    var w = S.st.switches[num];
    w.moving = { target: pos, from: w.pos, until: S.st.t + CFG.swTime, started: S.st.t };
    log('ZDARZENIE', 'Zwrotnica ' + num + ' – przestawianie do „' + pos + '”' + (byRoute ? ' (przebiegowo)' : ''));
  }
  function updateSwitches() {
    var st = S.st;
    Object.keys(st.switches).forEach(function (n) {
      var w = st.switches[n];
      if (!w.moving) return;
      if (!st.power.nn && !w.frozen) { return; } // przestawianie wstrzymane (brak napięcia nastawczego)
      if (w.frozen) {
        if (st.t - w.moving.started > CFG.swTimeout && !w.moving.alarmed) { w.moving.alarmed = true; alarm('Zwrotnica ' + n + ': przestawianie trwa zbyt długo – wydaj polecenie przestawienia do położenia pierwotnego (§24 ust. 6)'); }
        if (w.moving.target === w.moving.from && st.t >= w.moving.until) { w.pos = w.moving.target; w.moving = null; }
        return;
      }
      if (st.t >= w.moving.until) { w.pos = w.moving.target; w.moving = null; log('ZDARZENIE', 'Zwrotnica ' + n + ' – kontrola w położeniu „' + w.pos + '”'); }
    });
    Object.keys(st.derailers).forEach(function (n) { var d = st.derailers[n]; if (d.moving && st.t >= d.moving.until) { d.on = d.moving.target; d.moving = null; } });
  }

  // ------------------------------------------------------------------ sygnały, odstępy
  function updateSignals() {
    var st = S.st;
    Object.keys(st.signals).forEach(function (n) {
      var s = st.signals[n], d = L.signals[n];
      if (s.aspect === 'sz' && st.t >= s.szUntil) { s.aspect = 'stop'; log('ZDARZENIE', 'Sygnał zastępczy na ' + n + ' wygasł samoczynnie'); }
      if (d.type === 'odstepowy') {
        var b = L.blocks[d.szlak], bs = st.blocks[d.szlak];
        var ahead = d.dir === b.outward ? b.secs[1] : b.secs[0];
        s.aspect = (bs.dir === d.dir && !secOcc(ahead) && !bs.changing) ? 'train' : 'stop';
        s.dark = bs.dir !== d.dir;
      }
    });
  }

  // ------------------------------------------------------------------ zasilanie
  function updatePower() {
    var p = S.st.power;
    if (!p.mains && !p.gen && p.genAuto && !p.genFail && p.outageAt != null && S.st.t - p.outageAt > 15) {
      p.gen = true; log('ZDARZENIE', 'Agregat prądotwórczy uruchomiony samoczynnie');
    }
  }

  // ------------------------------------------------------------------ blokady liniowe
  function updateBlocks() {
    var st = S.st;
    Object.keys(st.blocks).forEach(function (id) {
      var b = L.blocks[id], bs = st.blocks[id];
      if (bs.changing && st.t >= bs.changing.until) {
        if (b.type === 'sbl') { bs.dir = bs.changing.to; log('ZDARZENIE', 'Blokada ' + id + ': ustawiony kierunek ' + dirText(b, bs.dir)); }
        else { bs.perm = bs.changing.to; log('ZDARZENIE', 'Blokada ' + id + ': pozwolenie ' + (bs.perm === 'local' ? 'na posterunku ŁW' : 'na posterunku ' + b.nbr)); }
        bs.changing = null; bs.req = null; bs.nbrReq = false;
      }
    });
  }
  function dirText(b, dir) { return dir === b.outward ? 'ŁW → ' + b.nbr : b.nbr + ' → ŁW'; }
  S.dirText = dirText;

  function blockChangeConds(id, emergency) {
    var b = L.blocks[id], st = S.st, why = [];
    if (emergency) return why;
    b.secs.forEach(function (sc) { if (secOcc(sc)) why.push('odstęp ' + sc + ' zajęty'); });
    if (st.signals[b.entrySig].aspect !== 'stop') why.push('semafor wjazdowy ' + b.entrySig + ' nie wskazuje „Stój”');
    Object.keys(st.routes).forEach(function (u) { if (st.routes[u].end === id) why.push('nastawiony przebieg wyjazdowy na szlak ' + id); });
    if (S.trains && S.trains.onBlock(id)) why.push('pociąg na szlaku ' + id);
    return why;
  }
  S.blockChangeConds = blockChangeConds;

  // ------------------------------------------------------------------ polecenia
  var SPECIAL = {
    SZ: 'wyświetlenie sygnału zastępczego', DZ: 'doraźne (czasowe) zwolnienie przebiegu', ZB: 'przestawienie zwrotnicy z bocznikowaniem kontroli niezajętości',
    RK: 'skasowanie sygnalizacji rozprucia zwrotnicy', ZER: 'zerowanie licznika osi', NN1: 'załączenie napięcia nastawczego', BKA: 'awaryjna zmiana kierunku blokady', OSL: 'odwołanie zamknięcia sygnalizatora/odcinka (specjalne)'
  };
  var HELP = [
    ['PP <pocz> <koniec>', 'przebieg pociągowy, np. PP A S1, PP S1 WA1'],
    ['PM <pocz> <koniec>', 'przebieg manewrowy, np. PM M8 KZW, PM Tm14 R8'],
    ['ST <sygn>', 'sygnał „Stój” (§17 ust. 2 pkt 4)'],
    ['STW', 'sygnał „Stój” na wszystkich semaforach stacji'],
    ['ZP <sygn>', 'zwolnienie (odwołanie) przebiegu przed wjazdem'],
    ['Z+ <nr> / Z- <nr>', 'przestawienie zwrotnicy (§15)'],
    ['ZS <nr> / ZO <nr>', 'zastopowanie / odstopowanie zwrotnicy (§15 ust. 6)'],
    ['WZ <wk> / WD <wk>', 'założenie / zdjęcie wykolejnicy'],
    ['ZT <odc> / OT <odc>', 'zamknięcie / otwarcie toru (odcinka) dla ruchu'],
    ['SZM <sygn> / SOM <sygn>', 'zamknięcie / otwarcie sygnalizatora'],
    ['OZ <sygn>', 'wyłączenie sygnału zastępczego'],
    ['BK <szlak>', 'zmiana kierunku blokady (żądanie lub zgoda), np. BK WA1'],
    ['BPZ <szlak> / BZP <szlak>', 'oddanie / żądanie pozwolenia (blokada półsamoczynna WD, WO)'],
    ['NN0', 'wyłączenie napięcia nastawczego (techniczne)'],
    ['AGR', 'ręczne uruchomienie agregatu prądotwórczego'],
    ['KW', 'pokwitowanie alarmów'],
    ['— specjalne (dwuetapowe) —', ''],
    ['SZ <sygn>', 'sygnał zastępczy (§17 ust. 7, §27)'],
    ['DZ <sygn>', 'doraźne zwolnienie przebiegu – czasowe (§18 ust. 4, §25 ust. 2)'],
    ['ZB <nr> +|-', 'przestawienie zwrotnicy z bocznikowaniem kontroli niezajętości (§15 ust. 3, §24 ust. 9)'],
    ['RK <nr>', 'skasowanie rozprucia (§22 ust. 3 pkt 2)'],
    ['ZER <odc>', 'zerowanie licznika osi (§20 ust. 7 pkt 6)'],
    ['NN1', 'załączenie napięcia nastawczego (§13 ust. 10)'],
    ['BKA <szlak>', 'awaryjna zmiana kierunku blokady (§20 ust. 5)']
  ];
  S.HELP = HELP; S.SPECIAL = SPECIAL;

  function sigExists(n) { return !!L.signals[n] && L.signals[n].type !== 'odstepowy'; }
  function normName(x) {
    if (!x) return x;
    var all = Object.keys(L.signals).concat(Object.keys(L.sections), Object.keys(L.blocks), Object.keys(L.derailers), Object.keys(L.switches), ['KZW', 'KZE']);
    var up = x.toUpperCase();
    for (var i = 0; i < all.length; i++) if (all[i].toUpperCase() === up) return all[i];
    return x;
  }

  /* Wykonanie polecenia tekstowego. Zwraca {ok, msg}. */
  function exec(line, opts) {
    opts = opts || {};
    var st = S.st;
    line = (line || '').trim().replace(/\s+/g, ' ');
    if (!line) return { ok: false, msg: '' };
    var parts = line.split(' ');
    var code = parts[0].toUpperCase();
    var a = normName(parts[1]), b = normName(parts[2]);
    if (code === 'KW') { st.alarms.forEach(function (x) { x.ack = true; }); return { ok: true, msg: 'Alarmy pokwitowane' }; }
    if (code === 'POMOC' || code === '?') { if (S.ui) S.ui.togglePanel('help'); return { ok: true, msg: '' }; }
    if (!st.user) return reject(line, 'Brak zalogowanego użytkownika – polecenia nastawcze zablokowane (§7 ust. 2)');
    if (st.pending && !opts.confirm) return reject(line, 'Zainicjowano polecenie specjalne ' + st.pending.code + ' ' + st.pending.arg + ' – potwierdź lub wycofaj (§13 ust. 8)');
    if (st.postStatus !== 'obsluga') return reject(line, 'Posterunek nie jest obsługiwany z tego stanowiska');

    if (SPECIAL[code] && !opts.confirm) {
      var chk = specialPrecheck(code, a, b);
      if (chk) return reject(line, chk);
      st.pending = { code: code, arg: a, arg2: b, line: line, until: st.t + CFG.specialConfirm, notes: specialNotes(code, a) };
      log('POLECENIE', 'Zainicjowano polecenie specjalne: ' + line);
      if (S.ui) S.ui.showConfirm();
      return { ok: true, msg: 'Polecenie specjalne zainicjowane – sprawdź adres i kod, potwierdź' };
    }
    st.stats.cmds++;
    var r = run(code, a, b, line);
    if (r.ok) { log('POLECENIE', line + (r.msg ? ' – ' + r.msg : '')); if (SPECIAL[code]) { st.stats.special++; if (S.ui) S.ui.openBookDraft(code, a, line); } }
    else { st.stats.rejected++; log('ODRZUCONE', line + ' – ' + r.msg); }
    return r;
  }
  S.exec = exec;
  function reject(line, msg) { S.st.stats.rejected++; log('ODRZUCONE', line + ' – ' + msg); return { ok: false, msg: msg }; }

  function specialPrecheck(code, a, b) {
    var st = S.st;
    if (code === 'SZ' || code === 'DZ') { if (!sigExists(a)) return 'Nieznany sygnalizator „' + (a || '') + '”'; }
    if (code === 'SZ') { var t = L.signals[a].type; if (t !== 'wjazdowy' && t !== 'wyjazdowy') return 'Sygnał zastępczy możliwy tylko na semaforze'; if (st.signals[a].aspect === 'train') return 'Semafor wyświetla sygnał zezwalający'; }
    if (code === 'DZ' && !st.signals[a].route && !Object.keys(st.routes).some(function (u) { return st.routes[u].start === a; })) return 'Brak przebiegu od ' + a;
    if (code === 'ZB' || code === 'RK') { if (!L.switches[a]) return 'Nieznana zwrotnica „' + (a || '') + '”'; }
    if (code === 'ZB') { if (b !== '+' && b !== '-') return 'Podaj położenie: ZB ' + a + ' + lub ZB ' + a + ' -'; if (st.switches[a].lock) return 'Zwrotnica utwierdzona w przebiegu'; if (st.switches[a].stopped) return 'Zwrotnica zastopowana'; }
    if (code === 'RK' && !st.switches[a].rozpr) return 'Zwrotnica ' + a + ' nie sygnalizuje rozprucia';
    if (code === 'ZER') { if (!L.sections[a]) return 'Nieznany odcinek „' + (a || '') + '”'; if (st.sections[a].fault !== 'reset' && st.sections[a].fault !== 'occ') return 'Odcinek ' + a + ' nie wymaga zerowania'; }
    if (code === 'BKA' && (!L.blocks[a] || L.blocks[a].type !== 'sbl')) return 'Awaryjna zmiana kierunku dotyczy blokady samoczynnej (WF1, WF2, WA1, WA2)';
    if (code === 'NN1' && st.power.nn) return 'Napięcie nastawcze jest załączone';
    if (code === 'OSL') return 'Nieobsługiwane w prototypie';
    return null;
  }
  function specialNotes(code, a) {
    var n = {
      SZ: ['Sprawdź na monitorze położenie zwrotnic i wykolejnic w drodze przebiegu (§27 ust. 1 pkt 3).',
        'Zwrotnice w drodze przebiegu zamknij: polecenie ZS (zastopowanie), przebieg manewrowy w kierunku przeciwnym lub NN0 (§18 ust. 2, §27 ust. 1 pkt 1–2).',
        'Upewnij się, że droga przebiegu jest wolna; przy wyjeździe – kierunek blokady i wolny pierwszy odstęp (§27 ust. 1 pkt 4).',
        'Użycie odnotuj w książce przebiegów i książce kontroli urządzeń z numerem pociągu i przyczyną (§17 ust. 7 pkt 5).'],
      DZ: ['Upewnij się, że pociąg z sygnałami końca pociągu minął miejsce przebiegowe lub nie zbliża się do semafora (§18 ust. 4).', 'Zwolnienie nastąpi z opóźnieniem czasowym.', 'Odnotuj w książce kontroli urządzeń (§17 ust. 2 pkt 5).'],
      ZB: ['Przekonaj się, że zwrotnica nie jest zajęta przez pojazd i nic się do niej nie zbliża (§15 ust. 3).', 'Wymagany zapis w książce kontroli urządzeń i powiadomienie automatyka.'],
      RK: ['Sprawdź w terenie stan iglic, zamknięć nastawczych, pręta nastawczego i prętów kontrolnych (§22 ust. 3 pkt 1).', 'Załóż zamek zapasowy na iglicę odlegającą; ogranicz prędkość do 40 km/h dla jazd pociągowych (§22 ust. 3 pkt 3, 7).', 'Powiadom pracowników z wykazów A i B książki kontroli urządzeń oraz dyspozytora zakładu.'],
      ZER: ['Stwierdź, że na odcinku nie ma pojazdu kolejowego (warunki wg regulaminu technicznego).', 'Po zerowaniu odcinek oczekuje na pierwszy przejazd – kolejna jazda na sygnał zastępczy lub rozkaz pisemny.'],
      NN1: ['Upewnij się, że pracownicy utrzymania nie pracują przy napędach zwrotnicowych (§13 ust. 10).'],
      BKA: ['Normalna zmiana kierunku jest niemożliwa (§20 ust. 5 pkt 1).', 'Stwierdź, że szlak pomiędzy stacjami jest wolny od pojazdów kolejowych (§20 ust. 5 pkt 2).', 'Polecenie wydaje dyżurny stacji oddającej pozwolenie.']
    };
    return n[code] || [];
  }

  function confirmPending(typed) {
    var st = S.st, p = st.pending; if (!p) return { ok: false, msg: 'Brak polecenia do potwierdzenia' };
    if (st.t > p.until) { st.pending = null; return reject(p.line, 'Upłynął czas na potwierdzenie'); }
    if ((typed || '').trim().toUpperCase() !== String(p.arg).toUpperCase()) return { ok: false, msg: 'Wpisany adres nie zgadza się z adresem polecenia (' + p.arg + ')' };
    st.pending = null;
    return exec(p.line, { confirm: true });
  }
  function cancelPending() { var p = S.st.pending; if (p) { log('POLECENIE', 'Wycofano polecenie specjalne: ' + p.line); S.st.pending = null; } }
  S.confirmPending = confirmPending; S.cancelPending = cancelPending;

  function run(code, a, b, line) {
    var st = S.st, r, w, s, bl, bs, why;
    switch (code) {
      case 'PP': case 'PM':
        if (!sigExists(a)) return { ok: false, msg: 'Nieznany sygnalizator początkowy „' + (a || '') + '”' };
        r = findRoute(a, b, code === 'PP' ? 'P' : 'M');
        if (!r) return { ok: false, msg: 'Brak przebiegu ' + (code === 'PP' ? 'pociągowego' : 'manewrowego') + ' ' + a + ' → ' + (b || '?') + ' w tablicy przebiegów' };
        var res = setRoute(r);
        if (!res.ok) return { ok: false, msg: res.why[0] + (res.why.length > 1 ? ' (+' + (res.why.length - 1) + ')' : '') };
        return { ok: true, msg: 'przebieg ' + r.id + ' w realizacji' };
      case 'ST':
        if (!sigExists(a)) return { ok: false, msg: 'Nieznany sygnalizator' };
        s = st.signals[a]; if (s.aspect === 'stop') return { ok: false, msg: 'Semafor wskazuje „Stój”' };
        if (s.aspect === 'sz') { s.aspect = 'stop'; s.szUntil = 0; return { ok: true, msg: 'wyłączono sygnał zastępczy' }; }
        s.aspect = 'stop'; var ru = st.routes[s.route]; if (ru) { ru.state = 'stopped'; ru.manualStop = true; }
        return { ok: true, msg: 'semafor ' + a + ' – „Stój” (przebieg pozostaje utwierdzony)' };
      case 'STW':
        Object.keys(st.signals).forEach(function (n) { var x = st.signals[n]; if (L.signals[n].type !== 'odstepowy' && x.aspect !== 'stop') { x.aspect = 'stop'; var q = st.routes[x.route]; if (q) { q.state = 'stopped'; q.manualStop = true; } } });
        return { ok: true, msg: 'wszystkie semafory – „Stój”' };
      case 'ZP':
        if (!sigExists(a)) return { ok: false, msg: 'Nieznany sygnalizator' };
        var uid = st.signals[a].route; if (!uid) return { ok: false, msg: 'Brak przebiegu od ' + a };
        r = st.routes[uid]; if (r.passed) return { ok: false, msg: 'Pociąg minął semafor – przebieg zwalnia się samoczynnie lub poleceniem DZ' };
        var appr = L.signals[a].approachSec;
        if ((r.state === 'set' || r.state === 'stopped') && appr && secOcc(appr)) { st.signals[a].aspect = 'stop'; r.timeRelUntil = st.t + CFG.timeRelease; r.secs.forEach(function (sc) { if (st.sections[sc].lock === uid) st.sections[sc].timeRel = r.timeRelUntil; }); return { ok: true, msg: 'zajęty odcinek zbliżania – zwolnienie czasowe ' + CFG.timeRelease + ' s' }; }
        releaseRoute(uid, 'odwołanie poleceniem'); return { ok: true, msg: 'przebieg odwołany' };
      case 'Z+': case 'Z-':
        if (!L.switches[a]) return { ok: false, msg: 'Nieznana zwrotnica „' + (a || '') + '”' };
        w = st.switches[a]; var pos = code === 'Z+' ? '+' : '-';
        if (w.nodata) return { ok: false, msg: 'Brak danych o zwrotnicy' };
        if (w.lock) return { ok: false, msg: 'Zwrotnica utwierdzona w przebiegu' };
        if (w.ovLock) return { ok: false, msg: 'Zwrotnica w drodze ochronnej' };
        if (w.stopped) return { ok: false, msg: 'Zwrotnica zastopowana (§15 ust. 6)' };
        if (w.local) return { ok: false, msg: 'Zwrotnica przekazana do nastawiania lokalnego' };
        if (w.rozpr) return { ok: false, msg: 'Sygnalizowane rozprucie – najpierw RK po sprawdzeniu w terenie' };
        if (!st.power.nn) return { ok: false, msg: 'Napięcie nastawcze wyłączone' };
        if (secOcc(L.switches[a].sec)) return { ok: false, msg: 'Odcinek zwrotnicowy zajęty – możliwe tylko poleceniem specjalnym ZB (§15 ust. 3)' };
        if (!w.moving && w.pos === pos) return { ok: false, msg: 'Zwrotnica już w położeniu „' + pos + '”' };
        moveSwitch(a, pos); return { ok: true, msg: '' };
      case 'ZS': case 'ZO':
        if (!L.switches[a]) return { ok: false, msg: 'Nieznana zwrotnica' };
        st.switches[a].stopped = code === 'ZS'; return { ok: true, msg: code === 'ZS' ? 'zwrotnica zastopowana' : 'zwrotnica odstopowana' };
      case 'WZ': case 'WD':
        if (!L.derailers[a]) return { ok: false, msg: 'Nieznana wykolejnica' };
        var d = st.derailers[a]; if (d.lock) return { ok: false, msg: 'Wykolejnica utwierdzona w przebiegu' };
        if (secOcc(L.derailers[a].sec)) return { ok: false, msg: 'Odcinek zajęty' };
        d.moving = { target: code === 'WZ', until: st.t + CFG.swTime }; return { ok: true, msg: '' };
      case 'ZT': case 'OT':
        if (!L.sections[a]) return { ok: false, msg: 'Nieznany odcinek' };
        s = st.sections[a]; if (code === 'ZT' && s.lock) return { ok: false, msg: 'Odcinek utwierdzony w przebiegu' };
        s.closed = code === 'ZT'; return { ok: true, msg: code === 'ZT' ? 'odcinek zamknięty dla ruchu' : 'odcinek otwarty' };
      case 'SZM': case 'SOM':
        if (!sigExists(a)) return { ok: false, msg: 'Nieznany sygnalizator' };
        if (code === 'SZM' && st.signals[a].aspect !== 'stop') return { ok: false, msg: 'Sygnalizator nie wskazuje „Stój”' };
        st.signals[a].closed = code === 'SZM'; return { ok: true, msg: '' };
      case 'OZ':
        if (!sigExists(a) || st.signals[a].aspect !== 'sz') return { ok: false, msg: 'Na ' + a + ' nie jest wyświetlany sygnał zastępczy' };
        st.signals[a].aspect = 'stop'; st.signals[a].szUntil = 0; return { ok: true, msg: '' };
      case 'NN0': st.power.nn = false; return { ok: true, msg: 'napięcie nastawcze wyłączone' };
      case 'AGR':
        if (st.power.mains) return { ok: false, msg: 'Zasilanie sieciowe obecne' };
        if (st.power.gen) return { ok: false, msg: 'Agregat pracuje' };
        st.power.gen = true; st.power.genFail = false; return { ok: true, msg: 'agregat uruchomiony' };
      case 'BK':
        bl = L.blocks[a]; if (!bl || bl.type !== 'sbl') return { ok: false, msg: 'Nieznany szlak z blokadą samoczynną (WF1, WF2, WA1, WA2)' };
        bs = st.blocks[a]; if (bs.changing) return { ok: false, msg: 'Trwa zmiana kierunku' };
        why = blockChangeConds(a, false);
        if (why.length) return { ok: false, msg: why[0] + ' (§20 ust. 4)' };
        bs.changing = { to: -bs.dir, until: st.t + CFG.blockChange + (bs.nbrReq ? 0 : CFG.nbrDelay * 0.5) };
        return { ok: true, msg: bs.nbrReq ? 'zgoda na zmianę kierunku' : 'żądanie zmiany kierunku wysłane do ' + bl.nbr };
      case 'BKA':
        bl = L.blocks[a]; bs = st.blocks[a];
        bs.changing = { to: -bs.dir, until: st.t + 2, emergency: true };
        return { ok: true, msg: 'awaryjna zmiana kierunku' };
      case 'BPZ':
        bl = L.blocks[a]; if (!bl || bl.type !== 'psb') return { ok: false, msg: 'Nieznany szlak z blokadą półsamoczynną (WD, WO)' };
        bs = st.blocks[a]; if (bs.perm !== 'local') return { ok: false, msg: 'Pozwolenie znajduje się na posterunku ' + bl.nbr };
        why = blockChangeConds(a, false); if (why.length) return { ok: false, msg: why[0] + ' (§20 ust. 9 pkt 3)' };
        bs.changing = { to: 'remote', until: st.t + 2 }; return { ok: true, msg: 'pozwolenie oddane' };
      case 'BZP':
        bl = L.blocks[a]; if (!bl || bl.type !== 'psb') return { ok: false, msg: 'Nieznany szlak z blokadą półsamoczynną' };
        bs = st.blocks[a]; if (bs.perm === 'local') return { ok: false, msg: 'Pozwolenie jest na ŁW' };
        if (bs.req) return { ok: false, msg: 'Żądanie już wysłane' };
        bs.req = { at: st.t }; return { ok: true, msg: 'żądanie pozwolenia wysłane do ' + bl.nbr };
      // ----- specjalne (po potwierdzeniu)
      case 'SZ':
        s = st.signals[a]; s.aspect = 'sz'; s.szUntil = st.t + CFG.szTime; s.failAlarm = false;
        checkSzPractice(a);
        return { ok: true, msg: 'sygnał zastępczy na ' + a + ' (' + CFG.szTime + ' s)' };
      case 'DZ':
        uid = st.signals[a].route || Object.keys(st.routes).filter(function (u) { return st.routes[u].start === a; })[0];
        r = st.routes[uid]; st.signals[a].aspect = st.signals[a].aspect === 'sz' ? 'sz' : 'stop';
        r.timeRelUntil = st.t + CFG.timeRelease; r.secs.forEach(function (sc) { if (st.sections[sc].lock === uid) st.sections[sc].timeRel = r.timeRelUntil; });
        return { ok: true, msg: 'zwolnienie czasowe przebiegu ' + r.id };
      case 'ZB':
        w = st.switches[a]; if (!st.power.nn) return { ok: false, msg: 'Napięcie nastawcze wyłączone' };
        moveSwitch(a, b); return { ok: true, msg: 'przestawienie z bocznikowaniem' };
      case 'RK':
        w = st.switches[a]; w.rozpr = false; if (w.damaged) { w.noCtrl = true; alarm('Zwrotnica ' + a + ': nie można uzyskać kontroli po skasowaniu rozprucia – postępuj wg §22 ust. 2'); }
        return { ok: true, msg: 'skasowano rozprucie zwrotnicy ' + a };
      case 'ZER':
        st.sections[a].fault = 'await'; return { ok: true, msg: 'odcinek ' + a + ' oczekuje na pierwszy przejazd' };
      case 'NN1': st.power.nn = true; return { ok: true, msg: 'napięcie nastawcze załączone' };
    }
    return { ok: false, msg: 'Nieznany kod polecenia „' + code + '” (§13 ust. 12) – wpisz POMOC' };
  }

  // Kontrola praktyki przy sygnale zastępczym (§18 ust. 2, §27 ust. 1)
  function checkSzPractice(sigName) {
    var sig = L.signals[sigName], st = S.st;
    var nid = sig.node, from = null, bad = [], guard = 0;
    while (guard++ < 40) {
      var nx = nextByState(nid, sig.dir, from); if (!nx) break;
      var nn = other(nx.edge, nid), node = L.nodes[nid];
      if (node.sw) { var w = st.switches[node.sw]; if (!w.stopped && !w.lock && st.power.nn) bad.push(node.sw); }
      var stop = (L.nodes[nn].sigs || []).some(function (n) { return L.signals[n].dir === sig.dir && L.signals[n].type !== 'odstepowy'; }) || L.boundaries[nn];
      nid = nn; from = nx.edge; if (stop) break;
    }
    if (bad.length) remark('Sygnał zastępczy na ' + sigName + ': zwrotnice ' + bad.join(', ') + ' nie zostały zabezpieczone (zastopowanie / przebieg manewrowy / NN0) – §18 ust. 2, §27 ust. 1 pkt 1');
    else log('OCENA', 'Sygnał zastępczy na ' + sigName + ': zwrotnice w drodze przebiegu zabezpieczone – zgodnie z §27');
  }

  // ------------------------------------------------------------------ pętla
  function tick(dt) {
    var st = S.st;
    st.t += dt;
    if (st.pending && st.t > st.pending.until) { log('ODRZUCONE', 'Upłynął czas potwierdzenia polecenia ' + st.pending.line); st.pending = null; if (S.ui) S.ui.hideConfirm(); }
    updatePower(); updateSwitches(); updateBlocks();
    if (S.trains) S.trains.tick(dt);
    updateSignals(); updateRoutes();
  }
  S.tick = tick;

  S.reset = function () {
    S.st = initState();
    S.routeTable = buildRouteTable();
  };
  S.reset();
})(window.SIM);
