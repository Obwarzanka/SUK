using System.Globalization;

namespace LcsWidzew.Sim;

/// <summary>Wiersz wyciągu z dziennika ruchu: pociąg (kolumny 1–8), telefonogram / wpis / wpis automatyczny na całą szerokość.</summary>
public class WyciagWiersz
{
    /// <summary>„poc”, „tel”, „wpis”, „tekst”.</summary>
    public string Rodzaj;
    /// <summary>Wcześniejsza postać wiersza (wyciąg z historią – szara, pod bieżącą).</summary>
    public bool Historyczny;
    public string Np = "", P = "", Tor = "", Dw = "", Odj = "", Prz = "", Uwagi = "", Tekst = "", Podpis = "";
    /// <summary>Kolumna 9 – data i godzina zmiany (wyciąg z historią).</summary>
    public string Zmiana = "";
    public List<string> Dr = new();
    /// <summary>Jazda do km i z powrotem: 1 – wiersz wyjazdu, 2 – wiersz powrotu (klamra).</summary>
    public int Klamra;
}

/// <summary>Wyciąg z dziennika ruchu (podręcznik EDR rozdz. 8, Rys. 51–54).</summary>
public class WyciagDziennika
{
    public string Posterunek, Szlak, Od, Do;
    public bool Historia;
    /// <summary>Kolumna 8 – km posterunków dróżników (brak – kolumny 8 nie ma).</summary>
    public List<string> DrKm = new();
    public List<WyciagWiersz> Wiersze = new();
}

/// <summary>Wiersz wyciągu z dziennika telefonicznego (Rys. 56): Nr kol. (własny / odbiorcy), data, adres i treść, czas zakończenia, nadał / odebrał.</summary>
public class WyciagTel { public string Nr = "", Data = "", Tresc = "", Czas = "", Strony = "", Zmiana = ""; }

/// <summary>Okres zajętości toru stacyjnego (z wpisów: przyjazd na tor – odjazd).</summary>
public class WyciagTor { public string Tor = "", Pociag = "", Od = "", Do = ""; }

/// <summary>
/// Budowa wyciągów EDR: z dziennika ruchu (bez historii albo z historią zmian w wierszach), z dziennika telefonicznego
/// i z zajętości torów stacyjnych. Do wyciągu trafiają wiersze zmieniane w okresie od – do (podręcznik rozdz. 8.2).
/// </summary>
public static class EdrWyciag
{
    static readonly CultureInfo Pl = CultureInfo.GetCultureInfo("pl-PL");

    /// <summary>Znacznik zmiany „dd-MM-yyyy HH:mm:ss” jako data.</summary>
    public static DateTime? Czas(string znacznik) =>
        DateTime.TryParseExact(znacznik ?? "", "dd-MM-yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t : null;

    /// <summary>Data i godzina zmiany w kolumnie 9 jak w Rys. 52: „14/03/17 11:09:38”.</summary>
    static string Kol9(string znacznik) => Czas(znacznik) is DateTime t ? t.ToString("dd'/'MM'/'yy HH:mm:ss", CultureInfo.InvariantCulture) : znacznik ?? "";

    static bool WOkresie(string znacznik, DateTime od, DateTime @do) => Czas(znacznik) is DateTime t && t >= od && t <= @do;

    public static WyciagDziennika Dziennik(EdrDziennik d, string posterunek, DateTime od, DateTime @do, bool historia)
    {
        var dr = d.Biezace.Droznicy.Where(x => x.WDzienniku).ToList();
        var w = new WyciagDziennika
        {
            Posterunek = posterunek, Szlak = d.Nazwa, Historia = historia, Od = od.ToString("yyyy-MM-dd HH:mm", Pl), Do = @do.ToString("yyyy-MM-dd HH:mm", Pl),
            DrKm = dr.Select(x => x.Km != "" ? x.Km : x.Oznaczenie).ToList()
        };
        foreach (var r in d.Strony.SelectMany(s => s.Wiersze).OrderBy(x => x.Id))
        {
            if (!r.Wersje.Any(v => WOkresie(v.Zmiana, od, @do))) continue;
            var wersje = historia ? r.Wersje.AsEnumerable().Reverse().ToList() : new List<EdrWersja> { r.W };
            for (int i = 0; i < wersje.Count; i++)
            {
                var v = wersje[i];
                var x = new WyciagWiersz { Historyczny = i > 0, Zmiana = historia ? Kol9(v.Zmiana) : "" };
                switch (r.Rodzaj)
                {
                    case "telefonogram":
                        x.Rodzaj = "tel"; x.Tekst = r.Tel.Tresc + (r.Tel.Notatka != "" ? " " + r.Tel.Notatka : ""); x.Podpis = Edr.Podpis(r.Tel); break;
                    case "wpis":
                        x.Rodzaj = "wpis"; x.Tekst = r.Tekst; x.Podpis = $"Wpisał {v.Kto} godz. {v.Uwagi}"; break;
                    case "tekst":
                        x.Rodzaj = "tekst"; x.Tekst = r.Tekst; break;
                    default:
                        x.Rodzaj = "poc";
                        if (Edr.KolumnaNumeru(v.Nr) == 1) x.Np = v.Nr; else x.P = v.Nr;
                        x.Tor = v.Tor; x.Dw = v.Dw; x.Odj = v.Odj; x.Prz = v.Prz;
                        x.Uwagi = (v.Lewy ? "Po torze lewym" + (v.Uwagi != "" ? "; " : "") : "") + v.Uwagi;
                        x.Dr = dr.Select(z => Edr.Wartosc(v, "Dr:" + z.Id)).ToList();
                        x.Klamra = r.Para == 0 ? 0 : r.Pierwszy ? 1 : 2;
                        break;
                }
                w.Wiersze.Add(x);
            }
        }
        return w;
    }

    public static List<WyciagTel> Telefoniczny(List<EdrTelefonogram> l, DateTime od, DateTime @do) =>
        (l ?? new()).Where(t => WOkresie(t.Zapisano, od, @do)).Select(t => new WyciagTel
        {
            Nr = t.Nr + (t.NrOdbiorcy != "" ? " / " + t.NrOdbiorcy : ""),
            Data = Czas(t.Zapisano) is DateTime z ? z.ToString("yyyy-MM-dd", Pl) : t.Data,
            Tresc = (t.Rodzaj == "okolicznosc" ? "" : $"{(t.Nadaje ? "Nadany" : "Odebrany")} godz. {t.Godz}: ") + t.Tresc + (t.Notatka != "" ? " " + t.Notatka : ""),
            Czas = t.CzasZak, Strony = t.Rodzaj == "okolicznosc" ? t.Nadal : $"{t.Nadal} / {t.Odebral}", Zmiana = Kol9(t.Zapisano)
        }).ToList();

    /// <summary>Zajętość torów stacyjnych posterunku w okresie: od wpisu „przyjechał” w przyjęciu na tor do „odjechał” w wyprawieniu (data – ze strony doby).</summary>
    public static List<WyciagTor> ZajetoscTorow(IEnumerable<EdrDziennik> dzienniki, DateTime od, DateTime @do)
    {
        var wyn = new List<WyciagTor>();
        var otwarte = new Dictionary<string, WyciagTor>();
        DateTime? Kiedy(EdrStrona s, string g)
        {
            var h = Edr.Godzina(g)?.Split(" / ")[0];
            if (string.IsNullOrEmpty(h) || h == "---" || !DateTime.TryParseExact(s.Data, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dz)) return null;
            return dz + TimeSpan.Parse(h, CultureInfo.InvariantCulture);
        }
        foreach (var (s, r) in dzienniki.SelectMany(d => d.Strony.SelectMany(s => s.Wiersze.Select(r => (s, r)))).OrderBy(x => x.r.Id))
        {
            var v = r.W;
            if (r.Rodzaj is "przyj" or "powrot" && v.Tor != "" && Kiedy(s, v.Prz) is DateTime p)
            {
                if (otwarte.Remove(v.Tor, out var st)) st.Do = "(brak odjazdu)";
                var t = new WyciagTor { Tor = v.Tor, Pociag = v.Nr, Od = p.ToString("yyyy-MM-dd HH:mm", Pl) };
                otwarte[v.Tor] = t; wyn.Add(t);
            }
            else if (r.Rodzaj == "wypr" && Kiedy(s, v.Odj) is DateTime o)
                foreach (var k in otwarte.Where(x => Edr.TenSam(x.Value.Pociag, v.Nr) || v.Tor != "" && x.Key == v.Tor).Select(x => x.Key).ToList())
                { otwarte[k].Do = o.ToString("yyyy-MM-dd HH:mm", Pl); otwarte.Remove(k); }
        }
        DateTime D(string x) => DateTime.TryParseExact(x, "yyyy-MM-dd HH:mm", Pl, DateTimeStyles.None, out var t) ? t : DateTime.MaxValue;
        // okres zajętości nachodzi na okres wyciągu
        return wyn.Where(t => D(t.Od) <= @do && (t.Do == "" || t.Do.StartsWith("(") || D(t.Do) >= od)).OrderBy(t => t.Tor, Comparer<string>.Create(Edr.PorownajTory)).ThenBy(t => t.Od).ToList();
    }
}
