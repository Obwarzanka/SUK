# Instrukcje dla agentów AI

Przed pracą przeczytaj **`ROZWOJ.md`** i **`PLAN_EDYTOR_UT.md`** (nowy edytor układu torowego): zakres projektu, zasady ustalone z właścicielem (sekcja 2), wymagania wyglądu pulpitu (2a), architekturę i pułapki techniczne (4), etapy (5) i otwarte sprawy (5a). Obsługa programu jest opisana w `README.md`.

Najważniejsze zasady:
- Nie nadpisuj `app/LcsWidzew/Data/station.json` bez zgody właściciela. Testuj na kopii (`--station`).
- To ogólny symulator LCS. Nie zaszywaj w kodzie nazw stacji ani kodów posterunków.
- Zależności i obrazy projektuje człowiek. Nie generuj ich automatycznie, możesz tylko proponować.
- Logika srk ma się zgadzać z Ie-20, Ie-104 i `Zrodla/KOMPUTERY_notatki.md`. W razie wątpliwości pytaj właściciela.
- Teksty w programie i rozmowy po polsku.
- Zobrazowanie = obrazy pulpitu (wg Ie-104). Układ torowy (UT) = rzeczywista geometria w PUWG 1992 (X – północ, Y – wschód, Z – wysokość, metry), symbole wg Ig-10. Pulpit rysuje zobrazowanie, komputer zależności liczy z układu.
- Edytor UT to osobny program `app/Lcs.EdytorUT` z modelem `app/Lcs.Model`. Stary edytor w `LcsWidzew` jest usunięty (commit `d2c7431`), nie przywracaj go i nie przenoś jego kodu poza tym, co wymienia `PLAN_EDYTOR_UT.md` § 4.
- Wynik pokaż na zrzucie i zbuduj wersję do uruchamiania (`dotnet build -c Release` w `app/LcsWidzew` i w `app/Lcs.EdytorUT`, przy zamkniętym programie).
- Po zmianach: build, selftest na `app/LcsWidzew/Testy/station_test.json` (`WYNIK: nastawione 25 z 26`, kontrola zależności 4 z 4), test edytora `EdytorUT.exe --test wynik.txt zrzut.png` (`błędów: 0`). Potem zaktualizuj `ROZWOJ.md` (skrót), `HISTORIA_EDYTOR_UT.md` (szczegółowy opis prac nad edytorem UT), stan w `PLAN_EDYTOR_UT.md` i obsługę w `README.md`.
