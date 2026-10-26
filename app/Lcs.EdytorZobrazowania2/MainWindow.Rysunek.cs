using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorZobrazowania2;

/// <summary>
/// Etap E2 (PLAN_EDYTOR_ZOBRAZOWANIA_V2.md § 5, § 8, § 10): rysunek bez UT – pióro toru, rozjazdy powstające same, skrzyżowania,
/// zaznaczanie grupy, przesuwanie, rozciąganie, rząd z łącznikami, kopiuj / wklej / powiel, rozsuń rzędy, menu PPM, zakładka Właściwości.
/// Logika grafu – <see cref="ObrGraf"/> w Lcs.Model.
/// </summary>
public partial class MainWindow
{
    /// <summary>Zakładka Właściwości: zaznaczony element albo podsumowanie rysunku i uwagi.</summary>
    public readonly StackPanel PropPanel = new() { Margin = new Thickness(10) };
    /// <summary>Schowek edytora (fragment rysunku bez przypięć).</summary>
    static ObrGraf.Fragment clip;
    /// <summary>Ostatnio otwarte menu PPM (test).</summary>
    public ContextMenu LastMenu { get; private set; }

    void InitRysunek()
    {
        View.TorDone += pts =>
        {
            ObrGraf.Lamana r = null;
            Ch("Tor", o => r = ObrGraf.Rysuj(o, pts));
            if (r == null) return;
            View.Select(null, r.Odcinki);
            Message($"Tor: {r.Odcinki.Count} odc." + (r.Rozjazdy.Count > 0 ? $", rozjazdy: {r.Rozjazdy.Count}" : "") +
                (r.Skrzyzowania.Count > 0 ? $", skrzyżowania: {r.Skrzyzowania.Count} (PPM na węźle – rozjazd krzyżowy)" : "") + " – rysuj dalej albo Esc");
        };
        View.SelMoved += (nodes, dx, dy) => { Ch($"Przesunięcie o {dx}, {dy}", o => ObrGraf.Przesun(o, nodes, dx, dy)); Message($"Przesunięto o Δx {dx}, Δy {dy} j."); };
        View.RzadDragged += (nodes, dy) => { Ch($"Przesunięcie rzędu o {dy}", o => ObrGraf.PrzesunRzad(o, nodes, dy)); Message($"Rząd przesunięty o {dy} j. z łącznikami"); };
        View.MenuAt += (p, w, e) =>
        {
            LastMenu = Menu(p, w, e);
            if (LastMenu.Items.Count == 0 || NoPrompt) return;
            LastMenu.PlacementTarget = View; LastMenu.IsOpen = true;
        };
        View.Info += Message;
        View.SelectionChanged += RefreshWlasciwosci;
    }

    // ------------------------------------------------------------------ polecenia na zaznaczeniu
    public void DeleteSel()
    {
        if (Obraz == null || View.SelW.Count + View.SelE.Count + View.SelEl.Count == 0) return;
        var w = View.SelW.ToList(); var e = View.SelE.ToList(); var el = View.SelEl.ToList();
        Ch("Usunięcie", o => { o.Elementy.RemoveAll(x => el.Contains(x.Id)); o.Przypiecia.RemoveAll(x => el.Contains(x.El)); ObrGraf.Usun(o, w, e); });
        View.ClearSelection();
        Message($"Usunięto: węzłów {w.Count}, odcinków {e.Count}, symboli {el.Count} (Ctrl+Z przywraca)");
    }

    public void MoveSel(int dx, int dy)
    {
        if (Obraz == null) return;
        var n = ObrGraf.WezlyZaznaczenia(Obraz, View.SelW, View.SelE);
        // nic nie zaznaczono – strzałki przesuwają widok
        if (n.Count == 0) { if (View.SelEl.Count == 0) View.PanBy(-Math.Sign(dx) * 80, -Math.Sign(dy) * 80); return; }
        Ch($"Przesunięcie o {dx}, {dy}", o => ObrGraf.Przesun(o, n, dx, dy));
    }

    public void SelectAll() { if (Obraz != null) View.Select(Obraz.Wezly.Select(x => x.Id), Obraz.Odcinki.Select(x => x.Id)); }

    public void CopySel()
    {
        if (Obraz == null || View.SelW.Count + View.SelE.Count == 0) return;
        clip = ObrGraf.Kopiuj(Obraz, View.SelW, View.SelE);
        Message($"Skopiowano: węzłów {clip.Wezly.Count}, odcinków {clip.Odcinki.Count} (bez przypięć) – Ctrl+V wkleja w miejscu kursora");
    }

    /// <summary>Wklejenie schowka: lewy górny róg fragmentu w punkcie kursora (poza obrazem – 40 j. obok oryginału).</summary>
    public void Paste(bool atCursor = true)
    {
        if (Obraz == null || clip == null || clip.Wezly.Count == 0) return;
        int minX = clip.Wezly.Min(w => w.P[0]), minY = clip.Wezly.Min(w => w.P[1]);
        int dx = 40, dy = 40;
        if (atCursor && View.CursorPt is var (cx, cy) && cx >= 0 && cy >= 0 && cx <= Obraz.Szer && cy <= Obraz.Wys) { dx = cx - minX; dy = cy - minY; }
        (HashSet<string> W, HashSet<string> E) r = default;
        var f = clip;
        Ch("Wklejenie", o => r = ObrGraf.Wklej(o, f, dx, dy));
        View.Select(r.W, r.E);
        Message($"Wklejono: węzłów {r.W.Count}, odcinków {r.E.Count}");
    }

    public void Duplicate() { CopySel(); Paste(false); }

    public void RozsunRzedy()
    {
        if (Obraz == null) return;
        var n = ObrGraf.WezlyZaznaczenia(Obraz, View.SelW, View.SelE);
        List<int> cel = null;
        Ch("Rozsuń rzędy", o => cel = ObrGraf.RozsunRzedy(o, n, Doc.P.Siatka));
        Message(cel == null ? "Rozsuń rzędy: zaznacz tory w co najmniej trzech rzędach (skrajne zostają, środkowe w równych odstępach)"
            : "Rzędy w równych odstępach: " + string.Join(", ", cel));
    }

    /// <summary>Klawisze rysunku (plan § 8); true – obsłużony.</summary>
    internal bool RysunekKey(Key k, bool ctrl, bool shift)
    {
        int step = shift ? 40 : Doc.P.Siatka;
        switch (k)
        {
            case Key.Enter when View.Tool == ObrTool.Tor: View.FinishDraft(); return true;
            case Key.Back when View.Tool == ObrTool.Tor: View.UndoPoint(); return true;
            case Key.Delete when !ctrl: DeleteSel(); return true;
            case Key.Left when !ctrl: MoveSel(-step, 0); return true;
            case Key.Right when !ctrl: MoveSel(step, 0); return true;
            case Key.Up when !ctrl: MoveSel(0, -step); return true;
            case Key.Down when !ctrl: MoveSel(0, step); return true;
            case Key.C when ctrl: CopySel(); return true;
            case Key.V when ctrl: Paste(); return true;
            case Key.D when ctrl: Duplicate(); return true;
            case Key.A when ctrl: SelectAll(); return true;
            case Key.G when ctrl: RozsunRzedy(); return true;
        }
        return false;
    }

    // ------------------------------------------------------------------ menu PPM (to edytor, nie pulpit – PPM jest wolny, plan § 5)
    static MenuItem Item(string header, Action act, bool check = false)
    {
        var m = new MenuItem { Header = header, IsCheckable = check, IsChecked = check };
        m.Click += (s, e) => act();
        return m;
    }

    public ContextMenu Menu(int[] p, string wezel, string odcinek)
    {
        var m = new ContextMenu();
        var o = Obraz;
        if (wezel != null && ObrGraf.Wezel(o, wezel) is ObrWezel w)
        {
            if (!View.SelW.Contains(wezel)) View.Select(new[] { wezel }, null);
            int deg = ObrGraf.Przy(o, wezel).Count;
            if (deg == 4)
            {
                var rodz = ObrGraf.RodzajWezla(o, wezel);
                m.Items.Add(Item("Skrzyżowanie torów", () => UstawKrzyz(wezel, ObrGraf.Skrzyzowanie), rodz == ObrGraf.Rodzaj.Skrzyzowanie));
                m.Items.Add(Item("Rozjazd krzyżowy", () => UstawKrzyz(wezel, ObrGraf.RozjazdKrzyzowy), rodz == ObrGraf.Rodzaj.RozjazdKrzyzowy));
                m.Items.Add(new Separator());
            }
            if (deg == 2) m.Items.Add(Item("Połącz tor tutaj (usuń węzeł)", () => PolaczTor(wezel)));
            if (deg == 1 && NajblizszyKoniec(wezel) is ObrWezel kn)
                m.Items.Add(Item($"Połącz z końcem toru w ({kn.P[0]}, {kn.P[1]})", () => PolaczKonce(wezel, kn.Id)));
            m.Items.Add(Item("Usuń węzeł z torami", () => { View.Select(new[] { wezel }, null); DeleteSel(); }));
        }
        else if (odcinek != null && ObrGraf.Odcinek(o, odcinek) is ObrOdcinek e)
        {
            if (!View.SelE.Contains(odcinek)) View.Select(null, new[] { odcinek });
            var (a, b) = ObrGraf.Konce(o, e);
            var q = ObrGraf.NaOdcinku(a, b, p[0], p[1], Doc.P.Siatka);
            m.Items.Add(Item("Podziel tor tutaj", () => { Ch("Podział toru", x => { var oe = ObrGraf.Odcinek(x, odcinek); if (oe != null) ObrGraf.Podziel(x, oe, q); }); Message($"Tor podzielony w ({q[0]}, {q[1]}) – węzeł do przeciągnięcia"); }));
            if (ObrGraf.Poziomy(o, e)) m.Items.Add(Item("Zaznacz cały rząd", () => { var rz = ObrGraf.Rzad(Obraz, ObrGraf.Odcinek(Obraz, odcinek)); View.Select(rz, Obraz.Odcinki.Where(x => rz.Contains(x.A) && rz.Contains(x.B)).Select(x => x.Id)); }));
            m.Items.Add(new Separator());
            m.Items.Add(Item("Tor z kontrolą niezajętości (ciągły)", () => UstawStyl(ObrOdcinek.Ciagly), e.Styl == ObrOdcinek.Ciagly));
            m.Items.Add(Item("Tor bez kontroli niezajętości (przerywany)", () => UstawStyl(ObrOdcinek.BezKontroli), e.Styl == ObrOdcinek.BezKontroli));
            m.Items.Add(new Separator());
            m.Items.Add(Item("Usuń zaznaczone", DeleteSel));
        }
        else if (clip != null) m.Items.Add(Item("Wklej tutaj", () => Paste()));
        MenuSymbole(m, p, wezel, odcinek);
        return m;
    }

    /// <summary>Połączenie toru w węźle – odwrotność „Podziel tor tutaj” (uwaga właściciela 2026-10-22: „jak można rozrywać tory, to i łączyć”).</summary>
    public void PolaczTor(string wezel)
    {
        string why = null;
        Ch("Połączenie toru", x => why = ObrGraf.Polacz(x, wezel));
        if (why != null) { Message("Nie można połączyć: " + why); return; }
        View.ClearSelection();
        Message("Tor połączony – jeden odcinek");
    }

    /// <summary>Najbliższy inny koniec toru (do 400 j.) – do połączenia z końcem <paramref name="wezel"/>.</summary>
    ObrWezel NajblizszyKoniec(string wezel)
    {
        var o = Obraz; var w = ObrGraf.Wezel(o, wezel);
        return o.Wezly.Where(n => n.Id != wezel && ObrGraf.Przy(o, n.Id).Count == 1 && ObrGraf.Drugi(ObrGraf.Przy(o, n.Id)[0], n.Id) != wezel)
            .Select(n => (n, d: Math.Sqrt(Math.Pow(n.P[0] - w.P[0], 2) + Math.Pow(n.P[1] - w.P[1], 2)))).Where(z => z.d <= 400).OrderBy(z => z.d).Select(z => z.n).FirstOrDefault();
    }

    public void PolaczKonce(string a, string b)
    {
        string why = null;
        Ch("Połączenie końców torów", x => why = ObrGraf.PolaczKonce(x, a, b));
        if (why != null) { Message("Nie można połączyć: " + why); return; }
        View.ClearSelection();
        Message("Końce torów połączone – po zmianach F8");
    }

    public void UstawKrzyz(string wezel, string typ) =>
        Ch(typ == ObrGraf.RozjazdKrzyzowy ? "Rozjazd krzyżowy" : "Skrzyżowanie", o =>
        {
            o.Elementy.RemoveAll(x => x.Na == wezel && (x.Typ == ObrGraf.Skrzyzowanie || x.Typ == ObrGraf.RozjazdKrzyzowy));
            o.Elementy.Add(new ObrElement { Typ = typ, Na = wezel });
        });

    /// <summary>Styl zaznaczonych odcinków (Ie-104 § 8 ust. 1 – tor bez kontroli niezajętości przerywany).</summary>
    public void UstawStyl(string styl)
    {
        var ids = View.SelE.ToList();
        Ch(styl == ObrOdcinek.BezKontroli ? "Tor bez kontroli niezajętości" : "Tor z kontrolą niezajętości", o =>
        {
            foreach (var e in o.Odcinki.Where(x => ids.Contains(x.Id))) e.Styl = styl;
            ObrGraf.Upros(o);
        });
    }

    // ------------------------------------------------------------------ zakładka Właściwości
    void RefreshWlasciwosci()
    {
        PropPanel.Children.Clear();
        PropPanel.Children.Add(Header("Właściwości"));
        var o = Obraz;
        if (o == null) { PropPanel.Children.Add(Note("Brak otwartego zobrazowania.")); return; }
        int nw = View.SelW.Count, ne = View.SelE.Count;
        var jeden = nw == 0 && ne == 0 && View.SelEl.Count == 1 ? o.Elementy.FirstOrDefault(x => x.Id == View.SelEl.First()) : null;
        // droga przebiegu na obrazie – tylko póki zaznaczony jest jej sygnalizator początkowy
        if (View.Droga != null && jeden?.Id != drogaEl) { View.Droga = null; drogaKlucz = drogaEl = null; View.InvalidateVisual(); }
        if (jeden is ObrElement sel) { ElementWlasciwosci(sel); PrzebiegiSemafora(sel); }
        else if (nw == 0 && ne == 1 && ObrGraf.Odcinek(o, View.SelE.First()) is ObrOdcinek e)
        {
            var (a, b) = ObrGraf.Konce(o, e);
            int dx = b[0] - a[0], dy = b[1] - a[1];
            string kier = dy == 0 ? "poziomy" : dx == 0 ? "pionowy" : Math.Abs(dx) == Math.Abs(dy) ? "ukos 45°" : "nie co 45° (popraw)";
            PropPanel.Children.Add(Label("Odcinek toru"));
            PropPanel.Children.Add(Note($"od ({a[0]}, {a[1]}) do ({b[0]}, {b[1]}), długość {Math.Sqrt(dx * dx + dy * dy):0.#} j., {kier}"));
            var styl = new ComboBox { Margin = new Thickness(0, 2, 0, 8) };
            styl.Items.Add("z kontrolą niezajętości (ciągły)"); styl.Items.Add("bez kontroli niezajętości (przerywany)");
            styl.SelectedIndex = e.Styl == ObrOdcinek.BezKontroli ? 1 : 0;
            styl.SelectionChanged += (s, x) => UstawStyl(styl.SelectedIndex == 1 ? ObrOdcinek.BezKontroli : ObrOdcinek.Ciagly);
            PropPanel.Children.Add(Label("Styl linii")); PropPanel.Children.Add(styl);
            PrzypiecieToru(e);
        }
        else if (nw == 1 && ne == 0 && ObrGraf.Wezel(o, View.SelW.First()) is ObrWezel w)
        {
            var rodz = ObrGraf.RodzajWezla(o, w.Id);
            string nazwa = rodz switch
            {
                ObrGraf.Rodzaj.Koniec => "koniec toru", ObrGraf.Rodzaj.Zalamanie => "załamanie toru", ObrGraf.Rodzaj.Rozjazd => "rozjazd (numer – E albo PPM)",
                ObrGraf.Rodzaj.Skrzyzowanie => "skrzyżowanie torów", ObrGraf.Rodzaj.RozjazdKrzyzowy => "rozjazd krzyżowy", _ => "węzeł do wyjaśnienia",
            };
            PropPanel.Children.Add(Label("Węzeł"));
            PropPanel.Children.Add(Note($"({w.P[0]}, {w.P[1]}), torów: {ObrGraf.Przy(o, w.Id).Count} – {nazwa}"));
            if (rodz is ObrGraf.Rodzaj.Rozjazd or ObrGraf.Rodzaj.RozjazdKrzyzowy or ObrGraf.Rodzaj.Skrzyzowanie) PrzypiecieWezla(w);
        }
        else if (nw + ne + View.SelEl.Count > 0)
        {
            var n = ObrGraf.WezlyZaznaczenia(o, View.SelW, View.SelE);
            PropPanel.Children.Add(Note($"Zaznaczono: węzłów {n.Count}, odcinków {o.Odcinki.Count(x => n.Contains(x.A) && n.Contains(x.B))}. Przeciągnij albo strzałki – przesunięcie; Del – usuń; Ctrl+C / Ctrl+D – kopiuj / powiel; Ctrl+G – rozsuń rzędy."));
            var row = new WrapPanel();
            row.Children.Add(Btn("Usuń", (s, x) => DeleteSel(), "Usuń zaznaczenie (Del)"));
            row.Children.Add(Btn("Powiel", (s, x) => Duplicate(), "Kopia zaznaczenia 40 j. obok (Ctrl+D)"));
            row.Children.Add(Btn("Rozsuń rzędy", (s, x) => RozsunRzedy(), "Rzędy torów zaznaczenia w równych odstępach (Ctrl+G)"));
            PropPanel.Children.Add(row);
        }
        else
        {
            int rozj = o.Wezly.Count(x => ObrGraf.RodzajWezla(o, x.Id) == ObrGraf.Rodzaj.Rozjazd);
            PropPanel.Children.Add(Note($"Nic nie zaznaczono. Rysunek: odcinków {o.Odcinki.Count}, węzłów {o.Wezly.Count}, rozjazdów {rozj}. Pióro toru (T) – rysowanie; Wskaż (V) – klik albo prostokąt."));
        }
        var uw = ObrGraf.Uwagi(o).Concat(ObrSymbole.Uwagi(o)).ToList();
        PropPanel.Children.Add(new TextBlock { Text = $"Uwagi do rysunku ({uw.Count})", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 4) });
        if (uw.Count == 0) PropPanel.Children.Add(Note("Brak – tory co 45°, węzły o znanym kształcie."));
        foreach (var (el, tekst) in uw)
        {
            var id = el;
            var b = new Button { Content = "⚠ " + tekst, HorizontalContentAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 1, 0, 1), ToolTip = "Zaznacz na obrazie" };
            b.Click += (s, x) =>
            {
                if (ObrGraf.Wezel(Obraz, id) != null) View.Select(new[] { id }, null);
                else if (Obraz.Elementy.Any(q => q.Id == id)) View.Select(null, null, new[] { id });
                else View.Select(null, new[] { id });
            };
            PropPanel.Children.Add(b);
        }
    }
}
