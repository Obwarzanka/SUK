using System.Diagnostics;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows;
using Lcs.Model;

namespace Lcs.EdytorZobrazowania;

/// <summary>
/// „Do symulatora…” (etap 9): UT + wszystkie obrazy → plik układu symulatora (<c>station.json</c>) w <c>LcsWidzew/Data/projekty</c>,
/// skąd symulator pokazuje go w oknie wyboru układu przy starcie (<c>uruchom.bat</c>). Plik podstawowy <c>Data/station.json</c>
/// nigdy nie jest nadpisywany.
/// </summary>
public partial class MainWindow
{
    static readonly JsonSerializerOptions StationJson = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Katalog projektów symulatora (<c>app/LcsWidzew/Data/projekty</c>) – szukany w górę od katalogu programu; null – brak.</summary>
    public static string SimProjectsDir()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            var p = Path.Combine(d.FullName, "LcsWidzew", "Data", "projekty");
            if (Directory.Exists(p)) return p;
            p = Path.Combine(d.FullName, "app", "LcsWidzew", "Data", "projekty");
            if (Directory.Exists(p)) return p;
        }
        return null;
    }

    /// <summary>Plik symulatora dla tego zobrazowania: nazwa pliku zobrazowania (bez .zob.json) w katalogu projektów symulatora.</summary>
    public string SimPathFor()
    {
        var name = Path.GetFileName(Doc.DefaultPath);
        name = name.EndsWith(".zob.json", StringComparison.OrdinalIgnoreCase) ? name[..^9] : Path.GetFileNameWithoutExtension(name);
        return Path.Combine(SimProjectsDir() ?? Path.GetDirectoryName(Doc.DefaultPath)!, name + ".json");
    }

    /// <summary>
    /// Zapis pliku symulatora (path null – <see cref="SimPathFor"/>). Zwraca uwagi przekształcenia; null – nie zapisano.
    /// Bez <see cref="NoPrompt"/>: przy błędach „Sprawdź” pytanie, po zapisie – propozycja uruchomienia symulatora.
    /// </summary>
    public List<string> DoSymulatora(string path = null)
    {
        if (Doc == null) { Message("Do symulatora: najpierw otwórz zobrazowanie"); return null; }
        var errs = Doc.P.Obrazy.SelectMany(o => ZobSymbole.Sprawdz(Doc.Ut, o)).Where(u => u.Error).ToList();
        if (errs.Count > 0 && !NoPrompt &&
            MessageBox.Show(this, $"„Sprawdź” zgłasza błędy ({errs.Count}), np.: {errs[0].Text}.\n\nZapisać mimo to?", "Do symulatora", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return null;
        path ??= SimPathFor();
        var w = Lcs.Model.DoSymulatora.Buduj(Doc.Ut, Doc.P, $"UT: {Path.GetFileName(Doc.UtPath)} + zobrazowanie: {Path.GetFileName(Doc.DefaultPath)}");
        try
        {
            if (File.Exists(path)) File.Copy(path, path + ".bak", true);
            File.WriteAllText(path, w.Json.ToJsonString(StationJson));
        }
        catch (Exception ex) { Message("Do symulatora: " + ex.Message); if (!NoPrompt) MessageBox.Show(this, ex.Message, "Do symulatora"); return null; }
        Message($"Do symulatora: zapisano {Path.GetFileName(path)}" + (w.Uwagi.Count > 0 ? $", uwag {w.Uwagi.Count}" : ", bez uwag"));
        if (!NoPrompt)
        {
            var exe = SimExe();
            var txt = $"Zapisano układ dla symulatora:\n{path}\n\n" +
                (w.Uwagi.Count > 0 ? "Uwagi:\n• " + string.Join("\n• ", w.Uwagi.Take(15)) + "\n\n" : "") +
                "W symulatorze (uruchom.bat) wybierz go w oknie wyboru układu." + (exe != null ? "\n\nUruchomić symulator z tym układem teraz?" : "");
            if (exe == null) MessageBox.Show(this, txt, "Do symulatora", MessageBoxButton.OK, MessageBoxImage.Information);
            else if (MessageBox.Show(this, txt, "Do symulatora", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                try { Process.Start(SimStartInfo(exe, path)); }
                catch (Exception ex) { MessageBox.Show(this, "Nie można uruchomić symulatora: " + ex.Message, "Do symulatora"); }
        }
        return w.Uwagi;
    }

    /// <summary>Zbudowany symulator (Release) obok katalogu projektów; null – brak.</summary>
    static string SimExe()
    {
        var proj = SimProjectsDir(); if (proj == null) return null;
        var exe = Path.GetFullPath(Path.Combine(proj, "..", "..", "bin", "Release", "net10.0-windows", "LcsWidzew.exe"));
        return File.Exists(exe) ? exe : null;
    }

    /// <summary>Uruchamia aktualny build także z lokalnym runtime używanym przez launchery.</summary>
    static ProcessStartInfo SimStartInfo(string exe, string stationPath)
    {
        var info = new ProcessStartInfo(exe) { WorkingDirectory = Path.GetDirectoryName(exe), UseShellExecute = false };
        for (var dir = new DirectoryInfo(Path.GetDirectoryName(exe)); dir != null; dir = dir.Parent)
        {
            var dotnet = Path.Combine(dir.FullName, ".tools", "dotnet", "dotnet.exe");
            if (!File.Exists(dotnet)) continue;
            info.FileName = dotnet;
            info.ArgumentList.Add(Path.ChangeExtension(exe, ".dll"));
            break;
        }
        info.ArgumentList.Add("--station");
        info.ArgumentList.Add(stationPath);
        return info;
    }
}
