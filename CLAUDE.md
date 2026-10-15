# Instrukcje dla agentów AI

Przed pracą przeczytaj **`ROZWOJ.md`**: zakres projektu, zasady ustalone z właścicielem (sekcja 2), wymagania wyglądu pulpitu (2a), architekturę i pułapki techniczne (4), etapy (5) i otwarte sprawy (5a). Obsługa programu jest opisana w `README.md`.

Najważniejsze zasady:
- Nie nadpisuj `app/LcsWidzew/Data/station.json` bez zgody właściciela. Testuj na kopii (`--station`).
- To ogólny symulator LCS. Nie zaszywaj w kodzie nazw stacji ani kodów posterunków.
- Zależności i obrazy projektuje człowiek. Nie generuj ich automatycznie, możesz tylko proponować.
- Logika srk ma się zgadzać z Ie-20, Ie-104 i `Zrodla/KOMPUTERY_notatki.md`. W razie wątpliwości pytaj właściciela.
- Teksty w programie i rozmowy po polsku.
- Zobrazowanie = obrazy pulpitu (wg Ie-104); układ torowy = plan schematyczny całego węzła (znaki umowne wg Ig-10, `UI/Ig10.cs`). Pulpit rysuje zobrazowanie, komputer zależności liczy z układu.
- Wynik pokaż na zrzucie i zbuduj wersję do uruchamiania (`dotnet build -c Release` w `app/LcsWidzew`, przy zamkniętym symulatorze).
- Po zmianach: build, selftest na `app/LcsWidzew/Testy/station_test.json` (`WYNIK: nastawione 25 z 26`, kontrola zależności 4 z 4), `--edtest`, fuzz edytora `fuzz 300 7` i `dfuzz 300 7` (po 0 błędów). Potem zaktualizuj `ROZWOJ.md`.
