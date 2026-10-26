using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Controls;
using Lcs.Model;
using K = System.Windows.Input.Key;

namespace Lcs.EdytorZobrazowania2;

/// <summary>
/// Szlak wewnętrzny obszaru LCS (pytanie właściciela 2026-10-22): układ <c>projekty_ut/lcs_testowe</c> – stacje TG i TW, szlak 1GW / 2GW z SBL między nimi
/// (oba końce – semafory wjazdowe, droga przez tory obu stacji). Obraz starego edytora: eksport jak w starym edytorze (strzałki blokady obu końców);
/// odstępy SBL przy szlaku 1GW na obrazie TG: pola z km odstępów po całej drodze (także na torze TW), numery odstępowych, nazwa sąsiada = druga stacja.
/// </summary>
public static partial class Test
{
    static async Task RunSzlakWewnetrzny(MainWindow w, string shot)
    {
        string src = null;
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null && src == null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "projekty_ut", "lcs_testowe", "lcs_testowe.zob.json"))) src = Path.Combine(d.FullName, "projekty_ut", "lcs_testowe");
        if (src == null) { Ok(true, "lcs_testowe – brak w repozytorium, pominięte"); return; }
        var dir = Path.Combine(Path.GetTempPath(), "edytor_zob2_test_lcs"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        foreach (var f in new[] { "lcs_testowe.ut.json", "lcs_testowe.zob.json" }) File.Copy(Path.Combine(src, f), Path.Combine(dir, f));
        var v1 = Path.Combine(dir, "lcs_testowe.zob.json");
        var stary = ZobDocument.Load(v1);
        var jStary = Lcs.Model.DoSymulatora.Buduj(stary.Ut, stary.P, "test").Json;
        Ok(w.OpenPath(v1) && w.Doc.ZWersji1 != null, $"lcs_testowe (wersja 1, dwie stacje) otwarte w nowym edytorze ({w.Doc.P.Obrazy.Count} obrazy)");
        var u = w.Doc.Ut;
        var kz = w.Kompiluj().Zob;
        var roz = Roznice(jStary, Lcs.Model.DoSymulatora.Buduj(u, kz, "test").Json);
        Ok(roz.Count == 0, $"eksport z szlakami wewnętrznymi 1GW / 2GW identyczny ze starym edytorem ({(roz.Count == 0 ? "bez różnic" : string.Join("; ", roz))})");

        // strzałki blokady: koniec szlaku z reguły nowego edytora = koniec ustawiony w starym edytorze (szlaki wewnętrzne – oba końce)
        var strz = w.Doc.P.Obrazy.SelectMany(o => o.Elementy.Where(e => e.Typ == ObrSymbole.StrzalkaBlokady).Select(e => (o, e))).ToList();
        var zle = strz.Where(z => z.o.Przypiecia.FirstOrDefault(p => p.El == z.e.Id)?.Ut is string ln && ObrKompilacja.KoniecSzlaku(u, ln, z.o, z.e) != (z.e.Pola ?? 0)).ToList();
        var wewn = strz.Count(z => z.o.Przypiecia.FirstOrDefault(p => p.El == z.e.Id)?.Ut is string ln && u.Lines.First(l => l.Id == ln).Konce.All(k => k.Semafor != null));
        Ok(strz.Count > 0 && zle.Count == 0 && wewn >= 4, $"strzałki blokady: {strz.Count} (na szlakach wewnętrznych {wewn}) – koniec szlaku jak w starym edytorze ({string.Join(", ", zle.Select(z => z.o.Nazwa))})");

        // obraz stacji TG, koniec toru szlaku 1GW
        var tg = u.Posts.First(p => p.Kod == "TG");
        var ob = w.Doc.P.Obrazy.First(o => o.Rodzaj == ZobObraz.Szczegolowy && o.Posterunki.Contains(tg.Id));
        w.ShowImage(ob.Id); await Settle(w);
        var l1gw = u.Lines.First(l => l.Numer == "1GW");
        var koniec = w.Obraz.Wezly.Where(n => ObrGraf.Przy(w.Obraz, n.Id).Count == 1).Select(n =>
        {
            var e = ObrGraf.Przy(w.Obraz, n.Id)[0]; var t = Pr(w, e.Id)?.Tor;
            double km = t == null ? double.NaN : e.A == n.Id ? t.S0 : t.S1;
            return (n, t, km);
        }).FirstOrDefault(z => z.t != null && ObrPrzypinanie.Linie(u, z.t.Tor, z.km).Any(l => l.Id == l1gw.Id));
        Ok(koniec.n != null, $"obraz TG: koniec toru na szlaku 1GW ({(koniec.n == null ? "brak" : $"{koniec.n.P[0]}, {koniec.n.P[1]}")})");
        if (koniec.n == null) return;
        Click(w.SymBtns[ObrSymbole.OdstepySbl]); w.View.SimClick(S(w, koniec.n.P[0], koniec.n.P[1]));
        Key(w, System.Windows.Input.Key.Escape);
        var od = w.Obraz.Elementy.LastOrDefault(e => e.Typ == ObrSymbole.OdstepySbl);
        Ok(od?.Na == koniec.n.Id, "odstępy SBL przy końcu toru szlaku 1GW");
        w.Przypnij();
        Ok(Pr(w, od.Id)?.Ut == l1gw.Id, "F8: odstępy przypięte do szlaku wewnętrznego 1GW");
        var ou = ObrSzlak.OdstepyUt(u, w.Obraz, od, l1gw.Id);
        var drTory = Szlaki.Drogi(u)[l1gw.Id].Kawalki.Select(k => k.Track).Distinct().ToList();
        Ok(ou != null && ou.Droga.Select(k => k.Track).Distinct().Count() == drTory.Count && Math.Abs(ou.L - Szlaki.Drogi(u)[l1gw.Id].Dlugosc) < 1,
            $"droga 1GW od semafora {ou?.Wjazdowy.Name}: {ou?.Droga.Count} kawałki na {drTory.Count} torach, {ou?.L:0} m, granic odstępów {ou?.Granice.Count - 2}");
        var post2 = Posterunki.Of(u, u.Devices.First(d => d.Id == l1gw.Konce.First(k => k.Semafor != null && Posterunki.Of(u, u.Devices.First(x => x.Id == k.Semafor).Track)?.Id != tg.Id).Semafor).Track);
        Ok(ObrSzlak.NazwaSasiada(u, l1gw.Id, w.Obraz) == post2.Nazwa + (l1gw.Linia.Length > 0 ? $" LK {l1gw.Linia}" : ""), $"nazwa sąsiada szlaku wewnętrznego – druga stacja obszaru: „{ObrSzlak.NazwaSasiada(u, l1gw.Id, w.Obraz)}”");
        Ok(od.Opisy[0].Tekst.Length > 0 && od.Opisy[0].Tekst.Contains('/'), $"numery pól z UT (dwa kierunki w jednym miejscu – „a/b”): {string.Join(" ", od.Opisy.Select(d => d.Tekst))}");
        var k = w.Kompiluj();
        var zo = k.Zob.Obrazy.First(o => o.Id == ob.Id);
        // kreski pól: oba końce na kresce któregoś pola (pierwsze pole – od punktu semafora wjazdowego)
        var pola = ObrSymbole.PolaSbl(w.Obraz, od);
        var semF = zo.Sygnalizatory.First(x => x.Ut == ou.Wjazdowy.Id).P;
        bool NaPolu(int[] p) => (p[0] == semF[0] && p[1] == semF[1]) || pola.Any(q => ObrGraf.Rzut(new[] { (int)q.Ax, (int)q.Ay }, new[] { (int)q.Bx, (int)q.By }, p[0], p[1]).Dist < 1.5)
            || (p[1] == (int)pola[0].Ay && p[0] >= Math.Min(semF[0], pola[0].Bx) - 1 && p[0] <= Math.Max(semF[0], pola[0].Bx) + 1);
        var kreski = zo.Tory.Where(t => t.Pkt.Count == 2 && drTory.Contains(t.Ut.Tor) && t.Pkt.All(NaPolu) && !(t.Pkt[0][0] == t.Pkt[1][0] && t.Pkt[0][1] == t.Pkt[1][1])).ToList();
        double pokryte = kreski.Sum(t => Math.Abs(t.Ut.S1 - t.Ut.S0));
        Ok(kreski.Select(t => t.Ut.Tor).Distinct().Count() == drTory.Count && Math.Abs(pokryte - ou.L) < 1,
            $"kreski pól na obu torach drogi 1GW, razem {pokryte:0} m = długość szlaku");
        var js = Lcs.Model.DoSymulatora.Buduj(u, k.Zob, "test");
        Ok(!js.Uwagi.Any(x => x.Contains("nie ma na żadnym obrazie")), $"eksport: wszystkie kawałki torów na obrazach ({string.Join("; ", js.Uwagi.Take(3))})");
        if (shot != null) { w.View.Fit(); await Settle(w); Save(w, shot); }
        await RunSzlakJedenObraz(w, u, tg, l1gw, shot == null ? null : Path.ChangeExtension(shot, null) + "_jeden_obraz.png");
    }

    /// <summary>
    /// Szlak wewnętrzny z obiema stacjami na jednym obrazie (uwaga właściciela 2026-10-22 „dalej nie mogę zrobić szlaku wewnętrznego”): tor szlaku idzie od
    /// stacji do stacji, nie ma końca – koniec szlaku, odstępy SBL i strzałka blokady wstawiane klikiem na torze, strona stacji sama, spacja – odwrotnie.
    /// </summary>
    static async Task RunSzlakJedenObraz(MainWindow w, UtProject u, UtPost tg, UtLine l1gw, string shot)
    {
        // obrazy TG i TW obok siebie na jednym obrazie (jak narysowane ręcznie), bez przypięć; koniec toru 1GW w TG i w TW połączone piórem
        var tw = u.Posts.First(p => p.Id != tg.Id && Posterunki.Of(u, u.Devices.First(d => d.Id == l1gw.Konce.First(k => k.Semafor != null && Posterunki.Of(u, u.Devices.First(x => x.Id == k.Semafor).Track)?.Id != tg.Id).Semafor).Track)?.Id == p.Id);
        var oa = w.Doc.P.Obrazy.First(o => o.Rodzaj == ZobObraz.Szczegolowy && o.Posterunki.Contains(tg.Id));
        var ob = w.Doc.P.Obrazy.First(o => o.Rodzaj == ZobObraz.Szczegolowy && o.Posterunki.Contains(tw.Id));
        ObrWezel Koniec1GW(ObrObraz o) => o.Wezly.FirstOrDefault(n => ObrGraf.Przy(o, n.Id).Count == 1 && o.Przypiecia.FirstOrDefault(p => p.El == ObrGraf.Przy(o, n.Id)[0].Id)?.Tor is ZobUtTor t
            && ObrPrzypinanie.Linie(u, t.Tor, ObrGraf.Przy(o, n.Id)[0].A == n.Id ? t.S0 : t.S1).Any(l => l.Id == l1gw.Id));
        var ka = Koniec1GW(oa); var kb = Koniec1GW(ob);
        Ok(ka != null && kb != null, $"końce toru 1GW: TG ({ka?.P[0]}, {ka?.P[1]}), TW ({kb?.P[0]}, {kb?.P[1]})");
        if (ka == null || kb == null) return;
        var nowy = new ObrObraz { Nazwa = "TG + TW", Rodzaj = ZobObraz.Szczegolowy, Posterunki = new() { tg.Id, tw.Id }, Szer = oa.Szer + ob.Szer, Wys = Math.Max(oa.Wys, ob.Wys) };
        nowy.Strefy = ObrObraz.StrefyDomyslne(nowy.Szer);
        ObrGraf.Wklej(nowy, ObrGraf.Kopiuj(oa, oa.Wezly.Select(n => n.Id), Array.Empty<string>()), 0, 0);
        ObrGraf.Wklej(nowy, ObrGraf.Kopiuj(ob, ob.Wezly.Select(n => n.Id), Array.Empty<string>()), oa.Szer, 0);
        // semafory odstępowe szlaku (w starym edytorze – symbole) zastąpią odstępy SBL
        var odst = u.Devices.Where(d => d.Kind == UtDevice.Semafor && d.Funkcja == UtDevice.FOdstepowy).Select(d => d.Name).ToHashSet();
        nowy.Elementy.RemoveAll(e => e.Typ == ObrSymbole.Semafor && odst.Contains(e.Opisy.FirstOrDefault()?.Tekst ?? ""));
        w.Doc.Change("dwie stacje", p => p.Obrazy.Add(nowy));
        w.ShowImage(nowy.Id); await Settle(w); w.View.Fit(); await Settle(w);
        int xa = ka.P[0], ya = ka.P[1], xb = kb.P[0] + oa.Szer, yb = kb.P[1];
        Key(w, K.T);
        w.View.SimClick(S(w, xa, ya));
        if (ya != yb) w.View.SimClick(S(w, xb - Math.Abs(yb - ya), ya));
        w.View.SimClick(S(w, xb, yb));
        Key(w, K.Escape); Key(w, K.Escape);
        var A = w.Obraz.Wezly.First(n => n.P[0] == xa && n.P[1] == ya); var B = w.Obraz.Wezly.First(n => n.P[0] == xb && n.P[1] == yb);
        Ok(ObrGraf.Przy(w.Obraz, A.Id).Count == 2 && ObrGraf.Przy(w.Obraz, B.Id).Count == 2, $"jeden obraz TG + TW ({w.Obraz.Odcinki.Count} odcinków, bez przypięć): tor szlaku 1GW narysowany od stacji do stacji, bez końca");
        Key(w, K.F8);
        Ok(true, $"F8: do wyjaśnienia {Wyj(w)} ({string.Join("; ", w.Obraz.Przypiecia.Where(p => p.Stan == ObrPrzypiecie.DoWyjasnienia).Select(p => p.Powod).GroupBy(x => x).Select(g => $"{g.Count()}× {g.Key}").Take(6))})");
        // te same nazwy w obu stacjach – człowiek potwierdza po jednym semaforze w każdej stacji, F8 dopina resztę
        var fy = w.Obraz.Elementy.Where(e => e.Typ == ObrSymbole.Semafor && Pr(w, e.Id) is { Stan: ObrPrzypiecie.DoWyjasnienia, Kandydaci.Count: > 1 } && ObrSymbole.Pozycja(w.Obraz, e) is not null).ToList();
        var fa = fy.FirstOrDefault(e => ObrSymbole.Pozycja(w.Obraz, e).Value.X < oa.Szer); var fb = fy.FirstOrDefault(e => ObrSymbole.Pozycja(w.Obraz, e).Value.X >= oa.Szer);
        string Kand(ObrElement e, UtPost p) => Pr(w, e.Id).Kandydaci.FirstOrDefault(id => Posterunki.Of(u, u.Devices.FirstOrDefault(d => d.Id == id)?.Track)?.Id == p.Id);
        if (fa != null && fb != null && Kand(fa, tg) != null && Kand(fb, tw) != null)
        {
            w.PotwierdzPrzypiecie(fa.Id, Kand(fa, tg)); w.PotwierdzPrzypiecie(fb.Id, Kand(fb, tw));
            Key(w, K.F8);
        }
        Ok(w.Obraz.Elementy.Where(e => e.Typ == ObrSymbole.Semafor).All(e => Pr(w, e.Id)?.Stan is ObrPrzypiecie.Pewne or ObrPrzypiecie.Potwierdzone
            && Posterunki.Of(u, u.Devices.First(d => d.Id == Pr(w, e.Id).Ut).Track)?.Id == (ObrSymbole.Pozycja(w.Obraz, e).Value.X < oa.Szer ? tg.Id : tw.Id)),
            "semafory obu stacji (te same nazwy A, B, C1…) przypięte do swoich stacji");
        var bledy0 = w.Sprawdz().Where(x => x.Blad).Select(x => x.Tekst).ToHashSet();
        var seg = ObrGraf.Przy(w.Obraz, A.Id).First(e => ObrGraf.Drugi(e, A.Id) != ObrGraf.Przy(w.Obraz, A.Id).Select(x => x.Id).FirstOrDefault() && ObrGraf.Wezel(w.Obraz, ObrGraf.Drugi(e, A.Id)).P[0] > xa);
        var (pa, pb) = ObrGraf.Konce(w.Obraz, seg);
        (double X, double Y) At(double f) => (pa[0] + (pb[0] - pa[0]) * f, pa[1] + (pb[1] - pa[1]) * f);
        var strz = w.Obraz.Elementy.Where(e => e.Typ == ObrSymbole.StrzalkaBlokady).ToList();
        var na1 = strz.Where(e => Pr(w, e.Id)?.Ut == l1gw.Id).ToList();
        var kon = na1.Select(e => ObrKompilacja.KoniecSzlaku(u, l1gw.Id, w.Obraz, e)).ToList();
        Ok(na1.Count == 2 && kon.Distinct().Count() == 2, $"F8: strzałki blokady obu stacji przypięte do 1GW ({na1.Count}), dwa różne końce szlaku ({string.Join(", ", kon)})");
        // Sprawdź (uwaga właściciela 2026-10-26): semafory końców 1GW bez przypięcia – strzałki na ślepo, obie w ten sam koniec
        Ok(!w.Sprawdz().Any(x => x.Tekst.StartsWith("strzałka blokady szlaku")), "Sprawdź: strzałki 1GW bez błędów (semafory końców przypięte)");
        var bez = JsonSerializer.Deserialize<ObrObraz>(JsonSerializer.Serialize(w.Obraz, UtDocument.Json), UtDocument.Json);
        var semKonca = l1gw.Konce.Select(k => k.Semafor).ToHashSet();
        bez.Przypiecia.RemoveAll(p => p.Ut != null && semKonca.Contains(p.Ut));
        // strzałki ze starego edytora mają koniec szlaku zapisany (Pola) – jak narysowane w nowym: bez
        foreach (var e in bez.Elementy.Where(e => e.Typ == ObrSymbole.StrzalkaBlokady)) e.Pola = null;
        var sb = ObrSprawdz.Sprawdz(u, bez).Where(x => x.Blad && x.Tekst.StartsWith("strzałka blokady szlaku")).ToList();
        Ok(sb.Count == 2 && sb.All(x => x.Tekst.Contains("nie są przypięte")),
            $"Sprawdź bez przypiętych semaforów końców 1GW: {sb.Count} błędy strzałek ({string.Join("; ", sb.Select(x => x.Tekst).Distinct().Select(t => t.Length > 90 ? t[..90] + "…" : t))})");
        foreach (var e in bez.Elementy.Where(e => e.Typ == ObrSymbole.StrzalkaBlokady)) e.Pola = 0;
        sb = ObrSprawdz.Sprawdz(u, bez).Where(x => x.Blad && x.Tekst.StartsWith("strzałka blokady szlaku 1GW:")).ToList();
        Ok(sb.Count(x => x.Tekst.Contains("tym samym końcem")) == 2, $"Sprawdź: obie strzałki 1GW w tym samym końcu – {sb.Count} błędy ({sb.FirstOrDefault()?.Tekst})");
        // koniec szlaku klikiem na torze bez końca: strzałka (semafor wjazdowy już jest w stronę stacji)
        int przed = w.Obraz.Elementy.Count;
        Click(w.SymBtns[ObrSymbole.KoniecSzlaku]); var q1 = At(0.5); w.View.SimClick(S(w, q1.X, q1.Y));
        Key(w, K.Escape); Key(w, K.Escape);
        var ns = w.Obraz.Elementy.Skip(przed).ToList();
        Ok(ns.Count >= 1 && ns[0].Typ == ObrSymbole.StrzalkaBlokady, $"koniec szlaku klikiem w środku toru szlaku: {string.Join(", ", ns.Select(e => ObrSymbole.NazwaTypu(e.Typ)))}");
        w.DoUndo();
        Click(w.SymBtns[ObrSymbole.OdstepySbl]); var qo = At(0.5); w.View.SimClick(S(w, qo.X, qo.Y));
        Key(w, K.Escape);
        var od = w.Obraz.Elementy.LastOrDefault(e => e.Typ == ObrSymbole.OdstepySbl);
        Ok(od != null && ObrGraf.Przy(w.Obraz, od.Na).Count == 2 && ObrSymbole.PolaSbl(w.Obraz, od).Count == od.Pola, $"odstępy SBL w środku toru szlaku: tor podzielony, {od?.Pola} pól bez kolumny");
        Key(w, K.F8);
        Ok(Pr(w, od.Id)?.Ut == l1gw.Id, $"F8: odstępy przypięte do 1GW ({Pr(w, od.Id)?.Stan} {Pr(w, od.Id)?.Powod})");
        var w1 = ObrSzlak.OdstepyUt(u, w.Obraz, od, l1gw.Id)?.Wjazdowy;
        w.View.Select(null, null, new[] { od.Id }); w.SetTool(ObrTool.Wskaz); Key(w, K.Space);
        od = w.Obraz.Elementy.First(e => e.Id == od.Id);
        var w2 = ObrSzlak.OdstepyUt(u, w.Obraz, od, l1gw.Id)?.Wjazdowy;
        Ok(w1 != null && w2 != null && w1.Id != w2.Id, $"odstępy: pola w stronę stacji semafora {w1?.Name}, spacja – w stronę drugiej ({w2?.Name})");
        var spr = w.Sprawdz();
        var js = Lcs.Model.DoSymulatora.Buduj(u, w.Kompiluj().Zob, "test");
        var nowe = spr.Where(x => x.Blad && !bledy0.Contains(x.Tekst)).Select(x => x.Tekst).ToList();
        Ok(nowe.Count == 0 && !js.Uwagi.Any(x => x.Contains("pominięte")), $"Sprawdź: bez nowych błędów po wstawieniu szlaku (poza szlakiem zostały {bledy0.Count} – z obrazów starego edytora po zdjęciu przypięć, np. rozjazdy krzyżowe 4ab), eksport bez pominiętych odstępów ({string.Join("; ", nowe.Concat(js.Uwagi.Where(x => x.Contains("pominięte"))).Take(4))})");
        if (shot != null)
        {
            // szlak 1GW między stacjami w powiększeniu
            w.View.ClearSelection(); w.View.SetView(0.75, new System.Windows.Point(w.View.ActualWidth / 2 - 1920 * 0.75, w.View.ActualHeight / 2 - 512 * 0.75)); await Settle(w); Save(w, shot);
        }
    }
}
