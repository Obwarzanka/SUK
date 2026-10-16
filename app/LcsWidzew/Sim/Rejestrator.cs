using System.Text.RegularExpressions;
using LcsWidzew.Model;

namespace LcsWidzew.Sim;

/// <summary>
/// Rejestrator zdarzeń i alarmów (okno ZDARZENIA na pulpicie, Ie-104 § 7) – usługa wspólna dla wszystkich modułów
/// symulatora: komputera zależności, urządzeń przytorowych, ruchu, otoczenia i instruktora.
/// </summary>
public class Rejestrator
{
    readonly Station L;
    readonly Func<double> czas;
    readonly Func<string> zegar;
    readonly Func<User> uzytkownik;
    long evSeq;

    public List<EventRecord> Events = new();
    public List<EventRecord> Alarms = new();
    public List<(string Time, string Text)> Remarks = new();

    public event Action<EventRecord> EventAdded;
    public event Action<EventRecord> AlarmAdded;
    public event Action Beep;

    /// <param name="czas">czas symulacji [s]</param>
    /// <param name="zegar">godzina do kolumny „Czas”</param>
    /// <param name="uzytkownik">zalogowany operator (kolumna „Operator”)</param>
    public Rejestrator(Station layout, Func<double> czas, Func<string> zegar, Func<User> uzytkownik)
    {
        L = layout; this.czas = czas; this.zegar = zegar; this.uzytkownik = uzytkownik;
    }

    // przedrostki posterunków (np. „LW:”) i ich skróty (np. ŁW) – z wczytanego układu, nie zaszyte w programie
    Regex prefix;
    Regex Prefix => prefix ??= L.Posts.Count == 0 ? new Regex("(?!)") : new Regex(@"\b(" + string.Join("|", L.Posts.Keys.Select(Regex.Escape)) + "):", RegexOptions.Compiled);
    public string Clean(string s) => s == null ? "" : Prefix.Replace(s, "");
    /// <summary>Skrót posterunku: inicjały nazwy (Łódź Widzew → ŁW, Koluszki → Ko).</summary>
    public static string PostShort(Station L, string post)
    {
        if (string.IsNullOrEmpty(post) || !L.Posts.TryGetValue(post, out var n) || string.IsNullOrWhiteSpace(n)) return post ?? "";
        var w = n.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(x => char.IsLetter(x[0])).ToList();
        return w.Count == 1 ? (w[0].Length <= 4 ? w[0] : w[0][..2]) : string.Concat(w.Select(x => char.ToUpper(x[0])));
    }
    string StationOf(string key, string text)
    {
        var p = Station.PostOf(key);
        if (p == "") { var m = Prefix.Match(text ?? ""); if (m.Success) p = m.Groups[1].Value; }
        return L.Posts.ContainsKey(p) ? PostShort(L, p) : "LCS";
    }

    public EventRecord Log(string kategoria, string text, string typ = "", string obiekt = "", string zrodlo = "System")
    {
        var e = new EventRecord { Id = ++evSeq, T = czas(), Czas = zegar(), Obszar = L.Name ?? "LCS", Kategoria = kategoria, Zdarzenie = Clean(text), TypObiektu = typ, Obiekt = Clean(obiekt), Stacja = StationOf(obiekt, text), Operator = uzytkownik()?.Name ?? "", Zrodlo = zrodlo };
        Events.Add(e);
        EventAdded?.Invoke(e);
        return e;
    }

    /// <summary>Alarm (Ie-104 § 7): lista z potwierdzeniem i znacznikiem trwania stanu alarmowego.</summary>
    public void Alarm(string text, string typ = "", string obiekt = "", string key = null, Func<bool> stillActive = null)
    {
        key ??= text;
        if (Alarms.Any(a => a.Key == key && (!a.Ack || a.Active))) return;
        var e = new EventRecord { Id = ++evSeq, T = czas(), Czas = zegar(), Obszar = L.Name ?? "LCS", Kategoria = "Alarm", Zdarzenie = Clean(text), TypObiektu = typ, Obiekt = Clean(obiekt), Stacja = StationOf(obiekt, text), Operator = uzytkownik()?.Name ?? "", Key = key, StillActive = stillActive };
        Alarms.Add(e);
        AlarmAdded?.Invoke(e);
        Log("Alarm", text, typ, obiekt);
        Beep?.Invoke();
    }

    public void Remark(string text) { Remarks.Add((zegar(), Clean(text))); Log("Uwaga", text, "Ocena"); }

    /// <summary>Koniec cyklu: alarmy, których przyczyna ustąpiła, dostają znacznik „ustąpił”.</summary>
    public void UpdateAlarms() { foreach (var a in Alarms) if (a.Active && a.StillActive != null && !a.StillActive()) a.Active = false; }
}
