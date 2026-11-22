using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Lcs.Powtarzacz;

/// <summary>
/// Jedno pole powtarzacza: symbol przejazdu wg Ie-104 § 13 (barwy jak pulpit gry, P0 2026-11-21) i części SSP wg § 14:
/// „3” – gotowość SSP (szara; awaria – czerwona na przemian z białą), „4a / 4b” – tory (szare; wyłączone czujniki – białe;
/// manualna aktywacja – czerwona, gra jej nie ma), „5” – awaryjne załączenie ostrzegania (szara; gra go nie ma). Brak danych – białe.
/// </summary>
public sealed class PrzejazdView : FrameworkElement
{
    static Brush B(byte r, byte g, byte b) { var x = new SolidColorBrush(Color.FromRgb(r, g, b)); x.Freeze(); return x; }
    // barwy pulpitu LCS (LcsWidzew/UI/PulpitSymbole.cs, PanelView.cs)
    public static readonly Brush Tlo = B(0, 0, 0), Szary = B(0x9a, 0x9a, 0x9a), Rozowy = B(0xf0, 0x80, 0xf0), Zolty = B(0xff, 0xf0, 0x00),
        Czerwony = B(0xff, 0x10, 0x10), Bialy = B(0xff, 0xff, 0xff), Ciemny = B(0x30, 0x30, 0x30), Napis = B(0xd0, 0xd0, 0xd0), Ramka = B(0x40, 0x40, 0x40);
    static readonly Typeface Mono = new(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    static readonly Typeface MonoB = new(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    public const double Szer = 250, Wys = 150;
    public Przejazd P;
    public bool Miga, BrakDanych, AlarmNiekwitowany, Wybrany;

    public PrzejazdView() { Width = Szer; Height = Wys; Margin = new Thickness(4); }

    protected override void OnRender(DrawingContext dc)
    {
        var p = P; if (p == null) return;
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        dc.DrawRectangle(Tlo, new Pen(AlarmNiekwitowany ? (Miga ? Czerwony : Ramka) : p.Awaria && !BrakDanych ? Czerwony : Wybrany ? Zolty : Ramka, AlarmNiekwitowany || Wybrany ? 2 : 1),
                         new Rect(0.5, 0.5, Szer - 1, Wys - 1));

        void T(string s, double x, double y, double size, Brush b, bool bold = false)
            => dc.DrawText(new FormattedText(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, bold ? MonoB : Mono, size, b, dpi), new Point(x, y));

        // nagłówek: km (szary, Ie-104 § 13), kategoria, rodzaj
        T(p.KmTekst, 8, 5, 13, Szary, true);
        T($"LK{p.Linia}" + (p.Kat.Length > 0 ? $"  kat. {p.Kat}" : "") + (p.Ssp ? "  SSP" : "  obsługiwany"), 88, 7, 10, Napis);

        // symbol przejazdu (§ 13)
        var stan = BrakDanych ? null : p.Stan;
        Brush linia = stan switch
        {
            null => Bialy,
            "otwarty" => Rozowy,
            "zamykanie" or "otwieranie" => Miga ? Rozowy : Tlo,
            "zamkniety" => Szary,
            "uszkodzony" => Miga ? Czerwony : Bialy,
            _ => Rozowy,
        };
        Brush konce = stan == "zamkniety" && p.Potwierdzony ? Zolty : linia;
        Symbol(dc, 40, 48, 112, linia, konce);

        // części SSP (§ 14)
        Brush g3 = BrakDanych || p.O <= 0 ? Bialy : p.Awaria ? (Miga ? Czerwony : Bialy) : Szary;
        Brush g4a = BrakDanych || p.O <= 0 ? Bialy : p.ToryWyl.Count >= 1 ? Bialy : Szary;
        Brush g4b = BrakDanych || p.O <= 0 ? Bialy : p.ToryWyl.Count >= 2 ? Bialy : Szary;
        Brush g5 = BrakDanych || p.O <= 0 ? Bialy : Szary;
        Pole(dc, "3", 84, 32, g3, dpi); Pole(dc, "4a", 120, 32, g4a, dpi); Pole(dc, "4b", 156, 32, g4b, dpi); Pole(dc, "5", 192, 32, g5, dpi);

        // opis słowny
        double y = 66;
        T(BrakDanych ? "brak danych z gry" : Przejazd.Slownie(stan) + (stan == "zamkniety" && p.Potwierdzony ? ", utwierdzony" : ""), 84, y, 11,
          stan == "uszkodzony" ? Czerwony : Napis, true);
        y += 16;
        if (!BrakDanych && p.O > 0)
        {
            T($"sygnalizacja: {(p.Ostrzeganie ? "OSTRZEGA" : "wył.")}", 84, y, 10, p.Ostrzeganie ? Czerwony : Napis); y += 14;
            T($"rogatki wj: {Przejazd.Rog(p.Wj)}", 84, y, 10, Napis); y += 14;
            T($"rogatki wy: {Przejazd.Rog(p.Wy)}", 84, y, 10, Napis); y += 14;
            var dod = new List<string>();
            if (p.ToryWyl.Count > 0) dod.Add("wył. czujniki: " + string.Join(",", p.ToryWyl));
            if (p.Droznik == true) dod.Add("dróżnik");
            if (p.NaPulpicie) dod.Add(p.Uzk ? "na pulpicie gry (UZK)" : "na pulpicie gry");
            if (dod.Count > 0) T(string.Join(" · ", dod), 8, Wys - 16, 9, Szary);
        }
    }

    static void Pole(DrawingContext dc, string n, double x, double y, Brush b, double dpi)
    {
        dc.DrawRectangle(b, null, new Rect(x, y, 30, 16));
        dc.DrawText(new FormattedText(n, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Mono, 10, Tlo, dpi), new Point(x + 15 - 3.5 * n.Length, y + 2));
    }

    /// <summary>Linia przejazdu z oznaczeniami końców („Y”) – jak PulpitSymbole.Przejazd w LcsWidzew.</summary>
    static void Symbol(DrawingContext dc, double x, double y0, double y1, Brush linia, Brush konce)
    {
        // jak pulpit SUK (PanelView): najpierw przejazd, potem tory NA WIERZCHU – tor przecina linię przejazdu
        dc.DrawLine(new Pen(linia, 3), new Point(x, y0 + 14), new Point(x, y1 - 14));
        var pk = new Pen(konce, 2.6);
        foreach (var (y, side) in new[] { (y0, -1), (y1, 1) })
        {
            dc.DrawLine(pk, new Point(x, y), new Point(x, y + side * 14));
            dc.DrawLine(pk, new Point(x, y + side * 10), new Point(x - 6, y + side * 16));
            dc.DrawLine(pk, new Point(x, y + side * 10), new Point(x + 6, y + side * 16));
        }
        var tor = new Pen(Szary, 4.5);
        dc.DrawLine(tor, new Point(x - 30, y0 + 22), new Point(x + 30, y0 + 22));
        dc.DrawLine(tor, new Point(x - 30, y1 - 22), new Point(x + 30, y1 - 22));
    }
}
