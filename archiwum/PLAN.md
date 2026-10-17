# Symulator komputerowych urządzeń srk – LCS Łódź Widzew
## Plan działania i stan prac (v0.5)

Data: 14.10.2026

---

## 1. Cel

Aplikacja na komputer (Windows) do szkolenia dyżurnych ruchu LCS Łódź Widzew:
- obsługa komputerowego pulpitu nastawczego zgodnie z **Ie-20** (polecenia zwykłe, specjalne i techniczne, przebiegi, blokady, postępowanie przy przeszkodach),
- zobrazowanie wg **Ie-104**, układ ekranu wzorowany na pulpitach LCS (obraz stacji, zegar, tabele ZDARZENIA i ALARMY),
- **rzeczywisty układ torowy** stacji (zwrotnice, tory, wykolejnice),
- docelowo **cały obszar LCS**, w tym **Łódź Fabryczna**.

Symulator jest narzędziem szkoleniowym. Nie zastępuje regulaminu technicznego ani stanowiskowej instrukcji obsługi.

---

## 2. Struktura projektu

```
symulator/
├── uruchom.bat                 ← start aplikacji (dotnet run)
├── PLAN.md                     ← ten dokument
├── Zrodla/                     ← Ie-20, wyciągi Ie-104, materiały PKP PLK 2013
├── tools/                      ← przygotowanie danych stacji (Python)
│   ├── lcs_panel.py            ← ★ obszar LCS (ŁW + ŁF + ŁN) zdefiniowany ręcznie na siatce → station.json
│   ├── osm2layout.py, schematize.py, build_station.py  ← wcześniejszy import z OSM (tylko pomocniczo)
│   └── osm/                    ← pobrane dane OSM
├── app/LcsWidzew/              ← aplikacja C# / WPF (.NET 8)
│   ├── Data/station.json       ← model obszaru LCS: obrazy, posterunki, blokady
│   ├── Model/Station.cs        ← wczytanie modelu, geometria zwrotnic
│   ├── Sim/                    ← zależności, polecenia, pociągi, scenariusze, usterki
│   ├── UI/PanelView.cs         ← obraz szczegółowy (rysowanie, mysz, powiększenie)
│   ├── UI/Dialogs.cs           ← logowanie, polecenie specjalne, E-1758, plan ruchu, tablica przebiegów, instruktor, ocena, pomoc
│   └── MainWindow.xaml(.cs)    ← okno pulpitu
└── archiwum/prototyp_html/     ← pierwszy prototyp przeglądarkowy (porzucony)
```

**Uruchomienie:** dwuklik `uruchom.bat` albo:

```bash
dotnet run --project app/LcsWidzew/LcsWidzew.csproj -c Release
```

**Odtworzenie danych stacji** (po zmianach w układzie):

```bash
python tools/lcs_panel.py app/LcsWidzew/Data/station.json
```

**Testy bez okna:** `LcsWidzew.exe --selftest wynik.txt` (scenariusz z automatycznym dyżurnym: `--selftest wynik.txt ic_05`), `--edtest wynik.txt` (operacje edytora), wypisuje tablicę przebiegów i przebieg scenariusza. `LcsWidzew.exe --screenshot obraz.png 300 "#page=1" "E S1 Pociąg" ...` zapisuje zrzut pulpitu („#page=N” przełącza obraz, „@t” czeka do czasu t).

---

## 3. Źródła danych i ich wiarygodność

| Dane | Źródło | Status |
|---|---|---|
| Topologia, numery zwrotnic (6–65, rozjazdy krzyżowe 17ab/cd, 18ab/cd), wykolejnice, tory 1–8, 102, 104, 200, 204, 204a, 7b | plan schematyczny Semaforek (stan 01.2017) – `Zrodla/Semaforek_Lodz_Widzew.png`, odczyt: `Zrodla/Semaforek_Widzew_odczyt.md` | **rzeczywiste** |
| Rozmieszczenie elementów na siatce pulpitu | wzór pulpitu `Zrodla/SimRail_LCS_Widzew_wzor.webp` | odwzorowane |
| Semafory: wjazdowe A, D, E, G, H, W, V, U, T; wyjazdowe L1–L7, M4–M8, S1–S7, R4–R8, N/P102, N/P104; tarcze Tm1–Tm35, Tm100, Tm136; tarcze ostrzegawcze ToA, ToG, ToH, ToU, ToT; powtarzacze | j.w. | **rzeczywiste nazwy** |
| Baza ŁKA (tory 21–28, zwr. 101–116) | nastawnia CS – pokazana jako obszar przekazany | uproszczenie |
| Odcinki izolowane / liczniki osi, drogi ochronne | reguła: odcinek na zwrotnicę + odcinki torowe między semaforami | **umowne** |
| Łódź Fabryczna (tory 1–9, zwr. 21–38, Tm21–36), Łódź Niciarniana (zwr. 1–5, B/C) | plan Semaforek 2016 – `Zrodla/Semaforek_Lodz_Fabryczna_2016.png` | **rzeczywiste**, uproszczone |
| Typy blokad (elektroniczna samoczynna dwukierunkowa), podział na odstępy, długości szlaków | Ie-104 § 8 pkt 17, wzór EbiScreen | **umowne** |
| Typ urządzeń: EBILock (Bombardier/Alstom) | Rynek Kolejowy 2014 | potwierdzone pośrednio |
| Zasady obsługi | Ie-20, Ie-104 (wyciąg) | wiążące |

---

## 4. Co działa w v0.4

- **Obszar LCS – dwa obrazy:** (1) Łódź Fabryczna + Łódź Niciarniana, (2) Łódź Widzew. Zakładki obrazów nad pulpitem, adresy obiektów rozpoznawane w obrębie wyświetlanego obrazu. Szlaki tunelowe ŁF–ŁN (2T, 1T, 102T, 101T) i ŁN–ŁW (2F, 1F) są wewnętrzne – oba końce obsługuje dyżurny LCS.
- **Zobrazowanie Ie-104:** symbole semaforów w osi toru, tarcze manewrowe, znaczniki końca przebiegu, zaznaczanie (lazurowe) i markowanie (pomarańczowe), stałe okna numerów pociągów zamiast ruchomych ikon.
- **Polecenia Ie-104** (§ 11–12): `adresy kod` (np. „E S1 Pociąg”, „13 Minus”, „2A Wbl”) lub w stylu EbiScreen „POC E S1”. Polecenia specjalne: zamarkowanie obiektów, potwierdzenie nie wcześniej niż po 5 s i nie później niż po 60 s.
- **Przebiegi wariantowe:** do 6 wariantów na parę początek–koniec; wybór punktem pośrednim („E 13+ S3 Pociąg”) lub Ctrl+klik na pulpicie.
- **Przebiegi manewrowe:** od tarczy/semafora do tarczy, semafora lub kozła; złożone przez kilka tarcz („Tm10 R6 Manewr”). Lokomotywy manewrowe jeżdżą tylko w nastawionych przebiegach.
- **Blokada samoczynna (Ie-104 § 8 pkt 17):** odstępy z numerami sygnałów odstępowych, samoczynne sygnały odstępowe, strzałka kierunku (szara – neutralny, żółta – ustawiony, migający grot – żądanie, czerwona – AZK), lampki TOR i STOP. Polecenia Wbl, oWbl, Pzk, Zwbl, Stop/oStop, AZK. Posterunki spoza LCS odpowiadają same.
- **Alarm żądania pozwolenia** pojawia się dopiero po 3 min bez reakcji dyżurnego (Cfg.ReqAlarmAfter).
- **Pociągi:** wjazdy ze szlaków i z torów Fabrycznej, kończenie biegu i zmiana numeru, odstawianie, ruch przez tunel ŁF–ŁN–ŁW.
- **Przebiegi manewrowe w stronę szlaku:** do semafora wjazdowego (granica przetaczania, np. „L2 W Manewr”), wyjazd manewrowy na szlak do końca pierwszego odstępu („L2 2A Manewr”, wymaga kierunku WYJAZD), powrót ze szlaku od semafora wjazdowego na sygnał zastępczy.
- **Edytor układu** (przycisk „Edytor układu”): tory, zwrotnice, semafory i tarcze, kozły, wykolejnice, odcinki izolowane, szlaki (SBL z liczbą odstępów albo blokada liniowa bez odstępów), perony, napisy, obrazy i posterunki; tablica przebiegów – wybór przebiegu zasadniczego (★) i wyłączanie przebiegów; cofanie, kontrola poprawności, zapis Data/station.json i uruchomienie w symulatorze. Można zbudować nową stację od zera.
- **Rozkład rzeczywisty PKP IC** (scenariusze „Rozkład rzeczywisty PKP IC …”): `tools/pkpic_import.py <data> app/LcsWidzew/Data/rozklad_ic.json` (źródło ptrc.gay/pkpic). Próżne z zaplecza Olechów do ŁF, zmiany czoła w ŁF, zjazdy próżne i luzem do Olechowa, postoje wg rozkładu (tory i perony w planie ruchu). Manewry jednostek ŁKA z bazy Widzew – przykładowe (źródło obejmuje tylko PKP IC).
- **Długości odcinków:** każdy odcinek izolowany i każdy odstęp szlaku ma długość w metrach (edytor: „Długość odcinka [m]”, lista odstępów szlaku); od długości zależą czasy jazdy i zajętości.
- **Obieg jednostek ŁKA (przykładowy):** pociąg kończący bieg w ŁW zmienia numer na numer jednostki (EN76-xx) i zjeżdża manewrem do bazy; z bazy jednostka wyjeżdża manewrem na tor peronowy i odjeżdża pod nowym numerem; pociągi przez ŁW do ŁF zmieniają czoło i numer w ŁF.
- **Narzędzia edytora:** siatka (G), celownik ze współrzędnymi, warstwy: odcinki, długości, węzły; szukaj (Ctrl+F); miarka (M) – droga po torach w metrach; Ctrl – tor pod kątem 45°; zaznaczanie grupy (Shift+prostokąt, Shift+klik, Ctrl+A), przesuwanie grupy myszą i strzałkami, wyrównanie do wspólnego Y, równe odstępy, przypisanie odcinka grupie, usuwanie grupy.
- **Odporność edytora:** błędna zmiana jest wycofywana z komunikatem; błędy zapisują się do `app/LcsWidzew/bledy.log`; test losowy `--editshot plik.png fuzz 800` (tysiące operacji bez błędu).
- **Szlak Widzew – Olechów (1O/2O):** blokada liniowa bez odstępów (bez SBL).
- **Tabele ZDARZENIA i ALARMY**, książka E-1758, tablica przebiegów, instruktor (usterki, lokomotywy, scenariusze), ocena.

---

## 5. Plan działania

| Etap | Zakres | Rezultat |
|---|---|---|
| **A. Wierność układu ŁW** ✔ (v0.3) | Obraz szczegółowy narysowany ręcznie na siatce wg planu Semaforka i wzoru pulpitu: wszystkie semafory, tarcze manewrowe i ostrzegawcze, powtarzacze, wykolejnice, przejazdy | Zrobione – do przeglądu przez praktyka |
| **B. Zależności wg dokumentacji** | Tablica zależności i przebiegów z regulaminu technicznego, ochrona boczna, drogi ochronne wg projektu, prędkości i sygnały S1–S13, tarcze ostrzegawcze, zwrotnice ręczne zaplecza / grupy towarowej. Testy automatyczne każdego przebiegu | Zależności zweryfikowane przez praktyka |
| **C. LCS wieloposterunkowy – Łódź Fabryczna** ✔ (v0.4, częściowo) | Model obszaru LCS: wiele plików `station.json` (ŁW, ŁFa), szlak Widzew – Fabryczna (tunel, Niciarniana) jako wspólny element dwóch posterunków. Przełączanie obrazów posterunków, obraz poglądowy całego LCS (§7 ust. 9), status i przejmowanie obsługi posterunku (Ie-104 p. 20). Fabryczna: dane z OSM + plan schematyczny | Jeden pulpit LCS: Widzew + Fabryczna |
| **D. Stanowisko wielomonitorowe** | Osobne okna obrazów na kolejnych monitorach (jak na LCS), zapamiętywanie układu okien | Konfiguracja 2–3 monitorów |
| **E. Dydaktyka** | Rozkazy pisemne, telefoniczne zapowiadanie (Ir-1), książka przebiegów, dziennik ruchu, biblioteka scenariuszy, nagrywanie i odtwarzanie ćwiczenia, raport z oceną | Gotowe ćwiczenia szkoleniowe |
| **F. Instruktor – kursant** | Sieć: instruktor na osobnym komputerze, opcjonalnie drugi kursant jako dyżurny posterunku sąsiedniego | Ćwiczenia zespołowe |
| **G. Pilotaż i wydanie** | Szkolenie pilotażowe, poprawki, instalator | Wersja 1.0 |

**Najbliższe kroki:**
1. Przegląd v0.4 przez praktyka: układ Fabrycznej i Niciarnianej, podział szlaków na odstępy, nazwy sygnałów odstępowych.
2. Obraz poglądowy całego LCS (§ 7 ust. 9) i stanowisko wielomonitorowe.
3. Sygnalizacja prędkości (S1–S13) i ochrona boczna w zależnościach.

---

## 6. Ryzyka i kwestie prawne

- **Ie-20 / Ie-104:** komercyjne użycie wymaga zgody PKP PLK (zastrzeżenie na stronie tytułowej Ie-20).
- **Interfejs dostawcy (EBILock/Alstom):** wierne kopiowanie wyglądu i katalogu poleceń dostawcy wymaga jego zgody. Obecnie stosowane są zobrazowanie Ie-104 i własne kody poleceń.
- **OpenStreetMap (ODbL):** przy dystrybucji trzeba podać źródło („© współtwórcy OpenStreetMap”). Pochodna baza danych musi pozostać na licencji ODbL.
- **Dane infrastruktury krytycznej** (regulaminy techniczne, tablice zależności): tylko lokalnie, bez publikowania.
- **Ryzyko merytoryczne:** błędne zależności utrwalają złe nawyki. Każda zmiana zależności wymaga testu i akceptacji praktyka.

---

## 7. Źródła

- *Instrukcja Ie-20* (PKP PLK, 2020) – `Zrodla/Ie-20_WCAG.pdf`
- PKP PLK, *Stacja Łódź Widzew – rozpoczęcie prac*, 2013 – <https://www.plk-sa.pl/files/_migrated/news_uploads/prezentacja_Lodz_Widzew_01.pdf>
- PKP PLK, *Wyższy standard obsługi podróżnych na stacji Łódź Widzew*, 2013 – <https://www.plk-sa.pl/o-spolce/biuro-prasowe/informacje-prasowe/szczegoly/wyzszy-standard-obslugi-podroznych-na-stacji-lodz-widzew-2066>
- Rynek Kolejowy, *Łódź Widzew: Baza ŁKA najnowocześniejsza w Polsce*, 2014 – <https://www.rynek-kolejowy.pl/wiadomosci/lodz-widzew-baza-lka-najnowoczesniejsza-w-polsce-24911.html>
- OpenStreetMap / Overpass API – dane z 14.10.2026, `tools/osm/widzew_overpass.json` (© współtwórcy OpenStreetMap, ODbL)
- Wikipedia, *Łódź Widzew* – <https://pl.wikipedia.org/wiki/%C5%81%C3%B3d%C5%BA_Widzew>
- Alstom, *SRK w tunelu KDP w Łodzi*, 2026 – <https://www.alstom.com/pl/press-releases-news/2026/3/alstom-dostarczy-rozwiazania-srk-w-tunelu-kolei-duzych-predkosci-w-lodzi>
