namespace Lcs.Lacznosc;

/// <summary>Tryb pokazowy (--demo): dane przykładowe do sprawdzenia wyglądu bez gry. Numery i godziny są zmyślone.</summary>
static class Demo
{
    public static void Wypelnij(MainWindow w)
    {
        var teraz = DateTime.Today.AddHours(10).AddMinutes(44);
        w.UstawStanPokazowy("Koluszki", "Koluszki", teraz.ToString("yyyy-MM-dd HH:mm:ss"),
            new[]
            {
                new Sasiad { Nazwa = "Łódź Widzew", Gracz = false, Nieprzeczytane = 1 },
                new Sasiad { Nazwa = "Rogów", Gracz = false },
                new Sasiad { Nazwa = "Słotwiny", Gracz = true, Oczekujace = 1 },
                new Sasiad { Nazwa = "Mikołajów", Gracz = false },
            },
            new[] { ("14023", "l1_kol_it5"), ("40610", "l1_kol_it2") });

        RozkladSimRail.Punkt P(string n, int przyj, int odj, string tor, bool postoj = true) =>
            new(n, teraz.AddMinutes(przyj), teraz.AddMinutes(odj), tor, postoj ? "II" : "", "", postoj);
        w.UstawRozklad(new List<RozkladSimRail.Pociag>
        {
            new("14023", "ROJ", "Łódź Fabryczna", "Warszawa Zachodnia", "EN57", "65", "128", new() { P("Łódź Widzew", -12, -11, "1"), P("Koluszki", -2, 3, "2"), P("Rogów", 10, 11, "1") }),
            new("40610", "TME", "Idzikowice", "Łódź Olechów", "ET22", "581", "2850", new() { P("Słotwiny", -20, -19, "1", false), P("Koluszki", -6, 6, "4"), P("Łódź Widzew", 16, 18, "3") }),
            new("13101", "MPE", "Warszawa Wschodnia", "Łódź Fabryczna", "EU07", "254", "420", new() { P("Rogów", 4, 4, "2", false), P("Koluszki", 11, 13, "1"), P("Łódź Widzew", 24, 25, "2") }),
            new("4512", "TLK", "Kraków Główny", "Gdynia Główna", "EP07", "305", "520", new() { P("Słotwiny", 15, 15, "1", false), P("Koluszki", 22, 22, "3", false), P("Łódź Widzew", 33, 34, "1") }),
            new("94322", "TNE", "Tomaszów Mazowiecki", "Skierniewice", "E4DCUd", "485", "2340", new() { P("Mikołajów", 31, 31, "1", false), P("Koluszki", 39, 47, "6"), P("Rogów", 56, 56, "2", false) }),
            new("19211", "ROJ", "Skierniewice", "Łódź Kaliska", "EN76", "75", "152", new() { P("Rogów", 48, 49, "1"), P("Koluszki", 56, 58, "2"), P("Łódź Widzew", 69, 70, "1") }),
        });

        w.DodajWpis(new Wpis { Czas = "10:31", Wychodzacy = true, Od = "Koluszki", Do = "Łódź Widzew", Klasa = "TrainRequestMessage", Pociag = "14023", Tekst = "Czy droga dla pociągu 14023 po torze 1 jest wolna?", Tor = 1, Status = "w czacie gry" });
        w.DodajWpis(new Wpis { Czas = "10:32", Wychodzacy = false, Od = "Łódź Widzew", Do = "Koluszki", Ai = true, Klasa = "TrainPermissionMessage", Pociag = "14023", Tekst = "Dla pociągu 14023 po torze 1 droga jest wolna o 10:32.", Tor = 1 });
        w.DodajWpis(new Wpis { Czas = "10:38", Wychodzacy = true, Od = "Koluszki", Do = "Słotwiny", Klasa = "TrainDepartedMessage", Pociag = "40610", Tekst = "Pociąg 40610 odjechał o 10:38.", Tor = 1, Status = "w czacie gry" });
        w.DodajWpis(new Wpis { Czas = "10:43", Wychodzacy = false, Od = "Łódź Widzew", Do = "Koluszki", Ai = true, Klasa = "TrainRequestMessage", Pociag = "13101", Tekst = "Czy droga dla pociągu 13101 po torze 2 jest wolna?", Tor = 2 });
        w.Wybierz(0, "TrainRequestMessage", "");
    }
}
