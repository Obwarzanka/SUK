# Plan rozwoju – dla agentów AI i programistów

Dokument opisuje stan projektu, ustalenia z właścicielem i kolejne etapy. Jest **źródłem prawdy o kierunku prac** i ma pierwszeństwo przed przestarzałym `PLAN.md` (v0.4–0.5).

Po zakończeniu etapu albo zmianie ustaleń zaktualizuj sekcje „Stan” i „Etapy”.

Ostatnia aktualizacja: 2026-10-16 (nowy edytor UT, etapy 0–4, baza symboli Ig-10).

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
- Strony PDF (np. Ig-10 zał. 8) ogląda się przez `tools/pdfr` (render do PNG) – narzędzie Read bez pdftoppm nie pokazuje stron.
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

**ZROBIONE 2026-10-16 (cd.): mapka kafelków i stała siatka** (zgłoszenie właściciela: „pobieranie podkładów ciężkie, ciężko na ślepo trafić – może mapka z kafelkami”)
- **Okno „🗺 Mapka kafelków…”** (`TileMapWindow.cs`, przycisk w panelu „Podkłady”): mapa OSM (albo zdjęcia Esri / ortofotomapa) z liniami kolejowymi OpenRailwayMap (numery torów, rozjazdy) i siatką kafelków wybranej rozdzielczości. LPM – zaznaczanie; przeciąganie „maluje” wzdłuż linii (droga kursora próbkowana, szybki ruch nie zostawia dziur), zaczęcie na zaznaczonym – odznacza. PPM / środkowy – przesuwanie, kółko – przybliżenie, „Idź do”. Zielone – już w projekcie, niebieskie – do pobrania, czerwona ramka – zamówione, niepobrane. Licznik (kafelki, km², MB), „Pobierz zaznaczone”, „Pokaż w edytorze”. Zaznaczenie osobno dla każdej rozdzielczości.
- **Stała siatka** (`UtGrid.Global`, początek w (0, 0) PUWG 1992, kafelek 2048 px: 5 cm → 102,4 m, 10 cm → 204,8 m, 25 cm → 512 m, 50 cm → 1024 m): ten sam kafelek ma zawsze te same granice. Wszystkie pobrania jednej rozdzielczości (mapka, „Zaznacz obszar”, „Cały widok”) trafiają do jednej warstwy „Ortofotomapa N cm” i katalogu `podklady/orto_Ncm/`, już pobrane są pomijane. `UtBackground.Wanted` – zamówione kafelki, „Dopobierz brakujące” bierze zamówione, a niepobrane. Dawne pobrania (siatka lokalna z pierwszej wersji etapu 2) działają jak dotąd.
- Przerwane pobieranie zachowuje pobrane kafelki w projekcie (`GridResult.Cancelled`).
- Test `--test`: 111 sprawdzeń, **0 błędów** (m.in. malowanie 3 kafelków z pominięciem pobranych, odznaczanie, PPM przesuwa, szybkie przeciągnięcie przez 6 kafelków, osobne zaznaczenie dla rozdzielczości, „Pobierz zaznaczone” dokłada do tej samej mozaiki, ten sam obszar drugi raz – bez dubli, Ctrl+Z). Zrzut: `zrzuty/EdytorUT_mapka_kafelkow.png`.

**POPRAWIONE 2026-10-16: puste białe kafelki ortofotomapy** (zgłoszenie właściciela ze zrzutem: część pobranych kafelków biała). Przyczyna: Geoportal czasem zwraca „200 OK” z pustym białym obrazem (zawsze 115 315 B) zamiast zdjęcia – w próbie 6 zapytań o ten sam kafelek 1 pusty (u właściciela 9 z 44). Teraz: `Pobieranie.IsBlank` (miniatura 64 px, rozpiętość jasności < 6) – pusty obraz to nieudana próba (nowe połączenie, do 6 prób); plik pusty z wcześniejszego pobrania jest pobierany od nowa. Przycisk warstwy „Napraw puste kafelki” (sprawdza pliki, usuwa puste, dopobiera). `ImageCache` czyta pliki do pamięci (plik nie zostaje otwarty). Zaznaczona mozaika – ramka tylko po obwodzie. Test: 115 sprawdzeń, 0 błędów (m.in. podmieniony biały kafelek wykryty i pobrany od nowa).

**ZROBIONE 2026-10-16 – etap 3 (tory):**
- **Model** (`Lcs.Model/UtTrack.cs`): tor = oś toru jak trasa kolejowa – wielobok styczny (`UtVertex`: X, Y w PUWG 1992, R, L w terenie) z łukiem kołowym R i krzywymi przejściowymi (parabola 3. stopnia y = x³ / (6RL), L wzdłuż stycznej) w każdym wierzchołku wewnętrznym; numer, kategoria (gz / gd / boczny), elektryfikacja, Vmax. Kierunek toru = od pierwszego wierzchołka (kilometraż toru). Rozjazdy i urządzenia (etapy 4–5) przypina się po kilometrażu – tor nie jest cięty.
- **Geometria** (`Trasa.Compute`): τ = atan(L / 2R), koniec przejściowej E = (L, L² / 6R) leży na okręgu R ze styczną τ, środek okręgu (p, q) = (L − R·sin τ, L² / 6R + R·cos τ) na dwusiecznej → styczna T = p + q·tg(α/2), łuk kołowy α − 2τ; bez przejściowej T = R·tg(α/2). Elementy `Element` (prosta / przejściowa / łuk) z `At(s)` – punkt i azymut; `TrasaResult.At(km)`, `Nearest`, `Sample`. R i L w terenie mnożone przez skalę odwzorowania (długości w terenie = w układzie / K). Uwagi: łuk się nie mieści (brakuje x m prostej), przejściowe za długie dla kąta, załamanie bez łuku.
- **Tor równoległy** (`Trasa.Offset`): proste przesunięte, wierzchołki na przecięciu przesuniętych stycznych, R ∓ d (łuki współśrodkowe; na przejściowych odchyłka międzytorza ≤ 2 cm).
- **Płótno** (`UtView.Tory.cs`): oś wg Ig-10 (główny 0,5 mm, boczny 0,35 mm w skali rysunku, 1,5–4 px; zelektryfikowany – niebieski), przy ≤ 18 cm/px obie szyny w rozstawie 1435 mm (do dokładnego ułożenia na zdjęciu), numer toru; zaznaczony – wielobok stycznych, uchwyty wierzchołków, kreski granic elementów, etykiety R / L; błędy – czerwone kółko w wierzchołku.
- **Narzędzie Tor (T)**: klik – kolejne wierzchołki (podgląd z łukami na bieżąco), długość z klawiatury + Enter (w stronę kursora), Shift – na przedłużeniu ostatniego odcinka, przyciąganie do końca / wierzchołka / osi toru (Ctrl – bez), start na końcu toru – przedłużenie tego toru (dawny koniec dostaje łuk), klik na końcu innego toru – koniec rysowania, Enter / PPM / dwuklik – koniec, Backspace – cofnij, Esc – rezygnacja. R i L nowych wierzchołków – pola na pasku.
- **Edycja (Wskaż)**: klik – zaznaczenie toru / wierzchołka, przeciąganie wierzchołka (z przyciąganiem), dwuklik na boku wieloboku – nowy wierzchołek, Del – wierzchołek albo tor; przeciąganie pustego miejsca – przesuwanie widoku (od kilku px).
- **Panel toru**: numer, kategoria, elektryfikacja, Vmax, długość w terenie i w układzie, lista elementów (długości, kąty), uwagi, R i L każdego wierzchołka, X / Y zaznaczonego wierzchołka, tor równoległy (międzytorze, lewa / prawa), odwróć kierunek, pokaż, usuń. Po cofnięciu zaznaczenie zostaje (ponowienie przywraca panel).
- Test `--test` (część „etap 3”): 158 sprawdzeń razem, **0 błędów** – m.in. ciągłość położenia i kierunku na stykach (≤ 1e-6 m), punkty łuku w odległości R, T = R·tg(α/2), łuk α − 2τ, krzywizna przejściowej 0 → 0,985/R, prosta 1000 m = odległość geodezyjna, tor równoległy 4,7500 m; budowa klikami na ortofotomapie: 3 wierzchołki, długość 150 m z klawiatury (150,0000 m), PPM, numer i kategoria z panelu, R 800 w W2, przeciąganie wierzchołka, dwuklik + Del, przedłużenie od końca, tor równoległy z panelu, przyciąganie do osi, Esc, Del toru, zapis i odczyt. Zrzuty: `zrzuty/EdytorUT_etap3_tory.png`, `EdytorUT_etap3_szyny.png`.
- Nie sprawdzono prawdziwą myszą. Symulator (`LcsWidzew`) bez zmian.

**ZROBIONE 2026-10-16 – etap 4 (rozjazdy z katalogu):**
- **Katalog** (`Lcs.Model/UtTurnout.cs` – `RozjazdTyp`, `Katalog`; plik `app/Lcs.EdytorUT/Dane/rozjazdy.json`): Rz 60E1 / 49E1 (190–1200, 1:7–1:18,5), Rkpd, Rkp, St; R, skos 1:N, a (PR → ŚR), b (ŚR → KR), V na zwrot (rozjazdy_1.pdf). **a, b przybliżone** (`approx: true`): 1:9 – 16,615 m, pozostałe a = R·tg(α/2), b = a; właściciel wpisuje katalogowe w pliku.
- **Rozjazd zwyczajny** (`UtTurnout`): tor zasadniczy, km PR, kierunek (±1 względem km toru), strona, położenie zasadnicze (+ / −), zwrotnica nieuzależniona, numer (wpisuje projektant – okno po wskazaniu miejsca; zajęty – odrzucony). **Odgałęzienie to zwykły tor**: wierzchołki PR i ŚR (promień R typu) ustawia rozjazd (`Rozjazdy.Sync` w każdym `Change` – jedno Ctrl+Z), dalej przedłuża się je narzędziem Tor od końca; wierzchołki ustawiane przez rozjazd są zablokowane (przeciąganie, Del, wstawianie). Geometria: od PR prosto a − T, łuk R o kąt skosu, prosto do KR (T = R·tg(α/2)).
- **Łącznik** (przeciągnięcie na tor sąsiedni, równoległy do 0,5°): dwa rozjazdy tego typu i tor łączący PR1 – ŚR1 – ŚR2 – PR2; drugi rozjazd (`BranchAtEnd`, `Partner`) trzyma się przecięcia skosu z torem (`Relink`) – przesunięcie pierwszego albo toru przesuwa łącznik. Del na którymkolwiek – cały łącznik.
- **Skrzyżowanie (St), rozjazd krzyżowy (Rkp, Rkpd)** (`UtCrossing`): klik na przecięciu dwóch narysowanych torów (`Rozjazdy.Intersect`), kontrola kąta z typem; łuki przejazdów (R typu, styczne do obu torów; Rkp – strona do wyboru); po zmianie torów przecięcie przeliczane.
- **Kontrola** (`Rozjazdy.Check`): poza torem, na łuku (rozjazdy łukowe – później), nachodzi na inny rozjazd.
- **Narzędzie Rozjazd (R)**, typ z listy na pasku: wciśnij na torze w miejscu PR i przeciągnij w stronę odgałęzienia (kierunek i strona z przeciągnięcia, podgląd na bieżąco), puść na torze sąsiednim – łącznik; krzyżowy / St – klik na przecięciu. Wskaż: klik – zaznaczenie, przeciągnięcie – przesunięcie wzdłuż toru. Panel: numer, typ, położenie zasadnicze, ręczna, km PR, zmień stronę, odwróć, dane typu (a, b przybliżone), uwagi, „Odgałęzienie”, usuń.
- Poprawione przy okazji: przekroczenie czasu Geoportalu (60 s, `TaskCanceledException`) było brane za „Przerwij” – pobieranie kończyło się zamiast ponowić; pusty obraz z pełnej rozdzielczości – od razu próba StandardResolution; testy ignorują prawdziwą klawiaturę (`UtView.IgnoreKeyboard` – pisanie przy komputerze zamieniało przeciągnięcie w przesuwanie).
- **Baza symboli Ig-10** (prośba właściciela: rysunki rozjazdów zgodne z Ig-10, „zrób bazę z symbolami”): `Lcs.Model/Symbole.cs`, plik **`app/Lcs.EdytorUT/Dane/symbole_ig10.json`** (generator `tools/symbole_ig10.py`, potem poprawki ręczne w JSON): znak = elementy (line / poly z wypełnieniem ink / paper / circle / text „{nr}”) w mm arkusza, x wzdłuż toru, y w stronę odgałęzienia; nazwa i strona Ig-10. Na razie 11 znaków z zał. 8 s. 18–24: rozjazd zwyczajny uzależniony (zasadniczo na tor główny / odgałęźny), nieuzależniony, nieoddany do użytku; Rkpd (4 odmiany), Rkp, skrzyżowanie. Rysowanie `SymbolPainter` (px/mm = skala rysunku, co najmniej 2,6 px/mm): rozjazd – w PR wzdłuż toru po stronie odgałęzienia; krzyżowy – w punkcie matematycznym. Okno **„Symbole Ig-10…”** (pasek): wszystkie znaki z opisem i stroną Ig-10, „Wczytaj ponownie z pliku”, „Otwórz plik”.
- Ig-10 można teraz oglądać: `tools/pdfr` (Windows.Data.Pdf) – `dotnet run --project tools/pdfr -- plik.pdf katalog od do skala` zapisuje strony jako PNG (narzędzie Read nie renderuje stron PDF bez pdftoppm). Znaki rozjazdów: s. 18–24, dalej (s. 25–40) kozły, wykolejnice, semafory, tarcze, wskaźniki – do etapu 5.
- Test `--test` (część „etap 4”): razem 203 sprawdzenia, **0 błędów** – mijanka na skośnej linii (63° i 250°) klikami: tor 1 + równoległy 2, łącznik 1/2 przeciągnięciem (tor łączący 76,200 m = wzór, styczny w PR1 i PR2, prosta pod 1:9, ŚR2 na osi toru 2), łącznik 3/4 w drugą stronę, rozjazd 5 do bocznicy przedłużonej narzędziem Tor; położenie zasadnicze, blokada wierzchołków, przesunięcie rozjazdu z łącznikiem, nachodzenie, zajęty numer, Del + Ctrl+Z, Rkpd i St na przecięciu z torem 3 (przejazdy na osiach torów, odchyłka 0,0 mm), zapis / odczyt bez zmian; baza symboli i okno katalogu. Zrzuty: `zrzuty/EdytorUT_symbole_ig10.png`, `EdytorUT_etap4_mijanka.png`, `EdytorUT_etap4_rozjazdy_blisko.png`, `EdytorUT_etap4_skrzyzowania.png`.
- Do decyzji / sprawdzenia przez właściciela: proporcje znaków (długość klina 8 mm, wysokość 2 mm, położenie numeru) – zał. 8 nie podaje wszystkich wymiarów, poprawki w `symbole_ig10.json`; wymiary a / b z kart katalogowych; rozjazdy łukowe (Rłj / Rłd) i podwójne (Rpj / Rpd) – kolejny krok.

**ZROBIONE 2026-10-16 – poprawka widoku po etapie 4** (właściciel: „wygląda nierealnie, zbyt szczegółowo”, wzór – UT z ISDR; `PLAN_EDYTOR_UT.md` § 5b):
- **Widok Schemat** (domyślny) i **Teren** (przycisk na pasku albo klawisz S). Schemat: oś stacji poziomo, w poprzek ×4 (lista ×1 / ×2 / ×4 / ×6), białe tło, bez siatki i linijek, podkład ukryty (pole „Podkład” – przygaszony i rozciągnięty). Teren: północ u góry, 1:1, ortofotomapa – do odrysowania. To tylko przeliczenie świat ↔ ekran w `UtView` (obrót o azymut osi, rozciągnięcie w poprzek; `ToScreen` / `ToWorld` / `ScreenMatrix` dla podkładów, podgląd na żywo dla prostokąta obejmującego obrócony ekran). Odległości przy trafianiu myszą liczone na ekranie, nie przez `mpp`. Narzędzia podkładów (obszar, kalibracja, przesuwanie) same przełączają na Teren.
- **Oś schematu:** `UtProject.AxisAz` (null – cięciwa najdłuższego toru, zwrócona na wschód; liczona przy przełączeniu, więc rysowanie nie obraca widoku), „Oś wg toru” (zaznaczony tor), „↻ 180°”, z cofaniem. Ustawienia widoku (`schemat`, `stretch`, `showBg`) w `view` pliku.
- **Wygląd jak w ISDR:** tor to jedna linia osi (bez szyn), 2 / 1,6 / 1,2 px (gz / gd / boczny), czarna; numer toru „• 1” nad osią, z dala od rozjazdów. Znaki Ig-10 2,2–3,6 px/mm (nie puchną przy przybliżaniu). Panele „Obszar” i „Podkłady” zwinięte, pasek w dwóch rzędach.
- **Znak rozjazdu jak w Ig-10 i na Semaforku** (właściciel: „gdzie tam jest trójkąt na rozjeździe?” – dawny klin stał w PR wzdłuż toru, poza kątem): `UtView.DrawWedge` – czarny klin w kącie między osią toru a odgałęzieniem, liczony z geometrii rozjazdu na ekranie (zgadza się w Terenie i w Schemacie ×4), po stronie kierunku zamkniętego (+ – przy odgałęzieniu, − – przy torze), od strony otwartej odstęp 0,5 mm, długość do KR, najwyżej 12 mm; zwrotnica nieuzależniona – biały z kreskowaniem; kreska początku 2 mm w PR, numer nad torem przed PR. Rkp / Rkpd wg zał. 8 s. 21: kreska w punkcie matematycznym, dwa kliny w kątach ostrych między torami (8 mm), numer; łuki przejazdów tylko w widoku Teren. Znaki rozjazdów w `symbole_ig10.json` i generator `tools/symbole_ig10.py` poprawione tak samo (podgląd w oknie katalogu). Zrzuty: `EdytorUT_schemat_blisko.png`, `EdytorUT_schemat_rkpd.png`, `EdytorUT_symbole_ig10.png`.
- **Odgałęzienie trzyma skos rozjazdu** (błąd: po przesunięciu rozjazdu albo trzeciego wierzchołka kąt przestawał być 1:N): `UtTurnout.BranchLen` – trzeci wierzchołek odgałęzienia zawsze na prostej skosu za ŚR (Sync), przeciąganie go tylko wzdłuż skosu, Del – odmowa; stare pliki – rzut wierzchołka na skos przy odczycie. „Sprawdź” rozjazdu: łuk za rozjazdem zaczynający się przed KR.
- **Tor odgałęźny równoległy od razu:** puszczenie myszy obok toru (≥ 2 m) – skos rozjazdu, łuk zwrotny R (pole R na pasku) i prosta równoległa w odstępie zaokrąglonym do 0,25 m (najmniej tyle, ile wymaga b + styczna łuku); bliżej toru – samo odgałęzienie do KR, na torze sąsiednim – łącznik (bez zmian).
- Test `--test`: razem 228 sprawdzeń, **0 błędów**; nowa część „widok schematu”: tory odrysowane w Terenie na linii 63°, w Schemacie tor poziomo (0,0000 px), międzytorze 5 m = 40 px przy ×4, ekran ↔ świat, zoom, przesuwanie; łączniki przeciągnięciem w schemacie; rozjazd 5 z torem odgałęźnym 5,000 m (skos 45,277 m = 5 / sin α, łuk zwrotny za KR), przesunięcie rozjazdu – skos dalej 1:9, przeciąganie wierzchołka na skosie, stary plik; łącznik na tor odgałęźny, bocznica 6 m; Teren ↔ Schemat, ↻ 180° z cofaniem, zapis / odczyt. Zrzuty: `zrzuty/EdytorUT_schemat.png`, `EdytorUT_schemat_blisko.png`. Selftest symulatora bez zmian: 25 z 26 / 4 z 4.

**ZROBIONE 2026-10-16 – budowa torów od nowa** (właściciel: „łączenie torów trzeba ogarnąć i to, jak łuki powstają”; `PLAN_EDYTOR_UT.md` § 5c):
- Narzędzie **Tor: tryb Prosta / Łuk** (przyciski „─ Prosta” / „◠ Łuk”, Tab). Każdy odcinek styczny do końca: prosta na przedłużeniu (długość z kursora albo z klawiatury), łuk o promieniu R z pola (z przejściowymi L) – strona i kąt za kursorem, z klawiatury długość łuku, Shift – koniec równoległy do osi schematu. Pierwszy odcinek nowego toru ma kierunek za kursorem; Alt – wolny kierunek (dawne załamanie z łukiem w wierzchołku). Wielobok liczy `Lcs.Model/UtBudowa.cs` (`Budowa.Straight` / `Arc` – wierzchołek V w odległości stycznej T, `Simplify` usuwa punkty na prostej; wierzchołek na skosie rozjazdu chroniony). Backspace cofa odcinek.
- **Łączenie:** klik na końcu innego toru łączy stycznie (`Budowa.Connect`): jeden łuk, gdy styczne przecinają się przed końcami (R z pola albo największy, jaki się mieści – komunikat), inaczej łuki odwrotne (wierzchołki w ¼ odległości). Tor wchłaniany, jeśli nie jest związany z rozjazdem ani skrzyżowaniem (kierunek toru, do którego się dochodzi, zostaje); start na końcu toru A i klik na końcu toru B łączy dwa istniejące tory. Podgląd z opisem przed kliknięciem.
- **Uchwyt łuku:** zaznaczony tor ma kółko w środku każdego łuku z etykietą R; przeciągnięcie zmienia promień (`Budowa.RadiusForMid` – bisekcja, środek łuku pod kursorem na dwusiecznej), z cofaniem.
- Test `--test`: razem 240 sprawdzeń, **0 błędów**; etap 3 przepisany: prosta 260 m – łuk 120 m z klawiatury (R 500, L 40) – prosta 150 m daje 3 wierzchołki i dokładne długości, łuk myszą i Backspace, przedłużenie o 80 m bez nowego wierzchołka, esowanie (zmiana rozstawu 6 m na 140 m) w jeden tor, łuk 90° z R zmniejszonym do 150 m, uchwyt łuku (R 150 → 90), Alt; w mijance bocznica od KR: prosta, łuk z Shift równoległy do toru, skos 1:9 zachowany. Zrzuty: `zrzuty/EdytorUT_tor_laczenie.png`, `EdytorUT_tor_uchwyt_luku.png`.

**Następny krok:** etap 5 – urządzenia na torze (semafory, tarcze, kozły, wykolejnice, liczniki osi / złącza, perony, przejazdy, kilometraż) ze znakami z bazy symboli Ig-10

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
