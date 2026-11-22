using System.Text.RegularExpressions;

namespace Lcs.Lacznosc;

/// <summary>
/// Formułki komunikatów EZP w brzmieniu SimRail. Źródło: ponad 1400 komunikatów z czatu dyżurnych zapisanych przez mod
/// (UserData/SimRailMost/polecenia_*.jsonl, 2026-11-20…22). Zdania oznaczone „potwierdzone” występują w grze dosłownie;
/// pozostałe są przybliżone – i tak zastępuje je echo z gry („w czacie gry”), które niesie prawdziwy tekst.
/// Gdy gra poda szablon z miejscami {0}/{1}… (Rodzaj.Szablon z GetTemplateMessage), pierwszeństwo ma szablon gry.
/// </summary>
public static class Formulki
{
    /// <summary>Tekst komunikatu tak, jak pokaże go gra. tor == null – bez toru, czas == null – bez godziny.</summary>
    public static string Tekst(string klasa, string pociag, int? tor, string czas, string powod, string tekst, Rodzaj r = null)
    {
        var zGry = ZSzablonu(r, pociag, tor, czas, powod);
        if (zGry != null) return zGry;
        string n = pociag ?? "…", T = tor?.ToString();
        string poTorze = T != null ? $" po torze {T}" : "";
        string oCzasie = !string.IsNullOrEmpty(czas) ? $" o {czas}" : "";
        return klasa switch
        {
            // potwierdzone: „Czy droga dla pociągu 414042 po torze 2 jest wolna?”, „Czy droga dla pociągu 11583 jest wolna?”
            "TrainRequestMessage" => $"Czy droga dla pociągu {n}{poTorze} jest wolna?",
            // potwierdzone: „Dla pociągu 7337 po torze 4 droga jest wolna o 22:42.”, „Dla pociągu 514026 po torze 2 droga jest wolna.”
            "TrainPermissionMessage" => $"Dla pociągu {n}{poTorze} droga jest wolna{oCzasie}.",
            // potwierdzone: „Pociąg 514040 odjechał o 21:26 po torze 2.”, „Pociąg 74564 odjechał po torze 1.”, „Pociąg 512040 odjechał o 16:58.”
            "TrainDepartedMessage" => $"Pociąg {n} odjechał{oCzasie}{poTorze}.",
            // potwierdzone: „Pociąg 414042 odwołany.”
            "TrainCancelledMessage" => $"Pociąg {n} odwołany.",
            // potwierdzone: „Co odjechało?”
            "TrainQuestionMessage" => "Co odjechało?",
            "TrainDenialMessage" => Odmowa(n, T, powod),
            // przybliżone (brak w zapisach)
            "TrainArrivedMessage" => $"Pociąg {n} przyjechał{oCzasie}.",
            "LockLineblockMessage" => "Proszę zablokować blok początkowy.",
            "CustomTextMessage" => tekst ?? "",
            _ => Teksty.Etykieta(klasa),
        };
    }

    static string Odmowa(string n, string T, string powod) => powod switch
    {
        // potwierdzone: „…po torze 2 ze względu na brak niezajętych torów w stacji.”
        "NoTracksAvailable" => $"Nie można przyjąć pociągu {n}{(T != null ? $" po torze {T}" : "")} ze względu na brak niezajętych torów w stacji.",
        // potwierdzone: „Nie można przyjąć pociągu … po zajętym torze szlakowym 4.”
        "TrackOccupied" => T != null ? $"Nie można przyjąć pociągu {n} po zajętym torze szlakowym {T}." : $"Nie można przyjąć pociągu {n} – tor zajęty.",
        // potwierdzone: „…po torze 2, który jest nieznany.”, „…na nieznany tor.”
        "UnknownLine" => T != null ? $"Nie można przyjąć pociągu {n} po torze {T}, który jest nieznany." : $"Nie można przyjąć pociągu {n} na nieznany tor.",
        _ => $"Nie można przyjąć pociągu {n}{(T != null ? $" po torze {T}" : "")} – {Teksty.Powody.FirstOrDefault(x => x.Kod == powod).Opis ?? "odmowa"}.",
    };

    /// <summary>Szablon z gry z miejscami {0}, {1}… w kolejności Arg (Train, TrackNumber, Time, DenialReason) albo {Train}…</summary>
    static string ZSzablonu(Rodzaj r, string pociag, int? tor, string czas, string powod)
    {
        if (r == null || string.IsNullOrEmpty(r.Szablon) || !r.Szablon.Contains('{')) return null;
        string Wart(string a) => a switch
        {
            "Train" => pociag,
            "TrackNumber" => tor?.ToString(),
            "Time" => czas,
            "DenialReason" => Teksty.Powody.FirstOrDefault(x => x.Kod == powod).Opis,
            _ => null,
        };
        var s = r.Szablon;
        for (int i = 0; i < r.Arg.Length; i++)
        {
            var v = Wart(r.Arg[i]);
            if (v == null) return null;   // brak wartości – szablon gry niepełny, zostaje formułka
            s = s.Replace("{" + i + "}", v).Replace("{" + r.Arg[i] + "}", v);
        }
        return s.Contains('{') ? null : s;
    }

    static readonly Regex TorWTekscie = new(@"torze (?:szlakowym )?(\d+)", RegexOptions.CultureInvariant);

    /// <summary>
    /// Numer toru z tekstu komunikatu gry („po torze 224”). Mod 0.10–0.12 odczytuje TrackNumber z obiektu gry jako 0
    /// (Nullable przez Il2Cpp) – tekst jest wiarygodny, liczba z moda tylko gdy &gt; 0.
    /// </summary>
    public static int? TorZTekstu(string tekst, int? zModa)
    {
        if (!string.IsNullOrEmpty(tekst))
        {
            var m = TorWTekscie.Match(tekst);
            if (m.Success && int.TryParse(m.Groups[1].Value, out var t)) return t;
        }
        return zModa > 0 ? zModa : null;
    }
}
