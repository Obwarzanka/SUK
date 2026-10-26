namespace Lcs.Model;

/// <summary>
/// Symbole i opisy obrazu w wersji 2 (PLAN_EDYTOR_ZOBRAZOWANIA_V2.md § 2, § 5, etap E3): rodzaje elementów, położenie na torze, kierunek,
/// domyślne miejsca opisów wg pulpitu (Ie-104 § 8: opis sygnalizatora z prawej strony patrząc w kierunku wskazanym). Bez WPF.
/// Kształty rysuje wspólny kod pulpitu (<c>LcsWidzew/UI/PulpitSymbole.cs</c>).
/// </summary>
public static class ObrSymbole
{
    // rodzaje elementów (ObrElement.Typ)
    public const string Semafor = "semafor", Tarcza = "tarcza", Powtarzacz = "powtarzacz", Pk = "pk", Przerwa = "przerwa", Wykolejnica = "wykolejnica",
        Koziol = "koziol", Przejazd = "przejazd", Peron = "peron", Zwrotnica = "zwrotnica", NumerToru = "numerToru", OpisOdcinka = "opisOdcinka", Napis = "napis",
        /// <summary>Strzałka blokady liniowej (z obrazu wersji 1; klocek końca szlaku – etap E6). <see cref="ObrElement.Pola"/> – koniec szlaku UT (0 / 1).</summary>
        StrzalkaBlokady = "strzalkaBlokady",
        /// <summary>
        /// Odstępy SBL (etap E6, decyzja właściciela 2026-10-22): rząd pól w osi toru szlakowego przy jego końcu na obrazie i kolumna dalszych pól
        /// przy krawędzi obrazu (wzór GrM: 238 252 270 280 i nad nimi 228, 208, 194). <see cref="ObrElement.Na"/> – węzeł końca toru,
        /// <see cref="ObrElement.Pola"/> – pól w rzędzie, <see cref="ObrElement.Rozm"/> – [pól w kolumnie, kierunek kolumny −1 w górę / +1 w dół];
        /// opisy – numery pól po kolei od stacji na zewnątrz.
        /// </summary>
        OdstepySbl = "odstepySbl",
        /// <summary>Okno numeru pociągu dla szlaku (pole PIP – wzór GrM „228-91922”): prostokąt <see cref="ObrElement.P"/>, <see cref="ObrElement.Rozm"/>.</summary>
        PolePip = "polePip",
        /// <summary>Klocek „koniec szlaku” z palety – wstawia strzałkę blokady, semafor wjazdowy i nazwę sąsiada (zwykłe elementy).</summary>
        KoniecSzlaku = "koniecSzlaku";
    /// <summary>Długość pola odstępu [j.] (z przerwą 6 j.) i odstęp kolejnych pól kolumny [j.] – jak na wzorze GrM.</summary>
    public const int PoleSbl = 40, KrokKolumny = 42;
    // style (ObrElement.Styl)
    /// <summary>Semafor: wjazdowy (z trójkątem końca przebiegu wyjazdowego), z funkcją tarczy manewrowej (domyślny), tylko pociągowy.</summary>
    public const string SemWjazdowy = "wjazdowy", SemManewrowy = "manewrowy", SemPociagowy = "pociagowy",
        /// <summary>Semafor wjazdowy bez trójkąta końca przebiegu wyjazdowego (decyzja właściciela 2026-10-22: do wyboru przy semaforze).</summary>
        SemWjazdowyBez = "wjazdowyBezKonca";
    public static bool Wjazdowy(string styl) => styl is SemWjazdowy or SemWjazdowyBez;
    /// <summary>Powtarzacz: semafora (domyślny) albo tarcza ostrzegawcza.</summary>
    public const string PowtSemafora = "sp", PowtTarcza = "to";
    /// <summary>Zwrotnica: położenie zasadnicze „+” na wprost (domyślne) albo na bok.</summary>
    public const string PlusBok = "plusBok";
    /// <summary>Numer toru niewidoczny: tylko kotwica przypięcia toru (F8), bez pola numeru na pulpicie, w PNG i na wydruku (pytanie właściciela 2026-10-24).</summary>
    public const string NumerUkryty = "ukryty";
    public static bool Ukryty(ObrElement el) => el.Typ == NumerToru && el.Styl == NumerUkryty;
    // role opisów
    public const string Nazwa = "nazwa", Numer = "numer";

    /// <summary>Paleta (decyzja właściciela 2026-10-22, plan § 11 pkt 9): klawisze 1…9 wg Ie-104, potem opisy.</summary>
    public static readonly (string Typ, string Nazwa, string Klawisz)[] Paleta =
    {
        (Semafor, "semafor", "1"), (Tarcza, "tarcza manewrowa", "2"), (Powtarzacz, "powtarzacz", "3"), (Pk, "PK", "4"), (Przerwa, "przerwa izolacyjna", "5"),
        (Wykolejnica, "wykolejnica", "6"), (Koziol, "kozioł", "7"), (Przejazd, "przejazd", "8"), (Peron, "peron", "9"),
        (NumerToru, "numer toru", "N"), (OpisOdcinka, "opis odcinka", "O"), (Napis, "napis", "X"),
    };
    public static string NazwaTypu(string t) => t == Zwrotnica ? "zwrotnica" : t == StrzalkaBlokady ? "strzałka blokady" : t == OdstepySbl ? "odstępy SBL" : t == PolePip ? "okno numeru pociągu" : t == KoniecSzlaku ? "koniec szlaku" : t == ObrGraf.Skrzyzowanie ? "skrzyżowanie" : t == ObrGraf.RozjazdKrzyzowy ? "rozjazd krzyżowy"
        : Paleta.FirstOrDefault(p => p.Typ == t).Nazwa ?? t;

    /// <summary>Rodzaj i odmiana symbolu sygnalizatora z UT (rodzaj, funkcja, komora B – tarcza manewrowa); null – nie sygnalizator.</summary>
    public static (string Typ, string Styl)? ZUt(UtDevice d) => d.Kind switch
    {
        UtDevice.TarczaMan => (Tarcza, null),
        UtDevice.TarczaOstrz => (Powtarzacz, PowtTarcza),
        UtDevice.Semafor when d.Funkcja == UtDevice.FPowtarzacz => (Powtarzacz, PowtSemafora),
        UtDevice.Semafor when d.Funkcja == UtDevice.FPk => (Pk, null),
        UtDevice.Semafor when d.Funkcja == UtDevice.FWjazdowy => (Semafor, SemWjazdowy),
        UtDevice.Semafor => (Semafor, UtDevice.Czynne(d.LightsOrDefault).Any(x => x.C == 'B') ? SemManewrowy : SemPociagowy),
        _ => null,
    };

    /// <summary>Element stoi na odcinku toru (pozostałe: w węźle – kozioł, zwrotnica; wolne – peron, napis).</summary>
    public static bool NaTorze(string t) => t is Semafor or Tarcza or Powtarzacz or Pk or Przerwa or Wykolejnica or Przejazd or NumerToru or OpisOdcinka or StrzalkaBlokady;
    public static bool WWezle(string t) => t is Koziol or Zwrotnica or OdstepySbl;
    public static bool Wolny(string t) => t is Peron or Napis or PolePip;
    /// <summary>Symbol ma kierunek (ostrze) – obrót spacją.</summary>
    public static bool Kierunkowy(string t) => t is Semafor or Tarcza or Powtarzacz or Pk;
    /// <summary>Rola opisu tworzonego przy wstawieniu (null – bez opisu).</summary>
    public static string RolaOpisu(string t) => t switch
    {
        Semafor or Tarcza or Powtarzacz or Pk or Wykolejnica or Przejazd => Nazwa,
        Zwrotnica or NumerToru or OpisOdcinka or Peron or Napis or OdstepySbl => Numer,
        _ => null,
    };

    /// <summary>
    /// Położenie elementu [j.] i kierunek jednostkowy (w stronę ostrza; dla toru – wzdłuż odcinka od A do B razy <see cref="ObrElement.Kier"/>;
    /// kozioł – na zewnątrz toru; zwrotnica – w stronę iglicy). Null – element bez miejsca (np. odcinek usunięty).
    /// </summary>
    public static (double X, double Y, double Ux, double Uy)? Pozycja(ObrObraz o, ObrElement el)
    {
        if (Wolny(el.Typ) || el.Na == null) return el.P == null ? null : (el.P[0], el.P[1], 1, 0);
        if (ObrGraf.Odcinek(o, el.Na) is ObrOdcinek e)
        {
            var (a, b) = ObrGraf.Konce(o, e);
            double dx = b[0] - a[0], dy = b[1] - a[1], l = Math.Max(1e-9, Math.Sqrt(dx * dx + dy * dy));
            int k = el.Kier < 0 ? -1 : 1;
            return (a[0] + dx * el.T, a[1] + dy * el.T, k * dx / l, k * dy / l);
        }
        if (ObrGraf.Wezel(o, el.Na) is ObrWezel w)
        {
            var przy = ObrGraf.Przy(o, w.Id);
            if (el.Typ == OdstepySbl && przy.Count >= 1)
            {
                // odstępy SBL: kierunek od końca toru do wnętrza (w stronę stacji); w środku toru szlaku wewnętrznego (dwa odcinki) – odcinek w stronę
                // stacji wg Kier (+1 – w prawo, a dla pionu w dół)
                var s = przy.Count == 2 ? przy.FirstOrDefault(z => { var (zx, zy) = ObrGraf.Kier(o, z, w.Id); return (zx != 0 ? zx : zy) == (el.Kier < 0 ? -1 : 1); }) ?? przy[0] : przy[0];
                var (kx, ky) = ObrGraf.Kier(o, s, w.Id);
                return Jedn(w.P[0], w.P[1], kx, ky);
            }
            if (el.Typ == Koziol && przy.Count >= 1)
            {
                var (kx, ky) = ObrGraf.Kier(o, przy[0], w.Id);
                return Jedn(w.P[0], w.P[1], -kx, -ky);
            }
            if (Iglica(o, w.Id) is var (ix, iy)) return Jedn(w.P[0], w.P[1], ix, iy);
            return (w.P[0], w.P[1], 1, 0);
        }
        return null;
    }

    static (double, double, double, double) Jedn(double x, double y, double ux, double uy)
    {
        double l = Math.Sqrt(ux * ux + uy * uy);
        return l < 1e-9 ? (x, y, 1, 0) : (x, y, ux / l, uy / l);
    }

    /// <summary>Kierunek iglicy rozjazdu: odcinek toru zasadniczego po przeciwnej stronie niż odgałęzienie (od węzła).</summary>
    public static (int X, int Y)? Iglica(ObrObraz o, string wezel)
    {
        var odg = ObrGraf.Odgalezienie(o, wezel); if (odg == null) return null;
        var b = ObrGraf.Kier(o, odg, wezel);
        foreach (var e in ObrGraf.Przy(o, wezel).Where(x => x != odg))
        {
            var k = ObrGraf.Kier(o, e, wezel);
            if (k.X * b.X + k.Y * b.Y < 0) return k;
        }
        return null;
    }

    /// <summary>Odcinek „+” rozjazdu: na wprost (przedłużenie iglicy) albo odgałęzienie – wg stylu zwrotnicy.</summary>
    public static ObrOdcinek OdcinekPlus(ObrObraz o, string wezel, bool plusBok)
    {
        var odg = ObrGraf.Odgalezienie(o, wezel); if (odg == null) return null;
        if (plusBok) return odg;
        var ig = Iglica(o, wezel);
        return ObrGraf.Przy(o, wezel).FirstOrDefault(e => e != odg && ig is var (ix, iy) && ObrGraf.Kier(o, e, wezel) is var k && (k.X != ix || k.Y != iy));
    }

    /// <summary>Zwrotnica (element numeru) w węźle rozjazdu; null – rozjazd bez numeru.</summary>
    public static ObrElement ZwrotnicaW(ObrObraz o, string wezel) => o.Elementy.FirstOrDefault(x => x.Typ == Zwrotnica && x.Na == wezel);

    /// <summary>
    /// Domyślne przesunięcie opisu od elementu [j.] – jak na pulpicie (PanelView): sygnalizator – z prawej strony patrząc w kierunku ostrza
    /// (w prawo – pod torem, w lewo – nad torem), zwrotnica – po stronie przeciwnej do odgałęzienia, przesunięta od iglicy; numer toru –
    /// w osi toru; opis odcinka – pod torem; wykolejnica – pod torem; przejazd – obok linii przejazdu.
    /// </summary>
    public static int[] OpisDomyslny(ObrObraz o, ObrElement el)
    {
        var poz = Pozycja(o, el);
        double ux = poz?.Ux ?? 1, uy = poz?.Uy ?? 0;
        // prawa strona kierunku na ekranie (oś y w dół): (−uy, ux)
        int[] Prawa(double r) => new[] { (int)Math.Round(-uy * r), (int)Math.Round(ux * r) };
        switch (el.Typ)
        {
            case Semafor: case Tarcza: return Prawa(ux < 0 ? 16 : 17);
            case Powtarzacz: return Prawa(15);
            case Pk: return Prawa(13);
            case Wykolejnica: return new[] { 0, 17 };
            case Przejazd: return new[] { 8, -(Rozmiar(el)[0] + 4) };
            case NumerToru: case Napis: return new[] { 0, 0 };
            case OpisOdcinka: return new[] { 0, 14 };
            case Peron: return new[] { (el.Rozm?[0] ?? 160) / 2, (el.Rozm?[1] ?? 24) / 2 };
            case OdstepySbl: return new[] { 0, -12 };
            case Zwrotnica:
                {
                    var odg = ObrGraf.Odgalezienie(o, el.Na);
                    bool odgDol = odg != null && ObrGraf.Kier(o, odg, el.Na).Y > 0;
                    var ig = Iglica(o, el.Na);
                    int ix = ig?.X ?? -1;
                    return new[] { ix < 0 ? 8 : ix > 0 ? -8 : 0, odgDol ? -12 : 13 };
                }
        }
        return new[] { 0, -14 };
    }

    /// <summary>Zasięg przejazdu w górę i w dół [j.] (domyślnie 28 / 28 jak w starym edytorze).</summary>
    public static int[] Rozmiar(ObrElement el) => el.Typ switch
    {
        Przejazd => el.Rozm ?? new[] { 28, 28 },
        PolePip => el.Rozm ?? new[] { 88, 68 },
        OdstepySbl => el.Rozm ?? new[] { 0, -1 },
        _ => el.Rozm ?? new[] { 160, 24 },
    };

    /// <summary>
    /// Pola odstępów SBL po kolei od stacji na zewnątrz: kreska od A do B [j.], w kolumnie czy w rzędzie. Rząd – w osi toru od końca toru do wnętrza
    /// (pole <see cref="PoleSbl"/> j. z przerwą 6 j.), kolumna – nad / pod najdalszym polem rzędu, co <see cref="KrokKolumny"/> j.
    /// </summary>
    public static List<(double Ax, double Ay, double Bx, double By, bool Kolumna)> PolaSbl(ObrObraz o, ObrElement el)
    {
        var res = new List<(double, double, double, double, bool)>();
        if (el.Typ != OdstepySbl || Pozycja(o, el) is not var (x, y, ux, uy)) return res;
        int n = Math.Max(0, el.Pola ?? 0); var r = Rozmiar(el); int m = Math.Max(0, r[0]), cd = r[1] < 0 ? -1 : 1;
        // rząd: j = 0 przy końcu toru; na zewnątrz – od j = n − 1 do 0
        for (int j = n - 1; j >= 0; j--)
            res.Add((x + ux * (j * PoleSbl + 3), y + uy * (j * PoleSbl + 3), x + ux * ((j + 1) * PoleSbl - 3), y + uy * ((j + 1) * PoleSbl - 3), false));
        // kolumna nad / pod polem przy końcu toru (wektor prostopadły w stronę cd)
        double px = Math.Abs(uy) > 0.5 ? cd : 0, py = Math.Abs(uy) > 0.5 ? 0 : cd;
        for (int c = 1; c <= m; c++)
            res.Add((x + ux * 3 + px * KrokKolumny * c, y + uy * 3 + py * KrokKolumny * c, x + ux * (PoleSbl - 3) + px * KrokKolumny * c, y + uy * (PoleSbl - 3) + py * KrokKolumny * c, true));
        return res;
    }

    /// <summary>
    /// Miejsce symbolu na torze pod kliknięciem: odcinek najbliższy (do <paramref name="tol"/> j.), punkt siatki na nim i położenie T; null – za daleko.
    /// </summary>
    public static (ObrOdcinek E, double T, int[] P)? NaTorzeW(ObrObraz o, double x, double y, double tol, int g)
    {
        var best = o.Odcinki.Select(e => { var (a, b) = ObrGraf.Konce(o, e); return (e, a, b, r: ObrGraf.Rzut(a, b, x, y)); })
            .Where(z => z.r.Dist <= tol).OrderBy(z => z.r.Dist).FirstOrDefault();
        if (best.e == null) return null;
        var p = ObrGraf.NaOdcinku(best.a, best.b, x, y, g);
        double len = Math.Sqrt(Math.Pow(best.b[0] - best.a[0], 2) + Math.Pow(best.b[1] - best.a[1], 2));
        double t = len < 1e-9 ? 0 : Math.Sqrt(Math.Pow(p[0] - best.a[0], 2) + Math.Pow(p[1] - best.a[1], 2)) / len;
        return (best.e, t, p);
    }

    /// <summary>Kierunek domyślny symbolu na odcinku: ostrzem w stronę środka obrazu (wjazd do stacji); <paramref name="odwroc"/> – przeciwnie.</summary>
    public static int KierDomyslny(ObrObraz o, ObrOdcinek e, double x, double y, bool odwroc)
    {
        var (a, b) = ObrGraf.Konce(o, e);
        double dx = b[0] - a[0], dy = b[1] - a[1];
        double s = Math.Abs(dx) > 1e-9 ? Math.Sign(dx) * Math.Sign(o.Szer / 2.0 - x) : Math.Sign(dy) * Math.Sign(o.Wys / 2.0 - y);
        int k = s < 0 ? -1 : 1;
        return odwroc ? -k : k;
    }

    /// <summary>
    /// Odcinki toru od odcinka <paramref name="e"/> w stronę <paramref name="k"/> (+1 – do B, −1 – do A) przez węzły z dwoma odcinkami, do rozjazdu albo
    /// końca: (odcinek, zgodnie z A → B), stopień węzła na końcu.
    /// </summary>
    public static (List<(ObrOdcinek E, bool Fwd)> Odc, int Stopien) Dalej(ObrObraz o, ObrOdcinek e, int k)
    {
        var res = new List<(ObrOdcinek, bool)> { (e, k > 0) };
        var w = k > 0 ? e.B : e.A; var cur = e;
        for (int i = 0; i < o.Odcinki.Count; i++)
        {
            var przy = ObrGraf.Przy(o, w);
            if (przy.Count != 2) return (res, przy.Count);
            cur = przy[0] == cur ? przy[1] : przy[0];
            res.Add((cur, cur.A == w));
            w = ObrGraf.Drugi(cur, w);
        }
        return (res, 2);
    }

    static double Dl(ObrObraz o, ObrOdcinek e) { var (a, b) = ObrGraf.Konce(o, e); return Math.Sqrt(Math.Pow(b[0] - a[0], 2) + Math.Pow(b[1] - a[1], 2)); }

    /// <summary>
    /// Strona stacji na torze szlakowym w punkcie T odcinka (szlak wewnętrzny obszaru LCS – tor między dwiema stacjami jednego obrazu): +1 – w stronę B,
    /// −1 – w stronę A. Stacja – po stronie rozjazdu (koniec toru to dalszy szlak), dwa rozjazdy – bliższy; bez rozjazdów – w stronę środka obrazu.
    /// </summary>
    public static int StronaStacji(ObrObraz o, ObrOdcinek e, double t, bool odwroc)
    {
        double L = Dl(o, e);
        var (ob, sb) = Dalej(o, e, 1); var (oa, sa) = Dalej(o, e, -1);
        double db = L * (1 - t) + ob.Skip(1).Sum(z => Dl(o, z.E)), da = L * t + oa.Skip(1).Sum(z => Dl(o, z.E));
        int k;
        if (sb >= 3 && sa < 3) k = 1;
        else if (sa >= 3 && sb < 3) k = -1;
        else if (sa >= 3 && sb >= 3) k = db <= da ? 1 : -1;
        else { var (a, b) = ObrGraf.Konce(o, e); k = KierDomyslny(o, e, a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, false); }
        return odwroc ? -k : k;
    }

    /// <summary>Semafor wjazdowy na torze od punktu T odcinka w stronę <paramref name="k"/> do najbliższego rozjazdu.</summary>
    public static bool WjazdowyDalej(ObrObraz o, ObrOdcinek e, double t, int k)
    {
        foreach (var (z, fwd) in Dalej(o, e, k).Odc)
            if (o.Elementy.Any(x => x.Typ == Semafor && Wjazdowy(x.Styl) && x.Na == z.Id && (z != e || (x.T - t) * k > 0))) return true;
        return false;
    }

    /// <summary>Kier elementu w węźle (odstępy SBL w środku toru): znak kierunku odcinka w stronę stacji – x, a dla pionu y.</summary>
    public static int KierWezla(ObrObraz o, ObrOdcinek e, int k)
    {
        var (a, b) = ObrGraf.Konce(o, e);
        int sx = Math.Sign(b[0] - a[0]) * k, sy = Math.Sign(b[1] - a[1]) * k;
        return (sx != 0 ? sx : sy) < 0 ? -1 : 1;
    }

    /// <summary>Nowy element z opisem w miejscu domyślnym (tekst pusty – wpisuje człowiek).</summary>
    public static ObrElement Nowy(ObrObraz o, string typ, string na, double t, int kier, int[] p = null)
    {
        var el = new ObrElement { Typ = typ, Na = na, T = t, Kier = kier, P = p };
        if (typ == Semafor) el.Styl = SemManewrowy;
        if (typ == Powtarzacz) el.Styl = PowtSemafora;
        if (typ == Peron) el.Rozm = new[] { 160, 24 };
        if (typ == PolePip) el.Rozm = new[] { 88, 68 };
        if (typ == OdstepySbl)
        {
            el.Pola = 4; el.Rozm = new[] { 3, -1 };
            o.Elementy.Add(el);
            for (int i = 0; i < 7; i++) el.Opisy.Add(new ObrOpis { Rola = Numer, Tekst = "", D = new[] { 0, -12 } });
            return el;
        }
        if (typ == Przejazd) el.Rozm = new[] { 28, 28 };
        o.Elementy.Add(el);
        if (RolaOpisu(typ) is string rola) el.Opisy.Add(new ObrOpis { Rola = rola, Tekst = "", D = OpisDomyslny(o, el) });
        return el;
    }

    /// <summary>Uwagi do symboli (⚠ na płótnie): sygnalizator bez nazwy, rozjazd bez numeru, kozioł nie na końcu toru.</summary>
    public static List<(string El, string Tekst)> Uwagi(ObrObraz o)
    {
        var r = new List<(string, string)>();
        foreach (var el in o.Elementy)
        {
            if (Kierunkowy(el.Typ) && el.Opisy.All(d => string.IsNullOrWhiteSpace(d.Tekst))) r.Add((el.Id, $"{NazwaTypu(el.Typ)} bez nazwy"));
            if (el.Typ == Koziol && ObrGraf.Przy(o, el.Na).Count != 1) r.Add((el.Id, "kozioł nie na końcu toru"));
            if (el.Typ == Zwrotnica && ObrGraf.RodzajWezla(o, el.Na) != ObrGraf.Rodzaj.Rozjazd) r.Add((el.Id, "numer zwrotnicy w węźle, który nie jest rozjazdem"));
        }
        foreach (var w in o.Wezly.Where(w => ObrGraf.RodzajWezla(o, w.Id) == ObrGraf.Rodzaj.Rozjazd && ZwrotnicaW(o, w.Id) == null))
            r.Add((w.Id, "rozjazd bez numeru (E albo PPM)"));
        return r;
    }
}
