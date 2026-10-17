using System.IO;
using System.Text.Json;

namespace LcsWidzew.Sim;

/// <summary>Sygnał na semaforze świetlnym wg Ie-1 (Data/sygnaly_ie1.json).</summary>
public class SygnalDef
{
    public string Kod, Nazwa, Swiatla, Pas;
    /// <summary>Prędkość przy tym semaforze: 0 – największa dozwolona, 40 / 60 / 100; null – sygnał bez prędkości (S1, Sz, Ms).</summary>
    public int? V;
    /// <summary>Co wskazuje następny semafor: „Vmax”, „100”, „40/60”, „Stoj”; null – nie dotyczy.</summary>
    public string Nast;
    public bool Zezwalajacy => V != null || Kod is "Sz" or "Ms2";
    /// <summary>Światła od góry: litera koloru (Z, P, C, B, N) i czy migające.</summary>
    public List<(char C, bool Mig)> Lampy()
    {
        var l = new List<(char, bool)>();
        foreach (var ch in Swiatla ?? "") { if (ch == '*' && l.Count > 0) l[^1] = (l[^1].Item1, true); else l.Add((ch, false)); }
        return l;
    }
    /// <summary>Opis świateł słowami, np. „zielone migające, pomarańczowe, pas zielony”.</summary>
    public string OpisSwiatel()
    {
        if (string.IsNullOrEmpty(Swiatla)) return "ciemny";
        var s = string.Join(", ", Lampy().Select(x => Sygnaly.Kolor(x.C) + (x.Mig ? " migające" : "")));
        if (Pas != null) s += ", pas " + (Pas == "Z" ? "zielony" : "pomarańczowy");
        return s;
    }
}

/// <summary>
/// Tabela sygnałów Ie-1 (§ 4 ust. 13, § 8, zał. 1) i dobór sygnału: prędkość przy semaforze (z przebiegu) × co wskazuje następny semafor.
/// </summary>
public static class Sygnaly
{
    public static readonly Dictionary<string, SygnalDef> Tab = Wczytaj();
    public static string Zrodlo { get; private set; }

    static Dictionary<string, SygnalDef> Wczytaj()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "sygnaly_ie1.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        Zrodlo = doc.RootElement.GetProperty("zrodlo").GetString();
        var res = new Dictionary<string, SygnalDef>();
        foreach (var e in doc.RootElement.GetProperty("sygnaly").EnumerateArray())
        {
            string S(string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            res[S("kod") ?? ""] = new SygnalDef
            {
                Kod = S("kod") ?? "", Nazwa = S("nazwa"), Swiatla = S("swiatla"), Pas = S("pas"), Nast = S("nast"),
                V = e.TryGetProperty("v", out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null,
            };
        }
        return res;
    }

    public static SygnalDef Def(string kod) => kod != null && Tab.TryGetValue(kod, out var d) ? d : null;

    /// <summary>Klasa prędkości sygnalizowanej z ograniczenia przebiegu [km/h] (0 / brak – bez ograniczenia): 40, 60, 100 albo 0 (największa dozwolona).</summary>
    public static int Klasa(double v) => v <= 0 ? 0 : v <= 40 ? 40 : v <= 60 ? 60 : v <= 100 ? 100 : 0;

    /// <summary>Kolumna zał. 1 wg sygnału na następnym semaforze: „Stoj”, gdy nie zezwala na jazdę pociągu, inaczej wg prędkości przy nim.</summary>
    public static string Kolumna(string kodNastepnego)
    {
        var d = Def(kodNastepnego);
        if (d?.V is not int v) return "Stoj";
        return v switch { 0 => "Vmax", 100 => "100", _ => "40/60" };
    }

    /// <summary>Sygnał zezwalający dla prędkości przy semaforze (0 / 40 / 60 / 100) i kolumny (co wskazuje następny).</summary>
    public static string Dobierz(int klasa, string kolumna) =>
        Tab.Values.First(d => d.V == klasa && d.Nast == kolumna).Kod;

    public static string Kolor(char c) => c switch { 'Z' => "zielone", 'P' => "pomarańczowe", 'C' => "czerwone", 'B' => "białe", 'N' => "niebieskie", _ => c.ToString() };

    /// <summary>Sygnał widziany w terenie jako rodzaj zezwolenia (dla ruchu pociągów).</summary>
    public static Aspect AspectOf(string kod) => kod switch
    {
        "Sz" => Aspect.Sz,
        "Ms2" => Aspect.Shunt,
        _ => Def(kod)?.V != null ? Aspect.Train : Aspect.Stop,
    };
}
