using System.IO;
using System.Windows.Threading;
using LcsWidzew.Model;
using LcsWidzew.Sim;

namespace LcsWidzew.Net;

/// <summary>
/// Pulpit zdalny SimRail (etap 3 integracji): stanowisko SUK bez własnego komputera zależności. Zależności liczy serwer gry,
/// SUK jest tylko pulpitem – jak stanowisko sieciowe (<see cref="RemoteSession"/>), ale źródłem meldunków jest gra:
/// <list type="bullet">
/// <item>meldunki (<see cref="Indication"/>) budowane ze stanów moda SimRailMost (UDP 47130) przez mapę nazw
///       <c>&lt;układ&gt;.simrail.json</c> (nazwa SimRail → odcinek / zwrotnica / sygnalizator SUK);</item>
/// <item>polecenia z pulpitu (Exec) tłumaczone na polecenia gry i wysyłane do moda (UDP 47131) – na razie pojedyncze:
///       Plus, Minus, Stop, oStop, Stój; przebiegi dopiero po podsłuchu prawdziwego polecenia pulpitu gry.</item>
/// </list>
/// </summary>
public sealed class SimRailSession : ILcsSession, IDisposable
{
    /// <summary>Pulpit zdalny działa (okno „SimRail” nie zatrzymuje wtedy wspólnego odbioru).</summary>
    public static bool Aktywna { get; private set; }

    readonly Dispatcher ui;
    readonly SimRailOdbiornik odb = SimRailOdbiornik.Wspolny;
    readonly Station uklad;
    readonly Dictionary<string, List<string>> doSimRail = new(StringComparer.Ordinal);   // id SUK → nazwy SimRail
    readonly Dictionary<string, string> doSuk = new(StringComparer.OrdinalIgnoreCase);     // nazwa SimRail → id SUK
    readonly Dictionary<string, bool> poprzZaj = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Zwrotnice gry, które choć raz podały położenie (Wprost/Bok). Tylko dla nich „NoDir” znaczy przekładanie –
    /// pulpit Domino podaje zawsze NoDir i jego zwrotnice nie migałyby bez końca.</summary>
    readonly HashSet<string> zwrZPolozeniem = new(StringComparer.OrdinalIgnoreCase);
    long seq; long nrZdarzenia;
    SimRailMapa posterunkiUkladu = new();
    bool obcyPosterunek;
    User user;

    public Simulator Sim => null;
    public bool IsHost => false;
    public string DeskId => "simrail";
    public string DeskName => "Pulpit zdalny SimRail";
    public Station L { get; }
    public Dictionary<string, List<RouteDef>> RouteTable { get; }
    public Indication Last { get; private set; }
    public List<EventRecord> Events { get; } = new();
    public List<EventRecord> Alarms { get; } = new();

    public event Action<EventRecord> EventAdded, AlarmAdded;
    public event Action AlarmsChanged, Beep, SpecialExecuted, PendingExpired;
    public event Action<string> Disconnected;
    public event Action Reset;

    public SimRailSession(Station uklad, Dispatcher ui, int port = SimRailOdbiornik.DomyslnyPort)
    {
        this.uklad = uklad; this.ui = ui; L = uklad.View;
        // tablica przebiegów układu – do wyboru początku i końca na pulpicie (wysyłanie przebiegów do gry jeszcze nieobsługiwane)
        try { RouteTable = new Simulator(uklad).RouteTable; } catch { RouteTable = new(); }
        var mapa = SimRailMapa.Wczytaj(SimRailMapa.PlikDla(uklad.SourcePath));
        foreach (var (sr, suk) in mapa.Mapa)
        {
            doSuk[sr] = suk;
            if (!doSimRail.TryGetValue(suk, out var l)) doSimRail[suk] = l = new();
            l.Add(sr);
        }
        if (mapa.Mapa.Count == 0) throw new InvalidOperationException($"Brak mapy nazw SimRail → SUK ({SimRailMapa.PlikDla(uklad.SourcePath)}). Wypełnij ją w oknie „SimRail”.");
        // układ przypisany do innego posterunku niż ten w grze – nie pokazuj obcych stanów (te same nazwy obiektów mogą się powtarzać)
        var stGry0 = odb.PosterunkiGry();
        if (mapa.Posterunki.Count > 0 && stGry0.Count > 0 && !mapa.ObejmujeKtorys(stGry0))
            throw new InvalidOperationException($"W grze jest posterunek „{string.Join(", ", stGry0)}”, a ten układ obsługuje: {string.Join(", ", mapa.Posterunki)}. Uruchom układ tego posterunku.");
        posterunkiUkladu = mapa;
        // katalog przebiegów gry zapamiętany przy poprzednim połączeniu (<układ>.przebiegi_gry.json): przebiegi, których SUK sam nie wyliczy
        // (np. R1 → S3, U1 → P1 – koniec na semaforze przeciwnego kierunku), są od razu, także gdy gra jest w menu; świeży katalog z gry zastępuje go
        if (KatalogZapamietany() is string kz && File.Exists(kz))
            try { ScalKatalog(File.ReadAllText(kz), kz, zPamieci: true); } catch (Exception ex) { Zdarz("Zdarzenie", "", "Zapamiętany katalog przebiegów gry – błąd: " + ex.Message); }
        if (!odb.Dziala) odb.Start(port);
        Aktywna = true;
        Zdarz("Zdarzenie", "", "Pulpit zdalny SimRail: stany i polecenia przez mod SimRailMost (zależności liczy gra)");
    }

    public void Dispose() { Aktywna = false; }

    // ------------------------------------------------------------------ meldunki z gry

    public Indication Receive()
    {
        var wiek = odb.OstatniPakiet == default ? double.MaxValue : (DateTime.Now - odb.OstatniPakiet).TotalSeconds;
        if (wiek > 10) return Last = KzLink.NoData(L, Last);
        // gracz przeszedł w grze na inny posterunek – brak danych zamiast stanów obcej stacji
        var stGryL = odb.PosterunkiGry();
        var stGry = string.Join(", ", stGryL);
        bool obcy = posterunkiUkladu.Posterunki.Count > 0 && stGryL.Count > 0 && !posterunkiUkladu.ObejmujeKtorys(stGryL);
        if (obcy != obcyPosterunek)
        {
            obcyPosterunek = obcy;
            Zdarz(obcy ? "Alarm" : "Zdarzenie", "", obcy ? $"W grze posterunek „{stGry}” – nie należy do tego układu ({string.Join(", ", posterunkiUkladu.Posterunki)}); stany wstrzymane"
                                                      : $"W grze znów posterunek tego układu („{stGry}”)");
        }
        if (obcy) return Last = KzLink.NoData(L, Last);
        PobierzKatalog();
        var I = new Indication { Seq = ++seq, User = user, Clock = Zegar(odb.CzasGry) };
        I.T = TimeSpan.TryParse(I.Clock, out var ts) ? ts.TotalSeconds : 0;
        foreach (var k in L.Sections.Keys) I.Secs[k] = new SecInd();
        foreach (var k in L.Switches.Keys) I.Sws[k] = new SwInd { Pos = Last?.Sws.GetValueOrDefault(k)?.Pos ?? "+" };
        foreach (var k in L.Signals.Keys) I.Sigs[k] = new SigInd { Aspect = Aspect.Stop };
        foreach (var k in L.Derailers.Keys) I.Ders[k] = new DerInd { On = true };
        foreach (var k in L.Blocks.Keys) I.Blocks[k] = new BlockInd();
        foreach (var p in L.Posts.Keys) I.Control[p] = DeskId;
        I.Desks.Add(new DeskInd { Id = DeskId, Name = DeskName, User = user?.Name });

        // mod ≥ 0.10.2 podaje posterunek obiektu – obiekty posterunków spoza układu pomijane (starszy mod: pusty, bez filtra)
        var wPrzebiegu = new Dictionary<string, (string Typ, bool Czasowe)>(StringComparer.Ordinal);
        var obiekty = odb.Obiekty().Where(o => o.Stacja.Length == 0 || posterunkiUkladu.Posterunki.Count == 0 || posterunkiUkladu.Obejmuje(o.Stacja)).ToList();
        foreach (var o in obiekty)
        {
            if (!doSuk.TryGetValue(o.Nazwa, out var id)) continue;
            // stany liczbowo (mod ≥ 0.9) – nazwy wyliczeń gry mają polskie aliasy; starszy mod – po tekście
            // przebieg na pulpicie dopiero, gdy gra go potwierdzi (Confirmed / ConfirmedInRoute) – samo wybranie
            // albo budowanie przebiegu (ChoosedRoute, BuildingRoute) nie jest jeszcze przebiegiem ustawionym w grze
            bool potw = o.Pn >= 0 ? (o.Pn & (RsConfirmed | RsConfirmedInRoute)) != 0 : o.Przebieg.Contains("Confirmed");
            bool pociagowy = potw && (o.Pn >= 0 ? (o.Pn & RsTrainRoute) != 0 : o.Przebieg.Contains("TrainRoute"));
            bool manewrowy = potw && (o.Pn >= 0 ? (o.Pn & RsShuntingRoute) != 0 : o.Przebieg.Contains("ShuntingRoute"));
            bool sygnal = o.Pn >= 0 ? (o.Pn & RsSignal) != 0 : o.Przebieg.Contains("Signal");
            bool zastepczy = o.Pn >= 0 ? (o.Pn & RsSubsidiary) != 0 : o.Przebieg.Contains("SubsidiarySignal");
            bool czasowe = o.Pn >= 0 ? (o.Pn & RsTimeRelease) != 0 : o.Przebieg.Contains("TimeRelease");
            bool zastopowany = o.Bln >= 0 ? (o.Bln & (BsStopped | BsGlobalStopped)) != 0 : o.Blokada.Contains("Stopped") || o.Blokada.Contains("Zastopowanie");
            bool zamkniety = o.Bln >= 0 && (o.Bln & BsIndividualClose) != 0 || o.Blokada.Contains("IndividualClose");
            // droga ochronna za semaforem końcowym i ochrona boczna przebiegu – w grze bity BlockingState (Overlap, FlankProtection)
            bool ochronna = o.Bln >= 0 ? (o.Bln & BsOverlap) != 0 : o.Blokada.Contains("Overlap") || o.Blokada.Contains("DrogaOchronna");
            bool boczna = o.Bln >= 0 ? (o.Bln & BsFlankProtection) != 0 : o.Blokada.Contains("FlankProtection") || o.Blokada.Contains("OchronaBoczna");
            if (I.Secs.TryGetValue(id, out var s))
            {
                s.TrainOcc |= o.Zajety;
                if (pociagowy) { s.LockType = "train"; s.Lock = "simrail"; }
                else if (manewrowy && s.LockType == null) { s.LockType = "shunt"; s.Lock = "simrail"; }
                if (ochronna && s.LockType == null) { s.LockType = "overlap"; s.Lock = "simrail"; }
                if (czasowe) s.TimeRel = 0;
                s.Closed |= zamkniety;
                // numery pociągów z gry; gdzie je pokazać (tory stacyjne, odstępy) – decyduje pulpit (Station.Pip)
                if (o.Pociagi.Length > 0) I.Windows[id] = o.Pociagi.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
            }
            if (o.Rodzaj == "zwrotnica" && I.Sws.TryGetValue(id, out var w) && L.Switches.TryGetValue(id, out var sw))
            {
                // położenie: „Wprost/Straight” = tor prosty; „+” to położenie zasadnicze (prosto, chyba że zasadniczo na „−”)
                bool prosto = o.Zwrotnica is "Wprost" or "Straight";
                bool bok = o.Zwrotnica is "Bok" or "Side";
                if (prosto || bok) { w.Pos = prosto ^ sw.BasicMinus ? "+" : "-"; zwrZPolozeniem.Add(o.Nazwa); }
                // „NoDir” u zwrotnicy, która miała już położenie = iglice w drodze (gra nie podaje celu – przeciwne do dotychczasowego);
                // pulpit rysuje przekładanie (iglica odcięta od obu odgałęzień), początek z poprzedniego meldunku
                else if (o.Zwrotnica is "NoDir" && zwrZPolozeniem.Contains(o.Nazwa))
                    w.Moving = Last?.Sws.GetValueOrDefault(id)?.Moving ?? new MoveInd { Target = w.Pos == "+" ? "-" : "+", Started = I.T };
                w.Rozpr = o.Zwrotnica is "Rozprucie" or "Broken";
                w.Stopped = zastopowany;
                if (pociagowy || manewrowy || ochronna) w.Lock = "simrail";
                w.Flank = boczna;
                w.NoCtrl = o.Basic.Contains("NoControl") || o.Basic.Contains("BrakKontroli");
                // zajętość i przebieg zwrotnicy z gry barwią jej odcinek na pulpicie
                if (sw.Sec != null && I.Secs.TryGetValue(sw.Sec, out var ss))
                {
                    ss.TrainOcc |= o.Zajety;
                    ss.Closed |= zamkniety;
                    if (pociagowy) { ss.LockType = "train"; ss.Lock = "simrail"; }
                    else if (manewrowy && ss.LockType == null) { ss.LockType = "shunt"; ss.Lock = "simrail"; }
                    if (ochronna && ss.LockType == null) { ss.LockType = "overlap"; ss.Lock = "simrail"; }
                }
            }
            if (I.Ders.TryGetValue(id, out var d)) { d.Flank = boczna; if (pociagowy || manewrowy || ochronna) d.Lock = "simrail"; }
            if (o.Rodzaj is "sygnalizator" or "" && I.Sigs.TryGetValue(id, out var g))
            {
                if (pociagowy || manewrowy) wPrzebiegu[id] = (pociagowy ? "P" : "M", czasowe);
                if (zastepczy) g.Aspect = Aspect.Sz;
                else if (sygnal) g.Aspect = manewrowy && !pociagowy ? Aspect.Shunt : Aspect.Train;
                g.Closed = zastopowany;
                g.Flank = boczna;
            }
        }
        BlokadyZGry(I, obiekty);
        PrzejazdyZGry(I, obiekty);
        foreach (var h in odb.PobierzHistorie())
        {
            bool odrz = (h.S1 + " " + h.S2).Contains("odrzuc", StringComparison.OrdinalIgnoreCase) || (h.S1 + " " + h.S2).Contains("reject", StringComparison.OrdinalIgnoreCase);
            Zdarz(odrz ? "Odrzucone" : "Zdarzenie", h.Polecenie, $"gra – historia poleceń: {h.Polecenie} | {h.S1} | {h.S2}");
        }
        // ten sam obiekt gry narysowany na dwóch obrazach („X~2” bez własnego ID gry) – stan jak X
        foreach (var k in I.Sws.Keys.Where(k => k.Contains('~') && !doSimRail.ContainsKey(k)).ToList()) if (I.Sws.TryGetValue(Baza(k), out var w0)) I.Sws[k] = w0;
        foreach (var k in I.Sigs.Keys.Where(k => k.Contains('~') && !doSimRail.ContainsKey(k)).ToList()) if (I.Sigs.TryGetValue(Baza(k), out var g0)) I.Sigs[k] = g0;
        foreach (var k in I.Ders.Keys.Where(k => k.Contains('~') && !doSimRail.ContainsKey(k)).ToList()) if (I.Ders.TryGetValue(Baza(k), out var d0)) I.Ders[k] = d0;
        DrogaZGry(I);
        PrzebiegiOdSemaforow(I, wPrzebiegu);
        if (pending != null && I.T > pending.Until) { Zdarz("Polecenie", Station.Lbl(pending.Arg), $"Upłynął czas na wykonanie polecenia specjalnego {pending.Line}"); pending = null; }
        I.Pending = pending;
        foreach (var o in obiekty)
        {
            if (poprzZaj.TryGetValue(o.Nazwa, out var b) && b != o.Zajety && doSuk.TryGetValue(o.Nazwa, out var id) && L.Sections.ContainsKey(id))
                Zdarz("Zdarzenie", Station.Lbl(id), (o.Zajety ? "zajęcie odcinka" : "zwolnienie odcinka") + (o.Pociagi.Length > 0 ? $" – pociąg {o.Pociagi}" : ""));
            poprzZaj[o.Nazwa] = o.Zajety;
        }
        return Last = I;
    }

    /// <summary>
    /// Gra podaje przebieg tylko per odcinek. W odcinku zwrotnicowym (grupa rozjazdów) pulpit barwiłby wtedy wszystkie tory,
    /// także odgałęzienia, którymi przebieg nie biegnie (np. 23 → C106 przy przebiegu przez 20). Droga: tory utwierdzonych
    /// odcinków połączone przez czynne odgałęzienia zwrotnic; zostają części sięgające innego utwierdzonego odcinka. Odcinek,
    /// z którego nic nie zostało (przebieg w jednym odcinku, położenie jeszcze nieznane) – cały, jak dawniej.
    /// Wynik jako przebieg „simrail” w meldunku – pulpit rysuje wtedy tylko jego tory (pozostałe tory odcinka szare).
    /// </summary>
    void DrogaZGry(Indication I)
    {
        // przebieg: odcinek oderwany od drogi (np. iz9/13 – w układzie tylko tor 13 → C102, a gra utwierdza go za zwrotnicę 9
        // przy przebiegu C103 → A) nie jest drogą; cały odcinek tylko, gdy przebieg nie ma żadnej drogi wieloodcinkowej
        var droga = Polaczone(I, s => s.Lock == "simrail", bezDrogiTylkoGdyPusta: true);
        if (droga != null) I.Routes["simrail"] = new RouteInd { Start = "", End = "", Type = "P", State = "set", Edges = droga };
        // zajętość: tak samo tory połączone z zajętymi sąsiadami (iz20/23: pociąg C104 → 20 nie barwi 23 → C106); odcinek
        // samotny (pociąg w całości w nim) – cały, chyba że styka się z drogą zajętości, a odgałęzienie zwrotnicy jest nieczynne
        var zaj = Polaczone(I, s => s.TrainOcc, bezDrogiTylkoGdyPusta: false);
        if (zaj != null) I.Routes[ZajetoscKlucz] = new RouteInd { Start = "", End = "", Type = "Z", State = "set", Edges = zaj };
    }

    /// <summary>Klucz w <see cref="Indication.Routes"/> z torami zajętymi (pulpit barwi zajętość tylko na nich).</summary>
    public const string ZajetoscKlucz = "simrail-zajetosc";

    List<string> Polaczone(Indication I, Func<SecInd, bool> wybrany, bool bezDrogiTylkoGdyPusta)
    {
        var tory = L.Edges.Values.Where(e => I.Secs.TryGetValue(e.Sec ?? "", out var s) && wybrany(s)).Select(e => e.Id).ToList();
        if (tory.Count == 0) return null;
        var wybrane = new HashSet<string>(tory);
        // mosty: tory odcinków bez obwodu gry (np. _a73eae2c między it103 a it103a przy E103) – gra nigdy ich nie utwierdza ani
        // nie zajmuje, więc bez nich droga rwałaby się na nich; łączą części, a na pulpicie są barwione, gdy leżą między nimi
        // Także tory odcinków z obiektem gry, który niczego nie melduje (Koluszki l1_kol_iz127/129: rysunek toru między 97 a
        // zwrotnicą 127, niewybieralny, gra nie utwierdza go ani nie zajmuje – obwód to t39653 i zwrotnice 127/129) – ale tylko
        // tor wolny, bez utwierdzenia, bez semafora na końcach (wolny odcinek między dwoma przebiegami nie staje się drogą)
        bool Martwy(Edge e) => I.Secs.TryGetValue(e.Sec, out var s) && s.Lock == null && !s.TrainOcc && s.Fault == null
                               && L.Nodes.TryGetValue(e.A, out var na) && na.Signals.Count == 0 && L.Nodes.TryGetValue(e.B, out var nb) && nb.Signals.Count == 0;
        var mosty = L.Edges.Values.Where(e => !wybrane.Contains(e.Id) && !string.IsNullOrEmpty(e.Sec) && (!doSimRail.ContainsKey(e.Sec) || Martwy(e))).Select(e => e.Id).ToHashSet();
        var zbior = new HashSet<string>(wybrane.Concat(mosty));
        var rodzic = zbior.ToDictionary(t => t, t => t);
        string Korzen(string t) { while (rodzic[t] != t) t = rodzic[t] = rodzic[rodzic[t]]; return t; }
        // tor rozcięty w narożniku przez zob_do_suk.py (węzeł „X_r” w tym samym punkcie co „X”, np. Koluszki 4s przy IISpG4,
        // 3s przy IISpS3) – dla drogi i zajętości to jeden węzeł
        string Wezel(string id) => id.EndsWith("_r", StringComparison.Ordinal) && L.Nodes.ContainsKey(id[..^2]) ? id[..^2] : id;
        IEnumerable<string> TorWezla(string id) => L.Nodes.TryGetValue(id, out var nd) ? nd.Edges.Concat(L.Nodes.TryGetValue(id + "_r", out var nr) ? nr.Edges : Enumerable.Empty<string>()) : Enumerable.Empty<string>();
        foreach (var n in L.Nodes.Values)
        {
            if (Wezel(n.Id) != n.Id) continue;
            var przy = TorWezla(n.Id).Where(zbior.Contains).Distinct().ToList();
            if (przy.Count < 2) continue;
            SwitchDef sw = n.Switch != null ? L.Switches.GetValueOrDefault(n.Switch) : null;
            string czynne = sw == null ? null : (I.Sws.GetValueOrDefault(sw.Num)?.Pos ?? "+") == "+" ? sw.Plus : sw.Minus;
            for (int i = 0; i < przy.Count; i++)
                for (int j = i + 1; j < przy.Count; j++)
                {
                    string a = przy[i], b = przy[j];
                    // w zwrotnicy: ostrze łączy się tylko z czynnym odgałęzieniem, odgałęzienia ze sobą – nigdy
                    if (sw != null)
                    {
                        bool zwA = a == sw.Tip || a == sw.Plus || a == sw.Minus, zwB = b == sw.Tip || b == sw.Plus || b == sw.Minus;
                        // ostrze z odgałęzieniem-mostem (tor bez stanu z gry, np. Koluszki t39655 między 97 a 127): położenie nie decyduje –
                        // gra podaje dla takich zwrotnic NoDir albo położenie wg własnej geometrii (127: isUp, inna strona niż w rysunku);
                        // most i tak zostaje tylko, gdy oba jego końce stykają się z drogą (obcinanie niżej)
                        bool mostOdgalezienie = a == sw.Tip && mosty.Contains(b) || b == sw.Tip && mosty.Contains(a);
                        if (zwA && zwB && !mostOdgalezienie && !(a == sw.Tip && b == czynne || b == sw.Tip && a == czynne)) continue;
                    }
                    rodzic[Korzen(a)] = Korzen(b);
                }
        }
        // most zostaje tylko między torami: oba końce stykają się z innym torem tej samej części (obcinanie „wiszących” mostów)
        var most = new HashSet<string>(mosty);
        for (bool zmiana = true; zmiana;)
        {
            zmiana = false;
            foreach (var m in most.ToList())
            {
                var e = L.Edges[m];
                bool Styk(string wezel) => TorWezla(Wezel(wezel)).Any(x => x != m && (wybrane.Contains(x) || most.Contains(x)) && Korzen(x) == Korzen(m));
                if (!Styk(e.A) || !Styk(e.B)) { most.Remove(m); zmiana = true; }
            }
        }
        var odcinkiCzesci = tory.GroupBy(Korzen).ToDictionary(g => g.Key, g => g.Select(t => L.Edges[t].Sec).Distinct().Count());
        var droga = tory.Where(t => odcinkiCzesci[Korzen(t)] >= 2).ToList();
        droga.AddRange(most.Where(m => odcinkiCzesci.TryGetValue(Korzen(m), out var k) && k >= 2));
        var zDroga = droga.Select(t => L.Edges[t].Sec).ToHashSet();
        var wDrodze = droga.ToHashSet();
        var wezlyDrogi = droga.SelectMany(t => new[] { Wezel(L.Edges[t].A), Wezel(L.Edges[t].B) }).ToHashSet();
        foreach (var grupa in tory.Where(t => !zDroga.Contains(L.Edges[t].Sec)).GroupBy(Korzen))
        {
            if (bezDrogiTylkoGdyPusta && wDrodze.Count > 0) continue;
            // odgałęzienie przy drodze (wspólny węzeł, ale zwrotnica w nim nie prowadzi na ten tor) – nie zajęte
            if (!bezDrogiTylkoGdyPusta && grupa.Any(t => wezlyDrogi.Contains(Wezel(L.Edges[t].A)) || wezlyDrogi.Contains(Wezel(L.Edges[t].B)))) continue;
            droga.AddRange(grupa);
        }
        return droga;
    }

    /// <summary>
    /// Przebiegi od semaforów (do przycisków ZD / ZDP / ZCZ / ZDM w panelu): semafor, przy którym gra pokazuje przebieg pociągowy
    /// albo manewrowy. Koniec – z ostatniego przebiegu nastawionego z SUK od tego semafora; przebieg nastawiony w grze ma koniec nieznany (null).
    /// </summary>
    void PrzebiegiOdSemaforow(Indication I, Dictionary<string, (string Typ, bool Czasowe)> wPrzebiegu)
    {
        // wpis znika, gdy semafor nie ma już przebiegu (po 30 s od wysłania – gra buduje przebieg chwilę)
        foreach (var k in nastawioneZSuk.Where(x => !wPrzebiegu.ContainsKey(x.Key) && I.T - x.Value.Od > 30).Select(x => x.Key).ToList()) nastawioneZSuk.Remove(k);
        foreach (var (sg, (typ, czasowe)) in wPrzebiegu)
        {
            var z = nastawioneZSuk.GetValueOrDefault(sg);
            I.Routes["simrail:" + sg] = new RouteInd { Start = sg, End = z.Typ == typ ? z.Koniec : null, Type = typ, State = "set", TimeRel = czasowe };
        }
    }

    /// <summary>Polecenie specjalne zainicjowane z tego pulpitu (ZDP, SZI) – w meldunku jako Pending, wykonanie po 5 s (ZDPP / SZ / NSZ), OPS – odwołanie.</summary>
    PendingInd pending;
    /// <summary>Semafor SUK → koniec (id SUK) i rodzaj ostatniego przebiegu nastawionego z pulpitu SUK.</summary>
    readonly Dictionary<string, (string Koniec, string Typ, double Od)> nastawioneZSuk = new(StringComparer.Ordinal);
    double Teraz => Last?.T ?? 0;

    static string Zegar(string czasGry) => czasGry != null && czasGry.Length >= 19 ? czasGry[11..19] : "--:--:--";

    // wartości wyliczeń gry (Il2Cpp.RouteState, BlockingState, LineBlockState, TrafficControl.CrossingState)
    const int RsConfirmedInRoute = 2, RsConfirmed = 8, RsTimeRelease = 16, RsTrainRoute = 32, RsShuntingRoute = 64, RsSignal = 128, RsSubsidiary = 256;
    const int BsStopped = 8, BsGlobalStopped = 16, BsOverlap = 64, BsFlankProtection = 128, BsIndividualClose = 256;
    const int LbArrivalRequest = 1, LbDepartureRequest = 2, LbArrivalApproved = 4, LbDepartureApproved = 8, LbDepartureSignal = 16,
              LbTrainOnRoute = 32, LbReleasing = 2048, LbBroken = 65536, LbChanging = 131072, LbPermissionGranted = 262144;
    const int CsUnknown = 1, CsOpen = 2, CsSwitch = 4, CsClose = 8, CsHold = 32, CsBroken = 128;

    // ------------------------------------------------------------------ blokady liniowe z gry

    readonly Dictionary<string, string> blokadaGry = new(StringComparer.Ordinal);
    readonly Dictionary<string, string> przejazdGry = new(StringComparer.Ordinal);
    readonly Dictionary<string, int> stanBlokady = new(StringComparer.Ordinal);

    /// <summary>
    /// Nazwa blokady w grze dla szlaku SUK: wpis mapy wprost na szlak albo nazwa semafora wyjazdowego na szlak z przyrostkiem „bl”
    /// (SimRail: semafor „1803_ko_a” → blokada „1803_ko_abl”; szlak SUK „szA”, na pulpicie gry „2R”).
    /// </summary>
    string BlokadaGry(BlockDef b, ICollection<string> nazwyBlokad)
    {
        if (blokadaGry.TryGetValue(b.Id, out var n)) return n;
        n = doSimRail.GetValueOrDefault(b.Id)?.FirstOrDefault(nazwyBlokad.Contains);
        foreach (var en in b.Ends)
        {
            if (n != null) break;
            if (en.EntrySig == null || !doSimRail.TryGetValue(en.EntrySig, out var sygn)) continue;
            n = sygn.Select(x => x + "bl").FirstOrDefault(nazwyBlokad.Contains);
        }
        if (n != null) blokadaGry[b.Id] = n;
        return n;
    }

    /// <summary>
    /// Stan blokady z gry (LineBlockState) na meldunek szlaku: kierunek z „DepartureApproved” (WYJAZD) / „ArrivalApproved” (PRZYJAZD),
    /// żądanie z „DepartureRequest” (nasze) / „ArrivalRequest” (od sąsiada), pociąg na szlaku, zwalnianie kierunku.
    /// </summary>
    void BlokadyZGry(Indication I, List<SimRailOdbiornik.Obiekt> obiekty)
    {
        var blokady = new Dictionary<string, SimRailOdbiornik.Obiekt>(StringComparer.OrdinalIgnoreCase);
        foreach (var o in obiekty) if (o.Lb >= 0) blokady[o.Nazwa] = o;
        if (blokady.Count == 0) return;
        var nazwy = new HashSet<string>(blokady.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (var b in L.Blocks.Values)
        {
            if (b.Ends.Count == 0 || !I.Blocks.TryGetValue(b.Id, out var bi)) continue;
            var ng = BlokadaGry(b, nazwy); if (ng == null || !blokady.TryGetValue(ng, out var o)) continue;
            int lb = o.Lb, outw = b.Ends[0].Outward;
            if (o.GrotBlokady is int grot)
            {
                // mod ≥ 0.9.4: grot strzałki na pulpicie gry (+1 w prawo / −1 w lewo). Rysunek Koluszek pochodzi z pulpitu gry,
                // więc strzałka SUK ma wskazywać tę samą stronę: grot na zewnątrz stacji (VOut) = kierunek WYJAZD.
                int vout = b.Ends[0].VOut != 0 ? b.Ends[0].VOut : outw;
                bi.Dir = grot == 0 ? 0 : grot == vout ? outw : -outw;
                // miganie grota – żądanie / zmiana / zwalnianie kierunku
                bi.Req = o.MigaBlokada && bi.Dir != 0 ? new ReqInd { EndIdx = bi.Dir == outw ? 0 : 1 } : null;
            }
            else
            {
                bi.Dir = (lb & (LbDepartureApproved | LbDepartureSignal)) != 0 ? outw : (lb & LbArrivalApproved) != 0 ? -outw : 0;
                bi.Req = (lb & LbDepartureRequest) != 0 ? new ReqInd { EndIdx = 0 } : (lb & LbArrivalRequest) != 0 ? new ReqInd { EndIdx = 1 } : null;
            }
            bi.TrainOn = (lb & LbTrainOnRoute) != 0;
            if ((lb & LbReleasing) != 0) bi.ReleaseUntil = I.T + 1;
            bi.Info = $"gra {ng}: {OpisBlokady(lb)}{(o.WidokBlokady.Length > 0 ? $", strzałka w grze {o.WidokBlokady}" : "")}";
            if (stanBlokady.TryGetValue(b.Id, out var poprz) && poprz != lb) Zdarz("Zdarzenie", b.Label ?? b.Id, $"blokada liniowa (gra): {OpisBlokady(lb)}");
            stanBlokady[b.Id] = lb;
        }
    }

    static string OpisBlokady(int lb)
    {
        if (lb == 0) return "stan zasadniczy";
        var n = new List<string>();
        void F(int bit, string t) { if ((lb & bit) != 0) n.Add(t); }
        F(LbDepartureApproved, "kierunek WYJAZD"); F(LbArrivalApproved, "kierunek PRZYJAZD"); F(LbDepartureRequest, "nasze żądanie kierunku");
        F(LbArrivalRequest, "żądanie kierunku od sąsiada"); F(LbDepartureSignal, "sygnał wyjazdowy"); F(LbTrainOnRoute, "pociąg na szlaku");
        F(LbReleasing, "zwalnianie kierunku"); F(LbChanging, "zmiana kierunku"); F(LbPermissionGranted, "pozwolenie udzielone"); F(LbBroken, "USZKODZENIE");
        return n.Count > 0 ? string.Join(", ", n) + $" ({lb})" : $"stan {lb}";
    }

    // ------------------------------------------------------------------ przejazdy z gry

    /// <summary>Przejazd z rysunku (nazwa = kilometr, np. „103.742”) ↔ przejazd w grze („l1_103.742_a”): najbliższy kilometr (≤ 0,5 km).</summary>
    string PrzejazdGry(CrossingDef c, List<SimRailOdbiornik.Obiekt> przejazdy)
    {
        if (c.Name == null) return null;
        if (przejazdGry.TryGetValue(c.Name, out var n)) return n;
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        if (!double.TryParse(c.Name.Replace(',', '.'), System.Globalization.NumberStyles.Float, ci, out var km)) return null;
        static double Km(string nazwa, IFormatProvider ci)
        {
            var m = System.Text.RegularExpressions.Regex.Match(nazwa, @"(\d+\.\d+)");
            return m.Success ? double.Parse(m.Value, ci) : double.NaN;
        }
        var best = przejazdy.Select(o => (o.Nazwa, d: Math.Abs(Km(o.Nazwa, ci) - km))).Where(x => x.d <= 0.5).OrderBy(x => x.d).FirstOrDefault();
        if (best.Nazwa != null) przejazdGry[c.Name] = best.Nazwa;
        return best.Nazwa;
    }

    void PrzejazdyZGry(Indication I, List<SimRailOdbiornik.Obiekt> obiekty)
    {
        // stan liczbowy (Pz > 0) albo – mod ≥ 0.9.3 – sam podgląd barw symbolu przejazdu w grze (stan „nieznany”, polecenia dostępne)
        var przejazdy = obiekty.Where(o => o.Pz > 0 || o.WidokPrzejazdu.Length > 0).ToList();
        if (przejazdy.Count == 0) return;
        foreach (var c in L.Crossings)
        {
            var ng = PrzejazdGry(c, przejazdy); if (ng == null) continue;
            var o = przejazdy.FirstOrDefault(x => string.Equals(x.Nazwa, ng, StringComparison.OrdinalIgnoreCase)); if (o == null) continue;
            int pz = o.Pz;
            if (pz <= 0)
            {
                I.Crossings[c.Name] = new CrossInd { Stan = "nieznany", Info = $"gra {ng}: {o.WidokPrzejazdu}" };
                if (widokPrzejazdu.TryGetValue(c.Name, out var pw) && pw != o.WidokPrzejazdu) Zdarz("Zdarzenie", "przejazd " + c.Name, $"przejazd (gra) – symbol: {o.WidokPrzejazdu}");
                widokPrzejazdu[c.Name] = o.WidokPrzejazdu;
                continue;
            }
            string stan = (pz & CsBroken) != 0 ? "uszkodzony"
                        : (pz & CsSwitch) != 0 ? ((pz & CsClose) != 0 ? "otwieranie" : "zamykanie")
                        : (pz & (CsClose | CsHold)) != 0 ? "zamkniety"
                        : (pz & CsOpen) != 0 ? "otwarty" : null;
            I.Crossings[c.Name] = new CrossInd { Stan = stan, Info = $"gra {ng}: {OpisPrzejazdu(pz)}" };
            if (stanPrzejazdu.TryGetValue(c.Name, out var poprz) && poprz != pz) Zdarz("Zdarzenie", "przejazd " + c.Name, $"przejazd (gra): {OpisPrzejazdu(pz)}");
            stanPrzejazdu[c.Name] = pz;
        }
    }
    readonly Dictionary<string, int> stanPrzejazdu = new(StringComparer.Ordinal);
    readonly Dictionary<string, string> widokPrzejazdu = new(StringComparer.Ordinal);

    static string OpisPrzejazdu(int pz)
    {
        var n = new List<string>();
        void F(int bit, string t) { if ((pz & bit) != 0) n.Add(t); }
        F(CsOpen, "otwarty"); F(CsSwitch, "przestawianie rogatek"); F(CsClose, "zamknięty"); F(CsHold, "podtrzymanie zamknięcia"); F(CsBroken, "USZKODZENIE"); F(CsUnknown, "stan nieznany");
        return n.Count > 0 ? string.Join(", ", n) + $" ({pz})" : $"stan {pz}";
    }


    /// <summary>
    /// Nazwa SimRail dla obiektu SUK. Jeden identyfikator SUK może mieć kilka nazw z gry (np. zwrotnica 59 i tarcza 59 → „KO:59”
    /// w różnych słownikach układu) – wybór wg rodzaju obiektu ze stanów gry, a bez stanów wg przedrostka (zwrotnice „z…”).
    /// </summary>
    // ------------------------------------------------------------------ katalog przebiegów gry
    /// <summary>Przebiegi dopisane z katalogu gry: Id RouteDef → nazwy gry (początek, koniec, zwrotnice „z1985=Plus”) do wysłania bez tłumaczenia.</summary>
    readonly Dictionary<string, (string Start, string Koniec, List<string> Zw)> zKatalogu = new(StringComparer.Ordinal);
    DateTime katalogProba; bool katalogGotowy; volatile bool katalogTrwa;

    /// <summary>
    /// Tablica przebiegów pulpitu zdalnego uzupełniana z katalogu gry (mod: „RouteCatalog”). Tablica z zobrazowania (Simulator)
    /// bywa niepełna – odtworzona topologia gubi połączenia; gra jest źródłem prawdy, więc przebieg, który gra ma, a SUK nie,
    /// dopisujemy. Próba co 15 s do skutku (katalog jest dopiero, gdy pulpit gry pobierze przebiegi posterunku).
    /// </summary>
    void PobierzKatalog()
    {
        if (katalogGotowy || katalogTrwa || (DateTime.Now - katalogProba).TotalSeconds < 15) return;
        katalogProba = DateTime.Now; katalogTrwa = true;
        Task.Run(() =>
        {
            try
            {
                var (ok, msg) = odb.WyslijKatalogPrzebiegow();
                if (!ok || !File.Exists(msg)) return;
                var tekst = File.ReadAllText(msg);
                ui.BeginInvoke(() => { try { ScalKatalog(tekst, msg); } catch (Exception ex) { Zdarz("Zdarzenie", "", "Katalog przebiegów gry – błąd: " + ex.Message); } });
            }
            catch { }
            finally { katalogTrwa = false; }
        });
    }

    /// <summary>Plik zapamiętanego katalogu przebiegów gry obok układu: „koluszki_zob.json” → „koluszki_zob.przebiegi_gry.json”.</summary>
    string KatalogZapamietany() => string.IsNullOrEmpty(uklad.SourcePath) ? null : Path.ChangeExtension(uklad.SourcePath, null) + ".przebiegi_gry.json";

    void ScalKatalog(string json, string plik, bool zPamieci = false)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        // ponowne scalenie (świeży katalog po zapamiętanym): bez podwójnych przebiegów „gra:”
        foreach (var lista in RouteTable.Values) lista.RemoveAll(r => (r.Id ?? "").StartsWith("gra:"));
        zKatalogu.Clear();
        if (!zPamieci && KatalogZapamietany() is string kz)
            try { File.WriteAllText(kz, json); } catch { }
        var k = doc.RootElement;
        var aliasy = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (k.TryGetProperty("aliasy", out var al))
            foreach (var p in al.EnumerateObject()) aliasy[p.Name] = p.Value.EnumerateArray().Select(x => x.GetString()).Where(x => x != null).ToList();
        string Suk(string n)
        {
            if (string.IsNullOrEmpty(n)) return null;
            if (doSuk.TryGetValue(n, out var id)) return id;
            foreach (var a in aliasy.GetValueOrDefault(n) ?? new()) if (doSuk.TryGetValue(a, out id)) return id;
            return null;
        }
        int wszystkie = 0, dodane = 0; var bezMapy = new SortedSet<string>(StringComparer.Ordinal); var raport = new List<string>();
        var wariantyGry = new Dictionary<(string, string, string), HashSet<string>>(); var niepelne = new HashSet<(string, string, string)>();
        static string Kanon(IEnumerable<(string Num, string Pos)> sw) => string.Join(" ", sw.Select(q => q.Num + q.Pos).OrderBy(x => x, StringComparer.Ordinal));
        var nowe = new List<RouteDef>();
        foreach (var r in k.GetProperty("przebiegi").EnumerateArray())
        {
            string S(string n) => r.TryGetProperty(n, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String ? v.GetString() ?? "" : "";
            var gStart = S("start"); var gKoniec = S("koniec"); var typ = S("typ") == "M" ? "M" : "P";
            var s = Suk(gStart); if (s == null || !L.Signals.ContainsKey(s)) continue;   // inny posterunek albo semafor bez mapy
            wszystkie++;
            // wyjazd na szlak: koniec w grze „…kps” (np. 1803_KO_R1kps) – szlak, którego semafor wjazdowy to „…” (1803_KO_R1 → KO:R1)
            string e = null, rodzajKonca = "sig", wezel = null;
            if (gKoniec.EndsWith("kps", StringComparison.OrdinalIgnoreCase))
            {
                var wj = Suk(gKoniec[..^3]);
                var bl = wj == null ? null : L.Blocks.Values.FirstOrDefault(b => b.Ends.Any(x => x.EntrySig == wj));
                if (bl != null) { e = bl.Id; rodzajKonca = typ == "M" ? "szlakM" : "szlak"; wezel = bl.Ends.First(x => x.EntrySig == wj).Boundary; }
            }
            if (e == null)
            {
                e = Suk(gKoniec);
                if (e == null || !L.Signals.ContainsKey(e)) { bezMapy.Add($"{typ} {Station.Lbl(s)} → {gKoniec} (koniec bez semafora ani szlaku w mapie)"); continue; }
                wezel = L.Signals[e].Node;
            }
            var zwGry = r.TryGetProperty("zw", out var za) ? za.EnumerateArray().Select(x => x.GetString()).Where(x => x != null).ToList() : new();
            var sw = new List<(string, string)>(); bool pelne = true;
            foreach (var z in zwGry)
            {
                var q = z.Split('='); var num = q.Length == 2 ? Suk(q[0]) : null;
                if (num == null || !L.Switches.ContainsKey(num)) { pelne = false; continue; }
                sw.Add((num, q[1].Equals("Plus", StringComparison.OrdinalIgnoreCase) ? "+" : "-"));
            }
            // warianty gry tej pary (położenia zwrotnic) – do odrzucenia wariantów SUK, których gra nie zna; para ze zwrotnicą bez mapy – bez odrzucania
            var kan = Kanon(sw);
            if (!wariantyGry.TryGetValue((s, typ, e), out var wg)) wariantyGry[(s, typ, e)] = wg = new(StringComparer.Ordinal);
            wg.Add(kan);
            if (!pelne || zwGry.Any(z => !z.Contains('='))) niepelne.Add((s, typ, e));
            // ten semafor na każdym obrazie pulpitu („X~2” – ten sam obiekt gry)
            foreach (var st in L.Signals.Keys.Where(x => x == s || Baza(x) == s).ToList())
            {
                var lista = RouteTable.GetValueOrDefault(st);
                // ten sam przebieg (te same położenia zwrotnic) już jest – w SUK albo wcześniej z katalogu (gra podaje go kilka razy)
                if (lista != null && lista.Any(x => x.Type == typ && x.End == e && Kanon(x.Sw) == kan)) continue;
                if (lista == null) RouteTable[st] = lista = new();
                var rd = new RouteDef
                {
                    Id = $"gra:{S("id")}:{st}", Start = st, End = e, EndKind = rodzajKonca, Type = typ, EndNode = wezel, Sw = sw,
                    Minus = sw.Count(x => x.Item2 == "-"), Variant = lista.Count(x => x.Type == typ && x.End == e),
                };
                lista.Add(rd); nowe.Add(rd);
                zKatalogu[rd.Id] = (gStart, gKoniec, zwGry);
                if (st == s) { dodane++; raport.Add($"{typ} {Station.Lbl(s)} → {Station.Lbl(e)}  [{string.Join(" ", zwGry)}]{(pelne ? "" : " (część zwrotnic bez mapy)")}"); }
            }
        }
        // warianty SUK, których gra nie ma (para jest w grze, położenia zwrotnic inne) – gra by je odrzuciła; z tablicy pulpitu zdalnego znikają
        int usuniete = 0;
        foreach (var (st, lista) in RouteTable)
            usuniete += lista.RemoveAll(r =>
            {
                if ((r.Id ?? "").StartsWith("gra:")) return false;
                var key = (Baza(st), r.Type, r.End);
                if (!wariantyGry.TryGetValue(key, out var wg) || niepelne.Contains(key) || wg.Contains(Kanon(r.Sw))) return false;
                raport.Add($"usunięty (gra nie ma tego wariantu): {r.Type} {Station.Lbl(st)} → {Station.Lbl(r.End)}  [{r.SwText}]");
                return true;
            });
        // numery wariantów jak w SUK: zasadniczy (0) – najmniej zwrotnic na „−”, potem najkrótszy; tylko grupy z przebiegami gry
        foreach (var lista in RouteTable.Values)
            foreach (var g in lista.GroupBy(r => (r.Type, r.End)).Where(g => g.Any(r => (r.Id ?? "").StartsWith("gra:"))))
            {
                int v = 0;
                foreach (var r in g.OrderBy(r => r.Minus).ThenBy(r => r.Sw.Count).ThenBy(r => Kanon(r.Sw), StringComparer.Ordinal)) r.Variant = v++;
            }
        if (usuniete > 0) Zdarz("Zdarzenie", "", $"Katalog przebiegów gry: {usuniete} wariantów SUK, których gra nie ma, usunięto z tablicy pulpitu");
        if (zPamieci)
        {
            // świeży katalog z gry i tak zostanie pobrany (katalogGotowy bez zmian) – tu tylko meldunek
            Zdarz("Zdarzenie", "", $"Zapamiętany katalog przebiegów gry ({File.GetLastWriteTime(plik):yyyy-MM-dd HH:mm}): dopisano {dodane} przebiegów; świeży katalog – po połączeniu z grą");
            return;
        }
        katalogGotowy = true;
        try
        {
            File.WriteAllLines(Path.ChangeExtension(plik, ".dodane.txt"),
                new[] { $"Katalog gry: {wszystkie} przebiegów od semaforów tego pulpitu; dopisano do tablicy SUK {dodane} brakujących.", "" }
                .Concat(raport.OrderBy(x => x, StringComparer.Ordinal)).Append("").Append("Pominięte:").Concat(bezMapy));
        }
        catch { }
        Zdarz("Zdarzenie", "", $"Katalog przebiegów gry: {wszystkie} przebiegów, dopisano {dodane} brakujących w tablicy SUK" +
                               (bezMapy.Count > 0 ? $", pominięto {bezMapy.Count} (koniec bez mapy)" : "") + $" – lista: {Path.ChangeExtension(plik, ".dodane.txt")}");
    }

    string NazwaGry(string id, string rodzaj)
    {
        if (!doSimRail.ContainsKey(id)) id = Baza(id);   // „X~2” – ten sam obiekt gry co X (drugi obraz)
        if (!doSimRail.TryGetValue(id, out var l) || l.Count == 0) return null;
        if (l.Count == 1) return l[0];
        var rodzaje = odb.Obiekty().ToDictionary(o => o.Nazwa, o => o.Rodzaj, StringComparer.OrdinalIgnoreCase);
        var r = l.FirstOrDefault(n => rodzaje.TryGetValue(n, out var rr) && rr == rodzaj);
        if (r != null) return r;
        bool Zwr(string n) => n.StartsWith("z", StringComparison.OrdinalIgnoreCase) && n.Length > 1 && char.IsDigit(n[1]);
        return rodzaj == "zwrotnica" ? l.FirstOrDefault(Zwr) ?? l[0] : l.FirstOrDefault(n => !Zwr(n)) ?? l[0];
    }
    /// <summary>Ten sam obiekt gry na drugim obrazie pulpitu ma w SUK nazwę z „~2” (zob_do_suk.py) – baza bez przyrostka.</summary>
    static string Baza(string id) { var i = id?.IndexOf('~') ?? -1; return i > 0 ? id[..i] : id; }
    string RodzajSuk(string id) => L.Switches.ContainsKey(id) ? "zwrotnica" : L.Signals.ContainsKey(id) ? "sygnalizator"
                                 : L.Derailers.ContainsKey(id) ? "wykolejnica" : "odcinek";

    // ------------------------------------------------------------------ polecenia do gry

    static readonly Dictionary<string, string> NaGre = new()
    {
        ["PLUS"] = "Plus", ["MINUS"] = "Minus", ["STOP"] = "Stop", ["OSTOP"] = "RevokeStop", ["STOJ"] = "StopSignal",
        ["ZMK"] = "Close", ["OZMK"] = "RevokeClose",
    };

    /// <summary>Polecenia przejazdu (spoza słownika poleceń LCS – tylko pulpit zdalny SimRail): ZAMK – zamknięcie, OTW – otwarcie.</summary>
    static readonly Dictionary<string, string> PrzejazdNaGre = new() { ["ZAMK"] = "CloseCrossing", ["OTW"] = "OpenCrossing", ["AZAMK"] = "EmergencyCloseCrossing" };

    public CmdResult Exec(string line)
    {
        var t = (line ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        var kp = t.Select(x => SystemNadrzedny.Fold(x)).LastOrDefault(PrzejazdNaGre.ContainsKey);
        if (kp != null) return Przejazd(t.Where(x => !PrzejazdNaGre.ContainsKey(SystemNadrzedny.Fold(x))).ToList(), kp);
        int ik = SystemNadrzedny.IndeksKodu(t, L);
        if (ik < 0) return new CmdResult(false, "Nieznane polecenie");
        var kod = SystemNadrzedny.KodPolecenia(t[ik]);
        var obiekty = t.Where((x, i) => i != ik).ToList();
        if (kod is "POCIAG" or "MANEWR") return Przebieg(obiekty, kod == "POCIAG" ? "P" : "M");
        if (kod is "WBL" or "OWBL" or "PZK" or "ZWBL" or "AZK" or "ZKB" or "OZKB" or "PZM" || (kod is "STOP" or "OSTOP" && obiekty.Any(o => L.Blocks.ContainsKey(o))))
            return Blokada(obiekty, kod);
        if (kod is "SZ" or "SZI" or "NSZ" or "OSZ") return SygnalZastepczy(obiekty, kod, line);
        if (kod is "ZD" or "ZDM" or "ZDP" or "ZDPP" or "ZCZ" or "OZCZ" or "OPS") return ZwolnieniePrzebiegu(obiekty, kod, line);
        if (!NaGre.TryGetValue(kod, out var order))
            return new CmdResult(false, $"Pulpit zdalny SimRail: polecenie {kod} jeszcze nieobsługiwane (Plus, Minus, Stop, oStop, Stój, ZMK, oZMK, Pociąg, Manewr, ZD, ZDM, ZDP, ZDPP, ZCZ, OPS, SZ, SZI, NSZ, oSZ, blokada: Wbl, oWbl, Pzk, Zwbl, AZK, ZKB, oZKB, PZM, przejazd: ZAMK, OTW).");
        if (obiekty.Count == 0) return new CmdResult(false, "Brak obiektu polecenia");
        var wyniki = new List<string>(); bool wszystkieOk = true;
        foreach (var ob in obiekty)
        {
            var id = Znajdz(ob, kod);
            if (id == null) { wszystkieOk = false; wyniki.Add($"{ob}: nie ma takiego obiektu na pulpicie"); continue; }
            var ng = NazwaGry(id, RodzajSuk(id));
            if (ng == null) { wszystkieOk = false; wyniki.Add($"{ob}: brak w mapie SimRail"); continue; }
            var (ok, msg) = odb.WyslijPolecenie(ng, order);
            wszystkieOk &= ok; wyniki.Add($"{ob}: {msg}");
            Zdarz("Polecenie", ob, $"{kod} → gra: {(ok ? "wysłano" : "odrzucono")} ({msg})");
        }
        return new CmdResult(wszystkieOk, string.Join("; ", wyniki));
    }

    /// <summary>
    /// Sygnał zastępczy w grze (polecenie specjalne – potwierdzenie polecenia daje pulpit SUK): SZ – inicjacja i po 1,5 s
    /// potwierdzenie (SubsidiarySignalInit / Confirm), NSZ – to samo z W24, SZI – sama inicjacja, oSZ – odwołanie.
    /// </summary>
    CmdResult SygnalZastepczy(List<string> obiekty, string kod, string line)
    {
        if (obiekty.Count == 0) return new CmdResult(false, "Brak sygnalizatora w poleceniu");
        var wyniki = new List<string>(); bool wszystkieOk = true;
        foreach (var ob in obiekty)
        {
            var f = SystemNadrzedny.Fold(ob);
            var sg = L.Signals.Values.FirstOrDefault(x => SystemNadrzedny.Fold(x.Label ?? "") == f || SystemNadrzedny.Fold(x.Name) == f);
            if (sg == null) { wszystkieOk = false; wyniki.Add($"{ob}: nie ma takiego sygnalizatora"); continue; }
            var ng = NazwaGry(sg.Name, "sygnalizator");
            if (ng == null) { wszystkieOk = false; wyniki.Add($"{ob}: brak w mapie SimRail"); continue; }
            // SZ po SZI z tego pulpitu: gra ma już inicjację – samo potwierdzenie (nie wcześniej niż po 5 s, Ie-104 § 11 ust. 13)
            bool poSzi = pending?.Code == "SZI" && pending.Arg == sg.Name;
            if (poSzi && kod is "SZ" or "NSZ" && Teraz < pending.NotBefore)
            { wszystkieOk = false; wyniki.Add($"{ob}: wykonanie możliwe po upływie 5 s od SZI – jeszcze {pending.NotBefore - Teraz:0} s"); continue; }
            var (ok, msg) = kod switch
            {
                "SZ" when poSzi => odb.WyslijPolecenie(ng, "SubsidiarySignalConfirm"),
                "SZ" => odb.WyslijPolecenie(ng, "SubsidiarySignalInit", "SubsidiarySignalConfirm", 1.5),
                "NSZ" => odb.WyslijPolecenie(ng, "SubsidiarySignalWithW24Init", "SubsidiarySignalWithW24Confirm", 1.5),
                "SZI" => odb.WyslijPolecenie(ng, "SubsidiarySignalInit"),
                _ => odb.WyslijPolecenie(ng, "RevokeSubsidiarySignal"),
            };
            if (ok && kod == "SZI") pending = new PendingInd { Code = "SZI", Arg = sg.Name, Line = line, Desc = "sygnał zastępczy – inicjalizacja", Targets = { ng }, NotBefore = Teraz + 5, Until = Teraz + 60 };
            else if (ok && poSzi && kod is "SZ" or "NSZ") pending = null;
            wszystkieOk &= ok; wyniki.Add($"{ob}: {msg}");
            Zdarz("Polecenie", ob, $"{kod} → gra: {(ok ? "wysłano" : "odrzucono")} ({msg})");
        }
        return new CmdResult(wszystkieOk, string.Join("; ", wyniki));
    }

    /// <summary>
    /// Zwolnienia przebiegów i odwołanie polecenia specjalnego jak w ISDR (mod ≥ 0.11, „RouteSpecial”): ZD – zwolnienie doraźne
    /// (gra wybiera pociągowy / manewrowy i sama liczy zwłokę), ZDM – manewrowy, ZDP – inicjalizacja zwolnienia natychmiastowego,
    /// ZDPP – wykonanie po 5 s, ZCZ – zwolnienie czasowe (inicjacja i potwierdzenie), OPS – odwołanie zainicjowanego polecenia.
    /// Pierwszy obiekt – semafor początkowy, ostatni (opcjonalnie) – koniec przebiegu; bez końca – koniec przebiegu nastawionego
    /// z SUK od tego semafora, a gdy go nie ma – mod szuka przebiegu nastawionego w grze. Zależności i zwłoki sprawdza gra.
    /// </summary>
    CmdResult ZwolnieniePrzebiegu(List<string> obiekty, string kod, string line)
    {
        if (kod == "OZCZ") return new CmdResult(false, "Pulpit zdalny SimRail: gra nie ma polecenia przerwania zwolnienia czasowego (oZCZ)");
        if (kod == "OPS" && obiekty.Count == 0 && pending != null) obiekty = new() { Station.Lbl(pending.Arg) };
        if (obiekty.Count == 0) return new CmdResult(false, "Brak sygnalizatora w poleceniu");
        string Fold(string s) => SystemNadrzedny.Fold(s ?? "");
        var f0 = Fold(obiekty[0]);
        var start = L.Signals.Values.FirstOrDefault(x => Fold(x.Label) == f0 || Fold(x.Name) == f0);
        if (start == null) return new CmdResult(false, $"{obiekty[0]}: nie ma takiego sygnalizatora");
        var sStart = NazwaGry(start.Name, "sygnalizator");
        if (sStart == null) return new CmdResult(false, $"{start.Label}: brak w mapie SimRail");
        bool mojePending = pending != null && pending.Arg == start.Name;

        // OPS polecenia specjalnego na samym semaforze (SZI) – odwołanie na obiekcie
        if (kod == "OPS" && (mojePending && pending.Code == "SZI" || !mojePending && obiekty.Count < 2 && !nastawioneZSuk.ContainsKey(start.Name)))
        {
            var (ok0, msg0) = odb.WyslijPolecenie(sStart, "RevokeSpecialInit");
            if (ok0 && mojePending) pending = null;
            Zdarz("Polecenie", start.Label, $"OPS → gra: {(ok0 ? "wysłano" : "odrzucono")} ({msg0})");
            return new CmdResult(ok0, msg0);
        }

        // koniec przebiegu: wskazany, z inicjalizacji ZDP, z ostatniego przebiegu z SUK albo nieznany (mod szuka w grze)
        string koniecSuk = null, koniecGry = null, rodzaj = null;
        if (obiekty.Count >= 2)
        {
            var fk = Fold(obiekty[^1]);
            var rd = RouteTable.GetValueOrDefault(start.Name, new())
                .FirstOrDefault(r => Fold(Station.Lbl(r.End)) == fk || Fold(r.End) == fk || (L.Signals.TryGetValue(r.End ?? "", out var es) && Fold(es.Label) == fk));
            koniecSuk = rd?.End ?? L.Signals.Values.FirstOrDefault(x => Fold(x.Label) == fk || Fold(x.Name) == fk)?.Name;
            if (koniecSuk == null) return new CmdResult(false, $"{obiekty[^1]}: nie ma takiego końca przebiegu od {start.Label}");
        }
        else if (mojePending && pending.Code == "ZDP" && pending.Targets.Count >= 3)
        { koniecGry = pending.Targets[1]; rodzaj = pending.Targets[2]; }
        else if (nastawioneZSuk.TryGetValue(start.Name, out var ns)) { koniecSuk = ns.Koniec; rodzaj = ns.Typ; }
        if (koniecGry == null && koniecSuk != null)
        {
            var rd = RouteTable.GetValueOrDefault(start.Name, new()).FirstOrDefault(r => r.End == koniecSuk && zKatalogu.ContainsKey(r.Id ?? ""));
            koniecGry = rd != null ? zKatalogu[rd.Id].Koniec : L.Signals.ContainsKey(koniecSuk) ? NazwaGry(koniecSuk, "sygnalizator") ?? "" : "";
        }
        koniecGry ??= "";
        rodzaj = kod switch { "ZDM" => "M", "ZDP" or "ZDPP" or "ZCZ" => "P", _ => rodzaj ?? Last?.Routes.GetValueOrDefault("simrail:" + start.Name)?.Type ?? "" };

        if (kod == "ZDPP")
        {
            if (!mojePending || pending.Code != "ZDP") return new CmdResult(false, "Najpierw zainicjuj zwolnienie doraźne (ZDP), wykonanie ZDPP po 5 s");
            if (Teraz < pending.NotBefore) return new CmdResult(false, $"Wykonanie możliwe po upływie 5 s od inicjalizacji (Ie-104 § 11 ust. 13) – jeszcze {pending.NotBefore - Teraz:0} s");
        }
        if (kod == "ZDP" && pending != null) return new CmdResult(false, $"Zainicjowano już polecenie specjalne {pending.Line} – wykonaj je albo odwołaj (OPS)");

        var (ok, msg) = kod switch
        {
            "ZD" => odb.WyslijSpecjalnePrzebiegu(sStart, koniecGry, rodzaj, "ForceRelease"),
            "ZDM" => odb.WyslijSpecjalnePrzebiegu(sStart, koniecGry, rodzaj, "ForceReleaseShuntingRoute"),
            "ZDP" => odb.WyslijSpecjalnePrzebiegu(sStart, koniecGry, rodzaj, "ForceInstantRouteReleaseInit"),
            "ZDPP" => odb.WyslijSpecjalnePrzebiegu(sStart, koniecGry, rodzaj, "ForceInstantRouteReleaseConfirm"),
            "ZCZ" => odb.WyslijSpecjalnePrzebiegu(sStart, koniecGry, rodzaj, "TimeRouteReleaseInit", "TimeRouteReleaseConfirm", 1.5),
            _ => odb.WyslijSpecjalnePrzebiegu(sStart, koniecGry, rodzaj, "RevokeSpecialInit"),
        };
        if (ok && kod == "ZDP")
            pending = new PendingInd { Code = "ZDP", Arg = start.Name, Line = line, Desc = "zwolnienie doraźne przebiegu – inicjalizacja",
                                       Targets = { sStart, koniecGry, rodzaj }, NotBefore = Teraz + 5, Until = Teraz + 60 };
        else if (ok && kod is "ZDPP" or "OPS" && mojePending) pending = null;
        Zdarz("Polecenie", start.Label, $"{kod} {start.Label}{(koniecSuk != null ? " → " + Station.Lbl(koniecSuk) : "")} → gra: {(ok ? "wysłano" : "odrzucono")} ({msg})");
        return new CmdResult(ok, msg);
    }

    /// <summary>Zamknięcie / otwarcie przejazdu w grze (obiekt – nazwa przejazdu z rysunku, np. „103.742”).</summary>
    CmdResult Przejazd(List<string> obiekty, string kod)
    {
        if (obiekty.Count == 0) return new CmdResult(false, "Brak przejazdu w poleceniu");
        var przejazdy = odb.Obiekty().Where(o => o.Pz >= 0 || o.Rodzaj == "przejazd").ToList();
        var wyniki = new List<string>(); bool wszystkieOk = true;
        foreach (var ob in obiekty)
        {
            var c = L.Crossings.FirstOrDefault(x => x.Name == ob || SystemNadrzedny.Fold(x.Name ?? "") == SystemNadrzedny.Fold(ob));
            var ng = c != null ? PrzejazdGry(c, przejazdy) : null;
            if (ng == null) { wszystkieOk = false; wyniki.Add($"przejazd {ob}: {(c == null ? "nie ma go na pulpicie" : "brak w stanach z gry")}"); continue; }
            var (ok, msg) = odb.WyslijPolecenie(ng, PrzejazdNaGre[kod]);
            wszystkieOk &= ok; wyniki.Add($"przejazd {ob}: {msg}");
            Zdarz("Polecenie", "przejazd " + ob, $"{kod} → gra: {(ok ? "wysłano" : "odrzucono")} ({msg})");
        }
        return new CmdResult(wszystkieOk, string.Join("; ", wyniki));
    }

    /// <summary>
    /// Blokada liniowa w grze: Wbl – wybór kierunku WYJAZD (ChooseBlockDirection, a przy kierunku PRZYJAZD – zmiana kierunku
    /// ChangeBlockDirection), oWbl – odwołanie, Pzk – pozwolenie (BlockDirectionApproval), Zwbl – zwolnienie kierunku,
    /// AZK – awaryjna zmiana kierunku (inicjacja + potwierdzenie; potwierdzenie polecenia specjalnego daje pulpit SUK).
    /// Które polecenia przyjmie dana blokada (SHL, Eac, Eap) – decyduje serwer gry; wynik widać po stanie blokady.
    /// </summary>
    CmdResult Blokada(List<string> obiekty, string kod)
    {
        var id = obiekty.FirstOrDefault(L.Blocks.ContainsKey)
                 ?? L.Blocks.Values.FirstOrDefault(b => obiekty.Any(o => SystemNadrzedny.Fold(o) == SystemNadrzedny.Fold(b.Label ?? "") || SystemNadrzedny.Fold(o) == SystemNadrzedny.Fold(b.Id)))?.Id;
        if (id == null) return new CmdResult(false, "Brak szlaku w poleceniu");
        var b = L.Blocks[id];
        var nazwy = odb.Obiekty().Where(o => o.Lb >= 0 || o.Rodzaj == "blokada" || o.Nazwa.EndsWith("bl", StringComparison.OrdinalIgnoreCase)).Select(o => o.Nazwa).ToList();
        var ng = BlokadaGry(b, new HashSet<string>(nazwy, StringComparer.OrdinalIgnoreCase));
        if (ng == null) return new CmdResult(false, $"Szlak {id}: brak blokady w stanach z gry");
        var bi = Last?.Blocks.GetValueOrDefault(id);
        int outw = b.Ends.Count > 0 ? b.Ends[0].Outward : 1;
        bool przyjazd = bi != null && bi.Dir == -outw;
        string[] polecenia = kod switch
        {
            "WBL" => new[] { przyjazd ? "ChangeBlockDirection" : "ChooseBlockDirection" },
            "OWBL" => new[] { przyjazd ? "RevokeChangeBlockDirection" : "RevokeChooseBlockDirection" },
            "PZK" => new[] { "BlockDirectionApproval" },
            "ZWBL" => new[] { "ReleaseBlockDirection" },
            "AZK" => new[] { "EmergencyBlockDirectionChangeInit", "EmergencyBlockDirectionChangeConfirm" },
            "ZKB" => new[] { "ChangeBlockDirection" },
            "OZKB" => new[] { "RevokeChangeBlockDirection" },
            "PZM" => new[] { "BlockDirectionChangePermission" },
            "STOP" => new[] { "Stop" },
            "OSTOP" => new[] { "RevokeStop" },
            _ => Array.Empty<string>(),
        };
        if (polecenia.Length == 0) return new CmdResult(false, $"Polecenie {kod} dla blokady nieobsługiwane");
        var wyniki = new List<string>(); bool wszystkieOk = true;
        foreach (var pol in polecenia)
        {
            var (ok, msg) = odb.WyslijPolecenie(ng, pol);
            wszystkieOk &= ok; wyniki.Add(msg);
            if (!ok) break;
        }
        var opis = string.Join("; ", wyniki);
        Zdarz("Polecenie", b.Label ?? id, $"{kod} → gra: {(wszystkieOk ? "wysłano" : "odrzucono")} ({opis})");
        return new CmdResult(wszystkieOk, $"szlak {id}: {opis}");
    }

    /// <summary>
    /// Przebieg: pierwszy obiekt – sygnalizator początkowy, ostatni – koniec (sygnalizator / koniec szlaku), środkowe – zwrotnice
    /// wariantu. Droga (zwrotnice) z tablicy przebiegów układu SUK, wysyłka do gry z położeniami zwrotnic w nazwach SimRail;
    /// mod wybiera z katalogu gry przebieg o tym początku, końcu i zwrotnicach.
    /// </summary>
    CmdResult Przebieg(List<string> obiekty, string rodzaj)
    {
        if (obiekty.Count < 2) return new CmdResult(false, "Przebieg: podaj początek i koniec");
        string Fold(string s) => SystemNadrzedny.Fold(s ?? "");
        var f0 = Fold(obiekty[0]);
        var start = L.Signals.Values.FirstOrDefault(x => Fold(x.Label) == f0 || Fold(x.Name) == f0);
        if (start == null) return new CmdResult(false, $"{obiekty[0]}: nie ma takiego sygnalizatora");
        var fk = Fold(obiekty[^1]);
        var przez = obiekty.Skip(1).Take(obiekty.Count - 2)
            .Select(o => L.Switches.Values.FirstOrDefault(x => Fold(x.Label) == Fold(o) || Fold(x.Num) == Fold(o))?.Num).Where(x => x != null).ToList();
        var kand = RouteTable.GetValueOrDefault(start.Name, new())
            .Where(r => r.Type == rodzaj && (Fold(Station.Lbl(r.End)) == fk || Fold(r.End) == fk ||
                                             (L.Signals.TryGetValue(r.End ?? "", out var es) && Fold(es.Label) == fk)))
            .Where(r => przez.All(p => r.Sw.Any(s => s.Num == p)))
            .OrderBy(r => r.Variant).ToList();
        if (kand.Count == 0) return new CmdResult(false, $"Brak przebiegu {(rodzaj == "P" ? "pociągowego" : "manewrowego")} {obiekty[0]} → {obiekty[^1]} w tablicy przebiegów");
        var rd = kand[0];
        if (zKatalogu.TryGetValue(rd.Id ?? "", out var pg))
        {
            // przebieg z katalogu gry (brak go w tablicy odtworzonej z zobrazowania) – do gry dokładnie jej nazwy i zwrotnice
            var (ok1, msg1) = odb.WyslijPrzebieg(pg.Start, pg.Koniec, rodzaj, pg.Zw);
            if (ok1) nastawioneZSuk[start.Name] = (rd.End, rodzaj, Teraz);
            Zdarz("Polecenie", start.Label, $"{(rodzaj == "P" ? "POCIĄG" : "MANEWR")} {obiekty[0]} → {obiekty[^1]} (z katalogu gry) → gra: {(ok1 ? "wysłano" : "odrzucono")} ({msg1})");
            return new CmdResult(ok1, msg1);
        }
        var sStart = NazwaGry(start.Name, "sygnalizator");   // alias „~2” → ten sam semafor gry
        if (sStart == null) return new CmdResult(false, $"{start.Label}: brak w mapie SimRail");
        string koniecSr = rd.End != null && L.Signals.ContainsKey(rd.End) ? NazwaGry(rd.End, "sygnalizator") ?? "" : "";   // szlak: koniec nieznany – wybór po zwrotnicach
        var zw = new List<string>();
        foreach (var (num, pos) in rd.Sw)
        {
            var sn = NazwaGry(num, "zwrotnica");
            if (sn == null) return new CmdResult(false, $"zwrotnica {Station.Lbl(num)}: brak w mapie SimRail");
            zw.Add($"{sn}={(pos == "+" ? "Plus" : "Minus")}");
        }
        var (ok, msg) = odb.WyslijPrzebieg(sStart, koniecSr, rodzaj, zw);
        if (ok) nastawioneZSuk[start.Name] = (rd.End, rodzaj, Teraz);
        Zdarz("Polecenie", start.Label, $"{(rodzaj == "P" ? "POCIĄG" : "MANEWR")} {obiekty[0]} → {obiekty[^1]} → gra: {(ok ? "wysłano" : "odrzucono")} ({msg})");
        return new CmdResult(ok, msg);
    }

    /// <summary>Obiekt po oznaczeniu z pulpitu: zwrotnica / wykolejnica dla Plus/Minus, sygnalizator dla Stój, oba dla Stop, odcinek dla ZMK.</summary>
    string Znajdz(string ozn, string kod)
    {
        var f = SystemNadrzedny.Fold(ozn);
        bool Rowne(string label, string name) => SystemNadrzedny.Fold(label ?? "") == f || SystemNadrzedny.Fold(name ?? "") == f;
        if (kod is "ZMK" or "OZMK")
            return L.Sections.Keys.FirstOrDefault(k => SystemNadrzedny.Fold(Station.Lbl(k)) == f || SystemNadrzedny.Fold(k) == f
                                                       || SystemNadrzedny.Fold(Station.Lbl(k)) == "IT" + f);
        if (kod != "STOJ")
        {
            var sw = L.Switches.Values.FirstOrDefault(x => Rowne(x.Label, x.Num)); if (sw != null) return sw.Num;
            var de = L.Derailers.Values.FirstOrDefault(x => Rowne(x.Label, x.Name)); if (de != null) return de.Name;
        }
        if (kod is "STOJ" or "STOP" or "OSTOP")
        {
            var sg = L.Signals.Values.FirstOrDefault(x => Rowne(x.Label, x.Name)); if (sg != null) return sg.Name;
        }
        return null;
    }

    void Zdarz(string kat, string obiekt, string tekst)
    {
        var e = new EventRecord { Id = ++nrZdarzenia, Czas = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), Obszar = L.Name ?? "SimRail", Stacja = "SimRail",
                                  TypObiektu = "SimRail", Obiekt = obiekt, Kategoria = kat, Zdarzenie = tekst, Operator = user?.Name ?? "", Zrodlo = "SimRail" };
        void Dodaj() { Events.Add(e); EventAdded?.Invoke(e); }
        if (ui.CheckAccess()) Dodaj(); else ui.BeginInvoke(Dodaj);
    }

    // ------------------------------------------------------------------ reszta stanowiska – bez komputera zależności
    /// <summary>Okno potwierdzenia polecenia specjalnego wpisanego w polu polecenia (ZDP, SZI): wykonanie – ZDPP albo SZ.</summary>
    public CmdResult Confirm(string typed)
    {
        var p = pending; if (p == null) return new(false, "Brak polecenia do potwierdzenia");
        var lbl = Station.Lbl(p.Arg);
        var tf = SystemNadrzedny.Fold((typed ?? "").Trim());
        if (tf != SystemNadrzedny.Fold(lbl) && tf != SystemNadrzedny.Fold(p.Arg ?? "") && tf != SystemNadrzedny.Fold(L.Signals.TryGetValue(p.Arg ?? "", out var sg) ? sg.Label ?? "#" : "#"))
            return new(false, $"Wpisany adres nie zgadza się z adresem polecenia ({lbl})");
        return Exec($"{lbl} {(p.Code == "ZDP" ? "ZDPP" : "SZ")}");
    }
    public void Cancel() { if (pending != null) Exec($"{Station.Lbl(pending.Arg)} OPS"); }
    public void AckAlarm(long id) { }
    public void Login(User u) => user = u;
    public void SetPage(string page) { }
    public string RequestControl(string post) => null;
    public void ReleaseControl(string post) { }
    public void AnswerControl(string post, bool yes) { }
    public void Phone(string toDesk, string text) { }
    public List<PlanRow> GetPlan() => new();
    public ScoreData GetScore() => new() { Stats = new() };
    public BookData GetBook() => new();
    public void AddBook(string text, string auto) { }
    public EdrData GetEdr() => null;
    public CmdResult EdrCmd(EdrPolecenie p) => new(false, "EDR niedostępny w trybie pulpitu zdalnego SimRail");

    void Nieuzywane() { AlarmAdded?.Invoke(null); AlarmsChanged?.Invoke(); Beep?.Invoke(); SpecialExecuted?.Invoke(); PendingExpired?.Invoke(); Disconnected?.Invoke(null); Reset?.Invoke(); }
}
