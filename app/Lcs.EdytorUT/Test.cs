using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Test automatyczny edytora (--test wynik.txt [zrzut.png]): operacje przez kontrolki okna i mysz płótna, jak użytkownik.
/// Wynik: lista sprawdzeń OK / BŁĄD i ostatnia linia „błędów: N”.
/// </summary>
public static partial class Test
{
    public static bool Running;
    static readonly StringBuilder log = new();
    static int errors;
    static string outPath;

    static void Ok(bool cond, string what)
    {
        log.AppendLine((cond ? "OK    " : "BŁĄD  ") + what);
        if (!cond) errors++;
    }

    public static void Fail(Exception ex)
    {
        errors++; log.AppendLine("WYJĄTEK " + ex);
        Finish();
    }

    static void Finish()
    {
        log.AppendLine($"błędów: {errors}");
        File.WriteAllText(outPath, log.ToString());
        Application.Current.Shutdown();
    }

    public static void Start(MainWindow w, string output, string shot)
    {
        Running = true; outPath = output;
        UtView.IgnoreKeyboard = true;
        if (shot != null) shotRails = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(shot))!, Path.GetFileNameWithoutExtension(shot) + "_szyny.png");
        if (shot != null) shotMap = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(shot))!, Path.GetFileNameWithoutExtension(shot) + "_mapka.png");
        OffScreen(w);
        w.ContentRendered += (s, e) => w.Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                w.SetSchemat(false);       // etapy 1–4 sprawdzają widok terenu (północ u góry)
                log.AppendLine("== etap 1: szkielet"); Run(w);
                log.AppendLine("== etap 2: podkłady"); await RunBg(w);
                log.AppendLine("== etap 3: tory"); await RunTory(w);
                log.AppendLine("== etap 4: rozjazdy"); await RunRozjazdy(w);
                log.AppendLine("== widok schematu (poprawka po etapie 4)"); await RunSchemat(w);
                if (shot != null) { await Settle(w); Save(w, shot); }
                log.AppendLine("== rozjazdy łukowe i wstawki międzyrozjazdowe"); await RunLukowe(w);
                log.AppendLine("== łącznik na łuku (łuk wyrównujący)"); await RunLacznik(w);
                log.AppendLine("== łuk z punktów"); await RunLukPunkty(w);
                log.AppendLine("== łuk o zmiennym promieniu (podział łuku)"); await RunLukKoszowy(w);
                log.AppendLine("== usuwanie kawałka toru, zaznaczanie kilku"); await RunZaznaczenie(w);
                log.AppendLine("== etap 5 cz. 2, 6, 7: odcinki, W4 / W5, perony, przejazdy, kilometraż, profil, kontrola, warstwy, wydruk"); await RunWyposazenie(w, shot);
            }
            catch (Exception ex) { errors++; log.AppendLine("WYJĄTEK " + ex); }
            Finish();
        }, DispatcherPriority.ApplicationIdle);
    }

    /// <summary>
    /// Okno testu poza ekranem i bez aktywowania – mysz i klawiatura osoby pracującej przy komputerze nie zmieniają wyniku
    /// (wcześniej obrót kółka nad oknem testu zmieniał przybliżenie zrzutu), a test nie zabiera okna.
    /// </summary>
    static void OffScreen(Window w)
    {
        w.WindowState = WindowState.Normal; w.Width = 1500; w.Height = 900;
        w.WindowStartupLocation = WindowStartupLocation.Manual; w.ShowActivated = false; w.ShowInTaskbar = false;
        w.Left = SystemParameters.VirtualScreenLeft - 3200; w.Top = SystemParameters.VirtualScreenTop;
    }

    public static void Shot(MainWindow w, string shot, string point, double mpp)
    {
        OffScreen(w);
        w.ContentRendered += (s, e) => w.Dispatcher.BeginInvoke(async () =>
        {
            if (point != null) { w.GoTo(point); if (mpp > 0) w.View.SetView(w.View.Center, mpp); }
            await Settle(w); Save(w, shot); Application.Current.Shutdown();
        }, DispatcherPriority.ApplicationIdle);
    }

    static void Save(Window w, string path)
    {
        w.UpdateLayout();
        var rtb = new RenderTargetBitmap((int)w.ActualWidth, (int)w.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(w);
        var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(rtb));
        using var f = File.Create(path); enc.Save(f);
    }

    static void Click(ButtonBase b) => b.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
    static void Type(TextBox t, string text) { t.Focus(); t.Text = text; t.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent)); }

    static void Run(MainWindow w)
    {
        var inv = CultureInfo.InvariantCulture;
        // --- przeliczenia PUWG 1992
        var p19 = Geo.ToPuwg92(52, 19);
        Ok(Math.Abs(p19.Y - 500000) < 0.001, $"południk 19° → Y = 500 000 m ({p19.Y.ToString("0.000", inv)})");
        double worst = 0;
        foreach (var (la, lo) in new[] { (49.0, 14.2), (54.8, 24.1), (51.7631, 19.5438), (50.06, 19.94), (53.43, 14.55), (52.23, 21.01) })
        {
            var q = Geo.ToPuwg92(la, lo); var (la2, lo2) = Geo.ToWgs84(q);
            var back = Geo.ToPuwg92(la2, lo2);
            worst = Math.Max(worst, XY.Dist(q, back));
        }
        Ok(worst < 0.0001, $"PUWG 1992 ↔ WGS84 tam i z powrotem – największa różnica {worst * 1000:0.000} mm");
        // skala na południku 19°: 1″ szerokości ≈ 30,9 m · 0,9993
        var a = Geo.ToPuwg92(52, 19); var b = Geo.ToPuwg92(52 + 1.0 / 3600, 19);
        Ok(Math.Abs(b.X - a.X - 30.9) < 0.1, $"1″ szerokości na 52° = {(b.X - a.X).ToString("0.00", inv)} m (ok. 30,9 m)");
        Ok(Geo.TryParsePoint("51,7631 19,5438", out var g1) && XY.Dist(g1, Geo.ToPuwg92(51.7631, 19.5438)) < 1e-6, "punkt z Google Maps (przecinki)");
        Ok(Geo.TryParsePoint("384120.5 537450", out var g2) && g2.X == 384120.5 && g2.Y == 537450, "punkt X Y w PUWG 1992");
        Ok(!Geo.TryParsePoint("abc", out _) && !Geo.TryParsePoint("12 34000000", out _), "zły punkt odrzucony");

        // --- nowy dokument
        var d = w.Doc;
        Ok(!d.Dirty && d.UndoName == null && d.P.Crs == "EPSG:2180", "nowy projekt: bez zmian, nic do cofnięcia, PUWG 1992");
        Ok(!w.UndoBtn.IsEnabled && !w.RedoBtn.IsEnabled, "przyciski Cofnij / Ponów wyłączone");

        // --- nazwa i skala rysunku przez panel
        Type(w.NameBox, "Stacja testowa");
        Ok(d.P.Name == "Stacja testowa" && d.Dirty && d.UndoName == "Nazwa obszaru", "nazwa wpisana w panelu → zmiana z cofaniem");
        Ok(w.Title.StartsWith("* Stacja testowa"), "tytuł okna: gwiazdka i nazwa");
        Type(w.NameBox, "Stacja testowa");
        Ok(d.UndoName == "Nazwa obszaru" && UndoDepth(w) == 1, "ta sama nazwa – bez nowej zmiany");
        Ok(d.P.DrawingScale == 2000 && w.ScaleBox.SelectedIndex == 0, "skala arkusza domyślnie 1:2000 / 1:500 (Ig-10 § 3)");
        w.ScaleBox.SelectedIndex = 1;
        Ok(d.P.DrawingScale == 4000 && d.UndoName == "Skala rysunku", "skala arkusza 1:4000 / 1:1000 z listy");
        Type(w.NotesBox, "uwaga 1");
        Ok(d.P.Notes == "uwaga 1", "uwagi");

        // --- cofnij / ponów
        w.DoUndo(); Ok(d.P.Notes == "" && d.P.DrawingScale == 4000, "Ctrl+Z: uwagi cofnięte");
        Click(w.UndoBtn); Ok(d.P.DrawingScale == 2000 && w.ScaleBox.SelectedIndex == 0, "Cofnij: skala 1:2000, lista w panelu też");
        Click(w.UndoBtn); Ok(d.P.Name == "Nowy obszar" && w.NameBox.Text == "Nowy obszar", "Cofnij: nazwa, pole w panelu też");
        Ok(!w.UndoBtn.IsEnabled && w.RedoBtn.IsEnabled, "nic więcej do cofnięcia, jest co ponowić");
        Ok(!d.Undo(), "kolejne cofnięcie nic nie robi");
        Click(w.RedoBtn); Click(w.RedoBtn);
        Ok(d.P.Name == "Stacja testowa" && d.P.DrawingScale == 4000 && d.P.Notes == "", "Ponów ×2: nazwa i skala wróciły");

        // --- widok: idź do, kółko myszy, przeciąganie
        w.View.Focus();
        w.GoToBox.Text = "51.7631, 19.5438"; Click(w.GoToBtn);
        var target = Geo.ToPuwg92(51.7631, 19.5438);
        Ok(XY.Dist(w.View.Center, target) < 1e-6 && Math.Abs(w.View.Mpp - 0.5) < 1e-12, $"Idź do: środek widoku w punkcie, 1 px = 0,5 m");
        Ok(!w.GoTo("coś"), "Idź do: zły tekst – komunikat, widok bez zmian");
        var sp = new Point(400, 250); var before = w.View.ToWorld(sp);
        w.View.ZoomAt(sp, 1 / 1.2); w.View.ZoomAt(sp, 1 / 1.2); w.View.ZoomAt(sp, 1.2);
        Ok(XY.Dist(before, w.View.ToWorld(sp)) < 1e-6 && Math.Abs(w.View.Mpp - 0.5 / 1.2) < 1e-9, "kółko myszy: punkt pod kursorem zostaje w miejscu");
        var c0 = w.View.Center; double m = w.View.Mpp;
        w.View.PanBy(new Vector(100, -40));
        Ok(Math.Abs(w.View.Center.Y - (c0.Y - 100 * m)) < 1e-6 && Math.Abs(w.View.Center.X - (c0.X - 40 * m)) < 1e-6, "przeciągnięcie w prawo i do góry: obraz jedzie za myszą");
        var s1 = w.View.ToScreen(target); var t2 = w.View.ToWorld(s1);
        Ok(XY.Dist(t2, target) < 1e-6, "ekran ↔ świat bez utraty dokładności przy współrzędnych 10⁵ m");
        foreach (var mm in new[] { 0.003, 0.05, 0.5, 7, 120, 1500 }) { w.View.SetView(target, mm); var (mi, ma) = w.View.GridSteps(); if (!(mi / mm >= 14 - 1e-9 && mi / mm < 70 && ma > mi)) { Ok(false, $"siatka przy {mm} m/px: {mi} / {ma}"); goto gridDone; } }
        Ok(true, "siatka: krok drobny ≥ 14 px, główny większy – od 3 mm/px do 1,5 km/px");
    gridDone:
        w.View.SetView(target, 1e-9); Ok(w.View.Mpp == UtView.MinMpp, "najbliższe przybliżenie ograniczone");
        w.View.SetView(target, 0.25);

        // --- zapis i odczyt
        var dir = Path.Combine(Path.GetTempPath(), "edytor_ut_test"); Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "test.ut.json"); if (File.Exists(file)) File.Delete(file);
        Ok(w.SaveDoc(false, file) && !d.Dirty && File.Exists(file), "zapis: plik jest, brak zmian do zapisania");
        Ok(!w.Title.StartsWith("*") && w.Title.Contains("test.ut.json"), "tytuł po zapisie bez gwiazdki, z nazwą pliku");
        var json = File.ReadAllText(file);
        Ok(json.Contains("\"crs\": \"EPSG:2180\"") && json.Contains("\"name\": \"Stacja testowa\""), "plik: układ i nazwa (polskie litery bez kodowania)");
        var l = UtDocument.Load(file);
        Ok(UtDocument.Serialize(l.P) == UtDocument.Serialize(d.P), "odczyt: projekt identyczny z zapisanym");
        Ok(Math.Abs(l.P.View.Mpp - 0.25) < 1e-12 && XY.Dist(new XY(l.P.View.X, l.P.View.Y), target) < 1e-6, "odczyt: widok zapamiętany");
        Type(w.NameBox, "Stacja testowa 2");
        Ok(w.SaveDoc(false) && File.Exists(file + ".bak") && File.ReadAllText(file + ".bak").Contains("\"Stacja testowa\""), "drugi zapis: poprzednia wersja w .bak");
        w.NewDoc();
        Ok(w.Doc.P.Name == "Nowy obszar" && XY.Dist(w.View.Center, target) < 1e-6, "Nowy: pusty projekt w miejscu widoku");
        Ok(w.OpenPath(file) && w.Doc.P.Name == "Stacja testowa 2" && !w.Doc.Dirty && w.NameBox.Text == "Stacja testowa 2", "Otwórz: projekt wczytany do okna");
        File.WriteAllText(Path.Combine(dir, "zly.ut.json"), "{ \"crs\": \"EPSG:4326\" }");
        Ok(!w.OpenPath(Path.Combine(dir, "zly.ut.json")) && w.Doc.P.Name == "Stacja testowa 2", "plik w innym układzie – odrzucony, otwarty projekt zostaje");
        w.GoTo("51.7631, 19.5438"); w.View.SetView(w.View.Center, 0.25);
    }

    static int UndoDepth(MainWindow w)
    {
        // liczba zmian do cofnięcia bez naruszania stanu: cofnij wszystko i ponów
        int n = 0; while (w.Doc.Undo()) n++;
        for (int i = 0; i < n; i++) w.Doc.Redo();
        return n;
    }
}
