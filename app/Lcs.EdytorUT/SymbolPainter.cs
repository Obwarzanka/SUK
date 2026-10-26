using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Rysowanie znaku z bazy symboli Ig-10 na ekranie: punkt zaczepienia, kierunek osi x (wektor jednostkowy na ekranie),
/// strona (+1 – y w prawo od kierunku, −1 – w lewo), piksele na mm arkusza. Napisy zawsze poziomo.
/// </summary>
public static class SymbolPainter
{
    static readonly Brush Ink = Frozen(Color.FromRgb(0x10, 0x10, 0x10)), Paper = Frozen(Colors.White), Halo = Frozen(Color.FromArgb(0xd8, 0xff, 0xff, 0xff));
    static readonly Typeface Font = new("Arial");

    public static void Draw(DrawingContext dc, SymbolDef s, Point anchor, Vector dir, int side, double pxPerMm, string nr, double dpi, Brush ink = null)
    {
        if (s == null) return;
        ink ??= Ink;
        dir.Normalize();
        var nrm = new Vector(-dir.Y, dir.X) * side;           // na ekranie (y w dół): obrót o 90° zgodnie z ruchem wskazówek – prawa strona
        Point P(double[] q) => anchor + dir * (q[0] * pxPerMm) + nrm * (q[1] * pxPerMm);
        foreach (var p in s.Prims)
        {
            double w = Math.Max(1, p.W * pxPerMm);
            var pen = new Pen(ink, w) { LineJoin = PenLineJoin.Miter, StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
            switch (p.T)
            {
                case "line" when p.Pts?.Count >= 2:
                    for (int i = 1; i < p.Pts.Count; i++) dc.DrawLine(pen, P(p.Pts[i - 1]), P(p.Pts[i]));
                    break;
                case "poly" when p.Pts?.Count >= 3:
                {
                    var g = new StreamGeometry();
                    using (var c = g.Open()) { c.BeginFigure(P(p.Pts[0]), true, true); foreach (var q in p.Pts.Skip(1)) c.LineTo(P(q), true, false); }
                    g.Freeze();
                    dc.DrawGeometry(p.Fill == "ink" ? ink : p.Fill == "paper" ? Paper : null, p.W > 0 ? pen : null, g);
                    break;
                }
                case "circle" when p.At != null:
                    dc.DrawEllipse(p.Fill == "ink" ? ink : p.Fill == "paper" ? Paper : null, p.W > 0 ? pen : null, P(p.At), p.R * pxPerMm, p.R * pxPerMm);
                    break;
                case "text" when p.At != null:
                {
                    var txt = (p.Text ?? "").Replace("{nr}", nr ?? "");
                    if (txt.Length == 0) break;
                    // wysokość wersalików H mm → rozmiar czcionki ok. 1,4·H; nie mniej niż 10 px
                    var ft = new FormattedText(txt, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Font, Math.Clamp(p.H * 1.4 * pxPerMm, 10, 15), ink, dpi);
                    var c = P(p.At);
                    dc.DrawRoundedRectangle(Halo, null, new Rect(c.X - ft.Width / 2 - 2, c.Y - ft.Height / 2, ft.Width + 4, ft.Height), 2, 2);
                    dc.DrawText(ft, new Point(c.X - ft.Width / 2, c.Y - ft.Height / 2));
                    break;
                }
            }
        }
    }

    static Brush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
}

/// <summary>Okno katalogu symboli Ig-10: wszystkie znaki z pliku, opis, miejsce w Ig-10; „Wczytaj ponownie” po zmianie pliku.</summary>
public class SymbolsWindow : Window
{
    readonly System.Windows.Controls.WrapPanel cards = new() { Margin = new Thickness(8) };
    readonly System.Windows.Controls.TextBlock src = new() { Margin = new Thickness(8, 6, 8, 0), Foreground = Brushes.DimGray, TextWrapping = TextWrapping.Wrap };
    public int CardCount => cards.Children.Count;
    readonly Action reloaded;

    public SymbolsWindow(Window owner, Action onReload)
    {
        Owner = owner; reloaded = onReload;
        Title = "Symbole Ig-10 (zał. 8)"; Width = 1100; Height = 760; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        FontFamily = new FontFamily("Segoe UI"); FontSize = 13; Background = Brushes.White;
        var reload = new System.Windows.Controls.Button { Content = "Wczytaj ponownie z pliku", Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(8, 6, 6, 0) };
        reload.Click += (s, e) => Reload();
        var openF = new System.Windows.Controls.Button { Content = "Otwórz plik", Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(0, 6, 6, 0) };
        openF.Click += (s, e) => { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Symbole.Source) { UseShellExecute = true }); } catch (Exception ex) { src.Text = "Nie można otworzyć: " + ex.Message; } };
        var bar = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        bar.Children.Add(reload); bar.Children.Add(openF);
        var root = new System.Windows.Controls.DockPanel();
        System.Windows.Controls.DockPanel.SetDock(bar, System.Windows.Controls.Dock.Top); root.Children.Add(bar);
        System.Windows.Controls.DockPanel.SetDock(src, System.Windows.Controls.Dock.Top); root.Children.Add(src);
        root.Children.Add(new System.Windows.Controls.ScrollViewer { Content = cards, VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto });
        Content = root;
        Build();
    }

    public void Reload()
    {
        try { Symbole.Load(Symbole.Source.StartsWith("brak") ? System.IO.Path.Combine(AppContext.BaseDirectory, "Dane", "symbole_ig10.json") : Symbole.Source); }
        catch (Exception ex) { src.Text = "Błąd w pliku symboli: " + ex.Message; return; }
        Build(); reloaded?.Invoke();
    }

    void Build()
    {
        cards.Children.Clear();
        src.Text = $"{Symbole.All.Count} znaków · plik: {Symbole.Source} · wymiary w mm arkusza (x – wzdłuż toru, y – w stronę odgałęzienia); podgląd 12 px/mm (obiekty inżynieryjne – w skali dopasowanej do karty)";
        foreach (var s in Symbole.All)
        {
            var sp = new System.Windows.Controls.StackPanel { Width = 250, Margin = new Thickness(6) };
            sp.Children.Add(new SymbolPreview(s) { Height = 120 });
            sp.Children.Add(new System.Windows.Controls.TextBlock { Text = s.Name, TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold });
            sp.Children.Add(new System.Windows.Controls.TextBlock { Text = $"{s.Ig10} · {s.Id}", Foreground = Brushes.DimGray, FontSize = 12 });
            cards.Children.Add(new System.Windows.Controls.Border { Child = sp, BorderBrush = Brushes.Gainsboro, BorderThickness = new Thickness(1), Margin = new Thickness(4) });
        }
    }

    /// <summary>Podgląd znaku na przykładowym torze (rozjazd – tor na wprost i odgałęzienie; krzyżowy – dwa tory pod kątem).</summary>
    class SymbolPreview : FrameworkElement
    {
        readonly SymbolDef s;
        public SymbolPreview(SymbolDef s) { this.s = s; }
        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight, k = 12, dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, w, h));
            var track = new Pen(Brushes.Black, 0.35 * k / 2); var thin = new Pen(Brushes.Black, 1);
            if (s.Id.StartsWith("ob_"))
            {
                // obiekt inżynieryjny: tor i przeszkoda są w samym znaku – podgląd w skali dopasowanej do karty (najwyżej 12 px/mm)
                var pts = s.Prims.SelectMany(p => p.Pts ?? new()).Concat(s.Prims.Where(p => p.At != null).Select(p => p.At)).ToList();
                if (pts.Count == 0) return;
                double x0 = pts.Min(p => p[0]), x1 = pts.Max(p => p[0]), y0 = pts.Min(p => p[1]), y1 = pts.Max(p => p[1]);
                double kk = Math.Min(k, Math.Min((w - 16) / Math.Max(1, x1 - x0), (h - 16) / Math.Max(1, y1 - y0)));
                var c = new Point(w / 2 - (x0 + x1) / 2 * kk, h / 2 + (y0 + y1) / 2 * kk);
                SymbolPainter.Draw(dc, s, c, new Vector(1, 0), -1, kk, "", dpi);
                var ft = new FormattedText($"{kk:0.#} px/mm", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 10, Brushes.Gray, dpi);
                dc.DrawText(ft, new Point(w - ft.Width - 4, h - ft.Height - 2));
                return;
            }
            bool cross = s.Id.StartsWith("rk") || s.Id == "st";
            var a = cross ? new Point(w / 2, h / 2 + 8) : new Point(w * 0.35, h * 0.62);
            dc.DrawLine(track, new Point(6, a.Y), new Point(w - 6, a.Y));
            // tor odgałęźny / przecinający w kierunku znaku (y w górę na podglądzie – strona −1)
            double slope = 2.0 / 8;
            if (cross) dc.DrawLine(thin, a + new Vector(-(w / 2 - 6), (w / 2 - 6) * slope), a + new Vector(w / 2 - 6, -(w / 2 - 6) * slope));
            else dc.DrawLine(thin, a, a + new Vector(w * 0.6, -w * 0.6 * slope));
            SymbolPainter.Draw(dc, s, a, new Vector(1, 0), -1, k, "12", dpi);
        }
    }
}
