using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Lcs.Lacznosc;

/// <summary>
/// Klient moda SimRailMost (protokół simrail-most v1, opis w SimRailMost/Lacznosc.cs). Co 3 s wysyła „lacznosc_zapisz”
/// na port poleceń moda (domyślnie 127.0.0.1:47131); mod odsyła na ten sam gniazdo „lacznosc_stan”, „czat” i „odp”.
/// Zdarzenia przychodzą w wątku odbioru – okno przenosi je do wątku UI.
/// </summary>
public sealed class MostKlient : IDisposable
{
    readonly UdpClient _udp;
    readonly IPEndPoint _mod;
    readonly Timer _zapis;
    long _id;
    volatile bool _koniec;

    public event Action<JsonElement> Pakiet;
    public DateTime OstatniPakiet { get; private set; } = DateTime.MinValue;
    public bool Polaczony => (DateTime.Now - OstatniPakiet).TotalSeconds < 8;

    public MostKlient(int portModa = 47131)
    {
        _mod = new IPEndPoint(IPAddress.Loopback, portModa);
        _udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        // mod nie słucha (gra zamknięta) → ICMP „port unreachable” przerywałby Receive błędem 10054
        try { _udp.Client.IOControl(-1744830452, new byte[] { 0 }, null); } catch { }
        new Thread(Petla) { IsBackground = true, Name = "Łączność – odbiór" }.Start();
        _zapis = new Timer(_ => Wyslij(w => w.WriteString("typ", "lacznosc_zapisz")), null, 0, 3000);
    }

    void Petla()
    {
        while (!_koniec)
        {
            try
            {
                IPEndPoint ep = null;
                var dane = _udp.Receive(ref ep);
                using var doc = JsonDocument.Parse(dane);
                OstatniPakiet = DateTime.Now;
                Pakiet?.Invoke(doc.RootElement.Clone());
            }
            catch (ObjectDisposedException) { break; }
            catch (SocketException) { if (_koniec) break; }
            catch (JsonException) { }
        }
    }

    void Wyslij(Action<Utf8JsonWriter> tresc)
    {
        try
        {
            using var ms = new MemoryStream();
            using (var w = new Utf8JsonWriter(ms)) { w.WriteStartObject(); w.WriteNumber("v", 1); tresc(w); w.WriteEndObject(); }
            var b = ms.ToArray();
            _udp.Send(b, b.Length, _mod);
        }
        catch { }
    }

    /// <summary>Wysyła komunikat do posterunku; zwraca id (odpowiedź „odp” z tym id).</summary>
    public long WyslijKomunikat(string doKogo, string klasa, string pociag, int? tor, string czas, string powod, string tekst)
    {
        var id = Interlocked.Increment(ref _id);
        Wyslij(w =>
        {
            w.WriteString("typ", "czat_wyslij"); w.WriteNumber("id", id); w.WriteString("do", doKogo); w.WriteString("klasa", klasa);
            if (!string.IsNullOrWhiteSpace(pociag)) w.WriteString("pociag", pociag.Trim());
            if (tor.HasValue) w.WriteNumber("tor", tor.Value);
            if (!string.IsNullOrWhiteSpace(czas)) w.WriteString("czas", czas.Trim());
            if (!string.IsNullOrWhiteSpace(powod)) w.WriteString("powod", powod);
            if (!string.IsNullOrWhiteSpace(tekst)) w.WriteString("tekst", tekst.Trim());
        });
        return id;
    }

    public void Dispose()
    {
        _koniec = true;
        _zapis.Dispose();
        _udp.Dispose();
    }

    public static string S(JsonElement e, string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
