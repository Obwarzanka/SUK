# SUK – Symulator urządzeń komputerowych

Szkoleniowy symulator komputerowych urządzeń srk w Lokalnych Centrach Sterowania (LCS): pulpitu nastawczego i komputera zależności. Symulator jest ogólny – stacje, posterunki i obrazy pochodzą z wczytanego układu, a nie z kodu. Pierwszym obszarem jest **LCS Łódź Widzew**: Łódź Widzew (ŁW), Łódź Fabryczna (ŁF) i Łódź Niciarniana (ŁN). Program działa na Windows, w C#/WPF na .NET 8.

- Obsługa wg **Ie-20**: polecenia, przebiegi, blokady liniowe, postępowanie przy usterkach.
- Zobrazowanie wg **Ie-104**, wzorowane na pulpitach LCS (EbiScreen / SimRail).
- Osobny program **edytor układu torowego** (`EdytorUT.exe`) do rysowania układu w rzeczywistej geometrii (PUWG 1992). Etapy 0–7 są gotowe, dane z niego nie trafiają jeszcze do symulatora. Plan: `PLAN_EDYTOR_UT.md`.
- Osobny **kalkulator przechyłki** (`Przechylka.exe`).
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

## Kalkulator przechyłki

Osobny program: `uruchom_przechylka.bat` albo `app/Lcs.Przechylka/bin/Release/net8.0-windows/Przechylka.exe`. Wpisujesz prędkość V, promień R i przechyłkę h. Puste h oznacza, że program sam zaproponuje przechyłkę.

Prześwit toru jest w polu i wynosi domyślnie 1435 mm. Przechyłka działa na rozstawie osi główek szyn s = prześwit + szerokość główki (1435 + 65 = 1500 mm), z którego bierze się znany współczynnik 11,8. Bez h program proponuje przechyłkę równoważącą (co 5 mm, najwyżej h_max) i podaje najmniejszą dopuszczalną.

Program podaje przechyłkę równoważącą h_r = s·V²/(9,81·3,6²·R) (≈ 11,8·V²/R), niedobór h_n, przyspieszenie niezrównoważone a_n i długość krzywej przejściowej L. L to największa z trzech wartości: z rampy przechyłkowej (1:n, n = k·V), z przyrostu przyspieszenia ψ i ze zmiany przechyłki f. Program pokazuje, który warunek decyduje, oraz największą prędkość dla danego R i h. Wartości dopuszczalne (h_max, niedobór, ψ, f, k) są w polach do zmiany. Domyślne są przykładowe, więc wpisz obowiązujące z przepisów. Test: `Przechylka.exe --test wynik.txt [zrzut.png]`.

## Edytor układu torowego (UT)

Osobny program: `uruchom_edytor_ut.bat` albo `app/Lcs.EdytorUT/bin/Release/net8.0-windows/EdytorUT.exe [plik.ut.json]`. Plan budowy i decyzje są w `PLAN_EDYTOR_UT.md`.

Dotychczasowe edytory (układ i zobrazowanie wbudowane w symulator) zostały usunięte 2026-10-16. Są w historii git (commit `d2c7431`). Symulator nadal wczytuje istniejące pliki `station.json`.

Stan (etapy 1–7, bez edytora zobrazowania):
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
- **Usunięcie kawałka toru:** PPM na torze we Wskaż otwiera menu: „Usuń ten kawałek” usuwa prostą albo cały łuk pod kursorem, a tor rozcina się na dwa (albo skraca, gdy to koniec). „Rozetnij tor tutaj” (tylko na prostej) daje dwa tory stykające się w tym miejscu. Wszystko, co leży za cięciem (rozjazdy, urządzenia, odcinki, perony, przejazdy, punkty km, profil), przechodzi na nowy tor z kilometrażem od jego początku. Co leżało w usuniętym kawałku, znika. Program nie tnie w obrębie rozjazdu ani toru łączącego łącznika.
- **Zaznaczanie kilku elementów (Wskaż):** Ctrl + klik dodaje element do zaznaczenia albo go zdejmuje: tor, rozjazd, skrzyżowanie, urządzenie, odcinek, peron, przejazd, punkt km. Shift + przeciągnięcie od pustego miejsca zaznacza prostokątem wszystko, co leży w nim w całości. Panel pokazuje listę zaznaczonych. Del albo „Usuń zaznaczone” usuwa wszystkie naraz, a jedno Ctrl+Z je przywraca. Esc albo zwykły klik kończy zaznaczenie.
- **Uchwyt łuku:** zaznaczony tor ma kółko w środku każdego łuku. Przeciągnięcie zmienia promień, a R widać na żywo. Przeciągnięcie do wierzchołka usuwa łuk (R 0). Załamanie bez łuku ma kreskowane kółko po wewnętrznej stronie kąta: odciągnięcie go tworzy łuk.
- **R 0 na pasku:** w trybie Łuk łuk przechodzi przez punkt pod kursorem, a promień wynika z miejsca kliknięcia. Z Alt powstaje załamanie bez łuku.
- **Edycja:** klik zaznacza tor i otwiera panel z numerem, kategorią, elektryfikacją, Vmax, długością w terenie, listą elementów oraz R i L każdego wierzchołka. Wierzchołki przeciąga się myszą. Dwuklik na boku dodaje wierzchołek, Del usuwa wierzchołek albo tor. W panelu jest też „Tor równoległy” (międzytorze, strona) i „Odwróć kierunek”. Błędy geometrii, np. łuk nie mieści się między wierzchołkami, widać jako czerwone kółko i opis w panelu.

- **Rozjazd (R):** typ wybierasz z listy na pasku (katalog w `Dane/rozjazdy.json`). Wciśnij na torze w miejscu początku rozjazdu (PR) i przeciągnij w stronę odgałęzienia. Program zapyta o numer. Jeśli puścisz na torze sąsiednim, powstanie łącznik: dwa rozjazdy i tor łączący. Tory mogą odbiegać od równoległości do 3°. Dwa osobno postawione rozjazdy też da się złączyć w łącznik: narzędziem Tor zacznij na końcu odgałęzienia jednego i kliknij odgałęzienie drugiego. Drugi rozjazd przesunie się tak, żeby skosy się zeszły. Jeśli puścisz obok toru (co najmniej 2 m od niego), powstanie od razu tor odgałęźny: skos rozjazdu, łuk zwrotny o promieniu R z pola na pasku i prosta równoległa w odstępie, w którym puściłeś (co 0,25 m). Puszczenie blisko toru daje samo odgałęzienie do końca rozjazdu. Odgałęzienie przedłużasz narzędziem Tor od jego końca. Wierzchołek za rozjazdem przesuwa się tylko wzdłuż skosu, więc kąt odgałęzienia zawsze zgadza się z typem rozjazdu. Rozjazd krzyżowy (Rkp, Rkpd) i skrzyżowanie (St) wstawiasz tak samo: wciśnij na torze w miejscu środka i przeciągnij w stronę toru przecinającego. Powstanie on pod kątem typu, np. 1:9, a dalej przedłużasz go narzędziem Tor od końców. Można też kliknąć na istniejącym przecięciu dwóch torów.
- **Przesuwanie:** rozjazd przeciągasz myszą od razu, bez wcześniejszego zaznaczania. Łącznik jedzie w całości, nawet gdy chwycisz go za drugi rozjazd. Strzałki przesuwają zaznaczony rozjazd wzdłuż toru o 0,1 m (z Shift o 1 m). Zaznaczony tor albo wierzchołek strzałki przesuwają o 5 cm (z Shift o 0,5 m), w Schemacie góra / dół zmienia międzytorze. Ctrl + przeciągnięcie zaznaczonego toru przesuwa cały tor razem z rozjazdami. PPM na rozjeździe otwiera menu: strona, odwrócenie, położenie zasadnicze, zwrotnica ręczna, usunięcie.
- **Rozjazd w panelu:** numer, typ, położenie zasadnicze, zwrotnica ręczna, km PR, zmiana strony i odwrócenie, uwagi (np. nachodzi na inny), wstawki międzyrozjazdowe (w łączniku: długość między końcami rozjazdów, najmniejsza dopuszczalna v/10, nie mniej niż 6 m, i ψ), przyciski „Łącznik z rozjazdem N” (te same pozycje są w menu PPM). Taki łącznik powstaje też wtedy, gdy odgałęzienie było już dociągnięte do drugiego rozjazdu narzędziem Tor.
- **Rozjazd łukowy:** rozjazd postawiony na łuku toru sam się wygina. Odgałęzienie do środka łuku daje rozjazd jednostronny (Rłj), na zewnątrz dwustronny (Rłd). Panel podaje promienie obu torów i prędkość na zwrot. Po zmianie łuku toru rozjazd od razu się dopasowuje. Rozjazd nie może leżeć na krzywej przejściowej ani na granicy prostej i łuku – uwaga podaje, o ile go przesunąć w każdą stronę. Szybciej: przycisk **„Popraw łuk”** w panelu rozjazdu (i w menu PPM) przesuwa granicę łuku poza rozjazd. Łuk zaczyna się wtedy za rozjazdem albo rozjazd leży w całości na łuku. Wierzchołek i proste zostają, zmienia się tylko R, a przycisk pokazuje R przed i po. Na działanie symulatora ta uwaga nie wpływa: to zgodność z rzeczywistym układem.
- **Łącznik na łuku:** na torach w łuku skosy obu rozjazdów łącznika rozchodzą się (tory obracają się na jego długości). Program wstawia wtedy we wstawkę łuk wyrównujący (największy, jaki się mieści, w środku wstawki) i ustawia drugi rozjazd symetrycznie. Panel rozjazdu podaje jego promień i kąt. Uwaga pojawia się dopiero, gdy łuk wyrównujący ma mniej niż 140 m albo skosy rozchodzą się o więcej niż 3°.
- **Dopasuj promienie** (panel toru, gdy łuk się nie mieści): zmniejsza R tylko w tych łukach, nie poniżej 140 m. We „Wskaż” rozjazd przeciąga się wzdłuż toru, a łącznik idzie za nim.
- **Urządzenie (U):** rodzaj wybierasz z listy na pasku: semafor świetlny, tarcza ostrzegawcza, tarcza manewrowa, wykolejnica, kozioł oporowy, licznik osi, złącze izolowane, ukres. Wciśnij na torze w miejscu urządzenia i przeciągnij w stronę jazdy, której dotyczy, po stronie, po której stoi. Kozioł przyciąga się do końca toru. Licznik, złącze i ukres stawiasz kliknięciem. Program podpowiada nazwę (A, B…, Tm1, Wk1, lo1). We Wskaż urządzenie przeciągasz wzdłuż toru albo przesuwasz strzałkami. PPM daje menu (strona, kierunek, usuń), a w panelu są nazwa i km.
- **Konfiguracja semafora i tarczy** (panel, znaki wg BSK ISDR „Oznaczenia urządzeń srk”): układ gotowy (2-, 3-, 4- i 5-komorowy, tarcza ostrzegawcza, tarcza manewrowa karzełkowa albo wysoka) albo komory po kolei od masztu. Każda komora ma kolor (czerwona, zielona, pomarańczowa, biała, niebieska) i znacznik „pulsuje”, kolejność zmieniasz ↑ ↓, jest też ✕ i „+ komora”. Komory można też wpisać tekstem, np. `B*PCZ*`. Ustawiasz ponadto, która komora świeci w stanie zasadniczym, oraz mocowanie: na maszcie (słup), na wysięgniku, na bramce albo karzełkowy. Semafor może też mieć pas pomarańczowy, pas zielony, W24 i W21 z cyfrą prędkości (np. 6 = 60 km/h). Wszystko to jest w polu „Konfiguracja semafora”.
- **Semafory na bramce** (mocowanie „na bramce”): rysunek jak na planach schematycznych (Semaforek, Łódź Widzew – L3 / L5, S1 / S3, D / E). Semafory na bramce w tym samym miejscu (do 8 m wzdłuż toru, ten sam kierunek) tworzą jedną bramkę: belkę w poprzek wszystkich ich torów, na obu końcach słup w stronę komór zakończony kreską stopy. Semafor na bramce nie ma własnej stopy, tylko wisi na belce. Przy belce jest komora górna i kreska W24, a na drugim końcu W21, pasy świetlne i nazwa.
- **Światło komory** (panel semafora, lista przy każdej komorze): stałe (bez kresek), stałe lub pulsujące (2 kreski na obwodzie), tylko pulsujące (4 kreski). W zapisie tekstem „*” po literze oznacza stałe lub pulsujące, a „**” tylko pulsujące, np. „B**PCZ*”.
- **Licznik osi, złącze izolowane, ukres** (urządzenia bez kierunku): przy wstawianiu stają po tej stronie toru, po której klikniesz. Później PPM „Zmień stronę”.
- **Znaki Ig-10:** rozjazd ma kreskę początku, numer i czarny klin w kącie między torami, po stronie kierunku zamkniętego, jak w Ig-10 zał. 8 i na planach schematycznych. Klin liczony jest z geometrii rozjazdu. Rozjazd krzyżowy ma dwa kliny w kątach ostrych przy punkcie matematycznym. Wzory znaków są w bazie `Dane/symbole_ig10.json`. Przycisk „Symbole Ig-10…” pokazuje wszystkie znaki. Po poprawkach w pliku użyj „Wczytaj ponownie”.
- **W4 i W5:** na liście urządzeń (wskaźnik zatrzymania się czoła pociągu W4, wskaźnik granicy przetaczania W5, znaki wg Ig-10 s. 33 i 30). Stawiasz je jak semafor: wciśnij na torze i przeciągnij w stronę jazdy, po stronie wskaźnika.
- **Odcinek (O):** odcinki kontroli niezajętości. Najpierw stawiasz granice: liczniki osi albo złącza izolowane (narzędzie Urządzenie). Program sam wyznacza kawałki torów między granicami, także przez rozjazdy i łączniki. Kliknięcie narzędziem Odcinek na torze nadaje nazwę całemu kawałkowi. Podpowiedź to numer toru albo „z” i numery rozjazdów (np. z1/2), wpisać można dowolną. Kolor na planie oznacza odcinek, czerwone kreski – kawałek bez odcinka. Kliknięcie w kawałek, który ma już nazwę, zaznacza ten odcinek. Panel: nazwa, rodzaj (liczniki osi / obwód torowy), długość, kawałki torów z km, granice, czy to odcinek zwrotnicowy. Nazwę odcinka (pod torem) przeciągasz we Wskaż.
- **Peron:** wciśnij na torze w miejscu początku krawędzi peronowej i przeciągnij wzdłuż toru do jej końca, po stronie peronu. Panel: numer, km od–do, długość krawędzi (z km linii PP / KP), odległość krawędzi od osi toru, wysokość nad główką szyny, szerokość, „Zmień stronę”, „Dwukrawędziowy” (druga krawędź przy sąsiednim torze). Krawędź jest podwójną linią jak w Ig-10.
- **Przejazd:** kliknięcie na torze daje drogę prostopadłą, przeciągnięcie wzdłuż osi drogi daje kąt i długość drogi na planie. Panel: opis, kategoria A–F albo przejście (P), km, kąt, szerokość, rogatki (mechaniczne / elektryczne). Na planie: krawędzie drogi kreskowane, napis z kategorią i km linii.
- **Kilometraż (km):** wciśnij na torze w miejscu, gdzie znasz km linii, i przeciągnij w stronę rosnącego km. W oknie wpisz numer linii i km, np. `17 25.452`. Drugi punkt na tym samym torze ustala kilometr skrócony albo wydłużony. Pozostałe tory biorą km z rzutu na tor z kilometrażem. Na planie są wskaźniki hektometrowe i kilometrowe. Km pod kursorem pokazuje pasek stanu, a panele peronu, przejazdu i toru podają km linii.
- **Profil… (profil podłużny):** dla zaznaczonego toru. „Teren z NMT GUGiK” pobiera wysokości terenu co krok (domyślnie 10 m). „Niweleta z terenu” układa odcinki stałego pochylenia (tolerancja, najkrótszy odcinek – domyślnie 0,3 m i 100 m). Załom przeciągasz na wykresie w pionie albo wpisujesz w tabeli km, Z i promień łuku pionowego Rv. Wykres pokazuje pochylenia ‰, długości oraz nasyp (pomarańczowy) i przekop (niebieski). NMT to teren, nie główka szyny: na wiaduktach i w przekopach popraw niweletę ręcznie. Pochylenia widać też na planie (Ig-10 s. 58), a Z niwelety na pasku stanu.
- **Sprawdź (F7):** kontrola całego układu. Lista w panelu „Kontrola układu”: błędy na czerwono, uwagi na pomarańczowo. Kliknięcie pokazuje miejsce i zaznacza obiekt. Sprawdzane są: geometria torów, otwarte końce (tor bez połączenia i bez kozła – na granicy obszaru zaznacz w panelu toru „początek / koniec – granica obszaru”), tory bez numeru, rozjazdy bez numeru i ich uwagi, urządzenia poza torem i powtórzone nazwy, nakładające się tory, odcinki (kawałek bez odcinka, odcinki bez granicy między sobą, granica w obrębie rozjazdu), perony, przejazdy, kilometraż, profil.
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
| Ctrl + klik (Wskaż) | dodaj element do zaznaczenia kilku / zdejmij |
| Shift + przeciągnięcie od pustego miejsca (Wskaż) | zaznaczenie prostokątem |
| PPM na torze (Wskaż) | menu: usuń kawałek (prostą / łuk), wygładź łuk, podziel łuk (tutaj / na części), rozetnij tor, usuń tor |
| Ctrl+N / Ctrl+O / Ctrl+S / Ctrl+Shift+S | nowy / otwórz / zapisz / zapisz jako |
| Ctrl+Z / Ctrl+Y (Ctrl+Shift+Z) | cofnij / ponów |

Peron, Przejazd i km nie mają klawisza – wybiera się je przyciskiem na pasku.


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
├── README.md                  ← obsługa programów (ten plik)
├── ROZWOJ.md                  ← stan, ustalenia z właścicielem, etapy (dla programistów i agentów AI)
├── PLAN_EDYTOR_UT.md          ← plan, decyzje i pytania – edytor UT
├── HISTORIA_EDYTOR_UT.md      ← szczegółowa historia prac nad edytorem UT
├── PODZIAL_MODULOW.md         ← podział symulatora na moduły
├── CLAUDE.md, AGENTS.md       ← instrukcje dla agentów AI
├── uruchom.bat, uruchom_edytor_ut.bat, uruchom_przechylka.bat
├── archiwum/                  ← stare materiały (PLAN.md v0.5, prototypy)
├── projekty_ut/               ← projekty układu torowego (*.ut.json)
├── Zrodla/                    ← Ie-20, Ie-104, materiały PKP PLK
├── tools/                     ← import rozkładu (gtfs_import.py, pkpic_import.py), pdfr (strony PDF do PNG)
├── zrzuty/                    ← zrzuty ekranu
├── app/Lcs.slnx               ← rozwiązanie: symulator, model UT, edytor UT
├── app/Lcs.Model/             ← model układu torowego (tory, rozjazdy, urządzenia, odcinki, kontrola, PUWG 1992) – bez okien
├── app/Lcs.EdytorUT/          ← edytor układu torowego (EdytorUT.exe), katalog rozjazdów i baza symboli w Dane/
├── app/Lcs.Przechylka/        ← kalkulator przechyłki (Przechylka.exe)
└── app/LcsWidzew/             ← symulator C# / WPF (.NET 8)
    ├── Data/station.json      ← układ obszaru LCS
    ├── Data/kopie/            ← automatyczne kopie zapasowe układu
    ├── Data/rozklad*.json     ← rozkład jazdy
    ├── Model/                 ← model stacji wczytywany przez symulator (Station.cs)
    ├── Sim/                   ← zależności, polecenia, blokady, pociągi, rozkład, testy
    ├── UI/                    ← pulpit (PanelView.cs), plan stacji, okna
    └── MainWindow.xaml(.cs)   ← okno pulpitu
```

## Komputerownia (podgląd kart sterowników)

Osobny program: `uruchom_komputerownia.bat` albo `app/Lcs.Komputerownia/bin/Release/net8.0-windows/Komputerownia.exe`. Pokazuje karty sterowników obiektowych działającego symulatora – dla każdej karty (odcinek: licznik osi / obwód torowy, zwrotnica, wykolejnica):

- **rozkaz od KZ** – ostatni rozkaz komputera zależności i kiedy przyszedł (np. „przestaw do „-””, „zerowanie licznika osi”);
- **status do KZ** – to, co karta melduje (np. „kontrola „+””, „przekładanie → „-”, 3,2 s”, „BRAK KONTROLI”);
- **teren** – stan rzeczywisty przy torze (iglice, osie pojazdu, wykolejnica na szynie);
- **usterki** – po wybraniu karty w panelu po prawej można je wprowadzać i usuwać (jak instruktor).

Karty sygnalizatorów pokazują sygnał zlecony przez KZ i wyświetlany w terenie wg Ie-1 (S1…S13a, Sz, Ms1, Ms2) i zapalone światła. Sygnał wynika z prędkości przebiegu (zwrotnice na bok) i sygnału następnego semafora (Ie-1 zał. 1) – np. wjazdowy przy wyjazdowym na „Stój” pokazuje S13, po otwarciu wyjazdowego sam zmienia się na S12; na SBL czterostawnej S3 oznacza dwa wolne odstępy. Wiersze żółte – stan nietypowy (rozkaz w toku, usterka), czerwone – brak kontroli, sygnał niezapalony, brak łączności. Filtry: rodzaj karty, posterunek (odstępowe – szlak), obiekt, „tylko stany nietypowe”.

Połączenie przez sieć: symulator (gra jednoosobowa albo serwer) zawsze nasłuchuje dla Komputerowni na **127.0.0.1:47121** (tylko ten komputer) – wystarczy „Połącz”. Do serwera stanowisk na innym komputerze (np. przez Radmin) – jego adres i port **47120**. `Komputerownia.exe --connect host:port` łączy od razu.

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

Kalkulator przechyłki (z `app/Lcs.Przechylka/bin/Release/net8.0-windows/`): `Przechylka.exe --test wynik.txt [zrzut.png]`.

Komputerownia – test z działającym symulatorem: najpierw `LcsWidzew.exe --station Testy/station_test.json --screenshot s.png 40 "#serwis=47131" "#fault=swNoCtrl:LW:57" "@35" "#hold=30"` (w tle), potem `Komputerownia.exe --test wynik.txt [zrzut.png] 127.0.0.1:47131` – ostatnia linia `błędów: 0`.


## Stacja testowa

`Data/projekty/stacja_testowa.json` („Testowo”, wybór przy uruchomieniu): tor zasadniczy ze szlakami 1Z (SBL dwukierunkowa, 3 odstępy) i 1W (PBL dwukierunkowa), tory 2 i 3, bocznica 4 (kozioł, wykolejnica Wk1, tarcza Tm1), 5 rozjazdów, semafory A, B, D1–D3, F1–F3, perony, przejazd; plan schematyczny (lekko skośny, skala rzeczywista) i osobne zobrazowanie pulpitu.
