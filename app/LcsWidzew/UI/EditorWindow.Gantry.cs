using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LcsWidzew.Model;

namespace LcsWidzew.UI;

/// <summary>Konstrukcje sygnalizatorów na planie: bramki sygnałowe i wybór konstrukcji (tylko układ torowy – bez wpływu na zależności).</summary>
public partial class EditorWindow
{
    JsonObject GantriesOrNull => doc.Root["gantries"] as JsonObject;

    /// <summary>Panel semafora: konstrukcja (Ig-10 9.8) i bramka.</summary>
    void MountPanel(string sig)
    {
        if (doc.Signals[sig] is not JsonObject o || StationDoc.Sv(o["type"]) == "pk") return;
        props.Children.Add(T("Konstrukcja na planie (Ig-10, znaki 9.8) – bez wpływu na zależności", 11, Brushes.Gray));
        var cur = StationDoc.Sv(o["mount"]) ?? "";
        var ms = StationDoc.Mounts;
        var cb = new ComboBox { ItemsSource = ms.Select(x => x.Text).ToList(), SelectedIndex = Math.Max(0, Array.FindIndex(ms, x => x.Val == cur)), Margin = new Thickness(0, 2, 0, 4) };
        var root = doc.Root;
        cb.SelectionChanged += (a, e) => Safe(() =>
        {
            if (cb.SelectedIndex < 0 || !ReferenceEquals(root, doc.Root)) return;
            var val = ms[cb.SelectedIndex].Val; if (val == cur) return;
            if (val == "bramka")
            {
                var gs = GantriesOrNull;
                if (gs == null || gs.Count == 0) { status.Text = "Najpierw postaw bramkę narzędziem „⊤ Bramka” (U) – przeciągnij w poprzek torów"; ShowProps(); return; }
                var (nx, ny) = doc.Pos(StationDoc.Sv(o["node"]));
                var near = gs.OrderBy(g => Math.Abs(StationDoc.D(g.Value["x"]) - nx) + (ny < StationDoc.D(g.Value["y0"]) || ny > StationDoc.D(g.Value["y1"]) ? 1e4 : 0)).First().Key;
                doc.Snapshot(); bool moved = doc.AttachToGantry(sig, near);
                Changed($"Sygnalizator {Station.Lbl(sig)} na bramce {Station.Lbl(near)}" + (moved ? "" : " (nie dosunięty – między nim a bramką jest inny węzeł; przesuń ręcznie)"));
                return;
            }
            doc.Snapshot(); doc.SetMount(sig, val);
            Changed($"Sygnalizator {Station.Lbl(sig)}: {ms[cb.SelectedIndex].Text}");
        });
        props.Children.Add(cb);
        if (cur == "bramka" && GantriesOrNull is { Count: > 0 } all)
        {
            var keys = all.Select(g => g.Key).ToList(); var gid = StationDoc.Sv(o["gantry"]);
            props.Children.Add(T("Bramka", 11, Brushes.Gray));
            var gb = new ComboBox { ItemsSource = keys.Select(Station.Lbl).ToList(), SelectedIndex = keys.IndexOf(gid ?? ""), Margin = new Thickness(0, 2, 0, 4) };
            gb.SelectionChanged += (a, e) => Safe(() =>
            {
                if (gb.SelectedIndex < 0 || !ReferenceEquals(root, doc.Root) || keys[gb.SelectedIndex] == gid) return;
                doc.Snapshot(); bool moved = doc.AttachToGantry(sig, keys[gb.SelectedIndex]);
                Changed($"Sygnalizator {Station.Lbl(sig)} na bramce {Station.Lbl(keys[gb.SelectedIndex])}" + (moved ? "" : " (nie dosunięty – przesuń ręcznie)"));
            });
            props.Children.Add(gb);
        }
    }

    /// <summary>Panel bramki: położenie (przesuwa przypięte sygnalizatory), lista przypiętych, przypinanie w pobliżu.</summary>
    void GantryPanel(string gid)
    {
        var g = (JsonObject)doc.Gantries[gid];
        props.Children.Add(T("Położenie X (przesuwa przypięte sygnalizatory)", 11, Brushes.Gray));
        var tb = Box(StationDoc.D(g["x"]).ToString("0.#", CultureInfo.InvariantCulture)); var root = doc.Root; bool done = false;
        void Commit()
        {
            if (done || !ReferenceEquals(root, doc.Root)) return;
            if (!double.TryParse(tb.Text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var x)) { status.Text = "✗ wpisz liczbę"; return; }
            if (Math.Abs(x - StationDoc.D(g["x"])) < 0.05) return;
            done = true; doc.Snapshot(); int stuck = doc.MoveGantry(gid, x);
            Changed($"Bramka {Station.Lbl(gid)} przesunięta" + (stuck > 0 ? $" – {stuck} sygnalizatorów nie dosunięto (przeskoczyłyby sąsiedni węzeł)" : ""));
        }
        tb.KeyDown += (a, e) => { if (e.Key == Key.Enter) Safe(Commit); };
        tb.LostFocus += (a, e) => Dispatcher.BeginInvoke(() => Safe(Commit));
        props.Children.Add(tb);
        var sigs = doc.GantrySignals(gid).ToList();
        props.Children.Add(T(sigs.Count == 0 ? "Brak przypiętych sygnalizatorów." : "Na bramce: " + string.Join(", ", sigs.Select(Station.Lbl)), 11, Brushes.LightGray));
        props.Children.Add(Btn("Przypnij sygnalizatory w pobliżu", (a, e) => Safe(() =>
        {
            doc.Snapshot(); var (att, nm) = doc.AttachNear(gid, Math.Max(12, 10 / panel.Scale));
            Changed($"Przypięto {att}" + (nm > 0 ? $", {nm} nie dosunięto" : ""));
        })));
        props.Children.Add(T("Del – usuń bramkę (sygnalizatory wracają na maszt). Konstrukcja jest tylko na planie – zależności i pulpit bez zmian.", 10.5, Brushes.Gray));
    }

    /// <summary>Test (--editshot … bramka out.json): bramka narzędziem, przypięcie, konstrukcje, przesunięcie, usunięcie, cofnięcie; przebiegi bez zmian.</summary>
    public string GantryForTest(string outJson)
    {
        var log = new System.Text.StringBuilder(); FuzzMode = true; int bad = 0;
        void Ok(bool c, string m) { log.AppendLine((c ? "OK  " : "BŁĄD") + " " + m); if (!c) bad++; }
        PlanForTest();
        string Table() { var sim = new LcsWidzew.Sim.Simulator(doc.Build()); return string.Join("|", sim.RouteTable.OrderBy(x => x.Key).SelectMany(x => x.Value.Select(r => r.Id + ":" + string.Join(",", r.Secs) + ":" + r.SwKey + ":" + r.FlankText))); }
        var t0 = Table(); string before = doc.Root.ToJsonString();
        // kolumna sygnalizatorów: semafor wyjazdowy, przy którym w pionie stoją inne sygnalizatory (± 12 j.)
        var sigs = st.Signals.Values.Where(s => s.Type != "pk").ToList();
        var col = sigs.Where(s => s.Type == "wyjazdowy").Select(s => (s, near: sigs.Where(q => Math.Abs(st.Nodes[q.Node].X - st.Nodes[s.Node].X) <= 8).ToList()))
            .OrderByDescending(x => x.near.Count).First();
        double gx = st.Nodes[col.s.Node].X, y0 = col.near.Min(q => st.Nodes[q.Node].Y) - 12, y1 = col.near.Max(q => st.Nodes[q.Node].Y) + 12;
        log.AppendLine($"bramka przy {Station.Lbl(col.s.Name)}: x = {gx:0.#}, sygnalizatory w kolumnie: {string.Join(", ", col.near.Select(q => Station.Lbl(q.Name)))}");
        postBox.SelectedItem = col.s.Post;
        SetTool(Tool.Gantry); AutoAnswers.Enqueue(new[] { "B9" });
        Down(new Point(gx, y0), null); Up(new Point(gx, y1), null);
        var gid = $"{Post}:B9";
        Ok(GantriesOrNull?.ContainsKey(gid) == true, "bramka postawiona narzędziem");
        var on = doc.GantrySignals(gid).ToList();
        Ok(on.Contains(col.s.Name) && on.Count >= 1, $"przypięte: {string.Join(", ", on.Select(Station.Lbl))}");
        Ok(on.All(k => Math.Abs(doc.Pos(StationDoc.Sv(doc.Signals[k]["node"])).X - StationDoc.D(doc.Gantries[gid]["x"])) < 0.06), "sygnalizatory dosunięte do bramki");
        Ok(Table() == t0, "tablica przebiegów (odcinki, zwrotnice, ochrona boczna) bez zmian");
        doc.Snapshot(); int stuck = doc.MoveGantry(gid, StationDoc.D(doc.Gantries[gid]["x"]) + 2); Changed();
        Ok(Table() == t0, $"przesunięcie bramki o 2 j. (niedosuniętych {stuck}) – przebiegi bez zmian");
        Select("gantry", gid); Safe(DeleteSel);
        Ok(GantriesOrNull?.ContainsKey(gid) != true && on.All(k => doc.Signals[k]["mount"] == null), "Del: bramka usunięta, sygnalizatory na maszcie");
        while (doc.Root.ToJsonString() != before && doc.Undo()) { }
        Rebuild();
        Ok(doc.Root.ToJsonString() == before, "Ctrl+Z przywraca stan sprzed bramki");
        // pokaz: bramka i różne konstrukcje obok siebie
        SetTool(Tool.Gantry); AutoAnswers.Enqueue(new[] { "B9" });
        Down(new Point(gx, y0), null); Up(new Point(gx, y1), null);
        var others = sigs.Where(s => !doc.GantrySignals(gid).Contains(s.Name) && Math.Abs(st.Nodes[s.Node].Y - (y0 + y1) / 2) < (y1 - y0)).OrderBy(s => Math.Abs(st.Nodes[s.Node].X - gx)).Take(4).ToList();
        string[] mounts = { "wysiegnik", "nad", "karzel", "" };
        doc.Snapshot(); for (int i = 0; i < others.Count; i++) doc.SetMount(others[i].Name, mounts[i]); Changed();
        log.AppendLine("konstrukcje: " + string.Join(", ", others.Select((s, i) => $"{Station.Lbl(s.Name)} – {StationDoc.Mounts.First(m => m.Val == mounts[i]).Text}")));
        Ok(Table() == t0, "konstrukcje bez wpływu na przebiegi");
        doc.Save(outJson);
        var re = Station.Load(outJson);
        Ok(re.Gantries.ContainsKey(gid) && re.Signals[col.s.Name].Mount == "bramka" && re.Signals[col.s.Name].Gantry == gid, "zapis i wczytanie (bramka, konstrukcja semafora)");
        SetTool(Tool.Select); Select("gantry", gid);
        var c0 = st.Nodes[col.s.Node]; ZoomForTest(c0.X, c0.Y, 3);
        log.AppendLine($"błędów testu: {bad}, błędów edytora: {Errors.Count}");
        return log.ToString();
    }
}
