using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>Zaznaczenie kilku elementów (panel, usuwanie naraz) i menu PPM toru (usunięcie kawałka, rozcięcie).</summary>
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
        P.Children.Add(new TextBlock { Text = "Ctrl + klik – dodaj / zdejmij element, Shift + przeciągnięcie od pustego miejsca – prostokąt, Esc – koniec zaznaczenia.", TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = Brushes.DimGray, Margin = new Thickness(0, 4, 0, 6) });
        var act = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        act.Children.Add(SmallBtn("Usuń zaznaczone", () => DeleteMulti(), "Usuń wszystkie zaznaczone elementy (Del); jedno Ctrl+Z przywraca"));
        act.Children.Add(SmallBtn("Odznacz", () => View.ClearMulti(), "Koniec zaznaczenia (Esc)"));
        P.Children.Add(act);
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
            _ => null,
        };
        return string.IsNullOrEmpty(n) ? "(bez nazwy)" : n;
    }

    /// <summary>Menu PPM na torze: usuń kawałek pod kursorem (prosta albo łuk), rozetnij tutaj, usuń tor.</summary>
    public ContextMenu BuildTrackMenu(string id, double s)
    {
        var cm = new ContextMenu();
        var t = Doc.P.Tracks.FirstOrDefault(x => x.Id == id); if (t == null) return cm;
        MenuItem Item(string h, Action a, bool en = true, string tip = null) { var mi = new MenuItem { Header = h, IsEnabled = en, ToolTip = tip }; mi.Click += (o, e) => a(); cm.Items.Add(mi); return mi; }
        cm.Items.Add(new MenuItem { Header = t.Number.Length > 0 ? $"Tor {t.Number}" : "Tor (bez numeru)", IsEnabled = false, FontWeight = FontWeights.SemiBold });
        var r = View.Geometry(t);
        if (Ciecie.KawalekAt(r, s) is TorKawalek k)
        {
            string what = k.Luk ? $"łuk W{k.Vertex + 1}" : "prostą";
            Item($"Usuń ten kawałek – {what}, km {F(k.S0)}–{F(k.S1)} ({F(k.Len)} m)", () => CutTrack(id, k.S0, k.S1, "Usunięcie kawałka toru"),
                tip: "Tor zostaje rozcięty na dwa (albo skrócony, gdy to koniec); co leży na usuniętym kawałku – znika");
        }
        Item("Rozetnij tor tutaj", () => CutTrack(id, s, s, "Rozcięcie toru"), Ciecie.KawalekAt(r, s) is { Luk: false }, "Dwa tory stykające się w tym miejscu (tylko na prostej)");
        cm.Items.Add(new Separator());
        Item("Usuń cały tor", () => { View.Select(id); DeleteSelection(); });
        return cm;
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
}
