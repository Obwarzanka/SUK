using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;

namespace LcsWidzew.SimRail;

/// <summary>
/// Odbiornik stanów z gry SimRail (mod MelonLoadera „SimRailMost”, protokół „simrail-most” v1, datagramy JSON po UDP).
/// Etap 1 integracji: tylko podgląd – dane nie trafiają do komputera zależności ani do pulpitu.
/// <list type="bullet">
/// <item>„stan” – pełna migawka obiektów posterunku w częściach (pole czesc / czesci w obrębie serii);</item>
/// <item>„zmiana” – zajęcie albo zwolnienie obiektu (odcinek, zwrotnica…) z godziną gry i numerami pociągów.</item>
/// </list>
/// Odbiór na osobnym wątku; okno odczytuje stan przez <see cref="Obiekty"/> i <see cref="PobierzZdarzenia"/>.
/// </summary>
public sealed class SimRailOdbiornik : IDisposable
{
    public const int DomyslnyPort = 47130;

    /// <summary>Jeden obiekt SimRail (nazwa z gry, np. „l1_kol_it5”).</summary>
    /// <remarks>Od moda 0.9 stany także liczbowo (−1 = brak / nie dotyczy): Pn – RouteState, Bln – BlockingState,
    /// Lb – LineBlockState (blokada liniowa), Pz – CrossingState.StateOverall (przejazd).</remarks>
    public sealed record Obiekt(string Nazwa, string Rodzaj, bool Zajety, string Basic, string Przebieg, string Blokada,
                                string Zwrotnica, string Pociagi, int Pn = -1, int Bln = -1, int Lb = -1, int Pz = -1, string WidokBlokady = "", string WidokPrzejazdu = "",
                                int? GrotBlokady = null, bool MigaBlokada = false, string Stacja = "",
                                string SzczegolyPrzejazdu = "", string BarwaSygnalu = "");   // mod ≥ 0.12 „pzd”: „s=8;wj=8;wy=8;c=1;m=0;uzk=1;sw=0;top=0;aut=0;zd=0;tw=”

    /// <summary>Wpis historii poleceń pulpitu EBI w grze (mod ≥ 0.9.1): polecenie i odpowiedź serwera (przyjęte / odrzucone).</summary>
    public sealed record WpisHistorii(DateTime Odebrano, string CzasGry, string Data, string Polecenie, string S1, string S2);
    readonly Queue<WpisHistorii> _historia = new();
    /// <summary>Wpisy historii gry odebrane od ostatniego wywołania (kolejka jest opróżniana).</summary>
    public List<WpisHistorii> PobierzHistorie() { lock (_blokada) { var l = _historia.ToList(); _historia.Clear(); return l; } }

    /// <summary>Zajęcie / zwolnienie zgłoszone przez mod.</summary>
    public sealed record Zdarzenie(DateTime Odebrano, string CzasGry, string Stacja, string Nazwa, string Rodzaj, bool Zajety, string Pociagi);

    readonly object _blokada = new();
    readonly Dictionary<string, Obiekt> _obiekty = new(StringComparer.Ordinal);
    readonly Queue<Zdarzenie> _zdarzenia = new();
    UdpClient _udp;
    Thread _watek;
    volatile bool _praca;

    public int Port { get; private set; }
    public bool Dziala => _praca;
    public long Pakiety { get; private set; }
    public long Bledy { get; private set; }
    public DateTime OstatniPakiet { get; private set; }
    public string Stacja { get; private set; } = "";
    public string Zrodlo { get; private set; } = "";
    public string CzasGry { get; private set; } = "";
    public string OstatniBlad { get; private set; } = "";

    public void Start(int port)
    {
        Stop();
        Port = port;
        _udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
        _praca = true;
        _watek = new Thread(Petla) { IsBackground = true, Name = "SimRailOdbiornik" };
        _watek.Start();
    }

    public void Stop()
    {
        _praca = false;
        try { _udp?.Dispose(); } catch { }
        _udp = null;
        _watek = null;
    }

    public void Dispose() { Stop(); try { _nadawca?.Dispose(); } catch { } _nadawca = null; }

    void Petla()
    {
        var udp = _udp;
        while (_praca && udp != null)
        {
            try
            {
                IPEndPoint nadawca = null;
                var dane = udp.Receive(ref nadawca);
                Przyjmij(dane);
            }
            catch (SocketException) { if (!_praca) break; }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex) { lock (_blokada) { Bledy++; OstatniBlad = ex.Message; } }
        }
    }

    /// <summary>Przetwarza jeden datagram (publiczne dla testów).</summary>
    public void Przyjmij(byte[] dane)
    {
        using var doc = JsonDocument.Parse(dane);
        var k = doc.RootElement;
        if (Liczba(k, "v") != 1) throw new InvalidDataException("nieznana wersja protokołu");
        var typ = Tekst(k, "typ");
        lock (_blokada)
        {
            Pakiety++;
            OstatniPakiet = DateTime.Now;
            // posterunek gry – tylko z migawki; w „zmianie” „stacja” to posterunek jednego obiektu (przy kilku posterunkach – sąsiad)
            var st = Tekst(k, "stacja");
            var cg = Tekst(k, "czasGry"); if (cg.Length > 0) CzasGry = cg;
            if (typ == "stan")
            {
                if (st.Length > 0) Stacja = st;
                Zrodlo = Tekst(k, "zrodlo");
                if (k.TryGetProperty("obiekty", out var lista) && lista.ValueKind == JsonValueKind.Array)
                    foreach (var o in lista.EnumerateArray())
                    {
                        var n = Tekst(o, "n");
                        if (n.Length == 0) continue;
                        _obiekty[n] = new Obiekt(n, Tekst(o, "r"), Logiczna(o, "z"), Tekst(o, "b"), Tekst(o, "p"), Tekst(o, "bl"),
                                                 Tekst(o, "zw"), Tekst(o, "po"), Liczba(o, "pn"), Liczba(o, "bln"), Liczba(o, "lb"), Liczba(o, "pz"), Tekst(o, "lbo"), Tekst(o, "pzo"),
                                                 o.TryGetProperty("lbk", out var gk) && gk.TryGetInt32(out var gki) ? gki : null, Logiczna(o, "lbm"), Tekst(o, "st"), Tekst(o, "pzd"), Tekst(o, "sgb"));
                    }
            }
            else if (typ == "zmiana")
            {
                var n = Tekst(k, "n");
                if (n.Length == 0) return;
                bool z = Logiczna(k, "z");
                var po = Tekst(k, "po");
                var r = Tekst(k, "r");
                _obiekty[n] = _obiekty.TryGetValue(n, out var stary)
                    ? stary with { Zajety = z, Pociagi = po.Length > 0 || !z ? po : stary.Pociagi, Stacja = stary.Stacja.Length > 0 ? stary.Stacja : st }
                    : new Obiekt(n, r, z, "", "", "", "", po, Stacja: st);
                _zdarzenia.Enqueue(new Zdarzenie(DateTime.Now, cg, st, n, r, z, po));
                while (_zdarzenia.Count > 5000) _zdarzenia.Dequeue();
            }
            else if (typ == "odp")
            {
                long id = k.TryGetProperty("id", out var iv) && iv.TryGetInt64(out var ii) ? ii : 0;
                if (_odpowiedzi.Remove(id, out var tcs)) tcs.TrySetResult((Logiczna(k, "ok"), Tekst(k, "msg")));
            }
            else if (typ == "historia")
            {
                _historia.Enqueue(new WpisHistorii(DateTime.Now, cg, Tekst(k, "data"), Tekst(k, "polecenie"), Tekst(k, "s1"), Tekst(k, "s2")));
                while (_historia.Count > 500) _historia.Dequeue();
            }
            else throw new InvalidDataException($"nieznany typ komunikatu: {typ}");
        }
    }

    // ------------------------------------------------------------------ polecenia do gry (pulpit zdalny)
    public const int DomyslnyPortPolecen = 47131;
    readonly Dictionary<long, TaskCompletionSource<(bool, string)>> _odpowiedzi = new();
    long _nrPolecenia;
    UdpClient _nadawca;

    /// <summary>
    /// Polecenie pojedyncze do moda (IndividualOrder): nazwa obiektu SimRail (railNetworkName) i polecenie gry
    /// (Plus, Minus, Stop, RevokeStop, StopSignal). Czeka na odpowiedź moda do 3 s.
    /// </summary>
    public (bool Ok, string Msg) WyslijPolecenie(string nazwa, string order, int port = DomyslnyPortPolecen)
        => Wyslij(new() { ["kod"] = "IndividualOrder", ["nazwa"] = nazwa, ["order"] = order }, port);

    /// <summary>Polecenie specjalne dwuetapowe (mod ≥ 0.9.4): <paramref name="order"/> od razu, <paramref name="potem"/> po <paramref name="zwloka"/> s.</summary>
    public (bool Ok, string Msg) WyslijPolecenie(string nazwa, string order, string potem, double zwloka, int port = DomyslnyPortPolecen)
        => Wyslij(new() { ["kod"] = "IndividualOrder", ["nazwa"] = nazwa, ["order"] = order, ["potem"] = potem, ["zwloka"] = zwloka }, port);

    /// <summary>
    /// Przebieg do gry (RouteOrder): początek i koniec – nazwy SimRail (koniec może być pusty, gdy nieznany – np. wyjazd na szlak),
    /// rodzaj P / M, zwrotnice przebiegu jako „z1985=Plus” (wybór wariantu z katalogu przebiegów gry).
    /// </summary>
    public (bool Ok, string Msg) WyslijPrzebieg(string start, string koniec, string rodzaj, IEnumerable<string> zwrotnice, int port = DomyslnyPortPolecen)
        => Wyslij(new() { ["kod"] = "RouteOrder", ["start"] = start, ["koniec"] = koniec ?? "", ["rodzaj"] = rodzaj, ["zwrotnice"] = zwrotnice.ToList() }, port);

    /// <summary>
    /// Polecenie przebiegowe ISDR (mod ≥ 0.11, „RouteSpecial”): zwolnienia ZD / ZDM / ZDP / ZDPP / ZCZ i OPS dla pary początek–koniec.
    /// Koniec pusty – mod szuka przebiegu od tego początku nastawionego w grze. <paramref name="potem"/> – drugi etap po <paramref name="zwloka"/> s.
    /// </summary>
    public (bool Ok, string Msg) WyslijSpecjalnePrzebiegu(string start, string koniec, string rodzaj, string order, string potem = null, double zwloka = 0, int port = DomyslnyPortPolecen)
    {
        var pola = new Dictionary<string, object> { ["kod"] = "RouteSpecial", ["start"] = start, ["koniec"] = koniec ?? "", ["rodzaj"] = rodzaj ?? "", ["order"] = order };
        if (potem != null) { pola["potem"] = potem; pola["zwloka"] = zwloka; }
        return Wyslij(pola, port);
    }

    /// <summary>Katalog przebiegów gry (mod ≥ 0.10.x, polecenie „RouteCatalog”): mod zapisuje go do pliku JSON i odpowiada jego ścieżką.</summary>
    public (bool Ok, string Msg) WyslijKatalogPrzebiegow(int port = DomyslnyPortPolecen)
        => Wyslij(new() { ["kod"] = "RouteCatalog" }, port);

    (bool Ok, string Msg) Wyslij(Dictionary<string, object> pola, int port)
    {
        var id = Interlocked.Increment(ref _nrPolecenia);
        var tcs = new TaskCompletionSource<(bool, string)>();
        lock (_blokada) _odpowiedzi[id] = tcs;
        pola["v"] = 1; pola["typ"] = "polecenie"; pola["id"] = id;
        var json = JsonSerializer.SerializeToUtf8Bytes(pola);
        try
        {
            _nadawca ??= new UdpClient();
            _nadawca.Send(json, json.Length, new IPEndPoint(IPAddress.Loopback, port));
        }
        catch (Exception ex) { lock (_blokada) _odpowiedzi.Remove(id); return (false, "Nie można wysłać do gry: " + ex.Message); }
        if (!Dziala) { lock (_blokada) _odpowiedzi.Remove(id); return (false, "Odbiór stanów z gry jest wyłączony – brak potwierdzenia"); }
        if (tcs.Task.Wait(TimeSpan.FromSeconds(3))) return tcs.Task.Result;
        lock (_blokada) _odpowiedzi.Remove(id);
        return (false, "Gra nie potwierdziła polecenia (mod SimRailMost ≥ 0.7 uruchomiony?)");
    }

    /// <summary>Wspólny odbiornik programu: okno „SimRail” i pulpit zdalny korzystają z jednego gniazda UDP.</summary>
    public static SimRailOdbiornik Wspolny { get; } = new();

    /// <summary>
    /// Posterunki gry w ostatnich danych: z obiektów (mod ≥ 0.10.2, LCS z kilkoma posterunkami – kilka), a bez nich –
    /// posterunek migawki. Pusta lista – gra nic nie nadała.
    /// </summary>
    public List<string> PosterunkiGry()
    {
        lock (_blokada)
        {
            var l = _obiekty.Values.Select(o => o.Stacja).Where(s => s.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s, StringComparer.CurrentCulture).ToList();
            if (l.Count == 0 && Stacja.Length > 0) l.Add(Stacja);
            return l;
        }
    }

    /// <summary>Kopia stanu wszystkich obiektów.</summary>
    public List<Obiekt> Obiekty() { lock (_blokada) return _obiekty.Values.ToList(); }

    /// <summary>Zdarzenia odebrane od ostatniego wywołania (kolejka jest opróżniana).</summary>
    public List<Zdarzenie> PobierzZdarzenia()
    {
        lock (_blokada)
        {
            var l = _zdarzenia.ToList();
            _zdarzenia.Clear();
            return l;
        }
    }

    public void Wyczysc() { lock (_blokada) { _obiekty.Clear(); _zdarzenia.Clear(); Pakiety = 0; Bledy = 0; } }

    static string Tekst(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
    static bool Logiczna(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.True;
    static int Liczba(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : -1;
}
