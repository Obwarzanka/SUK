# Nowy edytor układu torowego (UT) – plan

Projekt z 2026-10-16, po decyzji właściciela: edytory UT i zobrazowania budujemy od nowa, jako osobne aplikacje. Zaczynamy od UT. Starych edytorów nie przerabiamy, tylko je usuwamy (zostają w historii git).

Kolejność: **edytor UT → edytor zobrazowania → przekazywanie danych z UT do modułów symulatora.**

**Stan (2026-10-17):** etapy 0–7 zrobione (skrót w `ROZWOJ.md`, etap 1, szczegóły w `HISTORIA_EDYTOR_UT.md`): podkłady, tory, rozjazdy (też łukowe i krzyżowe), urządzenia, odcinki kontroli niezajętości, W4 / W5, perony, przejazdy, kilometraż, profil z NMT, „Sprawdź”, warstwy, wydruk w skali (§ 5f). Następny: odpowiedzi właściciela na pytania 11–15, potem etap 8 – edytor zobrazowania.

---

## 1. Założenia

1. **UT to rzeczywista geometria w układzie XYZ, w metrach.** Tory, łuki, rozjazdy i urządzenia leżą tam, gdzie w terenie. Długość toru wynika z geometrii, nie trzeba jej wpisywać.
   - X, Y: współrzędne płaskie PUWG 1992 (EPSG:2180). W tym układzie jest ortofotomapa Geoportalu, więc podkład pasuje bez dopasowywania. Konwencja geodezyjna: **X – na północ, Y – na wschód** (na ekranie północ u góry). W kodzie `XY.X` to zawsze północ.
   - Z: wysokość główki szyny (niweleta), z ręki albo z NMT.
2. **Projektuje człowiek.** Edytor daje narzędzia (rysowanie, przyciąganie, wpisywanie wymiarów, kontrolę), ale niczego nie generuje sam. Nie budujemy gotowych stacji.
3. **Skala i symbole.** Na ekranie zoom jest dowolny. Symbole (semafory, kozły, numery zwrotnic) mają wymiary wg Ig-10 przy wybranej **skali rysunku** (1:500 / 1:1000 / 1:2000) i nie zależą od geometrii. Zmiana skali rysunku zmienia wielkość symboli, a geometrii nie rusza.
4. **Długości w terenie, nie w układzie.** PUWG 1992 zniekształca odległości: na południku 19° skala 0,9993, czyli −70 cm/km, na krańcach Polski do +90 cm/km. Długość toru i każdy pomiar liczy się z poprawką skali odwzorowania (`Geo.GroundDistance`, zgodność z odległością geodezyjną < 2 mm na 3 km). Później dochodzi poprawka na wysokość: 200 m n.p.m. to +3 cm/km.
5. **Urządzenia są przypięte do toru:** tor + odległość od początku (albo km) + strona + kierunek działania. XYZ symbolu wynika z toru, więc po przesunięciu toru urządzenie jedzie razem z nim.
6. **Osobne programy, wspólna biblioteka modelu:**
   - `Lcs.Model` – model UT, zapis i odczyt pliku, kontrola poprawności (bez okien);
   - `Lcs.EdytorUT` – aplikacja WPF do projektowania UT;
   - później `Lcs.EdytorZobrazowania` – obrazy pulpitu wg Ie-104, korzysta z UT tylko do odczytu;
   - symulator (`LcsWidzew`) – na razie bez zmian, czyta dotychczasowe `station.json`.
7. **Nowy plik UT** (np. `*.ut.json`), niezależny od `station.json`. Jak dane z UT trafiają do symulatora, ustalimy w etapie 9.

## 2. Model danych UT (szkic)

- **Punkty węzłowe:** id, X, Y, Z. Są to końce torów, początki rozjazdów (PR), końce rozjazdów (KR), koniec toru / kozioł.
- **Tor (oś):** id, numer toru, łańcuch elementów geometrii od węzła do węzła:
  - prosta (długość, azymut);
  - łuk kołowy (promień, kąt / długość, strona);
  - krzywa przejściowa (parabola 3. stopnia);
  - atrybuty: kategoria (gz / gd / boczny), elektryfikacja, vmax, przechyłka, profil (pochylenia ‰ albo wysokości w punktach).
- **Rozjazd:** obiekt z katalogu (typ, skos 1:n, promień, wymiary a / b, lewy / prawy, PR, KR, środek rozjazdu), wstawiany na tor w punkcie i z azymutem toru. Numer i położenie zasadnicze wpisuje projektant. Osobno rozjazdy krzyżowe (Rkp, Rkj) i skrzyżowania torów.
- **Urządzenia przytorowe:** semafory, tarcze, wskaźniki, kozły, wykolejnice, punkty liczenia osi / złącza izolowane, przejazdy, perony, słupki km, bramki. Każde: tor, położenie na torze, strona, kierunek, parametry własne.
- **Odcinki kontroli niezajętości:** wyznaczane przez punkty liczenia osi / złącza izolowane (dane projektowe, wpisuje człowiek).
- **Kilometraż:** linia kolejowa + km w punktach odniesienia; km dowolnego miejsca toru liczony po osi.
- **Podkłady:** lista (plik graficzny z georeferencją albo kafelki ortofotomapy), krycie, kolejność, widoczność.

## 3. Etapy

| Etap | Zakres | Gotowe, gdy |
|---|---|---|
| 0 ✔ | Usunięcie starych edytorów UT i zobrazowania z `LcsWidzew` (okna, narzędzia, testy edytora). Pulpit, komputer zależności i selftest zostają. | build OK, selftest 25 z 26 / 4 z 4 na `station_test.json` |
| 1 ✔ | Szkielet: rozwiązanie z `Lcs.Model` i `Lcs.EdytorUT`. Płótno w metrach: przesuwanie, zoom, siatka, linijki, współrzędne kursora (X, Y, Z, km), zapis / odczyt, cofnij / ponów. | pusty projekt zapisuje się i wczytuje; zrzut okna |
| 2 ✔ | Podkłady (z mapką kafelków i stałą siatką): obraz z kalibracją (2 i więcej punktów, plik `.pgw` / `.jgw`), ortofotomapa Geoportalu wprost w EPSG:2180 (kafelki z mapki), OSM / Esri jako zapasowe. Kilka podkładów, krycie, kolejność. | ortofotomapa stacji leży w dobrym miejscu i skali; zmierzona odległość = rzeczywista |
| 3 ✔ | Tory (wielobok styczny z łukami R i przejściowymi L w wierzchołkach): prosta, łuk, krzywa przejściowa. Przyciąganie (koniec, styczność, punkt na torze, podkład), wymiary z klawiatury (długość, promień, azymut), tor równoległy w zadanym międzytorzu, edycja uchwytami. | test „jak użytkownik”: tor po ortofotomapie z łukiem, długości zgodne z geometrią |
| 4 ✔ | Rozjazdy z katalogu wstawiane na tor (odgałęzienie z prawdziwą geometrią), łącznik między torami, rozjazdy krzyżowe, skrzyżowania. Numery wpisuje projektant. | mijanka z łącznikami zbudowana klikami na skośnych torach |
| 5 ✔ | Urządzenia na torze, symbole Ig-10 w skali rysunku, obrót / strona / kierunek, odcinki kontroli niezajętości, perony, przejazdy, kilometraż. | mijanka z semaforami, kozłami i odcinkami; „Sprawdź” bez uwag |
| 6 ✔ | Wysokości (Z): profil podłużny, pochylenia, NMT z Geoportalu, nasyp / przekop. | profil toru zgodny z NMT w punktach kontrolnych |
| 7 ✔ | Kontrola układu (otwarte końce, rozjazd bez numeru, urządzenie poza torem, nakładające się tory), warstwy, wydruk / eksport PNG / PDF w skali. | wydruk 1:1000 – zmierzone na kartce = skala |
| 8 | Edytor zobrazowania jako osobna aplikacja (osobny plan, po zakończeniu UT). | – |
| 9 | Przekazywanie danych z UT do symulatora: z UT powstaje topologia (tory, zwrotnice, odcinki, sygnalizatory, długości, pochylenia, prędkości) dla komputera zależności, kart urządzeń i ruchu pociągów; przebiegi i zależności projektuje człowiek (tablica zależności). | do ustalenia z właścicielem |

Po każdym etapie: build, test automatyczny etapu (kliknięcia jak użytkownik, wg `lcs-testuj-jak-uzytkownik`), zrzut, szczegółowy wpis w `HISTORIA_EDYTOR_UT.md` i skrót w `ROZWOJ.md`.

## 4. Co przenosimy ze starego kodu, a czego nie

- **Przenosimy** tylko sprawdzone kawałki niezwiązane z edytorem: pobieranie ortofotomapy z Geoportalu z ponawianiem (404 load balancera), przeliczenie WGS84 → PUWG 1992, okno mapki z kafelkami, wysokości z NMT GUGiK i uproszczenie profilu (`Profil.cs`), znaki Ig-10 (`UI/Ig10.cs`) jako punkt wyjścia do rysowania symboli.
- **Nie przenosimy:** `EditorWindow*`, `StationDoc*`, schematu z pasami, autoukładu, skal Ig-10 1:2000/1:500 w poprzek.

## 5. Decyzje właściciela (2026-10-16)

1. Układ współrzędnych: **PUWG 1992** (EPSG:2180), konwencja geodezyjna **X – północ, Y – wschód** (potwierdzone przez właściciela).
2. Stare edytory: **usunąć od razu** (etap 0).
3. **Osobne programy** (`EdytorUT.exe`, później `EdytorZobrazowania.exe`, symulator) ze wspólną biblioteką.
4. **Skala: oba widoki.** Dane w metrach (dokładność do 1 mm), niezależnie od pikseli podkładu (ortofotomapa ok. 5–25 cm/px). Widok główny w równej skali 1:1000 / 1:2000: zdjęcie bez zniekształceń, symbole Ig-10 w mm arkusza. Przełącznik „Ig-10 1:2000 / 1:500”: tory i podkład rozciągnięte w poprzek osi stacji ×4. Rysować można w obu widokach, a zapis zawsze idzie w metrach terenu.
5. **Krzywe przejściowe: parabola 3. stopnia od razu** (etap 3), razem z prostą i łukiem kołowym.
6. **Katalog rozjazdów: `Zrodla/KZN_KOLEJ_02.pdf`** (KZN Bieżanów). Z niego lista typów: Rz, Rkp, Rkpd, Rpj, Rpd, Rłs, Rłj / Rłd, skrzyżowania St, profile 60E1 / 49E1 / 54E4 / Ph37 / G51, promień, skos, prześwit. Katalog **nie podaje wymiarów a / b** ani długości, więc liczę je z promienia i skosu (wartości przybliżone). Tabela typów będzie w osobnym pliku danych, żeby można było wpisać wymiary katalogowe.
   - Drugie źródło: **`Zrodla/rozjazdy_1.pdf`** (D. Kosicki, drogi-zelazne). Zawiera podział rozjazdów: Rz; podwójne Rpj / Rpd; łukowe Rłj / Rłd; krzyżowe Rkp / Rkpd. Do tego budowę oznaczenia, np. `Rz UIC60-500-1:12 sspd`: rodzaj, typ szyn (S49 = 49E1, S60 = UIC60 = 60E1), promień toru zwrotnego, skos, iglice (c / s / ss), prawy / lewy (p / l), podrozjazdnice (d / b). Edytor przyjmuje i zapisuje oznaczenie w tej postaci.
   - Prędkość na zwrot: v = 3,6·√(0,65·R) [km/h]. Wychodzi R190 ≈ 40, R300 ≈ 50, R500 ≈ 65, R760 ≈ 80, R1200 ≈ 100. Według tego źródła dopuszczalne: 190 / 300 → 40, 500 → 60, 760 → 80, 1200 → 100. Sygnały pozwalają tylko na 40 / 60 / 100, więc dla R760 semafor poda 60. W edytorze pokazuję wartość ze wzoru, a w polu `vbok` – dopuszczalną z tabeli (do potwierdzenia przez właściciela).
   - Skos 1:9 to skos zasadniczy. Skos większy niż 1:9 wolno stosować tylko w torach bocznych. Rpj / Rpd / Rk – tylko wyjątkowo, w torach bocznych. Rł – tylko gdy rozjazd musi leżeć w łuku. „Sprawdź” będzie o tym przypominać (uwaga, nie błąd).
7. **Wysokości (Z): z NMT GUGiK, jak w poprzedniej wersji.** Stary kod (`GeoPodklad.Nmt` w `UI/EditorWindow.Podklad.cs`, usługa `GetHByPointList`; `Model/Profil.cs` – próbkowanie toru, uproszczenie do odcinków o stałym pochyleniu ‰) przenoszę do `Lcs.Model`. W PUWG 1992 nie trzeba już przeliczać współrzędnych. Uwaga ze starej wersji: NMT to teren, nie główka szyny, a na wiaduktach pokazuje dolinę, więc wysokość trzeba móc poprawić ręcznie.

## 5b. Poprawka po etapie 4 – widok schematu (decyzja właściciela, 2026-10-16)

Właściciel ocenił widok jako nierealny i zbyt szczegółowy. Wzór: UT w stylu ISDR (białe tło, cienkie czarne linie, układ obrócony do poziomu, rozciągnięty w poprzek, przy zwrotnicach numery, małe znaki). Model w metrach PUWG zostaje, a zmienia się tylko widok i rozjazdy:

1. **Dwa widoki** (przełącznik na pasku):
   - **Schemat** (domyślny) – oś stacji poziomo, rozciągnięcie w poprzek ×4 (jak plan schematyczny 1:2000 / 1:500), białe tło, bez siatki i linijek, podkład ukryty (pole „Podkład” – pokazuje go także w schemacie, rozciągniętego).
   - **Teren** – północ u góry, 1:1, ortofotomapa na żywo – do odrysowania z podkładu.
   Oba widoki to tylko przeliczenie świat ↔ ekran (obrót o azymut osi + rozciągnięcie w poprzek). Geometria i zapis się nie zmieniają; rysować można w obu.
2. **Oś schematu:** domyślnie kierunek najdłuższego toru (cięciwa), tak żeby wschód był po prawej; przycisk „Oś wg toru” bierze kierunek zaznaczonego toru, „↻ 180°” odwraca. Zapis w projekcie (`axisAz`), z cofaniem.
3. **Wygląd:** bez szyn przy przybliżeniu, linie 1,2 px (tory boczne) / 2 px (główne), czarne. Znaki Ig-10 w stałym przedziale wielkości (2,2–3,6 px/mm arkusza), więc nie puchną przy przybliżaniu. Napisy mają stałą wielkość. Numer toru jest małą czcionką nad osią, bez ramki.
4. **Odgałęzienie trzyma skos rozjazdu:** wierzchołki PR i ŚR ustawia rozjazd, a trzeci wierzchołek leży zawsze na prostej skosu za ŚR (`branchLen` – odległość od ŚR, w terenie). Przesunięcie rozjazdu przenosi całe odgałęzienie, a ten wierzchołek można przeciągać tylko wzdłuż skosu. Wcześniej po przesunięciu rozjazdu albo wierzchołka kąt przestawał być 1:N.
5. **Odgałęzienie do toru równoległego:** przy wstawianiu rozjazdu puszczenie myszy obok toru (nie na torze) buduje od razu tor odgałęźny: skos rozjazdu, łuk zwrotny R (pole R na pasku) i prosta równoległa w odstępie zaokrąglonym do 0,5 m (od 4,5 m). Krótkie przeciągnięcie daje samo odgałęzienie do KR, jak dotąd. Puszczenie na torze sąsiednim tworzy łącznik, bez zmian.
6. **Panel:** sekcje „Obszar” i „Podkłady” są zwinięte, a na wierzchu jest panel zaznaczonego obiektu.

## 5c. Budowa torów od nowa – prosta i łuk po kolei, łączenie, edycja łuku (decyzja właściciela, 2026-10-16)

Właściciel: „łączenie torów trzeba ogarnąć i to, jak łuki powstają”. Do poprawy są wszystkie cztery sprawy: łączenie (dziś osobny tor z załamaniem), łuk w wierzchołku wieloboku (trzeba trafić w punkt poza torem), brak połączeń łukiem i łuków odwrotnych, edycja łuku tylko liczbami. Model (wielobok styczny z R i L) zostaje, a zmieniają się narzędzia:

1. **Tor: tryb Prosta / Łuk** (przyciski na pasku, Tab przełącza). Każdy odcinek jest styczny do końca toru, jak w ISDR:
   - **Prosta** – na przedłużeniu, długość z położenia kursora albo z klawiatury. Pierwszy odcinek nowego toru ma kierunek za kursorem. Alt – wolny kierunek (załamanie z łukiem R w wierzchołku, dawny sposób, np. do odrysowania ze zdjęcia).
   - **Łuk** – promień R (i przejściowe L) z pola, strona i kąt za kursorem, z klawiatury długość łuku [m]; Shift – koniec łuku równoległy do osi schematu (tor stacyjny).
   - Wielobok powstaje sam: łuk o kącie α to wierzchołek w odległości T od końca, a zbędne wierzchołki na prostej znikają.
2. **Łączenie:** zakończenie na końcu innego toru (przyciąganie) łączy stycznie – jednym łukiem, gdy styczne się przecinają (R z pola albo największy, jaki się mieści), inaczej łukami odwrotnymi (esowanie, zmiana rozstawu). Tory scalają się w jeden, jeśli drugi nie jest związany z rozjazdem ani skrzyżowaniem. Start na końcu toru A i klik na końcu toru B łączy dwa istniejące tory.
3. **Edycja łuku myszą:** zaznaczony tor ma uchwyt w środku każdego łuku. Przeciągnięcie zmienia R (środek łuku idzie za kursorem po dwusiecznej), a R widać na żywo. Wierzchołek dalej przeciąga się jak dotąd.
4. Backspace cofa ostatni odcinek, Enter / PPM / dwuklik kończy, Esc rezygnuje.
5. **Łuk z punktów (J)** (2026-10-17, właściciel: „kilka punktów, żeby zrobić precyzyjniejszy łuk”, potem „ciężko jest”): klik łuku toru, potem punkty na osi toru ze zdjęcia (łuk i proste obok); wierzchołek, R i L metodą najmniejszych kwadratów (`Budowa.FitArcFree`); W – proste stałe, tylko R / L (`Budowa.FitArc`).
6. **Usuwanie kawałka toru i zaznaczanie kilku** (2026-10-17): PPM na torze – usuń kawałek (prosta / łuk) z rozcięciem toru, rozetnij tor (`Ciecie`); Ctrl + klik i Shift + prostokąt we Wskaż, Del usuwa naraz.
7. **Łuk o zmiennym promieniu** (2026-10-17): PPM na łuku – „Podziel łuk tutaj” / „na części” (kształt bez zmian), „Łuk z punktów” dopasowuje wszystkie części łuku złożonego naraz (`Budowa.SplitArc`, `SplitArcN`, `ArcGroup`, `FitArcGroup`).
8. **Wygładź łuk, przejściowe z prędkości** (2026-10-17): PPM „Wygładź łuk” (grupa → jeden łuk z L / bez L / dwa łuki, `Budowa.SmoothArc`); panel toru „Przejściowe z prędkości” (h i L ze wzorów kalkulatora, `UtVertex.H`, `Budowa.TransitionsFromSpeed`). Wartości dopuszczalne przykładowe – do potwierdzenia przez właściciela.

## 5d. Etap 5, część 1 – urządzenia na torze (2026-10-16)

Znaki wg Ig-10 zał. 8: semafor świetlny (s. 27), tarcza ostrzegawcza (s. 27–28), tarcza manewrowa świetlna (wysoka / karzełkowa, s. 29), kozioł oporowy (s. 25), wykolejnica (s. 52), licznik osi (s. 51), złącze izolowane (s. 50), ukres W17 (s. 24).

1. **Model `UtDevice`** (`Lcs.Model/UtDevice.cs`): rodzaj, nazwa (opis z planu, np. „A”, „Tm12”, „Wk3”), tor + kilometraż S [m w terenie], kierunek działania `Dir` (+1 – dla jazdy w stronę rosnącego km toru), strona `Side` (+1 – po prawej patrząc w kierunku działania), dla semafora liczba komór. Położenie wynika z toru: po przesunięciu toru urządzenie jedzie razem z nim. Usunięcie toru usuwa jego urządzenia.
2. **Narzędzie Urządzenie (U)**, rodzaj z listy na pasku. Wciśnij na torze w miejscu urządzenia i przeciągnij w stronę jazdy, której dotyczy, po stronie, po której stoi. Urządzenia bez kierunku (kozioł, licznik, złącze, ukres) – samo kliknięcie. Nazwę wpisuje projektant (okno z podpowiedzią kolejnej wolnej).
3. **Wskaż:** klik zaznacza, przeciągnięcie przesuwa wzdłuż toru, strzałki 0,1 / 1 m, Del usuwa, PPM – menu (zmień stronę, odwróć kierunek, usuń); panel: nazwa, km, kierunek, strona, liczba komór.
4. **Rysowanie:** znak w mm arkusza Ig-10 (1:2000 / 1:500 – skaluje się razem z torami, jak wydruk), maszt po stronie urządzenia, komory w stronę jazdy, której dotyczy (Ig-10 „A ⊢○⊘”), nazwa przy stopie.
5. **Część 2 (następna):** odcinki kontroli niezajętości (nazwy, granice z liczników osi / złączy, „Sprawdź”), wskaźniki W4 / W5, perony, przejazdy, kilometraż.

## 5e. Rozjazdy łukowe i wstawki międzyrozjazdowe (2026-10-17)

Właściciel: „te łączniki to wstawki międzyrozjazdowe, znajdź coś na ich temat” i „rozjazdy łukowe, które trzeba na bieżąco dostosowywać w edytorze”.

Źródła: rozporządzenie w sprawie warunków technicznych dla budowli kolejowych (Dz.U. 2014 poz. 867) § 24 ust. 2, § 33, § 35; Kampczyk, Skoczylas, „Geometryczne układy połączeń torów kolejowych z zastosowaniem rozjazdów zwyczajnych”, Przegląd Komunikacyjny 4/2017; `Zrodla/rozjazdy_1.pdf` (Rłj, Rłd, v = 3,6·√(0,65·R)).

1. **Wstawka międzyrozjazdowa** – odcinek między stykami dwóch rozjazdów, do 30 m (dłuższy to tor z numerem). W łączniku z krzyżownicami łukowymi końcami do siebie: l ≥ v/10, nie mniej niż 6 m (§ 24 ust. 2 pkt 4); ψ = V·(a₁ + a₂) / (3,6·(b + w)), b = 20 m, a = V² / (12,96·R) – nie więcej niż 1 m/s³ (§ 24 ust. 2 pkt 5, § 35 ust. 7). `Rozjazdy.Wstawki` liczy wstawkę łącznika (KR – KR) i odstępy do sąsiednich rozjazdów na torze zasadniczym; „Sprawdź” – wstawka za krótka, rozjazdy nachodzą na siebie, ψ > 1. Panel rozjazdu pokazuje wstawki.
2. **Rozjazd łukowy** = rozjazd zwyczajny z katalogu położony na łuku toru (wygięty). Program nie ma osobnych typów Rłj / Rłd: rodzaj i promienie wynikają z toru i liczą się przy każdej zmianie (`Rozjazdy.Geometry`, `Sync`). Krzywizna toru zwrotnego = krzywizna toru zasadniczego ± 1/R typu (1/Rz = 1/Rg + 1/R – odgałęzienie do środka łuku, Rłj; 1/R − 1/Rg – na zewnątrz, Rłd); kąt między torami na końcu rozjazdu zostaje 1:N. Na torze prostym geometria bez zmian. Uwagi: rozjazd na krzywej przejściowej albo na granicy prostej i łuku, zwrotny R < 140 m (§ 24 ust. 2 pkt 2). Panel: rodzaj, R obu torów, V z promienia.
   - Przybliżenie do sprawdzenia przez właściciela: tor zwrotny jako jeden łuk od PR (jak w rozjeździe prostym), bez osobnego wygięcia prostej za krzyżownicą; dokładne wzory łukowania – W. Rzepka, „Rozjazdy łukowe w planie i profilu” (nie mamy).
3. **Łącznik z rozjazdem** (panel i PPM rozjazdu): z dwóch osobno postawionych rozjazdów tego samego typu, także gdy odgałęzienie dociągnięto narzędziem Tor do drugiego (przedłużenia znikają, gdy końce się stykają). **Dopasuj promienie** (panel toru z błędem „łuk nie mieści się”): R zmniejszane po 5 %, nie poniżej 140 m; łuki rozjazdu bez zmian.
4. **Łącznik na łuku** (2026-10-17, zrzut właściciela: łącznik 41/42 – „tory łącznika nie są równoległe”, „łuk w wierzchołku nie mieści się”): tory obracają się na długości łącznika, więc skosy rozjazdów rozchodzą się o δ. We wstawce łuk wyrównujący (Kampczyk, Skoczylas 2017: „łuk kołowy wyrównujący w torach nierównoległych”) w przecięciu skosów X, w środku wstawki; R – największy, przy którym KR obu rozjazdów zostają na prostej (co 10 m). Tor łączący PR1 – ŚR1 – X – ŚR2 – PR2, wszystkie wierzchołki ustawia łącznik (`Rozjazdy.Relink`, `Equalizer`, `Sync`). Uwaga, gdy R < 140 m albo δ > 3°.

## 5f. Etap 5, część 2, etap 6 i etap 7 (2026-10-17)

Zakres z prośby właściciela: odcinki kontroli niezajętości, W4 / W5, perony, przejazdy, kilometraż; wysokości z NMT; kontrola układu, warstwy, wydruk w skali.

1. **Odcinki** projektuje człowiek: granice (liczniki osi, złącza izolowane) i nazwy. Program tylko wyznacza zasięg z układu i sprawdza, czy każdy kawałek toru ma odcinek i czy odcinki są rozdzielone. Nazwa jest przypięta do miejsca na torze – należy do kawałka, w którym leży. Przyjęte (do potwierdzenia): skrzyżowanie i rozjazd krzyżowy leżą w jednym odcinku; granica w obrębie rozjazdu (między PR a KR) to uwaga, nie błąd.
2. **Znaki** z Ig-10 zał. 8: W4 s. 33, W5 s. 30, peron s. 52 (krawędź – podwójna linia 0,7 mm), drogi s. 57 (kreska 2 / odstęp 1 mm), rogatki s. 53, wskaźnik km / hm s. 56 (niestabilizowany: kreska i km z jedną cyfrą), pochylenie niwelety s. 58, kilometraż s. 64. Warstwy – nazwy z kart zał. 8 (§ 4 ust. 4: schemat z warstwami).
3. **Kilometraż:** punkty km na torach (linia, km, kierunek), między punktami liniowo – kilometr skrócony / wydłużony. Tory bez punktów – rzut prostopadły na najbliższy tor z kilometrażem (Ig-10 § 4 ust. 1 pkt 20–21: km urządzeń wg linii).
4. **Profil:** niweleta na torze (załomy, łuki pionowe), teren z NMT GUGiK. NMT to teren – „Niweleta z terenu” jest tylko propozycją do ręcznej poprawki (wiadukty, przekopy).
5. **Kontrola:** otwarty koniec toru jest w porządku przy koźle albo po zaznaczeniu „granica obszaru” w panelu toru (tor biegnie dalej poza projektem).
6. **Wydruk:** to samo płótno co na ekranie, w trybie wydruku. PDF bez zewnętrznych bibliotek – obraz planu 150–300 dpi na stronie w wymiarze kartki (nie wektor). Podkłady nie są drukowane.

## 5g. Metody budowy łuku – całość (decyzja właściciela 2026-10-17: „wykonaj według planu, ważne, żeby była całość”)

**Stan: zrobione (2026-10-17) – wszystkie punkty 1–7; szczegóły i wyniki testu w `HISTORIA_EDYTOR_UT.md`.**

Ograniczenia dotychczasowego modelu (wielobok stycznych: wierzchołek + R + L): przejściowe zawsze równe po obu stronach, brak przejściowej
między łukami o różnych R (łuk koszowy ze skokiem krzywizny), wierzchołek (przecięcie prostych) poza torem – niewidoczny na zdjęciu.

1. **B – wielobok:** osobna przejściowa na wyjściu (`UtVertex.L2`, puste – jak L), styczne T₁ = m₁ + (R+p₂ − (R+p₁)·cos α)/sin α;
   blokada R w wierzchołku (`LockR` – dopasowania jej nie zmieniają); z klawiatury długość łuku i styczna T → R.
2. **A – trasa z elementów:** tor może mieć postać ciągu elementów (`UtTrack.Align`): prosta, przejściowa (klotoida – krzywizna
   liniowo od 1/R₀ do 1/R₁), łuk; początek i azymut. Daje L₁ ≠ L₂, przejściową między łukami, łuk koszowy. Zamiana wielobok ↔ elementy
   (w stronę wieloboku – przybliżenie z podaną odchyłką). Narzędzia wieloboku (wierzchołki) na torze z elementów – z komunikatem.
3. **E – wykres krzywizny** pod mapą dla zaznaczonego toru (krzywizna 1000/R od km), ostrzeżenia: skok krzywizny bez przejściowej
   (tory główne albo V > 40), za krótka prosta między łukami (warunki techniczne § 36: tory główne V/2,5, nie mniej niż 20 m; pozostałe 10 m),
   R za mały dla Vmax, przejściowa krótsza niż z prędkości; te same uwagi w „Sprawdź”.
4. **A – edycja na wykresie:** przeciąganie granic elementów (długości) i poziomu łuku (R), tabela elementów (długość, R₀, R₁),
   podział elementu, wstawienie przejściowej na granicy, usunięcie elementu; „Łuk z punktów” (J) na torze z elementów dopasowuje
   długości i promienie elementów do punktów.
5. **D – uchwyty na torze (wielobok):** prosta przesuwana równolegle (sąsiednie proste zachowują kierunek), początek łuku przesuwany
   wzdłuż prostej (zmienia się R).
6. **C – szybkie:** łuk styczny do prostych przez punkt = „Łuk z punktów” z W (jeden punkt); łuk z R i długości / stycznej z klawiatury (p. 1).
7. **F – punkty z OpenRailwayMap / OSM** jako punkty do dopasowania (tylko źródło punktów; rysuje projektant – zgodnie z decyzją z 2026-10-16
   o rezygnacji z generatora z OSM).

## 6. Pytania do właściciela

1.–7. Rozstrzygnięte (sekcja 5).
8. Wymiary a / b rozjazdów katalogowe (np. z Id-4 albo kart katalogowych) – jeśli masz, wpiszemy do tabeli typów.
9. Istniejące `Data/station.json` (Widzew, Fabryczna): zostaje tylko dla symulatora, a w nowym edytorze rysujesz od nowa – czy coś trzeba z niego przenieść (np. numery torów i zwrotnic)?
10. Rozjazdy łukowe: czy wystarczy przybliżenie z § 5e (jeden łuk toru zwrotnego), czy potrzebne są dokładne wymiary z kart katalogowych Rłj / Rłd? Wstawka – czy liczyć ją między KR (styki), jak teraz?
11. Odcinki: czy skrzyżowanie / rozjazd krzyżowy ma być zawsze jednym odcinkiem (tak jest teraz)? Czy nazwy odcinków mają mieć stały wzór (np. z5/7, 2-3)?
12. Kilometraż: czy wystarczą wskaźniki niestabilizowane (kreska + km), czy rysować też stabilizowane (kółko) z międzytorzem?
13. Peron: domyślna odległość krawędzi od osi 1,725 m i wysokość 0,76 m – czy inne wartości domyślne?
14. Przejazd: czy na planie potrzebne są też krzyże św. Andrzeja i sygnalizatory drogowe (Ig-10 s. 59–60), czy wystarczy droga, kategoria, rogatki i km?
15. Wydruk: czy potrzebny PDF wektorowy / DWG (Ig-10 § 1 ust. 15) i tabele z lewej strony arkusza (zał. 2–5), czy wystarczy obraz w skali z opisem?
