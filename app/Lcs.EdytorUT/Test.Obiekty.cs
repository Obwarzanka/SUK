using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Test § 5m jak praca użytkownika: dwa tory na skośnej linii z łukiem R 300 (tor 1 klikami, tor 2 równoległy z panelu), w Schemacie
/// narzędziem Obiekt: wiadukt nad drogą na łuku (klik OD, klik DO) i Ctrl + klik – tor 2 dołożony, most przeciągnięciem, tunel,
/// wiadukt nad torem, przepust, przejście pod torami (skośnie), kładka, wiadukt drogowy, mur i ściana oporowa; panel (przeszkoda,
/// rozdzielenie na tory), przesunięcie myszą, „Sprawdź” (nakładanie, bez nazwy), km po zmianie łuku, cięcie toru, profil – niweleta
/// z terenu bez doliny pod wiaduktem i wzgórza nad tunelem, warstwa „budowla”, zapis i odczyt, zrzuty znaków do akceptacji.
/// </summary>
public static partial class Test
{
    static async Task RunObiekty(MainWindow w, string shot)
    {
        const double lineAz = 63;
        var dir = Path.Combine(Path.GetTempPath(), "edytor_ut_test", "obiekty"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        w.SetSchemat(false);
        w.NewDoc(); w.SaveDoc(false, Path.Combine(dir, "obiekty.ut.json"));
        var d = w.Doc;
        var o = Geo.ToPuwg92(51.7700, 19.5600); double k = Geo.ScaleFactor(o);
        XY Go(XY from, double azDeg, double len) => from + Element.Dir(azDeg * Math.PI / 180) * (len * k);
        Point S(XY p) => w.View.ToScreen(p);
        void Look(XY p, double mpp) => w.View.SetView(p, mpp);
        string shotDir = shot != null ? Path.GetDirectoryName(Path.GetFullPath(shot))! : null, shotName = shot != null ? Path.GetFileNameWithoutExtension(shot) : null;
        string Shot(string tag) => Path.Combine(shotDir, shotName + "_obiekty_" + tag + ".png");

        // --- tor 1: prosta – łuk R 300 (20°) – prosta, klikami; tor 2 równoległy 5 m z panelu
        w.SetTool(UtTool.Tor); w.View.Mode = UtView.TorMode.Prosta; w.NewRBox.Text = "300"; w.NewLBox.Text = "0";
        var a = Go(o, lineAz + 180, 500);
        double az0 = lineAz * Math.PI / 180, R = 300 * k;
        var cen = o + Element.Right(az0) * R; var e20 = cen - Element.Right(az0 + 20 * Math.PI / 180) * R; var b = Go(e20, lineAz + 20, 500);
        Look(o, 2); w.View.SimClick(S(a)); w.View.SimClick(S(o));
        w.View.Mode = UtView.TorMode.Luk; w.View.SimClick(S(e20));
        w.View.Mode = UtView.TorMode.Prosta; w.View.SimClick(S(b)); w.View.SimClick(S(b), MouseButton.Right);
        var t1 = d.P.Tracks.Single(); w.View.Select(t1.Id);
        Type(Find<TextBox>(w.TrackPanel, _ => true), "1");
        Type(Find<TextBox>(w.TrackPanel, x => x.Text == "4,75"), "5");
        Click(BtnIn(w.TrackPanel, "← po lewej"));
        var t2 = d.P.Tracks.First(x => x.Id != t1.Id); w.View.Select(t2.Id); Type(Find<TextBox>(w.TrackPanel, _ => true), "2");
        w.SetTool(UtTool.Wskaz); w.View.Select(null); Click(w.SchematBtn);
        var r1 = w.View.Geometry(t1); var r2 = w.View.Geometry(t2);
        var luk = Ciecie.Kawalki(r1).FirstOrDefault(x => x.Luk);
        Ok(d.P.Tracks.Count == 2 && luk != null && luk.S0 < 515 && luk.S1 > 555 && Math.Abs(w.View.Geometry(t2).Length - r1.Length) < 5, $"tory 1 i 2 z łukiem R 300 (łuk km {luk?.S0:0}–{luk?.S1:0}) w Schemacie");

        // --- narzędzie Obiekt (I), rodzaje z listy
        void Kind(string key) => w.ObKindBox.SelectedIndex = Array.FindIndex(Obiekty.Kinds, x => x.Key == key);
        var names = new Queue<string>(); w.AskNameForTest = _ => names.Count > 0 ? names.Dequeue() : "";
        await Settle(w); KeyPress(w, Key.I);
        Ok(w.View.Tool == UtTool.Obiekt && w.LastMessage.StartsWith("most"), $"klawisz I – narzędzie Obiekt ({w.View.Tool}), opis na pasku: {w.LastMessage}");

        // wiadukt nad drogą na łuku: klik OD (km 520), klik DO (km 550)
        Kind(Obiekty.Wiadukt); names.Enqueue("WK-1");
        Look(r1.At(535).P, 0.3);
        w.View.SimClick(S(r1.At(520).P));
        Ok(w.View.ObFrom is var (ft, fs) && ft == t1.Id && Math.Abs(fs - 520) < 0.3 && d.P.Obiekty.Count == 0, "wiadukt: klik OD – czeka na DO, nic jeszcze nie wstawione");
        w.View.SimClick(S(r1.At(550).P));
        var wk = d.P.Obiekty.FirstOrDefault();
        Ok(wk != null && wk.Kind == Obiekty.Wiadukt && wk.Name == "WK-1" && wk.Track == t1.Id && Math.Abs(wk.S0 - 520) < 0.3 && Math.Abs(wk.S1 - 550) < 0.3 && wk.Width == 8
            && w.View.SelectedObj == (Kontrola.KObiekt, wk.Id) && d.UndoName == "Wiadukt kolejowy nad drogą", $"wiadukt WK-1 na łuku toru 1: km {wk?.S0:0.0}–{wk?.S1:0.0}, zaznaczony");
        Type(Find<TextBox>(w.TrackPanel, x => x.ToolTip as string == "np. ul. Piłsudskiego, rz. Ner, linia nr 17"), "ul. Testowa");
        Ok(d.P.Obiekty[0].Nad == "ul. Testowa" && Obiekty.Opis(d.P.Obiekty[0]) == "wiadukt kolejowy nad drogą WK-1 (ul. Testowa)", "panel: przeszkoda „ul. Testowa”");
        // Ctrl + klik na torze 2 – dołożenie toru
        var q2 = r2.At(r2.Nearest(r1.At(535).P).S).P;
        UtView.TestMods = ModifierKeys.Control; w.View.SimClick(S(q2)); UtView.TestMods = ModifierKeys.None;
        wk = d.P.Obiekty[0];
        var tor2 = wk.Tory?.FirstOrDefault();
        double e0 = r2.Nearest(r1.At(520).P).S, e1 = r2.Nearest(r1.At(550).P).S;
        Ok(wk.Tory?.Count == 1 && tor2.Track == t2.Id && Math.Abs(tor2.S0 - Math.Min(e0, e1)) < 0.05 && Math.Abs(tor2.S1 - Math.Max(e0, e1)) < 0.05 && d.UndoName == "Tor dołożony do obiektu",
            $"Ctrl + klik na torze 2 – tor dołożony do wiaduktu: km {tor2?.S0:0.0}–{tor2?.S1:0.0} (rzut końców)");
        UtView.TestMods = ModifierKeys.Control; w.View.SimClick(S(q2)); UtView.TestMods = ModifierKeys.None;
        Ok(d.P.Obiekty[0].Tory.Count == 1 && w.LastMessage.Contains("już należy"), "ponowny Ctrl + klik na torze 2 – „już należy do obiektu”");
        Ok(w.View.HitObj(S(Go(r1.At(535).P, lineAz + 90 + 10, 2.5))) is (Kontrola.KObiekt, _), "obrys wiaduktu obejmuje oba tory (klik między torami trafia w obiekt)");

        // most przeciągnięciem wzdłuż toru 1 (km 150 → 200)
        Kind(Obiekty.Most); names.Enqueue("M-2");
        Look(r1.At(175).P, 0.4); w.View.SimDrag(S(r1.At(150).P), S(r1.At(200).P));
        var most = d.P.Obiekty.LastOrDefault();
        Ok(most?.Kind == Obiekty.Most && Math.Abs(most.S0 - 150) < 0.3 && Math.Abs(most.S1 - 200) < 0.3 && most.Tory == null, $"most M-2 przeciągnięciem: km {most?.S0:0.0}–{most?.S1:0.0}");
        // tunel (dwa kliki km 900 → 800 – kolejność bez znaczenia), wiadukt nad torem km 600–615
        Kind(Obiekty.Tunel); names.Enqueue("T-1");
        Look(r1.At(850).P, 0.6); w.View.SimClick(S(r1.At(900).P)); w.View.SimClick(S(r1.At(800).P));
        var tun = d.P.Obiekty.Last();
        Ok(tun.Kind == Obiekty.Tunel && Math.Abs(tun.S0 - 800) < 0.3 && Math.Abs(tun.S1 - 900) < 0.3, $"tunel T-1: km {tun.S0:0.0}–{tun.S1:0.0} (kliki od końca)");
        UtView.TestMods = ModifierKeys.Control; w.View.SimClick(S(r2.At(r2.Nearest(r1.At(850).P).S).P)); UtView.TestMods = ModifierKeys.None;
        Ok(d.P.Obiekty.Last().Tory?.Count == 1, "tunel dwutorowy – tor 2 dołożony Ctrl + klikiem");
        Kind(Obiekty.WiaduktTor); names.Enqueue("WT-4");
        Look(r1.At(607).P, 0.3); w.View.SimClick(S(r1.At(600).P)); w.View.SimClick(S(r1.At(615).P));
        Ok(d.P.Obiekty.Last().Kind == Obiekty.WiaduktTor && d.P.Obiekty.Last().Len > 14.5, "wiadukt nad innym torem WT-4 (km 600–615)");

        // poprzeczne: przepust klikiem (prostopadle), przejście pod torami przeciągnięciem pod kątem 60°, kładka, wiadukt drogowy
        Kind(Obiekty.Przepust); names.Enqueue("P-3");
        Look(r1.At(300).P, 0.3); w.View.SimClick(S(r1.At(300).P));
        var prz = d.P.Obiekty.Last();
        Ok(prz.Kind == Obiekty.Przepust && Math.Abs(prz.S0 - 300) < 0.3 && prz.S0 == prz.S1 && prz.Angle == 90 && prz.Reach == 10 && prz.Width == 1.5, "przepust P-3 klikiem: km 300, prostopadle, ±10 m, światło 1,5 m");
        Kind(Obiekty.PrzejsciePod); names.Enqueue("PP-1");
        var pp = r1.At(650); Look(pp.P, 0.4);
        w.View.SimDrag(S(pp.P), S(pp.P + Element.Dir(pp.Az + 60 * Math.PI / 180) * (14 * k)));
        var ppo = d.P.Obiekty.Last();
        Ok(ppo.Kind == Obiekty.PrzejsciePod && Math.Abs(ppo.Angle - 60) <= 1 && Math.Abs(ppo.Reach - 14) <= 1, $"przejście pod torami przeciągnięciem: kąt {ppo.Angle:0}°, ±{ppo.Reach:0} m");
        Kind(Obiekty.Kladka); names.Enqueue("K-1");
        Look(r1.At(720).P, 0.4); w.View.SimClick(S(r1.At(720).P));
        Kind(Obiekty.WiaduktDrog); names.Enqueue("WD-1");
        Look(r1.At(380).P, 0.4); w.View.SimClick(S(r1.At(380).P));
        Ok(d.P.Obiekty.Count(x => !Obiekty.Wzdluz(x)) == 4, "kładka K-1 i wiadukt drogowy WD-1 klikiem");
        // mur oporowy po prawej toru 1 (km 220–280), ściana oporowa po lewej toru 2
        Kind(Obiekty.Mur); names.Enqueue("MO-1");
        Look(r1.At(250).P, 0.4); w.View.SimClick(S(r1.At(220).P));
        w.View.SimClick(S(r1.At(280).P + Element.Right(r1.At(280).Az) * (3 * k)));
        var mur = d.P.Obiekty.Last();
        Ok(mur.Kind == Obiekty.Mur && mur.Side == 1 && Math.Abs(mur.Len - 60) < 0.5, $"mur oporowy MO-1 po prawej toru 1: {mur.Len:0.0} m");
        Kind(Obiekty.Sciana); names.Enqueue("SO-2");
        var s2a = r2.Nearest(r1.At(220).P).S; var s2b = r2.Nearest(r1.At(280).P).S;
        w.View.SimClick(S(r2.At(s2a).P)); w.View.SimClick(S(r2.At(s2b).P - Element.Right(r2.At(s2b).Az) * (3 * k)));
        var sc = d.P.Obiekty.Last();
        Ok(sc.Kind == Obiekty.Sciana && sc.Track == t2.Id && sc.Side == -1, "ściana oporowa SO-2 po lewej toru 2");
        // PPM w narzędziu – rezygnacja z OD
        Kind(Obiekty.Most); w.View.SimClick(S(r1.At(940).P));
        w.View.SimClick(S(r1.At(940).P), MouseButton.Right);
        Ok(w.View.ObFrom == null && d.P.Obiekty.Count == 10, "PPM po kliku OD – od nowa, bez obiektu");
        w.AskNameForTest = null;

        // --- panel: rozdzielenie na tory i Ctrl+Z, przesunięcie myszą we Wskaż
        w.SetTool(UtTool.Wskaz); w.View.SelectObj(Kontrola.KObiekt, wk.Id);
        Ok(Find<TextBlock>(w.TrackPanel, x => x.Text.StartsWith("Tory obiektu:\ntor 1: km 520")) != null, "panel wiaduktu: lista torów z km");
        Click(BtnIn(w.TrackPanel, "Rozdziel na tory"));
        Ok(d.P.Obiekty.Count == 11 && d.P.Obiekty.Count(x => x.Name == "WK-1") == 2 && d.P.Obiekty.All(x => x.Name != "WK-1" || x.Tory == null), "„Rozdziel na tory” – dwa osobne wiadukty WK-1");
        w.DoUndo();
        Ok(d.P.Obiekty.Count == 10 && d.P.Obiekty.First(x => x.Id == wk.Id).Tory?.Count == 1, "Ctrl+Z – znowu jeden wiadukt pod dwoma torami");
        var hit = Go(r1.At(175).P, lineAz - 90, 0.5); Look(r1.At(175).P, 0.3);
        Ok(w.View.HitObj(S(hit)) == (Kontrola.KObiekt, most.Id), "most trafiany kliknięciem przy osi");
        w.View.SimDrag(S(hit), S(r1.At(185).P + (hit - r1.At(175).P)));
        var mm = d.P.Obiekty.First(x => x.Id == most.Id);
        Ok(Math.Abs(mm.S0 - 160) < 1 && Math.Abs(mm.Len - 50) < 1e-6 && d.UndoName == "Przesunięcie wzdłuż toru", $"most przesunięty myszą wzdłuż toru: km {mm.S0:0.0}");
        w.DoUndo();

        // --- „Sprawdź”: obiekty bez uwag; nakładanie; obiekt bez nazwy
        List<Uwaga> Ob(List<Uwaga> l) => l.Where(u => u.Kind == Kontrola.KObiekt).ToList();
        var c0 = Ob(w.RunCheck(true));
        Ok(c0.Count == 0, $"„Sprawdź”: 10 obiektów bez uwag ({string.Join(" | ", c0.Select(u => u.Text))})");
        d.Change("test: nakładanie", p => p.Obiekty.Add(new UtObiekt { Kind = Obiekty.Most, Name = "M-9", Track = t1.Id, S0 = 190, S1 = 230 }));
        var c1 = Ob(w.RunCheck(true));
        Ok(c1.Any(u => u.Error && u.Text.Contains("most M-2 i most M-9 nakładają się")) && !c1.Any(u => u.Text.Contains("MO-1") && u.Text.Contains("nakładają")), $"most na moście – błąd; mur obok mostu – bez uwag ({string.Join(" | ", c1.Select(u => u.Text))})");
        w.DoUndo();
        d.Change("test: bez nazwy", p => { var x = p.Obiekty.First(q => q.Name == "P-3"); x.Name = ""; });
        var c2 = Ob(w.RunCheck(true));
        Ok(c2.Count == 1 && !c2[0].Error && c2[0].Text == "przepust bez nazwy", "przepust bez nazwy – uwaga (nie błąd)");
        w.DoUndo();
        // rozjazd i urządzenia na obiekcie – bez uwag (decyzja właściciela 2026-10-22, pyt. 19)
        d.Change("test: semafor na moście", p => p.Devices.Add(new UtDevice { Kind = UtDevice.Semafor, Name = "X", Track = t1.Id, S = 170, Dir = 1, Side = 1 }));
        Ok(Ob(w.RunCheck(false)).Count == 0, "semafor na moście – bez uwag");
        w.DoUndo();

        // --- km po zmianie łuku (Reanchor): wiadukt zostaje w tym samym miejscu w terenie
        var w0 = r1.At(wk.S0).P; var w1 = r1.At(wk.S1).P; var tun0 = r1.At(tun.S0).P;
        d.Change("test: R 400", p => { var t = p.Tracks.First(x => x.Id == t1.Id); var old = Trasa.Compute(t); t.Vertices[1].R = 400; Naprawa.Reanchor(p, t, old); });
        var rn = w.View.Geometry(d.P.Tracks.First(x => x.Id == t1.Id)); var wkn = d.P.Obiekty.First(x => x.Id == wk.Id); var tunn = d.P.Obiekty.First(x => x.Id == tun.Id);
        // km z dawnego położenia: punkt obiektu na nowej osi = najbliższy punkt nowej osi do dawnego miejsca
        double Near(XY q) => XY.Dist(rn.At(rn.Nearest(q, 0.05).S).P, q);
        double dw = Math.Max(XY.Dist(rn.At(wkn.S0).P, w0), XY.Dist(rn.At(wkn.S1).P, w1)), dt = XY.Dist(rn.At(tunn.S0).P, tun0);
        double dn = Math.Max(Math.Abs(XY.Dist(rn.At(wkn.S0).P, w0) - Near(w0)), Math.Abs(XY.Dist(rn.At(wkn.S1).P, w1) - Near(w1)));
        Ok(dn < 0.05 && dt < 0.05 && Math.Abs(tunn.S0 - tun.S0) > 0.1 && wkn.Tory[0].S0 == tor2.S0, $"łuk R 300 → 400: km obiektów przeliczone – wiadukt na łuku {dw:0.00} m od dawnego miejsca = odsunięcie nowej osi łuku (różnica {dn:0.000} m), tunel za łukiem o {dt:0.000} m, km tunelu {tun.S0:0.0} → {tunn.S0:0.0}; tor 2 bez zmian");
        w.DoUndo();
        // cięcie toru 1 w km 700: tunel przechodzi na nowy tor z km − 700
        d.Change("test: cięcie", p => Ciecie.Split(p, t1.Id, 700));
        var nt = d.P.Tracks.FirstOrDefault(x => x.Id != t1.Id && x.Id != t2.Id); var tc = d.P.Obiekty.First(x => x.Id == tun.Id);
        Ok(nt != null && tc.Track == nt.Id && Math.Abs(tc.S0 - 100) < 0.05 && d.P.Obiekty.First(x => x.Id == wk.Id).Track == t1.Id, "cięcie toru 1 w km 700: tunel na nowym torze (km 100–200), wiadukt zostaje");
        w.DoUndo();

        // --- profil: teren z wąwozem pod wiaduktem (−8 m) i wzgórzem nad tunelem (+12 m); niweleta z terenu idzie prosto przez obiekty
        d.Change("test: teren", p =>
        {
            var t = p.Tracks.First(x => x.Id == t1.Id); var len = w.View.Geometry(t).Length;
            t.Terrain = new();
            for (double s = 0; s <= len + 1e-6; s += 10) t.Terrain.Add(new UtProfilePoint { S = s, Z = 200 + 0.002 * s + (s > 518 && s < 552 ? -8 : 0) + (s > 805 && s < 895 ? 12 : 0) });
        });
        w.View.Select(t1.Id);
        var pw = w.OpenProfile(false);
        pw.FromTerrain();
        var tt = d.P.Tracks.First(x => x.Id == t1.Id);
        double Dev(IReadOnlyList<UtProfilePoint> v) => new[] { 525.0, 535, 545, 820, 850, 880 }.Max(s => Math.Abs(Profil.Z(v, s) - (200 + 0.002 * s)));
        var bez = Profil.FromTerrain(tt.Terrain, 0.3, 100);
        Ok(Dev(tt.Profile) < 0.05 && Dev(bez) > 2 && pw.Status.Contains("wiadukt kolejowy nad drogą WK-1 (ul. Testowa)") && pw.Status.Contains("linia kolejowa w tunelu T-1"),
            $"niweleta z terenu: na wiadukcie i w tunelu odchyłka od linii 2 ‰ {Dev(tt.Profile):0.00} m (bez obiektów byłoby {Dev(bez):0.00} m); {pw.Status}");
        var bezRange = Obiekty.BezNmt(d.P, t1.Id);
        Ok(bezRange.Count == 4 && bezRange.Any(x => x.O.Kind == Obiekty.Most) && !bezRange.Any(x => x.O.Kind == Obiekty.Przepust || x.O.Kind == Obiekty.Mur), "bez NMT: most, wiadukt, wiadukt nad torem, tunel – przepust i mur nie");
        if (shot != null)
        {
            pw.Left = SystemParameters.VirtualScreenLeft - 3200; pw.Top = SystemParameters.VirtualScreenTop; pw.ShowActivated = false; pw.ShowInTaskbar = false; pw.Width = 1200; pw.Height = 600;
            pw.Show(); await Task.Delay(300); await w.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            Save(pw, Shot("profil"));
            pw.Close();
        }

        // --- warstwa „budowla”, usuwanie, zapis i odczyt
        Look(r1.At(175).P, 0.3);
        w.SetLayer("budowle", false);
        Ok(!w.View.Shown("budowle") && w.View.HitObj(S(hit)) == null, "warstwa „budowla” ukryta – obiektów nie widać i nie da się ich kliknąć");
        w.SetLayer("budowle", true);
        w.View.SelectObj(Kontrola.KObiekt, most.Id); w.DeleteSelection();
        Ok(d.P.Obiekty.Count == 9 && d.UndoName == "Usunięcie obiektu", "Del – most usunięty");
        w.DoUndo();
        Ok(w.SaveDoc(false), "zapis projektu");
        var l = UtDocument.Load(d.Path);
        Ok(l.P.Obiekty.Count == 10 && l.P.Obiekty.First(x => x.Id == wk.Id).Tory?.Count == 1 && UtDocument.Serialize(l.P) == UtDocument.Serialize(d.P), "zapis i odczyt: 10 obiektów, wiadukt z torem dołożonym – projekt identyczny");

        // --- nastawnia (Ig-10 zał. 8 s. 35–39): klik OD, klik DO po stronie budynku, panel, zmiana rodzaju, warstwa „budynek”
        w.AskNameForTest = _ => names.Count > 0 ? names.Dequeue() : "";
        Kind(Obiekty.NastawniaPrefix + "n_k"); names.Enqueue("Ab");
        Look(r1.At(466).P, 0.12);
        w.View.SimClick(S(r1.At(460).P)); w.View.SimClick(S(r1.At(472).P + Element.Right(r1.At(472).Az) * (8 * k)));
        w.AskNameForTest = null;
        var na = d.P.Obiekty.Last();
        Ok(na.Kind == "nastawnia_n_k" && na.Name == "Ab" && na.Side == 1 && Math.Abs(na.Len - 12) < 0.5 && na.Offset == Obiekty.NastawniaOdsun && na.Width == 6,
            $"nastawnia parterowa komputerowa Ab po prawej toru 1: {na.Len:0.0} m, {na.Offset:0} m od osi, głębokość {na.Width:0} m");
        var nm = r1.At(466); var wnetrze = nm.P + Element.Right(nm.Az) * ((na.Offset + 3) * k);
        w.SetTool(UtTool.Wskaz);
        Ok(w.View.HitObj(S(wnetrze)) == (Kontrola.KObiekt, na.Id) && w.View.HitObj(S(nm.P + Element.Right(nm.Az) * (2 * k))) != (Kontrola.KObiekt, na.Id),
            "klik w budynek nastawni trafia w nią, klik między torem a budynkiem – nie");
        w.View.SelectObj(Kontrola.KObiekt, na.Id);
        Ok(Find<TextBlock>(w.TrackPanel, x => x.Text == "Oznaczenie nastawni (Ig-10 zał. 1)") != null && Find<TextBlock>(w.TrackPanel, x => x.Text == "Przy czym (opis)") == null, "panel nastawni: oznaczenie wg Ig-10 zał. 1, bez pola „przy czym”");
        Type(Find<TextBox>(w.TrackPanel, x => x.Text == "6"), "10");
        Ok(d.P.Obiekty.Last().Width == 10 && d.UndoName == "Głębokość nastawni", "panel: głębokość budynku 10 m");
        var kb = Find<ComboBox>(w.TrackPanel, _ => true); kb.SelectedIndex = Array.FindIndex(Obiekty.Kinds, x => x.Key == Obiekty.NastawniaPrefix + "p_e");
        na = d.P.Obiekty.Last();
        Ok(na.Kind == "nastawnia_p_e" && Obiekty.Pietrowa(na.Kind) && Obiekty.Urzadzenia(na.Kind) == 'e' && na.Width == 10 && na.Offset == Obiekty.NastawniaOdsun, "zmiana rodzaju na piętrową elektryczną – wymiary zostają");
        Ok(!Obiekty.Check(d.P, Topologia.Routes(d.P)).Any(x => x.Id == na.Id), "„Sprawdź” – nastawnia bez uwag (nie nakłada się z murem ani przejściem)");
        w.SetLayer("budynek", false);
        Ok(w.View.HitObj(S(wnetrze)) == null && w.View.Shown("budowle"), "warstwa „budynek” ukryta – nastawni nie da się kliknąć, budowle zostają");
        w.SetLayer("budynek", true);
        Ok(w.SaveDoc(false) && UtDocument.Load(d.Path).P.Obiekty.Last() is var nl && nl.Kind == na.Kind && nl.Width == 10 && nl.Name == "Ab", "zapis i odczyt nastawni");

        // wiadukt drogowy z dwiema jezdniami na osobnych ustrojach (plan stacji właściciela, ul. Przybyszewskiego) – z panelu
        var wd = d.P.Obiekty.First(x => x.Name == "WD-1");
        w.View.SelectObj(Kontrola.KObiekt, wd.Id);
        // pole pod etykietą
        TextBox Pole(string etykieta)
        {
            var ch = w.TrackPanel.Children.Cast<UIElement>().ToList();
            int i = ch.FindIndex(x => x is TextBlock tb && tb.Text.StartsWith(etykieta));
            return i < 0 ? null : ch.Skip(i + 1).OfType<TextBox>().FirstOrDefault();
        }
        Type(Pole("Liczba ustrojów"), "2");
        Type(Pole("Szerokość jednego ustroju"), "8");
        Type(Pole("Odstęp między ustrojami"), "4");
        wd = d.P.Obiekty.First(x => x.Id == wd.Id);
        var wdp = r1.At(wd.S0); var miedzy = wdp.P; var wUstr = wdp.P + Element.Dir(wdp.Az) * ((wd.Width + wd.OdstepUstrojow) / 2 * k);
        Ok(wd.Ustroje == 2 && wd.Width == 8 && wd.OdstepUstrojow == 4 && w.View.HitObj(S(wUstr)) == (Kontrola.KObiekt, wd.Id),
            $"wiadukt drogowy WD-1: 2 ustroje po 8 m, odstęp 4 m – z panelu, klik na ustroju trafia w obiekt ({wd.Ustroje} × {wd.Width} m, odstęp {wd.OdstepUstrojow}; trafienie {w.View.HitObj(S(wUstr))}; pola: {string.Join(" | ", All<TextBox>(w.TrackPanel).Select(x => x.Text))})");
        var skw = Find<CheckBox>(w.TrackPanel, x => (x.Content as string)?.StartsWith("Skrzydełka wspólne") == true); skw.IsChecked = true; Click(skw);
        Ok(d.P.Obiekty.First(x => x.Id == wd.Id).SkrzydlaWspolne && d.UndoName == "Skrzydełka wiaduktu", "skrzydełka wspólne z panelu");
        Ok(UtDocument.Deserialize(UtDocument.Serialize(d.P)).Obiekty.First(x => x.Id == wd.Id) is var wl && wl.Ustroje == 2 && wl.OdstepUstrojow == 4 && wl.SkrzydlaWspolne, "zapis i odczyt ustrojów");

        // --- zrzuty do akceptacji znaków: całość, wiadukt na łuku pod dwoma torami, tunel i wiadukt nad torem, obiekty poprzeczne, mur, katalog
        if (shot != null)
        {
            w.SetTool(UtTool.Wskaz); w.View.Select(null);
            w.View.FitPoints(Eksport.Extent(d.P)); await Settle(w); Save(w, Shot("calosc"));
            Look(Go(r1.At(535).P, lineAz + 110, 2.5), 0.12); await Settle(w); Save(w, Shot("wiadukt"));
            Look(Go(r1.At(720).P, lineAz + 110, 2.5), 0.6); await Settle(w); Save(w, Shot("tunel_kladka"));
            Look(Go(r1.At(340).P, lineAz + 110, 2.5), 0.25); await Settle(w); Save(w, Shot("przepust_wiadukt_drog"));
            Look(Go(r1.At(215).P, lineAz + 110, 2.5), 0.3); await Settle(w); Save(w, Shot("most_mur"));
            Look(Go(r1.At(640).P, lineAz + 110, 2.5), 0.2); await Settle(w); Save(w, Shot("przejscie_wiadukt_tor"));
            w.SetSchemat(false); Look(r1.At(380).P, 0.08); await Settle(w); Save(w, Shot("wiadukt_dwa_ustroje")); w.SetSchemat(true);
            Look(Go(r1.At(466).P, lineAz + 90, 5), 0.1); await Settle(w); Save(w, Shot("nastawnia"));
            Look(Go(r1.At(466).P, lineAz + 90, 5), 0.6); await Settle(w); Save(w, Shot("nastawnia_daleko"));
            w.SetSchemat(false);
            Look(Go(r1.At(466).P, lineAz + 90, 10), 0.04); await Settle(w); Save(w, Shot("nastawnia_teren"));
            w.SetSchemat(true);
            var sw = w.OpenSymbols(); await w.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            Ok(sw.CardCount == Symbole.All.Count && Symbole.All.Count(x => x.Id.StartsWith("ob_")) == 19, $"katalog Symbole Ig-10: {Symbole.All.Count(x => x.Id.StartsWith("ob_"))} znaków obiektów inżynieryjnych");
            sw.Height = 1000; sw.Width = 1120; await Settle(w);
            if (Find<ScrollViewer>(sw, _ => true) is ScrollViewer sv) { sv.UpdateLayout(); sv.ScrollToEnd(); sv.UpdateLayout(); }
            await w.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            Save(sw, Shot("katalog")); sw.Close();
        }
    }
}
