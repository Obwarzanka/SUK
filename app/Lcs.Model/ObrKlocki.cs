using System.Text.Json;

namespace Lcs.Model;

/// <summary>
/// Klocki (PLAN_EDYTOR_ZOBRAZOWANIA_V2.md § 5 sposób 2, etap E7): gotowe kawałki rysunku – łącznik „[”, łącznik ukośny, rozjazd krzyżowy, koniec toru –
/// i własne klocki człowieka (zapisany fragment rysunku, bez przypięć). Klocek wstawiony jest zwykłym fragmentem grafu: jego końce zaczepiają się
/// o węzły i tory w tym miejscu (tor dzieli się – rozjazd powstaje sam). Punkt zaczepienia klocka – (0, 0). Bez WPF.
/// </summary>
public static class ObrKlocki
{
    public const string Lacznik = "klocekLacznik", Ukos = "klocekUkos", Krzyzowy = "klocekKrzyzowy", KoniecToru = "klocekKoniecToru";
    public static readonly (string Typ, string Nazwa, string Opis)[] Wbudowane =
    {
        (Lacznik, "łącznik „[”", "Łącznik „[”: ukos – pion – ukos z toru w punkcie kliknięcia do następnego rzędu (prowadnicy) – rozjazdy powstają same; spacja – odbicie (w lewo / w górę)"),
        (Ukos, "łącznik ukośny", "Łącznik ukośny 45° z toru w punkcie kliknięcia do następnego rzędu – rozjazdy powstają same; spacja – odbicie"),
        (Krzyzowy, "rozjazd krzyżowy", "Rozjazd krzyżowy: tor ukośny przez tor w punkcie kliknięcia – węzeł rozjazdu krzyżowego; spacja – odbicie"),
        (KoniecToru, "koniec toru", "Koniec toru: krótki tor z kozłem od punktu kliknięcia (końca toru); spacja – w drugą stronę"),
    };
    public static string Nazwa(string typ) => Wbudowane.FirstOrDefault(w => w.Typ == typ).Nazwa ?? typ;

    static Fragment Z(params int[][] pts)
    {
        var f = new Fragment();
        foreach (var p in pts) f.Wezly.Add(new ObrWezel { P = p });
        for (int i = 1; i < f.Wezly.Count; i++) f.Odcinki.Add(new ObrOdcinek { A = f.Wezly[i - 1].Id, B = f.Wezly[i].Id });
        return f;
    }
    static int[] P(int x, int y) => new[] { x, y };
    static ObrGraf.Fragment F(Fragment f) => new() { Wezly = f.Wezly, Odcinki = f.Odcinki, Elementy = f.Elementy };
    sealed class Fragment { public List<ObrWezel> Wezly = new(); public List<ObrOdcinek> Odcinki = new(); public List<ObrElement> Elementy = new(); }

    /// <summary>
    /// Klocek wbudowany: <paramref name="h"/> – odległość do następnego rzędu w dół [j.] (łączniki), wariant 0–3 – odbicie w poziomie (1, 3) i w pionie (2, 3).
    /// </summary>
    public static ObrGraf.Fragment Klocek(string typ, int h, int wariant)
    {
        h = Math.Max(16, h);
        Fragment f;
        switch (typ)
        {
            case Lacznik:
                f = h >= 64 ? Z(P(0, 0), P(24, 24), P(24, h - 24), P(48, h)) : Z(P(0, 0), P(h, h));
                break;
            case Ukos: f = Z(P(0, 0), P(h, h)); break;
            case Krzyzowy:
                {
                    // tor na wprost przez punkt i ukos przez ten sam punkt – węzeł z czterema torami i elementem rozjazdu krzyżowego
                    f = Z(P(-48, 0), P(0, 0), P(48, 0));
                    var a = new ObrWezel { P = P(-32, -32) }; var b = new ObrWezel { P = P(32, 32) };
                    f.Wezly.Add(a); f.Wezly.Add(b);
                    f.Odcinki.Add(new ObrOdcinek { A = a.Id, B = f.Wezly[1].Id }); f.Odcinki.Add(new ObrOdcinek { A = f.Wezly[1].Id, B = b.Id });
                    f.Elementy.Add(new ObrElement { Typ = ObrGraf.RozjazdKrzyzowy, Na = f.Wezly[1].Id });
                    break;
                }
            case KoniecToru:
                f = Z(P(0, 0), P(48, 0));
                f.Elementy.Add(new ObrElement { Typ = ObrSymbole.Koziol, Na = f.Wezly[1].Id });
                break;
            default: return new ObrGraf.Fragment();
        }
        return Odbij(F(f), wariant);
    }

    /// <summary>Fragment odbity: wariant 1 – w poziomie, 2 – w pionie, 3 – oba (względem punktu zaczepienia (0, 0)).</summary>
    public static ObrGraf.Fragment Odbij(ObrGraf.Fragment f, int wariant)
    {
        var k = Kopia(f, false);
        int sx = (wariant & 1) != 0 ? -1 : 1, sy = (wariant & 2) != 0 ? -1 : 1;
        foreach (var w in k.Wezly) w.P = new[] { w.P[0] * sx, w.P[1] * sy };
        foreach (var e in k.Elementy.Where(e => e.P != null)) e.P = new[] { e.P[0] * sx, e.P[1] * sy };
        return k;
    }

    /// <summary>Głęboka kopia fragmentu; <paramref name="noweId"/> – nowe id węzłów, odcinków i elementów (przypięcia za nimi).</summary>
    public static ObrGraf.Fragment Kopia(ObrGraf.Fragment f, bool noweId)
    {
        var k = JsonSerializer.Deserialize<ObrGraf.Fragment>(JsonSerializer.Serialize(f, UtDocument.Json), UtDocument.Json);
        k.Przypiecia ??= new();
        if (!noweId) return k;
        var map = new Dictionary<string, string>();
        foreach (var w in k.Wezly) { var n = ObrId.Nowy(); map[w.Id] = n; w.Id = n; }
        foreach (var e in k.Odcinki) { var n = ObrId.Nowy(); map[e.Id] = n; e.Id = n; e.A = map[e.A]; e.B = map[e.B]; }
        foreach (var el in k.Elementy) { var n = ObrId.Nowy(); map[el.Id] = n; el.Id = n; if (el.Na != null && map.TryGetValue(el.Na, out var na)) el.Na = na; }
        foreach (var p in k.Przypiecia) if (map.TryGetValue(p.El, out var ne)) p.El = ne;
        return k;
    }

    /// <summary>
    /// Wstawienie fragmentu przesuniętego o (dx, dy): węzeł w miejscu istniejącego węzła – ten węzeł, na linii istniejącego toru – tor dzieli się
    /// (rozjazd powstaje sam), inaczej nowy węzeł; odcinki pokrywające istniejące tory – scalone. Elementy i przypięcia idą za węzłami / odcinkami.
    /// Zwraca węzły i odcinki wstawione (po położeniu).
    /// </summary>
    public static (HashSet<string> Wezly, HashSet<string> Odcinki) Wstaw(ObrObraz o, ObrGraf.Fragment f, int dx, int dy)
    {
        var k = Kopia(f, true);
        foreach (var w in k.Wezly) w.P = new[] { w.P[0] + dx, w.P[1] + dy };
        foreach (var e in k.Elementy.Where(e => e.P != null)) e.P = new[] { e.P[0] + dx, e.P[1] + dy };
        var map = new Dictionary<string, string>();
        var dodane = new List<ObrWezel>();
        foreach (var w in k.Wezly)
        {
            var ist = o.Wezly.FirstOrDefault(x => x.P[0] == w.P[0] && x.P[1] == w.P[1]);
            if (ist == null && o.Odcinki.FirstOrDefault(e => { var (a, b) = ObrGraf.Konce(o, e); return ObrGraf.NaLinii(a, b, w.P); }) is ObrOdcinek pod)
                ist = ObrGraf.Podziel(o, pod, w.P);
            if (ist != null) { map[w.Id] = ist.Id; if (w.Poza != null) (ist.Poza ??= new()).AddRange(w.Poza); }
            else { o.Wezly.Add(w); dodane.Add(w); map[w.Id] = w.Id; }
        }
        foreach (var e in k.Odcinki) { e.A = map[e.A]; e.B = map[e.B]; if (e.A != e.B) o.Odcinki.Add(e); }
        foreach (var el in k.Elementy) { if (el.Na != null && map.TryGetValue(el.Na, out var na)) el.Na = na; o.Elementy.Add(el); }
        foreach (var p in k.Przypiecia)
        {
            if (map.TryGetValue(p.El, out var ne)) p.El = ne;
            o.Przypiecia.RemoveAll(x => x.El == p.El && x.Stan != ObrPrzypiecie.Potwierdzone);
            if (!o.Przypiecia.Any(x => x.El == p.El)) o.Przypiecia.Add(p);
        }
        var pos = k.Wezly.Select(w => w.P).ToList();
        ObrGraf.Upros(o);
        var wz = o.Wezly.Where(w => pos.Any(p => p[0] == w.P[0] && p[1] == w.P[1])).Select(w => w.Id).ToHashSet();
        return (wz, o.Odcinki.Where(e => wz.Contains(e.A) && wz.Contains(e.B)).Select(e => e.Id).ToHashSet());
    }

    /// <summary>Własny klocek – nazwany fragment rysunku (bez przypięć), wstawiany w punkcie swojego pierwszego węzła.</summary>
    public class Wlasny
    {
        public string Nazwa { get; set; } = "";
        public ObrGraf.Fragment Fragment { get; set; } = new();
    }

    /// <summary>Własny klocek z zaznaczenia: kopia bez przypięć, punkt zaczepienia – lewy górny węzeł (przesunięty do (0, 0)).</summary>
    public static Wlasny ZZaznaczenia(ObrObraz o, IEnumerable<string> wezly, IEnumerable<string> odcinki, string nazwa)
    {
        var f = ObrGraf.Kopiuj(o, wezly, odcinki);
        f.Przypiecia = new();
        if (f.Wezly.Count == 0) return null;
        var z = f.Wezly.OrderBy(w => w.P[1]).ThenBy(w => w.P[0]).First().P.ToArray();
        foreach (var w in f.Wezly) { w.P = new[] { w.P[0] - z[0], w.P[1] - z[1] }; w.Poza = null; }
        foreach (var e in f.Elementy.Where(e => e.P != null)) e.P = new[] { e.P[0] - z[0], e.P[1] - z[1] };
        return new Wlasny { Nazwa = nazwa, Fragment = f };
    }

    public static List<Wlasny> Wczytaj(string path)
    {
        try { return File.Exists(path) ? JsonSerializer.Deserialize<List<Wlasny>>(File.ReadAllText(path), UtDocument.Json) ?? new() : new(); }
        catch { return new(); }
    }

    public static void Zapisz(string path, List<Wlasny> lista)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(lista, UtDocument.Json));
    }
}
