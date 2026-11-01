using System.Text.Json;
using Lcs.RozkladJazdy;

namespace LcsWidzew.Sim;

/// <summary>
/// Rozkład jazdy z pliku <c>&lt;układ&gt;.rj.json</c> (PLAN_EDYTOR_RJ.md, etap R1): import z dzisiejszych źródeł (Data/rozklad.json z GTFS,
/// rozklad_ic.json, <c>&lt;układ&gt;.rozklad.json</c>) i wczytanie do kolejki pociągów. Wczytanie odtwarza dokładnie to, co budował
/// <see cref="RealTimetable"/> – reguły obszaru (zmiana czoła, zaplecze, zjazd luzem) są w danych pociągu, nie w kodzie.
/// </summary>
public partial class Rozklad
{
    // ------------------------------------------------------------------ import z rozkładu rzeczywistego (rozklad.json / rozklad_ic.json)
    /// <summary>
    /// Rozkład dobowy z pliku importu GTFS / PKP IC (czasy doby). Reguły obszaru Widzewa z <see cref="RealTimetable"/> zamieniają się na dane:
    /// zmiana czoła i numeru w punktach biegu, odcinek próżny, start z zaplecza, odstawienie, zjazd luzem lokomotywy.
    /// Przykładowe jednostki ŁKA (gdy jest tylko rozklad_ic.json) nie są importowane – nie ma ich w źródle.
    /// </summary>
    public static RjDokument ImportRozkladu(Simulator S, string path)
    {
        bool gtfs = Path.GetFileName(path) == "rozklad.json";
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var d = new RjDokument
        {
            Dzien = doc.RootElement.TryGetProperty("date", out var dt) ? dt.GetString() : null,
            Zrodlo = (doc.RootElement.TryGetProperty("source", out var src) ? src.GetString() : Path.GetFileName(path)) + " (import do .rj.json)",
            ToryZaplecza = new() { new() { Odcinek = "LW:JT102", Tor = "102" }, new() { Odcinek = "LW:JT104", Tor = "104" } }
        };
        string Sx(JsonElement e, string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        static string PostShort(string p) => p switch { "LW" => "ŁW", "LF" => "ŁF", "LN" => "ŁN", _ => p };
        string Hm(double tod) { var s = (long)tod; return $"{s / 3600 % 24:00}:{s / 60 % 60:00}"; }
        var przew = new SortedSet<string>();
        int nr = 0;
        foreach (var e in doc.RootElement.GetProperty("trains").EnumerateArray())
        {
            double appear = e.GetProperty("appear").GetDouble();
            string num = Sx(e, "num"), cat = Sx(e, "cat") ?? "IC", veh = Sx(e, "veh") ?? "", from = Sx(e, "from"), to = Sx(e, "to");
            bool unit = e.GetProperty("unit").GetBoolean(), emptyIn = e.GetProperty("emptyIn").GetBoolean(), emptyOut = e.GetProperty("emptyOut").GetBoolean();
            double len = e.GetProperty("len").GetDouble();
            var stops = e.GetProperty("stops").EnumerateArray().Select(s => (Post: Sx(s, "post"), Arr: s.GetProperty("arr").GetDouble(), Dep: s.GetProperty("dep").GetDouble(),
                Stop: s.GetProperty("stop").GetBoolean(), Track: Sx(s, "track"), Pl: Sx(s, "platform"))).ToList();
            if (stops.Count == 0) continue;
            if (to == null && !stops[^1].Stop) stops[^1] = stops[^1] with { Stop = true };
            var revIdx = e.TryGetProperty("revIdx", out var ri0) ? ri0.EnumerateArray().Select(x => x.GetInt32()).ToHashSet() : null;
            var renumIdx = e.TryGetProperty("renumIdx", out var rn0) ? rn0.EnumerateArray().Select(x => x.GetInt32()).ToHashSet() : new HashSet<int>();
            var numsAt = e.TryGetProperty("numsAt", out var na) ? na.EnumerateObject().ToDictionary(x => int.Parse(x.Name), x => x.Value.GetString()) : new();
            // odcinki biegu = odcinki między zmianami czoła / numeru (jak legs w RealTimetable)
            var konce = new List<int>();
            for (int i = 0; i < stops.Count; i++)
            {
                bool rev = revIdx != null ? revIdx.Contains(i) || renumIdx.Contains(i) : stops[i].Post == "LF" && (i < stops.Count - 1 || to != null);
                if (rev && i < stops.Count - 1) konce.Add(i);
            }
            string carrier = Sx(e, "carrier") ?? cat, kat = Sx(e, "kat");
            przew.Add(carrier);
            var p = new RjPociag
            {
                Id = $"{num}#{++nr}", Nazwa = Sx(e, "name"), Pojazd = veh, Dlugosc = len, RelacjaOd = Sx(e, "origin"), RelacjaDo = Sx(e, "destination"),
                Pojawia = RjDokument.Czas(appear), SzlakZ = from, SzlakDo = to,
                Odcinki = { new RjOdcinek { Numer = num, Kat = kat, KatRuchu = emptyIn ? "PRÓŻ" : cat, Przewoznik = carrier } }
            };
            string curNum = num, curKat = p.Odcinki[0].KatRuchu;
            for (int i = 0; i < stops.Count; i++)
            {
                var s = stops[i];
                var b = new RjPunkt { Post = s.Post, Przyjazd = RjDokument.Czas(s.Arr), Odjazd = RjDokument.Czas(s.Dep), Postoj = s.Stop, Tor = s.Track, Peron = s.Pl, Odcinek = p.Odcinki.Count - 1 };
                if (konce.Contains(i))
                {
                    string nowaKat = s.Post == "LF" ? (emptyIn ? cat : emptyOut ? "PRÓŻ" : null) : null;
                    curNum = numsAt.GetValueOrDefault(i, curNum); curKat = nowaKat ?? curKat;
                    p.Odcinki.Add(new RjOdcinek { Numer = curNum, Kat = kat, KatRuchu = curKat, Przewoznik = carrier });
                    b.Odcinek = p.Odcinki.Count - 1; b.ZmianaCzola = !renumIdx.Contains(i); b.KatRuchu = nowaKat;
                }
                p.Bieg.Add(b);
            }
            bool endBaza = e.TryGetProperty("endBaza", out var eb) && eb.GetBoolean(), startBaza = e.TryGetProperty("startBaza", out var sb) && sb.GetBoolean();
            if (to == null && gtfs && stops[^1].Post == "LF") p.Koniec = new RjKoniec { ZmianaCzolaW = "LF", Po = 300, KatRuchu = "PRÓŻ", Post = "LW", NrManewrowy = curNum + "M" };
            else if (to == null) p.Koniec = new RjKoniec { Post = stops[^1].Post, NrManewrowy = endBaza ? curNum + "M" : null };
            var plan = string.Join(" → ", stops.Where(s => s.Stop).Select(s => $"{PostShort(s.Post)} {(s.Track != null ? "t" + s.Track : "")}{(s.Pl != null ? " p" + s.Pl : "")} {Hm(s.Arr)}/{Hm(s.Dep)}"));
            p.Opis = $"{Sx(e, "name")} ({veh}{(emptyIn ? ", próżny z Olechowa" : "")}): {plan}{(emptyOut ? " → próżny do Olechowa" : "")}";
            var f = stops[0];
            if (from == null && startBaza)
            {
                int ddir = 0;
                if (to != null && S.L.Blocks.TryGetValue(to, out var tb)) ddir = tb.Ends.FirstOrDefault(x => x.Post == "LW")?.Outward ?? 0;
                if (ddir == 0 && stops.Count > 1)
                {
                    double Ax(string post) { var ns = S.L.Nodes.Values.Where(n => n.Post == post).ToList(); return ns.Count > 0 ? ns.Average(n => n.X) : 0; }
                    ddir = Math.Sign(Ax(stops[1].Post) - Ax("LW"));
                }
                p.Odjazd = RjDokument.Czas(f.Dep);
                p.Start = new RjStart { ZZaplecza = true, Kierunek = -1, Lok = true, Pojawia = RjDokument.Czas(f.Dep - 1200), NrJednostki = num + "M", KierunekOdjazdu = ddir, TorOdjazdu = f.Track,
                    OpisZaplecza = $"z bazy ŁKA (tor {{tor}}) na ŁW t{f.Track} p{f.Pl}, odjazd jako {num}: " };
            }
            else if (from == null)
            {
                p.Odjazd = RjDokument.Czas(f.Dep);
                p.Start = new RjStart { Odcinek = f.Post == "LF" ? "LF:JT" + (f.Track ?? "7") : "LW:JT6", Kierunek = f.Post == "LF" ? 1 : stops.Count > 1 && stops[1].Post == "LF" ? -1 : 1, Pojawia = RjDokument.Czas(f.Arr - 600) };
            }
            d.Pociagi.Add(p);
            // zjazd luzem lokomotywy z ŁF (skład wagonowy) – pociąg towarzyszący
            var lf = stops.FindIndex(s => s.Post == "LF");
            if (!unit && lf >= 0 && konce.Count > 0)
            {
                var tr = stops[lf].Track ?? "7";
                d.Pociagi.Add(new RjPociag
                {
                    Id = $"L{num}#{++nr}", Rodzic = p.Id, Pojazd = veh, SzlakDo = "1O", Odjazd = RjDokument.Czas(stops[lf].Dep + 480), Pojawia = RjDokument.Czas(stops[lf].Dep + 420),
                    Start = new RjStart { Odcinek = "LF:JT" + tr, Kierunek = 1, Pojawia = RjDokument.Czas(stops[lf].Dep + 420) },
                    Opis = $"zjazd luzem ŁF t{tr} → zaplecze Olechów (lok. {veh} z pociągu {num})",
                    Odcinki = { new RjOdcinek { Numer = "L" + new string(num.TakeWhile(char.IsDigit).ToArray()), KatRuchu = "LUZ" } }
                });
            }
        }
        d.Przewoznicy = przew.Select(k => new RjPrzewoznik { Kod = k, Nazwa = "" }).ToList();
        return d;
    }

    // ------------------------------------------------------------------ import z pliku układu (<układ>.rozklad.json)
    /// <summary>Rozkład względny (czasy od początku scenariusza) z pociągów pliku układu: jeden odcinek, postoje jako punkty biegu bez godzin.</summary>
    public static RjDokument ImportPliku(List<TimetableEntry> l, string zrodlo)
    {
        var d = new RjDokument { Wzgledny = true, Zrodlo = zrodlo + " (import do .rj.json)" };
        int nr = 0;
        foreach (var q in l)
        {
            var p = new RjPociag
            {
                Id = $"{q.Num}#{++nr}", Pojawia = RjDokument.Czas(q.T), SzlakZ = q.From, SzlakDo = q.To, Odjazd = RjDokument.Czas(q.Dep), Opis = q.Track, Sklad = q.Sklad,
                PostojWszedzie = q.Stop && q.StopAt == null, Odcinki = { new RjOdcinek { Numer = q.Num, KatRuchu = q.Cat } }
            };
            if (q.StartSec != null || q.StartDir != 1 || q.Loco) p.Start = new RjStart { Odcinek = q.StartSec, Kierunek = q.StartDir, Lok = q.Loco };
            foreach (var s in q.StopAt ?? new()) p.Bieg.Add(new RjPunkt { Post = s, Postoj = true });
            d.Pociagi.Add(p);
        }
        return d;
    }

    // ------------------------------------------------------------------ wczytanie do kolejki pociągów
    /// <summary>Pociąg bez biegu z godzinami (plik układu, zjazd luzem): numer, kategoria, szlaki, tor startu, odjazd, postoje.</summary>
    static TimetableEntry ZRjProsty(RjPociag p, double t0, bool maks)
    {
        var o = p.Odcinki[0];
        var postoje = p.Bieg.Where(b => b.Postoj && b.Post != null).Select(b => b.Post).ToHashSet();
        double t = (RjDokument.Sekundy(p.Start?.Pojawia ?? p.Pojawia) ?? 1) - t0;
        return new TimetableEntry
        {
            T = maks ? Math.Max(1, t) : t, Num = o.Numer, Cat = o.KatRuchu, Name = p.Nazwa, Veh = p.Pojazd, Len = p.Dlugosc, Przewoznik = o.Przewoznik, KatIr11 = o.Kat,
            Origin = p.RelacjaOd, Destination = p.RelacjaDo, From = p.SzlakZ, To = p.SzlakDo, StartSec = p.Start?.Odcinek, StartDir = p.Start?.Kierunek ?? 1, Loco = p.Start?.Lok == true,
            Dep = RjDokument.Sekundy(p.Odjazd) is double dj ? dj - t0 : null, Track = p.Opis, Sklad = p.Sklad,
            Stop = p.PostojWszedzie || postoje.Count > 0, StopAt = postoje.Count > 0 ? postoje : null, UwagiEksp = p.UwagiEksp, UwagiPrzew = p.UwagiPrzew
        };
    }

    /// <summary>
    /// Kolejka pociągów z rozkładu dobowego dla okna scenariusza (od <paramref name="startHour"/>, <paramref name="hours"/> godz.): pociągi pojawiające się
    /// w oknie (towarzyszące – razem z rodzicem, do pół godziny po oknie), zmiany czoła, numeru i kategorii w punktach biegu, start z toru albo zaplecza, odstawienie.
    /// </summary>
    public List<TimetableEntry> ZRj(RjDokument d, int startHour, int hours)
    {
        var res = new List<TimetableEntry>();
        S.StartClock = startHour * 3600;
        double t0 = S.StartClock, t1 = t0 + hours * 3600;
        RealSource = d.Dzien ?? "";
        int bazaN = 0;
        var wlaczone = new HashSet<string>();
        foreach (var p in d.Pociagi)
        {
            if (p.Odcinki.Count == 0) continue;
            if (p.Rodzic != null)
            {
                if (!wlaczone.Contains(p.Rodzic) || !(RjDokument.Sekundy(p.Start?.Pojawia ?? p.Pojawia) < t1 + 1800)) continue;
                var lz = ZRjProsty(p, t0, false); lz.Stop = false; lz.StopAt = null;
                res.Add(lz); continue;
            }
            double appear = RjDokument.Sekundy(p.Pojawia) ?? 0;
            if (appear < t0 - 60 || appear > t1) continue;
            wlaczone.Add(p.Id);
            if (p.Bieg.Count == 0 || p.Bieg.All(b => b.Przyjazd == null && b.Odjazd == null)) { res.Add(ZRjProsty(p, t0, true)); continue; }
            res.Add(ZRjBieg(p, d, t0, ref bazaN));
        }
        return res.OrderBy(q => q.T).ToList();
    }

    /// <summary>Kolejka pociągów z rozkładu względnego (czasy od początku scenariusza).</summary>
    public static List<TimetableEntry> ZRjWzgledny(RjDokument d) => d.Pociagi.Where(p => p.Odcinki.Count > 0).Select(p => ZRjProsty(p, 0, false)).ToList();

    /// <summary>Pociąg z biegiem przez posterunki (jak w <see cref="RealTimetable"/>).</summary>
    TimetableEntry ZRjBieg(RjPociag p, RjDokument d, double t0, ref int bazaN)
    {
        var st = p.Bieg.Select(b => (Post: b.Post, Arr: RjDokument.Sekundy(b.Przyjazd) ?? 0, Dep: RjDokument.Sekundy(b.Odjazd) ?? 0, Stop: b.Postoj, Track: b.Tor, Pl: b.Peron)).ToList();
        int Przyj(int i) => i == 0 ? 0 : p.Bieg[i - 1].Odcinek;   // odcinek, z którym pociąg przyjeżdża do punktu
        var legs = new List<List<int>>(); var cur = new List<int>();
        for (int i = 0; i < st.Count; i++)
        {
            cur.Add(i);
            if (i < st.Count - 1 && p.Bieg[i].Odcinek != Przyj(i)) { legs.Add(cur); cur = new(); }
        }
        if (cur.Count > 0 || legs.Count == 0) legs.Add(cur);
        HashSet<string> StopsOf(List<int> leg) => leg.Where(i => st[i].Stop).Select(i => st[i].Post).ToHashSet();
        Dictionary<string, double> DepsOf(List<int> leg) { var x = new Dictionary<string, double>(); foreach (var i in leg) if (st[i].Stop) x[st[i].Post] = st[i].Dep - t0; return x; }
        TrainTerm first = null, last = null;
        void AddTerm(TrainTerm tm) { if (first == null) first = tm; else last.Then = tm; last = tm; }
        for (int k = 0; k < legs.Count - 1; k++)
        {
            var ri = legs[k][^1]; var b = p.Bieg[ri];
            AddTerm(new TrainTerm
            {
                AtPost = b.Post, Reverse = b.ZmianaCzola, NewNum = p.Odcinki[b.Odcinek].Numer, NewDep = st[ri].Dep - t0, NewStopAt = StopsOf(legs[k + 1]), NewDepBy = DepsOf(legs[k + 1]),
                To = k + 1 == legs.Count - 1 ? p.SzlakDo : null, NewCat = b.KatRuchu
            });
        }
        string curNum = p.Odcinki[p.Bieg[^1].Odcinek].Numer;
        if (p.Koniec?.ZmianaCzolaW != null)
        {
            AddTerm(new TrainTerm { AtPost = p.Koniec.ZmianaCzolaW, Reverse = true, NewNum = curNum, NewCat = p.Koniec.KatRuchu, NewDep = st[^1].Dep - t0 + p.Koniec.Po, NewStopAt = new() { p.Koniec.Post }, NewDepBy = new() });
            AddTerm(new TrainTerm { AtPost = p.Koniec.Post, Odstaw = true, ShuntNum = p.Koniec.NrManewrowy });
        }
        else if (p.Koniec != null) AddTerm(new TrainTerm { AtPost = p.Koniec.Post, Odstaw = true, ShuntNum = p.Koniec.NrManewrowy });
        var o = p.Odcinki[0];
        var q = new TimetableEntry
        {
            T = Math.Max(1, (RjDokument.Sekundy(p.Pojawia) ?? 0) - t0), Num = o.Numer, Cat = o.KatRuchu, Name = p.Nazwa, Veh = p.Pojazd, Len = p.Dlugosc,
            Origin = p.RelacjaOd, Destination = p.RelacjaDo, Przewoznik = o.Przewoznik, KatIr11 = o.Kat, UwagiEksp = p.UwagiEksp, UwagiPrzew = p.UwagiPrzew, Sklad = p.Sklad,
            From = p.SzlakZ, To = legs.Count == 1 ? p.SzlakDo : null, Stop = StopsOf(legs[0]).Count > 0, StopAt = StopsOf(legs[0]), DepBy = DepsOf(legs[0]), Term = first, Track = p.Opis
        };
        var s0 = p.Start;
        if (s0?.ZZaplecza == true && d.ToryZaplecza.Count > 0)
        {
            var tz = d.ToryZaplecza[bazaN++ % d.ToryZaplecza.Count];
            q.StartSec = tz.Odcinek; q.StartDir = s0.Kierunek ?? -1; q.Loco = s0.Lok; q.T = Math.Max(1, (RjDokument.Sekundy(s0.Pojawia) ?? 0) - t0);
            q.Duty = new UnitDuty { Num = o.Numer, To = q.To, Dep = (RjDokument.Sekundy(p.Odjazd) ?? 0) - t0, Armed = true, Unit = s0.NrJednostki, Term = q.Term, StopAt = q.StopAt, DepBy = q.DepBy, Dir = s0.KierunekOdjazdu, Track = s0.TorOdjazdu };
            q.Num = s0.NrJednostki; q.Term = null;
            q.Track = (s0.OpisZaplecza ?? "").Replace("{tor}", tz.Tor) + q.Track;
        }
        else if (s0 != null)
        {
            q.StartSec = s0.Odcinek; q.StartDir = s0.Kierunek ?? 1; q.Loco = s0.Lok;
            q.T = Math.Max(1, (RjDokument.Sekundy(s0.Pojawia) ?? 0) - t0); q.Dep = RjDokument.Sekundy(p.Odjazd) is double dj ? dj - t0 : null;
        }
        // plan na posterunkach dla wykazu pociągów EDR
        q.PlanPrz = new(); q.PlanOdj = new(); q.PlanTor = new(); q.PlanPeron = new(); q.Przebieg = new();
        for (int i = 0; i < st.Count; i++)
            if (st[i].Post != null)
                q.Przebieg.Add(new PlanPunkt { Post = st[i].Post, Arr = st[i].Arr - t0, Dep = st[i].Dep - t0, Track = st[i].Track, Peron = st[i].Pl, Num = p.Odcinki[Przyj(i)].Numer });
        foreach (var s in st)
        {
            if (s.Post == null) continue;
            q.PlanPrz.TryAdd(s.Post, s.Arr - t0); q.PlanOdj[s.Post] = s.Dep - t0;
            if (s.Track != null) q.PlanTor.TryAdd(s.Post, s.Track);
            if (!string.IsNullOrWhiteSpace(s.Pl)) q.PlanPeron.TryAdd(s.Post, s.Pl.Trim());
        }
        return q;
    }
}
