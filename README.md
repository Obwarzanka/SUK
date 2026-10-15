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

### Plan stacji (podgląd terenu)
Przycisk **„Plan stacji”** otwiera osobne okno z planem schematycznym całego węzła na żywo: pociągi (numer, rodzaj, prędkość, czoło na czerwono), semafory jako głowice ze światłami, zwrotnice w aktualnym położeniu (nieczynne odgałęzienie – szare). Na dole: **śledzenie składu** (widok jedzie za wybranym pociągiem), wybór **posterunku** i „Cały węzeł”. To podgląd dla instruktora i do nauki – dyżurny pracuje na pulpicie. Działa tam, gdzie pracuje komputer zależności (gra jednoosobowa, serwer).

## Edytor układu

Przycisk **„Edytor układu”** w oknie pulpitu. Edytor zapisuje `app/LcsWidzew/Data/station.json`, czyli ten sam plik, który wczytuje symulator.

- **Zapis (Ctrl+S)** od razu odświeża pulpit symulatora.
- Każdy zapis tworzy kopię w `Data/kopie/` (ostatnie 30). Przycisk **„Przywróć kopię…”** wczytuje starszą wersję.
- **Ctrl+Z / Ctrl+Y** cofa i ponawia zmiany.
- **Shift** przy narzędziu jednorazowym pozwala stawiać kolejne obiekty bez wychodzenia z narzędzia.
- **Ctrl+F** wyszukuje obiekt (sygnalizator, zwrotnicę, węzeł).

### Dwa tryby: zobrazowanie i plan schematyczny
Przycisk w lewym górnym rogu edytora przełącza tryb. Edytor otwiera się na zobrazowaniu.

- **Zobrazowanie** – obrazy pulpitu (EbiScreen / SimRail), czyli to, co widzi dyżurny. Elementy są powiązane z układem torowym, ale ich położenie jest niezależne: tory mogą się zbliżać i krzyżować, bo połączeń nie tworzy obraz.
  - przeciągnięcie węzła przesuwa go razem z końcami torów;
  - Shift+klik albo prostokąt zaznacza grupę, przeciąganie i strzałki ją przesuwają;
  - dwuklik na torze dodaje punkt załamania, PPM na punkcie go usuwa;
  - Del usuwa z obrazu (układ zostaje bez zmian);
  - lista „Nieumieszczone na obrazie” pokazuje tory układu, których dyżurny jeszcze nie widzi, z przyciskiem „Umieść”;
  - **Napis (N), Peron (P), Przejazd (X)** – elementy pulpitu. Zobrazowanie i plan schematyczny mają osobne kopie: przesunięcie, dodanie albo usunięcie napisu na obrazie nie zmienia planu i odwrotnie. Przy pierwszym otwarciu zobrazowanie dostaje kopię obecnych.
- **Plan schematyczny (układ torowy)** – cały węzeł na jednym arkuszu, znakami umownymi wg **Ig-10** (zał. 8): osie torów (główne grubsze, zelektryfikowane niebieskie), rozjazdy z klinem, semafory i tarcze z komorami, kozły, wykolejnice, liczniki osi / złącza izolowane, perony, przejazdy. Kategorię toru i sieć trakcyjną ustawiasz w panelu toru (cały tor: Shift+dwuklik, potem przyciski w panelu grupy), liczbę komór – w panelu sygnalizatora (`lamps`). Okno „Plan stacji” używa tych samych znaków.
  - **Skala planu (Ig-10 § 3)** (panel po prawej, gdy nic nie jest zaznaczone): 1:2000 wzdłuż / 1:500 w poprzek albo 1:4000 / 1:1000, a do rysowania po zdjęciach **skala rzeczywista** 1:2000 (albo 1:1000) w obu kierunkach – zdjęcie bez zniekształceń. Zmiana skali przeskalowuje rysunek, długości zostają. **„Długości torów z rysunku”** – długość toru wynika z rysunku (ile jednostek planu, tyle metrów wg skali; skos liczony z obu skal), bez wpisywania. Szlaki (rysowane w skróceniu), łączniki obrazów i tory z długością wpisaną ręcznie (wpisanie długości odcinka albo odznaczenie „Długość z rysunku” w panelu toru) – bez zmian. Bez tej opcji długości są danymi i przesuwanie węzłów ich nie zmienia. Siatka „Ig-10” = 10 m × 4,75 m; linijki, podpowiedź, rysowany tor i miarka pokazują metry.
  - **Punkty załamania toru**: dwuklik na torze – nowy punkt (dokładnie na torze), przeciągnij go na zdjęcie; PPM na punkcie – usuń. Cały tor zaznacza Alt+dwuklik.
  - **Przyciąganie do siatki**: przełącznik „przyciąganie” obok siatki, Alt – chwilowo odwrotnie (przy podkładzie domyślnie wyłączone).
  - **Przesuwanie widoku** w każdym narzędziu (np. w trakcie rysowania toru): środkowy przycisk myszy albo Spacja + LPM; kółko – powiększenie.
  - **Kafelki ortofotomapy z mapki** (ten sam panel): okno z mapą OSM i liniami kolejowymi (OpenRailwayMap) – LPM zaznacza kafelki (przeciągnięcie „maluje” je wzdłuż linii), PPM przesuwa mapę, kółko przybliża. „Pobierz zaznaczone i wstaw na plan” – mozaika trafia na plan od razu w skali; kolejne kafelki dokładają się w swoje miejsce (raz wyrównana do poziomu zostaje wyrównana). Źródło: ortofotomapa Geoportalu albo zdjęcia satelitarne Esri.
  - **Kilka podkładów naraz**: lista w panelu – zaznaczony jest aktywny (jego dotyczą obrót, skala, przesunięcie, usuwanie), przy każdym widoczność i kolejność ↑↓, krycie osobno. Np. plan z Semaforka nad ortofotomapą.
  - **Nowy podkład** (ten sam panel): ortofotomapa (zdjęcia lotnicze z Geoportalu), mapa OpenStreetMap, zdjęcia satelitarne Esri – podajesz środek („szer., dł.”, np. z Google Maps), wymiary w metrach i rozdzielczość; podkład pobiera się i trafia na plan od razu w skali (bez rozciągania), zostaje go obrócić („1. Wyrównaj tor do poziomu”). Plan z Semaforka: adres obrazu (PPM na obrazie → „Kopiuj adres obrazu”) albo plik z dysku – potem skala wzdłuż / w poprzek. Pliki podkładów: katalog `podklady` obok pliku układu.
  - **Podkład** (ten sam panel): „Wczytaj podkład…” – plan Semaforka, skan albo mapa. Ustawienie w skali planu: **1. Wyrównaj tor do poziomu** (2 punkty na osi toru), **2. Skala wzdłuż** (2 punkty + odległość w m, np. z kilometrażu), **3. Skala w poprzek** (2 sąsiednie tory + rozstaw, zwykle 4,75 m), **Mapa: rozciągnij pionowo** (mapa ma jedną skalę, plan – dwie), **Przesuń podkład**, „Dopasuj dwoma punktami” (do węzłów). Pola: położenie, szerokość, rozciągnięcie pionowe, obrót; przezroczystość. PPM/Esc przerywa. Podkład widać tylko w edytorze. Potem rysuj tory narzędziem Tor po podkładzie.
  - **Zbiorcze zaznaczanie**: przycisk **„⬚ Zaznacz obszarem”** (Q) – przeciąganie po pustym miejscu rysuje prostokąt zaznaczenia (bez Shift; Shift dodaje), w planie i w zobrazowaniu. Ctrl+A – wszystko na obrazie. Grupę przeciągasz, przesuwasz strzałkami, usuwasz Del.
  - **Warstwy planu** (Ig-10): włączanie i wyłączanie sygnalizatorów, numerów, liczników osi, peronów, przejazdów, kozłów i wykolejnic, napisów, łuków. W „Plan stacji” – przycisk „Warstwy ▾”.
  - **„Rozjazdy jako skosy”** zamienia pionowe łączniki na skosy jak w terenie (tylko rysunek planu).
  - **„Autoukład w skali”** – propozycja całego planu w skali Ig-10: długości torów z danych (1:2000), tory każdej stacji w równych pasach (1:500), łączniki jako skosy, szlaki skrócone. Kolejność torów zostaje z obecnego planu. Pulpit się nie zmienia, Ctrl+Z cofa całość. Potem popraw ręcznie, np. na podkładzie. W pasku stanu raport: największa odchyłka długości wskazuje niespójne długości w danych. Tu budujesz topologię: tory, zwrotnice, sygnalizatory, odcinki, szlaki. Z tego liczy komputer zależności. Plan można ułożyć jak w terenie, niezależnie od obrazów pulpitu.

- **✂ Podziel tor / ⊕ Scal tory** (pasek, w obu trybach): klik na torze dzieli go w tym miejscu (nowy węzeł na planie i na obrazie), klik na węźle między dwoma torami łączy je w jeden. Shift – kolejne bez wychodzenia, PPM – koniec.

Podział, scalenie i prostowanie toru na planie zmieniają też obraz. Nowe tory trzeba umieścić na obrazie samemu. Przy pierwszym otwarciu obecny rysunek staje się jednorazowo zobrazowaniem, a jego kopia – planem.

### Narzędzia

| Narzędzie | Klawisz | Działanie |
|---|---|---|
| Wskaż / przesuń | – | zaznaczanie, przeciąganie węzłów, napisów i peronów; prostokąt – zaznaczenie grupy (wyrównanie, rozsunięcie torów, prostowanie rozjazdów) |
| ◠ Łuk | O | łuk po okręgu: klik – początek, klik – punkt na łuku, klik – koniec; Ctrl+klik zamiast punktu na łuku – łuk styczny do toru; kolejne łuki ciągną się od końca. Promień [m] zapisuje się w torze, długość liczy się po łuku. Łuk o zadanym promieniu w narożniku – panel węzła z dwoma torami |
| ⊤ Bramka | U | bramka sygnałowa na planie (znak 9.8.8): przeciągnij w pionie w poprzek torów – semafory w pobliżu przypinają się i dosuwają do bramki; konstrukcję semafora (maszt, maszt z wysięgnikiem, wysięgnik nad torem, bramka, karzełkowy) wybierasz w jego panelu. Tylko plan – zależności i pulpit bez zmian |
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

## Wersje układu

**Pusty szablon:** `Data/projekty/pusty_szablon.json` – pusty obszar w skali rzeczywistej 1:2000 z długościami z rysunku. Wybierz go przy starcie, w edytorze wczytaj podkład (zdjęcia / mapa / Semaforek), narysuj stację i zapisz pod własną nazwą („Zapisz jako…” do `Data/projekty`).

Zwykłe uruchomienie (np. `uruchom.bat`) pokazuje **listę wersji układu**: ★ podstawowy `Data/station.json` i pliki z `Data/projekty` (np. własne stacje zapisane przez „Zapisz jako…”). Ostatni wybór jest zapamiętany. Generator układu z OpenStreetMap został usunięty (2026-10-16) – stacje rysuje się w edytorze na podkładzie.

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
7. Czas i tempo, panel instruktora i edytor układu działają na serwerze. Po rozłączeniu stanowiska jego posterunki wracają do serwera.

Planowane dalsze kroki: teren za sterownikami obiektowymi, stanowisko instruktora przez sieć.

## Struktura projektu

```
symulator/
├── README.md, PLAN.md, uruchom.bat
├── Zrodla/                    ← Ie-20, Ie-104, materiały PKP PLK
├── tools/                     ← import rozkładu (gtfs_import.py, pkpic_import.py)
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
| `LcsWidzew.exe --editshot zrzut.png fuzz 300 7` | losowe operacje w edytorze – plan schematyczny (szuka błędów) |
| `LcsWidzew.exe --editshot zrzut.png dfuzz 300 7` | losowe operacje w trybie zobrazowania (obraz nie może zmienić układu) |
| `LcsWidzew.exe --editshot zrzut.png deco wynik.json` | napisy, perony, przejazdy – rozdzielenie kopii pulpitu i planu |
| `LcsWidzew.exe --editshot zrzut.png validate` | wynik „Sprawdź” do pliku `zrzut.png.txt` |
| `LcsWidzew.exe --screenshot zrzut.png 2 "#page=1" "E S1 Pociąg"` | zrzut pulpitu po poleceniach (`#page=N`, `#overview`, `@t`) |

Opcja `--station plik.json` wczytuje inny układ, np. kopię z `Data/kopie/`.
