# Plan rozwoju – dla agentów AI i programistów

Dokument opisuje stan projektu, ustalenia z właścicielem i kolejne etapy. Jest **źródłem prawdy o kierunku prac** i ma pierwszeństwo przed przestarzałym `PLAN.md` (v0.4–0.5).

Po zakończeniu etapu albo zmianie ustaleń zaktualizuj sekcje „Stan” i „Etapy”.

Ostatnia aktualizacja: 2026-10-16 (nowy edytor UT, etapy 0–2).

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
- `Sim/Urzadzenia/` – urządzenia przytorowe (krok 4): `KartaOdcinka` (obwód torowy / licznik osi: osie w terenie, zajętość bez pojazdu, błąd zliczenia, zerowanie, pierwszy przejazd). Ruch zgłasza osie (`Urzadzenia.Osie`), karta melduje KZ status (`Publikuj` → `SecState.TrainOcc/Fault`), ZeroLO to rozkaz `Urzadzenia.Zeruj`, usterki odcinków idą do karty. KZ zna „pociąg na szlaku” z odcinków i meldunku sąsiada (`TrainOnBlock`, `Otoczenie.SzlakZajetyUSasiada`), nie z listy pociągów.

Pozostałe elementy:
- **Sieć:** `Net/LcsNet.cs` – `LcsServer` przy komputerze zależności i `RemoteSession` na stanowisku. JSON w liniach przez TCP, port 47120.
- **Model układu (symulator):** `Model/Station.cs` – wczytany `station.json`, tylko do odczytu. Nowy model UT: `app/Lcs.Model` (`UtProject`, `UtDocument`, `Geo`).
- **Edytor układu torowego:** osobny program `app/Lcs.EdytorUT` (`EdytorUT.exe`), plan w `PLAN_EDYTOR_UT.md`. W symulatorze nie ma już edytora.
- **Podgląd komputera zależności:** `UI/KzWindow.cs` (przebiegi, utwierdzenia, liczniki czasu, blokady, łącze).
- **Testy bez okna** (z `bin/Release/net8.0-windows`):
  - `--selftest out.txt` – scenariusz z automatycznym dyżurnym (postęp w `out.txt.postep`);
  - `--screenshot p.png 3 "#page=1" "E S1 Pociąg"` – zrzut pulpitu (też `#serve=`, `#join=`, `#fault=`, `#start=`, `#via=`, `#kz=`).
  - edytor UT: `app/Lcs.EdytorUT/bin/Release/net8.0-windows/EdytorUT.exe --test wynik.txt [zrzut.png]`, oczekiwane „błędów: 0”.
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

### Etap 1 – Edytor układu torowego od nowa (od 2026-10-16)
Decyzja właściciela (2026-10-16): edytory UT i zobrazowania budujemy od nowa jako osobne programy. Stare usunięte, bez przerabiania. **Plan, decyzje i etapy: `PLAN_EDYTOR_UT.md`** (XYZ w PUWG 1992, dwa widoki skali, parabola 3°, katalog rozjazdów KZN + `Zrodla/rozjazdy_1.pdf`, wysokości z NMT).

**ZROBIONE 2026-10-16 – etap 0 (usunięcie starych edytorów):**
- Usunięte z `LcsWidzew`: `UI/EditorWindow*.cs`, `UI/TilePicker.cs`, `Model/StationDoc*.cs`, `Model/Profil.cs`, `Sim/EditorTest.cs`, przyciski edytorów w oknie pulpitu, tryby `--edtest` i `--editshot`. Z `PanelView` usunięte: interfejs edytora, widok edytora (`EditView`) i plan schematyczny (`PlanView`, `DrawPlan`). `PlanLayers` przeniesione do `UI/PlanStationWindow.cs`.
- Stan sprzed usunięcia: commit **`d2c7431`**. `ROZWOJ.md` w tej wersji ma pełną historię starego edytora: plan Ig-10, podkłady, rozjazdy, zobrazowanie, testy fuzz / dfuzz / ut / mijanka.
- Zostają: pulpit, komputer zależności, „Plan stacji” na żywo (`Ig10.cs`), `Model/Station.cs` i `Model/TrackOrientation.cs`, selftest. Symulator wczytuje dotychczasowe `station.json`. Selftest po usunięciu: 25 z 26 / 4 z 4, ochrona boczna 959 z 979 / 4 z 4 (bez zmian).

**ZROBIONE 2026-10-16 – etap 1 (szkielet):**
- `app/Lcs.slnx`, czyli rozwiązanie: `Lcs.Model` (net8.0, bez okien), `Lcs.EdytorUT` (WPF, `EdytorUT.exe`), `LcsWidzew`.
- `Lcs.Model`: `UtProject` (nazwa, `crs` EPSG:2180, skala rysunku 1:500 / 1:1000 / 1:2000, uwagi, ostatni widok), `UtDocument` (plik `*.ut.json`, zapis przez `.tmp` z `.bak`; każda zmiana przez `Change` ze zrzutem JSON do cofania, widok poza cofaniem), `Geo` (PUWG 1992 ↔ WGS84 szeregami Krügera do n⁴, tam i z powrotem 0,001 mm; odczyt punktu „lat, lon” albo „X Y”; konwencja geodezyjna **X – północ, Y – wschód**).
- `Lcs.EdytorUT`: płótno `UtView` w metrach (rysowanie w pikselach, przeliczenie w double), kółko z punktem pod kursorem, przesuwanie LPM / środkowym, siatka 1/2/5·10ᵏ (krok ≥ 14 px), linijki ze współrzędnymi, podziałka, strzałka N. Do tego „Idź do” punktu, pasek stanu (X, Y, φ λ, m/px, skala ekranu), panel obszaru, Nowy / Otwórz / Zapisz / Zapisz jako (domyślnie `projekty_ut/`), Cofnij / Ponów.
- Test `EdytorUT.exe --test wynik.txt [zrzut.png]` (`Test.cs`, przez kontrolki okna): 35 sprawdzeń, **0 błędów**. Zrzut: `zrzuty/EdytorUT_etap1.png`. Uruchamianie: `uruchom_edytor_ut.bat`.
- Nie sprawdzono prawdziwą myszą (test woła metody płótna i zdarzenia kontrolek).

**ZROBIONE 2026-10-16 – etap 2 (podkłady):**
- Model (`Lcs.Model`): `UtBackground` – ortofotomapa (siatka `UtGrid`, kafelki) albo grafika (punkty kalibracji, tryb podobieństwo / afiniczne). `UtTile` – plik i położenie `Affine` (piksel → PUWG 1992: Y = A·c + B·r + C, X = D·c + E·r + F; plik world – środek piksela przeliczany na narożnik). Dopasowanie metodą najmniejszych kwadratów: podobieństwo od 2 punktów (uwzględnia odbicie wierszy obrazu), afiniczne od 3. Ścieżki plików względem projektu (po „Zapisz jako” poza katalog – bezwzględne). `Change(…, merge)` łączy kolejne zmiany suwaka w jedno cofnięcie.
- **Długości w terenie:** `Geo.ScaleFactor` (skala odwzorowania, 0,9993 na południku 19°) i `Geo.GroundDistance` (odległość w układzie / skala, Simpson). Sprawdzone z odległością geodezyjną Vincenty'ego (`Geo.GeodesicDistance`): < 2 mm na 2–3 km. Przykład z testu: Łódź, 2026,962 m w terenie wobec 2025,580 m w układzie.
- **Ortofotomapa Geoportalu** (`Pobieranie.cs`): WMS 1.3.0 wprost w EPSG:2180 (BBOX: X, Y), pełna rozdzielczość (HighResolution; na stacji ok. 5 cm/px – widać podkłady), gdzie jej brak – StandardResolution. Ponawianie z nowym połączeniem bez ciasteczek (losowe 404 Geoportalu, jak w starym kodzie). Do projektu: „Zaznacz obszar…” (przeciągnięcie prostokąta) albo „Cały widok”, rozdzielczość 5 / 10 / 25 / 50 cm. Siatka kafelków 2048 px wyrównana do wielokrotności rozdzielczości, pliki `podklady/orto_…/r{w}_c{k}.jpg` obok projektu, po 2 naraz, nieudany kafelek nie przerywa, „Dopobierz brakujące”, „Przerwij”.
- **Podgląd na żywo** (`LivePreview.cs`, nie zapisuje się): ortofotomapa bieżącego widoku jednym zapytaniem w rozdzielczości ekranu po 350 ms bezruchu (poprzedni obraz zostaje na swoim miejscu) albo zapasowo kafelki Esri / OSM (Web Mercator → PUWG 1992 afinicznie z narożników kafelka).
- **Rysowanie** (`UtView.cs`, `ImageCache.cs`): podkłady pod nakładkami, nad siatką. Obrazy wczytywane w tle na poziomach 1/2ⁿ wg przybliżenia, limit pamięci 900 MB (najdawniej używane zwalniane). Brakujący plik – czerwona ramka „brak pliku”. Podpis źródła w prawym dolnym rogu.
- **Grafika z pliku** (skan planu, mapa): kopia do `podklady/` projektu. Z plikiem world (.pgw / .jgw / .tfw / .wld) od razu na miejscu, bez niego – na środku widoku z kryciem 70 %. „Kalibruj punktami”: klik na grafice, klik w prawdziwym miejscu (np. na ortofotomapie); 1 punkt – przesunięcie, 2 – podobieństwo, 3+ – afiniczne (wybór); odchyłki w cm w panelu, PPM – rezygnacja z punktu, Ctrl+Z – cofnięcie punktu. Na czas wskazywania miejsca grafika półprzezroczysta. „Przesuń” (przeciąganie), obrót [°] i piksel [cm] z klawiatury (zmienia się tylko wpisane pole).
- **Warstwy:** lista (u góry – na wierzchu), widoczność, krycie (suwak), ↑ ↓, „Pokaż”, „Usuń” (pliki zostają).
- **Miarka (M):** łamana, długości odcinków i suma w terenie, w pasku: długość w układzie, skala odwzorowania, azymut; Backspace – cofnij punkt, PPM – wyczyść; przeciągnięcie przesuwa widok.
- Test `EdytorUT.exe --test` (część „etap 2”, potrzebny internet): 96 sprawdzeń, **0 błędów** – m.in. pobranie 2 kafelków przez przeciągnięcie, styk kafelków, miarka = odległość geodezyjna, kalibracja klikami do 0,000 mm, Przesuń, obrót, warstwy, zapis w innym katalogu, Esri. Okna testu i zrzutu otwierają się poza ekranem, bez aktywowania. Wcześniej obrót kółka myszy nad oknem testu zmieniał przybliżenie zrzutu. Zrzuty: `zrzuty/EdytorUT_etap2_podklady.png`, `EdytorUT_etap2_ortofotomapa_4cm.png`.
- Nie sprawdzono prawdziwą myszą. Symulator (`LcsWidzew`) bez zmian w tym etapie.

**Następny krok:** etap 3 – tory (prosta, łuk, parabola 3°; długości w terenie z `GroundDistance`).

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
- Pytania do właściciela przed krokami 5, 6, 8: rozprucie a brak kontroli (kto rozstrzyga, usterka „sygnalizacja rozprucia” bez pojazdu), przepalenie włókna czerwonego / zezwalającego, sygnalizator bez łączności (ciemny czy czerwony), manewry wg tarcz zamiast utwierdzeń, opóźnienie jednego cyklu rozkaz → meldunek.
- Pozostałe przecieki (do kroków 4–9): ruch czyta `Sigs[].Aspect`, `Sws[].Pos`, `Secs[].LockType`, `Routes` i pisze `Rozpr`; `Otoczenie` czyta listę pociągów (`EmergingAtFar`) i porównuje strony szlaku po X (`FarSec`); pulpit bierze numery pociągów z listy pociągów.

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
