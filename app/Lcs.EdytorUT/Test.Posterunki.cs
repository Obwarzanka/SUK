using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Test etapu Z0 (PLAN_EDYTOR_ZOBRAZOWANIA.md § 6) jak praca użytkownika: mijanka na skośnej linii w widoku Schemat (dwa łączniki,
/// kozły), semafory przeciągnięciem i ich funkcje z panelu, posterunek z zaznaczonych torów (Shift + prostokąt, „+ posterunek”),
/// dwa szlaki narzędziem Szlak (koniec toru na granicy obszaru – semafor wjazdowy), semafor odstępowy na szlaku, blokada i stawność
/// z panelu szlaku, „Sprawdź” do zera uwag, błędy celowe, nazwy w obrębie posterunku, zapis i odczyt.
/// </summary>
public static partial class Test
{
    static IEnumerable<T> All<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T t) yield return t;
        if (root is FrameworkElement fe) fe.ApplyTemplate();
        foreach (var c in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var x in All<T>(c)) yield return x;
    }

    static async Task RunPosterunki(MainWindow w, string shot)
    {
        const double lineAz = 63;
        var dir = Path.Combine(Path.GetTempPath(), "edytor_ut_test", "posterunki"); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
        w.SetSchemat(false);
        w.NewDoc(); w.SaveDoc(false, Path.Combine(dir, "mijanka_z0.ut.json"));
        var d = w.Doc;
        var o = Geo.ToPuwg92(51.7631, 19.5438); double k = Geo.ScaleFactor(o);
        XY Go(XY from, double azDeg, double len) => from + Element.Dir(azDeg * Math.PI / 180) * (len * k);
        Point S(XY p) => w.View.ToScreen(p);
        void Look(XY p, double mpp) => w.View.SetView(p, mpp);

        // --- mijanka: tor 1 (1200 m) klikami, tor 2 równoległy 5 m, łączniki 1/2 i 3/4, kozły na końcach toru 2
        w.SetTool(UtTool.Tor); w.View.Mode = UtView.TorMode.Prosta; w.NewRBox.Text = "300"; w.NewLBox.Text = "0";
        var a = Go(o, lineAz + 180, 600); var b = Go(o, lineAz, 600);
        Look(o, 2); w.View.SimClick(S(a)); w.View.SimClick(S(b)); w.View.SimClick(S(b), MouseButton.Right);
        var t1 = d.P.Tracks.Single(); w.View.Select(t1.Id);
        Type(Find<TextBox>(w.TrackPanel, _ => true), "1");
        Find<ComboBox>(w.TrackPanel, c => c.Items.Count == 3).SelectedIndex = 2;
        foreach (var txt in new[] { "początek – granica obszaru", "koniec – granica obszaru" })
        { var cb = Find<CheckBox>(w.TrackPanel, x => x.Content as string == txt); cb.IsChecked = true; cb.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); }
        Type(Find<TextBox>(w.TrackPanel, x => x.Text == "4,75"), "5");
        Click(BtnIn(w.TrackPanel, "← po lewej"));
        var t2 = d.P.Tracks.First(x => x.Id != t1.Id); w.View.Select(t2.Id); Type(Find<TextBox>(w.TrackPanel, _ => true), "2");
        Find<ComboBox>(w.TrackPanel, c => c.Items.Count == 3).SelectedIndex = 1;
        w.SetTool(UtTool.Wskaz); w.View.Select(null); Click(w.SchematBtn);
        var r1 = w.View.Geometry(t1); var r2 = w.View.Geometry(t2);
        Ok(t1.OpenStart && t1.OpenEnd && Math.Abs(r1.Length - 1200) < 0.01, "tor 1: 1200 m, oba końce na granicy obszaru (szlaki)");
        w.SetTool(UtTool.Rozjazd); w.RzTypeBox.SelectedItem = "Rz 60E1-190-1:9";
        var p1 = r1.At(400).P; Look(Go(p1, lineAz, 40), 0.5);
        w.View.SimDrag(S(p1), S(r2.At(r2.Nearest(Go(p1, lineAz, 60)).S).P));
        var p3 = r1.At(800).P; Look(Go(p3, lineAz + 180, 40), 0.5);
        w.View.SimDrag(S(p3), S(r2.At(r2.Nearest(Go(p3, lineAz + 180, 60)).S).P));
        Ok(d.P.Turnouts.Count == 4 && d.P.Tracks.Count == 4, $"łączniki 1/2 i 3/4 przeciągnięciem ({string.Join(", ", d.P.Turnouts.Select(x => x.Number))})");
        void Kind(string key) => w.DevKindBox.SelectedIndex = Array.FindIndex(UtDevice.Kinds, x => x.Key == key);
        Kind(UtDevice.Koziol);
        foreach (var s in new[] { 2.0, r2.Length - 2 }) { Look(r2.At(s).P, 0.4); w.View.SimClick(S(r2.At(s).P)); }
        Ok(d.P.Devices.Count(x => x.Kind == UtDevice.Koziol && x.Track == t2.Id) == 2, "kozły na obu końcach toru 2");

        // --- semafory przeciągnięciem: A, B wjazdowe, F wyjazdowy, S60 odstępowy na szlaku
        Kind(UtDevice.Semafor);
        void PlaceDir(UtTrack tr, double km, int dirr, int side)
        {
            var rr = w.View.Geometry(tr); var (pp, az) = rr.At(km); Look(pp, 0.4);
            var to = Go(pp, az * 180 / Math.PI + (dirr > 0 ? 0 : 180), 8) + Element.Right(az + (dirr > 0 ? 0 : Math.PI)) * (side * 1.5 * k);
            w.View.SimDrag(S(pp), S(to));
        }
        var names = new Queue<string>(new[] { "A", "B", "F", "S60" });
        w.AskNameForTest = _ => names.Dequeue();
        PlaceDir(t1, 150, 1, 1); PlaceDir(t1, 1050, -1, 1); PlaceDir(t2, 900, 1, 1); PlaceDir(t1, 60, 1, 1);
        w.AskNameForTest = null;
        UtDevice Dev(string n) => d.P.Devices.First(x => x.Name == n);
        Ok(d.P.Devices.Count(x => x.Kind == UtDevice.Semafor) == 4 && Dev("A").Dir == 1 && Dev("B").Dir == -1, "semafory A, B (wjazdowe), F, S60 przeciągnięciem");
        Ok(Kontrola.Run(d.P).Count(u => u.Text.Contains("funkcja semafora nie określona")) == 4, "„Sprawdź”: 4 semafory bez funkcji");
        void Funkcja(string n, string key)
        {
            w.SetTool(UtTool.Wskaz); w.View.SelectDevice(Dev(n).Id);
            var cb = Find<ComboBox>(w.TrackPanel, c => c.Items.Contains("wjazdowy"));
            cb.SelectedIndex = Array.FindIndex(UtDevice.Funkcje, f => f.Key == key) + 1;
        }
        Funkcja("A", UtDevice.FWjazdowy); Funkcja("B", UtDevice.FWjazdowy); Funkcja("F", UtDevice.FWyjazdowy); Funkcja("S60", UtDevice.FOdstepowy);
        Ok(Dev("A").Funkcja == UtDevice.FWjazdowy && Dev("F").Funkcja == UtDevice.FWyjazdowy && Dev("S60").Funkcja == UtDevice.FOdstepowy && d.UndoName == "Funkcja semafora",
            "funkcje z panelu urządzenia: A, B wjazdowe, F wyjazdowy, S60 odstępowy");
        Ok(!Kontrola.Run(d.P).Any(u => u.Text.Contains("funkcja semafora")), "„Sprawdź”: funkcje określone");

        // --- posterunek: Shift + prostokąt przez całą mijankę, „+ posterunek”
        w.View.FitPoints(new[] { a, b, Go(a, lineAz + 90, 5), Go(b, lineAz + 90, 5) });
        var all = new[] { a, b, Go(a, lineAz + 90, 5), Go(b, lineAz + 90, 5) }.Select(S).ToList();
        var q0 = new Point(all.Min(q => q.X) - 30, all.Min(q => q.Y) - 30); var q1 = new Point(all.Max(q => q.X) + 30, all.Max(q => q.Y) + 30);
        UtView.TestMods = ModifierKeys.Shift; w.View.SimDrag(q0, q1); UtView.TestMods = ModifierKeys.None;
        Ok(w.View.Multi.Count(x => x.Kind == Kontrola.KTor) == 4, $"Shift + prostokąt: 4 tory mijanki ({w.View.Multi.Count} elementów)");
        Ok(Find<ComboBox>(w.TrackPanel, c => c.Items.Contains("(brak – szlak / poza posterunkami)")) != null, "panel zaznaczenia: lista „Posterunek zaznaczonych torów”");
        var codes = new Queue<string>(new[] { "MJ", "Mijanka" });
        w.AskNameForTest = _ => codes.Dequeue();
        Click(Find<Button>((DependencyObject)w.PostExpander.Content, x => x.Content as string == "+ posterunek"));
        w.AskNameForTest = null;
        var post = d.P.Posts.SingleOrDefault();
        Ok(post != null && post.Kod == "MJ" && post.Nazwa == "Mijanka" && d.P.Tracks.All(t => t.Post == post.Id), "posterunek MJ Mijanka – wszystkie 4 tory przypisane");
        Ok(w.PostExpander.IsExpanded && w.View.ShowPosts && w.View.SelectedObj == (Kontrola.KPosterunek, post.Id) && Find<TextBlock>(w.TrackPanel, x => x.Text == "Posterunek MJ Mijanka") != null,
            "pasek „Posterunki i szlaki” rozwinięty, posterunek zaznaczony, jego panel");
        Ok(Find<TextBlock>(w.TrackPanel, x => x.Text.StartsWith("Tory (4)") && x.Text.Contains("Rozjazdy: 4, semafory: 4")) != null, "panel posterunku: 4 tory, 4 rozjazdy, 4 semafory");
        // panel toru: posterunek z listy; panel urządzenia: posterunek z toru
        w.View.Select(t2.Id);
        var pc = Find<ComboBox>(w.TrackPanel, c => c.Items.Contains("MJ Mijanka"));
        Ok(pc != null && pc.SelectedIndex == 1, "panel toru 2: posterunek MJ Mijanka");
        w.View.SelectDevice(Dev("F").Id);
        Ok(Find<TextBlock>(w.TrackPanel, x => x.Text == "Posterunek MJ Mijanka (z toru)") != null, "panel semafora F: posterunek z toru");
        // nazwy i numery powtarzają się między posterunkami, nie w obrębie posterunku
        Ok(Urzadzenia.NameTaken(d.P, "A", t2.Id) && Rozjazdy.NumberUsed(d.P, "1", null, t2.Id) && Urzadzenia.NextName(d.P, UtDevice.Semafor, t2.Id) == "C",
            "w posterunku MJ nazwa A i numer 1 zajęte, następny semafor – C");
        var tObcy = new UtTrack { Number = "9", Vertices = new() { new() { X = Go(a, lineAz + 90, 40).X, Y = Go(a, lineAz + 90, 40).Y }, new() { X = Go(b, lineAz + 90, 40).X, Y = Go(b, lineAz + 90, 40).Y } } };
        d.Change("Tor obcy", p => { p.Posts.Add(new UtPost { Kod = "XX", Nazwa = "Inny" }); tObcy.Post = p.Posts[^1].Id; p.Tracks.Add(tObcy); });
        Ok(!Urzadzenia.NameTaken(d.P, "A", tObcy.Id) && !Rozjazdy.NumberUsed(d.P, "1", null, tObcy.Id) && Urzadzenia.NextName(d.P, UtDevice.Semafor, tObcy.Id) == "A",
            "w innym posterunku nazwa A i numer 1 wolne");
        w.DoUndo();
        Ok(d.P.Posts.Count == 1 && d.P.Tracks.Count == 4, "Ctrl+Z – drugi posterunek i tor obcy znikają");

        // --- szlaki narzędziem Szlak (B)
        KeyPress(w, Key.B);
        Ok(w.View.Tool == UtTool.Szlak && w.LastMessage.StartsWith("Szlak:"), "B – narzędzie Szlak z opisem na pasku stanu");
        // ręcznie (Ctrl + klik): pierwszy koniec, Esc
        w.View.CtrlForTest = true;
        Look(r1.At(0).P, 0.4); w.View.SimClick(S(r1.At(0).P));
        Ok(w.View.SzlakFrom is { Track: var ft, AtStart: true } && ft == t1.Id && w.LastMessage.StartsWith("Szlak od: początek toru 1"), $"Ctrl + klik przy początku toru 1 – pierwszy koniec ręcznie ({w.LastMessage})");
        w.View.CtrlForTest = null;
        KeyPress(w, Key.Escape);
        Ok(w.View.SzlakFrom == null && w.View.Tool == UtTool.Szlak, "Esc – pierwszy koniec zdjęty, narzędzie zostaje");
        // kursor na torze szlaku – podgląd: początek toru 1 (granica obszaru) – semafor wjazdowy A, odstępowy S60
        Look(r1.At(75).P, 0.4); w.View.Drag(S(r1.At(75).P), false);
        var pv = w.View.SzlakPodglad;
        Ok(pv != null && pv.Err == null && pv.A?.Semafor != null != (pv.B?.Semafor != null) && pv.Droga.Semafory.Select(x => x.Name).SequenceEqual(new[] { "S60" }) && w.LastMessage.Contains("semafor A") && w.LastMessage.Contains("klik: utwórz"),
            $"kursor na torze szlaku – podgląd do semafora wjazdowego i granicy obszaru ({w.LastMessage})");
        if (shotRails != null) { w.View.SetView(r1.At(110).P, 0.35); w.View.Drag(S(r1.At(75).P), false); await Settle(w); Save(w, shotRails.Replace("_szyny.png", "_szlak_podglad.png")); }
        // w obrębie posterunku (między semaforami wjazdowymi) – bez propozycji
        Look(r1.At(600).P, 0.4); w.View.Drag(S(r1.At(600).P), false);
        Ok(w.View.SzlakPodglad?.Err?.Contains("w obrębie posterunku") == true, $"kursor w obrębie stacji – „{w.View.SzlakPodglad?.Err}”");
        w.View.SimClick(S(r1.At(600).P));
        Ok(d.P.Lines.Count == 0, "klik w obrębie stacji – szlak nie powstaje");
        w.AskNameForTest = _ => "1A";
        Look(r1.At(75).P, 0.4); w.View.SimClick(S(r1.At(75).P));
        var l1 = d.P.Lines.SingleOrDefault();
        var dr1 = l1 != null ? Szlaki.Droga(d.P, l1) : null;
        Ok(l1 != null && l1.Numer == "1A" && dr1.Err == null && dr1.Kawalki.Count == 1 && Math.Abs(dr1.Dlugosc - 150) < 0.05 && dr1.Semafory.Select(x => x.Name).SequenceEqual(new[] { "S60" }),
            $"szlak 1A: początek toru 1 – semafor A, 150 m, na szlaku S60 ({w.LastMessage})");
        Ok(w.View.SelectedObj == (Kontrola.KSzlak, l1?.Id) && Find<TextBlock>(w.TrackPanel, x => x.Text == "Szlak 1A") != null && Find<TextBlock>(w.TrackPanel, x => x.Text == "Semafory na szlaku: S60") != null,
            "panel szlaku 1A: droga, semafory na szlaku");
        // klik na torze istniejącego szlaku – zaznacza go, nowy nie powstaje
        w.View.Select(null); w.View.SimClick(S(r1.At(100).P));
        Ok(d.P.Lines.Count == 1 && w.View.SelectedObj == (Kontrola.KSzlak, l1?.Id), "klik na torze szlaku 1A – zaznaczony, bez nowego szlaku");
        w.AskNameForTest = _ => "2A";
        Look(r1.At(1120).P, 0.4); w.View.SimClick(S(r1.At(1120).P));
        w.AskNameForTest = null;
        var l2 = d.P.Lines.FirstOrDefault(x => x.Numer == "2A");
        var dr2 = l2 != null ? Szlaki.Droga(d.P, l2) : null;
        Ok(l2 != null && dr2.Err == null && Math.Abs(dr2.Dlugosc - 150) < 0.05 && dr2.Semafory.Count == 0, "szlak 2A: semafor B – koniec toru 1, 150 m, bez odstępów");
        // końce ręcznie: zwrot semaforów nie ma znaczenia – S60 i A działają w tę samą stronę, kolejność końców dowolna
        UtLineEnd SE(string n) => new() { Semafor = Dev(n).Id };
        var drS = Szlaki.Droga(d.P, new UtLine { Konce = new() { SE("S60"), SE("A") } });
        var drS2 = Szlaki.Droga(d.P, new UtLine { Konce = new() { SE("A"), SE("S60") } });
        var drK = Szlaki.Droga(d.P, new UtLine { Konce = new() { new() { Track = t1.Id, AtStart = false }, SE("B") } });
        Ok(Dev("S60").Dir == Dev("A").Dir && drS.Err == null && drS2.Err == null && Math.Abs(drS.Dlugosc - drS2.Dlugosc) < 0.05 && drS.Dlugosc > 1 && drK.Err == null && Math.Abs(drK.Dlugosc - 150) < 0.05,
            $"szlak ręcznie S60 – A i A – S60 (ten sam zwrot), koniec toru 1 – B: droga jest ({drS.Err ?? drS2.Err ?? drK.Err ?? $"{drS.Dlugosc:0} m"})");
        // uwagi do szlaków: brak posterunku sąsiedniego
        var ch = Kontrola.Run(d.P);
        Ok(ch.Count(u => u.Kind == Kontrola.KSzlak && u.Text.Contains("brak nazwy posterunku sąsiedniego")) == 2, "„Sprawdź”: na obu szlakach brak nazwy posterunku sąsiedniego");
        // panel szlaku: sąsiad, blokada, stawność
        w.SetTool(UtTool.Wskaz); w.View.SelectObj(Kontrola.KSzlak, l1.Id);
        var boxes = All<TextBox>(w.TrackPanel).ToList();     // oznaczenie, nazwa, skrócona, linia, tor, sąsiad (koniec 1), oznaczenie końca 1, oznaczenie końca 2
        Ok(boxes.Count == 8, $"panel szlaku: 8 pól, z nazwą pełną i skróconą ({boxes.Count})");
        var pst = d.P.Posts[0];
        Ok(l1.Nazwa == $"? – {pst.Nazwa}" && l1.NazwaSkr == $"? – {pst.Kod}", $"nazwy szlaku z posterunków przy tworzeniu: „{l1.Nazwa}” / „{l1.NazwaSkr}”");
        Type(boxes[3], "17"); Type(All<TextBox>(w.TrackPanel).ToList()[5], "Łódź Andrzejów");
        Ok(l1.Nazwa == $"Łódź Andrzejów – {pst.Nazwa}" && l1.NazwaSkr == $"ŁA – {pst.Kod}", $"posterunek sąsiedni wpisany – nazwy szlaku poszły za nim: „{l1.Nazwa}” / „{l1.NazwaSkr}”");
        Type(All<TextBox>(w.TrackPanel).ToList()[2], "ŁAn – MJ");
        Ok(l1.NazwaSkr == "ŁAn – MJ" && Find<Button>(w.TrackPanel, b => (b.Content as string)?.StartsWith("Zaproponuj:") == true) != null, "nazwa skrócona wpisana ręcznie, przycisk „Zaproponuj”");
        Ok(DoSymulatora.Buduj(d.P, null).Json["blocks"]?["1A"]?["name"]?.ToString() == l1.Nazwa, "nazwa szlaku w danych symulatora");
        Ok(l1.Linia == "17" && l1.Konce[0].Sasiad == "Łódź Andrzejów", "panel szlaku: linia 17, posterunek sąsiedni");
        Find<ComboBox>(w.TrackPanel, c => c.Items.Contains("PBL dwukierunkowa (półsamoczynna)")).SelectedIndex = 2;
        Ok(l1.Typ == UtLine.Pol, "panel szlaku: blokada PBL");
        Ok(Kontrola.Run(d.P).Any(u => u.Text.Contains("semafory odstępowe na szlaku bez SBL")), "„Sprawdź”: semafor odstępowy na szlaku z PBL – uwaga");
        Find<ComboBox>(w.TrackPanel, c => c.Items.Contains("PBL dwukierunkowa (półsamoczynna)")).SelectedIndex = 0;
        Find<ComboBox>(w.TrackPanel, c => c.Items.Contains("4-stawna")).SelectedIndex = 2;
        Ok(l1.Typ == UtLine.Sbl && l1.Stawnosc == 4, "SBL 4-stawna z panelu");
        // kilometraż linii (wymaga go „Sprawdź”)
        w.SetTool(UtTool.Km); w.AskNameForTest = _ => "17 10.000";
        var pk = r1.At(300).P; Look(pk, 0.5); w.View.SimDrag(S(pk), S(Go(pk, lineAz, 20)));
        w.AskNameForTest = null; w.SetTool(UtTool.Wskaz);
        Ok(d.P.KmPoints.Count == 1, "punkt kilometrażu linii 17 przeciągnięciem");
        w.View.SelectObj(Kontrola.KSzlak, l2.Id);
        Type(All<TextBox>(w.TrackPanel).ToList()[6], "Olechów");     // oznaczenie, nazwy (2), linia, tor, koniec 1 – semafor B (bez sąsiada), koniec 2 – tor
        Ok(l2.Nazwa == "Mijanka – Olechów" && l2.NazwaSkr == "MJ – O", $"szlak 2A: nazwy „{l2.Nazwa}” / „{l2.NazwaSkr}”");
        ch = Kontrola.Run(d.P);
        Ok(ch.Count == 0, "mijanka z posterunkiem i szlakami: „Sprawdź” bez uwag" + (ch.Count > 0 ? $" ({string.Join("; ", ch.Select(u => u.Text))})" : ""));
        // błędy celowe: odwrócony semafor wjazdowy, odstępowy poza szlakiem, wjazdowy jako wyjazdowy, tor bez posterunku
        w.View.SelectDevice(Dev("A").Id); Click(BtnIn(w.TrackPanel, "Odwróć kierunek"));
        Ok(Kontrola.Run(d.P).Any(u => u.Text.StartsWith("szlak 1A:")), $"A odwrócony – szlak 1A ma uwagę ({Kontrola.Run(d.P).FirstOrDefault(u => u.Text.StartsWith("szlak 1A"))?.Text})");
        w.DoUndo();
        Funkcja("B", UtDevice.FWyjazdowy);
        Ok(Kontrola.Run(d.P).Any(u => u.Text.Contains("funkcja semafora powinna być „wjazdowy”")), "B jako wyjazdowy na końcu szlaku – uwaga");
        w.DoUndo();
        w.View.Select(t2.Id); Find<ComboBox>(w.TrackPanel, c => c.Items.Contains("MJ Mijanka")).SelectedIndex = 0;
        Ok(d.P.Tracks.First(x => x.Id == t2.Id).Post == null && Kontrola.Run(d.P).Any(u => u.Text == "tor 2 nie należy do posterunku ani do szlaku"), $"tor 2 bez posterunku – uwaga ({string.Join("; ", Kontrola.Run(d.P).Select(u => u.Text))})");
        w.DoUndo();
        Ok(Kontrola.Run(d.P).Count == 0, $"Ctrl+Z ×3 – znowu bez uwag ({string.Join("; ", Kontrola.Run(d.P).Select(u => u.Text))})");

        // --- tarcza ostrzegawcza To1 przed semaforem A: „Powtarza” z propozycji, „Sprawdź”, eksport do symulatora (repeats)
        Kind(UtDevice.TarczaOstrz);
        w.AskNameForTest = _ => "To1"; PlaceDir(t1, 100, 1, 1); w.AskNameForTest = null;
        UtDevice To1() => d.P.Devices.FirstOrDefault(x => x.Name == "To1");
        var to1 = To1();
        Ok(to1 != null && to1.Kind == UtDevice.TarczaOstrz && to1.Dir == 1, "tarcza ostrzegawcza To1 przeciągnięciem (km 100, dla jazdy do A)");
        Ok(Kontrola.Run(d.P).Any(u => u.Text.Contains("To1") && u.Text.Contains("nie wskazano semafora powtarzanego")), "„Sprawdź”: To1 – nie wskazano semafora powtarzanego");
        Ok(Urzadzenia.Propozycja(d.P, to1)?.Name == "A", "propozycja dla To1: A (pierwszy semafor w kierunku działania; S60 leży za tarczą)");
        w.SetTool(UtTool.Wskaz); w.View.SelectDevice(to1.Id);
        var pcb = Find<ComboBox>(w.TrackPanel, c => c.Items.Contains("(nie wskazano)"));
        Ok(pcb != null && pcb.Items.Contains("semafor A (MJ)") && pcb.Items.Contains("szlak 1A") && !pcb.Items.Contains("semafor To1 (MJ)"), "panel To1: lista „Powtarza” – semafory i szlaki");
        Click(BtnIn(w.TrackPanel, "Zaproponuj"));
        Ok(To1().Powtarza == Dev("A").Id && d.UndoName == "Semafor powtarzany" && w.LastMessage.Contains("powtarza semafor A (MJ)"), $"„Zaproponuj” – To1 powtarza A ({w.LastMessage})");
        Ok(!Kontrola.Run(d.P).Any(), $"„Sprawdź” bez uwag z To1 ({string.Join("; ", Kontrola.Run(d.P).Select(u => u.Text))})");
        var wyn = DoSymulatora.Buduj(d.P, null);
        Ok(wyn.Json["signals"]?["MJ:To1"]?["repeats"]?.ToString() == "MJ:A" && wyn.Json["signals"]?["MJ:To1"]?["type"]?.ToString() == "to", "„Do symulatora”: MJ:To1 typ to, repeats MJ:A");
        // szlak z listy, potem usunięty semafor – uwaga
        Find<ComboBox>(w.TrackPanel, c => c.Items.Contains("(nie wskazano)")).SelectedItem = "szlak 1A";
        Ok(To1().Powtarza == l1.Id && DoSymulatora.Buduj(d.P, null).Json["signals"]?["MJ:To1"]?["repeats"]?.ToString() == "1A", "„Powtarza”: szlak 1A z listy → repeats 1A");
        w.DoUndo();
        d.Change("Usunięcie A (test)", q => q.Devices.RemoveAll(x => x.Name == "A"));
        Ok(Kontrola.Run(d.P).Any(u => u.Text.Contains("To1") && u.Text.Contains("usunięty")), "A usunięty – To1: semafor powtarzany usunięty");
        w.DoUndo();
        Ok(To1().Powtarza == Dev("A").Id && !Kontrola.Run(d.P).Any(), "Ctrl+Z – A wrócił, bez uwag");

        // --- zapis i odczyt
        Ok(w.SaveDoc(false), "zapis projektu z posterunkiem i szlakami");
        var back = UtDocument.Load(d.Path);
        Ok(back.P.Posts.Count == 1 && back.P.Lines.Count == 2 && back.P.Lines[0].Stawnosc == 4 && back.P.Devices.Count(x => x.Funkcja != null) == 4 && UtDocument.Serialize(back.P) == UtDocument.Serialize(d.P),
            "odczyt: posterunek, szlaki, funkcje semaforów bez zmian");
        if (shot != null)
        {
            w.View.SelectObj(Kontrola.KSzlak, l1.Id);
            w.View.FitPoints(new[] { a, b }); await Settle(w);
            Save(w, Path.Combine(Path.GetDirectoryName(Path.GetFullPath(shot))!, Path.GetFileNameWithoutExtension(shot) + "_posterunki.png"));
        }
        // usunięcie posterunku z panelu: tory zostają bez posterunku; Ctrl+Z
        w.View.SelectObj(Kontrola.KPosterunek, post.Id); Click(BtnIn(w.TrackPanel, "Usuń"));
        Ok(d.P.Posts.Count == 0 && d.P.Tracks.All(t => t.Post == null) && d.P.Tracks.Count == 4, "Usuń posterunek – tory zostają bez posterunku");
        w.DoUndo();
        Ok(d.P.Posts.Count == 1 && d.P.Tracks.All(t => t.Post == post.Id), "Ctrl+Z – posterunek i przypisania wróciły");
        w.SetTool(UtTool.Wskaz); w.View.Select(null); w.PostExpander.IsExpanded = false;
    }
}
