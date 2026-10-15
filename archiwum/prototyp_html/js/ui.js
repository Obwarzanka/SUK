'use strict';
/*
 * Interfejs: logowanie, mysz (wybór przebiegu, menu kontekstowe), linia poleceń,
 * potwierdzanie poleceń specjalnych, panele (plan ruchu, tablica przebiegów, rejestr,
 * książka kontroli urządzeń E-1758, instruktor, ocena, pomoc), pętla czasu.
 */
(function (S) {
  var L = S.LAYOUT;
  var $ = function (id) { return document.getElementById(id); };
  var U = S.ui = { selStart: null, selSwitch: null, speed: 2, paused: false, panel: null, muted: false, drafts: [] };

  function esc(s) { return String(s == null ? '' : s).replace(/[&<>"]/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]; }); }

  // ------------------------------------------------------------------ polecenia
  U.run = function (line) {
    var r = S.exec(line);
    var out = $('cmdres');
    if (r.msg !== '') { out.textContent = (r.ok ? '✓ ' : '✗ ') + line + (r.msg ? ' – ' + r.msg : ''); out.className = r.ok ? 'ok' : 'err'; }
    if (!r.ok && r.msg) U.beep(true);
    U.refreshPanel();
    return r;
  };

  U.hint = function (t) { $('hint').textContent = t; };

  // ------------------------------------------------------------------ mysz
  U.clickSignal = function (name, ev) {
    hideMenu();
    if (!U.selStart) { if (!(S.routeTable[name] || []).length) { U.hint('Brak przebiegów od ' + name); return; } U.selStart = name; U.hint('Początek: ' + name + ' – wskaż koniec przebiegu (semafor, blokada szlaku lub koniec toru). Esc – rezygnacja.'); return; }
    if (U.selStart === name) { U.selStart = null; return; }
    U.clickDest(name, ev);
  };
  U.clickDest = function (end, ev) {
    if (!U.selStart) return;
    var start = U.selStart;
    var list = (S.routeTable[start] || []).filter(function (r) { return r.end === end; });
    if (!list.length) { U.hint('Brak przebiegu ' + start + ' → ' + end + ' w tablicy przebiegów'); return; }
    var items = list.map(function (r) { return { label: (r.type === 'P' ? 'Przebieg pociągowy ' : 'Przebieg manewrowy ') + start + ' → ' + end, cmd: (r.type === 'P' ? 'PP ' : 'PM ') + start + ' ' + end }; });
    items.push({ label: 'Anuluj', cmd: null });
    showMenu(ev, 'Przebieg', items, function () { U.selStart = null; });
  };

  U.ctxSignal = function (name, ev) {
    var s = S.st.signals[name], d = L.signals[name], items = [];
    items.push({ label: 'Wybierz jako początek przebiegu', fn: function () { U.selStart = name; } });
    if (s.aspect === 'train' || s.aspect === 'shunt') items.push({ label: 'Sygnał „Stój”', cmd: 'ST ' + name });
    if (s.route) items.push({ label: 'Zwolnienie przebiegu (odwołanie)', cmd: 'ZP ' + name });
    items.push(s.closed ? { label: 'Otwarcie sygnalizatora', cmd: 'SOM ' + name } : { label: 'Zamknięcie sygnalizatora', cmd: 'SZM ' + name });
    if (s.aspect === 'sz') items.push({ label: 'Wyłączenie sygnału zastępczego', cmd: 'OZ ' + name });
    if (d.type !== 'tm') items.push({ sep: 'Polecenia specjalne' }, { label: 'Sygnał zastępczy (SZ)', cmd: 'SZ ' + name, special: true });
    items.push({ label: 'Doraźne zwolnienie przebiegu (DZ)', cmd: 'DZ ' + name, special: true });
    showMenu(ev, 'Sygnalizator ' + name, items);
  };
  U.ctxSwitch = function (num, ev) {
    var w = S.st.switches[num];
    U.selSwitch = num;
    var items = [
      { label: 'Przestaw do „+” (Z+)', cmd: 'Z+ ' + num, dis: w.pos === '+' && !w.moving },
      { label: 'Przestaw do „−” (Z-)', cmd: 'Z- ' + num, dis: w.pos === '-' && !w.moving },
      w.stopped ? { label: 'Odstopuj (ZO)', cmd: 'ZO ' + num } : { label: 'Zastopuj (ZS)', cmd: 'ZS ' + num },
      { sep: 'Polecenia specjalne' },
      { label: 'Przestaw „+” z bocznikowaniem (ZB)', cmd: 'ZB ' + num + ' +', special: true },
      { label: 'Przestaw „−” z bocznikowaniem (ZB)', cmd: 'ZB ' + num + ' -', special: true },
      { label: 'Skasuj rozprucie (RK)', cmd: 'RK ' + num, special: true, dis: !w.rozpr }
    ];
    showMenu(ev, 'Zwrotnica ' + num + ' (położenie ' + w.pos + ')', items, function () { U.selSwitch = null; });
  };
  U.ctxSection = function (sec, ev) {
    var s = S.st.sections[sec];
    showMenu(ev, 'Odcinek ' + sec, [
      s.closed ? { label: 'Otwarcie toru (OT)', cmd: 'OT ' + sec } : { label: 'Zamknięcie toru (ZT)', cmd: 'ZT ' + sec },
      { sep: 'Polecenia specjalne' },
      { label: 'Zerowanie licznika osi (ZER)', cmd: 'ZER ' + sec, special: true, dis: !(s.fault === 'reset' || s.fault === 'occ') }
    ]);
  };
  U.ctxDerailer = function (name, ev) {
    var d = S.st.derailers[name];
    showMenu(ev, 'Wykolejnica ' + name, [{ label: 'Załóż na tor (WZ)', cmd: 'WZ ' + name, dis: d.on }, { label: 'Zdejmij z toru (WD)', cmd: 'WD ' + name, dis: !d.on }]);
  };
  U.ctxBlock = function (id, ev) {
    var b = L.blocks[id];
    var items = b.type === 'sbl'
      ? [{ label: 'Zmiana kierunku / zgoda (BK)', cmd: 'BK ' + id }, { sep: 'Polecenia specjalne' }, { label: 'Awaryjna zmiana kierunku (BKA)', cmd: 'BKA ' + id, special: true }]
      : [{ label: 'Oddaj pozwolenie (BPZ)', cmd: 'BPZ ' + id }, { label: 'Żądaj pozwolenia (BZP)', cmd: 'BZP ' + id }];
    showMenu(ev, 'Blokada ' + id + ' – ' + b.nbr, items);
  };

  function showMenu(ev, title, items, onClose) {
    var m = $('menu');
    m.innerHTML = '<div class="m-title">' + esc(title) + '</div>';
    items.forEach(function (it) {
      if (it.sep) { var s = document.createElement('div'); s.className = 'm-sep'; s.textContent = it.sep; m.appendChild(s); return; }
      var d = document.createElement('div');
      d.className = 'm-item' + (it.special ? ' special' : '') + (it.dis ? ' dis' : '');
      d.innerHTML = esc(it.label) + (it.cmd ? '<span class="m-cmd">' + esc(it.cmd) + '</span>' : '');
      d.addEventListener('click', function () {
        if (it.dis) return;
        hideMenu();
        if (it.fn) it.fn(); else if (it.cmd) { U.selStart = null; U.run(it.cmd); }
        if (onClose) onClose();
      });
      m.appendChild(d);
    });
    m.style.display = 'block';
    var x = Math.min(ev.clientX, window.innerWidth - m.offsetWidth - 8), y = Math.min(ev.clientY, window.innerHeight - m.offsetHeight - 8);
    m.style.left = Math.max(4, x) + 'px'; m.style.top = Math.max(4, y) + 'px';
    U.menuClose = onClose;
  }
  function hideMenu() { var m = $('menu'); if (m.style.display === 'block') { m.style.display = 'none'; if (U.menuClose) { var f = U.menuClose; U.menuClose = null; f(); } } }

  // ------------------------------------------------------------------ polecenia specjalne
  U.showConfirm = function () {
    var p = S.st.pending; if (!p) return;
    $('cf-code').textContent = p.code + ' – ' + S.SPECIAL[p.code];
    $('cf-addr').textContent = p.arg + (p.arg2 ? ' ' + p.arg2 : '');
    $('cf-notes').innerHTML = p.notes.map(function (n) { return '<li>' + esc(n) + '</li>'; }).join('');
    $('cf-input').value = ''; $('cf-err').textContent = '';
    $('confirm').style.display = 'flex';
    setTimeout(function () { $('cf-input').focus(); }, 30);
  };
  U.hideConfirm = function () { $('confirm').style.display = 'none'; };
  function doConfirm() {
    var r = S.confirmPending($('cf-input').value);
    if (!r.ok && S.st.pending) { $('cf-err').textContent = r.msg; return; }
    U.hideConfirm();
    var out = $('cmdres'); out.textContent = (r.ok ? '✓ ' : '✗ ') + (r.msg || ''); out.className = r.ok ? 'ok' : 'err';
  }

  // ------------------------------------------------------------------ książka E-1758
  U.openBookDraft = function (code, arg, line) {
    U.drafts.push({ time: S.clock(), code: code, arg: arg, line: line, t: S.st.t });
    U.togglePanel('book', true);
  };

  // ------------------------------------------------------------------ dźwięk
  var actx = null;
  U.beep = function (short) {
    if (U.muted) return;
    try {
      actx = actx || new (window.AudioContext || window.webkitAudioContext)();
      var o = actx.createOscillator(), g = actx.createGain();
      o.frequency.value = short ? 520 : 880; g.gain.value = 0.05;
      o.connect(g); g.connect(actx.destination); o.start(); o.stop(actx.currentTime + (short ? 0.08 : 0.25));
    } catch (e) { /* brak audio */ }
  };

  // ------------------------------------------------------------------ panele
  U.togglePanel = function (name, forceOpen) {
    if (U.panel === name && !forceOpen) { U.panel = null; $('side').classList.remove('open'); }
    else { U.panel = name; $('side').classList.add('open'); }
    document.querySelectorAll('nav button[data-panel]').forEach(function (b) { b.classList.toggle('active', b.getAttribute('data-panel') === U.panel); });
    U.refreshPanel(true);
  };
  U.onLog = function () { if (U.panel === 'log') U.logDirty = true; };

  U.refreshPanel = function (force) {
    var p = U.panel, box = $('side-body');
    if (!p) return;
    var act = document.activeElement;
    if (!force && act && box.contains(act) && (act.tagName === 'INPUT' || act.tagName === 'TEXTAREA' || act.tagName === 'SELECT')) return;
    var h = '';
    if (p === 'plan') h = panelPlan();
    else if (p === 'routes') h = panelRoutes();
    else if (p === 'log') h = panelLog();
    else if (p === 'book') h = panelBook();
    else if (p === 'instr') h = panelInstr();
    else if (p === 'score') h = panelScore();
    else if (p === 'help') h = panelHelp();
    if (box.getAttribute('data-p') === p && box.getAttribute('data-h') === String(h.length) && !force && p !== 'plan' && p !== 'score') return;
    var keep = {};
    box.querySelectorAll('input,select,textarea').forEach(function (i) { if (i.id) keep[i.id] = i.type === 'checkbox' ? i.checked : i.value; });
    box.innerHTML = h; box.setAttribute('data-p', p); box.setAttribute('data-h', String(h.length));
    Object.keys(keep).forEach(function (id) { var i = $(id); if (i && !force) { if (i.type === 'checkbox') i.checked = keep[id]; else i.value = keep[id]; } });
    wirePanel(p);
  };

  function panelPlan() {
    var st = S.st, T = S.trains;
    var rows = T.queue.slice().sort(function (a, b) { return a.t - b.t; }).map(function (q) {
      var tr = T.list.filter(function (x) { return x.num === q.num || (x.term === null && q.term && x.num === q.term.newNum) || x.num === q.num + 'M'; })[0];
      var status;
      if (!q.done) status = q.t <= st.t ? '<span class="warn">oczekuje na szlak</span>' : 'planowy';
      else if (!tr) status = 'obsłużony';
      else if (tr.state === 'dwell') status = 'postój (tor ' + trackName(tr) + ')';
      else if (tr.readySince && tr.v < 0.1) status = '<span class="warn">gotowy do odjazdu' + (tr.waitingAt ? ' (' + esc(tr.waitingAt) + ')' : '') + '</span>';
      else if (tr.waitingAt) status = '<span class="warn">stoi przed ' + esc(tr.waitingAt) + '</span>';
      else status = 'w ruchu';
      var dest = q.term && q.term.reverse ? 'kończy bieg → ' + q.term.newNum + ' do ' + nb(q.term.to) : (q.term && q.term.odstaw ? 'kończy bieg, odstawienie' : nb(q.to));
      return '<tr><td><b>' + esc(q.num) + '</b></td><td>' + esc(q.cat) + '</td><td>' + esc(nb(q.from)) + '</td><td>' + esc(dest) + '</td><td>' + S.clock(q.t) + '</td><td>' + (q.dep ? S.clock(q.dep) : (q.stop ? '' : 'bez post.')) + '</td><td>' + esc(q.track || '') + '</td><td>' + status + '</td></tr>';
    }).join('');
    return '<h3>Plan ruchu – posterunek ŁW</h3><p class="small">Godz. przyjazdu = wyprawienie z posterunku sąsiedniego. Tor = zalecany tor przyjęcia.</p>' +
      '<table class="tbl"><tr><th>Nr</th><th>Kat.</th><th>Z</th><th>Do</th><th>Wypr.</th><th>Odj.</th><th>Tor</th><th>Stan</th></tr>' + rows + '</table>';
  }
  function nb(sz) { if (!sz) return '–'; var b = L.blocks[sz]; return b ? sz + ' (' + b.nbr.split(' (')[0] + ')' : sz; }
  function trackName(tr) { for (var i = tr.segs.length - 1; i >= 0; i--) { var e = tr.segs[i].e; if (e && L.edges[e].track) return L.edges[e].track; } return '?'; }

  function panelRoutes() {
    var f = ($('rt-filter') && $('rt-filter').value || '').toUpperCase();
    var rows = [];
    Object.keys(S.routeTable).sort().forEach(function (k) {
      S.routeTable[k].forEach(function (r) {
        if (f && r.id.toUpperCase().indexOf(f) < 0) return;
        rows.push('<tr><td>' + (r.type === 'P' ? 'poc.' : 'man.') + '</td><td><b>' + esc(r.start) + '</b></td><td><b>' + esc(r.end) + '</b></td><td>' + r.sw.map(function (q) { return q.num + q.pos; }).join(' ') + (r.ders.length ? ' ' + r.ders.join(' ') : '') + '</td><td class="small">' + r.secs.join(' ') + '</td><td>' + (r.overlap || '') + '</td></tr>');
      });
    });
    return '<h3>Tablica przebiegów (generowana z topologii)</h3><input id="rt-filter" placeholder="filtr, np. A→ lub S1" value="' + esc(f) + '">' +
      '<table class="tbl"><tr><th>Typ</th><th>Od</th><th>Do</th><th>Zwrotnice</th><th>Odcinki</th><th>Dr. ochr.</th></tr>' + rows.join('') + '</table><p class="small">' + rows.length + ' przebiegów</p>';
  }

  function panelLog() {
    var f = $('log-filter') ? $('log-filter').value : '';
    var rows = S.st.log.slice().reverse().filter(function (l) { return !f || l.kind === f; }).slice(0, 400).map(function (l) {
      return '<tr class="k-' + l.kind + '"><td>' + l.time + '</td><td>' + l.kind + '</td><td>' + esc(l.text) + '</td><td>' + esc(l.user) + '</td></tr>';
    }).join('');
    var kinds = ['', 'POLECENIE', 'ODRZUCONE', 'ZDARZENIE', 'ALARM', 'RUCH', 'UWAGA', 'OCENA', 'INSTRUKTOR', 'E-1758'];
    return '<h3>Rejestrator zdarzeń (§21 ust. 3)</h3><select id="log-filter">' + kinds.map(function (k) { return '<option value="' + k + '"' + (k === f ? ' selected' : '') + '>' + (k || 'wszystkie') + '</option>'; }).join('') +
      '</select> <button id="log-csv">Eksport CSV</button><table class="tbl"><tr><th>Czas</th><th>Rodzaj</th><th>Treść</th><th>Użytk.</th></tr>' + rows + '</table>';
  }

  function panelBook() {
    var d = U.drafts[0];
    var form = '';
    if (d) {
      var pre = d.time + ' – użyto polecenia specjalnego: ' + S.SPECIAL[d.code] + ' (' + d.line + ').\nOkoliczności / przyczyna: \nWprowadzone obostrzenia: \n';
      form = '<div class="draft"><b>Wymagany zapis po poleceniu specjalnym</b> (§14 ust. 4)' + (U.drafts.length > 1 ? ' – oczekujących: ' + U.drafts.length : '') +
        '<textarea id="bk-text" rows="6">' + esc(pre) + '</textarea>' +
        '<label>Powiadomiono automatyka (imię i nazwisko, godz.): <input id="bk-auto" placeholder="np. J. Kowalski, 06:12"></label>' +
        '<button id="bk-save">Zapisz w książce</button></div>';
    } else {
      form = '<div class="draft"><b>Nowy wpis</b><textarea id="bk-text" rows="4" placeholder="Treść zapisu (usterka, przeszkoda, obostrzenie…)"></textarea>' +
        '<label>Powiadomiono automatyka: <input id="bk-auto"></label><button id="bk-save">Zapisz w książce</button></div>';
    }
    var list = S.st.book.slice().reverse().map(function (b) { return '<div class="entry"><div class="small">' + b.time + ' · ' + esc(b.user) + (b.auto ? ' · powiadomiono: ' + esc(b.auto) : ' · <span class="warn">brak powiadomienia automatyka</span>') + '</div><pre>' + esc(b.text) + '</pre></div>'; }).join('');
    return '<h3>Książka kontroli urządzeń (E-1758)</h3>' + form + list;
  }

  function panelInstr() {
    var opts = function (arr) { return arr.map(function (x) { return '<option>' + esc(x) + '</option>'; }).join(''); };
    var ft = Object.keys(S.FAULTS).map(function (k) { return '<option value="' + k + '">' + esc(S.FAULTS[k].name) + '</option>'; }).join('');
    var sc = Object.keys(S.SCENARIOS).map(function (k) { return '<option value="' + k + '"' + (S.trains.scenario === k ? ' selected' : '') + '>' + esc(S.SCENARIOS[k].name) + '</option>'; }).join('');
    return '<h3>Panel instruktora</h3>' +
      '<fieldset><legend>Usterki</legend><select id="in-ft">' + ft + '</select><br><select id="in-target"></select> ' +
      '<button id="in-apply">Wprowadź</button> <button id="in-clear">Usuń</button></fieldset>' +
      '<fieldset><legend>Pociąg dodatkowy</legend>Ze szlaku <select id="in-from">' + opts(Object.keys(L.blocks)) + '</select> kat. <select id="in-cat">' + opts(Object.keys(S.CAT)) + '</select><br>' +
      'nr <input id="in-num" size="7" placeholder="auto"> do <select id="in-to"><option value="">–</option>' + opts(Object.keys(L.blocks)) + '</select> <label><input type="checkbox" id="in-stop" checked> postój</label> <button id="in-spawn">Dodaj do kolejki</button></fieldset>' +
      '<fieldset><legend>Scenariusz</legend><select id="in-sc">' + sc + '</select> <button id="in-restart">Uruchom od nowa</button><p class="small" id="in-desc"></p></fieldset>' +
      '<fieldset><legend>Stan urządzeń</legend><button id="in-nodata">Brak danych: wszystkie sygnalizatory (5 s)</button></fieldset>';
  }

  function panelScore() {
    var st = S.st, s = st.stats;
    var unsaved = U.drafts.length;
    var lines = [
      ['Polecenia wykonane', s.cmds], ['Polecenia odrzucone', s.rejected], ['Polecenia specjalne', s.special],
      ['Zapisy w książce E-1758', st.book.length], ['Brakujące zapisy po poleceniach specjalnych', unsaved],
      ['Pociągi przyjęte / wyprawione', s.trainsIn + ' / ' + s.trainsOut], ['Wyprawienia w niewłaściwym kierunku', s.wrongDir],
      ['Łączny czas postoju pociągów przed semaforami', Math.round(s.delaySum) + ' s'], ['Minięcia sygnału „Stój”', s.spad || 0]
    ];
    return '<h3>Ocena przebiegu ćwiczenia</h3><table class="tbl">' + lines.map(function (l) { return '<tr><td>' + l[0] + '</td><td><b>' + l[1] + '</b></td></tr>'; }).join('') + '</table>' +
      '<h4>Uwagi (zgodność z Ie-20)</h4>' + (st.remarks.length ? '<ul>' + st.remarks.map(function (r) { return '<li>' + r.time + ' – ' + esc(r.text) + '</li>'; }).join('') + '</ul>' : '<p class="small">Brak uwag.</p>');
  }

  function panelHelp() {
    var C = S.COLORS;
    var leg = [[C.grey, 'stan podstawowy'], [C.red, 'odcinek zajęty / sygnalizator na początku lub końcu utwierdzonego przebiegu'], [C.green, 'przebieg pociągowy / sygnał zezwalający'], [C.yellow, 'przebieg manewrowy, droga ochronna'], [C.magenta, 'zwalnianie czasowe, zastopowanie, zamknięcie'], [C.white, 'brak danych / brak kontroli (miganie), sygnał zastępczy (miganie)'], [C.darkRed, 'oczekiwanie na pierwszy przejazd (po zerowaniu)']];
    return '<h3>Pomoc</h3><p><b>Mysz:</b> LPM na semaforze – początek przebiegu; LPM na drugim semaforze / ramce blokady / końcu toru – koniec i wybór rodzaju przebiegu. PPM na semaforze, zwrotnicy, odcinku, blokadzie – menu poleceń. Esc – rezygnacja.</p>' +
      '<p><b>Polecenia specjalne</b> są dwuetapowe: po zainicjowaniu trzeba wpisać ponownie adres obiektu i potwierdzić w ciągu ' + S.CFG.specialConfirm + ' s (§13 ust. 7–9). Po wykonaniu otwiera się książka kontroli urządzeń.</p>' +
      '<h4>Barwy zobrazowania (Ie-104)</h4>' + leg.map(function (l) { return '<div class="leg"><span style="background:' + l[0] + '"></span>' + l[1] + '</div>'; }).join('') +
      '<h4>Polecenia</h4><table class="tbl">' + S.HELP.map(function (h) { return '<tr><td><code>' + esc(h[0]) + '</code></td><td>' + esc(h[1]) + '</td></tr>'; }).join('') + '</table>' +
      '<h4>Szlaki</h4><p class="small">WF1, WF2 – Łódź Fabryczna (blokada samoczynna dwukierunkowa); WA1, WA2 – Łódź Andrzejów (j.w.); WD – l. 540 Dąbrowa/Chojny, WO – l. 541 Olechów (blokada półsamoczynna z blokiem pozwolenia i kontrolą niezajętości, §20 ust. 9).</p>';
  }

  function wirePanel(p) {
    if (p === 'routes') $('rt-filter').addEventListener('input', function () { U.refreshPanel(true); var i = $('rt-filter'); i.focus(); i.setSelectionRange(i.value.length, i.value.length); });
    if (p === 'log') {
      $('log-filter').addEventListener('change', function () { U.refreshPanel(true); });
      $('log-csv').addEventListener('click', exportCsv);
    }
    if (p === 'book') $('bk-save').addEventListener('click', function () {
      var text = $('bk-text').value.trim(); if (!text) return;
      var d = U.drafts.shift();
      S.st.book.push({ time: S.clock(), user: S.st.user ? S.st.user.name : '-', text: text, auto: $('bk-auto').value.trim(), forCode: d ? d.code : null });
      S.log('E-1758', text.split('\n')[0]);
      if (d && !$('bk-auto').value.trim()) S.remark('Zapis po poleceniu ' + d.line + ' bez informacji o powiadomieniu automatyka (§14 ust. 4)');
      U.refreshPanel(true);
    });
    if (p === 'instr') {
      var fillTargets = function () {
        var f = S.FAULTS[$('in-ft').value], sel = $('in-target'), arr = [];
        if (f.target === 'sec') arr = Object.keys(L.sections).sort();
        if (f.target === 'sw') arr = Object.keys(L.switches).sort(function (a, b) { return a - b; });
        if (f.target === 'sig') arr = Object.keys(L.signals).filter(function (n) { return L.signals[n].type !== 'odstepowy'; }).sort();
        if (f.target === 'block') arr = Object.keys(L.blocks);
        sel.innerHTML = arr.length ? arr.map(function (x) { return '<option>' + x + '</option>'; }).join('') : '<option value="">(cała nastawnia)</option>';
      };
      $('in-ft').addEventListener('change', fillTargets); fillTargets();
      $('in-apply').addEventListener('click', function () { S.applyFault($('in-ft').value, $('in-target').value, false); });
      $('in-clear').addEventListener('click', function () { S.applyFault($('in-ft').value, $('in-target').value, true); });
      $('in-spawn').addEventListener('click', function () { S.trains.enqueueManual($('in-from').value, $('in-cat').value, $('in-num').value.trim(), $('in-to').value, $('in-stop').checked); S.log('INSTRUKTOR', 'Dodano pociąg do kolejki ze szlaku ' + $('in-from').value); });
      var desc = function () { $('in-desc').textContent = S.SCENARIOS[$('in-sc').value].desc; };
      $('in-sc').addEventListener('change', desc); desc();
      $('in-restart').addEventListener('click', function () { start($('in-sc').value, S.st.user); });
      $('in-nodata').addEventListener('click', function () {
        Object.keys(S.st.signals).forEach(function (n) { S.st.signals[n].nodata = true; });
        S.alarm('Utrata komunikacji z systemem zależnościowym – brak aktualnych danych');
        setTimeout(function () { Object.keys(S.st.signals).forEach(function (n) { S.st.signals[n].nodata = false; }); }, 5000);
      });
    }
  }

  function exportCsv() {
    var rows = [['czas', 'rodzaj', 'tresc', 'uzytkownik']].concat(S.st.log.map(function (l) { return [l.time, l.kind, l.text, l.user]; }));
    var csv = '﻿' + rows.map(function (r) { return r.map(function (c) { return '"' + String(c).replace(/"/g, '""') + '"'; }).join(';'); }).join('\r\n');
    var a = document.createElement('a');
    a.href = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }));
    a.download = 'rejestr_LCS_Widzew_' + S.clock().replace(/:/g, '') + '.csv';
    a.click();
  }

  // ------------------------------------------------------------------ pasek górny, alarmy
  function updateTop() {
    var st = S.st, p = st.power;
    $('clock').textContent = S.clock();
    $('life').classList.toggle('on', Math.floor(performance.now() / 500) % 2 === 0);
    var lamp = function (id, on, warn) { var e = $(id); e.className = 'lamp ' + (on ? (warn ? 'warn' : 'on') : 'off'); };
    lamp('l-mains', p.mains); lamp('l-ups', p.ups && !p.mains, true); lamp('l-gen', p.gen, true); lamp('l-nn', p.nn);
    var ps = $('post-status');
    if (!st.user) { ps.textContent = 'brak możliwości wydawania poleceń'; ps.className = 'ps-off'; }
    else { ps.textContent = 'obsługa: ' + st.user.name + ' (' + st.user.role + ')'; ps.className = 'ps-on'; }
    var al = st.alarms.filter(function (a) { return !a.ack; });
    var box = $('alarms');
    var html = al.slice(-4).reverse().map(function (a) { return '<div class="al">⚠ ' + a.time + ' ' + esc(a.text) + '</div>'; }).join('') || '<div class="al-none">Brak niepokwitowanych alarmów</div>';
    if (box.getAttribute('data-h') !== html) { box.innerHTML = html; box.setAttribute('data-h', html); }
    $('al-count').textContent = al.length ? '(' + al.length + ')' : '';
    if (st.pending) { var left = Math.max(0, Math.ceil(st.pending.until - st.t)); $('cf-time').textContent = left + ' s'; }
  }

  // ------------------------------------------------------------------ start / pętla
  function start(scenario, user) {
    S.reset();
    S.trains.load(scenario);
    U.drafts = []; U.selStart = null;
    S.st.user = user || null;
    S.log('ZDARZENIE', 'Start symulacji – scenariusz: ' + S.SCENARIOS[scenario].name + (user ? ', zalogowany: ' + user.name : ''));
    S.render.update(); U.refreshPanel(true);
  }

  var lastT = null, acc = 0, renderAcc = 0, panelAcc = 0;
  function loop(now) {
    if (lastT == null) lastT = now;
    var real = Math.min(0.25, (now - lastT) / 1000); lastT = now;
    if (!U.paused && S.st.user !== undefined) {
      acc += real * U.speed;
      var steps = 0;
      while (acc >= 0.1 && steps < 120) { S.tick(0.1); acc -= 0.1; steps++; }
    }
    renderAcc += real; panelAcc += real;
    if (renderAcc > 0.06) { S.render.update(); updateTop(); renderAcc = 0; }
    if (panelAcc > 1) { if (U.panel && U.panel !== 'book' && U.panel !== 'instr') U.refreshPanel(); panelAcc = 0; }
    requestAnimationFrame(loop);
  }

  U.init = function () {
    S.render.build($('panel'));
    start('podstawowy', null);
    U.paused = true;
    // logowanie
    var sc = $('lg-sc');
    sc.innerHTML = Object.keys(S.SCENARIOS).map(function (k) { return '<option value="' + k + '">' + esc(S.SCENARIOS[k].name) + '</option>'; }).join('');
    $('lg-go').addEventListener('click', function () {
      var name = $('lg-name').value.trim() || 'Dyżurny';
      start(sc.value, { name: name, role: $('lg-role').value });
      $('login').style.display = 'none'; U.paused = false; $('cmd').focus();
    });
    $('btn-logout').addEventListener('click', function () {
      if (S.st.user) { S.log('ZDARZENIE', 'Wylogowano: ' + S.st.user.name); S.st.user = null; $('login').style.display = 'flex'; $('lg-go').textContent = 'Zaloguj i rozpocznij od nowa'; $('lg-resume').style.display = 'inline-block'; }
    });
    $('lg-resume').addEventListener('click', function () {
      var name = $('lg-name').value.trim() || 'Dyżurny';
      S.st.user = { name: name, role: $('lg-role').value }; S.log('ZDARZENIE', 'Zalogowano: ' + name);
      $('login').style.display = 'none'; U.paused = false;
    });
    // linia poleceń
    var hist = [], hi = 0;
    $('cmd').addEventListener('keydown', function (e) {
      if (e.key === 'Enter') { var v = this.value; if (v.trim()) { hist.push(v); hi = hist.length; U.run(v); } this.value = ''; }
      else if (e.key === 'ArrowUp') { if (hi > 0) { hi--; this.value = hist[hi]; } e.preventDefault(); }
      else if (e.key === 'ArrowDown') { if (hi < hist.length) { hi++; this.value = hist[hi] || ''; } e.preventDefault(); }
    });
    $('cf-ok').addEventListener('click', doConfirm);
    $('cf-input').addEventListener('keydown', function (e) { if (e.key === 'Enter') doConfirm(); if (e.key === 'Escape') { S.cancelPending(); U.hideConfirm(); } });
    $('cf-cancel').addEventListener('click', function () { S.cancelPending(); U.hideConfirm(); });
    document.addEventListener('keydown', function (e) {
      if (e.key === 'Escape') { U.selStart = null; U.selSwitch = null; hideMenu(); }
      if (e.key === 'F1') { e.preventDefault(); U.togglePanel('help'); }
    });
    document.addEventListener('click', function (e) { if (!$('menu').contains(e.target) && !e.target.closest('.hit-c') && !e.target.closest('.clickable')) hideMenu(); });
    $('panel').addEventListener('contextmenu', function (e) { e.preventDefault(); });
    // przyciski
    document.querySelectorAll('nav button[data-panel]').forEach(function (b) { b.addEventListener('click', function () { U.togglePanel(b.getAttribute('data-panel')); }); });
    document.querySelectorAll('.speed button').forEach(function (b) {
      b.addEventListener('click', function () {
        var v = b.getAttribute('data-s');
        if (v === 'p') { U.paused = !U.paused; b.classList.toggle('active', U.paused); return; }
        U.speed = +v; document.querySelectorAll('.speed button[data-s]:not([data-s="p"])').forEach(function (x) { x.classList.toggle('active', x === b); });
      });
    });
    $('btn-ack').addEventListener('click', function () { U.run('KW'); });
    $('btn-mute').addEventListener('click', function () { U.muted = !U.muted; this.textContent = U.muted ? '🔇' : '🔊'; });
    $('side-close').addEventListener('click', function () { U.togglePanel(U.panel); });
    requestAnimationFrame(loop);
  };

  window.addEventListener('DOMContentLoaded', U.init);
})(window.SIM);
