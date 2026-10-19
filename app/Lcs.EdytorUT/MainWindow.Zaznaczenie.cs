using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>Zaznaczenie kilku elementów (panel, usuwanie naraz, łączenie torów) i menu PPM toru (usunięcie kawałka, rozcięcie).</summary>
public partial class MainWindow
{
    /// <summary>Usunięcie wszystkich zaznaczonych jednym Ctrl+Z: z torem znikają jego rozjazdy i ich odgałęzienia do skosu / tory łączące.</summary>
    public bool DeleteMulti()
    {
        var sel = View.Multi.ToList(); if (sel.Count == 0) return false;
        HashSet<string> Of(string kind) => sel.Where(x => x.Kind == kind).Select(x => x.Id).ToHashSet();
        var tracks = Of(Kontrola.KTor); var turnouts = Of(Kontrola.KRozjazd); var crossings = Of(Kontrola.KSkrzyzowanie); var devices = Of(Kontrola.KUrzadzenie);
        var objs = Of(Kontrola.KOdcinek).Concat(Of(Kontrola.KPeron)).Concat(Of(Kontrola.KPrzejazd)).Concat(Of(Kontrola.KKm)).ToHashSet();
        Doc.Change($"Usunięcie zaznaczonych ({sel.Count})", p =>
        {
            p.Devices.RemoveAll(x => devices.Contains(x.Id));
            p.Sections.RemoveAll(x => objs.Contains(x.Id)); p.Platforms.RemoveAll(x => objs.Contains(x.Id));
            p.LevelCrossings.RemoveAll(x => objs.Contains(x.Id)); p.KmPoints.RemoveAll(x => objs.Contains(x.Id));
            p.Crossings.RemoveAll(x => crossings.Contains(x.Id));
            // rozjazdy: z łącznikiem (partner) i odgałęzieniem
            var rz = p.Turnouts.Where(x => turnouts.Contains(x.Id)).ToList();
            var rzIds = rz.SelectMany(x => new[] { x.Id, x.Partner }).Where(x => x != null).ToHashSet();
            var gone = rz.Select(x => x.Branch).Where(x => x != null).ToHashSet();
            // tory: z ich rozjazdami; odgałęzienia do skosu i tory łączące – też, przedłużone (bocznice) zostają
            foreach (var r in p.Turnouts.Where(r => tracks.Contains(r.Track)))
            {
                rzIds.Add(r.Id); if (r.Partner != null) rzIds.Add(r.Partner);
                if (p.Tracks.FirstOrDefault(x => x.Id == r.Branch) is UtTrack br && (r.Partner != null || br.Vertices.Count <= 3)) gone.Add(br.Id);
            }
            gone.UnionWith(tracks);
            p.Turnouts.RemoveAll(x => rzIds.Contains(x.Id));
            p.Tracks.RemoveAll(x => gone.Contains(x.Id));
        });
        View.ClearMulti(); View.Select(null);
        Message($"Usunięte: {string.Join(", ", sel.GroupBy(x => x.Kind).Select(g => $"{UtView.KindName(g.Key, g.Count())} {g.Count()}"))} (Ctrl+Z przywraca)");
        return true;
    }

    void BuildMultiPanel()
    {
        var P = TrackPanel;
        P.Children.Add(Header($"Zaznaczone: {View.Multi.Count}"));
        foreach (var g in View.Multi.GroupBy(x => x.Kind))
            P.Children.Add(new TextBlock { Text = $"{UtView.KindName(g.Key, g.Count())}: {string.Join(", ", g.Select(x => Name(x.Kind, x.Id)))}", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) });
        P.Children.Add(new TextBlock { Text = "Shift + klik (albo Ctrl + klik) – dodaj / zdejmij element, Shift + przeciągnięcie – prostokąt, Esc – koniec zaznaczenia.", TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = Brushes.DimGray, Margin = new Thickness(0, 4, 0, 6) });
        var act = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        var tory = View.Multi.Where(x => x.Kind == Kontrola.KTor).Select(x => x.Id).ToList();
        if (tory.Count >= 2)
            act.Children.Add(SmallBtn($"Połącz tory ({tory.Count})", () => JoinTracks(tory),
                "Zaznaczone tory w jeden (Ctrl+J): najbliższe końce stykiem albo stycznie łukiem R z paska (łuki odwrotne przy przesunięciu); wszystko, co na nich leży, przechodzi na tor wynikowy"));
        act.Children.Add(SmallBtn("Usuń zaznaczone", () => DeleteMulti(), "Usuń wszystkie zaznaczone elementy (Del); jedno Ctrl+Z przywraca"));
        act.Children.Add(SmallBtn("Odznacz", () => View.ClearMulti(), "Koniec zaznaczenia (Esc)"));
        P.Children.Add(act);
        PostCombo(P, View.Multi.Where(x => x.Kind == Kontrola.KTor).Select(x => x.Id).ToList(), "Posterunek zaznaczonych torów");
        P.Children.Add(new Separator { Margin = new Thickness(0, 4, 0, 10) });
    }

    string Name(string kind, string id)
    {
        var p = Doc.P;
        string n = kind switch
        {
            Kontrola.KTor => p.Tracks.FirstOrDefault(x => x.Id == id)?.Number,
            Kontrola.KRozjazd => p.Turnouts.FirstOrDefault(x => x.Id == id)?.Number,
            Kontrola.KSkrzyzowanie => p.Crossings.FirstOrDefault(x => x.Id == id)?.Number,
            Kontrola.KUrzadzenie => p.Devices.FirstOrDefault(x => x.Id == id)?.Name,
            Kontrola.KOdcinek => p.Sections.FirstOrDefault(x => x.Id == id)?.Name,
            Kontrola.KPeron => p.Platforms.FirstOrDefault(x => x.Id == id)?.Name,
            Kontrola.KPrzejazd => p.LevelCrossings.FirstOrDefault(x => x.Id == id)?.Name,
            Kontrola.KKm => p.KmPoints.FirstOrDefault(x => x.Id == id) is UtKmPoint k ? $"{k.Line} km {k.Km:0.000}" : null,
            Kontrola.KSzlak => p.Lines.FirstOrDefault(x => x.Id == id)?.Numer,
            Kontrola.KPosterunek => p.Posts.FirstOrDefault(x => x.Id == id)?.Kod,
            _ => null,
        };
        return string.IsNullOrEmpty(n) ? "(bez nazwy)" : n;
    }

    /// <summary>Menu PPM na torze: połącz zaznaczone, usuń kawałek pod kursorem (prosta albo łuk), rozetnij tutaj, usuń tor.</summary>
    public ContextMenu BuildTrackMenu(string id, double s)
    {
        var cm = new ContextMenu();
        var t = Doc.P.Tracks.FirstOrDefault(x => x.Id == id); if (t == null) return cm;
        // akcje na jednym torze kończą zaznaczenie kilku
        MenuItem Item(string h, Action a, bool en = true, string tip = null) { var mi = new MenuItem { Header = h, IsEnabled = en, ToolTip = tip }; mi.Click += (o, e) => { View.ClearMulti(); a(); }; cm.Items.Add(mi); return mi; }
        cm.Items.Add(new MenuItem { Header = t.Number.Length > 0 ? $"Tor {t.Number}" : "Tor (bez numeru)", IsEnabled = false, FontWeight = FontWeights.SemiBold });
        var tory = View.Multi.Where(x => x.Kind == Kontrola.KTor).Select(x => x.Id).ToList();
        if (tory.Count >= 2 && tory.Contains(id))
        {
            var mj = new MenuItem { Header = $"Połącz zaznaczone tory ({tory.Count}) – Ctrl+J", ToolTip = "Najbliższe końce stykiem albo stycznie łukiem R z paska" };
            mj.Click += (o, e) => JoinTracks(tory); cm.Items.Add(mj); cm.Items.Add(new Separator());
        }
        var r = View.Geometry(t);
        if (Ciecie.KawalekAt(r, s) is TorKawalek k)
        {
            string what = k.Luk ? $"łuk W{k.Vertex + 1}" : "prostą";
            Item($"Usuń ten kawałek – {what}, km {F(k.S0)}–{F(k.S1)} ({F(k.Len)} m)", () => CutTrack(id, k.S0, k.S1, "Usunięcie kawałka toru"),
                tip: "Tor zostaje rozcięty na dwa (albo skrócony, gdy to koniec); co leży na usuniętym kawałku – znika");
        }
        if (Ciecie.KawalekAt(r, s) is { Luk: true } kl)
            Item($"Podziel łuk W{kl.Vertex + 1} tutaj (dwa łuki – potem każdy może mieć inny promień)", () => SplitArc(id, kl.Vertex, s),
                tip: "Łuk dzieli się w tym miejscu na dwa o tym samym promieniu (kształt bez zmian); „Łuk z punktów” (J) dopasuje potem promienie wszystkich części naraz");
        if (Ciecie.KawalekAt(r, s) is { Luk: true } kg && Budowa.ArcGroup(t.Vertices, kg.Vertex, Trasa.KFor(t), Budowa.SmoothStraight, true) is var grp && grp.Count > 1)
        {
            var sm = new MenuItem { Header = $"Wygładź łuk W{grp.Min() + 1}–W{grp.Max() + 1} ({grp.Count} części)", ToolTip = "Cała grupa łuków → jeden łuk (albo dwa) jak najbliżej obecnego kształtu; skrajne proste zostają" };
            foreach (var (txt, mode) in new[] { ("→ jeden łuk z krzywymi przejściowymi", 0), ("→ jeden łuk bez przejściowych", 1), ("→ dwa łuki", 2) })
            {
                var it = new MenuItem { Header = txt }; int md = mode; it.Click += (o, e) => SmoothArc(id, kg.Vertex, md); sm.Items.Add(it);
            }
            cm.Items.Add(sm);
        }
        if (Ciecie.KawalekAt(r, s) is { Luk: true } kn)
        {
            var sub = new MenuItem { Header = $"Podziel łuk W{kn.Vertex + 1} na części", ToolTip = "Więcej wierzchołków na łuku – każdą część dostosujesz osobno (przeciągnięcie wierzchołka, R w panelu albo „Łuk z punktów” na całość)" };
            foreach (var nn in new[] { 2, 3, 4, 6 })
            {
                var it = new MenuItem { Header = $"{nn} części" }; int cnt = nn;
                it.Click += (o, e) => SplitArcN(id, kn.Vertex, cnt);
                sub.Items.Add(it);
            }
            cm.Items.Add(sub);
        }
        Item($"Rozetnij tor tutaj (km {F(s)})", () => SplitTrack(id, s), true, "Dwa tory stykające się w tym miejscu; w łuku – łuk dzieli się na dwa o tym samym R, kształt bez zmian");
        cm.Items.Add(new Separator());
        Item("Usuń cały tor", () => { View.Select(id); DeleteSelection(); });
        return cm;
    }

    /// <summary>Podział łuku na dwa w miejscu km s (<see cref="Budowa.SplitArc"/>).</summary>
    public void SplitArc(string id, int vi, double s)
    {
        var t = Doc.P.Tracks.FirstOrDefault(x => x.Id == id); if (t == null) return;
        if (View.Locked(id, vi)) { Message("Ten łuk ustawia rozjazd – nie da się go podzielić"); return; }
        var (nv, err) = Budowa.SplitArc(t.Vertices, vi, Trasa.KFor(t), s);
        if (err != null) { Message("Nie da się podzielić: " + err); return; }
        ChTrack(id, "Podział łuku", x => x.Vertices = nv);
        View.Select(id, vi);
        Message($"Łuk W{vi + 1} podzielony na dwa (W{vi + 1} i W{vi + 2}, oba R {F(nv[vi].R, "0")} m) – „Łuk z punktów” (J) dopasuje promienie obu części naraz (Ctrl+Z przywraca)");
    }

    /// <summary>Wygładzenie łuku złożonego (<see cref="Budowa.SmoothArc"/>).</summary>
    public void SmoothArc(string id, int vi, int mode)
    {
        var t = Doc.P.Tracks.FirstOrDefault(x => x.Id == id); if (t == null) return;
        var grp = Budowa.ArcGroup(t.Vertices, vi, Trasa.KFor(t), Budowa.SmoothStraight, true);
        if (grp.Any(i => View.Locked(id, i))) { Message("W tym łuku jest wierzchołek ustawiany przez rozjazd – nie da się go wygładzić"); return; }
        var (nv, max, err) = Budowa.SmoothArc(t.Vertices, vi, Trasa.KFor(t), mode);
        if (err != null) { Message("Nie da się wygładzić: " + err); return; }
        ChTrack(id, "Wygładzenie łuku", x => x.Vertices = nv);
        int lo = grp.Min(); View.Select(id, lo);
        string what = mode == 2 ? $"dwa łuki R {F(nv[lo].R, "0")} / {F(nv[lo + 1].R, "0")} m" : $"jeden łuk R {F(nv[lo].R, "0")} m" + (nv[lo].L > 0 ? $", L {F(nv[lo].L, "0")} m" : "");
        Message($"Łuk W{lo + 1}–W{grp.Max() + 1} ({grp.Count} części) wygładzony: {what}; najwyżej {max * 100:0.#} cm od dotychczasowego kształtu (Ctrl+Z przywraca)");
    }

    /// <summary>Podział łuku na n równych części (<see cref="Budowa.SplitArcN"/>).</summary>
    public void SplitArcN(string id, int vi, int n)
    {
        var t = Doc.P.Tracks.FirstOrDefault(x => x.Id == id); if (t == null) return;
        if (View.Locked(id, vi)) { Message("Ten łuk ustawia rozjazd – nie da się go podzielić"); return; }
        var (nv, err) = Budowa.SplitArcN(t.Vertices, vi, Trasa.KFor(t), n);
        if (err != null) { Message("Nie da się podzielić: " + err); return; }
        ChTrack(id, "Podział łuku", x => x.Vertices = nv);
        View.Select(id, vi);
        Message($"Łuk W{vi + 1} podzielony na {n} części (W{vi + 1}–W{vi + n}, R {F(nv[vi].R, "0")} m) – przeciągaj wierzchołki, zmieniaj R w panelu albo „Łuk z punktów” (J) na całość (Ctrl+Z przywraca)");
    }

    /// <summary>Usunięcie kawałka / rozcięcie – najpierw na kopii (komunikat o przeszkodzie), potem jedna zmiana z cofaniem.</summary>
    public CiecieWynik CutTrack(string id, double sa, double sb, string undo)
    {
        var test = Ciecie.Cut(UtDocument.Deserialize(UtDocument.Serialize(Doc.P)), id, sa, sb);
        if (test.Error != null) { Message("Nie da się: " + test.Error); return test; }
        CiecieWynik res = null;
        Doc.Change(undo, p => res = Ciecie.Cut(p, id, sa, sb));
        View.Select(id);
        Message(char.ToUpper(res.Info[0]) + res.Info[1..] + " (Ctrl+Z przywraca)");
        return res;
    }

    /// <summary>Rozcięcie toru w km s – także w łuku (<see cref="Ciecie.Split"/>); najpierw na kopii, potem jedna zmiana z cofaniem.</summary>
    public CiecieWynik SplitTrack(string id, double s)
    {
        var test = Ciecie.Split(UtDocument.Deserialize(UtDocument.Serialize(Doc.P)), id, s);
        if (test.Error != null) { Message("Nie da się rozciąć: " + test.Error); return test; }
        CiecieWynik res = null;
        Doc.Change("Rozcięcie toru", p => res = Ciecie.Split(p, id, s));
        View.Select(id);
        Message(char.ToUpper(res.Info[0]) + res.Info[1..] + " (Ctrl+Z przywraca)");
        return res;
    }

    /// <summary>Zaznaczone tory w jeden (<see cref="Laczenie.Join"/>) – R i L z paska; najpierw na kopii, potem jedna zmiana z cofaniem.</summary>
    public LaczenieWynik JoinTracks(IReadOnlyList<string> ids)
    {
        double r = View.NewR > 0 ? View.NewR : 300, l = View.NewL;
        var test = Laczenie.Join(UtDocument.Deserialize(UtDocument.Serialize(Doc.P)), ids, r, l);
        if (test.Error != null) { Message("Nie da się połączyć: " + test.Error); return test; }
        LaczenieWynik res = null;
        Doc.Change($"Połączenie torów ({ids.Count})", p => res = Laczenie.Join(p, ids, r, l));
        View.ClearMulti(); View.Select(res.Track);
        var tr = Doc.P.Tracks.First(x => x.Id == res.Track);
        Message($"Połączone w jeden tor ({UtView.Len3(View.Geometry(tr).Length)}): {res.Info} (Ctrl+Z przywraca)");
        return res;
    }

    /// <summary>Ctrl+J: połączenie zaznaczonych torów.</summary>
    public bool JoinSelected()
    {
        var tory = View.Multi.Where(x => x.Kind == Kontrola.KTor).Select(x => x.Id).ToList();
        if (tory.Count < 2) { Message("Łączenie: zaznacz co najmniej dwa tory (Shift + klik)"); return false; }
        return JoinTracks(tory).Error == null;
    }
}
