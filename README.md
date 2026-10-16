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

Stan (etapy 1–4):
- **Układ współrzędnych PUWG 1992** (EPSG:2180): X na północ, Y na wschód, Z to wysokość, wszystko w metrach.
- **Widok Schemat / Teren** (przyciski na pasku, klawisz S). Schemat (domyślny) wygląda jak UT w ISDR: oś stacji poziomo, w poprzek rozciągnięte ×4 (lista obok), białe tło, sama oś toru. Podkład jest ukryty, pole „Podkład” go pokazuje. „Oś wg toru” kładzie poziomo zaznaczony tor, a „↻ 180°” obraca schemat. Teren: północ u góry, 1:1, ortofotomapa – do odrysowania torów ze zdjęcia. Rysować i edytować można w obu widokach, zapis jest zawsze w metrach terenu.
- **Płótno:** kółko myszy przybliża wokół kursora (od 2 mm do 2 km na piksel), przeciąganie lewym albo środkowym przyciskiem przesuwa widok. Do tego siatka o kroku 1 / 2 / 5 · 10ᵏ m, linijki ze współrzędnymi, podziałka i strzałka północy.
- **Punkt:** w polu wpisz współrzędne z Google Maps (np. `51.7631, 19.5438`) albo `X Y` w PUWG 1992, potem Enter albo „Idź do”.
- **Pasek stanu:** X, Y i szerokość / długość geograficzna pod kursorem, rozdzielczość (1 px = … m), przybliżona skala na ekranie.
- **Panel obszaru:** nazwa, skala rysunku 1:500 / 1:1000 / 1:2000 (wielkość symboli Ig-10, nie zmienia geometrii), uwagi.
- **Plik:** Nowy, Otwórz, Zapisz, Zapisz jako (`*.ut.json`, domyślnie w katalogu `projekty_ut`). Poprzednia wersja zostaje jako `.bak`, zapamiętywany jest też ostatni widok. Cofnij / Ponów: Ctrl+Z, Ctrl+Y.

- **Podgląd na żywo** (panel „Podkłady”): ortofotomapa Geoportalu dla bieżącego widoku albo zapasowo zdjęcia Esri / mapa OSM. Nie zapisuje się w projekcie.
- **Ortofotomapa do projektu – „🗺 Mapka kafelków…”:** mapa z liniami kolejowymi i siatką kafelków. Wybierz rozdzielczość (5 cm widać podkłady, 10 cm szyny) i zaznacz kafelki lewym przyciskiem, przeciągając wzdłuż linii (zaczęcie na zaznaczonym odznacza). Prawy przycisk przesuwa mapę. Potem „Pobierz zaznaczone”, a pobrane zrobią się zielone. Na płótnie można też „Zaznacz obszar…” (prostokąt) albo „Cały widok”. Wszystkie pobrania jednej rozdzielczości składają się w jedną warstwę w stałej siatce, w `podklady/orto_Ncm/` obok projektu, bez dubli. Nieudane można „Dopobrać”. Geoportal czasem oddaje pusty biały obraz: program to wykrywa i pobiera kafelek jeszcze raz, a dla starszych pobrań jest przycisk „Napraw puste kafelki”.
- **Grafika z pliku** (skan planu, mapa): z plikiem world (`.pgw` / `.jgw` / `.tfw`) ląduje od razu na miejscu. Bez niego użyj „Kalibruj punktami”: klik na grafice, potem klik w miejscu, gdzie ten punkt naprawdę jest na ortofotomapie. Wystarczą 2 punkty, lepiej 3–4 daleko od siebie, panel pokaże odchyłki. Do tego „Przesuń”, obrót i rozmiar piksela z klawiatury.
- **Warstwy:** widoczność, krycie, kolejność, usuwanie.
- **Miarka (M):** długość łamanej **w terenie**, czyli z poprawką skali PUWG 1992 (na południku 19° układ skraca o 70 cm/km). Backspace cofa punkt, PPM czyści.

- **Tor (T):** budujesz tor odcinkami stycznymi, jak w ISDR. Tryb wybierasz przyciskiem „─ Prosta” / „◠ Łuk” albo klawiszem Tab.
  - Pierwszy klik to początek toru albo koniec istniejącego toru (wtedy go przedłużasz). Pierwsza prosta nowego toru idzie w stronę kursora.
  - Dopóki tor ma jedną prostą, każde kolejne kliknięcie przestawia jej koniec. Kliknij więc daleko, na osi toru na zdjęciu: im dalej, tym dokładniejszy kierunek.
  - **Prosta** biegnie na przedłużeniu końca. Długość wynika z kursora albo wpisujesz ją z klawiatury i zatwierdzasz Enterem. Alt daje wolny kierunek: powstaje załamanie z łukiem R w wierzchołku.
  - **Łuk** ma promień R i krzywe przejściowe L z pól na pasku. Stronę i kąt wyznacza kursor, a z klawiatury wpisujesz długość łuku. Z Shift łuk kończy się równolegle do osi schematu.
  - **Łączenie:** kliknięcie na końcu innego toru łączy oba tory stycznie: jednym łukiem albo łukami odwrotnymi (esowanie). Gdy R się nie mieści, program bierze największy możliwy. Tory scalają się w jeden, chyba że drugi należy do rozjazdu. Start na końcu toru A i kliknięcie na końcu toru B łączy dwa gotowe tory.
  - Backspace cofa ostatni odcinek, Enter / PPM / dwuklik kończy, Esc rezygnuje, Ctrl wyłącza przyciąganie.
- **Równoległy (P):** tor równoległy odrysowany z podkładu. Kliknij tor wzorcowy, potem na zdjęciu oś nowego toru w miejscu jego początku. Odstęp liczy się z kliknięcia co 1 cm, z Shift co 0,25 m. Na koniec kliknij miejsce końca albo naciśnij Enter, wtedy tor biegnie do końca toru wzorcowego. Łuki stają się współśrodkowe.
- **Uchwyt łuku:** zaznaczony tor ma kółko w środku każdego łuku. Przeciągnięcie zmienia promień, a R widać na żywo.
- **Edycja:** klik zaznacza tor i otwiera panel z numerem, kategorią, elektryfikacją, Vmax, długością w terenie, listą elementów oraz R i L każdego wierzchołka. Wierzchołki przeciąga się myszą. Dwuklik na boku dodaje wierzchołek, Del usuwa wierzchołek albo tor. W panelu jest też „Tor równoległy” (międzytorze, strona) i „Odwróć kierunek”. Błędy geometrii, np. łuk nie mieści się między wierzchołkami, widać jako czerwone kółko i opis w panelu.

- **Rozjazd (R):** typ wybierasz z listy na pasku (katalog w `Dane/rozjazdy.json`). Wciśnij na torze w miejscu początku rozjazdu (PR) i przeciągnij w stronę odgałęzienia. Program zapyta o numer. Jeśli puścisz na torze sąsiednim, powstanie łącznik: dwa rozjazdy i tor łączący. Tory mogą odbiegać od równoległości do 3°. Dwa osobno postawione rozjazdy też da się złączyć w łącznik: narzędziem Tor zacznij na końcu odgałęzienia jednego i kliknij odgałęzienie drugiego. Drugi rozjazd przesunie się tak, żeby skosy się zeszły. Jeśli puścisz obok toru (co najmniej 2 m od niego), powstanie od razu tor odgałęźny: skos rozjazdu, łuk zwrotny o promieniu R z pola na pasku i prosta równoległa w odstępie, w którym puściłeś (co 0,25 m). Puszczenie blisko toru daje samo odgałęzienie do końca rozjazdu. Odgałęzienie przedłużasz narzędziem Tor od jego końca. Wierzchołek za rozjazdem przesuwa się tylko wzdłuż skosu, więc kąt odgałęzienia zawsze zgadza się z typem rozjazdu. Rozjazd krzyżowy (Rkp, Rkpd) i skrzyżowanie (St) wstawiasz klikiem na przecięciu dwóch torów.
- **Rozjazd w panelu:** numer, typ, położenie zasadnicze, zwrotnica ręczna, km PR, zmiana strony i odwrócenie, uwagi (np. rozjazd na łuku, nachodzi na inny). We „Wskaż” rozjazd przeciąga się wzdłuż toru, a łącznik idzie za nim.
- **Znaki Ig-10:** rozjazd ma kreskę początku, numer i czarny klin w kącie między torami, po stronie kierunku zamkniętego, jak w Ig-10 zał. 8 i na planach schematycznych. Klin liczony jest z geometrii rozjazdu. Rozjazd krzyżowy ma dwa kliny w kątach ostrych przy punkcie matematycznym. Wzory znaków są w bazie `Dane/symbole_ig10.json`. Przycisk „Symbole Ig-10…” pokazuje wszystkie znaki. Po poprawkach w pliku użyj „Wczytaj ponownie”.

Wymiary a i b rozjazdów w katalogu są przybliżone, katalogowe wpisz w `Dane/rozjazdy.json`. Następne etapy: urządzenia (semafory, tarcze, kozły…) ze znakami z bazy Ig-10, wysokości z NMT.

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
| `EdytorUT.exe --test wynik.txt [zrzut.png]` | test automatyczny przez kontrolki okna i mysz płótna (PUWG 1992, panel, cofanie, widok, zapis; podkłady, kalibracja, miarka – potrzebny internet; geometria i budowa torów); ostatnia linia `błędów: 0`; okno poza ekranem; dodatkowo zrzuty `_mapka.png`, `_szyny.png`, `_symbole.png`, `_schemat_blisko.png` |
| `EdytorUT.exe --shot zrzut.png [plik.ut.json] ["punkt" [m/px]]` | sam zrzut okna |


## Stacja testowa

`Data/projekty/stacja_testowa.json` („Testowo”, wybór przy uruchomieniu): tor zasadniczy ze szlakami 1Z (SBL dwukierunkowa, 3 odstępy) i 1W (PBL dwukierunkowa), tory 2 i 3, bocznica 4 (kozioł, wykolejnica Wk1, tarcza Tm1), 5 rozjazdów, semafory A, B, D1–D3, F1–F3, perony, przejazd; plan schematyczny (lekko skośny, skala rzeczywista) i osobne zobrazowanie pulpitu.
