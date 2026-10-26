using System.Diagnostics;
using Lcs.Model;

namespace Lcs.EdytorUT;

/// <summary>
/// Próba na kopii projektu (EDYTOR_UT_TEST=zawis, EDYTOR_UT_PLIK, EDYTOR_UT_TORY – dwa id torów po przecinku, EDYTOR_UT_WIDOK – „x;y;mpp”):
/// widok jak w chwili zawieszenia edytora (Schemat, przybliżenie), „Połącz tory”, czas rysowania przed i po.
/// </summary>
public static partial class Test
{
    static async Task RunZawis(MainWindow w, string path, string tory, string widok, string shot)
    {
        w.OpenPath(path); w.SetTool(UtTool.Wskaz); w.SetSchemat(true); await Settle(w);
        var v = widok.Split(';').Select(x => double.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        var sw = Stopwatch.StartNew();
        w.View.SetView(new XY(v[0], v[1]), v[2]); await Settle(w);
        log.AppendLine($"INFO  widok przed połączeniem: {sw.ElapsedMilliseconds} ms");
        var ids = tory.Split(',');
        w.View.SetMulti(ids.Select(id => (Kontrola.KTor, id)));
        sw.Restart();
        bool ok = w.JoinSelected();
        log.AppendLine($"INFO  połączenie: {ok}, {sw.ElapsedMilliseconds} ms ({w.LastMessage})");
        sw.Restart(); await Settle(w);
        log.AppendLine($"INFO  rysowanie po połączeniu: {sw.ElapsedMilliseconds} ms");
        // kształt toru z pamięci zawieszonego edytora (EDYTOR_UT_WIERZ – „x,y,R;…”)
        if (Environment.GetEnvironmentVariable("EDYTOR_UT_WIERZ") is string wz)
        {
            var vs = wz.Split(';').Select(q => q.Split(',').Select(x => double.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray())
                .Select(a => new UtVertex { X = a[0], Y = a[1], R = a[2] }).ToList();
            w.Doc.Change("Kształt z pamięci (test)", q => q.Tracks.First(t => t.Id == ids[0]).Vertices = vs);
            sw.Restart(); await Settle(w);
            log.AppendLine($"INFO  rysowanie toru o kształcie z pamięci: {sw.ElapsedMilliseconds} ms");
        }
        if (shot != null) Save(w, shot);
        Ok(sw.ElapsedMilliseconds < 3000, "rysowanie po połączeniu poniżej 3 s");
    }
}
