using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Lcs.Model;
using LcsWidzew.UI;

namespace Lcs.EdytorZobrazowania2;

/// <summary>
/// Etap E3 (PLAN_EDYTOR_ZOBRAZOWANIA_V2.md § 5, § 8, § 10): paleta symboli z ikonami rysowanymi kodem pulpitu, klawisze 1…9 (kolejność wg Ie-104,
/// decyzja 2026-10-22), N / O / X – opisy, spacja – obrót, E – opis, menu PPM elementu, właściwości elementu, numer zwrotnicy i „+”.
/// </summary>
public partial class MainWindow
{
    /// <summary>Przyciski palety symboli (rodzaj → przycisk).</summary>
    public readonly Dictionary<string, ToggleButton> SymBtns = new();
    /// <summary>Pole wpisywania opisu na płótnie (plan § 5: „opis pojawia się w miejscu domyślnym, z kursorem do wpisania tekstu”).</summary>
    public readonly TextBox OpisEdit = new() { MinWidth = 60, FontFamily = new FontFamily("Consolas"), FontSize = 13, Visibility = Visibility.Collapsed, Padding = new Thickness(2, 0, 2, 0) };
    readonly Canvas overlay = new() { ClipToBounds = true };
    /// <summary>Edytowany opis: element, numer opisu.</summary>
    (string El, int Idx)? opisFor;
    DateTime opisOd;
    /// <summary>Pole opisu otwarte – klawisze trafiają do niego, nie do skrótów palety.</summary>
    public bool OpisOtwarty => opisFor != null && OpisEdit.Visibility == Visibility.Visible;

    void FokusOpisu()
    {
        if (!OpisOtwarty || OpisEdit.IsKeyboardFocusWithin) return;
        OpisEdit.Focus(); Keyboard.Focus(OpisEdit);
    }

    /// <summary>Płótno z warstwą pola opisu.</summary>
    UIElement BuildCanvas()
    {
        var g = new Grid();
        g.Children.Add(View);
        overlay.Children.Add(OpisEdit);
        g.Children.Add(overlay);
        g.Children.Add(SzkicBar);
        overlay.IsHitTestVisible = true; overlay.Background = null;
        OpisEdit.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Enter) { CommitOpis(); e.Handled = true; }
            else if (e.Key == Key.Escape) { CloseOpis(); e.Handled = true; }
        };
        // fokus zabrany zaraz po otwarciu pola (klik na płótnie, odświeżenie paneli) – pole zostaje otwarte; później utrata fokusu zatwierdza opis
        // (uwaga właściciela 2026-10-22: „numeru toru nie mogę dodać” – pole zatwierdzało się puste, a cyfry działały jak skróty palety)
        OpisEdit.LostKeyboardFocus += (s, e) =>
        {
            if (opisFor == null) return;
            if (e.NewFocus is not TextBox && (DateTime.UtcNow - opisOd).TotalSeconds < 0.6) { Dispatcher.BeginInvoke(DispatcherPriority.Input, FokusOpisu); return; }
            CommitOpis();
        };
        return g;
    }

    void BuildSymbolePalette(StackPanel sp)
    {
        sp.Children.Add(new TextBlock { Text = "Symbole", FontWeight = FontWeights.SemiBold, Margin = new Thickness(2, 10, 0, 4) });
        foreach (var (typ, nazwa, klawisz) in ObrSymbole.Paleta)
        {
            if (klawisz == "N") sp.Children.Add(new TextBlock { Text = "Opisy", FontWeight = FontWeights.SemiBold, Margin = new Thickness(2, 10, 0, 4) });
            var row = PaletteRow(nazwa, klawisz);
            var ic = new Image { Source = Ikona(typ), Width = 30, Height = 18, Margin = new Thickness(0, 0, 6, 0) };
            var grid = new DockPanel(); DockPanel.SetDock(ic, System.Windows.Controls.Dock.Left); grid.Children.Add(ic); grid.Children.Add(row);
            var b = new ToggleButton { Content = grid, Focusable = false, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(4, 2, 6, 2), Margin = new Thickness(0, 1, 0, 1),
                ToolTip = $"{nazwa} ({klawisz}): klik na torze – wstawienie" + (ObrSymbole.Kierunkowy(typ) ? "; ostrze w stronę środka obrazu, spacja – odwrotnie" : "") + (ObrSymbole.RolaOpisu(typ) != null ? "; potem wpisz opis i Enter" : "") };
            var t = typ;
            b.Click += (s, e) => SetSymbol(t);
            SymBtns[typ] = b;
            sp.Children.Add(b);
        }
    }

    /// <summary>Klocki szlaku (etap E6): koniec szlaku, odstępy SBL, okno numeru pociągu – klik przy końcu toru szlakowego / w miejscu okna.</summary>
    void BuildKlockiSzlaku(StackPanel sp)
    {
        foreach (var (typ, nazwa, tip) in new[]
        {
            (ObrSymbole.KoniecSzlaku, "koniec szlaku", "Koniec szlaku: klik przy końcu toru szlakowego – strzałka blokady, semafor wjazdowy (gdy go nie ma) i nazwa sąsiedniej stacji (po F8 – z UT); szlak wewnętrzny z obiema stacjami na obrazie – klik na torze szlaku przy stacji (strzałka i semafor, spacja – druga stacja); dalej zwykłe elementy"),
            (ObrSymbole.OdstepySbl, "odstępy SBL", "Odstępy SBL: klik przy końcu toru szlakowego – rząd pól w osi toru i kolumna dalszych pól przy krawędzi (jak GrM); szlak wewnętrzny – klik na torze szlaku, pola idą w stronę stacji (spacja – w drugą); liczba pól i kierunek kolumny – w Właściwościach; po F8 pola dostają odstępy i semafory odstępowe z UT"),
            (ObrSymbole.StrzalkaBlokady, "strzałka blokady", "Sama strzałka blokady: klik na torze szlaku (np. przy drugiej stacji szlaku wewnętrznego); po F8 – szlak z UT"),
            (ObrSymbole.PolePip, "okno numeru pociągu", "Okno numeru pociągu dla szlaku (PIP): klik w miejscu okna; na pulpicie czerwone z numerem odstępu i pociągu, gdy szlak zajęty (tylko posterunek z PIP)"),
        })
        {
            var ic = new Image { Source = Ikona(typ), Width = 30, Height = 18, Margin = new Thickness(0, 0, 6, 0) };
            var grid = new DockPanel(); DockPanel.SetDock(ic, System.Windows.Controls.Dock.Left); grid.Children.Add(ic); grid.Children.Add(PaletteRow(nazwa, ""));
            var b = new ToggleButton { Content = grid, Focusable = false, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(4, 2, 6, 2), Margin = new Thickness(0, 1, 0, 1), ToolTip = tip };
            var t = typ;
            b.Click += (s, e) => SetSymbol(t);
            SymBtns[typ] = b;
            sp.Children.Add(b);
        }
    }

    /// <summary>
    /// Klocek „koniec szlaku” (decyzja właściciela 2026-10-22): przy końcu toru <paramref name="wezel"/> – strzałka blokady za odstępami (albo 60 j. od końca),
    /// semafor wjazdowy 40 j. dalej w stronę stacji (gdy na tym odcinku nie ma jeszcze semafora wjazdowego), nazwa sąsiedniej stacji pod torem (pusta – F8
    /// wpisze ją z UT). Wynik – zwykłe elementy do edycji.
    /// </summary>
    public ObrElement KlocekKoniecSzlaku(string wezel, double tNa = 0, int kNa = 1)
    {
        var o = Obraz;
        if (ObrGraf.Odcinek(o, wezel) != null) return KoniecSzlakuNaTorze(wezel, tNa, kNa);
        var w = ObrGraf.Wezel(o, wezel);
        var e = w == null ? null : ObrGraf.Przy(o, wezel).FirstOrDefault();
        if (e == null) { Message("Koniec szlaku: kliknij przy końcu toru"); return null; }
        string semId = null;
        Ch("Klocek: koniec szlaku", q =>
        {
            var qe = ObrGraf.Odcinek(q, e.Id); var qw = ObrGraf.Wezel(q, wezel);
            var (a, b) = ObrGraf.Konce(q, qe);
            double len = Math.Sqrt(Math.Pow(b[0] - a[0], 2) + Math.Pow(b[1] - a[1], 2));
            var od = q.Elementy.FirstOrDefault(x => x.Typ == ObrSymbole.OdstepySbl && x.Na == wezel);
            double d0 = od != null ? (od.Pola ?? 0) * ObrSymbole.PoleSbl + 18 : 60;     // strzałka wycina tor od 18 j. przed środkiem – zaraz za polami
            // położenie T na odcinku w odległości d od końca toru (węzła)
            double T(double d) { double f = Math.Clamp(d / Math.Max(1, len), 0.02, 0.95); return qe.A == wezel ? f : 1 - f; }
            int kierDoStacji = qe.A == wezel ? 1 : -1;
            q.Elementy.Add(new ObrElement { Typ = ObrSymbole.StrzalkaBlokady, Na = qe.Id, T = T(d0), Kier = kierDoStacji });
            if (!q.Elementy.Any(x => x.Typ == ObrSymbole.Semafor && x.Na == qe.Id && ObrSymbole.Wjazdowy(x.Styl)))
            {
                var sem = ObrSymbole.Nowy(q, ObrSymbole.Semafor, qe.Id, T(d0 + 40), kierDoStacji);
                sem.Styl = ObrSymbole.SemWjazdowy;
                sem.Opisy[0].D = ObrSymbole.OpisDomyslny(q, sem);
                semId = sem.Id;
            }
            int ux = Math.Sign(qe.A == wezel ? b[0] - a[0] : a[0] - b[0]);
            var nap = ObrSymbole.Nowy(q, ObrSymbole.Napis, null, 0, 1, new[] { ZobTory.Grid(qw.P[0] + ux * 40, 8), ZobTory.Grid(qw.P[1] + 40, 8) });
            nap.Styl = ZobNapis.Sasiad;
        });
        Message("Koniec szlaku: strzałka blokady" + (semId != null ? ", semafor wjazdowy" : "") + " i nazwa sąsiada (po F8 – z UT)" + (semId != null ? " – wpisz nazwę semafora i Enter" : ""));
        if (semId != null) { View.Select(null, null, new[] { semId }); EdytujOpis(semId); }
        return semId == null ? null : Obraz.Elementy.FirstOrDefault(x => x.Id == semId);
    }

    /// <summary>
    /// Koniec szlaku wewnętrznego, gdy obie stacje są na obrazie (tor szlaku bez końca): strzałka blokady w klikniętym miejscu, semafor wjazdowy 40 j. w stronę
    /// stacji <paramref name="k"/> (gdy do najbliższego rozjazdu nie ma żadnego); bez nazwy sąsiada – druga stacja jest na obrazie.
    /// </summary>
    ObrElement KoniecSzlakuNaTorze(string odc, double t, int k)
    {
        string semId = null;
        Ch("Klocek: koniec szlaku", q =>
        {
            var qe = ObrGraf.Odcinek(q, odc);
            var (a, b) = ObrGraf.Konce(q, qe);
            double len = Math.Max(1, Math.Sqrt(Math.Pow(b[0] - a[0], 2) + Math.Pow(b[1] - a[1], 2)));
            q.Elementy.Add(new ObrElement { Typ = ObrSymbole.StrzalkaBlokady, Na = qe.Id, T = t, Kier = k });
            if (!ObrSymbole.WjazdowyDalej(q, qe, t, k))
            {
                var sem = ObrSymbole.Nowy(q, ObrSymbole.Semafor, qe.Id, Math.Clamp(t + k * 40 / len, 0.02, 0.98), k);
                sem.Styl = ObrSymbole.SemWjazdowy;
                sem.Opisy[0].D = ObrSymbole.OpisDomyslny(q, sem);
                semId = sem.Id;
            }
        });
        Message("Koniec szlaku wewnętrznego: strzałka blokady" + (semId != null ? " i semafor wjazdowy – wpisz nazwę semafora i Enter" : " (semafor wjazdowy już jest)") + "; przy drugiej stacji – to samo (spacja zmienia stronę)");
        if (semId != null) { View.Select(null, null, new[] { semId }); EdytujOpis(semId); }
        return semId == null ? null : Obraz.Elementy.FirstOrDefault(x => x.Id == semId);
    }

    /// <summary>Odstępy SBL w środku toru szlaku (szlak wewnętrzny): tor dzieli się w klikniętym punkcie, pola idą w stronę stacji <paramref name="k"/>, bez kolumny.</summary>
    ObrElement OdstepyNaTorze(string odc, int[] p, int k)
    {
        ObrElement el = null;
        Ch("Wstawienie: odstępy SBL", q =>
        {
            var qe = ObrGraf.Odcinek(q, odc);
            int kw = ObrSymbole.KierWezla(q, qe, k);
            var w = ObrGraf.Podziel(q, qe, p);
            if (q.Elementy.Any(x => x.Typ == ObrSymbole.OdstepySbl && x.Na == w.Id)) return;
            el = ObrSymbole.Nowy(q, ObrSymbole.OdstepySbl, w.Id, 0, kw);
            el.Rozm = new[] { 0, -1 };
        });
        if (el == null) { Message("Odstępy SBL już są w tym miejscu"); return null; }
        View.Select(null, null, new[] { el.Id });
        Message("Odstępy SBL na torze szlaku: pola w stronę stacji (spacja – w drugą); liczba pól – we Właściwościach");
        return el;
    }

    /// <summary>Ikona palety – symbol narysowany tym samym kodem co na pulpicie (<see cref="PulpitSymbole"/>), na czarnym tle.</summary>
    static ImageSource Ikona(string typ)
    {
        var dg = new DrawingGroup();
        using (var dc = dg.Open())
        {
            dc.DrawRectangle(PulpitSymbole.Bg, null, new Rect(-20, -12, 40, 24));
            var tor = new Pen(PulpitSymbole.Grey, PulpitSymbole.Tor);
            var g = PulpitSymbole.Grey; var bg = PulpitSymbole.Bg;
            switch (typ)
            {
                case ObrSymbole.Koziol: dc.DrawLine(tor, new Point(-20, 0), new Point(2, 0)); PulpitSymbole.Koziol(dc, 2, 0, 1, g); break;
                case ObrSymbole.Peron: PulpitSymbole.Peron(dc, new Rect(-18, -7, 36, 14)); break;
                case ObrSymbole.PolePip: dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0xa0, 0xa0, 0xa0)), new Pen(PulpitSymbole.White, 1), new Rect(-12, -9, 24, 18)); break;
                case ObrSymbole.OdstepySbl:
                    foreach (var x0 in new[] { -19, -6, 7 }) dc.DrawLine(tor, new Point(x0, 5), new Point(x0 + 10, 5));
                    dc.DrawLine(tor, new Point(-19, -6), new Point(-9, -6));
                    break;
                case ObrSymbole.KoniecSzlaku:
                    dc.DrawLine(tor, new Point(-20, 0), new Point(-14, 0)); dc.DrawLine(tor, new Point(12, 0), new Point(20, 0));
                    PulpitSymbole.StrzalkaNeutralna(dc, new Point(-2, 0), new Vector(1, 0), g, bg);
                    PulpitSymbole.Wjazdowy(dc, 12, 0, 1, g, bg);
                    break;
                case ObrSymbole.Napis: break;
                case ObrSymbole.Przejazd: dc.DrawLine(tor, new Point(-20, 0), new Point(20, 0)); dc.DrawLine(new Pen(PulpitSymbole.CrossPink, 3), new Point(0, -11), new Point(0, 11)); break;
                default:
                    dc.DrawLine(tor, new Point(-20, 0), new Point(20, 0));
                    switch (typ)
                    {
                        case ObrSymbole.Semafor: dc.DrawRectangle(bg, null, new Rect(-9, -4, 18, 8)); PulpitSymbole.Semafor(dc, 0, 0, 1, g, true); break;
                        case ObrSymbole.Tarcza: dc.DrawRectangle(bg, null, new Rect(-9, -4, 18, 8)); PulpitSymbole.Tarcza(dc, 0, 0, 1, g); break;
                        case ObrSymbole.Powtarzacz: dc.DrawRectangle(bg, null, new Rect(-9, -4, 18, 8)); PulpitSymbole.Powtarzacz(dc, 0, 0, 1, g, true); break;
                        case ObrSymbole.Pk: dc.DrawRectangle(bg, null, new Rect(-9, -4, 18, 8)); PulpitSymbole.Pk(dc, 0, 0, 1, PulpitSymbole.SwLabel); break;
                        case ObrSymbole.Przerwa: dc.DrawRectangle(bg, null, new Rect(-2.5, -4, 5, 8)); break;
                        case ObrSymbole.Wykolejnica: PulpitSymbole.Wykolejnica(dc, 0, 0, g); break;
                        case ObrSymbole.NumerToru: dc.DrawRectangle(bg, null, PulpitSymbole.PoleNumeruToru(0, 0)); break;
                    }
                    break;
            }
            var tekst = typ switch { ObrSymbole.NumerToru => "1", ObrSymbole.OpisOdcinka => "13", ObrSymbole.Napis => "Abc", _ => null };
            if (tekst != null)
            {
                var ft = new FormattedText(tekst, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Consolas"), 10, PulpitSymbole.SwLabel, 1.0);
                dc.DrawText(ft, new Point(-ft.Width / 2, typ == ObrSymbole.OpisOdcinka ? 2 : -ft.Height / 2));
            }
        }
        dg.ClipGeometry = new RectangleGeometry(new Rect(-20, -12, 40, 24));
        var img = new DrawingImage(dg); img.Freeze();
        return img;
    }

    public void SetSymbol(string typ)
    {
        View.SymbolTyp = typ; View.SymbolOdwroc = false;
        View.Tool = ObrTool.Symbol;
        // nowe wstawianie – spacja nie obraca symbolu zaznaczonego wcześniej
        View.Select(View.SelW.ToList(), View.SelE.ToList(), null);
        Message($"{ObrSymbole.NazwaTypu(typ)}: " + (ObrSymbole.Wolny(typ) ? "klik w miejscu" : typ == ObrSymbole.Koziol ? "klik przy końcu toru" : "klik na torze")
            + (ObrSymbole.Kierunkowy(typ) ? "; spacja – ostrze w drugą stronę" : "") + "; PPM / Esc – Wskaż");
    }

    void InitSymbole()
    {
        View.SymbolPlaced += (typ, na, t, kier, p) =>
        {
            if (typ == ObrSymbole.Koziol && Obraz.Elementy.Any(x => x.Typ == ObrSymbole.Koziol && x.Na == na)) { Message("Kozioł już stoi na tym końcu toru"); return; }
            if (typ == ObrSymbole.KoniecSzlaku) { KlocekKoniecSzlaku(na, t, kier); return; }
            if (typ == ObrSymbole.OdstepySbl && ObrGraf.Odcinek(Obraz, na) != null) { OdstepyNaTorze(na, p, kier); return; }
            if (typ == ObrSymbole.OdstepySbl && Obraz.Elementy.Any(x => x.Typ == ObrSymbole.OdstepySbl && x.Na == na)) { Message("Odstępy SBL już są przy tym końcu toru"); return; }
            ObrElement el = null;
            Ch($"Wstawienie: {ObrSymbole.NazwaTypu(typ)}", o => el = ObrSymbole.Nowy(o, typ, na, t, kier, p));
            if (el == null) return;
            View.Select(null, null, new[] { el.Id });
            if (ObrSymbole.RolaOpisu(typ) != null) EdytujOpis(el.Id);
            else Message($"Wstawiono: {ObrSymbole.NazwaTypu(typ)}");
        };
        View.ElementMoved += (id, t, p) => Ch("Przesunięcie symbolu", o => { var el = o.Elementy.FirstOrDefault(x => x.Id == id); if (el == null) return; if (p != null) el.P = p; else el.T = t; });
        View.OpisMoved += (id, idx, d) => Ch("Przesunięcie opisu", o => { var el = o.Elementy.FirstOrDefault(x => x.Id == id); if (el != null && idx < el.Opisy.Count) el.Opisy[idx].D = d; });
        View.ElementMenuAt += (p, id) =>
        {
            LastMenu = ElementMenu(id);
            if (LastMenu.Items.Count == 0 || NoPrompt) return;
            LastMenu.PlacementTarget = View; LastMenu.IsOpen = true;
        };
        View.ViewChanged += PlaceOpisEdit;
    }

    /// <summary>Klawisze symboli (plan § 8): 1…9, N, O, X – symbol z palety, spacja – obrót, E – opis; true – obsłużony.</summary>
    internal bool SymbolKey(Key k)
    {
        int n = k is >= Key.D1 and <= Key.D9 ? k - Key.D1 : k is >= Key.NumPad1 and <= Key.NumPad9 ? k - Key.NumPad1 : -1;
        if (n >= 0) { SetSymbol(ObrSymbole.Paleta[n].Typ); return true; }
        switch (k)
        {
            case Key.N: SetSymbol(ObrSymbole.NumerToru); return true;
            case Key.O: SetSymbol(ObrSymbole.OpisOdcinka); return true;
            case Key.X: SetSymbol(ObrSymbole.Napis); return true;
            case Key.Space: Obroc(); return true;
            case Key.E: EdytujZaznaczony(); return true;
        }
        return false;
    }

    /// <summary>
    /// Spacja: w narzędziu Symbol – następne symbole ostrzem w drugą stronę (widać na cieniu pod kursorem); we Wskaż – zaznaczone symbole
    /// (opis w miejscu domyślnym przechodzi na drugą stronę toru).
    /// </summary>
    public void Obroc()
    {
        if (Obraz == null || OdbijKlocek()) return;
        // także strzałka blokady i odstępy SBL w środku toru (strona stacji)
        var ids = View.SelEl.Where(id => Obraz.Elementy.FirstOrDefault(x => x.Id == id) is ObrElement el && (ObrSymbole.Kierunkowy(el.Typ) || el.Typ == ObrSymbole.StrzalkaBlokady
            || el.Typ == ObrSymbole.OdstepySbl && el.Na != null && ObrGraf.Przy(Obraz, el.Na).Count == 2)).ToList();
        if (View.Tool == ObrTool.Symbol)
        {
            // narzędzie symbolu: obraca właśnie wstawiony symbol (zaznaczony) i ustawia ten kierunek dla następnych (uwaga właściciela 2026-10-22:
            // „obracanie nie działa” – wcześniej spacja zmieniała tylko następne symbole)
            View.SymbolOdwroc = !View.SymbolOdwroc; View.InvalidateVisual();
            if (ids.Count == 0)
            {
                Message(View.SymbolOdwroc ? "Następne symbole ostrzem od środka obrazu (spacja – z powrotem)" : "Następne symbole ostrzem w stronę środka obrazu");
                return;
            }
        }
        if (ids.Count == 0) { Message("Obrót: zaznacz symbol z kierunkiem (semafor, tarcza, powtarzacz, PK) i naciśnij spację"); return; }
        Ch("Obrót symbolu", o =>
        {
            foreach (var el in o.Elementy.Where(x => ids.Contains(x.Id)))
            {
                var stary = ObrSymbole.OpisDomyslny(o, el);
                el.Kier = -el.Kier;
                var nowy = ObrSymbole.OpisDomyslny(o, el);
                foreach (var d in el.Opisy.Where(d => d.D[0] == stary[0] && d.D[1] == stary[1])) d.D = nowy;
            }
        });
        Message($"Obrócono: {ids.Count}");
    }

    /// <summary>E: opis zaznaczonego elementu; zaznaczony rozjazd bez numeru – nowy numer zwrotnicy.</summary>
    public void EdytujZaznaczony()
    {
        if (Obraz == null) return;
        if (View.SelEl.Count == 1) { EdytujOpis(View.SelEl.First()); return; }
        if (View.SelW.Count == 1 && View.SelE.Count == 0) NumerZwrotnicy(View.SelW.First());
    }

    /// <summary>Numer zwrotnicy w węźle rozjazdu: istniejący – do poprawy, brak – nowy element w miejscu domyślnym (jak na pulpicie).</summary>
    public void NumerZwrotnicy(string wezel)
    {
        if (ObrGraf.RodzajWezla(Obraz, wezel) != ObrGraf.Rodzaj.Rozjazd) { Message("Numer zwrotnicy stawia się w węźle rozjazdu"); return; }
        var el = ObrSymbole.ZwrotnicaW(Obraz, wezel);
        if (el == null) Ch("Numer zwrotnicy", o => el = ObrSymbole.Nowy(o, ObrSymbole.Zwrotnica, wezel, 0, 1));
        View.Select(null, null, new[] { el.Id });
        EdytujOpis(el.Id);
    }

    /// <summary>Pole opisu na płótnie w miejscu opisu (Enter – zapis, Esc – bez zmian).</summary>
    public void EdytujOpis(string id, int idx = 0)
    {
        var el = Obraz?.Elementy.FirstOrDefault(x => x.Id == id);
        if (el == null) return;
        if (el.Opisy.Count == 0)
        {
            Ch("Opis", o => { var e = o.Elementy.First(x => x.Id == id); e.Opisy.Add(new ObrOpis { Rola = ObrSymbole.Nazwa, D = ObrSymbole.OpisDomyslny(o, e) }); });
            el = Obraz.Elementy.First(x => x.Id == id);
        }
        int i = Math.Min(idx, el.Opisy.Count - 1);
        opisFor = (id, i);
        OpisEdit.Text = el.Opisy[i].Tekst;
        OpisEdit.Visibility = Visibility.Visible;
        PlaceOpisEdit();
        Message($"{ObrSymbole.NazwaTypu(el.Typ)}: wpisz {(el.Opisy[i].Rola == ObrSymbole.Nazwa ? "nazwę" : "numer")} i Enter (Esc – bez zmian)");
        opisOd = DateTime.UtcNow;
        OpisEdit.Focus(); OpisEdit.SelectAll();
        // po obsłużeniu kliknięcia (MouseDown płótna, odświeżenie paneli) fokus jeszcze raz w polu
        Dispatcher.BeginInvoke(DispatcherPriority.Input, FokusOpisu);
    }

    void PlaceOpisEdit()
    {
        if (opisFor is not var (id, idx) || Obraz?.Elementy.FirstOrDefault(x => x.Id == id) is not ObrElement el || idx >= el.Opisy.Count) return;
        if (ObrView.OpisPkt(Obraz, el, el.Opisy[idx], idx) is not var (x, y)) return;
        var s = View.ToScreen(x, y);
        Canvas.SetLeft(OpisEdit, s.X - 30); Canvas.SetTop(OpisEdit, s.Y - 11);
    }

    public void CommitOpis()
    {
        if (opisFor is not var (id, idx)) return;
        var t = OpisEdit.Text.Trim();
        CloseOpis();
        Ch("Opis", o => { var el = o.Elementy.FirstOrDefault(x => x.Id == id); if (el != null && idx < el.Opisy.Count) el.Opisy[idx].Tekst = t; });
        Message(t.Length > 0 ? $"Opis „{t}”" : "Opis pusty");
    }

    void CloseOpis()
    {
        opisFor = null; OpisEdit.Visibility = Visibility.Collapsed;
        View.Focus();
    }

    // ------------------------------------------------------------------ menu i właściwości elementu
    public ContextMenu ElementMenu(string id)
    {
        var m = new ContextMenu();
        var el = Obraz?.Elementy.FirstOrDefault(x => x.Id == id);
        if (el == null) return m;
        if (ObrSymbole.Kierunkowy(el.Typ)) m.Items.Add(Item("Obróć (Spacja)", Obroc));
        if (el.Opisy.Count > 0 || ObrSymbole.RolaOpisu(el.Typ) != null) m.Items.Add(Item("Opis… (E)", () => EdytujOpis(id)));
        if (el.Opisy.Count > 0) m.Items.Add(Item("Opis w miejscu domyślnym", () => Ch("Opis w miejscu domyślnym", o => { var e = o.Elementy.First(x => x.Id == id); foreach (var d in e.Opisy) d.D = ObrSymbole.OpisDomyslny(o, e); })));
        foreach (var (styl, nazwa) in Odmiany(el.Typ))
        {
            if (m.Items.Count > 0 && styl == Odmiany(el.Typ)[0].Styl) m.Items.Add(new Separator());
            var s = styl;
            m.Items.Add(Item(nazwa, () => UstawStylEl(id, s), (el.Styl ?? Odmiany(el.Typ)[0].Styl) == s));
        }
        m.Items.Add(new Separator());
        m.Items.Add(Item("Usuń (Del)", DeleteSel));
        return m;
    }

    /// <summary>Odstępy SBL – liczba pól w rzędzie i w kolumnie, kierunek kolumny (opisy pól – wyżej, po kolei od stacji).</summary>
    void OdstepyWlasciwosci(ObrElement el)
    {
        var r = ObrSymbole.Rozmiar(el);
        PropPanel.Children.Add(Label("Pól w rzędzie, w kolumnie"));
        var box = new TextBox { Text = $"{el.Pola ?? 0}, {r[0]}", Margin = new Thickness(0, 2, 0, 4) };
        void Commit()
        {
            var v = box.Text.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x => int.TryParse(x, out var n) ? n : -1).ToArray();
            if (v.Length != 2 || v[0] < 1 || v[0] > 12 || v[1] < 0 || v[1] > 12) { Message("Odstępy SBL: pól w rzędzie 1–12, w kolumnie 0–12"); return; }
            UstawPolaSbl(el.Id, v[0], v[1], null);
        }
        box.LostFocus += (s, e) => Commit();
        box.KeyDown += (s, e) => { if (e.Key == Key.Enter) Commit(); };
        PropPanel.Children.Add(box);
        var cb = new ComboBox { Margin = new Thickness(0, 2, 0, 8) };
        cb.Items.Add("kolumna w górę"); cb.Items.Add("kolumna w dół");
        cb.SelectedIndex = r[1] < 0 ? 0 : 1;
        cb.SelectionChanged += (s, e) => UstawPolaSbl(el.Id, null, null, cb.SelectedIndex == 0 ? -1 : 1);
        PropPanel.Children.Add(cb);
    }

    /// <summary>Liczba pól odstępów SBL (rząd, kolumna) i kierunek kolumny; opisy pól dopełniane / obcinane.</summary>
    public void UstawPolaSbl(string id, int? rzad, int? kolumna, int? kierunek) =>
        Ch("Odstępy SBL", q =>
        {
            var e = q.Elementy.FirstOrDefault(x => x.Id == id); if (e == null) return;
            var r = ObrSymbole.Rozmiar(e).ToArray();
            if (rzad is int n) e.Pola = n;
            if (kolumna is int m) r[0] = m;
            if (kierunek is int k) r[1] = k;
            e.Rozm = r;
            int ile = (e.Pola ?? 0) + r[0];
            while (e.Opisy.Count < ile) e.Opisy.Add(new ObrOpis { Rola = ObrSymbole.Numer, Tekst = "", D = new[] { 0, -12 } });
            if (e.Opisy.Count > ile) e.Opisy.RemoveRange(ile, e.Opisy.Count - ile);
        });

    /// <summary>Odmiany elementu (styl): semafor, powtarzacz, zwrotnica.</summary>
    static (string Styl, string Nazwa)[] Odmiany(string typ) => typ switch
    {
        ObrSymbole.Semafor => new[] { (ObrSymbole.SemManewrowy, "semafor z funkcją tarczy manewrowej"), (ObrSymbole.SemPociagowy, "semafor tylko pociągowy"), (ObrSymbole.SemWjazdowy, "semafor wjazdowy z końcem przebiegu wyjazdowego (trójkąt)"), (ObrSymbole.SemWjazdowyBez, "semafor wjazdowy bez końca przebiegu (bez trójkąta)") },
        ObrSymbole.Powtarzacz => new[] { (ObrSymbole.PowtSemafora, "powtarzacz semafora"), (ObrSymbole.PowtTarcza, "tarcza ostrzegawcza") },
        ObrSymbole.Zwrotnica => new[] { ((string)null, "„+” na wprost"), (ObrSymbole.PlusBok, "„+” na bok") },
        ObrSymbole.NumerToru => new[] { ((string)null, "numer toru widoczny na pulpicie"), (ObrSymbole.NumerUkryty, "numer toru niewidoczny – tylko do przypięcia toru") },
        _ => Array.Empty<(string, string)>(),
    };

    public void UstawStylEl(string id, string styl) => Ch("Odmiana symbolu", o => { var el = o.Elementy.FirstOrDefault(x => x.Id == id); if (el != null) el.Styl = styl; });

    /// <summary>Dodatkowe pozycje menu PPM węzła / toru (etap E3): numer zwrotnicy, „+”, wstaw przerwę.</summary>
    void MenuSymbole(ContextMenu m, int[] p, string wezel, string odcinek)
    {
        if (wezel != null && ObrGraf.RodzajWezla(Obraz, wezel) == ObrGraf.Rodzaj.Rozjazd)
        {
            var z = ObrSymbole.ZwrotnicaW(Obraz, wezel);
            m.Items.Insert(0, Item(z == null ? "Numer zwrotnicy… (E)" : $"Numer zwrotnicy {z.Opisy.FirstOrDefault()?.Tekst}… (E)", () => NumerZwrotnicy(wezel)));
            if (z != null)
            {
                m.Items.Insert(1, Item("„+” na wprost", () => UstawStylEl(z.Id, null), z.Styl != ObrSymbole.PlusBok));
                m.Items.Insert(2, Item("„+” na bok", () => UstawStylEl(z.Id, ObrSymbole.PlusBok), z.Styl == ObrSymbole.PlusBok));
            }
            m.Items.Insert(z == null ? 1 : 3, new Separator());
        }
        if (odcinek != null)
            m.Items.Insert(0, Item("Wstaw przerwę izolacyjną tutaj", () =>
            {
                if (ObrSymbole.NaTorzeW(Obraz, p[0], p[1], 24, Doc.P.Siatka) is var (e, t, _))
                    Ch("Wstawienie: przerwa izolacyjna", o => ObrSymbole.Nowy(o, ObrSymbole.Przerwa, e.Id, t, 1));
            }));
    }

    /// <summary>Zakładka Właściwości – zaznaczony element.</summary>
    void ElementWlasciwosci(ObrElement el)
    {
        var o = Obraz;
        PropPanel.Children.Add(Label(char.ToUpper(ObrSymbole.NazwaTypu(el.Typ)[0]) + ObrSymbole.NazwaTypu(el.Typ)[1..]));
        string gdzie = ObrSymbole.Pozycja(o, el) is var (x, y, ux, uy) ? $"({x:0}, {y:0})" + (ObrSymbole.Kierunkowy(el.Typ) ? $", ostrze {(Math.Abs(ux) > 0.5 ? ux > 0 ? "w prawo" : "w lewo" : uy > 0 ? "w dół" : "w górę")}" : "") : "";
        PropPanel.Children.Add(Note(gdzie + (ObrSymbole.NaTorze(el.Typ) ? " – na torze; przeciągnij, by przesunąć wzdłuż toru" : "")));
        for (int i = 0; i < el.Opisy.Count; i++)
        {
            int idx = i; var d = el.Opisy[i];
            PropPanel.Children.Add(Label(d.Rola == ObrSymbole.Nazwa ? "Nazwa" : "Numer / tekst"));
            var box = new TextBox { Text = d.Tekst, Margin = new Thickness(0, 2, 0, 8) };
            void Commit() { var t = box.Text.Trim(); if (Obraz?.Elementy.FirstOrDefault(x => x.Id == el.Id) is ObrElement e && idx < e.Opisy.Count && e.Opisy[idx].Tekst != t) Ch("Opis", q => q.Elementy.First(x => x.Id == el.Id).Opisy[idx].Tekst = t); }
            box.LostFocus += (s, e) => Commit();
            box.KeyDown += (s, e) => { if (e.Key == Key.Enter) Commit(); };
            PropPanel.Children.Add(box);
        }
        var st = Odmiany(el.Typ);
        if (st.Length > 0)
        {
            var cb = new ComboBox { Margin = new Thickness(0, 2, 0, 8) };
            foreach (var (_, n) in st) cb.Items.Add(n);
            cb.SelectedIndex = Math.Max(0, Array.FindIndex(st, z => z.Styl == el.Styl));
            cb.SelectionChanged += (s, e) => { if (cb.SelectedIndex >= 0) UstawStylEl(el.Id, st[cb.SelectedIndex].Styl); };
            PropPanel.Children.Add(Label("Odmiana")); PropPanel.Children.Add(cb);
        }
        if (el.Typ == ObrSymbole.OdstepySbl) OdstepyWlasciwosci(el);
        if (el.Typ is ObrSymbole.Peron or ObrSymbole.Przejazd or ObrSymbole.PolePip)
        {
            var r = ObrSymbole.Rozmiar(el);
            PropPanel.Children.Add(Label(el.Typ == ObrSymbole.Przejazd ? "Zasięg linii w górę, w dół [j.]" : "Szerokość, wysokość [j.]"));
            var box = new TextBox { Text = $"{r[0]}, {r[1]}", Margin = new Thickness(0, 2, 0, 8) };
            void Commit()
            {
                var v = box.Text.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x => int.TryParse(x, out var n) ? n : -1).ToArray();
                if (v.Length != 2 || v.Any(x => x < 8 || x > 2000)) { Message("Dwie liczby od 8 do 2000 j."); return; }
                Ch("Rozmiar", q => { var e = q.Elementy.First(x => x.Id == el.Id); e.Rozm = v; if (e.Typ == ObrSymbole.Peron) foreach (var d in e.Opisy) d.D = ObrSymbole.OpisDomyslny(q, e); });
            }
            box.LostFocus += (s, e) => Commit();
            box.KeyDown += (s, e) => { if (e.Key == Key.Enter) Commit(); };
            PropPanel.Children.Add(box);
        }
        var row = new WrapPanel();
        if (ObrSymbole.Kierunkowy(el.Typ)) row.Children.Add(Btn("Obróć", (s, e) => Obroc(), "Ostrze w drugą stronę (Spacja)"));
        row.Children.Add(Btn("Usuń", (s, e) => DeleteSel(), "Usuń element (Del)"));
        PropPanel.Children.Add(row);
    }
}
