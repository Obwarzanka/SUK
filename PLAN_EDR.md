# Plan – EDR (elektroniczny dziennik ruchu) w symulatorze

Wzór: `Zrodla/EDR_notatki.md` (podręcznik PKP PLK 2017) i `Zrodla/ISDR_zdjecia.md` § 5, § 7.7 (film ze stanowiska ISDR).
Etap 8 w `ROZWOJ.md`. Etapy robimy po kolei, każdy z akceptacją właściciela.

## 1. Decyzje właściciela (2026-10-27)

1. EDR to **okno w symulatorze**, które da się przenieść na drugi monitor. Nie robimy osobnego programu.
2. **Każdy szlak** ma swój dziennik ruchu, także szlak z SBL.
3. **Wzory telefonogramów** właściciel poda później. Do tego czasu jest tylko telefonogram „[bez szablonu]” (treść wpisuje się ręcznie).
4. Godziny wpisuje dyżurny **ręcznie**. Dwuklik na komórce wstawia bieżącą godzinę, a kółko myszy zmienia minuty albo godziny (zależnie od części pola pod kursorem).
   Posterunki sąsiednie (`Otoczenie`) nie wypełniają dziennika same.
5. **Nie robimy oceny.** EDR nie porównuje wpisów z rejestratorem i nie zmienia wyniku ćwiczenia.

## 2. Zasady

- EDR niczego nie nastawia i nie blokuje w komputerze zależności. Tylko zapisuje to, co wpisze dyżurny, i pokazuje stan z meldunków.
- Nie zaszywamy w kodzie nazw stacji ani szlaków. Dzienniki, linie, kierunek parzysty i przejazdy pochodzą z danych stacji, które projektuje człowiek.
- Wpisów się nie kasuje. Każda zmiana wiersza jest zapisywana jako nowa wersja, bo z tego powstaje wyciąg z historią.
- Teksty i wygląd okna są jak na zdjęciach z podręcznika i filmu.

## 3. Architektura

- **`Sim/Edr/`** (rdzeń `Lcs.Core`, bez WPF) to moduł obok `Rejestrator` i `Otoczenie`, tworzony w `Simulator`. Zawiera:
  - `Edr` – dzienniki stanowisk, wykaz pociągów, polecenia (przyjęcie, wyprawienie, zmiana komórki, wpis dowolny, telefonogram);
  - `DziennikRuchu` – strona = doba, wiersze z wersjami, wiersze przyjęcia i wyprawienia, wpisy na całą szerokość, klamra „do km i z powrotem”;
  - `DziennikTelefoniczny`.
- Dane są w symulatorze (na serwerze). Stanowisko pobiera je przez `ILcsSession`, tak jak dziś pobiera książkę (`BookData`), a zmiany wysyła jako polecenia EDR. Każde stanowisko ma swoje szlaki (wg obszaru sterowania), instruktor widzi wszystkie.
- **Okno `UI/EdrWindow.cs`** zawiera:
  - u góry wykaz pociągów;
  - na dole zakładki dzienników (po dwa obok siebie) i „Dziennik telefoniczny”;
  - pod dziennikami kontrolkę zajętości torów stacyjnych;
  - w prawym górnym rogu zegar;
  - na stopce „Użytkownik / Zalogowany od / TRYB OPERATORA”.

### Źródła danych

| Element | Źródło |
|---|---|
| Wykaz pociągów | `Rozklad.Queue` (`TimetableEntry`), rozkład GTFS |
| Godziny rzeczywiste w wykazie | zdarzenia ruchu (`Trains` / rejestrator) |
| Dzienniki | `station.json` → `blocks` (jeden dziennik na szlak) |
| Ikony torów szlakowych | `BlockInd` (Dir, TrainOn) z meldunku |
| Kontrolka torów stacyjnych | `Indication.Secs` |
| Dyżur objął / Prowadzi | zalogowany użytkownik stanowiska |

### Dane do uzupełnienia przez człowieka (edytor zobrazowania albo UT)

- Dla szlaku: numer i nazwa linii, kierunek parzysty, nazwa dziennika. Gdy pola są puste, nazwa dziennika to nazwa sąsiada i numer linii.
- Dla przejazdu z dróżnikiem: km i sposób powiadamiania (godzina albo ukośnik).
- Lista torów stacyjnych do kontrolki. Gdy jej nie ma, kontrolka bierze tory stacyjne z układu.

## 4. Etapy

**E1 – Dziennik szlaku**
- Model wiersza z wersjami.
- Kolumny 1–8: numer nieparzysty, numer parzysty, tor, droga wolna, pociąg odjechał, pociąg przyjechał, uwagi, dróżnicy.
- Przyciski pustego przyjęcia i wyprawienia (okno jak Rys. 34 / 36).
- Niebieskie tło pól wymaganych. Wymagane pola zależą od kierunku i rodzaju blokady szlaku (`type`).
- Edycja godzin wg decyzji 4 (dwuklik = teraz, kółko = ± minuta / godzina).
- Pomarańczowy wiersz przed zatwierdzeniem, szary po zatwierdzeniu.
- Automatyczne wpisy „Dyżur objął …” i zmiana doby; nawigacja |< < data > >| ^ v.

**E2 – Wykaz pociągów**
- Lista z rozkładu w kolumnach jak w `ISDR_zdjecia.md` § 5.
- Menu pod prawym przyciskiem: Przyjmij, Wypraw, Przyjmij z innego szlaku, Wypraw na inny szlak.
- Dwuklik i przeciąganie numeru na dziennik.
- Parzystość numeru wg kierunku szlaku; tor z kolumny PT.
- Okno „Szlaki posterunku”.

**E3 – Stan na żywo**
- Ikony torów szlakowych w nagłówku dziennika: zajęty na czerwono, strzałka kierunku, szara strzałka kierunku zasadniczego.
- Kontrolka zajętości torów stacyjnych.

**E4 – Telefonogramy**
- Okno „Telefonogram do dziennika”: Nadaję / Odbieram, data, godzina, Nadał / Odebrał, wzór, treść, notatka.
- Na razie tylko „[bez szablonu]”. Wzory poda właściciel; będą w pliku `Dane/telefonogramy.json` z polami do wypełnienia.
- Wpis na całą szerokość dziennika i wpis w dzienniku telefonicznym („Rejestruj telefonogram”, „Rejestruj inne okoliczności”).
- Stan bieżący szlaku (okno z kołem zębatym), zmieniany telefonogramem, np. zamknięcie toru dodaje znak na ikonie toru.

**E5 – Pozostałe wpisy**
- Powiadomienie dróżników w kolumnie 8 (godzina albo ukośnik).
- Jazda do km i z powrotem jako dwa wiersze w klamrze.
- Wpis o dowolnej treści (i).
- Przyciski cyklicznych sprawdzeń (np. łączność z dróżnikami, oświetlenie semaforów).

**E6 – Wyciąg**
- Wyciąg z dziennika ruchu, dziennika telefonicznego albo zajętości torów za wybrany okres.
- Wersja bez historii i z historią zmian (kolumna 9 „Data i godzina zmiany”), do druku.

## 5. Testy

- Po każdym etapie: build, selftest na `app/LcsWidzew/Testy/station_test.json` (wyniki jak w `CLAUDE.md`) i test edytora UT.
- Nowy test EDR bez okna. Na kopii testowej przyjmuje i wyprawia pociąg z rozkładu na każdy szlak, zmienia komórkę i sprawdza wiersze, parzystość oraz wersje.
- Zrzut okna EDR (`--screenshot` z nowym `#edr`) porównany ze zdjęciami.

## 6. Otwarte sprawy

- Wzory telefonogramów – 2026-10-28 przepisane z EDR.exe (E8e). Do sprawdzenia przez właściciela: treść wzorów i skutki; wzory 31 / 32 (trakcja elektryczna) bez skutku – stan szlaku nie ma trakcji.
- Dróżnicy: godzina czy ukośnik zależnie od liczby torów szlaku. W EDR.exe „Dwutorowych” = godzina, „Jednotorowych” = ukośnik, u nas (E5) odwrotnie. Do ustalenia z właścicielem (2026-10-28), kod bez zmian.
- Zawartość zakładek konfiguracji „Wymagane sprawdzenia” i „Układ dzienników” (strony 30–38 podręcznika są tylko częściowo na zdjęciach).

## 7. Stan

- 2026-10-27: plan zapisany, decyzje właściciela w § 1.
- **2026-10-27: E1 zrobione (czeka na akceptację właściciela).** Zrzuty: `zrzuty/edr_e1/` (1 – dzienniki, 2 – okno przyjęcia, 3 – stanowisko przez sieć po przejęciu posterunku), wynik testu `zrzuty/edr_e1/edrtest.txt`.
  - Rdzeń: `Sim/Edr/Edr.cs` – `Edr` (moduł w `Simulator`, `Edr.Tick` w cyklu), `EdrDziennik`, `EdrStrona`, `EdrWiersz`, `EdrWersja`, `EdrData`, `EdrPolecenie`. Dzienniki powstają z `blocks`: jeden na posterunek, sąsiada i linię (końce szlaku wewnętrznego między dwoma posterunkami – po dzienniku na każdym), tory szlakowe razem. Tory stacyjne do okna – z odcinków posterunku poza szlakami.
  - Sesja i sieć: `ILcsSession.GetEdr` / `EdrCmd` (lokalnie, host bez okna, `RemoteSession`); serwer: `get edr`, `edr`.
  - Okno: `UI/EdrWindow.cs` (`EdrWindow`, `DziennikWidok`, `OknoPociagu`), przycisk „EDR” na pasku pulpitu. Odświeżanie co 1 s, przebudowa tylko po zmianie danych.
  - Test: `LcsWidzew.exe --edrtest wynik.txt` – `EDR: kontrola 20 z 20` na `Testy/station_test.json`. Osobno od selftestu, bo raport selftestu porównuje się z bazą. Selftest bez zmian (25 z 26, 5 z 5), test edytora UT 0 błędów.
  - **Przyjęte założenia (do potwierdzenia przez właściciela):**
    1. Pola wymagane: numer, tor, odjechał, przyjechał; przy `pol`, `pol1`, `tel` także droga wolna (wg Rys. 37 i 40 podręcznika). To samo dla przyjęcia i wyprawienia.
    2. Pole wyboru przy godzinie w oknie przyjęcia / wyprawienia = „nie dotyczy” (`---`, jak w Rys. 48).
    3. Kolumna numeru wg ostatniej cyfry wpisanego numeru. Wymuszanie parzystości wg kierunku szlaku jest w E2 (potrzebny kierunek parzysty w danych szlaku).
    4. Wiersz niekompletny – tło pomarańczowe, kompletny – białe (podręcznik i film).
    5. Kolumna 8 (dróżnicy) i ikony torów szlakowych w nagłówku – w E5 i E3.
  - Nie sprawdzone: dane EDR nie są zapisywane do pliku (giną po zamknięciu programu, tak jak dziś książka E-1758). W sieci stanowisko pobiera cały stan EDR co 1 s; przy długich dziennikach trzeba będzie wysyłać tylko zmiany. W oknie przyjęcia pole „Tor stacyjny” (lista z wpisywaniem) nie zmienia koloru na niebieski, choć jest wymagane.
- 2026-10-27: właściciel zaakceptował E1 z założeniami 1–5.
- **2026-10-27: E2 zrobione (czeka na akceptację właściciela).** Zrzuty: `zrzuty/edr_e2/` (1 – wykaz i dzienniki po dwukliku na 64410 i 46021, 2 – „Szlaki posterunku”, 3 – stanowisko przez sieć), wynik testu `zrzuty/edr_e2/edrtest.txt`.
  - Dane: kierunek pociągów parzystych szlaku ustala projektant w edytorze UT (panel szlaku, `UtLine.Parzysty`: od końca 1 do 2 / od 2 do 1); eksport `DoSymulatora` → `blocks.*.evenDir` (zwrot jak `defDir`), `BlockDef.EvenDir`. Dziennik: `EdrDziennik.Parzysty` (1 – wyprawiane parzyste, −1 – przyjmowane parzyste). Bez ustalonego kierunku numer zostaje, jak go wpisano. Test edytora UT: panel → `evenDir`, „nie ustalono” → bez pola.
  - Parzystość: przy nowym wierszu i przy zmianie numeru EDR zmienia ostatnią cyfrę (parzysty = o 1 mniejszy, nieparzysty = o 1 większy; pary 0/1, 2/3 …); numer bez cyfry na końcu – bez zmian. W wykazie pociąg jest „ten sam” mimo zmiany parzystości (`Edr.TenSam`).
  - Wykaz (`Edr.Wykaz`, `EdrPociag`): z `Rozklad.Queue`; plan na posterunku z GTFS (`TimetableEntry.PlanPrz / PlanOdj / PlanTor`, uzupełniane w `RealTimetable`), w innych rozkładach – odjazd (`DepBy`, `Dep`) i krótki zapis toru. Wykonanie (`Edr.Ruch`, `Edr.Zarejestruj` w `Edr.Tick`): czoło pociągu na stacji posterunku albo na szlaku (odcinki `secsWE`); przyjazd z szlakiem, odjazd z szlakiem, tor – ostatni tor z numerem pod czołem w czasie pobytu.
  - Okno: `UI/EdrWykaz.cs` (`WykazPociagow`, `SzlakiPosterunku`); w `EdrWindow`: `DodajZWykazu` (dwuklik, upuszczenie), `PolecenieWykazu` (PPM), `WybierzSzlak`. Komunikat EDR (także zmiana numeru) na stopce okna.
  - Test `--edrtest`: 32 z 32 (E1 + parzystość, wykaz, wjazdy w ruchu po nastawieniu przebiegów wjazdowych, serializacja). Selftest bez zmian (raport identyczny), test edytora UT 0 błędów.
  - **Ograniczenia (do decyzji właściciela):**
    1. Stacji początkowej i końcowej ani przewoźnika nie ma w danych rozkładu – kolumn nie ma; jest rodzaj (kategoria) i nazwa pociągu.
    2. Pociąg, który zmienia czoło na innym posterunku i wraca (np. 18100 → 18101 przez ŁW do ŁF i z powrotem), ma w wykazie jeden wiersz na posterunek: drugi przejazd przez ten sam posterunek nie jest osobno rejestrowany, a szlak odjazdu przy pierwszym przejeździe zna się dopiero z ruchu.
    3. Planowy przyjazd jest tylko w rozkładzie rzeczywistym (GTFS); w rozkładzie wbudowanym kolumna jest pusta.
    4. Nie ma jeszcze filtrów wykazu z filmu (częstotliwość odświeżania, ± godzin, pociągi / potwierdzenia „wszystkie”). Wykaz pokazuje cały rozkład scenariusza.
    5. Kierunek parzysty trzeba ustawić w edytorze UT i wyeksportować układ – `station.json` Widzewa i `Testy/station_test.json` go nie mają (test ustawia go w pamięci).
- 2026-10-27: właściciel zaakceptował E2 z ograniczeniami 1–5.
- **2026-10-27: E3 zrobione (czeka na akceptację właściciela).** Decyzja właściciela przed etapem: **zajętość torów szlakowych i stacyjnych z wpisów w dzienniku, nie z urządzeń srk** (jak w prawdziwym EDR: podręcznik, Rys. 37 – ikona czerwona po zarejestrowaniu wiersza). Zmienia to wiersz „Ikony torów szlakowych / Kontrolka” w tabeli źródeł w § 3. Zrzuty: `zrzuty/edr_e3/` (1 – Chojny: oba tory zajęte, przyjęcie ↓ i wyprawienie ↑, Andrzejów wolny, tor stacyjny 1 zajęty przez 18100; 2 – ikony powiększone), wynik testu `zrzuty/edr_e3/edrtest.txt`.
  - Rdzeń (`Sim/Edr/Edr.cs`): `EdrTorSzlakowy` (szlak, tor, zasadniczy kierunek −1 / 0 / 1, pociągi, kierunek jazdy) w `EdrDziennik.ToryStan` – `Edr.StanSzlaku`; `EdrTorStacyjny` w `EdrData.ZajetoscTorow` – `Edr.ZajetoscTorowStacyjnych`. Zasadniczy kierunek z `BlockDef.DefDir` względem końca szlaku przy posterunku; jednotorowy dwukierunkowy – nieustalony; szlak z oboma końcami na jednym posterunku – jedna ikona. Kolejność ikon: tor przyjazdowy (lewy), dwukierunkowy, wyjazdowy (prawy).
  - Tor szlakowy zajęty: wiersz z „odjechał” (godzina) i pustym „przyjechał”; przyjęcie – tor do nas, wyprawienie – od nas, „po torze lewym” – przeciwny, brak takiego – dwukierunkowy albo pierwszy. Tor stacyjny: przyjęcie z „przyjechał” i torem zajmuje tor, wyprawienie z „odjechał” zwalnia tor tego pociągu (także po zmianie parzystości) i tor wpisany w wyprawieniu; kolejność wg kolejności wpisów.
  - Okno: ikony w nagłówku dziennika (`DziennikWidok.IkonaToru`), kontrolka pod zakładkami (`EdrWindow.Kontrolka`), podpowiedzi z pociągami.
  - Testy: `--edrtest` 43 z 43, raport selftestu identyczny, test edytora UT – sprawdzenia EDR (kierunek parzysty) przechodzą; 2 błędy w ostatnim przebiegu to profil z NMT GUGiK (usługa sieciowa zwróciła 0 m), wcześniejszy przebieg tego samego kodu – 0 błędów.
  - Założenie do potwierdzenia: tor lewy = zasadniczo przyjazdowy (ruch prawostronny na szlaku dwutorowym, jak na Rys. 37). Lewy / prawy nie są liczone z geometrii.
- **2026-10-27: poprawki właściciela do E3 (czekają na akceptację).** „Przejrzyj, jak wygląda EDR na zdjęciach, i odwzoruj kolorystycznie i wyglądowo” oraz „kierunek zasadniczy wg instrukcji określa się w ustawieniach dziennika”; w trakcie: zrzut legendy „Opis oznaczeń” z filmu i uwaga „wstęga jest tylko, jak klikniesz na pociąg, lub samo przeskoczy na aktualny pociąg wg wstęgi”.
  - **Ustawienia dziennika** (`EdrUstawienia`, `EdrTorUstawienia` w `Sim/Edr/Edr.cs`; okno `UI/EdrUstawienia.cs` wg klatki f1_01 i podręcznika rozdz. 5.2.1): kierunek parzysty, opcje wypełniania (pola wymagane – zastępują stałe `WymaganeDla`), tory szlakowe: lewy / prawy, status, zasadniczy rodzaj ruchu, **zasadniczy kierunek ruchu**, prowadzenie dziennika jak dla, urządzenie srk (z układu). Polecenie EDR `ustawienia` (sprawdzenie: te same tory co w układzie, różne strony, tor jednokierunkowy z kierunkiem), zapis w `<układ>.edr.json` obok pliku układu (wczytywany przy starcie). Bez pliku: kierunek zasadniczy nieustalony (blokada jednokierunkowa – z układu), kierunek parzysty z edytora UT, tor lewy / prawy wg numerów. Zasadnicze – menu Konfiguracja albo PPM na nazwie dziennika; bieżące (koło zębate) – podgląd, zmiany przez telefonogramy w E4.
  - Zajętość torów szlakowych liczy się z kierunku zasadniczego z ustawień; przy nieustalonym – przyjęcie po torze lewym, wyprawienie po prawym.
  - **Wygląd** (`UI/EdrWindow.cs`, `UI/EdrWykaz.cs`) wg filmu `Zrodla/EDR/20261027_111725.mp4` i klatek `Zrodla/ISDR_klatki/f1_*`: kolory nagłówków i kolumn wykazu, oliwkowe tło, P. zam. / P. obl., kolumny „Tor” (rzeczywisty) i „P T” (z rozkładu), filtr numeru, czas pobrania danych, informacje dodatkowe Z / Do / P, „Opis oznaczeń” – okno `LegendaEdr` (kolory z legendy), dziennik z ciemnym nagłówkiem w trzech pasach, numer toru nad ikoną, przyciski kwadratowe, „Zapisz”, menu (Konfiguracja czynna), granatowy zegar, kontrolka torów na pasku dolnym.
  - **Wstęga** = kolor zaznaczonego wiersza (`EdrPociag.Wstega`: przyjazd / odjazd / przejazd); zaznaczenie samo przeskakuje na aktualny pociąg, gdy ten się zmieni. Z legendy działają też „Rodz.” (czas potwierdzony przez urządzenie), „+/-” (opóźnienie), „Typ p.” (ph). Pozostałe oznaczenia legendy (kursuje / nie kursuje, uwagi, TWR, towary niebezpieczne, POCIĄG STÓJ, przesyłki nadzwyczajne) wymagają danych, których rozkład nie ma.
  - Zrzuty: `zrzuty/edr_e3/1_tory_szlakowe_i_kontrolka.png`, `3_porownanie_film_symulator.png` (u góry klatka filmu, na dole symulator), `4_ustawienia_zasadnicze.png`, `5_wstega_aktualny_pociag.png`.
  - Testy: `--edrtest` 48 z 48 (nowe: ustawienia – odrzucenie złych, zapis i odczyt pliku, pola wymagane z ustawień), raport selftestu identyczny, test edytora UT 0 błędów.
- 2026-10-27: właściciel zaakceptował E3 z poprawkami.
- **2026-10-27: E4 zrobione (czeka na akceptację właściciela), bez wzorów telefonogramów** (decyzja 3 z § 1). Zrzuty: `zrzuty/edr_e4/` (1 – telefonogram w dzienniku ruchu na całą szerokość i zamknięty tor lewy z krzyżem, 2 – okno telefonogramu, 3 – dziennik telefoniczny z telefonogramem i innymi okolicznościami), wynik testu `zrzuty/edr_e4/edrtest.txt`.
  - Rdzeń (`Sim/Edr/Edr.cs`): `EdrTelefonogram` (rodzaj telefonogram / okoliczność, numer własny kolejny w dobie na posterunku, numer odbiorcy, nadaję / odbieram, data, godzina nadania, czas zakończenia, nadał / odebrał z posterunkami, wzór, treść, notatka, dziennik ruchu, zmiany torów), `EdrZmianaToru`, `Edr.Telefoniczny` (dziennik telefoniczny posterunków), wiersz dziennika ruchu `Rodzaj = "telefonogram"` (`EdrWiersz.Tel`, bez zmian – sprostowanie kolejnym telefonogramem), `Edr.Podpis` („Nadał …, odebrał … godz. …”, Rys. 39). Polecenie `telefonogram` (z dziennika ruchu: `Dziennik`; z dziennika telefonicznego: `Wartosc` = posterunek). Sprawdzenie: treść, „Odebrał” przy nadaniu, „Nadał” przy odbiorze, godziny, tory należą do dziennika, obszar sterowania.
  - **Stan bieżący** `EdrDziennik.Biezace` = ustawienia zasadnicze + zmiany telefonogramów (w kolejności wpisów; po zmianie ustawień zasadniczych zmiany są nakładane ponownie). Ikony torów i zajętość liczą się ze stanu bieżącego; zamknięty tor – czerwony krzyż na ikonie. Pola wymagane w nowym wierszu – z ustawień bieżących; „Droga wolna” także przy bieżącym telefonicznym zapowiadaniu (podręcznik rozdz. 7.2). Okno ustawień bieżących (koło zębate) pokazuje `Biezace`.
  - Okno: `UI/EdrTelefonogram.cs` – `OknoTelefonogramu` (układ jak na filmie ISDR: „Telefonogram do Dziennika Telefonicznego”, z dziennika ruchu – „Rejestracja telefonogramu w Dzienniku ruchu” z częścią „Zmiana stanu bieżącego szlaku”), `TelefonicznyWidok` (zakładka „Dziennik telefoniczny”: Nr kol., Data, Adres i treść, Czas zakończenia, Nadał / odebrał, przyciski „Rejestruj telefonogram”, „Rejestruj inne okoliczności”, strony = doby). Wpis telefonogramu w dzienniku ruchu – pomarańczowe tło, treść pogrubiona, notatka, podpis.
  - Testy: `--edrtest` 61 z 61, raport selftestu identyczny, test edytora UT 0 błędów. Zapis telefonogramu trafia też do rejestratora (kategoria „Telefon”).
  - **Założenia do potwierdzenia:**
    1. Telefonogram z dziennika ruchu zapisuje się też w dzienniku telefonicznym (z numerem kolejnym). Podręcznik mówi tylko o dzienniku ruchu.
    2. Numer własny jest kolejny w dobie dla posterunku.
    3. Zmianę stanu szlaku wybiera się ręcznie w oknie telefonogramu, dopóki nie ma wzorów. Wzory mają to robić same, np. [21] „Zamknięcie toru szlakowego”.
    4. Data i godzina w podpisie to godzina nadania (Rys. 39 ma w podpisie inną godzinę niż w polu nadania, nie wiadomo którą).
  - Nie zrobione: wzory telefonogramów (czekają na treść od właściciela), wydruk dziennika telefonicznego (E6).
- **2026-10-27: odpowiedzi właściciela na założenia E4 i poprawki (czekają na akceptację).**
  1. „Telefonogram z dziennika ruchu także w dzienniku telefonicznym” – **nie**: zapisuje się tylko w dzienniku ruchu. Dziennik telefoniczny ma tylko swoje telefonogramy i inne okoliczności.
  2. Numer własny – **numeracja dobowa, miesięczna albo roczna**: ustawienie posterunku (menu Konfiguracja → Numeracja telefonogramów – posterunek; polecenie EDR `numeracja`; plik `<układ>.edr.json` – `Numeracja`), domyślnie dobowa. Jeden licznik posterunku dla telefonogramów w dziennikach ruchu i wpisów dziennika telefonicznego (`Edr.NastepnyNumer`, `EdrData.NastepnyNr`, `EdrData.Numeracja`); okno telefonogramu pokazuje numer i rodzaj numeracji.
  3. Zmiana stanu szlaku ręcznie w oknie, do czasu wzorów – **tak** (bez zmian).
  4. **Godzina nadania** – osobne pole; **w podpisie** („Nadał …, odebrał … godz. …”) – **godzina wprowadzenia** do EDR (`EdrTelefonogram.Wprowadzono`). Godzina nadania – w podpowiedzi wpisu.
  - Testy: `--edrtest` 62 z 62 (nowe: tylko w dzienniku ruchu, godzina wprowadzenia w podpisie, numeracja – nowa doba przy dobowej, ciąg dalszy przy miesięcznej i rocznej, nieznana odrzucona), raport selftestu identyczny, test edytora UT 0 błędów. Zrzuty `zrzuty/edr_e4/` odświeżone.
- 2026-10-27: właściciel zaakceptował E4 z poprawkami.
- **2026-10-27: E5 zrobione (czeka na akceptację właściciela).** Zrzuty: `zrzuty/edr_e5/` (1 – dziennik z kolumną 8, klamrą, wpisem dowolnym, drugą godziną „08:00 / 08:02” i przyciskami sprawdzeń przy zegarze; 2 – okno powiadomienia dróżników; 3 – ustawienia z posterunkami dróżników; 4 – wymagane sprawdzenia), wynik testu `zrzuty/edr_e5/edrtest.txt`.
  - **Dróżnicy** (podręcznik rozdz. 7.4 i Rys. 24): `EdrDroznik` w `EdrUstawienia.Droznicy` (km na linii, numer na linii, nazwa, oznaczenie w dzienniku ruchu, powiadamianie według szlaku – „Jednotorowych” = godzina, „Dwu i więcej torowych” = ukośnik, dla pociągów, w dzienniku ruchu). Przejazdy w `station.json` nie mają dróżnika ani szlaku, więc posterunki dróżników wpisuje człowiek w ustawieniach dziennika (sekcja „Posterunki dróżników przejazdowych”). Wartości w `EdrWersja.Dr` (pole „Dr:id”), wymagane w wierszu, gdy dróżnik dotyczy kierunku pociągu. Okno `OknoDroznikow` (przycisk z listą nad dziennikiem).
  - **Jazda do km i z powrotem** (rozdz. 7.5, Rys. 48): polecenie przyjęcia / wyprawienia z `Pole = "powrot"` i kilometrem – wiersz wyjazdu (uwaga „do km … i z powrotem”, przyjechał „---”) i wiersz `Rodzaj = "powrot"` (droga wolna i odjechał „---”, wymagane przyjechał), spięte `EdrWiersz.Para`; klamra w oknie. Tor szlakowy zajęty od odjazdu do wpisu powrotu; powrót z torem i przyjazdem zajmuje tor stacyjny.
  - **Wpis o dowolnej treści**: polecenie `wpis`, wiersz `Rodzaj = "wpis"` na całą szerokość, niezmienny.
  - **Sprawdzenia cykliczne**: `EdrSprawdzenie` w `Edr.Sprawdzenia` (posterunek → najwyżej cztery: nazwa, okres w minutach; plik `<układ>.edr.json`), polecenia `sprawdzenia` (konfiguracja) i `sprawdzenie` (wykonanie), stan `EdrSprawdzenieStan` (ok / wkrótce – 15 min przed terminem / po terminie; termin od ostatniego wykonania, bez wykonania – od początku symulacji). Okna `OknoSprawdzenia`, `OknoSprawdzenUst` (`UI/EdrE5.cs`), przyciski przy zegarze, menu Konfiguracja.
  - **Poprawki w trakcie (uwagi właściciela):**
    1. Dwuklik w wykazie nie dubluje pociągu: najpierw przyjęcie, potem wyprawienie, potem tylko komunikat „już wpisany” (kolejny wpis – PPM). Upuszczenie na dziennik – tak samo.
    2. Fabryczna (posterunek, do którego pociąg przyjeżdża szlakiem wewnętrznym od innego posterunku LCS): kierunki z kolejności posterunków w biegu pociągu (`Edr.Sekwencja` – GTFS albo szlak wjazdowy, zmiany czoła, szlak wyjazdowy; `Edr.Laczacy`), więc dwuklik działa.
    3. Wpisywanie godzin jak na stanowisku ISDR: klik w komórkę – edytor (pole z godziną, pole wyboru – **druga godzina, gdy potrzebna**, czerwony krzyżyk – rezygnacja; Enter albo wyjście – zapis). Komórka pokazuje „08:00 / 08:12”, gdy wpisano drugą godzinę (`Edr.Godzina` przyjmuje „a / b”). Wcześniejsze dwuklik = bieżąca godzina zastąpione edytorem (kółko myszy działa w edytorze).
  - Testy: `--edrtest` 76 z 76 (test pracuje na kopii układu w katalogu tymczasowym – nie czyta ani nie zapisuje `<układ>.edr.json` obok układu; wcześniej zrzut E5 zapisał taki plik obok `Testy/station_test.json` – usunięty), raport selftestu identyczny, test edytora UT 0 błędów.
  - **Założenia do potwierdzenia:**
    1. Sprawdzenie cykliczne bez wykonania – termin liczony od początku symulacji; żółty 15 min przed terminem.
    2. Wykonanie sprawdzenia zapisuje się w dzienniku telefonicznym jako inne okoliczności (z numerem).
    3. Co oznacza druga godzina w komórce (np. przy „Pociąg odjechał”), nie wiadomo – EDR ją tylko zapisuje i pokazuje; do zajętości torów liczy się pierwsza.
  - Wersja do uruchamiania: jeśli program był otwarty w czasie budowania – zbudować ponownie po zamknięciu (`dotnet build -c Release` w `app/LcsWidzew`).
- 2026-10-27: właściciel zaakceptował E5.
- **2026-10-28: E6 zrobione.** Zrzuty: `zrzuty/edr_e6/` (1 – wyciąg z dziennika ruchu bez historii, 2 – z historią zmian, 3 – z dziennika telefonicznego, 4 – z zajętości torów stacyjnych), wynik testu `zrzuty/edr_e6/edrtest.txt`.
  - Rdzeń `Sim/Edr/EdrWyciag.cs`: `EdrWyciag.Dziennik` (wiersze zmieniane w okresie; bez historii – ostatnia postać, z historią – bieżąca i pod nią wcześniejsze, najstarsza na dole, kolumna 9 „dd/MM/yy HH:mm:ss”), `EdrWyciag.Telefoniczny` (Rys. 56: Nr kol. – własny / odbiorcy, data, adres i treść, czas zakończenia godz. / min., nadał / odebrał), `EdrWyciag.ZajetoscTorow` (z wpisów: przyjazd na tor – odjazd; okresy nachodzące na okres wyciągu).
  - Okno `UI/EdrWyciag.cs` (`WyciagWindow`, Rys. 50): menu **Raporty** w oknie EDR; posterunek ruchu, dziennik ruchu, od – do, „Pokaż historię zmian w wierszach”, „Pokaż”, podgląd stron (FlowDocumentReader – strony, powiększenie, szukanie), „Drukuj…” ze stopką (data wydruku, „System EDR”, n / m), „Zamknij”. Układ wyciągu wg Rys. 51–53: godziny w g. / m., „—” dla „---”, kolumna 8 tylko przy dróżnikach, zapisy na całą szerokość bez kratek, klamra, „Wyciąg sporządził: … Stanowisko: … dnia … godz. …” (zalogowany i jego rola).
  - **Poprawka (uwaga właściciela 2026-10-27):** posterunek wybrany u góry wyznacza całe okno – zakładki pokazują tylko dzienniki tego posterunku (Widzew – swoje szlaki, Fabryczna – swoje), dziennik telefoniczny i kontrolka torów też; tytuł okna z nazwą posterunku. Wcześniej zakładki pokazywały dzienniki wszystkich posterunków stanowiska naraz. `#edrpost=KOD` – zrzut z wybranym posterunkiem.
  - Testy: `--edrtest` 81 z 81 (nowe: wyciąg bez historii, z historią – kolejność postaci i kolumna 9, pusty okres, telefoniczny, zajętość torów), raport selftestu identyczny, test edytora UT 0 błędów.
  - Nie zrobione / do decyzji: w wyciągu brak „Jednostka” i „tel.” sporządzającego (symulator nie zna jednostki ani telefonu użytkownika); wydruk nie był sprawdzony na drukarce (tylko podgląd na zrzucie); zapis do pliku – przez drukarkę PDF, bez osobnego przycisku.
- **Stan planu: E1–E6 zrobione.** Otwarte: wzory telefonogramów (treść i numery od właściciela), Pociąg STÓJ / zgłoszenie gotowości / analiza pociągu w menu wykazu (poza planem E1–E6), zapis dzienników do pliku (dziś giną po zamknięciu programu).
- 2026-10-28: właściciel zaakceptował E6. Plan EDR E1–E6 zamknięty.

## E7 – zgodność ze wzorem (zrzut EDR z SimRaila, 2026-10-28) – w przygotowaniu

Różnice wskazane przez właściciela (zrzut EDR Dąbrowa Górnicza Wschodnia vs nasz Łódź Fabryczna) i jego odpowiedzi z 2026-10-28:
- K / NK w wykazie = **Kursuje / Nie kursuje**; zaznacza dyspozytor (nie dyżurny).
- Posterunki i dzienniki: nie wyprowadzać na siłę ze szlaków układu – dać w Konfiguracji **dodawanie i konfigurowanie posterunków ruchu i ich dzienników**.
- Dzienniki parami w jednej zakładce (np. „Dorota / Kozioł”) – **do ustawienia w Konfiguracji**.
- Kolumny wykazu: Stacja początkowa, Stacja końcowa, Przewoźnik; nazwa pociągu – w zakładce **Opis pociągu**; pamiętać o zakładce/oknie Przewoźnik. Na razie szkielet.
- Uwagi ekspl. / Uwagi Przew. wpisuje dyspozytor – z edytora rozkładu jazdy albo z panelu instruktora; Uwagi własne – dyżurny.
- Pole „Czas bez wypadku spow. przez pracownika PLK” – **ma być**.
- „Rodz.” = kategoria pociągu wg Ir-11. Komórka numeru pociągu: **turkusowa – nieparzysty, zielona – parzysty**.
- Do dziennika przepisuje się pociąg **tylko kliknięciem w numer pociągu** (nie w dowolne miejsce wiersza).
- Wygląd wg wzoru: turkusowe zaznaczenie, większa czcionka i wiersze, pogrubiony numer, „Prezentuj daty”, „Północ dnia …”, „Dyżur przyjął …”, duży zegar z datą, pasek torów, nazwa dziennika na środku, pełne nazwy zakładek.
- 2026-10-28: dalsze odpowiedzi właściciela: kolor „Rodz.” – zielony: zatwierdzony przez człowieka, czerwony: automatycznie, szary: przez dyspozytora; EDR jest tylko zobrazowaniem rozkładu jazdy (NK niczego nie wyłącza w symulacji); przepisanie do dziennika – dwuklik w numer, reszta bez zmian; czas bez wypadku – do ustawienia w konfiguracji przez administratora. W trakcie: „odjazd rzeczywisty w przypadku braku opóźnienia ma przepisywać czas planowy”; „każdy POCIĄG Z SWOIM NUMEREM MA OSOBNY WIERSZ” (kolumny Nr poc.: nieparzysty / parzysty).
- **2026-10-28: E7 zrobione (czeka na akceptację właściciela).** Zrzuty `zrzuty/edr_e7/` (1 – Fabryczna z konfiguracją: dwa dzienniki w jednej zakładce, tory stacyjne 1–9, wpisy po dwukliku; 2 – Fabryczna bez konfiguracji, z układu; 3 – okno „Posterunki ruchu i dzienniki”; 4 – czas bez wypadku; 5 – opis pociągu; 6 – trasa pociągu), test `zrzuty/edr_e7/edrtest.txt`. Zrzut 1 – na kopii układu z przykładową konfiguracją w scratchpadzie (`Data/station.edr.json` właściciela nie powstał).
  - Rdzeń `Sim/Edr/EdrKonfiguracja.cs`: `EdrPosterunekUst` / `EdrDziennikUst` (kod, nazwa, tory stacyjne; dziennik: nazwa, sąsiad, linia, urządzenia, szlaki, tory ręcznie, zakładka), `ZbudujDzienniki` (konfiguracja albo szlaki układu, dziennik o tym samym id zachowuje strony), polecenia `posterunki`, `posterunkizukladu`, `bezwypadku`, `uwagi`, `kursuje`, `potwierdz`; administrator = rola „administrator” albo „instruktor” (rola „administrator” dodana do logowania).
  - Wykaz (`Edr.Wykaz`): osobny wiersz na numer (nowy numer na posterunku – wiersz przyjazdu i wiersz odjazdu), `NrNieparzysty` / `NrParzysty`, `Potw` (czlowiek / auto / dyspozytor), `Kursuje`, stacja początkowa / końcowa i przewoźnik z rozkładu (`origin`, `destination`, `carrier` – brak: kod przewoźnika z GTFS), kategoria Ir-11 z pola `kat` rozkładu (dziś puste – rozkład jej nie ma), uwagi, godziny z datą, trasa; kierunek: najbliższy posterunek w biegu, do którego prowadzi szlak (przystanek bez szlaków pomijany), po zmianie czoła – w stronę przyjazdu.
  - Okno: zegar z datą i polem „Czas bez wypadku” w prawym górnym rogu obok paska, większa czcionka i wiersze wykazu, wykaz wyższy, nazwa dziennika na środku, większe przyciski, pełne nazwy w zakładkach (sąsiad), zakładki wg numeru z konfiguracji.
  - Testy: `--edrtest` 98 z 98, raport selftestu identyczny, test edytora UT 0 błędów.
  - Nie zrobione / do decyzji: kategoria wg Ir-11 – skąd brać (rozkład GTFS jej nie ma; edytor rozkładu?); przewoźnik – dziś kod (ŁKA, PR, IC), pełna nazwa z rozkładu, gdy będzie; tory stacyjne Fabrycznej – w układzie odcinki Fabrycznej nie mają numerów torów, do wpisania w konfiguracji albo w edytorze UT; trasa – jeden plan na posterunek (pociąg wracający przez Widzew ma przyjazd z pierwszego przejazdu, odjazd z drugiego); Częstotliwość odświeżania działa na zegarze symulacji.
- 2026-10-28: odpowiedzi właściciela do E7: kategoria wg Ir-11 i przewoźnik – będą wpisywane w edytorze rozkładu jazdy; tory stacyjne Fabrycznej wpisze właściciel. Poprawka: rozkład skleja obieg jednostki (np. 11550/1 Skierniewice → ŁF, zmiana czoła, 11504/5 ŁF → Skierniewice) w jeden zapis ze stacją początkową pierwszego i końcową drugiego pociągu („Skierniewice – Skierniewice”); teraz każdy numer ma własną relację (przyjeżdżający kończy na posterunku, odjeżdżający zaczyna), nazwę (część nazwy przed / po „ / ”) i trasę (`TimetableEntry.Przebieg` – bieg z numerem na każdym odcinku; bez przyjazdu tam, gdzie numer zaczyna bieg, bez odjazdu tam, gdzie kończy).

## E8 – wnioski z analizy EDR.exe (`Zrodla/EDR_EXE_analiza.md`, 2026-10-28)

Decyzje właściciela z 2026-10-28: kolory mają się w miarę zgadzać z EDR.exe, zakładki „Opis pociągu” i „Trasa pociągu” mają mieć jego układ, ale **wpisywania godzin z EDR.exe nie kopiujemy** (tam działa źle). Punkty 1–5 analizy – tak, telefonogramy – przepisać, dróżnicy – do ustalenia (§ 6). **Każdą zmianę konsultujemy z właścicielem**, kroki po kolei:

- **E8a** – kolory wykazu i dziennika wg EDR.exe (tylko kolory).
- **E8b** – zakładka „Opis pociągu” w układzie EDR.exe (pola po lewej, uwagi po prawej, pola wyboru TN / TWR / nadzwyczajne).
- **E8c** – zakładka „Trasa pociągu”: kremowe tło, nagłówki w kolorach grup, kolumny wg EDR.exe z danymi, które mamy.
- **E8d** – zapis dzienników do pliku (tmp + `.bak`), wykreślanie z przyczyną, tor szlakowy z uwag, droga wolna przy ruchu jednotorowym i zamkniętym torze, godzina „0812” i przejście przez północ.
- **E8e** – 22 wzory telefonogramów w `Dane/telefonogramy.json` (treść z polami + skutek dla szlaku).
- **2026-10-28: E8a zrobione.** Zrzut `zrzuty/edr_e8/1_kolory_wykazu.png`, test `zrzuty/edr_e8/edrtest.txt`.
  - `UI/EdrWykaz.cs`: nagłówki wykazu w kolorach EDR.exe (K / NK – fioletowy, godziny plan. / rzecz. – oliwkowy, +/- i Rodz. – bordowy, Nr nieparzysty i Z kierunku – turkusowy, Nr parzysty i W kierunku – ciemnozielony, Tor – srebrny, Nr L. – żółty, P. zam. / P. obl. / Typ p. – szary, P T – niebieski, stacje i przewoźnik – granatowy). Komórki: godziny, K / NK, stacje – kremowe, Z kierunku – jasnoniebieskie, W kierunku i Nr L. – łososiowe, Tor – srebrne, P T – peron, P. zam. / P. obl. – pomarańczowe przy postoju „ph”, szare bez postoju, Typ p. – jasny przy postoju. Wstęga: magenta / limonka / cyjan, zaznaczenie z postojem – niebieskie. „Rodz.”: zielony / bordowy / srebrny. Opóźnienie – bordowe.
  - `UI/EdrWindow.cs`: pole wymagane w dzienniku 151,186,205.
  - Zostawione wg wcześniejszej uwagi właściciela: numer nieparzysty – turkusowy (w EDR.exe jasnoniebieski jak „Z kierunku”). Do potwierdzenia.
  - Testy: `--edrtest` 98 z 98, selftest 25 z 26 / 5 z 5.
  - Poprawka właściciela (2026-10-28): przy „Nr pociągu” zamiast lupy – gumka jak w EDR.exe, czyści wpisany numer (filtr wykazu odświeża się sam). Zrzut `zrzuty/edr_e8/2_gumka_nr_pociagu.png`, `--edrtest` 98 z 98.
  - Poprawka właściciela (2026-10-28): kolumna „P T” = peron cyframi rzymskimi i tor cyframi arabskimi (np. „III 2”). Peron z pola `platform` rozkładu rzeczywistego (`TimetableEntry.PlanPeron`), liczba zamieniana na cyfry rzymskie (`Edr.PeronTor`); rozkład wbudowany nie ma peronów – sam tor. `EdrPociag.Tor` zostaje bez zmian (wpis do dziennika). Zrzut `zrzuty/edr_e8/3_peron_tor_PT.png` (scenariusz `ic_05`), `--edrtest` 99 z 99.
- **2026-10-28: E8b zrobione.** Zrzut `zrzuty/edr_e8/4_opis_pociagu.png` (pociąg 19284, scenariusz `ic_05`), `--edrtest` 99 z 99.
  - `UI/EdrWindow.cs` (`OpisJakEdr`): białe tło, granatowe pogrubione etykiety (Tahoma), kremowe pola tylko do odczytu. Po lewej: Numer pociągu (przy zmianie numeru „przyjazd / odjazd”), Numer międzynarodowy, Nazwa, Numer zamówienia, Rodzaj pociągu (kategoria Ir-11), Przewoźnik, Typ pociągu (kod rodzaju z rozkładu, pusty, gdy to kod przewoźnika), Stacja początkowa, Stacja końcowa, Rodzaj rozkładu jazdy, Nr i data wniosku, Wnioskodawca, Rezygnacja i data rezygnacji (łososiowe). Po prawej: Informacje dodatkowe (pojazd, długość, NK, stan), Ładunek, Uwagi przewoźnika, eksploatacyjne, własne; pola wyboru Przekroczona skrajnia, Przekroczony nacisk, Inne nadzwyczajne, TWR, TN, Zamknięciowy, Śledzony.
  - Puste, bo rozkład ich nie ma: numer międzynarodowy, zamówienie, rodzaj rozkładu, wniosek, wnioskodawca, rezygnacja, ładunek, pola wyboru. Do decyzji: skąd je brać (edytor rozkładu jazdy?).
- 2026-10-28: właściciel zaakceptował E8a (z gumką i P T) i E8b.
- **2026-10-28: E8c zrobione.** Zrzut `zrzuty/edr_e8/5_trasa_pociagu.png` (pociąg 19284, scenariusz `ic_05`), `--edrtest` 100 z 100, selftest 25 z 26 / 5 z 5, dziennik zdarzeń identyczny.
  - `UI/EdrWindow.cs` (`KolumnyTrasy`): 29 kolumn jak w EDR.exe – Przyjazd plan. / Godz., Przyjazd rzecz. / Godz., +/-, Z, Lp., Stacja, Post. zam., P. obl., Typ postoju, *, P T, Odjazd plan. / Godz., Odjazd rzecz. / Godz., +/-, Rodz., Nr poc., Got. do odj., Czas wpisu, Maszynista, Seria / Dł. / Brutto pl. i rz. Nagłówki – gradient od białego do koloru grupy (godziny – oliwkowy, postój – szary, gotowość – zielony, skład rzeczywisty – morski, pozostałe – granatowy), napisy zawijane; kremowe komórki, Tahoma 12, zaznaczenie niebieskie; szerokości w proporcjach EDR.exe dopasowane do okna.
  - Dane (`Sim/Edr/Edr.cs` – `PunktTrasy`, `EdrTrasa` w `EdrKonfiguracja.cs`): plan z biegu rozkładu (`PlanPunkt.Peron` – nowe pole), rzeczywiste z ruchu na posterunku; bez przejazdu – czas planowy (jak w wykazie), „✓” po zarejestrowaniu przejazdu, data „dd-MM-yy”, P T = peron rzymski + tor, Seria pl. = pojazd, Dł. pl. = długość.
  - Puste, bo brak danych: „*”, gotowość do odjazdu, czas wpisu, maszynista, brutto, skład rzeczywisty, Rodz. (kategoria Ir-11 – z edytora rozkładu).
  - Zauważone, bez zmian: w wykazie postój zamówiony pociągu, który przejeżdża przez posterunek dwa razy w obiegu (np. 19284 w ŁW 05:06, potem odjazd 06:22 innym numerem), liczy się do ostatniego odjazdu z posterunku (76 min), a w trasie – do odjazdu tym numerem (3 min). Ograniczenie E7 „jeden plan na posterunek”.
  - Test: sprawdzenie trasy w `--edrtest` na rozkładzie wbudowanym jest słabe (ten rozkład nie ma planu na posterunkach, trasa pusta); trasa z rozkładem rzeczywistym sprawdzona na zrzucie.
- 2026-10-28: właściciel zaakceptował E8c. Decyzje przed E8d: zapis – **ciągła historia** (jeden plik obok układu, każde uruchomienie dopisuje dalej); droga wolna na szlaku jednotorowym – **zawsze** (jak w EDR.exe).
- **2026-10-28: E8d zrobione.** Zrzut `zrzuty/edr_e8/6_wykreslenie_tor_z_uwag_polnoc.png`, `--edrtest` 110 z 110, selftest 25 z 26 / 5 z 5, dziennik zdarzeń identyczny.
  - **Zapis do pliku** (`Sim/Edr/Edr.cs` – `WczytajDzienniki`, `ZapiszDzienniki`): `<układ>.edr_dzienniki.json` obok układu – strony dzienników ruchu (po id dziennika), prowadzący, dziennik telefoniczny. Zapis po każdym udanym poleceniu EDR i po wpisie automatycznym (`Tick`): plik `.tmp`, potem `File.Replace` z kopią `.bak`. Odczyt przy starcie: doba strony z jej daty względem dnia startu, numery wierszy dalej. Dzienniki z pliku, których nie ma w konfiguracji, zapisują się dalej bez zmian. Błędny plik – zostaje, zapis wyłączony, komunikat na stopce (`EdrData.BladDziennikow`). Zapis tylko na pulpicie (`Edr.ZapisDziennikow`, `App.xaml.cs`).
  - **Wpisy z wcześniejszych uruchomień** (`EdrWiersz.Wczytany`, bez zapisu) są historią: nie zajmują torów szlakowych i stacyjnych i nie liczą się w wykazie (przyjęty / wyprawiony, potwierdzenie), bo symulacja startuje bez pociągów. Podpowiedź na wierszu mówi, że to wpis z pliku. **Do potwierdzenia.**
  - **Wykreślenie** (polecenie `wykresl`, okno `OknoWykreslenia` w `UI/EdrE5.cs`, PPM na wierszu): przyczyna obowiązkowa, nowa wersja wiersza z `EdrWersja.Wykreslono` (historia zmian i wyciąg z historią), para „do km i z powrotem” – oba wiersze; nie dla wpisów automatycznych i telefonogramów; wykreślonego nie można zmieniać ani wykreślić ponownie; nie zajmuje torów. Okno: przekreślony, szary, „Wykreślono: …” na czerwono w uwagach. Wyciąg: „Wykreślono: …” w uwagach (`WyciagWiersz.Wykreslony`).
  - **Tor szlakowy z uwag** (`Edr.TorZUwag`, `SprawdzTorZUwag`): „po (zamkniętym) torze nr N” – zajęty tor N (pierwszeństwo przed kierunkiem zasadniczym), „po torze lewym / prawym” w tekście – jak pole „Po torze lewym”. Odrzucane przy zapisie i zmianie uwag: dwa numery, numer spoza szlaku, lewy i prawy naraz, numer sprzeczny z lewym / prawym (lewy = tor o kierunku zasadniczym przeciwnym do jazdy).
  - **Droga wolna** (`Edr.Wymagane(bieżące, zasadnicze)`): także na szlaku jednotorowym, przy zamkniętym torze i przy torze zasadniczo jednokierunkowym prowadzonym dwukierunkowo. Okno przyjęcia / wyprawienia bierze pola wymagane z tych samych ustawień (wcześniej – ze stałej `WymaganeDla`).
  - **Godziny** (`Edr.Godzina`, `SprawdzGodziny`): „812” → 08:12; „przyjechał” przed „odjechał” – odrzucone, poza przejściem przez północ (odjazd ≥ 18:00, przyjazd < 6:00).
  - Zrzut: nowe polecenie `#edrwykresl=i|przyczyna`.
  - Nie sprawdzone na pulpicie: zapis w czasie prawdziwej pracy (testy i zrzuty mają zapis wyłączony albo na kopii w katalogu tymczasowym). Wersja Release w `app/LcsWidzew/bin` – do przebudowania po zamknięciu programu (w czasie prac symulator był otwarty).
  - Poprawka właściciela (2026-10-28, zrzut EDR z SimRaila): **szary pasek – znacznik wpisu do dziennika** jak w EDR.exe. „Z kierunku post.” – pasek przy prawej krawędzi, gdy przyjęcie pociągu jest wpisane do dziennika; „W kierunku post.” – przy lewej, gdy wpisane jest wyprawienie (`EdrPociag.Przyjety` / `Wyprawiony`, wiersze bieżące – bez wykreślonych i z wcześniejszych uruchomień). Wykaz odświeża się też po samej zmianie wpisu (sygnatura zmian w `UI/EdrWykaz.cs`). Zrzut `zrzuty/edr_e8/7_znacznik_wpisu_w_dzienniku.png` (19284 – przyjęty i wyprawiony, 1529/8 – przyjęty), `--edrtest` 110 z 110.
- 2026-10-28: właściciel zaakceptował E8d i znacznik wpisu w wykazie.
- **2026-10-28: E8e zrobione.** Zrzut `zrzuty/edr_e8/8_telefonogram_wzor_21.png`, `--edrtest` 117 z 117, selftest 25 z 26 / 5 z 5, dziennik zdarzeń identyczny.
  - **Plik wzorów** `app/LcsWidzew/Data/telefonogramy.json` (w projekcie symulatora katalog danych to `Data`, nie `Dane` z § 4 E4; kopiowany do wersji do uruchamiania obok `sygnaly_ie1.json`): 22 wzory wg Ir-1 przepisane z EDR.exe – 9, 10, 11, 12, 16, 16a, 17, 17a, 21, 21a, 022, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32. Wzór: numer, tytuł, treść z miejscami `{pole}` (i `{ pole}` – ze spacją, gdy niepuste), lista pól, skutek. Pola: godzina, tor (tory szlakowe dziennika), pociąg, tekst, wybór (tryb zamknięcia: –/planowo/awaryjnie; „z powodu” / „celem”; „przyjechał” / „przejechał”). Wbudowane: `{od}`, `{do}`, `{od_do}` (nadaję – od nas do sąsiada, odbieram – od sąsiada do nas), `{godz_h}`, `{godz_m}`.
  - **Rdzeń** `Sim/Edr/EdrWzory.cs` (`EdrWzory`, `EdrWzor`, `EdrPoleWzoru`, `EdrSkutekWzoru`): wczytanie i sprawdzenie pliku (numery bez powtórzeń, znane pola i miejsca), treść z pól (godzina, tor należący do szlaku, wybór z listy, pola wymagane), skutek. Polecenie `telefonogram` z `Tel.Wzor` = numer wzoru i `Tel.Pola`: treść i zmiana stanu szlaku ze wzoru (ręczna zmiana pomijana), w telefonogramie zapisane `WzorNr`, `Pola`, tytuł wzoru.
  - **Skutki:** 16, 16a – prowadzenie ruchu jak dla telefonicznego zapowiadania (droga wolna wymagana); 17, 17a – prowadzenie ruchu zasadnicze; 21 – tor zamknięty; 21a – tor zamknięty **dopiero po przyjeździe pociągu** z pola „Numer pociągu” (wpis „przyjechał” w dzienniku; `EdrZmianaToru.PoPociagu`, stan bieżący przeliczany przy każdym odświeżeniu – `PrzeliczBiezace`); 22 – tor otwarty; 23, 24, 26 – ruch dwukierunkowy na torze; 25 – rodzaj ruchu zasadniczy na wszystkich torach; 27 – rodzaj ruchu zasadniczy na torze („*” w zmianie = przywrócenie z ustawień zasadniczych). Pozostałe – bez zmiany stanu szlaku. Z dziennika telefonicznego wzór daje tylko treść.
  - **Okno telefonogramu** (`UI/EdrTelefonogram.cs`): lista wzorów („[bez szablonu]” + 22), pola wybranego wzoru (godzina – dwuklik i kółko myszy, tor – lista torów dziennika), podgląd treści na bieżąco (puste pola jako „[etykieta]”), ręczna zmiana stanu szlaku wyłączona przy wzorze, opis skutku wzoru. Zrzut: `#edrtelokno=i|nr`.
  - **Do decyzji:** wzory 31 / 32 (trakcja elektryczna) – stan bieżący toru nie ma trakcji, więc bez skutku; wzór 24 (żądanie) zmienia ruch na dwukierunkowy jak w EDR.exe – czy ma, skoro to tylko żądanie; 16a (radiotelefoniczne) – prowadzenie ruchu jak dla telefonicznego (lista podstaw nie ma radiotelefonicznego).
- 2026-10-28: właściciel zaakceptował E8e. **Plan E8 (wnioski z analizy EDR.exe) zamknięty.** Otwarte: sprawy „Do decyzji” z E8e (wzory 31 / 32, 24, 16a) i dróżnicy – godzina / ukośnik (§ 6).
