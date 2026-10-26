using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Obiekty inżynieryjne w oknie (§ 5m): rodzaj w narzędziu Obiekt, wstawianie (nazwa z okna), dołożenie toru (Ctrl + klik),
/// rozdzielenie na tory, panel obiektu, menu PPM.
/// </summary>
public partial class MainWindow
{
    public readonly ComboBox ObKindBox = new() { Width = 190, ToolTip = "Rodzaj obiektu inżynieryjnego (Ig-10 zał. 8)" };

    void InitObiekty()
    {
        foreach (var k in Obiekty.Kinds) ObKindBox.Items.Add(k.Name);
        ObKindBox.SelectedIndex = 0;
        ObKindBox.SelectionChanged += (s, e) =>
        {
            if (ObKindBox.SelectedIndex < 0) return;
            View.ObKind = Obiekty.Kinds[ObKindBox.SelectedIndex].Key; View.ObCancel();
            if (View.Tool == UtTool.Obiekt) ToolShown(); else SetTool(UtTool.Obiekt);
        };
        View.ObiektPlace += OnObiektPlace;
        View.ObiektDoloz += DolozTor;
    }

    void OnObiektPlace(string track, double s0, double s1, double angle, double reach, int side)
    {
        var inf = Obiekty.Info(View.ObKind);
        var name = AskName(char.ToUpper(inf.Name[0]) + inf.Name[1..], "Nazwa albo numer obiektu (może być pusty, przeszkodę wpiszesz w panelu)", "");
        if (name == null) return;
        var o = new UtObiekt { Kind = inf.Key, Name = name, Track = track, S0 = s0, S1 = s1, Angle = angle, Reach = reach, Side = side };
        if (inf.Width > 0) o.Width = inf.Width;
        Doc.Change(char.ToUpper(inf.Name[0]) + inf.Name[1..], p => p.Obiekty.Add(o));
        View.SelectObj(Kontrola.KObiekt, o.Id);
        string km = Kilometraz.Text(Doc.P, track, s0);
        Message(inf.Pol == Obiekty.Pol.Poprzeczny
            ? $"{Obiekty.Opis(o)}: tor {TrNr(track)} km {F(s0)}, kąt {F(angle, "0")}°, w obie strony {F(reach)} m" + (km.Length > 0 ? ", " + km : "")
            : $"{Obiekty.Opis(o)}: tor {TrNr(track)} km {F(s0)} – {F(s1)} ({F(o.Len)} m)" + (inf.Pol == Obiekty.Pol.ZBoku ? $" po {(side > 0 ? "prawej" : "lewej")}" : "") + (km.Length > 0 ? ", " + km : ""));
    }

    /// <summary>Dołożenie toru do obiektu wzdłuż toru (km od–do – rzut końców obiektu na ten tor).</summary>
    public void DolozTor(string id, string track)
    {
        var o = Doc.P.Obiekty.FirstOrDefault(x => x.Id == id); if (o == null) return;
        if (o.Track == track || (o.Tory ?? new()).Any(x => x.Track == track)) { Message($"Tor {TrNr(track)} już należy do obiektu"); return; }
        var main = Doc.P.Tracks.FirstOrDefault(x => x.Id == o.Track); var other = Doc.P.Tracks.FirstOrDefault(x => x.Id == track);
        if (main == null || other == null) return;
        var t = Obiekty.Doloz(View.Geometry(main), View.Geometry(other), track, o);
        if (t.S1 - t.S0 < 1) { Message($"Tor {TrNr(track)} nie biegnie wzdłuż obiektu – nie dołożony"); return; }
        Doc.Change("Tor dołożony do obiektu", p => { var x = p.Obiekty.First(q => q.Id == id); (x.Tory ??= new()).Add(t); });
        View.SelectObj(Kontrola.KObiekt, id);
        Message($"{Obiekty.Opis(o)}: dołożony tor {TrNr(track)} km {F(t.S0)} – {F(t.S1)} (Ctrl + klik na kolejnym torze – następny)");
    }

    void Rozdziel(string id)
    {
        int n = Doc.P.Obiekty.FirstOrDefault(x => x.Id == id)?.Tory?.Count ?? 0; if (n == 0) return;
        Doc.Change("Rozdzielenie obiektu na tory", p => { var o = p.Obiekty.First(q => q.Id == id); p.Obiekty.AddRange(Obiekty.Rozdziel(o)); });
        Message($"Obiekt rozdzielony: {n + 1} osobne obiekty, po jednym na torze (Ctrl+Z przywraca)");
    }

    void ObiektMenu(ContextMenu cm, string id)
    {
        var o = Doc.P.Obiekty.FirstOrDefault(x => x.Id == id); if (o == null) return;
        var inf = Obiekty.Info(o.Kind);
        if (inf.Pol == Obiekty.Pol.ZBoku) { var sw = new MenuItem { Header = "Zmień stronę" }; sw.Click += (s, e) => Doc.Change("Strona obiektu", p => { var x = p.Obiekty.First(q => q.Id == id); x.Side = -x.Side; }); cm.Items.Add(sw); }
        if (o.Tory is { Count: > 0 }) { var rz = new MenuItem { Header = "Rozdziel na tory" }; rz.Click += (s, e) => Rozdziel(id); cm.Items.Add(rz); }
        var kind = new MenuItem { Header = "Zmień rodzaj" };
        foreach (var k in Obiekty.Kinds.Where(k => (k.Pol == Obiekty.Pol.Poprzeczny) == (inf.Pol == Obiekty.Pol.Poprzeczny) && k.Key != o.Kind))
        { var kk = k; var mi = new MenuItem { Header = k.Name }; mi.Click += (s, e) => Doc.Change("Rodzaj obiektu", p => SetKind(p.Obiekty.First(q => q.Id == id), kk.Key)); kind.Items.Add(mi); }
        cm.Items.Add(kind);
    }

    /// <summary>Zmiana rodzaju: przy przejściu wzdłuż ↔ poprzeczny km do = km od albo km od + 20 m.</summary>
    static void SetKind(UtObiekt x, string key)
    {
        bool was = Obiekty.Info(x.Kind).Pol == Obiekty.Pol.Poprzeczny, now = Obiekty.Info(key).Pol == Obiekty.Pol.Poprzeczny;
        x.Kind = key;
        if (now && !was) { x.S0 = x.S1 = Math.Round((x.Lo + x.Hi) / 2, 2); x.Tory = null; }
        if (!now && was) x.S1 = x.S0 + 20;
        if (Obiekty.Info(key).Width > 0) x.Width = Obiekty.Info(key).Width;
    }

    void BuildObiektPanel(UtObiekt o)
    {
        var P = TrackPanel; var inf = Obiekty.Info(o.Kind);
        void Ch(string what, Action<UtObiekt> a) => Doc.Change(what, p => { var x = p.Obiekty.FirstOrDefault(q => q.Id == o.Id); if (x != null) a(x); });
        P.Children.Add(Header(char.ToUpper(Obiekty.Opis(o)[0]) + Obiekty.Opis(o)[1..]));
        P.Children.Add(new TextBlock { Text = "Ig-10 § 4 ust. 1 pkt 15a i 20; " + inf.Ig10, Foreground = Brushes.DimGray, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, -4, 0, 6) });
        P.Children.Add(Label("Rodzaj"));
        var kb = new ComboBox { Margin = new Thickness(0, 2, 0, 6) };
        foreach (var k in Obiekty.Kinds) kb.Items.Add(k.Name);
        kb.SelectedIndex = Math.Max(0, Array.FindIndex(Obiekty.Kinds, k => k.Key == o.Kind));
        kb.SelectionChanged += (s, e) => { if (kb.SelectedIndex >= 0 && Obiekty.Kinds[kb.SelectedIndex].Key != o.Kind) Ch("Rodzaj obiektu", x => SetKind(x, Obiekty.Kinds[kb.SelectedIndex].Key)); };
        P.Children.Add(kb);
        Field(P, "Nazwa albo numer", o.Name, n => Ch("Nazwa obiektu", x => x.Name = n));
        Field(P, inf.Pol == Obiekty.Pol.ZBoku ? "Przy czym (opis)" : inf.Pol == Obiekty.Pol.Poprzeczny ? "Co przechodzi (droga, ciek)" : "Co przekracza (droga, rzeka, linia)", o.Nad, n => Ch("Przeszkoda", x => x.Nad = n), "np. ul. Piłsudskiego, rz. Ner, linia nr 17");
        if (inf.Pol == Obiekty.Pol.Poprzeczny)
        {
            Field(P, $"Tor {TrNr(o.Track)} – km [m od początku toru]", F(o.S0), t => { if (Num(t) is double v) Ch("Kilometraż obiektu", x => x.S0 = x.S1 = v); });
            if (Kilometraz.Text(Doc.P, o.Track, o.S0) is var km && km.Length > 0) P.Children.Add(new TextBlock { Text = km, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, -2, 0, 6) });
            Field(P, "Kąt osi przeszkody z osią toru [°]", F(o.Angle, "0.#"), t => { if (Num(t) is double v && v > 0 && v < 180) Ch("Kąt obiektu", x => x.Angle = v); });
            Field(P, "Na planie – w obie strony od osi toru [m]", F(o.Reach), t => { if (Num(t) is double v && v >= 2 && v < 500) Ch("Zasięg obiektu", x => x.Reach = v); });
            Field(P, "Światło / szerokość [m]", F(o.Width), t => { if (Num(t) is double v && v > 0 && v < 200) Ch("Szerokość obiektu", x => x.Width = v); });
        }
        else
        {
            Field(P, $"Tor {TrNr(o.Track)} – km od / do [m od początku toru]", $"{F(o.Lo)} – {F(o.Hi)}", t =>
            {
                var a = t.Split(new[] { '–', '-', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
                if (a.Length == 2 && Num(a[0]) is double s0 && Num(a[1]) is double s1 && Math.Abs(s1 - s0) >= 1) Ch("Kilometraż obiektu", x => { x.S0 = Math.Min(s0, s1); x.S1 = Math.Max(s0, s1); });
                else Message("Obiekt: wpisz km początku i końca, np. „120 – 165”");
            });
            P.Children.Add(new TextBlock { Text = $"Długość {UtView.Len3(o.Len)}" + (Kilometraz.Text(Doc.P, o.Track, o.Lo) is var k0 && k0.Length > 0 ? $"\npoczątek {k0}, koniec {Kilometraz.Text(Doc.P, o.Track, o.Hi)}" : ""), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
            if (o.Kind == Obiekty.Wiadukt || o.Kind == Obiekty.WiaduktTor)
                Field(P, "Kąt osi przeszkody z osią toru [°]", F(o.Angle, "0.#"), t => { if (Num(t) is double v && v > 0 && v < 180) Ch("Kąt obiektu", x => x.Angle = v); });
            if (o.Kind == Obiekty.Wiadukt)
                Field(P, "Szerokość drogi pod wiaduktem [m]", F(o.Width), t => { if (Num(t) is double v && v >= 0 && v < 200) Ch("Szerokość drogi", x => x.Width = v); });
            if (inf.Pol == Obiekty.Pol.ZBoku)
            {
                Field(P, "Odległość od osi toru [m]", F(o.Offset, "0.00"), t => { if (Num(t) is double v && v > 0.5 && v < 50) Ch("Odległość od osi toru", x => x.Offset = v); });
                P.Children.Add(new TextBlock { Text = $"po {(o.Side > 0 ? "prawej" : "lewej")} stronie toru (patrząc w stronę rosnącego km toru)", FontSize = 12, Margin = new Thickness(0, 0, 0, 4) });
                P.Children.Add(SmallBtn("Zmień stronę", () => Ch("Strona obiektu", x => x.Side = -x.Side), "Ściana / mur po drugiej stronie toru"));
            }
            else
            {
                var tory = o.Wszystkie().Select(x => $"tor {TrNr(x.Track)}: km {F(x.S0)} – {F(x.S1)}");
                P.Children.Add(new TextBlock { Text = "Tory obiektu:\n" + string.Join("\n", tory), FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) });
                P.Children.Add(new TextBlock { Text = "Ctrl + klik na innym torze w narzędziu Obiekt – dołóż tor (jeden obiekt pod kilkoma torami)", Foreground = Brushes.DimGray, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) });
                if (o.Tory is { Count: > 0 }) P.Children.Add(SmallBtn("Rozdziel na tory", () => Rozdziel(o.Id), "Osobny obiekt na każdym torze (ta sama nazwa)"));
            }
            if (inf.BezNmt) P.Children.Add(new TextBlock { Text = $"Profil: „Niweleta z terenu” pomija tu NMT (z zapasem {F(Obiekty.ZapasNmt)} m na końcach)", Foreground = Brushes.DimGray, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 4) });
        }
        Field(P, "Uwagi", o.Uwagi, n => Ch("Uwagi obiektu", x => x.Uwagi = n));
        Notes(P, Obiekty.Check(Doc.P, Topologia.Routes(Doc.P)).Where(x => x.Id == o.Id).Select(x => x.Text));
        P.Children.Add(SmallBtn("Usuń", () => DeleteObj(), "Usuń obiekt (Del)"));
        P.Children.Add(new Separator { Margin = new Thickness(0, 8, 0, 10) });
    }
}
