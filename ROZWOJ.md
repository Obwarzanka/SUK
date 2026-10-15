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
| `Zrodla/Instrukcja_Ig-10_-_WCAG.pdf` | **Ig-10 [D-27]** – plany schematyczne: treść (§ 4), skale, **znaki umowne (zał. 8)** – wzór dla planu w edytorze i okna „Plan stacji” |
| `zrzuty/wzory/`, `Zrodla/SimRail_*` | wzory wyglądu pulpitu |
| `README.md` | obsługa programu, edytor, gra sieciowa, testy |

## 4. Architektura (stan obecny)

Trzy warstwy, jak w rzeczywistości (KOMPUTERY.pdf, Tabela 23):

1. **Zobrazowanie / stanowisko**
   - `UI/PanelView.cs` (rysowanie), `MainWindow.xaml(.cs)` (okno pulpitu), `UI/Dialogs.cs`, `UI/NetWindow.cs`, `UI/KzWindow.cs` (podgląd komputera zależności).
   - Korzysta **wyłącznie** z sesji `ILcsSession` (`Sim/Session.cs`) i meldunków `Indication` (`Sim/Meldunki.cs`). Nie sięga do wnętrza symulatora.
2. **Komputer zależności (EBILock):** `Sim/Simulator.cs`, `Interlocking.cs` (przebiegi), `Blocks.cs` (blokady liniowe), `Commands.cs` (polecenia Ie-104), `Desk.cs` (stanowiska, obszary sterowania).
3. **Teren:** `Sim/Trains.cs` (pociągi, usterki), `RealTimetable.cs` (rozkład GTFS).

Pozostałe elementy:
- **Sieć:** `Net/LcsNet.cs` – `LcsServer` przy komputerze zależności i `RemoteSession` na stanowisku. JSON w liniach przez TCP, port 47120.
- **Model układu:** `Model/Station.cs` (wczytany, tylko do odczytu) i `Model/StationDoc.cs` (dokument edytora na `JsonObject`, z cofaniem).
- **Edytor:** `UI/EditorWindow.cs` + `UI/EditorWindow.Display.cs` (tryb zobrazowania, klasa `partial`). Kontrola układu: `StationDoc.Problems()` zwraca problemy z miejscem na rysunku (węzeł albo `edge:id`), okno „Kontrola układu” numeruje je na rysunku.
- **Podgląd komputera zależności:** `UI/KzWindow.cs` (przebiegi, utwierdzenia, liczniki czasu, blokady, łącze).
- Testy trybu edytora `--editshot p.png <tryb>`: `podklad orto|osm|esri "lat, lon" szer wys out.json`, `skala out.json [podkład]`, `dclear out.json`, `fuzz N seed`, `dfuzz N seed`, `deco out.json`, `validate`, `problems i`, `display out.json`, `plan`, `planskew`, `autoname`, `block ID [nowaNazwa]`, `blockpanel ID`, `szlakdiag`, `script`, `szlaki`, `szlaki2`, `rozjazd`, `normalize`, `respace`, `select`, `zoom`, `demo`, `userszlak`. Wynik tekstowy w `p.png.txt`.
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
- tory: `radius` (łuk [m]), `cant` (przechyłka [mm]) – na razie tylko dane, bez wpływu na prędkość;
- tory (Ig-10): `cat` – `gz` główny zasadniczy / `gd` główny dodatkowy / brak = boczny (grubość osi na planie), `elec: true` – zelektryfikowany (niebieski); wypełnia właściciel (panel toru albo panel grupy po zaznaczeniu toru);
- sygnalizator: `lamps` – liczba komór na planie Ig-10 (brak = domyślna: wjazdowy 5, wyjazdowy 4 / 3 bez Ms2, odstępowy 3, tarcze 1);
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
