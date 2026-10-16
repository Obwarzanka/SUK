# Symulator LCS Łódź Widzew

Szkoleniowy symulator komputerowego pulpitu nastawczego **LCS Łódź Widzew** obejmującego Łódź Widzew (ŁW), Łódź Fabryczną (ŁF) i Łódź Niciarnianą (ŁN). Program działa na Windows, w C#/WPF na .NET 8.

- Obsługa wg **Ie-20**: polecenia, przebiegi, blokady liniowe, postępowanie przy usterkach.
- Zobrazowanie wg **Ie-104**, wzorowane na pulpitach LCS (EbiScreen / SimRail).
- Osobny program **edytor układu torowego** (`EdytorUT.exe`) do rysowania układu w rzeczywistej geometrii (PUWG 1992). Jest w budowie, plan: `PLAN_EDYTOR_UT.md`.
- **Rzeczywisty rozkład jazdy** z GTFS (IC, Polregio, ŁKA) z obiegiem jednostek ŁKA.

> Symulator jest narzędziem szkoleniowym. Nie zastępuje regulaminu technicznego ani stanowiskowej instrukcji obsługi.

---

## Uruchomienie

Wymagany **.NET 8 SDK**.

- Dwuklik `uruchom.bat`, albo z wiersza poleceń:

```bash
dotnet run --project app/LcsWidzew/LcsWidzew.csproj -c Release
```

- Po zmianach w kodzie zbuduj program w katalogu `app/LcsWidzew`, przy **zamkniętym** symulatorze (otwarty blokuje plik `.exe`):

```bash
dotnet build -c Release
```

Po zalogowaniu (dyżurny ruchu) wybierz scenariusz. Zakładki u góry przełączają obrazy:
- **0 – Obraz poglądowy**: cały obszar LCS, klik przenosi do obrazu stacji;
- **1 – ŁF + ŁN**;
- **2 – ŁW**.

---

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

---

### Plan stacji (podgląd terenu)
Przycisk **„Plan stacji”** otwiera osobne okno z planem schematycznym całego węzła na żywo: pociągi (numer, rodzaj, prędkość, czoło na czerwono), semafory jako głowice ze światłami, zwrotnice w aktualnym położeniu (nieczynne odgałęzienie – szare). Na dole: **śledzenie składu** (widok jedzie za wybranym pociągiem), wybór **posterunku** i „Cały węzeł”. To podgląd dla instruktora i do nauki – dyżurny pracuje na pulpicie. Działa tam, gdzie pracuje komputer zależności (gra jednoosobowa, serwer).

## Edytor układu torowego (UT)

Osobny program: `uruchom_edytor_ut.bat` albo `app/Lcs.EdytorUT/bin/Release/net8.0-windows/EdytorUT.exe [plik.ut.json]`. Plan budowy i decyzje są w `PLAN_EDYTOR_UT.md`.

Dotychczasowe edytory (układ i zobrazowanie wbudowane w symulator) zostały usunięte 2026-10-16. Są w historii git (commit `d2c7431`). Symulator nadal wczytuje istniejące pliki `station.json`.

Stan (etap 1):
- **Układ współrzędnych PUWG 1992** (EPSG:2180): X na północ, Y na wschód, Z to wysokość, wszystko w metrach. Północ jest u góry.
- **Płótno:** kółko myszy przybliża wokół kursora (od 2 mm do 2 km na piksel), przeciąganie lewym albo środkowym przyciskiem przesuwa widok. Do tego siatka o kroku 1 / 2 / 5 · 10ᵏ m, linijki ze współrzędnymi, podziałka i strzałka północy.
- **Punkt:** w polu wpisz współrzędne z Google Maps (np. `51.7631, 19.5438`) albo `X Y` w PUWG 1992, potem Enter albo „Idź do”.
- **Pasek stanu:** X, Y i szerokość / długość geograficzna pod kursorem, rozdzielczość (1 px = … m), przybliżona skala na ekranie.
- **Panel obszaru:** nazwa, skala rysunku 1:500 / 1:1000 / 1:2000 (wielkość symboli Ig-10, nie zmienia geometrii), uwagi.
- **Plik:** Nowy, Otwórz, Zapisz, Zapisz jako (`*.ut.json`, domyślnie w katalogu `projekty_ut`). Poprzednia wersja zostaje jako `.bak`, zapamiętywany jest też ostatni widok. Cofnij / Ponów: Ctrl+Z, Ctrl+Y.

Następne etapy: podkłady (ortofotomapa, grafiki), tory z łukami i krzywymi przejściowymi, rozjazdy z katalogu, urządzenia, wysokości z NMT.

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

---

## Architektura: zobrazowanie i komputer zależności

Podział jak w rzeczywistym LCS:

- **Komputer zależności** (`Sim/`: Simulator, Interlocking, Blocks, Commands) jako jedyny podejmuje decyzje: przebiegi, utwierdzenia, sygnały, blokady.
- **Zobrazowanie** (`UI/PanelView.cs`) rysuje wyłącznie z **meldunków**: telegramu stanu (`Sim/Meldunki.cs`, klasa `Indication`) odbieranego przez łącze `KzLink`. Do tego zna tylko układ torowy. Nie widzi przebiegów od środka, liczników czasu ani pociągów.
- **Utrata łączności** to usterka instruktora „Utrata łączności stanowiska z komputerem zależności”. Wszystkie obiekty przechodzą w stan „brak danych” (biały), polecenia nie są przyjmowane, a w ALARMACH pojawia się wpis.

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
├── README.md, ROZWOJ.md, PLAN_EDYTOR_UT.md, uruchom.bat, uruchom_edytor_ut.bat
├── projekty_ut/               ← projekty układu torowego (*.ut.json)
├── Zrodla/                    ← Ie-20, Ie-104, materiały PKP PLK
├── tools/                     ← import rozkładu (gtfs_import.py, pkpic_import.py)
├── zrzuty/                    ← zrzuty ekranu
├── app/Lcs.slnx               ← rozwiązanie: symulator, model UT, edytor UT
├── app/Lcs.Model/             ← model układu torowego (UtProject, UtDocument, PUWG 1992) – bez okien
├── app/Lcs.EdytorUT/          ← edytor układu torowego (EdytorUT.exe)
└── app/LcsWidzew/             ← symulator C# / WPF (.NET 8)
    ├── Data/station.json      ← układ obszaru LCS
    ├── Data/kopie/            ← automatyczne kopie zapasowe układu
    ├── Data/rozklad*.json     ← rozkład jazdy
    ├── Model/                 ← model stacji wczytywany przez symulator (Station.cs)
    ├── Sim/                   ← zależności, polecenia, blokady, pociągi, rozkład, testy
    ├── UI/                    ← pulpit (PanelView.cs), plan stacji, okna
    └── MainWindow.xaml(.cs)   ← okno pulpitu
```

## Testy (bez okna)

Uruchamiane z `app/LcsWidzew/bin/Release/net8.0-windows/`:

| Polecenie | Co robi |
|---|---|
| `LcsWidzew.exe --selftest wynik.txt [scenariusz]` | tablica przebiegów i scenariusz z automatycznym dyżurnym |
| `LcsWidzew.exe --screenshot zrzut.png 2 "#page=1" "E S1 Pociąg"` | zrzut pulpitu po poleceniach (`#page=N`, `#overview`, `@t`) |

Opcja `--station plik.json` wczytuje inny układ, np. kopię z `Data/kopie/`.

Edytor UT (z `app/Lcs.EdytorUT/bin/Release/net8.0-windows/`):

| Polecenie | Co robi |
|---|---|
| `EdytorUT.exe --test wynik.txt [zrzut.png]` | test automatyczny przez kontrolki okna (PUWG 1992, panel, cofanie, widok, zapis i odczyt); ostatnia linia `błędów: 0` |
| `EdytorUT.exe --shot zrzut.png [plik.ut.json] ["punkt" [m/px]]` | sam zrzut okna |


## Stacja testowa

`Data/projekty/stacja_testowa.json` („Testowo”, wybór przy uruchomieniu): tor zasadniczy ze szlakami 1Z (SBL dwukierunkowa, 3 odstępy) i 1W (PBL dwukierunkowa), tory 2 i 3, bocznica 4 (kozioł, wykolejnica Wk1, tarcza Tm1), 5 rozjazdów, semafory A, B, D1–D3, F1–F3, perony, przejazd; plan schematyczny (lekko skośny, skala rzeczywista) i osobne zobrazowanie pulpitu.
