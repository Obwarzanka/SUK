# Plan rozwoju – dla agentów AI i programistów

Dokument opisuje stan projektu, ustalenia z właścicielem i kolejne etapy. Jest **źródłem prawdy o kierunku prac** i ma pierwszeństwo przed przestarzałym `archiwum/PLAN.md` (v0.4–0.5).

Pozostałe dokumenty: `PLAN_EDYTOR_UT.md` (plan, decyzje i pytania – edytor UT), `PLAN_EDYTOR_ZOBRAZOWANIA.md` (etap 8 – edytor zobrazowania, zaakceptowany 2026-10-17), `HISTORIA_EDYTOR_UT.md` (szczegółowe opisy zrobionych prac nad edytorem UT i kalkulatorem przechyłki), `PODZIAL_MODULOW.md` (podział `Simulator` na moduły), `README.md` (obsługa programów).

Po zakończeniu etapu albo zmianie ustaleń zaktualizuj sekcje „Stan” i „Etapy”.

Ostatnia aktualizacja: 2026-10-17 (krok 10 – KZ w `Sim/Kz/`, `Simulator` jako fasada; krok 9 – system nadrzędny i PIP po zajętościach; krok 7 – zasilanie jako urządzenie; PIP jako opcja zobrazowania `display.pip`; karta zwrotnicy i wykolejnicy – krok 5 podziału modułów; decyzje właściciela: pytania 11–15 UT, najpierw karty, potem edytor zobrazowania).

---

## 1. Czym jest projekt

**Ogólny symulator szkoleniowy komputerowych urządzeń srk w Lokalnych Centrach Sterowania (LCS)** – pulpitów EbiScreen i komputerów zależności EBILock – wg instrukcji PKP PLK **Ie-20** (obsługa) i **Ie-104** (zobrazowanie).

- Pierwszy obszar to LCS Łódź Widzew (ŁW, ŁF, ŁN), ale **nie jest to symulator jednej stacji**. W kodzie nie zaszywa się nazw stacji, kodów posterunków (LW/LF/LN) ani tytułów. Wszystko pochodzi z wczytanego układu (`station.json`).
- Aplikacja na Windows: C# / WPF / .NET 8, w `app/LcsWidzew/` (nazwa katalogu historyczna).
- Właściciel jest praktykiem (dyżurny ruchu / srk). Wymaga realizmu, a wzorem wyglądu jest SimRail LCS i EbiScreen. Gdy czegoś nie wiesz o zasadach lub symbolach – **pytaj właściciela**, nie zgaduj.

## 2. Zasady pracy (ustalenia z właścicielem – obowiązują)

1. **Nie nadpisuj `app/LcsWidzew/Data/station.json`** bez wyraźnej zgody. To praca właściciela. Testy rób na kopii (`--station kopia.json`). Nowy edytor UT zapisuje własne pliki `*.ut.json` (poprzednia wersja w `.bak`).
2. **Zależności i zobrazowanie projektuje człowiek.** Komputer zależności nie wyprowadza wszystkiego sam z układu: przypisanie odcinków, przebiegi, drogi ochronne itd. to dane projektowe, jak tablica zależności. Automat może najwyżej **zaproponować**. Obrazy pulpitu rysuje użytkownik, a **nie generuj ich automatycznie** (wyjątek: jednorazowa konwersja istniejącego rysunku).
3. **Znaczniki pomocnicze edytora** (złącza, siatka, numery problemów) są tylko w edytorze, nigdy na pulpicie.
4. Gra wieloosobowa przez **Radmin VPN** (łączenie po IP, TCP). Liczba posterunków i stanowisk jest dowolna, z przekazywaniem obszaru sterowania (za zgodą).
5. Wygląd zgodny z Ie-104 i wzorami SimRail / EbiScreen. Wzory są w `zrzuty/`, `zrzuty/wzory/` i `Zrodla/`.
6. Teksty w programie i w komunikacji z właścicielem po polsku.
7. Przed zbudowaniem programu sprawdź, czy symulator jest zamknięty: otwarty blokuje `LcsWidzew.exe`. Testy buduj do osobnego katalogu (`dotnet build -c Release -o <tmp>`). Proces testu też blokuje swój katalog – przy równoległych testach używaj różnych katalogów wyjściowych.
8. Właściciel nie lubi zgadywania i pół-rozwiązań. Pokazuje wzory (zrzuty SimRail/EbiScreen) – odwzoruj je dokładnie i sprawdź na zrzucie (`--screenshot`, `EdytorUT.exe --shot`), zanim powiesz, że działa. Mów wprost, czego nie sprawdzono.
9. Po każdej zmianie zbuduj wersję, którą uruchamia właściciel (`dotnet build -c Release` w `app/LcsWidzew`, w `app/Lcs.EdytorUT`, a po zmianach kalkulatora – w `app/Lcs.Przechylka`, Komputerowni – w `app/Lcs.Komputerownia`) i powiedz, czy się udało.

## 2a. Wymagania wyglądu pulpitu (ustalone na wzorach – nie zmieniać bez zgody)
- **Blokada liniowa:** strzałka w osi toru tuż za semaforem wjazdowym (od strony szlaku), szara dwustronna – brak kierunku, żółta – kierunek, migający grot – żądanie, czerwona – AZK. **Zielona kreska nad strzałką** = szlak wolny i brak przebiegu na szlak.
- **Szlak wewnętrzny** (między stacjami LCS): strzałka przy **każdej** stacji, każda z własnym napisem (`ends[].label`, np. 1A / 1G), obie zawsze ten sam kierunek. Nie jedna strzałka na środku.
- Na SBL widać tylko numery sygnalizatorów odstępowych (bez symboli). Na szlaku bez odstępów – oznaczenie szlaku (np. it1Z) za strzałką.
- Semafor wjazdowy ma mały trójkąt „koniec przebiegu wyjazdowego” po stronie stacji (▶◀); przy wyborze końca przebiegu na szlak dostaje turkusową ramkę (strzałka szlaku – nie).
- **Wybór przebiegu:** początek – turkusowa prostokątna ramka; wszystkie możliwe końce – turkusowe kółko z wypełnionym kwadratem (ciemny symbol). Wariant: po wybraniu początku zwykły klik na zwrotnicy (turkusowy prostokąt na torze), kolejne zwrotnice – kolejne warianty.
- **Nastawianie przebiegu:** dopóki zwrotnice się przekładają – droga szara, numery zwrotnic białe, sygnalizator początkowy w turkusowej ramce; zielona/żółta droga dopiero po utwierdzeniu.
- **Zwrotnica:** nieczynne odgałęzienie odsunięte od iglicy (wyraźna przerwa) i szare na całej długości łącznika; w trakcie przekładania iglica i oba odgałęzienia odcięte. Zaznaczenie zwrotnicy (menu) – ramka wokół zobrazowania na torze, nie wokół numeru. „+” – położenie zasadnicze.
- Symbole: PK – mały trójkąt (czerwony, gdy kończy się w nim przebieg); kozioł oporowy – symbol „[” (sam symbol, `bumpers`); WY/WJ – powtarzacz `sp`; przejazd – różowa linia z Y/T i kilometrem; tor rozebrany – przerywany.
- Znaczniki edytora (złącza SZLAK WEWNĘTRZNY, numery problemów) – nigdy na pulpicie.

## 3. Źródła wiedzy (czytaj przed zmianami w logice)

| Plik | Zawartość |
|---|---|
| `Zrodla/Ie-20_WCAG.pdf` | obsługa urządzeń srk (polecenia, przebiegi, blokady, usterki) |
| `Zrodla/Ie-1_obowiazuje_od_2026-01-17__uz_1041_25__WCAG.pdf` | **Ie-1** – sygnalizacja: sygnały semaforów S1–S13a, Sz (§ 4), Ms (§ 8), zestawienie obrazów sygnałowych (zał. 1) → `app/LcsWidzew/Data/sygnaly_ie1.json` |
| `Zrodla/Ie-104_2025_WCAG.pdf` | zobrazowanie: symbole, barwy, polecenia (§ 4–12) |
| `Zrodla/KOMPUTERY.pdf` + **`Zrodla/KOMPUTERY_notatki.md`** | podręcznik UTK: LCS, warstwy systemu, blokada stacyjna, SBL 2/3/4-stawna, półsamoczynna (Po/Ko/Poz/Pwl/dPo/dKo), komputerowa blokada liniowa |
| **`Zrodla/EBILock_komputerownia.md`** | budowa EBILock 950 (komputer zależności + gorąca rezerwa, pętle i koncentratory, sterowniki obiektowe i karty: zwrotnica, sygnalizator, odcinek, przekaźnikowe, we/wy), stany i usterki kart, propozycja modułów symulatora i komunikatów; oznaczone PEWNE / WTÓRNE / DOMYSŁ, pytania P1–P8 do właściciela |
| `Zrodla/Instrukcja_Ig-10_-_WCAG.pdf` | **Ig-10 [D-27]** – plany schematyczne: treść (§ 4), skale, **znaki umowne (zał. 8)** – wzór dla planu w edytorze i okna „Plan stacji” |
| `zrzuty/wzory/`, `Zrodla/SimRail_*` | wzory wyglądu pulpitu |
| `README.md` | obsługa programu, edytor UT (ze skrótami klawiszy), kalkulator przechyłki, gra sieciowa, testy |

## 4. Architektura (stan obecny)

Trzy warstwy, jak w rzeczywistości (KOMPUTERY.pdf, Tabela 23):

1. **Zobrazowanie / stanowisko**
   - `UI/PanelView.cs` (rysowanie), `MainWindow.xaml(.cs)` (okno pulpitu), `UI/Dialogs.cs`, `UI/NetWindow.cs`, `UI/KzWindow.cs` (podgląd komputera zależności).
   - Korzysta **wyłącznie** z sesji `ILcsSession` (`Sim/Session.cs`) i meldunków `Indication` (`Sim/Meldunki.cs`). Nie sięga do wnętrza symulatora.
2. **Komputer zależności (EBILock):** `Sim/Kz/` – `KomputerZaleznosci.cs` (stan, tablica przebiegów, statusy kart, sygnały), `Interlocking.cs` (przebiegi), `Blocks.cs` (blokady liniowe), `Commands.cs` (wykonanie poleceń). System nadrzędny (parser poleceń Ie-104, dwuetapowość, stanowiska, obszary sterowania, PIP): `Sim/Pulpit/`. `Sim/Simulator.cs` – fasada (czas, rejestrator, topologia, moduły, cykl `Tick`).
3. **Teren:** `Sim/Trains.cs` (ruch pociągów).

Moduły wydzielone z `Simulator` (etap 7, kroki 1–3; `Simulator` tworzy je w konstruktorze i jest dla UI fasadą):
- `Sim/Rejestrator.cs` – dziennik zdarzeń i alarmy (wspólna usługa); `Simulator.Log/Alarm/Remark/Events/Alarms` przekazują do `sim.Rej`.
- `Sim/Instruktor.cs` – lista usterek (`Instruktor.Faults`), `sim.Instruktor.ApplyFault`, usterki scenariusza.
- `Sim/Rozklad.cs` + `RealTimetable.cs` – kolejka pociągów (`sim.Rozklad.Queue`), scenariusze (`Rozklad.Scenarios`), rozkład GTFS, `Enqueue`.
- `Sim/Otoczenie.cs` – posterunki sąsiednie spoza LCS: wyprawianie pociągu na szlak (`Wyprawienie`), żądanie Pzk, odpowiedź na Wbl, blok początkowy / końcowy sąsiada.
- Cykl `Simulator.Tick`: zasilanie, zwrotnice, blokady (z komunikatami do `Otoczenie`), `Rozklad.Tick` i `Instruktor.Tick` (tylko gdy są pociągi), `Trains.Tick`, sygnały, przebiegi, alarmy.
- `Trains.Load(key)` ładuje scenariusz: `Rozklad.Load` + `Instruktor.Load`.
- `Sim/Urzadzenia/` – urządzenia przytorowe (krok 4): `KartaOdcinka` (obwód torowy / licznik osi: osie w terenie, zajętość bez pojazdu, błąd zliczenia, zerowanie, pierwszy przejazd). Ruch zgłasza osie (`Urzadzenia.Osie`), karta melduje KZ status (`Publikuj` → `SecState.TrainOcc/Fault`), ZeroLO to rozkaz `Urzadzenia.Zeruj`, usterki odcinków idą do karty. KZ zna „pociąg na szlaku” z odcinków i meldunku sąsiada (`TrainOnBlock`, `Otoczenie.SzlakZajetyUSasiada`), nie z listy pociągów.

Pozostałe elementy:
- **Logi techniczne (S01.01, plan MP rozdz. 25):** `app/Lcs.Logi` (net8.0/net10.0) – moduły dostają `ILogger` (MEL) przez `Diagnostyka.Modul(moduleId, wersja)`; most Serilog kieruje zdarzenia do jednego ujścia w pamięci (lista dozwolonych pól, redakcja, ograniczona kolejka 1024 rekordy / 2 MiB, `TryWrite` bez czekania), osobny wątek zapisuje JSONL przez Serilog.Sinks.File + CompactJsonFormatter (rotacja, retencja, nadzór miejsca). Kody zdarzeń w `kontrakty/logi/katalog_zdarzen_v1.json` (definicje `DefinicjaZdarzenia` + `[LoggerMessage]`). W symulatorze: `App` (start/stop/błędy), `Rejestrator` → `Sim/ZdarzeniaRejestratora.cs` (kopia wpisu bez treści). Nowy moduł: własny podzakres/kody w katalogu, logger przez `Diagnostyka.Modul`, test limitów i redakcji. Testy: `app/Lcs.Logi.Testy` (xUnit v3).
- **Sieć:** `Net/LcsNet.cs` – `LcsServer` przy komputerze zależności i `RemoteSession` na stanowisku. JSON w liniach przez TCP, port 47120.
- **Model układu (symulator):** `Model/Station.cs` – wczytany `station.json`, tylko do odczytu. Nowy model UT: `app/Lcs.Model` (`UtProject`, `UtDocument`, `Geo`).
- **Edytor układu torowego:** osobny program `app/Lcs.EdytorUT` (`EdytorUT.exe`), plan w `PLAN_EDYTOR_UT.md`, historia prac w `HISTORIA_EDYTOR_UT.md`. W symulatorze nie ma już edytora.
  - `Lcs.Model` (bez okien): `UtProject` / `UtDocument` (projekt, zapis, cofanie), `Geo` (PUWG 1992, długości w terenie), `Affine`, `UtBackground` (podkłady), `UtTrack` (`Trasa` – geometria osi), `UtBudowa` (`Budowa` – budowa i łączenie torów, łuki), `UtTurnout` (katalog, rozjazdy, łączniki, skrzyżowania), `UtDevice` (urządzenia), `UtOdcinki`, `UtWyposazenie` (perony, przejazdy, kilometraż, profil), `UtKontrola` („Sprawdź”), `Symbole` (baza Ig-10).
  - `Lcs.EdytorUT`: `MainWindow*.cs` (okno, panele), `UtView*.cs` (płótno i narzędzia), `LivePreview`, `Pobieranie`, `ImageCache`, `TileMapWindow`, `ProfilWindow`, `Eksport`, `SymbolPainter`, dane w `Dane/` (`rozjazdy.json`, `symbole_ig10.json`), test w `Test*.cs`.
- **Kalkulator przechyłki:** osobny program `app/Lcs.Przechylka` (`Przechylka.exe`), na razie niezależny od edytora.
- **Podgląd komputera zależności:** `UI/KzWindow.cs` (przebiegi, utwierdzenia, liczniki czasu, blokady, łącze).
- **Testy bez okna** (z `bin/Release/net8.0-windows`):
  - `--selftest out.txt` – scenariusz z automatycznym dyżurnym (postęp w `out.txt.postep`);
  - `--screenshot p.png 3 "#page=1" "E S1 Pociąg"` – zrzut pulpitu (też `#serve=`, `#join=`, `#fault=`, `#start=`, `#via=`, `#kz=`).
  - edytor UT: `app/Lcs.EdytorUT/bin/Release/net8.0-windows/EdytorUT.exe --test wynik.txt [zrzut.png]`, oczekiwane „błędów: 0”.
  - Selftest uruchamiaj na stałej kopii testowej: `--station app/LcsWidzew/Testy/station_test.json` (kopia układu z 2026-10-15; `Data/kopie/` rotuje się – trzyma 30 ostatnich zapisów).
  - Scenariusz selftestu (`SelfTest.Run`) nie zna nazw stacji: z tablicy przebiegów bierze po jednym przebiegu wyjazdowym na każdy szlak (z Wbl i Pzk), po jednym wjeździe z każdego semafora wjazdowego i jeden manewr. Każdy przebieg czeka na sygnał zezwalający, potem jest zwalniany (ZDP/ZDM). Do tego kontrola zależności: drugi przebieg z zajętego sygnalizatora musi zostać odrzucony. Wynik w linii `WYNIK:`, braki w liniach `NIE NASTAWIONO` z przyczyną.
  - Na kopii testowej oczekiwane: **`nastawione 25 z 26`, `kontrola zależności 4 z 4`**, `OCHRONA BOCZNA … kontrola 4 z 4`, `KARTY ZWROTNIC: kontrola 8 z 8`, `SYGNAŁY: kontrola 9 z 9`, `ZASILANIE: kontrola 8 z 8`, `PIP: numer w oknie pociągu 1526 z 1526`, `KONTROLA KZ NA ŻYWO: … naruszeń: 0`, `wykrywanie celowych naruszeń: 2 z 2`. Jedyny brak – 2A: odstęp 2A-1 zajęty przez pociąg z rozkładu (prawidłowe odrzucenie Wbl).

Model danych `station.json`:
- `nodes`, `edges` (`sec` = odcinek kontroli niezajętości `POST:nazwa`, `track`, `len` [m], `vmax`), `switches`, `signals` (wjazdowy, wyjazdowy z `shunt`, tm, to, sp, pk, odstepowy);
- `blocks` (szlaki: `type` sbl/sbl1/pol/pol1/tel, `ends` z `label`), `boundaries`, `farEnds`, `trackEnds`, `bumpers`, `ports`;
- `pages` (obrazy), `posts`, `labels`, `platforms`, `crossings`, `routeRules`;
- `sections` (rodzaj kontroli niezajętości `lo` licznik osi / `ot` obwód torowy), `detection` (domyślny);
- `display` – zobrazowanie (patrz Etap 1): `nodes`, `edges` oraz własne `labels`, `platforms`, `crossings` pulpitu (te w korzeniu należą do planu schematycznego), `hiddenSyms`, `pip` – posterunki z wyświetlaczami numerów pociągów (brak / `true` – wszystkie, `false` – żaden, lista kodów; decyzja właściciela 2026-10-17: PIP nie zawsze jest, opcja w danych zobrazowania; `Station.PipPosts`, `Station.Pip(sec)`);
- zwrotnice: `kind` (typ rozjazdu, np. „Rz 1:9 R190”), `radius`, `ratio`, `vbok` (puste – z typu, tabela `SwitchDef.Types`), `vprosto`, `manual`;
- tory: `radius` (łuk [m]), `cant` (przechyłka [mm]), `nasyp` (wysokość toru nad terenem [m], ujemna – przekop) – na razie tylko dane, bez wpływu na prędkość;
- tory (Ig-10): `cat` – `gz` główny zasadniczy / `gd` główny dodatkowy / brak = boczny (grubość osi na planie), `elec: true` – zelektryfikowany (niebieski); wypełnia właściciel (panel toru albo panel grupy po zaznaczeniu toru);
- sygnalizator: `mount` – konstrukcja na planie (maszt / `wysiegnik` / `nad` / `bramka` + `gantry` / `karzel`), bramki w `gantries`; `lamps` – liczba komór na planie Ig-10 (brak = domyślna: wjazdowy 5, wyjazdowy 4 / 3 bez Ms2, odstępowy 3, tarcze 1);
- sygnalizator `pk`: `routes` P/M/PM.

**Pułapki techniczne:**
- Strony PDF (np. Ig-10 zał. 8) ogląda się przez `tools/pdfr` (render do PNG) – narzędzie Read bez pdftoppm nie pokazuje stron.
- Edycja plików przez heredoc w bash psuje `\n` i ukośniki w literałach C# – zapisuj skrypt łatki (Python) narzędziem Write i uruchamiaj `python -I plik.py`; w C# zamiast `"\n"` używaj `{(char)10}`/`Environment.NewLine`.
- `Station` dla pulpitu to `L.View` (zobrazowanie); komputer zależności zawsze liczy z `L` (układ). `Simulator` implementuje `IPulpitSource.L` jawnie jako `L.View`.
- Węzeł ukryty na zobrazowaniu ma współrzędne `Station.Hidden` (-1e7).

## 5. Etapy

### Etap 1 – Edytor układu torowego od nowa (od 2026-10-16)
Decyzja właściciela (2026-10-16): edytory UT i zobrazowania budujemy od nowa jako osobne programy. Stare usunięte, bez przerabiania. **Plan, decyzje i etapy: `PLAN_EDYTOR_UT.md`** (XYZ w PUWG 1992, dwa widoki skali, parabola 3°, katalog rozjazdów KZN + `Zrodla/rozjazdy_1.pdf`, wysokości z NMT).

Szczegółowe opisy zrobionych prac (etapy 0–7 i poprawki po uwagach właściciela) są w **`HISTORIA_EDYTOR_UT.md`**. Tu tylko skrót:
- **Etap 0** (2026-10-16): stare edytory usunięte z `LcsWidzew`, stan sprzed usunięcia – commit **`d2c7431`** (tam też pełna historia starego edytora). Zostają: pulpit, komputer zależności, „Plan stacji” na żywo (`Ig10.cs`), `Model/Station.cs`, `Model/TrackOrientation.cs`, selftest.
- **Etapy 1–2:** szkielet (`Lcs.Model`, `Lcs.EdytorUT`, płótno w metrach PUWG 1992, zapis `*.ut.json` z `.bak`, cofanie), długości w terenie (`Geo.GroundDistance`), podkłady: ortofotomapa Geoportalu (mapka kafelków, stała siatka, puste białe kafelki), grafika z kalibracją, podgląd na żywo (Geoportal / Esri / OSM, OpenRailwayMap na wierzchu, kafelki na dysku), miarka.
- **Etap 3 i poprawki:** tory jako wielobok styczny z łukami R i przejściowymi L (parabola 3°), narzędzie Tor (Prosta / Łuk, łączenie łukiem albo esem, R 0), uchwyt łuku, Równoległy (P), Łuk od–do (K), przesuwanie myszą i strzałkami, usuwanie.
- **Etap 4 i poprawki:** rozjazdy z katalogu (`Dane/rozjazdy.json`), odgałęzienia trzymające skos, łączniki (też z dwóch rozjazdów i na łuku – łuk wyrównujący), rozjazdy łukowe Rłj / Rłd, wstawki międzyrozjazdowe, rozjazdy krzyżowe i skrzyżowania przeciągnięciem, „Dopasuj promienie”, widok Schemat jak w ISDR (siatka w Schemacie), znaki z bazy `Dane/symbole_ig10.json` w skali arkusza Ig-10.
- **Etap 5:** urządzenia na torze (semafory, tarcze, W4, W5, wykolejnice, kozły, liczniki osi, złącza, ukresy) ze znakami wg Ig-10 i BSK ISDR, konfiguracja semafora (komory, mocowanie, pasy, W24, W21), odcinki kontroli niezajętości, perony, przejazdy, kilometraż linii.
- **Etap 6:** profil podłużny – niweleta z załomami i łukami pionowymi, teren z NMT GUGiK.
- **Etap 7:** „Sprawdź” (F7), warstwy wg Ig-10, eksport w skali do PDF / PNG.
- Ostatni pełny test `--test` po zacommitowanych zmianach: **0 błędów**. Nie sprawdzono prawdziwą myszą ani na prawdziwej drukarce.
- **Łuk z punktów (J):** wierzchołek (kierunki prostych), promień i (Tab) krzywe przejściowe dopasowane metodą najmniejszych kwadratów do punktów klikniętych na osi toru na zdjęciu; test – wierzchołek narysowany 8 m obok wraca z dokładnością 6 cm, R 700 → 999 (prawdziwy 1000); ze stałymi prostymi (W) R 1000 / L 59 przy 1000 / 60.
- **Łuk o zmiennym promieniu:** PPM na łuku – „Podziel łuk tutaj” / „na 2–6 części” (kształt bez zmian; dwuklik na łuku też dzieli, zamiast wstawiać załamanie R 0), „Łuk z punktów” dopasowuje wszystkie części naraz (test: R 600 / 899 przy 600 / 900, 94 ms – odległość punktu od osi ze wzorów zamiast próbkowania, wcześniej 63 s).
- **Licznik osi po stronie kliknięcia** (właściciel): urządzenia bez kierunku (licznik osi, złącze, ukres) – strona toru z miejsca kliknięcia (dawniej zawsze prawa względem km toru).
- **Światło komory – trzy stany** (uwaga właściciela): stałe / stałe lub pulsujące (2 kreski) / tylko pulsujące (4 kreski); zapis „*” / „**”.
- **Bramka sygnałowa jak na planach** (Semaforek, Łódź Widzew): semafory „na bramce” w jednym miejscu – wspólna belka przez ich tory, słupy w stronę komór ze stopą, semafor wisi (komora górna przy belce, pasy i W21 na drugim końcu).
- **„Popraw łuk” pod rozjazdem:** przy uwadze „na granicy prostej i łuku” / „na krzywej przejściowej” przyciski w panelu i PPM rozjazdu – granica łuku poza rozjazd przez zmianę R (`Rozjazdy.ArcFixes`), tylko warianty bez uwagi i bez szkody dla innych rozjazdów toru.
- **Metody budowy łuku – całość** (`PLAN_EDYTOR_UT.md` § 5g): trasa z elementów (prosta / klotoida / łuk, `UtTrack.Align`) z edycją na wykresie krzywizny pod mapą i w tabeli, L1 ≠ L2 i blokada R w wieloboku, długość łuku / styczna → R, uchwyty na torze (prosta równolegle, początek łuku), „Łuk z punktów” na trasie z elementów (Levenberg–Marquardt), punkty z OSM, uwagi łuków w „Sprawdź” (skok krzywizny, prosta między łukami § 36, R i L wobec Vmax).
- **Wygładź łuk i przejściowe z prędkości:** PPM „Wygładź łuk” – grupa łuków → jeden łuk z L / bez L / dwa łuki jak najbliżej obecnego kształtu; panel toru „Przejściowe z prędkości” – h i L ze wzorów kalkulatora przechyłki (wzory przeniesione do `Lcs.Model/UtPrzechylka.cs`, wspólne z `Przechylka.exe`), przechyłka zapisana w wierzchołku (`UtVertex.H`), środek łuku na miejscu.
- **Usuwanie kawałka toru i zaznaczanie kilku:** PPM na torze – „Usuń ten kawałek” (prosta / łuk, tor rozcina się na dwa) i „Rozetnij tor tutaj”; Ctrl + klik i Shift + prostokąt zaznaczają kilka elementów, Del usuwa naraz (jedno Ctrl+Z).
- Obok edytora: **kalkulator przechyłki** `app/Lcs.Przechylka` (`Przechylka.exe`) – wartości dopuszczalne domyślne są przykładowe, do potwierdzenia przez właściciela; później wpięcie do edytora UT (pole L przy łuku).

**Następny krok** (decyzja właściciela 2026-10-17, `PLAN_EDYTOR_UT.md` § 7): najpierw karty urządzeń w symulatorze (kroki 5, 6, 6a, 7, 9, 10 zrobione 2026-10-17; krok 8 – ruch wg sygnałów w terenie – odłożony przez właściciela), potem: w UT funkcja semafora i posterunki (§ 7 pkt 4–5), plan edytora zobrazowania (etap 8) do akceptacji, przekazanie UT do symulatora (etap 9), potem etap 8 – edytor zobrazowania.

**Z dawnego etapu 1 – dotyczy symulatora (zostaje):**
**ZROBIONE (2026-10-15):** okno **„Plan stacji” na żywo** (`UI/PlanStationWindow.cs`, przycisk „Plan stacji”; test `--screenshot … "#plan=post:LW"` / `"#plan=train:NUM"`). Do rozwinięcia: S5/S4 na semaforach (dziś zielone/czerwone/białe), typ pojazdu z rozkładu (dziś kategoria lub jednostka), plan na stanowiskach sieciowych. Pierwotny opis: w symulatorze (wzór: zrzut z planem stacji z pociągami jako szare prostokąty z numerem i typem pojazdu, semafory jako głowice z kolorami świateł, „śledzenie składu”, wybór nastawni). Rysuje plan schematyczny (`L`, nie zobrazowanie) ze stanem terenu.

- **Orientacja torów** (`Model/TrackOrientation.cs`): każdy tor ma kierunek „+” = od A do B, spójny przez węzły (zwykły węzeł – przejście, zwrotnica – ostrze po przeciwnej stronie niż oba odgałęzienia; odgałęzienia = para torów o najbliższym kierunku, wg geometrii, nie X). `Station.EdgeDir` = A→B (pulpit / zobrazowanie: dalej z rysunku – `GeoDir`). Stary plik (bez `"orient": "ab"`) – jednorazowo w pamięci: tory odwracane tak, by A→B = dawny „wschód” (X / pole `dir`), zapisane ±1 (sygnalizatory `dir`, końce szlaków `outward`, `defDir`) zachowują znaczenie. Po każdej zmianie w edytorze (`StationDoc.Build` → `Orient`): tory niezgodne z sąsiednimi (np. narysowane od prawej, nowe połączenia) odwracane, a sygnalizatory i szlaki przy nich – przeliczane (wg toru przed sygnalizatorem / toru szlaku). Węzeł z dwoma torami pod kątem ostrzejszym niż 90° (tor zawraca w węźle – błąd rysunku) – bez przejazdu, jak dawniej; „Sprawdź” to pokazuje (u właściciela: **LF:n50** – tory e419 / e301 leżą na sobie przy zwrotnicy 86; bez tej reguły powstawało 31 przebiegów „przez szpilkę”). Sprzeczności (trójkąt torowy, pętla) – w „Sprawdź”.

- **Ochrona boczna przebiegu** (poradnik UTK dla inspektorów, t. I, 1.5.8, Tabela 22: „rozjazdy utwierdzone w ochronie bocznej”, wykolejnice, „sygnalizatory utwierdzone w ochronie przebiegu”): `Simulator.FlankFor` – od każdej zwrotnicy przebiegu nieużywaną gałęzią do pierwszego elementu: wykolejnica (założona), sygnalizator zwrócony w stronę przebiegu („Stój”), zwrotnica osiągnięta od gałęzi (położenie w drugą gałąź); od ostrza – brak ochrony. Elementy w `RouteDef.Flank`. Przy nastawianiu zwrotnice ochronne przestawiają się i są utwierdzane (`SwState.Flank`, `DerState.Flank`, `SigState.Flank`), warunek sygnału zezwalającego – kontrola położenia; zwalnianie razem ze zwrotnicą przebiegu (odcinkami) i przy zwolnieniu całego przebiegu. Przeszkody: element ochrony utwierdzony w innym przebiegu / w ochronie innego przebiegu w drugim położeniu, odcinek zajęty, brak kontroli, NN0. Polecenia Plus/Minus, PlusBZ/MinBZ, wykolejnica i Sz na elemencie w ochronie – odrzucone. Podgląd komputera zależności: kolumna „Ochrona boczna”, zwrotnice z ochroną. Na pulpicie bez zmian (Ie-104 – do ustalenia z właścicielem, czy i jak pokazywać).
- Selftest: osobna linia `OCHRONA BOCZNA: przebiegi z ochroną 959 z 979; kontrola 4 z 4` (przestawienie i utwierdzenie zwrotnicy ochronnej, odrzucenie jej przestawienia, blokada przebiegu sprzecznego, zwolnienie). `WYNIK` bez zmian: 25 z 26, 4 z 4.
- Dalsze braki komputera zależności wg poradnika (do ustalenia kolejności z właścicielem): zwrotnice w drodze ochronnej (dziś droga ochronna = jeden odcinek), ochrona boczna dalej niż pierwszy element (dojście od ostrza), blokada stacyjna (1.5.9), rejestrator zdarzeń z odtwarzaniem (Tabela 21 pkt 3), przejazdy (1.10), awaryjny terminal (Tabela 25).

**Plan podziału programu na moduły** (ustalenia z właścicielem 2026-10-16; podstawa: `Zrodla/EBILock_komputerownia.md`): pulpit (zobrazowanie + polecenia, PIP); komputer zależności (tylko srk: przebiegi wg tablicy zależności, utwierdzenia, ochrona boczna, blokada stacyjna, logika blokady liniowej – nie prowadzi pociągów); urządzenia przytorowe / sterowniki obiektowe z kartami (napędy zwrotnic, sygnalizatory, liczniki osi / obwody torowe, przejazdy – stany, czasy, usterki; dziś brak – pociągi sięgają wprost do stanu zależności); ruch pociągów; rozkład jazdy; otoczenie (sąsiednie posterunki spoza LCS); multiplayer i łączność; instruktor (scenariusze, usterki, rejestrator, ocena). Wspólne dane projektowe: `station.json` + edytor. Dziś `Simulator` to jedna klasa (partial) – podział to etap 7.

### Etap 2 – Logika blokad wg KOMPUTERY.pdf
- **Półsamoczynna jednoodstępowa:**
  - Wbl → miganie strzałki → sygnał wyjazdowy → **Pwl** (przeciwwtórność, jeden sygnał na zwolnienie);
  - **Po** po wygaszeniu sygnału, co odblokowuje **Ko** u sąsiada; wjazd stwierdzony czujnikami → **Ko**;
  - **Poz** – zmiana kierunku;
  - **dPo** / **dKo** przy sygnale zastępczym i rozkazie pisemnym, z licznikami;
  - lampki białe i czerwone.
- **SBL: stawność 2/3/4**, sygnały S1/S2/S5/S4 na semaforach odstępowych, szczelina **kTor**, semafory przeciwnego kierunku ciemne.
- Warunki Wbl/Pzk/Zwbl: szlak wolny i brak utwierdzonego przebiegu wyjazdowego.

### Etap 3 – Projektowanie zależności (tablica zależności)
Edycja przebiegu w edytorze:
- odcinki, zwrotnice z położeniami, droga ochronna, ochrona boczna, prędkość, sygnał;
- generator przebiegów tylko proponuje.

Walidacja sprzeczności. Testy każdego przebiegu.

### Etap 4 – Numery pociągów (PIP)
PIP jest opcją zobrazowania (`display.pip`, ZROBIONE 2026-10-17 – bez PIP pulpit nie pokazuje numerów); dalej – w edytorze zobrazowania jako ustawienie posterunku. Śledzenie numerów po zajętościach (`Sim/Pulpit/Pip.cs`) – ZROBIONE 2026-10-17 (krok 9). Do zrobienia: ręczne polecenia dyżurnego (nadanie, usunięcie, przesunięcie numeru), miganie numeru nieprzesuniętego automatycznie, numer czerwony wpisany na wolny tor (Ie-104 § 8 pkt 26 ust. 4).
Nadanie i usunięcie numeru, przesunięcie między torami (tryb awaryjny), notatki przy pociągu, kolumna numerów na szlaku jak w EbiScreen.

### Etap 5 – Styl EbiScreen jako drugi wygląd pulpitu
- Nagłówek: Zalogowany, MIEJSCOWY/ZDALNY, stacja, „Liczba osi”.
- Przełączniki ZG/PIP/STR.
- SERWER EBISCREEN 1/2 i „Komputer zależnościowy” (stany 2z2).
- Cieńsze tory i symbole jak na wzorach.

### Etap 6 – Urządzenia dodatkowe
- EOR (ogrzewanie rozjazdów), przejazdy (SSP, dróżnicy), oświetlenie.
- Awaryjny terminal (Tabela 25).
- Blokada stacyjna (Dn/On, Dz/Oz, Żz, DnSz/OnSz, jednorazowość) dla stacji z kilkoma okręgami lub stanowiskami.

### Etap 7 – Teren za sterownikami obiektowymi
Komputer zależności zleca przestawienie i czeka na meldunek. Pociągi widzą tylko teren. Komparator 2z2 i przejście w stan bezpieczny przy niezgodności (usterka instruktora).

**Podział `Simulator` na moduły – plan zaakceptowany przez właściciela 2026-10-16.** Pełny opis interfejsów (komunikaty: polecenie systemowe, meldunek, rozkazy KZ → karta, statusy karta → KZ, Ruch ↔ teren, KZ ↔ otoczenie, usterka) – w `PODZIAL_MODULOW.md`; skrót:
- moduły: pulpit / system nadrzędny, rejestrator, komputer zależności (bez pociągów), urządzenia przytorowe (karty: zwrotnica, sygnalizator, odcinek OT/LO, wykolejnica, później przejazd; zasilanie), ruch pociągów (widzi tylko urządzenia: sygnał wyświetlany, iglice), rozkład, otoczenie, sieć, instruktor;
- kroki: 0 pomiar odniesienia; 1 Rejestrator; 2 Instruktor; 3 Rozkład + Otoczenie; 4 karta odcinka; 5 karta zwrotnicy i wykolejnicy; 6 karta sygnalizatora; 6a okno „Komputerownia / karty”; 7 zasilanie; 8 ruch bez przecieków (manewry wg tarcz, prędkość z sygnału); 9 system nadrzędny (parser, dwuetapowość, stanowiska, PIP); 10 cienka fasada;
- warunek każdego kroku: build, selftest 25 z 26 / 4 z 4, test edytora UT (`EdytorUT.exe --test`, 0 błędów); w krokach przenosin dodatkowo identyczny dziennik selftestu (`--selftest out.txt` zapisuje pełny dziennik do `out.txt.zdarzenia`) i identyczne wyniki scenariuszy `--selftest out.txt usterki|zima|manewry|ic_05`. (Do 2026-10-16 także `--edtest`, `fuzz` / `dfuzz`, `testowo`, `mijanka` – usunięte razem ze starym edytorem.)
- **ZROBIONE 2026-10-16: kroki 0–4** (przenosiny i karta odcinka; zachowanie bez zmian – dziennik selftestu i scenariusze usterki/zima/manewry/ic_05/ic_15/ic_18 identyczne).
- **ZROBIONE 2026-10-17: krok 5** – karta zwrotnicy i wykolejnicy (`Sim/Urzadzenia/KartaZwrotnicy.cs`): rozkaz → przekładanie → status do KZ; rozprucie rozstrzyga KZ (brak kontroli + zajęty odcinek); ruch jedzie po iglicach. Selftest bez zmian wyniku + `KARTY ZWROTNIC: kontrola 8 z 8`. Szczegóły i zmiany zachowania: `PODZIAL_MODULOW.md` § 8. Krok 6 (sygnały z prędkości przebiegu i następnego semafora) czeka na Ie-1 (§ 9).
- **ZROBIONE 2026-10-17: krok 6** – karta sygnalizatora i sygnały wg **Ie-1** (`Data/sygnaly_ie1.json`, zał. 1): sygnał z prędkości przebiegu (zwrotnice na bok, VSet) i sygnału następnego semafora – np. A S13 przy B „Stój”, S12 po otwarciu B (S10); SBL 3- / 4-stawna (S3 – dwa odstępy wolne); ruch widzi sygnał wyświetlany przez kartę. Selftest + `SYGNAŁY: kontrola 9 z 9`. Szczegóły: `PODZIAL_MODULOW.md` § 11.
- **ZROBIONE 2026-10-17: Komputerownia – zakładka „Komputer zależności”**: przebiegi, utwierdzenia, dziennik KZ ↔ karty (rozkazy i zmiany statusów), polecenia z pulpitu, „Sprawdź przebieg” i **kontrola zależności na żywo** (niezmienniki KZ); w selftestcie `KONTROLA KZ NA ŻYWO: … naruszeń: 0` i wykrywanie celowych naruszeń 2 z 2. Szczegóły: `PODZIAL_MODULOW.md` § 12.
- **ZROBIONE 2026-10-17: krok 6a** – osobny program **`Komputerownia.exe`** (`app/Lcs.Komputerownia`): łączy się z symulatorem przez sieć (port serwisowy 127.0.0.1:47121 albo serwer 47120), pokazuje dla każdej karty rozkaz od KZ, status do KZ, teren i usterki; usterki kart z programu. Szczegóły: `PODZIAL_MODULOW.md` § 10, obsługa: `README.md`.
- **ZROBIONE 2026-10-17: krok 7** – zasilanie jako urządzenie (`Sim/Urzadzenia/Zasilanie.cs`): sieć, UPS, agregat, napięcie nastawcze w terenie, status do KZ (`StatusZasilania`, `Power` – kopia), rozkazy NN / oNN / AGR, karta zasilania w Komputerowni. Zachowanie bez zmian (dziennik selftestu i scenariusze identyczne) + `ZASILANIE: kontrola 8 z 8`. Szczegóły: `PODZIAL_MODULOW.md` § 13. Bez rozładowania UPS – do ustalenia z właścicielem.
- Pytania do właściciela przed krokami 5, 6, 8: rozprucie a brak kontroli (kto rozstrzyga, usterka „sygnalizacja rozprucia” bez pojazdu), przepalenie włókna czerwonego / zezwalającego, sygnalizator bez łączności (ciemny czy czerwony), manewry wg tarcz zamiast utwierdzeń, opóźnienie jednego cyklu rozkaz → meldunek.
- **ZROBIONE 2026-10-17: krok 9** – system nadrzędny (`Sim/Pulpit/SystemNadrzedny.cs`): parser poleceń, dwuetapowość, stanowiska i obszary sterowania, książka E-1758; KZ dostaje `PolecenieSystemowe` z rozwiązanymi adresami. **PIP** (`Sim/Pulpit/Pip.cs`) śledzi numery po zajętościach odcinków (nie z listy pociągów), numery z zapowiedzi sąsiada i z rozkładu; selftest `PIP: numer w oknie pociągu 1526 z 1526`. Krok 8 odłożony (decyzja właściciela). Szczegóły: `PODZIAL_MODULOW.md` § 14.
- **ZROBIONE 2026-10-17: krok 10** – komputer zależności jako osobna klasa `KomputerZaleznosci` w `Sim/Kz/` (stan, przebiegi, blokady, sygnały, statusy kart, polecenia), `Simulator` jako fasada (czas, rejestrator, topologia, moduły, `Tick`; przekierowania w `Sim/Kz/Fasada.cs`). Zachowanie bez zmian (dziennik i scenariusze identyczne). Szczegóły: `PODZIAL_MODULOW.md` § 15.
- Pozostałe przecieki (do kroków 8 i 10): ruch czyta `Sigs[].Aspect`, `Sws[].Pos`, `Secs[].LockType`, `Routes` i pisze `Rozpr`; `Otoczenie` czyta listę pociągów (`EmergingAtFar`) i porównuje strony szlaku po X (`FarSec`).

### Etap 8 – Dydaktyka i sieć
- Rozkazy pisemne, telefoniczne zapowiadanie (Ir-1), dziennik ruchu, nagrywanie i odtwarzanie ćwiczeń, ocena.
- Stanowisko instruktora przez sieć.
- Przesyłanie przez sieć samych zmian zamiast pełnego stanu (dziś pełny telegram 5×/s).

## 5a. Otwarte sprawy i pytania do właściciela
- Tabela prędkości na bok dla typów rozjazdów (R190 40, R300 50, R500 60, R760 80, R1200 100, R2500 130) – do potwierdzenia przez właściciela.
- Czy promień łuku ma ograniczać prędkość (i z jakim niedoborem przechyłki).
- ~~Skrypt selftestu używa nazw starego układu~~ – ZROBIONE 2026-10-15: scenariusz budowany z tablicy przebiegów.
- ~~Szlak „W 101” – spacja w nazwie~~ – ZROBIONE 2026-10-15 (decyzja właściciela: obsługa spacji, bez zmiany nazwy). `Simulator.JoinNames` w `Commands.cs` skleja sąsiednie słowa polecenia (do 4) w jeden adres, gdy tworzą nazwę istniejącego obiektu ze spacją (szlak, oznaczenie końca szlaku, sygnalizator, zwrotnica, odcinek, wykolejnica, koniec toru). Działa `W 101 Wbl`, `G9 W 101 Pociąg`, `W 101 Stop W`, `W 101-1 ZeroLO`. Uwaga: gdyby na obrazie był obiekt „W” i osobny „101”, a do tego istniał obiekt „W 101”, wygrywa nazwa ze spacją.
- Szlak 1W: komunikat Wbl mówi „wyjazd z Łódź Fabryczna do Łódź Fabryczna” – oba końce szlaku wewnętrznego na tym samym posterunku. Do sprawdzenia przez właściciela.
- Układ właściciela (`Data/station.json`, stan 2026-10-15): problemy w „Sprawdź” – odłączone kawałki odcinków LF:z1, z5, z71, z69, z82, pusty szlak 1ŁW, 26 odcinków z nazwą automatyczną „o…”. Poprawia właściciel (można zaproponować).
- Gra przez Radmin nie była jeszcze testowana na dwóch komputerach (tylko 127.0.0.1).
- Długości torów (`len`) niespójne między torami równoległymi (autoukład: do 142 m na LW tor 1) – do sprawdzenia przez właściciela.
- Plan Ig-10: właściciel uzupełnia w układzie kategorie torów (`cat`), elektryfikację (`elec`) i liczbę komór (`lamps`). Do potwierdzenia: znak powtarzacza na planie; kolejność barw komór (dziś od słupa: zielone, pomarańczowe, czerwone, pomarańczowe, białe).
- `PLAN.md` jest przestarzały (v0.4–0.5) i leży w `archiwum/` – źródłem prawdy jest ten plik.

## 6. Zrobione (skrót)
- Edytor układu torowego (`EdytorUT.exe`, etapy 0–7 – szczegóły w `HISTORIA_EDYTOR_UT.md`):
  - geometria w PUWG 1992, podkłady (ortofotomapa Geoportalu, grafika z kalibracją, OSM / Esri / OpenRailwayMap), widoki Schemat i Teren;
  - tory z łukami i krzywymi przejściowymi, rozjazdy z katalogu (też łukowe i krzyżowe), łączniki, wstawki;
  - urządzenia ze znakami Ig-10 / BSK ISDR, odcinki kontroli niezajętości, perony, przejazdy, kilometraż, profil z NMT;
  - „Sprawdź”, warstwy, eksport w skali do PDF / PNG.
- Dawny edytor wbudowany w symulator (usunięty 2026-10-16, commit `d2c7431`): tory, zwrotnice, sygnalizatory, szlaki SBL / PBL, panel blokady, kontrola układu, odcinki `POST:nazwa`. Dane z niego (`station.json`) dalej czyta symulator.
- Pulpit:
  - zobrazowanie wg Ie-104 i SimRail;
  - wybór przebiegu z oznaczeniem końców i wariantów przez zwrotnice;
  - nastawianie przebiegu w turkusowej ramce, droga zapala się po przełożeniu;
  - obraz poglądowy.
- Gra sieciowa (Radmin), obszary sterowania, telefon między stanowiskami, podgląd komputera zależności, utrata łączności.
- Rozkład rzeczywisty z GTFS z obiegiem jednostek ŁKA.
