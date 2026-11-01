using System.Text;
using System.Text.Json;
using Lcs.RozkladJazdy;
using LcsWidzew.Model;

namespace LcsWidzew.Sim;

/// <summary>
/// Test rozkładu <c>&lt;układ&gt;.rj.json</c> (<c>--rjtest wynik.txt</c>, PLAN_EDYTOR_RJ.md R1): import dzisiejszych źródeł i wczytanie dają tę samą
/// kolejkę pociągów, ten sam wykaz EDR i ten sam ruch co bez pliku. Pracuje na kopii układu w katalogu tymczasowym.
/// </summary>
public static class RozkladRJTest
{
    static readonly JsonSerializerOptions Opt = new() { IncludeFields = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static void Run(string stationPath, string outPath, SimulatorOptions options = null, string[] scenariusze = null)
    {
        var sb = new StringBuilder(); int ok = 0, tot = 0;
        void K(bool c, string m) { tot++; if (c) ok++; sb.AppendLine($"{(c ? "OK  " : "BŁĄD")} {m}"); }
        var kat = Path.Combine(Path.GetTempPath(), $"rjtest_{Environment.ProcessId}");
        Directory.CreateDirectory(kat);
        try
        {
            // kopia układu z plikami obok niego (rozkład, zależności, rozkład pliku układu); bez .rj.json
            var zrodlo = Path.GetDirectoryName(Path.GetFullPath(stationPath));
            var nazwa = Path.GetFileNameWithoutExtension(stationPath);
            foreach (var f in Directory.GetFiles(zrodlo).Where(f => Path.GetFileName(f) is "rozklad.json" or "rozklad_ic.json" || Path.GetFileName(f).StartsWith(nazwa + ".") && !f.EndsWith(RjDokument.Rozszerzenie) && !f.Contains(".edr")))
                File.Copy(f, Path.Combine(kat, Path.GetFileName(f)), true);
            var kopia = Path.Combine(kat, Path.GetFileName(stationPath));
            var rjPlik = RjDokument.PlikDla(kopia);
            var user = new User { Name = "Test", Role = "dyżurny ruchu" };

            // czasy: zapis i odczyt
            K(RjDokument.Czas(18300) == "05:05:00" && RjDokument.Sekundy("05:05:00") == 18300 && RjDokument.Sekundy(RjDokument.Czas(90000.5)) == 90000.5 && RjDokument.Sekundy("5:05") == 18300 && RjDokument.Sekundy("25:61") == null,
                "czasy: 05:05:00 ↔ 18300 s, ułamek sekundy, następna doba (25:00:00.5), błędny – odrzucony");

            Simulator Sym() { var s = new Simulator(Station.Load(kopia), options: options) { User = user }; var k = s.AddDesk("PC", "Stanowisko 1", true); k.User = user; s.Edr.PlikUstawien = null; _ = new Trains(s); _ = Sygnaly.Tab; _ = s.Trains.Tabor; return s; }
            string Kolejka(Simulator s) => JsonSerializer.Serialize(s.Rozklad.Queue, Opt);

            // 1. rozkład dobowy z importu (rozklad.json albo rozklad_ic.json)
            var rozkladPath = new[] { "rozklad.json", "rozklad_ic.json" }.Select(f => Path.Combine(kat, f)).FirstOrDefault(File.Exists);
            if (rozkladPath == null) sb.AppendLine("(brak rozklad.json / rozklad_ic.json obok układu – rozkład dobowy pominięty)");
            else
            {
                if (File.Exists(rjPlik)) File.Delete(rjPlik);
                var baza = Sym();
                var doc = ImportZaplecza(baza, rozkladPath);
                var json = doc.Json();
                var doc2 = RjDokument.Parsuj(json);
                K(doc2.Json() == json && doc.Sprawdz().Count == 0, $"import {Path.GetFileName(rozkladPath)}: {doc.Pociagi.Count} pociągów, {doc.Pociagi.Sum(p => p.Odcinki.Count)} odcinków, zapis i odczyt bez zmian, Sprawdź – {(doc.Sprawdz().Count == 0 ? "bez błędów" : string.Join("; ", doc.Sprawdz().Take(3)))}");
                foreach (var key in scenariusze ?? new[] { "ic_05", "ic_08", "ic_15", "ic_18" })
                {
                    if (File.Exists(rjPlik)) File.Delete(rjPlik);
                    var a = Sym(); a.Trains.Load(key);
                    doc2.Zapisz(rjPlik);
                    var b = Sym(); b.Trains.Load(key);
                    string qa = Kolejka(a), qb = Kolejka(b);
                    K(qa == qb && a.StartClock == b.StartClock, $"{key}: kolejka pociągów z .rj.json taka sama jak z rozkładu ({a.Rozklad.Queue.Count} pociągów){(qa == qb ? "" : " – różnica: " + Roznica(a, b))}");
                    // ruch i EDR: 20 min symulacji
                    for (int i = 0; i < 2400; i++) { a.Tick(0.5); b.Tick(0.5); }
                    var posty = a.Edr.Posterunki.Select(p => p.Kod).ToList();
                    string Wykaz(Simulator s) => JsonSerializer.Serialize(posty.ToDictionary(p => p, p => s.Edr.Wykaz(p)), Opt);
                    string Ruch(Simulator s) => string.Join("\n", s.Trains.List.Select(t => $"{t.Num}|{t.Cat}|{t.State}|{string.Join(",", t.Segs.Select(x => x.E ?? x.Szlak))}"));
                    K(Wykaz(a) == Wykaz(b), $"{key}: wykaz pociągów EDR po 20 min taki sam ({posty.Sum(p => a.Edr.Wykaz(p).Count)} wierszy)");
                    K(Ruch(a) == Ruch(b), $"{key}: ruch po 20 min taki sam ({a.Trains.List.Count} pociągów w ruchu)");
                }
                if (File.Exists(rjPlik)) File.Delete(rjPlik);
            }

            // 2. rozkład pliku układu (<układ>.rozklad.json) – względny, scenariusz „Ruch podstawowy”
            var plikUkladu = RozkladPlik.PlikDla(kopia);
            if (!File.Exists(plikUkladu)) sb.AppendLine($"(brak {Path.GetFileName(plikUkladu)} – rozkład pliku układu pominięty)");
            else
            {
                var a = Sym(); a.Trains.Load("podstawowy");
                var docP = Rozklad.ImportPliku(RozkladPlik.Wczytaj(kopia), Path.GetFileName(plikUkladu));
                docP.Zapisz(rjPlik);
                var b = Sym(); b.Trains.Load("podstawowy");
                K(Kolejka(a) == Kolejka(b) && docP.Wzgledny, $"plik układu {Path.GetFileName(plikUkladu)}: kolejka z .rj.json (względny) taka sama ({a.Rozklad.Queue.Count} pociągów)");
                File.Delete(rjPlik);
            }

            // 3. zmiany w biegu: numer i kategoria w punkcie, zmiana czoła, odstawienie – z danych, bez rozkładu źródłowego
            var t = new RjDokument
            {
                Pociagi = { new RjPociag
                {
                    Id = "t1", Pojawia = "06:00:00", SzlakZ = "Z1", SzlakDo = "D1", Nazwa = "Próba",
                    Odcinki = { new() { Numer = "10001", Kat = "AAA", KatRuchu = "IC", Przewoznik = "X" }, new() { Numer = "10003", Kat = "BBB", KatRuchu = "PR", Przewoznik = "Y" } },
                    Bieg = { new() { Post = "P1", Przyjazd = "06:05:00", Odjazd = "06:06:00", Postoj = true, Tor = "2" },
                             new() { Post = "P2", Przyjazd = "06:10:00", Odjazd = "06:10:00", Odcinek = 1, KatRuchu = "PR" },
                             new() { Post = "P3", Przyjazd = "06:15:00", Odjazd = "06:20:00", Postoj = true, Odcinek = 1 } }
                } }
            };
            var s3 = Sym();
            var q3 = s3.Rozklad.ZRj(t, 6, 1).Single();
            K(q3.Num == "10001" && q3.Cat == "IC" && q3.KatIr11 == "AAA" && q3.Term is { AtPost: "P2", Reverse: false, NewNum: "10003", NewCat: "PR", To: "D1" } && q3.To == null
                && q3.Przebieg.Select(x => x.Num).SequenceEqual(new[] { "10001", "10001", "10003" }) && q3.PlanOdj["P3"] == 1200 && q3.StopAt.SetEquals(new[] { "P1" }),
                "zmiana numeru i kategorii w punkcie bez postoju (P2): odcinek 10003 / BBB / PR, bieg z numerem na każdym punkcie, odjazd ze szlakiem D1");
        }
        catch (Exception ex) { K(false, "wyjątek: " + ex); }
        finally { try { Directory.Delete(kat, true); } catch { } }
        sb.Insert(0, $"RJ: kontrola {ok} z {tot}{Environment.NewLine}");
        File.WriteAllText(outPath, sb.ToString());
    }

    static RjDokument ImportZaplecza(Simulator s, string path) => Rozklad.ImportRozkladu(s, path);

    /// <summary>Pierwszy pociąg, którym różnią się kolejki (do raportu).</summary>
    static string Roznica(Simulator a, Simulator b)
    {
        var la = a.Rozklad.Queue; var lb = b.Rozklad.Queue;
        if (la.Count != lb.Count) return $"liczba {la.Count} / {lb.Count}";
        for (int i = 0; i < la.Count; i++)
        {
            string x = JsonSerializer.Serialize(la[i], Opt), y = JsonSerializer.Serialize(lb[i], Opt);
            if (x == y) continue;
            int j = 0; while (j < x.Length && j < y.Length && x[j] == y[j]) j++;
            return $"pociąg {la[i].Num}: …{x.Substring(Math.Max(0, j - 60), Math.Min(140, x.Length - Math.Max(0, j - 60)))} / …{y.Substring(Math.Max(0, j - 60), Math.Min(140, y.Length - Math.Max(0, j - 60)))}";
        }
        return "?";
    }
}
