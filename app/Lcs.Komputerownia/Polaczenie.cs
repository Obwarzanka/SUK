using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;

namespace Lcs.Komputerownia;

/// <summary>
/// Połączenie z symulatorem jako program serwisowy (protokół symulatora: JSON w liniach przez TCP; „serwis” zamiast „hello” –
/// bez stanowiska dyżurnego). Żądania: „karty” – stan wszystkich kart, „usterka” – wprowadzenie / usunięcie usterki karty.
/// </summary>
public sealed class Polaczenie : IDisposable
{
    /// <summary>Port serwisowy symulatora na tym komputerze (tylko 127.0.0.1); serwer stanowisk – 47120.</summary>
    public const int PortSerwisowy = 47121, PortSerwera = 47120;

    TcpClient tcp; StreamWriter w; readonly object lk = new();
    long seq; bool closed;
    readonly Dictionary<long, TaskCompletionSource<JsonNode>> waits = new();
    public string Stacja { get; private set; }
    public string Adres { get; private set; }
    public event Action<string> Rozlaczono;

    public static Polaczenie Polacz(string host, int port)
    {
        var p = new Polaczenie { Adres = $"{host}:{port}" };
        p.tcp = new TcpClient { NoDelay = true };
        if (!p.tcp.ConnectAsync(host, port).Wait(TimeSpan.FromSeconds(5))) { p.Dispose(); throw new IOException($"Brak odpowiedzi symulatora {host}:{port}"); }
        p.w = new StreamWriter(p.tcp.GetStream(), new UTF8Encoding(false)) { NewLine = "\n" };
        var hello = new TaskCompletionSource<JsonNode>();
        lock (p.waits) p.waits[0] = hello;
        new Thread(p.Czytaj) { IsBackground = true, Name = "Komputerownia" }.Start();
        p.Wyslij(new JsonObject { ["t"] = "serwis" });
        if (!hello.Task.Wait(TimeSpan.FromSeconds(6)) || hello.Task.Result == null)
        {
            p.Dispose();
            throw new IOException("Symulator nie odpowiada na żądanie programu serwisowego (starsza wersja symulatora?)");
        }
        p.Stacja = (string)hello.Task.Result["stacja"];
        return p;
    }

    void Wyslij(JsonObject m) { if (closed) return; try { lock (lk) { w.WriteLine(m.ToJsonString()); w.Flush(); } } catch { Utrata("błąd wysyłania"); } }

    /// <summary>Żądanie z odpowiedzią („res” o tym samym id); null – brak odpowiedzi w czasie.</summary>
    public Task<JsonNode> Zadanie(string t, params (string K, JsonNode V)[] pola)
    {
        var id = Interlocked.Increment(ref seq); var tcs = new TaskCompletionSource<JsonNode>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (waits) waits[id] = tcs;
        var m = new JsonObject { ["t"] = t, ["id"] = id };
        foreach (var (k, v) in pola) m[k] = v;
        Wyslij(m);
        _ = Task.Delay(6000).ContinueWith(_ => { lock (waits) waits.Remove(id); tcs.TrySetResult(null); });
        return tcs.Task;
    }

    void Czytaj()
    {
        try
        {
            using var r = new StreamReader(tcp.GetStream(), Encoding.UTF8);
            string line;
            while ((line = r.ReadLine()) != null)
            {
                JsonNode m; try { m = JsonNode.Parse(line); } catch { continue; }
                var t = (string)m["t"];
                long id = t == "serwis" ? 0 : t == "res" ? m["id"]?.GetValue<long>() ?? -1 : -1;
                if (id < 0) continue;
                TaskCompletionSource<JsonNode> tcs;
                lock (waits) { if (!waits.Remove(id, out tcs)) continue; }
                tcs.TrySetResult(t == "serwis" ? m : m["d"]);
            }
        }
        catch { }
        Utrata("symulator zamknął połączenie");
    }

    void Utrata(string why)
    {
        if (closed) return;
        closed = true;
        try { tcp?.Close(); } catch { }
        lock (waits) { foreach (var x in waits.Values) x.TrySetResult(null); waits.Clear(); }
        Rozlaczono?.Invoke(why);
    }

    public void Dispose() { if (closed) return; closed = true; try { tcp?.Close(); } catch { } }
}
