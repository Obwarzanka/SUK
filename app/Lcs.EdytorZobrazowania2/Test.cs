using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Lcs.Model;

namespace Lcs.EdytorZobrazowania2;

/// <summary>
/// Test automatyczny nowego edytora zobrazowania (--test wynik.txt [zrzut.png]): operacje przez kontrolki okna i zdarzenia myszy
/// na płótnie, jak użytkownik (PLAN_EDYTOR_ZOBRAZOWANIA_V2.md § 10). Dane: mijanka z testu edytora UT (Testy/mijanka.ut.json).
/// Wynik: lista sprawdzeń OK / BŁĄD i ostatnia linia „błędów: N”.
/// </summary>
public static partial class Test
{
    public static bool Running;
    static readonly StringBuilder log = new();
    static int errors;
    static string outPath;

    internal static void Ok(bool cond, string what)
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
                log.AppendLine("== etap E1: szkielet, plik wersji 2, płótno, prowadnice, strefy"); await RunE1(w, shot == null ? null : Path.ChangeExtension(shot, null) + "_E1.png");
                log.AppendLine("== etap E2: pióro toru, rozjazdy, skrzyżowania, zaznaczanie, przesuwanie, kopiuj / wklej, rozsuń rzędy"); await RunE2(w, shot == null ? null : Path.ChangeExtension(shot, null) + "_E2.png");
                log.AppendLine("== etap E3: symbole z palety, opisy, obrót, PPM, uwagi"); await RunE3(w, shot == null ? null : Path.ChangeExtension(shot, null) + "_E3.png");
                log.AppendLine("== etap E4: przypinanie do UT, do wyjaśnienia, nieumieszczone, Sprawdź"); await RunE4(w, shot == null ? null : Path.ChangeExtension(shot, null) + "_E4.png");
                log.AppendLine("== etap E5: odczyt wersji 1, kompilacja, eksport do symulatora, podgląd pulpitu"); await RunE5(w, shot == null ? null : Path.ChangeExtension(shot, null) + "_E5.png");
                log.AppendLine("== etap E6: szlaki – odstępy SBL, koniec szlaku, okna numeru pociągu"); await RunE6(w, shot == null ? null : Path.ChangeExtension(shot, null) + "_E6.png");
                log.AppendLine("== szlak wewnętrzny obszaru LCS (lcs_testowe: 1GW między TG i TW)"); await RunSzlakWewnetrzny(w, shot == null ? null : Path.ChangeExtension(shot, null) + "_lcs.png");
                log.AppendLine("== etap E7: klocki, własne klocki, szkic na życzenie"); await RunE7(w, shot == null ? null : Path.ChangeExtension(shot, null) + "_E7.png");
                log.AppendLine("== etap E8: wiele obrazów, obraz poglądowy, PNG i wydruk"); await RunE8(w, shot == null ? null : Path.ChangeExtension(shot, null) + "_E8.png");
                log.AppendLine("== tablica przebiegów (ROZWOJ.md etap 3): propozycje, przyjęcie, zasadniczy, Sprawdź, test, eksport"); await RunPrzebiegi(w, shot == null ? null : Path.ChangeExtension(shot, null) + "_przebiegi.png");
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

    /// <summary>Sam zrzut okna z wczytanym plikiem (cały obraz).</summary>
    public static void Shot(MainWindow w, string shot)
    {
        OffScreen(w);
        w.ContentRendered += (s, e) => w.Dispatcher.BeginInvoke(async () =>
        {
            await Settle(w); w.View.Fit(); await Settle(w);
            Save(w, shot); Application.Current.Shutdown();
        }, DispatcherPriority.ApplicationIdle);
    }

    internal static async Task Settle(Window w)
    {
        for (int i = 0; i < 3; i++) await w.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
    }

    internal static void Save(Window w, string path)
    {
        w.UpdateLayout();
        var rtb = new RenderTargetBitmap((int)w.ActualWidth, (int)w.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(w);
        var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(rtb));
        using var f = File.Create(path); enc.Save(f);
    }

    internal static void Click(ButtonBase b) => b.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
    internal static void Type(TextBox t, string text) { t.Focus(); t.Text = text; t.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent)); }
    internal static void Key(MainWindow w, Key k)
    {
        var src = PresentationSource.FromVisual(w);
        w.View.Focus();
        w.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, src, 0, k) { RoutedEvent = UIElement.PreviewKeyDownEvent });
    }
    static IEnumerable<T> All<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T t) yield return t;
        foreach (var c in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var x in All<T>(c)) yield return x;
    }
    static ButtonBase Btn(DependencyObject root, string text) => All<ButtonBase>(root).FirstOrDefault(b => b.Content as string == text || (b.Content as string)?.StartsWith(text) == true);
    static string Hash(string file) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
    static string Rz(MainWindow w) => string.Join(" ", w.Obraz.Rzedy);

    static async Task RunE1(MainWindow w, string shot)
    {
        var dir = Path.Combine(Path.GetTempPath(), "edytor_zob2_test"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        var ut = Path.Combine(dir, "mijanka.ut.json");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Testy", "mijanka.ut.json"), ut);
        var utHash = Hash(ut);

        // --- bez dokumentu
        Ok(w.Doc == null && !w.SaveBtn.IsEnabled && !w.UndoBtn.IsEnabled && w.Tabs.Children.Count == 0 && !w.ToolBtns[ObrTool.Prowadnica].IsEnabled,
            "start bez pliku: puste płótno, Zapisz / Cofnij / narzędzia wyłączone, bez zakładek");
        Ok(w.Side.SelectedItem == w.TabObraz && w.Side.Items.Count == 4, "prawy panel: cztery zakładki (Właściwości, Do wyjaśnienia, UT, Obraz)");

        // --- nowe zobrazowanie z UT
        Ok(w.NewFromUt(ut), "Nowy z projektu UT mijanki");
        var d = w.Doc; var post = d.Ut.Posts.Single();
        Ok(d.P.Version == 2 && d.P.Obrazy.Count == 1 && w.Obraz.Nazwa == "Mijanka" && w.Obraz.Rodzaj == ZobObraz.Szczegolowy && w.Obraz.Posterunki.SequenceEqual(new[] { post.Id }),
            $"wersja 2, jeden obraz szczegółowy „{w.Obraz.Nazwa}” z posterunkiem {post.Kod}");
        Ok(w.Obraz.Szer == 1920 && w.Obraz.Wys == 1080 && d.P.Siatka == 8, "obraz 1920 × 1080 j., siatka 8 (§ 0 pkt 6)");
        Ok(w.Obraz.Strefy.Count == 1 && w.Obraz.Strefy[0].Rodzaj == ObrStrefa.Polecenia && w.Obraz.Strefy[0].Prost.SequenceEqual(new[] { 1440, 0, 480, 160 }),
            "strefa domyślna: panel Polecenia / Historia 480 × 160 w prawym górnym rogu (§ 11 pkt 7)");
        Ok(w.Obraz.Wezly.Count == 0 && w.Obraz.Odcinki.Count == 0 && w.Obraz.Elementy.Count == 0 && w.Obraz.Przypiecia.Count == 0, "pusty graf rysunku, bez przypięć");
        Ok(w.Title.StartsWith("* mijanka.zob.json") && w.Dirty && d.Path == null, $"tytuł: gwiazdka i nazwa pliku obok UT ({w.Title})");
        Ok(w.ToolBtns[ObrTool.Wskaz].IsChecked == true && w.ToolBtns[ObrTool.Prowadnica].IsEnabled, "narzędzie Wskaż wybrane");
        await Settle(w);

        // --- zakładka Obraz: nazwa, posterunki, wymiary, cofanie
        Type(w.NameBox, "Mijanka MJ");
        Ok(w.Obraz.Nazwa == "Mijanka MJ" && d.UndoName == "Nazwa obrazu" && (w.Tabs.Children[0] as ToggleButton)?.Content as string == "Mijanka MJ", "nazwa obrazu z panelu – zakładka też");
        var cb = All<CheckBox>(w.PostsPanel).Single();
        Ok(cb.Content as string == "MJ Mijanka" && cb.IsChecked == true, "posterunek MJ zaznaczony");
        cb.IsChecked = false; Click(cb);
        Ok(w.Obraz.Posterunki.Count == 0, "posterunek zdjęty");
        w.DoUndo();
        Ok(w.Obraz.Posterunki.Count == 1, "Ctrl+Z – posterunek wrócił");
        Type(w.WidthBox, "3843"); Type(w.HeightBox, "1080");
        Ok(w.Obraz.Szer == 3840, "szerokość 3843 → 3840 (wielokrotność siatki)");
        Type(w.WidthBox, "50");
        Ok(w.Obraz.Szer == 3840 && w.WidthBox.Text == "3840" && w.LastMessage.StartsWith("Wymiary obrazu"), "za mała szerokość – odrzucona z komunikatem");
        w.DoUndo();
        Ok(w.Obraz.Szer == 1920 && w.WidthBox.Text == "1920", "Ctrl+Z – 1920 j.");

        // --- płótno: dopasowanie, przybliżenie, przyciąganie do siatki
        await Settle(w);
        w.View.Fit();
        var tl = w.View.ToScreen(0, 0); var br = w.View.ToScreen(w.Obraz.Szer, w.Obraz.Wys);
        Ok(tl.X >= 0 && tl.Y >= 0 && br.X <= w.View.ActualWidth && br.Y <= w.View.ActualHeight, $"Cały obraz: mieści się w oknie ({w.View.Zoom * 100:0} %)");
        var sp = new Point(300, 200); var before = w.View.ToImage(sp);
        w.View.ZoomAt(sp, 1.2); w.View.ZoomAt(sp, 1.2);
        var after = w.View.ToImage(sp);
        Ok(Math.Abs(before.X - after.X) < 1e-9 && Math.Abs(before.Y - after.Y) < 1e-9, "kółko myszy: punkt pod kursorem zostaje w miejscu");
        var (gx, gy) = w.View.Snap(w.View.ToScreen(203.9, 99));
        Ok(gx == 200 && gy == 96, $"przyciąganie do siatki 8: (203,9; 99) → ({gx}; {gy})");
        Click(Btn(w, "1:1"));
        Ok(Math.Abs(w.View.Zoom - 1) < 1e-9, "1:1 – powiększenie 100 %");
        w.View.Fit();
        var off = w.View.Off;
        w.View.SimDrag(w.View.ToScreen(600, 600), w.View.ToScreen(700, 650), MouseButton.Middle);
        Ok(w.View.Off != off, "środkowy przycisk: przeciągnięcie przesuwa widok");
        Ok(w.Obraz.Rzedy.Count == 0, "przesunięcie widoku nie zmienia obrazu");
        w.View.Fit();

        // --- prowadnice rzędów narzędziem (klawisz R, kliki na płótnie)
        Key(w, System.Windows.Input.Key.R);
        Ok(w.View.Tool == ObrTool.Prowadnica && w.ToolBtns[ObrTool.Prowadnica].IsChecked == true, "klawisz R – narzędzie Prowadnica");
        w.View.SimClick(w.View.ToScreen(500, 343));
        w.View.SimClick(w.View.ToScreen(800, 450));
        w.View.SimClick(w.View.ToScreen(900, 713));
        Ok(Rz(w) == "344 448 712" && w.RzedyText.Text == "344 448 712", $"trzy kliki – prowadnice w rzędnych siatki ({Rz(w)})");
        w.View.SimClick(w.View.ToScreen(500, 2000));
        Ok(Rz(w) == "344 448 712", "klik poza obrazem – bez prowadnicy");
        w.View.SimDrag(w.View.ToScreen(300, 448), w.View.ToScreen(300, 455));
        Ok(Rz(w) == "344 456 712" && d.UndoName == "Prowadnica 448 → 456", $"przeciągnięcie prowadnicy 448 → 456 ({Rz(w)})");
        w.View.SimClick(w.View.ToScreen(1200, 344), MouseButton.Right);
        Ok(Rz(w) == "456 712" && w.LastMessage.StartsWith("Usunięto prowadnicę 344"), "PPM na prowadnicy – usunięta");
        w.View.SimDrag(w.View.ToScreen(300, 712), w.View.ToScreen(300, 1200));
        Ok(Rz(w) == "456", "przeciągnięcie poza obraz – usunięta");
        w.DoUndo(); w.DoUndo();
        Ok(Rz(w) == "344 456 712", "Ctrl+Z ×2 – prowadnice wróciły");
        Type(w.RzedyText, "816, 344;712 100 101");
        Ok(Rz(w) == "96 104 344 712 816", $"pole prowadnic: siatka, bez powtórzeń, rosnąco ({Rz(w)})");
        Type(w.RzedyText, "344 abc");
        Ok(Rz(w) == "96 104 344 712 816" && w.RzedyText.Text == "96 104 344 712 816" && w.LastMessage.StartsWith("Prowadnice:"), "błędny tekst – odrzucony z komunikatem");
        Type(w.RzedyText, "344 448 712 816");
        Key(w, System.Windows.Input.Key.Escape);
        Ok(w.View.Tool == ObrTool.Wskaz, "Esc – Wskaż");
        w.View.SimClick(w.View.ToScreen(500, 600));
        Ok(Rz(w) == "344 448 712 816", "klik narzędziem Wskaż nie dodaje prowadnicy");

        // --- strefy zarezerwowane
        Type(w.StrefyBoxes[0], "1400, 0, 519, 163");
        Ok(w.Obraz.Strefy[0].Prost.SequenceEqual(new[] { 1400, 0, 520, 160 }), $"strefa: prostokąt z pola, do siatki ({string.Join(",", w.Obraz.Strefy[0].Prost)})");
        Type(w.StrefyBoxes[0], "1800, 0, 480, 160");
        Ok(w.Obraz.Strefy[0].Prost[0] == 1400 && w.LastMessage.StartsWith("Strefa:"), "strefa poza obrazem – odrzucona");
        Type(w.StrefyBoxes[0], "1440, 0, 480, 160");
        Click(Btn(w.ImgPanel, "+ strefa"));
        Ok(w.Obraz.Strefy.Count == 2 && w.StrefyBoxes.Count == 2 && w.Obraz.Strefy[1].Rodzaj == ObrStrefa.Inna, "+ strefa – druga strefa");
        Type(w.StrefyBoxes[1], "1440, 160, 480, 48");
        var zeg = All<ComboBox>(w.StrefyPanel).ElementAt(1); zeg.SelectedIndex = 1;
        Ok(w.Obraz.Strefy[1].Rodzaj == ObrStrefa.Zegar && w.Obraz.Strefy[1].Prost[1] == 160, "druga strefa: zegar i serwery pod panelem poleceń");
        var dels = All<Button>(w.StrefyPanel).Where(b => b.Content as string == "✕").ToList();
        Click(dels[1]);
        Ok(w.Obraz.Strefy.Count == 1, "✕ – strefa usunięta");
        w.DoUndo();
        Ok(w.Obraz.Strefy.Count == 2, "Ctrl+Z – strefa wróciła");

        // --- zakładki obrazów
        Click(Btn(w.Tabs, "+ obraz poglądowy"));
        var pog = w.Obraz;
        Ok(d.P.Obrazy.Count == 2 && pog.Rodzaj == ZobObraz.Pogladowy && pog.Posterunki.Count == 1 && pog.Strefy.Count == 1 && pog.Rzedy.Count == 0 && w.KindBox.SelectedIndex == 1,
            "+ obraz poglądowy: drugi obraz, cały obszar, własne prowadnice i strefy");
        Click(Btn(w.Tabs, "+ obraz szczegółowy"));
        Ok(d.P.Obrazy.Count == 3 && w.Obraz.Posterunki.Count == 0 && w.Obraz.Nazwa == "Obraz 3", "+ obraz szczegółowy: bez posterunków");
        Click(Btn(w.ImgPanel, "Usuń obraz"));
        Ok(d.P.Obrazy.Count == 2 && w.Current == pog.Id, "Usuń obraz – zostaje poprzedni");
        w.DoUndo();
        Ok(d.P.Obrazy.Count == 3, "Ctrl+Z – obraz wrócił");
        w.DoUndo();
        Ok(d.P.Obrazy.Count == 2, "Ctrl+Z – bez trzeciego obrazu");
        Click(w.Tabs.Children.OfType<ToggleButton>().First());
        Ok(w.Obraz.Nazwa == "Mijanka MJ" && w.View.Obraz == w.Obraz && Rz(w) == "344 448 712 816", "klik w zakładkę – obraz z prowadnicami na płótnie");

        // --- zakładka UT
        w.Side.SelectedItem = w.TabUt; await Settle(w);
        var utText = string.Join(" | ", All<TextBlock>(w.UtPanel).Select(t => t.Text));
        Ok(utText.Contains("MJ Mijanka") && utText.Contains("Szlaki: 1A, 2A") && utText.Contains("tylko do odczytu"), $"zakładka UT: posterunek, szlaki, tylko do odczytu");
        w.Side.SelectedItem = w.TabObraz;

        // --- zapis i odczyt
        Ok(w.SaveDoc(false), "Zapisz – plik obok UT");
        var zob = Path.Combine(dir, "mijanka.zob.json");
        Ok(File.Exists(zob) && d.Path == zob && !w.Dirty && !w.Title.StartsWith("*"), "plik mijanka.zob.json, tytuł bez gwiazdki");
        var json = File.ReadAllText(zob);
        Ok(json.Contains("\"version\": 2") && json.Contains("\"ut\": \"mijanka.ut.json\"") && json.Contains("\"utZapis\"") && json.Contains("\"rzedy\"") && json.Contains("\"strefy\"")
            && json.Contains("\"rodzaj\": \"polecenia\"") && json.Contains("\"wezly\"") && json.Contains("\"przypiecia\"") && json.Contains("\"nazwa\": \"Mijanka MJ\""),
            "plik: wersja 2, UT względem pliku, prowadnice, strefy, graf i przypięcia (polskie litery bez kodowania)");
        Ok(Hash(ut) == utHash, "UT bez zmian (tylko do odczytu)");
        var l = ObrDocument.Load(zob);
        Ok(ObrDocument.Serialize(l.P) == ObrDocument.Serialize(d.P) && !l.UtChanged, "odczyt: zobrazowanie identyczne z zapisanym, UT zgodny");
        Type(w.NameBox, "Mijanka");
        Ok(w.SaveDoc(false) && File.Exists(zob + ".bak") && File.ReadAllText(zob + ".bak").Contains("\"Mijanka MJ\""), "drugi zapis: poprzednia wersja w .bak");
        Ok(w.OpenPath(zob) && w.Doc.P.Obrazy.Count == 2 && w.Obraz.Nazwa == "Mijanka" && Rz(w) == "344 448 712 816" && w.Obraz.Strefy.Count == 2 && !w.Dirty, "Otwórz: zobrazowanie wczytane do okna");
        string staryBlad = null;
        try { ZobDocument.Load(zob); } catch (InvalidDataException ex) { staryBlad = ex.Message; }
        Ok(staryBlad?.Contains("EdytorZobrazowania2") == true, $"stary edytor: plik wersji 2 odrzucony z komunikatem ({staryBlad})");

        // --- zmiana UT po zapisie obrazu
        File.SetLastWriteTime(ut, DateTime.Now.AddMinutes(5));
        Ok(w.OpenPath(zob) && w.Doc.UtChanged && w.StUt.Text.StartsWith("⚠") && w.LastMessage.Contains("był zapisany po ostatnim zapisie"), "UT zapisany po obrazie – ostrzeżenie przy otwarciu");
        Ok(w.SaveDoc(false) && !w.Doc.UtChanged && !w.StUt.Text.StartsWith("⚠"), "zapis obrazu – zgodny z bieżącym UT");
        Click(w.ReloadUtBtn);
        Ok(w.LastMessage.StartsWith("Wczytano UT"), "Wczytaj UT ponownie");

        // --- plik starego edytora (wersja 1) i błędy plików
        var v1ut = Path.Combine(dir, "mijanka_testowa.ut.json"); var v1 = Path.Combine(dir, "mijanka_testowa.zob.json");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Testy", "mijanka_testowa.ut.json"), v1ut);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Testy", "mijanka_testowa.zob.json"), v1);
        var v1Hash = Hash(v1);
        Ok(w.OpenPath(v1) && w.Doc.Path == null && w.Doc.ZWersji1 == v1 && w.Doc.DefaultPath.EndsWith("mijanka_testowa.v2.zob.json") && w.LastMessage.Contains("wersja 1"),
            $"plik wersji 1 – przekształcony do wersji 2, propozycja nazwy *.v2.zob.json ({w.LastMessage})");
        Ok(w.SaveDoc(false) && File.Exists(Path.ChangeExtension(v1, null)[..^4] + ".v2.zob.json") && Hash(v1) == v1Hash, "zapis obok jako mijanka_testowa.v2.zob.json – plik wersji 1 bez zmian");
        Ok(w.OpenPath(v1ut) && w.Doc.Path == null, "nowe zobrazowanie obok pliku wersji 1");
        Ok(!w.SaveDoc(false) && Hash(v1) == v1Hash && w.LastMessage.Contains("starego edytora"), "zapis nie nadpisuje pliku wersji 1 bez pytania");
        var bad = Path.Combine(dir, "brak_ut.zob.json");
        File.WriteAllText(bad, "{ \"version\": 2, \"ut\": \"nie_ma.ut.json\", \"obrazy\": [] }");
        w.OpenPath(zob);
        Ok(!w.OpenPath(bad) && w.Doc.Path == zob && w.LastMessage.Contains("Nie ma projektu UT"), "zobrazowanie bez pliku UT – odrzucone, otwarte zostaje");
        Ok(w.OpenPath(ut) && w.Doc.Path == null && w.Obraz.Nazwa == "Mijanka", "Otwórz *.ut.json – nowe zobrazowanie do tego UT");
        Ok(Hash(ut) == utHash, "UT nadal bez zmian");
        w.OpenPath(zob);

        if (shot != null) { w.SetTool(ObrTool.Prowadnica); Ok(w.ToolBtns[ObrTool.Wskaz].IsChecked == false && w.ToolBtns[ObrTool.Prowadnica].IsChecked == true, "paleta: zaznaczone tylko wybrane narzędzie"); w.View.Fit(); await Settle(w); w.View.Move(w.View.ToScreen(700, 448)); await Settle(w); Save(w, shot); }
    }
}
