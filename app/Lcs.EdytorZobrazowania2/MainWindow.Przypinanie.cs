using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorZobrazowania2;

/// <summary>
/// Etap E4 (PLAN_EDYTOR_ZOBRAZOWANIA_V2.md § 6, § 9): „Przypnij do UT” (F8), lista „do wyjaśnienia” z kandydatami (najazd – podświetlenie
/// na obrazie i w podglądzie UT, wybór – przypięcie potwierdzone), obiekty UT nieumieszczone, „Sprawdź” (F7) z „Weź z UT”, podgląd UT.
/// Logika – <see cref="ObrPrzypinanie"/>, <see cref="ObrSprawdz"/> w Lcs.Model.
/// </summary>
public partial class MainWindow
{
    public readonly Button SprawdzBtn = new() { Content = "✔ Sprawdź", Padding = new Thickness(6, 2, 6, 2), ToolTip = "Sprawdź obraz (F7): przypięcia, opisy różne od UT, Ie-104 § 4 ust. 15, nieumieszczone obiekty UT" },
        PrzypnijBtn = new() { Content = "Przypnij do UT", Padding = new Thickness(6, 2, 6, 2), ToolTip = "Przypnij do UT (F8): kotwice po numerach i nazwach, tory i km między nimi; czego program nie jest pewny – lista „do wyjaśnienia”" };
    /// <summary>Zakładka „Do wyjaśnienia”: lista do wyjaśnienia, nieumieszczone, wynik „Sprawdź”.</summary>
    public readonly StackPanel WyjPanel = new() { Margin = new Thickness(10) };
    public readonly UtPodglad PodgladUt = new() { Height = 220, Margin = new Thickness(0, 4, 0, 8) };
    /// <summary>Ostatni wynik „Sprawdź” (null – nie uruchomiono).</summary>
    public List<ObrUwaga> Uwagi { get; private set; }
    UtProject podgladUt;

    public ObrPrzypinanie.Wynik Przypnij()
    {
        if (Doc == null || Obraz == null) return null;
        ObrPrzypinanie.Wynik w = null;
        Ch("Przypnij do UT", o => w = ObrPrzypinanie.Przypnij(Doc.Ut, o));
        w ??= ObrPrzypinanie.Przypnij(Doc.Ut, Obraz);       // bez zmian w przypięciach – sam wynik
        Side.SelectedItem = TabWyjasnienia;
        Message($"Przypięte: pewne {w.Pewne}, potwierdzone {w.Potwierdzone}; do wyjaśnienia {w.DoWyjasnienia}; bez przypięcia {w.Brak}");
        return w;
    }

    public List<ObrUwaga> Sprawdz()
    {
        if (Doc == null || Obraz == null) return new();
        Uwagi = ObrSprawdz.Sprawdz(Doc.Ut, Obraz);
        Side.SelectedItem = TabWyjasnienia;
        RefreshWyjasnienia();
        int b = Uwagi.Count(u => u.Blad);
        Message(b == 0 && Uwagi.Count == 0 ? "Sprawdź: bez uwag" : $"Sprawdź: błędów {b}, ostrzeżeń {Uwagi.Count - b}");
        return Uwagi;
    }

    public void PotwierdzPrzypiecie(string el, string utId)
    {
        Ch("Potwierdzenie przypięcia", o => { ObrPrzypinanie.Potwierdz(o, el, utId, Doc.Ut); ObrPrzypinanie.Przypnij(Doc.Ut, o); });
        Message($"{ObrSprawdz.Opis(Obraz, el)} → {ObrPrzypinanie.Opis(Doc.Ut, utId)} (potwierdzone)");
    }

    public void BezPrzypiecia(string el)
    {
        Ch("Bez przypięcia", o => ObrPrzypinanie.BezPrzypiecia(o, el));
        Message($"{ObrSprawdz.Opis(Obraz, el)}: bez przypięcia");
    }

    /// <summary>„Weź z UT”: opis elementu = nazwa obiektu UT, do którego jest przypięty.</summary>
    public void WezZUt(string elId)
    {
        var o = Obraz;
        // rozjazd bez numeru na obrazie – nowy numer zwrotnicy z UT
        if (ObrGraf.Wezel(o, elId) != null && o.Przypiecia.FirstOrDefault(x => x.El == elId)?.Ut is string zid && ObrPrzypinanie.NazwaUt(Doc.Ut, zid) is string nr && nr.Length > 0)
        {
            Ch("Numer zwrotnicy z UT", q => { var z = ObrSymbole.ZwrotnicaW(q, elId) ?? ObrSymbole.Nowy(q, ObrSymbole.Zwrotnica, elId, 0, 1); z.Opisy[0].Tekst = nr; });
            if (Uwagi != null) Uwagi = ObrSprawdz.Sprawdz(Doc.Ut, Obraz);
            RefreshWyjasnienia();
            Message($"Numer zwrotnicy z UT: {nr}");
            return;
        }
        var el = o.Elementy.FirstOrDefault(x => x.Id == elId); if (el == null) return;
        // szlak (E6): nazwa sąsiada, numery odstępów
        if (el.Typ == ObrSymbole.Napis && el.P != null && ObrSzlak.SzlakPrzy(o, el.P[0], el.P[1]) is string sl && ObrSzlak.NazwaSasiada(Doc.Ut, sl, o) is string ns)
        {
            Ch("Nazwa sąsiada z UT", q => q.Elementy.First(x => x.Id == elId).Opisy[0].Tekst = ns);
            if (Uwagi != null) Uwagi = ObrSprawdz.Sprawdz(Doc.Ut, Obraz);
            RefreshWyjasnienia(); Message($"Nazwa sąsiada z UT: {ns}"); return;
        }
        if (el.Typ == ObrSymbole.OdstepySbl && o.Przypiecia.FirstOrDefault(x => x.El == elId)?.Ut is string ol)
        {
            var nrs = ObrSzlak.Numery(Doc.Ut, o, el, ol);
            Ch("Numery odstępów z UT", q => { var e = q.Elementy.First(x => x.Id == elId); for (int i = 0; i < e.Opisy.Count && i < nrs.Count; i++) if (nrs[i].Length > 0) e.Opisy[i].Tekst = nrs[i]; });
            if (Uwagi != null) Uwagi = ObrSprawdz.Sprawdz(Doc.Ut, Obraz);
            RefreshWyjasnienia(); Message("Numery odstępów z UT"); return;
        }
        var key = el.Typ == ObrSymbole.Zwrotnica ? el.Na : el.Id;
        var p = o.Przypiecia.FirstOrDefault(x => x.El == key);
        var naz = p?.Ut != null ? ObrPrzypinanie.NazwaUt(Doc.Ut, p.Ut) : null;
        if (string.IsNullOrEmpty(naz)) return;
        Ch("Opis z UT", q => { var e = q.Elementy.First(x => x.Id == elId); if (e.Opisy.Count > 0) e.Opisy[0].Tekst = naz; });
        if (Uwagi != null) Uwagi = ObrSprawdz.Sprawdz(Doc.Ut, Obraz);
        RefreshWyjasnienia();
        Message($"Opis z UT: „{naz}”");
    }

    /// <summary>Podświetlenie elementu na obrazie i jego obiektu w podglądzie UT (najazd na pozycję listy).</summary>
    public void Pokaz(string el, IEnumerable<string> utIds = null)
    {
        var o = Obraz; if (o == null) return;
        if (ObrGraf.Wezel(o, el) != null) View.Select(new[] { el }, null);
        else if (ObrGraf.Odcinek(o, el) != null) View.Select(null, new[] { el });
        else if (o.Elementy.Any(x => x.Id == el)) View.Select(null, null, new[] { el });
        PodgladUt.HiTory.Clear(); PodgladUt.HiPunkty.Clear();
        var p = o.Przypiecia.FirstOrDefault(x => x.El == el);
        foreach (var id in (utIds ?? Enumerable.Empty<string>()).Concat(p?.Ut != null ? new[] { p.Ut } : Array.Empty<string>()).Append(el))
        {
            if (Doc.Ut.Tracks.Any(t => t.Id == id)) PodgladUt.HiTory.Add(id);
            else if (ObrPrzypinanie.Miejsce(Doc.Ut, id) is var (tr, s)) PodgladUt.HiPunkty.Add((tr, s));
        }
        if (p?.Tor?.Tor != null) PodgladUt.HiTory.Add(p.Tor.Tor);
        PodgladUt.InvalidateVisual();
    }

    void UtPanelPrzypiecia(UtProject u)
    {
        if (!ReferenceEquals(podgladUt, u)) { PodgladUt.SetUt(u); podgladUt = u; }
        PodgladUt.Przypiete = Obraz?.Przypiecia.Where(p => p.Tor?.Tor != null && p.Stan != ObrPrzypiecie.DoWyjasnienia).Select(p => p.Tor.Tor).ToHashSet() ?? new();
        if (PodgladUt.Parent is Panel pp) pp.Children.Remove(PodgladUt);
        UtPanel.Children.Add(new TextBlock { Text = "Podgląd UT", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 0) });
        UtPanel.Children.Add(PodgladUt);
        UtPanel.Children.Add(Note("Ciemne – tory przypięte do obrazu, jasne – pozostałe, pomarańczowe – obiekt wskazany na liście „do wyjaśnienia”."));
        if (Obraz == null) return;
        var nie = ObrPrzypinanie.Nieumieszczone(u, Obraz);
        UtPanel.Children.Add(new TextBlock { Text = $"Nieumieszczone na obrazie ({nie.Count})", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 4) });
        foreach (var g in nie.GroupBy(x => x.Grupa))
        {
            UtPanel.Children.Add(Label(g.Key));
            foreach (var x in g) UtPanel.Children.Add(Note("• " + x.Opis));
        }
    }

    static readonly Brush WyjBg = new SolidColorBrush(Color.FromRgb(0xfb, 0xf3, 0xe0)), WyjBorder = new SolidColorBrush(Color.FromRgb(0xe0, 0xb8, 0x60)),
        BladFg = new SolidColorBrush(Color.FromRgb(0xb0, 0x30, 0x20));

    /// <summary>Zakładka „Do wyjaśnienia”: stan przypięć, pozycje z kandydatami, wynik „Sprawdź”.</summary>
    void RefreshWyjasnienia()
    {
        WyjPanel.Children.Clear();
        WyjPanel.Children.Add(Header("Do wyjaśnienia"));
        var o = Obraz;
        if (Doc == null || o == null) { WyjPanel.Children.Add(Note("Brak otwartego zobrazowania.")); return; }
        if (o.Przypiecia.Count == 0)
        {
            WyjPanel.Children.Add(Note("Obraz nie jest jeszcze przypięty do UT. Narysuj tory i symbole, nadaj numery zwrotnic i nazwy semaforów, potem „Przypnij do UT” (F8)."));
        }
        else
        {
            int pw = o.Przypiecia.Count(p => p.Stan == ObrPrzypiecie.Pewne), po = o.Przypiecia.Count(p => p.Stan == ObrPrzypiecie.Potwierdzone), br = o.Przypiecia.Count(p => p.Stan == ObrPrzypiecie.Brak);
            var wyj = o.Przypiecia.Where(p => p.Stan == ObrPrzypiecie.DoWyjasnienia).ToList();
            WyjPanel.Children.Add(Note($"Przypięte: pewne {pw}, potwierdzone {po}, bez przypięcia {br}; do wyjaśnienia {wyj.Count}. Zmiana rysunku nie gubi przypięć – po zmianach F8 jeszcze raz."));
            if (wyj.Count == 0) WyjPanel.Children.Add(Note("Lista pusta – wszystko przypięte."));
            // łańcuchy i tory nienarysowane – raz dla całej listy (dla każdej pozycji osobno edytor zacinał się na kilka sekund)
            var lan = ObrPrzypinanie.Lancuchy(o); var nien = ObrSzkic.Nienarysowane(Doc.Ut, o).ToHashSet();
            foreach (var p in wyj) WyjPanel.Children.Add(Pozycja(p, lan, nien));
        }
        if (Uwagi != null)
        {
            // „Sprawdź” – aktualne dla bieżącego rysunku
            Uwagi = ObrSprawdz.Sprawdz(Doc.Ut, o);
            int b = Uwagi.Count(u => u.Blad);
            WyjPanel.Children.Add(new TextBlock { Text = $"Sprawdź – błędów {b}, ostrzeżeń {Uwagi.Count - b}", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 4) });
            if (Uwagi.Count == 0) WyjPanel.Children.Add(Note("Bez uwag."));
            foreach (var u in Uwagi)
            {
                var row = new DockPanel { Margin = new Thickness(0, 1, 0, 1) };
                if (u.Akcja == ObrSprawdz.WezZUt)
                {
                    var bz = Btn("Weź z UT", (s, e) => WezZUt(u.El), "Opis na obrazie = nazwa w UT");
                    DockPanel.SetDock(bz, System.Windows.Controls.Dock.Right); row.Children.Add(bz);
                }
                var t = new TextBlock { Text = (u.Blad ? "✖ " : "⚠ ") + u.Tekst, TextWrapping = TextWrapping.Wrap, Foreground = u.Blad ? BladFg : Brushes.Black, Cursor = Cursors.Hand };
                t.MouseLeftButtonUp += (s, e) => { if (u.El != null) Pokaz(u.El); };
                row.Children.Add(t);
                WyjPanel.Children.Add(row);
            }
        }
    }

    /// <summary>Tor UT do wyboru: numer (albo „bez numeru”), posterunek, długość i id z UT – da się go odróżnić i znaleźć w edytorze UT.</summary>
    string OpisToruUt(string id)
    {
        if (Doc.Ut.Tracks.FirstOrDefault(t => t.Id == id) is not UtTrack tr) return ObrPrzypinanie.Opis(Doc.Ut, id);
        var post = Posterunki.Of(Doc.Ut, tr.Id);
        return $"{ObrPrzypinanie.Opis(Doc.Ut, id)} – {(post != null ? (post.Kod.Length > 0 ? post.Kod : post.Nazwa) + ", " : "")}{ObrPrzypinanie.Dlugosci(Doc.Ut).GetValueOrDefault(id):0} m, id {id}";
    }

    static string StanNazwa(string stan) => stan switch
    {
        ObrPrzypiecie.Pewne => "przypięty (pewne)", ObrPrzypiecie.Potwierdzone => "przypięty (potwierdzone)", ObrPrzypiecie.DoWyjasnienia => "do wyjaśnienia", ObrPrzypiecie.Brak => "bez przypięcia", _ => stan,
    };

    /// <summary>
    /// Właściwości odcinka toru: przypięcie do toru UT (tor z id, km od – do, odcinki kontroli niezajętości UT na tym kawałku) i wybór toru UT
    /// (uwaga właściciela 2026-10-22: „tory i odcinki nie mają ID – jak je przypiąć do UT?”).
    /// </summary>
    void PrzypiecieToru(ObrOdcinek e)
    {
        var o = Obraz; var ut = Doc.Ut;
        var p = o.Przypiecia.FirstOrDefault(x => x.El == e.Id);
        PropPanel.Children.Add(Label("Przypięcie do UT"));
        if (p == null) PropPanel.Children.Add(Note("Jeszcze nie przypięty – „Przypnij do UT” (F8) albo wybierz tor UT poniżej."));
        else
        {
            var tor = p.Tor?.Tor ?? (ut.Tracks.Any(t => t.Id == p.Ut) ? p.Ut : null);
            PropPanel.Children.Add(Note($"{StanNazwa(p.Stan)}" + (tor != null ? $": {OpisToruUt(tor)}" : "") + (p.Tor != null ? $", km {p.Tor.S0:0}–{p.Tor.S1:0}" : "")
                + (p.Stan == ObrPrzypiecie.DoWyjasnienia && p.Powod != null ? $" – {p.Powod}" : "")));
            if (p.Tor?.Tor != null)
            {
                var od = ObrPrzypinanie.OdcinkiNaTorze(ut, p.Tor.Tor, p.Tor.S0, p.Tor.S1);
                PropPanel.Children.Add(Label("Odcinki kontroli niezajętości (z UT – nazwy i granice nadaje się w edytorze UT)"));
                PropPanel.Children.Add(Note(od.Count == 0 ? "brak – na torze UT nie ma liczników osi ani nazw odcinków" : string.Join("; ", od.Select(x => $"{x.Nazwa} (km {x.S0:0}–{x.S1:0})"))));
            }
        }
        // wybór toru UT – tory posterunków obrazu, najpierw przy rozjazdach na końcach tego toru
        var tory = ObrPrzypinanie.ToryObrazu(ut, o).Where(id => ut.Tracks.Any(t => t.Id == id)).ToList();
        var kand = ObrPrzypinanie.KandydaciPoKolei(ut, o, new ObrPrzypiecie { El = e.Id, Kandydaci = tory });
        var box = new ComboBox { Margin = new Thickness(0, 2, 0, 4), ToolTip = "Przypnij ten tor rysunku do wybranego toru UT (potwierdzone); km wyliczy F8" };
        box.Items.Add("(wybierz tor UT…)");
        foreach (var k in kand) box.Items.Add(OpisToruUt(k));
        box.SelectedIndex = 0;
        box.SelectionChanged += (s, x) => { if (box.SelectedIndex > 0) PotwierdzPrzypiecie(e.Id, kand[box.SelectedIndex - 1]); };
        PropPanel.Children.Add(Label("Przypnij do toru UT"));
        PropPanel.Children.Add(box);
        var row = new WrapPanel();
        if (p?.Tor?.Tor is string pt) row.Children.Add(Btn("Pokaż w UT", (s, x) => { Side.SelectedItem = TabUt; Pokaz(e.Id, new[] { pt }); }, "Zakładka UT – ten tor w podglądzie UT"));
        row.Children.Add(Btn("bez przypięcia", (s, x) => BezPrzypiecia(e.Id), "Tor rysunku bez toru w UT – zostaje na obrazie bez przypięcia"));
        PropPanel.Children.Add(row);
    }

    /// <summary>Właściwości rozjazdu / skrzyżowania: przypięcie do obiektu UT (z id) i wybór rozjazdu UT.</summary>
    void PrzypiecieWezla(ObrWezel w)
    {
        var o = Obraz; var ut = Doc.Ut;
        var p = o.Przypiecia.FirstOrDefault(x => x.El == w.Id);
        PropPanel.Children.Add(Label("Przypięcie do UT"));
        PropPanel.Children.Add(Note(p == null ? "Jeszcze nie przypięty – nadaj numer zwrotnicy (E) i F8 albo wybierz rozjazd UT poniżej."
            : $"{StanNazwa(p.Stan)}" + (p.Ut != null ? $": {ObrPrzypinanie.Opis(ut, p.Ut)}, id {p.Ut}" : "") + (p.Stan == ObrPrzypiecie.DoWyjasnienia && p.Powod != null ? $" – {p.Powod}" : "")));
        var tory = ObrPrzypinanie.ToryObrazu(ut, o);
        var rz = ut.Turnouts.Where(z => tory.Contains(z.Track)).OrderBy(z => z.Number.Length == 0 ? 1 : 0).ThenBy(z => z.Number).ToList();
        var box = new ComboBox { Margin = new Thickness(0, 2, 0, 4), ToolTip = "Przypnij ten rozjazd rysunku do wybranego rozjazdu UT (potwierdzone)" };
        box.Items.Add("(wybierz rozjazd UT…)");
        foreach (var z in rz) box.Items.Add($"{ObrPrzypinanie.Opis(ut, z.Id)}, id {z.Id}");
        box.SelectedIndex = 0;
        box.SelectionChanged += (s, x) => { if (box.SelectedIndex > 0) PotwierdzPrzypiecie(w.Id, rz[box.SelectedIndex - 1].Id); };
        PropPanel.Children.Add(Label("Przypnij do rozjazdu UT"));
        PropPanel.Children.Add(box);
    }

    /// <summary>Pozycja listy: element, powód, przyciski kandydatów i „bez przypięcia”; najazd – podświetlenie.</summary>
    UIElement Pozycja(ObrPrzypiecie p, List<ObrPrzypinanie.Lancuch> lan, HashSet<string> nien)
    {
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = ObrSprawdz.Opis(Obraz, p.El), FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        sp.Children.Add(new TextBlock { Text = p.Powod ?? "", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, FontSize = 12, Margin = new Thickness(0, 0, 0, 4) });
        var wrap = new WrapPanel();
        // kandydaci po kolei (tory przy rozjazdach na końcach najpierw); tory bez numeru – z posterunkiem i długością, żeby dało się je odróżnić
        var kand = ObrPrzypinanie.KandydaciPoKolei(Doc.Ut, Obraz, p, lan, nien);
        var opisy = kand.Select(k => ObrPrzypinanie.Opis(Doc.Ut, k)).ToList();
        string Etykieta(int i)
        {
            var k = kand[i];
            if (Doc.Ut.Tracks.FirstOrDefault(t => t.Id == k) is not UtTrack tr || (tr.Number.Length > 0 && opisy.Count(x => x == opisy[i]) == 1)) return opisy[i];
            var post = Posterunki.Of(Doc.Ut, tr.Id);
            return $"{opisy[i]} ({(post != null ? (post.Kod.Length > 0 ? post.Kod : post.Nazwa) + ", " : "")}{ObrPrzypinanie.Dlugosci(Doc.Ut).GetValueOrDefault(tr.Id):0} m)";
        }
        const int Pierwsze = 10;
        for (int i = 0; i < kand.Count; i++)
        {
            var kid = kand[i];
            var b = Btn(Etykieta(i), (s, e) => PotwierdzPrzypiecie(p.El, kid), "Przypnij do tego obiektu UT (potwierdzone); najazd – pokazuje go w podglądzie UT");
            b.Margin = new Thickness(0, 0, 4, 4); b.Tag = kid;
            b.MouseEnter += (s, e) => Pokaz(p.El, new[] { kid });
            if (i >= Pierwsze) b.Visibility = Visibility.Collapsed;
            wrap.Children.Add(b);
        }
        if (kand.Count > Pierwsze)
        {
            Button wiecej = null;
            wiecej = Btn($"więcej ({kand.Count - Pierwsze})…", (s, e) => { foreach (var c in wrap.Children.OfType<Button>()) c.Visibility = Visibility.Visible; wiecej.Visibility = Visibility.Collapsed; }, "Pokaż pozostałych kandydatów");
            wiecej.Margin = new Thickness(0, 0, 4, 4);
            wrap.Children.Add(wiecej);
        }
        var bez = Btn("bez przypięcia", (s, e) => BezPrzypiecia(p.El), "Element bez obiektu w UT (np. przerwa, której nie ma w UT) – zostaje na obrazie bez przypięcia");
        bez.Margin = new Thickness(0, 0, 4, 4);
        wrap.Children.Add(bez);
        sp.Children.Add(wrap);
        var bd = new Border { Child = sp, Background = WyjBg, BorderBrush = WyjBorder, BorderThickness = new Thickness(1), Padding = new Thickness(6), Margin = new Thickness(0, 0, 0, 6), Tag = p.El };
        bd.MouseEnter += (s, e) => Pokaz(p.El, p.Kandydaci);
        return bd;
    }
}
