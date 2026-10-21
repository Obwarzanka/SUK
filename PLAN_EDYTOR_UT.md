# Nowy edytor układu torowego (UT) – plan

**Regresja S02.03, 2026-10-20:** profil usługi i lokalny health hosta nie zmieniają funkcji ani formatów UT/zobrazowania. Ponowiono całą bramkę: UT **570 OK / 0 błędów**, zobrazowanie **255/0**, Przechyłka **10/10**, Komputerownia **16/16**, tabor **20/20**, edytor taboru **42/0**, selftest **25/26**, zależności **4/4**; **36/36** porównań ze świeżą bazą zgodnych. Mapy/NMT przeszły, informacyjne Overpass na żywo zwróciło 344 węzły. Build Release 15 projektów 0/0. Dowody: `SUK_plan_multiplayer/raporty/S02.03.md`, `artifacts/2026-10-20-S02.03/regresja/`. Dane właściciela zachowano. Powtórzono 21.10.2026 po utwardzeniu profilu usługi (`artifacts/2026-10-20-S02.03/weryfikacja/`): UT **570/0**, zobrazowanie **255/0**, Przechyłka **10/10**, Komputerownia **16/16**, edytor taboru **42/0**, selftest **25/26** i **4/4**, **36/36** porównań.

**Regresja S02.02, 2026-10-20:** podłączono pulpit do wspólnego cyklu świata przez lokalny adapter; nie zmieniono funkcji ani formatów edytorów. Ponownie wykonano pełny test UT **570 OK / 0 błędów**, zobrazowania **255 OK / 0 błędów**, Przechyłki **10/10**, Komputerowni **16/16**, taboru **20/20** i edytora taboru **42/0**. Próba Overpass zachowała informacyjne HTTP 502; pozostałe kontrole map/NMT/geometrii przeszły. Dowody: `SUK_plan_multiplayer/raporty/S02.02.md`, `artifacts/2026-10-19-S02.02/regresja/`. Chroniony układ zachowany.

**Regresja S02.01, 2026-10-19:** dodano osobny host istniejącego rdzenia bez WPF; zakres/funkcje UT i zobrazowania zachowane. Bieżąca pełna regresja: UT **570 OK / 0 błędów**, zobrazowanie **255 OK / 0 błędów**, Przechyłka **10/10**, Komputerownia **16/16**, tabor **20/20**, edytor taboru **42/0**. Informacyjna próba Overpass otrzymała HTTP 502, właściwe asercje geometrii/map/NMT przeszły. Raport: `SUK_plan_multiplayer/raporty/S02.01.md`; szczegółowe dowody: `artifacts/2026-10-19-S02.01/regresja/`. Dane właściciela zachowano.

**Stan techniczny po odtworzeniu S01.03 (2026-10-19):** wszystkie 10 projektów, w tym oba edytory, model UT i kalkulator, mają .NET 10 / C# 14. SDK dokładnie 10.0.402, runtime minimum 10.0.13 z poprawkami 10.0.x; pięć launcherów buduje i uruchamia przez ten sam lokalny/systemowy `dotnet`. Na bieżącym HEAD `de06f19` zbudowano świeżą bazę .NET 8 i przebadano całość po migracji: UT **520 OK, błędów: 0**, zobrazowanie **137 OK, błędów: 0**, geometria/formaty zachowane. Wcześniejsze artefakty dotyczą starszego kodu. Szczegóły, jawne różnice usług mapowych i ograniczenia: `SUK_plan_multiplayer/raporty/S01.03.md`; bieżące dowody `artifacts/2026-10-19-S01.03-odtworzenie/`. Migracja nie dodaje nowych funkcji UT ani zobrazowania.

Projekt z 2026-10-16, po decyzji właściciela: edytory UT i zobrazowania budujemy od nowa, jako osobne aplikacje. Zaczynamy od UT. Starych edytorów nie przerabiamy, tylko je usuwamy (zostają w historii git).

Kolejność: **edytor UT → edytor zobrazowania → przekazywanie danych z UT do modułów symulatora.**

**Regresja całej aplikacji, S01.02 (2026-10-18):** wydzielono rdzeń dotychczasowego symulatora do `Lcs.Core`; model UT (`Lcs.Model`) i edytor zachowują dotychczasowy zakres oraz .NET 8. Końcowy build całego rozwiązania Release powiódł się. Pełny test `EdytorUT.exe --test` wykonał 474 kontrole bez błędów, obejmując prawdziwe podkłady, geometrię, NMT i wydruk. Wyniki: `SUK_plan_multiplayer/raporty/S01.02.md`; szczegóły przebiegu `artifacts/2026-10-18-S01.02/edytor-final/`. Nie wykonano etapu 8 ani integracji danych UT z symulatorem.

**Aktualizacja techniczna 2026-10-18:** `uruchom_edytor_ut.bat` korzysta ze wspólnego launchera i SDK zgodnego z `app/global.json` (SDK 10.0.402; obsługiwana lokalna kopia `.tools/dotnet`). Przy błędzie budowania terminal pozostaje otwarty. Edytor i model UT nadal mają dotychczasowe cele .NET 8; zakres etapów UT i ustalenia geometrii nie zmieniają się. Wyniki bieżącej regresji w raporcie `SUK_plan_multiplayer/raporty/S01.01_audyt_2026-10-18.md`.

**Stan (2026-10-17):** etapy 0–7 zrobione (skrót w `ROZWOJ.md`, etap 1, szczegóły w `HISTORIA_EDYTOR_UT.md`): podkłady, tory, rozjazdy (też łukowe i krzyżowe), urządzenia, odcinki kontroli niezajętości, W4 / W5, perony, przejazdy, kilometraż, profil z NMT, „Sprawdź”, warstwy, wydruk w skali (§ 5f). Pytania 11–15 rozstrzygnięte (§ 7). Następny: karty urządzeń w symulatorze (`PODZIAL_MODULOW.md` kroki 5–7, 9 i 10 zrobione, krok 8 odłożony; decyzja właściciela 2026-10-17), potem etap 8 – edytor zobrazowania (§ 7). **2026-10-18:** etap Z0 (funkcja semafora, posterunki, szlaki w UT) zrobiony – `PLAN_EDYTOR_ZOBRAZOWANIA.md`.  **2026-10-19:** zmiana numeru wstawionego rozjazdu (menu PPM, w łączniku oba numery naraz; zajęty numer – zamiana po potwierdzeniu; wpis w polu przyjmowany przed zapisem, cofnięciem i zamknięciem); etap 9 – rozjazdy krzyżowe, pochylenia i przycisk „Opisy” na pulpicie. Projekt testowy „LCS testowe” (dwie stacje, Rkpd / Rkp, szlak wewnętrzny) – `projekty_ut/lcs_testowe/`. Tarcze ostrzegawcze i powtarzacze: pole „Powtarza” w UT (z „Zaproponuj”), w symulatorze `repeats`. Semafor: W21 „świetlny” albo tablica, pasy i W21 eksportowane do symulatora (`stripes`, `w21`) – dobór sygnału do prędkości przebiegu. Edytor zobrazowania: „Kopiuj obraz”. Łączenie bramek i masztów na wysięgniku we wspólną konstrukcję (panel urządzenia, menu PPM); bramka nad 1 albo 2 torami, na 1 albo 2 słupach. **2026-10-21:** montaż rozjazdów na łuku samoczynny i „Napraw tory i łuki” (§ 5i); poprawiony promień toru zwrotnego rozjazdu łukowego i km obiektów po zmianie łuku.

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
| 8 | Edytor zobrazowania jako osobna aplikacja – plan: `PLAN_EDYTOR_ZOBRAZOWANIA.md` (zaakceptowany 2026-10-17). | – |
| 9 | Przekazywanie danych z UT do symulatora: z UT powstaje topologia (tory, zwrotnice, odcinki, sygnalizatory, długości, pochylenia, prędkości) dla komputera zależności, kart urządzeń i ruchu pociągów; przebiegi i zależności projektuje człowiek (tablica zależności). | **zaczęty 2026-10-18:** `DoSymulatora` (UT + obraz → `station.json`), „Do symulatora…” w edytorze zobrazowania; mijanka testowa w symulatorze (selftest 5 z 5); 2026-10-19: elementy Z4 na pulpicie, przycisk „Opisy”, pochylenia (`poch`), rozjazdy krzyżowe i skrzyżowania (St, Rkp, Rkpd). 2026-10-19: tarcze ostrzegawcze i powtarzacze (`repeats` z pola „Powtarza” w UT). Brak: pochylenia w ruchu pociągów |

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
6. **Usuwanie kawałka toru i zaznaczanie kilku** (2026-10-17): PPM na torze – usuń kawałek (prosta / łuk) z rozcięciem toru, rozetnij tor (`Ciecie`); Ctrl + klik i Shift + prostokąt we Wskaż, Del usuwa naraz. **2026-10-19:** Shift + klik zaznacza jak Ctrl + klik; rozcięcie także w łuku (podział łuku i cięcie na styku części); łączenie zaznaczonych torów Ctrl+J (`Laczenie.Join`: styk albo wstawka łukowa `Budowa.Connect`, przeniesienie obiektów, odwrócenie toru bez obiektów); narzędzie Tor wchłania przy połączeniu tylko tor, na którym nic nie leży (dawniej urządzenia wchłoniętego toru zostawały bez toru).
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

## 5h. Łuki ze śladu: OSM / OpenRailwayMap, zdjęcie, plan schematyczny (decyzja właściciela 2026-10-20: „wsio rób”, potem „daj możliwość budowania ogólnie po osi toru z ORM”)

**Stan: zrobione (2026-10-20) – punkty 1–6; szczegóły i wyniki testu w `HISTORIA_EDYTOR_UT.md`.**

Cel: łuk policzyć bez rysowania go najpierw ręcznie. OpenRailwayMap pokazuje dane OSM, więc ślad bierze się z tych samych dróg kolejowych (Overpass).

1. **Sieć torów OSM jako linie** (`OsmSiec` w `OsmPunkty.cs`): pole „sieć torów OSM (linie)” i „Pobierz sieć OSM z widoku” w panelu „Podkłady”; w narzędziach Ślad i Łuk z punktów pobiera się sama – także po przesunięciu mapy; obszar z czterech narożników ekranu (poprawka 2026-10-21: w obróconym Schemacie linie się urywały). Najbliższy punkt na linii, droga po liniach (Dijkstra przez wspólne węzły – rozjazdy).
2. **Narzędzie Ślad (F):** klik przy linii OSM – kolejne kliknięcia biegną po liniach; klik obok / z Ctrl – punkt ze zdjęcia; Ctrl + przeciągnięcie – malowanie punktów co 2 m po osi toru (samo przeciągnięcie przesuwa mapę – poprawka 2026-10-20, wcześniej malowanie blokowało przesuwanie). Backspace – cofnij krok, Esc – wyczyść. Ślad nie zapisuje się w projekcie.
3. **Krzywizna śladu** (`Lcs.Model/UtSlad.cs`, `Slad.Krzywizna`): kierunek z prostej regresji w oknie bazy, krzywizna z różnicy kierunków; baza dobrana do rozrzutu punktów (`BazaAuto`: OSM ok. 60–100 m, zdjęcie 15–25 m) albo wpisana. Na wykresie krzywizny pomarańczowo (na km zaznaczonego toru, gdy ślad biegnie wzdłuż niego), w panelu lista łuków (`Slad.Luki`: R z poziomu, L ze zboczy, kąt z całki krzywizny).
4. **Tor po śladzie** (Enter / panel): każdy łuk dopasowany osobno przy kilku L na start (`Doprecyzuj`), potem cała trasa z elementów (Levenberg–Marquardt, początek i azymut swobodne, R do pełnych metrów i drugie dopasowanie długości), punkty odstające pominięte. Drugi Enter / „Utwórz tor” – **wielobok** (wszystkie narzędzia toru działają), „Utwórz jako trasę z elementów” – trasa z elementów. To propozycja do sprawdzenia; zgodnie z decyzją z 2026-10-16 nie ma automatu, który sam rysuje stację – projektant wskazuje każdy tor.
   - Poprawka 2026-10-20 (zgłoszenie: „po zbudowaniu toru z osi ORM nie mogę zaznaczyć toru i edytować go”): dopasowanie robiło pętle i wybiegało za koniec śladu (tor 255 km), doprecyzowanie łuku „uciekało” (z 1,8° na 84°), łuk R ~80 km z szumu dawał wierzchołek kilometry dalej, a łuki bez prostej się nie mieściły. Teraz: kilometraż punktu na trasie zgodny z kilometrażem śladu, elementy kończą się przed ostatnim punktem, R od 140 m (MinRDiv) do 100 km, doprecyzowanie zgodne z łukiem z wykresu i w swoim oknie, szczątkowe łuki (< 2°) usuwane, kontrola wyniku (komunikat zamiast toru obok śladu); wielobok bez wierzchołków przy kącie ~0 i z promieniami dopasowanymi (`FitRadii`), gdy łuki się nie mieszczą. Po utworzeniu – narzędzie Wskaż z nowym torem zaznaczonym. Test „linia 2,3 km z OSM, 6 losowań”. `Trasa.Compute`: łuk, którego krzywa byłaby dłuższa niż 3 × oba boki wieloboku (+100 m), to załamanie z błędem „zupełnie się nie mieści” – stary tor z ORM (R 79 915 m przy wieloboku zawracającym, oś 255 km) zamrażał edytor (otwarcie 20 s, odświeżenie > 100 s), więc żadnego toru nie dało się zaznaczyć.
   - Ograniczenie danych: z węzłów OSM (co 10–15 m, rozrzut ok. ±0,5 m) R wychodzi z dokładnością kilku %, a krzywych przejściowych nie da się rozpoznać (test: sama prawdziwa trasa odbiega od węzłów średnio 25 cm). Dokładnie – ze zdjęcia (malowanie, J).
5. **Łuk z punktów szybciej:** malowanie (Ctrl + przeciągnięcie), Shift + klik dwa razy przy linii OSM – węzły z odcinka, G – punkty śladu, O – jak dotąd. Punkty odstające pomijane (3,5 σ z mediany, nie mniej niż 30 cm; gdy odstaje więcej niż ćwierć – żaden), X – bez pomijania; przy punkcie „pominięty … cm”.
6. **Łuk z opisu planu** (panel łuku w wieloboku, „Łuk z opisu planu”): R, L, styczna T, długość łuku Ł, kąt α, km PŁ / KŁ z planu schematycznego (rysunek w poprzek nie jest w skali, opis – tak). „Zastosuj z planu”: R wprost albo dobrany do T, Ł, km PŁ albo km KŁ (kilometraż z punktów km); porównanie plan / rysunek pokazuje różnicę kąta albo km – proste narysowane inaczej niż na planie.

## 5i. Montaż rozjazdów na łuku i „Napraw tory i łuki” (zlecenie właściciela 2026-10-21: „napraw i przeanalizuj montowanie rozjazdów łukowych automatycznie, gdy jest taka potrzeba, dodaj funkcję napraw tory i łuki”)

**Stan: zrobione (2026-10-21) – punkty 1–5; szczegóły, analiza projektu „Nowy obszar” i wyniki testu w `HISTORIA_EDYTOR_UT.md`.**

1. **Błąd montażu rozjazdu łukowego (naprawiony):** promień toru zwrotnego był zaokrąglany do 0,1 m, a ŚR liczony z dokładnego – styczna łuku wierzchołka ŚR wychodziła o 2–3 mm dłuższa od odcinka PR – ŚR (tolerancja 1 mm), więc odgałęzienie rozjazdu łukowego Rz 500 1:12 albo Rz 300 1:9 miało błąd „łuk nie mieści się – brakuje 0 m prostej”. Teraz promień bez zaokrąglenia, ŚR i KR nie bliżej niż styczna łuku zwrotnego (`Rozjazdy.Geometry`).
2. **Kilometraż obiektów po zmianie łuku (naprawiony):** zmiana R albo L łuku zmienia długość toru, a rozjazdy, urządzenia, odcinki, perony, przejazdy, punkty km i niweleta za łukiem mają stałe km – „Popraw łuk” i „Dopasuj promienie” przesuwały je w terenie. `Naprawa.Reanchor` liczy km z ich dawnego położenia (najbliższy punkt nowej osi w oknie wokół dawnego km). Używają go: montaż, „Popraw łuk”, „Dopasuj promienie”, „Przejściowe z prędkości”, „Napraw”. Pozostałe edycje wieloboku (przeciąganie wierzchołka, R z panelu) zostawiają km bez zmian, jak dotąd – do decyzji właściciela (§ 6, pyt. 16).
3. **Warianty montażu** (`Naprawa.Warianty`, zastępuje `Rozjazdy.ArcFixes`): dla każdego łuku pod rozjazdem – R w górę / w dół (kroki 1 %, połowienie, R do pełnych metrów), krótsza przejściowa (nie krótsza niż 10 m), bez przejściowej (z R dobranym); rozjazd w całości na prostej albo na stałym promieniu z zapasem 1 m, inne rozjazdy toru nie trafiają na granicę, tor bez nowych błędów, łuk za skosem odgałęzienia nie przed KR; każdy sprawdzony na kopii projektu po `Sync` (drugi rozjazd łącznika ustawia się sam). Miara – największe przesunięcie osi toru (cm); warianty powyżej 3 m pomijane. Do tego przesunięcie rozjazdu / łącznika (do 160 m, sprawdzone na kopii) i inny typ, gdy tor zwrotny R < 140 m.
4. **Montaż samoczynny** („gdy jest taka potrzeba”): po wstawieniu rozjazdu, łącznika, złączeniu w łącznik, przesunięciu i wpisaniu km – gdy rozjazd leży na granicy łuku albo na przejściowej, najlepszy wariant łuku stosuje się sam, jeśli oś przesuwa się najwyżej o 30 cm (próg potwierdzony przez właściciela 2026-10-21) i łuk nie traci przejściowej (`Naprawa.Auto`, `AutoDev`). W łączniku para razem (`Naprawa.AutoPlan`): poprawka tylko wtedy, gdy po niej oba rozjazdy leżą dobrze; drugi rozjazd liczony na nowym przecięciu skosu (`Rozjazdy.PairPos`), przesunięcie – całej pary (poprawka 2026-10-21 po zgłoszeniu „przy parach rozjazdów to nie działa”). Osobna zmiana „Montaż rozjazdu na łuku” – Ctrl+Z cofa samą poprawkę. Większe zmiany, usunięcie przejściowej i przesunięcie rozjazdu – tylko przyciskami w panelu / menu PPM.
5. **Napraw tory i łuki** (`Naprawa.Napraw`, przycisk „🔧 Napraw” na pasku, „Napraw tor i łuki” w panelu toru): zbędne wierzchołki (powtórzone, załamanie < 1 cm; nie wierzchołki ustawiane przez rozjazd ani z zablokowanym R), łuki, które się nie mieszczą (R po 1 %, przyjęte tylko, gdy wszystkie łuki toru się mieszczą), rozjazdy na granicy / przejściowej (jak p. 4); jedna zmiana, lista „Naprawa” (zmiany i co zostało do decyzji). Drugie uruchomienie nic nie zmienia. Nie rusza: tras z elementów, rozjazdów nachodzących na siebie, wychodzących poza tor, krótkich wstawek – tylko wypisuje.

## 5j. Narzędzie Szlak od nowa, nazwa szlaku (decyzja właściciela 2026-10-21: „działa okropnie, dodaj nazwę szlaku pełną i skróconą, zmień narzędzie zaznaczania szlaku, żeby było lepsze do budowania”)

**Stan: zrobione (2026-10-21); szczegóły w `HISTORIA_EDYTOR_UT.md`.**

1. **Nazwy:** `UtLine.Nazwa` (pełna, np. „Łódź Widzew – Łódź Andrzejów”) i `UtLine.NazwaSkr` (np. „ŁW – ŁA”). Propozycja (`Szlaki.ProponujNazwy`): przy semaforze wjazdowym – nazwa i kod jego posterunku, przy końcu na granicy obszaru – posterunek sąsiedni i jego skrót (pierwsze litery wyrazów). Wstawiana przy tworzeniu, idzie za wpisanym posterunkiem sąsiednim, dopóki projektant jej nie zmieni; „Zaproponuj” w panelu. Na planie i na liście – oznaczenie z nazwą skróconą; do symulatora `name` / `nameShort` (`BlockDef.Name`, `NameShort`).
2. **Narzędzie Szlak (B):** kursor na torze szlaku – podgląd (`Szlaki.Proponuj`): od kursora w obie strony po torach do pierwszego semafora wjazdowego działającego ze szlaku do posterunku albo do końca toru bez dalszego połączenia (granica obszaru); droga zielonym pasem, oba końce opisane, semafory odstępowe w ramkach, długość na pasku stanu; klik – szlak. Kursor w obrębie posterunku (za semaforem wjazdowym albo na torze stacyjnym bez wyjścia na szlak) – podgląd z błędem, klik nic nie tworzy. Szlak nie wchodzi na tory innego posterunku: kończy się na ostatnim semaforze w stronę stacji przed granicą (funkcja inna niż wjazdowy – uwaga). Klik na torze istniejącego szlaku – zaznacza go. Ctrl + klik – końce ręcznie jak dawniej, z podglądem drogi do końca pod kursorem.
3. **Droga przez styk kilku torów:** zamiast błędu „niejednoznaczna” – tor najbardziej na wprost, z uwagą.

## 6. Pytania do właściciela

1.–7. Rozstrzygnięte (sekcja 5).
8. Wymiary a / b rozjazdów katalogowe (np. z Id-4 albo kart katalogowych) – jeśli masz, wpiszemy do tabeli typów.
9. Istniejące `Data/station.json` (Widzew, Fabryczna): zostaje tylko dla symulatora, a w nowym edytorze rysujesz od nowa – czy coś trzeba z niego przenieść (np. numery torów i zwrotnic)?
10. Rozjazdy łukowe: czy wystarczy przybliżenie z § 5e (jeden łuk toru zwrotnego), czy potrzebne są dokładne wymiary z kart katalogowych Rłj / Rłd? Wstawka – czy liczyć ją między KR (styki), jak teraz?
11.–15. Rozstrzygnięte 2026-10-17 – zostaje jak jest (§ 7).
16. (2026-10-21, § 5i) Próg samoczynnego montażu i naprawy – **30 cm zostaje (decyzja właściciela 2026-10-21)**. Otwarte: czy km obiektów ma się przeliczać z położenia także przy ręcznej edycji toru (przeciąganie wierzchołka, R z panelu), czy zostać stały jak dziś?

## 7. Decyzje właściciela (2026-10-17)

1. Pytania 11–15 – **zostaje obecny stan**: skrzyżowanie i rozjazd krzyżowy to jeden odcinek; nazwy odcinków dowolne (bez wzoru);
   kilometraż – wskaźniki niestabilizowane; peron domyślnie 1,725 m od osi, 0,76 m wysokości; przejazd – droga, kategoria, rogatki, km;
   wydruk – obraz w skali z opisem.
2. **Edytor zobrazowania (etap 8):** czyta UT (tylko do odczytu), każdy element obrazu przypięty do obiektu UT; rysuje **człowiek**
   (bez propozycji z UT); skosy i symbole **jak w poprzedniej wersji** (siatka 8, łącznik „ukos 45° – pion – ukos 45°”, symbole
   z `PanelView` – zgodne z Ie-104 / Ie-20 i wzorami).
3. Z każdego urządzenia UT (semafor, licznik osi, zwrotnica…) powstaje **wirtualna karta** jak w rzeczywistości, która daje meldunki
   (`PODZIAL_MODULOW.md`, karty).
4. **Funkcja semafora** (wjazdowy, wyjazdowy, drogowskazowy, odstępowy, powtarzacz, PK) – pole w UT, panel urządzenia.
5. **Posterunki** – obszary w UT: tory przypisane do posterunku, urządzenia dziedziczą posterunek z toru.
6. **PIP (numery pociągów na pulpicie) nie zawsze jest** – opcja w danych zobrazowania, dla posterunku (dziś `display.pip` w `station.json`; edytor zobrazowania ma mieć to ustawienie).
7. **Kolejność: najpierw karty** w symulatorze (na obecnym `station.json`, `PODZIAL_MODULOW.md` kroki 5–6a), potem edytor zobrazowania.
