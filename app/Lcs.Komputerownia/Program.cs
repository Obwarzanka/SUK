using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Lcs.Komputerownia;

/// <summary>
/// Komputerownia – osobny program podglądu kart. Bez argumentów – okno (Połącz z symulatorem); <c>--connect host:port</c> – od razu łączy;
/// <c>--test wynik.txt [zrzut.png] host:port</c> – test z działającym symulatorem (ostatnia linia „błędów: N”).
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("pl-PL");
        var app = new Application();
        var w = new MainWindow();
        string Hp(string a) { var p = a.Split(':'); w.Host.Text = p[0]; if (p.Length > 1) w.Port.Text = p[1]; return a; }
        if (args.Length >= 2 && args[0] == "--connect") { Hp(args[1]); w.ContentRendered += (s, e) => w.Polacz(); }
        if (args.Length >= 2 && args[0] == "--test")
        {
            string outPath = args[1], shot = args.Length >= 3 && args[2].EndsWith(".png") ? args[2] : null;
            var hp = args.Skip(2).FirstOrDefault(a => !a.EndsWith(".png"));
            if (hp != null) Hp(hp);
            w.Left = SystemParameters.VirtualScreenLeft - 3000; w.ShowActivated = false; w.ShowInTaskbar = false; w.WindowStartupLocation = WindowStartupLocation.Manual;
            w.ContentRendered += async (s, e) =>
            {
                try { await Test.Run(w, outPath, shot); }
                catch (Exception ex) { File.WriteAllText(outPath, "WYJĄTEK " + ex + Environment.NewLine + "błędów: 1"); }
                app.Shutdown();
            };
        }
        app.Run(w);
    }

    public static void Save(Window w, string path)
    {
        w.UpdateLayout();
        var rtb = new RenderTargetBitmap((int)w.ActualWidth, (int)w.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(w);
        var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(rtb));
        using var f = File.Create(path); enc.Save(f);
    }
}

/// <summary>
/// Test z symulatorem uruchomionym tak: <c>LcsWidzew.exe --station kopia.json --screenshot p.png 40 "#serwis=PORT" "#fault=swNoCtrl:ZWR" "@35" "#hold=60"</c>
/// – sprawdza: połączenie, karty każdego rodzaju, brak kontroli zwrotnicy z usterką, wprowadzenie i usunięcie usterki z programu.
/// </summary>
static class Test
{
    public static async Task Run(MainWindow w, string outPath, string shot)
    {
        var log = new System.Text.StringBuilder(); int err = 0;
        void Ok(bool c, string what) { log.AppendLine((c ? "OK    " : "BŁĄD  ") + what); if (!c) err++; }
        for (int i = 0; i < 20 && w.Pol == null; i++) { w.Polacz(); if (w.Pol == null) await Task.Delay(500); }
        Ok(w.Pol != null, $"połączenie z symulatorem {w.Host.Text}:{w.Port.Text}: {w.Info.Text}");
        if (w.Pol != null)
        {
            await w.Odswiez();
            int od = w.Karty.Count(k => k.Rodzaj == "odcinek"), zw = w.Karty.Count(k => k.Rodzaj == "zwrotnica"), wk = w.Karty.Count(k => k.Rodzaj == "wykolejnica");
            int sg = w.Karty.Count(k => k.Rodzaj == "sygnalizator");
            Ok(od > 0 && zw > 0 && wk > 0 && sg > 0, $"karty: odcinki {od}, zwrotnice {zw}, wykolejnice {wk}, sygnalizatory {sg}");
            var zez = w.Karty.Where(k => k.Rodzaj == "sygnalizator" && k.Status.StartsWith("wyświetla S") && !k.Status.StartsWith("wyświetla S1") && !k.Status.StartsWith("wyświetla Sz")).ToList();
            Ok(zez.Count > 0 && zez.All(k => k.Rozkaz.StartsWith(k.Status[10..].Split(',')[0])), $"sygnały zezwalające: {string.Join(", ", zez.Take(6).Select(k => $"{k.Obiekt}: {k.Status[10..]} – {k.Teren}"))}");
            var bk = w.Karty.Where(k => k.BrakKontroli && k.Rodzaj == "zwrotnica").ToList();
            Ok(bk.Count > 0 && bk.All(k => k.UsterkiKlucze.Contains("swNoCtrl")), $"brak kontroli: {string.Join(", ", bk.Select(k => k.Post + ":" + k.Obiekt + " (" + k.Usterki + ")"))}");
            var zwr = w.Karty.First(k => k.Rodzaj == "zwrotnica" && !k.BrakKontroli);
            var r = await w.Usterka("swFrozen", zwr.Id, false);
            var po = w.Karty.First(k => k == zwr);
            Ok(r == "OK" && po.UsterkiKlucze.Contains("swFrozen") && po.Nietypowa, $"usterka z programu: zamarznięcie {zwr.Id} → „{po.Usterki}”");
            r = await w.Usterka("swFrozen", zwr.Id, true);
            Ok(r == "OK" && !w.Karty.First(k => k == zwr).UsterkiKlucze.Contains("swFrozen"), $"usunięcie usterki {zwr.Id}");
            var zl = await w.Usterka("nieMa", zwr.Id, false);
            Ok(zl == "nieznana usterka", $"nieznana usterka odrzucona: {zl}");
            var bad = bk.FirstOrDefault();
            if (bad != null) w.Zaznacz(bad.Rodzaj, bad.Id);
            w.UpdateLayout(); await Task.Delay(200);
            Ok(w.Szczegoly.Children.OfType<Button>().Any(b => b.Tag is true && ((TextBlock)b.Content).Text.StartsWith("Usuń:")), "panel karty: przycisk „Usuń” dla wprowadzonej usterki");
        }
        if (shot != null) Program.Save(w, shot);
        if (shot != null && w.Pol != null)
        {
            // drugi zrzut: same sygnalizatory, zaznaczony pierwszy z sygnałem zezwalającym
            w.Rodzaje.SelectedIndex = 4; w.Szukaj.Text = "";
            var z = w.Karty.Where(k => k.Rodzaj == "sygnalizator" && k.Typ.Contains("wjazdowy") && k.Status.StartsWith("wyświetla S") && !k.Status.StartsWith("wyświetla S1 ")).OrderBy(k => k.Status == "wyświetla S1").FirstOrDefault();
            if (z != null) w.Zaznacz(z.Rodzaj, z.Id);
            w.UpdateLayout(); await Task.Delay(200);
            Program.Save(w, System.IO.Path.ChangeExtension(shot, null) + "_sygnaly.png");
        }
        log.AppendLine($"błędów: {err}");
        File.WriteAllText(outPath, log.ToString());
    }
}
