using System.Windows;
using System.Windows.Media;

namespace LcsWidzew.UI;

/// <summary>
/// Kształty symboli pulpitu wg Ie-104 § 8 (wzory EbiScreen / SimRail) – jedno miejsce dla pulpitu symulatora (<see cref="PanelView"/>)
/// i edytora zobrazowania (EdytorZobrazowania2, PLAN_EDYTOR_ZOBRAZOWANIA_V2.md: „symbole rysuj tym samym kodem, nie kopią”).
/// Jednostki obrazu (1 j. = 1 px przy powiększeniu 1); symbol w osi toru poziomego, <c>d</c> = +1 / −1 – ostrze w prawo / w lewo
/// (edytor obraca układ współrzędnych dla torów ukośnych). Barwę stanu podaje wywołujący.
/// </summary>
public static class PulpitSymbole
{
    static Brush B(byte r, byte g, byte b) { var x = new SolidColorBrush(Color.FromRgb(r, g, b)); x.Freeze(); return x; }
    // barwy wg Ie-104 § 4 ust. 13
    public static readonly Brush Bg = B(0, 0, 0), Grey = B(0x9a, 0x9a, 0x9a), SwLabel = B(0xb4, 0xb4, 0xb4), PlatformLine = B(0xb0, 0xb0, 0xb0),
        SigName = B(0xe8, 0xc0, 0x00), TmName = B(0x18, 0xc0, 0xc0), CrossPink = B(0xf0, 0x80, 0xf0), Label = B(0xd0, 0xd0, 0xd0), White = B(0xff, 0xff, 0xff), Dim2 = B(0x80, 0x80, 0x80);

    /// <summary>Połowa wysokości symbolu sygnalizatora [j.].</summary>
    public const double H = 6.2;
    /// <summary>Grubość toru w stanie zasadniczym [j.].</summary>
    public const double Tor = 4.6;

    public static Geometry Tri(double tipX, double cy, double d, double h, bool filled)
    {
        var g = new StreamGeometry();
        using (var c = g.Open()) { c.BeginFigure(new Point(tipX, cy), filled, true); c.LineTo(new Point(tipX - d * 1.5 * h, cy - h), true, false); c.LineTo(new Point(tipX - d * 1.5 * h, cy + h), true, false); }
        g.Freeze(); return g;
    }
    public static Geometry Chevron(double tipX, double cy, double d, double h, double len)
    {
        var g = new StreamGeometry();
        using (var c = g.Open()) { c.BeginFigure(new Point(tipX - d * len, cy - h), false, false); c.LineTo(new Point(tipX, cy), true, false); c.LineTo(new Point(tipX - d * len, cy + h), true, false); }
        g.Freeze(); return g;
    }

    /// <summary>Ostrze sygnalizatora pociągowego / tarczy (x) dla symbolu w punkcie <paramref name="cx"/>.</summary>
    public static double Ostrze(double cx, double d) => cx + d * 8;

    /// <summary>Semafor: pełny trójkąt; z funkcją tarczy manewrowej (sygnały Ms2) – otwarty grot za nim (Ie-104, symbol 52.6).</summary>
    public static void Semafor(DrawingContext dc, double cx, double cy, double d, Brush c, bool manewrowy)
    {
        double tipX = Ostrze(cx, d);
        dc.DrawGeometry(c, null, Tri(tipX, cy, d, H + 1, true));
        if (manewrowy) dc.DrawGeometry(null, new Pen(c, 2.4) { LineJoin = PenLineJoin.Miter }, Chevron(tipX - d * 11.5, cy, d, H + 1, 9));
    }

    /// <summary>
    /// Semafor wjazdowy: pełny trójkąt i przerwa w torze pod trójkątem końca przebiegu wyjazdowego (rysowanym osobno – <see cref="KoniecWyjazdu"/>);
    /// <paramref name="koniec"/> = false – semafor bez końca przebiegu wyjazdowego (decyzja właściciela 2026-10-22: do wyboru przy semaforze), bez przerwy.
    /// </summary>
    public static void Wjazdowy(DrawingContext dc, double cx, double cy, double d, Brush c, Brush bg, bool koniec = true)
    {
        dc.DrawGeometry(c, null, Tri(Ostrze(cx, d), cy, d, H + 1, true));
        if (koniec) dc.DrawRectangle(bg, null, new Rect(cx + d * 15 - 6, cy - 4, 12, 8));
    }

    /// <summary>Funkcja końca przebiegu wyjazdowego (Ie-104 § 8 pkt 3 lit. f) – mały trójkąt za semaforem wjazdowym, skierowany w stronę szlaku.</summary>
    public static void KoniecWyjazdu(DrawingContext dc, double cx, double cy, double d, Brush c) => dc.DrawGeometry(c, null, Tri(cx + d * 18, cy, -d, 5, true));

    /// <summary>Tarcza manewrowa: pojedynczy otwarty grot.</summary>
    public static void Tarcza(DrawingContext dc, double cx, double cy, double d, Brush c) =>
        dc.DrawGeometry(null, new Pen(c, 2.4) { LineJoin = PenLineJoin.Miter }, Chevron(Ostrze(cx, d), cy, d, H, 10));

    /// <summary>Powtarzacz: semafora (<paramref name="sp"/> – trójkąt obrysowany) albo tarcza ostrzegawcza (grot z kropką).</summary>
    public static void Powtarzacz(DrawingContext dc, double cx, double cy, double d, Brush c, bool sp)
    {
        if (sp) dc.DrawGeometry(null, new Pen(c, 1.6), Tri(cx + d * 6, cy, d, H * 0.8, false));
        else { dc.DrawGeometry(null, new Pen(c, 1.8), Chevron(cx + d * 6, cy, d, H * 0.8, 9)); dc.DrawEllipse(c, null, new Point(cx - d * 1, cy), 1.8, 1.8); }
    }

    /// <summary>PK – punkt końcowy przebiegu: mały trójkąt w osi toru.</summary>
    public static void Pk(DrawingContext dc, double cx, double cy, double d, Brush c) => dc.DrawGeometry(null, new Pen(c, 1.6), Tri(cx + d * 5, cy, d, 4.5, false));

    /// <summary>Kozioł oporowy: nawias zamykający koniec toru, otwarty w stronę toru (<paramref name="od"/> – na zewnątrz toru).</summary>
    public static void Koziol(DrawingContext dc, double x, double y, int od, Brush c)
    {
        double bx = x + od * 1, t = -od * 6;
        var pen = new Pen(c, 2.4) { StartLineCap = PenLineCap.Square, EndLineCap = PenLineCap.Square };
        dc.DrawLine(pen, new Point(bx, y - 8), new Point(bx, y + 8));
        dc.DrawLine(pen, new Point(bx, y - 8), new Point(bx + t, y - 8));
        dc.DrawLine(pen, new Point(bx, y + 8), new Point(bx + t, y + 8));
    }

    /// <summary>Wykolejnica: prostokąt w poprzek toru.</summary>
    public static void Wykolejnica(DrawingContext dc, double x, double y, Brush c) => dc.DrawRectangle(c, null, new Rect(x - 2.5, y - 9, 5, 18));

    /// <summary>Przerwa izolacyjna w torze (jak przerwa między odcinkami na pulpicie – 1,8 j. z każdej strony styku).</summary>
    public static void Przerwa(DrawingContext dc, double x, double y, Brush bg) => dc.DrawRectangle(bg, null, new Rect(x - 1.8, y - 4, 3.6, 8));

    /// <summary>Oznaczenie końca linii przejazdu: „Y” – rozwidlenie, „T” – poprzeczka.</summary>
    public static void KoniecPrzejazdu(DrawingContext dc, double x, double y, int side, string kind = "Y")
    {
        var p = new Pen(CrossPink, 2.6);
        dc.DrawLine(p, new Point(x, y), new Point(x, y + side * 14));
        if (kind == "T") { dc.DrawLine(p, new Point(x - 6, y), new Point(x + 6, y)); return; }
        dc.DrawLine(p, new Point(x, y + side * 10), new Point(x - 6, y + side * 16));
        dc.DrawLine(p, new Point(x, y + side * 10), new Point(x + 6, y + side * 16));
    }

    /// <summary>Linia przejazdu w poprzek torów z oznaczeniami końców (od <paramref name="y0"/> do <paramref name="y1"/>).</summary>
    public static void Przejazd(DrawingContext dc, double x, double y0, double y1, string kind = "Y")
    {
        dc.DrawLine(new Pen(CrossPink, 3), new Point(x, y0 + 14), new Point(x, y1 - 14));
        KoniecPrzejazdu(dc, x, y0, -1, kind); KoniecPrzejazdu(dc, x, y1, 1, kind);
    }

    /// <summary>Peron: prostokąt z podwójną ramką (napis rysuje wywołujący).</summary>
    public static void Peron(DrawingContext dc, Rect r)
    {
        dc.DrawRectangle(null, new Pen(PlatformLine, 1.4), r);
        dc.DrawRectangle(null, new Pen(PlatformLine, 0.8), new Rect(r.X + 3, r.Y + 3, r.Width - 6, r.Height - 6));
    }

    /// <summary>Grot strzałki blokady liniowej (Ie-104 § 8 pkt 17): ostrze w <paramref name="tip"/>, oś <paramref name="u"/>, zwrot <paramref name="toward"/> (±1).</summary>
    public static void GrotBlokady(DrawingContext dc, Point tip, Vector u, double toward, Brush c)
    {
        var back = tip - u * (toward * 10); var nrm = new Vector(-u.Y, u.X) * 6.5;
        var g = new StreamGeometry();
        using (var ctx = g.Open()) { ctx.BeginFigure(tip, true, true); ctx.LineTo(back + nrm, true, false); ctx.LineTo(back - nrm, true, false); }
        g.Freeze(); dc.DrawGeometry(c, null, g);
    }

    /// <summary>
    /// Strzałka blokady w stanie neutralnym (szara dwustronna) ze środkiem w <paramref name="srodek"/> – jak na pulpicie: wycięcie toru od 18 j.
    /// przed do 18 j. za środkiem, trzon ±7 j., groty ±16 j. (PanelView: d0..d1 = 14..46 od granicy, środek 30 j.).
    /// </summary>
    public static void StrzalkaNeutralna(DrawingContext dc, Point srodek, Vector u, Brush c, Brush bg)
    {
        dc.DrawLine(new Pen(bg, 9) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat }, srodek - u * 18, srodek + u * 18);
        dc.DrawLine(new Pen(c, 6), srodek - u * 7, srodek + u * 7);
        GrotBlokady(dc, srodek + u * 16, u, 1, c); GrotBlokady(dc, srodek - u * 16, u, -1, c);
    }

    /// <summary>Tło pod numerem toru w przerwie linii.</summary>
    public static Rect PoleNumeruToru(double x, double y) => new(x - 10, y - 6, 20, 12);
}
