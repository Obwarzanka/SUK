using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Lcs.Tabor;

namespace Lcs.EdytorTaboru;

/// <summary>
/// Test automatyczny edytora taboru (--test wynik.txt [zrzut.png]): operacje przez kontrolki okna, jak użytkownik, na kopii wspólnego katalogu.
/// Nowa seria SM42 (lokomotywa spalinowa) wpisana w formularzu z krzywą „Z mocy”, skład SM42 + 3 × 111A zbudowany przyciskami,
/// zmiana oznaczenia serii z poprawą składów, odmowy, „Sprawdź”, cofanie, zapis i odczyt. Ostatnia linia „błędów: N”.
/// </summary>
public static class Test
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

    public static void Fail(Exception ex) { errors++; log.AppendLine("WYJĄTEK " + ex); Finish(); }

    static void Finish()
    {
        log.AppendLine($"błędów: {errors}");
        File.WriteAllText(outPath, log.ToString());
        Application.Current.Shutdown();
    }

    public static void Start(MainWindow w, string output, string shot)
    {
        Running = true; outPath = output;
        w.WindowState = WindowState.Normal; w.Width = 1400; w.Height = 900;
        w.WindowStartupLocation = WindowStartupLocation.Manual; w.ShowActivated = false; w.ShowInTaskbar = false;
        w.Left = SystemParameters.VirtualScreenLeft - 3200; w.Top = SystemParameters.VirtualScreenTop;
        w.ContentRendered += (s, e) => w.Dispatcher.BeginInvoke(async () =>
        {
            try { await Run(w, shot); }
            catch (Exception ex) { errors++; log.AppendLine("WYJĄTEK " + ex); }
            Finish();
        }, DispatcherPriority.ApplicationIdle);
    }

    static async Task Settle(Window w) { for (int i = 0; i < 3; i++) await w.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle); }

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
    static ButtonBase Btn(DependencyObject root, string text) => All<ButtonBase>(root).First(b => b.Content as string == text);
    static string Shot(string shot, string suf) => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(shot))!, Path.GetFileNameWithoutExtension(shot) + suf);

    static async Task Run(MainWindow w, string shot)
    {
        // --- kopia wspólnego katalogu
        var dir = Path.Combine(Path.GetTempPath(), "edytor_taboru_test"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        var plik = Path.Combine(dir, "tabor.json");
        File.Copy(TaborPlik.SciezkaWspolna(), plik);
        Ok(w.OpenPath(plik), "otwarcie kopii katalogu");
        await Settle(w);
        var P = () => w.Doc.P;
        Ok(P().Pojazdy.Count == 6 && P().Sklady.Count == 4 && w.PojazdList.Items.Count == 6 && w.SkladList.Items.Count == 4, "katalog: 6 pojazdów, 4 składy na listach");
        Ok(w.Sprawdz().Count == 0, $"Sprawdź: bez uwag ({w.LastMessage})");
        var pojTab = (TabItem)w.Tabs.Items[0]; var skTab = (TabItem)w.Tabs.Items[1];

        // --- nowa seria SM42 w formularzu
        Click(Btn(pojTab, "+ pojazd"));
        Ok(P().Pojazdy.Count == 7 && w.Pojazd.Seria == "Nowy" && w.PojazdIdx == 1, "„+ pojazd” – nowa seria za zaznaczoną, zaznaczona");
        Type(w.SeriaBox, "SM42");
        Type(w.NazwaBox, "SM42 – lokomotywa spalinowa manewrowa");
        w.RodzajBox.SelectedIndex = Array.FindIndex(Rodzaj.Wszystkie, r => r.Key == Rodzaj.LokSp);
        foreach (var (pole, v) in new[] { ("Długość", "14,24"), ("Liczba osi", "4"), ("Vmax", "90"), ("Masa", "72"), ("Ładunek", "0"), ("Masa napędna", "72"), ("Masy wirujące", "0,2"),
                     ("Opór a", "1,9"), ("Opór b", "0,01"), ("Opór c", "0,00035"), ("Masa hamująca próżny P", "50"), ("Masa hamująca próżny G", "45") })
            Type(w.Pola[pole], v);
        var sm = P().Pojazd("SM42");
        Ok(sm != null && sm.Rodzaj == Rodzaj.LokSp && sm.Dlugosc == 14.24 && sm.MasaNapedna == 72 && sm.Opory.C == 0.00035 && sm.MasaHamujaca["P"] == 50 && sm.MasaHamujaca["G"] == 45 && sm.MasyWirujace == 0.2,
            "formularz: seria, rodzaj, wymiary, masy, opory (przecinek dziesiętny), masa hamująca G / P");
        Ok(w.Pola["Opór c"].Text == "0,00035", $"pole oporu c pokazuje wartość z pliku bez obcięcia ({w.Pola["Opór c"].Text})");
        Ok(w.Sprawdz().Any(u => u.Contains("SM42") && u.Contains("bez charakterystyki")), "Sprawdź: SM42 bez charakterystyki trakcyjnej");
        Type(w.Pola["Masa"], "abc");
        Ok(P().Pojazd("SM42").Masa == 72 && w.LastMessage.StartsWith("Masa: wpisz liczbę"), $"zła liczba odrzucona ({w.LastMessage})");
        var sp = w.Chary[1];
        sp.Jest.IsChecked = true; Click(sp.Jest);
        Type(sp.Moc, "590"); sp.F0.Text = "200"; Click(sp.ZMocy);
        var ch = P().Pojazd("SM42").Char(Zasilanie.Spalinowy);
        Ok(ch != null && ch.Moc == 590 && ch.Punkty.Count == 10 && Math.Abs(ch.F(50) - 42.5) < 0.001 && ch.F(0) == 200, $"charakterystyka spalinowa „Z mocy”: {ch?.Punkty.Count} punktów, F(50) = {ch?.F(50):0.00} kN");
        Ok(w.Sprawdz().Count == 0, $"Sprawdź po charakterystyce: bez uwag ({string.Join("; ", w.Uwagi)})");
        var przed = TaborPlik.Serialize(P());
        Type(sp.Punkty, "0 200\n30 abc");
        Ok(TaborPlik.Serialize(P()) == przed && w.LastMessage.Contains("wiersz 2"), $"błędny wiersz punktów odrzucony ({w.LastMessage})");
        Type(sp.Punkty, "0 200\n20 200\n40 100\n90 45");
        Ok(P().Pojazd("SM42").Char(Zasilanie.Spalinowy).Punkty.Count == 4, "punkty F(v) wpisane ręcznie");
        if (shot != null) { await Settle(w); Save(w, Shot(shot, "_pojazdy.png")); }

        // --- zmiana serii 111A → 111Ax poprawia składy; Ctrl+Z
        w.PojazdList.SelectedIndex = P().Pojazdy.FindIndex(p => p.Seria == "111A");
        Type(w.SeriaBox, "111Ax");
        Ok(P().Pojazd("111Ax") != null && P().Sklad("EU07+6x111A").Pojazdy[1].Seria == "111Ax", "zmiana serii poprawia skład EU07+6x111A");
        Type(w.SeriaBox, "EN57");
        Ok(w.LastMessage.Contains("już") && P().Pojazd("111Ax") != null, $"seria zajęta – odmowa ({w.LastMessage})");
        w.DoUndo();
        Ok(P().Pojazd("111A") != null && P().Sklad("EU07+6x111A").Pojazdy[1].Seria == "111A" && w.SeriaBox.Text == "111A", "Ctrl+Z – seria i skład jak przedtem, pole serii też (choć ma fokus)");
        w.PojazdList.SelectedIndex = P().Pojazdy.FindIndex(p => p.Seria == "EU07");
        Click(Btn(pojTab, "Usuń"));
        Ok(P().Pojazd("EU07") != null && P().Pojazd("111A") != null && w.LastMessage.Contains("EU07 – jest w składach"), $"usunięcie serii ze składu – odmowa; przejście do innej serii nie zapisuje starego wpisu ({w.LastMessage})");

        // --- skład SM42 + 3 × 111A
        w.Tabs.SelectedIndex = 1; await Settle(w);
        Click(Btn(skTab, "+ skład"));
        Type(w.SkladIdBox, "SM42+3x111A");
        Type(w.SkladNazwaBox, "SM42 + 3 × 111A (osobowy na linii niezelektryfikowanej)");
        w.NastawienieBox.SelectedIndex = 1;
        Click(Btn(skTab, "+ pojazd do składu")); Click(Btn(skTab, "+ pojazd do składu"));
        Ok(w.Sklad.Pojazdy.Count == 2, "dwa pojazdy dodane przyciskiem");
        var rows = w.PozycjePanel.Children.OfType<StackPanel>().ToList();
        All<ComboBox>(rows[0]).First().SelectedItem = "SM42";
        rows = w.PozycjePanel.Children.OfType<StackPanel>().ToList();
        var cb1 = All<ComboBox>(rows[1]).First();
        cb1.SelectedItem = "111A";
        rows = w.PozycjePanel.Children.OfType<StackPanel>().ToList();
        var boxes = All<TextBox>(rows[1]).ToList();
        Type(boxes[0], "3");
        rows = w.PozycjePanel.Children.OfType<StackPanel>().ToList(); boxes = All<TextBox>(rows[1]).ToList();
        Type(boxes[1], "50");
        var sk = P().Sklad("SM42+3x111A");
        Ok(sk != null && sk.Nastawienie == "P" && sk.Pojazdy.Count == 2 && sk.Pojazdy[0].Seria == "SM42" && sk.Pojazdy[1] is { Seria: "111A", Liczba: 3, Zaladowanie: 0.5 },
            $"skład: id, nastawienie P, SM42 + 3 × 111A załadowanie 50 % ({(sk == null ? "brak" : sk.Nastawienie + ": " + string.Join(", ", sk.Pojazdy.Select(x => $"{x.Seria} ×{x.Liczba} {x.Zaladowanie}")))})");
        var poc = new Pociag(sk, P());
        Ok(Math.Abs(poc.Masa - (72 + 3 * 43)) < 1e-9 && w.Sumy.Text.Contains("masa 201 t") && w.Sumy.Text.Contains("Rusza na wzniesieniu"), $"sumy: {w.Sumy.Text.Split('\n')[0]}");
        rows = w.PozycjePanel.Children.OfType<StackPanel>().ToList();
        Click(All<ButtonBase>(rows[1]).First(b => b.Content as string == "↑"));
        Ok(P().Sklad("SM42+3x111A").Pojazdy[0].Seria == "111A", "↑ – kolejność pojazdów");
        w.DoUndo();
        Ok(P().Sklad("SM42+3x111A").Pojazdy[0].Seria == "SM42", "Ctrl+Z – kolejność jak przedtem");
        Ok(w.Sprawdz().Count == 0, $"Sprawdź: bez uwag ({string.Join("; ", w.Uwagi)})");
        Click(Btn(skTab, "+ skład"));
        Click(Btn(skTab, "+ pojazd do składu"));
        rows = w.PozycjePanel.Children.OfType<StackPanel>().ToList();
        All<ComboBox>(rows[0]).First().SelectedItem = "111A";
        Ok(w.Sprawdz().Any(u => u.Contains("bez pojazdu napędnego")), "Sprawdź: skład z samym wagonem – bez pojazdu napędnego");
        w.DoUndo(); w.DoUndo(); w.DoUndo();
        Ok(P().Sklady.Count == 5 && w.Sprawdz().Count == 0, "Ctrl+Z ×3 – pusty skład usunięty");
        if (shot != null) { w.SkladList.SelectedIndex = P().Sklady.FindIndex(s => s.Id == "SM42+3x111A"); await Settle(w); Save(w, Shot(shot, "_sklady.png")); Save(w, shot); }

        // --- zapis, odczyt
        Ok(w.SaveDoc(false), "zapis");
        Ok(File.Exists(plik + ".bak") && TaborPlik.Serialize(TaborPlik.Load(plik)) == TaborPlik.Serialize(P()), "plik zapisany (z .bak), odczyt = to samo");
        var k = TaborPlik.Load(plik);
        var r = Jazda.Rozpedzanie(new Pociag(k.Sklad("SM42+3x111A"), k), 60);
        Ok(r.T is > 20 and < 200, $"skład z pliku jedzie wg fizyki: 0 → 60 km/h w {r.T:0} s");
        Ok(!w.Doc.Dirty && !w.Title.StartsWith("*"), "po zapisie – bez znaku zmian w tytule");
    }
}
