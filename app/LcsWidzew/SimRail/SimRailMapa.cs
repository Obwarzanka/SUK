using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;

namespace LcsWidzew.SimRail;

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
