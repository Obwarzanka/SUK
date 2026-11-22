using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace Lcs.Powtarzacz;

/// <summary>
/// Odbiór stanu przejazdów od moda SimRailMost (≥ 0.12, PowtarzaczUdp.cs): UDP 127.0.0.1:47134, datagramy
/// {"v":1,"typ":"przejazdy","posterunek":"…","seria":5,"czesc":0,"czesci":4,"p":[…]}. Wątek tła; okno pobiera <see cref="Migawka"/>.
/// </summary>
public sealed class Odbiornik : IDisposable
{
    readonly UdpClient _udp;
    readonly Thread _watek;
    readonly ConcurrentDictionary<string, Przejazd> _stan = new(StringComparer.OrdinalIgnoreCase);
    volatile bool _koniec;
    public string Blad { get; private set; }
    public string Posterunek { get; private set; } = "";
    public DateTime Ostatni { get; private set; } = DateTime.MinValue;
    public int Port { get; }

    public Odbiornik(int port)
    {
        Port = port;
        try
        {
            _udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
            _watek = new Thread(Petla) { IsBackground = true, Name = "Powtarzacz UDP" };
            _watek.Start();
        }
        catch (Exception ex) { Blad = $"Nie można otworzyć portu UDP {port}: {ex.Message}"; }
    }

    void Petla()
    {
        var ep = new IPEndPoint(IPAddress.Any, 0);
        while (!_koniec)
        {
            try
            {
                var dane = _udp.Receive(ref ep);
                using var doc = JsonDocument.Parse(dane);
                var r = doc.RootElement;
                if (!r.TryGetProperty("typ", out var typ) || typ.GetString() != "przejazdy") continue;
                if (r.TryGetProperty("posterunek", out var po) && po.GetString() is string s) Posterunek = s;
                if (r.TryGetProperty("p", out var lista) && lista.ValueKind == JsonValueKind.Array)
                    foreach (var e in lista.EnumerateArray())
                    {
                        var p = Przejazd.ZJson(e);
                        if (p.Nazwa.Length > 0) _stan[p.Nazwa] = p;
                    }
                Ostatni = DateTime.Now;
            }
            catch (SocketException) when (_koniec) { return; }
            catch { /* uszkodzony datagram albo reset gniazda – dalej */ }
        }
    }

    public List<Przejazd> Migawka() => _stan.Values.ToList();

    public void Dispose() { _koniec = true; try { _udp?.Close(); } catch { } }
}
