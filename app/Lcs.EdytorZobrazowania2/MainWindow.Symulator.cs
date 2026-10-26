using System.Diagnostics;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Lcs.Model;

namespace Lcs.EdytorZobrazowania2;

/// <summary>
/// Etap E5 (PLAN_EDYTOR_ZOBRAZOWANIA_V2.md § 4, § 9): „kompilacja” obrazów v2 do <see cref="ZobProject"/> (<see cref="ObrKompilacja"/>), na niej –
/// bez zmian – „Podgląd pulpitu” (okno podglądu i kod pulpitu symulatora, jak w starym edytorze) i „Do symulatora…” (<see cref="Lcs.Model.DoSymulatora"/>,
/// zapis do <c>LcsWidzew/Data/projekty</c>; <c>Data/station.json</c> nigdy nie jest nadpisywany).
/// </summary>
public partial class MainWindow
{
    public readonly Button PodgladBtn = new() { Content = "Podgląd pulpitu", Padding = new Thickness(6, 2, 6, 2), ToolTip = "Podgląd pulpitu (F9): obraz jak na pulpicie symulatora – ten sam kod pulpitu i komputer zależności; nic nie zapisuje" },
        SymBtn = new() { Content = "Do symulatora…", Padding = new Thickness(6, 2, 6, 2), ToolTip = "Zapisz UT i obrazy jako układ symulatora (LcsWidzew/Data/projekty); Data/station.json zostaje bez zmian" };
    static readonly JsonSerializerOptions StationJson = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Obrazy v2 → <see cref="ZobProject"/> w pamięci (tylko przypięte elementy).</summary>
    public ObrKompilacja.Wynik Kompiluj() => Doc == null ? null : ObrKompilacja.Kompiluj(Doc.Ut, Doc.P);

    /// <summary>Okno podglądu „jak na pulpicie” na stronie bieżącego obrazu (obraz poglądowy – pierwsza strona).</summary>
    public PodgladWindow Podglad()
    {
        if (Doc == null) { Message("Podgląd pulpitu: najpierw otwórz zobrazowanie"); return null; }
        try
        {
            var k = Kompiluj();
            var pw = new PodgladWindow(Doc.Ut, k.Zob, Obraz?.Rodzaj == ZobObraz.Szczegolowy ? Obraz.Id : null, Doc.P.Przebiegi) { Owner = this };
            if (!NoPrompt) pw.Show();
            Message($"Podgląd pulpitu: {pw.Station.View.Pages.Count} obrazów, {pw.Routes.Count} przebiegów" + (pw.Uwagi.Count + k.Uwagi.Count > 0 ? $", uwagi: {pw.Uwagi.Count + k.Uwagi.Count}" : ""));
            return pw;
        }
        catch (Exception ex) { Message("Podgląd pulpitu: " + ex.Message); if (!NoPrompt) MessageBox.Show(this, ex.Message, "Podgląd pulpitu"); return null; }
    }

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

    /// <summary>Plik symulatora: nazwa pliku zobrazowania (bez .zob.json i .v2) w katalogu projektów symulatora.</summary>
    public string SimPathFor()
    {
        var name = Path.GetFileName(Doc.DefaultPath);
        name = name.EndsWith(".zob.json", StringComparison.OrdinalIgnoreCase) ? name[..^9] : Path.GetFileNameWithoutExtension(name);
        if (name.EndsWith(".v2", StringComparison.OrdinalIgnoreCase)) name = name[..^3];
        return Path.Combine(SimProjectsDir() ?? Path.GetDirectoryName(Doc.DefaultPath)!, name + ".json");
    }

    /// <summary>
    /// UT + obrazy → plik układu symulatora (path null – <see cref="SimPathFor"/>). Zwraca uwagi; null – nie zapisano. Bez <see cref="NoPrompt"/>:
    /// przy błędach „Sprawdź” pytanie, po zapisie – propozycja uruchomienia symulatora.
    /// </summary>
    public List<string> DoSymulatora(string path = null)
    {
        if (Doc == null) { Message("Do symulatora: najpierw otwórz zobrazowanie"); return null; }
        var errs = Doc.P.Obrazy.SelectMany(o => ObrSprawdz.Sprawdz(Doc.Ut, o)).Where(u => u.Blad).ToList();
        if (errs.Count > 0 && !NoPrompt &&
            MessageBox.Show(this, $"„Sprawdź” zgłasza błędy ({errs.Count}), np.: {errs[0].Tekst}.\n\nZapisać mimo to?", "Do symulatora", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return null;
        path ??= SimPathFor();
        var k = Kompiluj();
        var w = Lcs.Model.DoSymulatora.Buduj(Doc.Ut, k.Zob, $"UT: {Path.GetFileName(Doc.UtPath)} + zobrazowanie: {Path.GetFileName(Doc.DefaultPath)}", Doc.P.Przebiegi);
        var uw = k.Uwagi.Concat(w.Uwagi).Concat(UwagiPrzebiegow(k)).ToList();
        try
        {
            if (File.Exists(path)) File.Copy(path, path + ".bak", true);
            File.WriteAllText(path, w.Json.ToJsonString(StationJson));
        }
        catch (Exception ex) { Message("Do symulatora: " + ex.Message); if (!NoPrompt) MessageBox.Show(this, ex.Message, "Do symulatora"); return null; }
        Message($"Do symulatora: zapisano {Path.GetFileName(path)}" + (uw.Count > 0 ? $", uwag {uw.Count}" : ", bez uwag"));
        if (!NoPrompt)
        {
            var exe = SimExe();
            var txt = $"Zapisano układ dla symulatora:\n{path}\n\n" + (uw.Count > 0 ? "Uwagi:\n• " + string.Join("\n• ", uw.Take(15)) + "\n\n" : "") +
                "W symulatorze (uruchom.bat) wybierz go w oknie wyboru układu." + (exe != null ? "\n\nUruchomić symulator z tym układem teraz?" : "");
            if (exe == null) MessageBox.Show(this, txt, "Do symulatora", MessageBoxButton.OK, MessageBoxImage.Information);
            else if (MessageBox.Show(this, txt, "Do symulatora", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                try { Process.Start(SimStartInfo(exe, path)); }
                catch (Exception ex) { MessageBox.Show(this, "Nie można uruchomić symulatora: " + ex.Message, "Do symulatora"); }
        }
        return uw;
    }

    /// <summary>Zbudowany symulator (Release) obok katalogu projektów; null – brak.</summary>
    static string SimExe()
    {
        var proj = SimProjectsDir(); if (proj == null) return null;
        var exe = Path.GetFullPath(Path.Combine(proj, "..", "..", "bin", "Release", "net10.0-windows", "LcsWidzew.exe"));
        return File.Exists(exe) ? exe : null;
    }

    /// <summary>Uruchomienie symulatora także z lokalnym runtime używanym przez launchery.</summary>
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
