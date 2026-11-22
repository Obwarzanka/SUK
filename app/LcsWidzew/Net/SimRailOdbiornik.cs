using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;

namespace LcsWidzew.Net;

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

/// <summary>
/// Mapa nazw SimRail → odcinki SUK, plik danych obok układu: <c>&lt;układ&gt;.simrail.json</c>.
/// Mapę projektuje właściciel (podziały odcinków w grze i w SUK mogą się różnić); program jej nie zgaduje.
/// <code>{ "opis": "…", "posterunki": [ "Koluszki" ], "mapa": { "l1_kol_it5": "KO:5" } }</code>
/// Każdy posterunek SimRail ma własny układ SUK (Data/station.json albo Data/projekty/*.json) i własną mapę obok niego.
/// <see cref="Posterunki"/> – nazwy posterunków gry (pole „stacja” moda), które obsługuje ten układ; po nich program
/// proponuje układ przy starcie i ostrzega, gdy w grze jest inny posterunek. Pusta lista – układ bez przypisania.
/// </summary>
public sealed class SimRailMapa
{
    public string Opis { get; set; } = "Mapa obiektów SimRail (klucz) na odcinki / obiekty SUK (wartość). Wypełnia właściciel.";
    /// <summary>Posterunki SimRail (nazwy jak w grze), które obsługuje układ tej mapy.</summary>
    public List<string> Posterunki { get; set; } = new();
    public SortedDictionary<string, string> Mapa { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Czy układ obsługuje posterunek gry <paramref name="stacja"/> (bez rozróżniania wielkości liter, polskich znaków i odstępów).</summary>
    public bool Obejmuje(string stacja) => !string.IsNullOrWhiteSpace(stacja) && Posterunki.Any(p => Klucz(p) == Klucz(stacja));

    /// <summary>Czy układ obsługuje choć jeden z posterunków gry (LCS z kilkoma posterunkami).</summary>
    public bool ObejmujeKtorys(IEnumerable<string> stacje) => stacje.Any(Obejmuje);

    /// <summary>Nazwa posterunku do porównań: bez wielkości liter, polskich znaków i odstępów („Łódź  Widzew” = „lodz widzew”).</summary>
    public static string Klucz(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var d = s.Trim().ToLowerInvariant().Replace('ł', 'l').Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        bool odstep = false;
        foreach (var c in d)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            if (char.IsWhiteSpace(c) || c == '_' || c == '-') { odstep = sb.Length > 0; continue; }
            if (odstep) { sb.Append(' '); odstep = false; }
            sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>Układy (pliki <c>*.json</c>) z mapą SimRail, która obejmuje posterunek <paramref name="stacja"/>.</summary>
    public static List<string> UkladyDlaPosterunku(IEnumerable<string> uklady, string stacja)
    {
        var wynik = new List<string>();
        foreach (var u in uklady)
            try { if (Wczytaj(PlikDla(u)).Obejmuje(stacja)) wynik.Add(u); } catch { }
        return wynik;
    }

    static readonly JsonSerializerOptions Opcje = new()
    {
        WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string PlikDla(string sciezkaUkladu) => sciezkaUkladu == null ? null
        : Path.Combine(Path.GetDirectoryName(sciezkaUkladu) ?? "", Path.GetFileNameWithoutExtension(sciezkaUkladu) + ".simrail.json");

    public static SimRailMapa Wczytaj(string plik)
    {
        if (plik == null || !File.Exists(plik)) return new SimRailMapa();
        var m = JsonSerializer.Deserialize<SimRailMapa>(File.ReadAllText(plik), Opcje) ?? new SimRailMapa();
        m.Mapa = new SortedDictionary<string, string>(m.Mapa ?? new(), StringComparer.Ordinal);
        m.Posterunki = (m.Posterunki ?? new()).Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return m;
    }

    /// <summary>
    /// Krótki nasłuch portu stanów moda (bez wspólnego odbiornika): nazwa posterunku z pierwszego pakietu albo null
    /// (gra nie nadaje, port zajęty, przerwano). Do wyboru układu przy starcie.
    /// </summary>
    public static async Task<string> WykryjPosterunek(int port, TimeSpan czas, CancellationToken stop)
    {
        using var udp = new UdpClient();
        try
        {
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, false);
            udp.Client.Bind(new IPEndPoint(IPAddress.Loopback, port));
        }
        catch { return null; }
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(stop);
        cts.CancelAfter(czas);
        try
        {
            while (!cts.IsCancellationRequested)
            {
                var r = await udp.ReceiveAsync(cts.Token);
                try
                {
                    using var doc = JsonDocument.Parse(r.Buffer);
                    if (doc.RootElement.TryGetProperty("stacja", out var s) && s.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(s.GetString()))
                        return s.GetString().Trim();
                }
                catch (JsonException) { }
            }
        }
        catch (OperationCanceledException) { }
        catch (SocketException) { }
        return null;
    }

    public void Zapisz(string plik)
    {
        if (File.Exists(plik)) File.Copy(plik, plik + ".bak", true);
        File.WriteAllText(plik, JsonSerializer.Serialize(this, Opcje));
    }
}
