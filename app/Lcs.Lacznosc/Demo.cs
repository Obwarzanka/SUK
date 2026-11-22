namespace Lcs.Lacznosc;

/// <summary>
/// Tryb pokazowy (--demo): Koluszki z sąsiadami jak w grze (Gałkówek, Rogów, Żakowice Południowe, Rokiciny; tory szlaków
/// z zapisów czatu). Numery pociągów i godziny zmyślone, teksty komunikatów w brzmieniu gry.
/// </summary>
static class Demo
{
    public static void Wypelnij(MainWindow w)
    {
        var teraz = DateTime.Today.AddHours(10).AddMinutes(44);
        w.UstawStanPokazowy("Koluszki", "Koluszki", teraz.ToString("yyyy-MM-dd HH:mm:ss"),
            new[]
            {
                new Sasiad { Nazwa = "Gałkówek", Gracz = false },
                new Sasiad { Nazwa = "Żakowice Południowe", Gracz = true, Oczekujace = 1 },
                new Sasiad { Nazwa = "Rogów", Gracz = false },
                new Sasiad { Nazwa = "Rokiciny", Gracz = false },
            },
            new[] { ("14023", "l1_kol_it5"), ("40610", "l1_kol_it2"), ("13101", "l1_it988") });

        RozkladSimRail.Punkt P(string n, int przyj, int odj, string tor, bool postoj = true) =>
            new(n, teraz.AddMinutes(przyj), teraz.AddMinutes(odj), tor, postoj ? "II" : "", "", postoj);
        w.UstawRozklad(new List<RozkladSimRail.Pociag>
        {
            new("14023", "ROJ", "Łódź Fabryczna", "Warszawa Zachodnia", "EN57", "65", "128", new() { P("Gałkówek", -8, -7, "1"), P("Koluszki", -2, 3, "2"), P("Rogów", 10, 11, "1") }),
            new("40610", "TME", "Idzikowice", "Łódź Olechów", "ET22", "581", "2850", new() { P("Rokiciny", -20, -19, "1", false), P("Koluszki", -6, 6, "4"), P("Gałkówek", 16, 18, "2", false) }),
            new("13101", "MPE", "Warszawa Wschodnia", "Łódź Fabryczna", "EU07", "254", "420", new() { P("Rogów", -6, -5, "2", false), P("Koluszki", 2, 4, "1"), P("Gałkówek", 12, 13, "1") }),
            new("4512", "TLK", "Kraków Główny", "Gdynia Główna", "EP07", "305", "520", new() { P("Żakowice Południowe", 9, 9, "1", false), P("Koluszki", 16, 16, "3", false), P("Rogów", 26, 26, "1") }),
            new("94322", "TNE", "Tomaszów Mazowiecki", "Skierniewice", "E4DCUd", "485", "2340", new() { P("Rokiciny", 31, 31, "1", false), P("Koluszki", 39, 47, "6"), P("Rogów", 56, 56, "2", false) }),
            new("19211", "ROJ", "Skierniewice", "Łódź Kaliska", "EN76", "75", "152", new() { P("Rogów", 48, 49, "1"), P("Koluszki", 56, 58, "2"), P("Żakowice Południowe", 64, 65, "2") }),
        });

        Wpis W(string czas, bool wych, string sasiad, string klasa, string nr, int? tor, string tekst, string status = "") => new()
        {
            Czas = czas, Wychodzacy = wych, Od = wych ? "Koluszki" : sasiad, Do = wych ? sasiad : "Koluszki", Ai = sasiad != "Żakowice Południowe" && !wych,
            Klasa = klasa, Pociag = nr, Tor = tor, Tekst = tekst, Status = status,
        };
        w.DodajWpis(W("10:31", true, "Rogów", "TrainRequestMessage", "14023", 1, "Czy droga dla pociągu 14023 po torze 1 jest wolna?", "w czacie gry"));
        w.DodajWpis(W("10:32", false, "Rogów", "TrainPermissionMessage", "14023", 1, "Dla pociągu 14023 po torze 1 droga jest wolna o 10:32."));
        w.DodajWpis(W("10:38", false, "Rogów", "TrainDepartedMessage", "13101", 2, "Pociąg 13101 odjechał po torze 2."));
        w.DodajWpis(W("10:42", false, "Rogów", "TrainDepartedMessage", "61114", 2, "Pociąg 61114 odjechał po torze 2."));
        w.DodajWpis(W("10:40", true, "Gałkówek", "TrainPermissionMessage", "40610", 2, "Dla pociągu 40610 po torze 2 droga jest wolna o 10:40.", "w czacie gry"));
        w.DodajWpis(W("10:41", true, "Gałkówek", "TrainDepartedMessage", "14023", 1, "Pociąg 14023 odjechał o 10:41 po torze 1.", "w czacie gry  (auto)"));
        w.DodajWpis(W("10:43", false, "Żakowice Południowe", "TrainRequestMessage", "4512", 3, "Czy droga dla pociągu 4512 po torze 3 jest wolna?"));
        w.DodajWpis(W("10:43", false, "Rokiciny", "TrainDenialMessage", "94322", 2, "Nie można przyjąć pociągu 94322 po zajętym torze szlakowym 2."));
        w.Wybierz("Żakowice Południowe", "TrainPermissionMessage", "4512");
    }
}
