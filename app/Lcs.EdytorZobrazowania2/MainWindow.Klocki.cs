using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorZobrazowania2;

/// <summary>
/// Etap E7 (PLAN_EDYTOR_ZOBRAZOWANIA_V2.md § 5, § 7, § 10): klocki z palety (łącznik „[”, łącznik ukośny, rozjazd krzyżowy, koniec toru), własne klocki
/// (zaznaczenie → „Zapisz jako klocek”, biblioteka w pliku), szkic na życzenie z UT (prostokąt → szara warstwa → Przyjmij / Przyjmij i edytuj / Odrzuć).
/// Logika – <see cref="ObrKlocki"/>, <see cref="ObrSzkic"/> w Lcs.Model.
/// </summary>
public partial class MainWindow
{
    /// <summary>Plik biblioteki własnych klocków (domyślnie <c>projekty_ut/klocki_zobrazowania.json</c>; test – plik tymczasowy).</summary>
    public static string KlockiPath { get; set; }
    public readonly Dictionary<string, ToggleButton> KlocekBtns = new();
    public readonly StackPanel MojeKlocki = new();
    public List<ObrKlocki.Wlasny> Wlasne { get; private set; } = new();
    /// <summary>Wybrany klocek: typ wbudowany albo „w:numer” – własny.</summary>
    public string KlocekTyp { get; private set; }
    public int KlocekWariant { get; private set; }
    /// <summary>Pasek szkicu nad płótnem (widoczny, gdy jest szkic).</summary>
    public readonly StackPanel SzkicBar = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(8), Visibility = Visibility.Collapsed };
    /// <summary>Tory UT do szkicu (puste – wszystkie nienarysowane).</summary>
    public readonly HashSet<string> SzkicTory = new();

    string KlockiPlik => KlockiPath ??= Path.Combine(ProjectsDir(), "klocki_zobrazowania.json");

    void BuildKlockiTorow(StackPanel sp)
    {
        foreach (var (typ, nazwa, opis) in ObrKlocki.Wbudowane)
        {
            var b = new ToggleButton { Content = PaletteRow(nazwa, ""), Focusable = false, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(6, 3, 6, 3), Margin = new Thickness(0, 1, 0, 1), ToolTip = opis + "; klik – wstawienie w punkcie (węzeł, tor albo siatka)" };
            var t = typ; b.Click += (s, e) => SetKlocek(t);
            KlocekBtns[typ] = b; sp.Children.Add(b);
        }
        sp.Children.Add(new TextBlock { Text = "Moje klocki", FontWeight = FontWeights.SemiBold, Margin = new Thickness(2, 10, 0, 4) });
        sp.Children.Add(MojeKlocki);
        var zap = Btn("Zapisz zaznaczenie…", (s, e) => ZapiszKlocek(null), "Zaznaczony fragment rysunku jako własny klocek (bez przypięć) – do wstawiania w innych obrazach");
        zap.Margin = new Thickness(0, 2, 0, 0); sp.Children.Add(zap);
        Wlasne = ObrKlocki.Wczytaj(KlockiPlik);
        RefreshMojeKlocki();
    }

    /// <summary>Biblioteka własnych klocków od nowa z pliku <see cref="KlockiPath"/>.</summary>
    public void WczytajKlocki() { Wlasne = ObrKlocki.Wczytaj(KlockiPlik); RefreshMojeKlocki(); Refresh(); }

    void RefreshMojeKlocki()
    {
        MojeKlocki.Children.Clear();
        foreach (var k in KlocekBtns.Keys.Where(k => k.StartsWith("w:")).ToList()) KlocekBtns.Remove(k);
        for (int i = 0; i < Wlasne.Count; i++)
        {
            var key = "w:" + i; var w = Wlasne[i];
            var b = new ToggleButton { Content = PaletteRow(w.Nazwa, ""), Focusable = false, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(6, 3, 6, 3), Margin = new Thickness(0, 1, 0, 1), ToolTip = $"Własny klocek „{w.Nazwa}” ({w.Fragment.Odcinki.Count} odcinków): klik – wstawienie, punkt zaczepienia – lewy górny węzeł" };
            b.Click += (s, e) => SetKlocek(key);
            KlocekBtns[key] = b; MojeKlocki.Children.Add(b);
        }
        if (Wlasne.Count == 0) MojeKlocki.Children.Add(Note("Brak – zaznacz fragment i „Zapisz zaznaczenie…”."));
    }

    public void SetKlocek(string typ)
    {
        KlocekTyp = typ; KlocekWariant = 0;
        View.KlocekDla = FragmentDla;
        View.Tool = ObrTool.Klocek;
        Message($"Klocek „{NazwaKlocka(typ)}”: klik w punkcie zaczepienia (węzeł, tor, siatka); spacja – odbicie; PPM / Esc – Wskaż");
    }

    string NazwaKlocka(string typ) => typ?.StartsWith("w:") == true && int.TryParse(typ[2..], out var i) && i < Wlasne.Count ? Wlasne[i].Nazwa : ObrKlocki.Nazwa(typ);

    /// <summary>Klocek dla punktu zaczepienia: łącznik – do następnej prowadnicy rzędu w swoją stronę (brak – 96 j.).</summary>
    ObrGraf.Fragment FragmentDla(int[] p)
    {
        if (Obraz == null || KlocekTyp == null) return null;
        if (KlocekTyp.StartsWith("w:")) return int.TryParse(KlocekTyp[2..], out var i) && i < Wlasne.Count ? ObrKlocki.Odbij(Wlasne[i].Fragment, KlocekWariant) : null;
        bool wGore = (KlocekWariant & 2) != 0;
        var rz = Obraz.Rzedy.Where(y => wGore ? y < p[1] : y > p[1]).Select(y => Math.Abs(y - p[1])).DefaultIfEmpty(96).Min();
        return ObrKlocki.Klocek(KlocekTyp, rz, KlocekWariant);
    }

    void InitKlocki()
    {
        View.KlocekPlaced += p =>
        {
            var f = FragmentDla(p); if (f == null) return;
            (HashSet<string> W, HashSet<string> E) r = default;
            Ch($"Klocek: {NazwaKlocka(KlocekTyp)}", o => r = ObrKlocki.Wstaw(o, f, p[0], p[1]));
            View.Select(r.W, r.E);
            Message($"Wstawiono klocek „{NazwaKlocka(KlocekTyp)}” – rozjazdy: {Obraz.Wezly.Count(w => ObrGraf.RodzajWezla(Obraz, w.Id) == ObrGraf.Rodzaj.Rozjazd)}");
        };
        View.SzkicRect += prost => ZaproponujSzkic(prost);
        View.SzkicMoved += (dx, dy) => Ch("Przesunięcie szkicu", o => { if (o.Szkic != null) ObrSzkic.Przesun(o.Szkic, dx, dy); });
        foreach (var (nazwa, act, tip) in new (string, Action, string)[]
        {
            ("Przyjmij", () => PrzyjmijSzkic(false), "Szkic staje się częścią obrazu (z przypięciami)"),
            ("Przyjmij i edytuj", () => PrzyjmijSzkic(true), "Przyjmij i zaznacz wstawiony fragment – do przesunięcia i poprawek"),
            ("Odrzuć", OdrzucSzkic, "Usuń szkic (Esc)"),
        })
        {
            var b = Btn(nazwa, (s, e) => act(), tip); b.Margin = new Thickness(0, 0, 6, 0); SzkicBar.Children.Add(b);
        }
        SzkicBar.Children.Insert(0, new Border { Background = new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x30)), Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(0, 0, 6, 0),
            Child = new TextBlock { Text = "Szkic z UT – propozycja, obraz projektuje człowiek; przeciągnij, by przesunąć", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center } });
    }

    /// <summary>Spacja w narzędziu klocka – następny wariant (odbicie w poziomie / w pionie).</summary>
    bool OdbijKlocek()
    {
        if (View.Tool != ObrTool.Klocek) return false;
        KlocekWariant = (KlocekWariant + 1) % 4; View.InvalidateVisual();
        Message($"Klocek: wariant {KlocekWariant + 1} z 4");
        return true;
    }

    /// <summary>Zaznaczenie jako własny klocek (nazwa – z okienka; bez okien – „Klocek N”).</summary>
    public ObrKlocki.Wlasny ZapiszKlocek(string nazwa)
    {
        if (Obraz == null || View.SelW.Count + View.SelE.Count == 0) { Message("Zapisz jako klocek: najpierw zaznacz fragment (prostokąt)"); return null; }
        nazwa ??= NoPrompt ? $"Klocek {Wlasne.Count + 1}" : Zapytaj("Nazwa klocka", $"Klocek {Wlasne.Count + 1}");
        if (string.IsNullOrWhiteSpace(nazwa)) return null;
        var k = ObrKlocki.ZZaznaczenia(Obraz, View.SelW, View.SelE, nazwa.Trim());
        if (k == null) return null;
        Wlasne.Add(k);
        try { ObrKlocki.Zapisz(KlockiPlik, Wlasne); } catch (Exception ex) { Message("Zapis klocków: " + ex.Message); }
        RefreshMojeKlocki(); RefreshTools();
        Message($"Zapisano klocek „{k.Nazwa}” ({k.Fragment.Odcinki.Count} odcinków) – w palecie „Moje klocki”");
        return k;
    }

    /// <summary>Proste okienko z polem tekstowym.</summary>
    string Zapytaj(string tytul, string wartosc)
    {
        var box = new TextBox { Text = wartosc, Margin = new Thickness(10), MinWidth = 260 };
        var ok = new Button { Content = "OK", IsDefault = true, Width = 80, Margin = new Thickness(10, 0, 10, 10), HorizontalAlignment = HorizontalAlignment.Right };
        var win = new Window { Title = tytul, Owner = this, SizeToContent = SizeToContent.WidthAndHeight, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel { Children = { box, ok } } };
        ok.Click += (s, e) => win.DialogResult = true;
        win.Loaded += (s, e) => { box.Focus(); box.SelectAll(); };
        return win.ShowDialog() == true ? box.Text : null;
    }

    // ------------------------------------------------------------------ szkic na życzenie
    public void ZacznijSzkic()
    {
        if (Doc == null || Obraz == null) return;
        View.Tool = ObrTool.Szkic;
        Message("Szkic z UT: przeciągnij prostokąt obszaru szkicu" + (SzkicTory.Count > 0 ? $" ({SzkicTory.Count} torów)" : " (wszystkie tory nienarysowane)") + "; Esc – przerwij");
    }

    public void ZaproponujSzkic(int[] prost)
    {
        if (prost[2] < 64 || prost[3] < 32) { Message("Szkic: za mały prostokąt"); return; }
        var tory = SzkicTory.Count > 0 ? SzkicTory.ToList() : null;
        ObrGraf.Fragment sz = null;
        Ch("Szkic z UT", o => o.Szkic = sz = ObrSzkic.Zaproponuj(Doc.Ut, o, tory, prost, Doc.P.Siatka));
        View.Tool = ObrTool.Wskaz;
        Message(sz == null ? "Szkic: brak torów UT do szkicu (wszystkie są już na obrazie?)"
            : $"Szkic: {sz.Odcinki.Count} odcinków, {sz.Elementy.Count(e => e.Typ == ObrSymbole.Zwrotnica)} zwrotnic – propozycja; Przyjmij / Przyjmij i edytuj / Odrzuć (Esc)");
    }

    public void PrzyjmijSzkic(bool edytuj)
    {
        if (Obraz?.Szkic == null) return;
        (HashSet<string> W, HashSet<string> E) r = default;
        Ch("Przyjęcie szkicu", o => { r = ObrKlocki.Wstaw(o, o.Szkic, 0, 0); o.Szkic = null; });
        if (edytuj) View.Select(r.W, r.E); else View.ClearSelection();
        Message($"Szkic przyjęty: {r.E?.Count} odcinków" + (edytuj ? " – zaznaczony, do poprawek" : ""));
    }

    public void OdrzucSzkic()
    {
        if (Obraz?.Szkic == null) return;
        Ch("Odrzucenie szkicu", o => o.Szkic = null);
        Message("Szkic odrzucony");
    }

    /// <summary>Zakładka UT: wybór torów do szkicu i przycisk „Zaproponuj szkic…”.</summary>
    void UtPanelSzkic(UtProject u)
    {
        if (Obraz == null) return;
        UtPanel.Children.Add(new TextBlock { Text = "Szkic na życzenie", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 4) });
        var nie = ObrSzkic.Nienarysowane(u, Obraz);
        SzkicTory.IntersectWith(nie);
        UtPanel.Children.Add(Note("Program zaproponuje rysunek zaznaczonych torów (bez zaznaczenia – wszystkich nienarysowanych) w prostokącie, który wskażesz. To propozycja – obraz projektuje człowiek."));
        foreach (var t in nie)
        {
            var id = t;
            var cb = new CheckBox { Content = ObrPrzypinanie.Opis(u, t), IsChecked = SzkicTory.Contains(t), Margin = new Thickness(0, 1, 0, 1) };
            cb.Click += (s, e) => { if (cb.IsChecked == true) SzkicTory.Add(id); else SzkicTory.Remove(id); };
            UtPanel.Children.Add(cb);
        }
        var b = Btn("Zaproponuj szkic…", (s, e) => ZacznijSzkic(), "Przeciągnij prostokąt na płótnie – szkic z UT pojawi się na osobnej warstwie");
        b.HorizontalAlignment = HorizontalAlignment.Left; b.Margin = new Thickness(0, 4, 0, 0); b.IsEnabled = nie.Count > 0;
        UtPanel.Children.Add(b);
    }
}
