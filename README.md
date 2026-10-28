# SUK – Symulator urządzeń komputerowych

Symulator Urządzeń Komputerowych urządzeń srk w Lokalnych Centrach Sterowania (LCS): pulpitu nastawczego i komputera zależności. Symulator jest ogólny – stacje, posterunki i obrazy pochodzą z wczytanego układu, a nie z kodu. Pierwszym obszarem jest **LCS Łódź Widzew**: Łódź Widzew (ŁW), Łódź Fabryczna (ŁF) i Łódź Niciarniana (ŁN). Program działa na Windows, w C#/WPF na .NET 10.

- Obsługa wg **Ie-20**: polecenia, przebiegi, blokady liniowe, postępowanie przy usterkach.
- Zobrazowanie wg **Ie-104**, wzorowane na pulpitach LCS (EbiScreen / SimRail).
- Osobny program **edytor układu torowego** (`EdytorUT.exe`) do rysowania układu w rzeczywistej geometrii (PUWG 1992). Etapy 0–7 są gotowe, dane z niego nie trafiają jeszcze do symulatora. Plan: `PLAN_EDYTOR_UT.md`.
- Osobny program **edytor zobrazowania** (`EdytorZobrazowania2.exe`) do rysowania obrazów pulpitu przypiętych do UT (etapy E1–E8). Plan: `PLAN_EDYTOR_ZOBRAZOWANIA_V2.md`.
- **Nowy edytor zobrazowania** (`EdytorZobrazowania2.exe`, w budowie – etap E1): najpierw rysunek, potem przypięcie do UT. Plan: `PLAN_EDYTOR_ZOBRAZOWANIA_V2.md`.
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

### Mapa własności KZ (S02A.01)

Każdy posterunek ma stały identyfikator komputera zależności `KZ-<kod posterunku>` (np. `KZ-LW`). Mapa własności przypisuje każde urządzenie (odcinek, zwrotnicę, sygnalizator, wykolejnicę, zasilanie) i każdy koniec szlaku jego posterunkowi. Wylicza też **granice**, czyli miejsca, w których zależności jednego posterunku korzystają z urządzenia innego posterunku. Przykłady: następny semafor za przebiegiem albo szlak między dwiema stacjami LCS. Mapa powstaje wyłącznie z danych układu (pole `post`, przedrostek klucza, posterunki węzłów torów) i z tablicy przebiegów. Niczego nie projektuje. Gdy właściciel jest niejednoznaczny lub nieznany, mapa zgłasza problem i nie wskazuje domyślnego KZ. Symulacja działa jak dotąd; mapa jest na razie tylko odczytem dla kolejnych kroków (S02A.02+).

```powershell
app\Lcs.Core.TestHost\bin\Release\net10.0\Lcs.Core.TestHost.exe --station kopia\station_test.json --mapa-wlasnosci wynik\mapa.json
python3 kontrakty/testy/sprawdz_kontrakty.py --mapa wynik/mapa.json
```

Wynik jest kanonicznym plikiem JSON zgodnym z `kontrakty/kz/mapa_wlasnosci_v1.schema.json`, ze skrótem SHA-256 do porównań. Program nie nadpisze istniejącego pliku, układu ani jego zależności specjalnych. Host świata i pulpit budują mapę przy starcie świata i zapisują w logach technicznych `KZ.OWNERSHIP_MAP_BUILT` (4101) ze skrótem i licznikami, `KZ.OWNERSHIP_UNRESOLVED` (4102, najwyżej 16 na start) dla problemów przypisania oraz `KZ.OWNERSHIP_MAP_FAILED` (4103). Szczegóły i decyzje do podjęcia: [S02A.01](SUK_plan_multiplayer/raporty/S02A.01.md).

### Zamrożone wejście cyklu KZ (S02A.02)

Karty sterowników nie wywołują już komputera zależności i nie zapisują jego stanu. Każda karta zgłasza swój status (odcinek, zwrotnica, wykolejnica, sygnalizator, zasilanie) do **rejestru wejść cyklu** świata. Rejestr zbiera zgłoszenia w jawnych **fazach** i tworzy z nich numerowane **wejścia cyklu** każdego KZ (`KZ-LW`, `KZ-LF`, `KZ-LN`):

- **KARTY** i **RUCH** (fazy regularne) – cykl kart przed KZ oraz zajętość wszystkich odcinków po ruchu pociągów; zgłoszenia są zamrażane na końcu fazy, a każdy KZ dostaje wejście w każdym cyklu (także puste),
- **ROZKAZ**, **USTERKA**, **ZDARZENIE_KARTY** (fazy natychmiastowe) – status po NN/oNN/AGR lub ZeroLO, po usterce instruktora oraz po końcu przejazdu z błędem zliczenia i po pierwszym przejeździe; każdy status trafia do KZ od razu, zanim karta wpisze alarm lub zdarzenie.

Moment działania KZ jest taki jak dotąd: dziennik, alarmy, dziennik KZ↔karty i stan KZ są w każdym cyklu identyczne z bazą, także w chwili, gdy program reaguje na alarm. Status trafia do KZ właściciela i do KZ, które z obiektu korzystają według mapy własności. Odstępy `1ŁF-1` i `2ŁW-1` bez jednoznacznego właściciela trafiają do obu KZ, a szafa zasilania do wszystkich. Wejście zawiera wyłącznie wartości: numer wejścia KZ bez luk, numer partii, cykl, fazę, czas i statusy w porządku kanonicznym. Nie zawiera odwołań do kart, terenu, pociągów ani do KZ. Wszyscy odbiorcy jednego KZ dostają ten sam obiekt. Odbiorca nie może zgłosić statusu, wprowadzić usterki, wykonać cyklu ani polecenia, ani przeliczyć tablicy przebiegów; taka próba albo jego błąd wyłącza go bez wpływu na symulację. Dziś wejścia przyjmuje przejściowy jeden KZ całego układu; osobne KZ i grupy dojdą w kolejnych etapach.

```powershell
app\Lcs.Core.TestHost\bin\Release\net10.0\Lcs.Core.TestHost.exe --station kopia\station_test.json --wejscia-cyklu wynik\wejscia podstawowy 600
python3 kontrakty/testy/sprawdz_kontrakty.py --wejscia wynik/wejscia
```

Program uruchamia scenariusz dwa razy, bez odbiorców i z dwoma testowymi odbiorcami każdego KZ, z usterkami i poleceniami oNN/NN. Katalog wyników musi być nowy (powstaje przed próbą). Czas próby to 1 s – 24 h; odbiorcy liczą strumieniowo, więc pamięć nie rośnie z długością próby. Program zapisuje tabelę rozsyłania `trasa.json`, początek strumieni `wejscia-KZ-*.jsonl` i `raport.json` z łańcuchem skrótów. Kod 0 oznacza: odbiorcy zgodni, numeracja bez luk, brak naruszeń faz, dziennik jak bez odbiorców. Kontrakt: `kontrakty/kz/wejscie_cyklu_v1.schema.json`. Postać kanoniczna (ciągi wg RFC 8785) ma wektory zgodności C#/Python w `kontrakty/testy/wejscie_cyklu_kanon.json`. Logi techniczne: `KZ.CYCLE_INPUT_ROUTING_READY` (4111, raz na start), `KZ.CYCLE_INPUT_ROUTING_FALLBACK` (4112, najwyżej 16), `KZ.CYCLE_INPUT_ROUTING_UNAVAILABLE` (4113), `KZ.CYCLE_INPUT_PHASE_VIOLATION` (4114, najwyżej 16 na kod), `KZ.CYCLE_INPUT_RECEIVER_FAILED` (4115) i `KZ.CYCLE_INPUT_VIOLATIONS_SUMMARY` (4116, podsumowanie ponad limity – także przy zatrzymaniu i na końcu świata). Szczegóły: [S02A.02](SUK_plan_multiplayer/raporty/S02A.02.md).

### Gra jednoosobowa (S02.02)

Uruchom pulpit jak dotychczas przez `uruchom.bat`. Lokalna gra korzysta z plików układu i scenariusza; nie wymaga konta serwera ani Internetu. Dotychczasowe logowanie dyżurnego służy lokalnej sesji symulatora. Pauza i przyciski tempa działają na wspólnym cyklu o kroku 0,1 s. Polecenia w pauzie, również tuż po uruchomieniu przy czasie 0, otrzymują wynik bez przesuwania czasu.

Restart scenariusza tworzy nową epokę świata i zamyka okna instruktora, przebiegów oraz inne okna związane z poprzednią sesją. Okno „Stanowiska i sieć” odświeża przyciski dla nowej sesji. Przyłączenie do serwera zamyka lokalny świat, a „Rozłącz” tworzy nową grę jednoosobową. Logi adaptera mają moduł `Singleplayer.Session`, kody `SP.*` i identyfikatory 6101–6104, bez treści poleceń i nazw operatorów. Diagnostyka nie zastępuje dziennika zdarzeń symulacji.

## Obsługa pulpitu

### Panel „Polecenia / Historia” (wg ISDR)
W prawym górnym rogu obrazu stacji jest panel z zakładkami **Polecenia** i **Historia**. Zastępuje dawne menu PPM. Klik (lewym albo prawym przyciskiem) na obiekcie pokazuje w panelu jego nazwę i przyciski poleceń. **Ciemny przycisk** oznacza polecenie niedostępne w danym stanie. **Czerwone** są przyciski poleceń specjalnych (inicjalizacja): SZI oraz specjalne zwrotnicy, odcinka i szlaku (PLUSBZ, MINUSBZ, KSR, ZEROLO, AZK, DPO, DKO).

Po kliknięciu przycisku nagłówek pokazuje całe polecenie (np. „POCIĄG R 1B”) i przycisk **Wyślij**. Polecenie idzie do systemu dopiero po „Wyślij”. Potem w panelu zostaje napis „MINUS 22 - Odebrane - przyjęte” (zielony) albo „… - odrzucone” (czerwony) – do następnego wyboru obiektu. Klik w puste miejsce obrazu albo **Esc** – rezygnacja.

| Obiekt | Przyciski |
|---|---|
| semafor | `STOP` `OSTOP` `SZ` `SZI` `NSZ` (jak na zdjęciach), dalej `STÓJ` `OSZ` (odwołanie sygnału zastępczego) `ZCZ` `OZCZ` `ZDM` |
| para początek–koniec przebiegu | `POCIĄG` `MANEWR` `ZD` `ZDP` `OPS` `ZDPP` |
| ikona nastawni („An”) | `SSS` `OSSS` `ZSS` `OZSS` `OSZ` |
| zwrotnica | `PLUS` `MINUS` `ZMK` `OZMK` `PLUSBZ` `MINUSBZ` `KSR` (kolejność jak na filmie ISDR), dalej `STOP` `OSTOP` `ZEROLO`; zwrotnica w turkusowej ramce, po wybraniu polecenia – w zielonej do „Wyślij” |
| odcinek, szlak, wykolejnica | kody Ie-104 jak w dawnym menu, np. przy szlaku `WBL` `OWBL` `PZK` `ZWBL` `PO` `KO` `POZ` `DPO` `DKO` `AZK` (opis – w podpowiedzi przycisku) |

Polecenia specjalne mają dwa etapy (Ie-104 § 11 ust. 13):
- **SZI** – inicjalizacja sygnału zastępczego. Po 5 s jaśnieje **SZ** (sygnał zastępczy) albo **NSZ**. NSZ to sygnał zastępczy z W24, tylko przy wyjeździe na tor szlaku w kierunku przeciwnym do zasadniczego.
- **ZDP** – inicjalizacja zwolnienia doraźnego przebiegu pociągowego. Po 5 s jaśnieje **ZDPP**: przebieg zwalnia się natychmiast.
- **OPS** – odwołanie zainicjowanego, niewykonanego polecenia specjalnego.
- **ZD** – zwolnienie doraźne jednoetapowe. Gdy pociąg jest w strefie zbliżania albo za semaforem, przebieg odlicza 90 s, inaczej zwalnia się od razu. Przebieg manewrowy zwalnia się od razu.
**Ochrona boczna na obrazie** (wg filmu ISDR): zwrotnica zamknięta w ochronie bocznej nastawionego przebiegu ma krótką żółtą kreskę na torze przy iglicy, sygnalizator w ochronie bocznej świeci na czerwono, wykolejnica w ochronie jest żółta.

- Polecenia specjalne wpisane w polu „Polecenie>” (np. `S1 SZ`, `13 PlusBZ`) i specjalne zwrotnic albo szlaku potwierdza się jak dotąd w oknie potwierdzenia. `ZDP` wpisane i potwierdzone w oknie też zwalnia przebieg od razu.

Polecenia stacji (ikona nastawni):
- **SSS / OSSS** – zastopowanie wszystkich sygnalizatorów stacji i jego odwołanie;
- **ZSS / OZSS** – to samo dla wszystkich zwrotnic;
- **OSZ** – odświeżenie stanu komputera zobrazowania (zawsze dostępne).

Zakładka **Historia**: kolumny Czas, Polecenie, Przyjęcie (przyjęte / odrzucone przez system) i Realizacja (np. „zrealizowane 06:00:26” po nastawieniu przebiegu, „zainicjowane”, „niezrealizowane”). Trafiają tu też polecenia wpisane w polu „Polecenie>”.

### Przebiegi myszą
- Klik na sygnalizatorze początkowym: wokół niego pojawia się turkusowa ramka, a na wszystkich możliwych końcach turkusowe kółko z kwadratem (przy przebiegu na szlak – na trójkącie końca przebiegu wyjazdowego przy semaforze wjazdowym). Panel pokazuje polecenia semafora.
- Najazd myszą na możliwy koniec – kwadrat robi się żółty.
- Klik na końcu: kółka znikają, koniec dostaje turkusową ramkę, a panel pokazuje polecenia przebiegu (np. „R 1B”). **Klik nie nastawia przebiegu** – wybierz `POCIĄG` albo `MANEWR` (ramki robią się zielone) i kliknij **Wyślij**.
- Przebieg wariantowy: po wybraniu początku klik na zwrotnicy (albo **Ctrl+klik** na sygnalizatorze / odcinku) – punkt pośredni.
- Zwolnienie przebiegu: wybierz jego początek i koniec, potem `ZD` albo `ZDP` → `ZDPP`.

### Polecenia z klawiatury
Polecenia wpisujesz w polu „Polecenie>”, w postaci `obiekt(y) KOD`, np. `E S1 Pociąg`, `Tm24 E3 Manewr`, `13 Minus`, `1A Wbl`. `POMOC` wyświetla pełną listę.

| Kod | Znaczenie |
|---|---|
| `Pociąg` / `Manewr` | przebieg pociągowy / manewrowy |
| `Stój` | sygnał zabraniający |
| `SZ` / `oSZ` | sygnał zastępczy / odwołanie |
| `SZI`, potem `SZ` albo `NSZ` | sygnał zastępczy dwuetapowo jak w ISDR (NSZ – z W24, wyjazd pod prąd) |
| `ZDP`, `ZDM`, `ZCZ`, `oZCZ` | zwolnienia doraźne przebiegów (Ie-104) |
| `ZD`, `ZDP` → `ZDPP`, `OPS` | zwolnienia jak w ISDR (ZD – zwłoka 90 s przy pociągu w strefie zbliżania, ZDPP – natychmiast) i odwołanie polecenia specjalnego |
| `An SSS`, `OSSS`, `ZSS`, `OZSS`, `OSZ` | polecenia stacji (adres – skrót nastawni albo kod posterunku) |
| `Plus` / `Minus`, `PlusBZ` / `MinBZ` | przestawianie zwrotnic (także z bocznikowaniem zajętości) |
| `Stop` / `oStop` | zastopowanie sygnalizatora/zwrotnicy, zamknięcie wyjazdu na szlak |
| `Zmk` / `oZmk` | zamknięcie ruchowe toru/zwrotnicy |
| `KSR`, `ZeroLO` | kasowanie rozprucia, zerowanie licznika osi |
| `Wbl`, `Pzk`, `oWbl`, `Zwbl`, `AZK`, `Bkon` | blokada liniowa: kierunek, pozwolenie, zwolnienie, awaryjna zmiana, blok końcowy (PBL) |
| `NN` / `oNN`, `AGR`, `KW` | napięcie nastawcze, agregat, potwierdzenie alarmów |

Szlak wewnętrzny można wskazać oznaczeniem z obrazu dowolnej stacji. Na przykład `1G Wbl` ustawia kierunek wyjazdu od strony stacji, na której szlak nazywa się 1G.

### ZDARZENIA i ALARMY
- Nad każdą tabelą: **zakres dat od – do** (kalendarz i godzina; domyślnie bieżący dzień 00:00 – 23:59). Kolumna Czas ma datę i godzinę.
- **Lejek** przy kolumnach Obszar, Stacja, Typ obiektu, Obiekt, Kategoria – filtr: zaznacz wartości i OK. Aktywny lejek jest niebieski; „Wyczyść filtr” usuwa filtr.
- „Liczba zdarzeń / alarmów”, „Strona n / m” i przewijanie stron ⏮ ◀ ▶ ⏭ (strona 1 – najnowsze). Na stronie mieści się tyle wierszy, ile widać.
- **Online** (zielony) – bieżące wpisy. Przeglądanie stron albo zmiana dat wyłącza tryb Online, klik na „Online” wraca do bieżących (cały bieżący dzień, strona 1).
- **Zostaw na wierzchu** – każdy nowy wpis przewija tabelę na pierwszą stronę, na najnowszy wpis.
- Polecenie dyżurnego daje trzy wpisy jak w ISDR: „Polecenie wysłane : MINUS”, „Polecenie ruchowe zostało przyjęte : MINUS”, „Polecenie poprawne : MINUS” (kategoria Polecenie, typ obiektu np. Zwrotnica, obiekt 53; przebieg – typ „Przebieg”, obiekt „W|L|F”). Wpisy poleceń i działań dyżurnego mają operatora, samoczynne skutki w urządzeniach („Przestawianie do „-””, „Kontrola położenia”) – bez operatora.
- Przy zdarzeniach – „Eksport CSV”, przy alarmach – „Kwituj wszystkie (KW)”, „Wycisz” i liczba niepotwierdzonych. Dwuklik na alarmie go potwierdza.

### EDR – elektroniczny dziennik ruchu (etapy E1–E8)
- Przycisk **EDR** na pasku otwiera osobne okno, które można przenieść na drugi monitor. U góry jest wykaz pociągów, na dole zakładki z dziennikami ruchu, po dwa obok siebie.
- **Posterunek wybrany u góry** (lista nad wykazem) wyznacza całe okno: wykaz pociągów, zakładki z dziennikami ruchu tego posterunku (np. Widzew – swoje szlaki, Fabryczna – swoje), dziennik telefoniczny i kontrolkę torów; nazwa posterunku jest w tytule okna.
- **Wykaz pociągów** wybranego posterunku (wygląd wg zrzutu EDR, E7): pociągi z rozkładu, które przyjeżdżają z szlaków posterunku, odjeżdżają na nie, zaczynają lub kończą na nim bieg. **Każdy pociąg ze swoim numerem ma osobny wiersz** – pociąg, który na posterunku dostaje nowy numer (np. 19284 → 12203 po zmianie czoła), to dwa wiersze: przyjazd starego numeru i odjazd nowego, każdy z własną relacją, nazwą i trasą (rozkład skleja obieg jednostki – np. 11550/1 Skierniewice → Łódź Fabryczna i 11504/5 Łódź Fabryczna → Skierniewice); pociąg zmieniający tylko parzystość – jeden wiersz z oboma numerami. Kolumny: K / NK (kursuje / nie kursuje – zaznacza dyspozytor, w symulatorze administrator / instruktor klikiem), Przyjazd plan., +/-, Przyjazd rzecz., Rodz. (kategoria wg Ir-11; tło – czas potwierdzony: zielone – przez człowieka, wpis w dzienniku; czerwone – automatycznie, z urządzeń; szare – przez dyspozytora), Nr poc. **nieparzysty** (turkusowa), Z kierunku post., Nr poc. **parzysty** (zielona), W kierunku post., Tor, Nr L., P. zam., P. obl., Typ p., P T, Odjazd plan., +/-, Odjazd rzecz., Stacja początkowa, Stacja końcowa, Przewoźnik. Czas rzeczywisty bez danych z ruchu = planowy (+/- 0); odjazd przesuwa się o opóźnienie przyjazdu. „Prezentuj daty” – godziny z datą „dd-MM gg:mm”. Nad wykazem: Częstotliwość odświeżania (min), -/+ godz. (zakres wykazu), Dane bieżące, Czas pobrania danych z bazy, Nr pociągu z szukaniem, filtry „Potwierdzenie: wszystkie / potwierdzone / niepotwierdzone” i „Pociągi: wszystkie / kursujące / niekursujące”, Uwagi eksp. i Uwagi Przew. (wpisuje dyspozytor), Uwagi własne (dyżurny), Informacje dodatkowe Z / Do / P / N (stacja początkowa, końcowa, przewoźnik, nazwa). Zakładki nad wykazem: Opis pociągu, Trasa pociągu (posterunki z rozkładu), Przewoźnik (szkielet).
- **Konfiguracja → Posterunki ruchu i dzienniki…** (administrator: rola „administrator” albo „instruktor”): dodawanie, usuwanie i ustawianie posterunków ruchu (kod, nazwa, tory stacyjne – puste: z układu) i ich dzienników ruchu (nazwa, posterunek sąsiedni, linia, urządzenia, szlaki układu albo tory wpisane ręcznie, zakładka – dzienniki z tym samym numerem stoją obok siebie, najwyżej dwa). Bez konfiguracji – wszystko ze szlaków układu, „Przywróć z układu” wraca do tego. Dziennik o tym samym id zachowuje wpisy. Zapis w `<układ>.edr.json`; błędna konfiguracja z pliku jest pomijana, a okno pokazuje dlaczego. **Konfiguracja → Czas bez wypadku…** – napis i początek liczenia pola nad zegarem („31 dni 23 godz. 39 min.”).
- Wpisy automatyczne w dzienniku (pogrubione): „Północ dnia …” na początku każdej strony, „Dyżur przyjął … dnia … o godzinie …”.
- Na pociągu w wykazie: **dwuklik w numer pociągu** (tylko w kolumnę „Nr poc.”) – wiersz w dzienniku bez okna (przyjęcie z szlaku przyjazdu, a gdy już wpisane – wyprawienie na szlak odjazdu); **przeciągnięcie** numeru na zakładkę dziennika (pokazuje ją) i na dziennik – wiersz w tym dzienniku; **PPM**: Przyjmij pociąg, Wypraw pociąg (okno z numerem, torem i godziną), Przyjmij z innego szlaku, Wypraw na inny szlak (najpierw okno „Szlaki posterunku”). Pociąg STÓJ, zgłoszenie gotowości i analiza pociągu – w kolejnych etapach.
- **Wygląd jak EDR na stanowisku ISDR** (film i zdjęcia z 2026-10-27): szare okno, granatowy zegar, wykaz na oliwkowym tle z kolorowymi nagłówkami i kolumnami (Nr poc. przy przyjeździe – niebieska, Z kierunku – jasnoniebieska, Nr poc. przy odjeździe – zielona, W kierunku – jasnoróżowa, Tor – szara, Nr L. i postoje – pomarańczowe, P T – niebieska), dziennik z ciemnym nagłówkiem w trzech pasach, kwadratowe przyciski, „Zapisz” (zapis i tak jest automatyczny), kontrolka torów stacyjnych na środku paska dolnego.
- **Wstęga** (pasek zaznaczenia wg legendy EDR) to kolor zaznaczonego pociągu: magenta – przyjazd (pociąg z postojem zbliża się z szlaku), zielona – odjazd (pociąg stoi na stacji), turkusowa – przejazd bez zatrzymania; niebieska – pociąg bez takiego stanu. Zaznaczenie samo przeskakuje na aktualny pociąg (najpierw stojący na stacji, potem najbliższy zbliżający się), gdy ten się zmieni; kliknięcie wybiera inny. Kolumna „Rodz.” bordowa – czas przejazdu potwierdzony przez urządzenie (z ruchu), „+/-” bordowe – opóźnienie, „Typ p.” pogrubiony – postój handlowy. „Opis oznaczeń” – okno z legendą. Nad wykazem: posterunek, filtr „Nr pociągu”, czas pobrania danych, „Informacje dodatkowe” Z / Do / P zaznaczonego pociągu.
- **Ustawienia dziennika** (jak w prawdziwym EDR – kierunek zasadniczy określa się w ustawieniach dziennika): menu **Konfiguracja → Ustawienia zasadnicze dziennika** albo PPM na nazwie dziennika. W oknie: szlak, rodzaj linii, kierunek parzysty (nieparzysty – przeciwny), opcje wypełniania (które pola są wymagane), dla każdego toru szlakowego – tor lewy / prawy względem posterunku, status, zasadniczy rodzaj ruchu, **zasadniczy kierunek ruchu** (nieustalony / Z posterunku sąsiedniego / DO posterunku sąsiedniego), rodzaj urządzenia srk (z układu), prowadzenie dziennika jak dla. „Zapisz” zapisuje plik `<układ>.edr.json` obok pliku układu. Bez pliku kierunek zasadniczy jest nieustalony, kierunek parzysty – z edytora UT (jeśli ustawiony). Przycisk z kołem zębatym – ustawienia bieżące (podgląd).
- **Stan torów – z wpisów w dzienniku, nie z urządzeń** (jak w prawdziwym EDR; decyzja właściciela 2026-10-27). Przy nazwie dziennika – ikony torów szlakowych z numerem toru nad ikoną: tor lewy przed prawym (wg ustawień), szara strzałka – zasadniczy kierunek z ustawień (↓ Z posterunku sąsiedniego, ↑ DO posterunku sąsiedniego, ↕ nieustalony). Tor jest czerwony, gdy w dzienniku pociąg „odjechał”, a jeszcze nie „przyjechał”; niebieska strzałka na torze – kierunek jazdy. Przyjęcie zajmuje tor o zasadniczym kierunku „Z posterunku sąsiedniego”, wyprawienie – „DO posterunku sąsiedniego”, „po torze lewym” – drugi; przy nieustalonym kierunku – przyjęcie tor lewy, wyprawienie prawy (ruch prawostronny). „---” w „przyjechał” zamyka wpis.
- **Kontrolka zajętości torów stacyjnych** pod dziennikami: kwadraty z numerami torów posterunku, zielony – wolny, czerwony – zajęty. Tor jest zajęty od wpisu „przyjechał” w przyjęciu na ten tor do wpisu „odjechał” w wyprawieniu tego pociągu (albo z tego toru). Podpowiedź pokazuje pociąg i godzinę przyjazdu. Przy kilku posterunkach – osobno dla każdego.
- **Telefonogramy** (okno jak na stanowisku ISDR): przycisk z telefonem nad dziennikiem ruchu albo „Rejestruj telefonogram” w zakładce **Dziennik telefoniczny**. W oknie: Nadaję / Odbieram, numer własny (nadaje program – kolejny w dobie, miesiącu albo roku wg numeracji posterunku; menu **Konfiguracja → Numeracja telefonogramów**, zapis w `<układ>.edr.json`, domyślnie dobowa; jeden licznik dla telefonogramów w dziennikach ruchu i wpisów dziennika telefonicznego), numer odbiorcy, nadał / odebrał (zalogowany wpisuje się sam po swojej stronie), posterunek drugiej strony, data, godzina nadania, czas zakończenia (godziny – dwuklik i kółko jak w dzienniku), wzór telefonogramu (na razie tylko „[bez szablonu]” – wzory poda właściciel), adres i treść, dodatkowa notatka. Telefonogram z dziennika ruchu trafia tylko na koniec dziennika ruchu, na całą szerokość (treść pogrubiona, pod nią „Nadał …, odebrał … godz. …” – godzina wprowadzenia do EDR; godzina nadania jest osobnym polem, widać ją w podpowiedzi). Telefonogram z zakładki „Dziennik telefoniczny” – tylko w dzienniku telefonicznym. Telefonogramu się nie zmienia – sprostowanie kolejnym telefonogramem.
- **Stan bieżący szlaku** zmienia telefonogram z dziennika ruchu: w oknie część „Zmiana stanu bieżącego szlaku” – dla każdego toru status (zamknięty / otwarty), rodzaj ruchu, zasadniczy kierunek, prowadzenie ruchu jak dla (np. telefoniczne zapowiadanie). Wzory telefonogramów zrobią to same, na razie wybiera się ręcznie. Zamknięty tor ma na ikonie czerwony krzyż; przy telefonicznym zapowiadaniu „Droga wolna” staje się wymagana. Przycisk z kołem zębatym pokazuje ustawienia bieżące, a zmiana ustawień zasadniczych nie znosi zmian z telefonogramów.
- **Dziennik telefoniczny** (ostatnia zakładka): Nr kol., Data, Adres i treść, Czas zakończenia, Nadał / odebrał; „Rejestruj telefonogram”, „Rejestruj inne okoliczności” (wpis tylko w dzienniku telefonicznym); strony = doby; przy kilku posterunkach – wybór posterunku.
- **Dróżnicy przejazdowi (kolumna 8):** posterunki dróżników wpisuje się w ustawieniach zasadniczych dziennika („Posterunki dróżników przejazdowych”: km na linii, numer na linii, nazwa, oznaczenie w dzienniku ruchu, powiadamianie według szlaku – jednotorowych: godzina, dwu i więcej torowych: ukośnik, dla pociągów – w obu kierunkach / z / do posterunku sąsiedniego, w dzienniku ruchu). Kolumna 8 „O jeździe pociągu zawiadomiono dróżników” ma podkolumnę na dróżnika (km w nagłówku); niebieska – trzeba powiadomić, szara – nie dotyczy kierunku. Klik w komórkę: godzina (edytor jak wyżej) albo ukośnik „\”. Przycisk z listą nad dziennikiem – okno „Powiadomienie dróżników przejazdowych” dla wybranego pociągu (wszyscy naraz).
- **Jazda do km i z powrotem:** w oknie wyprawienia „Wprowadź powrót ze szlaku” (w przyjęciu – „Powrót ze szlaku”) i kilometr. Powstają dwa wiersze spięte klamrą: wyjazd z uwagą „do km … i z powrotem” (przyjechał „---”) i powrót (droga wolna i odjechał „---”, przyjechał – do wpisania po powrocie). Tor szlakowy jest zajęty od odjazdu do wpisu powrotu.
- **Wpis o dowolnej treści:** przycisk „i” nad dziennikiem – wpis na całą szerokość z podpisem „Wpisał … godz. …” (bez zmian po zapisie).
- **Sprawdzenia cykliczne:** menu Konfiguracja → „Wymagane sprawdzenia – posterunek…” (najwyżej cztery: nazwa i co ile minut; podpowiedzi: łączność z dróżnikami, oświetlenie semaforów i wskaźników; zapis w `<układ>.edr.json`). Przyciski obok zegara: ostatnie wykonanie i (termin); zielony – w terminie, żółty – 15 min przed terminem, czerwony – po terminie. Klik – okno wykonania z uwagami; zapis „Sprawdzenie: … – wykonane” w dzienniku telefonicznym (inne okoliczności).
- **Wyciągi** (menu **Raporty**, podręcznik EDR rozdz. 8): „Wyciąg z dziennika ruchu…”, „Wyciąg z dziennika telefonicznego…”, „Wyciąg z zajętości torów stacyjnych…”. W oknie: posterunek ruchu, dziennik ruchu, od – do (data i godzina), „Pokaż historię zmian w wierszach”, „Pokaż”. Wyciąg wygląda jak papierowy R-146: godziny w kolumnach g. / m., kolumna 8 tylko, gdy szlak ma dróżników, telefonogramy i wpisy na całą szerokość, klamra przy jeździe do km, na końcu „Wyciąg sporządził … Stanowisko … dnia … godz. …”. Wyciąg z historią: pod każdym wierszem wcześniejsze postacie (szare, kursywa, najstarsza na dole), kolumna 9 „Data i godzina zmiany”. Do wyciągu trafiają wiersze zmieniane w wybranym okresie. „Drukuj…” – wydruk ze stopką (data wydruku, „System EDR”, strona n / m); do pliku PDF – drukarka „Microsoft Print to PDF”.
- **Parzystość numeru:** gdy szlak ma w edytorze UT ustalony kierunek pociągów parzystych, EDR sam zmienia ostatnią cyfrę numeru (np. wyprawienie 12345 w kierunku parzystym → 12344) i pisze o tym na stopce okna.
- **Każdy szlak przyległy do posterunku ma swój dziennik.** Wszystkie tory szlakowe do tego samego sąsiada po tej samej linii są w jednym dzienniku (nazwa: „sąsiad L. linia”). Stanowisko widzi dzienniki posterunków ze swojego obszaru sterowania.
- Po zalogowaniu w każdym dzienniku pojawia się wpis „Dyżur objął … dnia … o godzinie …”. Po północy dziennik dostaje nową stronę z wpisem „Zmiana doby”.
- Przyciski nad dziennikiem: **↓ przyjęcie** i **↑ wyprawienie** pociągu otwierają okno z polami Nr pociągu, Tor stacyjny, Droga wolna, Pociąg odjechał (domyślnie bieżąca godzina), Pociąg przyjechał, „Po torze lewym” (przy szlaku wielotorowym) i Uwagi. Pole wyboru przy godzinie oznacza „nie dotyczy” (`---`). Pozostałe przyciski (dróżnicy, telefonogram, wpis dowolny, stan szlaku) dojdą w kolejnych etapach.
- Kolumny dziennika: 1 – numer nieparzysty, 2 – parzysty (wg ostatniej cyfry), 3 – tor, 4 – droga wolna, 5 – pociąg odjechał, 6 – pociąg przyjechał, 7 – uwagi. Niekompletny wiersz ma tło pomarańczowe, brakujące pola niebieskie. Wymagane są numer, tor, odjazd i przyjazd, a przy blokadzie półsamoczynnej i telefonicznym zapowiadaniu także droga wolna.
- **Godziny w dzienniku** (jak na stanowisku ISDR): klik w komórkę otwiera edytor – pole z godziną (pusta komórka – bieżąca; kółko myszy na lewej połowie zmienia godziny, na prawej minuty), pole wyboru – **druga godzina**, gdy potrzebna (komórka pokaże np. „08:00 / 08:12”), czerwony krzyżyk albo Esc – rezygnacja, Enter albo kliknięcie gdzie indziej – zapis. Prawy przycisk daje „Bieżąca godzina”, „Nie dotyczy (---)” i „Wyczyść”. Numer, tor i uwagi zmienia się dwuklikiem (Enter – zapis, Esc – rezygnacja).
- Wpisów się nie kasuje: każda zmiana to nowa wersja wiersza, a podpowiedź na wierszu pokazuje historię (kto, kiedy, co). Kolejne zmiany tego samego pola w ciągu 10 s (np. kółkiem) tworzą jedną wersję.
- Pod dziennikiem: |< < data > >| (strony = doby) oraz ^ v (pierwszy i ostatni wpis), „Prowadzi:”.
- **Zapis dzienników do pliku** (E8d): dzienniki ruchu i telefoniczne zapisują się po każdej zmianie do `<układ>.edr_dzienniki.json` obok pliku układu (najpierw plik `.tmp`, potem podmiana z kopią `.bak` – przerwany zapis nie psuje pliku). Przy następnym uruchomieniu dzienniki wczytują się i są prowadzone dalej (ciągła historia). Wpisy z wcześniejszych uruchomień są historią: nie zajmują torów i nie liczą się w wykazie pociągów. Uszkodzony plik zostaje bez zmian, a dzienniki tej sesji nie są zapisywane (komunikat na stopce okna EDR). Zapis działa tylko na pulpicie – selftest, zrzuty i `--edrtest` nie zostawiają plików obok układu.
- **Wzory telefonogramów** (E8e, wg Ir-1, plik `Data/telefonogramy.json`): w oknie telefonogramu lista „Wzór telefonogramu” – „[bez szablonu]” albo jeden z 22 wzorów (9–32). Po wyborze wzoru okno pokazuje jego pola (godzina, tor szlakowy z listy torów dziennika, numer pociągu, powód, tryb zamknięcia …), a treść składa się sama (podgląd na bieżąco). Wzory zmieniają stan bieżący szlaku same: 21 – tor zamknięty, 21a – zamknięty po przyjeździe wskazanego pociągu, 22 – otwarty, 16 / 16a – telefoniczne zapowiadanie (droga wolna wymagana), 17 / 17a – zasadnicze, 23 / 24 / 26 – ruch dwukierunkowy, 25 / 27 – rodzaj ruchu zasadniczy. Ręczna zmiana stanu szlaku – tylko przy „[bez szablonu]”.
- **Znacznik wpisu w wykazie** (jak w EDR.exe): szary pasek przy prawej krawędzi „Z kierunku post.” – przyjęcie pociągu jest wpisane do dziennika; przy lewej krawędzi „W kierunku post.” – wpisane jest wyprawienie.
- **Wykreślenie wpisu** (E8d): PPM na wierszu pociągu albo wpisie dowolnym → „Wykreśl wpis…”, przyczyna obowiązkowa. Wpis zostaje w dzienniku przekreślony i szary, z „Wykreślono: przyczyna” w uwagach (też w wyciągu), nie zajmuje toru i nie da się go zmienić. Jazda do km i z powrotem – wykreśla się oba wiersze. Wpisów automatycznych (dyżur, północ) i telefonogramów nie wykreśla się.
- **Tor szlakowy z uwag** (E8d): „po torze nr N” w uwagach (także „po zamkniętym torze nr N”) wskazuje tor szlakowy zajęty przez pociąg; „po torze lewym / prawym” – tor wg kierunku zasadniczego. EDR odrzuca uwagi z dwoma numerami, z numerem spoza szlaku, z lewym i prawym naraz oraz z numerem sprzecznym z „lewym / prawym”.
- **Droga wolna** (E8d) jest wymagana także na szlaku jednotorowym (zawsze), przy zamkniętym torze szlakowym i przy ruchu jednotorowym (tor zasadniczo jednokierunkowy prowadzony dwukierunkowo) – obok ustawień dziennika i telefonicznego zapowiadania.
- **Godziny** (E8d): można wpisać bez dwukropka – „0812” i „812” to 08:12. „Pociąg przyjechał” nie może być wcześniej niż „pociąg odjechał”, z wyjątkiem przejścia przez północ (odjazd od 18:00, przyjazd przed 6:00 – następna doba).

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
Przycisk **„Tablica przebiegów”** pokazuje przebiegi układu – z tablicy zaprojektowanej w edytorze zobrazowania (pole `przebiegi` w pliku układu), a gdy jej nie ma – wyznaczone z układu: zwrotnice, odcinki, drogę ochronną i zależności specjalne (gwiazdka `*` – z zależności specjalnych). Na komputerze z komputerem zależności (gra jednoosobowa, serwer) pod tablicą jest panel **zależności specjalnych** wybranego przebiegu. Zależności wpisuje człowiek, a program sam niczego nie dopisuje:
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
  Punkty szybciej: Ctrl + przeciągnięcie maluje punkty po osi toru co 2 m (samo przeciągnięcie przesuwa mapę); Shift + klik dwa razy przy linii OSM dodaje węzły OSM z odcinka między kliknięciami; G dodaje punkty śladu (narzędzie Ślad), O – węzły OSM do 8 m od toru. Punkty odstające (pomyłka kliknięcia, węzeł OSM przy rozjeździe) są pomijane i oznaczone „pominięty … cm”; X wyłącza i włącza pomijanie.
- **Ślad (F):** budowa toru po osi z OpenStreetMap / OpenRailwayMap albo ze zdjęcia i liczenie łuków. Pomarańczowe linie to osie torów z OSM (te same dane co OpenRailwayMap; pobierają się same – także po przesunięciu mapy i w obróconym widoku Schemat; w panelu „Podkłady” – „sieć torów OSM (linie)” i „Pobierz sieć OSM z widoku”). Kliknij przy linii w miejscu początku toru, potem w miejscu końca – ślad biegnie po liniach, także przez rozjazdy (przy rozgałęzieniu kliknij po drodze). Klik obok linii albo z Ctrl dodaje punkt ze zdjęcia, a Ctrl + przeciągnięcie maluje punkty po osi toru co 2 m (samo przeciągnięcie przesuwa mapę). Na wykresie krzywizny widać krzywiznę śladu (poziomo – łuk, wysokość = R; pochyło – krzywa przejściowa; zero – prosta), a w panelu „Ślad” listę łuków z R, L i kątem. Baza krzywizny dobiera się sama (można ją wpisać). Enter (albo „Tor po śladzie”) liczy propozycję toru, drugi Enter („Utwórz tor”) tworzy tor jako wielobok, a „Utwórz jako trasę z elementów” – jako trasę z elementów; po utworzeniu edytor przechodzi na Wskaż z nowym torem zaznaczonym. Gdy dopasowanie nie trzyma się śladu (np. bardzo długi ślad z wieloma łukami), pojawia się komunikat zamiast toru – podziel ślad na krótsze kawałki. Gdy przy bazie dobieranej samej tor wyraźnie nie trzyma się śladu, program próbuje też mniejszych baz (40 i 30 m) i bierze najlepiej pasujący tor – inaczej dwa łuki z krótką prostą między nimi mogłyby wyjść jako jeden (opis propozycji podaje wtedy „łuki rozpoznane przy bazie … m”). Gdy tor i ślad się rozchodzą, odcinek śladu jest gruby i czerwony, a propozycja zaczyna się od uwagi: „ślad przeskakuje na sąsiedni tor” (tor skręca tam esem jak przez połączenie torów – cofnij ślad Backspace do miejsca przed przeskokiem i klikaj gęściej po jednym torze albo z Ctrl na osi toru) albo „tor odchodzi od śladu” (przeskok albo nierozpoznany łuk – sprawdź na zdjęciu, wpisz mniejszą bazę krzywizny albo podziel ślad). Pojedyncze punkty odstające (węzeł OSM obok, pomyłka kliknięcia) są nadal pomijane i opisane „pominięty … cm”. Backspace cofa krok, Esc czyści ślad albo odrzuca propozycję. Z OSM promień wychodzi z dokładnością kilku procent, a krzywych przejściowych nie widać – popraw je potem „Łukiem z punktów” na zdjęciu. Ślad nie zapisuje się w projekcie.
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
- **Łuk z opisu planu** (w tym samym panelu): na zeskanowanym planie schematycznym rysunek w poprzek nie jest w skali, ale opis łuku jest dokładny. Wpisz to, co jest na planie: R, L, styczną T, długość łuku Ł, kąt zwrotu α, km początku (PŁ / PKP) i końca (KŁ / KKP). „Zastosuj z planu” ustawia R i L – wprost albo dobrany do T, Ł, km początku lub km końca (km – gdy tor ma kilometraż). Porównanie plan / rysunek pokazuje różnice: inny kąt albo km znaczy, że proste są narysowane inaczej niż na planie.
- **Uchwyty na torze** (Wskaż, zaznaczony tor):
  - kwadracik w środku prostej przesuwa ją równolegle, a sąsiednie proste zachowują kierunek;
  - romb na początku albo końcu łuku przesuwa się wzdłuż prostej, a R dobiera się tak, żeby łuk zaczynał się albo kończył w tym miejscu.

  Wierzchołka nie trzeba widzieć.
- **Punkty z OpenStreetMap** (w „Łuku z punktów” klawisz O): program pobiera osie torów w widoku z OSM (Overpass) i dodaje jako punkty dopasowania węzły leżące do 8 m od toru. Rysujesz nadal ty, OSM daje tylko punkty.
- **Uwagi do łuków** (na wykresie i w „Sprawdź”):
  - skok krzywizny bez krzywej przejściowej (tory główne albo V > 40 km/h);
  - za krótka prosta między łukami (§ 36: tor główny V/2,5, nie mniej niż 20 m; pozostałe 10 m);
  - promień za mały dla Vmax;
  - przejściowa krótsza niż wynika z prędkości;
  - załamanie na styku dwóch torów (koniec jednego – początek albo koniec drugiego) powyżej 0,1° bez łuku – wyrównaj tory albo połącz je (Ctrl+J) i wpisz R. Rozjazd może wychodzić za koniec toru, jeśli dalej na styku jest tor na wprost; inaczej uwaga mówi, o ile metrów i dlaczego.
- **Przejściowe z prędkości** (przycisk w panelu toru): dla każdego łuku program liczy przechyłkę h i długość krzywej przejściowej L ze wzorów kalkulatora przechyłki (Vmax toru, R łuku, rozstaw szyn 1435 mm) i wpisuje je. Wierzchołek odsuwa się tak, żeby środek łuku został na miejscu, czyli na zdjęciu. Ostrzega, gdy promień jest za mały dla tej prędkości albo gdy L się nie mieści. Wartości dopuszczalne są przykładowe, jak w kalkulatorze. Najpierw wpisz Vmax toru.
- **Usunięcie kawałka toru:** PPM na torze we Wskaż otwiera menu: „Usuń ten kawałek” usuwa prostą albo cały łuk pod kursorem, a tor rozcina się na dwa (albo skraca, gdy to koniec). „Rozetnij tor tutaj” daje dwa tory stykające się w tym miejscu – także w łuku (łuk dzieli się na dwa o tym samym R, kształt się nie zmienia; łuku z krzywymi przejściowymi nie rozetniesz w środku). Wszystko, co leży za cięciem (rozjazdy, urządzenia, odcinki, perony, przejazdy, punkty km, profil), przechodzi na nowy tor z kilometrażem od jego początku. Co leżało w usuniętym kawałku, znika. Program nie tnie w obrębie rozjazdu ani toru łączącego łącznika.
- **Zaznaczanie kilku elementów (Wskaż):** Shift + klik (albo Ctrl + klik) dodaje element do zaznaczenia albo go zdejmuje: tor, rozjazd, skrzyżowanie, urządzenie, odcinek, peron, przejazd, punkt km. Zwykły klik na torze, potem Shift + klik na drugim – zaznaczone oba. Shift + przeciągnięcie zaznacza prostokątem wszystko, co leży w nim w całości. Panel pokazuje listę zaznaczonych. Del albo „Usuń zaznaczone” usuwa wszystkie naraz, a jedno Ctrl+Z je przywraca. Esc albo zwykły klik kończy zaznaczenie.
- **Łączenie torów:** zaznacz dwa albo więcej torów (Shift + klik) i wciśnij Ctrl+J, „Połącz tory” w panelu albo „Połącz zaznaczone tory” z menu PPM. Program łączy po kolei najbliższe końce: stykające się – wprost (przy załamaniu daje łuk R z paska w miejscu styku), oddalone – stycznie łukiem R z paska albo łukami odwrotnymi, gdy tory są przesunięte (R zmniejsza się, gdy większy się nie mieści). Zostaje tor, do którego się dołącza (numer, rozjazdy, urządzenia). Wszystko, co leżało na drugim, przechodzi na tor wynikowy z kilometrażem od jego początku. Gdy tory mają przeciwne kierunki, odwraca się ten, na którym nic nie leży. Nie łączy się torów z elementów, torów łączących łączników ani początku odgałęzienia rozjazdu. Jedno Ctrl+Z cofa łączenie.
- **Uchwyt łuku:** zaznaczony tor ma kółko w środku każdego łuku. Przeciągnięcie zmienia promień, a R widać na żywo. Przeciągnięcie do wierzchołka usuwa łuk (R 0). Załamanie bez łuku ma kreskowane kółko po wewnętrznej stronie kąta: odciągnięcie go tworzy łuk.
- **R 0 na pasku:** w trybie Łuk łuk przechodzi przez punkt pod kursorem, a promień wynika z miejsca kliknięcia. Z Alt powstaje załamanie bez łuku.
- **Edycja:** klik zaznacza tor i otwiera panel z numerem, kategorią, elektryfikacją, Vmax, długością w terenie, listą elementów oraz R i L każdego wierzchołka. Wierzchołki przeciąga się myszą. Dwuklik na boku dodaje wierzchołek, Del usuwa wierzchołek albo tor (odgałęzienie rozjazdu – razem z rozjazdem). W panelu jest też „Tor równoległy” (międzytorze, strona) i „Odwróć kierunek”. Błędy geometrii, np. łuk nie mieści się między wierzchołkami, widać jako czerwone kółko i opis w panelu.

- **Rozjazd (R):** typ wybierasz z listy na pasku (katalog w `Dane/rozjazdy.json`). Wciśnij na torze w miejscu początku rozjazdu (PR) i przeciągnij w stronę odgałęzienia. Program zapyta o numer. Jeśli puścisz na torze sąsiednim, powstanie łącznik: dwa rozjazdy i tor łączący. Tory mogą odbiegać od równoległości do 3°. Dwa osobno postawione rozjazdy też da się złączyć w łącznik: narzędziem Tor zacznij na końcu odgałęzienia jednego i kliknij odgałęzienie drugiego. Drugi rozjazd przesunie się tak, żeby skosy się zeszły. Jeśli puścisz obok toru (co najmniej 2 m od niego), powstanie od razu tor odgałęźny: skos rozjazdu, łuk zwrotny o promieniu R z pola na pasku i prosta równoległa w odstępie, w którym puściłeś (co 0,25 m). Puszczenie blisko toru daje samo odgałęzienie do końca rozjazdu. Odgałęzienie przedłużasz narzędziem Tor od jego końca. Wierzchołek za rozjazdem przesuwa się tylko wzdłuż skosu, więc kąt odgałęzienia zawsze zgadza się z typem rozjazdu. Rozjazd krzyżowy (Rkp, Rkpd) i skrzyżowanie (St) wstawiasz tak samo: wciśnij na torze w miejscu środka i przeciągnij w stronę toru przecinającego. Powstanie on pod kątem typu, np. 1:9, a dalej przedłużasz go narzędziem Tor od końców. Można też kliknąć na istniejącym przecięciu dwóch torów.
- **Przesuwanie:** rozjazd przeciągasz myszą od razu, bez wcześniejszego zaznaczania. Łącznik jedzie w całości, nawet gdy chwycisz go za drugi rozjazd. Strzałki przesuwają zaznaczony rozjazd wzdłuż toru o 0,1 m (z Shift o 1 m). Zaznaczony tor albo wierzchołek strzałki przesuwają o 5 cm (z Shift o 0,5 m), w Schemacie góra / dół zmienia międzytorze. Ctrl + przeciągnięcie zaznaczonego toru przesuwa cały tor razem z rozjazdami. PPM na rozjeździe otwiera menu: strona, odwrócenie, położenie zasadnicze, zwrotnica ręczna, usunięcie.
- **Rozjazd w panelu:** numer (Enter – zastosuj; zmiana też z menu PPM „Zmień numer…”, w łączniku oba numery naraz; numer zajęty w posterunku przez inny rozjazd – pytanie „Zamienić numery?”, tamten dostaje zwolniony numer, Ctrl+Z cofa obie zmiany naraz; wpis w polu bez Enter jest przyjmowany także przed zapisem, „Cofnij”, „Ponów”, „Sprawdź” i zamknięciem), typ, położenie zasadnicze, zwrotnica ręczna, km PR, zmiana strony i odwrócenie, uwagi (np. nachodzi na inny), wstawki międzyrozjazdowe (w łączniku: długość między końcami rozjazdów, najmniejsza dopuszczalna v/10, nie mniej niż 6 m, i ψ), przyciski „Łącznik z rozjazdem N” (te same pozycje są w menu PPM). Taki łącznik powstaje też wtedy, gdy odgałęzienie było już dociągnięte do drugiego rozjazdu narzędziem Tor.
- **Rozjazd łukowy:** rozjazd postawiony na łuku toru sam się wygina. Odgałęzienie do środka łuku daje rozjazd jednostronny (Rłj), na zewnątrz dwustronny (Rłd). Panel podaje promienie obu torów i prędkość na zwrot. Po zmianie łuku toru rozjazd od razu się dopasowuje. Rozjazd nie może leżeć na krzywej przejściowej ani na granicy prostej i łuku – uwaga podaje, o ile go przesunąć w każdą stronę. **Montaż na łuku jest samoczynny:** gdy wstawisz albo przesuniesz rozjazd (także łącznik, także km w panelu) na granicę łuku albo na przejściową, łuk toru sam się dopasowuje – granica łuku wychodzi poza rozjazd (zmiana R albo krótsza przejściowa jednego łuku, wierzchołek i proste zostają), jeśli oś toru przesuwa się przy tym najwyżej o 30 cm i przejściowa nie znika. W łączniku oba rozjazdy poprawiają się razem – tylko wtedy, gdy po poprawce oba leżą dobrze (drugi rozjazd staje na nowym przecięciu skosu); inaczej zostaje przycisk „Przesuń: przesuń łącznik …” w panelu każdego z nich. Komunikat mówi, co się zmieniło, a Ctrl+Z cofa samą poprawkę (rozjazd zostaje). Gdy trzeba więcej, panel rozjazdu (i menu PPM) pokazuje warianty: **„Popraw łuk: …”** (rozjazd w całości na łuku, łuk zaczyna się za rozjazdem albo kończy przed nim; R / L przed i po oraz o ile najwyżej przesunie się oś), **„Przesuń: …”** (rozjazd albo łącznik w całości na prostą albo na stały promień) i **„Typ: …”** (gdy tor zwrotny rozjazdu łukowego ma R < 140 m). Urządzenia, inne rozjazdy, punkty km i niweleta toru zostają w tym samym miejscu w terenie, choć długość toru się zmienia. Na działanie symulatora ta uwaga nie wpływa: to zgodność z rzeczywistym układem.
- **Napraw tory i łuki** (przycisk **🔧 Napraw** na pasku – cały układ, w panelu toru „Napraw tor i łuki” – jeden tor): usuwa zbędne wierzchołki (powtórzone punkty, załamania poniżej 1 cm), dopasowuje łuki, które się nie mieszczą (R w dół po 1 %, tylko gdy wszystkie łuki toru się zmieszczą), i montuje rozjazdy leżące na granicy łuku albo na przejściowej (jak wyżej, do 30 cm przesunięcia osi). Wszystko to jedna zmiana – Ctrl+Z cofa całość. Lista „Naprawa” w panelu bocznym podaje zmiany (✔) i to, czego program sam nie poprawił (⚠, np. rozjazd, który trzeba przesunąć, rozjazdy nachodzące na siebie, wychodzące poza koniec toru) – te sprawy rozstrzygasz sam przyciskami w panelu rozjazdu.
- **Łącznik na łuku:** na torach w łuku skosy obu rozjazdów łącznika rozchodzą się (tory obracają się na jego długości). Program wstawia wtedy we wstawkę łuk wyrównujący (największy, jaki się mieści, w środku wstawki) i ustawia drugi rozjazd symetrycznie. Panel rozjazdu podaje jego promień i kąt. Uwaga pojawia się dopiero, gdy łuk wyrównujący ma mniej niż 140 m albo skosy rozchodzą się o więcej niż 3°.
- **Dopasuj promienie** (panel toru, gdy łuk się nie mieści): zmniejsza R tylko w tych łukach, nie poniżej 140 m; obiekty toru zostają w terenie. We „Wskaż” rozjazd przeciąga się wzdłuż toru, a łącznik idzie za nim.
- **Urządzenie (U):** rodzaj wybierasz z listy na pasku: semafor świetlny, tarcza ostrzegawcza, tarcza manewrowa, wykolejnica, kozioł oporowy, licznik osi, złącze izolowane, ukres. Wciśnij na torze w miejscu urządzenia i przeciągnij w stronę jazdy, której dotyczy, po stronie, po której stoi. Kozioł przyciąga się do końca toru. Licznik, złącze i ukres stawiasz kliknięciem. Program podpowiada nazwę (A, B…, Tm1, Wk1, lo1). We Wskaż urządzenie przeciągasz wzdłuż toru albo przesuwasz strzałkami. PPM daje menu (strona, kierunek, usuń), a w panelu są nazwa i km.
- **Konfiguracja semafora i tarczy** (panel, znaki wg BSK ISDR „Oznaczenia urządzeń srk”): układ gotowy (2-, 3-, 4- i 5-komorowy, tarcza ostrzegawcza, tarcza manewrowa karzełkowa albo wysoka) albo komory po kolei od masztu. Każda komora ma kolor (czerwona, zielona, pomarańczowa, biała, niebieska) i znacznik „pulsuje”, kolejność zmieniasz ↑ ↓, jest też ✕ i „+ komora”. Komory można też wpisać tekstem, np. `B*PCZ*`. Ustawiasz ponadto, która komora świeci w stanie zasadniczym, oraz mocowanie: na maszcie (słup), na wysięgniku, na bramce albo karzełkowy. Semafor może też mieć pas pomarańczowy, pas zielony, W24 i W21 z cyfrą prędkości (np. 6 = 60 km/h). Wszystko to jest w polu „Konfiguracja semafora”.
- **Bramka i wysięgnik wspólny:** sygnalizatory na jednej konstrukcji stoją w jednym przekroju – przesunięcie jednego (przeciągnięcie, strzałki, km) przesuwa wszystkie, belka idzie w poprzek torów (także w Schemacie). Bramka narysowana wcześniej krzywo: zaznacz semafor, który stoi dobrze, i „Ustaw bramkę w przekroju …” w panelu. Komory semafora na bramce wpisuje się od dołu, jak na maszcie – pierwsza najdalej od belki, ostatnia (górna, zwykle zielona) przy belce; wpisane od góry (z planu, od belki) odwraca przycisk „↕ Odwróć kolejność”. **Belkę można przesunąć bez semaforów:** przeciągnij ją we Wskaż (chwyć za belkę, nie za komory) albo wpisz przesunięcie w panelu semafora („Belka przesunięta względem semaforów [m]”, „Zeruj”). Znaki semaforów wiszą wtedy na belce, a ich miejsce na torze (km, które liczy się w zależnościach) zostaje – kreska w poprzek toru i kreskowana linia do belki. Przesunięcie semafora przesuwa bramkę razem z nim, z tym samym przesunięciem belki.
- **Semafory na bramce** (mocowanie „na bramce”): rysunek jak na planach schematycznych (Semaforek, Łódź Widzew – L3 / L5, S1 / S3, D / E). Semafory na bramce w tym samym miejscu (do 8 m wzdłuż toru, ten sam kierunek) tworzą jedną bramkę: belkę w poprzek wszystkich ich torów, na obu końcach słup w stronę komór zakończony kreską stopy. Semafor na bramce nie ma własnej stopy, tylko wisi na belce. Przy belce jest komora górna i kreska W24, a na drugim końcu W21, pasy świetlne i nazwa.
- **Łączenie bramek i wysięgników**: w panelu sygnalizatora (pole „Wspólna konstrukcja”) albo w menu PPM wybierz „Połącz z…” i sygnalizator w pobliżu (ten sam kierunek, do 60 m) – zawiśnie na tej samej belce, a dołączony przesunie się na swoim torze w miejsce konstrukcji. Na wysięgniku powstaje wspólny wysięgnik nad torami z jednym słupem, na bramce – jedna bramka. „Odłącz” daje osobną konstrukcję. Zmiana mocowania bramka ↔ wysięgnik dotyczy całej konstrukcji, „na maszcie” odłącza.
- **Słupy i zasięg bramki**: w polu „Konstrukcja” wybierasz słupy – dwa (po obu stronach), jeden po lewej albo jeden po prawej (patrząc w kierunku jazdy) – i tory, nad którymi belka przechodzi bez sygnalizatora (np. bramka z jednym semaforem nad dwoma torami). Dołączony sygnalizator przejmuje te ustawienia.
- **Światło komory** (panel semafora, lista przy każdej komorze): stałe (bez kresek), stałe lub pulsujące (2 kreski na obwodzie), tylko pulsujące (4 kreski), nieczynna (komora kreskowana, nie świeci). W zapisie tekstem „*” po literze oznacza stałe lub pulsujące, „**” tylko pulsujące, a „-” nieczynną, np. „B**PCZ*” albo „B-P-CP-Z-” (czynna tylko czerwona). Komory nieczynne nie idą do symulatora (liczba komór, sygnalizacja manewrowa) ani do zobrazowania.
- **Licznik osi, złącze izolowane, ukres** (urządzenia bez kierunku): przy wstawianiu stają po tej stronie toru, po której klikniesz. Później PPM „Zmień stronę”. Licznik osi rysuje się tuż przy torze, więc nie wchodzi na komory semafora w tym samym miejscu. Jego nazwa jest po drugiej stronie toru.
- **Znaki Ig-10:** rozjazd ma kreskę początku, numer i czarny klin w kącie między torami, po stronie kierunku zamkniętego, jak w Ig-10 zał. 8 i na planach schematycznych. Klin liczony jest z geometrii rozjazdu. Rozjazd krzyżowy ma dwa kliny w kątach ostrych przy punkcie matematycznym. Wzory znaków są w bazie `Dane/symbole_ig10.json`. Przycisk „Symbole Ig-10…” pokazuje wszystkie znaki. Po poprawkach w pliku użyj „Wczytaj ponownie”.
- **W4 i W5:** na liście urządzeń (wskaźnik zatrzymania się czoła pociągu W4, wskaźnik granicy przetaczania W5, znaki wg Ig-10 s. 33 i 30). Stawiasz je jak semafor: wciśnij na torze i przeciągnij w stronę jazdy, po stronie wskaźnika. **W5 w symulatorze:** granica przetaczania. Manewr w stronę szlaku kończy się na semaforze wjazdowym (granica posterunku). Gdy za semaforem wjazdowym, na torze szlakowym, stoi W5 zwrócony w stronę szlaku, jest też przebieg manewrowy „na szlak” do W5 (np. `M8 2ŁW Manewr`, wymaga kierunku WYJAZD). Powrót od semafora wjazdowego na sygnał zastępczy jest dozwolony tylko wtedy, gdy tabor stoi między semaforem a W5 (bez W5 – w odcinku tuż za semaforem).
- **Odcinek (O):** odcinki kontroli niezajętości. Najpierw stawiasz granice: liczniki osi albo złącza izolowane (narzędzie Urządzenie). Program sam wyznacza kawałki torów między granicami, także przez rozjazdy i łączniki. Kliknięcie narzędziem Odcinek na torze nadaje nazwę całemu kawałkowi. Podpowiedź to „It” i numer toru (odcinek torowy, np. It1) albo „Iz” i numery rozjazdów (odcinek zwrotnicowy, np. Iz1/2), wpisać można dowolną. Kolor na planie oznacza odcinek, czerwone kreski – kawałek bez odcinka. Kliknięcie w kawałek, który ma już nazwę, zaznacza ten odcinek. Panel: nazwa, rodzaj (liczniki osi / obwód torowy), długość, kawałki torów z km, granice, czy to odcinek zwrotnicowy. Nazwę odcinka (pod torem) przeciągasz we Wskaż.
- **Peron:** wciśnij na torze w miejscu początku krawędzi peronowej i przeciągnij wzdłuż toru do jej końca, po stronie peronu. Panel: numer, km od–do, długość krawędzi (z km linii PP / KP), odległość krawędzi od osi toru, wysokość nad główką szyny, szerokość, „Zmień stronę”, „Dwukrawędziowy” (druga krawędź przy sąsiednim torze). Krawędź jest podwójną linią jak w Ig-10.
- **Przejazd:** kliknięcie na torze daje drogę prostopadłą, przeciągnięcie wzdłuż osi drogi daje kąt i długość drogi na planie. Panel: opis, kategoria A–F albo przejście (P), km, kąt, szerokość, rogatki (mechaniczne / elektryczne). Na planie: krawędzie drogi kreskowane, napis z kategorią i km linii.
- **Obiekt (I) – obiekty inżynieryjne** (Ig-10 zał. 8): rodzaj z listy obok przycisku – most, wiadukt kolejowy nad drogą, wiadukt nad innym torem, linia w tunelu, wiadukt drogowy nad torami, tunel pod linią, przepust, przejście piesze pod torami, kładka, ściana oporowa, mur oporowy.
  - Most, wiadukt, tunel, ściana, mur: kliknij na torze w miejscu początku (OD), potem w miejscu końca (DO) albo przeciągnij wzdłuż toru. Ściana i mur stają po tej stronie toru, po której klikniesz DO. PPM / Esc – od nowa.
  - Przepust, tunel pod linią, przejście, kładka, wiadukt drogowy: kliknij na torze w osi przeszkody (prostopadle) albo przeciągnij wzdłuż jej osi (kąt i zasięg na planie).
  - Jeden obiekt pod kilkoma torami: zaznacz obiekt, w narzędziu Obiekt Ctrl + klik na drugim torze – tor dołożony z własnym km. „Rozdziel na tory” (panel, PPM) robi osobny obiekt na każdym torze.
  - Panel: rodzaj, nazwa / numer, co przekracza (droga, rzeka, linia), km od–do albo km, kąt, zasięg, światło, szerokość drogi pod wiaduktem, strona i odległość muru. Warstwa „budowla”.
  - Profil: „Niweleta z terenu” pomija NMT na moście, wiadukcie i w tunelu (z zapasem 5 m) – niweleta przechodzi prosto, bez doliny pod wiaduktem. Na wykresie obiekty brązowym pasem z nazwą, teren pod nimi przerywany.
  - „Sprawdź”: obiekt poza torem, km od–do krótszy niż 1 m, dwa obiekty w tym samym miejscu toru (ściana / mur – po tej samej stronie), obiekt bez nazwy (uwaga). Rozjazd i urządzenia na obiekcie – bez uwag.
- **Kilometraż (km):** wciśnij na torze w miejscu, gdzie znasz km linii, i przeciągnij w stronę rosnącego km. W oknie wpisz numer linii i km, np. `17 25.452`. Drugi punkt na tym samym torze ustala kilometr skrócony albo wydłużony. Pozostałe tory biorą km z rzutu na tor z kilometrażem. Na planie są wskaźniki hektometrowe i kilometrowe. Km pod kursorem pokazuje pasek stanu, a panele peronu, przejazdu i toru podają km linii.
  - **Słupki km z OSM:** w narzędziu km panel „Kilometraż ze słupków OSM” → „Szukaj słupków km w widoku”. Słupki z OpenStreetMap przy torach (do 25 m) pokazują się jako propozycje (fioletowe romby, opis „l. 17 km 3.000 ?”), pogrupowane w osie: jedna linia – jedna oś na torze, przy którym stoi najwięcej jej słupków. Zaznacz punkty albo całą oś i „Dodaj zaznaczone” (Ctrl+Z cofa). „położenie szacowane” – autor mapy wstawił słupek z interpolacji, sprawdź km na zdjęciu albo planie. Słupkom bez numeru linii w OSM wpisz numer w panelu.
- **Podpowiedzi z OSM** (panel w narzędziach Tor, Rozjazd i Urządzenie): „Szukaj w widoku” pobiera z OpenStreetMap sygnalizatory, wykolejnice, kozły, zwrotnice i numery torów (`railway:track_ref`) w widoku i do 200 m dookoła. Propozycje dotyczą tylko torów już narysowanych: urządzenie na osi toru (do 10 m od obiektu OSM) z rodzajem z tagów (semafor, tarcza manewrowa, ostrzegawcza, W4, W5), nazwą z `ref` i kierunkiem z „forward / backward”; numer zwrotnicy z OSM najbliżej PR rozjazdu (do 15 m); numer toru, jeśli tor OSM z numerem leży przy co najmniej połowie toru (do 2,5 m od osi, łączniki pomijane). Fioletowe znaki na planie, w panelu grupy z zaznaczaniem, „Przyjmij zaznaczone” – jeden krok (Ctrl+Z cofa). Urządzenie, które już stoi w pobliżu na tym torze, nie jest proponowane. Miejsca sprawdź na zdjęciu, funkcję semafora i komory ustaw w panelu. W Łodzi OSM ma mało sygnalizatorów (ok. 25 na Widzewie, 4 z nazwą), za to dużo numerów zwrotnic i torów.
- **Profil… (profil podłużny):** dla zaznaczonego toru. „Teren z NMT GUGiK” pobiera wysokości terenu co krok (domyślnie 10 m). „Niweleta z terenu” układa odcinki stałego pochylenia (tolerancja, najkrótszy odcinek – domyślnie 0,3 m i 100 m). Załom przeciągasz na wykresie w pionie albo wpisujesz w tabeli km, Z i promień łuku pionowego Rv. Wykres pokazuje pochylenia ‰, długości oraz nasyp (pomarańczowy) i przekop (niebieski). NMT to teren, nie główka szyny: na wiaduktach i w przekopach popraw niweletę ręcznie. Pochylenia widać też na planie (Ig-10 s. 58), a Z niwelety na pasku stanu.
- **Funkcja semafora** (panel semafora): wjazdowy, wyjazdowy, drogowskazowy, odstępowy, powtarzacz (Sp), PK. Od niej zależy symbol na pulpicie, więc „Sprawdź” przypomina o semaforze bez funkcji.
- **Powtarza** (panel tarczy ostrzegawczej i semafora-powtarzacza): semafor, którego sygnał urządzenie powtarza, albo szlak (informacja z sąsiedniej stacji). Wybierasz z listy. „Zaproponuj” wstawia pierwszy semafor przed urządzeniem w kierunku jego działania (po torze na wprost i przez styki torów, do 3 km) – to tylko propozycja, sprawdź ją. „Sprawdź” przypomina o tarczy bez wskazania i o usuniętym semaforze. W symulatorze tarcza jest zielona, gdy semafor powtarzany podaje sygnał zezwalający (pole `repeats`).
- **Posterunki i szlaki** (panel „Posterunki i szlaki”; gdy jest rozwinięty, tory posterunków są podświetlone ich kolorem, a szlaki – pasem z numerem):
  - **posterunek:** zaznacz tory (Shift + prostokąt albo Ctrl + klik), potem „+ posterunek” – kod (np. LW) i nazwa. Tor przypiszesz też w panelu toru (pole „Należy do”). Rozjazdy i urządzenia należą do posterunku swojego toru. Nazwy semaforów i numery rozjazdów nie mogą się powtarzać w jednym posterunku, ale na różnych posterunkach mogą (np. „A” na każdej stacji);
  - **szlak – narzędzie Szlak (B), OD – DO:** klik – OD (na semaforze, przy końcu toru albo w dowolnym miejscu toru), potem klik – DO; między klikami podgląd drogi do miejsca pod kursorem: zielony pas, opisane końce, semafory odstępowe w ramkach, długość na pasku stanu. Kolejność końców i zwrot semaforów są bez znaczenia. PPM / Esc – od nowa. Klik w środku istniejącego szlaku go zaznacza. **Ctrl + klik na torze** – program sam znajdzie oba końce (semafory wjazdowe posterunków albo koniec toru na granicy obszaru); czerwony podgląd mówi, czego brakuje (np. kursor w obrębie stacji – za semaforem wjazdowym). Gdzie schodzi się kilka torów, droga idzie najpierw najbardziej na wprost;
  - **szlak z torów – „Należy do” w panelu toru:** zaznacz tory szlaku (Ctrl / Shift + klik, Shift + prostokąt) i wybierz w panelu „Należy do” istniejący szlak (tory dochodzą do niego, koniec szlaku przesuwa się na koniec ciągu) albo „+ nowy szlak z tych torów…”. Tory muszą łączyć się końcami w jeden ciąg; końcem szlaku zostaje semafor do 30 m od końca ciągu albo koniec toru. Tory szlaku nie należą do posterunku. Pomarańczowy podgląd to ostrzeżenie – np. na drodze są rozjazdy, czyli zwykle tory stacji nie mają jeszcze posterunku albo semafor wjazdowy nie ma funkcji „wjazdowy” (program pozna granicę stacji dopiero po nich). W panelu szlaku: oznaczenie, **nazwa pełna** (np. „Łódź Widzew – Łódź Andrzejów”) i **skrócona** (np. „ŁW – ŁA”) – proponowane z posterunków na końcach (przy końcu na granicy obszaru z posterunku sąsiedniego, gdy go wpiszesz), „Zaproponuj”; linia, tor szlakowy, blokada (SBL, PBL, telefoniczna), stawność SBL, **kierunek pociągów parzystych** (od końca 1 do 2 albo odwrotnie – EDR dostosowuje według niego ostatnią cyfrę numeru pociągu w dzienniku ruchu; eksport do symulatora jako `evenDir`), dla każdego końca – posterunek sąsiedni (koniec na granicy obszaru) i oznaczenie końca na pulpicie (np. 1G). Na planie przy szlaku – oznaczenie i nazwa skrócona;
  - Del na zaznaczonym posterunku usuwa go (tory zostają bez posterunku), na szlaku – szlak.
- **Sprawdź (F7):** kontrola całego układu. Lista w panelu „Kontrola układu”: błędy na czerwono, uwagi na pomarańczowo. Kliknięcie pokazuje miejsce i zaznacza obiekt. Sprawdzane są: geometria torów, otwarte końce (tor bez połączenia i bez kozła – na granicy obszaru zaznacz w panelu toru „początek / koniec – granica obszaru”), tory bez numeru, rozjazdy bez numeru i ich uwagi, urządzenia poza torem i powtórzone nazwy, nakładające się tory, odcinki (kawałek bez odcinka, odcinki bez granicy między sobą, granica w obrębie rozjazdu), perony, przejazdy, kilometraż, profil, funkcje semaforów, posterunki i szlaki, odstępy SBL (między semaforem odstępowym a następnym semaforem tego kierunku albo końcem szlaku musi być granica odcinka – licznik osi albo złącze; uwaga tylko, gdy odcinki są już projektowane).
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
| J | Łuk z punktów; w nim Tab – także krzywe przejściowe L, W – proste stałe / wierzchołek swobodny, O – punkty z OpenStreetMap, G – punkty śladu, X – pomijanie odstających, Shift + klik ×2 – odcinek linii OSM, Ctrl + przeciągnięcie – malowanie, Enter – zastosuj |
| F | Ślad (po liniach OSM / ze zdjęcia); w nim Enter – tor po śladzie, drugi Enter – utwórz tor, Ctrl + klik – punkt ze zdjęcia, Backspace – cofnij krok, Esc – wyczyść |
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

Obiekt inżynieryjny – klawisz I. Peron, Przejazd i km nie mają klawisza – wybiera się je przyciskiem na pasku.


## Edytor zobrazowania (EdytorZobrazowania2)

Osobny program: `uruchom_edytor_zobrazowania2.bat` albo `app/Lcs.EdytorZobrazowania2/bin/Release/net10.0-windows/EdytorZobrazowania2.exe [plik.zob.json | plik.ut.json]`. Zasada: **najpierw rysunek, potem przypięcie do UT** – rysuje się bez wybierania obiektów UT, program nie odrzuca kliknięć. Plan, decyzje i etapy: `PLAN_EDYTOR_ZOBRAZOWANIA_V2.md`. Stary edytor zobrazowania (`EdytorZobrazowania.exe`, plan `PLAN_EDYTOR_ZOBRAZOWANIA.md`) usunięto 2026-10-22 po etapie E8 – jego pliki (`*.zob.json` w wersji 1) otwiera nowy edytor.

Stan: **etapy E1–E8** (szkielet, rysowanie torów, symbole i opisy, przypinanie do UT, eksport do symulatora, szlaki, klocki i szkic, obraz poglądowy, PNG i wydruk). Działa:
- plik `*.zob.json` w **wersji 2** obok UT (poprzednia wersja w `.bak`); plik starego edytora (wersja 1) program przekształca do wersji 2 (niżej); zapis nie nadpisuje pliku wersji 1 bez pytania;
- zakładki obrazów (+ szczegółowy / + poglądowy, **Kopiuj obraz** – kopia z rysunkiem i przypięciami zakładką obok, usuń, kolejność ◀ ▶), cofnij / ponów (Ctrl+Z / Ctrl+Y);
- płótno: czarne tło, siatka 8; **poruszanie się:** kółko – przybliżenie, Shift + kółko – w poziomie, Ctrl + kółko – w pionie, **przeciągnięcie PPM** albo środkowym przyciskiem – przesuwanie widoku (klik PPM bez ruchu – menu jak dotąd), strzałki bez zaznaczenia – przesuwanie widoku; Home – cały obraz, 1:1;
- **prowadnice rzędów** – narzędzie Prowadnica (R): klik – nowa prowadnica, przeciągnięcie – przesunięcie (poza obraz – usunięcie), PPM – usunięcie; albo liczby w zakładce Obraz; Esc / V – Wskaż;
- **strefy zarezerwowane** przez pulpit (domyślnie panel Polecenia / Historia 480 × 160 j. w prawym górnym rogu) – w zakładce Obraz: rodzaj, „x, y, szer., wys.”, + strefa, ✕;
- prawy panel w zakładkach: Właściwości (zaznaczony odcinek / węzeł, uwagi do rysunku), Do wyjaśnienia, UT (podsumowanie UT, tylko do odczytu), Obraz. Paleta z lewej pokazuje narzędzia, symbole (klawisze 1–9) i klocki kolejnych etapów – wyszarzone do czasu ich etapu;
- **Pióro toru (T):** klik – klik – klik; kierunek co 45° najbliższy myszy, przyklejanie do siatki, prowadnic, węzłów i torów. Koniec: klik na torze albo węźle, Enter, PPM, dwuklik; Backspace – cofnij punkt; Esc – przerwij. Tor zaczęty albo skończony na innym torze – **rozjazd powstaje sam** (odgałęzienie z przerwą przy węźle jak na pulpicie). Tor przecinający inny bez węzła – skrzyżowanie; PPM na węźle – „Rozjazd krzyżowy”;
- **Wskaż (V / Esc):** klik – zaznaczenie, Shift + klik – dodaj / zdejmij, przeciągnięcie na pustym miejscu – prostokąt; przeciągnięcie zaznaczenia albo węzła – przesunięcie (tory się rozciągają); przeciągnięcie niezaznaczonego toru poziomego – **cały rząd w pionie z łącznikami** (łącznik „[” się wydłuża, koniec długiego ukosu ślizga się po nim); strzałki – o 8 j. (Shift – o 40 j.); Del – usuń; Ctrl+A – wszystko; Ctrl+C / Ctrl+V – kopiuj / wklej w miejscu kursora; Ctrl+D – powiel; **Ctrl+G – rozsuń rzędy** (skrajne zostają, środkowe w równych odstępach); PPM – menu (podziel tor, zaznacz rząd, tor ciągły / bez kontroli niezajętości, usuń, wklej). Środkowy przycisk – przesuwanie widoku;
- **Symbole z palety** (ikony rysowane tym samym kodem co pulpit): 1 semafor, 2 tarcza manewrowa, 3 powtarzacz, 4 PK, 5 przerwa izolacyjna, 6 wykolejnica, 7 kozioł (przy końcu toru), 8 przejazd, 9 peron; N – numer toru, O – opis odcinka, X – napis. Klik na torze wstawia symbol (pod kursorem widać jego cień); ostrze domyślnie w stronę środka obrazu; **spacja zaraz po wstawieniu obraca wstawiony symbol** i następne wstawiają się w tym kierunku. Po wstawieniu na płótnie otwiera się pole opisu – wpisz nazwę i Enter (Esc – bez opisu). PPM albo Esc – powrót do Wskaż;
- **numer zwrotnicy:** Wskaż, klik w węzeł rozjazdu, **E** (albo PPM → „Numer zwrotnicy…”); „+” pokazuje położenie zasadnicze – PPM: „+” na wprost / na bok (przerwa przy węźle przechodzi na nieczynne ramię, jak na pulpicie);
- na symbolu: klik – zaznaczenie i zakładka Właściwości (nazwa, odmiana, rozmiar peronu / zasięg przejazdu); przeciągnięcie – przesunięcie wzdłuż toru (peron i pole PIP same stają na środku między torami, krawędzią przy końcu toru, sygnalizatorze albo krawędzi innego peronu); przeciągnięcie opisu – nowe miejsce opisu; **spacja** – obrót (opis w miejscu domyślnym przechodzi na drugą stronę); **E** – opis; Del – usuń; PPM – obróć, opis, opis w miejscu domyślnym, odmiana (semafor z tarczą manewrową / tylko pociągowy / wjazdowy z trójkątem końca przebiegu wyjazdowego / wjazdowy bez tego trójkąta – też na pulpicie; powtarzacz semafora / tarcza ostrzegawcza), usuń. PPM na torze – „Wstaw przerwę izolacyjną tutaj”. Symbole i opisy jadą z torem przy przesuwaniu, rzędach i kopiowaniu;
- odcinek nie co 45°, węzeł nieznanego kształtu, sygnalizator bez nazwy, rozjazd bez numeru – znak ⚠ i wpis w „Uwagi do rysunku”; rysunek nigdy nie jest blokowany;
- **Przypnij do UT (F8):** program przypina rysunek do UT – rozjazdy po numerach zwrotnic, sygnalizatory po nazwach, tory i km między nimi (koniec toru bez kotwicy = koniec toru w UT), rozjazdy bez numeru i symbole bez nazwy – według sąsiedztwa i km. Czego nie jest pewny, trafia do zakładki **„Do wyjaśnienia”**: powód i przyciski kandydatów (klik – przypięcie potwierdzone), „bez przypięcia” (np. przerwa, której nie ma w UT); najazd na pozycję zaznacza element na obrazie i pokazuje obiekt w podglądzie UT. Przypięcia nie giną przy zmianie rysunku; po zmianach F8 jeszcze raz (potwierdzone zostają). Po zmianie UT (np. tor dłuższy) Sprawdź pokazuje błąd „km … nieaktualne – wg UT km … – F8”, a „Do symulatora…” i podgląd – uwagę; F8 liczy km od nowa. Tor narysowany jedną linią może przechodzić przez kilka torów UT połączonych końcami (np. tor stacji i tor szlaku z tym samym numerem) – F8 sam wstawia węzeł w miejscu styku (wygląd bez zmian); rozjazd może być narysowany z torem zasadniczym na ukos (tak jak na planie schematycznym) – F8 rozpoznaje ramiona po semaforach i numerach torów. Opis na obrazie może różnić się od nazwy w UT – Sprawdź pokazuje różnicę z przyciskiem „Weź z UT”;
- **Sprawdź (F7):** błędy (element bez przypięcia, do wyjaśnienia, obiekt usunięty z UT, km dwa razy na obrazie, strzałka blokady bez przypiętego semafora wjazdowego końca szlaku, dwie strzałki sterujące tym samym końcem szlaku) i ostrzeżenia (opis różny od UT, strefa zarezerwowana, odstęp torów i tarcza obok wykolejnicy wg Ie-104 § 4 ust. 15, obraz poglądowy, obiekty UT nieumieszczone); klik w uwagę – zaznaczenie;
- zakładka **UT**: podgląd osi torów UT (przypięte – ciemne, wskazany obiekt – pomarańczowy) i lista obiektów UT nieumieszczonych na obrazie (odstępowe – w etapie E6);
- **Podgląd pulpitu (F9)** – obraz jak na pulpicie symulatora (ten sam kod pulpitu i komputer zależności), nic nie zapisuje; **Do symulatora…** – UT i obrazy jako układ symulatora w `app/LcsWidzew/Data/projekty/<nazwa>.json` (`Data/station.json` bez zmian). Na pulpit trafiają tylko elementy przypięte do UT;
- **szlaki** (klocki w palecie): **koniec szlaku** – klik przy końcu toru szlakowego wstawia strzałkę blokady, semafor wjazdowy (gdy go nie ma) i nazwę sąsiedniej stacji; **odstępy SBL** – klik przy końcu toru: rząd pól w osi toru i kolumna dalszych pól przy krawędzi obrazu (jak na GrM), liczba pól i kierunek kolumny we Właściwościach; **okno numeru pociągu** – prostokąt, na pulpicie czerwony z „odstęp-numer pociągu”, gdy szlak zajęty, szary, gdy wolny (tylko posterunek z PIP). Po F8 puste numery odstępów i nazwa sąsiada („Andrzejów LK 17”) przychodzą z UT; wpisane ręcznie zostają, „Sprawdź” pokazuje różnicę z „Weź z UT”. Na pulpicie tor szlaku za semaforem wjazdowym to kreski odstępów z UT; pole bez odstępu w UT – tylko napis. **Szlak wewnętrzny** między stacjami obszaru LCS (oba końce – semafory wjazdowe) działa tak samo: odstępy po całej drodze szlaku (także przez tory drugiej stacji) do semafora wjazdowego drugiej stacji, nazwa sąsiada – nazwa tej stacji; na każdym obrazie stacji ten sam szlak można narysować przy jej końcu toru;
- **szlak wewnętrzny z obiema stacjami na jednym obrazie:** tor szlaku rysujesz od stacji do stacji; **koniec szlaku** kliknij na torze szlaku przy stacji (strzałka blokady i semafor wjazdowy, gdy go jeszcze nie ma), to samo przy drugiej stacji; **odstępy SBL** – klik na torze szlaku, pola idą w stronę stacji (cień pod kursorem), **spacja** – w stronę drugiej (także po wstawieniu, na zaznaczonych); jest też sama **strzałka blokady**. Te same nazwy semaforów i numery zwrotnic w obu stacjach F8 rozróżnia po położeniu; gdy żadna stacja nie ma jeszcze pewnej kotwicy – potwierdź na liście jeden element w każdej stacji i F8 jeszcze raz;
- **numer toru niewidoczny:** zaznacz numer toru – we Właściwościach (albo PPM) odmiana „niewidoczny – tylko do przypięcia toru”: w edytorze szary w przerywanej ramce, na pulpicie, w PNG i na wydruku go nie ma; F8 dalej przypina po nim tor;
- **przypięcie toru i rozjazdu we Właściwościach:** zaznacz odcinek toru – zakładka Właściwości pokazuje, do którego toru UT jest przypięty (numer, posterunek, długość, **id z UT**, km od–do) i jakie **odcinki kontroli niezajętości** z UT leżą na tym kawałku (nazwy i granice odcinków nadaje się w edytorze UT); „Przypnij do toru UT” – lista torów obrazu (najpierw tory przy rozjazdach na końcach), wybór = przypięcie potwierdzone; „Pokaż w UT”, „bez przypięcia”. Zaznaczony rozjazd – przypięcie z id i „Przypnij do rozjazdu UT”;
- **łączenie torów** (odwrotność PPM → „Podziel tor tutaj”): PPM na węźle między dwoma kawałkami toru na prostej → **Połącz tor tutaj** (węzeł znika, przypięcie do tego samego toru UT zostaje z km obu kawałków); PPM na końcu toru → **Połącz z końcem toru (x, y)** – z najbliższym końcem (do 400 j., co 45°; inaczej połącz piórem T). Nie łączy się załamania, rozjazdu ani węzła z elementem (np. kozioł) – komunikat mówi dlaczego;
- **klocki** (paleta): łącznik „[” (do następnej prowadnicy rzędu), łącznik ukośny, rozjazd krzyżowy, koniec toru – pod kursorem widać cień, **spacja** odbija klocek, klik wstawia: końce klocka zaczepiają się o węzły i tory (rozjazdy powstają same); **Moje klocki** – zaznacz fragment i „Zapisz zaznaczenie…” (nazwa), potem wstawiasz go w dowolnym obrazie (bez przypięć; biblioteka `projekty_ut/klocki_zobrazowania.json`);
- **szkic na życzenie:** zakładka UT – zaznacz tory (albo nic – wszystkie nienarysowane) i „Zaproponuj szkic…”, przeciągnij prostokąt; program proponuje rysunek z UT (rzędy, łączniki 45°, semafory, kozły, numery, od razu przypięty) na szarej warstwie – przeciągnij, by przesunąć; **Przyjmij**, **Przyjmij i edytuj** (zaznaczony do poprawek) albo **Odrzuć** (Esc). To propozycja – obraz projektuje człowiek;
- **obraz poglądowy** (Ie-104 § 6): „+ obraz poglądowy” albo Kopiuj obraz szczegółowy i w zakładce Obraz rodzaj „poglądowy” (posterunki – cały obszar); zdejmij tarcze, wykolejnice, PK, opisy odcinków, perony i kozły – Sprawdź ostrzega o symbolach spoza § 6 ust. 4 i nie wymaga ich na obrazie; do symulatora obraz poglądowy nie trafia (na pulpicie zakładka poglądowa jak dotąd);
- **przebiegi (tablica przebiegów):** zaznacz semafor albo tarczę na obrazie – w zakładce Właściwości „Przebiegi od X”: wszystkie przebiegi możliwe w układzie, pogrupowane wg końca. Na górze panelu **„Własny przebieg od X”**: wybierz koniec, w razie potrzeby położenie każdej zwrotnicy (+ / − / dowolnie) i odcinek „przez” – program pokaże drogi w układzie spełniające warunki (także spoza propozycji, bez limitu wariantów); „Dodaj” – do tablicy. **✓** – przebieg w tablicy (do symulatora idą tylko zaznaczone), **★** – przebieg zasadniczy (pozostałe do tego samego końca – wariantowe), **V** – prędkość przebiegu pociągowego (pierwsza pozycja: z torów i zwrotnic; ⚠ gdy semafor nie ma pasa / W21). Klik na opisie przebiegu – jego droga na zielono na obrazie. „Wszystkie od X” – wszystkie do tablicy, „Testuj” – każdy przebieg z tablicy nastawiany osobno (czy dochodzi do sygnału zezwalającego). **Przebiegi… (F6)** – cała tablica w jednym oknie: propozycje szare, w tablicy czarne, czerwone – nie ma ich w układzie (np. po zmianie UT); Sprawdź, Testuj. Dopóki nic nie zaznaczysz, tablica nie jest zaprojektowana i symulator wyznacza przebiegi sam. Zmiany cofa Ctrl+Z, zapisują się w pliku zobrazowania;
- **PNG…** – bieżący obraz jak na pulpicie (1 j. = 1 piksel, bez siatki, prowadnic, stref i znaczników); **Drukuj…** – obraz na jednej kartce poziomo, z nazwą obrazu i UT;
- **plik starego edytora** (`*.zob.json` w wersji 1) otwiera się w nowym edytorze – przekształcony do wersji 2 (tory, rozjazdy, symbole, opisy, przypięcia jako potwierdzone); zapis proponuje nazwę `*.v2.zob.json`, plik starego edytora zostaje. Eksport do symulatora daje ten sam układ co stary edytor.

**Do symulatora… – co trafia do układu symulatora** (eksport wspólny z dawnym edytorem, `Lcs.Model/DoSymulatora.cs`): zapisuje UT i wszystkie obrazy jako układ symulatora w `app/LcsWidzew/Data/projekty/<nazwa>.json` (nazwa jak plik zobrazowania; poprzednia wersja w `.bak`). `Data/station.json` zostaje bez zmian. Przy błędach „Sprawdź” program pyta, czy zapisać. Po zapisie pokazuje uwagi (np. odcinki bez nazwy, semafor bez funkcji) i proponuje uruchomienie symulatora z tym układem; później wybierasz go w oknie wyboru układu po `uruchom.bat`. Z UT: tory podzielone na kawałki między rozjazdami, urządzeniami i licznikami osi (długości z km), zwrotnice z typem i prędkością na bok, sygnalizatory wg funkcji semafora (tarcze ostrzegawcze i powtarzacze z `repeats` z pola „Powtarza”), odcinki z nazw UT (kod posterunku przed nazwą, np. `MT:T1`), szlaki z blokadą i odstępami. Z obrazu: pulpit – węzły trafiają dokładnie w narysowane symbole, reszta jest rozłożona wzdłuż toru obrazu. Przebiegi wyznacza symulator z topologii (Tablica przebiegów w symulatorze). Z obrazu na pulpit trafiają też napisy, perony, przejazdy, numery torów, opisy odcinków (wg ustawienia obrazu), strzałki blokady w narysowanych miejscach i tory bez kontroli niezajętości. Rozjazdy krzyżowe i skrzyżowania: St – węzeł krzyża `KOD:krN` na obu torach; Rkpd i Rkp – zwrotnice `KOD:Nab` / `KOD:Ncd` (łącznik między ostrzami to wnętrze toru A, wnętrze toru B pominięte), cały rozjazd w jednym odcinku; Rkp – dodatkowo `noPass`: para torów bez przejazdu, komputer zależności nie wyznacza przez nią przebiegów. Pochylenie każdego kawałka toru (`poch`, ‰ od A do B) – średnie z niwelety UT (bez niwelety – brak pola). Opisy odcinków „na żądanie” pokazuje na pulpicie przycisk „Opisy”.

Siatka, prowadnice i strefy są tylko w edytorze – nie trafiają na pulpit. Test: `EdytorZobrazowania2.exe --test wynik.txt [zrzut.png]` (ostatnia linia `błędów: 0`); sam zrzut: `EdytorZobrazowania2.exe --shot zrzut.png plik.zob.json`.

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
├── ZALACZNIK_WSKAZNIKI_IE1.md ← załącznik: wskaźniki wg Ie-1 (opis, ustawienie, stan w symulatorze)
├── CLAUDE.md, AGENTS.md       ← instrukcje dla agentów AI
├── PLAN_EDYTOR_ZOBRAZOWANIA.md ← plan i decyzje – edytor zobrazowania
├── PLAN_EDYTOR_ZOBRAZOWANIA_V2.md ← plan nowego edytora zobrazowania (EdytorZobrazowania2)
├── uruchom.bat, uruchom_edytor_ut.bat, uruchom_edytor_zobrazowania2.bat, uruchom_przechylka.bat
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
├── app/Lcs.EdytorZobrazowania2/ ← edytor zobrazowania (EdytorZobrazowania2.exe, plik wersji 2, model Obr* w Lcs.Model), podgląd pulpitu, dane testowe w Testy/
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

Karty sygnalizatorów pokazują sygnał zlecony przez KZ i wyświetlany w terenie wg Ie-1 (S1…S13a, Sz, Ms1, Ms2) i zapalone światła. Sygnał wynika z prędkości przebiegu (zwrotnice na bok) i sygnału następnego semafora (Ie-1 zał. 1) – np. wjazdowy przy wyjazdowym na „Stój” pokazuje S13, po otwarciu wyjazdowego sam zmienia się na S12; na SBL czterostawnej S3 oznacza dwa wolne odstępy. Semafor odstępowy sprawdza cały swój odstęp: odcinki od pierwszego licznika osi za nim do następnego semafora tego kierunku (z odcinkiem, w którym ten stoi) – odstęp może mieć kilka odcinków, a licznik może stać kilkanaście metrów za semaforem (Ie-4 § 51 ust. 17). Na posterunku bez semaforów wyjazdowych (same wjazdowe, np. odgałęźny) przebieg pociągowy od semafora wjazdowego prowadzi przez posterunek prosto na następny szlak. Na pulpicie szlak z edytora zobrazowania ma przerwy tylko tam, gdzie postawiono je na obrazie (przerwa albo sygnalizator); kawałek szlaku między nimi świeci się jako całość – zajęcie dowolnego jego odcinka czerwieni cały kawałek. Wiersze żółte – stan nietypowy (rozkaz w toku, usterka), czerwone – brak kontroli, sygnał niezapalony, brak łączności. Filtry: rodzaj karty, posterunek (odstępowe – szlak), obiekt, „tylko stany nietypowe”.

Zakładka **„Komputer zależności”**: na górze **kontrola zależności na żywo** – zielone „brak naruszeń” albo czerwona lista naruszeń (np. sygnał zezwalający przy zwrotnicy bez kontroli, odcinek w dwóch przebiegach, sygnał niezgodny z Ie-1) i żółte uwagi (np. przebieg czeka na kontrolę zwrotnicy w ochronie bocznej); niżej przebiegi w KZ (stan, sygnał, prędkość, zwrotnice, odcinki, ochrona boczna), utwierdzenia, dziennik **komunikacji KZ ↔ karty** (rozkazy i zmiany statusów) i **polecenia z pulpitu** z odpowiedzią KZ. **Sprawdź przebieg** – wpisz fragment nazwy (np. `P102 1A`): zwrotnice, odcinki, ochrona boczna, prędkość, sygnał i dlaczego można albo nie można go nastawić.

Połączenie przez sieć: symulator (gra jednoosobowa albo serwer) zawsze nasłuchuje dla Komputerowni na **127.0.0.1:47121** (tylko ten komputer) – wystarczy „Połącz”. Do serwera stanowisk na innym komputerze (np. przez Radmin) – jego adres i port **47120**. `Komputerownia.exe --connect host:port` łączy od razu.

## Testy (bez okna)

Uruchamiane z `app/LcsWidzew/bin/Release/net10.0-windows/`:

| Polecenie | Co robi |
|---|---|
| `LcsWidzew.exe --selftest wynik.txt [scenariusz]` | tablica przebiegów i scenariusz z automatycznym dyżurnym |
| `LcsWidzew.exe --screenshot zrzut.png 2 "#page=1" "E S1 Pociąg"` | zrzut pulpitu po poleceniach (`#page=N`, `#overview`, `#opisy` – opisy odcinków na żądanie włączone, `@t`; `#tablica=filtr` – dodatkowo zrzut tablicy przebiegów z panelem zależności specjalnych do `zrzut.png.tablica.png`; `#tablica=filtr;v=60` – przedtem wpis prędkości pierwszego przebiegu pociągowego z filtra i zapis zależności, `v=0` usuwa wpis; `#stan` – do `zrzut.png.txt` sygnały zezwalające z kodami Ie-1 i pociągi z prędkością) |
| `LcsWidzew.exe --edrtest wynik.txt` | test EDR bez okna (dzienniki z szlaków, objęcie dyżuru, przyjęcie i wyprawienie, pola wymagane, wersje, zmiana doby, obszary sterowania, serializacja); oczekiwane na `Testy/station_test.json`: `EDR: kontrola 117 z 117` (z E8e: 22 wzory telefonogramów – plik, treść z pól, skutki 21 / 21a / 22 / 16 / 17 / 23 / 25, błędy pól; z E8d: godzina „812” / „0812”, przejście przez północ, droga wolna na szlaku jednotorowym, przy zamkniętym torze i ruchu jednotorowym, tor szlakowy z uwag „po torze nr N”, wykreślenie wpisu z przyczyną, zapis dzienników do pliku z kopią .bak, odczyt przy następnym uruchomieniu, uszkodzony plik bez zmian; z E8a: P T – peron rzymski i tor; z E7: konfiguracja posterunków i dzienników – tylko administrator, zachowanie wpisów, dziennik z torami ręcznie, nowy posterunek, błędy, odczyt z pliku, przywrócenie z układu; K / NK, potwierdzenie przez dyspozytora i człowieka, uwagi, czas bez wypadku; z E6: wyciąg z dziennika ruchu bez historii i z historią, z dziennika telefonicznego, z zajętości torów; z E5: dróżnicy – ustawienia, godzina / ukośnik, kierunki; jazda do km i z powrotem; wpis dowolny; sprawdzenia cykliczne – terminy, wykonanie; druga godzina w komórce; test pracuje na kopii układu w katalogu tymczasowym; z E4: telefonogramy nadane i odebrane, inne okoliczności, numeracja dobowa / miesięczna / roczna, zamknięcie i otwarcie toru, zapowiadanie telefoniczne, serializacja; z E2: parzystość wg kierunku szlaku, wykaz pociągów, przyjazd i odjazd z ruchu; z E3: stan torów z wpisów, ustawienia dziennika – sprawdzenie, zapis i odczyt pliku, pola wymagane z ustawień) |
| `--screenshot … "#edrprzyj=i:nr:tor" "#edrwypr=i:nr:tor" "#edrpole=i:Pole:wartość" "#edrklik=nr" "#edrszlaki" "#edrust=i" "#edrtel=i|treść|odebrał|lewy" "#edrtelokno=i|nr" "#edrtelefoniczny" "#edrdr=i|ozn:km:j|ozn:km:d" "#edrpowrot=i:nr:tor:km" "#edrwpis=i|treść" "#edrspr=POST|nazwa|min" "#edrdrokno=i" "#edrsprust=POST" "#edrwyciag=rodzaj|i|h" "#edrpost=KOD" "#edradmin" "#edrkonf=KOD" "#edrbezwyp" "#edropis=nr" "#edrwykaz=KOD" "#edrwykresl=i|przyczyna" "#edr=i"` | wpisy do dziennika i (numer w kolejności dzienników stanowiska; pole `Nr`/`Tor`/`Dw`/`Odj`/`Prz`/`Uwagi` ostatniego wiersza, wartość `teraz` albo `+N` minut) i zrzut okna EDR z zakładką dziennika i do `zrzut.png.edr.png` oraz okna przyjęcia do `zrzut.png.edr_przyjecie.png`; `#edrklik=nr` – dwuklik na pociągu w wykazie po upływie czasu zrzutu, `#edrszlaki` – okno „Szlaki posterunku” do `zrzut.png.edr_szlaki.png`, `#edrust=i` – okno ustawień zasadniczych dziennika i do `zrzut.png.edr_ustawienia.png`; `#edrtel=i|treść|odebrał|lewy` – telefonogram nadany z dziennika i (ostatnie pole – zamknięcie toru lewego; i = −1 – inne okoliczności), `#edrtelokno=i` – okno telefonogramu do `zrzut.png.edr_telefonogram.png`, `#edrtelefoniczny` – zakładka dziennika telefonicznego do `zrzut.png.edr_telefoniczny.png`; E5: `#edrdr=…` – dróżnicy w ustawieniach dziennika i (j – godzina, d – ukośnik), `#edrpowrot=…` – jazda do km i z powrotem, `#edrwpis=…` – wpis dowolny, `#edrspr=…` – wymagane sprawdzenie, `#edrdrokno=i` / `#edrsprust=POST` – zrzut okna dróżników / konfiguracji sprawdzeń (`.edr_droznicy.png`, `.edr_sprawdzenia.png`). Polecenia zmieniające ustawienia zapisują `<układ>.edr.json` – uruchamiaj je na kopii układu; E6: `#edrwyciag=ruchu|i|h` (h – z historią; też `telefoniczny`, `tory`) – zrzut okna wyciągu do `.edr_wyciag_n.png`, `#edrpost=KOD` – posterunek wybrany u góry okna EDR; E7: `#edradmin` – zalogowany administrator, `#edrkonf=KOD` – okno posterunków i dzienników (`.edr_posterunki.png`), `#edrbezwyp` – okno czasu bez wypadku, `#edropis=nr` – zakładki Opis / Trasa pociągu (`.edr_opis.png`, `.edr_trasa.png`), `#edrwykaz=KOD` – wykaz posterunku do `.txt` (kierunki, szlaki, bieg z rozkładu); E8d: `#edrwykresl=i|przyczyna` – wykreślenie ostatniego wiersza pociągu (albo wpisu) dziennika i |

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

**Testy (ostatnie przebiegi):** selftest `station_test.json` 25 z 26 / 4 z 4, dziennik zdarzeń bez zmian; `EdytorUT.exe --test` – sprawdzenia bramek, zaznaczania i łączenia bez błędów (551 sprawdzeń w przebiegu z bramkami); część przebiegów miała błędy tylko przy pobieraniu kafelków ortofotomapy z Geoportalu (sieć, zmienne między przebiegami); `EdytorZobrazowania2.exe --test` 0 błędów; `Lcs.Tabor.Testy` 20 z 20; `EdytorTaboru.exe --test` 42 sprawdzenia, 0 błędów. Zrzuty w `zrzuty/` (`EdytorUT_wysiegnik_wspolny.png`, `EdytorUT_bramka_*.png`, `EdytorUT_laczenie_torow.png`, `EdytorTaboru_*.png`, `Pulpit_fizyka_*.png`).

**Nie sprawdzono / do zrobienia**
- Obsługa prawdziwą myszą i klawiaturą (testy wołają metody okna i symulują modyfikatory).
- W21 nie jest rysowany na pulpicie ani na planie stacji (widać go w podglądzie KZ i `#stan`).
- Wspólna konstrukcja (bramka / wysięgnik) nie trafia do symulatora – eksport bez zmian (`mount`).
- Pliki `Data/projekty/*.json` nie zostały ponownie wyeksportowane: brak pasów, W21, `poch` i `sklad`. Wpisuje je człowiek.
- Zmiana numeru rozjazdu w UT nie poprawia odwołań w zobrazowaniu – po zmianie sprawdź obraz.
- Tabor: dane katalogu i kalibracja (`kalibracja_tabor.txt`) do oceny właściciela; etapy T5–T6 planu (składy domyślne kategorii, GTFS → seria, scenariusz instruktora) do ustalenia.
- Commity nie są wypchnięte (`git push` dopiero na prośbę właściciela).
