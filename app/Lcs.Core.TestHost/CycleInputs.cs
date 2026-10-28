using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using LcsWidzew.Model;
using LcsWidzew.Sim;
using Microsoft.Extensions.Logging;

namespace Lcs.Core.TestHost;

/// <summary>
/// Dowód rozsyłania zamrożonych wejść cyklu (S02A.02) na jawnej kopii układu: scenariusz bez odbiorców (pomiar odniesienia) i ten
/// sam scenariusz z dwoma testowymi odbiorcami każdego KZ. Sprawdza, że odbiorcy dostali identyczne strumienie (te same obiekty,
/// ta sama treść kanoniczna), numerację bez luk i brak naruszeń faz; zapisuje tabelę rozsyłania, początek strumieni (JSONL do
/// kontroli schematu) i raport z łańcuchem skrótów. Wyniki nie nadpisują istniejących plików, układu ani jego zależności.
/// Próba wprowadza jawne usterki instruktora i polecenia ONN/NN (fazy Usterka i Rozkaz), bez zmiany danych układu. Odbiorcy liczą
/// strumieniowo (łańcuch skrótów, liczniki, próbka JSONL) – pamięć nie rośnie z długością próby.
/// </summary>
internal static class CycleInputs
{
    const double Dt = 0.25;
    const int ProbkaJsonl = 50;

    /// <summary>
    /// Testowy odbiorca liczący strumieniowo: numeracja bez luk, partie i cykle rosnące, fazy. Odbiorca A liczy też łańcuch SHA-256
    /// treści kanonicznej i początek strumienia (JSONL); odbiorca B sprawdza, że każde wejście jest tym samym obiektem, który właśnie
    /// dostał A (ta sama treść – bez ponownego liczenia skrótu).
    /// </summary>
    sealed class Odbiorca(string kzId, string skrotTrasy, Odbiorca wzor = null) : IOdbiorcaWejscCyklu
    {
        public long Liczba, Statusy, OstatniNr;
        public bool BezLuk = true, Rosnace = true, Zgodny = true, Wlasciwe = true;
        public string Lancuch = new('0', 64);
        public WejscieCyklu Ostatnie;
        public readonly SortedDictionary<string, long> Fazy = new(StringComparer.Ordinal);
        public readonly StringBuilder Probka = new();
        public void Odbierz(WejscieCyklu w)
        {
            if (wzor != null && !ReferenceEquals(wzor.Ostatnie, w)) Zgodny = false;
            Liczba++; Statusy += w.Statusy.Length;
            if (w.NrWejscia != OstatniNr + 1) BezLuk = false;
            if (Ostatnie != null && (w.NrPartii <= Ostatnie.NrPartii || w.Cykl < Ostatnie.Cykl)) Rosnace = false;
            if (w.KzId != kzId || w.SkrotTrasy != skrotTrasy) Wlasciwe = false;
            OstatniNr = w.NrWejscia; Ostatnie = w;
            string nazwa = WejscieCyklu.NazwaFazy(w.Faza); Fazy[nazwa] = Fazy.GetValueOrDefault(nazwa) + 1;
            if (wzor != null) return;
            string kanon = w.Kanoniczny();
            Lancuch = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Lancuch + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(kanon))))));
            if (Liczba <= ProbkaJsonl) Probka.Append(kanon).Append('\n');
        }
    }

    internal static int Run(string stationPath, string outputDirectory, string scenario, double seconds, ILogger logger)
    {
        if (!Rozklad.Scenarios.Any(s => s.Key == scenario)) throw new ArgumentException("Nieznany scenariusz.");
        if (!double.IsFinite(seconds) || seconds < 1 || seconds > 24 * 3600) throw new ArgumentException("Czas próby 1 s – 24 h.");
        var protectedPaths = new[] { stationPath, Zaleznosci.PlikDla(stationPath) }.Where(p => p != null).Select(Path.GetFullPath).ToArray();
        if (protectedPaths.Any(p => p.StartsWith(Path.GetFullPath(outputDirectory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Katalog wyników nie może zawierać układu.");
        // katalog wyników powstaje od razu (przed symulacją) i musi być nowy, pusty i nie być dowiązaniem; pliki – tylko CreateNew
        if (Directory.Exists(outputDirectory) || File.Exists(outputDirectory)) throw new IOException("Katalog wyników już istnieje – użyj nowej ścieżki (dowody pozostają nienaruszone).");
        var katalog = Directory.CreateDirectory(outputDirectory);
        if (katalog.LinkTarget != null || katalog.EnumerateFileSystemInfos().Any()) throw new IOException("Katalog wyników nie jest nowym, pustym katalogiem.");

        // 0. rozgrzewka JIT (wynik pomijany) – czasy obu przebiegów są porównywalne
        _ = Przebieg(stationPath, scenario, Math.Min(seconds, 60), null, null);
        // 1. odniesienie: ten sam scenariusz bez odbiorców wejść
        var (bezOdbiorcow, czasBez, _) = Przebieg(stationPath, scenario, seconds, null, null);
        // 2. dwa testowe odbiorcy każdego KZ
        var odbiorcyA = new Dictionary<string, Odbiorca>(StringComparer.Ordinal);
        var odbiorcyB = new Dictionary<string, Odbiorca>(StringComparer.Ordinal);
        var (sim, czasZ, trasa) = Przebieg(stationPath, scenario, seconds, odbiorcyA, odbiorcyB, logger);

        Zapisz(Path.Combine(outputDirectory, "trasa.json"), trasa.Json);
        var opcje = new JsonWriterOptions { Indented = true, NewLine = "\n", Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) };
        bool zgodne = true;
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms, opcje))
        {
            w.WriteStartObject();
            w.WriteNumber("schemaVersion", 1);
            w.WriteString("kontrakt", "kz.wejscie_cyklu.raport");
            w.WriteString("scenariusz", scenario);
            w.WriteNumber("sekundy", seconds);
            w.WriteNumber("cykle", sim.NrCyklu);
            w.WriteString("skrotMapy", trasa.SkrotMapy);
            w.WriteString("skrotTrasy", trasa.Sha256);
            w.WriteStartArray("kz");
            foreach (var kz in trasa.KzIds)
            {
                var a = odbiorcyA[kz]; var b = odbiorcyB[kz];
                bool teSame = a.Liczba == b.Liczba && b.Zgodny && b.BezLuk && b.Wlasciwe;
                bool bezLuk = a.BezLuk && a.Liczba == sim.Wejscia.OstatniNrWejscia(kz);
                zgodne &= teSame && bezLuk && a.Rosnace && a.Wlasciwe;
                w.WriteStartObject();
                w.WriteString("kzId", kz);
                w.WriteNumber("wejscia", a.Liczba);
                w.WriteNumber("ostatniNrWejscia", sim.Wejscia.OstatniNrWejscia(kz));
                w.WriteNumber("statusy", a.Statusy);
                w.WriteStartObject("fazy");
                foreach (var (faza, n) in a.Fazy) w.WriteNumber(faza, n);
                w.WriteEndObject();
                w.WriteBoolean("odbiorcyZgodni", teSame);
                w.WriteBoolean("numeracjaBezLuk", bezLuk);
                w.WriteBoolean("kolejnoscRosnaca", a.Rosnace);
                w.WriteString("skrotLancucha", a.Lancuch);
                w.WriteEndObject();
                Zapisz(Path.Combine(outputDirectory, $"wejscia-{kz}.jsonl"), a.Probka.ToString());
            }
            w.WriteEndArray();
            w.WriteStartObject("liczniki");
            foreach (var (k, v) in sim.Wejscia.Liczniki.OrderBy(p => p.Key, StringComparer.Ordinal)) w.WriteNumber(k, v);
            w.WriteEndObject();
            bool czyste = sim.Wejscia.Liczniki["violations"] == 0 && sim.Wejscia.Liczniki["receiverFailures"] == 0;
            bool takieSame = DziennikZgodny(bezOdbiorcow, sim);
            zgodne &= czyste && takieSame;
            w.WriteBoolean("bezNaruszen", czyste);
            w.WriteBoolean("dziennikBezOdbiorcowZgodny", takieSame);
            w.WriteBoolean("zgodne", zgodne);
            w.WriteEndObject();
        }
        Zapisz(Path.Combine(outputDirectory, "raport.json"), Encoding.UTF8.GetString(ms.ToArray()) + "\n");
        // czasy osobno – raport.json pozostaje identyczny w powtórzeniach
        Zapisz(Path.Combine(outputDirectory, "czasy.txt"),
            $"bez_odbiorcow_ms={czasBez.TotalMilliseconds:0}\nz_odbiorcami_ms={czasZ.TotalMilliseconds:0}\ncykle={sim.NrCyklu}\n" +
            $"narzut_odbiorcow_na_cykl_us={(czasZ - czasBez).TotalMicroseconds / Math.Max(1, sim.NrCyklu):0.0}\n");
        Console.WriteLine($"Wejścia cyklu: {trasa.KzIds.Length} KZ, {sim.NrCyklu} cykli, zgodne={zgodne}; trasa {trasa.Sha256}");
        return zgodne ? 0 : 1;
    }

    static (Simulator Sim, TimeSpan Czas, TrasaWejsc Trasa) Przebieg(string stationPath, string scenario, double seconds,
        Dictionary<string, Odbiorca> a, Dictionary<string, Odbiorca> b, ILogger logger = null)
    {
        var sim = new Simulator(Station.Load(stationPath), options: new SimulatorOptions { WejscieCykluLogger = logger });
        sim.User = new User { Name = "test", Role = "dyżurny ruchu" };
        var trains = new Trains(sim); _ = Sygnaly.Tab; _ = trains.Tabor; trains.Load(scenario);
        var trasa = sim.Wejscia.Trasa ?? throw new InvalidOperationException("Brak mapy własności – nie ma tabeli rozsyłania.");
        if (a != null)
            foreach (var kz in trasa.KzIds)
            {
                a[kz] = new Odbiorca(kz, trasa.Sha256); b[kz] = new Odbiorca(kz, trasa.Sha256, a[kz]);
                sim.Wejscia.Subskrybuj(kz, a[kz]); sim.Wejscia.Subskrybuj(kz, b[kz]);    // A przed B – B porównuje z ostatnim A
            }
        string sekcja = sim.L.Sections.Values.Where(s => s.AxleCounter).Select(s => s.Id).OrderBy(x => x, StringComparer.Ordinal).FirstOrDefault();
        var watch = Stopwatch.StartNew();
        int ticks = (int)Math.Round(seconds / Dt);
        for (int i = 1; i <= ticks; i++)
        {
            sim.Tick(Dt);
            // jawne fazy natychmiastowe (bez zmiany danych układu): usterka zasilania i odcinka, ONN/NN
            if (i == 40) sim.Instruktor.ApplyFault("mains", null, false);
            if (i == 80) sim.Instruktor.ApplyFault("mains", null, true);
            if (i == 100 && sekcja != null) sim.Instruktor.ApplyFault("secOcc", sekcja, false);
            if (i == 120 && sekcja != null) sim.Instruktor.ApplyFault("secOcc", sekcja, true);
            if (i == 140) sim.Exec("ONN", true);
            if (i == 160) sim.Exec("NN", true);
        }
        watch.Stop();
        return (sim, watch.Elapsed, trasa);
    }

    /// <summary>Odbiorcy wejść nie mogą zmienić świata: dziennik zdarzeń i alarmów obu przebiegów jest identyczny.</summary>
    static bool DziennikZgodny(Simulator x, Simulator y) =>
        x.Events.Count == y.Events.Count && x.Alarms.Count == y.Alarms.Count && x.NrCyklu == y.NrCyklu &&
        x.Events.Zip(y.Events).All(p => p.First.Czas == p.Second.Czas && p.First.Zdarzenie == p.Second.Zdarzenie && p.First.Obiekt == p.Second.Obiekt);

    /// <summary>Zapis wyłącznie nowego pliku (CreateNew) w katalogu, który nadal jest zwykłym katalogiem (nie dowiązaniem podmienionym w trakcie próby). Zawęża okno TOCTOU; nadpisaniu zapobiega CreateNew; dowiązań w katalogach nadrzędnych nie sprawdza (lokalny host testowy).</summary>
    static void Zapisz(string path, string text)
    {
        var katalog = new DirectoryInfo(Path.GetDirectoryName(path)!);
        if (!katalog.Exists || katalog.LinkTarget != null) throw new IOException("Katalog wyników zmienił się w trakcie próby – zapis przerwany.");
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(text);
    }
}
