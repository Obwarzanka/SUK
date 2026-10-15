# Symulator LCS Łódź Widzew

Szkoleniowy symulator komputerowego pulpitu nastawczego **LCS Łódź Widzew** obejmującego Łódź Widzew (ŁW), Łódź Fabryczną (ŁF) i Łódź Niciarnianą (ŁN). Program działa na Windows, w C#/WPF na .NET 8.

- Obsługa wg **Ie-20**: polecenia, przebiegi, blokady liniowe, postępowanie przy usterkach.
- Zobrazowanie wg **Ie-104**, wzorowane na pulpitach LCS (EbiScreen / SimRail).
- Wbudowany **edytor układu torowego**, a w nim stacje, szlaki, blokady, przebiegi i prędkości.
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

## Edytor układu

Przycisk **„Edytor układu”** w oknie pulpitu. Edytor zapisuje `app/LcsWidzew/Data/station.json`, czyli ten sam plik, który wczytuje symulator.

- **Zapis (Ctrl+S)** od razu odświeża pulpit symulatora.
- Każdy zapis tworzy kopię w `Data/kopie/` (ostatnie 30). Przycisk **„Przywróć kopię…”** wczytuje starszą wersję.
- **Ctrl+Z / Ctrl+Y** cofa i ponawia zmiany.
- **Shift** przy narzędziu jednorazowym pozwala stawiać kolejne obiekty bez wychodzenia z narzędzia.
- **Ctrl+F** wyszukuje obiekt (sygnalizator, zwrotnicę, węzeł).

### Narzędzia

| Narzędzie | Klawisz | Działanie |
|---|---|---|
| Wskaż / przesuń | – | zaznaczanie, przeciąganie węzłów, napisów i peronów; prostokąt – zaznaczenie grupy (wyrównanie, rozsunięcie torów, prostowanie rozjazdów) |
| Tor | T | rysowanie toru punkt po punkcie (Shift – poziomo), PPM/Esc kończy |
| Odcinek | – | przypisanie torów do odcinków izolowanych (Ctrl+klik pobiera odcinek) |
| **Rozjazd** | R | **rysuje nowe połączenie**: klik na torze, potem na drugim torze tworzy łącznik ukos–pion–ukos z dwiema zwrotnicami; klik w puste miejsce tworzy odgałęzienie |
| **Zwrotnica** | Z | **nadaje zwrotnicę istniejącemu rozgałęzieniu** (węzeł z 3 torami) |
| Semafor / tarcza | S | wjazdowy, wyjazdowy (z Ms2 lub bez), tarcza manewrowa, tarcza ostrzegawcza (to), powtarzacz (sp), **PK** |
| Kozioł, Wykolejnica | K, W | koniec toru / wykolejnica |
| Łącznik obrazów | – | logiczne połączenie końców torów na dwóch obrazach |
| Szlak | – | szlak (blokada liniowa) od semafora wjazdowego |
| Peron, Przejazd, Napis | P, X, N | perony, przejazdy (Y/T, kilometr w nazwie), napisy |
| Miarka | M | odległość na obrazie i długość po torach [m] |

### PK – punkt końcowy przebiegu
Mały trójkąt w osi toru, w którym kończy się przebieg, gdy nie ma tam sygnalizatora. Stosuje się go np. przy bliskich posterunkach bez blokady.

- Narzędzie Semafor/tarcza → typ **„pk – punkt końcowy przebiegu”**.
- Kierunek: kierunek przebiegów, które w nim się kończą.
- Rodzaj przebiegów: **pociągowe, manewrowe albo oba**. Rodzaj można zmienić później w panelu właściwości.
- Gdy kończy się w nim nastawiony przebieg, PK świeci na czerwono.
- Pociąg zatrzymuje się przed PK, jeśli dalej nie ma nastawionego przebiegu pociągowego.

### Szlaki i blokady liniowe
**Szlak zewnętrzny** (do posterunku spoza LCS):
1. Narzędzie Szlak → klik na semaforze wjazdowym.
2. Edytor idzie torem na zewnątrz stacji.
3. Wybierz rodzaj blokady, kierunek zasadniczy i liczbę odstępów.

**Szlak wewnętrzny** (między stacjami LCS, także na różnych obrazach):
1. Narzędzie Szlak → klik na semaforze wjazdowym pierwszej stacji → rodzaj **„wewnętrzny – złącze SZLAK WEWNĘTRZNY”**. Na końcu toru pojawia się żółty kwadrat złącza (widoczny tylko w edytorze).
2. To samo zrób na drugiej stacji. Edytor zapyta, z którym złączem połączyć i jaka jest długość szlaku.
3. Złącze można też zaznaczyć później, wybrać złącze drugiej stacji z listy i kliknąć **„Połącz”**.

Jeśli semafor ma już szlak (np. błędnie utworzony), edytor zapyta, czy usunąć stary szlak i utworzyć nowy.

**Rodzaje blokad:**
- **SBL dwukierunkowa** – samoczynna, Wbl/Pzk, jedno- lub wieloodstępowa;
- **SBL jednokierunkowa** – kierunek stały, bez bloku pozwolenia;
- **PBL dwukierunkowa / jednokierunkowa** – półsamoczynna, blok początkowy i końcowy;
- **brak blokady** – telefoniczne zapowiadanie pociągów.

**Panel szlaku** (zaznacz szlak; wszystkie dane blokady liniowej):
- oznaczenie szlaku (zmiana nazwy przenosi odstępy, sygnalizatory i przebiegi);
- rodzaj blokady i kierunek zasadniczy;
- linia, tor szlakowy, oznaczenie szlaku na obrazie (it…), posterunki sąsiednie;
- dla każdego końca: **napis nad strzałką** i **semafor wjazdowy**;
- prędkość na szlaku;
- liczba i długości odstępów, nazwy (numery) sygnalizatorów odstępowych.

> Rozkład jazdy odwołuje się do szlaków zewnętrznych po nazwie (1A, 2A, 1O, 2O, 1C, 2C, 1M). Zmiana nazwy takiego szlaku odetnie od niego pociągi z rozkładu.

### Prędkości i przebiegi
- **Tor:** `vmax` w km/h.
- **Zwrotnica:** prędkość na bok (na ostrze i z ostrza, domyślnie 40) i na wprost.
- **Sygnalizator** – panel „Przebiegi od …”:
  - ✓ – przebieg czynny;
  - ★ – przebieg zasadniczy;
  - pole liczbowe – prędkość przebiegu w granicach stacji.

### Sprawdź (walidacja)
Przycisk **„Sprawdź”** wykrywa błędy budowy układu, m.in.:
- rozgałęzienia bez zwrotnicy (wtedy przebiegi idą „na skróty”);
- odcinki złożone z niepołączonych kawałków (świeci tor, którym przebieg nie idzie);
- odcinki przechodzące przez sygnalizator (przycisk „Odcinki na sygnalizatorach”);
- szlaki wewnętrzne bez semafora na końcu i niepołączone złącza.

### Symbole jak w SimRail
| Na obrazie | W edytorze |
|---|---|
| WY / WJ przy końcu szlaku zewnętrznego | powtarzacz **sp** z nazwą WY/WJ, powtarza semafor sąsiedniej stacji |
| różowa pionowa linia z Y/T i kilometrem | **Przejazd** |
| ToP, ToC… | tarcza ostrzegawcza **to** |
| „+” przy zwrotnicy | położenie zasadnicze |
| tor przerywany | tor rozebrany (właściwość toru) |

---

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

Planowane dalsze kroki: polecenia jako komunikaty (rozbiór tekstu po stronie stanowiska), teren za sterownikami obiektowymi, osobne stanowiska (np. ŁW i ŁF).

## Struktura projektu

```
symulator/
├── README.md, PLAN.md, uruchom.bat
├── Zrodla/                    ← Ie-20, Ie-104, materiały PKP PLK
├── tools/                     ← import rozkładu (gtfs_import.py, pkpic_import.py), dawny generator układu
├── zrzuty/                    ← zrzuty ekranu
└── app/LcsWidzew/             ← aplikacja C# / WPF (.NET 8)
    ├── Data/station.json      ← układ obszaru LCS (edytowany w edytorze)
    ├── Data/kopie/            ← automatyczne kopie zapasowe układu
    ├── Data/rozklad*.json     ← rozkład jazdy
    ├── Model/                 ← model stacji (Station.cs) i dokument edytora (StationDoc.cs)
    ├── Sim/                   ← zależności, polecenia, blokady, pociągi, rozkład, testy
    ├── UI/                    ← pulpit (PanelView.cs), edytor (EditorWindow.cs), okna
    └── MainWindow.xaml(.cs)   ← okno pulpitu
```

## Testy (bez okna)

Uruchamiane z `app/LcsWidzew/bin/Release/net8.0-windows/`:

| Polecenie | Co robi |
|---|---|
| `LcsWidzew.exe --selftest wynik.txt [scenariusz]` | tablica przebiegów i scenariusz z automatycznym dyżurnym |
| `LcsWidzew.exe --edtest wynik.txt` | operacje edytora: stacje, szlaki, odstępy, PK, złącza |
| `LcsWidzew.exe --editshot zrzut.png fuzz 300 7` | losowe operacje w edytorze (szuka błędów) |
| `LcsWidzew.exe --editshot zrzut.png validate` | wynik „Sprawdź” do pliku `zrzut.png.txt` |
| `LcsWidzew.exe --screenshot zrzut.png 2 "#page=1" "E S1 Pociąg"` | zrzut pulpitu po poleceniach (`#page=N`, `#overview`, `@t`) |

Opcja `--station plik.json` wczytuje inny układ, np. kopię z `Data/kopie/`.
