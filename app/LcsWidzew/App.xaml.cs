using System.IO;
using System.Windows;

namespace LcsWidzew;

public partial class App : Application
{
    /// <summary>Plik układu: przy uruchomieniu z katalogu projektu – Data/station.json projektu (edycje nie giną przy kompilacji), inaczej kopia obok programu.</summary>
    public static string StationPath { get; set; } = FindStation();
    static string FindStation()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 6 && d != null; i++, d = d.Parent)
        {
            var p = Path.Combine(d.FullName, "Data", "station.json");
            if (File.Exists(Path.Combine(d.FullName, "LcsWidzew.csproj")) && File.Exists(p)) return p;
        }
        return Path.Combine(AppContext.BaseDirectory, "Data", "station.json");
    }

    public static string ErrorLog => Path.Combine(Path.GetDirectoryName(StationPath) ?? AppContext.BaseDirectory, "..", "bledy.log");
    public static void LogError(Exception ex, string where)
    {
        try { File.AppendAllText(ErrorLog, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {where}: {ex}\n\n"); } catch { }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        // --station <plik> – inny plik układu (np. kopia)
        var args = e.Args.ToList();
        int si = args.IndexOf("--station");
        if (si >= 0 && si + 1 < args.Count) { StationPath = Path.GetFullPath(args[si + 1]); args.RemoveRange(si, 2); }
        var A = args.ToArray();
        DispatcherUnhandledException += (s, a) =>
        {
            LogError(a.Exception, "UI");
            a.Handled = true;
            if (Current?.MainWindow?.IsVisible == true || Current?.Windows.Count > 0)
                MessageBox.Show("Wystąpił błąd – operacja została przerwana, program działa dalej.\n\n" + a.Exception.Message + "\n\nSzczegóły: " + Path.GetFullPath(ErrorLog), "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
        };
        if (A.Length > 0 && A[0] == "--selftest")
        {
            if (A.Length > 3 && A[3] == "wzorce") Sim.SelfTest.RunPatterns(StationPath, A[1], A[2]);
            else if (A.Length > 3 && A[3].StartsWith("tylko:")) Sim.SelfTest.RunScenario(StationPath, A[1], A[2], A[3][6..]);
            else if (A.Length > 2) Sim.SelfTest.RunScenario(StationPath, A[1], A[2]);
            else Sim.SelfTest.Run(StationPath, A.Length > 1 ? A[1] : "selftest.txt");
            Shutdown();
            return;
        }
        if (A.Length > 1 && A[0] == "--trasy")
        {
            // diagnostyka: tablica przebiegów pliku – wczytany wprost, przez edytor (StationDoc), po edycji szlaku
            string Dump(Model.Station s) => string.Join(Environment.NewLine, new Sim.Simulator(s).RouteTable.SelectMany(kv => kv.Value.Select(r => $"{kv.Key} {r.Id} [{r.SwText}]")).OrderBy(x => x));
            File.WriteAllText(A[1] + ".a.txt", Dump(Model.Station.Load(StationPath)));
            { var rj = (System.Text.Json.Nodes.JsonObject)System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(StationPath)); var rr = Model.TrackOrientation.Apply(rj); var r2 = Model.TrackOrientation.Apply(rj);
              File.WriteAllText(A[1] + ".o.txt", $"migracja: odwrócone {rr.Flipped}, sprzeczne węzły {rr.Conflicts.Count}: {string.Join(" ", rr.Conflicts)} | normalizacja po migracji: odwrócone {r2.Flipped}, sprzeczne {r2.Conflicts.Count}: {string.Join(" ", r2.Conflicts)}"); }
            var dd = Model.StationDoc.Load(StationPath); File.WriteAllText(A[1] + ".b.txt", Dump(dd.Build()));
            if (A.Length > 2) { dd.SetOdstepy(A[2], 2); File.WriteAllText(A[1] + ".c.txt", Dump(dd.Build())); }
            Shutdown(); return;
        }
        if (A.Length > 0 && A[0] == "--edtest")
        {
            Sim.EditorTest.Run(A.Length > 1 ? A[1] : "edtest.txt");
            Shutdown();
            return;
        }
        base.OnStartup(e);
        if (A.Length > 1 && A[0] == "--editshot")
        {
            var ed = new UI.EditorWindow(StationPath, null) { WindowState = WindowState.Normal, Width = 1600, Height = 950, NoPrompt = true };
            var shot = A[1];
            ed.ContentRendered += (a, b) => ed.Dispatcher.BeginInvoke(() =>
            {
                if (A.Length > 2 && A[2] == "script") File.WriteAllText(shot + ".txt", ed.Script());
                if (A.Length > 2 && A[2] == "demo") ed.DemoTools();
                if (A.Length > 3 && A[2] == "blockpanel") File.WriteAllText(shot + ".txt", ed.BlockPanelTest(A[3]));
                if (A.Length > 3 && A[2] == "block") File.WriteAllText(shot + ".txt", ed.BlockForTest(A[3], A.Length > 4 ? A[4] : null));
                if (A.Length > 2 && A[2] == "plan") ed.PlanForTest();
                if (A.Length > 2 && A[2] == "splitmerge") File.WriteAllText(shot + ".txt", ed.SplitMergeForTest());
                if (A.Length > 2 && A[2] == "planskew") File.WriteAllText(shot + ".txt", ed.PlanSkewForTest());
                if (A.Length > 3 && A[2] == "display") File.WriteAllText(shot + ".txt", ed.DisplayForTest(A[3]));
                if (A.Length > 2 && A[2] == "autoname") File.WriteAllText(shot + ".txt", ed.AutoNameForTest());
                if (A.Length > 3 && A[2] == "problems") File.WriteAllText(shot + ".txt", ed.ProblemsForTest(int.Parse(A[3])));
                if (A.Length > 2 && A[2] == "validate") File.WriteAllText(shot + ".txt", ed.ValidateText());
                if (A.Length > 2 && A[2] == "szlakdiag") File.WriteAllText(shot + ".txt", ed.SzlakDiag());
                if (A.Length > 2 && A[2] == "userszlak") File.WriteAllText(shot + ".txt", ed.UserSzlakTest());
                if (A.Length > 5 && A[2] == "normalize") File.WriteAllText(shot + ".txt", ed.NormalizeForTest(double.Parse(A[3], System.Globalization.CultureInfo.InvariantCulture), double.Parse(A[4], System.Globalization.CultureInfo.InvariantCulture), double.Parse(A[5], System.Globalization.CultureInfo.InvariantCulture)));
                if (A.Length > 2 && A[2] == "szlaki") File.WriteAllText(shot + ".txt", ed.ScriptSzlaki());
                if (A.Length > 2 && A[2] == "rozjazd") File.WriteAllText(shot + ".txt", ed.ScriptRozjazd());
                if (A.Length > 2 && A[2] == "szlaki2") File.WriteAllText(shot + ".txt", ed.ScriptSzlaki2());
                if (A.Length > 3 && A[2] == "respace") File.WriteAllText(shot + ".txt", ed.RespaceForTest(new Rect(-1400, 600, 900, 400), double.Parse(A[3], System.Globalization.CultureInfo.InvariantCulture)));
                if (A.Length > 6 && A[2] == "respace") ed.ZoomForTest(double.Parse(A[4], System.Globalization.CultureInfo.InvariantCulture), double.Parse(A[5], System.Globalization.CultureInfo.InvariantCulture), double.Parse(A[6], System.Globalization.CultureInfo.InvariantCulture));
                if (A.Length > 5 && A[2] == "zoom") ed.ZoomForTest(double.Parse(A[3], System.Globalization.CultureInfo.InvariantCulture), double.Parse(A[4], System.Globalization.CultureInfo.InvariantCulture), double.Parse(A[5], System.Globalization.CultureInfo.InvariantCulture));
                if (A.Length > 3 && A[2] == "select") ed.SelectForTest(A[3]);
                if (A.Length > 3 && A[2] == "autolayout") File.WriteAllText(shot + ".txt", ed.AutoLayoutForTest(A[3]));
                if (A.Length > 4 && A[2] == "bg") File.WriteAllText(shot + ".txt", ed.BgForTest(A[3], A[4]));
                if (A.Length > 3 && A[2] == "dclear") File.WriteAllText(shot + ".txt", ed.DClearForTest(A[3]));
                if (A.Length > 7 && A[2] == "podklad") File.WriteAllText(shot + ".txt", ed.BgGeoForTest(A[3], A[4], double.Parse(A[5], System.Globalization.CultureInfo.InvariantCulture), double.Parse(A[6], System.Globalization.CultureInfo.InvariantCulture), A[7]));
                if (A.Length > 5 && A[2] == "kafelki") File.WriteAllText(shot + ".txt", ed.TilesForTest(A[3], A[4], A[5], A.Length > 6 ? A[6] : null));
                if (A.Length > 4 && A[2] == "jakosc") File.WriteAllText(shot + ".txt", ed.QualityForTest(A[3], A[4]));
                if (A.Length > 3 && A[2] == "luki") File.WriteAllText(shot + ".txt", ed.CurvesForTest(A[3]));
                if (A.Length > 3 && A[2] == "testowo") File.WriteAllText(shot + ".txt", ed.TestowoForTest(A[3], A.Length > 4 ? double.Parse(A[4], System.Globalization.CultureInfo.InvariantCulture) : 4));
                if (A.Length > 3 && A[2] == "mijanka") File.WriteAllText(shot + ".txt", ed.MijankaForTest(A[3], A.Length > 4 ? double.Parse(A[4], System.Globalization.CultureInfo.InvariantCulture) : 0));
                if (A.Length > 3 && A[2] == "plantools") File.WriteAllText(shot + ".txt", ed.PlanToolsForTest(A[3]));
                if (A.Length > 3 && A[2] == "pomiar") File.WriteAllText(shot + ".txt", ed.MeasureForTest(A[3]));
                if (A.Length > 4 && A[2] == "ostrosc") File.WriteAllText(shot + ".txt", ed.DetailForTest(A[3], A[4]));
                if (A.Length > 4 && A[2] == "profil") File.WriteAllText(shot + ".txt", ed.ProfileForTest(A[3], A[4]));
                if (A.Length > 3 && A[2] == "bramka") File.WriteAllText(shot + ".txt", ed.GantryForTest(A[3]));
                if (A.Length > 3 && A[2] == "skala") File.WriteAllText(shot + ".txt", ed.ScaleForTest(A[3], A.Length > 4 ? A[4] : null));
                if (A.Length > 3 && A[2] == "deco") File.WriteAllText(shot + ".txt", ed.DecoForTest(A[3]));
                if (A.Length > 3 && A[2] == "dfuzz") File.WriteAllText(shot + ".txt", ed.DFuzz(int.Parse(A[3]), A.Length > 4 ? int.Parse(A[4]) : 1));
                if (A.Length > 3 && A[2] == "fuzz") File.WriteAllText(shot + ".txt", ed.Fuzz(int.Parse(A[3]), A.Length > 4 ? int.Parse(A[4]) : 1));
                ed.UpdateLayout();
                var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap((int)ed.ActualWidth, (int)ed.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                rtb.Render(ed);
                var enc = new System.Windows.Media.Imaging.PngBitmapEncoder(); enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                using (var f = File.Create(shot)) enc.Save(f);
                Shutdown();
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            ed.Show();
            return;
        }
        // zwykłe uruchomienie bez --station: wybór wersji układu (podstawowy albo projekt z Data/projekty)
        if (si < 0 && A.Length == 0)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var chosen = UI.LayoutChooser.Choose(StationPath);
            if (chosen == null) { Shutdown(); return; }
            StationPath = chosen;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
        }
        var w = new MainWindow();
        MainWindow = w;
        if (A.Length > 1 && A[0] == "--screenshot")
        {
            w.ShotPath = A[1]; w.WindowState = WindowState.Normal; w.Width = 1600; w.Height = 950;
            if (A.Length > 2) w.ShotSeconds = double.Parse(A[2], System.Globalization.CultureInfo.InvariantCulture);
            w.ShotCmds = A.Skip(3).ToArray();
        }
        w.Show();
    }
}
