# Edytor rozkładu jazdy (EdytorRJ) – plan do akceptacji

Projekt z 2026-10-29. **Stan: zaakceptowany 2026-10-30 z propozycjami z § 9. R1 zrobione (czeka na akceptację).** Polecenie: „Przygotuj plan osobnego edytora rozkładu jazdy (numery, kategorie Ir-11, przewoźnik, zmiany w biegu)”.
Wcześniejsze decyzje, które plan uwzględnia (`PLAN_EDR.md`, E7–E8):
- kategoria wg Ir-11 i przewoźnik będą wpisywane w edytorze rozkładu jazdy;
- uwagi eksploatacyjne i uwagi przewoźnika wpisuje dyspozytor, z edytora rozkładu albo z panelu instruktora;
- EDR jest tylko zobrazowaniem rozkładu: niczego nie wyłącza w symulacji.

---

## 1. Stan obecny

Rozkład ma dziś dwa źródła i żadnego edytora.

| Źródło | Plik | Co ma | Czego nie ma |
|---|---|---|---|
| Import GTFS / PKP IC (`tools/gtfs_import.py`, `tools/pkpic_import.py`) | `Data/rozklad.json`, `Data/rozklad_ic.json` | numer, nazwa, `cat` (IC, ŁKA…), pojazd, długość, relacja, punkty biegu z przyjazdem / odjazdem / torem / peronem, zmiany numeru (`numsAt`, `renumIdx`) i czoła (`revIdx`), jazdy próżne | kategorii Ir-11 (`kat` puste), pełnej nazwy przewoźnika, zmiany kategorii w biegu, uwag |
| Plik układu (pisze człowiek) | `<układ>.rozklad.json` | czas pojawienia się, numer, `kat` (ŁKA, PR, IC, TOW…), szlak wjazdu albo tor startu, szlak wyjazdu, postoje, odjazd, skład | punktów biegu z godzinami, zmiany numeru, przewoźnika, Ir-11 |

Ograniczenia w kodzie:
- `Sim/RealTimetable.cs` ma zaszyte posterunki Widzewa i Fabrycznej („LW”, „LF”), bazę ŁKA i tory. To łamie zasadę ogólnego symulatora; nowy format ma tego nie potrzebować.
- Zmiana numeru działa tylko na posterunku, na którym pociąg stoi (`TrainTerm`). W biegu bez postoju numer się nie zmienia.
- Kategoria jest jedna na pociąg (`TimetableEntry.KatIr11`). Wewnętrzne „PRÓŻ” przy Fabrycznej to wyjątek zaszyty w imporcie.
- EDR (`Sim/Edr/Edr.cs`) czyta kategorię, przewoźnika, relację, uwagi i bieg z `TimetableEntry`. Pola „Opis pociągu” bez danych (numer międzynarodowy, zamówienie, rodzaj rozkładu, wniosek…) są puste albo z danych lokalnych EDR.

## 2. Cel

Osobny program `app/Lcs.EdytorRJ` (`EdytorRJ.exe`), jak edytory UT, zobrazowania i taboru. W nim człowiek:
1. wczytuje rozkład z importu (GTFS / PKP IC) albo zaczyna od pustego;
2. dla każdego pociągu ustawia numer, kategorię Ir-11, przewoźnika i opis;
3. układa bieg przez posterunki układu (godziny, postoje, tory, perony);
4. wpisuje zmiany w biegu: numeru, kategorii, czoła, przewoźnika;
5. zapisuje plik, który czyta symulator i EDR.

Edytor nie wymyśla rozkładu sam. Może proponować (np. czas przejazdu szlakiem z długości i prędkości), ale wpis zatwierdza człowiek, tak jak przy zależnościach.

## 3. Dane

### 3.1 Plik rozkładu
Nowy format `<układ>.rj.json` obok pliku układu. Zastępuje oba dzisiejsze źródła; stare pliki edytor importuje (§ 5). Zapis przez plik `.tmp` i podmianę z kopią `.bak`, jak dzienniki EDR.

```json
{
  "wersja": 1,
  "dzien": "2026-10-15",
  "zrodlo": "GTFS Polish Trains + poprawki ręczne",
  "przewoznicy": [ { "kod": "IC", "nazwa": "PKP Intercity S.A." } ],
  "pociagi": [ {
    "id": "1511-0-a",
    "nazwa": "LAZUR", "nrMiedzynarodowy": "", "zamowienie": "", "rodzajRozkladu": "roczny",
    "pojazd": "EP07", "sklad": "", "dlugosc": 260,
    "relacja": { "od": "Łódź Fabryczna", "do": "Gdynia Główna" },
    "uwagiEksp": "", "uwagiPrzew": "",
    "odcinki": [
      { "numer": "1511/0", "kat": "PRÓŻ", "przewoznik": "IC", "uwagi": "dojazd próżny" },
      { "numer": "1511/0", "kat": "<kod Ir-11 ze słownika>", "przewoznik": "IC" }
    ],
    "bieg": [
      { "post": "LW", "przyjazd": "05:05:00", "odjazd": "05:05:00", "postoj": false, "tor": "", "peron": "", "odcinek": 0, "szlakZ": "2O" },
      { "post": "LF", "przyjazd": "05:11:00", "odjazd": "05:31:00", "postoj": true, "tor": "7", "peron": "IV", "odcinek": 0, "zmianaCzola": true },
      { "post": "LW", "przyjazd": "05:37:30", "odjazd": "05:38:30", "postoj": true, "tor": "1", "peron": "II", "odcinek": 1, "szlakDo": "1A" }
    ]
  } ]
}
```

- **Odcinek** to część biegu z jednym numerem, kategorią i przewoźnikiem. Zmiana któregokolwiek z nich zaczyna nowy odcinek.
- **Punkt biegu** wskazuje odcinek, z którym pociąg **odjeżdża** z posterunku. Zmiana numeru albo kategorii jest więc w punkcie i może być także **bez postoju** (np. na posterunku odgałęźnym). Zmiana na szlaku nie występuje.
- Kody posterunków i szlaków pochodzą z układu, edytor nie ma ich zaszytych w kodzie. Szlak wjazdu w pierwszym punkcie i wyjazdu w ostatnim wybiera się z szlaków posterunku.
- Godziny są w czasie doby (z sekundami); przejście przez północ edytor liczy sam.

### 3.2 Słownik kategorii Ir-11
Plik `Data/kategorie_ir11.json`: kod (np. trzyliterowy), nazwa, rodzaj (pasażerski / towarowy / służbowy), kolor w zobrazowaniu, prędkość i długość domyślna (dziś `Cats` w `Sim/Trains.cs`) i skład domyślny z edytora taboru.
**Treść słownika poda właściciel.** Instrukcji Ir-11 nie ma w `Zrodla/`. Agent może przygotować propozycję oznaczoną „do potwierdzenia”, jeśli właściciel dołoży PDF Ir-11.

### 3.3 Przewoźnicy
Słownik w pliku rozkładu (kod, pełna nazwa). Import uzupełnia kody (IC, ŁKA, PR, KM…), pełne nazwy wpisuje człowiek. EDR pokazuje pełną nazwę (dziś kod).

## 4. Okno edytora

Wygląd i obsługa jak w pozostałych edytorach: menu, lista po lewej, szczegóły po prawej, „Sprawdź”, zapis z `.bak`.

1. **Lista pociągów:** numer(y), kategoria, przewoźnik, relacja, pierwsza i ostatnia godzina w obszarze. Filtry: numer, kategoria, przewoźnik, posterunek, przedział godzin. Kopiuj / usuń / nowy pociąg.
2. **Karta pociągu:** nazwa, numer międzynarodowy, zamówienie, rodzaj rozkładu, relacja, pojazd i skład (lista z edytora taboru), długość, uwagi eksploatacyjne i przewoźnika.
3. **Odcinki:** tabela numer / kategoria Ir-11 (lista ze słownika) / przewoźnik / uwagi; kolorowy pasek odcinka.
4. **Bieg:** tabela punktów (posterunek z układu, przyjazd, odjazd, postój, tor z listy torów posterunku, peron, odcinek odjazdu, zmiana czoła, szlak wjazdu / wyjazdu). Zmiana numeru lub kategorii w punkcie to wybór innego odcinka w kolumnie „odcinek”; nowy odcinek – przycisk w wierszu.
5. **Wykres ruchu** (podgląd, drugi etap): posterunki na osi pionowej, czas na poziomej, linie pociągów w kolorze kategorii. Przeciąganie punktu zmienia godzinę.
6. **Sprawdź:** brak szlaku łączącego kolejne posterunki, godzina wstecz, postój na torze bez peronu przy pociągu pasażerskim, tor spoza posterunku, kategoria spoza słownika, ten sam numer dwa razy o tej samej porze, dwa pociągi na jednym torze stacyjnym w tym samym czasie (uwaga, nie błąd).

## 5. Import

- `rozklad.json` / `rozklad_ic.json` (obecny format): punkty biegu, `numsAt` / `renumIdx` → odcinki, `revIdx` → zmiana czoła, `emptyIn` / `emptyOut` → odcinek próżny, `cat` → podpowiedź kategorii (człowiek wybiera kod Ir-11).
- `<układ>.rozklad.json` (pisany ręcznie): pociąg z jednym odcinkiem, bieg z szlaku wjazdu, postojów i szlaku wyjazdu (godzinę pośrednią edytor proponuje do zatwierdzenia).
- Skrypty `tools/*_import.py` zostają. Edytor czyta ich wynik; ewentualnie dopisze się do nich zapis `.rj.json`.
- Posterunki spoza układu (np. „Łódź Olechów” jako sąsiad) zamieniają się na szlak wjazdu / wyjazdu, nie na punkt biegu.

## 6. Symulator i EDR

- **Wczytanie:** `Sim/RozkladRJ.cs` czyta `<układ>.rj.json` do `TimetableEntry`. Gdy pliku nie ma, działa jak dziś (`rozklad.json`, `<układ>.rozklad.json`).
- **Zmiana numeru i kategorii w biegu:** `TrainTerm` dostaje zmianę bez zatrzymania – wykonuje się, gdy czoło pociągu mija posterunek (dziś tylko po postoju). Kategoria zmienia kolor, prędkość i długość domyślną tylko wtedy, gdy pociąg nie ma składu z edytora taboru.
- **Zaszyte posterunki:** logika Widzewa / Fabrycznej z `RealTimetable.cs` (zmiana czoła w ŁF, baza ŁKA w ŁW, zjazd luzem) przechodzi do danych pliku (zmiana czoła, odcinek próżny, start z toru, manewr). Bez pliku `.rj.json` zachowanie się nie zmienia.
- **EDR:**
  - „Rodz.” – kategoria odcinka, z którym pociąg przyjeżdża, a w wierszu odjazdu (po zmianie numeru) – odcinka odjazdu;
  - „Przewoźnik” – pełna nazwa;
  - „Opis pociągu” – pola z karty (numer międzynarodowy, zamówienie, rodzaj rozkładu, wniosek…) zamiast pustych; dane lokalne EDR nadal je nadpisują;
  - „Trasa pociągu” – „Rodz.” i „Nr poc.” wg odcinka w każdym punkcie;
  - wiersz wykazu na każdy przejazd – z biegu pliku dla każdego rozkładu (dziś tylko z GTFS).
- **Uwagi dyspozytora:** wpisane w EDR (polecenie `uwagi`) zostają w sesji; edytor RJ ustawia uwagi „z rozkładu”.

## 7. Etapy (każdy z akceptacją właściciela)

- **R1 – format i wczytanie:** `.rj.json` w `Lcs.Model` (wspólny dla edytora i symulatora), import z `rozklad.json` / `rozklad_ic.json` / `<układ>.rozklad.json`, wczytanie w symulatorze do `TimetableEntry` z odcinkami. Test: import Widzewa → ten sam wykaz EDR i ten sam przebieg ruchu w selftest scenariusza `ic_05`.
- **R2 – zmiana numeru i kategorii w biegu w symulatorze i EDR:** zmiana bez zatrzymania, „Rodz.” i przewoźnik wg odcinka, opis pociągu z karty.
- **R3 – edytor: lista, karta, odcinki, bieg, zapis:** `app/Lcs.EdytorRJ`, „Sprawdź”, słowniki kategorii i przewoźników.
- **R4 – usunięcie zaszytych posterunków z `RealTimetable.cs`:** reguły obiegów ŁF / ŁW jako dane w pliku Widzewa; selftest bez zmian.
- **R5 – wykres ruchu** w edytorze (podgląd, przeciąganie godzin).
- **R6 – propozycje:** czas przejazdu szlakiem (długość, Vmax), zajętość torów stacyjnych, kopiowanie pociągu z przesunięciem (takt).

## 8. Testy

- `EdytorRJ.exe --test wynik.txt zrzut.png` (jak edytor UT): import, odcinki, zmiana numeru bez postoju, „Sprawdź”, zapis i odczyt bez zmian.
- `--edrtest`: nowe sprawdzenia „Rodz.” i przewoźnika wg odcinka, numer zmieniony bez postoju.
- Selftest na `station_test.json` bez zmian; scenariusz `ic_05` z `.rj.json` daje to samo co z `rozklad.json`.

## 9. Pytania do właściciela

1. **Słownik Ir-11:** czy dołożysz instrukcję Ir-11 (PDF) do `Zrodla/`, czy podasz listę kategorii sam? Bez niej słownik będzie pusty („do potwierdzenia”).
2. **Format:** nowy plik `<układ>.rj.json` zastępujący oba dzisiejsze (propozycja), czy rozszerzenie `<układ>.rozklad.json`?
3. **Zmiana numeru / kategorii bez postoju:** tylko na posterunku (propozycja), czy także w punkcie na szlaku (np. granica zarządcy)?
4. **Kto edytuje w trakcie symulacji:** edytor RJ działa tylko przed uruchomieniem scenariusza (propozycja), czy instruktor ma też zmieniać rozkład „na żywo”, np. pociąg dodatkowy, odwołanie, zmiana numeru?
5. **Kolejność:** zacząć od R1–R2 (dane i symulator, bez okna), czy od R3 (okno edytora na obecnych danych)?

## 10. Stan

- 2026-10-30: właściciel zaakceptował plan z propozycjami z § 9 (słownik Ir-11 – później od właściciela; nowy plik `<układ>.rj.json`; zmiana numeru / kategorii tylko na posterunku; edycja przed uruchomieniem scenariusza; najpierw R1–R2).
- **2026-10-30: R1 zrobione (czeka na akceptację właściciela).**
  - **Biblioteka `app/Lcs.RozkladJazdy`** (bez WPF, jak `Lcs.Tabor`; nie `Lcs.Model`, bo ten ma cały model UT, a nazwa `Lcs.Rozklad` kolidowała z klasą `Rozklad` w hoście świata): `RjDokument` (wersja, dzień, źródło, względny, przewoźnicy, tory zaplecza, pociągi; zapis `.tmp` + `.bak`, odczyt, `Sprawdz`, czasy „GG:MM:SS”), `RjPociag` (karta, pojawia się, szlak wjazdu / wyjazdu, odjazd, start na torze albo z zaplecza – `RjStart`, koniec biegu – `RjKoniec`, pociąg towarzyszący – `Rodzic`), `RjOdcinek` (numer, kategoria Ir-11, kategoria ruchu, przewoźnik, uwagi), `RjPunkt` (posterunek, przyjazd, odjazd, postój, tor, peron, odcinek odjazdu, zmiana czoła, zmiana kategorii ruchu).
  - **Import** (`Sim/RozkladRJ.cs`): `Rozklad.ImportRozkladu` – `rozklad.json` / `rozklad_ic.json` (dobowy; reguły Widzewa z `RealTimetable` – zmiana czoła w ŁF, odcinek próżny, baza ŁKA, odstawienie, zjazd luzem – zapisane jako dane pociągu), `Rozklad.ImportPliku` – `<układ>.rozklad.json` (względny). Polecenie: `LcsWidzew.exe --station <układ> --rj-import wynik.rj.json [rozklad|plik]`.
  - **Wczytanie**: gdy obok układu jest `<układ>.rj.json` – dobowy zastępuje rozkład rzeczywisty w scenariuszach `ic_*` (okno godzin jak dotąd), względny – plik układu w „Ruchu podstawowym”. Bez pliku – bez zmian. Błędny plik – zdarzenie, rozkład jak bez pliku. Host świata zna plik jako wejście (`timetableRj`).
  - **Test** `LcsWidzew.exe --station <układ> --rjtest wynik.txt [scenariusze]` (kopia układu w katalogu tymczasowym): Widzew – 15 z 15 (import 332 pociągów / 456 odcinków, zapis i odczyt bez zmian; `ic_05`, `ic_08`, `ic_15`, `ic_18`: ta sama kolejka pociągów, ten sam wykaz EDR i ten sam ruch po 20 min), `lcs_testowe` – 3 z 3 (plik układu). Wyniki `zrzuty/rj_r1/`, przykładowy plik Widzewa `zrzuty/rj_r1/station.rj.json` (nie w `Data` – plik zmieniłby źródło rozkładu bez zgody właściciela).
  - Selftest 25 z 26 / 5 z 5, `--edrtest` 136 z 136. Testy hosta świata: te same 32 porażki co na czystym HEAD (`82bc2fd`, logi i nazwy wyjść) – nie od R1.
  - **Nie zrobione w R1 (zgodnie z planem):** zmiana numeru / kategorii bez postoju w ruchu (R2 – dziś dane to opisują, symulator wykonuje zmianę dopiero po zatrzymaniu); kategoria wg Ir-11 pusta (słownik od właściciela); reguły Widzewa są w danych, ale tekst opisu i nazwy (np. „ŁKA”, „Olechów”) – z importu Widzewa; przykładowe jednostki ŁKA (gdy jest tylko `rozklad_ic.json`) nie są importowane.
