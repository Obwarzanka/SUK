# SUK – Symulator urządzeń komputerowych

Symulator Urządzeń Komputerowych urządzeń srk w Lokalnych Centrach Sterowania (LCS): pulpitu nastawczego i komputera zależności. Symulator jest ogólny – stacje, posterunki i obrazy pochodzą z wczytanego układu, a nie z kodu. Pierwszym obszarem jest **LCS Łódź Widzew**: Łódź Widzew (ŁW), Łódź Fabryczna (ŁF) i Łódź Niciarniana (ŁN). Program działa na Windows, w C#/WPF na .NET 10.

- Obsługa wg **Ie-20**: polecenia, przebiegi, blokady liniowe, postępowanie przy usterkach.
- Zobrazowanie wg **Ie-104**, wzorowane na pulpitach LCS (EbiScreen / SimRail).
- Osobny program **edytor układu torowego** (`EdytorUT.exe`) do rysowania układu w rzeczywistej geometrii (PUWG 1992). Etapy 0–7 są gotowe, dane z niego nie trafiają jeszcze do symulatora. Plan: `PLAN_EDYTOR_UT.md`.
- Osobny program **edytor zobrazowania** (`EdytorZobrazowania.exe`) do rysowania obrazów pulpitu przypiętych do UT (etapy Z1–Z6). Plan: `PLAN_EDYTOR_ZOBRAZOWANIA.md`.
- Osobny program **edytor taboru** (`EdytorTaboru.exe`) – pojazdy, charakterystyki F(v), składy, wykresy i przejazd próbny po profilu. Pociąg ze składem jedzie w symulatorze wg fizyki (pochylenia, łuki, sieć). Plan: `PLAN_EDYTOR_TABORU.md`.
- Osobny **kalkulator przechyłki** (`Przechylka.exe`).
- **Rzeczywisty rozkład jazdy** z GTFS (IC, Polregio, ŁKA) z obiegiem jednostek ŁKA.

> Symulator jest narzędziem szkoleniowym. Nie zastępuje regulaminu technicznego ani stanowiskowej instrukcji obsługi.

---

## Uruchomienie

Do uruchamiania programów okienkowych wymagany jest **.NET Desktop Runtime 10.0.13 lub nowsza poprawka 10.0.x**; host świata potrzebuje zwykłego runtime .NET 10.0.13. Kompilacja wymaga dokładnie **SDK 10.0.402** przypiętego przez `app/global.json`. Launchery mogą używać lokalnego SDK wraz z runtime bez instalacji systemowej.

- Dwuklik `uruchom.bat`. Skrypty `uruchom*.bat` najpierw budują aktualną wersję Release, a potem uruchamiają program i przekazują wszystkie argumenty. Przy błędzie terminal pozostaje otwarty, aby można było odczytać komunikat. SDK jest wybierane z katalogu `app`, więc obowiązuje `app/global.json` także przy uruchomieniu z innego katalogu.
- Launcher korzysta z lokalnego `.tools/dotnet/dotnet.exe`, jeżeli istnieje; w przeciwnym razie z systemowego `dotnet`. Lokalna kopia SDK to rozpakowane oficjalne archiwum Windows x64 z [witryny Microsoft](https://dotnet.microsoft.com/download/dotnet/10.0), bez zmiany instalacji systemowej. `.tools/` jest pomijany przez Git. Przy lokalnym SDK pakiety są przechowywane w `.tools/nuget/packages`, chyba że ustawiono własne `NUGET_PACKAGES`.
- Z wiersza poleceń:

```bash
uruchom.bat
```

- Po zmianach w kodzie zbuduj program w katalogu `app/LcsWidzew`, przy **zamkniętym** symulatorze (otwarty blokuje plik `.exe`):

```bash
dotnet build -c Release
```

Polecenie wykonuj z SDK zgodnym z `app/global.json`. Przy lokalnej kopii SDK możesz z katalogu głównego użyć `.tools\dotnet\dotnet.exe build app\LcsWidzew\LcsWidzew.csproj -c Release`. Testy automatyczne launcherów ustawiają `SUK_NO_PAUSE=1`, aby przy błędzie zwrócić kod zakończenia bez oczekiwania na klawisz.

Po zalogowaniu (dyżurny ruchu) wybierz scenariusz. Zakładki u góry przełączają obrazy:
- **0 – Obraz poglądowy**: cały obszar LCS, klik przenosi do obrazu stacji;
- **1 – ŁF + ŁN**;
- **2 – ŁW**.

---

## Host świata bez okna (S02.01)

`uruchom_host_swiata.bat --station D:\pelna\sciezka\uklad.json` buduje Release i uruchamia istniejący rdzeń bez WPF. Plik układu jest wymagany; host go nie zapisuje. Zakończenie: **Ctrl+C**. Bez `--ticks` proces działa do zatrzymania. Launcher wykonuje polecenie z katalogu `app`, dlatego używaj pełnych ścieżek wejścia i wyników.

Próba z katalogu głównego repo, po zbudowaniu hosta i utworzeniu katalogu wyników:

```powershell
.tools\dotnet\dotnet.exe app\Lcs.WorldHost\bin\Release\net10.0\Lcs.WorldHost.dll --station app\LcsWidzew\Testy\station_test.json --ticks 100 --report artifacts\swiat.json --logi artifacts\logi-swiata --logi-raport artifacts\logi-swiata.txt
```

Opcjonalnie: `--scenario podstawowy` (także `usterki`, `zima`, `manewry`, `ic_05` itd.), `--world-id local`, `--events artifacts\dziennik.txt`. `--ticks 100` oznacza 100 kroków po 0,1 s. Dla `ic_*` plik `rozklad.json` musi znajdować się obok testowej kopii układu. Wyniki eksportowane są po zatrzymaniu; katalogi wyników należy utworzyć wcześniej, a ścieżki wejścia i eksportów muszą być różne. Kod 0 oznacza poprawne zakończenie świata i eksportów; kod 2 – błąd, opisany w raporcie i zredagowanych logach.

Gotowość oznacza zakończoną inicjalizację i pierwszy udany krok. Raport zawiera epokę świata, liczbę kroków, metryki kolejki/cyklu, hashe znanych wejść, osobny stan diagnostyki i kompletność zatrzymania. Niedostępny zapis diagnostyki daje degradację logów i nie zmienia autonomicznej pracy rdzenia. Domyślne logi procesu: `%ProgramData%\SUK\Logs`; `--logi` ustawia lokalny katalog próby. Limity profilu serwera: 4096 rekordów / 8 MiB, 16 plików po 64 MiB / 7 dni, opróżnienie do 5 s. Diagnostyka nie stanowi trwałego audytu ani replay.

`Lcs.WorldHost` jest procesem lokalnym. Pulpit od **S02.02** korzysta z tego samego `WorldLoop` przez adapter `SingleplayerHost` działający w procesie okna. **S02.03** dostarcza profil usługi Windows, ACL i lokalny kanał health; rozdzielenie KZ i Ada pozostaje kolejnymi zadaniami. Szczegóły: [S02.01](SUK_plan_multiplayer/raporty/S02.01.md), [S02.02](SUK_plan_multiplayer/raporty/S02.02.md), [S02.03](SUK_plan_multiplayer/raporty/S02.03.md). Testy hosta, adaptera i profilu usługi: `dotnet test --project app/Lcs.WorldHost.Testy/Lcs.WorldHost.Testy.csproj -c Release`.

### Profil usługi i kontrola zdrowia (S02.03)

[Generator planu usługi](deploy/windows/WorldHost/README.md) przygotowuje konkretny profil, plan praw i skrypt instalacji do przeglądu. Domyślnie niczego nie instaluje. Konto docelowe: `NT SERVICE\SUK.WorldHost`, bez hasła i uprawnień administratora. Dane są oddzielone od binariów: `Inputs` i `Config` tylko do odczytu, `Logs` i `State` do zapisu konta usługi. Uruchomienie przez SCM wymaga tego konta oraz zatwierdzonego profilu. Konto profilu i opcjonalny odbiorca health (`-HealthClientSid`) muszą być pojedynczym kontem (`S-1-5-21-…`, RID od 1000) albo SID konkretnej usługi (`S-1-5-80-…`); grupy, np. Users czy Domain Users, są odrzucane. Instalacja, uruchomienie usługi i reboot są osobnymi operacjami operatora.

```powershell
Lcs.WorldHost.exe --service-profile "C:\ProgramData\SUK\WorldHost\local\Config\profile.json"
Lcs.WorldHost.exe --health "C:\ProgramData\SUK\WorldHost\local\Config\profile.json"
```

Pierwsze polecenie działa także jako konsolowa próba przygotowanego profilu; nadal sprawdza konto procesu, ACL, właścicieli i ścieżki. Kanał health jest lokalny i tylko do odczytu. Sprawdza tożsamość/PID serwera oraz świeżość zakończonych ticków; nie przyjmuje poleceń do świata. Kod health **0** oznacza Ready, **3** — proces odpowiedział, lecz nie jest gotowy, **2** — błąd, odmowę lub brak odpowiedzi w 1 s. Samo istnienie procesu albo pliku `State\host.json` nie potwierdza gotowości.

Domyślny próg świeżości to 2 s, start 30 s, shutdown 10 s, miękki limit working set 1024 MiB. Przekroczenie limitu pamięci wycofuje Ready i żąda kontrolowanego stopu. Problemy diagnostyki lub wolne miejsce poniżej jej progu ograniczenia wycofują Ready, zachowując autonomiczny cykl świata. Profil nie wyłącza istniejących limitów i rotacji logów. Szczegóły backoff, ACL, runtime i ograniczeń testów są w instrukcji generatora oraz raporcie etapu.

### Gra jednoosobowa (S02.02)

Uruchom pulpit jak dotychczas przez `uruchom.bat`. Lokalna gra korzysta z plików układu i scenariusza; nie wymaga konta serwera ani Internetu. Dotychczasowe logowanie dyżurnego służy lokalnej sesji symulatora. Pauza i przyciski tempa działają na wspólnym cyklu o kroku 0,1 s. Polecenia w pauzie, również tuż po uruchomieniu przy czasie 0, otrzymują wynik bez przesuwania czasu.

Restart scenariusza tworzy nową epokę świata i zamyka okna instruktora, przebiegów oraz inne okna związane z poprzednią sesją. Okno „Stanowiska i sieć” odświeża przyciski dla nowej sesji. Przyłączenie do serwera zamyka lokalny świat, a „Rozłącz” tworzy nową grę jednoosobową. Logi adaptera mają moduł `Singleplayer.Session`, kody `SP.*` i identyfikatory 6101–6104, bez treści poleceń i nazw operatorów. Diagnostyka nie zastępuje dziennika zdarzeń symulacji.

## Obsługa pulpitu

### Przebiegi myszą
- Klik na sygnalizatorze początkowym, potem na końcu przebiegu. Końcem może być sygnalizator, strzałka szlaku, kozioł albo PK.
- **Ctrl+klik** – punkt pośredni (przebieg wariantowy).
- **Esc** – rezygnacja.
- **PPM** na obiekcie – menu poleceń.

### Polecenia z klawiatury
Polecenia wpisujesz w polu „Polecenie>”, w postaci `obiekt(y) KOD`, np. `E S1 Pociąg`, `Tm24 E3 Manewr`, `13 Minus`, `1A Wbl`. `POMOC` wyświetla pełną listę.

| Kod | Znaczenie |
|---|---|
| `Pociąg` / `Manewr` | przebieg pociągowy / manewrowy |
| `Stój` | sygnał zabraniający |
| `SZ` / `oSZ` | sygnał zastępczy / odwołanie |
| `ZDP`, `ZDM`, `ZCZ`, `oZCZ` | zwolnienia doraźne przebiegów |
| `Plus` / `Minus`, `PlusBZ` / `MinBZ` | przestawianie zwrotnic (także z bocznikowaniem zajętości) |
| `Stop` / `oStop` | zastopowanie sygnalizatora/zwrotnicy, zamknięcie wyjazdu na szlak |
| `Zmk` / `oZmk` | zamknięcie ruchowe toru/zwrotnicy |
| `KSR`, `ZeroLO` | kasowanie rozprucia, zerowanie licznika osi |
| `Wbl`, `Pzk`, `oWbl`, `Zwbl`, `AZK`, `Bkon` | blokada liniowa: kierunek, pozwolenie, zwolnienie, awaryjna zmiana, blok końcowy (PBL) |
| `NN` / `oNN`, `AGR`, `KW` | napięcie nastawcze, agregat, potwierdzenie alarmów |

Szlak wewnętrzny można wskazać oznaczeniem z obrazu dowolnej stacji. Na przykład `1G Wbl` ustawia kierunek wyjazdu od strony stacji, na której szlak nazywa się 1G.

### Zobrazowanie
- **Odcinki:**
  - zielony – przebieg pociągowy;
  - żółty – przebieg manewrowy lub droga ochronna;
  - czerwony – zajętość;
  - różowy – zwalnianie czasowe.
- **Zwrotnice:** nieczynne odgałęzienie jest szare na całej długości łącznika.
- **Blokada liniowa:** strzałka w osi toru tuż za semaforem wjazdowym.
  - żółta – ustawiony kierunek;
  - szara dwustronna – brak kierunku;
  - migający grot – żądanie pozwolenia;
  - zielona kreska nad strzałką – szlak wolny i żaden przebieg na niego nie jest nastawiony.
- **Szlak wewnętrzny** (między stacjami LCS) ma strzałkę przy każdej stacji, każdą z własnym napisem (np. 1A / 1G). Obie zawsze wskazują ten sam kierunek.
- Na szlakach SBL widać numery sygnalizatorów odstępowych. Na szlakach bez odstępów jest oznaczenie szlaku (np. it1Z), za strzałką.
- **Przycisk „Opisy”** (pasek górny, obok „Dopasuj obraz”): pokazuje i chowa opisy odcinków wyświetlane na żądanie dyżurnego (Ie-104 § 5 ust. 2; w edytorze zobrazowania ustawienie „Opisy odcinków na pulpicie: na żądanie”). Gdy są włączone, przycisk jest podświetlony. Numery torów i nazwy posterunków są widoczne zawsze.
- **Skrzyżowanie torów:** przez krzyż jedzie się tylko na wprost. **Rozjazd krzyżowy podwójny** to dwie zwrotnice zwrócone ostrzami (np. 3ab, 3cd – Ie-104 § 8 pkt 9). **Rozjazd krzyżowy pojedynczy** wygląda i działa tak samo, ale ma jeden przejazd: przebiegu przez brakujący przejazd nie ma w tablicy przebiegów.

---

### Plan stacji (podgląd terenu)
Przycisk **„Plan stacji”** otwiera osobne okno z planem schematycznym całego węzła na żywo: pociągi (numer, rodzaj, prędkość, czoło na czerwono), semafory jako głowice ze światłami, zwrotnice w aktualnym położeniu (nieczynne odgałęzienie – szare). Na dole: **śledzenie składu** (widok jedzie za wybranym pociągiem), wybór **posterunku** i „Cały węzeł”. To podgląd dla instruktora i do nauki – dyżurny pracuje na pulpicie. Działa tam, gdzie pracuje komputer zależności (gra jednoosobowa, serwer).

### Tablica przebiegów i zależności specjalne
Przycisk **„Tablica przebiegów”** pokazuje przebiegi wyznaczone z układu: zwrotnice, odcinki, drogę ochronną i zależności specjalne (gwiazdka `*` – z zależności specjalnych). Na komputerze z komputerem zależności (gra jednoosobowa, serwer) pod tablicą jest panel **zależności specjalnych** wybranego przebiegu. Zależności wpisuje człowiek, a program sam niczego nie dopisuje:
- **Droga ochronna – warianty**: odcinki i zwrotnice z położeniem oddzielone spacją, np. `MT:Z1/2 MT:2+`. Kolejność wariantów oznacza pierwszeństwo: komputer zależności bierze pierwszy wariant możliwy do nastawienia. Zwrotnice drogi ochronnej są przestawiane i utwierdzane, a polecenia `Plus` / `Minus` dla nich są odrzucane. **„bez drogi ochronnej”** – przebieg bez drogi ochronnej. Bez wpisu droga ochronna pochodzi z układu: jest to pierwszy odcinek za końcem przebiegu, bez zwrotnic.
- **Przebiegi sprzeczne**: dodatkowe wykluczenia, ponad wynikające z układu. Działają obustronnie.
- **Przebiegi dopuszczone jednocześnie**: przebiegi, które można nastawić razem mimo wspólnego odcinka (np. na mijance B→D2 z drogą ochronną do kozła i A→C1). Odcinek jest wtedy utwierdzony w obu przebiegach. Pulpit barwi każdy tor kolorem jego przebiegu.
- **Ochrona boczna – dodaj / usuń**: zwrotnica z położeniem (`MT:4+`), wykolejnica albo sygnalizator „Stój”.
- **Warunki dodatkowe**: `MT:3+` (zwrotnica w położeniu), `MT:T1 wolny`, `MT:W1 założona` / `zdjęta`, `MT:B stój`.
- **Prędkość przebiegu [km/h]** (tylko przebieg pociągowy): 40, 50, 60, 70, 80, 100, 120 albo 130. „z układu” usuwa wpis. Obok widać prędkość z torów i zwrotnic oraz sygnał semafora początkowego. Ustalona prędkość zastępuje wyliczoną, także gdy jest wyższa (np. zwrotnica na bok 40, przebieg 60). W tablicy taki przebieg ma w kolumnie V gwiazdkę (np. `60*`). Bez wpisu obowiązuje `routeRules[].vmax` z pliku układu, jeśli jest, a dalej prędkość wyliczona.
  - **Sygnał wg Ie-1:** semafor daje 40 km/h, 60 km/h z pasem pomarańczowym albo 100 km/h z pasem zielonym. Wyższą prędkość (50, 70, 80, 120, 130) pokazuje wskaźnik **W21** razem z tym sygnałem, np. 70 → sygnał 60 (S10a…S13a) + W21 „7”, 120 → sygnał 100 (S6…S9) + W21 „12”. W21 świetlny pokazuje liczbę z prędkości przebiegu, a stała tablica zawsze swoją cyfrę. Pasy i W21 ustawia się przy semaforze w edytorze UT (W21 „świetlny” albo cyfra tablicy) i przenosi „Do symulatora…”.
  - Jeśli semafor nie może zasygnalizować wybranej prędkości (brak pasa albo W21), „Zapisz zależności” i „Sprawdź plik” ostrzegają, a semafor pokazuje najbliższą niższą prędkość, którą ma (np. 40 zamiast 60). Pociąg nie jedzie szybciej niż sygnał. Plik układu zapisany przed tą zmianą (bez pasów i W21) działa jak dotąd: klasa 40 / 60 / 100 bez W21.
- **„tylko ten wariant”** – wpis dotyczy tylko wariantu o tych położeniach zwrotnic. Bez zaznaczenia wpis dotyczy wszystkich wariantów przebiegu.

Pole wyboru pod każdą listą podpowiada obiekty układu. Można też wpisać obiekt ręcznie, a potem kliknąć „Dodaj”. **„Zapisz zależności”** najpierw sprawdza dane: nieistniejące obiekty, przebieg jednocześnie sprzeczny i dopuszczony, zwrotnicę w dwóch położeniach. Potem zapisuje plik **`<układ>.zaleznosci.json`** obok pliku układu (poprzednia wersja trafia do `.bak`) i przelicza tablicę przebiegów. Przeliczenie wymaga, żeby żaden przebieg nie był nastawiony; w przeciwnym razie zwolnij przebiegi i naciśnij „Przelicz tablicę”. Plik układu ani `station.json` się nie zmieniają, a eksport „Do symulatora…” nie nadpisuje pliku zależności. Stanowiska sieciowe widzą tablicę tylko do odczytu i dostają nową tablicę dopiero po ponownym połączeniu.

## Kalkulator przechyłki

Osobny program: `uruchom_przechylka.bat` albo `app/Lcs.Przechylka/bin/Release/net10.0-windows/Przechylka.exe`. Wpisujesz prędkość V, promień R i przechyłkę h. Puste h oznacza, że program sam zaproponuje przechyłkę.

Prześwit toru jest w polu i wynosi domyślnie 1435 mm. Przechyłka działa na rozstawie osi główek szyn s = prześwit + szerokość główki (1435 + 65 = 1500 mm), z którego bierze się znany współczynnik 11,8. Bez h program proponuje przechyłkę równoważącą (co 5 mm, najwyżej h_max) i podaje najmniejszą dopuszczalną.

Program podaje przechyłkę równoważącą h_r = s·V²/(9,81·3,6²·R) (≈ 11,8·V²/R), niedobór h_n, przyspieszenie niezrównoważone a_n i długość krzywej przejściowej L. L to największa z trzech wartości: z rampy przechyłkowej (1:n, n = k·V), z przyrostu przyspieszenia ψ i ze zmiany przechyłki f. Program pokazuje, który warunek decyduje, oraz największą prędkość dla danego R i h. Wartości dopuszczalne (h_max, niedobór, ψ, f, k) są w polach do zmiany. Domyślne są przykładowe, więc wpisz obowiązujące z przepisów. Test: `Przechylka.exe --test wynik.txt [zrzut.png]`.

## Edytor układu torowego (UT)

Osobny program: `uruchom_edytor_ut.bat` albo `app/Lcs.EdytorUT/bin/Release/net10.0-windows/EdytorUT.exe [plik.ut.json]`. Plan budowy i decyzje są w `PLAN_EDYTOR_UT.md`.

Dotychczasowe edytory (układ i zobrazowanie wbudowane w symulator) zostały usunięte 2026-10-16. Są w historii git (commit `d2c7431`). Symulator nadal wczytuje istniejące pliki `station.json`.

Stan (etapy 1–7 i Z0 – posterunki, szlaki, funkcja semafora; edytor zobrazowania w budowie):
- **Układ współrzędnych PUWG 1992** (EPSG:2180): X na północ, Y na wschód, Z to wysokość, wszystko w metrach.
- **Siatka:** pole „Siatka” na pasku działa w obu widokach. W Terenie siatka idzie po współrzędnych PUWG, w Schemacie wzdłuż i w poprzek osi stacji. Przełącznik jest osobny dla każdego widoku, a w Schemacie siatka jest domyślnie wyłączona.
- **Widok Schemat / Teren** (przyciski na pasku, klawisz S). Schemat (domyślny) wygląda jak UT w ISDR: oś stacji poziomo, w poprzek rozciągnięte ×4 (lista obok), białe tło, sama oś toru. Podkład jest ukryty, pole „Podkład” go pokazuje. „Oś wg toru” kładzie poziomo zaznaczony tor, a „↻ 180°” obraca schemat. Teren: północ u góry, 1:1, ortofotomapa – do odrysowania torów ze zdjęcia. Rysować i edytować można w obu widokach, zapis jest zawsze w metrach terenu.
- **Płótno:** kółko myszy przybliża wokół kursora (od 2 mm do 2 km na piksel), przeciąganie lewym albo środkowym przyciskiem przesuwa widok. Do tego siatka o kroku 1 / 2 / 5 · 10ᵏ m, linijki ze współrzędnymi, podziałka i strzałka północy.
- **Punkt:** w polu wpisz współrzędne z Google Maps (np. `51.7631, 19.5438`) albo `X Y` w PUWG 1992, potem Enter albo „Idź do”.
- **Pasek stanu:** X, Y i szerokość / długość geograficzna pod kursorem, rozdzielczość (1 px = … m), przybliżona skala na ekranie.
- **Panel obszaru:** nazwa, skala arkusza wg Ig-10 § 3 – „1:2000 wzdłuż, 1:500 w poprzek” albo 1:4000 / 1:1000 (wielkość znaków i wydruk, nie zmienia geometrii), uwagi.
- **Plik:** Nowy, Otwórz, Zapisz, Zapisz jako (`*.ut.json`, domyślnie w katalogu `projekty_ut`). Poprzednia wersja zostaje jako `.bak`, zapamiętywany jest też ostatni widok. Cofnij / Ponów: Ctrl+Z, Ctrl+Y.

- **Podgląd na żywo** (panel „Podkłady”): ortofotomapa Geoportalu dla bieżącego widoku albo zapasowo zdjęcia Esri / mapa OSM. Nie zapisuje się w projekcie.
- **Linie kolejowe (OpenRailwayMap) na wierzchu** (pole pod podglądem): przezroczysta warstwa z osiami torów i numerami linii nad zdjęciem i nad podkładami projektu – do odrysowania. Kafelki zapamiętują się na dysku (`%LOCALAPPDATA%\LcsEdytorUT\kafelki`, 30 dni), przy przybliżaniu od razu widać powiększony poprzedni obraz, pobierane są najpierw kafelki przy środku widoku.
- **Ortofotomapa do projektu – „🗺 Mapka kafelków…”:** mapa z liniami kolejowymi i siatką kafelków. Wybierz rozdzielczość (5 cm widać podkłady, 10 cm szyny) i zaznacz kafelki lewym przyciskiem, przeciągając wzdłuż linii (zaczęcie na zaznaczonym odznacza). Prawy przycisk przesuwa mapę. Potem „Pobierz zaznaczone”, a pobrane zrobią się zielone. Na płótnie można też „Zaznacz obszar…” (prostokąt) albo „Cały widok”. Wszystkie pobrania jednej rozdzielczości składają się w jedną warstwę w stałej siatce, w `podklady/orto_Ncm/` obok projektu, bez dubli. Nieudane można „Dopobrać”. Geoportal czasem oddaje pusty biały obraz: program to wykrywa i pobiera kafelek jeszcze raz, a dla starszych pobrań jest przycisk „Napraw puste kafelki”.
- **Grafika z pliku** (skan planu, mapa): z plikiem world (`.pgw` / `.jgw` / `.tfw`) ląduje od razu na miejscu. Bez niego użyj „Kalibruj punktami”: klik na grafice, potem klik w miejscu, gdzie ten punkt naprawdę jest na ortofotomapie. Wystarczą 2 punkty, lepiej 3–4 daleko od siebie, panel pokaże odchyłki. Do tego „Przesuń”, obrót i rozmiar piksela z klawiatury.
- **Warstwy:** widoczność, krycie, kolejność, usuwanie.
- **Zwrotnica (Z), Sygnalizator (S), Wykolejnica (W), Kozioł (K):** program bierze obiekt wybrany w panelu UT albo następny jeszcze nieumieszczony (pokazuje go na pasku stanu). Kliknij na linii jego toru obrazu, w miejscu, gdzie ma stać. Klik na innym torze albo na kawałku toru bez jego km – odmowa z podaniem właściwego toru i km. Zwrotnica przyciąga się do miejsca, gdzie odchodzi narysowany tor odgałęźny, kozioł – do końca toru. Kierunek sygnalizatora (w którą stronę ostrze) i strona numeru zwrotnicy wynikają z UT i z rysunku – nie ustawia się ich. Symbole wyglądają jak na pulpicie w stanie zasadniczym (Ie-104 § 8): semafor – trójkąt, z grotem, gdy ma światło białe (manewry), wjazdowy z małym trójkątem końca przebiegu, tarcza manewrowa – grot, odstępowy – sam numer, zwrotnica – przerwa na nieczynnym ramieniu, numer i „+” przy położeniu zasadniczym.
- **Przerwa (P):** licznik osi albo złącze izolowane z UT – kliknij na linii jego toru; na obrazie przerwa w linii (granica odcinków), w edytorze z kółkiem.
- **Opis odcinka (O):** kliknij w miejscu opisu, przy torze odcinka (do 48 j., zwykle nad torem – Ie-104 § 8 ust. 1). Opis przeciągasz myszą. W panelu obrazu „Opisy odcinków na pulpicie”: na żądanie dyżurnego (Ie-104 § 5 ust. 2), zawsze albo nie.
- **Numer toru (N):** kliknij na linii toru – numer w przerwie linii, jak na pulpicie. Na pulpicie numer staje w tym miejscu.
- **Peron:** przeciągnij prostokąt peronu (peron UT z panelu albo następny); podwójna ramka z numerem. Przesuwasz go myszą we Wskaż.
- **Przejazd:** kliknij na linii toru w miejscu przejazdu – różowa linia z „Y” na końcach; w panelu zasięg linii w górę i w dół (np. przez sąsiedni tor).
- **Szlak (B):** strzałka blokady – kliknij na torze za semaforem wjazdowym, od strony szlaku (po stronie stacji program odmówi). Przy szlaku wewnętrznym stawiasz strzałkę przy każdym końcu. Na pulpicie strzałka stoi w tym miejscu.
- **Napis (X):** kliknij w miejscu napisu i wpisz tekst (podpowiedzi – posterunki sąsiednie z UT); rodzaj: nazwa posterunku sąsiedniego, nastawnia (ikona i skrót w cudzysłowie) albo dowolny.
- **Bez kontroli niezajętości:** pole w panelu toru obrazu – tor linią przerywaną (Ie-104 § 8 ust. 1), także na pulpicie.
- **Opisy:** domyślnie jak na pulpicie. We Wskaż opis przeciągasz myszą; panel symbolu ma „Opis w miejscu domyślnym” i „Zdejmij z obrazu” (Del). Przesunięcie toru strzałkami przesuwa też jego symbole.
- **✔ Sprawdź (F7):** lista w panelu z prawej – obiekty UT, których nie ma na obrazie, przypięcia do obiektów usuniętych z UT, symbole poza swoim torem, kolejność symboli wzdłuż toru inna niż km w UT. Klik na pozycji zaznacza obiekt.
- **Miarka (M):** długość łamanej **w terenie**, czyli z poprawką skali PUWG 1992 (na południku 19° układ skraca o 70 cm/km). Backspace cofa punkt, PPM czyści.

- **Tor (T):** budujesz tor odcinkami stycznymi, jak w ISDR. Tryb wybierasz przyciskiem „─ Prosta” / „◠ Łuk” albo klawiszem Tab.
  - Pierwszy klik to początek toru albo koniec istniejącego toru (wtedy go przedłużasz). Pierwsza prosta nowego toru idzie w stronę kursora.
  - Dopóki tor ma jedną prostą, każde kolejne kliknięcie przestawia jej koniec. Kliknij więc daleko, na osi toru na zdjęciu: im dalej, tym dokładniejszy kierunek.
  - **Prosta** biegnie na przedłużeniu końca. Długość wynika z kursora albo wpisujesz ją z klawiatury i zatwierdzasz Enterem. Alt daje wolny kierunek: powstaje załamanie z łukiem R w wierzchołku.
  - **Łuk** ma promień R i krzywe przejściowe L z pól na pasku. Stronę i kąt wyznacza kursor, a z klawiatury wpisujesz długość łuku. Z Shift łuk kończy się równolegle do osi schematu.
  - **Łączenie:** kliknięcie na końcu innego toru łączy oba tory stycznie: jednym łukiem albo łukami odwrotnymi (esowanie). Gdy R się nie mieści, program bierze największy możliwy. Tory scalają się w jeden, chyba że drugi należy do rozjazdu. Start na końcu toru A i kliknięcie na końcu toru B łączy dwa gotowe tory.
  - Backspace cofa ostatni odcinek, Enter / PPM / dwuklik kończy, Esc rezygnuje, Ctrl wyłącza przyciąganie.
- **Równoległy (P):** tor równoległy odrysowany z podkładu. Kliknij tor wzorcowy, potem na zdjęciu oś nowego toru w miejscu jego początku. Odstęp liczy się z kliknięcia co 1 cm, z Shift co 0,25 m. Na koniec kliknij miejsce końca albo naciśnij Enter, wtedy tor biegnie do końca toru wzorcowego. Łuki stają się współśrodkowe.
- **Łuk od–do (K):** kliknij na torze w miejscu, gdzie zaczyna się łuk (np. na zdjęciu kończy się prosta), a potem tam, gdzie się kończy. Program wstawi łuk o pasującym promieniu, a wierzchołki pośrednie, np. z odrysowania łuku kilkoma kliknięciami, usunie. Przed drugim kliknięciem widać podgląd z R.
- **Łuk z punktów (J):** dokładny łuk ze zdjęcia. Kliknij łuk toru (albo przy załamaniu), potem punkty na osi toru na zdjęciu: na łuku i kawałek na prostych przed nim i za nim (razem co najmniej 4, im więcej i szerzej, tym dokładniej). Program sam dobiera położenie wierzchołka (czyli kierunki obu prostych), promień i po Tab krzywe przejściowe L tak, żeby tor przechodził jak najbliżej wszystkich punktów. Końce toru zostają na miejscu. Przy każdym punkcie widać odchyłkę w cm, a kropkowana linia pokazuje, gdzie przesunie się wierzchołek. Klawisz W przełącza na proste stałe (dobiera się tylko R i L). Backspace usuwa ostatni punkt, Enter albo PPM zatwierdza, Esc zaczyna od nowa.
- **Łuk o zmiennym promieniu (łuk koszowy):** PPM na łuku – „Podziel łuk tutaj” (albo dwuklik na łuku we Wskaż) dzieli go w tym miejscu na dwa łuki, a „Podziel łuk na części” na 2, 3, 4 albo 6 równych części. Każda część ma swój wierzchołek i ten sam R, kształt toru się nie zmienia. Potem każdą część dostosujesz osobno: przeciągnij wierzchołek albo wpisz R w panelu. Możesz też użyć „Łuku z punktów” (J): klik na którejkolwiek części i punkty wzdłuż całego łuku, a program dopasuje wszystkie części naraz, każdą z własnym promieniem.
- **Wygładź łuk** (PPM na łuku z kilku części): cała grupa łuków skręcających w tę samą stronę (z prostymi między nimi do 30 m) zamienia się na jeden łuk z krzywymi przejściowymi, jeden łuk bez nich albo dwa łuki. Skrajne proste zostają, a R i L dobierają się tak, żeby nowy łuk leżał jak najbliżej dotychczasowego kształtu. Komunikat podaje, o ile najwyżej się odsunął. Ctrl+Z cofa.
- **Wykres krzywizny** (przycisk „Krzywizna” na pasku): pod mapą krzywizna zaznaczonego toru wzdłuż km. Prosta to 0, łuk to poziomy odcinek (łuk w prawo w górę), przejściowa to ukośny odcinek, skok to pionowa kreska. Widać etykiety R i L, a czerwone trójkąty pokazują uwagi. Kółko myszy przybliża wykres, przeciąganie pustego miejsca go przesuwa. Kursor na mapie pokazuje km na wykresie i odwrotnie.
- **Trasa z elementów** (panel toru „Zamień na trasę z elementów” albo PPM na wykresie): tor jako ciąg elementów – prosta, przejściowa (klotoida), łuk. Pozwala na różne przejściowe na wejściu i wyjściu oraz przejściową między łukami (łuk koszowy bez skoku krzywizny). Edycja:
  - na wykresie: przeciągnięcie granicy (■) zmienia długości dwóch sąsiednich elementów, przeciągnięcie poziomu łuku (●) zmienia R przy tym samym kącie zwrotu;
  - PPM na wykresie: „Podziel element tutaj”, „Wstaw przejściową na granicy”, „Zamień na prostą / łuk”, „Usuń element”;
  - w panelu: tabela elementów z długością i R (+ w prawo, − w lewo; promienie przejściowej wynikają z sąsiadów);
  - „Łuk z punktów” (J) na takim torze dopasowuje długości i promienie elementów w miejscu klikniętych punktów.

  „Zamień na wielobok” wraca do wierzchołków. To przybliżenie: łuk koszowy z przejściową między łukami dostanie jeden R, a komunikat poda odchyłkę.
- **Łuk w wierzchołku (wielobok)**, po zaznaczeniu wierzchołka z łukiem: przejściowa na wyjściu L2 (inna niż L na wejściu), długość łuku albo styczna T z klawiatury (program dobierze R), kąt zwrotu, „zablokuj R” (dopasowania i uchwyt łuku go nie zmieniają).
- **Uchwyty na torze** (Wskaż, zaznaczony tor):
  - kwadracik w środku prostej przesuwa ją równolegle, a sąsiednie proste zachowują kierunek;
  - romb na początku albo końcu łuku przesuwa się wzdłuż prostej, a R dobiera się tak, żeby łuk zaczynał się albo kończył w tym miejscu.

  Wierzchołka nie trzeba widzieć.
- **Punkty z OpenStreetMap** (w „Łuku z punktów” klawisz O): program pobiera osie torów w widoku z OSM (Overpass) i dodaje jako punkty dopasowania węzły leżące do 8 m od toru. Rysujesz nadal ty, OSM daje tylko punkty.
- **Uwagi do łuków** (na wykresie i w „Sprawdź”):
  - skok krzywizny bez krzywej przejściowej (tory główne albo V > 40 km/h);
  - za krótka prosta między łukami (§ 36: tor główny V/2,5, nie mniej niż 20 m; pozostałe 10 m);
  - promień za mały dla Vmax;
  - przejściowa krótsza niż wynika z prędkości.
- **Przejściowe z prędkości** (przycisk w panelu toru): dla każdego łuku program liczy przechyłkę h i długość krzywej przejściowej L ze wzorów kalkulatora przechyłki (Vmax toru, R łuku, rozstaw szyn 1435 mm) i wpisuje je. Wierzchołek odsuwa się tak, żeby środek łuku został na miejscu, czyli na zdjęciu. Ostrzega, gdy promień jest za mały dla tej prędkości albo gdy L się nie mieści. Wartości dopuszczalne są przykładowe, jak w kalkulatorze. Najpierw wpisz Vmax toru.
- **Usunięcie kawałka toru:** PPM na torze we Wskaż otwiera menu: „Usuń ten kawałek” usuwa prostą albo cały łuk pod kursorem, a tor rozcina się na dwa (albo skraca, gdy to koniec). „Rozetnij tor tutaj” daje dwa tory stykające się w tym miejscu – także w łuku (łuk dzieli się na dwa o tym samym R, kształt się nie zmienia; łuku z krzywymi przejściowymi nie rozetniesz w środku). Wszystko, co leży za cięciem (rozjazdy, urządzenia, odcinki, perony, przejazdy, punkty km, profil), przechodzi na nowy tor z kilometrażem od jego początku. Co leżało w usuniętym kawałku, znika. Program nie tnie w obrębie rozjazdu ani toru łączącego łącznika.
- **Zaznaczanie kilku elementów (Wskaż):** Shift + klik (albo Ctrl + klik) dodaje element do zaznaczenia albo go zdejmuje: tor, rozjazd, skrzyżowanie, urządzenie, odcinek, peron, przejazd, punkt km. Zwykły klik na torze, potem Shift + klik na drugim – zaznaczone oba. Shift + przeciągnięcie zaznacza prostokątem wszystko, co leży w nim w całości. Panel pokazuje listę zaznaczonych. Del albo „Usuń zaznaczone” usuwa wszystkie naraz, a jedno Ctrl+Z je przywraca. Esc albo zwykły klik kończy zaznaczenie.
- **Łączenie torów:** zaznacz dwa albo więcej torów (Shift + klik) i wciśnij Ctrl+J, „Połącz tory” w panelu albo „Połącz zaznaczone tory” z menu PPM. Program łączy po kolei najbliższe końce: stykające się – wprost (przy załamaniu daje łuk R z paska w miejscu styku), oddalone – stycznie łukiem R z paska albo łukami odwrotnymi, gdy tory są przesunięte (R zmniejsza się, gdy większy się nie mieści). Zostaje tor, do którego się dołącza (numer, rozjazdy, urządzenia). Wszystko, co leżało na drugim, przechodzi na tor wynikowy z kilometrażem od jego początku. Gdy tory mają przeciwne kierunki, odwraca się ten, na którym nic nie leży. Nie łączy się torów z elementów, torów łączących łączników ani początku odgałęzienia rozjazdu. Jedno Ctrl+Z cofa łączenie.
- **Uchwyt łuku:** zaznaczony tor ma kółko w środku każdego łuku. Przeciągnięcie zmienia promień, a R widać na żywo. Przeciągnięcie do wierzchołka usuwa łuk (R 0). Załamanie bez łuku ma kreskowane kółko po wewnętrznej stronie kąta: odciągnięcie go tworzy łuk.
- **R 0 na pasku:** w trybie Łuk łuk przechodzi przez punkt pod kursorem, a promień wynika z miejsca kliknięcia. Z Alt powstaje załamanie bez łuku.
- **Edycja:** klik zaznacza tor i otwiera panel z numerem, kategorią, elektryfikacją, Vmax, długością w terenie, listą elementów oraz R i L każdego wierzchołka. Wierzchołki przeciąga się myszą. Dwuklik na boku dodaje wierzchołek, Del usuwa wierzchołek albo tor. W panelu jest też „Tor równoległy” (międzytorze, strona) i „Odwróć kierunek”. Błędy geometrii, np. łuk nie mieści się między wierzchołkami, widać jako czerwone kółko i opis w panelu.

- **Rozjazd (R):** typ wybierasz z listy na pasku (katalog w `Dane/rozjazdy.json`). Wciśnij na torze w miejscu początku rozjazdu (PR) i przeciągnij w stronę odgałęzienia. Program zapyta o numer. Jeśli puścisz na torze sąsiednim, powstanie łącznik: dwa rozjazdy i tor łączący. Tory mogą odbiegać od równoległości do 3°. Dwa osobno postawione rozjazdy też da się złączyć w łącznik: narzędziem Tor zacznij na końcu odgałęzienia jednego i kliknij odgałęzienie drugiego. Drugi rozjazd przesunie się tak, żeby skosy się zeszły. Jeśli puścisz obok toru (co najmniej 2 m od niego), powstanie od razu tor odgałęźny: skos rozjazdu, łuk zwrotny o promieniu R z pola na pasku i prosta równoległa w odstępie, w którym puściłeś (co 0,25 m). Puszczenie blisko toru daje samo odgałęzienie do końca rozjazdu. Odgałęzienie przedłużasz narzędziem Tor od jego końca. Wierzchołek za rozjazdem przesuwa się tylko wzdłuż skosu, więc kąt odgałęzienia zawsze zgadza się z typem rozjazdu. Rozjazd krzyżowy (Rkp, Rkpd) i skrzyżowanie (St) wstawiasz tak samo: wciśnij na torze w miejscu środka i przeciągnij w stronę toru przecinającego. Powstanie on pod kątem typu, np. 1:9, a dalej przedłużasz go narzędziem Tor od końców. Można też kliknąć na istniejącym przecięciu dwóch torów.
- **Przesuwanie:** rozjazd przeciągasz myszą od razu, bez wcześniejszego zaznaczania. Łącznik jedzie w całości, nawet gdy chwycisz go za drugi rozjazd. Strzałki przesuwają zaznaczony rozjazd wzdłuż toru o 0,1 m (z Shift o 1 m). Zaznaczony tor albo wierzchołek strzałki przesuwają o 5 cm (z Shift o 0,5 m), w Schemacie góra / dół zmienia międzytorze. Ctrl + przeciągnięcie zaznaczonego toru przesuwa cały tor razem z rozjazdami. PPM na rozjeździe otwiera menu: strona, odwrócenie, położenie zasadnicze, zwrotnica ręczna, usunięcie.
- **Rozjazd w panelu:** numer (Enter – zastosuj; zmiana też z menu PPM „Zmień numer…”, w łączniku oba numery naraz; numer zajęty w posterunku przez inny rozjazd – pytanie „Zamienić numery?”, tamten dostaje zwolniony numer, Ctrl+Z cofa obie zmiany naraz; wpis w polu bez Enter jest przyjmowany także przed zapisem, „Cofnij”, „Ponów”, „Sprawdź” i zamknięciem), typ, położenie zasadnicze, zwrotnica ręczna, km PR, zmiana strony i odwrócenie, uwagi (np. nachodzi na inny), wstawki międzyrozjazdowe (w łączniku: długość między końcami rozjazdów, najmniejsza dopuszczalna v/10, nie mniej niż 6 m, i ψ), przyciski „Łącznik z rozjazdem N” (te same pozycje są w menu PPM). Taki łącznik powstaje też wtedy, gdy odgałęzienie było już dociągnięte do drugiego rozjazdu narzędziem Tor.
- **Rozjazd łukowy:** rozjazd postawiony na łuku toru sam się wygina. Odgałęzienie do środka łuku daje rozjazd jednostronny (Rłj), na zewnątrz dwustronny (Rłd). Panel podaje promienie obu torów i prędkość na zwrot. Po zmianie łuku toru rozjazd od razu się dopasowuje. Rozjazd nie może leżeć na krzywej przejściowej ani na granicy prostej i łuku – uwaga podaje, o ile go przesunąć w każdą stronę. Szybciej: przycisk **„Popraw łuk”** w panelu rozjazdu (i w menu PPM) przesuwa granicę łuku poza rozjazd. Łuk zaczyna się wtedy za rozjazdem albo rozjazd leży w całości na łuku. Wierzchołek i proste zostają, zmienia się tylko R, a przycisk pokazuje R przed i po. Na działanie symulatora ta uwaga nie wpływa: to zgodność z rzeczywistym układem.
- **Łącznik na łuku:** na torach w łuku skosy obu rozjazdów łącznika rozchodzą się (tory obracają się na jego długości). Program wstawia wtedy we wstawkę łuk wyrównujący (największy, jaki się mieści, w środku wstawki) i ustawia drugi rozjazd symetrycznie. Panel rozjazdu podaje jego promień i kąt. Uwaga pojawia się dopiero, gdy łuk wyrównujący ma mniej niż 140 m albo skosy rozchodzą się o więcej niż 3°.
- **Dopasuj promienie** (panel toru, gdy łuk się nie mieści): zmniejsza R tylko w tych łukach, nie poniżej 140 m. We „Wskaż” rozjazd przeciąga się wzdłuż toru, a łącznik idzie za nim.
- **Urządzenie (U):** rodzaj wybierasz z listy na pasku: semafor świetlny, tarcza ostrzegawcza, tarcza manewrowa, wykolejnica, kozioł oporowy, licznik osi, złącze izolowane, ukres. Wciśnij na torze w miejscu urządzenia i przeciągnij w stronę jazdy, której dotyczy, po stronie, po której stoi. Kozioł przyciąga się do końca toru. Licznik, złącze i ukres stawiasz kliknięciem. Program podpowiada nazwę (A, B…, Tm1, Wk1, lo1). We Wskaż urządzenie przeciągasz wzdłuż toru albo przesuwasz strzałkami. PPM daje menu (strona, kierunek, usuń), a w panelu są nazwa i km.
- **Konfiguracja semafora i tarczy** (panel, znaki wg BSK ISDR „Oznaczenia urządzeń srk”): układ gotowy (2-, 3-, 4- i 5-komorowy, tarcza ostrzegawcza, tarcza manewrowa karzełkowa albo wysoka) albo komory po kolei od masztu. Każda komora ma kolor (czerwona, zielona, pomarańczowa, biała, niebieska) i znacznik „pulsuje”, kolejność zmieniasz ↑ ↓, jest też ✕ i „+ komora”. Komory można też wpisać tekstem, np. `B*PCZ*`. Ustawiasz ponadto, która komora świeci w stanie zasadniczym, oraz mocowanie: na maszcie (słup), na wysięgniku, na bramce albo karzełkowy. Semafor może też mieć pas pomarańczowy, pas zielony, W24 i W21 z cyfrą prędkości (np. 6 = 60 km/h). Wszystko to jest w polu „Konfiguracja semafora”.
- **Semafory na bramce** (mocowanie „na bramce”): rysunek jak na planach schematycznych (Semaforek, Łódź Widzew – L3 / L5, S1 / S3, D / E). Semafory na bramce w tym samym miejscu (do 8 m wzdłuż toru, ten sam kierunek) tworzą jedną bramkę: belkę w poprzek wszystkich ich torów, na obu końcach słup w stronę komór zakończony kreską stopy. Semafor na bramce nie ma własnej stopy, tylko wisi na belce. Przy belce jest komora górna i kreska W24, a na drugim końcu W21, pasy świetlne i nazwa.
- **Łączenie bramek i wysięgników**: w panelu sygnalizatora (pole „Wspólna konstrukcja”) albo w menu PPM wybierz „Połącz z…” i sygnalizator w pobliżu (ten sam kierunek, do 60 m) – zawiśnie na tej samej belce, a dołączony przesunie się na swoim torze w miejsce konstrukcji. Na wysięgniku powstaje wspólny wysięgnik nad torami z jednym słupem, na bramce – jedna bramka. „Odłącz” daje osobną konstrukcję. Zmiana mocowania bramka ↔ wysięgnik dotyczy całej konstrukcji, „na maszcie” odłącza.
- **Słupy i zasięg bramki**: w polu „Konstrukcja” wybierasz słupy – dwa (po obu stronach), jeden po lewej albo jeden po prawej (patrząc w kierunku jazdy) – i tory, nad którymi belka przechodzi bez sygnalizatora (np. bramka z jednym semaforem nad dwoma torami). Dołączony sygnalizator przejmuje te ustawienia.
- **Światło komory** (panel semafora, lista przy każdej komorze): stałe (bez kresek), stałe lub pulsujące (2 kreski na obwodzie), tylko pulsujące (4 kreski). W zapisie tekstem „*” po literze oznacza stałe lub pulsujące, a „**” tylko pulsujące, np. „B**PCZ*”.
- **Licznik osi, złącze izolowane, ukres** (urządzenia bez kierunku): przy wstawianiu stają po tej stronie toru, po której klikniesz. Później PPM „Zmień stronę”. Licznik osi rysuje się tuż przy torze, więc nie wchodzi na komory semafora w tym samym miejscu. Jego nazwa jest po drugiej stronie toru.
- **Znaki Ig-10:** rozjazd ma kreskę początku, numer i czarny klin w kącie między torami, po stronie kierunku zamkniętego, jak w Ig-10 zał. 8 i na planach schematycznych. Klin liczony jest z geometrii rozjazdu. Rozjazd krzyżowy ma dwa kliny w kątach ostrych przy punkcie matematycznym. Wzory znaków są w bazie `Dane/symbole_ig10.json`. Przycisk „Symbole Ig-10…” pokazuje wszystkie znaki. Po poprawkach w pliku użyj „Wczytaj ponownie”.
- **W4 i W5:** na liście urządzeń (wskaźnik zatrzymania się czoła pociągu W4, wskaźnik granicy przetaczania W5, znaki wg Ig-10 s. 33 i 30). Stawiasz je jak semafor: wciśnij na torze i przeciągnij w stronę jazdy, po stronie wskaźnika.
- **Odcinek (O):** odcinki kontroli niezajętości. Najpierw stawiasz granice: liczniki osi albo złącza izolowane (narzędzie Urządzenie). Program sam wyznacza kawałki torów między granicami, także przez rozjazdy i łączniki. Kliknięcie narzędziem Odcinek na torze nadaje nazwę całemu kawałkowi. Podpowiedź to numer toru albo „z” i numery rozjazdów (np. z1/2), wpisać można dowolną. Kolor na planie oznacza odcinek, czerwone kreski – kawałek bez odcinka. Kliknięcie w kawałek, który ma już nazwę, zaznacza ten odcinek. Panel: nazwa, rodzaj (liczniki osi / obwód torowy), długość, kawałki torów z km, granice, czy to odcinek zwrotnicowy. Nazwę odcinka (pod torem) przeciągasz we Wskaż.
- **Peron:** wciśnij na torze w miejscu początku krawędzi peronowej i przeciągnij wzdłuż toru do jej końca, po stronie peronu. Panel: numer, km od–do, długość krawędzi (z km linii PP / KP), odległość krawędzi od osi toru, wysokość nad główką szyny, szerokość, „Zmień stronę”, „Dwukrawędziowy” (druga krawędź przy sąsiednim torze). Krawędź jest podwójną linią jak w Ig-10.
- **Przejazd:** kliknięcie na torze daje drogę prostopadłą, przeciągnięcie wzdłuż osi drogi daje kąt i długość drogi na planie. Panel: opis, kategoria A–F albo przejście (P), km, kąt, szerokość, rogatki (mechaniczne / elektryczne). Na planie: krawędzie drogi kreskowane, napis z kategorią i km linii.
- **Kilometraż (km):** wciśnij na torze w miejscu, gdzie znasz km linii, i przeciągnij w stronę rosnącego km. W oknie wpisz numer linii i km, np. `17 25.452`. Drugi punkt na tym samym torze ustala kilometr skrócony albo wydłużony. Pozostałe tory biorą km z rzutu na tor z kilometrażem. Na planie są wskaźniki hektometrowe i kilometrowe. Km pod kursorem pokazuje pasek stanu, a panele peronu, przejazdu i toru podają km linii.
- **Profil… (profil podłużny):** dla zaznaczonego toru. „Teren z NMT GUGiK” pobiera wysokości terenu co krok (domyślnie 10 m). „Niweleta z terenu” układa odcinki stałego pochylenia (tolerancja, najkrótszy odcinek – domyślnie 0,3 m i 100 m). Załom przeciągasz na wykresie w pionie albo wpisujesz w tabeli km, Z i promień łuku pionowego Rv. Wykres pokazuje pochylenia ‰, długości oraz nasyp (pomarańczowy) i przekop (niebieski). NMT to teren, nie główka szyny: na wiaduktach i w przekopach popraw niweletę ręcznie. Pochylenia widać też na planie (Ig-10 s. 58), a Z niwelety na pasku stanu.
- **Funkcja semafora** (panel semafora): wjazdowy, wyjazdowy, drogowskazowy, odstępowy, powtarzacz (Sp), PK. Od niej zależy symbol na pulpicie, więc „Sprawdź” przypomina o semaforze bez funkcji.
- **Powtarza** (panel tarczy ostrzegawczej i semafora-powtarzacza): semafor, którego sygnał urządzenie powtarza, albo szlak (informacja z sąsiedniej stacji). Wybierasz z listy. „Zaproponuj” wstawia pierwszy semafor przed urządzeniem w kierunku jego działania (po torze na wprost i przez styki torów, do 3 km) – to tylko propozycja, sprawdź ją. „Sprawdź” przypomina o tarczy bez wskazania i o usuniętym semaforze. W symulatorze tarcza jest zielona, gdy semafor powtarzany podaje sygnał zezwalający (pole `repeats`).
- **Posterunki i szlaki** (panel „Posterunki i szlaki”; gdy jest rozwinięty, tory posterunków są podświetlone ich kolorem, a szlaki – pasem z numerem):
  - **posterunek:** zaznacz tory (Shift + prostokąt albo Ctrl + klik), potem „+ posterunek” – kod (np. LW) i nazwa. Tor przypiszesz też w panelu toru (pole „Posterunek”). Rozjazdy i urządzenia należą do posterunku swojego toru. Nazwy semaforów i numery rozjazdów nie mogą się powtarzać w jednym posterunku, ale na różnych posterunkach mogą (np. „A” na każdej stacji);
  - **szlak – narzędzie Szlak (B):** kliknij semafor wjazdowy posterunku albo koniec toru na granicy obszaru, potem drugi koniec. Droga po torach i semafory odstępowe wyznaczą się same. W panelu szlaku: oznaczenie, linia, tor szlakowy, blokada (SBL, PBL, telefoniczna), stawność SBL, dla każdego końca – posterunek sąsiedni (koniec na granicy obszaru) i oznaczenie końca na pulpicie (np. 1G);
  - Del na zaznaczonym posterunku usuwa go (tory zostają bez posterunku), na szlaku – szlak.
- **Sprawdź (F7):** kontrola całego układu. Lista w panelu „Kontrola układu”: błędy na czerwono, uwagi na pomarańczowo. Kliknięcie pokazuje miejsce i zaznacza obiekt. Sprawdzane są: geometria torów, otwarte końce (tor bez połączenia i bez kozła – na granicy obszaru zaznacz w panelu toru „początek / koniec – granica obszaru”), tory bez numeru, rozjazdy bez numeru i ich uwagi, urządzenia poza torem i powtórzone nazwy, nakładające się tory, odcinki (kawałek bez odcinka, odcinki bez granicy między sobą, granica w obrębie rozjazdu), perony, przejazdy, kilometraż, profil, funkcje semaforów, posterunki i szlaki.
- **Warstwy** (panel): widoczność warstw wg nazw z Ig-10 zał. 8 – układ torowy, rozjazdy, sygnalizator, wskaźniki, wykolejnica, licznik osi, złącze izolowane, odcinki, peron, drogi, kilometraż, opis, podkłady. Dotyczy ekranu i wydruku, zapisuje się z widokiem.
- **Eksport… (wydruk w skali):** Schemat Ig-10 (wzdłuż 1:2000, w poprzek 1:500) albo Teren (1:500 … 1:5000), cały projekt albo bieżący widok, kartka dopasowana do planu albo A4–A0 (plan dzielony na kilka kartek), 150 / 200 / 300 dpi, zapis do PDF albo PNG. Na dole kartki jest opis i podziałka 100 mm. Drukuj w „rzeczywistym rozmiarze”, bez dopasowania do strony – wtedy 1 mm na kartce odpowiada skali. Podkłady (ortofotomapa) nie są drukowane.

Wymiary a i b rozjazdów w katalogu są przybliżone, katalogowe wpisz w `Dane/rozjazdy.json`. Następny etap: edytor zobrazowania (osobny program), potem przekazanie danych z UT do symulatora.

### Skróty klawiszy edytora UT

| Klawisz | Działanie |
|---|---|
| V | Wskaż (zaznaczanie, przeciąganie, menu PPM) |
| T | Tor; w nim Tab przełącza Prosta / Łuk |
| R | Rozjazd (typ z listy na pasku) |
| U | Urządzenie (rodzaj z listy na pasku) |
| P | Równoległy |
| K | Łuk od–do |
| J | Łuk z punktów; w nim Tab – także krzywe przejściowe L, W – proste stałe / wierzchołek swobodny, O – punkty z OpenStreetMap, Enter – zastosuj |
| O | Odcinek |
| B | Szlak |
| M | Miarka |
| S | widok Schemat / Teren |
| F7 | Sprawdź |
| Del | usuń zaznaczone (wierzchołek, tor, rozjazd, urządzenie… – także kilka naraz) |
| strzałki (z Shift – więcej) | przesuń zaznaczony rozjazd, urządzenie, tor albo wierzchołek |
| Enter | zatwierdź długość z klawiatury / zakończ tor |
| Backspace | cofnij ostatni odcinek toru albo punkt miarki |
| Esc | rezygnacja, powrót do Wskaż |
| Alt (w narzędziu Tor) | wolny kierunek – załamanie z łukiem w wierzchołku |
| Shift (w narzędziu Tor) | łuk kończy się równolegle do osi schematu |
| Ctrl (w narzędziu Tor) | bez przyciągania |
| Ctrl + przeciągnięcie | przesunięcie zaznaczonego toru razem z rozjazdami |
| Shift + klik / Ctrl + klik (Wskaż) | dodaj element do zaznaczenia kilku / zdejmij |
| Ctrl+J | połącz zaznaczone tory w jeden |
| Shift + przeciągnięcie od pustego miejsca (Wskaż) | zaznaczenie prostokątem |
| PPM na torze (Wskaż) | menu: usuń kawałek (prostą / łuk), wygładź łuk, podziel łuk (tutaj / na części), rozetnij tor, usuń tor |
| Ctrl+N / Ctrl+O / Ctrl+S / Ctrl+Shift+S | nowy / otwórz / zapisz / zapisz jako |
| Ctrl+Z / Ctrl+Y (Ctrl+Shift+Z) | cofnij / ponów |

Peron, Przejazd i km nie mają klawisza – wybiera się je przyciskiem na pasku.


## Edytor zobrazowania

Osobny program: `uruchom_edytor_zobrazowania.bat` albo `app/Lcs.EdytorZobrazowania/bin/Release/net10.0-windows/EdytorZobrazowania.exe [plik.zob.json | plik.ut.json]`. Rysuje obrazy pulpitu (wg Ie-104) do projektu UT. Plan i decyzje: `PLAN_EDYTOR_ZOBRAZOWANIA.md`.

Stan (etapy Z1–Z6 – szkielet, tory, zwrotnice, sygnalizatory, wykolejnice, kozły, przerwy izolacyjne, opisy odcinków, numery torów, perony, przejazdy, strzałki blokady, napisy, kilka obrazów, obraz poglądowy, „Sprawdź”, PNG i wydruk, podgląd pulpitu):
- **UT tylko do odczytu.** Tory, rozjazdy, urządzenia, odcinki i ich nazwy pochodzą z UT. Braki (np. numer zwrotnicy, funkcja semafora, posterunek) poprawia się w edytorze UT, a potem tu „Wczytaj UT ponownie”.
- **Nowy…** wybiera projekt UT (`*.ut.json`) i tworzy obraz szczegółowy ze wszystkimi posterunkami UT. **Otwórz…** otwiera zobrazowanie (`*.zob.json`); wskazanie pliku `*.ut.json` też zaczyna nowe zobrazowanie. **Zapisz** zapisuje plik obok UT (`widzew.ut.json` → `widzew.zob.json`), poprzednia wersja zostaje jako `.bak`. Jeśli UT zapisano po ostatnim zapisie zobrazowania, program ostrzega przy otwarciu i na pasku stanu.
- **Zakładki obrazów** (pod paskiem): klik pokazuje obraz, „+ obraz szczegółowy” (posterunki wybierasz w panelu obrazu), „+ obraz poglądowy” (cały obszar, rysowany osobno – Ie-104 § 6).
- **Panel obiektów UT** (z lewej): obiekty posterunków obrazu po grupach – tory (także szlakowe), rozjazdy, sygnalizatory z funkcją, wykolejnice, kozły, przerwy izolacyjne, odcinki, perony, przejazdy, szlaki. Zielone – są na obrazie, czerwone – brak.
- **Panel obrazu** (z prawej): nazwa, rodzaj (szczegółowy / poglądowy), posterunki na obrazie (bez posterunku – cały obszar), wymiary w jednostkach obrazu (domyślnie 1920 × 1080, zaokrąglane do siatki), kolejność zakładek ◀ ▶, „Kopiuj obraz” (nowa zakładka zaraz za bieżącą, nazwa „… (kopia)”, cała treść – tory, symbole, opisy, perony, napisy – przypięta do tych samych obiektów UT; Ctrl+Z cofa), „Usuń obraz”. Kopia to punkt wyjścia do drugiego obrazu albo obrazu poglądowego: zmień rodzaj i posterunki, zdejmij zbędne elementy, „Sprawdź” pokaże resztę. Ten sam obiekt UT może być na dwóch obrazach – na pulpicie jest na obu.
- **Płótno:** czarne tło jak na pulpicie, siatka co 8 j. (co 40 j. grubsza; „Siatka” na pasku ją ukrywa). Kółko myszy przybliża wokół kursora, przeciąganie lewym albo środkowym przyciskiem przesuwa. „Cały obraz” (Home) i „1:1” (1 j. = 1 piksel). Pasek stanu pokazuje punkt siatki pod kursorem. Siatka, ramka i znacznik kursora są tylko w edytorze, nie na pulpicie.
- **Tor (T):** wybierz tor w panelu UT (albo kliknij koniec toru na obrazie – pojawi się lista torów UT do narysowania, najpierw te połączone z klikniętym), potem klikaj punkty łamanej. Punkt staje w węźle siatki, z Shift – poziomo od poprzedniego, z Ctrl – poziomo, pionowo albo ukosem 45° (tory na pulpicie biegną tylko tak, odcinek pod innym kątem ma czerwoną kreskę). Punkty przyciągają się do końców i załamań torów oraz do linii innych torów. Koniec: klik na węźle albo na linii innego toru, PPM, Enter albo dwuklik. Backspace cofa punkt, Esc rezygnuje. Tor obrazu to kawałek toru UT: program bierze pierwszy jeszcze nie narysowany kilometraż. Gdy zaczynasz albo kończysz na torze, z którym ten tor łączy się w UT, kierunek km wynika z UT, inaczej „km od” to pierwszy punkt łamanej. Ten sam km nie może być dwa razy na jednym obrazie. Gdy tor jest już wybrany w panelu UT, klawisz T od razu zaczyna rysowanie jego brakującego kawałka.
- **Łącznik (R):** kliknij tor obrazu w miejscu pierwszej zwrotnicy, potem drugi tor. Program rysuje łącznik jak na pulpitach LCS: ukos 45° na jeden krok siatki, pion i ukos na jeden krok (kształt „[”). Drugi klik wybiera drugi tor i stronę (w lewo albo w prawo). Łącznik przypina się do toru łączącego z UT: wybranego w panelu UT albo tego, którego rozjazd leży najbliżej wskazanego miejsca. Działa między torami poziomymi; inny tor łączący narysuj narzędziem Tor. Na wzorach SimRail tory stacyjne leżą co 96 j. (12 kratek).
- **Wskaż (V):** klik na torze zaznacza go. Panel toru pokazuje tor UT, km od – do (można wpisać, np. tor przerwany na granicy obrazów), „Odwróć kierunek km” i „Usuń z obrazu”. Przeciągnięcie kwadracika przesuwa punkt łamanej, strzałki przesuwają cały tor o krok siatki (z Shift ×5), Del zdejmuje tor z obrazu (w UT zostaje). Przeciągnięcie pustego miejsca przesuwa widok.
- **Panel UT:** tor narysowany w całości jest zielony, w części – brązowy z brakującym km. Klik na torze, który jest na obrazie, zaznacza go. **Podgląd UT** pod panelem pokazuje osie torów UT (w poprzek rozciągnięte) z wybranym torem na pomarańczowo.
- **Miarka (M):** klikaj punkty – długość w jednostkach obrazu i w kratkach siatki; PPM czyści.
- **Kilka obrazów:** każdy posterunek (albo fragment) na swoim obrazie; posterunki obrazu wybierasz w panelu obrazu. Pole „obiekty innych posterunków” w panelu UT pokazuje na szaro obiekty pozostałych posterunków – można je też narysować (np. wspólna głowica na dwóch obrazach); „Sprawdź” ich nie wymaga. Na pulpicie symulatora taki obiekt jest na każdym obrazie, na którym go narysowano.
- **Obraz poglądowy** („+ obraz poglądowy”): rysowany osobno, mniejszy odstęp torów (co najmniej 10 j.); „Sprawdź” wymaga tylko symboli z Ie-104 § 6 ust. 4 (tory, rozjazdy, semafory, szlaki, przejazdy, numery torów) i ostrzega o tarczach, wykolejnicach, opisach odcinków i peronach. Na pulpicie zakładka „Obraz poglądowy” pokazuje – jak dotąd – pomniejszone obrazy szczegółowe; „Do symulatora…” obrazu poglądowego nie przenosi.
- **Rozjazdy krzyżowe i skrzyżowania:** skrzyżowanie (St) nie ma symbolu – to krzyż z narysowanych torów (na pulpicie węzeł krzyża trafia w punkt przecięcia). Rozjazd krzyżowy ma w panelu UT dwa końce: „koniec ab” (od mniejszego km toru A) i „koniec cd”; stawiasz je narzędziem Zwrotnica (Z), opis na pulpicie np. „3ab”. **Rkpd i Rkp** – dwie zwrotnice zwrócone ostrzami (Ie-104 § 8 pkt 9 – obraz jednakowy dla rozjazdów pełnych i niepełnych): tor A rysujesz w całości (łącznik między ostrzami), tor B w dwóch kawałkach – do punktu zwrotnicy ab i od punktu cd, np. jak na pulpitach LCS: oba tory poziomo, tor B dochodzi nawiasem „[” do ab i wychodzi nawiasem „]” od cd (rysuj w przybliżeniu – punkt bliżej niż 8 px od linii toru przyciąga się do niej) (narzędzie Tor samo pomija wnętrze rozjazdu na torze B; zwrotnica ab przyciąga się do końca toru B). „Sprawdź”: tor B rozjazdu krzyżowego kończy się w punkcie zwrotnicy, tory skrzyżowania krzyżują się na obrazie.
- **„Sprawdź” – także:** odstęp torów (co najmniej 2 wysokości opisu – 20 j., Ie-104 § 4 ust. 15), tarcza manewrowa obok wykolejnicy, strzałka blokady tuż przy semaforze wjazdowym, zwrotnica, od której narysowany tor odgałęźny nie odchodzi, ten sam km toru dwa razy.
- **Przypnij do innego obiektu UT** (panel symbolu, panel toru obrazu): gdy w UT zmienił się obiekt (np. rozcięty tor), symbol albo tor obrazu zostaje w miejscu i przypina się do wybranego obiektu.
- **Podgląd pulpitu** (pasek obok „Do symulatora…”): okno z obrazem rysowanym tym samym kodem co pulpit symulatora i z prawdziwym komputerem zależności, bez zapisywania czegokolwiek. Wybierasz obraz i przebieg z tablicy przebiegów, „Nastaw” – przebieg jak polecenie dyżurnego (czas płynie: zwrotnice się przekładają, droga się utwierdza, semafor daje sygnał), „Stan zasadniczy” – wszystko od nowa, „Opisy odcinków” – opisy na żądanie.
- **PNG…** zapisuje obraz jak na pulpicie (1 j. = 1 piksel, bez siatki i znaczników edytora), **Drukuj…** – na jednej kartce poziomo.
- Odcinek łamanej, który nie biegnie co 45°, ma czerwoną kreskę (tylko w edytorze).
- Ctrl+N / Ctrl+O / Ctrl+S / Ctrl+Shift+S, Ctrl+Z / Ctrl+Y.
- **Do symulatora…** (pasek narzędzi): zapisuje UT i wszystkie obrazy jako układ symulatora w `app/LcsWidzew/Data/projekty/<nazwa>.json` (nazwa jak plik zobrazowania; poprzednia wersja w `.bak`). `Data/station.json` zostaje bez zmian. Przy błędach „Sprawdź” program pyta, czy zapisać. Po zapisie pokazuje uwagi (np. odcinki bez nazwy, semafor bez funkcji) i proponuje uruchomienie symulatora z tym układem; później wybierasz go w oknie wyboru układu po `uruchom.bat`. Z UT: tory podzielone na kawałki między rozjazdami, urządzeniami i licznikami osi (długości z km), zwrotnice z typem i prędkością na bok, sygnalizatory wg funkcji semafora (tarcze ostrzegawcze i powtarzacze z `repeats` z pola „Powtarza”), odcinki z nazw UT (kod posterunku przed nazwą, np. `MT:T1`), szlaki z blokadą i odstępami. Z obrazu: pulpit – węzły trafiają dokładnie w narysowane symbole, reszta jest rozłożona wzdłuż toru obrazu. Przebiegi wyznacza symulator z topologii (Tablica przebiegów w symulatorze). Z obrazu na pulpit trafiają też napisy, perony, przejazdy, numery torów, opisy odcinków (wg ustawienia obrazu), strzałki blokady w narysowanych miejscach i tory bez kontroli niezajętości. Rozjazdy krzyżowe i skrzyżowania: St – węzeł krzyża `KOD:krN` na obu torach; Rkpd i Rkp – zwrotnice `KOD:Nab` / `KOD:Ncd` (łącznik między ostrzami to wnętrze toru A, wnętrze toru B pominięte), cały rozjazd w jednym odcinku; Rkp – dodatkowo `noPass`: para torów bez przejazdu, komputer zależności nie wyznacza przez nią przebiegów. Pochylenie każdego kawałka toru (`poch`, ‰ od A do B) – średnie z niwelety UT (bez niwelety – brak pola). Opisy odcinków „na żądanie” pokazuje na pulpicie przycisk „Opisy”.

Test: `EdytorZobrazowania.exe --test wynik.txt [zrzut.png]` (ostatnia linia `błędów: 0`). Sam zrzut okna: `EdytorZobrazowania.exe --shot zrzut.png plik.zob.json ["x,y" [powiększenie]]` – cały obraz albo okolica punktu obrazu (domyślnie 1:1).

**Mijanka testowa:** `projekty_ut/mijanka_testowa/mijanka_testowa.ut.json` (UT) i `mijanka_testowa.zob.json` (obraz) – przykład do otwarcia w obu edytorach: linia jednotorowa z SBL, tor 1 (gz) i tor 2 (gd, 1154 m) z łącznikami 1/2 i 3/4 (Rz 60E1-190-1:9, rozstaw 4,75 m), wjazdowe A / B, wyjazdowe C1, C2 (na wschód) i D1, D2 (na zachód), odstępowe 103 / 104 i 127 / 128, kozły na końcach toru 2, liczniki osi i odcinki, perony, przejazd, szlaki 1Z i 1W; posterunek MT. „Sprawdź” w obu edytorach – bez uwag. Na obrazie są tory, zwrotnice, sygnalizatory i kozły; przerwy, opisy odcinków, perony, przejazd i szlaki dojdą w etapie Z4. Nazwy semaforów i odcinków są przykładowe. **W symulatorze:** `uruchom.bat` → w oknie wyboru układu „mijanka_testowa” (plik `app/LcsWidzew/Data/projekty/mijanka_testowa.json`, zapisany przez „Do symulatora…”); od razu: `uruchom.bat --station app\LcsWidzew\Data\projekty\mijanka_testowa.json`.

**LCS testowe – dwie stacje jednego LCS** (`projekty_ut/lcs_testowe/lcs_testowe.ut.json` + `lcs_testowe.zob.json`, w symulatorze `app/LcsWidzew/Data/projekty/lcs_testowe.json`): linia dwutorowa, posterunki **TG „Testowo Główne”** i **TW „Testowo Wschodnie”**, między nimi szlak wewnętrzny dwutorowy **1GW / 2GW** (SBL 3-stawna dwukierunkowa, odstępowe 139–142 i 149–152, oznaczenia końców 1TW / 2TW przy TG i 1TG / 2TG przy TW), szlaki 1Z / 2Z (do „Zachodnia”) i 1E / 2E (do „Wschodnia”) z granicą obszaru.
- TG: tory 1, 2 (gz), 3, 4 (gd, perony), bocznica 6 (rozjazd 8, wykolejnica Wk1, tarcze Tm1 / Tm2, kozioł); łączniki 1/2, 6/7, 9/10, 14/15; łącznik 3/5 z toru 1 na tor 4 przez **rozjazd krzyżowy podwójny 4 (Rkpd, zwrotnice 4ab / 4cd)** na torze 2; łącznik 11/13 z toru 3 na tor 2 przez **rozjazd krzyżowy pojedynczy 12 (Rkp, 12ab / 12cd, przejazd z toru 3 na tor 1)** na torze 1; wjazdowe A, B, F, G, wyjazdowe C1–C4, D1–D4.
- TW: tory 1–4, łączniki 1/2, 3/4, 5/6, 7/8, 9/10, 11/12, przejazd kat. C, perony.
- „Sprawdź” – bez uwag w UT i na obu obrazach. Rozkład testowy `lcs_testowe.rozklad.json` (pociągi przez obie stacje, ŁKA 90501 na torze 3 TG, lokomotywa SM42 na bocznicy 6). Nazwy są przykładowe. Uruchomienie: `uruchom.bat --station app\LcsWidzew\Data\projekty\lcs_testowe.json`. Dwóch dyżurnych: jeden prowadzi serwer, drugi łączy się i przejmuje obszar drugiej stacji (rozdział „Gra wieloosobowa”) – na tym układzie przez sieć jeszcze nie sprawdzone.

## Edytor taboru

Osobny program: `uruchom_edytor_taboru.bat` albo `app/Lcs.EdytorTaboru/bin/Release/net10.0-windows/EdytorTaboru.exe [plik.json]`. Bez pliku otwiera wspólny katalog `app/Lcs.Tabor/Data/tabor.json`, który czyta też symulator (zmiana zapisana w edytorze działa w symulatorze bez przebudowy). Plan i decyzje: `PLAN_EDYTOR_TABORU.md`, historia: `HISTORIA_EDYTOR_TABORU.md`.

- **Pojazdy:** seria, rodzaj (EZT, BZT, lokomotywa, wagon…), długość, masy, Vmax, opory a + b·v + c·v², masy hamujące G / P / R, hamulec ED, źródło danych i znacznik „do potwierdzenia”. Charakterystyki „sieć” / „spalinowa” jako punkty F(v) w polu tekstowym albo na wykresie: przeciągnięcie punktu, dwuklik dodaje punkt, prawy przycisk usuwa. „Z mocy” podpowiada krzywą F = min(F0, P/v).
- **Składy:** pojazdy od czoła (seria, liczba, załadowanie), nastawienie hamulca. Sumy (długość, masa, λ, Vmax) i podgląd jazdy liczone kodem fizyki symulatora: rozpędzanie, hamowanie nagłe, ruszanie na wzniesieniu, wykres sił i oporów (prędkość równowagi), wykres v(s) / v(t).
- **Przejazd próbny:** profil z tekstu („długość pochylenie Vmax [sieć]”) albo „Profil z układu…” (tor z `station.json`, w obie strony); wynik – czas, prędkości, „NIE DOJECHAŁ” z powodem.
- „✔ Sprawdź” (F7), Ctrl+Z / Ctrl+Y / Ctrl+S / Ctrl+O. Zapis zostawia `.bak`.
- W symulatorze pociąg dostaje skład polem `"sklad"` w `<układ>.rozklad.json`; bez tego pola jedzie jak dotąd.
- **Dane katalogu są przybliżone – do potwierdzenia przez właściciela.**

## Wersje układu


Zwykłe uruchomienie (np. `uruchom.bat`) pokazuje **listę wersji układu**: ★ podstawowy `Data/station.json` i pliki z `Data/projekty` (np. własne stacje zapisane przez „Zapisz jako…”). Ostatni wybór jest zapamiętany.

## Rozkład jazdy

Symulator wczytuje `app/LcsWidzew/Data/rozklad.json` (GTFS). Gdy go brak, wczytuje `rozklad_ic.json` (tylko PKP IC). Import z pliku GTFS (np. `polish_trains.zip`) na wybrany dzień:

```bash
python tools/gtfs_import.py polish_trains.zip app/LcsWidzew/Data/rozklad.json 2026-10-15 app/LcsWidzew/Data/rozklad_ic.json
```

Import:
- przypisuje pociągi do szlaków (Andrzejów, Olechów, Chojny/Kaliska, Marysin), stacji i torów;
- łączy obiegi na ŁF/ŁW (zmiana numeru, zmiana czoła);
- usuwa duplikaty IC;
- jednostki ŁKA przejeżdżają do i z bazy (zjazdy i wyjazdy próżne).

Tylko IC (ptrc.gay/pkpic):

```bash
python tools/pkpic_import.py 2026-10-15 app/LcsWidzew/Data/rozklad_ic.json
```

### Rozkład układu z pliku (`<układ>.rozklad.json`)

Plik obok pliku układu (np. `Data/projekty/lcs_testowe.rozklad.json`) – pisze człowiek. Gdy jest, scenariusz **„Ruch podstawowy”** bierze pociągi z niego zamiast z wbudowanego rozkładu obszaru Widzewa. Lista `"pociagi"`, w każdym:
- `t` – czas od początku scenariusza [s], `numer`, `kat` (ŁKA, PR, IC, TOW…);
- `ze` – szlak, z którego pociąg przyjeżdża od posterunku sąsiedniego spoza obszaru, albo `odcinek` (np. `"TG:3"`) i `kierunek` (±1) – pociąg stoi na torze stacyjnym;
- `na` – szlak, na który ma odjechać; `postoj` – kody posterunków z postojem (`[]` – na każdym peronie, brak pola – bez postoju); `odjazd` – czas odjazdu [s];
- `lok: true` – lokomotywa manewrowa na `odcinek`.

Postój jest na torze przy peronie: odcinek „…:JT” albo – w układach z „Do symulatora…” – tor, przy którym w UT stoi peron.

---

## Architektura: zobrazowanie i komputer zależności

Podział jak w rzeczywistym LCS:

- **Komputer zależności** (`Sim/`: Simulator, Interlocking, Blocks, Commands) jako jedyny podejmuje decyzje: przebiegi, utwierdzenia, sygnały, blokady.
- **Zobrazowanie** (`UI/PanelView.cs`) rysuje wyłącznie z **meldunków**: telegramu stanu (`Sim/Meldunki.cs`, klasa `Indication`) odbieranego przez łącze `KzLink`. Do tego zna tylko układ torowy. Nie widzi przebiegów od środka, liczników czasu ani pociągów.
- **Utrata łączności** to usterka instruktora „Utrata łączności stanowiska z komputerem zależności”. Wszystkie obiekty przechodzą w stan „brak danych” (biały), polecenia nie są przyjmowane, a w ALARMACH pojawia się wpis.

- **PIP – numery pociągów na pulpicie** (Ie-104 § 8 pkt 26) idą za pociągiem po zajętościach odcinków, tak jak w prawdziwym systemie: numer przychodzi od posterunku sąsiedniego (od razu na pierwszym odstępie szlaku) albo z rozkładu i przechodzi z toru na tor, gdy pociąg zajmuje kolejne odcinki. Gdy pociąg wjeżdża na tor zajęty przez inny pociąg, numer przechodzi dopiero po zwolnieniu poprzedniego odcinka. PIP to opcja zobrazowania. W `station.json`, w sekcji `display`, pole `"pip"`: brak albo `true` – numery na wszystkich posterunkach, `false` – na żadnym, lista, np. `["LF", "LN"]` – tylko na tych posterunkach. Bez PIP na torze widać symbol odcinka (zajęty – czerwony) i numer toru. Pociągi jeżdżą tak samo.

- **Sesja stanowiska** (`Sim/Session.cs`, `ILcsSession`): okno pulpitu korzysta z komputera zależności tylko przez sesję. W grze jednoosobowej i na serwerze jest to sesja lokalna, a na stanowisku przyłączonym przez sieć – sesja sieciowa (`Net/LcsNet.cs`).

## Gra wieloosobowa (Radmin VPN)

1. Wszyscy instalują Radmin VPN i dołączają do tej samej sieci.
2. Jedna osoba (serwer) klika **„Stanowiska / sieć” → „Uruchom serwer na tym komputerze”**. Okno pokazuje adresy IP; adres z Radmina zaczyna się od `26.`. Przy pierwszym uruchomieniu zapora Windows zapyta o dostęp do sieci – trzeba zezwolić (sieci prywatne).
3. Pozostali klikają **„Stanowiska / sieć”**, wpisują adres serwera, port (domyślnie `47120`) i nazwę stanowiska, potem **„Połącz”**. Układ stacji, tablica przebiegów i rejestrator przychodzą z serwera.
4. **Obszary sterowania:** na początku wszystkimi posterunkami steruje serwer. W oknie „Stanowiska / sieć” przy posterunku jest **„Przejmij”** (gdy posterunek ma inne stanowisko, ono musi się zgodzić) i **„Oddaj”**. Polecenia dla obiektów cudzego posterunku są odrzucane z podaniem, kto nim steruje.
5. **Szlak wewnętrzny między posterunkami różnych dyżurnych** (np. ŁW–ŁF) działa jak w rzeczywistości: jeden daje **Wbl** (żądanie), drugi **Pzk** (pozwolenie).
6. **Telefon** – w tym samym oknie wiadomości do wybranego stanowiska albo do wszystkich, z zapisem w rejestratorze (kategoria „Telefon”).
7. Czas i tempo oraz panel instruktora działają na serwerze. Po rozłączeniu stanowiska jego posterunki wracają do serwera.

Planowane dalsze kroki: teren za sterownikami obiektowymi, stanowisko instruktora przez sieć.

## Struktura projektu

```
symulator/
├── README.md                  ← obsługa programów (ten plik)
├── ROZWOJ.md                  ← stan, ustalenia z właścicielem, etapy (dla programistów i agentów AI)
├── PLAN_EDYTOR_UT.md          ← plan, decyzje i pytania – edytor UT
├── HISTORIA_EDYTOR_UT.md      ← szczegółowa historia prac nad edytorem UT
├── PODZIAL_MODULOW.md         ← podział symulatora na moduły
├── CLAUDE.md, AGENTS.md       ← instrukcje dla agentów AI
├── PLAN_EDYTOR_ZOBRAZOWANIA.md ← plan i decyzje – edytor zobrazowania
├── uruchom.bat, uruchom_edytor_ut.bat, uruchom_edytor_zobrazowania.bat, uruchom_przechylka.bat
├── archiwum/                  ← stare materiały (PLAN.md v0.5, prototypy)
├── projekty_ut/               ← projekty układu torowego (*.ut.json) i zobrazowania (*.zob.json)
├── Zrodla/                    ← Ie-20, Ie-104, materiały PKP PLK
├── tools/                     ← import rozkładu (gtfs_import.py, pkpic_import.py), pdfr (strony PDF do PNG)
├── zrzuty/                    ← zrzuty ekranu
├── app/Lcs.slnx               ← rozwiązanie: symulator, rdzeń, model UT, edytor UT, edytor zobrazowania
├── app/Lcs.Core/              ← wspólny rdzeń symulacji net10.0, bez WPF i providera logów
├── app/Lcs.Core.TestHost/      ← konsolowy host tej samej regresji, testy granic i porównanie dzienników
├── app/Lcs.Model/             ← model układu torowego (tory, rozjazdy, urządzenia, odcinki, posterunki, szlaki, kontrola, PUWG 1992) i zobrazowania (Zob*) – bez okien
├── app/Lcs.EdytorUT/          ← edytor układu torowego (EdytorUT.exe), katalog rozjazdów i baza symboli w Dane/
├── app/Lcs.EdytorZobrazowania/ ← edytor zobrazowania (EdytorZobrazowania.exe), dane testowe w Testy/
├── app/Lcs.Przechylka/        ← kalkulator przechyłki (Przechylka.exe)
├── app/Lcs.Logi/              ← logi techniczne SUK (MEL + Serilog, JSONL, limity, redakcja) i testy w app/Lcs.Logi.Testy/
└── app/LcsWidzew/             ← symulator C# / WPF (.NET 10)
    ├── Data/station.json      ← układ obszaru LCS
    ├── Data/kopie/            ← automatyczne kopie zapasowe układu
    ├── Data/rozklad*.json     ← rozkład jazdy
    ├── Model/                 ← model stacji wczytywany przez symulator (Station.cs)
    ├── Sim/                   ← zależności, polecenia, blokady, pociągi, rozkład, testy
    ├── UI/                    ← pulpit (PanelView.cs), plan stacji, okna
    └── MainWindow.xaml(.cs)   ← okno pulpitu
```

## Komputerownia (podgląd kart sterowników)

Osobny program: `uruchom_komputerownia.bat` albo `app/Lcs.Komputerownia/bin/Release/net10.0-windows/Komputerownia.exe`. Pokazuje karty sterowników obiektowych działającego symulatora – dla każdej karty (odcinek: licznik osi / obwód torowy, zwrotnica, wykolejnica):

- **rozkaz od KZ** – ostatni rozkaz komputera zależności i kiedy przyszedł (np. „przestaw do „-””, „zerowanie licznika osi”);
- **status do KZ** – to, co karta melduje (np. „kontrola „+””, „przekładanie → „-”, 3,2 s”, „BRAK KONTROLI”);
- **teren** – stan rzeczywisty przy torze (iglice, osie pojazdu, wykolejnica na szynie);
- **usterki** – po wybraniu karty w panelu po prawej można je wprowadzać i usuwać (jak instruktor).

Karta **zasilania** (rodzaj „zasilanie”): sieć, UPS, agregat i napięcie nastawcze. Pokazuje ostatni rozkaz (NN, oNN, AGR), status do KZ i teren, np. „agregat – rozruch za 12 s” po zaniku sieci. Usterki: zanik sieci (agregat rusza sam po 15 s) oraz zanik sieci z agregatem, który nie startuje (trzeba podać AGR). Przy odłączonym napięciu nastawczym napędy zwrotnic stoją.

Karty sygnalizatorów pokazują sygnał zlecony przez KZ i wyświetlany w terenie wg Ie-1 (S1…S13a, Sz, Ms1, Ms2) i zapalone światła. Sygnał wynika z prędkości przebiegu (zwrotnice na bok) i sygnału następnego semafora (Ie-1 zał. 1) – np. wjazdowy przy wyjazdowym na „Stój” pokazuje S13, po otwarciu wyjazdowego sam zmienia się na S12; na SBL czterostawnej S3 oznacza dwa wolne odstępy. Wiersze żółte – stan nietypowy (rozkaz w toku, usterka), czerwone – brak kontroli, sygnał niezapalony, brak łączności. Filtry: rodzaj karty, posterunek (odstępowe – szlak), obiekt, „tylko stany nietypowe”.

Zakładka **„Komputer zależności”**: na górze **kontrola zależności na żywo** – zielone „brak naruszeń” albo czerwona lista naruszeń (np. sygnał zezwalający przy zwrotnicy bez kontroli, odcinek w dwóch przebiegach, sygnał niezgodny z Ie-1) i żółte uwagi (np. przebieg czeka na kontrolę zwrotnicy w ochronie bocznej); niżej przebiegi w KZ (stan, sygnał, prędkość, zwrotnice, odcinki, ochrona boczna), utwierdzenia, dziennik **komunikacji KZ ↔ karty** (rozkazy i zmiany statusów) i **polecenia z pulpitu** z odpowiedzią KZ. **Sprawdź przebieg** – wpisz fragment nazwy (np. `P102 1A`): zwrotnice, odcinki, ochrona boczna, prędkość, sygnał i dlaczego można albo nie można go nastawić.

Połączenie przez sieć: symulator (gra jednoosobowa albo serwer) zawsze nasłuchuje dla Komputerowni na **127.0.0.1:47121** (tylko ten komputer) – wystarczy „Połącz”. Do serwera stanowisk na innym komputerze (np. przez Radmin) – jego adres i port **47120**. `Komputerownia.exe --connect host:port` łączy od razu.

## Testy (bez okna)

Uruchamiane z `app/LcsWidzew/bin/Release/net10.0-windows/`:

| Polecenie | Co robi |
|---|---|
| `LcsWidzew.exe --selftest wynik.txt [scenariusz]` | tablica przebiegów i scenariusz z automatycznym dyżurnym |
| `LcsWidzew.exe --screenshot zrzut.png 2 "#page=1" "E S1 Pociąg"` | zrzut pulpitu po poleceniach (`#page=N`, `#overview`, `#opisy` – opisy odcinków na żądanie włączone, `@t`; `#tablica=filtr` – dodatkowo zrzut tablicy przebiegów z panelem zależności specjalnych do `zrzut.png.tablica.png`; `#tablica=filtr;v=60` – przedtem wpis prędkości pierwszego przebiegu pociągowego z filtra i zapis zależności, `v=0` usuwa wpis; `#stan` – do `zrzut.png.txt` sygnały zezwalające z kodami Ie-1 i pociągi z prędkością) |

Opcja `--station plik.json` wczytuje inny układ, np. kopię z `Data/kopie/`.

Na układzie LCS testowe: `LcsWidzew.exe --station ..\..\..\Data\projekty\lcs_testowe.json --selftest wynik.txt` – oczekiwane `nastawione 15 z 15`, KZ na żywo 0 naruszeń. W `--screenshot` strony liczy się od 0 (`#page=0` – Testowo Główne, `#page=1` – Testowo Wschodnie).

### Rdzeń i host bez WPF (S01.02)

Pulpit korzysta z `Lcs.Core.dll`. Biblioteka kompiluje pojedyncze źródła `LcsWidzew/Model` i `LcsWidzew/Sim` przez linki projektu; sam pulpit ich nie kompiluje. Model nowego UT (`Lcs.Model`) pozostaje osobną biblioteką. `Point2`, `Bounds2` i `RgbColor` nie wymagają WPF. `SimulatorOptions` pozwala hostowi przekazać katalog rozkładu i `ILogger`; domyślny katalog rozkładu pochodzi z pliku wskazanego w `Station.Load`. Zegar symulacji nadal wynika z jawnych wywołań `Tick(dt)`. Provider i zapis logów należą do aplikacji uruchamiającej rdzeń.

`Lcs.Core.TestHost.exe` z `app/Lcs.Core.TestHost/bin/Release/net10.0/` wymaga zwykłego runtime .NET 10. Uruchamia ten sam `SelfTest` co WPF, w tym wszystkie scenariusze i wzorce. Tabela `Data/sygnaly_ie1.json` jest kopiowana do obu programów z tego samego źródła; musi pozostać obok pliku wykonywalnego. Przykłady z katalogu głównego repo po zbudowaniu hosta:

```powershell
app\Lcs.Core.TestHost\bin\Release\net10.0\Lcs.Core.TestHost.exe --station app\LcsWidzew\Testy\station_test.json --selftest artifacts\selftest-core.txt
app\Lcs.Core.TestHost\bin\Release\net10.0\Lcs.Core.TestHost.exe --station app\LcsWidzew\Testy\station_test.json --test artifacts\adaptery-core.txt
app\Lcs.Core.TestHost\bin\Release\net10.0\Lcs.Core.TestHost.exe --boundary artifacts\granica-core.txt
app\Lcs.Core.TestHost\bin\Release\net10.0\Lcs.Core.TestHost.exe --compare artifacts\selftest-wpf.txt artifacts\selftest-core.txt artifacts\porownanie.txt --require-events
```

Katalog wyników utwórz przed próbą. Dla scenariuszy `ic_*` kopiuj też `Data/rozklad.json` obok testowej kopii układu; sam plik w `Testy/` nie ma obok siebie rozkładu GTFS. Zarówno selftest, jak i pojedynczy scenariusz zapisują pełny dziennik `wynik.txt.zdarzenia`. Porównanie normalizuje wyłącznie końce linii, zachowując czas symulacji, kolejność i treści zdarzeń. Kod 0 oznacza zgodność porównania albo zakończenie próby; wynik domenowy należy sprawdzić w raporcie (oczekiwane 25/26 i 4/4, bez wymuszania 26/26). W trybie `--test` kod 1 oznacza nieudaną kontrolę, w `--compare` różnicę, a kod 2 błąd wykonania. `--boundary` sprawdza przechodnie referencje biblioteki oraz brak WindowsDesktop w konfiguracji hosta. Host obsługuje też `--logi`, `--logi-raport` i `--logi-proba` z tego samego adaptera co pulpit.

Pełna regresja S01.02 i przekazanie do S01.03: [raport](SUK_plan_multiplayer/raporty/S01.02.md). Odebrany S01.03 przeniósł aplikacje na .NET 10; S02.01 uzupełnia tę konfigurację dla później dodanego taboru.

### Logi techniczne (od S01.01)

Symulator zapisuje logi techniczne w formacie JSONL (jedna linia = jeden rekord JSON wg `kontrakty/logi/rekord_logu_v1.schema.json`) w `%LocalAppData%\SUK\LcsWidzew\Logs\LcsWidzew-RRRRMMDD.jsonl`. Działa bez internetu i konta. Pliki do 32 MiB, najwyżej 8 plików i 7 dni (starsze są usuwane), zapis przez osobny wątek – pulpit i cykl symulacji nie czekają na dysk; przy przepełnieniu kolejki rekordy są pomijane i liczone (rekord `LOG.DIAGNOSTICS_DROPPED`). Do logów trafiają: start i zamknięcie programu, nieobsłużone błędy (po redakcji: bez haseł, tokenów, kluczy, ścieżki profilu użytkownika, adresów IP) i kopia wpisów okna Zdarzenia bez ich treści (kategoria, typ i identyfikator obiektu, numer wpisu). Okno Zdarzenia i eksport `.zdarzenia` działają jak dotąd. Od 18.10.2026 symulator nie dopisuje surowych wyjątków do starego `bledy.log`; istniejący plik pozostaje na dysku. Komunikat błędu wskazuje katalog logów JSONL. Właściciel potwierdził schemat v1, powyższą retencję i dalsze pomijanie treści wpisów. Telemetrię narzędzi testowych wyłączamy przez `TESTINGPLATFORM_TELEMETRY_OPTOUT=1` i `DOTNET_CLI_TELEMETRY_OPTOUT=1`.

| Opcja (z dowolnym trybem) | Co robi |
|---|---|
| `--logi katalog` | logi do podanego katalogu (testy na kopii danych) |
| `--logi-raport plik.txt` | przy zamknięciu zapisuje stan logów: przyjęte, zapisane, pominięte wg powodu, gotowość zapisu |
| `--logi-proba` | próba kontrolna: błąd z testowymi sekretami i CR/LF, pola niedozwolone, pomiar kosztu emisji (wynik w `--logi-raport`) |

Testy biblioteki logów (Linux/VM z .NET SDK 10.0.402): `dotnet test --project app/Lcs.Logi.Testy/Lcs.Logi.Testy.csproj -c Release` (net10.0). Sprawdzenie plików logów kontraktem: `python3 kontrakty/testy/sprawdz_kontrakty.py --jsonl katalog_logów`.

Edytor UT (z `app/Lcs.EdytorUT/bin/Release/net10.0-windows/`):

| Polecenie | Co robi |
|---|---|
| `EdytorUT.exe --test wynik.txt [zrzut.png]` | test automatyczny przez kontrolki okna i mysz płótna (PUWG 1992, panel, cofanie, widok, zapis; podkłady, kalibracja, miarka – potrzebny internet; geometria i budowa torów); ostatnia linia `błędów: 0`; okno poza ekranem; dodatkowo zrzuty `_mapka.png`, `_szyny.png`, `_symbole.png`, `_schemat_blisko.png` |
| `EdytorUT.exe --shot zrzut.png [plik.ut.json] ["punkt" [m/px]]` | sam zrzut okna |

Tabor i fizyka jazdy (`app/Lcs.Tabor`, etapy B1–B2): pociąg w `<układ>.rozklad.json` z polem `"sklad": "EN57"` (id składu z pliku taboru) jedzie wg fizyki – siła pociągowa, opory, pochylenia (`poch` z UT) i łuki pod składem, sieć trakcyjna, hamowanie z masy hamującej; na wzniesieniu może zwolnić albo nie ruszyć (wpis w zdarzeniach). Pociąg bez `"sklad"` jedzie jak dotąd. W trybie zrzutu `#stan` przy takim pociągu: skład, masa, pochylenie, prędkość docelowa, siła i hamowanie. Testy: `dotnet test --project app/Lcs.Tabor.Testy/Lcs.Tabor.Testy.csproj -c Release` – 20 testów (wynik jazdy na mijance w `jazda_mijanka.txt`); raport `kalibracja_tabor.txt` w `app/Lcs.Tabor.Testy/bin/Release/net10.0/` (czasy rozpędzania, drogi hamowania, prędkości równowagi na pochyleniach). Katalog pojazdów: `app/Lcs.Tabor/Data/tabor.json` (dane przykładowe do potwierdzenia).

Kalkulator przechyłki (z `app/Lcs.Przechylka/bin/Release/net10.0-windows/`): `Przechylka.exe --test wynik.txt [zrzut.png]`.

Komputerownia – test z działającym symulatorem: najpierw `LcsWidzew.exe --station Testy/station_test.json --screenshot s.png 40 "#serwis=47131" "#fault=swNoCtrl:LW:57" "#page=2" "E S1 Pociąg" "@35" "#hold=30"` (w tle), potem `Komputerownia.exe --test wynik.txt [zrzut.png] 127.0.0.1:47131` – ostatnia linia `błędów: 0`.


## Stacja testowa

`Data/projekty/stacja_testowa.json` („Testowo”, wybór przy uruchomieniu): tor zasadniczy ze szlakami 1Z (SBL dwukierunkowa, 3 odstępy) i 1W (PBL dwukierunkowa), tory 2 i 3, bocznica 4 (kozioł, wykolejnica Wk1, tarcza Tm1), 5 rozjazdów, semafory A, B, D1–D3, F1–F3, perony, przejazd; plan schematyczny (lekko skośny, skala rzeczywista) i osobne zobrazowanie pulpitu.

---

## Dla następnego agenta – stan na 2026-10-19

Najpierw przeczytaj `CLAUDE.md`, `ROZWOJ.md`, `PLAN_EDYTOR_UT.md` i `PLAN_EDYTOR_TABORU.md`. Szczegóły prac są w `HISTORIA_EDYTOR_UT.md` i `HISTORIA_EDYTOR_TABORU.md` (wpisy z 2026-10-19).

**Co zrobiono 2026-10-19**
1. **Prędkość przebiegu ustalana ręcznie** (symulator): pole „Prędkość przebiegu” w „Tablicy przebiegów” (`"v"` w `<układ>.zaleznosci.json`, wartości z `Zaleznosci.Predkosci`). `VSet` w KZ bierze najpierw tę wartość, potem `routeRules[].vmax`, i zastępuje ograniczenia torów i zwrotnic. Sygnał dobiera `SignalDef.Dobor`: 40, 60 (pas P), 100 (pas Z), wyżej W21; brak pasa / W21 – sygnał niższy z uwagą. `station.json`: `"stripes"`, `"w21"` (eksport z UT w `Lcs.Model/DoSymulatora.cs`).
2. **Edytor UT:**
   - zamiana numerów rozjazdów i skrzyżowań przy zajętym numerze (jedno Ctrl+Z), `CommitPending()` – wpis bez Enter nie przepada;
   - **łączenie bramek i wysięgników** (`UtDevice.Konstrukcja`): „Połącz z…” / „Odłącz” w panelu i menu PPM, wspólny wysięgnik z jednym słupem, kontrola w „Sprawdź”;
   - **słupy i zasięg bramki**: jeden / dwa słupy, strona słupa, belka nad torami bez sygnalizatora (`Slupy`, `SlupStrona`, `NadTorami`);
   - **Shift + klik** – zaznaczanie kilku torów i elementów, prostokąt także od toru;
   - **rozcinanie toru w łuku** (łuk dzieli się na dwa o tym samym R) i **łączenie torów** Ctrl+J (`Lcs.Model/UtLaczenie.cs`) – styk wprost, odstęp łukiem albo łukami odwrotnymi, obiekty wchłoniętego toru przechodzą na wynikowy;
   - narzędzie Tor wchłania przy łączeniu tylko pusty tor (wcześniej obiekty zostawały z nieistniejącym torem);
   - szybsze płótno: pamięć geometrii bez budowania napisów, odsiewanie torów ramką przy trafianiu, rysowaniu i zaznaczaniu (400 torów – 2000 trafień ok. 0,2 s).
3. **Edytor zobrazowania:** „Kopiuj obraz”.
4. **Tabor i fizyka jazdy (etapy B1–B4):** biblioteka `app/Lcs.Tabor` (pojazdy, składy, fizyka, maszynista wspólny z symulatorem, przejazd próbny), jazda w symulatorze z pochyleniami i łukami dla pociągów z `"sklad"`, edytor taboru `app/Lcs.EdytorTaboru` z wykresami (opis wyżej, „Edytor taboru”).

**Testy (ostatnie przebiegi):** selftest `station_test.json` 25 z 26 / 4 z 4, dziennik zdarzeń bez zmian; `EdytorUT.exe --test` – sprawdzenia bramek, zaznaczania i łączenia bez błędów (551 sprawdzeń w przebiegu z bramkami); część przebiegów miała błędy tylko przy pobieraniu kafelków ortofotomapy z Geoportalu (sieć, zmienne między przebiegami); `EdytorZobrazowania.exe --test` 0 błędów; `Lcs.Tabor.Testy` 20 z 20; `EdytorTaboru.exe --test` 42 sprawdzenia, 0 błędów. Zrzuty w `zrzuty/` (`EdytorUT_wysiegnik_wspolny.png`, `EdytorUT_bramka_*.png`, `EdytorUT_laczenie_torow.png`, `EdytorTaboru_*.png`, `Pulpit_fizyka_*.png`).

**Nie sprawdzono / do zrobienia**
- Obsługa prawdziwą myszą i klawiaturą (testy wołają metody okna i symulują modyfikatory).
- W21 nie jest rysowany na pulpicie ani na planie stacji (widać go w podglądzie KZ i `#stan`).
- Wspólna konstrukcja (bramka / wysięgnik) nie trafia do symulatora – eksport bez zmian (`mount`).
- Pliki `Data/projekty/*.json` nie zostały ponownie wyeksportowane: brak pasów, W21, `poch` i `sklad`. Wpisuje je człowiek.
- Zmiana numeru rozjazdu w UT nie poprawia odwołań w zobrazowaniu – po zmianie sprawdź obraz.
- Tabor: dane katalogu i kalibracja (`kalibracja_tabor.txt`) do oceny właściciela; etapy T5–T6 planu (składy domyślne kategorii, GTFS → seria, scenariusz instruktora) do ustalenia.
- Commity nie są wypchnięte (`git push` dopiero na prośbę właściciela).
