using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Lcs.Model;

namespace Lcs.EdytorZobrazowania;

/// <summary>
/// Test automatyczny edytora zobrazowania (--test wynik.txt [zrzut.png]): operacje przez kontrolki okna, jak użytkownik.
/// Dane: mijanka z testu edytora UT (Testy/mijanka.ut.json – posterunek MJ, szlaki 1A i 2A, semafory z funkcjami).
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
        OffScreen(w);
        w.ContentRendered += (s, e) => w.Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                log.AppendLine("== etap Z1: szkielet"); await RunZ1(w, shot);
                log.AppendLine("== etap Z2: tory obrazu, łączniki"); await RunZ2(w, shot);
                log.AppendLine("== etap Z3: zwrotnice, sygnalizatory, kozły"); await RunZ3(w, shot);
                log.AppendLine("== etap 9: do symulatora"); await RunSymulator(w);
                log.AppendLine("== etap Z4: przerwy, opisy odcinków, numery torów, perony, przejazdy, szlaki, napisy"); await RunZ4(w, shot);
                log.AppendLine("== etap Z5: dwa posterunki na dwóch obrazach, obraz poglądowy, PNG"); await RunZ5(w, shot);
                log.AppendLine("== etap Z6: podgląd jak na pulpicie (kod pulpitu i komputer zależności symulatora)"); await RunZ6(w, shot);
            }
            catch (Exception ex) { errors++; log.AppendLine("WYJĄTEK " + ex); }
            Finish();
        }, DispatcherPriority.ApplicationIdle);
    }

    /// <summary>Okno poza ekranem i bez aktywowania – mysz osoby przy komputerze nie zmienia wyniku.</summary>
    static void OffScreen(Window w)
    {
        w.WindowState = WindowState.Normal; w.Width = 1500; w.Height = 900;
        w.WindowStartupLocation = WindowStartupLocation.Manual; w.ShowActivated = false; w.ShowInTaskbar = false;
        w.Left = SystemParameters.VirtualScreenLeft - 3200; w.Top = SystemParameters.VirtualScreenTop;
    }

    /// <summary>Sam zrzut okna z wczytanym plikiem: cały obraz, a z punktem „x,y” – powiększenie (domyślnie 1:1) ze środkiem w tym punkcie.</summary>
    public static void Shot(MainWindow w, string shot, string point, double zoom)
    {
        OffScreen(w);
        w.ContentRendered += (s, e) => w.Dispatcher.BeginInvoke(async () =>
        {
            await Settle(w);
            w.Sprawdz(); w.View.Fit();
            var xy = point?.Split(',', ';', ' ');
            if (xy?.Length == 2 && double.TryParse(xy[0], System.Globalization.CultureInfo.InvariantCulture, out var x) && double.TryParse(xy[1], System.Globalization.CultureInfo.InvariantCulture, out var y))
            {
                w.View.Actual();
                if (zoom > 0 && zoom != 1) w.View.ZoomAt(new Point(w.View.ActualWidth / 2, w.View.ActualHeight / 2), zoom);
                w.View.CenterOn(x, y);
            }
            await Settle(w); Save(w, shot); Application.Current.Shutdown();
        }, DispatcherPriority.ApplicationIdle);
    }

    static async Task Settle(Window w)
    {
        for (int i = 0; i < 3; i++) await w.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
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
    static IEnumerable<T> All<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T t) yield return t;
        foreach (var c in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var x in All<T>(c)) yield return x;
    }
    static ButtonBase Btn(DependencyObject root, string text) => All<ButtonBase>(root).FirstOrDefault(b => b.Content as string == text || (b.Content as string)?.StartsWith(text) == true);
    static string Hash(string file) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
    static List<TreeViewItem> Leaves(MainWindow w) => w.UtTree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>()).ToList();

    static async Task RunZ1(MainWindow w, string shot)
    {
        var dir = Path.Combine(Path.GetTempPath(), "edytor_zob_test"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        var ut = Path.Combine(dir, "mijanka.ut.json");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Testy", "mijanka.ut.json"), ut);
        var utHash = Hash(ut);

        // --- bez dokumentu
        Ok(w.Doc == null && !w.SaveBtn.IsEnabled && !w.UndoBtn.IsEnabled && w.Tabs.Children.Count == 0, "start bez pliku: puste płótno, Zapisz / Cofnij wyłączone, bez zakładek");

        // --- nowe zobrazowanie z UT
        Ok(w.NewFromUt(ut), "Nowy z projektu UT mijanki");
        var d = w.Doc; var post = d.Ut.Posts.Single();
        Ok(d.P.Obrazy.Count == 1 && w.Obraz.Nazwa == "Mijanka" && w.Obraz.Rodzaj == ZobObraz.Szczegolowy && w.Obraz.Posterunki.SequenceEqual(new[] { post.Id }),
            $"jeden obraz szczegółowy „{w.Obraz.Nazwa}” z posterunkiem {post.Kod}");
        Ok(w.Obraz.Szer == 1920 && w.Obraz.Wys == 1080 && d.P.Siatka == 8, "obraz 1920 × 1080 j., siatka 8 (decyzja § 9 pkt 2)");
        Ok(w.Title.StartsWith("* mijanka.zob.json") && w.Dirty && d.Path == null, $"tytuł: gwiazdka i nazwa pliku obok UT ({w.Title})");
        await Settle(w);

        // --- panel UT
        var exp = ZobUt.Obiekty(d.Ut, w.Obraz);
        var leaves = Leaves(w);
        Ok(leaves.Count == exp.Count && exp.Count > 0 && exp.All(x => !x.NaObrazie), $"panel UT: {exp.Count} obiektów, żaden jeszcze na obrazie");
        string Grp(string g) => string.Join(", ", exp.Where(x => x.Grupa == g).Select(x => x.Opis));
        Ok(exp.Count(x => x.Grupa == ZobUt.GTory) == 4 && Grp(ZobUt.GTory) == "tor 1 (gz), tor 2 (gd), tor łączący 1/2, tor łączący 3/4", $"tory: {Grp(ZobUt.GTory)}");
        Ok(Grp(ZobUt.GRozjazdy) == "rozjazd 1, rozjazd 2, rozjazd 3, rozjazd 4", $"rozjazdy: {Grp(ZobUt.GRozjazdy)}");
        Ok(Grp(ZobUt.GSygnalizatory).Contains("A – semafor wjazdowy") && Grp(ZobUt.GSygnalizatory).Contains("F – semafor wyjazdowy") && Grp(ZobUt.GSygnalizatory).Contains("S60 – semafor odstępowy")
            && exp.Count(x => x.Grupa == ZobUt.GSygnalizatory) == 4, $"sygnalizatory z funkcją z UT: {Grp(ZobUt.GSygnalizatory)}");
        Ok(exp.Count(x => x.Grupa == ZobUt.GKozly) == 2, "kozły: 2");
        Ok(Grp(ZobUt.GSzlaki) == "szlak 1A, szlak 2A", $"szlaki dochodzące do posterunku: {Grp(ZobUt.GSzlaki)}");
        var groups = w.UtTree.Items.OfType<TreeViewItem>().Select(g => g.Header as string).ToList();
        Ok(groups.Contains("Tory (0/4)") && groups.Contains("Rozjazdy (0/4)") && leaves.All(l => ((string)l.Header).EndsWith("– brak")), $"grupy z licznikami: {string.Join("; ", groups)}");
        Ok(w.UtHeader.Text == "Obiekty UT – MJ" && w.UtInfo.Text.StartsWith($"Na obrazie 0 z {exp.Count}"), $"nagłówek panelu UT ({w.UtHeader.Text})");
        leaves[0].IsSelected = true;
        Ok(w.LastMessage.Contains("nie ma go jeszcze na obrazie"), $"klik w obiekt UT – komunikat ({w.LastMessage})");

        // --- panel obrazu: nazwa, rodzaj, posterunki, wymiary, cofanie
        Type(w.NameBox, "Mijanka MJ");
        Ok(w.Obraz.Nazwa == "Mijanka MJ" && d.UndoName == "Nazwa obrazu" && (w.Tabs.Children[0] as ToggleButton)?.Content as string == "Mijanka MJ", "nazwa obrazu z panelu – zakładka też");
        var cb = All<CheckBox>(w.PostsPanel).Single();
        Ok(cb.Content as string == "MJ Mijanka" && cb.IsChecked == true, "posterunek MJ zaznaczony w panelu obrazu");
        cb.IsChecked = false; Click(cb);
        Ok(w.Obraz.Posterunki.Count == 0 && w.UtHeader.Text == "Obiekty UT – cały obszar", "posterunek zdjęty – obraz obejmuje cały obszar");
        w.DoUndo();
        Ok(w.Obraz.Posterunki.Count == 1 && w.UtHeader.Text == "Obiekty UT – MJ", "Ctrl+Z – posterunek wrócił");
        Type(w.WidthBox, "3843"); Type(w.HeightBox, "1080");
        Ok(w.Obraz.Szer == 3840 && w.Obraz.Wys == 1080, $"szerokość 3843 → 3840 (wielokrotność siatki; dwa monitory)");
        Type(w.WidthBox, "50");
        Ok(w.Obraz.Szer == 3840 && w.WidthBox.Text == "3840" && w.LastMessage.StartsWith("Wymiary obrazu"), "za mała szerokość – odrzucona z komunikatem");
        w.DoUndo();
        Ok(w.Obraz.Szer == 1920 && w.WidthBox.Text == "1920", "Ctrl+Z – 1920 j.");

        // --- zakładki obrazów
        Click(Btn(w.Tabs, "+ obraz poglądowy"));
        var pog = w.Obraz;
        Ok(d.P.Obrazy.Count == 2 && pog.Rodzaj == ZobObraz.Pogladowy && pog.Posterunki.Count == 1 && w.KindBox.SelectedIndex == 1, "+ obraz poglądowy: drugi obraz, cały obszar");
        Ok(w.Tabs.Children.OfType<ToggleButton>().Count() == 2 && (w.Tabs.Children[1] as ToggleButton)?.IsChecked == true, "dwie zakładki, poglądowy pokazany");
        Click(Btn(w.Tabs, "+ obraz szczegółowy"));
        Ok(d.P.Obrazy.Count == 3 && w.Obraz.Posterunki.Count == 0 && w.Obraz.Nazwa == "Obraz 3", "+ obraz szczegółowy: bez posterunków (wybiera człowiek)");
        Click(Btn(w.ImgPanel, "Usuń obraz"));
        Ok(d.P.Obrazy.Count == 2 && w.Current == pog.Id, "Usuń obraz – zostaje poprzedni");
        w.DoUndo();
        Ok(d.P.Obrazy.Count == 3, "Ctrl+Z – obraz wrócił");
        w.DoUndo();
        Ok(d.P.Obrazy.Count == 2, "Ctrl+Z – bez trzeciego obrazu");
        Click(w.Tabs.Children.OfType<ToggleButton>().First());
        Ok(w.Obraz.Nazwa == "Mijanka MJ" && w.View.Obraz == w.Obraz, "klik w zakładkę – obraz na płótnie");
        Click(w.RedoBtn);
        Ok(d.P.Obrazy.Count == 3, "Ponów – trzeci obraz");
        w.DoUndo();

        // --- płótno: dopasowanie, przybliżenie, przyciąganie do siatki
        await Settle(w);
        w.View.Fit();
        var tl = w.View.ToScreen(0, 0); var br = w.View.ToScreen(w.Obraz.Szer, w.Obraz.Wys);
        Ok(tl.X >= 0 && tl.Y >= 0 && br.X <= w.View.ActualWidth && br.Y <= w.View.ActualHeight && (br.X - tl.X > w.View.ActualWidth - 50 || br.Y - tl.Y > w.View.ActualHeight - 50),
            $"Cały obraz: mieści się w oknie ({w.View.Zoom * 100:0} %)");
        var sp = new Point(300, 200); var before = w.View.ToImage(sp);
        w.View.ZoomAt(sp, 1.2); w.View.ZoomAt(sp, 1.2);
        var after = w.View.ToImage(sp);
        Ok(Math.Abs(before.X - after.X) < 1e-9 && Math.Abs(before.Y - after.Y) < 1e-9, "kółko myszy: punkt pod kursorem zostaje w miejscu");
        var (gx, gy) = w.View.Snap(w.View.ToScreen(203.9, 99));
        Ok(gx == 200 && gy == 96, $"przyciąganie do siatki 8: (203,9; 99) → ({gx}; {gy})");
        Click(Btn(w, "1:1"));
        Ok(Math.Abs(w.View.Zoom - 1) < 1e-9, "1:1 – powiększenie 100 %");
        w.View.Fit();

        // --- zapis i odczyt
        Ok(w.SaveDoc(false), "Zapisz – plik obok UT");
        var zob = Path.Combine(dir, "mijanka.zob.json");
        Ok(File.Exists(zob) && d.Path == zob && !w.Dirty && !w.Title.StartsWith("*"), "plik mijanka.zob.json, tytuł bez gwiazdki");
        var json = File.ReadAllText(zob);
        Ok(json.Contains("\"ut\": \"mijanka.ut.json\"") && json.Contains("\"utZapis\"") && json.Contains("\"rodzaj\": \"pogladowy\"") && json.Contains("\"nazwa\": \"Mijanka MJ\""),
            "plik: UT względem pliku, czas zapisu UT, obrazy (polskie litery bez kodowania)");
        Ok(Hash(ut) == utHash, "UT bez zmian (tylko do odczytu)");
        var l = ZobDocument.Load(zob);
        Ok(ZobDocument.Serialize(l.P) == ZobDocument.Serialize(d.P) && !l.UtChanged, "odczyt: zobrazowanie identyczne z zapisanym, UT zgodny");
        Type(w.NameBox, "Mijanka");
        Ok(w.SaveDoc(false) && File.Exists(zob + ".bak") && File.ReadAllText(zob + ".bak").Contains("\"Mijanka MJ\""), "drugi zapis: poprzednia wersja w .bak");
        Ok(w.OpenPath(zob) && w.Doc.P.Obrazy.Count == 2 && w.Obraz.Nazwa == "Mijanka" && !w.Dirty, "Otwórz: zobrazowanie wczytane do okna");

        // --- zmiana UT po zapisie obrazu
        File.SetLastWriteTime(ut, DateTime.Now.AddMinutes(5));
        Ok(w.OpenPath(zob) && w.Doc.UtChanged && w.StUt.Text.StartsWith("⚠") && w.LastMessage.Contains("był zapisany po ostatnim zapisie"), "UT zapisany po obrazie – ostrzeżenie przy otwarciu");
        Ok(w.SaveDoc(false) && !w.Doc.UtChanged && !w.StUt.Text.StartsWith("⚠"), "zapis obrazu – zgodny z bieżącym UT");
        Click(w.ReloadUtBtn);
        Ok(w.LastMessage.StartsWith("Wczytano UT") && Leaves(w).Count == exp.Count, "Wczytaj UT ponownie – panel UT bez zmian");

        // --- błędy plików
        var bad = Path.Combine(dir, "brak_ut.zob.json");
        File.WriteAllText(bad, "{ \"version\": 1, \"ut\": \"nie_ma.ut.json\", \"obrazy\": [] }");
        Ok(!w.OpenPath(bad) && w.Doc.Path == zob && w.LastMessage.Contains("Nie ma projektu UT"), "zobrazowanie bez pliku UT – odrzucone, otwarte zostaje");
        Ok(w.OpenPath(ut) && w.Doc.Path == null && w.Obraz.Nazwa == "Mijanka", "Otwórz *.ut.json – nowe zobrazowanie do tego UT");
        Ok(Hash(ut) == utHash, "UT nadal bez zmian");
        w.OpenPath(zob);

        if (shot != null) { w.View.Fit(); await Settle(w); Save(w, shot); }
    }
}
