using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LcsWidzew.Model;

namespace LcsWidzew.UI;

/// <summary>
/// Warstwy planu schematycznego (Ig-10 § 4 ust. 4 – schemat opracowany z wykorzystaniem warstw).
/// Wspólne dla planu w edytorze i okna „Plan stacji”.
/// </summary>
public class PlanLayers
{
    public bool Signals = true, Numbers = true, Detection, Platforms = true, Crossings = true, Ends = true, Texts = true, Radius;
    public static readonly PlanLayers Current = new();
    public static readonly (string Name, Func<PlanLayers, bool> Get, Action<PlanLayers, bool> Set)[] All =
    {
        ("sygnalizatory", l => l.Signals, (l, v) => l.Signals = v),
        ("numery torów i zwrotnic", l => l.Numbers, (l, v) => l.Numbers = v),
        ("liczniki osi / złącza izolowane", l => l.Detection, (l, v) => l.Detection = v),
        ("perony", l => l.Platforms, (l, v) => l.Platforms = v),
        ("przejazdy", l => l.Crossings, (l, v) => l.Crossings = v),
        ("kozły, wykolejnice", l => l.Ends, (l, v) => l.Ends = v),
        ("napisy, szlaki, posterunki", l => l.Texts, (l, v) => l.Texts = v),
        ("łuki (R, h)", l => l.Radius, (l, v) => l.Radius = v),
    };
}

/// <summary>
/// Plan schematyczny w edytorze: podkład (plan Semaforka, mapa, skan) do odrysowania torów i warstwy Ig-10.
/// Podkład jest tylko tłem edytora – nie trafia na pulpit ani do „Plan stacji”. Zapis w station.json: „planBg”.
/// </summary>
public partial class EditorWindow
{
    int bgCal;                       // kalibracja: 1 – punkt na podkładzie, 2 – miejsce docelowe, 3 – drugi punkt, 4 – drugie miejsce
    Point bgP1, bgQ1, bgP2;
    /// <summary>Operacja podkładu dwoma kliknięciami: move – przesunięcie, level – wyrównanie do poziomu, along / across – skala wzdłuż / w poprzek.</summary>
    string bgOp;
    readonly List<Point> bgPts = new();
    static readonly Dictionary<string, BitmapSource> bgCache = new();

    JsonObject Bg => doc?.Root["planBg"] as JsonObject;
    string BgFile(JsonObject bg)
    {
        var f = StationDoc.Sv(bg?["file"]); if (string.IsNullOrEmpty(f)) return null;
        return Path.IsPathRooted(f) ? f : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path) ?? ".", f));
    }
    BitmapSource BgImage(string full)
    {
        if (full == null) return null;
        if (bgCache.TryGetValue(full, out var b)) return b;
        try
        {
            var bi = new BitmapImage(); bi.BeginInit(); bi.CacheOption = BitmapCacheOption.OnLoad; bi.UriSource = new Uri(full); bi.EndInit(); bi.Freeze();
            return bgCache[full] = bi;
        }
        catch { return bgCache[full] = null; }
    }

    /// <summary>
    /// Przekształcenie podkładu: świat = O + diag(1, Sy) · obrót(Rot) · S · piksel. S – skala obrazu (z szerokości „w”),
    /// Sy – dodatkowe rozciągnięcie pionowe planu (skala poprzeczna inna niż podłużna, np. mapa: ×4 przy 1:2000 / 1:500).
    /// </summary>
    record struct BgT(Point O, double Rot, double S, double Sy);
    static double Num(JsonNode n, double def) => n is JsonValue v && v.TryGetValue<double>(out var d) ? d : n is JsonValue s && s.TryGetValue<string>(out var t) && double.TryParse(t.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d2) ? d2 : def;
    BgT BgXf(JsonObject bg, BitmapSource img)
    {
        double w = Num(bg["w"], img.PixelWidth), sy = Num(bg["sy"], 1);
        return new BgT(new Point(Num(bg["x"], 0), Num(bg["y"], 0)), Num(bg["rot"], 0), w / Math.Max(1, img.PixelWidth), sy > 1e-4 ? sy : 1);
    }
    static Point BgToWorld(BgT t, Point px)
    {
        double r = t.Rot * Math.PI / 180, x = px.X * t.S, y = px.Y * t.S;
        return t.O + new Vector(x * Math.Cos(r) - y * Math.Sin(r), (x * Math.Sin(r) + y * Math.Cos(r)) * t.Sy);
    }
    static void BgStore(JsonObject bg, BitmapSource img, BgT t)
    {
        bg["x"] = Math.Round(t.O.X, 2); bg["y"] = Math.Round(t.O.Y, 2); bg["rot"] = Math.Round(t.Rot, 3);
        bg["w"] = Math.Round(img.PixelWidth * t.S, 2);
        if (Math.Abs(t.Sy - 1) < 1e-6) bg.Remove("sy"); else bg["sy"] = Math.Round(t.Sy, 5);
    }

    void DrawPlanBackground(DrawingContext dc, double scale)
    {
        var bg = Bg; if (bg == null || bg["hidden"] is JsonValue hv && hv.TryGetValue<bool>(out var h) && h) return;
        var img = BgImage(BgFile(bg)); if (img == null) return;
        var t = BgXf(bg, img);
        double op = bg["opacity"] != null ? Math.Clamp(Num(bg["opacity"], 0.45), 0.05, 1) : 0.45;
        dc.PushTransform(new TranslateTransform(t.O.X, t.O.Y)); dc.PushTransform(new ScaleTransform(1, t.Sy)); dc.PushTransform(new RotateTransform(t.Rot)); dc.PushOpacity(op);
        dc.DrawImage(img, new Rect(0, 0, img.PixelWidth * t.S, img.PixelHeight * t.S));
        dc.Pop(); dc.Pop(); dc.Pop(); dc.Pop();
        // znaczniki kalibracji
        var pen = new Pen(Brushes.OrangeRed, 2 / scale);
        void Mark(Point p) { dc.DrawLine(pen, p + new Vector(-8, 0) / scale, p + new Vector(8, 0) / scale); dc.DrawLine(pen, p + new Vector(0, -8) / scale, p + new Vector(0, 8) / scale); }
        if (bgCal >= 2) Mark(bgP1); if (bgCal >= 3) { Mark(bgQ1); dc.DrawLine(new Pen(Brushes.OrangeRed, 1 / scale) { DashStyle = DashStyles.Dash }, bgP1, bgQ1); }
        if (bgCal >= 4) Mark(bgP2);
        if (bgOp != null) foreach (var p in bgPts) { Mark(p); dc.DrawLine(new Pen(Brushes.OrangeRed, 1 / scale) { DashStyle = DashStyles.Dash }, p, mouse); }
    }

    /// <summary>Kalibracja dwoma punktami: punkt podkładu → miejsce na planie (węzeł albo kliknięte miejsce).</summary>
    void BgCalClick(Point w)
    {
        Point Target(Point p) { var n = PickNode(p); return n != null ? new Point(st.Nodes[n].X, st.Nodes[n].Y) : p; }
        switch (bgCal)
        {
            case 1: bgP1 = w; bgCal = 2; status.Text = "Podkład 2/4: kliknij, gdzie ten punkt ma leżeć na planie (węzeł – dokładnie w węźle)"; break;
            case 2: bgQ1 = Target(w); bgCal = 3; status.Text = "Podkład 3/4: kliknij drugi punkt na podkładzie (daleko od pierwszego)"; break;
            case 3: bgP2 = w; bgCal = 4; status.Text = "Podkład 4/4: kliknij, gdzie drugi punkt ma leżeć na planie"; break;
            case 4:
            {
                var q2 = Target(w); bgCal = 0;
                var dp = bgP2 - bgP1; var dq = q2 - bgQ1;
                if (dp.Length < 1e-6) { status.Text = "✗ Punkty podkładu w tym samym miejscu – kalibracja przerwana"; break; }
                var bg = Bg; var img = BgImage(BgFile(bg)); if (img == null) break;
                var t = BgXf(bg, img); string msg;
                if (Math.Abs(t.Sy - 1) < 1e-6)
                {
                    // ta sama skala w obu kierunkach: przekształcenie podobieństwa (przesunięcie, skala, obrót) – a = dq / dp
                    double k = dq.Length / dp.Length, ang = Math.Atan2(dq.Y, dq.X) - Math.Atan2(dp.Y, dp.X);
                    Vector Apply(Vector v) => new(k * (v.X * Math.Cos(ang) - v.Y * Math.Sin(ang)), k * (v.X * Math.Sin(ang) + v.Y * Math.Cos(ang)));
                    t = t with { O = bgQ1 + Apply(t.O - bgP1), Rot = t.Rot + ang * 180 / Math.PI, S = t.S * k };
                    msg = $"Podkład dopasowany: skala ×{k:0.###}, obrót {ang * 180 / Math.PI:0.##}°";
                }
                else
                {
                    // podkład rozciągnięty w pionie: osobno skala pozioma i pionowa, bez obrotu
                    double kx = Math.Abs(dp.X) > 2 ? dq.X / dp.X : dq.Length / dp.Length, ky = Math.Abs(dp.Y) > 2 && Math.Abs(dq.Y) > 2 ? dq.Y / dp.Y : kx;
                    if (kx <= 0 || ky <= 0) { status.Text = "✗ Dopasowanie odwróciłoby podkład – wybierz punkty w tej samej kolejności"; break; }
                    t = t with { O = bgQ1 + new Vector(kx * (t.O.X - bgP1.X), ky * (t.O.Y - bgP1.Y)), S = t.S * kx, Sy = t.Sy * ky / kx };
                    msg = $"Podkład dopasowany: skala pozioma ×{kx:0.###}, pionowa ×{ky:0.###}";
                }
                doc.Snapshot(); BgStore(bg, img, t); Changed(msg);
                break;
            }
        }
        panel.InvalidateVisual();
    }

    void StartBgOp(string op)
    {
        bgCal = 0; bgOp = op; bgPts.Clear();
        status.Text = "Podkład 1/2: " + op switch
        {
            "move" => "kliknij punkt podkładu, który chcesz przesunąć",
            "level" => "kliknij punkt na osi długiego prostego toru podkładu (lewy)",
            "along" => "kliknij pierwszy punkt o znanym położeniu wzdłuż toru (np. słup kilometrowy, semafor, początek rozjazdu)",
            _ => "kliknij oś pierwszego toru podkładu"
        } + "   ·  PPM / Esc – przerwij";
        panel.InvalidateVisual();
    }

    /// <summary>Drugi punkt operacji podkładu i jej wykonanie (jedno Ctrl+Z cofa).</summary>
    void BgOpClick(Point w)
    {
        bgPts.Add(w);
        if (bgPts.Count < 2)
        {
            status.Text = "Podkład 2/2: " + bgOp switch
            {
                "move" => "kliknij, gdzie ten punkt ma się znaleźć",
                "level" => "kliknij drugi punkt na osi tego samego toru (daleko od pierwszego)",
                "along" => "kliknij drugi punkt (daleko od pierwszego); potem podasz odległość w metrach",
                _ => "kliknij oś sąsiedniego toru; potem podasz rozstaw w metrach"
            };
            panel.InvalidateVisual(); return;
        }
        var op = bgOp; var p1 = bgPts[0]; var p2 = bgPts[1]; bgOp = null; bgPts.Clear(); panel.InvalidateVisual();
        var bg = Bg; var img = BgImage(BgFile(bg)); if (img == null) { status.Text = "✗ Brak obrazu podkładu"; return; }
        var t = BgXf(bg, img); string msg;
        double? AskM(string title, string label, double def)
        {
            var r = Ask(title, (label, def.ToString("0.##", CultureInfo.InvariantCulture)));
            return r != null && double.TryParse(r[0].Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var m) && m > 0.1 && m < 100000 ? m : null;
        }
        switch (op)
        {
            case "move":
                t = t with { O = t.O + (p2 - p1) }; msg = $"Podkład przesunięty o ({p2.X - p1.X:0}, {p2.Y - p1.Y:0})";
                break;
            case "level":
            {
                var u = new Vector(p2.X - p1.X, (p2.Y - p1.Y) / t.Sy);
                if (u.Length < 2) { status.Text = "✗ Punkty za blisko siebie"; return; }
                double ang = -Math.Atan2(u.Y, u.X); if (ang > Math.PI / 2) ang -= Math.PI; if (ang < -Math.PI / 2) ang += Math.PI;
                var v = p1 - t.O; v = new Vector(v.X, v.Y / t.Sy);
                var vr = new Vector(v.X * Math.Cos(ang) - v.Y * Math.Sin(ang), v.X * Math.Sin(ang) + v.Y * Math.Cos(ang));
                t = t with { O = p1 - new Vector(vr.X, vr.Y * t.Sy), Rot = t.Rot + ang * 180 / Math.PI };
                msg = $"Podkład obrócony o {ang * 180 / Math.PI:0.##}° – tor poziomo";
                break;
            }
            case "along":
            {
                double dx = Math.Abs(p2.X - p1.X);
                if (dx < 2) { status.Text = "✗ Punkty muszą być oddalone wzdłuż toru (w poziomie)"; return; }
                var m = AskM("Skala podkładu wzdłuż toru", $"Odległość między punktami wzdłuż toru [m] (np. różnica kilometrażu). Plan: 1:{doc.ScaleAlong:0}", dx / doc.Ux);
                if (m == null) { status.Text = "Przerwano"; return; }
                double k = m.Value * doc.Ux / dx;
                t = t with { O = new Point(p1.X + k * (t.O.X - p1.X), t.O.Y), S = t.S * k, Sy = t.Sy / k };
                msg = $"Podkład w skali wzdłuż: {m:0.#} m = {m * doc.Ux:0} j. planu (×{k:0.###} poziomo)";
                break;
            }
            default:
            {
                double dy = Math.Abs(p2.Y - p1.Y);
                if (dy < 1) { status.Text = "✗ Punkty muszą leżeć na różnych torach (w pionie)"; return; }
                var m = AskM("Skala podkładu w poprzek", $"Rozstaw torów (odległość osi) [m] – zwykle 4,75. Plan: 1:{doc.ScaleAcross:0}", 4.75);
                if (m == null) { status.Text = "Przerwano"; return; }
                double ky = m.Value * doc.Uy / dy;
                t = t with { O = new Point(t.O.X, p1.Y + ky * (t.O.Y - p1.Y)), Sy = t.Sy * ky };
                msg = $"Podkład w skali w poprzek: {m:0.##} m = {m * doc.Uy:0.#} j. planu (×{ky:0.###} pionowo)";
                break;
            }
        }
        doc.Snapshot(); BgStore(bg, img, t); Changed(msg);
    }

    /// <summary>Panel planu (nic nie zaznaczono): skala, podkład i warstwy.</summary>
    void PlanSideProps()
    {
        PlanScalePanel();
        props.Children.Add(T("Podkład do odrysowania", 13, Brushes.White, true));
        props.Children.Add(T("Ortofotomapa (zdjęcia lotnicze z Geoportalu) i mapa OSM trafiają na plan od razu w skali – wystarczy obrócić tor do poziomu. Plan z Semaforka, skan albo zdjęcie ustaw w skali planu (wyrównaj tor do poziomu, skala wzdłuż – dwa punkty i odległość w metrach, w poprzek – dwa tory i rozstaw), potem rysuj tory narzędziem Tor po podkładzie – długości wyjdą z rysunku. Podkład widać tylko w edytorze.", 10.5, Brushes.Gray));
        var bg = Bg;
        props.Children.Add(Btn(bg == null ? "Nowy podkład: zdjęcia lotnicze / mapa / Semaforek / plik…" : "Zmień podkład (zdjęcia / mapa / Semaforek / plik)…", (a, e) => BgNewDialog()));
        if (bg == null) { PlanLayersPanel(); return; }
        var full = BgFile(bg); var img = BgImage(full);
        props.Children.Add(T(img == null ? "✗ Nie można wczytać: " + full : $"{Path.GetFileName(full)} ({img.PixelWidth}×{img.PixelHeight})", 10.5, img == null ? Brushes.OrangeRed : Brushes.LightGray));
        if (StationDoc.Sv(bg["attribution"]) is { Length: > 0 } attr) props.Children.Add(T(attr, 10.5, Brushes.Gray));
        if (img != null)
        {
            var t = BgXf(bg, img);
            props.Children.Add(T($"1 piksel podkładu ≈ {t.S / doc.Ux:0.###} m wzdłuż, {t.S * t.Sy / doc.Uy:0.###} m w poprzek" + (Math.Abs(t.Rot) > 0.01 ? $" (obrót {t.Rot:0.##}°)" : ""), 10.5, Brushes.Gray));
        }
        props.Children.Add(Btn("1. Wyrównaj tor do poziomu (2 punkty)", (a, e) => StartBgOp("level")));
        props.Children.Add(Btn("2. Skala wzdłuż: 2 punkty + odległość [m]", (a, e) => StartBgOp("along")));
        props.Children.Add(Btn("3. Skala w poprzek: 2 tory + rozstaw [m]", (a, e) => StartBgOp("across")));
        props.Children.Add(Btn($"Rozciągnij pionowo ×{doc.Uy / doc.Ux:0.##} (zniekształca zdjęcie)", (a, e) => Safe(() =>
        {
            var b = Bg; var im = BgImage(BgFile(b)); if (im == null) return;
            var t = BgXf(b, im); doc.Snapshot(); BgStore(b, im, t with { Sy = doc.Uy / doc.Ux });
            Changed($"Podkład: rozciągnięcie pionowe ×{doc.Uy / doc.Ux:0.##} (skala poprzeczna 1:{doc.ScaleAcross:0} przy podłużnej 1:{doc.ScaleAlong:0})");
        })));
        props.Children.Add(Btn("Przesuń podkład (punkt → miejsce)", (a, e) => StartBgOp("move")));
        props.Children.Add(Btn("Dopasuj dwoma punktami (do węzłów)", (a, e) => { bgOp = null; bgCal = 1; status.Text = "Podkład 1/4: kliknij punkt na podkładzie (np. środek rozjazdu, który już jest na planie). PPM / Esc – przerwij"; }));
        var hidden = bg["hidden"] is JsonValue hv && hv.TryGetValue<bool>(out var hb) && hb;
        var chk = new CheckBox { Content = "pokaż podkład", IsChecked = !hidden, Foreground = Brushes.White, Margin = new Thickness(0, 4, 0, 2) };
        chk.Click += (a, e) => { if (chk.IsChecked == true) bg.Remove("hidden"); else bg["hidden"] = true; dirty = true; panel.InvalidateVisual(); };
        props.Children.Add(chk);
        props.Children.Add(T("Przezroczystość", 10.5, Brushes.Gray));
        var sl = new Slider { Minimum = 0.05, Maximum = 1, Value = bg["opacity"] != null ? Num(bg["opacity"], 0.45) : 0.45, Margin = new Thickness(0, 0, 0, 4) };
        sl.ValueChanged += (a, e) => { bg["opacity"] = Math.Round(sl.Value, 2); dirty = true; panel.InvalidateVisual(); };
        props.Children.Add(sl);
        foreach (var (k, lbl) in new[] { ("x", "Położenie X"), ("y", "Położenie Y"), ("w", "Szerokość na planie"), ("sy", "Rozciągnięcie pionowe (×, 1 – bez)"), ("rot", "Obrót [°]") }) Field(bg, k, lbl);
        props.Children.Add(Btn("Usuń podkład", (a, e) => Safe(() => { doc.Snapshot(); doc.Root.Remove("planBg"); Changed("Usunięto podkład"); })));
        PlanLayersPanel();
    }

    /// <summary>Skala planu (Ig-10 § 3) i długości torów z rysunku.</summary>
    void PlanScalePanel()
    {
        props.Children.Add(T("Skala planu (Ig-10 § 3)", 13, Brushes.White, true));
        props.Children.Add(T($"Wzdłuż 1:{doc.ScaleAlong:0} – 1 m = {doc.Ux:0.###} j. ({1 / doc.Ux:0.##} m na j.); w poprzek 1:{doc.ScaleAcross:0} – rozstaw 4,75 m = {4.75 * doc.Uy:0.##} j. Siatka „Ig-10” = 10 m × 4,75 m.", 10.5, Brushes.Gray));
        var items = StationDoc.PlanScales.Select(x => x.Name).ToList();
        int cur = Array.FindIndex(StationDoc.PlanScales, x => x.Along == doc.ScaleAlong && x.Across == doc.ScaleAcross);
        if (cur < 0) { items.Add($"1:{doc.ScaleAlong:0} / 1:{doc.ScaleAcross:0} (własna)"); cur = items.Count - 1; }
        var cb = new ComboBox { ItemsSource = items, SelectedIndex = cur, Margin = new Thickness(0, 2, 0, 4) };
        cb.SelectionChanged += (a, e) => Safe(() =>
        {
            int i = cb.SelectedIndex; if (i < 0 || i >= StationDoc.PlanScales.Length) return;
            var (al, ac, _) = StationDoc.PlanScales[i];
            if (al == doc.ScaleAlong && ac == doc.ScaleAcross) return;
            if (!NoPrompt && !FuzzMode && MessageBox.Show(this, $"Zmienić skalę planu na 1:{al} / 1:{ac}?{(char)10}{(char)10}Rysunek planu (tory, napisy, perony, przejazdy, podkład) zostanie przeskalowany, długości torów w metrach się nie zmienią. Ctrl+Z cofa.", "Skala planu", MessageBoxButton.OKCancel) != MessageBoxResult.OK) { ShowProps(); return; }
            doc.Snapshot(); doc.RescalePlan(al, ac);
            Changed($"Skala planu 1:{al} / 1:{ac} – rysunek przeskalowany, długości bez zmian");
            panel.FitAll();
        });
        props.Children.Add(cb);
        var auto = new CheckBox { Content = "Długości torów z rysunku (bez wpisywania)", IsChecked = doc.LenAuto, Foreground = Brushes.White, Margin = new Thickness(0, 4, 0, 2),
            ToolTip = "Długość toru = jego długość na planie w skali (poziomo wg skali podłużnej, skos także wg poprzecznej). Nie dotyczy szlaków (na planie w skróceniu), łączników obrazów i torów z długością wpisaną ręcznie." };
        auto.Click += (a, e) => Safe(() =>
        {
            bool on = auto.IsChecked == true;
            if (on && !NoPrompt && !FuzzMode && MessageBox.Show(this, $"Liczyć długości torów z rysunku planu?{(char)10}{(char)10}Długości wszystkich torów stacyjnych zostaną teraz przeliczone z rysunku w skali 1:{doc.ScaleAlong:0} (szlaki, łączniki obrazów i długości wpisane – bez zmian). Przy danych z pomiaru / OSM to je zastąpi. Ctrl+Z cofa.", "Długości z rysunku", MessageBoxButton.OKCancel) != MessageBoxResult.OK) { auto.IsChecked = false; return; }
            doc.Snapshot(); doc.SetPlanScale(doc.ScaleAlong, doc.ScaleAcross, on);
            Changed(on ? "Długości z rysunku: " + doc.RecalcLens() : "Długości torów wpisane (rysunek ich nie zmienia)");
        });
        props.Children.Add(auto);
        if (doc.LenAuto)
            props.Children.Add(Btn("Przelicz także długości wpisane ręcznie", (a, e) => Safe(() => { doc.Snapshot(); Changed("Długości z rysunku (wszystkie tory stacyjne): " + doc.RecalcLens(true)); })));
        else
            props.Children.Add(T("Długości są danymi (wpisane / z pomiaru) – przesuwanie węzłów ich nie zmienia. Nowe tory dostają długość z rysunku.", 10.5, Brushes.Gray));
    }

    /// <summary>Panel toru (plan): długość na rysunku i przełącznik „długość z rysunku / wpisana”.</summary>
    void EdgeScaleInfo(JsonObject e)
    {
        if (dispMode || !st.Edges.TryGetValue(StationDoc.Sv(e["id"]) ?? "", out var se)) return;
        double geo = doc.PlanLen(se.Pts);
        if (!doc.LenAuto || StationDoc.SzlakEdge(e) || StationDoc.IsLink(e))
        {
            props.Children.Add(T($"Na rysunku (1:{doc.ScaleAlong:0}): {geo:0} m; długość toru: {StationDoc.D(e["len"]):0} m" + (doc.LenAuto ? " (szlak / łącznik obrazów – wpisana)" : ""), 10.5, Brushes.Gray));
            return;
        }
        var cb = new CheckBox { Content = $"Długość z rysunku ({geo:0} m)", IsChecked = !StationDoc.LenFixed(e), Foreground = Brushes.White, Margin = new Thickness(0, 2, 0, 4),
            ToolTip = "Odznacz, gdy tor jest narysowany w skróceniu – wtedy obowiązuje długość wpisana w polu odcinka." };
        cb.Click += (a, b) => Safe(() => { doc.Snapshot(); doc.SetLenFix(e, cb.IsChecked != true); Changed(cb.IsChecked == true ? $"Tor {StationDoc.Sv(e["id"])}: długość z rysunku" : $"Tor {StationDoc.Sv(e["id"])}: długość wpisana"); });
        props.Children.Add(cb);
    }

    /// <summary>Ustawienie pliku podkładu: ścieżka względna, gdy plik leży obok station.json; podkład wstępnie na szerokość planu.</summary>
    public void SetBackground(string file)
    {
        var dir = Path.GetFullPath(Path.GetDirectoryName(path) ?? ".");
        var full = Path.GetFullPath(file);
        var rel = full.StartsWith(dir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? Path.GetRelativePath(dir, full) : full;
        var img = BgImage(full); if (img == null) { status.Text = "✗ Nie można wczytać obrazu"; return; }
        double x0 = st.Nodes.Count > 0 ? st.Nodes.Values.Min(n => n.X) : 0, x1 = st.Nodes.Count > 0 ? st.Nodes.Values.Max(n => n.X) : 2000;
        double y0 = st.Nodes.Count > 0 ? st.Nodes.Values.Min(n => n.Y) : 0;
        doc.Snapshot();
        var bg = Bg ?? new JsonObject();
        bg["file"] = rel;
        if (Bg == null) { bg["x"] = Math.Round(x0, 1); bg["y"] = Math.Round(y0, 1); bg["w"] = Math.Round(Math.Max(500, x1 - x0), 1); bg["rot"] = 0; bg["opacity"] = 0.45; doc.Root["planBg"] = bg; }
        Changed("Wczytano podkład " + Path.GetFileName(full) + " – ustaw go w skali (panel z prawej) albo dopasuj dwoma punktami");
    }

    void PlanLayersPanel()
    {
        props.Children.Add(T("Warstwy planu (Ig-10 § 4 ust. 4)", 13, Brushes.White, true));
        var L = PlanLayers.Current;
        foreach (var (name, get, set) in PlanLayers.All)
        {
            var cb = new CheckBox { Content = name, IsChecked = get(L), Foreground = Brushes.White, Margin = new Thickness(0, 1, 0, 1) };
            cb.Click += (a, e) => { set(L, cb.IsChecked == true); panel.InvalidateVisual(); };
            props.Children.Add(cb);
        }
    }

    /// <summary>Autoukład planu w skali (propozycja): najpierw zobrazowanie (żeby pulpit się nie zmienił), jedno Ctrl+Z cofa.</summary>
    string AutoLayout()
    {
        doc.Snapshot(); doc.EnsureDisplay();
        var rep = doc.AutoLayoutPlan();
        Changed("Autoukład planu (propozycja – popraw ręcznie, Ctrl+Z cofa): " + rep);
        if (panel.PlanView) panel.FitAll();
        return rep;
    }

    /// <summary>Test (--editshot … autolayout out.json): autoukład – topologia, przebiegi i pulpit bez zmian, cofnięcie.</summary>
    public string AutoLayoutForTest(string outJson)
    {
        var log = new System.Text.StringBuilder(); FuzzMode = true;
        PlanForTest(); doc.EnsureDisplay();
        string Topo() { var r = (JsonObject)doc.Root.DeepClone(); foreach (var e in ((JsonArray)r["edges"]).OfType<JsonObject>()) e.Remove("pts"); foreach (var (_, n) in (JsonObject)r["nodes"]) { ((JsonObject)n).Remove("x"); ((JsonObject)n).Remove("y"); } foreach (var k in new[] { "labels", "platforms", "crossings", "dashed", "planBg" }) r.Remove(k); return r.ToJsonString(); }
        int Routes() => new LcsWidzew.Sim.Simulator(doc.Build()).RouteTable.Values.Sum(l => l.Count);
        string before = doc.Root.ToJsonString(), topo0 = Topo(); int r0 = Routes();
        var rep = AutoLayout();
        log.AppendLine("raport: " + rep);
        log.AppendLine((Topo() == topo0 ? "OK  " : "BŁĄD") + " topologia, długości, odcinki, sygnalizatory i zobrazowanie bez zmian");
        int r1 = Routes(); log.AppendLine((r1 == r0 ? "OK  " : "BŁĄD") + $" przebiegi {r0} -> {r1}");
        var b = doc.Build();
        int vert = b.Edges.Values.Count(e => !e.Link && Math.Abs(e.Pts[0].X - e.Pts[^1].X) < 0.5 && Math.Abs(e.Pts[0].Y - e.Pts[^1].Y) > 0.5);
        log.AppendLine((vert == 0 ? "OK  " : "UWAGA") + $" torów pionowych na planie: {vert}");
        doc.Save(outJson);
        DoUndo();
        log.AppendLine((doc.Root.ToJsonString() == before ? "OK  " : "BŁĄD") + " Ctrl+Z przywraca plan sprzed autoukładu");
        DoRedo();
        log.AppendLine($"błędów edytora: {Errors.Count}");
        return log.ToString();
    }

    /// <summary>Test (--editshot … bg plik.png): wczytanie podkładu, kalibracja dwoma punktami do węzłów, zapis.</summary>
    public string BgForTest(string img, string outJson)
    {
        var log = new System.Text.StringBuilder(); FuzzMode = true;
        PlanForTest();
        SetBackground(img);
        var bg = Bg; var bi = BgImage(BgFile(bg));
        log.AppendLine($"podkład: {StationDoc.Sv(bg?["file"])}, obraz {bi?.PixelWidth}×{bi?.PixelHeight}");
        // dwa węzły daleko od siebie; punkty podkładu – tam, gdzie leżą po przesunięciu i obrocie (symulacja)
        var ns = st.Nodes.Values.Where(n => n.Edges.Count > 1).OrderBy(n => n.X).ToList();
        var a = ns.First(); var b = ns.Last();
        var t = BgXf(bg, bi);
        var pa = BgToWorld(t, new Point(bi.PixelWidth * 0.2, bi.PixelHeight * 0.3)); var pb = BgToWorld(t, new Point(bi.PixelWidth * 0.8, bi.PixelHeight * 0.6));
        bgCal = 1; BgCalClick(pa); BgCalClick(new Point(a.X, a.Y)); BgCalClick(pb); BgCalClick(new Point(b.X, b.Y));
        var t2 = BgXf(Bg, bi);
        var ra = BgToWorld(t2, new Point(bi.PixelWidth * 0.2, bi.PixelHeight * 0.3)); var rb = BgToWorld(t2, new Point(bi.PixelWidth * 0.8, bi.PixelHeight * 0.6));
        log.AppendLine($"po kalibracji: punkt 1 → ({ra.X:0.0}, {ra.Y:0.0}) cel ({a.X:0.0}, {a.Y:0.0}); punkt 2 → ({rb.X:0.0}, {rb.Y:0.0}) cel ({b.X:0.0}, {b.Y:0.0}); obrót {t2.Rot:0.###}°");
        bool ok = (ra - new Point(a.X, a.Y)).Length < 1 && (rb - new Point(b.X, b.Y)).Length < 1;
        log.AppendLine(ok ? "OK  kalibracja trafia w węzły" : "BŁĄD kalibracja nie trafia w węzły");
        doc.Save(outJson);
        var re = Station.Load(outJson);
        log.AppendLine($"OK  zapis i wczytanie (węzły {re.Nodes.Count}); błędów edytora: {Errors.Count}");
        panel.InvalidateVisual();
        return log.ToString();
    }

    /// <summary>
    /// Test (--editshot … skala out.json [podkład.png]): skala Ig-10 odczytana z pliku, długości z rysunku (przesuwanie węzła,
    /// szlak, długość wpisana, cofanie), zmiana skali bez zmiany długości, nowy tor w skali, podkład w skali (poziom, wzdłuż, w poprzek, mapa).
    /// </summary>
    public string ScaleForTest(string outJson, string img)
    {
        var log = new System.Text.StringBuilder(); FuzzMode = true; int bad = 0;
        void Ok(bool c, string m) { log.AppendLine((c ? "OK  " : "BŁĄD") + " " + m); if (!c) bad++; }
        PlanForTest();
        log.AppendLine($"skala odczytana z pliku: 1:{doc.ScaleAlong:0} / 1:{doc.ScaleAcross:0}, długości z rysunku: {(doc.LenAuto ? "tak" : "nie")}");
        // tor stacyjny (nie szlak, nie łącznik) z węzłem zwykłym na końcu
        JsonObject Pick() => doc.EdgeObjs.First(e => !StationDoc.SzlakEdge(e) && !StationDoc.IsLink(e) && doc.IsPlainNode(StationDoc.Sv(e["b"])) && doc.EdgesAt(StationDoc.Sv(e["b"])).All(x => !StationDoc.SzlakEdge(x) && !StationDoc.IsLink(x)));
        var e0 = Pick(); string eid = StationDoc.Sv(e0["id"]), nid = StationDoc.Sv(e0["b"]);
        double len0 = StationDoc.D(e0["len"]); var p0 = doc.Pos(nid);
        doc.Snapshot(); doc.MoveNode(nid, p0.X + 40, p0.Y); Changed();
        Ok(Math.Abs(StationDoc.D(doc.Edge(eid)["len"]) - len0) < 0.01, $"bez „długości z rysunku” przesunięcie węzła nie zmienia długości ({len0:0.#} m)");
        DoUndo();
        // włączenie: przeliczenie wszystkich torów
        string before = doc.Root.ToJsonString();
        doc.Snapshot(); doc.SetPlanScale(doc.ScaleAlong, doc.ScaleAcross, true); var rep = doc.RecalcLens(); Changed();
        log.AppendLine("przeliczenie: " + rep);
        int wrong = doc.EdgeObjs.Count(e => doc.LenFromPlan(e) && Math.Abs(StationDoc.D(e["len"]) - Math.Round(Math.Max(1, doc.PlanLen(st.Edges[StationDoc.Sv(e["id"])].Pts)), 1)) > 0.06);
        Ok(wrong == 0, $"po przeliczeniu każdy tor stacyjny ma długość z rysunku (niezgodnych: {wrong})");
        var szl = doc.EdgeObjs.FirstOrDefault(StationDoc.SzlakEdge);
        if (szl != null) Ok(Math.Abs(StationDoc.D(szl["len"]) - StationDoc.D(((JsonObject)JsonNode.Parse(before))["edges"]!.AsArray().OfType<JsonObject>().First(x => StationDoc.Sv(x["id"]) == StationDoc.Sv(szl["id"]))["len"])) < 0.01, "szlak zachowuje wpisaną długość");
        // przesunięcie węzła o 10 m wzdłuż (w skali podłużnej)
        e0 = doc.Edge(eid); p0 = doc.Pos(nid); double l1 = StationDoc.D(e0["len"]);
        doc.Snapshot(); doc.MoveNode(nid, p0.X + 10 * doc.Ux * Math.Sign(p0.X - doc.Pos(StationDoc.Sv(e0["a"])).X + 0.001), p0.Y); Changed();
        double l2 = StationDoc.D(doc.Edge(eid)["len"]);
        Ok(Math.Abs(l2 - doc.PlanLen(st.Edges[eid].Pts)) < 0.06 && Math.Abs(l2 - l1) > 5, $"przesunięcie węzła o 10 m: tor {eid} {l1:0.#} → {l2:0.#} m (z rysunku)");
        // długość wpisana – tor przestaje brać długość z rysunku
        var sec = StationDoc.Sv(doc.Edge(eid)["sec"]);
        doc.Snapshot(); doc.SetSecLen(sec, 777); Changed();
        Ok(StationDoc.LenFixed(doc.Edge(eid)), "wpisanie długości odcinka oznacza tory „lenFix”");
        double l3 = StationDoc.D(doc.Edge(eid)["len"]); p0 = doc.Pos(nid);
        doc.Snapshot(); doc.MoveNode(nid, p0.X + 3 * doc.Ux, p0.Y); Changed();
        Ok(Math.Abs(StationDoc.D(doc.Edge(eid)["len"]) - l3) < 0.01, "tor z długością wpisaną nie zmienia jej przy przesuwaniu");
        doc.Snapshot(); doc.SetLenFix(doc.Edge(eid), false); Changed();
        Ok(Math.Abs(StationDoc.D(doc.Edge(eid)["len"]) - doc.PlanLen(st.Edges[eid].Pts)) < 0.06, "powrót do długości z rysunku");
        // zmiana skali: rysunek ×2 wzdłuż, długości bez zmian
        string lens() => string.Join(",", doc.EdgeObjs.Select(e => StationDoc.D(e["len"]).ToString("0.#", CultureInfo.InvariantCulture)));
        var L0 = lens(); var ax = doc.Pos(nid).X; double al = doc.ScaleAlong;
        var target = StationDoc.PlanScales.First(s => s.Along != al);
        doc.Snapshot(); doc.RescalePlan(target.Along, target.Across); Changed();
        Ok(lens() == L0, $"zmiana skali 1:{al:0} → 1:{target.Along}: długości torów bez zmian");
        Ok(Math.Abs(doc.Pos(nid).X - ax * al / target.Along) < 0.2, $"rysunek przeskalowany (x {ax:0.#} → {doc.Pos(nid).X:0.#})");
        int wrong2 = doc.EdgeObjs.Count(e => doc.LenFromPlan(e) && Math.Abs(StationDoc.D(e["len"]) - doc.PlanLen(st.Edges[StationDoc.Sv(e["id"])].Pts)) > 0.5);
        Ok(wrong2 == 0, $"po zmianie skali długości nadal zgodne z rysunkiem (niezgodnych: {wrong2})");
        // nowy tor: 100 m w skali
        doc.Snapshot();
        var na = doc.AddNode(Post, 0, -500); var nb = doc.AddNode(Post, 100 * doc.Ux, -500); var ne = doc.AddEdge(na, nb, null, Post);
        var nc = doc.AddNode(Post, 100 * doc.Ux + 30 * doc.Ux, -500 + 4.75 * doc.Uy * 4); var ne2 = doc.AddEdge(nb, nc, null, Post);
        Changed();
        Ok(Math.Abs(StationDoc.D(doc.Edge(ne)["len"]) - 100) < 0.06, $"nowy tor poziomy 100 m → {StationDoc.D(doc.Edge(ne)["len"]):0.#} m");
        double skos = Math.Sqrt(30 * 30 + 19 * 19);
        Ok(Math.Abs(StationDoc.D(doc.Edge(ne2)["len"]) - skos) < 0.1, $"skos 30 m × 19 m → {StationDoc.D(doc.Edge(ne2)["len"]):0.#} m (oczekiwane {skos:0.#})");
        // cofanie całości
        while (doc.Root.ToJsonString() != before && doc.Undo()) { }
        Rebuild();
        Ok(doc.Root.ToJsonString() == before, "Ctrl+Z przywraca stan sprzed włączenia długości z rysunku");
        // podkład w skali
        if (img != null)
        {
            SetBackground(img); var bi = BgImage(BgFile(Bg));
            // obrót podkładu o 3°, potem wyrównanie: dwa piksele jednej linii poziomej obrazu mają wyjść na tej samej wysokości
            doc.Snapshot(); Bg["rot"] = 3; Changed();
            Point Wp(double fx, double fy) => BgToWorld(BgXf(Bg, bi), new Point(bi.PixelWidth * fx, bi.PixelHeight * fy));
            StartBgOp("level"); BgOpClick(Wp(0.1, 0.5)); BgOpClick(Wp(0.9, 0.5));
            Ok(Math.Abs(Wp(0.1, 0.5).Y - Wp(0.9, 0.5).Y) < 0.05, $"wyrównanie do poziomu (obrót {BgXf(Bg, bi).Rot:0.###}°)");
            var keep = Wp(0.1, 0.5);
            StartBgOp("along"); AutoAnswers.Enqueue(new[] { "250" }); BgOpClick(Wp(0.1, 0.5)); BgOpClick(Wp(0.6, 0.5));
            Ok(Math.Abs(Wp(0.6, 0.5).X - Wp(0.1, 0.5).X - 250 * doc.Ux) < 0.05 && (Wp(0.1, 0.5) - keep).Length < 0.05, $"skala wzdłuż: 250 m = {Wp(0.6, 0.5).X - Wp(0.1, 0.5).X:0.##} j. (oczekiwane {250 * doc.Ux:0.##}), pierwszy punkt w miejscu");
            double y5 = Wp(0.1, 0.5).Y;
            StartBgOp("across"); AutoAnswers.Enqueue(new[] { "4,75" }); BgOpClick(Wp(0.3, 0.5)); BgOpClick(Wp(0.3, 0.52));
            Ok(Math.Abs(Wp(0.3, 0.52).Y - Wp(0.3, 0.5).Y - 4.75 * doc.Uy) < 0.05 && Math.Abs(Wp(0.6, 0.5).X - Wp(0.1, 0.5).X - 250 * doc.Ux) < 0.05, $"skala w poprzek: 4,75 m = {Wp(0.3, 0.52).Y - Wp(0.3, 0.5).Y:0.##} j., skala wzdłuż bez zmian");
            var m0 = Wp(0.5, 0.5);
            StartBgOp("move"); BgOpClick(m0); BgOpClick(m0 + new Vector(37, -12));
            Ok((Wp(0.5, 0.5) - (m0 + new Vector(37, -12))).Length < 0.05, "przesunięcie podkładu");
            // dopasowanie dwoma punktami przy rozciągniętym podkładzie (osobno poziomo i pionowo)
            var q1 = Wp(0.2, 0.3) + new Vector(15, 8); var q2 = Wp(0.8, 0.7) + new Vector(40, 30);
            var s1 = Wp(0.2, 0.3); var s2 = Wp(0.8, 0.7);
            bgCal = 1; BgCalClick(s1); BgCalClick(q1); BgCalClick(s2); BgCalClick(q2);
            Ok((Wp(0.2, 0.3) - q1).Length < 0.1 && (Wp(0.8, 0.7) - q2).Length < 0.1, "dopasowanie dwoma punktami podkładu rozciągniętego pionowo");
            doc.Save(outJson);
            var re = StationDoc.Load(outJson);
            Ok(re.Root["planBg"]?["sy"] != null && re.ScaleAlong == doc.ScaleAlong, "zapis podkładu (rozciągnięcie) i skali planu");
            panel.FitAll();
        }
        else doc.Save(outJson);
        log.AppendLine($"błędów testu: {bad}, błędów edytora: {Errors.Count}");
        return log.ToString();
    }
}
