# Zobrazowanie ŁW + ŁF w jednym pliku – do akceptacji

Plik: `Nowy obszar – odzyskany – propozycja numeracji.v2.zob.json`. Jest przypięty do kopii UT `Nowy obszar – odzyskany – propozycja numeracji.ut.json` (odpowiedź właściciela: „Kopia z propozycją”).
Oryginały są bez zmian: `Nowy obszar – odzyskany.v2.zob.json`, `.ut.json` i `app/LcsWidzew/Data/station.json`.

Zrzuty: `zrzuty/propozycja_zobrazowania_LW/`
- `1_edytor_obraz_Widzew.png` – nowy obraz w edytorze zobrazowania;
- `2_pulpit_Widzew.png` – ekran Widzewa w symulatorze;
- `3_pulpit_Fabryczna.png` – ekran Fabrycznej w symulatorze, bez zmian względem oryginału.

## Co zrobiono

Według odpowiedzi „Dwa obrazy, jeden plik”:
- Do pliku zobrazowania z obrazem „Łodz Fabryczna” doszedł obraz szczegółowy **„Łódź Widzew”** (posterunek ŁW). Pozostałe obrazy są nietknięte.
- Obraz Widzewa przeniesiono jednorazowo ze strony „ŁW” starego pulpitu (`app/LcsWidzew/Data/station.json`).
  - Skala bez zmian: 1 px starego pulpitu = 1 j. Obraz ma 2000 × 1128 j., jak dawna strona 2000 × 1125.
  - Punkty dociągnięte do siatki 8.
  - Jedna linia była pod innym kątem niż 45° – tor 2 od Fabrycznej przy lewej krawędzi. Dostała załamanie: najpierw poziomo, potem 45°.
- Przeniesione elementy:
  - tory: odcinki proste, załamania i rozjazdy w węzłach;
  - 49 zwrotnic, w tym końce rozjazdów krzyżowych 17ab / 17cd / 18ab / 18cd wg wzoru właściciela;
  - 29 semaforów;
  - 32 tarcze manewrowe – nazwy „Tm…”; na starym pulpicie były same numery;
  - 12 powtarzaczy i tarcz ostrzegawczych;
  - 5 wykolejnic, 6 kozłów;
  - 3 perony, 2 przejazdy, 6 napisów;
  - tor bez kontroli niezajętości;
  - 8 strzałek blokady, 10 numerów torów.
- Pominięte: semafory odstępowe SBL. W v2 robi się je klockiem „odstępy SBL” z UT, a kolumn SBL nie stawiałem.
- Przypięcie do UT zrobiła funkcja edytora „Przypnij do UT” (F8). Wynik: **279 pewnych, 4 potwierdzone, 47 do wyjaśnienia, 6 bez przypięcia** (po nadaniu numerów torów w kopii UT – niżej). Bez przypięcia jest 6 napisów, co jest dla nich normalne.
- „Potwierdzone” to tylko końce rozjazdów krzyżowych 17 i 18 (przypięte do `17:ab`, `17:cd` itd.). F8 ich nie przypina, a konwersja ze starego edytora robi to tak samo.
- **Połączenie z Fabryczną odbywa się przez wspólny UT.**
  - Obraz Fabrycznej kończy się na końcu torów szlakowych 1 i 2 (km 1333 i 790).
  - Obraz Widzewa zaczyna się na torach stacji ŁW.
  - Nie ma dziury ani nakładania.
  - W symulatorze to dwa sąsiednie ekrany (zakładki „2 Łodz Fabryczna”, „3 Łódź Widzew”). Szlak ŁF–ŁW ma blokadę po obu stronach (strzałki 1ŁF / 1ŁW).
- Sprawdzenie eksportu „Do symulatora”: pliki tylko testowe, w katalogu tymczasowym; `Data/projekty` nietknięte.
  - Ekran Fabrycznej jest identyczny jak z oryginału.
  - 4 uwagi o przebiegach Fabrycznej („brak w układzie”) były już w oryginale.

## Numery torów ŁW w kopii UT (decyzja właściciela 2026-11-02: „Tak, nadaj numery”)

Tory bez numeru dostały numery wg Semaforka. Każdy numer pasował w obu źródłach: w przypięciu obrazu (łańcuch toru wyznaczony z rozjazdów) i w `railway:track_ref` z OpenRailwayMap.
- tor **1**: `9f06061b`; tor **3**: `c98da234`; tor **4**: `5298bb73`; tor **5**: `aaebb1d3`; tor **6**: `655bb95e`;
- tor **8**: `cbe9335b`, `944dc197`; tor **102**: `14b230e8`, `0cfc3cec`; tor **104**: `8b9a22fd`, `e3b11e3c`;
- tor **201**: `7bbc9ead`; tor **204**: `8ebd6794`.

Tory, które miały już numer, zostały bez zmian. Kontrola UT („Sprawdź”) daje 49 błędów, tyle co wcześniej, i 419 uwag zamiast 424.

## Uzupełnienie 2026-11-02 (prośba właściciela: braki w UT, odstępy SBL, łączenie przy lewej krawędzi, reszta błędów)

### Obraz Widzewa
- **Lewa krawędź:** tory szlaków 1ŁF i 2ŁW od Fabrycznej wchodzą poziomo w rzędy torów 1 i 2. Długie ukosy od krawędzi zniknęły.
- **Odstępy SBL** dostały szlaki 2A i 1A (samoczynne): po 3 pola, jak na starym pulpicie. W UT są na nich 1 odstęp, więc 2 pola mają tylko napis. Gdy w UT staną semafory odstępowe, pola dostaną je same.
- **Rozjazdy 21, 24, 43, 45, 47:** stare łamane nakładały się na siebie. Odcinki są teraz podzielone, a numer stoi w prawdziwym rozjeździe (przesunięcie o 8–16 j.).
- **Drugi „28”** (przy torach 102/104) ma na obrazie numer **29**, jak w UT.
- **Tm16:** symbol przesunięty na łącznik 24 → 104 w miejscu z UT.

### Przypięcie
- Wynik: **294 pewne, 39 potwierdzonych, 9 do wyjaśnienia** (wcześniej 47).
- Potwierdzone to miejsca, gdzie łańcuch toru na obrazie przechodzi przez kilka torów UT, a F8 tego nie umie:
  - łączniki 23, 24 → 104;
  - rzędy semaforów A (od rozjazdu krzyżowego 17 przez `564ebf07` do A na `c8a86843`) i H (tor 5 → `592911ba` → `5fee572a`);
  - odcinki przy końcach rozjazdów krzyżowych 17 i 18.
- km na tych odcinkach policzył konwerter, proporcjonalnie do rysunku między kotwicami.
- **Uwaga:** jeśli w edytorze naciśniesz F8, dwa odcinki mogą dostać inne km („km nieaktualne”). F8 nie obsługuje łańcuchów z kilku torów bez kotwic.

### Kopia UT – co doszło
- **Funkcje semaforów:** S1, S2, S3, L1, L3, L5, L7, M8 – wyjazdowy (eksport do symulatora je pomijał).
- **Szlaki** wg starego pulpitu, koniec toru oznaczony jako granica obszaru:
  - **2A** (W – Łódź Andrzejów, LK 17, tor 2, SBL), **1A** (V, tor 1, SBL);
  - **2O** (U – Łódź Olechów, LK 541, tor 2, PBL), **1O** (T, tor 1, PBL);
  - **1M** (A – Łódź Marysin, LK 16, PBL – rodzaj blokady do sprawdzenia), **2C** (H – Łódź Chojny, LK 540, tor 2, SBL).
- **Powtarzacze i tarcze ostrzegawcze:** ToG, IIISpG, IISpG, ISpG, ToH, IIISpH, IISpH, ISpH, ISpS3, ISpS7.
  - Stoją na drodze od semafora na zewnątrz: ISp 150 m, IISp 300 m, IIISp 450 m, To 1300 m.
  - To odległości szacunkowe, tylko kolejność jest jak na rysunku. OSM ma przy G tarczę ostrzegawczą bez nazwy na `7cda7f44` km 2341 – do porównania.
- **Kozły:** tory `49092800` km 374, 204 km 835, 7 km 600, `f36ec749` (CS) km 155 – tylko na otwartych końcach torów.
- **Przejazdy:**
  - km 5,077 – na torze 104, miejsce wg kilometrażu;
  - km 7,712 – na torze 2 (`725e7192`) km 180, miejsce z rysunku; kilometraż tam nie sięga.
- **Tarcze CS:** Tm100 (201 km 107), Tm136 (`f36ec749` km 56) – miejsca z rysunku.
- **Zmiany w poprzedniej propozycji:**
  - Tm21 przesunięta za Wk47 (s 14 → 67), bo na starym rysunku kolejność jest Tm21 – Wk47 – rozjazd 47;
  - Tm8 – kierunek odwrócony, jak na rysunku.
- Kontrola UT: **49 błędów** (bez zmian), **396 uwag** (było 419).
  - Nowe są 3 uwagi: na szlaku 2C stoją semafory A, C i S bez funkcji odstępowej. To semafory na `5fee572a` / dalej, nie Widzewa – do decyzji.

## Do wyjaśnienia (9) i inne sprawy dla właściciela

1. **ToU, ToT:** w UT tory szlaków 2O / 1O kończą się 484 m od U / T, więc tarcza ostrzegawcza (ok. 1300 m) nie mieści się w UT. Trzeba dorysować dalszy ciąg szlaku albo podać miejsce.
2. **Łącznik 17ab → 18cd (3 kawałki).** W UT tor 201 (`7bbc9ead`) zaczyna się przy 17 (styk z `2e246a3c`) i przechodzi przez rozjazd 24, Wk24, Tm15, Tm100 do rozjazdu krzyżowego 18. Na starym rysunku 24 i te urządzenia leżą na osobnej linii (y 384), a 17 łączy się z 18 krótkim łącznikiem „[”. Rysunek albo UT trzeba poprawić.
3. **Łącznik 44 / 46 (3 kawałki).** W UT odgałęzienie rozjazdu 46 (`9877f004`) kończy się w powietrzu (433005,1 / 537951,8) i nie łączy się z torem od 44 (`446cc3a8` → `bb8beb26`). To przerwa w geometrii UT.
4. **Kozioł na końcu toru 201 (x 960, y 384).** W UT ten koniec toru łączy się z innym torem (patrz pkt 2).
5. **Końce rozjazdów krzyżowych 17ab / 17cd / 18ab / 18cd.**
   - „Sprawdź” zgłasza „przypięty do obiektu, którego nie ma w UT”, ale to tylko ograniczenie edytora. Przypięcie do końca rozjazdu krzyżowego (`id:ab`) obsługuje eksport do symulatora, ale nie „Sprawdź” (`ObrPrzypinanie.Opis`).
   - Do poprawki w kodzie (osobna sprawa).
6. Obraz poglądowy nadal bez ŁW.

## Tablica przebiegów Widzewa (2026-11-02, po zgłoszeniu „nic nie działa”)

- **Przyczyna.** Plik zobrazowania miał tablicę przebiegów tylko dla ŁF i ŁW R3 pzs (21 przebiegów). Od 2026-10-26 symulator bierze wyłącznie przebiegi z tablicy, więc semafory Widzewa nie miały żadnego przebiegu.
- **Propozycja w kopii.** Dodałem 499 przebiegów od semaforów i tarcz ŁW oraz od A i H, wyznaczonych z układu tym samym kodem co okno „Przebiegi…” w edytorze. Zrobiłem to tak jak przycisk „Przyjmij wszystkie widoczne”, a wariant 0 jest przebiegiem zasadniczym. Tablica ma teraz 520 przebiegów. Niepotrzebne usuniesz w oknie „Przebiegi…” (F6).
- **Test każdego przebiegu** (jak „Testuj” w edytorze): **471 z 499 daje sygnał zezwalający**. Pozostałe 28 są odrzucone zgodnie z zasadami:
  - 20 wyjazdów na szlak 1ŁW-ŁCh – w stanie zasadniczym blokada ma kierunek wjazdu, więc najpierw trzeba ją zmienić;
  - 8 manewrów od semaforów wjazdowych – dozwolone tylko przy powrocie taboru ze szlaku.
- **Bez żadnej drogi w układzie:** S7, Tm33, Tm136. W UT tor 7 za S7 nie ma połączenia.
- **Do symulatora.** Układ jest zapisany jako `app/LcsWidzew/Data/projekty/Nowy obszar – odzyskany – propozycja numeracji.json`. Zrzut `zrzuty/propozycja_zobrazowania_LW/4_pulpit_Widzew_przebiegi.png` pokazuje nastawione przebiegi D→S1 i S2→2A z sygnałem; przebieg W→L2 jest słusznie odrzucony, bo koliduje z S2→2A.

## Ruch pociągów (2026-11-02)

- **Pociągi czekają na pozwolenie.** Eksport do symulatora ustawia na każdym szlaku zewnętrznym kierunek blokady „wyjazd” (`DoSymulatora`: `defDir = outward`), a UT nie ma pola na kierunek zasadniczy. Sąsiad z pociągiem prosi więc o pozwolenie: alarm „żądanie pozwolenia … – Pzk 2A”. Po poleceniu `2A Pzk` kierunek zmienia się na przyjazd i pociąg wjeżdża na szlak.
- **Tego nie da się poprawić w danych.** Gdy w pliku symulatora ręcznie ustawić kierunek „przyjazd”, `TrackOrientation` przy wczytywaniu odwraca część torów razem z `outward`, ale `defDir` zostawia bez zmian. Działa to więc tylko na niektórych szlakach. Do poprawki w kodzie: pole „kierunek zasadniczy” szlaku w UT i jego obsługa w `DoSymulatora` oraz `TrackOrientation`.
- **Zwrotnice 1, 2 (tor do Marysina, km 4,8 i 5,8) i 62 (tor do Chojen, km 1,85)** należą do innych posterunków. Wcześniej leżały na szlakach Widzewa i pociąg z Marysina je rozpruwał. Szlaki **1M i 2C kończą się teraz przed nimi**: granica obszaru w km 4,7 i 1,8 toru.
- **Test w symulatorze:**
  - 18100 z Andrzejowa przyjechał na przebiegu W→L2, a 64410 z Chojen na przebiegu H→S5; oba wjechały na stację;
  - kolejne pociągi z rozkładu dojeżdżają;
  - rozpruć nie ma.
