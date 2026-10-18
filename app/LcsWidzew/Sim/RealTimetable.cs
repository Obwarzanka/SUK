using System.IO;
using System.Text.Json;

namespace LcsWidzew.Sim;

/// <summary>
/// Rozkład rzeczywisty PKP Intercity (Data/rozklad_ic.json – tools/pkpic_import.py, źródło ptrc.gay/pkpic).
/// Pociąg w obszarze LCS: wjazd ze szlaku (albo z toru), postoje na ŁW / ŁF wg rozkładu, zmiana czoła w ŁF (stacja czołowa),
/// próżne z / do zaplecza Olechów, zjazdy luzem lokomotyw z ŁF. Uzupełnienie: manewry jednostek ŁKA z bazy Widzew (przykładowe – brak w źródle).
/// </summary>
public partial class Rozklad
{
    public static string RealSource;

    List<TimetableEntry> RealTimetable(int startHour, int hours)
    {
        var res = new List<TimetableEntry>();
        // rozklad.json – pełny rozkład z GTFS (tools/gtfs_import.py), rozklad_ic.json – tylko PKP IC (tools/pkpic_import.py)
        var dir = S.DataDirectory;
        var path = Path.Combine(dir, "rozklad.json"); bool gtfs = File.Exists(path);
        if (!gtfs) path = Path.Combine(dir, "rozklad_ic.json");
        if (!File.Exists(path)) { S.Log("Zdarzenie", "Brak pliku rozkładu (tools/gtfs_import.py lub tools/pkpic_import.py)", "System", ""); return res; }
        int bazaN = 0;
        S.StartClock = startHour * 3600;
        double t0 = S.StartClock, t1 = t0 + hours * 3600;
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        RealSource = doc.RootElement.TryGetProperty("date", out var dt) ? dt.GetString() : "";
        string Sx(JsonElement e, string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        static string PostShort(string p) => p switch { "LW" => "ŁW", "LF" => "ŁF", "LN" => "ŁN", _ => p };
        string Hm(double tod) { var s = (long)tod; return $"{s / 3600 % 24:00}:{s / 60 % 60:00}"; }

        foreach (var e in doc.RootElement.GetProperty("trains").EnumerateArray())
        {
            double appear = e.GetProperty("appear").GetDouble();
            if (appear < t0 - 60 || appear > t1) continue;
            string num = Sx(e, "num"), cat = Sx(e, "cat") ?? "IC", veh = Sx(e, "veh") ?? "", from = Sx(e, "from"), to = Sx(e, "to");
            bool unit = e.GetProperty("unit").GetBoolean(), emptyIn = e.GetProperty("emptyIn").GetBoolean(), emptyOut = e.GetProperty("emptyOut").GetBoolean();
            double len = e.GetProperty("len").GetDouble();
            var stops = e.GetProperty("stops").EnumerateArray().Select(s => (Post: Sx(s, "post"), Arr: s.GetProperty("arr").GetDouble(), Dep: s.GetProperty("dep").GetDouble(),
                Stop: s.GetProperty("stop").GetBoolean(), Track: Sx(s, "track"), Pl: Sx(s, "platform"))).ToList();
            if (stops.Count == 0) continue;
            // bieg kończy się w obszarze LCS bez postoju w ostatnim punkcie (przejazd techniczny) – zatrzymanie i odstawienie tam
            if (to == null && !stops[^1].Stop) stops[^1] = stops[^1] with { Stop = true };
            // odcinki biegu między zmianami czoła (ŁF – zawsze, gdy pociąg jedzie dalej)
            var legs = new List<List<int>>(); var cur = new List<int>();
            var revIdx = e.TryGetProperty("revIdx", out var ri0) ? ri0.EnumerateArray().Select(x => x.GetInt32()).ToHashSet() : null;
            var renumIdx = e.TryGetProperty("renumIdx", out var rn0) ? rn0.EnumerateArray().Select(x => x.GetInt32()).ToHashSet() : new HashSet<int>();
            for (int i = 0; i < stops.Count; i++)
            {
                cur.Add(i);
                bool rev = revIdx != null ? revIdx.Contains(i) || renumIdx.Contains(i) : stops[i].Post == "LF" && (i < stops.Count - 1 || to != null);
                if (rev) { legs.Add(cur); cur = new(); }
            }
            if (cur.Count > 0 || legs.Count == 0) legs.Add(cur);
            HashSet<string> StopsOf(List<int> leg) => leg.Where(i => stops[i].Stop).Select(i => stops[i].Post).ToHashSet();
            Dictionary<string, double> DepsOf(List<int> leg) { var d = new Dictionary<string, double>(); foreach (var i in leg) if (stops[i].Stop) d[stops[i].Post] = stops[i].Dep - t0; return d; }

            // łańcuch zmian czoła
            TrainTerm first = null, last = null;
            var numsAt = e.TryGetProperty("numsAt", out var na) ? na.EnumerateObject().ToDictionary(x => int.Parse(x.Name), x => x.Value.GetString()) : new();
            string curNum = num;
            void AddTerm(TrainTerm tm) { if (first == null) first = tm; else last.Then = tm; last = tm; }
            for (int k = 0; k < legs.Count - 1; k++)
            {
                var ri = legs[k][^1]; var rp = stops[ri];
                bool lastLeg = k + 1 == legs.Count - 1;
                AddTerm(new TrainTerm
                {
                    AtPost = rp.Post, Reverse = !renumIdx.Contains(ri), NewNum = curNum = numsAt.GetValueOrDefault(ri, curNum), NewDep = rp.Dep - t0, NewStopAt = StopsOf(legs[k + 1]), NewDepBy = DepsOf(legs[k + 1]),
                    To = lastLeg ? to : null, NewCat = rp.Post == "LF" ? (emptyIn ? cat : emptyOut ? "PRÓŻ" : null) : null
                });
            }
            bool endBaza = e.TryGetProperty("endBaza", out var eb) && eb.GetBoolean(), startBaza = e.TryGetProperty("startBaza", out var sb) && sb.GetBoolean();
            if (to == null && gtfs && stops[^1].Post == "LF")
            {
                // koniec biegu w ŁF bez dalszego obiegu – zmiana czoła i próżny do bazy w ŁW
                AddTerm(new TrainTerm { AtPost = "LF", Reverse = true, NewNum = curNum, NewCat = "PRÓŻ", NewDep = stops[^1].Dep - t0 + 300, NewStopAt = new() { "LW" }, NewDepBy = new() });
                AddTerm(new TrainTerm { AtPost = "LW", Odstaw = true, ShuntNum = curNum + "M" });
            }
            else if (to == null) AddTerm(new TrainTerm { AtPost = stops[^1].Post, Odstaw = true, ShuntNum = endBaza ? curNum + "M" : null });

            var plan = string.Join(" → ", stops.Where(s => s.Stop).Select(s => $"{PostShort(s.Post)} {(s.Track != null ? "t" + s.Track : "")}{(s.Pl != null ? " p" + s.Pl : "")} {Hm(s.Arr)}/{Hm(s.Dep)}"));
            var q = new TimetableEntry
            {
                T = Math.Max(1, appear - t0), Num = num, Cat = emptyIn ? "PRÓŻ" : cat, Name = Sx(e, "name"), Veh = veh, Len = len,
                From = from, To = legs.Count == 1 ? to : null, Stop = StopsOf(legs[0]).Count > 0, StopAt = StopsOf(legs[0]), DepBy = DepsOf(legs[0]), Term = first,
                Track = $"{Sx(e, "name")} ({veh}{(emptyIn ? ", próżny z Olechowa" : "")}): {plan}{(emptyOut ? " → próżny do Olechowa" : "")}"
            };
            if (from == null && startBaza)
            {
                // pociąg zaczyna bieg w ŁW – jednostka wyjeżdża z bazy ŁKA (manewr) na tor peronowy i staje się pociągiem
                var f = stops[0]; var tr = bazaN++ % 2 == 0 ? "102" : "104";
                q.StartSec = "LW:JT" + tr; q.StartDir = -1; q.Loco = true; q.T = Math.Max(1, f.Dep - 1200 - t0);
                // kierunek odjazdu: na szlak (strona wyjazdu z ŁW) albo do następnego posterunku LCS (położenie na obrazie)
                int ddir = 0;
                if (to != null && S.L.Blocks.TryGetValue(to, out var tb)) ddir = tb.Ends.FirstOrDefault(x => x.Post == "LW")?.Outward ?? 0;
                if (ddir == 0 && stops.Count > 1)
                {
                    double Ax(string post) { var ns = S.L.Nodes.Values.Where(n => n.Post == post).ToList(); return ns.Count > 0 ? ns.Average(n => n.X) : 0; }
                    ddir = Math.Sign(Ax(stops[1].Post) - Ax("LW"));
                }
                q.Duty = new UnitDuty { Num = num, To = q.To, Dep = f.Dep - t0, Armed = true, Unit = num + "M", Term = q.Term, StopAt = q.StopAt, DepBy = q.DepBy, Dir = ddir, Track = f.Track };
                q.Num = num + "M"; q.Term = null;
                q.Track = $"z bazy ŁKA (tor {tr}) na ŁW t{f.Track} p{f.Pl}, odjazd jako {num}: " + q.Track;
            }
            else if (from == null)
            {
                // skład wystawiony na torze (np. postój w ŁW przed jazdą do ŁF)
                var f = stops[0];
                q.StartSec = f.Post == "LF" ? "LF:JT" + (f.Track ?? "7") : "LW:JT6";
                q.StartDir = f.Post == "LF" ? 1 : stops.Count > 1 && stops[1].Post == "LF" ? -1 : 1;
                q.T = Math.Max(1, f.Arr - 600 - t0); q.Dep = f.Dep - t0;
            }
            res.Add(q);

            // zjazd luzem lokomotywy z ŁF (skład wagonowy – lokomotywa pozostaje przy kozle i odjeżdża po odjeździe pociągu)
            var lf = stops.FindIndex(s => s.Post == "LF");
            if (!unit && lf >= 0 && legs.Count > 1 && stops[lf].Dep + 420 < t1 + 1800)
            {
                var tr = stops[lf].Track ?? "7";
                res.Add(new TimetableEntry
                {
                    T = stops[lf].Dep + 420 - t0, Num = "L" + new string(num.TakeWhile(char.IsDigit).ToArray()), Cat = "LUZ", Veh = veh, StartSec = "LF:JT" + tr, StartDir = 1, To = "1O",
                    Stop = false, Dep = stops[lf].Dep + 480 - t0, Track = $"zjazd luzem ŁF t{tr} → zaplecze Olechów (lok. {veh} z pociągu {num})"
                });
            }
        }

        if (gtfs) return res.OrderBy(q => q.T).ToList();     // pełny rozkład – bez przykładowych jednostek ŁKA
        // Obieg jednostek ŁKA (przykładowy – źródło obejmuje tylko PKP IC); numer zmienia się przy każdej zmianie roli:
        //  • z bazy Widzew (tory 102/104): numer manewrowy jednostki → manewr na tor peronowy → pociąg,
        //  • pociąg Andrzejów → ŁW → ŁF: zmiana czoła w ŁF i nowy numer, powrót przez ŁW do Andrzejowa,
        //  • pociąg kończący bieg w ŁW: numer manewrowy jednostki → zjazd do bazy → później nowy pociąg.
        int n = 0, lkaNum = 19101;
        string Unit(int k) => "EN76-" + (k + 1).ToString("00");
        string Nr() { var x = lkaNum.ToString(); lkaNum += 2; return x; }
        for (double t = 300; t < hours * 3600 - 900; t += 900, n++)
        {
            switch (n % 3)
            {
                case 0:
                {
                    var tr = n % 2 == 0 ? "102" : "104";
                    var go = n % 6 == 0 ? "1A" : "1C"; var tn = Nr();
                    res.Add(new TimetableEntry
                    {
                        T = t, Num = Unit(n), Cat = "ŁKA", Len = 85, StartSec = "LW:JT" + tr, StartDir = -1, Loco = true,
                        Duty = new UnitDuty { Num = tn, To = go, Dep = t + 900, Armed = true, Unit = Unit(n) },
                        Track = $"jednostka {Unit(n)} w bazie ŁKA (tor {tr}) – manewr na tor peronowy ŁW, zmiana numeru na {tn}, odjazd {Hm(t0 + t + 900)} na {go} (przykład)"
                    });
                    break;
                }
                case 1:
                {
                    var a1 = Nr(); var a2 = (int.Parse(a1) + 1).ToString();
                    double arrW = t + 180, arrF = t + 600, depF = t + 1200;
                    res.Add(new TimetableEntry
                    {
                        T = t, Num = a1, Cat = "ŁKA", Len = 85, From = "2A", Stop = true, StopAt = new() { "LW", "LF" },
                        DepBy = new() { ["LW"] = arrW + 60, ["LF"] = depF },
                        Term = new TrainTerm { AtPost = "LF", Reverse = true, NewNum = a2, NewDep = depF, NewStopAt = new() { "LW" }, NewDepBy = new() { ["LW"] = depF + 420 }, To = "1A" },
                        Track = $"ŁKA {a1}: Andrzejów → ŁW {Hm(t0 + arrW)} → ŁF {Hm(t0 + arrF)}, zmiana czoła i numeru na {a2}, odjazd {Hm(t0 + depF)} → ŁW → Andrzejów (przykład)"
                    });
                    break;
                }
                default:
                {
                    var a1 = Nr(); var next = Nr(); var go = n % 2 == 0 ? "1A" : "1C";
                    res.Add(new TimetableEntry
                    {
                        T = t, Num = a1, Cat = "ŁKA", Len = 85, From = n % 4 == 2 ? "2A" : "2C", Stop = true, StopAt = new() { "LW" }, DepBy = new() { ["LW"] = t + 300 },
                        Term = new TrainTerm { AtPost = "LW", Odstaw = true, ShuntNum = Unit(n), NewDuty = new UnitDuty { Num = next, To = go, Dep = t + 3000, Unit = Unit(n) } },
                        Track = $"ŁKA {a1}: kończy bieg w ŁW, jednostka {Unit(n)} zjeżdża do bazy (manewr), dalej jako {next} o {Hm(t0 + t + 3000)} na {go} (przykład)"
                    });
                    break;
                }
            }
        }
        return res.OrderBy(q => q.T).ToList();
    }
}
