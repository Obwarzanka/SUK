using System.Windows;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>Próba na projekcie użytkownika (kopia): w zapisanym widoku klik na osi każdego widocznego toru (narzędzie Wskaż) – czy się zaznacza.</summary>
public static partial class Test
{
    static async Task RunZaznacz(MainWindow w, string path, string shot)
    {
        var prog = Environment.GetEnvironmentVariable("EDYTOR_UT_POSTEP");
        void P(string m) { if (prog != null) System.IO.File.AppendAllText(prog, DateTime.Now.ToString("HH:mm:ss.fff ") + m + Environment.NewLine); }
        P("start"); w.OpenPath(path); P("otwarty"); w.SetTool(UtTool.Wskaz); await Settle(w);
        var v = w.View; double W = v.ActualWidth, H = v.ActualHeight;
        log.AppendLine($"widok: schemat {v.Schemat}, {W:0}×{H:0} px");
        foreach (bool sch in Environment.GetEnvironmentVariable("EDYTOR_UT_SZYBKO") != null ? new bool[0] : new[] { false, true })
        {
            P($"schemat {sch}"); w.SetSchemat(sch); await Settle(w); log.AppendLine($"-- widok {(sch ? "Schemat" : "Teren")}");
            foreach (var t in w.Doc.P.Tracks)
            {
                var r = v.Geometry(t); if (r.Elements.Count == 0) { log.AppendLine($"NIE  {t.Id}: brak geometrii"); continue; }
                double s0 = Math.Max(0, r.Length / 2 - 60), s1 = Math.Min(r.Length, r.Length / 2 + 60);
                v.FitPoints(new[] { r.At(s0).P, r.At(s1).P }); await Settle(w);
                int tried = 0, ok = 0; var wrong = new HashSet<string>();
                for (double s = s0; s <= s1; s += (s1 - s0) / 20)
                {
                    var p = v.ToScreen(r.At(s).P); if (p.X < 5 || p.Y < 5 || p.X > W - 5 || p.Y > H - 5) continue;
                    tried++; v.Select(null); v.SimClick(p);
                    if (v.SelectedTrack == t.Id) ok++; else wrong.Add(v.SelectedTrack ?? "nic");
                }
                log.AppendLine($"{(ok == tried && tried > 0 ? "OK  " : "NIE ")} {t.Id} nr {t.Number}: {ok}/{tried} klików, ok geometrii {r.Ok}, długość {r.Length:0} m" + (wrong.Count > 0 ? $", zamiast: {string.Join(",", wrong)}" : ""));
            }
        }
        P("obszar stacji");
        // tory w obszarze stacji (ramka pozostałych torów krótszych niż 5 km): klik na każdym torze w kilku miejscach w tej ramce
        var shortT = w.Doc.P.Tracks.Select(t => v.Geometry(t)).Where(r => r.Elements.Count > 0 && r.Length < 5000).SelectMany(r => r.Sample(20).Select(q => q.P)).ToList();
        double x0 = shortT.Min(p => p.X), x1 = shortT.Max(p => p.X), y0 = shortT.Min(p => p.Y), y1 = shortT.Max(p => p.Y);
        log.AppendLine($"-- w obszarze stacji ({(x1 - x0):0}×{(y1 - y0):0} m)");
        foreach (var t in w.Doc.P.Tracks)
        {
            var r = v.Geometry(t); if (r.Elements.Count == 0) continue;
            P($"{t.Id} próbki"); var inside = r.Sample(Math.Max(1, r.Length / 4000)).Where(q => q.P.X >= x0 && q.P.X <= x1 && q.P.Y >= y0 && q.P.Y <= y1).ToList();
            if (inside.Count == 0) continue;
            int tried = 0, ok = 0; var wrong = new HashSet<string>();
            int nk = Environment.GetEnvironmentVariable("EDYTOR_UT_SZYBKO") != null ? 2 : 6;
            for (int i = 0; i < nk; i++)
            {
                var q = inside[(int)((inside.Count - 1) * (i + 0.5) / nk)].P;
                P($"{t.Id} #{i} widok"); v.FitPoints(new[] { q + new XY(-40, -40), q + new XY(40, 40) }); await Settle(w);
                var p = v.ToScreen(q); tried++; P($"{t.Id} #{i} klik"); v.Select(null);
                var sw = System.Diagnostics.Stopwatch.StartNew(); v.SimClick(p); P($"{t.Id} #{i} po kliku {sw.ElapsedMilliseconds} ms → {v.SelectedTrack}");
                if (v.SelectedTrack == t.Id) ok++; else wrong.Add((v.SelectedTrack ?? (v.SelectedTurnout != null ? "rozjazd " + v.SelectedTurnout : v.SelectedCrossing != null ? "skrzyżowanie " + v.SelectedCrossing : v.SelectedDevice != null ? "urządzenie " + v.SelectedDevice : v.SelectedObj != null ? "obiekt" : "nic")) + $" ({sw.ElapsedMilliseconds} ms)");
            }
            log.AppendLine($"{(ok == tried ? "OK  " : "NIE ")} {t.Id}: {ok}/{tried}, długość {r.Length:0} m" + (wrong.Count > 0 ? $", zamiast: {string.Join(",", wrong)}" : ""));
        }
        // Del na każdym torze z błędem geometrii (klik na torze, potem Del) – czy znika
        log.AppendLine("-- usuwanie torów z błędami");
        foreach (var id in w.Doc.P.Tracks.Where(t => !v.Geometry(t).Ok).Select(t => t.Id).ToList())
        {
            var t = w.Doc.P.Tracks.FirstOrDefault(x => x.Id == id); if (t == null) continue;
            var r = v.Geometry(t); var q = r.At(r.Length * 0.37).P;
            v.FitPoints(new[] { q + new XY(-40, -40), q + new XY(40, 40) }); await Settle(w);
            v.Select(null); v.SimClick(v.ToScreen(q));
            string sel = $"zaznaczony {v.SelectedTrack} W{v.SelectedVertex}";
            int n = w.Doc.P.Tracks.Count; KeyPress(w, System.Windows.Input.Key.Delete);
            bool gone = w.Doc.P.Tracks.All(x => x.Id != id);
            log.AppendLine($"{(gone ? "OK  " : "NIE ")} Del {id}: {sel}, torów {n}→{w.Doc.P.Tracks.Count}, komunikat: {w.LastMessage}");
            if (gone) w.DoUndo();
        }
        if (shot != null) Save(w, shot);
    }
}
