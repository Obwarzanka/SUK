using System.Windows;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Próba na kopii projektu (EDYTOR_UT_TEST=przeciagnij, EDYTOR_UT_PLIK, EDYTOR_UT_SYG – nazwa sygnalizatora): przeciągnięcie
/// sygnalizatora na bramce za jego dalszą komorę wzdłuż toru – cała bramka w nowym miejscu, w jednym przekroju.
/// </summary>
public static partial class Test
{
    static async Task RunPrzeciagnij(MainWindow w, string path, string name)
    {
        w.OpenPath(path); w.SetTool(UtTool.Wskaz); await Settle(w);
        var d = w.Doc; var sg = d.P.Devices.First(x => x.Name == name);
        var k0 = Urzadzenia.KonstrukcjaOf(d.P, sg.Id)?.M ?? new() { sg };
        string Stan() => string.Join(", ", k0.Select(x => $"{x.Name} km {d.P.Devices.First(q => q.Id == x.Id).S:0.00}")) + $"; przekrzywienie {Urzadzenia.Przekrzywienie(d.P, d.P.Devices.First(q => q.Id == sg.Id)):0.00} m";
        log.AppendLine("INFO  przed: " + Stan());
        var (p, az, kk) = Urzadzenia.Pos(d.P, sg).Value;
        w.View.SetView(p, 0.08); await Settle(w);
        var b = w.View.ToScreen(p); var dd = w.View.ToScreen(p + Element.Dir(az) * kk) - b; dd.Normalize(); var n = new Vector(-dd.Y, dd.X) * sg.Side; double k = w.View.PxPerMm;
        var grab = b + n * (2.4 * k) + dd * ((UtView.HangingLen(sg) - 2) * k);       // przedostatnia komora
        double s0 = sg.S;
        w.View.SimDrag(grab, grab + dd * 120);
        var s1 = d.P.Devices.First(q => q.Id == sg.Id).S;
        Ok(Math.Abs(s1 - s0) > 1 && Urzadzenia.Przekrzywienie(d.P, d.P.Devices.First(q => q.Id == sg.Id)) < 0.05,
            $"{name} chwycony za przedostatnią komorę i przeciągnięty: km {s0:0.00} → {s1:0.00}; " + Stan() + $" ({w.LastMessage})");
    }
}
