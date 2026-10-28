using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using LcsWidzew.Sim;

namespace LcsWidzew.UI;

/// <summary>
/// Okno wyciągu (podręcznik EDR rozdz. 8, Rys. 50): posterunek ruchu, dziennik ruchu, od – do, „Pokaż historię zmian w wierszach”, „Pokaż”;
/// podgląd stron (powiększenie, szukanie), „Drukuj…” (stopka: data wydruku, „System EDR”, strona n / m), „Zamknij”.
/// Rodzaje: wyciąg z dziennika ruchu, z dziennika telefonicznego, z zajętości torów stacyjnych.
/// </summary>
public class WyciagWindow : Window
{
    static readonly CultureInfo Pl = CultureInfo.GetCultureInfo("pl-PL");
    readonly EdrWindow okno;
    readonly string rodzaj;
    readonly ComboBox posterunek = new() { Width = 220 }, dziennik = new() { Width = 260 };
    readonly DatePicker dOd = new() { Width = 120 }, dDo = new() { Width = 120 };
    readonly TextBox gOd = new() { Width = 50, Text = "00:00" }, gDo = new() { Width = 50 };
    readonly CheckBox historia = new() { Content = "Pokaż historię zmian w wierszach", VerticalAlignment = VerticalAlignment.Center };
    readonly FlowDocumentReader czytnik = new() { ViewingMode = FlowDocumentReaderViewingMode.Page, IsFindEnabled = true, IsPrintEnabled = false, Background = Brushes.Gray };
    readonly TextBlock blad = new() { Foreground = Brushes.Firebrick, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
    FlowDocument dok;
    List<EdrDziennik> listaDz = new();
    /// <summary>Szerokość tekstu strony A4 (794 − marginesy 2 × 40): kolumny tabel mają stałe szerokości, ostatnia „rozciągana” dostaje resztę.</summary>
    const double Szer = 714;
    static void Kolumny(Table t, params double[] w)
    {
        double reszta = Szer - w.Where(x => x > 0).Sum() - 4;
        foreach (var x in w) t.Columns.Add(new TableColumn { Width = new GridLength(x > 0 ? x : Math.Max(80, reszta)) });
    }
    public string Tytul => rodzaj switch { "telefoniczny" => "Wyciąg z dziennika telefonicznego", "tory" => "Wyciąg z zajętości torów stacyjnych", _ => "Wyciąg z dziennika ruchu" };

    public WyciagWindow(EdrWindow okno, string rodzaj)
    {
        this.okno = okno; this.rodzaj = rodzaj;
        Title = Tytul; Width = 1000; Height = 860; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = EdrWindow.Pedzel(0xE9, 0xE9, 0xE9); FontFamily = new FontFamily("Segoe UI"); FontSize = 12;
        var d = okno.Dane;
        var gora = new WrapPanel { Margin = new Thickness(8) };
        UIElement Et(string t) => new TextBlock { Text = t, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 4, 0) };
        gora.Children.Add(Et("Posterunek ruchu:")); gora.Children.Add(posterunek);
        if (rodzaj == "ruchu") { gora.Children.Add(Et("Dziennik ruchu:")); gora.Children.Add(dziennik); }
        gora.Children.Add(Et("Od:")); gora.Children.Add(dOd); gora.Children.Add(gOd);
        gora.Children.Add(Et("Do:")); gora.Children.Add(dDo); gora.Children.Add(gDo);
        if (rodzaj != "tory") { historia.Margin = new Thickness(14, 0, 0, 0); gora.Children.Add(historia); }
        var pokaz = new Button { Content = "Pokaż", Padding = new Thickness(14, 2, 14, 2), Margin = new Thickness(14, 0, 0, 0) };
        pokaz.Click += (a, e) => Pokaz();
        gora.Children.Add(pokaz); gora.Children.Add(blad);
        var dol = new DockPanel { Margin = new Thickness(8) };
        var zamknij = new Button { Content = "Zamknij", Width = 100, IsCancel = true }; zamknij.Click += (a, e) => Close();
        var druk = new Button { Content = "Drukuj…", Width = 100, Margin = new Thickness(0, 0, 8, 0), ToolTip = "Wydruk (także do pliku PDF – drukarka „Microsoft Print to PDF”)" }; druk.Click += (a, e) => Drukuj();
        DockPanel.SetDock(zamknij, Dock.Right); dol.Children.Add(zamknij); DockPanel.SetDock(druk, Dock.Right); dol.Children.Add(druk);
        var g = new DockPanel();
        DockPanel.SetDock(gora, Dock.Top); g.Children.Add(gora);
        DockPanel.SetDock(dol, Dock.Bottom); g.Children.Add(dol);
        g.Children.Add(czytnik);
        Content = g;

        // domyślnie: od początku pierwszej doby symulacji do bieżącej chwili
        var dzis = DateTime.TryParseExact(d?.Data, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dz) ? dz : DateTime.Today;
        dOd.SelectedDate = dzis.AddDays(-(d?.Doba ?? 0)); dDo.SelectedDate = dzis; gDo.Text = okno.Teraz;
        posterunek.ItemsSource = d?.Posterunki.ToList(); posterunek.DisplayMemberPath = "Value"; posterunek.SelectedValuePath = "Key";
        posterunek.SelectionChanged += (a, e) => Dzienniki();
        posterunek.SelectedValue = okno.WykazPanel.Posterunek ?? d?.Posterunki.Keys.FirstOrDefault();
        Dzienniki();
    }

    void Dzienniki()
    {
        // EdrDziennik ma pola, nie właściwości – lista dostaje gotowe nazwy, dziennik wybiera się po indeksie
        listaDz = okno.Dane?.Dzienniki.Where(x => x.Post == posterunek.SelectedValue as string).ToList() ?? new();
        dziennik.ItemsSource = listaDz.Select(x => x.Nazwa).ToList(); dziennik.SelectedIndex = listaDz.Count > 0 ? 0 : -1;
    }

    /// <summary>Zrzuty i testy: wybór dziennika i historii, potem „Pokaż”.</summary>
    public void Ustaw(int dz, bool hist) { if (dz >= 0 && dz < dziennik.Items.Count) dziennik.SelectedIndex = dz; historia.IsChecked = hist; Pokaz(); }

    DateTime? Chwila(DatePicker dp, TextBox g) => dp.SelectedDate is DateTime d && Edr.Godzina(g.Text) is string h && h != "" && h != "---" ? d.Date + TimeSpan.Parse(h, CultureInfo.InvariantCulture) : null;

    void Pokaz()
    {
        blad.Text = "";
        okno.Odswiez(true);
        var d = okno.Dane;
        if (Chwila(dOd, gOd) is not DateTime od || Chwila(dDo, gDo) is not DateTime @do) { blad.Text = "Błędna data albo godzina"; return; }
        if (@do < od) { blad.Text = "„Do” wcześniej niż „Od”"; return; }
        if (posterunek.SelectedValue is not string post) { blad.Text = "Wybierz posterunek"; return; }
        var nazwaPost = d.Posterunki.GetValueOrDefault(post, post);
        dok = rodzaj switch
        {
            "telefoniczny" => Telefoniczny(nazwaPost, EdrWyciag.Telefoniczny(d.Telefoniczny.GetValueOrDefault(post), od, @do), od, @do),
            "tory" => Tory(nazwaPost, EdrWyciag.ZajetoscTorow(d.Dzienniki.Where(x => x.Post == post), od, @do), od, @do),
            _ => dziennik.SelectedIndex >= 0 && dziennik.SelectedIndex < listaDz.Count ? Ruchu(EdrWyciag.Dziennik(listaDz[dziennik.SelectedIndex], nazwaPost, od, @do, historia.IsChecked == true)) : null
        };
        if (dok == null) { blad.Text = "Wybierz dziennik ruchu"; return; }
        czytnik.Document = dok;
    }

    // ------------------------------------------------------------------ dokument
    static FlowDocument Nowy() => new() { PageWidth = 794, PageHeight = 1123, PagePadding = new Thickness(40, 40, 40, 56), ColumnWidth = 2000, FontFamily = new FontFamily("Times New Roman"), FontSize = 11, Background = Brushes.White };

    static Paragraph TytulAkapit(string t) => new(new Run(t)) { FontSize = 16, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 8) };

    static Paragraph Naglowek(params (string Et, string War)[] p)
    {
        var a = new Paragraph { Margin = new Thickness(0, 0, 0, 6) };
        for (int i = 0; i < p.Length; i++)
        {
            if (i > 0) a.Inlines.Add(new Run(p[i].Et.StartsWith("\n") ? "" : "      "));
            a.Inlines.Add(new Run(p[i].Et.TrimStart('\n') + " ")); a.Inlines.Add(new Bold(new Run(p[i].War)));
            if (i + 1 < p.Length && p[i + 1].Et.StartsWith("\n")) a.Inlines.Add(new LineBreak());
        }
        return a;
    }

    /// <summary>„Wyciąg sporządził … Stanowisko … dnia … godz. …” (Rys. 51).</summary>
    Paragraph Sporzadzil()
    {
        var d = okno.Dane;
        var p = new Paragraph { Margin = new Thickness(30, 14, 0, 0) };
        p.Inlines.Add(new Run("Wyciąg sporządził: ")); p.Inlines.Add(new Bold(new Run(d?.Uzytkownik ?? "–"))); p.Inlines.Add(new LineBreak());
        p.Inlines.Add(new Run("Stanowisko: ")); p.Inlines.Add(new Bold(new Run(d?.Rola ?? "–"))); p.Inlines.Add(new LineBreak());
        var dz = DateTime.TryParseExact(d?.Data, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t.ToString("yyyy-MM-dd") : d?.Data;
        p.Inlines.Add(new Run("dnia ")); p.Inlines.Add(new Bold(new Run($"{dz} godz. {okno.Teraz}")));
        return p;
    }

    static TableCell Kom(string t, int span = 1, bool gruby = false, TextAlignment w = TextAlignment.Center, double rozm = 0, bool szary = false, Thickness? ramka = null)
    {
        var p = new Paragraph(new Run(t ?? "")) { TextAlignment = w, Margin = new Thickness(2, 1, 2, 1) };
        if (gruby) p.FontWeight = FontWeights.Bold;
        if (rozm > 0) p.FontSize = rozm;
        if (szary) { p.Foreground = Brushes.Gray; p.FontStyle = FontStyles.Italic; }
        return new TableCell(p) { ColumnSpan = span, BorderBrush = Brushes.Black, BorderThickness = ramka ?? new Thickness(0.5) };
    }

    static (string G, string M) GM(string godz)
    {
        if (string.IsNullOrEmpty(godz)) return ("", "");
        if (godz == "---") return ("—", "—");
        if (godz == "\\") return ("/", "");
        var cz = godz.Split(" / ");
        string G(string h) => h.Length >= 5 ? h[..2] : h; string M(string h) => h.Length >= 5 ? h[3..5] : "";
        return cz.Length > 1 ? ($"{G(cz[0])}\n{G(cz[1])}", $"{M(cz[0])}\n{M(cz[1])}") : (G(cz[0]), M(cz[0]));
    }

    FlowDocument Ruchu(WyciagDziennika w)
    {
        var doc = Nowy();
        doc.Blocks.Add(TytulAkapit("Wyciąg z dziennika ruchu."));
        doc.Blocks.Add(Naglowek(("Posterunek:", w.Posterunek), ("za okres:", $"{w.Od} - {w.Do}"), ("\nSzlak z i do:", w.Szlak)));
        int n = w.DrKm.Count;
        var t = new Table { CellSpacing = 0, BorderBrush = Brushes.Black, BorderThickness = new Thickness(1) };
        Kolumny(t, new[] { 52.0, 52, 30, 20, 20, 20, 20, 20, 20, 0 }.Concat(Enumerable.Repeat(44.0, n)).Concat(w.Historia ? new[] { 62.0 } : Array.Empty<double>()).ToArray());
        int wszystkie = 10 + n + (w.Historia ? 1 : 0);
        var rg = new TableRowGroup(); t.RowGroups.Add(rg);
        var h1 = new TableRow(); rg.Rows.Add(h1);
        h1.Cells.Add(Kom("Nr pociągu", 2, rozm: 9.5)); h1.Cells.Add(Kom("Tor stacyjny", rozm: 8.5)); h1.Cells.Add(Kom("Droga wolna", 2, rozm: 8.5)); h1.Cells.Add(Kom("Pociąg odjechał", 2, rozm: 8.5)); h1.Cells.Add(Kom("Pociąg przyjechał", 2, rozm: 8.5));
        h1.Cells.Add(Kom("Uwagi", rozm: 9.5));
        if (n > 0) h1.Cells.Add(Kom("O jeździe pociągu zawiadomiono dróżników przejazdowych", n, rozm: 8.5));
        if (w.Historia) h1.Cells.Add(Kom("Data i godzina zmiany", rozm: 8.5));
        var h2 = new TableRow(); rg.Rows.Add(h2);
        h2.Cells.Add(Kom("nieparzysty", rozm: 8)); h2.Cells.Add(Kom("parzysty", rozm: 8)); h2.Cells.Add(Kom(""));
        for (int i = 0; i < 3; i++) { h2.Cells.Add(Kom("g.", rozm: 8)); h2.Cells.Add(Kom("m.", rozm: 8)); }
        h2.Cells.Add(Kom(""));
        foreach (var km in w.DrKm) h2.Cells.Add(Kom(km, rozm: 8.5));
        if (w.Historia) h2.Cells.Add(Kom(""));
        var h3 = new TableRow(); rg.Rows.Add(h3);
        foreach (var (x, sp) in new[] { ("1", 1), ("2", 1), ("3", 1), ("4", 2), ("5", 2), ("6", 2), ("7", 1) }) h3.Cells.Add(Kom(x, sp, rozm: 8));
        if (n > 0) h3.Cells.Add(Kom("8", n, rozm: 8));
        if (w.Historia) h3.Cells.Add(Kom("9", rozm: 8));
        foreach (var r in w.Wiersze)
        {
            var row = new TableRow(); rg.Rows.Add(row);
            bool sz = r.Historyczny, gr = w.Historia && !r.Historyczny;
            if (r.Rodzaj == "poc")
            {
                string kl = r.Klamra == 1 ? "⎧ " : r.Klamra == 2 ? "⎩ " : "";
                row.Cells.Add(Kom(kl + r.Np, gruby: gr, szary: sz, w: TextAlignment.Left)); row.Cells.Add(Kom(r.P, gruby: gr, szary: sz)); row.Cells.Add(Kom(r.Tor, gruby: gr, szary: sz));
                foreach (var gm in new[] { r.Dw, r.Odj, r.Prz }) { var (gg, mm) = GM(gm); row.Cells.Add(Kom(gg, gruby: gr, szary: sz)); row.Cells.Add(Kom(mm, gruby: gr, szary: sz)); }
                row.Cells.Add(Kom(r.Uwagi, gruby: gr, szary: sz, w: TextAlignment.Left));
                foreach (var v in r.Dr) row.Cells.Add(Kom(v == "\\" ? "/" : v, gruby: gr, szary: sz));
            }
            else
            {
                // telefonogram, wpis i wpis automatyczny – na całą szerokość, bez kratek (Rys. 51)
                var p = new Paragraph { Margin = new Thickness(30, 1, 4, 1), TextAlignment = r.Rodzaj == "tekst" ? TextAlignment.Center : TextAlignment.Left };
                if (sz) { p.Foreground = Brushes.Gray; p.FontStyle = FontStyles.Italic; }
                p.Inlines.Add(r.Rodzaj == "tel" ? new Bold(new Run(r.Tekst)) : new Run(r.Tekst));
                if (r.Podpis != "") { p.Inlines.Add(new LineBreak()); p.Inlines.Add(new Run("        " + r.Podpis) { FontStyle = FontStyles.Italic }); }
                row.Cells.Add(new TableCell(p) { ColumnSpan = 10 + n, BorderBrush = Brushes.Black, BorderThickness = new Thickness(0.5) });
            }
            if (w.Historia) row.Cells.Add(Kom(r.Zmiana, rozm: 8, szary: sz));
        }
        if (w.Wiersze.Count == 0) { var row = new TableRow(); row.Cells.Add(Kom("(brak wpisów w wybranym okresie)", wszystkie)); rg.Rows.Add(row); }
        doc.Blocks.Add(t);
        doc.Blocks.Add(Sporzadzil());
        return doc;
    }

    FlowDocument Telefoniczny(string post, List<WyciagTel> l, DateTime od, DateTime @do)
    {
        var doc = Nowy(); bool hist = historia.IsChecked == true;
        doc.Blocks.Add(TytulAkapit("Wyciąg z dziennika telefonicznego."));
        doc.Blocks.Add(Naglowek(("Posterunek:", post), ("za okres:", $"{od:yyyy-MM-dd HH:mm} - {@do:yyyy-MM-dd HH:mm}")));
        var t = new Table { CellSpacing = 0, BorderBrush = Brushes.Black, BorderThickness = new Thickness(1) };
        Kolumny(t, hist ? new[] { 50.0, 70, 0, 30, 30, 110, 62 } : new[] { 50.0, 70, 0, 30, 30, 110 });
        var rg = new TableRowGroup(); t.RowGroups.Add(rg);
        var h1 = new TableRow(); rg.Rows.Add(h1);
        h1.Cells.Add(Kom("Nr kol.", rozm: 9)); h1.Cells.Add(Kom("Data", rozm: 9)); h1.Cells.Add(Kom("Adres i treść rozmowy lub telefonogramu", rozm: 9));
        h1.Cells.Add(Kom("Czas zakończenia rozmowy lub nadawania telefonogramu", 2, rozm: 8)); h1.Cells.Add(Kom("Nadał / Odebrał", rozm: 9));
        if (hist) h1.Cells.Add(Kom("Data i godzina zmiany", rozm: 8));
        var h2 = new TableRow(); rg.Rows.Add(h2);
        h2.Cells.Add(Kom("")); h2.Cells.Add(Kom("")); h2.Cells.Add(Kom("")); h2.Cells.Add(Kom("godz.", rozm: 8)); h2.Cells.Add(Kom("min.", rozm: 8)); h2.Cells.Add(Kom(""));
        if (hist) h2.Cells.Add(Kom(""));
        foreach (var x in l)
        {
            var row = new TableRow(); rg.Rows.Add(row);
            var (g, m) = GM(x.Czas);
            row.Cells.Add(Kom(x.Nr)); row.Cells.Add(Kom(x.Data)); row.Cells.Add(Kom(x.Tresc, w: TextAlignment.Left)); row.Cells.Add(Kom(g)); row.Cells.Add(Kom(m)); row.Cells.Add(Kom(x.Strony));
            if (hist) row.Cells.Add(Kom(x.Zmiana, rozm: 8));
        }
        if (l.Count == 0) { var row = new TableRow(); row.Cells.Add(Kom("(brak wpisów w wybranym okresie)", hist ? 7 : 6)); rg.Rows.Add(row); }
        doc.Blocks.Add(t);
        if (hist) doc.Blocks.Add(new Paragraph(new Run("Wpisów dziennika telefonicznego nie zmienia się – każdy ma jedną postać.")) { FontSize = 9, Foreground = Brushes.Gray });
        doc.Blocks.Add(Sporzadzil());
        return doc;
    }

    FlowDocument Tory(string post, List<WyciagTor> l, DateTime od, DateTime @do)
    {
        var doc = Nowy();
        doc.Blocks.Add(TytulAkapit("Wyciąg z zajętości torów stacyjnych."));
        doc.Blocks.Add(Naglowek(("Posterunek:", post), ("za okres:", $"{od:yyyy-MM-dd HH:mm} - {@do:yyyy-MM-dd HH:mm}")));
        var t = new Table { CellSpacing = 0, BorderBrush = Brushes.Black, BorderThickness = new Thickness(1) };
        Kolumny(t, 60, 120, 180, 0);
        var rg = new TableRowGroup(); t.RowGroups.Add(rg);
        var h = new TableRow(); rg.Rows.Add(h);
        foreach (var x in new[] { "Tor stacyjny", "Pociąg", "Zajęty od (przyjazd)", "Zwolniony (odjazd)" }) h.Cells.Add(Kom(x, gruby: true, rozm: 9.5));
        foreach (var x in l) { var row = new TableRow(); rg.Rows.Add(row); row.Cells.Add(Kom(x.Tor, gruby: true)); row.Cells.Add(Kom(x.Pociag)); row.Cells.Add(Kom(x.Od)); row.Cells.Add(Kom(x.Do == "" ? "(zajęty)" : x.Do)); }
        if (l.Count == 0) { var row = new TableRow(); row.Cells.Add(Kom("(brak zajętości w wybranym okresie)", 4)); rg.Rows.Add(row); }
        doc.Blocks.Add(t);
        doc.Blocks.Add(new Paragraph(new Run("Zajętość wg wpisów w dziennikach ruchu posterunku (przyjazd na tor – odjazd z toru).")) { FontSize = 9, Foreground = Brushes.Gray });
        doc.Blocks.Add(Sporzadzil());
        return doc;
    }

    // ------------------------------------------------------------------ wydruk ze stopką
    void Drukuj()
    {
        if (dok == null) { blad.Text = "Najpierw „Pokaż”"; return; }
        var pd = new PrintDialog();
        if (pd.ShowDialog() != true) return;
        var kopia = Nowy();
        // wydruk z drugiej kopii dokumentu (ten w podglądzie zostaje w czytniku)
        foreach (var b in Kopiuj(dok)) kopia.Blocks.Add(b);
        kopia.PageWidth = pd.PrintableAreaWidth; kopia.PageHeight = pd.PrintableAreaHeight;
        pd.PrintDocument(new Stopka(((IDocumentPaginatorSource)kopia).DocumentPaginator, $"{DateTime.Now:yyyy-MM-dd HH:mm}"), Tytul);
    }

    static List<Block> Kopiuj(FlowDocument d)
    {
        var xaml = System.Windows.Markup.XamlWriter.Save(d);
        var k = (FlowDocument)System.Windows.Markup.XamlReader.Parse(xaml);
        return k.Blocks.ToList().Select(b => { k.Blocks.Remove(b); return b; }).ToList();
    }

    /// <summary>Stopka strony wydruku jak w Rys. 52: data wydruku, „System EDR”, „n / m”.</summary>
    class Stopka : DocumentPaginator
    {
        readonly DocumentPaginator p; readonly string data;
        public Stopka(DocumentPaginator p, string data) { this.p = p; this.data = data; p.ComputePageCount(); }
        public override DocumentPage GetPage(int n)
        {
            var s = p.GetPage(n);
            var cv = new ContainerVisual(); cv.Children.Add(s.Visual);
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                var tf = new Typeface("Times New Roman"); double y = s.Size.Height - 36, ppd = 1.0;
                FormattedText F(string t) => new(t, Pl, FlowDirection.LeftToRight, tf, 10, Brushes.Black, ppd);
                dc.DrawLine(new Pen(Brushes.Black, 0.5), new Point(40, y - 4), new Point(s.Size.Width - 40, y - 4));
                dc.DrawText(F(data), new Point(40, y));
                var sr = F("System EDR"); dc.DrawText(sr, new Point((s.Size.Width - sr.Width) / 2, y));
                var nr = F($"{n + 1} / {p.PageCount}"); dc.DrawText(nr, new Point(s.Size.Width - 40 - nr.Width, y));
            }
            cv.Children.Add(dv);
            return new DocumentPage(cv, s.Size, s.BleedBox, s.ContentBox);
        }
        public override bool IsPageCountValid => p.IsPageCountValid;
        public override int PageCount => p.PageCount;
        public override Size PageSize { get => p.PageSize; set => p.PageSize = value; }
        public override IDocumentPaginatorSource Source => p.Source;
    }
}
