using System.Collections.Concurrent;
using System.Text.Json;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Xunit;

[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]

namespace Lcs.Logi.Testy;

internal static class Pom
{
    public static string NowyKatalog(string nazwa)
    {
        var d = Path.Combine(Path.GetTempPath(), "suk_logi_testy", nazwa + "_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(d);
        return d;
    }

    public static OpcjeLogow Opcje(string katalog, ILogEventSink sink = null) => new()
    {
        Aplikacja = "Test", Katalog = katalog, ZapisTestowy = sink,
        WolneMiejsce = () => (50, 100), OkresNadzoruMiejsca = TimeSpan.FromHours(1),
    };

    /// <summary>Wszystkie linie plików JSONL katalogu (kolejność: nazwa pliku, linia). Każda linia musi być jednym obiektem JSON.</summary>
    public static List<JsonElement> Rekordy(string katalog) => Linie(katalog).Select(Parsuj).ToList();

    public static List<string> Linie(string katalog)
    {
        var wynik = new List<string>();
        foreach (var f in Directory.GetFiles(katalog, "*.jsonl").OrderBy(x => x, StringComparer.Ordinal))
            foreach (var l in File.ReadAllText(f).Split('\n'))
                if (l.TrimEnd('\r').Length > 0) wynik.Add(l.TrimEnd('\r'));
        return wynik;
    }

    public static JsonElement Parsuj(string linia)
    {
        using var d = JsonDocument.Parse(linia);
        Assert.Equal(JsonValueKind.Object, d.RootElement.ValueKind);
        return d.RootElement.Clone();
    }

    public static string Tekst(this JsonElement e, string pole) => e.TryGetProperty(pole, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    public static long? Liczba(this JsonElement e, string pole) => e.TryGetProperty(pole, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : null;
    public static bool Ma(this JsonElement e, string pole) => e.TryGetProperty(pole, out _);
    public static long? Licznik(this JsonElement e, string nazwa) =>
        e.TryGetProperty("counters", out var c) && c.TryGetProperty(nazwa, out var v) ? v.GetInt64() : null;

    /// <summary>Katalog dowodów (zmienna SUK_DOWODY): kopie JSONL do walidacji schematem i pomiary.</summary>
    public static string Dowody => Environment.GetEnvironmentVariable("SUK_DOWODY");

    public static void Zachowaj(string katalog, string nazwa)
    {
        if (string.IsNullOrEmpty(Dowody)) return;
        var cel = Path.Combine(Dowody, "jsonl", nazwa + "_" + Environment.Version.Major);
        Directory.CreateDirectory(cel);
        foreach (var f in Directory.GetFiles(katalog, "*.jsonl")) File.Copy(f, Path.Combine(cel, Path.GetFileName(f)), true);
    }

    public static void Zachowaj(IEnumerable<string> linie, string nazwa)
    {
        if (string.IsNullOrEmpty(Dowody)) return;
        var cel = Path.Combine(Dowody, "jsonl", nazwa + "_" + Environment.Version.Major);
        Directory.CreateDirectory(cel);
        File.WriteAllText(Path.Combine(cel, "pamiec.jsonl"), string.Join("\n", linie) + "\n");
    }

    public static void Pomiar(string nazwa, string tekst)
    {
        if (string.IsNullOrEmpty(Dowody)) return;
        var cel = Path.Combine(Dowody, "pomiary");
        Directory.CreateDirectory(cel);
        File.AppendAllText(Path.Combine(cel, nazwa + "_net" + Environment.Version.Major + ".txt"), tekst + "\n");
    }

    public static string KatalogRepo()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "kontrakty", "logi", "katalog_zdarzen_v1.json"))) return d.FullName;
        return null;
    }
}

/// <summary>Sink testowy: formatuje rekord tym samym formatterem co plik (Compact + limit 8 KiB); opcjonalnie blokuje, opcjonalnie rzuca.</summary>
internal sealed class SinkTestowy : ILogEventSink
{
    public readonly ConcurrentQueue<string> Linie = new();
    public readonly ManualResetEventSlim Brama;
    public volatile bool Rzucaj;
    public int Wywolania;
    readonly OgraniczonyFormatter formatter = new(new CompactJsonFormatter(), KontraktLogu.MaxBajtowRekordu, null, null);

    public SinkTestowy(bool zablokowany = false) => Brama = new ManualResetEventSlim(!zablokowany);

    public void Emit(LogEvent e)
    {
        Interlocked.Increment(ref Wywolania);
        Brama.Wait();
        if (Rzucaj) throw new IOException("Symulowana awaria zapisu");
        var sw = new StringWriter();
        formatter.Format(e, sw);
        Linie.Enqueue(sw.ToString().TrimEnd('\r', '\n'));
    }

    public List<JsonElement> Rekordy() => Linie.Select(Pom.Parsuj).ToList();
}
