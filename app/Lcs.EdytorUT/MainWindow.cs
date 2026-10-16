using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Lcs.Model;
using Microsoft.Win32;

namespace Lcs.EdytorUT;

/// <summary>
/// Okno edytora układu torowego: pasek poleceń, płótno w PUWG 1992, panel właściwości, pasek stanu.
/// </summary>
public class MainWindow : Window
{
    public UtDocument Doc { get; private set; }
    public readonly UtView View = new();
    /// <summary>Bez okien dialogowych (testy).</summary>
    public bool NoPrompt;

    public readonly TextBox GoToBox = new() { Width = 230, Margin = new Thickness(4, 0, 2, 0), VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "Współrzędne z Google Maps (szerokość, długość – np. 51.7631, 19.5438) albo X Y w PUWG 1992 (najpierw X – północ). Enter – idź." };
    public readonly Button GoToBtn = new() { Content = "Idź do" };
    public readonly TextBox NameBox = new() { Margin = new Thickness(0, 2, 0, 8) };
    public readonly TextBox NotesBox = new() { Margin = new Thickness(0, 2, 0, 8), AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 90, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    public readonly ComboBox ScaleBox = new() { Margin = new Thickness(0, 2, 0, 8) };
    public readonly Button UndoBtn = new() { Content = "↶ Cofnij" }, RedoBtn = new() { Content = "↷ Ponów" };
    readonly CheckBox gridBox = new() { Content = "Siatka", IsChecked = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
    readonly TextBlock stCursor = new() { FontFamily = new FontFamily("Consolas"), MinWidth = 520 }, stView = new() { MinWidth = 230 }, stMsg = new();
    bool refreshing;

    public MainWindow(string path)
    {
        Width = 1500; Height = 900; WindowState = WindowState.Maximized;
        FontFamily = new FontFamily("Segoe UI"); FontSize = 13;
        Background = new SolidColorBrush(Color.FromRgb(0xf3, 0xf3, 0xf0));

        var root = new DockPanel();
        root.Children.Add(Dock(BuildToolbar(), System.Windows.Controls.Dock.Top));
        root.Children.Add(Dock(BuildStatus(), System.Windows.Controls.Dock.Bottom));
        root.Children.Add(Dock(BuildPanel(), System.Windows.Controls.Dock.Right));
        root.Children.Add(View);
        Content = root;

        View.CursorMoved += ShowCursor;
        View.ViewChanged += ShowView;
        gridBox.Click += (s, e) => { View.ShowGrid = gridBox.IsChecked == true; View.InvalidateVisual(); };
        PreviewKeyDown += OnKey;
        Closing += OnClosing;

        UtDocument doc = null;
        if (path != null)
        {
            try { doc = UtDocument.Load(path); }
            catch (Exception ex) { Message($"Nie można otworzyć {Path.GetFileName(path)}: {ex.Message}"); }
        }
        SetDoc(doc ?? new UtDocument());
    }

    static UIElement Dock(UIElement e, System.Windows.Controls.Dock d) { DockPanel.SetDock(e, d); return e; }

    // ------------------------------------------------------------------ budowa okna
    UIElement BuildToolbar()
    {
        var tb = new ToolBar { Background = new SolidColorBrush(Color.FromRgb(0xe6, 0xe6, 0xe2)) };
        ToolBarTray.SetIsLocked(tb, true);
        tb.Items.Add(Btn("Nowy", (s, e) => NewDoc(), "Nowy projekt układu torowego (Ctrl+N)"));
        tb.Items.Add(Btn("Otwórz…", (s, e) => OpenDoc(), "Otwórz projekt *.ut.json (Ctrl+O)"));
        tb.Items.Add(Btn("Zapisz", (s, e) => SaveDoc(false), "Zapisz (Ctrl+S)"));
        tb.Items.Add(Btn("Zapisz jako…", (s, e) => SaveDoc(true), "Zapisz pod nową nazwą (Ctrl+Shift+S)"));
        tb.Items.Add(new Separator());
        UndoBtn.Click += (s, e) => DoUndo(); RedoBtn.Click += (s, e) => DoRedo();
        tb.Items.Add(UndoBtn); tb.Items.Add(RedoBtn);
        tb.Items.Add(new Separator());
        tb.Items.Add(new TextBlock { Text = "Punkt:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) });
        GoToBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) { GoTo(GoToBox.Text); e.Handled = true; } };
        GoToBtn.Click += (s, e) => GoTo(GoToBox.Text);
        tb.Items.Add(GoToBox); tb.Items.Add(GoToBtn);
        tb.Items.Add(new Separator());
        tb.Items.Add(gridBox);
        return tb;
    }

    static Button Btn(string text, RoutedEventHandler click, string tip)
    {
        var b = new Button { Content = text, ToolTip = tip, Padding = new Thickness(6, 2, 6, 2) };
        b.Click += click; return b;
    }

    UIElement BuildPanel()
    {
        var sp = new StackPanel { Margin = new Thickness(10) };
        sp.Children.Add(Header("Obszar"));
        sp.Children.Add(Label("Nazwa"));
        NameBox.LostFocus += (s, e) => CommitName();
        NameBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) { CommitName(); e.Handled = true; } };
        sp.Children.Add(NameBox);
        sp.Children.Add(Label("Układ współrzędnych"));
        sp.Children.Add(new TextBlock { Text = "PUWG 1992 (EPSG:2180)\nX – na północ, Y – na wschód, Z – wysokość [m]", Margin = new Thickness(0, 2, 0, 8), Foreground = Brushes.DimGray, TextWrapping = TextWrapping.Wrap });
        sp.Children.Add(Label("Skala rysunku (wielkość symboli Ig-10, wydruk)"));
        foreach (var k in UtProject.DrawingScales) ScaleBox.Items.Add($"1:{k}");
        ScaleBox.SelectionChanged += (s, e) =>
        {
            if (refreshing || ScaleBox.SelectedIndex < 0) return;
            int k = UtProject.DrawingScales[ScaleBox.SelectedIndex];
            Doc.Change("Skala rysunku", p => p.DrawingScale = k);
        };
        sp.Children.Add(ScaleBox);
        sp.Children.Add(new TextBlock { Text = "Geometria zawsze w metrach terenu – skala rysunku jej nie zmienia.", Foreground = Brushes.DimGray, TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new Thickness(0, -4, 0, 8) });
        sp.Children.Add(Label("Uwagi"));
        NotesBox.LostFocus += (s, e) => { if (!refreshing && NotesBox.Text != Doc.P.Notes) Doc.Change("Uwagi", p => p.Notes = NotesBox.Text); };
        sp.Children.Add(NotesBox);
        return new Border
        {
            Width = 290, Child = new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xc8, 0xc8, 0xc2)), BorderThickness = new Thickness(1, 0, 0, 0),
        };
    }

    static TextBlock Header(string t) => new() { Text = t, FontWeight = FontWeights.SemiBold, FontSize = 15, Margin = new Thickness(0, 0, 0, 8) };
    static TextBlock Label(string t) => new() { Text = t, Foreground = Brushes.Black, Margin = new Thickness(0, 2, 0, 0) };

    UIElement BuildStatus()
    {
        var sb = new StatusBar { Background = new SolidColorBrush(Color.FromRgb(0xe6, 0xe6, 0xe2)) };
        sb.Items.Add(new StatusBarItem { Content = stCursor });
        sb.Items.Add(new Separator());
        sb.Items.Add(new StatusBarItem { Content = stView });
        sb.Items.Add(new Separator());
        sb.Items.Add(new StatusBarItem { Content = stMsg });
        return sb;
    }

    // ------------------------------------------------------------------ dokument
    void SetDoc(UtDocument d)
    {
        if (Doc != null) Doc.Changed -= Refresh;
        Doc = d; View.Doc = d;
        d.Changed += Refresh;
        var v = d.P.View;
        View.SetView(new XY(v.X, v.Y), v.Mpp);
        Refresh();
    }

    /// <summary>Panel, tytuł i przyciski zgodne z dokumentem (po każdej zmianie, cofnięciu, zapisie).</summary>
    void Refresh()
    {
        refreshing = true;
        try
        {
            // pola zatwierdzane przy wyjściu z pola – Changed nie przychodzi w trakcie pisania
            NameBox.Text = Doc.P.Name;
            NotesBox.Text = Doc.P.Notes;
            ScaleBox.SelectedIndex = Array.IndexOf(UtProject.DrawingScales, Doc.P.DrawingScale);
        }
        finally { refreshing = false; }
        Title = $"{(Doc.Dirty ? "* " : "")}{Doc.P.Name} – {(Doc.Path != null ? Path.GetFileName(Doc.Path) : "(nie zapisano)")} – Edytor układu torowego";
        UndoBtn.IsEnabled = Doc.UndoName != null; UndoBtn.ToolTip = Doc.UndoName != null ? $"Cofnij: {Doc.UndoName} (Ctrl+Z)" : "Nic do cofnięcia";
        RedoBtn.IsEnabled = Doc.RedoName != null; RedoBtn.ToolTip = Doc.RedoName != null ? $"Ponów: {Doc.RedoName} (Ctrl+Y)" : "Nic do ponowienia";
        View.InvalidateVisual();
    }

    void CommitName()
    {
        if (refreshing) return;
        var n = NameBox.Text.Trim();
        if (n.Length == 0) { NameBox.Text = Doc.P.Name; return; }
        Doc.Change("Nazwa obszaru", p => p.Name = n);
    }

    public void DoUndo() { var n = Doc.UndoName; if (Doc.Undo()) Message("Cofnięto: " + n); }
    public void DoRedo() { var n = Doc.RedoName; if (Doc.Redo()) Message("Ponowiono: " + n); }

    /// <summary>Widok zapisywany w pliku (bez cofania i bez oznaczania zmian).</summary>
    void StoreView() { var v = Doc.P.View; v.X = View.Center.X; v.Y = View.Center.Y; v.Mpp = View.Mpp; }

    bool ConfirmDiscard()
    {
        if (!Doc.Dirty || NoPrompt) return true;
        var r = MessageBox.Show(this, $"Zapisać zmiany w „{Doc.P.Name}”?", "Edytor układu torowego", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (r == MessageBoxResult.Cancel) return false;
        return r == MessageBoxResult.No || SaveDoc(false);
    }

    public void NewDoc()
    {
        if (!ConfirmDiscard()) return;
        var d = new UtDocument();
        // nowy projekt w miejscu bieżącego widoku – zwykle obok poprzedniego obszaru
        d.P.View = new UtViewState { X = View.Center.X, Y = View.Center.Y, Mpp = View.Mpp };
        SetDoc(d); Message("Nowy projekt");
    }

    void OpenDoc()
    {
        if (!ConfirmDiscard()) return;
        var dlg = new OpenFileDialog { Filter = "Układ torowy (*.ut.json)|*.ut.json|Wszystkie pliki|*.*", InitialDirectory = ProjectsDir() };
        if (dlg.ShowDialog(this) != true) return;
        OpenPath(dlg.FileName);
    }

    public bool OpenPath(string path)
    {
        try { SetDoc(UtDocument.Load(path)); Message("Otwarto " + Path.GetFileName(path)); return true; }
        catch (Exception ex) { Message("Nie można otworzyć: " + ex.Message); if (!NoPrompt) MessageBox.Show(this, ex.Message, "Otwieranie projektu"); return false; }
    }

    public bool SaveDoc(bool askName, string path = null)
    {
        path ??= Doc.Path;
        if (path == null || askName)
        {
            if (NoPrompt) return false;
            var dlg = new SaveFileDialog { Filter = "Układ torowy (*.ut.json)|*.ut.json", InitialDirectory = ProjectsDir(), FileName = SafeName(Doc.P.Name) + ".ut.json" };
            if (dlg.ShowDialog(this) != true) return false;
            path = dlg.FileName;
        }
        StoreView();
        try { Doc.Save(path); Message("Zapisano " + Path.GetFileName(path)); return true; }
        catch (Exception ex) { Message("Błąd zapisu: " + ex.Message); if (!NoPrompt) MessageBox.Show(this, ex.Message, "Zapis projektu"); return false; }
    }

    static string SafeName(string n) => string.Concat(n.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    /// <summary>Katalog projektów: <c>projekty_ut</c> w katalogu repozytorium (szukany w górę od programu), inaczej Dokumenty.</summary>
    public static string ProjectsDir()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "PLAN_EDYTOR_UT.md")))
            {
                var p = Path.Combine(d.FullName, "projekty_ut"); Directory.CreateDirectory(p); return p;
            }
        return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    void OnClosing(object s, CancelEventArgs e) { if (!ConfirmDiscard()) e.Cancel = true; }

    // ------------------------------------------------------------------ widok
    /// <summary>Idź do punktu (WGS84 albo PUWG 1992); przybliżenie 0,5 m/px, chyba że widok jest już bliżej.</summary>
    public bool GoTo(string text)
    {
        if (!Geo.TryParsePoint(text, out var p)) { Message("Nie rozpoznano punktu – wpisz np. „51.7631, 19.5438” albo „384120 537450” (X Y w PUWG 1992)"); return false; }
        View.SetView(p, Math.Min(View.Mpp, 0.5));
        Message($"Punkt X = {Geo.M(p.X)} m, Y = {Geo.M(p.Y)} m");
        return true;
    }

    void ShowCursor(XY? p)
    {
        if (p is not XY w) { stCursor.Text = ""; return; }
        var (lat, lon) = Geo.ToWgs84(w);
        stCursor.Text = $"X = {Geo.M(w.X)} m   Y = {Geo.M(w.Y)} m   ({lat.ToString("0.000000", System.Globalization.CultureInfo.InvariantCulture)}, {lon.ToString("0.000000", System.Globalization.CultureInfo.InvariantCulture)})";
    }

    void ShowView()
    {
        // 96 px na cal – skala obrazu na typowym monitorze
        double screen = View.Mpp / (0.0254 / 96);
        stView.Text = $"1 px = {UtView.Len(View.Mpp)}   ·   ekran ok. 1:{Geo.M(UtView.Nice(screen), 0)}";
    }

    public void Message(string m) => stMsg.Text = m;

    void OnKey(object s, KeyEventArgs e)
    {
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control), shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        bool inText = Keyboard.FocusedElement is TextBox;
        if (ctrl && e.Key == Key.S) { SaveDoc(shift); e.Handled = true; }
        else if (ctrl && e.Key == Key.O) { OpenDoc(); e.Handled = true; }
        else if (ctrl && e.Key == Key.N) { NewDoc(); e.Handled = true; }
        else if (!inText && ctrl && (e.Key == Key.Y || (e.Key == Key.Z && shift))) { DoRedo(); e.Handled = true; }
        else if (!inText && ctrl && e.Key == Key.Z) { DoUndo(); e.Handled = true; }
    }
}
