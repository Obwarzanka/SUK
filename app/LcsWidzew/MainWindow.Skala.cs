using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace LcsWidzew;

/// <summary>
/// Skala interfejsu pulpitu – na ekranach 2K/4K przy skalowaniu Windows 100% wszystko było za drobne.
/// Ctrl + / Ctrl − / Ctrl 0 – cały interfejs (przyciski, listy zdarzeń, plan);
/// Ctrl+Shift + / Ctrl+Shift − – tylko opisy na planie. Zapamiętywane w %LOCALAPPDATA%\SUK\pulpit_skala.txt.
/// </summary>
public partial class MainWindow
{
    double skalaUi = 1, skalaTekstu = 1.15;
    static string PlikSkali => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SUK", "pulpit_skala.txt");

    /// <summary>Domyślna skala wg wysokości ekranu w jednostkach WPF (już po skalowaniu Windows).</summary>
    static double SkalaAuto()
    {
        double h = SystemParameters.PrimaryScreenHeight;
        return h >= 2000 ? 1.75 : h >= 1600 ? 1.5 : h >= 1350 ? 1.25 : 1.0;
    }

    void Skala_Init(object s, EventArgs e)
    {
        skalaUi = SkalaAuto();
        try
        {
            if (File.Exists(PlikSkali))
                foreach (var cz in File.ReadAllText(PlikSkali).Split(';', '\n'))
                {
                    var kv = cz.Split('='); if (kv.Length != 2) continue;
                    if (!double.TryParse(kv[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) continue;
                    if (kv[0].Trim() == "ui") skalaUi = Math.Clamp(v, 0.75, 3);
                    if (kv[0].Trim() == "tekst") skalaTekstu = Math.Clamp(v, 0.6, 2.5);
                }
        }
        catch { }
        UstawSkale(false);
    }

    void UstawSkale(bool zapisz)
    {
        Korzen.LayoutTransform = Math.Abs(skalaUi - 1) < 0.01 ? Transform.Identity : new ScaleTransform(skalaUi, skalaUi);
        Panel.TekstK = skalaTekstu;
        Panel.InvalidateVisual();
        if (!zapisz) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PlikSkali)!);
            File.WriteAllText(PlikSkali, string.Format(CultureInfo.InvariantCulture, "ui={0:0.##};tekst={1:0.##}", skalaUi, skalaTekstu));
        }
        catch { }
        Hint.Text = $"Skala interfejsu {skalaUi * 100:0}% (Ctrl +/−/0), opisy planu {skalaTekstu * 100:0}% (Ctrl+Shift +/−)";
        Dispatcher.BeginInvoke(() => Panel.FitPage(), DispatcherPriority.Loaded);   // plan dopasowany do nowej wielkości okna
    }

    void Skala_PreviewKeyDown(object s, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        int kier = e.Key is Key.OemPlus or Key.Add ? 1 : e.Key is Key.OemMinus or Key.Subtract ? -1 : e.Key is Key.D0 or Key.NumPad0 ? 0 : 99;
        if (kier == 99) return;
        if (shift)
        {
            if (kier == 0) skalaTekstu = 1.15;
            else skalaTekstu = Math.Round(Math.Clamp(skalaTekstu + kier * 0.1, 0.6, 2.5), 2);
        }
        else
        {
            if (kier == 0) skalaUi = SkalaAuto();
            else skalaUi = Math.Round(Math.Clamp(skalaUi + kier * 0.125, 0.75, 3), 3);
        }
        UstawSkale(true);
        e.Handled = true;
    }
}
