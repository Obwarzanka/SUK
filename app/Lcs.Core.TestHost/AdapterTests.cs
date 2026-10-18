using System.Text;
using System.Text.Json;
using LcsWidzew.Model;
using LcsWidzew.Sim;
using Microsoft.Extensions.Logging;

namespace Lcs.Core.TestHost;

/// <summary>Kontrole nowych granic, niezależne od pełnych scenariuszy regresji i bez zmian w źródłowym układzie.</summary>
internal static class AdapterTests
{
    internal static RegressionComparison.Result Run(string stationPath, string reportPath)
    {
        var report = new StringBuilder();
        int checkedCount = 0, failedCount = 0;
        void Check(bool valid, string label)
        {
            checkedCount++;
            if (!valid) failedCount++;
            report.AppendLine($"{(valid ? "OK" : "BŁĄD")}: {label}");
        }
        static bool Near(double first, double second) => Math.Abs(first - second) < 1e-9;

        var point = new Point2(-3.25, 10.125);
        Check(point.X == -3.25 && point.Y == 10.125 && point == new Point2(-3.25, 10.125), "Point2 zachowuje współrzędne ujemne i ułamkowe oraz równość wartości");
        var bounds = new Bounds2(new Point2(8, -4), new Point2(-2, 6));
        Check(bounds.Left == -2 && bounds.Top == -4 && bounds.Width == 10 && bounds.Height == 10 && bounds.Right == 8 && bounds.Bottom == 6,
            "Bounds2 normalizuje odwrotną kolejność narożników bez utraty rozmiaru");
        Check(bounds.Contains(new Point2(-2, -4)) && bounds.Contains(new Point2(8, 6)) && !bounds.Contains(new Point2(8.001, 0)),
            "Bounds2 obejmuje krawędzie i odrzuca punkt poza granicą");
        var zero = new Bounds2(point, point);
        Check(zero.Width == 0 && zero.Height == 0 && zero.Contains(point) && !zero.Contains(new Point2(point.X + 0.01, point.Y)),
            "Bounds2 rozróżnia punktowy prostokąt od punktu leżącego poza nim");
        var platform = new PlatformDef { X = 10, Y = 20, W = 4, H = 3, Rot = 90 };
        var corners = platform.Corners(); var rotatedBounds = platform.Bounds();
        Check(corners.Length == 4 && Near(corners[1].X, 10) && Near(corners[1].Y, 24) && Near(corners[2].X, 7) && Near(corners[2].Y, 24),
            "Obrót peronu o 90° zachowuje narożniki w neutralnej geometrii");
        Check(Near(rotatedBounds.Left, 7) && Near(rotatedBounds.Right, 10) && Near(rotatedBounds.Top, 20) && Near(rotatedBounds.Bottom, 24)
            && platform.Contains(new Point2(8.5, 22)) && !platform.Contains(new Point2(6, 22)), "Obrócony peron: bounds i trafienie punktu są zgodne");

        var color = RgbColor.FromRgb(0, 127, 255);
        Check(color.R == 0 && color.G == 127 && color.B == 255 && JsonSerializer.Deserialize<RgbColor>(JsonSerializer.Serialize(color)) == color,
            "RgbColor zachowuje kolejność i pełny zakres kanałów również przez JSON");
        Check(Trains.Cats["IC"].Color == RgbColor.FromRgb(255, 210, 127), "Paleta kategorii ruchu zachowuje dotychczasowy kolor IC");

        var station = Station.Load(stationPath);
        var recording = new RecordingLogger();
        var simulation = new Simulator(station, routes: false, options: new SimulatorOptions { RejestratorLogger = recording });
        simulation.User = new User { Name = "OPERATOR_TESTU_ADAPTERA", Role = "dyżurny ruchu" };
        var entry = simulation.Log("Uwaga", "TRESC_TESTU_ADAPTERA_NIE_DLA_DIAGNOSTYKI", "System", "obiekt-testu");
        Check(recording.Records.Count == 1 && recording.Records[0].Id.Id == CoreLogging.EventId && recording.Records[0].Id.Name == CoreLogging.EventCode,
            "Rejestrator korzysta z wstrzykniętego ILogger i stabilnego REJ.EVENT_RECORDED");
        Check(recording.Records.Count == 1 && Convert.ToInt64(recording.Records[0].Fields["rejId"]) == entry.Id
            && Equals(recording.Records[0].Fields["kategoria"], "Uwaga"), "Kopia techniczna koreluje kategorię i ID z wpisem domenowym");
        Check(recording.Records.SelectMany(x => x.Fields.Values).All(v => !Convert.ToString(v).Contains("TESTU_ADAPTERA", StringComparison.Ordinal))
            && entry.Zdarzenie.Contains("TRESC_TESTU_ADAPTERA", StringComparison.Ordinal), "Logger nie otrzymuje operatora ani treści wpisu, a dziennik domenowy je zachowuje");
        var noLogger = new Simulator(station, routes: false);
        var throwingLogger = new Simulator(station, routes: false, options: new SimulatorOptions { RejestratorLogger = new ThrowingLogger() });
        Check(noLogger.Log("Uwaga", "próba bez providera").Id == 1 && throwingLogger.Log("Uwaga", "próba awarii providera").Id == 1,
            "Brak providera i wyjątek w providerze nie blokują domenowego rejestratora");

        // Artefakty są wyłącznie tworzone obok raportu. Nie zapisujemy nic w katalogu układu wejściowego.
        string fixtureRoot = reportPath + ".dane-" + Guid.NewGuid().ToString("N");
        string dataA = Path.Combine(fixtureRoot, "A"), dataB = Path.Combine(fixtureRoot, "B");
        Directory.CreateDirectory(dataA); Directory.CreateDirectory(dataB);
        var block = station.Blocks.Values.First(x => !x.Internal);
        string post = block.Ends[0].Post;
        void WriteTimetable(string directory, string number) => File.WriteAllText(Path.Combine(directory, "rozklad.json"), JsonSerializer.Serialize(new
        {
            date = "próba adaptera", trains = new[] { new { num = number, cat = "IC", veh = "test", from = block.Id, to = block.Id, appear = 18001,
                unit = true, emptyIn = false, emptyOut = false, len = 90, revIdx = Array.Empty<int>(),
                stops = new[] { new { post, arr = 18001, dep = 18031, stop = true, track = "1", platform = "1" } } } }
        }));
        WriteTimetable(dataA, "TEST_A"); WriteTimetable(dataB, "TEST_B");
        var simA = new Simulator(station, routes: false, options: new SimulatorOptions { DataDirectory = dataA });
        var simB = new Simulator(station, routes: false, options: new SimulatorOptions { DataDirectory = dataB });
        simA.Rozklad.Load("ic_05"); simB.Rozklad.Load("ic_05"); simA.Rozklad.Load("ic_05");
        Check(simA.Rozklad.Queue.Any(q => q.Num == "TEST_A") && simA.Rozklad.Queue.All(q => q.Num != "TEST_B")
            && simB.Rozklad.Queue.Any(q => q.Num == "TEST_B") && simB.Rozklad.Queue.All(q => q.Num != "TEST_A"),
            "Dwa jawne DataDirectory w jednym procesie zachowują własne rozkłady przy przeplatanym wczytywaniu");
        string fixtureStation = Path.Combine(dataA, "station-fixture.json");
        File.Copy(stationPath, fixtureStation);
        var loaded = Station.Load(fixtureStation);
        var defaultDirectory = new Simulator(loaded, routes: false);
        var overrideDirectory = new Simulator(loaded, routes: false, options: new SimulatorOptions { DataDirectory = dataB });
        defaultDirectory.Rozklad.Load("ic_05"); overrideDirectory.Rozklad.Load("ic_05");
        Check(loaded.SourcePath == fixtureStation && defaultDirectory.Rozklad.Queue.Any(q => q.Num == "TEST_A")
            && overrideDirectory.Rozklad.Queue.Any(q => q.Num == "TEST_B"), "Station.SourcePath ustala domyślny katalog, jawne DataDirectory ma pierwszeństwo");
        Check(station.SourcePath == stationPath && simulation.DataDirectory == Path.GetDirectoryName(stationPath), "Tworzenie kolejnych światów nie zmienia źródła pierwszego układu");

        string reference = Path.Combine(fixtureRoot, "porownanie-baza.txt"), actual = Path.Combine(fixtureRoot, "porownanie-wynik.txt");
        File.WriteAllText(reference, "06:00:00 ODRZ powód\r\n"); File.WriteAllText(actual, "06:00:00 ODRZ powód\n");
        File.WriteAllText(reference + ".zdarzenia", "06:00:00|Polecenie|A\n06:00:01|Odrzucone|B\n");
        File.WriteAllText(actual + ".zdarzenia", "06:00:00|Polecenie|A\r\n06:00:01|Odrzucone|B\r\n");
        Check(RegressionComparison.Compare(reference, actual, true).Equal, "Porównanie semantyczne akceptuje różne końce linii");
        File.WriteAllText(actual, "06:00:00 OK powód\n");
        Check(!RegressionComparison.Compare(reference, actual, true).Equal, "Porównanie wykrywa zmianę odmowy na przyjęcie polecenia");
        File.WriteAllText(actual, "06:00:00 ODRZ powód\n");
        File.WriteAllText(actual + ".zdarzenia", "06:00:01|Odrzucone|B\n06:00:00|Polecenie|A\n");
        Check(!RegressionComparison.Compare(reference, actual, true).Equal, "Porównanie wykrywa zmianę kolejności zdarzeń");
        report.AppendLine("Dane prób: " + fixtureRoot);
        report.AppendLine($"TESTY GRANIC: {checkedCount - failedCount} z {checkedCount}; błędów: {failedCount}");
        return new RegressionComparison.Result(failedCount == 0, report.ToString());
    }

    sealed class RecordingLogger : ILogger
    {
        internal readonly List<(EventId Id, Dictionary<string, object> Fields)> Records = new();
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception exception, Func<TState, Exception, string> formatter) =>
            Records.Add((id, ((IEnumerable<KeyValuePair<string, object>>)state).ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal)));
    }

    sealed class ThrowingLogger : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception exception, Func<TState, Exception, string> formatter) => throw new IOException("Próba awarii providera");
    }
}
