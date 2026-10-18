using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Lcs.Model;

namespace Lcs.EdytorZobrazowania;

/// <summary>
/// Test etapu Z2 (PLAN_EDYTOR_ZOBRAZOWANIA.md § 5) jak praca użytkownika: mijanka z UT narysowana klikami jak na pulpitach LCS
/// (wzory SimRail – tory co 96 j., łącznik „[”): tor 1 i tor 2 narzędziem Tor (wybór w panelu UT, punkt siatki, Shift – poziomo,
/// Ctrl – co 45°), łączniki 1/2 i 3/4 narzędziem Łącznik, zaznaczanie, kawałek toru (km od–do), przeciąganie wierzchołka, strzałki,
/// odwrócenie km, zdjęcie z obrazu, tor od węzła (kierunek km z UT), Miarka, zapis i odczyt.
/// </summary>
public static partial class Test
{
    static TreeViewItem Leaf(MainWindow w, string opis) => Leaves(w).FirstOrDefault(l => ((ZobUtObj)l.Tag).Opis.StartsWith(opis));
    static string Groups(MainWindow w) => string.Join("; ", w.UtTree.Items.OfType<TreeViewItem>().Select(g => g.Header as string));
    static string Pts(ZobTor t) => string.Join(" ", t.Pkt.Select(q => $"({q[0]},{q[1]})"));

    static async Task RunZ2(MainWindow w, string shot)
    {
        var dir = Path.Combine(Path.GetTempPath(), "edytor_zob_test", "z2"); Directory.CreateDirectory(dir);
        var ut = Path.Combine(dir, "mijanka.ut.json");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Testy", "mijanka.ut.json"), ut, true);
        if (File.Exists(ZobDocument.ZobPathFor(ut))) File.Delete(ZobDocument.ZobPathFor(ut));
        Ok(w.NewFromUt(ut), "Nowy z UT mijanki");
        await Settle(w); w.View.Fit();
        var o = w.Obraz; var d = w.Doc;
        Point S(int x, double y) => w.View.ToScreen(x, y);
        string Id(string opis) => ((ZobUtObj)Leaf(w, opis).Tag).Id;
        string t1 = Id("tor 1"), t2 = Id("tor 2"), l12 = Id("tor łączący 1/2"), l34 = Id("tor łączący 3/4");
        const ModifierKeys Shift = ModifierKeys.Shift;

        // --- następny punkt łamanej: siatka, Shift – poziomo, Ctrl – co 45°
        var k1 = ZobTory.Kierunek(new[] { 0, 0 }, 50, 46, 8, false); var k2 = ZobTory.Kierunek(new[] { 0, 0 }, 50, 46, 8, true);
        var k3 = ZobTory.Kierunek(new[] { 0, 0 }, 7, 61, 8, false); var k4 = ZobTory.Kierunek(new[] { 0, 0 }, 50, 30, 8, false, false);
        Ok(k1[0] == 48 && k1[1] == 48 && k2[0] == 48 && k2[1] == 0 && k3[0] == 0 && k3[1] == 64 && k4[0] == 48 && k4[1] == 32,
            "następny punkt: Ctrl – ukos (48,48) / pion (0,64), Shift – poziomo (48,0), bez klawisza – punkt siatki (48,32)");

        // --- tor 1 narzędziem Tor
        w.ToolKey(Key.T);
        Ok(w.View.Tool == ZobTool.Tor, "T – narzędzie Tor");
        w.View.SimClick(S(400, 300));
        Ok(o.Tory.Count == 0 && w.LastMessage.Contains("wybierz tor UT"), "klik bez wybranego toru UT – prośba o wybór");
        Ok(w.TorCandidates(null).Count == 4, "do narysowania 4 tory UT");
        Leaf(w, "tor 1").IsSelected = true;
        Ok(w.View.DraftUt == t1 && w.LastMessage.Contains("km 0–1200") && w.Preview.Highlight == t1, $"tor 1 wybrany w panelu UT – km 0–1200, podgląd UT ({w.LastMessage})");
        w.View.SimClick(S(96, 520)); w.View.SimClick(S(1824, 526));
        Ok(w.View.Draft.Count == 2 && w.View.Draft[1][0] == 1824 && w.View.Draft[1][1] == 528, "bez klawisza – punkt siatki (1824,528)");
        w.ToolKey(Key.Back);
        w.View.SimClick(S(1824, 526), MouseButton.Left, Shift);
        Ok(w.View.Draft.Count == 2 && w.View.Draft[1][1] == 520, "Backspace, z Shift – poziomo (1824,520)");
        w.View.SimClick(S(1824, 526), MouseButton.Right);
        var p1 = o.Tory.SingleOrDefault();
        Ok(p1 != null && p1.Ut.Tor == t1 && p1.Ut.S0 == 0 && Math.Abs(p1.Ut.S1 - 1200) < 0.01 && Pts(p1) == "(96,520) (1824,520)", $"PPM – tor 1 na obrazie: {(p1 == null ? "-" : Pts(p1))}");
        Ok(Leaf(w, "tor 1").Header as string == "tor 1 (gz) – na obrazie" && Groups(w).Contains("Tory (1/4)") && d.UndoName == "Tor obrazu", "panel UT: tor 1 na obrazie, Tory (1/4)");

        // --- tor 2 (odstęp 96 j. jak na pulpitach LCS): Enter kończy
        Leaf(w, "tor 2").IsSelected = true;
        w.View.SimClick(S(160, 616)); w.View.SimClick(S(1760, 619), MouseButton.Left, Shift);
        w.ToolKey(Key.Enter);
        var p2 = o.Tory.FirstOrDefault(t => t.Ut.Tor == t2);
        Ok(p2 != null && Pts(p2) == "(160,616) (1760,616)", $"Enter – tor 2: {(p2 == null ? "-" : Pts(p2))}");
        Leaf(w, "tor 1").IsSelected = true;
        Ok(w.View.DraftUt == null && w.LastMessage.Contains("już na obrazie w całości"), "tor 1 drugi raz – odmowa (ten sam km dwa razy)");

        // --- Backspace i Esc w trakcie rysowania
        Leaf(w, "tor łączący 1/2").IsSelected = true;
        w.View.SimClick(S(400, 400)); w.View.SimClick(S(480, 400)); w.ToolKey(Key.Back);
        Ok(w.View.Draft.Count == 1, "Backspace – cofnięty punkt");
        w.ToolKey(Key.Escape);
        Ok(w.View.Draft.Count == 0 && w.View.Tool == ZobTool.Tor && o.Tory.Count == 2, "Esc – rezygnacja z rysowania");

        // --- łączniki „[”: ukos na krok siatki – pion – ukos
        w.ToolKey(Key.R); w.View.Highlight = null;
        Ok(w.View.Tool == ZobTool.Lacznik, "R – narzędzie Łącznik");
        w.View.SimClick(S(600, 520)); w.View.SimClick(S(600, 522));
        Ok(w.LastMessage.Contains("innym torze"), "drugi punkt na tym samym torze – uwaga");
        w.View.SimClick(S(640, 615));
        var p12 = o.Tory.FirstOrDefault(t => t.Ut.Tor == l12);
        Ok(p12 != null && Pts(p12) == "(600,520) (608,528) (608,608) (616,616)", $"łącznik 1/2 w prawo: {(p12 == null ? w.LastMessage : Pts(p12))}");
        var z1 = d.Ut.Turnouts.First(z => z.Branch == l12 && z.Track == t1);
        Ok(p12 != null && (z1.BranchAtEnd ? p12.Ut.S0 > 0 : p12.Ut.S0 == 0), "łącznik 1/2: km od – od strony rozjazdu na torze 1");
        w.View.SimClick(S(1248, 520)); w.View.SimClick(S(1201, 614));
        var p34 = o.Tory.FirstOrDefault(t => t.Ut.Tor == l34);
        Ok(p34 != null && Pts(p34) == "(1248,520) (1240,528) (1240,608) (1232,616)", $"łącznik 3/4 w lewo (rozjazd km 800 wybrany z położenia): {(p34 == null ? w.LastMessage : Pts(p34))}");
        var all = ZobUt.Obiekty(d.Ut, o);
        Ok(Groups(w).Contains("Tory (4/4)") && all.Where(x => x.Grupa == ZobUt.GTory).All(x => x.NaObrazie), "mijanka: wszystkie 4 tory UT na obrazie");
        w.View.SimClick(S(800, 520)); w.View.SimClick(S(820, 616));
        Ok(o.Tory.Count == 4 && w.LastMessage.Contains("nie ma toru łączącego"), "trzeci łącznik między torami 1 i 2 – brak toru UT");

        // --- Wskaż: zaznaczenie, kawałek toru
        w.ToolKey(Key.V);
        w.View.SimClick(S(1000, 616));
        Ok(w.SelectedTor?.Id == p2.Id && w.KmFromBox?.Text == "0" && w.KmToBox?.Text == "1200" && w.Preview.Highlight == t2, "klik na torze 2 – zaznaczony, panel toru km 0–1200, podgląd UT");
        Type(w.KmToBox, "600");
        Ok(p2.Ut.S1 == 600 && Leaf(w, "tor 2").Header as string == "tor 2 (gd) – brak km 600–1200" && Groups(w).Contains("Tory (3/4)"), $"km do 600 – tor 2 w części ({Leaf(w, "tor 2").Header})");
        Type(w.KmToBox, "5000");
        Ok(p2.Ut.S1 == 600 && w.LastMessage.StartsWith("Km od – do"), "km poza torem – odmowa");
        Leaf(w, "tor 2").IsSelected = true;
        Ok(w.SelectedTor?.Id == p2.Id, "klik w panelu UT na torze na obrazie – zaznacza go");
        w.ToolKey(Key.T);
        Ok(w.View.DraftUt == t2 && w.LastMessage.Contains("km 600–1200"), "T przy torze 2 wybranym w panelu UT – od razu następny kawałek km 600–1200");
        w.ToolKey(Key.Escape); w.ToolKey(Key.Escape);
        Ok(w.View.Tool == ZobTool.Wskaz, "Esc ×2 – Wskaż");
        w.DoUndo();
        Ok(w.Obraz.Tory.First(t => t.Ut.Tor == t2).Ut.S1 == 1200, "Ctrl+Z – km 0–1200");

        // --- przeciąganie wierzchołka, strzałki, odwrócenie, usunięcie
        w.View.SimClick(S(1000, 616));
        w.View.SimDrag(S(1760, 616), S(1771, 618));
        p2 = w.Obraz.Tory.First(t => t.Ut.Tor == t2);
        Ok(Pts(p2) == "(160,616) (1768,616)", $"przeciągnięcie końca toru 2 – do siatki {Pts(p2)}");
        w.ToolKey(Key.Up);
        p2 = w.Obraz.Tory.First(t => t.Ut.Tor == t2);
        Ok(Pts(p2) == "(160,608) (1768,608)", "strzałka w górę – tor o krok siatki");
        w.DoUndo(); w.DoUndo();
        p2 = w.Obraz.Tory.First(t => t.Ut.Tor == t2);
        Ok(Pts(p2) == "(160,616) (1760,616)", "Ctrl+Z ×2 – jak przed przesunięciem");
        Click(Btn(w.ImgPanel, "Odwróć kierunek km"));
        p2 = w.Obraz.Tory.First(t => t.Ut.Tor == t2);
        Ok(p2.Ut.S0 == 1200 && p2.Ut.S1 == 0 && w.KmFromBox.Text == "1200", "Odwróć kierunek km – km od 1200");
        w.DoUndo();
        w.ToolKey(Key.Delete);
        Ok(w.Obraz.Tory.Count == 3 && w.SelectedTor == null && Groups(w).Contains("Tory (3/4)"), "Del – tor 2 zdjęty z obrazu");
        w.DoUndo();
        Ok(w.Obraz.Tory.Count == 4, "Ctrl+Z – tor 2 wrócił");

        // --- tor od węzła obrazu: najpierw tory połączone; kierunek km z UT
        w.View.SimClick(S(1240, 570));
        Ok(w.SelectedTor?.Ut.Tor == l34, "klik na łączniku 3/4 – zaznaczony");
        w.ToolKey(Key.Delete);
        var p1id = w.Obraz.Tory.First(t => t.Ut.Tor == t1).Id; var p2id = w.Obraz.Tory.First(t => t.Ut.Tor == t2).Id;
        var cands = w.TorCandidates(p1id);
        Ok(cands.Count == 1 && cands[0].Id == l34, $"węzeł na torze 1 – do wyboru: {string.Join(", ", cands.Select(c => c.Opis))}");
        w.SetTool(ZobTool.Tor);
        // od toru 2 (koniec łącznika 3/4 przy rozjeździe 4) – km od musi wyjść z UT, nie z kolejności klikania
        w.View.Actual(); w.View.CenterOn(1240, 568);      // przybliżenie 1:1 – przy 45 % punkt 9 j. od toru przyciąga się do jego linii
        Ok(w.StartTorAt(l34, new[] { 1232, 616 }, p2id), "tor łączący 3/4 od węzła na torze 2");
        w.View.SimClick(S(1240, 609), MouseButton.Left, ModifierKeys.Control);
        w.View.SimClick(S(1240, 529));
        w.View.SimClick(S(1249, 521));
        var p34b = w.Obraz.Tory.FirstOrDefault(t => t.Ut.Tor == l34);
        var z4 = d.Ut.Turnouts.First(z => z.Branch == l34 && z.Track == t2); double len34 = Trasa.Compute(d.Ut.Tracks.First(t => t.Id == l34)).Length;
        bool startAt0 = !z4.BranchAtEnd;      // rozjazd 4 ustawia początek łącznika – km 0 przy torze 2
        Ok(p34b != null && Pts(p34b) == "(1232,616) (1240,608) (1240,528) (1248,520)", $"Ctrl – ukos, klik na linii toru 1 kończy tor: {(p34b == null ? w.LastMessage : Pts(p34b))}");
        Ok(p34b != null && Math.Abs(p34b.Ut.S0 - (startAt0 ? 0 : len34)) < 0.01, $"km od z UT – koniec łącznika przy rozjeździe na torze 2 (km od {p34b?.Ut.S0:0.##})");

        w.View.Fit();

        // --- miarka
        w.ToolKey(Key.M);
        w.View.SimClick(S(96, 520)); w.View.SimClick(S(96, 616));
        Ok(w.View.Tool == ZobTool.Miarka && w.LastMessage.StartsWith("Miarka: 96 j. (12 kratek)"), $"Miarka: odstęp torów ({w.LastMessage})");
        w.View.SimClick(S(0, 0), MouseButton.Right);
        Ok(w.View.Miara.Count == 0, "PPM – miarka wyczyszczona");

        // --- zapis i odczyt
        Ok(w.SaveDoc(false), "zapis z torami");
        var zob = d.Path;
        var l = ZobDocument.Load(zob);
        Ok(l.P.Obrazy[0].Tory.Count == 4 && ZobDocument.Serialize(l.P) == ZobDocument.Serialize(w.Doc.P), "odczyt: 4 tory obrazu, identycznie");
        Ok(File.ReadAllText(zob).Contains("\"pkt\": [") && File.ReadAllText(zob).Contains("\"s1\": 1200"), "plik: łamane i km kawałków");

        w.SetTool(ZobTool.Wskaz);
        w.View.Select(w.Obraz.Tory.First(t => t.Ut.Tor == t2).Id);
        if (shot != null)
        {
            string Shot(string suf) => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(shot))!, Path.GetFileNameWithoutExtension(shot) + suf);
            w.View.Fit(); await Settle(w); Save(w, Shot("_tory.png"));
            w.View.Actual(); w.View.CenterOn(760, 568); await Settle(w); Save(w, Shot("_tory_1do1.png"));
        }
    }
}
