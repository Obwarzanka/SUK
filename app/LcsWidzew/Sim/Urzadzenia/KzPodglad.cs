using LcsWidzew.Model;

namespace LcsWidzew.Sim;

/// <summary>Wpis dziennika komunikacji komputera zależności z kartami: rozkaz (KZ → karta) albo zmiana statusu (karta → KZ).</summary>
public class KomWpis { public long Id; public double T; public string Czas, Kierunek, Rodzaj, Obiekt, Tresc; }

public partial class Urzadzenia
{
    /// <summary>Dziennik komunikacji KZ ↔ karty (ostatnie wpisy) – podgląd w programie Komputerownia.</summary>
    public readonly List<KomWpis> Dziennik = new();
    long komSeq;
    readonly Dictionary<string, string> ostStatus = new();

    void Wpis(string kier, string rodzaj, string id, string tresc)
    {
        Dziennik.Add(new KomWpis { Id = ++komSeq, T = S.T, Czas = S.Clock(), Kierunek = kier, Rodzaj = rodzaj, Obiekt = id, Tresc = tresc });
        if (Dziennik.Count > 2000) Dziennik.RemoveRange(0, 500);
    }

    /// <summary>Rozkaz KZ do karty – do dziennika.</summary>
    public void LogRozkaz(string rodzaj, string id, string tresc) => Wpis("KZ → karta", rodzaj, id, tresc);

    /// <summary>Status karty do KZ – do dziennika tylko przy zmianie (pierwszy status po uruchomieniu – bez wpisu).</summary>
    public void LogStatus(string rodzaj, string id, string tresc)
    {
        var k = rodzaj + "|" + id;
        if (ostStatus.TryGetValue(k, out var o) && o != tresc) Wpis("karta → KZ", rodzaj, id, tresc);
        ostStatus[k] = tresc;
    }
}

public class PrzebiegWiersz { public string Id, Typ, Stan, Sygnal, Zwrotnice, Odcinki, Zwolnione, Ochronna, Boczna, Czas, V; }
public class UtwWiersz { public string Rodzaj, Obiekt, Utwierdzenie, Stan; }
public class KontrolaWpis { public string Poziom, Tekst; }
public class PolecenieWpis { public long Id; public string Czas, Kategoria, Tresc, Operator; }

/// <summary>Stan komputera zależności dla programu Komputerownia (zakładka „Komputer zależności”).</summary>
public class KzStan
{
    public string Zegar; public double T;
    public List<PrzebiegWiersz> Przebiegi = new();
    public List<UtwWiersz> Utwierdzenia = new();
    public List<KontrolaWpis> Kontrola = new();
    public int Sprawdzono;
    public List<KomWpis> Komunikacja = new();
    public List<PolecenieWpis> Polecenia = new();
}

/// <summary>Sprawdzenie przebiegu z tablicy: przeszkody, sygnał, prędkość.</summary>
public class PrzebiegTest { public string Id, Typ, Zwrotnice, Odcinki, Boczna, V, Sygnal, Stan; public List<string> Przeszkody = new(); }

/// <summary>
/// Podgląd komputera zależności: przebiegi, utwierdzenia, komunikacja z kartami, polecenia z pulpitu i kontrola zależności na żywo –
/// sprawdzenie niezmienników (sygnał zezwalający tylko przy utwierdzonym przebiegu z kontrolą zwrotnic i wolnymi odcinkami, jeden przebieg
/// na odcinek, sygnał wg Ie-1 zgodny z następnym semaforem, karta wyświetla zlecony sygnał).
/// </summary>
public static class KzPodglad
{
    static string L(string k) => Station.Lbl(k ?? "");

    public static KzStan Stan(Simulator s, long odKom, long odPol)
    {
        var res = new KzStan { Zegar = s.Clock(), T = s.T };
        string RN(string uid) => uid != null && s.Routes.TryGetValue(uid, out var r) ? r.Id : uid;
        foreach (var r in s.Routes.Values.OrderBy(r => r.T0))
            res.Przebiegi.Add(new PrzebiegWiersz
            {
                Id = r.Id, Typ = r.Type == "P" ? "pociągowy" : "manewrowy",
                Stan = r.State switch { "setting" => "nastawianie", "set" => "utwierdzony, sygnał zezwalający", "passed" => "przejechany – zwalnianie", "stopped" => "„Stój” – utwierdzony", _ => r.State } + (r.TimeRelUntil != null ? ", zwalnianie czasowe" : ""),
                Sygnal = s.Sigs[r.Start].Sig ?? "", Zwrotnice = r.SwText, Odcinki = string.Join(" ", r.Secs.Skip(r.Rel).Select(L)), Zwolnione = $"{r.Rel}/{r.Secs.Count}",
                Ochronna = string.Join(" ", r.OvSecs.Select(L).Concat(r.OvSw.Select(q => L(q.Num) + q.Pos))), Boczna = r.FlankText, Czas = $"{s.T - r.T0:0} s", V = $"{r.VMax:0}" + (s.SignalSpeed(r) > 0 ? $" (sygnał {s.SignalSpeed(r):0})" : ""),
            });
        foreach (var x in s.Secs.Values.Where(x => x.Lock != null || x.Closed).OrderBy(x => x.Id))
            res.Utwierdzenia.Add(new UtwWiersz { Rodzaj = "odcinek", Obiekt = L(x.Id), Utwierdzenie = x.Lock == null ? "" : (x.LockType switch { "train" => "pociągowy", "shunt" => "manewrowy", "overlap" => "droga ochronna", _ => x.LockType }) + " – " + RN(x.Lock), Stan = string.Join(", ", new[] { s.SecOcc(x.Id) ? "zajęty" : "wolny", x.Closed ? "zamknięty ruchowo" : null }.Where(v => v != null)) });
        foreach (var w in s.Sws.Values.Where(w => w.Lock != null || w.Flank.Count > 0 || w.Ov.Count > 0 || w.Stopped).OrderBy(w => w.Num))
            res.Utwierdzenia.Add(new UtwWiersz { Rodzaj = "zwrotnica", Obiekt = L(w.Num), Utwierdzenie = string.Join("; ", new[] { w.Lock != null ? "w przebiegu " + RN(w.Lock) : null, w.Flank.Count > 0 ? "ochrona boczna " + string.Join(", ", w.Flank.Select(RN)) : null, w.Ov.Count > 0 ? "droga ochronna " + string.Join(", ", w.Ov.Select(RN)) : null, w.Stopped ? "zastopowana" : null }.Where(v => v != null)), Stan = (s.SwControlled(w.Num) ? $"kontrola „{w.Pos}”" : w.Moving != null ? $"przekładanie → „{w.Moving.Target}”" : "brak kontroli") });
        foreach (var d in s.Ders.Values.Where(d => d.Lock != null || d.Flank.Count > 0).OrderBy(d => d.Name))
            res.Utwierdzenia.Add(new UtwWiersz { Rodzaj = "wykolejnica", Obiekt = L(d.Name), Utwierdzenie = string.Join("; ", new[] { d.Lock != null ? "w przebiegu " + RN(d.Lock) : null, d.Flank.Count > 0 ? "ochrona boczna " + string.Join(", ", d.Flank.Select(RN)) : null }.Where(v => v != null)), Stan = d.MovingTo != null ? "przekładanie" : d.On ? "założona" : "zdjęta" });
        foreach (var x in s.Sigs.Values.Where(x => x.Flank.Count > 0 || x.Closed).OrderBy(x => x.Name))
            res.Utwierdzenia.Add(new UtwWiersz { Rodzaj = "sygnalizator", Obiekt = L(x.Name), Utwierdzenie = string.Join("; ", new[] { x.Flank.Count > 0 ? "„Stój” w ochronie bocznej " + string.Join(", ", x.Flank.Select(RN)) : null, x.Closed ? "zastopowany" : null }.Where(v => v != null)), Stan = x.Sig ?? x.Aspect.ToString() });
        res.Kontrola = Kontrola(s, out res.Sprawdzono);
        res.Komunikacja = s.Urzadzenia.Dziennik.Where(k => k.Id > odKom).TakeLast(400).ToList();
        res.Polecenia = s.Events.Where(e => e.Id > odPol && e.Kategoria is "Polecenie" or "Odrzucone").TakeLast(300)
            .Select(e => new PolecenieWpis { Id = e.Id, Czas = e.Czas, Kategoria = e.Kategoria, Tresc = e.Zdarzenie, Operator = e.Operator }).ToList();
        return res;
    }

    /// <summary>Kontrola zależności: naruszenia (błąd komputera zależności) i uwagi (stany przejściowe / do sprawdzenia).</summary>
    public static List<KontrolaWpis> Kontrola(Simulator s, out int sprawdzono)
    {
        var l = new List<KontrolaWpis>(); int n = 0;
        void Nar(string t) => l.Add(new KontrolaWpis { Poziom = "naruszenie", Tekst = t });
        void Uw(string t) => l.Add(new KontrolaWpis { Poziom = "uwaga", Tekst = t });
        var aktywne = s.Routes.Values.ToList();
        // 1. przebieg z sygnałem zezwalającym: zwrotnice z kontrolą w położeniu i utwierdzone, odcinki utwierdzone (pociągowy – wolne), wykolejnice zdjęte
        foreach (var r in aktywne.Where(r => r.State == "set" && !r.Passed))
        {
            foreach (var (num, pos) in r.Sw)
            {
                n++; var w = s.Sws[num];
                if (w.Pos != pos || !s.SwControlled(num)) Nar($"{r.Id}: sygnał zezwalający, a zwr. {L(num)} bez kontroli w położeniu „{pos}”");
                if (w.Lock != r.Uid) Nar($"{r.Id}: zwr. {L(num)} nieutwierdzona w przebiegu");
            }
            foreach (var sc in r.Secs.Skip(r.Rel))
            {
                n++;
                if (s.Secs[sc].Lock != r.Uid && !s.Secs[sc].Shared.Contains(r.Uid)) Nar($"{r.Id}: odcinek {L(sc)} nieutwierdzony w przebiegu");
                if (r.Type == "P" && s.SecOcc(sc)) Nar($"{r.Id}: sygnał zezwalający przy zajętym odcinku {L(sc)}");
            }
            foreach (var d in r.Ders) { n++; if (s.Ders[d].On || s.Ders[d].MovingTo != null) Nar($"{r.Id}: sygnał zezwalający przy założonej wykolejnicy {L(d)}"); }
            foreach (var (num, pos) in r.OvSw)
            { n++; if (s.Sws[num].Pos != pos || !s.SwControlled(num) || !s.Sws[num].Ov.Contains(r.Uid)) Nar($"{r.Id}: droga ochronna – zwr. {L(num)} bez kontroli / utwierdzenia w położeniu „{pos}”"); }
            foreach (var (kind, id, fpos, forSw) in r.Flank.Where(f => f.Kind == "sw"))
            { n++; if ((forSw == null || s.Sws[forSw].Lock == r.Uid) && (s.Sws[id].Pos != fpos || !s.SwControlled(id))) Nar($"{r.Id}: ochrona boczna – zwr. {L(id)} bez kontroli w położeniu „{fpos}”"); }
        }
        // 2. sygnał zezwalający pociągowy / manewrowy tylko przy nastawionym przebiegu (odstępowe – z blokady)
        foreach (var (name, x) in s.Sigs)
        {
            var d = s.L.Signals[name]; if (d.Type == "odstepowy") continue;
            n++;
            if (x.Aspect is Aspect.Train or Aspect.Shunt && (x.Route == null || !s.Routes.TryGetValue(x.Route, out var r) || r.State != "set"))
                Nar($"semafor {L(name)}: sygnał zezwalający bez nastawionego przebiegu");
        }
        // 3. odcinek nie może należeć do dwóch przebiegów (części niezwolnione)
        foreach (var g in aktywne.SelectMany(r => r.Secs.Skip(r.Rel).Select(sc => (sc, r))).GroupBy(x => x.sc).Where(g => g.Count() > 1 && !g.All(a => g.All(b => a.r == b.r || KomputerZaleznosci.Dopuszczone(a.r, b.r)))))
        { n++; Nar($"odcinek {L(g.Key)} w dwóch przebiegach: {string.Join(", ", g.Select(x => x.r.Id))}"); }
        // 4. utwierdzenie bez przebiegu
        foreach (var x in s.Secs.Values.Where(x => x.Lock != null)) { n++; if (!s.Routes.ContainsKey(x.Lock)) Nar($"odcinek {L(x.Id)} utwierdzony w nieistniejącym przebiegu"); }
        foreach (var w in s.Sws.Values.Where(w => w.Lock != null)) { n++; if (!s.Routes.ContainsKey(w.Lock)) Nar($"zwr. {L(w.Num)} utwierdzona w nieistniejącym przebiegu"); }
        // 5. sygnał wg Ie-1: prędkość przebiegu × sygnał semafora końca przebiegu
        foreach (var r in aktywne.Where(r => r.State == "set" && !r.Passed && r.Type == "P" && r.EndKind == "sig" && s.Urzadzenia.Sygnalizatory.ContainsKey(r.Start)))
        {
            n++;
            var nast = s.Urzadzenia.Sygnalizatory.ContainsKey(r.End) ? s.Sigs[r.End].Sig : null;
            var ocz = Sygnaly.Dobierz(Sygnaly.Klasa(s.SignalSpeed(r)), Sygnaly.Kolumna(nast));
            if (s.Sigs[r.Start].Sig != ocz) Nar($"{r.Id}: sygnał {s.Sigs[r.Start].Sig}, a wg Ie-1 (prędkość {s.SignalSpeed(r):0}, następny {L(r.End)} {nast ?? "„Stój”"}) – {ocz}");
        }
        // 6. karta wyświetla sygnał zlecony przez KZ (bez usterki, rozkaz starszy niż 0,5 s)
        foreach (var (name, k) in s.Urzadzenia.Sygnalizatory)
        {
            n++;
            if (k.UszkZezwalajacego || k.BrakLacznosci || k.OstRozkazT is not double t || s.T - t < 0.5) continue;
            if (k.Wyswietlany != (k.OstRozkaz ?? k.Stoj)) Nar($"sygnalizator {L(name)}: KZ zlecił {k.OstRozkaz}, karta wyświetla {k.Wyswietlany}");
        }
        // 7. zwrotnica przekładana przy zajętym odcinku (dozwolone tylko PlusBZ / MinBZ – do sprawdzenia)
        foreach (var w in s.Sws.Values.Where(w => w.Moving != null)) { n++; if (s.SecOcc(s.L.Switches[w.Num].Sec)) Uw($"zwr. {L(w.Num)} przekładana przy zajętym odcinku {L(s.L.Switches[w.Num].Sec)} (dozwolone tylko PlusBZ / MinBZ)"); }
        // 8. przebieg długo w nastawianiu
        foreach (var r in aktywne.Where(r => r.State == "setting" && s.T - r.T0 > 20)) { n++; Uw($"{r.Id}: nastawianie trwa {s.T - r.T0:0} s – {string.Join("; ", s.SignalConditions(r).Take(3).DefaultIfEmpty("warunki spełnione – sygnał w następnym cyklu"))}"); }
        sprawdzono = n;
        return l;
    }

    /// <summary>Przebiegi z tablicy zawierające tekst (np. „A” albo „P102 1A”): stan, przeszkody (dlaczego nie można nastawić), prędkość i sygnał.</summary>
    public static List<PrzebiegTest> Sprawdz(Simulator s, string tekst)
    {
        var q = (tekst ?? "").Trim(); var res = new List<PrzebiegTest>();
        if (q == "") return res;
        var parts = q.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var r in s.RouteTable.Values.SelectMany(v => v).Where(r => parts.All(p => r.Id.Contains(p, StringComparison.OrdinalIgnoreCase)))
                     .OrderBy(r => s.Routes.Values.Any(x => x.Id == r.Id) ? 0 : 1).ThenBy(r => r.Type == "P" ? 0 : 1).ThenBy(r => r.Variant).Take(30))
        {
            var act = s.Routes.Values.FirstOrDefault(x => x.Id == r.Id);
            var t = new PrzebiegTest
            {
                Id = r.Id, Typ = r.Type == "P" ? "pociągowy" : "manewrowy", Zwrotnice = r.SwText + (r.Ders.Count > 0 ? " " + string.Join(" ", r.Ders.Select(L)) : ""),
                Odcinki = string.Join(" ", r.Secs.Select(L)), Boczna = r.FlankText,
                V = $"{r.VMax:0}" + (r.Type == "P" ? $", sygnalizowana {(s.SignalSpeed(r) > 0 ? s.SignalSpeed(r).ToString("0") : "Vmax")}" : ""),
                Sygnal = r.Type == "M" ? "Ms2" : r.EndKind == "sig" ? $"{Sygnaly.Dobierz(Sygnaly.Klasa(s.SignalSpeed(r)), "Stoj")} … {Sygnaly.Dobierz(Sygnaly.Klasa(s.SignalSpeed(r)), "Vmax")} (zależnie od {L(r.End)})" : Sygnaly.Dobierz(Sygnaly.Klasa(s.SignalSpeed(r)), "Vmax"),
                Stan = act != null ? "aktywny: " + act.State : "",
            };
            if (act == null) t.Przeszkody = s.WhyNot(r);
            else if (act.State == "setting") t.Stan += " – czeka na: " + string.Join("; ", s.SignalConditions(act).DefaultIfEmpty("sygnał w następnym cyklu"));
            res.Add(t);
        }
        return res;
    }
}
