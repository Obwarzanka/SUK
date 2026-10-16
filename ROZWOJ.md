# Plan rozwoju – dla agentów AI i programistów

Dokument opisuje stan projektu, ustalenia z właścicielem i kolejne etapy. Jest **źródłem prawdy o kierunku prac** i ma pierwszeństwo przed przestarzałym `PLAN.md` (v0.4–0.5).

Po zakończeniu etapu albo zmianie ustaleń zaktualizuj sekcje „Stan” i „Etapy”.

Ostatnia aktualizacja: 2026-10-16.

---

## 1. Czym jest projekt

**Ogólny symulator szkoleniowy komputerowych urządzeń srk w Lokalnych Centrach Sterowania (LCS)** – pulpitów EbiScreen i komputerów zależności EBILock – wg instrukcji PKP PLK **Ie-20** (obsługa) i **Ie-104** (zobrazowanie).

- Pierwszy obszar to LCS Łódź Widzew (ŁW, ŁF, ŁN), ale **nie jest to symulator jednej stacji**. W kodzie nie zaszywa się nazw stacji, kodów posterunków (LW/LF/LN) ani tytułów. Wszystko pochodzi z wczytanego układu (`station.json`).
- Aplikacja na Windows: C# / WPF / .NET 8, w `app/LcsWidzew/` (nazwa katalogu historyczna).
- Właściciel jest praktykiem (dyżurny ruchu / srk). Wymaga realizmu, a wzorem wyglądu jest SimRail LCS i EbiScreen. Gdy czegoś nie wiesz o zasadach lub symbolach – **pytaj właściciela**, nie zgaduj.

## 2. Zasady pracy (ustalenia z właścicielem – obowiązują)

1. **Nie nadpisuj `app/LcsWidzew/Data/station.json`** bez wyraźnej zgody. To praca właściciela. Testy rób na kopii (`--station kopia.json`). Edytor sam robi kopie w `Data/kopie/`.
2. **Zależności i zobrazowanie projektuje człowiek.** Komputer zależności nie wyprowadza wszystkiego sam z układu: przypisanie odcinków, przebiegi, drogi ochronne itd. to dane projektowe, jak tablica zależności. Automat może najwyżej **zaproponować**. Obrazy pulpitu rysuje użytkownik, a **nie generuj ich automatycznie** (wyjątek: jednorazowa konwersja istniejącego rysunku).
3. **Znaczniki pomocnicze edytora** (złącza, siatka, numery problemów) są tylko w edytorze, nigdy na pulpicie.
4. Gra wieloosobowa przez **Radmin VPN** (łączenie po IP, TCP). Liczba posterunków i stanowisk jest dowolna, z przekazywaniem obszaru sterowania (za zgodą).
5. Wygląd zgodny z Ie-104 i wzorami SimRail / EbiScreen. Wzory są w `zrzuty/`, `zrzuty/wzory/` i `Zrodla/`.
6. Teksty w programie i w komunikacji z właścicielem po polsku.
7. Przed zbudowaniem programu sprawdź, czy symulator jest zamknięty: otwarty blokuje `LcsWidzew.exe`. Testy buduj do osobnego katalogu (`dotnet build -c Release -o <tmp>`). Proces testu też blokuje swój katalog – przy równoległych testach używaj różnych katalogów wyjściowych.
8. Właściciel nie lubi zgadywania i pół-rozwiązań. Pokazuje wzory (zrzuty SimRail/EbiScreen) – odwzoruj je dokładnie i sprawdź na zrzucie (`--screenshot`, `--editshot`), zanim powiesz, że działa. Mów wprost, czego nie sprawdzono.
9. Po każdej zmianie zbuduj wersję, którą uruchamia właściciel (`dotnet build -c Release` w `app/LcsWidzew`) i powiedz, czy się udało.

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
| `Zrodla/Ie-104_2025_WCAG.pdf` | zobrazowanie: symbole, barwy, polecenia (§ 4–12) |
| `Zrodla/KOMPUTERY.pdf` + **`Zrodla/KOMPUTERY_notatki.md`** | podręcznik UTK: LCS, warstwy systemu, blokada stacyjna, SBL 2/3/4-stawna, półsamoczynna (Po/Ko/Poz/Pwl/dPo/dKo), komputerowa blokada liniowa |
| **`Zrodla/EBILock_komputerownia.md`** | budowa EBILock 950 (komputer zależności + gorąca rezerwa, pętle i koncentratory, sterowniki obiektowe i karty: zwrotnica, sygnalizator, odcinek, przekaźnikowe, we/wy), stany i usterki kart, propozycja modułów symulatora i komunikatów; oznaczone PEWNE / WTÓRNE / DOMYSŁ, pytania P1–P8 do właściciela |
| `Zrodla/Instrukcja_Ig-10_-_WCAG.pdf` | **Ig-10 [D-27]** – plany schematyczne: treść (§ 4), skale, **znaki umowne (zał. 8)** – wzór dla planu w edytorze i okna „Plan stacji” |
| `zrzuty/wzory/`, `Zrodla/SimRail_*` | wzory wyglądu pulpitu |
| `README.md` | obsługa programu, edytor, gra sieciowa, testy |

## 4. Architektura (stan obecny)

Trzy warstwy, jak w rzeczywistości (KOMPUTERY.pdf, Tabela 23):

1. **Zobrazowanie / stanowisko**
   - `UI/PanelView.cs` (rysowanie), `MainWindow.xaml(.cs)` (okno pulpitu), `UI/Dialogs.cs`, `UI/NetWindow.cs`, `UI/KzWindow.cs` (podgląd komputera zależności).
   - Korzysta **wyłącznie** z sesji `ILcsSession` (`Sim/Session.cs`) i meldunków `Indication` (`Sim/Meldunki.cs`). Nie sięga do wnętrza symulatora.
2. **Komputer zależności (EBILock):** `Sim/Simulator.cs`, `Interlocking.cs` (przebiegi), `Blocks.cs` (blokady liniowe), `Commands.cs` (polecenia Ie-104), `Desk.cs` (stanowiska, obszary sterowania).
3. **Teren:** `Sim/Trains.cs` (ruch pociągów).

Moduły wydzielone z `Simulator` (etap 7, kroki 1–3; `Simulator` tworzy je w konstruktorze i jest dla UI fasadą):
- `Sim/Rejestrator.cs` – dziennik zdarzeń i alarmy (wspólna usługa); `Simulator.Log/Alarm/Remark/Events/Alarms` przekazują do `sim.Rej`.
- `Sim/Instruktor.cs` – lista usterek (`Instruktor.Faults`), `sim.Instruktor.ApplyFault`, usterki scenariusza.
- `Sim/Rozklad.cs` + `RealTimetable.cs` – kolejka pociągów (`sim.Rozklad.Queue`), scenariusze (`Rozklad.Scenarios`), rozkład GTFS, `Enqueue`.
- `Sim/Otoczenie.cs` – posterunki sąsiednie spoza LCS: wyprawianie pociągu na szlak (`Wyprawienie`), żądanie Pzk, odpowiedź na Wbl, blok początkowy / końcowy sąsiada.
- Cykl `Simulator.Tick`: zasilanie, zwrotnice, blokady (z komunikatami do `Otoczenie`), `Rozklad.Tick` i `Instruktor.Tick` (tylko gdy są pociągi), `Trains.Tick`, sygnały, przebiegi, alarmy.
- `Trains.Load(key)` ładuje scenariusz: `Rozklad.Load` + `Instruktor.Load`.

Pozostałe elementy:
- **Sieć:** `Net/LcsNet.cs` – `LcsServer` przy komputerze zależności i `RemoteSession` na stanowisku. JSON w liniach przez TCP, port 47120.
- **Model układu:** `Model/Station.cs` (wczytany, tylko do odczytu) i `Model/StationDoc.cs` (dokument edytora na `JsonObject`, z cofaniem).
- **Edytor:** `UI/EditorWindow.cs` + `UI/EditorWindow.Display.cs` (tryb zobrazowania, klasa `partial`). Kontrola układu: `StationDoc.Problems()` zwraca problemy z miejscem na rysunku (węzeł albo `edge:id`), okno „Kontrola układu” numeruje je na rysunku.
- **Podgląd komputera zależności:** `UI/KzWindow.cs` (przebiegi, utwierdzenia, liczniki czasu, blokady, łącze).
- Testy trybu edytora `--editshot p.png <tryb>`: `luki out.json`, `mijanka out.json [skos°]`, `testowo out.json [skos°]`, `plantools out.json`, `pomiar out.json`, `ostrosc "lat, lon" out.json`, `profil "lat, lon" out.json`, `jakosc "lat, lon" out.json`, `bramka out.json`, `kafelki orto|esri "lat, lon" out.json [mapka.png]`, `podklad orto|osm|esri "lat, lon" szer wys out.json`, `skala out.json [podkład]`, `dclear out.json`, `fuzz N seed`, `dfuzz N seed`, `deco out.json`, `validate`, `problems i`, `display out.json`, `plan`, `planskew`, `autoname`, `block ID [nowaNazwa]`, `blockpanel ID`, `szlakdiag`, `script`, `szlaki`, `szlaki2`, `rozjazd`, `normalize`, `respace`, `select`, `zoom`, `demo`, `userszlak`. Wynik tekstowy w `p.png.txt`.
- **Testy bez okna** (z `bin/Release/net8.0-windows`):
  - `--selftest out.txt` – scenariusz z automatycznym dyżurnym (postęp w `out.txt.postep`);
  - `--edtest out.txt` – operacje edytora;
  - `--editshot p.png fuzz 300 7` – test losowy edytora (plan schematyczny), oczekiwane „błędów 0”;
  - `--editshot p.png dfuzz 300 7` – test losowy trybu zobrazowania, oczekiwane „błędów 0”;
  - `--screenshot p.png 3 "#page=1" "E S1 Pociąg"` – zrzut pulpitu (też `#serve=`, `#join=`, `#fault=`, `#start=`, `#via=`, `#kz=`).
  - Selftest uruchamiaj na stałej kopii testowej: `--station app/LcsWidzew/Testy/station_test.json` (kopia układu z 2026-10-15; `Data/kopie/` rotuje się – trzyma 30 ostatnich zapisów).
  - Scenariusz selftestu (`SelfTest.Run`) nie zna nazw stacji: z tablicy przebiegów bierze po jednym przebiegu wyjazdowym na każdy szlak (z Wbl i Pzk), po jednym wjeździe z każdego semafora wjazdowego i jeden manewr. Każdy przebieg czeka na sygnał zezwalający, potem jest zwalniany (ZDP/ZDM). Do tego kontrola zależności: drugi przebieg z zajętego sygnalizatora musi zostać odrzucony. Wynik w linii `WYNIK:`, braki w liniach `NIE NASTAWIONO` z przyczyną.
  - Na kopii testowej oczekiwane: **`nastawione 25 z 26`, `kontrola zależności 4 z 4`**. Jedyny brak – 2A: odstęp 2A-1 zajęty przez pociąg z rozkładu (prawidłowe odrzucenie Wbl).

Model danych `station.json`:
- `nodes`, `edges` (`sec` = odcinek kontroli niezajętości `POST:nazwa`, `track`, `len` [m], `vmax`), `switches`, `signals` (wjazdowy, wyjazdowy z `shunt`, tm, to, sp, pk, odstepowy);
- `blocks` (szlaki: `type` sbl/sbl1/pol/pol1/tel, `ends` z `label`), `boundaries`, `farEnds`, `trackEnds`, `bumpers`, `ports`;
- `pages` (obrazy), `posts`, `labels`, `platforms`, `crossings`, `routeRules`;
- `sections` (rodzaj kontroli niezajętości `lo` licznik osi / `ot` obwód torowy), `detection` (domyślny);
- `display` – zobrazowanie (patrz Etap 1): `nodes`, `edges` oraz własne `labels`, `platforms`, `crossings` pulpitu (te w korzeniu należą do planu schematycznego);
- zwrotnice: `kind` (typ rozjazdu, np. „Rz 1:9 R190”), `radius`, `ratio`, `vbok` (puste – z typu, tabela `SwitchDef.Types`), `vprosto`, `manual`;
- tory: `radius` (łuk [m]), `cant` (przechyłka [mm]), `nasyp` (wysokość toru nad terenem [m], ujemna – przekop) – na razie tylko dane, bez wpływu na prędkość;
- tory (Ig-10): `cat` – `gz` główny zasadniczy / `gd` główny dodatkowy / brak = boczny (grubość osi na planie), `elec: true` – zelektryfikowany (niebieski); wypełnia właściciel (panel toru albo panel grupy po zaznaczeniu toru);
- sygnalizator: `mount` – konstrukcja na planie (maszt / `wysiegnik` / `nad` / `bramka` + `gantry` / `karzel`), bramki w `gantries`; `lamps` – liczba komór na planie Ig-10 (brak = domyślna: wjazdowy 5, wyjazdowy 4 / 3 bez Ms2, odstępowy 3, tarcze 1);
- sygnalizator `pk`: `routes` P/M/PM.

**Pułapki techniczne:**
- Edycja plików przez heredoc w bash psuje `\n` i ukośniki w literałach C# – zapisuj skrypt łatki (Python) narzędziem Write i uruchamiaj `python -I plik.py`; w C# zamiast `"\n"` używaj `{(char)10}`/`Environment.NewLine`.
- `Station` dla pulpitu to `L.View` (zobrazowanie); komputer zależności zawsze liczy z `L` (układ). `Simulator` implementuje `IPulpitSource.L` jawnie jako `L.View`.
- Węzeł ukryty na zobrazowaniu ma współrzędne `Station.Hidden` (-1e7).

## 5. Etapy

### Etap 1 – Rozdzielenie układu torowego od zobrazowania (ZROBIONE – podstawa, 2026-10-15)
**Stan:**
- Sekcja `display` w `station.json` (`nodes`: id → [x,y], `edges`: id → łamana).
- `Station.Parse` buduje `Station.Display` (geometria pulpitu, ta sama topologia i identyfikatory; zwrotnice +/− kopiowane z układu). `Station.View` – geometria dla pulpitu.
- Element bez zobrazowania leży w `Station.Hidden` (poza obrazami) i nie jest rysowany.
- **Terminologia właściciela:** zobrazowanie = obrazy pulpitu (dotychczasowy rysunek). Układ torowy = **plan schematyczny całego węzła** (jak plany Semaforka, `Zrodla/Semaforek_*.png`) – jeden arkusz, białe tło, cienkie tory (`PanelView.PlanView`, `DrawPlan`).
- Edytor otwiera się na zobrazowaniu. Przycisk „Tryb: ZOBRAZOWANIE / PLAN SCHEMATYCZNY” (`UI/EditorWindow.Display.cs`). W trybie zobrazowania:
  - przesuwanie węzłów, grup, punktów załamania, napisów i peronów;
  - usuwanie z obrazu;
  - lista nieumieszczonych torów z przyciskiem „Umieść”.
- Operacje układu `SplitEdge`, `MergeNode` i `NormalizeTurnout` przenoszą zmianę na obraz. Nowe tory nie pojawiają się same.
- Pierwsze wejście w tryb zobrazowania robi jednorazową konwersję obecnego rysunku.

- Plan schematyczny: przycisk „Rozjazdy jako skosy” (`StationDoc.PlanSkew/PlanSkewAll`) – łączniki jako proste skosy jak w terenie (pochylenie dx = ratio·dy), zmienia tylko rysunek układu; narzędzie Rozjazd na planie od razu robi skos. Typ rozjazdu (promień, skos) i łuki (promień, przechyłka) widoczne na planie.

- Narzędzia „✂ Podziel tor” / „⊕ Scal tory” w obu trybach (`EditorWindow.TrackOpDown`, `StationDoc.SplitEdgeAtFraction`, `MergeNode`). Przeciąganie pustego miejsca w zobrazowaniu – przesuwanie widoku; Shift+przeciąganie – zaznaczanie prostokątem.

**ZROBIONE (2026-10-15):** okno **„Plan stacji” na żywo** (`UI/PlanStationWindow.cs`, przycisk „Plan stacji”; test `--screenshot … "#plan=post:LW"` / `"#plan=train:NUM"`). Do rozwinięcia: S5/S4 na semaforach (dziś zielone/czerwone/białe), typ pojazdu z rozkładu (dziś kategoria lub jednostka), plan na stanowiskach sieciowych. Pierwotny opis: w symulatorze (wzór: zrzut z planem stacji z pociągami jako szare prostokąty z numerem i typem pojazdu, semafory jako głowice z kolorami świateł, „śledzenie składu”, wybór nastawni). Rysuje plan schematyczny (`L`, nie zobrazowanie) ze stanem terenu.

**ZROBIONE (2026-10-15): plan schematyczny wg Ig-10** (polecenie właściciela: teren zgodny z Ig-10). Wspólny moduł `UI/Ig10.cs` (znaki umowne zał. 8, wymiary w mm arkusza, `u` = jednostki na 1 mm) – używa go `PanelView.DrawPlan` (plan w edytorze, u = 4) i `PlanStationWindow` (na żywo, u = min(4; 3,5 px)). Symbole: osie torów (główne 0,5 / boczne 0,35 mm, zelektryfikowane niebieskie, rozebrane przerywane), numer toru w przerwie osi, rozjazd (kreska początku, numer, klin czarny – zasadniczo na wprost, jasny z kreską – na zwrot, kreskowany – nieuzależniony `manual`), semafor / tarcza świetlna (słupek po prawej w kierunku jazdy, podstawa ⊢, komory ⊘, tarcza manewrowa ⊗), kozioł „]”, wykolejnica (belka + grot 20°, opis), licznik osi „●●” / złącze izolowane „⊤” na granicach odcinków (wg `lo`/`ot`), peron (biały, obrys), przejazd (krawędzie drogi przerywane, km). W „Plan stacji” komory świecą wg sygnału, rozjazd w trakcie przekładania pomarańczowy, nieczynne odgałęzienie szare z przerwą. Pulpit bez zmian (Ie-104). Zrzuty: `zrzuty/Plan_schematyczny_Ig10.png`, `Plan_Ig10_tor_glowny_elektryfikacja.png`, `Plan_stacji_Ig10.png`.
Nie ma jeszcze (brak danych w układzie): kilometraż i oś stacji, ukresy W17, wskaźniki, nastawnie i budynki, sieć trakcyjna (odłączniki, izolatory), przebiegi pociągowe, granice okręgów nastawczych, rozjazdy krzyżowe jako osobny znak. PK nie ma znaku w Ig-10 – na planie się go nie rysuje. Powtarzacz (sp) rysowany jak tarcza ostrzegawcza świetlna – Ig-10 nie ma osobnego znaku (do potwierdzenia).

**Ocena właściciela (2026-10-15): plan „wygląda jeszcze gorzej”** – najgorsze: pionowe łączniki zamiast skosów, plan nie w skali, bałagan opisów. Kierunek: **podkład** (plan Semaforka / mapa) do odrysowania + ewentualnie **autoukład z poprawkami ręcznymi**.
**ZROBIONE (2026-10-15):**
- Podkład planu (`UI/EditorWindow.Plan.cs`, sekcja `planBg` w station.json: `file` – ścieżka względna do station.json albo bezwzględna, `x`, `y`, `w`, `rot`, `opacity`, `hidden`). Panel „Nic nie zaznaczono” w trybie planu: wczytaj / zmień plik, „Dopasuj dwoma punktami (do węzłów)” – przekształcenie podobieństwa (przesunięcie, skala, obrót), przezroczystość, położenie, usuń. Rysowany tylko w edytorze (`DrawBack`), pod siatką. Test: `--editshot p.png bg obraz.png out.json`. Zrzut: `zrzuty/Plan_podklad_Semaforek.png`.
- Warstwy planu (Ig-10 § 4 ust. 4, `PlanLayers.Current`, wspólne z „Plan stacji” – przycisk „Warstwy ▾”): sygnalizatory, numery, liczniki osi (domyślnie wył.), perony, przejazdy, kozły/wykolejnice, napisy, łuki (domyślnie wył.).
- Cieńszy styl: u = 2,5 (było 4) w edytorze, ≤ 2,5 w „Plan stacji”.
- Pułapka: fuzz klika przyciski panelu – okna dialogowe (wybór pliku) muszą mieć `if (FuzzMode || NoPrompt) return;`, inaczej test wisi.

**ZROBIONE (2026-10-15): autoukład planu w skali** – przycisk „Autoukład w skali” (tryb planu), `Model/StationDoc.AutoLayout.cs` (`AutoLayoutPlan`), PROPOZYCJA do ręcznych poprawek, jedno Ctrl+Z cofa całość, przedtem `EnsureDisplay` (pulpit bez zmian).
- X: skala podłużna 1:2000 (`PlanUnitsPerM` = 1,25 j./m). Najdłuższa ścieżka (Bellman–Ford) po torach skierowanych wg obecnego planu (każdy tor ≥ len), potem wygładzanie do `len` z blokadą: tor nie krótszy niż połowa długości i **nigdy odwrócony** – komputer zależności liczy kierunek jazdy z położenia końców toru (pierwsza wersja odwracała 17 torów i przebiegi spadły 979 → 245). Szlaki i łączniki obrazów skrócone do 250 m (Ig-10 § 1 ust. 8). Ślepe końce torów też wygładzane (inaczej zostawały na x = 0 i rozciągały tory).
- Y: tory poziome łączone w „pasma”, pasy **osobno dla każdego posterunku** co 23,75 j. (4,75 m w 1:500); posterunki wyrównane po torze, który je łączy. Węzły łączników – na prostym skosie między końcami; tory rysowane jako odcinki proste.
- Napisy, perony, przejazdy planu – przesunięte za najbliższym węzłem (X w nowej skali, Y przez odwzorowanie pasów posterunku).
- Raport: liczba pasów, szerokość planu [m], największa odchyłka długości, liczba odwróconych torów (musi być 0).
- Test: `--editshot p.png autolayout out.json` – topologia/długości/odcinki/zobrazowanie bez zmian, przebiegi 979 → 979, 0 torów pionowych, Ctrl+Z przywraca. Selftest na wyniku: 25 z 26. Zrzuty: `zrzuty/Plan_autouklad_Widzew.png`, `Plan_autouklad_Fabryczna.png`.
- Na kopii testowej odchyłki długości: mediana 0 m, największa 142 m (LW: tor 1 – e131…e136), 118 z 351 torów > 30 m – **długości torów równoległych w danych są niespójne** (do sprawdzenia przez właściciela; autoukład pokazuje gdzie).

**W TOKU (2026-10-15): nowy projekt układu torowego z OSM** (polecenie właściciela: „zobrazowanie zostaw, nowy zrób tylko układ torowy, zrób generator i go przystosuj”; stacje ŁF, ŁN, ŁW, Marysin).
- Generator `tools/gen_uklad.py` + konfiguracja `tools/gen_uklad_lodz.json` (stacje, promienie obszarów, arkusze planu, oś odniesienia – dane, nie kod). Dane: `tools/osm/lodz_t1…t4.json` (Overpass, 2026-10-15, © OSM ODbL; serwery bywają przeciążone – zapytania w kawałkach z ponawianiem).
- Wynik: **`app/LcsWidzew/Data/projekty/uklad_osm.json`** (+ `uklad_osm_raport.txt`) – osobny plik, `Data/station.json` nietknięty. Otwieranie: `--station Data/projekty/uklad_osm.json`.
- Topologia z OSM: tory dzielone na rozjazdach / sygnalizatorach / kozłach / wykolejnicach / końcach, długości z geometrii, `track`, `line`, `vmax`, `elec` (electrified), `cat` (usage=main → gz), zwrotnice z numerami (stopień 4 → połówki ab/cd; skrzyżowania torów rozdzielone), sygnalizatory PL (`railway:signal:combined/main:function` entry → wjazdowy, exit / intermediate w stacji → wyjazdowy, block → pominięty; shunting → tm; distant → to; bez Ms2 w states → `shunt: false`), kozły → trackEnds, wykolejnice, perony (tylko przy torach, ≤ 25 m), przejazdy. Odcinki wstępne: granice na sygnalizatorach i zwrotnicach, nazwy z / JT / it / sz.
- Plan wg Ig-10 § 3: rzut na wygładzoną oś arkusza (s, d), X = s·1,25 (1:2000), Y = d·5 (1:500), poza 70 m od osi ściśnięte; szlak między stacjami arkusza skrócony do 250 m; arkusze obok siebie (osobne obrazy). **Prostowanie**: najdłuższy fragment toru przy jednym poziomie → stały pas (grupowanie poziomów od początku grupy, tolerancja 1,5 m – łańcuchowe łączenie zlewało tory 1/2/3/5), odgięcia i łączniki – proste skosy.
- Stan: 216 węzłów, 246 torów, 94 zwrotnice, 16 sygnalizatorów (tylko ŁF), 7 peronów, 5 przejazdów, 18 kozłów, 5 wykolejnic; symulator wczytuje (121 przebiegów ŁF). Zrzuty: `zrzuty/Generator_OSM_Widzew.png`, `Generator_OSM_Fabryczna.png`.
- Prostowanie (pytanie właściciela „czemu tak krzywo”): rzeczywista geometria w skali poprzecznej 1:500 pokazuje każde odchylenie (×4 względem podłużnej); drogi OSM nie są dzielone na odgięciach; oś to uśredniona krzywa. Obecnie: ciągi torów (od zwrotnicy do zwrotnicy, dalej prosto przez rozjazd); ciąg o odchyleniu < 6 m – w całości na jednym pasie (pasy z median co 1,5 m), skosy przy końcach; reszta – punkty przyciągane do pasów (1,6 m) + Douglas–Peucker (2 m) → proste skosy. **Odrzucone:** pasy wyłącznie z kolejności (warstwy grafu przedziałów) – tory przez całą stację wypychane w górę, puste przerwy i długe skosy. Zostaje: łagodnie zbiegające się tory (np. ŁF, zbieżność > 6 m) jako skosy – do ręcznej poprawki na podkładzie Semaforka.
- **ZMIANA SPOSOBU (2026-10-15, po krytyce właściciela):** geometrii planu NIE da się automatycznie wyprowadzić z mapy (OSM/ORM to położenie w terenie; plan schematyczny jest ręcznie zniekształcony). Wszystkie próby (prostowanie, pasy z kolejności, przyciąganie, prowadzenie po pikselach) psuły coś innego. Obecny sposób: **geometria z planu schematycznego (Semaforek), topologia i długości z OSM**:
  - plik kotwic `tools/kotwice_<POST>.json` (odczyt ręczny z siatki: `switch` – zwrotnice po numerach, `end` – końce torów po numerze toru, `signal` – semafory po oznaczeniu, `via` – punkty pośrednie łuków wg toru; współrzędne pikseli planu), w konfiguracji `anchors`;
  - tor między kolejnymi kotwicami – krzywa Hermite'a (Catmull-Rom) przez kotwice i `via`, styczne od sąsiednich kotwic na przedłużeniu toru; węzły pośrednie wg długości z OSM; końce otwarte (szlak) – poziomo; posterunki dalej na wschód przesunięte o przyrost szerokości (także połówki „cd” rozjazdów krzyżowych – inaczej tor się odwraca i budowa tablicy przebiegów się zapętla);
  - kontrola: nałożenie wyniku na plan Semaforka (skrypt w scratchpadzie – do przeniesienia do tools/), `zrzuty/Generator_Fabryczna_nalozenie_Semaforek.png`, `Generator_Fabryczna_z_planu.png`;
  - perony posterunków z planu – nie z OSM (do dodania z planu); `tools/raster_maska.py` (maska pikseli) – na razie nieużywana po rezygnacji z prowadzenia po pikselach.
  - **ŁF gotowa** (44 kotwice: 18 zwrotnic, 10 końców, 16 semaforów E/G; 121 przebiegów, selftest OK). **Do zrobienia tym samym sposobem:** Widzew (72 zwrotnice), Niciarniana, Marysin; potem sygnalizatory z planów (Widzew – brak w OSM).
- **Nowy układ + stare zobrazowanie** (2026-10-15): `tools/przenies_zobrazowanie.py stary.json nowy.json wynik.json` → `Data/projekty/uklad_osm_zobrazowanie.json`. Kotwice: zwrotnice po numerze (w posterunku), sygnalizatory po nazwie, końce torów po numerze toru (67). Brakujące sygnalizatory przeniesione ze starego układu (przekształcenie stary układ → nowy plan, wstawienie na najbliższy tor, podział toru): 81; odstępowe SBL pominięte (brak szlaków), 17 – za daleko od toru. Zobrazowanie: kotwice w miejscach ze starego pulpitu (148), łańcuchy między kotwicami – kształt starej drogi na pulpicie, węzły wg długości; bez odpowiednika – nieumieszczone (pierwsza wersja z TPS nowy plan → pulpit rozrzucała elementy – odrzucona). Wynik: 182 z 297 węzłów, 185 z 327 torów na pulpicie; przebiegi 1772, selftest OK. Braki: ŁF – stary układ ma inne numery zwrotnic (68–89 wobec 21–38) – głowica wschodnia pusta; szlaki (blokady) nie przeniesione; długie skosy tam, gdzie połączenia OSM różnią się od starego układu. Zrzuty: `zrzuty/Nowy_uklad_stary_pulpit_Widzew.png`, `…_Fabryczna.png`.
- **Wybór wersji przy starcie** (`UI/LayoutChooser.cs`): zwykłe uruchomienie bez `--station` pokazuje listę: ★ `Data/station.json` i pliki z `Data/projekty` (bez raportów); ostatni wybór w `Data/ostatni_uklad.txt`. Z `--station` i w trybach testowych – bez okna. Okna nie sprawdzono na zrzucie (tryby zrzutów podają `--station`).
- **Braki:** OSM nie ma semaforów ani tarcz na Widzewie, Niciarnianej i Marysinie (tylko wskaźniki bez oznaczeń) – źródło do ustalenia z właścicielem (propozycja: przeniesienie z obecnego `station.json` po numerach zwrotnic i torów). Zwrotnice bez numeru w OSM: 4 (LW). Szlaki (blokady) – do dodania w edytorze. Drobne załamania torów w miejscach podziału dróg OSM.

**ZROBIONE (2026-10-16): plan w skali Ig-10, rysowanie na podkładzie, zbiorcze zaznaczanie** (polecenie właściciela: autogeneracja nie wychodzi – rysowanie na podkładzie, podkład skalowany, plan w poprawnej skali, „ile px toru, tyle metrów” zamiast wpisywania długości).
- `Model/StationDoc.Scale.cs`: `planScale` { `along`, `across`, `auto` } w station.json (Ig-10 § 3: 1:2000/1:500, za zgodą 1:4000/1:1000); rysunek 2,5 j./mm arkusza → `Ux` (1:2000 → 1,25 j./m), `Uy` (1:500 → 5 j./m). `PlanLen` – długość łamanej w metrach (poziomo wg skali podłużnej, pionowo wg poprzecznej). Plik bez `planScale` – skala odczytana z rysunku (mediana rzutu / len), `auto` wyłączone: **kopia testowa i `Data/station.json` mają 1:4000/1:1000, projekty OSM 1:2000/1:500**. Nowy pusty obszar: 1:2000/1:500, auto.
- `auto` (panel „Długości torów z rysunku”): każda zmiana rysunku toru (przesunięcie węzła/grupy, rozstaw, podział, skos rozjazdu, nowy tor) liczy `len` z rysunku. Wyjątki: szlaki (skrót, Ig-10 § 1 ust. 8), łączniki obrazów, `lenFix` (wpisanie długości odcinka przy auto ustawia `lenFix`; checkbox „Długość z rysunku” w panelu toru). Bez auto długości są danymi – **naprawiony błąd**: przesunięcie węzła wpisywało dotąd do `len` długość w pikselach (plany nie są 1 j. = 1 m). Nowe tory zawsze dostają długość z rysunku w skali.
- Zmiana skali (`RescalePlan`) przeskalowuje rysunek planu, napisy, perony, przejazdy i podkład – długości bez zmian. Autoukład używa skali pliku.
- Podkład: rozciągnięcie pionowe `planBg.sy` (świat = O + diag(1, sy)·obrót·S·piksel); operacje dwoma kliknięciami: wyrównanie do poziomu, skala wzdłuż (odległość w m), skala w poprzek (rozstaw), przesunięcie; „Mapa: rozciągnij pionowo ×Uy/Ux”; dopasowanie dwoma punktami przy `sy` ≠ 1 – osobno poziomo i pionowo. Pola `rot`, `sy` liczbowe (wcześniej obrót zapisywał się jako tekst).
- Siatka „Ig-10” (10 m × 4,75 m), linijki w metrach na planie, metry w podpowiedzi, przy rysowaniu toru i w miarce.
- **Zbiorcze zaznaczanie**: „⬚ Zaznacz obszarem” (Q) – prostokąt bez Shift w planie i zobrazowaniu; Ctrl+A także w zobrazowaniu.
- **Zobrazowanie projektu OSM usunięte na polecenie właściciela** („narysuję od nowa”): `Data/projekty/uklad_osm_zobrazowanie.json` ma puste `display` (kopia przed usunięciem: `Data/projekty/kopie/uklad_osm_zobrazowanie_przed_usunieciem_zobrazowania_2026-10-16.json`). W edytorze przycisk „Usuń całe zobrazowanie (rysuję od nowa)…” (`StationDoc.ClearDisplay` – puste `display`, bez ponownej konwersji z układu). Narzędzie `tools/przenies_zobrazowanie.py` – odrzucone przez właściciela.
- Testy: `--editshot p.png skala out.json [podkład.png]` (21× OK), `--editshot p.png dclear out.json` (9× OK, na kopii testowej i projekcie OSM). Zrzuty: `zrzuty/Plan_skala_Ig10_panel.png`, `Plan_skala_podklad_test.png`.

**ZROBIONE (2026-10-16, cd.): autogen z OSM usunięty, podkład z internetu, ochrona boczna**
- **Autogen z OSM / ORM usunięty** na polecenie właściciela („wyjeb ten autogen”): `tools/gen_uklad.py`, `gen_uklad_lodz.json`, `kotwice_LF.json`, `osm2layout.py`, `przenies_zobrazowanie.py`, `raster_maska.py`, `schematize.py`, `build_station.py`, `lcs_panel.py`, `tools/osm`, `tools/maski`, projekty `Data/projekty/uklad_osm*` (są w historii git). **Nie przywracać i nie proponować ponownie** – stacje rysuje właściciel. Zostały importy rozkładów (`gtfs_import.py`, `pkpic_import.py`). Opisy autogenu wyżej w tym pliku są historyczne.
- **Repozytorium:** projekt jest w `symulator/SUK` (git, GitHub Obwarzanka/SUK). Zewnętrzny `symulator/app` jest niepełny – nie budować tam.
- **Nowy podkład** (`UI/EditorWindow.Podklad.cs`, przycisk w panelu planu): ortofotomapa – zdjęcia lotnicze z Geoportalu GUGiK (WMS `PZGIK/ORTO/StandardResolution`, EPSG:2180, osie N, E; przeliczenie WGS84 → PUWG 1992 – `GeoPodklad.Puwg92`, sprawdzone na zdjęciu stacji), mapa OpenStreetMap (kafelki, User-Agent, najwyżej 400 kafelków), zdjęcia satelitarne Esri World Imagery (kafelki), obraz z adresu URL (np. plan z Semaforka – „Kopiuj adres obrazu”), plik z dysku. Środek obszaru jako „szer., dł.” (np. z Google Maps), wymiary w metrach, rozdzielczość m/px. Pliki w `podklady/` obok station.json; w `planBg`: `geo` (źródło, środek, m/px), `attribution` (pokazywana w panelu – wymóg licencji OSM / Esri).
- Zdjęcie i mapa trafiają na plan **w skali podłużnej i bez rozciągania** (pierwsza wersja rozciągała ×4 wg skali poprzecznej – zdjęcie było nieczytelne, właściciel odrzucił). Rozciąganie tylko przyciskiem. Siatka edytora rysowana **pod** podkładem; krycie 0,85. Obrót: „1. Wyrównaj tor do poziomu”.
- Geoportal bywa niedostępny (2026-10-16 przez chwilę zrywał połączenia po 21 s) – komunikat podpowiada inne źródło (Esri). Test: `--editshot p.png podklad orto|osm|esri "lat, lon" szer wys out.json`. Zrzut: `zrzuty/Podklad_zdjecia_satelitarne.png`.
- **Ochrona boczna przebiegu** (poradnik UTK dla inspektorów, t. I, 1.5.8, Tabela 22: „rozjazdy utwierdzone w ochronie bocznej”, wykolejnice, „sygnalizatory utwierdzone w ochronie przebiegu”): `Simulator.FlankFor` – od każdej zwrotnicy przebiegu nieużywaną gałęzią do pierwszego elementu: wykolejnica (założona), sygnalizator zwrócony w stronę przebiegu („Stój”), zwrotnica osiągnięta od gałęzi (położenie w drugą gałąź); od ostrza – brak ochrony. Elementy w `RouteDef.Flank`. Przy nastawianiu zwrotnice ochronne przestawiają się i są utwierdzane (`SwState.Flank`, `DerState.Flank`, `SigState.Flank`), warunek sygnału zezwalającego – kontrola położenia; zwalnianie razem ze zwrotnicą przebiegu (odcinkami) i przy zwolnieniu całego przebiegu. Przeszkody: element ochrony utwierdzony w innym przebiegu / w ochronie innego przebiegu w drugim położeniu, odcinek zajęty, brak kontroli, NN0. Polecenia Plus/Minus, PlusBZ/MinBZ, wykolejnica i Sz na elemencie w ochronie – odrzucone. Podgląd komputera zależności: kolumna „Ochrona boczna”, zwrotnice z ochroną. Na pulpicie bez zmian (Ie-104 – do ustalenia z właścicielem, czy i jak pokazywać).
- Selftest: osobna linia `OCHRONA BOCZNA: przebiegi z ochroną 959 z 979; kontrola 4 z 4` (przestawienie i utwierdzenie zwrotnicy ochronnej, odrzucenie jej przestawienia, blokada przebiegu sprzecznego, zwolnienie). `WYNIK` bez zmian: 25 z 26, 4 z 4.
- Pominięte na polecenie właściciela: kreator „nowa stacja od zera” (właściciel buduje stacje sam).
- Dalsze braki komputera zależności wg poradnika (do ustalenia kolejności z właścicielem): zwrotnice w drodze ochronnej (dziś droga ochronna = jeden odcinek), ochrona boczna dalej niż pierwszy element (dojście od ostrza), blokada stacyjna (1.5.9), rejestrator zdarzeń z odtwarzaniem (Tabela 21 pkt 3), przejazdy (1.10), awaryjny terminal (Tabela 25).

**ZROBIONE (2026-10-16, cd.): pusty szablon i podkład w skali rzeczywistej** (polecenie właściciela: „daj mi pusty szablon i dopasuj podkład do skali realnej na plan schematyczny”)
- `Data/projekty/pusty_szablon.json` – pusty obszar (posterunek ST, jeden obraz), plan 1:2000 w obu kierunkach, długości z rysunku, puste zobrazowanie; widoczny na liście wersji przy starcie. Pulpit i selftest go wczytują (0 przebiegów). Zapisuj stację pod własną nazwą („Zapisz jako…”), żeby szablon został pusty.
- Skale planu: oprócz Ig-10 (1:2000/1:500, 1:4000/1:1000) **skala rzeczywista** 1:2000/1:2000 i 1:1000/1:1000 (`StationDoc.RealScale`) – zdjęcie lotnicze / mapa leży na planie bez zniekształceń, a tory narysowane po nim mają prawdziwe długości (także skosy). Zmiana skali z listy przeskalowuje rysunek i podkład.
- Podkład geograficzny (orto / OSM / Esri) zawsze dokładnie w skali planu: szerokość wg skali podłużnej, wysokość wg poprzecznej (`sy` = Uy/Ux). Przy pustym planie w skali Ig-10 edytor przełącza go na rzeczywistą (jedno Ctrl+Z). W skali Ig-10 zdjęcie jest rozciągnięte – komunikat to mówi. `StationDoc.New` (Nowy obszar…) – też skala rzeczywista, auto, puste zobrazowanie. Zrzut: `zrzuty/Pusty_szablon_podklad_skala_rzeczywista.png`.

**ZROBIONE (2026-10-16, cd.): kilka podkładów naraz, kafelki ortofotomapy z mapki** (polecenie właściciela: „2 podkłady na raz” i „wybrać z mapki konkretne kafelki ortofotomapy dla danej linii, niech od razu się skaluje”)
- **Lista podkładów** `planBgs` (od dołu; dawny pojedynczy `planBg` przenoszony przy wczytaniu – `StationDoc.MigrateBg`). W panelu planu: lista z wyborem aktywnego (obrót, skala, przesunięcie, usuwanie działają na aktywnym), widoczność, kolejność ↑↓, krycie każdego osobno. Np. plan z Semaforka na wierzchu z małym kryciem nad ortofotomapą.
- **Okno mapki** (`UI/TilePicker.cs`, przycisk „Kafelki ortofotomapy z mapki…”): mapa OSM z nałożonymi liniami kolejowymi OpenRailwayMap, siatka kafelków Web Mercator z15 (ok. 750 m), LPM – zaznacz / „maluj” kafelki wzdłuż linii, PPM / środkowy – przesuwanie, kółko – przybliżenie, „Idź do” współrzędnych; zielone – kafelki już na planie. Źródło: ortofotomapa Geoportalu (WMS w EPSG:3857 dokładnie w granicach kafelka – bez szwów) albo zdjęcia Esri; 1024 px (ok. 0,7 m/px) lub 2048 px na kafelek.
- **Mozaika** (podkład `type: "tiles"`: `src`, `z`, `n`, `tx0`, `ty0`, `tiles`, `dir`; pliki `podklady/<src>_z15_<n>/<x>_<y>.jpg`, pobrane – pomijane): od razu w skali planu (wzdłuż – podłużna, w poprzek – poprzeczna; pusty plan → skala rzeczywista). Kolejne kafelki tego samego źródła dokładają się do tej samej mozaiki we właściwym miejscu (z zachowaniem obrotu – raz wyrównany tor zostaje poziomo); mozaika z innego źródła / rozdzielczości jest wyrównana geograficznie do istniejącej (wspólna siatka Web Mercator: p_A = k·p_B + c).
- Naprawione: `Num` (odczyt liczb z pól podkładu) nie czytał liczb całkowitych zapisanych w pamięci (np. `rot = 3`, numer kafelka) – zwracał wartość domyślną.
- Test: `--editshot p.png kafelki orto|esri "lat, lon" out.json [mapka.png]` (skala, dołożenie kafelków – punkt stacji w miejscu Δ < 0,01 j., druga mozaika Esri 2048 px wyrównana Δ < 0,01 j., zapis dwóch podkładów, zrzut okna mapki). Zrzuty: `zrzuty/Mapka_kafelki_ortofotomapy.png`, `zrzuty/Dwa_podklady_mozaiki.png`.
- Pamięć: kafelek 1024 px ≈ 4 MB po wczytaniu – kilkadziesiąt kafelków bez problemu; 2048 px – czterokrotnie więcej.

**ZROBIONE (2026-10-16, cd.): bramki sygnałowe i konstrukcje semaforów na planie** (polecenie właściciela: „czy mogę bramkę zrobić i na niej semafor powiesić” – „logika bez zmian, po prostu zmiany w UT i edytorze”)
- Znaki wg wykazu znaków planów (poradnik UTK t. I, s. 671, poz. 9.8): 9.8.3 maszt prosty (dotychczasowy), 9.8.7 maszt z wysięgnikiem (załamanie masztu w stronę toru), 9.8.8 na bramce lub wysięgniku (kreska prostopadła przez tor, poprzeczka po drugiej stronie, komory przy osi toru), 9.8.5 karzełkowy (komory przy podstawie). `Ig10.Signal` rysuje wg `SignalDef.Mount`, `Ig10.Gantry` – belka bramki z poprzeczkami podpór i oznaczeniem; także w oknie „Plan stacji”.
- Model: `gantries` { klucz `POST:oznaczenie` → `label`, `post`, `x`, `y0`, `y1` } (`Station.Gantries`, `GantryDef`), sygnalizator: `mount` ("" / `wysiegnik` / `nad` / `bramka` / `karzel`) i `gantry`. `Model/StationDoc.Gantry.cs`: `AddGantry`, `AttachNear` (sygnalizatory w zasięgu belki, w poziomie ≤ max(12 j., 10 px)), `AttachToGantry` (węzeł dosuwa się do bramki tylko wtedy, gdy nie przeskoczy sąsiedniego węzła – kierunek torów i przebiegi bez zmian), `MoveGantry`, `DeleteGantry` (sygnalizatory wracają na maszt).
- Edytor (`UI/EditorWindow.Gantry.cs`): narzędzie „⊤ Bramka” (U) – przeciągnięcie w pionie w poprzek torów, semafory w pobliżu przypinają się same; panel bramki (oznaczenie, końce belki, położenie X przesuwa przypięte, lista, „Przypnij sygnalizatory w pobliżu”, Del); panel semafora – „Konstrukcja na planie” i wybór bramki. Zobrazowanie (pulpit, Ie-104) i komputer zależności – bez zmian.
- Naprawiony błąd edytora: przycisk „Odcinek zwrotnicowy na łącznik” pokazywał się przy każdym zaznaczonym obiekcie (brak nawiasów przy `if (selKind == "sw")`).
- Test: `--editshot p.png bramka out.json` (bramka narzędziem przy kolumnie 11 sygnalizatorów, tablica przebiegów z ochroną boczną identyczna przed / po przypięciu, przesunięciu i konstrukcjach, Del, Ctrl+Z, zapis). Zrzut: `zrzuty/Plan_bramka_sygnalowa.png`.

**ZROBIONE (2026-10-16, cd.): niezawodne pobieranie podkładów** (zgłoszenie właściciela: „podkłady mi się nie ładują jak chcę je pobrać”)
- Przyczyna: Geoportal (load balancer F5) losowo zwraca **404** – ok. 1/3 zapytań, za każdym razem inne kafelki; przy 4 zapytaniach naraz ponad połowa. Dotychczasowy kod przerywał całe pobieranie na pierwszym błędzie i nic nie trafiało na plan (u właściciela: 44 kafelki ortofotomapy pobrane, mozaika nie powstała; Esri zadziałało).
- Teraz (`GeoPodklad.Mosaic`, `Tile`, `GetFresh`, `Retry`): każde zapytanie do Geoportalu nowym połączeniem (`Connection: close`) i bez ciasteczek (HttpClient bez `UseCookies` – Geoportal przypina sesję do jednego serwera), do 6 prób na kafelek, Geoportal po 2 naraz, Esri po 4; nieudany kafelek nie przerywa reszty – pobrane trafiają na plan, nieudane w komunikacie (z przyczyną) i w `Data/bledy.log`, przy ponownym zaznaczeniu dociągają się tylko brakujące. Zapis kafelków przez plik `.tmp` (przerwane pobieranie nie zostawia uszkodzonego pliku). Kafelek spoza mapy – błąd od razu. To samo zabezpieczenie (próby, nowe połączenie) przy pobieraniu obszaru ortofotomapy, kafelków OSM / Esri i obrazu z URL.
- Test `kafelki`: 16 kafelków Geoportalu wzdłuż linii – 16 / 16 w 15 s; nieudany kafelek nie przerywa pobierania; brak plików `.tmp`.

**ZROBIONE (2026-10-16, cd.): przesuwanie widoku przy budowie** (zgłoszenie właściciela: „podczas budowy nie mogę przesuwać mapy”)
- `PanelView`: środkowy przycisk myszy albo Spacja + LPM przesuwa widok w każdym narzędziu edytora (wcześniej tylko przeciąganie pustego miejsca w „Wskaż”; narzędzie Tor stawiało punkty, „Zaznacz obszarem” rysował prostokąt). W edytorze zakres przybliżenia 0,004–60 (było 0,2–6 – długa linia w skali rzeczywistej nie mieściła się w oknie). Pulpit bez zmian.
- Nie sprawdzono prawdziwą myszą (testy symulują tylko zdarzenia edytora); fuzz i dfuzz po 0 błędów.
- Skala do rysowania po ortofotomapie: **1:2000 w obu kierunkach (skala rzeczywista) z „Długościami torów z rysunku”** – długości (także skosów) rzeczywiste; dokładność zależy od przyciągania do siatki (siatka 8 j. = 6,4 m przy 1:2000; do rysowania po zdjęciu – siatka 1 albo Shift do poziomu).

**ZROBIONE (2026-10-16, cd.): jakość podkładów, przyciąganie, punkty załamania i łuki na planie** (zgłoszenie właściciela: „jakość podkładów słaba”, „przyciąganie do siatki okropne przy budowie na podkładach”, łuki)
- **Jakość:** kafelki ortofotomapy z usługi Geoportalu **HighResolution** (wcześniej StandardResolution – rozmyta przy powiększeniu; HighResolution pokazuje szyny i podkłady), gdy w danym miejscu nie ma obrazu – StandardResolution. Obszar (PUWG 1992) też z HighResolution. W mapce rozdzielczość 1024 / 2048 (domyślnie) / **4096 px na kafelek (≈ 0,18 m/px)**; Esri – najwyżej 2048. Rysowanie mozaiki: tylko widoczne kafelki, dekodowanie zmniejszone do wielkości na ekranie (poziomy 128…n px, w tle, tymczasem mniejszy poziom), pamięć do 700 MB (najdawniej używane zwalniane), wygładzanie przy skalowaniu (`BitmapScalingMode.HighQuality`). Napisy i linijki edytora stałej wielkości także przy dużym przybliżeniu (wcześniej min. 5 j. – rosły na ekranie). Pobrane wcześniej kafelki 1024 px ze StandardResolution zostają – lepsze pobiera się w mapce jako nową mozaikę (wyrówna się sama).
- **Przyciąganie do siatki:** przełącznik „przyciąganie” obok siatki, **Alt – chwilowo odwrotnie**; plik z podkładami otwiera się z wyłączonym przyciąganiem. Wyłączone – punkty i przesunięcia dokładnie tam, gdzie kliknięto (0,01 j.).
- **Punkty załamania na planie** (`StationDoc.InsertEdgePt / MoveEdgePt / RemoveEdgePt`): dwuklik na torze dodaje punkt dokładnie na torze (rzut), przeciąganie, PPM na punkcie usuwa; uchwyty przy zaznaczonym torze / węźle i torze pod kursorem. **Zaznaczenie całego toru – teraz Alt+dwuklik** (było: dwuklik). Długość z rysunku przeliczana.
- **Narzędzie „◠ Łuk” (O)** (`Model/StationDoc.Curves.cs`, `UI/EditorWindow.Curves.cs`): początek – punkt na łuku – koniec (okrąg przez 3 punkty w metrach), Ctrl+klik zamiast punktu na łuku – łuk styczny do toru kończącego się w początku; kolejne łuki od końca (ciąg), podgląd z promieniem i długością. Tor jako łamana po okręgu (cięciwa ≤ 4 m), `radius` [m] zapisany, długość po łuku. Odcinek dziedziczony jak w narzędziu Tor (`AddChainEdge` – wspólne z Tor).
- **Łuk o zadanym promieniu w narożniku** (panel węzła z dwoma torami: „Łuk w narożniku – promień R [m]…”, `FilletNode`): styczne T = R·tg(δ/2), węzeł przechodzi na środek łuku, promień w obu torach; za duży promień / zmiana kierunku toru – odrzucone z komunikatem.
- Testy: `--editshot p.png luki out.json` (punkt załamania: długość bez zmian / 404,5 m / powrót do 400 m; łuk przez 3 punkty R = 300 m, długość 314,2 m; łuk styczny R = 299 m; narożnik R = 100 m – suma długości zgodna; za duży promień odrzucony; przyciąganie wł. / wył.; zapis), `--editshot p.png jakosc "lat, lon" out.json` (2 kafelki 4096 px, 0,185 m/px). Zrzuty: `zrzuty/Ortofotomapa_HighResolution_018m.png`, `zrzuty/Plan_luki.png`.

**ZROBIONE (2026-10-16, cd.): łączenie torów przeciągnięciem, łuk przez przeciągnięcie toru** (zgłoszenie właściciela: „jak w edytorze połączyć tory”, „dalej źle robić łuki”)
- **Łączenie:** węzeł (np. koniec toru) przeciągnięty w „Wskaż” i upuszczony na inny węzeł łączy się z nim (`StationDoc.WeldNodes`); w trakcie przeciągania węzeł docelowy podświetlony na zielono. Warunki: razem najwyżej 3 tory (przy 3 – komunikat „wstaw zwrotnicę”), węzły nie połączone już torem, w przeciąganym węźle brak zwrotnicy / sygnalizatora / kozła / wykolejnicy / granicy szlaku; inaczej tylko przesunięcie i komunikat z przyczyną. Zobrazowanie: koniec toru w węźle docelowym (gdy jest na obrazie), inaczej tor zdjęty z obrazu. Jedno Ctrl+Z cofa przesunięcie z połączeniem. Wcześniej upuszczone węzły tylko leżały na sobie – tory nie były połączone.
- **Łuk przez przeciągnięcie toru:** narzędzie Łuk – wciśnij na torze i przeciągnij (samo kliknięcie – początek nowego łuku), w „Wskaż” – Ctrl+przeciągnij tor: tor wygina się w łuk przez kursor (końce w miejscu), podgląd z R i długością, promień zapisany (`BendTrack`).
- Test `luki`: połączenie przeciągnięciem (260,2 m), odrzucenie 4. toru w węźle, wygięcie toru 400 m o 40 m → R = 520 m.
- **Do zebrania od właściciela:** lista dalszych niewygód edytora („wiele innych kretyńskich rzeczy”) – poprawiać po kolei.

**ZROBIONE (2026-10-16, cd.): przyciąganie do torów, odgałęzienie przeciągnięciem, rysowanie łukami** (zgłoszenie właściciela: „nie ma przyciągania do istniejącego toru”, „budowanie łuków ciężkie”)
- **Zasięg łapania** węzłów i torów – stałe ok. 10 px na ekranie (`R = 10 / skala`); wcześniej min. 6 j. ≈ 4,8 m przy 1:2000 – przy rozstawie 4,75 m łapało sąsiedni tor.
- **Znacznik przyciągnięcia** w narzędziach rysujących (Tor, Łuk, Rozjazd, Semafor, Kozioł, Wykolejnica): zielony kwadrat – węzeł, pomarańczowy krzyżyk – punkt na torze (rzut), szare kółko – siatka / miejsce kliknięcia; podgląd toru prowadzi do tego punktu (`SnapTarget`). Podgląd cienką linią (było 3 j. – grube przy przybliżeniu).
- **Odgałęzienie przeciągnięciem:** koniec toru upuszczony na środek innego toru (pomarańczowy krzyżyk w trakcie) – tor dzielony w tym miejscu i połączony (węzeł z 3 torami – komunikat „wstaw zwrotnicę”).
- **Tor „łukiem” (przycisk „◠ łukiem”, klawisz C):** w narzędziu Tor każdy kolejny odcinek to łuk styczny do poprzedniego (pierwszy prosty albo styczny do toru, z którego się wychodzi) – klikanie wzdłuż toru na zdjęciu daje gładką linię; podgląd łuku z promieniem. C – powrót do prostych.
- Test `luki` – 25 kontroli (m.in. przyciąganie do toru, zasięg < pół rozstawu przy przybliżeniu 6, odgałęzienie, łuki styczne R = 400 / 398 m).
- Uwaga dla testów: tryby `--editshot` czytają prawdziwy stan klawiatury (Ctrl / Alt / Shift) – pisanie w trakcie testu może zmienić wynik (zdarzyło się raz).

**ZROBIONE (2026-10-16, cd.): parametry pomocnicze przy budowie – międzytorze, tor równoległy, nasyp** (prośba właściciela: „międzytorze, wysokość toru na nasypie, rysowanie toru spiętego z torem obok”)
- Pole **„Międzytorze [m]”** w pasku (domyślnie 4,75; 2–50).
- Narzędzie **„⫴ Równoległy” (E)**: najechanie na tor – podgląd toru obok w międzytorzu (strona kursora), klik tworzy tor wzdłuż całego ciągu (przez węzły bez zwrotnic, do zwrotnic / końców), także po łukach – promień R ± międzytorze (`StationDoc.OffsetPolyline`, `FitRadius`). Końce łączą się z istniejącym węzłem tylko w tym samym miejscu (≤ 0,5 m) i nie z końcami toru bazowego. Tor równoległy jest kopią w chwili tworzenia (nie podąża za późniejszymi zmianami toru bazowego).
- Przy rysowaniu toru – **odległość do toru obok** w podpowiedzi przy kursorze („do toru obok 4,73 m”, do 30 m).
- **Wysokość toru na nasypie** – pole toru `nasyp` [m] (ujemna – przekop; dane pomocnicze, bez wpływu na jazdę): podpowiedź toru, warstwa planu „łuki (R, h), nasypy / przekopy”.
- Punkty torów zapisywane z dokładnością 0,01 j. (ok. 1 cm przy 1:2000; było 0,1 j. ≈ 8 cm).
- Test `luki` – 30 kontroli (tor równoległy 4,75 m do prostego i R = 305 m do łuku R = 300, odległość do toru obok, nasyp). Zrzut: `zrzuty/Plan_tor_rownolegly_luki.png`.

**ZROBIONE (2026-10-16, cd.): miarka łamana, prowadnice z linijek, ostrzejszy podkład, profil wysokościowy z NMT** (prośba właściciela: „narzędzie pomiarowe do linijek, lepsza jakość podkładu, ściąganie profilu linii ze źródła internetowego”; po pytaniach: miarka łamana + prowadnice + kąt i promień + pomiar wzdłuż toru; „ostrzejsze wyświetlanie”; profil wysokościowy). **Właściciel: „skali mi nie zmieniaj”** – żadne z tych narzędzi nie zmienia `planScale` ani rysunku (testy to sprawdzają).
- **Miarka (M)** (`UI/EditorWindow.Measure.cs`): łamana – klik kolejnych punktów (przyciąganie jak w narzędziu Tor: węzeł / punkt na torze / siatka), dwuklik / Enter / PPM – koniec (pomiar zostaje, do 8 naraz), Backspace – cofnij punkt, Esc – wyczyść, przeciągnięcie – pomiar 2 punktów jak dawniej. Długość każdego odcinka i suma [m], kąt odcinka (w metrach, w górę dodatni) z pochyleniem 1:n przy małych kątach (np. 1:9 – rozjazd), **droga po torach** między punktami na torach także w środku toru (Dijkstra od położenia na torze wg `len`; dawniej tylko węzeł–węzeł), **promień okręgu przez 3 ostatnie punkty** (okrąg kropkowany). Uwaga: droga po torach nie sprawdza jazdy przez zwrotnicę „w ostrym kącie” (jak dotychczasowa).
- **Prowadnice** (`guides` w station.json: `h` – y, `v` – x; tylko edytor, tryb planu): przeciągnięcie z linijki u góry – pozioma, z lewej – pionowa; w trakcie łapią się wysokości / osi widocznego węzła (≈ 10 px), siatki (przy przyciąganiu) albo dokładnie; podpowiedź: położenie i odległość do sąsiedniej. Odstępy sąsiednich prowadnic w metrach przy linijkach. `Snap` przyciąga punkty do prowadnic (≈ 10 px, także bez siatki; Alt – nie). W „Wskaż” prowadnicę (poza węzłem / torem) można przeciągnąć; upuszczona na linijkę albo PPM – usunięta; „Usuń wszystkie prowadnice” w panelu planu. Ctrl+Z / Ctrl+Y nie zmienia prowadnic (`KeepGuides`).
- **Ostrzejszy podkład** (`UI/EditorWindow.Detail.cs`): u właściciela mozaiki 1024 px (≈ 0,74 m/px) – rozmyte przy przybliżeniu. Teraz kafelek większy na ekranie niż jego rozdzielczość jest w tle dociągany w 2048 / 4096 px (ortofotomapa HighResolution, Esri do 2048) do `<źródło>_z15_<n>` obok mozaiki i rysowany najlepszy dostępny (ten sam kafelek – położenie i skala bez zmian). Tylko oglądane kafelki, kolejka do 40 (najnowsze najpierw), po 2 naraz, nieudane – do `bledy.log`, bez ponawiania w tej sesji. Przełącznik „Ostrzejszy podkład przy przybliżeniu” w panelu planu (domyślnie wł.; w testach wył.). Poziom szczegółowości liczony w pikselach fizycznych (skalowanie Windows 125 / 150 %).
- **Profil wysokościowy** (`UI/EditorWindow.Profile.cs`, `Model/Profil.cs`, `GeoPodklad.Nmt`): trasa = ciąg toru do zwrotnic / końców (przycisk w panelu toru) albo pomiar miarką (H). Punkty co ≥ 2 m (najwyżej ok. 1500), plan → piksel podkładu (odwrotność `BgToWorld`) → PUWG 1992: mozaika kafelków (Web Mercator), obszar ortofotomapy (`geo.e0/n0/mpp`), obszar OSM / Esri (`geo.zoom`). Wysokości: usługa **NMT GUGiK** `services.gugik.gov.pl/nmt/?request=GetHByPointList&list=N E,…` (x = N, y = E; paczki po 200 – dłuższy adres daje 414; próby jak przy podkładach). Okno: wykres teren / profil (przewyższenie automatyczne, kursor z wysokością), pasek pochyleń jak na profilu linii (‰ i długość), tabela, kopiowanie do Excela. Uproszczenie: Douglas–Peucker (tolerancja, dom. 0,3 m) + scalanie odcinków krótszych niż „najkrótszy odcinek” (dom. 50 m). „Zapisz pochylenia w torach” (jedno Ctrl+Z): `poch` [‰ od A do B], `hA`, `hB` [m n.p.m.], `prof` – załomy wewnątrz toru [[m od A, h]]; model: `Edge.Grad`; warstwa planu „łuki (R, h), nasypy / przekopy, pochylenia ‰”, podpowiedź toru. To teren (ALS), nie główka szyny – na wiaduktach dolina; do oceny przez właściciela.
- Testy: `--editshot p.png pomiar out.json` (17 kontroli: łamana, kąt 90° i 1:9, R = 500 m, droga po torach od środka toru 604,1 m i przez węzeł 500 m, przeciąganie, Esc, prowadnice – z linijki, do węzła, przyciąganie, przesunięcie o 4,75 m, Ctrl+Z, zapis, usunięcie, skala bez zmian), `ostrosc "lat, lon" out.json` (1024 → 4096 px w ok. 10 s, podkład i skala bez zmian), `profil "lat, lon" out.json` (plan → PUWG Δ 0 m także po obrocie podkładu, NMT 601 punktów / 1200 m w 1–2 s, uproszczenie ≤ 0,3 m, odcinki ≥ 50 m, zapis w torach – także w torze odwróconym, wczytanie do modelu, skala bez zmian, zrzut okna). Zrzuty: `zrzuty/Miarka_lamana_prowadnice.png`, `zrzuty/Podklad_ostrzejszy_przy_przyblizeniu.png`, `zrzuty/Profil_wysokosciowy_NMT.png`, `zrzuty/Plan_pochylenia_po_profilu.png`.
- Nie sprawdzono prawdziwą myszą (testy wołają metody edytora); profil liczony na trasie testowej w poprzek osiedla, nie na prawdziwym torze. Decyzja właściciela (2026-10-16): automatyczne przełączanie pustego planu Ig-10 na skalę rzeczywistą przy wstawianiu podkładu **zostaje**.

**ZROBIONE (2026-10-16, cd.): rozdzielenie narzędzi planu (układ torowy) i zobrazowania; peron i rozjazd na torach skośnych** (zgłoszenie właściciela ze zrzutem: na ortofotomapie tory są skośne – „jak tu zbudować peron”, „rozjazdy jak na zobrazowaniu”, „rozdziel porządnie, zostaw tu i tu tylko odpowiednie funkcje”, „nie rób zbędnych zmian”; potem: „zbuduj sam prostą mijankę i zobacz”).
- **Paski narzędzi wg trybu** (`ApplyModeToolbars`, `planOnly` / `dispOnly`): na planie nie ma „Obraz:” ani „Nowy obraz…” (plan to zawsze cały węzeł); na zobrazowaniu nie ma pola odcinka, „jeden odcinek w ciągu”, „odcinek zwrotnicowy”, „międzytorze”, warstw odcinki / długości / prędkości ani przycisków układu (Nadaj identyfikatory, Kontrola niezajętości, Odcinki na sygnalizatorach, Napraw łączniki, Uprość tory, Nowy posterunek). Narzędzia rysujące tory były już ukryte na zobrazowaniu. „Łącznik obrazów” zostaje na planie (to połączenie w układzie torowym).
- **Peron na planie** (`UI/EditorWindow.PlanTools.cs`): klik – początek krawędzi peronu, klik – koniec (wzdłuż toru, także skośnego), klik – szerokość w bok; podgląd z wymiarami w metrach. Peron = prostokąt obrócony: pole `rot` [°] wokół (x, y) (`PlatformDef.Rot`, `Corners`, `Contains`, `Bounds`; rysowanie Ig-10 z obrotem; wskazywanie, zaznaczanie prostokątem i obrys – po obróconym kształcie; pole „rot” w panelu peronu na planie). Bez łapania osi toru (krawędź peronu jest ok. 1,7 m od osi) – siatka i prowadnice. Na zobrazowaniu peron jak dotąd – prostokąt przeciągany.
- **Rozjazd na planie jak w terenie:** klik na torze w miejscu zwrotnicy (tor dzielony w punkcie rzutu), klik na drugim torze – prosty łącznik ze zwrotnicami na obu końcach (odcinki zwrotnicowe jak dotąd, `CrossoverSecs`), klik w wolnym miejscu albo na końcu toru – odgałęzienie prostym torem. Kąt dowolny, bez ukosu–pionu–ukosu i bez „skosów” pulpitu. Na zobrazowaniu (i w starych testach bez widoku planu) – narzędzie pulpitowe bez zmian.
- **Zwrotnica na torach skośnych – poprawiony błąd modelu:** odgałęzienie (−) było wybierane jako tor bardziej nachylony względem poziomu (`LegSlope`); na torze pochylonym np. o 5° łącznik / odgałęzienie bliższe poziomowi wychodziło jako (+), tor zasadniczy jako (−) (zła prędkość na bok, złe przebiegi). Teraz (−) = większe odchylenie od przedłużenia iglicy (`Station.LegDev`, `StationDoc.MinusLeg`). Przy iglicy poziomej wynik jak dawniej – sprawdzone na `Data/station.json` (tylko odczyt): 0 różnic na 72 zwrotnicach.
- **Semafory na planie wzdłuż toru:** symbol obrócony o kąt najmniej pochylonego toru w węźle (`Ig10.TrackAngle`, do ±60°); tory poziome – bez zmian.
- **Test `--editshot p.png mijanka out.json [skos°]`** (`UI/EditorWindow.Mijanka.cs`): prosta mijanka budowana klikami narzędzi jak myszą (widok przesuwany do punktu, przybliżenie jak przy rysowaniu po zdjęciu – 1 m ≈ 3 px): tor 1 z szlakami, tor 2, dwa rozjazdy planu do końców toru 2, 6 semaforów, szlaki 1Z / 1W narzędziem Szlak, peron 3 kliknięciami; potem Sprawdź, zwrotnice (+/−), tablica przebiegów (20) i 8 przebiegów pociągowych nastawionych w symulatorze (wjazdy na tor 1 – 60 km/h, na tor 2 – 40 km/h, wyjazdy na oba szlaki); zrzuty planu z bliska i zobrazowania. Wynik: 0 błędów przy skosie 5° i 0°. `plantools out.json` – peron i rozjazdy na torach 5°, (+)/(−), paski.
- **Zauważone przy budowie mijanki (do decyzji właściciela):** (1) zasięg łapania ok. 10 px – przy oddalonym widoku (cała mijanka na ekranie, 1 px ≈ 1,5 m) tor rysowany 4,75 m obok przykleja się do istniejącego toru (znacznik pomarańczowy to pokazuje); rysować przy przybliżeniu ≥ ok. 3 px na metr. (2) W skali rzeczywistej 1:2000 / 1:2000 rozstaw 4,75 m to ok. 6 j., a symbole Ig-10 są w milimetrach arkusza – komory semafora sięgają sąsiedniego toru (w skali Ig-10 1:500 w poprzek tego nie ma). (3) Nowe tory planu nie trafiają same na zobrazowanie – lista „Nieumieszczone” (zgodnie z zasadą: obrazy rysuje człowiek).
- Zrzuty: `zrzuty/Mijanka_plan_rozjazd_skosny.png`, `zrzuty/Mijanka_plan_peron_skosny.png`, `zrzuty/Plan_peron_lacznik_odgalezienie_skos.png`, `zrzuty/Zobrazowanie_pasek_tylko_obraz.png`.

**ZROBIONE (2026-10-16, cd.): kierunek jazdy wg toru (A→B) zamiast „wschód / zachód”; urządzenia przy torze; skala symboli; stacja testowa** (zgłoszenie właściciela: „problem z założeniem, że semafory i inne są w lokalizacji wschód–zachód zamiast przyłożenia do toru; urządzenia muszą się spinać z ID toru”; decyzje: przebudowa całości, kierunek względem toru A→B).
- **Orientacja torów** (`Model/TrackOrientation.cs`): każdy tor ma kierunek „+” = od A do B, spójny przez węzły (zwykły węzeł – przejście, zwrotnica – ostrze po przeciwnej stronie niż oba odgałęzienia; odgałęzienia = para torów o najbliższym kierunku, wg geometrii, nie X). `Station.EdgeDir` = A→B (pulpit / zobrazowanie: dalej z rysunku – `GeoDir`). Stary plik (bez `"orient": "ab"`) – jednorazowo w pamięci: tory odwracane tak, by A→B = dawny „wschód” (X / pole `dir`), zapisane ±1 (sygnalizatory `dir`, końce szlaków `outward`, `defDir`) zachowują znaczenie. Po każdej zmianie w edytorze (`StationDoc.Build` → `Orient`): tory niezgodne z sąsiednimi (np. narysowane od prawej, nowe połączenia) odwracane, a sygnalizatory i szlaki przy nich – przeliczane (wg toru przed sygnalizatorem / toru szlaku). Węzeł z dwoma torami pod kątem ostrzejszym niż 90° (tor zawraca w węźle – błąd rysunku) – bez przejazdu, jak dawniej; „Sprawdź” to pokazuje (u właściciela: **LF:n50** – tory e419 / e301 leżą na sobie przy zwrotnicy 86; bez tej reguły powstawało 31 przebiegów „przez szpilkę”). Sprzeczności (trójkąt torowy, pętla) – w „Sprawdź”.
- **Równoważność:** `Data/station.json` – 979 przebiegów wczytany wprost, przez edytor i po edycji szlaku (diagnostyka `--trasy plik [szlak]`); edtest identyczny z wersją sprzed zmian poza zamierzonym tekstem „kierunek stały (wyjazd z …)”; selftest 25 z 26, 4 z 4; fuzz / dfuzz 0 błędów.
- **Sygnalizator przy torze:** `SignalDef.Ahead` / `Approach` (tor przed sygnalizatorem / dojazdowy – w panelu z odcinkami), `Side` („P” / „L”, pole `side`), `Dir` = obowiązuje dla jazdy zgodnie z A→B (+1) / przeciwnie. **Stawianie na planie:** klik na torze, klik po stronie toru – obowiązuje dla jazdy, przy której stoi po prawej (Ctrl – po lewej); podgląd ze strzałką. Panel: „Obowiązuje dla jazdy: zgodnie z A→B / przeciwnie”, „Strona toru”. Symbol na planie (`Ig10.TravelDir`) – wzdłuż toru przed sygnalizatorem, po właściwej stronie (tory skośne, pionowe, łuki). Testy skryptowe podające kierunek w odpowiedzi okna („→ / ←”) stawiają jak dawniej jednym kliknięciem.
- **Pulpit:** zwroty symboli z rysunku zobrazowania – `SignalDef.VDir`, `BlockEnd.VOut` (`Station.Visual`), strzałki blokady liniowej przeliczane na zwrot rysunku; logika (porównania kierunków) – bez zmian. Polecenia: koniec szlaku wewnętrznego – posterunkiem (np. `2A Wbl LW`) albo E / W = w prawo / w lewo **na obrazie**. Komunikaty: „kierunek stały (wyjazd z …)”, podgląd komputera zależności – „wyjazd z …” (`Simulator.DirText`).
- **Edytor bez wschodu / zachodu:** kierunek wyjazdu na szlak i kolejność odstępów SBL – wzdłuż toru (`StationDoc.OutwardDir`), nie z X; kierunek zasadniczy szlaku – „wyjazd z <posterunek>”; nagłówki końców szlaku; usunięte pole i pytanie „kierunek toru pionowego” oraz ostrzeżenie „tor pionowy bez dir”; pomoc.
- **Wielkość znaków Ig-10 na planie** (`Station.SymbolScale` z `planScale`): skala rzeczywista (1:2000 / 1:2000) – 0,25 (ta sama proporcja do rozstawu 4,75 m co na arkuszu 1:500 w poprzek; wcześniej semafor był 4× za duży i komory wchodziły na sąsiedni tor); skale Ig-10 – bez zmian; linie co najmniej ok. 1 px na ekranie. Skala planu bez zmian.
- **Prowadnice** – tylko prawdziwą myszą (skrypty testów klikały w pas linijki).
- **Stacja testowa „Testowo”** – `Data/projekty/stacja_testowa.json` (wybór przy uruchomieniu), budowana narzędziami jak myszą: `--editshot p.png testowo out.json [skos°]` (`UI/EditorWindow.Testowo.cs`). Tor 1 zasadniczy ze szlakami 1Z (SBL dwukierunkowa, 3 odstępy) i 1W (PBL dwukierunkowa), tory 2 i 3, bocznica 4 z kozłem, wykolejnicą Wk1 i tarczą Tm1, 5 rozjazdów, semafory A, B, D1–D3, F1–F3, perony 1 i 2, przejazd km 1,250, tory stacyjne JT1–JT4; plan skośny 4° w skali rzeczywistej, zobrazowanie pulpitu osobno (poziomo). „Sprawdź” bez uwag, 29 przebiegów, nastawiają się A→D1/D2/D3, B→F1/F2/F3, F1→1Z, D1→1W, Tm1→F1. Zrzuty: `zrzuty/Stacja_testowa_Testowo_pulpit.png`, `zrzuty/Stacja_testowa_Testowo_plan.png`.
- **Test `mijanka` przy dowolnym kierunku linii:** `--editshot p.png mijanka out.json 5|90|180|250` – za każdym razem 20 przebiegów i 8 nastawionych przebiegów pociągowych (semafory stawiane klikiem po stronie toru).
- **Do decyzji właściciela:** (1) szlak wewnętrzny w poleceniach – zostawić E / W („w prawo / w lewo na obrazie”) czy tylko posterunek; (2) LF:n50 – poprawić rysunek; (3) zapis pliku po otwarciu w edytorze utrwala orientację torów (`"orient": "ab"`, część torów ma zamienione końce A/B – znaczenie bez zmian).

**Plan podziału programu na moduły** (ustalenia z właścicielem 2026-10-16; podstawa: `Zrodla/EBILock_komputerownia.md`): pulpit (zobrazowanie + polecenia, PIP); komputer zależności (tylko srk: przebiegi wg tablicy zależności, utwierdzenia, ochrona boczna, blokada stacyjna, logika blokady liniowej – nie prowadzi pociągów); urządzenia przytorowe / sterowniki obiektowe z kartami (napędy zwrotnic, sygnalizatory, liczniki osi / obwody torowe, przejazdy – stany, czasy, usterki; dziś brak – pociągi sięgają wprost do stanu zależności); ruch pociągów; rozkład jazdy; otoczenie (sąsiednie posterunki spoza LCS); multiplayer i łączność; instruktor (scenariusze, usterki, rejestrator, ocena). Wspólne dane projektowe: `station.json` + edytor. Dziś `Simulator` to jedna klasa (partial) – podział to etap 7.

**Do zrobienia w etapie 1:**
- Ten sam tor na dwóch obrazach (granica stacji).
- Osobne obrazy z własnym układem współrzędnych zamiast zakresów X.
- ~~Napisy, perony i przejazdy wspólne dla obu trybów~~ – ZROBIONE 2026-10-15 (decyzja właściciela: **osobne kopie**). Zobrazowanie ma własne `display.labels`, `display.platforms`, `display.crossings`; plan schematyczny (i okno „Plan stacji”) – swoje w korzeniu pliku. Jednorazowa konwersja: przy wczytaniu w edytorze zobrazowanie bez własnych kopii dostaje kopię obecnych (`StationDoc.EnsureDisplayDeco`; do pliku trafia przy zapisie). `Station.Parse` bierze do `Display` kopie z `display`, a gdy ich brak – te z korzenia. W edytorze `StationDoc.DecoOnDisplay` (ustawiane przy zmianie trybu) przełącza `doc.Labels/Platforms/Crossings` na kopie zobrazowania, a `EditorWindow.V` to geometria bieżącego trybu. W trybie zobrazowania są narzędzia Napis (N), Peron (P), Przejazd (X), zaznaczanie (klik, Shift, prostokąt), przeciąganie, strzałki, Del (usuwa z pulpitu) i panel właściwości pojedynczego elementu. Test: `--editshot p.png deco out.json` (12× OK), a dfuzz ma operacje na tych elementach i sprawdza, że plan się nie zmienia.
- ~~Fuzz edytora także w trybie zobrazowania~~ – ZROBIONE 2026-10-15: `--editshot p.png dfuzz N seed` (`EditorWindow.DFuzz`). Losowo: przeciąganie węzłów i punktów załamania, punkty załamania (dodanie/usunięcie), prostokąt, strzałki, Del, „Umieść”, podział/scalenie torów, cofnij/ponów, zmiana obrazu. Sprawdza: końce torów obrazu w węzłach obrazu, tor na obrazie ⇒ jego węzły na obrazie, operacje obrazu nie zmieniają układu torowego ani tablicy przebiegów, Ctrl+Z przywraca dokładnie stan sprzed operacji, rysowanie i budowa modelu. Wynik na kopii testowej: 0 błędów (ziarna 1, 2, 3, 7, 11, 42). Test mutacyjny (wyłączone przesuwanie końca toru) dał tysiące błędów – kontrola działa.
- Plan schematyczny: kilometraż, opis szlaków i posterunków sąsiednich jak na planach Semaforka; ułożenie planu zgodne z terenem robi właściciel.

#### Pierwotny opis etapu
**Problem:** jeden rysunek jest jednocześnie topologią i obrazem pulpitu, więc przebudowa jednego miejsca wymusza zmiany w całości, a tory „nakładają się”.

**Cel:**
- **Układ torowy** jest źródłem prawdy: topologia, długości, odcinki, sygnalizatory, posterunki. Własny rysunek roboczy nie trafia na pulpit.
- **Zobrazowanie** (sekcja `display` w `station.json`) ma własne położenie każdego elementu na obrazach. Elementy są powiązane z układem po identyfikatorach. Przesuwanie i nakładanie nie zmienia połączeń.
- Pulpit rysuje z zobrazowania, a komputer zależności liczy z układu.
- Konwersja: istniejący rysunek staje się jednorazowo zobrazowaniem. Nowy element układu nie pojawia się na pulpicie, dopóki użytkownik go nie umieści.
- Edytor ma dwa tryby: „Układ torowy” i „Zobrazowanie”.

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
- warunek każdego kroku: build, selftest 25 z 26 / 4 z 4, `--edtest` (Widzew 979 przebiegów), `fuzz 300 7` i `dfuzz 300 7` po 0 błędów, `--editshot p.png testowo out.json` i `mijanka out.json 5` bez błędów; w krokach przenosin dodatkowo identyczny dziennik selftestu (`--selftest out.txt` zapisuje pełny dziennik do `out.txt.zdarzenia`) i identyczne wyniki scenariuszy `--selftest out.txt usterki|zima|manewry|ic_05`.
- **ZROBIONE 2026-10-16: kroki 0–3** (czyste przenosiny, zachowanie bez zmian – sprawdzone porównaniem dzienników).
- Pytania do właściciela przed krokami 5, 6, 8: rozprucie a brak kontroli (kto rozstrzyga, usterka „sygnalizacja rozprucia” bez pojazdu), przepalenie włókna czerwonego / zezwalającego, sygnalizator bez łączności (ciemny czy czerwony), manewry wg tarcz zamiast utwierdzeń, opóźnienie jednego cyklu rozkaz → meldunek.
- Pozostałe przecieki (do kroków 4–9): ruch czyta `Sigs[].Aspect`, `Sws[].Pos`, `Secs[].LockType`, `Routes` i pisze `Rozpr`, `TrainOcc`; KZ pyta `Trains.OnBlock`; `Otoczenie` czyta listę pociągów (`EmergingAtFar`) i porównuje strony szlaku po X (`FarSec`); pulpit bierze numery pociągów z listy pociągów.

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
- `PLAN.md` jest przestarzały (v0.4–0.5) – źródłem prawdy jest ten plik.

## 6. Zrobione (skrót)
- Edytor:
  - tory, zwrotnice, rozjazdy (ukos–pion–ukos), sygnalizatory, PK, kozły (funkcjonalne i symbol), wykolejnice, przejazdy, perony, napisy;
  - szlaki: SBL/PBL, jedno- i dwukierunkowe, bez blokady; wewnętrzne przez złącza; napis nad strzałką przy każdej stacji;
  - pełny panel danych blokady, prędkości, długości, reguły przebiegów;
  - kontrola układu z zaznaczaniem problemów na rysunku;
  - odcinki: identyfikator `POST:nazwa`, rodzaj kontroli (licznik osi albo obwód torowy).
- Pulpit:
  - zobrazowanie wg Ie-104 i SimRail;
  - wybór przebiegu z oznaczeniem końców i wariantów przez zwrotnice;
  - nastawianie przebiegu w turkusowej ramce, droga zapala się po przełożeniu;
  - obraz poglądowy.
- Gra sieciowa (Radmin), obszary sterowania, telefon między stanowiskami, podgląd komputera zależności, utrata łączności.
- Rozkład rzeczywisty z GTFS z obiegiem jednostek ŁKA.
