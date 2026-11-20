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
    public sealed record Obiekt(string Nazwa, string Rodzaj, bool Zajety, string Basic, string Przebieg, string Blokada,
                                string Zwrotnica, string Pociagi);

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
            var st = Tekst(k, "stacja"); if (st.Length > 0) Stacja = st;
            var cg = Tekst(k, "czasGry"); if (cg.Length > 0) CzasGry = cg;
            if (typ == "stan")
            {
                Zrodlo = Tekst(k, "zrodlo");
                if (k.TryGetProperty("obiekty", out var lista) && lista.ValueKind == JsonValueKind.Array)
                    foreach (var o in lista.EnumerateArray())
                    {
                        var n = Tekst(o, "n");
                        if (n.Length == 0) continue;
                        _obiekty[n] = new Obiekt(n, Tekst(o, "r"), Logiczna(o, "z"), Tekst(o, "b"), Tekst(o, "p"), Tekst(o, "bl"),
                                                 Tekst(o, "zw"), Tekst(o, "po"));
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
                    ? stary with { Zajety = z, Pociagi = po.Length > 0 || !z ? po : stary.Pociagi }
                    : new Obiekt(n, r, z, "", "", "", "", po);
                _zdarzenia.Enqueue(new Zdarzenie(DateTime.Now, cg, st, n, r, z, po));
                while (_zdarzenia.Count > 5000) _zdarzenia.Dequeue();
            }
            else if (typ == "odp")
            {
                long id = k.TryGetProperty("id", out var iv) && iv.TryGetInt64(out var ii) ? ii : 0;
                if (_odpowiedzi.Remove(id, out var tcs)) tcs.TrySetResult((Logiczna(k, "ok"), Tekst(k, "msg")));
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
    {
        var id = Interlocked.Increment(ref _nrPolecenia);
        var tcs = new TaskCompletionSource<(bool, string)>();
        lock (_blokada) _odpowiedzi[id] = tcs;
        var json = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
            { ["v"] = 1, ["typ"] = "polecenie", ["id"] = id, ["kod"] = "IndividualOrder", ["nazwa"] = nazwa, ["order"] = order });
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
/// <code>{ "opis": "…", "mapa": { "l1_kol_it5": "KO:5" } }</code>
/// </summary>
public sealed class SimRailMapa
{
    public string Opis { get; set; } = "Mapa obiektów SimRail (klucz) na odcinki / obiekty SUK (wartość). Wypełnia właściciel.";
    public SortedDictionary<string, string> Mapa { get; set; } = new(StringComparer.Ordinal);

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
        return m;
    }

    public void Zapisz(string plik)
    {
        if (File.Exists(plik)) File.Copy(plik, plik + ".bak", true);
        File.WriteAllText(plik, JsonSerializer.Serialize(this, Opcje));
    }
}
