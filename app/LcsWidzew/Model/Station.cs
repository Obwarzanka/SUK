using System.IO;
using System.Text.Json;

namespace LcsWidzew.Model;

/// <summary>
/// Model obszaru LCS wczytywany z Data/station.json (tools/lcs_panel.py).
/// Klucze obiektów mają przedrostek posterunku: LW: (Łódź Widzew), LF: (Łódź Fabryczna), LN: (Łódź Niciarniana).
/// Konwencja: oś X rośnie na wschód, dir = +1 – jazda na wschód.
/// </summary>
public class Node
{
    public string Id, Post;
    public double X, Y;
    public string Kind = "", Ref = "";
    public List<string> Edges = new();
    public string Switch;
    public List<string> Signals = new();
}

public class Edge
{
    public string Id, A, B, Sec, Track = "", Line = "";
    public List<(double X, double Y)> Pts = new();
    public double Len;
    /// <summary>Łuk: promień [m] i przechyłka [mm] (0 – tor prosty / brak danych).</summary>
    public double Radius, Cant;
    /// <summary>Wysokość toru nad terenem – nasyp [m] (ujemna – przekop); dane pomocnicze planu.</summary>
    public double Embank;
    /// <summary>Pochylenie podłużne [‰] od A do B (dodatnie – wzniesienie), z profilu wysokościowego; NaN – brak danych.</summary>
    public double Grad = double.NaN;
    public bool Platform, Szlak;
    /// <summary>Tor przy peronie z UT (JSON "peron": true, eksport „Do symulatora”) – jak odcinek „:JT” w dawnych układach.</summary>
    public bool PeronUt;
    /// <summary>Zobrazowanie: odcinek bez kontroli niezajętości – linia przerywana (Ie-104 § 8 ust. 1; oznacza człowiek w edytorze zobrazowania).</summary>
    public bool NoDetect;
    /// <summary>Kierunek jazdy A→B dla toru pionowego (1 – na wschód, -1 – na zachód); dla pozostałych wynika z położenia końców.</summary>
    public int Dir;
    /// <summary>Prędkość dopuszczalna na torze [km/h] (0 – domyślna: stacja 60, szlak – prędkość pociągu).</summary>
    public double VMax;
    /// <summary>Ograniczenie wynikające ze zwrotnicy (jazda na bok, na ostrze lub z ostrza) [km/h], 0 – brak.</summary>
    public double SwLimit;
    /// <summary>Łącznik obrazów: połączenie logiczne końców torów na dwóch obrazach (nie rysowane, ma długość).</summary>
    public bool Link;
    /// <summary>Tor rozebrany – rysowany linią przerywaną, wyłączony z jazd i przebiegów.</summary>
    public bool Dismantled;
    /// <summary>Kategoria toru (Ig-10 § 4): "gz" – główny zasadniczy, "gd" – główny dodatkowy, "b" – boczny / inny (domyślnie). Elec – tor zelektryfikowany.</summary>
    public string Cat = "b"; public bool Elec;
    public bool Main => Szlak || Cat is "gz" or "gd";
}

public class SwitchDef
{
    public string Num, Node, Sec, Tip, Plus, Minus, Label, Post; public int TipDir; public bool Manual;
    /// <summary>Położenie zasadnicze na tor zwrotny (JSON "zas": "-") – wtedy (+) to odgałęzienie. Diverging – odgałęzienie wg geometrii (prędkość na bok).</summary>
    public bool BasicMinus; public string Diverging;
    /// <summary>Prędkość przy jeździe na bok (na ostrze i z ostrza) [km/h]; VProsto – na wprost (0 – bez ograniczenia).</summary>
    public double VBok = 40, VProsto;
    /// <summary>Typ rozjazdu (np. „Rz 60E1-190-1:9”), promień toru zwrotnego [m] i skos (np. „1:9”).</summary>
    public string Kind, Ratio; public double Radius;
    /// <summary>Typowe rozjazdy: typ, skos, promień [m], prędkość na bok [km/h] (Id-1 / Id-4 – wartości typowe; ostateczna z regulaminu).</summary>
    public static readonly (string Kind, string Ratio, double R, double V)[] Types =
    {
        // zwyczajne
        ("Rz 1:9 R190", "1:9", 190, 40), ("Rz 1:9 R300", "1:9", 300, 50), ("Rz 1:12 R500", "1:12", 500, 60),
        ("Rz 1:14 R760", "1:14", 760, 80), ("Rz 1:18,5 R1200", "1:18,5", 1200, 100), ("Rz 1:26,5 R2500", "1:26,5", 2500, 130),
        // łukowe (oba tory w łuku – na planie schematycznym rysowane jak zwyczajne) i symetryczne
        ("Rłj 1:9 R190 (łukowy jednostronny)", "1:9", 190, 40), ("Rłj 1:9 R300 (łukowy jednostronny)", "1:9", 300, 50), ("Rłj 1:12 R500 (łukowy jednostronny)", "1:12", 500, 60),
        ("Rłd 1:9 R190 (łukowy dwustronny)", "1:9", 190, 40), ("Rłd 1:9 R300 (łukowy dwustronny)", "1:9", 300, 50), ("Rłd 1:12 R500 (łukowy dwustronny)", "1:12", 500, 60),
        ("Rs 1:9 R300 (symetryczny)", "1:9", 300, 50),
        // krzyżowe i skrzyżowanie torów
        ("Rkp 1:9 R190 (krzyżowy podwójny)", "1:9", 190, 40), ("Rkj 1:9 R190 (krzyżowy pojedynczy)", "1:9", 190, 40),
        ("Skrz 1:9 (skrzyżowanie torów)", "1:9", 0, 0), ("Skrz 1:12 (skrzyżowanie torów)", "1:12", 0, 0),
        ("Rł (łukowy)", "", 0, 40)
    };
    /// <summary>Rodzaj rozjazdu z typu: "rkp" krzyżowy podwójny, "rkj" krzyżowy pojedynczy, "skrz" skrzyżowanie torów, "rz" pozostałe.</summary>
    public static string Class(string kind) => kind == null ? "rz" : kind.StartsWith("Rkp") ? "rkp" : kind.StartsWith("Rkj") ? "rkj" : kind.StartsWith("Skrz") ? "skrz" : "rz";
}

public class SignalDef
{
    public string Name, Node, Type, Szlak, Track, ApproachSec, Label, Repeats, Post;
    /// <summary>Kierunek obowiązywania: +1 – dla jazdy zgodnie z A→B toru, −1 – przeciwnie (orientacja torów – Orientation).</summary>
    public int Dir;
    /// <summary>Tor, na który wjeżdża pociąg mijający sygnalizator (przed nim w kierunku jazdy), i tor dojazdowy.</summary>
    public string Ahead, Approach;
    /// <summary>Zwrot symbolu na rysunku pulpitu / planu (±1 w poziomie wg geometrii) – tylko do rysowania.</summary>
    public int VDir;
    /// <summary>Strona toru na planie: „P” – prawa (domyślnie, względem kierunku jazdy), „L” – lewa.</summary>
    public string Side = "P";
    public bool IsTrain => Type is "wjazdowy" or "wyjazdowy";
    public bool ForTrains => Type is "wjazdowy" or "wyjazdowy" or "odstepowy";
    /// <summary>Semafor wyjazdowy bez funkcji tarczy manewrowej (nie podaje Ms2) – w JSON "shunt": false.</summary>
    public bool NoShunt;
    /// <summary>PK – punkt końcowy przebiegu (bez sygnalizatora): rodzaj przebiegów kończących się w nim – "P", "M" albo "PM".</summary>
    public string PkRoutes = "PM";
    /// <summary>Liczba komór (plan schematyczny, Ig-10 zał. 8 / Ie-4); 0 – domyślna dla typu (DefaultLamps).</summary>
    public int Lamps;
    public int LampCount => Lamps > 0 ? Lamps : Type switch { "wjazdowy" => 5, "wyjazdowy" => NoShunt ? 3 : 4, "odstepowy" => 3, _ => 1 };
    /// <summary>Konstrukcja na planie (Ig-10 9.8): "" maszt prosty, "wysiegnik", "nad" (wysięgnik nad torem), "bramka", "karzel"; Gantry – bramka.</summary>
    public string Mount = "", Gantry;
    public bool PkTrain => Type == "pk" && PkRoutes.Contains('P');
    public bool PkShunt => Type == "pk" && PkRoutes.Contains('M');
    /// <summary>Może rozpoczynać przebieg manewrowy (podaje Ms2).</summary>
    public bool ForShunts => Type is "wjazdowy" or "tm" || (Type == "wyjazdowy" && !NoShunt);
    /// <summary>Obowiązuje manewry (sygnał „Stój” zatrzymuje także skład manewrowy) – może kończyć przebieg manewrowy.</summary>
    public bool StopsShunts => ForShunts || Type == "wyjazdowy";
    /// <summary>Pasy świetlne semafora: „P” – pomarańczowy (60 km/h), „Z” – zielony (100 km/h); null – plik bez tej informacji (wszystkie prędkości jak dotąd).</summary>
    public string Stripes;
    /// <summary>Wskaźnik W21: „S” – świetlny (liczba z prędkości przebiegu), cyfra – tablica odblaskowa (dziesiątki km/h), null – brak.</summary>
    public string W21;

    /// <summary>
    /// Dobór sygnału do prędkości przebiegu v [km/h] (Ie-1 § 4: pasy świetlne, W21 – podwyższenie prędkości z sygnału 40 / 60 / 100):
    /// klasa sygnału (0 – największa dozwolona), liczba na W21 (0 – nie świeci / brak), prędkość sygnalizowana i uwaga, gdy semafor
    /// nie może zasygnalizować v (wtedy niższy sygnał – decyzja właściciela 2026-10-19). Plik bez pasów – klasa jak dotąd (w górę do 40 / 60 / 100).
    /// </summary>
    public (int Klasa, int W21, double V, string Uwaga) Dobor(double v)
    {
        if (Stripes == null) { int kl = v <= 0 ? 0 : v <= 40 ? 40 : v <= 60 ? 60 : v <= 100 ? 100 : 0; return (kl, 0, kl == 0 ? v : Math.Min(v, kl), null); }
        if (v <= 0) return (0, 0, 0, null);
        int k = v >= 100 && Stripes.Contains('Z') ? 100 : v >= 60 && Stripes.Contains('P') ? 60 : 40;
        if (v <= k) return (k, 0, v, null);
        if (W21 == "S") { int n = (int)(v / 10); return (k, n, n * 10, n * 10 < v ? $"W21 pokaże {n} – prędkość {n * 10} zamiast {v:0} km/h" : null); }
        if (int.TryParse(W21, out var t) && t * 10 > k)
            return (k, t, t * 10, t * 10 > v ? $"tablica W21 „{t}” zezwala na {t * 10} km/h, więcej niż prędkość przebiegu {v:0}" : t * 10 < v ? $"tablica W21 „{t}” – sygnalizowane {t * 10} zamiast {v:0} km/h" : null);
        if (int.TryParse(W21, out var t2)) return (k, 0, k, $"tablica W21 „{t2}” na semaforze {Label} nie podwyższa prędkości ponad {k} – sygnał {k} km/h zamiast {v:0}");
        var brak = v >= 100 && !Stripes.Contains('Z') ? "pasa zielonego ani W21" : v >= 60 && !Stripes.Contains('P') && k < 60 ? "pasa pomarańczowego ani W21" : "W21";
        return (k, 0, k, $"semafor {Label} nie ma {brak} – sygnał {k} km/h zamiast {v:0}");
    }
}

public class DerailerDef { public string Name, Node, Sec, Label, Post; }
/// <summary>Koniec szlaku przy posterunku; Label – oznaczenie szlaku na obrazie tego posterunku (szlak wewnętrzny: np. 1A przy Gałkówku, 1G przy Andrzejowie).</summary>
public class BlockEnd { public string Boundary, EntrySig, Post, Label; public int Outward; /// <summary>Zwrot „na zewnątrz” na rysunku (±1 w poziomie) – tylko do rysowania.</summary>
    public int VOut;
    /// <summary>Zobrazowanie: środek strzałki blokady narysowany w edytorze zobrazowania (null – za semaforem wjazdowym jak dotąd).</summary>
    public double? MarkX, MarkY; }

public class BlockDef
{
    public string Id, Type, Nbr, Far, Tor, Line, Label, NbrW, NbrE;
    /// <summary>Nazwa szlaku pełna (np. „Łódź Widzew – Łódź Andrzejów”) i skrócona (np. „ŁW – ŁA”) – z edytora UT.</summary>
    public string Name, NameShort;
    public bool Internal;
    public int DefDir;
    public List<BlockEnd> Ends = new();
    public List<string> SecsWE = new();
    /// <summary>Rodzaje urządzeń bezpieczeństwa ruchu na szlaku.</summary>
    public static readonly (string Code, string Name)[] Types =
    {
        ("sbl", "SBL dwukierunkowa – samoczynna blokada liniowa (wielo- lub jednoodstępowa), zmiana kierunku: Wbl / Pzk"),
        ("sbl1", "SBL jednokierunkowa – samoczynna blokada liniowa bez bloku pozwolenia, kierunek stały"),
        ("pol", "PBL dwukierunkowa – półsamoczynna blokada liniowa: blok pozwolenia, blok początkowy i końcowy"),
        ("pol1", "PBL jednokierunkowa – półsamoczynna blokada liniowa bez bloku pozwolenia: blok początkowy i końcowy"),
        ("tel", "brak blokady liniowej – telefoniczne zapowiadanie pociągów")
    };
    /// <summary>Stawność SBL (KOMPUTERY.pdf 1.6.1): 2 – S1/S2, 3 – S1/S5/S2, 4 – S1/S5/S4 (zielone migające)/S2.</summary>
    public int Aspects = 3;
    public bool OneWay => Type is "sbl1" or "pol1";
    public bool SemiAuto => Type is "pol" or "pol1";
    public bool NoBlock => Type == "tel";
    /// <summary>Kierunek zmieniany żądaniem i pozwoleniem (blok pozwolenia).</summary>
    public bool Permission => !OneWay && !NoBlock;
    public string TypeName => Types.FirstOrDefault(t => t.Code == Type).Name ?? Type;
    public string TypeShort => (Type switch { "sbl" => "SBL dwukierunkowa", "sbl1" => "SBL jednokierunkowa", "pol" => "PBL dwukierunkowa", "pol1" => "PBL jednokierunkowa", "tel" => "telefoniczne zapowiadanie", _ => Type })
        + (NoBlock ? "" : SecsWE.Count == 1 ? ", jednoodstępowa" : $", {SecsWE.Count} odstępy");

    /// <summary>Pierwszy odstęp w kierunku jazdy dir (od strony stacji wyprawiającej).</summary>
    public string FirstSec(int dir) => dir > 0 ? SecsWE[0] : SecsWE[^1];
    public string LastSec(int dir) => dir > 0 ? SecsWE[^1] : SecsWE[0];
}

/// <summary>
/// Odcinek kontroli niezajętości. Detection: „lo” – licznik osi (punkty liczące na granicach; po zakłóceniu zerowanie ZeroLO
/// i przejazd sprawdzający), „ot” – obwód torowy (odcinek izolowany złączami; zajętość przez zwarcie toków, po ustąpieniu
/// przyczyny wraca samoczynnie).
/// </summary>
public class SectionDef
{
    public string Id; public List<string> Edges = new(); public string Kind = "tor";
    public string Detection = "lo";
    /// <summary>Tor stacyjny z UT (JSON sections → "tor": true): okno numeru pociągu i numer toru jak dla odcinka „:JT” dawnych układów.</summary>
    public bool TorUt;
    public bool StationTrack => Id.Contains(":JT") || TorUt;
    public bool AxleCounter => Detection != "ot";
    public string DetectionName => AxleCounter ? "licznik osi" : "obwód torowy";
}
/// <summary>Peron: prostokąt (x, y – narożnik, w – wzdłuż, h – w poprzek); Rot [°] – obrót wokół (x, y), na planie wzdłuż skośnego toru.</summary>
public class PlatformDef
{
    public string Name; public double X, Y, W, H, Rot;
    /// <summary>Narożniki (po obrocie).</summary>
    public Point2[] Corners()
    {
        double r = Rot * Math.PI / 180, c = Math.Cos(r), s = Math.Sin(r);
        Point2 P(double u, double v) => new(X + u * c - v * s, Y + u * s + v * c);
        return new[] { P(0, 0), P(W, 0), P(W, H), P(0, H) };
    }
    public bool Contains(Point2 w)
    {
        double r = -Rot * Math.PI / 180, dx = w.X - X, dy = w.Y - Y, u = dx * Math.Cos(r) - dy * Math.Sin(r), v = dx * Math.Sin(r) + dy * Math.Cos(r);
        return u >= 0 && u <= W && v >= 0 && v <= H;
    }
    public Bounds2 Bounds() { var c = Corners(); return new Bounds2(new Point2(c.Min(q => q.X), c.Min(q => q.Y)), new Point2(c.Max(q => q.X), c.Max(q => q.Y))); }
}
public class LabelDef { public double X, Y; public string Text, Anchor; }
/// <summary>Przejazd kolejowo-drogowy: Kind „Y” albo „T” – oznaczenie na końcach linii.</summary>
public class CrossingDef { public string Name, Kind = "Y"; public double X, Y0, Y1; }
/// <summary>Bramka sygnałowa na planie schematycznym: pionowa belka w poprzek torów (x, od y0 do y1).</summary>
public class GantryDef { public string Id, Label, Post; public double X, Y0, Y1; }
public class DashedDef { public double X0, X1, Y; }
public class TrackEndDef { public string Id, Label, Disp; public bool Marker; }
/// <summary>Reguła tablicy przebiegów z edytora: wyłączenie przebiegu albo wskazanie przebiegu zasadniczego.</summary>
public class RouteRule { public string Start, End, Type, Sw; public bool Off, Basic; public double VMax; }
public class PageDef { public string Id, Name; public double X0, X1; public List<string> Posts = new(); }

public class Station
{
    /// <summary>Pełna ścieżka źródła ustawiana wyłącznie przez Load; Parse nie wiąże modelu z plikiem.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string SourcePath { get; private set; }
    public string Name, Code, Source;
    public Dictionary<string, Node> Nodes = new();
    public Dictionary<string, Edge> Edges = new();
    public Dictionary<string, SectionDef> Sections = new();
    public Dictionary<string, SwitchDef> Switches = new();
    public Dictionary<string, SignalDef> Signals = new();
    public Dictionary<string, DerailerDef> Derailers = new();
    public Dictionary<string, BlockDef> Blocks = new();
    public Dictionary<string, string> Boundaries = new();
    public Dictionary<string, (string Szlak, string Nbr)> FarEnds = new();
    public Dictionary<string, TrackEndDef> TrackEnds = new();
    /// <summary>Kozły oporowe – tylko symbol na końcu toru (bez funkcji końca przebiegu).</summary>
    public HashSet<string> Bumpers = new();
    /// <summary>Pary torów, przez które nie ma jazdy (w obie strony): rozjazd krzyżowy pojedynczy – brakujący przejazd (pole „noPass”).</summary>
    public List<(string A, string B)> NoPass = new();
    public Dictionary<string, string> Posts = new();
    public List<PageDef> Pages = new();
    public List<PlatformDef> Platforms = new();
    /// <summary>
    /// Wielkość znaków umownych Ig-10 na planie względem mm arkusza: 1 – skale Ig-10 (w poprzek 4× dokładniej niż wzdłuż, rozstaw 4,75 m ≈ 9,5 mm);
    /// skala rzeczywista (w obu kierunkach ta sama) – 0,25, żeby semafory, napisy i kliny miały tę samą proporcję do rozstawu torów co na arkuszu.
    /// </summary>
    public double SymbolScale = 1;
    public List<LabelDef> Labels = new();
    public List<CrossingDef> Crossings = new();
    public Dictionary<string, GantryDef> Gantries = new();
    public List<DashedDef> Dashed = new();
    /// <summary>Zobrazowanie: numery torów w miejscach z edytora zobrazowania (tekst, środek); tor bez wpisu – numer na środku odcinka jak dotąd.</summary>
    public List<LabelDef> TrackLabels = new();
    /// <summary>Zobrazowanie: opisy odcinków (odcinek, środek napisu) i kiedy je pokazywać: „nazadanie” (Ie-104 § 5 ust. 2), „zawsze”, „nie”.</summary>
    public List<(string Sec, double X, double Y)> SecLabels = new();
    public string SecLabelsMode = "nazadanie";
    public List<RouteRule> RouteRules = new();
    /// <summary>Zależności specjalne przebiegów z pliku &lt;układ&gt;.zaleznosci.json (projektuje człowiek).</summary>
    [System.Text.Json.Serialization.JsonIgnore] public Zaleznosci Zal = new();
    public double Width = 2000, Height = 1125;
    /// <summary>
    /// Zobrazowanie (obrazy pulpitu EbiScreen): ta sama topologia i identyfikatory, własne położenia węzłów i przebieg torów
    /// (sekcja „display” w station.json). Null – brak osobnego zobrazowania (pulpit rysuje rysunek układu).
    /// Elementy układu nieumieszczone na zobrazowaniu leżą poza obrazami (Hidden) i nie są rysowane.
    /// </summary>
    public Station Display;
    /// <summary>Zobrazowanie: symbole (sygnalizatory, wykolejnice, kozły – identyfikatory) ukryte na pulpicie, choć ich węzeł leży na torze obrazu.</summary>
    public HashSet<string> HiddenSyms = new();
    /// <summary>Zobrazowanie: widok każdej strony z własną geometrią (obiekty narysowane na kilku obrazach); brak – geometria główna.</summary>
    public Dictionary<string, Station> PageViews = new();
    /// <summary>
    /// Zobrazowanie: posterunki z PIP – wyświetlaczami numerów pociągów (Ie-104 § 8 pkt 26). Pole „pip” w sekcji „display”:
    /// true / brak – wszystkie, false – żaden, lista kodów posterunków – tylko te. Null – wszystkie.
    /// Bez PIP pulpit pokazuje symbol odcinka i numer toru, bez numerów pociągów.
    /// </summary>
    public HashSet<string> PipPosts;
    public bool Pip(string sec) => PipPosts == null || PipPosts.Contains(PostOf(sec));
    /// <summary>Współrzędna elementu nieumieszczonego na zobrazowaniu.</summary>
    public const double Hidden = -1e7;
    /// <summary>Geometria dla pulpitu: zobrazowanie, jeśli istnieje, inaczej rysunek układu.</summary>
    public Station View => Display ?? this;

    public string Other(string eid, string nid) { var e = Edges[eid]; return e.A == nid ? e.B : e.A; }

    /// <summary>Kierunek toru od węzła (jednostkowy, z punktów toru).</summary>
    public (double X, double Y) Tangent(string eid, string nid)
    {
        var e = Edges[eid]; var p = e.A == nid ? e.Pts : Enumerable.Reverse(e.Pts).ToList();
        for (int i = 1; i < p.Count; i++)
        {
            double dx = p[i].X - p[0].X, dy = p[i].Y - p[0].Y, l = Math.Sqrt(dx * dx + dy * dy);
            if (l > 1e-6) return (dx / l, dy / l);
        }
        return (1, 0);
    }

    /// <summary>
    /// Skrzyżowanie torów (węzeł bez zwrotnicy z więcej niż dwoma torami – krzyż St / Rkp, Ie-104 § 8 pkt 2): z toru <paramref name="from"/>
    /// dalej tylko tor na jego przedłużeniu (najbardziej przeciwny kierunek od węzła).
    /// </summary>
    public string Straight(string nid, string from, IEnumerable<string> cands)
    {
        var (fx, fy) = Tangent(from, nid);
        return cands.OrderBy(e => { var (x, y) = Tangent(e, nid); return x * fx + y * fy; }).FirstOrDefault();
    }
    /// <summary>Węzły ze sprzeczną orientacją torów (trójkąt torowy, pętla) – do kontroli układu.</summary>
    public List<string> OrientConflicts = new();
    /// <summary>Geometria pulpitu: kierunek w poziomie z rysunku (zobrazowanie). Układ torowy – kierunek wg orientacji A→B.</summary>
    public bool GeoDir;
    /// <summary>Kierunek jazdy torem eid od węzła from: +1 – zgodnie z A→B, −1 – przeciwnie (zobrazowanie: w prawo / w lewo).</summary>
    public int EdgeDir(string eid, string from)
    {
        var e = Edges[eid];
        if (!GeoDir) return e.A == from ? 1 : -1;
        return GeoX(eid, from);
    }
    /// <summary>Kierunek w poziomie na rysunku (±1, 0 – pionowo bez pola dir).</summary>
    public int GeoX(string eid, string from)
    {
        var e = Edges[eid]; var dx = Nodes[Other(eid, from)].X - Nodes[from].X;
        if (Math.Abs(dx) < 0.5) return e.Dir == 0 ? 0 : (e.A == from ? e.Dir : -e.Dir);
        return Math.Sign(dx);
    }
    public PageDef PageOfX(double x) => Pages.FirstOrDefault(p => x >= p.X0 && x < p.X1) ?? Pages[0];
    public PageDef PageOfNode(string nid) => PageOfX(Nodes[nid].X);
    /// <summary>Nazwa wyświetlana – klucz bez przedrostka posterunku.</summary>
    public static string Lbl(string key) { if (key == null) return ""; var i = key.IndexOf(':'); return i >= 0 ? key[(i + 1)..] : key; }
    public static string PostOf(string key) { if (key == null) return ""; var i = key.IndexOf(':'); return i >= 0 ? key[..i] : ""; }

    static string S(JsonElement e, string n, string def = null) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : def;

    public static Station Load(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var station = Parse(File.ReadAllText(fullPath));
        station.SourcePath = fullPath;
        station.Zal = Zaleznosci.Wczytaj(fullPath);
        return station;
    }

    public static Station Parse(string json)
    {
        // orientacja torów (A→B spójnie; stary plik – jednorazowo wg dawnego „na wschód”), w pamięci
        var root = System.Text.Json.Nodes.JsonNode.Parse(json) as System.Text.Json.Nodes.JsonObject;
        var orient = root != null ? TrackOrientation.Apply(root) : new TrackOrientation.Result(0, new());
        var st = ParseCore(root?.ToJsonString() ?? json, false);
        st.OrientConflicts = orient.Conflicts;
        if (root?["display"] is System.Text.Json.Nodes.JsonObject disp)
        {
            root.Remove("display");
            // geometria każdej strony (edytor zobrazowania, plan § 10 pkt 5): obiekt narysowany na dwóch obrazach jest na pulpicie na obu
            var pageGeo = disp["pageGeo"] as System.Text.Json.Nodes.JsonObject;
            disp.Remove("pageGeo");
            var baseRoot = (System.Text.Json.Nodes.JsonObject)root.DeepClone();
            var d = BuildDisplay((System.Text.Json.Nodes.JsonObject)baseRoot.DeepClone(), disp, st);
            if (pageGeo != null)
                foreach (var (pid, pg) in pageGeo)
                    if (pg is System.Text.Json.Nodes.JsonObject pgo)
                    {
                        if (pgo["pip"] == null && disp["pip"] != null) pgo["pip"] = disp["pip"].DeepClone();
                        if (pgo["hiddenSyms"] == null && disp["hiddenSyms"] != null) pgo["hiddenSyms"] = disp["hiddenSyms"].DeepClone();
                        d.PageViews[pid] = BuildDisplay((System.Text.Json.Nodes.JsonObject)baseRoot.DeepClone(), pgo, st);
                    }
            st.Display = d;
        }
        st.Visual(st.Display ?? st);
        if (st.Display != null)
        {
            st.Display.Visual(st.Display, st);
            foreach (var pv in st.Display.PageViews.Values) pv.Visual(pv, st);
        }
        return st;
    }

    /// <summary>
    /// Widok zobrazowania z sekcji „display” (albo z geometrii jednej strony – „pageGeo”): położenia węzłów i przebieg torów, napisy,
    /// perony, przejazdy, elementy z edytora zobrazowania; topologia i zwrotnice z układu <paramref name="st"/>.
    /// </summary>
    static Station BuildDisplay(System.Text.Json.Nodes.JsonObject root, System.Text.Json.Nodes.JsonObject disp, Station st)
    {
        // geometria zobrazowania: węzły i tory z sekcji „display”, pozostałe – poza obrazami
        var dn = disp["nodes"] as System.Text.Json.Nodes.JsonObject; var de = disp["edges"] as System.Text.Json.Nodes.JsonObject;
        // napisy, perony i przejazdy pulpitu – własna kopia zobrazowania (brak – te same co na planie)
        foreach (var k in new[] { "labels", "platforms", "crossings" }) if (disp[k] is System.Text.Json.Nodes.JsonArray da) root[k] = da.DeepClone();
        foreach (var (id, n) in (System.Text.Json.Nodes.JsonObject)root["nodes"])
        {
            var p = dn?[id] as System.Text.Json.Nodes.JsonArray;
            n["x"] = p != null ? p[0].GetValue<double>() : Hidden; n["y"] = p != null ? p[1].GetValue<double>() : Hidden;
        }
        foreach (var e in ((System.Text.Json.Nodes.JsonArray)root["edges"]).OfType<System.Text.Json.Nodes.JsonObject>())
        {
            var p = de?[(string)e["id"]] as System.Text.Json.Nodes.JsonArray;
            e["pts"] = p != null && p.Count >= 2 ? p.DeepClone() : new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonArray(Hidden, Hidden), new System.Text.Json.Nodes.JsonArray(Hidden + 1, Hidden));
        }
        var d = ParseCore(root.ToJsonString(), true);
        if (disp["hiddenSyms"] is System.Text.Json.Nodes.JsonArray hs) foreach (var h in hs) if (h?.GetValue<string>() is string hid) d.HiddenSyms.Add(hid);
        // elementy z edytora zobrazowania (etap Z4): numery torów, opisy odcinków, strzałki blokady, odcinki bez kontroli niezajętości
        if (disp["trackLabels"] is System.Text.Json.Nodes.JsonArray tl)
            foreach (var x in tl.OfType<System.Text.Json.Nodes.JsonObject>())
                d.TrackLabels.Add(new LabelDef { Text = (string)x["text"], X = x["x"].GetValue<double>(), Y = x["y"].GetValue<double>(), Anchor = "middle" });
        if (disp["secLabels"] is System.Text.Json.Nodes.JsonArray sl)
            foreach (var x in sl.OfType<System.Text.Json.Nodes.JsonObject>())
                if ((string)x["sec"] is string sec) d.SecLabels.Add((sec, x["x"].GetValue<double>(), x["y"].GetValue<double>()));
        if (disp["secLabelsMode"]?.GetValue<string>() is string slm) d.SecLabelsMode = slm;
        if (disp["dashedEdges"] is System.Text.Json.Nodes.JsonArray dashE)
            foreach (var x in dashE) if (x?.GetValue<string>() is string eid && d.Edges.TryGetValue(eid, out var ed)) ed.NoDetect = true;
        if (disp["blockMarks"] is System.Text.Json.Nodes.JsonArray bm)
            foreach (var x in bm.OfType<System.Text.Json.Nodes.JsonObject>())
                if ((string)x["block"] is string bid && d.Blocks.TryGetValue(bid, out var db) && x["end"]?.GetValue<int>() is int ei && ei >= 0 && ei < db.Ends.Count)
                { db.Ends[ei].MarkX = x["x"].GetValue<double>(); db.Ends[ei].MarkY = x["y"].GetValue<double>(); }
        if (disp["pip"] is System.Text.Json.Nodes.JsonArray pa) d.PipPosts = new(pa.Select(x => x?.GetValue<string>()).Where(x => x != null));
        else if (disp["pip"] is System.Text.Json.Nodes.JsonValue pv && pv.TryGetValue<bool>(out var pb) && !pb) d.PipPosts = new();
        // położenia zwrotnic (+/−) i iglica – z układu torowego (zobrazowanie nie zmienia zależności)
        foreach (var (k, sw) in st.Switches)
        {
            if (!d.Switches.TryGetValue(k, out var ds)) { d.Switches[k] = ds = new SwitchDef { Num = sw.Num, Node = sw.Node, Sec = sw.Sec, Label = sw.Label, Post = sw.Post, Manual = sw.Manual, VBok = sw.VBok, VProsto = sw.VProsto, TipDir = sw.TipDir }; d.Nodes[sw.Node].Switch = k; }
            ds.Tip = sw.Tip; ds.Plus = sw.Plus; ds.Minus = sw.Minus; ds.Diverging = sw.Diverging; ds.BasicMinus = sw.BasicMinus;
        }
        return d;
    }


    static Station ParseCore(string json, bool geo)
    {
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;
        var st = new Station { Name = S(r, "name"), Code = S(r, "code"), Source = S(r, "source"), GeoDir = geo };
        if (r.TryGetProperty("width", out var ww)) { st.Width = ww.GetDouble(); st.Height = r.GetProperty("height").GetDouble(); }
        if (r.TryGetProperty("posts", out var ps)) foreach (var p in ps.EnumerateObject()) st.Posts[p.Name] = p.Value.GetString();
        if (r.TryGetProperty("pages", out var pg))
            foreach (var p in pg.EnumerateArray())
            {
                var pd = new PageDef { Id = S(p, "id"), Name = S(p, "name"), X0 = p.GetProperty("x0").GetDouble(), X1 = p.GetProperty("x1").GetDouble() };
                foreach (var x in p.GetProperty("posts").EnumerateArray()) pd.Posts.Add(x.GetString());
                st.Pages.Add(pd);
            }
        foreach (var p in r.GetProperty("nodes").EnumerateObject())
            st.Nodes[p.Name] = new Node { Id = p.Name, X = p.Value.GetProperty("x").GetDouble(), Y = p.Value.GetProperty("y").GetDouble(), Kind = S(p.Value, "kind", ""), Ref = S(p.Value, "ref", ""), Post = S(p.Value, "post", "") };
        foreach (var e in r.GetProperty("edges").EnumerateArray())
        {
            var ed = new Edge
            {
                Id = S(e, "id"), A = S(e, "a"), B = S(e, "b"), Sec = S(e, "sec"), Len = Math.Max(5, e.GetProperty("len").GetDouble()),
                Track = S(e, "track", ""), Line = S(e, "line", ""), Szlak = e.TryGetProperty("szlak", out var sz) && sz.ValueKind == JsonValueKind.True,
                Link = e.TryGetProperty("link", out var lk) && lk.ValueKind == JsonValueKind.True,
                Dismantled = e.TryGetProperty("dismantled", out var dm) && dm.ValueKind == JsonValueKind.True,
                VMax = e.TryGetProperty("vmax", out var evm) && evm.ValueKind == JsonValueKind.Number ? evm.GetDouble() : 0,
                Radius = e.TryGetProperty("radius", out var erd) && erd.ValueKind == JsonValueKind.Number ? erd.GetDouble() : 0,
                Cant = e.TryGetProperty("cant", out var ect) && ect.ValueKind == JsonValueKind.Number ? ect.GetDouble() : 0,
                Embank = e.TryGetProperty("nasyp", out var ens) && ens.ValueKind == JsonValueKind.Number ? ens.GetDouble() : 0,
                Grad = e.TryGetProperty("poch", out var egr) && egr.ValueKind == JsonValueKind.Number ? egr.GetDouble() : double.NaN,
                Dir = e.TryGetProperty("dir", out var edir) && edir.ValueKind == JsonValueKind.Number ? Math.Sign(edir.GetInt32()) : 0,
                Cat = S(e, "cat", "b"), PeronUt = e.TryGetProperty("peron", out var epr) && epr.ValueKind == JsonValueKind.True,
                Elec = e.TryGetProperty("elec", out var el) && (el.ValueKind == JsonValueKind.True || (el.ValueKind == JsonValueKind.String && el.GetString() == "true"))
            };
            foreach (var p in e.GetProperty("pts").EnumerateArray()) ed.Pts.Add((p[0].GetDouble(), p[1].GetDouble()));
            st.Edges[ed.Id] = ed;
            st.Nodes[ed.A].Edges.Add(ed.Id); st.Nodes[ed.B].Edges.Add(ed.Id);
            if (!st.Sections.TryGetValue(ed.Sec, out var sec)) st.Sections[ed.Sec] = sec = new SectionDef { Id = ed.Sec };
            sec.Edges.Add(ed.Id);
        }
        // rodzaj kontroli niezajętości: domyślny dla obszaru i ustawienia odcinków
        var defDet = r.TryGetProperty("detection", out var dd) && dd.ValueKind == JsonValueKind.String ? dd.GetString() : "lo";
        foreach (var sd in st.Sections.Values) sd.Detection = defDet;
        if (r.TryGetProperty("sections", out var sm) && sm.ValueKind == JsonValueKind.Object)
            foreach (var p in sm.EnumerateObject())
                if (st.Sections.TryGetValue(p.Name, out var sd))
                {
                    if (p.Value.TryGetProperty("detection", out var dv) && dv.ValueKind == JsonValueKind.String) sd.Detection = dv.GetString();
                    if (p.Value.TryGetProperty("tor", out var tv) && tv.ValueKind == JsonValueKind.True) sd.TorUt = true;
                }
        foreach (var p in r.GetProperty("switches").EnumerateObject())
        {
            var v = p.Value;
            var s = new SwitchDef { Num = p.Name, Node = S(v, "node"), Sec = S(v, "sec"), Label = S(v, "label", Lbl(p.Name)), Post = S(v, "post", PostOf(p.Name)), Manual = v.TryGetProperty("manual", out var mn) && mn.GetBoolean(),
                Kind = S(v, "kind"), Ratio = S(v, "ratio"), Radius = v.TryGetProperty("radius", out var rr0) && rr0.ValueKind == JsonValueKind.Number ? rr0.GetDouble() : 0,
                // prędkość na bok: wpisana, inaczej z typu rozjazdu, inaczej 40 km/h
                VBok = v.TryGetProperty("vbok", out var vb) && vb.ValueKind == JsonValueKind.Number ? vb.GetDouble()
                     : SwitchDef.Types.FirstOrDefault(t => t.Kind == S(v, "kind")).V is double tv && tv > 0 ? tv : 40,
                VProsto = v.TryGetProperty("vprosto", out var vp) && vp.ValueKind == JsonValueKind.Number ? vp.GetDouble() : 0,
                BasicMinus = S(v, "zas") == "-" };
            st.Switches[s.Num] = s; st.Nodes[s.Node].Switch = s.Num;
        }
        foreach (var p in r.GetProperty("signals").EnumerateObject())
        {
            var v = p.Value;
            var s = new SignalDef
            {
                Name = p.Name, Node = S(v, "node"), Dir = v.GetProperty("dir").GetInt32(), Type = S(v, "type"), Szlak = S(v, "szlak"), Track = S(v, "track"),
                Label = S(v, "label", Lbl(p.Name)), Repeats = S(v, "repeats"), Post = S(v, "post", PostOf(p.Name)),
                PkRoutes = S(v, "routes", "PM"), Mount = S(v, "mount", ""), Gantry = S(v, "gantry"), Side = S(v, "side", "P"), Stripes = S(v, "stripes"), W21 = S(v, "w21"),
                Lamps = v.TryGetProperty("lamps", out var lm) && lm.ValueKind == JsonValueKind.Number ? lm.GetInt32() : v.TryGetProperty("lamps", out var lm2) && lm2.ValueKind == JsonValueKind.String && int.TryParse(lm2.GetString(), out var lmi) ? lmi : 0,
                NoShunt = v.TryGetProperty("shunt", out var sh) && (sh.ValueKind == JsonValueKind.False || (sh.ValueKind == JsonValueKind.String && sh.GetString() == "false"))
            };
            st.Signals[s.Name] = s; st.Nodes[s.Node].Signals.Add(s.Name);
        }
        foreach (var p in r.GetProperty("derailers").EnumerateObject())
            st.Derailers[p.Name] = new DerailerDef { Name = p.Name, Node = S(p.Value, "node"), Sec = S(p.Value, "sec"), Label = S(p.Value, "label", Lbl(p.Name)), Post = S(p.Value, "post", PostOf(p.Name)) };
        foreach (var p in r.GetProperty("blocks").EnumerateObject())
        {
            var v = p.Value;
            var b = new BlockDef
            {
                Id = p.Name, Type = S(v, "type"), Nbr = S(v, "nbr"), Far = S(v, "far"), Tor = S(v, "tor"), Line = S(v, "line"), Label = S(v, "label", p.Name),
                Internal = v.TryGetProperty("internal", out var it) && it.GetBoolean(), DefDir = v.GetProperty("defDir").GetInt32(), NbrW = S(v, "nbrW"), NbrE = S(v, "nbrE")
            };
            if (v.TryGetProperty("aspects", out var asp) && asp.ValueKind == JsonValueKind.Number) b.Aspects = Math.Clamp(asp.GetInt32(), 2, 4);
            foreach (var en in v.GetProperty("ends").EnumerateArray())
                b.Ends.Add(new BlockEnd { Boundary = S(en, "boundary"), EntrySig = S(en, "entrySig"), Post = S(en, "post"), Label = S(en, "label"), Outward = en.GetProperty("outward").GetInt32() });
            foreach (var s in v.GetProperty("secsWE").EnumerateArray()) b.SecsWE.Add(s.GetString());
            if (b.Type is null or "blok") b.Type = "sbl";                                       // dawny zapis: SBL jednoodstępowa
            if (b.OneWay && b.DefDir == 0 && b.Ends.Count > 0) b.DefDir = b.Ends[0].Outward;   // jednokierunkowa – kierunek stały
            st.Blocks[b.Id] = b;
            foreach (var s in b.SecsWE) if (st.Sections.TryGetValue(s, out var sd)) sd.Kind = "szlak";
        }
        foreach (var p in r.GetProperty("boundaries").EnumerateObject()) st.Boundaries[p.Name] = p.Value.GetProperty("szlak").GetString();
        foreach (var p in r.GetProperty("farEnds").EnumerateObject()) st.FarEnds[p.Name] = (p.Value.GetProperty("szlak").GetString(), p.Value.GetProperty("nbr").GetString());
        if (r.TryGetProperty("noPass", out var np) && np.ValueKind == JsonValueKind.Array)
            foreach (var p in np.EnumerateArray()) if (p.GetArrayLength() == 2) st.NoPass.Add((p[0].GetString(), p[1].GetString()));
        if (r.TryGetProperty("bumpers", out var bmp) && bmp.ValueKind == JsonValueKind.Object)
            foreach (var p in bmp.EnumerateObject()) if (st.Nodes.ContainsKey(p.Name)) st.Bumpers.Add(p.Name);
        foreach (var p in r.GetProperty("trackEnds").EnumerateObject())
            st.TrackEnds[p.Name] = new TrackEndDef { Id = S(p.Value, "id"), Label = S(p.Value, "label", ""), Disp = S(p.Value, "disp", Lbl(S(p.Value, "id"))), Marker = p.Value.TryGetProperty("marker", out var mk) && mk.GetBoolean() };
        if (r.TryGetProperty("planScale", out var psc) && psc.ValueKind == JsonValueKind.Object && psc.TryGetProperty("along", out var pal) && psc.TryGetProperty("across", out var pac) && pal.ValueKind == JsonValueKind.Number && pac.ValueKind == JsonValueKind.Number && pac.GetDouble() > 0)
            st.SymbolScale = Math.Clamp(pal.GetDouble() / pac.GetDouble() / 4, 0.25, 1);
        foreach (var p in r.GetProperty("platforms").EnumerateArray())
            st.Platforms.Add(new PlatformDef { Name = S(p, "name"), X = p.GetProperty("x").GetDouble(), Y = p.GetProperty("y").GetDouble(), W = p.GetProperty("w").GetDouble(), H = p.GetProperty("h").GetDouble(), Rot = p.TryGetProperty("rot", out var prt) && prt.ValueKind == JsonValueKind.Number ? prt.GetDouble() : 0 });
        foreach (var p in r.GetProperty("labels").EnumerateArray())
            st.Labels.Add(new LabelDef { X = p.GetProperty("x").GetDouble(), Y = p.GetProperty("y").GetDouble(), Text = S(p, "text"), Anchor = S(p, "anchor", "start") });
        if (r.TryGetProperty("crossings", out var cr))
            foreach (var p in cr.EnumerateArray()) st.Crossings.Add(new CrossingDef { Name = S(p, "name"), Kind = S(p, "kind", "Y"), X = p.GetProperty("x").GetDouble(), Y0 = p.GetProperty("y0").GetDouble(), Y1 = p.GetProperty("y1").GetDouble() });
        if (r.TryGetProperty("gantries", out var gn) && gn.ValueKind == JsonValueKind.Object)
            foreach (var p in gn.EnumerateObject())
                st.Gantries[p.Name] = new GantryDef { Id = p.Name, Label = S(p.Value, "label", Lbl(p.Name)), Post = S(p.Value, "post", PostOf(p.Name)), X = p.Value.GetProperty("x").GetDouble(), Y0 = p.Value.GetProperty("y0").GetDouble(), Y1 = p.Value.GetProperty("y1").GetDouble() };
        if (r.TryGetProperty("dashed", out var da))
            foreach (var p in da.EnumerateArray()) st.Dashed.Add(new DashedDef { X0 = p.GetProperty("x0").GetDouble(), X1 = p.GetProperty("x1").GetDouble(), Y = p.GetProperty("y").GetDouble() });
        if (r.TryGetProperty("routeRules", out var rr))
            foreach (var p in rr.EnumerateArray())
                st.RouteRules.Add(new RouteRule { Start = S(p, "start"), End = S(p, "end"), Type = S(p, "type"), Sw = S(p, "sw", ""), Off = p.TryGetProperty("off", out var of) && of.GetBoolean(), Basic = p.TryGetProperty("basic", out var bs) && bs.GetBoolean(),
                    VMax = p.TryGetProperty("vmax", out var rv) && rv.ValueKind == JsonValueKind.Number ? rv.GetDouble() : 0 });
        if (st.Pages.Count == 0) st.Pages.Add(new PageDef { Id = "ALL", Name = st.Name, X0 = -1e9, X1 = 1e9 });
        st.Sanitize();
        st.Finalize();
        return st;
    }

    /// <summary>Usuwa niespójności (np. po edycji): odstępy bez torów, szlaki bez odstępów lub granic, odwołania do usuniętych szlaków.</summary>
    void Sanitize()
    {
        foreach (var b in Blocks.Values.ToList())
        {
            b.SecsWE.RemoveAll(x => !Sections.ContainsKey(x));
            if (b.SecsWE.Count == 0 || b.Ends.Any(e => e.Boundary == null || !Nodes.ContainsKey(e.Boundary)) || (!b.Internal && (b.Far == null || !Nodes.ContainsKey(b.Far))))
                Blocks.Remove(b.Id);
        }
        foreach (var sd in Sections.Values) if (sd.Kind == "szlak" && !Blocks.Values.Any(b => b.SecsWE.Contains(sd.Id))) sd.Kind = "tor";
        foreach (var k in Boundaries.Where(x => !Blocks.ContainsKey(x.Value) || !Nodes.ContainsKey(x.Key)).Select(x => x.Key).ToList()) Boundaries.Remove(k);
        foreach (var k in FarEnds.Where(x => !Blocks.ContainsKey(x.Value.Szlak) || !Nodes.ContainsKey(x.Key)).Select(x => x.Key).ToList()) FarEnds.Remove(k);
        foreach (var s in Signals.Values.ToList())
        {
            if (s.Szlak != null && !Blocks.ContainsKey(s.Szlak)) { if (s.Type == "odstepowy") { Signals.Remove(s.Name); Nodes[s.Node].Signals.Remove(s.Name); continue; } s.Szlak = null; }
            if (s.Repeats != null && !Signals.ContainsKey(s.Repeats) && !Blocks.ContainsKey(s.Repeats)) s.Repeats = null;
        }
        foreach (var e in Edges.Values) if (e.Szlak && !Blocks.Values.Any(b => b.SecsWE.Contains(e.Sec))) e.Szlak = false;
        foreach (var d in Derailers.Values) if (d.Sec == null || !Sections.ContainsKey(d.Sec)) d.Sec = Nodes[d.Node].Edges.Select(x => Edges[x].Sec).FirstOrDefault();
        foreach (var w in Switches.Values) if (w.Sec == null || !Sections.ContainsKey(w.Sec)) w.Sec = Nodes[w.Node].Edges.Select(x => Edges[x].Sec).FirstOrDefault();
        foreach (var k in TrackEnds.Keys.Where(k => !Nodes.ContainsKey(k)).ToList()) TrackEnds.Remove(k);
    }

    void Finalize()
    {
        foreach (var sw in Switches.Values.ToList())
        {
            var n = Nodes[sw.Node];
            if (n.Edges.Count != 3) { Switches.Remove(sw.Num); n.Switch = null; continue; }
            var side = n.Edges.Select(e => EdgeDir(e, n.Id)).ToList();
            int tipSide = side.Count(s => s > 0) == 1 ? 1 : -1;
            int tipIdx = side.IndexOf(tipSide);
            if (tipIdx < 0) { Switches.Remove(sw.Num); n.Switch = null; continue; }
            sw.Tip = n.Edges[tipIdx]; sw.TipDir = tipSide;
            var legs = n.Edges.Where((_, i) => i != tipIdx).OrderBy(e => LegDev(e, n, sw.Tip)).ToList();
            sw.Diverging = legs[1];
            // (+) = położenie zasadnicze: zwykle tor prosty; „zas”: „-” – zasadniczo na zwrot
            if (sw.BasicMinus) { sw.Plus = legs[1]; sw.Minus = legs[0]; } else { sw.Plus = legs[0]; sw.Minus = legs[1]; }
        }
        // ograniczenia zwrotnicowe: odgałęzienie (−) i łącznik za nim do następnej zwrotnicy – VBok; iglica i (+) – VProsto
        foreach (var sw in Switches.Values)
        {
            void Lim(string eid, double v) { if (v <= 0) return; var e = Edges[eid]; e.SwLimit = e.SwLimit > 0 ? Math.Min(e.SwLimit, v) : v; }
            var straight = sw.Plus == sw.Diverging ? sw.Minus : sw.Plus;
            if (sw.VProsto > 0) { Lim(sw.Tip, sw.VProsto); Lim(straight, sw.VProsto); }
            string cur = sw.Node, eid = sw.Diverging;
            for (int k = 0; k < 30 && eid != null; k++)
            {
                var e = Edges[eid]; Lim(eid, sw.VBok);
                var o = Other(eid, cur); var on = Nodes[o];
                if (on.Switch != null || on.Edges.Count != 2 || on.Signals.Count > 0) break;
                var next = on.Edges.First(x => x != eid); var ne = Edges[next];
                if (ne.Pts.All(p => Math.Abs(p.Y - ne.Pts[0].Y) < 0.5) && Math.Abs(Nodes[ne.A].Y - Nodes[ne.B].Y) < 0.5) break;   // dalej tor poziomy – koniec łącznika
                cur = o; eid = next;
            }
        }
        foreach (var s in Signals.Values)
        {
            var back = Nodes[s.Node].Edges.Where(e => EdgeDir(e, s.Node) == -s.Dir).ToList();
            s.ApproachSec = back.Count == 1 ? Edges[back[0]].Sec : null;
            s.Approach = back.Count == 1 ? back[0] : null;
            s.Ahead = Nodes[s.Node].Edges.FirstOrDefault(e => EdgeDir(e, s.Node) == s.Dir);
        }
        foreach (var e in Edges.Values) e.Platform = e.Track != "" && (e.Sec.Contains(":JT") || e.PeronUt);
    }

    /// <summary>
    /// Odchylenie odgałęzienia od przedłużenia iglicy [rad] – większe ma odgałęzienie (−). Liczone od kierunku toru za iglicą,
    /// więc działa także na torach skośnych (plan rysowany po obróconym zdjęciu); przy iglicy poziomej – jak nachylenie.
    /// </summary>
    /// <summary>
    /// Zwroty symboli do rysowania z geometrii geo (pulpit: zobrazowanie albo rysunek układu): sygnalizator – w stronę toru przed nim,
    /// koniec szlaku – w stronę toru szlakowego. logic – model z kierunkami (dla zobrazowania: układ torowy).
    /// </summary>
    void Visual(Station geo, Station logic = null)
    {
        logic ??= this;
        foreach (var s in Signals.Values)
        {
            var ls = logic.Signals.GetValueOrDefault(s.Name) ?? s;
            var ah = logic.Nodes.ContainsKey(ls.Node) ? logic.Nodes[ls.Node].Edges.FirstOrDefault(e => logic.EdgeDir(e, ls.Node) == ls.Dir) : null;
            var bk = logic.Nodes.ContainsKey(ls.Node) ? logic.Nodes[ls.Node].Edges.FirstOrDefault(e => logic.EdgeDir(e, ls.Node) == -ls.Dir) : null;
            int v = ah != null && geo.Edges.ContainsKey(ah) && geo.Nodes.ContainsKey(s.Node) ? geo.GeoX(ah, s.Node) : 0;
            if (v == 0 && bk != null && geo.Edges.ContainsKey(bk) && geo.Nodes.ContainsKey(s.Node)) v = -geo.GeoX(bk, s.Node);
            s.VDir = v != 0 ? v : (s.Dir != 0 ? s.Dir : 1);
        }
        foreach (var b in Blocks.Values)
        {
            var lb = logic.Blocks.GetValueOrDefault(b.Id) ?? b;
            for (int i = 0; i < b.Ends.Count; i++)
            {
                var le = i < lb.Ends.Count ? lb.Ends[i] : b.Ends[i]; var n = le.Boundary;
                var oe = logic.Nodes.ContainsKey(n) ? logic.Nodes[n].Edges.FirstOrDefault(e => logic.EdgeDir(e, n) == le.Outward) : null;
                int v = oe != null && geo.Edges.ContainsKey(oe) && geo.Nodes.ContainsKey(n) ? geo.GeoX(oe, n) : 0;
                b.Ends[i].VOut = v != 0 ? v : b.Ends[i].Outward;
            }
        }
    }

    public double LegDev(string eid, Node n, string tip)
    {
        var t = Edges[tip]; var q = t.A == n.Id ? t.Pts[1] : t.Pts[^2];
        double tx = n.X - q.X, ty = n.Y - q.Y, tl = Math.Sqrt(tx * tx + ty * ty);
        if (tl < 1e-9) return LegSlope(eid, n);
        var e = Edges[eid]; var p = e.A == n.Id ? e.Pts[1] : e.Pts[^2];
        double vx = p.X - n.X, vy = p.Y - n.Y, vl = Math.Sqrt(vx * vx + vy * vy);
        if (vl < 1e-9) return 0;
        return Math.Acos(Math.Clamp((tx * vx + ty * vy) / (tl * vl), -1, 1));
    }
    public double LegSlope(string eid, Node n)
    {
        var e = Edges[eid];
        var p = e.A == n.Id ? e.Pts[1] : e.Pts[^2];
        var dx = Math.Abs(p.X - n.X); var dy = Math.Abs(p.Y - n.Y);
        return dx < 0.01 ? 99 : dy / dx;
    }
}
