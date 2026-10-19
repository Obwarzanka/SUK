using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Lcs.Tabor;
using Microsoft.Win32;

namespace Lcs.EdytorTaboru;

/// <summary>
/// Okno edytora taboru (PLAN_EDYTOR_TABORU.md § 4, etap B3): pasek (plik, cofanie, „Sprawdź”), zakładki „Pojazdy” i „Składy”, pasek stanu.
/// Dane pojazdów wpisuje człowiek; przykłady w katalogu są oznaczone „do potwierdzenia”.
/// </summary>
public partial class MainWindow : Window
{
    public TaborDokument Doc { get; private set; }
    /// <summary>Bez okien dialogowych (testy).</summary>
    public bool NoPrompt;
    public readonly TabControl Tabs = new();
    public readonly Button UndoBtn = new() { Content = "↶ Cofnij" }, RedoBtn = new() { Content = "↷ Ponów" };
    readonly TextBlock stMsg = new(), stFile = new() { MinWidth = 260 };
    /// <summary>Uwagi ostatniego „Sprawdź” (null – nie sprawdzano).</summary>
    public List<string> Uwagi;
    public readonly ListBox UwagiList = new() { MaxHeight = 120, Margin = new Thickness(8, 0, 8, 4) };
    bool refreshing;

    static readonly Brush ToolbarBg = new SolidColorBrush(Color.FromRgb(0xe6, 0xe6, 0xe2)), Border = new SolidColorBrush(Color.FromRgb(0xc8, 0xc8, 0xc2)),
        MissFg = new SolidColorBrush(Color.FromRgb(0xb0, 0x30, 0x20)), OkFg = new SolidColorBrush(Color.FromRgb(0x10, 0x80, 0x10));
    public static readonly CultureInfo Pl = CultureInfo.GetCultureInfo("pl-PL");

    public MainWindow(string path, bool open = true)
    {
        Width = 1400; Height = 880;
        FontFamily = new FontFamily("Segoe UI"); FontSize = 13;
        Background = new SolidColorBrush(Color.FromRgb(0xf3, 0xf3, 0xf0));
        var root = new DockPanel();
        root.Children.Add(Dock(BuildToolbar(), System.Windows.Controls.Dock.Top));
        var st = new StatusBar { Background = ToolbarBg };
        st.Items.Add(new StatusBarItem { Content = stFile }); st.Items.Add(new Separator()); st.Items.Add(new StatusBarItem { Content = stMsg });
        root.Children.Add(Dock(st, System.Windows.Controls.Dock.Bottom));
        UwagiList.Visibility = Visibility.Collapsed;
        root.Children.Add(Dock(UwagiList, System.Windows.Controls.Dock.Bottom));
        Tabs.Items.Add(new TabItem { Header = "Pojazdy", Content = BuildPojazdy() });
        Tabs.Items.Add(new TabItem { Header = "Składy", Content = BuildSklady() });
        Tabs.Margin = new Thickness(6);
        root.Children.Add(Tabs);
        Content = root;
        PreviewKeyDown += OnKey;
        Closing += OnClosing;
        Loaded += (s, e) =>
        {
            if (!open) return;
            var p = path ?? TaborPlik.SciezkaWspolna();
            if (File.Exists(p)) OpenPath(p); else SetDoc(TaborDokument.Nowy());
        };
    }

    static UIElement Dock(UIElement e, System.Windows.Controls.Dock d) { DockPanel.SetDock(e, d); return e; }
    public static Button Btn(string text, RoutedEventHandler click, string tip) { var b = new Button { Content = text, ToolTip = tip, Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(0, 0, 4, 0) }; b.Click += click; return b; }
    static TextBlock Header(string t) => new() { Text = t, FontWeight = FontWeights.SemiBold, FontSize = 15, Margin = new Thickness(0, 6, 0, 6) };
    static TextBlock Label(string t) => new() { Text = t, Margin = new Thickness(0, 4, 8, 0), VerticalAlignment = VerticalAlignment.Center };

    UIElement BuildToolbar()
    {
        var tb = new ToolBar { Background = ToolbarBg };
        tb.Items.Add(Btn("Nowy", (s, e) => { if (ConfirmDiscard()) SetDoc(TaborDokument.Nowy()); }, "Nowy, pusty plik taboru"));
        tb.Items.Add(Btn("Otwórz…", (s, e) => OpenDoc(), "Otwórz plik taboru (*.json) (Ctrl+O)"));
        tb.Items.Add(Btn("Zapisz", (s, e) => SaveDoc(false), "Zapisz; poprzednia wersja zostaje w .bak (Ctrl+S)"));
        tb.Items.Add(Btn("Zapisz jako…", (s, e) => SaveDoc(true), "Zapisz jako inny plik, np. <układ>.tabor.json obok układu symulatora (Ctrl+Shift+S)"));
        tb.Items.Add(new Separator());
        UndoBtn.Click += (s, e) => DoUndo(); RedoBtn.Click += (s, e) => DoRedo();
        tb.Items.Add(UndoBtn); tb.Items.Add(RedoBtn);
        tb.Items.Add(new Separator());
        tb.Items.Add(Btn("✔ Sprawdź", (s, e) => Sprawdz(), "Sprawdź plik (F7): serie w składach, pojazd napędny bez charakterystyki, skład bez napędu, BZT bez dwóch charakterystyk"));
        return new ToolBarTray { Background = ToolbarBg, ToolBars = { tb } };
    }

    // ------------------------------------------------------------------ dokument
    void SetDoc(TaborDokument d)
    {
        if (Doc != null) Doc.Changed -= Refresh;
        Doc = d; Doc.Changed += Refresh;
        PojazdIdx = Doc.P.Pojazdy.Count > 0 ? 0 : -1; SkladIdx = Doc.P.Sklady.Count > 0 ? 0 : -1;
        Uwagi = null;
        Refresh();
    }

    public void Refresh()
    {
        refreshing = true;
        try
        {
            Title = $"{(Doc.Dirty || Doc.Path == null ? "* " : "")}{(Doc.Path != null ? Path.GetFileName(Doc.Path) : "nowy plik taboru")} – Edytor taboru";
            stFile.Text = Doc.Path ?? "(niezapisany)";
            UndoBtn.IsEnabled = Doc.UndoName != null; UndoBtn.ToolTip = Doc.UndoName != null ? $"Cofnij: {Doc.UndoName} (Ctrl+Z)" : "Nic do cofnięcia";
            RedoBtn.IsEnabled = Doc.RedoName != null; RedoBtn.ToolTip = Doc.RedoName != null ? $"Ponów: {Doc.RedoName} (Ctrl+Y)" : "Nic do ponowienia";
            PojazdIdx = Math.Min(PojazdIdx, Doc.P.Pojazdy.Count - 1); if (PojazdIdx < 0 && Doc.P.Pojazdy.Count > 0) PojazdIdx = 0;
            SkladIdx = Math.Min(SkladIdx, Doc.P.Sklady.Count - 1); if (SkladIdx < 0 && Doc.P.Sklady.Count > 0) SkladIdx = 0;
            RefreshPojazdy();
            RefreshSklady();
            if (Uwagi != null) Uwagi = Doc.P.Sprawdz();
            ShowUwagi();
        }
        finally { refreshing = false; }
    }

    public void Message(string m) => stMsg.Text = m;
    public string LastMessage => stMsg.Text;

    /// <summary>Wpis w polu, które ma fokus, przyjmowany przed zapisem, cofnięciem i zamknięciem (jak w edytorze UT).</summary>
    public void CommitPending()
    {
        if (Keyboard.FocusedElement is TextBox tb && tb.Tag is Action commit) commit();
    }

    // po cofnięciu pola pokazują stan pliku także wtedy, gdy mają fokus (inaczej stary wpis wróciłby przy wyjściu z pola)
    public void DoUndo() { CommitPending(); var n = Doc.UndoName; wymus = true; try { if (Doc.Undo()) Message("Cofnięto: " + n); } finally { wymus = false; } }
    public void DoRedo() { CommitPending(); var n = Doc.RedoName; wymus = true; try { if (Doc.Redo()) Message("Ponowiono: " + n); } finally { wymus = false; } }

    public List<string> Sprawdz()
    {
        CommitPending();
        Uwagi = Doc.P.Sprawdz();
        ShowUwagi();
        Message(Uwagi.Count == 0 ? "Sprawdź: bez uwag" : $"Sprawdź: {Uwagi.Count} uwag");
        return Uwagi;
    }

    void ShowUwagi()
    {
        UwagiList.Items.Clear();
        if (Uwagi == null) { UwagiList.Visibility = Visibility.Collapsed; return; }
        UwagiList.Visibility = Visibility.Visible;
        if (Uwagi.Count == 0) UwagiList.Items.Add(new TextBlock { Text = "✔ Sprawdź: bez uwag", Foreground = OkFg });
        foreach (var u in Uwagi) UwagiList.Items.Add(new TextBlock { Text = "⚠ " + u, Foreground = MissFg });
    }

    bool ConfirmDiscard()
    {
        CommitPending();
        if (Doc == null || !Doc.Dirty || NoPrompt) return true;
        var r = MessageBox.Show(this, "Zapisać zmiany w pliku taboru?", "Edytor taboru", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (r == MessageBoxResult.Cancel) return false;
        return r == MessageBoxResult.No || SaveDoc(false);
    }

    void OpenDoc()
    {
        if (!ConfirmDiscard()) return;
        var dlg = new OpenFileDialog { Filter = "Plik taboru (*.json)|*.json", InitialDirectory = Path.GetDirectoryName(Doc?.Path ?? TaborPlik.SciezkaWspolna()) };
        if (dlg.ShowDialog(this) == true) OpenPath(dlg.FileName);
    }

    public bool OpenPath(string path)
    {
        try { SetDoc(TaborDokument.Load(path)); Message("Otwarto " + Path.GetFileName(path)); return true; }
        catch (Exception ex) { Message("Nie można otworzyć: " + ex.Message); if (!NoPrompt) MessageBox.Show(this, ex.Message, "Otwieranie"); return false; }
    }

    public bool SaveDoc(bool askName, string path = null)
    {
        CommitPending();
        path ??= Doc.Path;
        if (path == null || askName)
        {
            if (NoPrompt) return false;
            var dlg = new SaveFileDialog { Filter = "Plik taboru (*.json)|*.json", FileName = Path.GetFileName(Doc.Path ?? "tabor.json"), InitialDirectory = Path.GetDirectoryName(Doc.Path ?? TaborPlik.SciezkaWspolna()) };
            if (dlg.ShowDialog(this) != true) return false;
            path = dlg.FileName;
        }
        try { Doc.Save(path); Message("Zapisano " + Path.GetFileName(path)); return true; }
        catch (Exception ex) { Message("Błąd zapisu: " + ex.Message); if (!NoPrompt) MessageBox.Show(this, ex.Message, "Zapis"); return false; }
    }

    void OnClosing(object s, CancelEventArgs e) { if (!ConfirmDiscard()) e.Cancel = true; }

    void OnKey(object s, KeyEventArgs e)
    {
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control), shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        if (ctrl && e.Key == Key.Z) { if (shift) DoRedo(); else DoUndo(); e.Handled = true; }
        else if (ctrl && e.Key == Key.Y) { DoRedo(); e.Handled = true; }
        else if (ctrl && e.Key == Key.S) { SaveDoc(shift); e.Handled = true; }
        else if (ctrl && e.Key == Key.O) { OpenDoc(); e.Handled = true; }
        else if (e.Key == Key.F7) { Sprawdz(); e.Handled = true; }
    }

    // ------------------------------------------------------------------ pola liczbowe
    public static string F(double v) => v.ToString("0.######", Pl);   // opór c ma 5 miejsc po przecinku (np. 0,00035)
    public static bool TryNum(string s, out double v) =>
        double.TryParse((s ?? "").Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v);

    /// <summary>Pole tekstowe z zatwierdzaniem przy Enter i wyjściu z pola (<see cref="TextBox.Tag"/> – akcja zatwierdzenia dla <see cref="CommitPending"/>).</summary>
    TextBox Pole(Action<string> commit, double width = 90, string tip = null)
    {
        var tb = new TextBox { Width = width, Margin = new Thickness(0, 2, 8, 2), ToolTip = tip };
        Action a = () => { if (!refreshing) commit(tb.Text); };
        tb.Tag = a;
        tb.LostFocus += (s, e) => a();
        tb.KeyDown += (s, e) => { if (e.Key == Key.Enter) a(); };
        return tb;
    }

    /// <summary>Odświeżenie po cofnięciu / ponowieniu – nadpisuje także pole z fokusem.</summary>
    static bool wymus;
    /// <summary>Tekst pola bez nadpisywania wpisu, który użytkownik właśnie pisze.</summary>
    static void Set(TextBox tb, string text) { if (wymus || !tb.IsKeyboardFocused) tb.Text = text; }
}
