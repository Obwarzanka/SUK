# Nowy edytor zobrazowania (EbiScreen) – plan do akceptacji

Projekt z 2026-10-22. **Stan: E0–E8 zaakceptowane 2026-10-22 – plan wykonany; stary edytor (`app/Lcs.EdytorZobrazowania`) usunięty 2026-10-22** (stan etapów – § 10a).
Poprzedni plan: `PLAN_EDYTOR_ZOBRAZOWANIA.md` (etap 8, wykonany 2026-10-17 – 2026-10-19). Ten plan go **nie unieważnia** –
stary edytor działa dalej, dopóki nowy go nie zastąpi.

Podstawa: rozmowa z właścicielem 2026-10-22 (§ 0), kod obecnego edytora `app/Lcs.EdytorZobrazowania` i modelu `Lcs.Model/Zob*.cs`,
źródła: `Zrodla/KOMPUTERY.pdf` (zdjęcie 46, s. 147 – EbiScreen Psary), `KOMPUTERY_notatki.md`, `EBILock_komputerownia.md` § 5,
`Ie-104_2025_WCAG.pdf` i `Ie-104_symbole_*.png`, `ISDR_zdjecia.md` + `ISDR_klatki/`, `scren pulpitu GrM.png`,
`przebieg pociagowy GrM.png`, `SimRail_LCS_Widzew_wzor.webp`, `Semaforek_*.png`, `ROZWOJ.md` § 2, § 2a.

---

## 0. Decyzje właściciela z 2026-10-22

1. **Obecny edytor jest „okropny do pracy”.** Najbardziej przeszkadza: nie da się dobrze rysować **szlaków**, narzędzia są
   niewygodne, trzeba **wybierać tor UT przed rysowaniem**, **trudno poprawić rysunek**, program **odrzuca** kliknięcia i tłumaczy
   to tylko w pasku stanu.
2. **Najpierw rysunek, potem przypięcie do UT.** Rysuje się swobodnie (siatka 8, kierunki co 45°), bez wybierania obiektów UT.
   Przypięcie do UT to osobny krok „Przypnij do UT”.
3. **Szkic na życzenie.** Program może – tylko na polecenie człowieka – zaproponować szkic fragmentu (np. głowicy) z UT. Szkic jest
   szary, do zatwierdzenia, przesunięcia albo odrzucenia. *Zmienia to decyzję z 2026-10-17 („bez propozycji z UT”) – w zakresie
   szkicu na życzenie. Zasada „obrazy projektuje człowiek” zostaje: nic nie trafia do obrazu bez akceptacji.*
4. **Nowy program obok starego.** Stary edytor zostaje, dopóki nowy go nie zastąpi; nowy czyta stare `*.zob.json`.
5. **Szlaki** – mają być łatwe: rząd numerów odstępów SBL, kolumna numerów pociągów (PIP) z boku obrazu, końcówka szlaku jako
   jeden klocek, szlak bez torów szlakowych w UT (przypięty do szlaku / blokady z UT, nie do toru).
6. **Siatka 8 j., obraz 1920 × 1080 j.** – bez zmian (decyzja 2026-10-17).
7. **Przypinanie, gdy program nie jest pewny:** lista „do wyjaśnienia” z podświetleniem na obrazie i w podglądzie UT;
   rysunek nigdy nie jest blokowany.
8. **Sposób rysowania na co dzień** („jedno pióro + paleta” czy „klocki z palety”) – właściciel prosi o rozwinięcie: § 5.
9. **Odpowiedzi na pytania § 11 (2026-10-22, druga rozmowa):** program `EdytorZobrazowania2.exe`; pióro **i** paleta (symbole + klocki); rozjazd
   powstaje sam; etapy w kolejności planu; opis na obrazie może różnić się od nazwy w UT (uwaga „Weź z UT”); kolumnę PIP i pola numerów
   rysuje człowiek; nazwa sąsiada i nr linii z UT, do zmiany na obrazie; strefy i klawisze 1–9 – domyślne z planu.

## 1. Co utrudnia pracę w obecnym edytorze (z odwołaniem do kodu)

| Problem | Gdzie w kodzie | Skutek dla rysującego |
|---|---|---|
| **Tor UT trzeba wybrać przed rysowaniem.** Narzędzie Tor startuje dopiero po wyborze toru w panelu UT albo w menu przy węźle (`OnTorNeedsUt`, `StartTor`). Km od–do program bierze sam – pierwsza nienarysowana luka (`ZobTory.Luki(...)[0]`). | `MainWindow.Tory.cs` 131–199 | Myśli się „tor UT o id…”, a nie „rysuję głowicę”. Drugi kawałek toru dostaje km, którego się nie chciało; poprawka – ręcznie w polach km. |
| **Jeden tor obrazu = jeden tor UT.** Łamana nie może przejść przez rozjazd (`ZobTor.Ut` = jeden tor UT, `Zachodzi`). | `Lcs.Model/ZobProject.cs` (`ZobTor`), `ZobTory.cs` | Tor szlakowy przez kilka torów UT trzeba rysować w kawałkach, zawsze od węzła. |
| **15 narzędzi w 3 paskach**, każdy rodzaj symbolu osobnym trybem (Z, S, W, K, P, O, N, B, X, peron, przejazd…). Symbol bierze „następny nieumieszczony” obiekt UT danego rodzaju (`PickSym`). | `MainWindow.cs` 103–125, `MainWindow.Symbole.cs` 59–75 | Ciągłe przełączanie trybów; trzeba pamiętać, który semafor jest „następny”. |
| **Odrzucanie kliknięć.** Symbol musi trafić w linię toru obrazu, na którym w UT leży jego km (`PlaceSym`: „najpierw narysuj tor…”, „kliknij na linii tego toru…”, „strzałka ma stać za semaforem…”). | `MainWindow.Symbole.cs` 86–145, `MainWindow.Z4.cs` 46–68 | Nie da się postawić symbolu „na brudno”, żeby dorysować tor później. Komunikat znika po następnym ruchu. |
| **Łącznik tylko między torami poziomymi**, kształt sztywny (1 krok ukosu – pion – 1 krok), tory ≥ 2 kroki od siebie, tor łącznika wybrany z UT. | `MainWindow.Tory.cs` 201–225, `ZobTory.Lacznik` | Rozjazdy na ukosie, łączniki „długie” (jak w GrM: 7–9, 57–61) trzeba rysować narzędziem Tor punkt po punkcie. |
| **Brak zaznaczania grupy.** Zaznacza się jeden tor albo jeden symbol (`View.Select`, `SelectSym`); strzałki przesuwają jeden tor z jego symbolami (`MoveSelected`). Wierzchołki – pojedynczo. | `MainWindow.Tory.cs` 238–255, `ZobView.cs` | Przesunięcie głowicy, rzędu toru, „rozsunięcie” obrazu = dziesiątki operacji. Brak kopiuj / wklej fragmentu. |
| **Szlaki.** Strzałka blokady to symbol przypięty do końca `UtLine` i musi stać za semaforem wjazdowym na narysowanym torze UT; numery odstępów SBL, nazwa sąsiedniej stacji, kolumna PIP – nie da się ich narysować (pulpit je dolicza albo nie ma ich wcale). | `MainWindow.Z4.cs` 24–50, `ZobSymbole` | Szlaki na obrazie nie wyglądają jak na wzorach (GrM, Psary). |
| **Panele zależne od UT.** Lewy panel to drzewo obiektów UT (kolory zielony / brązowy / czerwony), prawy – właściwości toru + symbolu + Z4 + Sprawdź + obraz w jednym słupku (`BuildImgPanelContent`). | `MainWindow.cs` 128–200, 303–330 | Dużo czytania, mało rysowania; płótno jest wąskie. |
| **Komunikaty tylko w pasku stanu** (`Message`). | wszędzie | Błąd znika, zanim się go przeczyta; brak listy „co poprawić”. |

Co jest dobre i zostaje: plik `*.zob.json` obok UT z `.bak`, cofnij / ponów (`ZobDocument.Change`), siatka 8, kierunki co 45°
(`ZobTory.Kierunek`), „Sprawdź” (kolejność wzdłuż km, nieumieszczone), „Podgląd pulpitu” kodem symulatora, „Do symulatora…”
(`Lcs.Model/DoSymulatora.cs`), test „jak użytkownik” (`--test`).

## 2. Jak wygląda zobrazowanie EbiScreen (ze źródeł) – i co musi umieć edytor

Wzory: Psary (KOMPUTERY.pdf, zdjęcie 46), Grodzisk Maz. (SimRail, `scren pulpitu GrM.png`), Anastazów (ISDR), Ie-104 § 4–9.

| Cecha obrazu | Co widać | Wymaganie dla edytora |
|---|---|---|
| **Tło, kolory** | czarne tło; tor w stanie zasadniczym szary; zielony / żółty / czerwony / turkus / różowy – stany (Ie-104 paleta, `Ie-104_paleta_barw.png`) | rysujemy tylko stan zasadniczy (szary); stany pokazuje „Podgląd pulpitu” |
| **Tory w rzędach** | tory stacyjne poziomo, w równych rzędach (GrM ok. 100 px, Psary ok. 45 px); szlaki na przedłużeniu rzędów | **prowadnice rzędów** (opcjonalne poziome linie, do których przykleja się tor) i równe rozsunięcie rzędów jednym poleceniem |
| **Łączniki** | pion z krótkimi ukosami 45° („[”, GrM 1–2, 5–6), długie ukosy 45° (GrM 13–14, 31–38), łamane schodkowe (GrM 57–61) | łącznik dowolnego kształtu z odcinków 0° / 45° / 90°; gotowe kształty „[”, „/”, „schodek” |
| **Przerwy izolacyjne** | tor jest pocięty na odcinki krótkimi przerwami; rozjazd ma przerwę między ostrzem a odgałęzieniem | przerwa jako element na torze (klik), przerwy przy rozjazdach rysują się same z kształtu rozjazdu |
| **Rozjazdy** | iglica w węźle, odgałęzienie odsunięte; numer biały z „+” po stronie położenia zasadniczego; krzyżowe – dwie zwrotnice „ab / cd” zwrócone ostrzami (Ie-104 57–63, `PLAN_EDYTOR_ZOBRAZOWANIA.md` § 11) | rozjazd **powstaje sam**, gdy tor odchodzi od innego toru; numer i „+” stawia się obok (domyślne miejsce z kierunku odgałęzienia) |
| **Sygnalizatory** | semafor – podwójny grot „>>”, tarcza manewrowa – pojedynczy „>”, powtarzacz WJ/WY, PK – mały trójkąt; nazwa żółta nad / pod torem (A, G4, H14, ISpM4); trójkąt końca przebiegu przy wjazdowym | paleta: semafor, tarcza, powtarzacz, PK, wskaźnik W4; kierunek strzałki = strzałka myszy / klawisz obrotu; nazwa jako opis przy symbolu |
| **Opisy** | numery odcinków turkusowe pod torem (13, 34, 11k), numer toru biały w przerwie linii w środku toru (1, 2, 4a), numery zwrotnic białe | każdy opis jest **przypięty do elementu** i jeździ z nim; domyślne miejsce wg Ie-104 § 8 ust. 1, przeciąganie |
| **Szlaki** | końcówka: strzałka blokady żółta + zielona kreska, semafor wjazdowy, nazwa sąsiedniej stacji szara z numerem linii („Pruszków LK 447”, „Sosnowiec Dańdówka L171”); rząd numerów odstępów SBL nad kreskami (238 252 270 280); oznaczenie (1P, 2Z, JT1D, ToA, ISpA) | **klocek „koniec szlaku”**, **rząd odstępów SBL** (n równych pól), szlak bez torów UT |
| **Numery pociągów (PIP)** | kolumna numerów przy krawędzi obrazu (194, 208, 228…), czerwone pola numeru na szlaku (228-91922), szare pola puste | **kolumna PIP** i **pole numeru pociągu** jako elementy obrazu (tylko gdy posterunek ma PIP – `display.pip`) |
| **Inne** | perony – prostokąt z podwójną ramką i napisem; przejazd – „Y” z km (20.722); ikona nastawni „Gr” / „An”; tor bez kontroli – przerywany; kozioł „[”; wykolejnica | paleta: peron, przejazd, nastawnia, kozioł, wykolejnica; styl linii „bez kontroli” |
| **Ramy ekranu** | nagłówek („Zalogowany”, MIEJSCOWY, nazwa stacji), zegar, SERWER EBISCREEN 1/2, Komputer zależnościowy, panel Polecenia / Historia, okno ZDARZENIA | **nie rysuje ich edytor obrazu** – rysuje je pulpit (etap 5 `ROZWOJ.md`); edytor pokazuje tylko **strefy zarezerwowane** (np. prawy górny róg na panel poleceń), żeby tory na nie nie wchodziły |

Wniosek: obraz EbiScreen to **schemat z prostych odcinków na siatce, w rzędach**, z symbolami, które siedzą na torze, i opisami,
które siedzą przy symbolach. Edytor ma więc przypominać **edytor schematów elektrycznych** (rysujesz linie, wstawiasz symbole na
linię, etykiety jadą z symbolem), a nie edytor przypięć do UT.

## 3. Warianty

### Wariant A – „Najpierw rysunek, potem przypięcie” (**rekomendowany, wybrany przez właściciela**)

- Rysunek jest **samodzielnym grafem**: węzły (punkty siatki) i odcinki toru między nimi; rozjazd = węzeł, z którego wychodzą trzy
  odcinki; symbole stoją na odcinku albo w węźle; opisy są przypięte do elementów.
- Podczas rysowania **nie ma UT** – nie ma odrzuceń. Jedyne reguły: siatka 8, kierunki 0° / 45° / 90°, symbol musi stać na torze.
- Krok **„Przypnij do UT”** (przycisk, F8): program porównuje graf rysunku z topologią UT i przypina, co pewne – po numerach
  (opis „12” przy rozjeździe = rozjazd 12 w UT, „A” przy semaforze = semafor A), po sąsiedztwie (rozjazd między przypiętymi
  sąsiadami), po kolejności wzdłuż toru i kierunku. Reszta trafia na listę „do wyjaśnienia” (§ 6).
- Plusy: rysuje się jak na kartce; UT może być niepełne; łatwo poprawić rysunek (przypięcia idą za elementami).
  Minusy: algorytm dopasowania jest najtrudniejszą częścią; trzeba dobrej listy „do wyjaśnienia”.

### Wariant B – Siatka kafli jak EbiScreen

- Obraz to kratki (np. 16 × 16 j.); w kratce leży kafel (tor poziomo, ukos, pion, rozjazd lewy / prawy, sygnalizator…), jak w
  edytorach map w grach. Przypięcia jak w A albo jak dziś.
- Plusy: bardzo równy obraz, prosty zapis. Minusy: sztywne (tory ukośne długości nie wielokrotnej kafla, opisy między kratkami),
  krok 16 przeczy decyzji „siatka 8”; nie odróżnia się w działaniu od A z prowadnicami rzędów.

### Wariant C – Poprawa obecnego edytora

- Zostaje model „łamana = kawałek toru UT”; dochodzi zaznaczanie grupy, panel błędów, lepszy łącznik.
- Plusy: najmniej pracy, ten sam plik. Minusy: zostaje główny problem – wybór toru UT przed rysowaniem i odrzucenia.

**Rekomendacja: wariant A** jako nowy program, z dwoma elementami z B: **prowadnice rzędów** i **gotowe kształty** (klocki) w palecie.

## 4. Program i plik

- **Nowy projekt** `app/Lcs.EdytorZobrazowania2` (exe `EdytorZobrazowania2.exe`, launcher `uruchom_edytor_zobrazowania2.bat`, wpis w `Lcs.slnx`) –
  nazwa wg decyzji właściciela 2026-10-22. Model w `Lcs.Model` (nowe pliki `Obr*.cs`), widok WPF jak w pozostałych edytorach.
- **Plik:** `*.zob.json` **w wersji 2** (`"version": 2`) obok UT, poprzednia wersja w `.bak`. Nowy edytor czyta wersję 1
  (stary edytor) i przekształca ją do wersji 2 (tory obrazu → odcinki grafu, symbole → elementy, przypięcia → potwierdzone).
  Stary edytor wersji 2 nie otwiera (komunikat). Zapisu wersji 1 z nowego edytora nie ma.
- **Eksport do symulatora bez zmian w `DoSymulatora.cs` na początek:** wersja 2 + przypięcia → obiekt `ZobProject` w pamięci
  („kompilacja” grafu do kawałków torów UT z km) → istniejące `DoSymulatora`. Zmiana pulpitu nie jest potrzebna.
  Elementy, których v1 nie ma (rząd SBL, kolumna PIP, klocek szlaku), dochodzą do `DoSymulatora` / `station.json` w etapie E6.

### Model danych (wersja 2, szkic)

```json
{
  "version": 2,
  "ut": "widzew.ut.json", "utZapis": "2026-10-22T10:00:00", "siatka": 8,
  "obrazy": [{
    "id": "o1", "nazwa": "Łódź Widzew", "rodzaj": "szczegolowy", "posterunki": ["p1"], "szer": 1920, "wys": 1080,
    "rzedy": [344, 448, 712, 816],                          // prowadnice rzędów torów [j.] (tylko edytor)
    "strefy": [{ "rodzaj": "polecenia", "prost": [1440, 0, 480, 160] }],   // miejsca zarezerwowane przez pulpit
    "wezly":   [{ "id": "w1", "p": [96, 344] }, { "id": "w2", "p": [480, 344] }],
    "odcinki": [{ "id": "e1", "a": "w1", "b": "w2", "pkt": [], "styl": "ciagly" }],   // pkt – załamania między węzłami; styl: ciagly / bezKontroli / rozebrany
    "elementy": [
      { "id": "s1", "typ": "semafor", "na": "e1", "t": 0.25, "kier": 1, "opisy": [{ "rola": "nazwa", "tekst": "A", "d": [0, -16] }] },
      { "id": "z1", "typ": "zwrotnica", "na": "w2", "opisy": [{ "rola": "numer", "tekst": "12", "d": [-8, -16] }] },
      { "id": "p1", "typ": "przerwa", "na": "e1", "t": 0.6 },
      { "id": "k1", "typ": "koniecSzlaku", "na": "w1", "kier": -1, "czesci": ["strzalka", "semaforWjazdowy", "nazwaSasiada"], "opisy": [...] },
      { "id": "r1", "typ": "rzadSbl", "na": "e0", "pola": 4, "opisy": [...] },
      { "id": "pip1", "typ": "kolumnaPip", "p": [8, 232], "pola": 6 },
      { "id": "n1", "typ": "napis", "p": [960, 120], "tekst": "GRODZISK MAZOWIECKI", "styl": "tytul" }
    ],
    "przypiecia": [
      { "el": "e1", "ut": { "tor": "a1b2c3d4", "s0": 0, "s1": 412.5 }, "stan": "pewne" },
      { "el": "s1", "ut": "c9d0e1f2", "stan": "potwierdzone" },
      { "el": "z1", "ut": null, "stan": "doWyjasnienia", "kandydaci": ["e5f6a7b8", "f0a1b2c3"] }
    ],
    "szkic": null                                                  // propozycja z UT czekająca na akceptację (§ 7)
  }]
}
```

- Element na odcinku ma **położenie względne** `t` (0–1 długości odcinka) – przesunięcie węzła przesuwa symbole i opisy.
- Opis ma **przesunięcie względem elementu** `d` – jedzie razem z nim.
- **Przypięcia są oddzielone od rysunku.** Zmiana rysunku nie gubi przypięć; usunięcie elementu usuwa jego przypięcie.
  `stan`: `pewne` (dopasował program), `potwierdzone` (człowiek), `doWyjasnienia`, `brak` (element bez znaczenia ruchowego).
- Tekst opisu (np. „12”, „A”) wpisuje człowiek albo przychodzi z UT po przypięciu; przy różnicy „Sprawdź” daje uwagę z przyciskiem
  „Weź z UT”. *Do potwierdzenia, czy opisy po przypięciu mają być zawsze brane z UT (§ 11 pyt. 4).*

## 5. Rysowanie na co dzień – dwa sposoby (rozwinięcie dla właściciela)

### Sposób 1 – „Jedno pióro + paleta symboli”

- **Pióro (T)** rysuje tory: klik – klik – klik, Enter / PPM / dwuklik kończy. Kierunki tylko 0° / 45° / 90° (najbliższy do
  myszy), przyklejanie do siatki 8, do prowadnic rzędów i do istniejących torów.
- **Rozjazd powstaje sam:** gdy zaczniesz albo skończysz pióro na środku istniejącego toru, w tym miejscu powstaje węzeł z rozjazdem
  (z przerwą izolacyjną i odsunięciem odgałęzienia jak na pulpicie). Gdy dwa tory się przetną bez węzła – skrzyżowanie (pytanie:
  skrzyżowanie czy rozjazd krzyżowy – mała lista przy kursorze).
- **Symbole z palety** (pasek z ikonami jak na pulpicie): przeciągasz na tor albo wybierasz symbol i klikasz na torze. Symbol sam
  ustawia się w osi toru; kierunek – w stronę ruchu myszy przy upuszczaniu albo klawisz „spacja” (obrót). Opis pojawia się
  w miejscu domyślnym, z kursorem do wpisania tekstu (Enter – dalej).
- **PPM na elemencie** – menu edytora (to nie pulpit, więc PPM jest wolny): Usuń, Obróć, Opis…, Przypnij do…, Pokaż w UT,
  Styl linii, Podziel tor, Wstaw przerwę.
- Dobre do: torów o nietypowym kształcie, szybkiego „szkicowania z głowy”, poprawek.

### Sposób 2 – „Klocki z palety”

- Paleta ma gotowe **klocki** – kawałki obrazu: odcinek toru (długość ustawiana przeciąganiem końca), rozjazd lewy / prawy
  (w górę / w dół), łącznik „[” między dwoma rzędami, łącznik ukośny, rozjazd krzyżowy, koniec toru z kozłem, **koniec szlaku**
  (strzałka + semafor wjazdowy + nazwa sąsiada + rząd SBL), peron, przejazd.
- Klocek przeciągasz na siatkę; jego końce **zaczepiają się** o końce innych klocków (jak klocki Lego / tory w grach kolejowych).
  Rozjazd z klocka ma od razu właściwe przerwy i odsunięcie.
- Dobre do: typowych głowic, równych obrazów, szybkiego składania szlaków.
- Słabsze przy: nietypowych kształtach (trzeba łączyć wiele małych klocków).

### Propozycja: oba naraz

- Pióro **i** paleta w jednym edytorze: pióro do torów, paleta ma **symbole** (semafor, tarcza, przerwa…) i **klocki** (łączniki,
  rozjazd krzyżowy, koniec szlaku, rząd SBL, kolumna PIP, peron, przejazd). Klocek po wstawieniu jest zwykłym fragmentem grafu –
  można go potem edytować piórem i myszą.
- Własne klocki: zaznaczasz fragment (np. głowicę) → „Zapisz jako klocek” → wstawiasz w innym obrazie (bez przypięć).
- *Do decyzji właściciela (§ 11 pyt. 2).*

## 6. Przypinanie do UT („Przypnij do UT”, F8)

1. **Kotwice:** elementy z opisem zgodnym z nazwą w UT (zwrotnica „12”, semafor „A”, tor „4a”, odcinek „13”) – przypięcie `pewne`,
   gdy nazwa jest jednoznaczna w posterunkach obrazu.
2. **Tory:** dla każdej pary przypiętych węzłów (rozjazdy, semafory, końce) – droga w UT między nimi; odcinki rysunku na tej drodze
   dostają tor UT i km (km rozkładane proporcjonalnie między kotwicami – jak dziś `DoSymulatora` interpoluje między symbolami).
   Odcinek rysunku może przechodzić przez kilka torów UT (podział przy „kompilacji” do v1).
3. **Bez nazw:** rozjazd bez opisu – kandydaci z UT według sąsiadów; semafor bez opisu – według kolejności i kierunku na torze.
4. **Lista „do wyjaśnienia”** (panel z prawej, zawsze widoczna po przypięciu): element, powód („2 kandydaci: rozjazd 12 i 14”,
   „brak w UT”, „kolejność różna niż w UT”), przyciski kandydatów; najazd na pozycję podświetla element na obrazie i obiekt
   w podglądzie UT. Wybór = `potwierdzone`.
5. **Nieumieszczone:** obiekty UT posterunków obrazu, których nie ma na rysunku – druga zakładka listy (jak dziś).
6. Przypięcia się **nie gubią** przy zmianie rysunku; po zmianie UT (`utZapis`) – ponowne przypięcie tylko elementów, których
   obiekt UT zniknął albo się zmienił. Km torów nieaktualne po zmianie UT (inna długość toru, przesunięty styk) – błąd w „Sprawdź”
   i uwaga przy eksporcie, F8 liczy je od nowa (2026-10-26).
7. **Szlak bez torów UT:** odcinki szlaku, klocek końca szlaku i rząd SBL przypina się do `UtLine` (szlak / blokada) i jego końca
   (`UtLineEnd`), numery odstępów – do semaforów odstępowych UT, a gdy ich nie ma – zostają napisami (uwaga w „Sprawdź”).

## 7. Szkic na życzenie

- Zaznaczasz w podglądzie UT (albo na liście nieumieszczonych) fragment: głowicę, tor, szlak → **„Zaproponuj szkic”** → program
  rysuje propozycję w obszarze, który wskażesz prostokątem: tory w rzędach w kolejności z UT, rozjazdy w kolejności km, łączniki „[”,
  symbole z nazwami, od razu przypięte.
- Szkic jest **szary, przerywany, na osobnej warstwie** – nie jest częścią obrazu. Przyciski: **Przyjmij**, **Przyjmij i edytuj**,
  **Odrzuć**; Esc = odrzuć. Szkic można przesunąć jako całość przed przyjęciem.
- Szkic nigdy nie zastępuje narysowanych elementów (gdy obiekt UT już jest na obrazie – szkic go pomija).
- W `README.md` i w podpowiedzi przycisku: „to propozycja – obraz projektuje człowiek” (`ROZWOJ.md` § 2 pkt 2).

## 8. Okno, narzędzia, skróty

- **Płótno na większości ekranu**, czarne tło, siatka 8 (co 5 – grubsza), prowadnice rzędów (cienkie, przerywane), strefy
  zarezerwowane (półprzezroczyste ramki). Znaczniki tylko w edytorze.
- **Paleta** (pasek z lewej, ikony narysowane kodem pulpitu): symbole i klocki, z klawiszem przy każdej ikonie.
- **Prawy panel w zakładkach:** *Właściwości* (zaznaczony element: typ, opis, styl, przypięcie), *Do wyjaśnienia / Sprawdź*,
  *UT* (drzewo obiektów i mały podgląd UT), *Obraz* (nazwa, rodzaj, posterunki, wymiary, PIP, opisy odcinków).
- **Uwagi na płótnie:** znacznik ⚠ przy elemencie z uwagą (klik – zakładka z opisem) zamiast komunikatu w pasku stanu.

| Klawisz | Działanie |
|---|---|
| V / Esc | Wskaż: klik, Shift + klik, prostokąt (zaznacza całe elementy), przeciąganie zaznaczenia, strzałki – o 8 j., Shift + strzałki – o 40 j. |
| T | Pióro toru; Backspace – cofnij punkt; Enter / PPM / dwuklik – koniec |
| Spacja | obrót symbolu / klocka (kierunek) |
| 1…9 | symbol z palety (semafor, tarcza, powtarzacz, PK, przerwa, wykolejnica, kozioł, przejazd, peron – kolejność do ustalenia) |
| L | klocek z palety (ostatnio użyty) |
| E | edycja opisu zaznaczonego elementu |
| Ctrl + C / V / D | kopiuj / wklej / powiel zaznaczenie (bez przypięć) |
| Ctrl + G | „Rozsuń rzędy”: zaznaczone rzędy w równe odstępy |
| Ctrl + Z / Y | cofnij / ponów |
| Del | usuń zaznaczenie |
| F7 | Sprawdź (Ie-104 i zgodność z UT) |
| F8 | Przypnij do UT |
| F9 | Podgląd pulpitu (kodem symulatora: stan zasadniczy, przykładowy przebieg, zajętość) |
| Home / 1:1 | cały obraz / powiększenie 1 |

- **Rozciąganie:** przeciągnięcie węzła ciągnie odcinki z nim połączone (zostają co 45°); przeciągnięcie odcinka poziomego w górę /
  w dół przesuwa cały rząd z łącznikami (łączniki „[” się wydłużają).
- **Narzędzie „Wstaw kolumnę / Usuń kolumnę”** (Ctrl + przeciągnięcie pionowej linii): rozsuwa / zsuwa obraz w poziomie od tego miejsca.

## 9. Sprawdź, podgląd, eksport

- **Sprawdź (F7):** wszystko z `PLAN_EDYTOR_ZOBRAZOWANIA.md` § 3 (kolejność wzdłuż km, nieumieszczone, Ie-104 § 4 ust. 15 – odstępy
  torów, opis jednoznaczny, obraz poglądowy tylko z symbolami z § 6 ust. 4) + nowe: element bez przypięcia, opis różny od nazwy w UT,
  element w strefie zarezerwowanej, odcinek nie co 45°, tor niepołączony, km torów nieaktualne po zmianie UT (F8).
- **Podgląd pulpitu (F9):** jak dziś (`PodgladWindow` – od E8 w `Lcs.EdytorZobrazowania2`), wejście przez „kompilację” v2 → `ZobProject`.
- **Do symulatora…:** jak dziś (`DoSymulatora`), z tą samą kompilacją; nie zapisuje `Data/station.json`.
- **PNG / druk:** jak dziś.

## 10. Etapy

| Etap | Zakres | Gotowe, gdy |
|---|---|---|
| E0 | Decyzje z § 11; nazwa programu; szkic okna (zrzut makiety) do akceptacji. | właściciel zaakceptował makietę i odpowiedzi |
| E1 | Szkielet: projekt, model `Obr*` (wersja 2), zapis / odczyt / `.bak`, płótno z siatką i prowadnicami, cofnij / ponów, test `--test`. | pusty obraz zapisuje się i wczytuje; test `błędów: 0`; zrzut |
| E2 | Pióro toru, węzły, rozjazdy powstające same, skrzyżowania, zaznaczanie grupy, przesuwanie, rozciąganie, kopiuj / wklej, rozsuń rzędy. | mijanka (`Testy/mijanka.ut.json`) narysowana klikami bez UT; przesunięcie całej głowicy jednym przeciągnięciem; zrzut |
| E3 | Paleta symboli (rysowane kodem pulpitu), opisy przy elementach, obrót, PPM menu edytora, uwagi ⚠ na płótnie. | mijanka z semaforami, tarczami, przerwami, numerami – wygląda jak pulpit; zrzut porównany z GrM / Psary |
| E4 | „Przypnij do UT”: kotwice po nazwach, tory i km między kotwicami, lista „do wyjaśnienia”, nieumieszczone, podgląd UT. | mijanka: wszystkie elementy przypięte (`pewne` + `potwierdzone`), lista pusta; „Sprawdź” bez błędów |
| E5 | Kompilacja v2 → `ZobProject`, Podgląd pulpitu, Do symulatora, odczyt starego `*.zob.json` (v1 → v2). | `mijanka_testowa.zob.json` (v1) otwiera się, eksport daje ten sam `station.json` co stary edytor (porównanie); selftest bez zmian |
| E6 | Szlaki: klocek końca szlaku, rząd SBL, kolumna PIP, pole numeru pociągu, szlak przypięty do `UtLine`; przeniesienie do `DoSymulatora` / pulpitu. | dwa szlaki mijanki jak na GrM (strzałka, WJ/WY, nazwa sąsiada, 4 odstępy SBL, kolumna PIP) – na pulpicie symulatora też |
| E7 | Klocki (łączniki, rozjazd krzyżowy, koniec toru) i własne klocki; szkic na życzenie (§ 7). | głowica mijanki złożona z klocków; szkic głowicy przyjęty i poprawiony; „Sprawdź” bez błędów |
| E8 | Obraz poglądowy, wiele obrazów, PNG / druk, README; decyzja o wycofaniu starego edytora. | Widzew (kopia UT) – obraz szczegółowy narysowany w nowym edytorze przez właściciela; właściciel decyduje o usunięciu starego |

Po każdym etapie: `dotnet build -c Release` nowego programu, test „jak użytkownik” (`EdytorZobrazowania2.exe --test wynik.txt zrzut.png`,
`błędów: 0`), selftest symulatora na `app/LcsWidzew/Testy/station_test.json` (gdy etap dotyka eksportu), zrzut, wpis w `ROZWOJ.md`
i stan w tym pliku.

## 10a. Stan etapów

| Etap | Stan | Wynik |
|---|---|---|
| E0 | **zrobiony 2026-10-22** | odpowiedzi na § 11 (niżej i § 0 pkt 9); makieta okna = zrzut E1 |
| E1 | **zrobiony i zaakceptowany 2026-10-22** | `app/Lcs.EdytorZobrazowania2`, model `Lcs.Model/ObrProject.cs` + `ObrDocument.cs` (wersja 2, graf, przypięcia osobno – `ut` jako id obiektu, `tor` jako kawałek toru UT), zapis z `.bak`, cofnij / ponów, płótno (siatka 8, prowadnice rzędów – narzędzie R i pole w zakładce Obraz, strefy zarezerwowane – domyślnie panel poleceń 480 × 160), zakładki obrazów, prawy panel w zakładkach, paleta z zapowiedzią E2–E7; plik wersji 1 – komunikat (odczyt E5), nie nadpisywany bez pytania; stary edytor odrzuca wersję 2. Test `--test` 64 sprawdzenia, 0 błędów; zrzut `zrzuty/EdytorZobrazowania2_E1.png`; selftest 25 z 26 / 4 z 4 |
| E2 | **zrobiony i zaakceptowany 2026-10-22** | Logika w `Lcs.Model/ObrGraf.cs` (bez WPF): odcinek zawsze prosty, załamanie = węzeł z dwoma odcinkami, rozjazd = węzeł z trzema (odgałęzienie = odcinek nie w linii z pozostałymi), skrzyżowanie / rozjazd krzyżowy = węzeł z czterema i elementem `skrzyzowanie` / `rozjazdKrzyzowy`. Pióro (T): klik – klik, kierunek co 45° najbliższy myszy, przyklejanie do siatki, prowadnic, węzłów i torów (koniec na torze dokładnie co 45°); start / koniec na torze – rozjazd sam; przecięcie bez węzła – skrzyżowanie, PPM – rozjazd krzyżowy; Enter / PPM / dwuklik / klik na torze – koniec, Backspace, Esc. Wskaż: klik, Shift + klik, prostokąt, przeciąganie zaznaczenia / węzła (odcinki się rozciągają, nie co 45° – uwaga ⚠, nie blokada), przeciąganie toru poziomego = rząd w pionie z łącznikami (załamanie „[” jedzie z rzędem, koniec długiego ukosu ślizga się po nim), strzałki 8 / 40 j., Del (dawny rozjazd scala się w prosty tor), Ctrl+C / V (w miejscu kursora) / D, Ctrl+A, Ctrl+G – rozsuń rzędy (skrajne zostają, środkowe w równych odstępach, prowadnice z nimi). PPM: podział toru, zaznacz rząd, styl ciągły / bez kontroli, usuń, wklej. Zakładka Właściwości: odcinek / węzeł / zaznaczenie, lista uwag. Odgałęzienie rozjazdu rysowane z przerwą 6 j. przy węźle (zasadnicze na wprost; „+” i numer – E3). Środkowy przycisk – przesuwanie widoku (lewy na pustym miejscu to teraz prostokąt). Test: mijanka klikami bez UT (tor 1, tor 2 z dwoma rozjazdami, tor 3 z łącznikiem „[”), głowica przesunięta jednym przeciągnięciem; `--test` 103 sprawdzenia (E1 + E2), 0 błędów; zrzuty `zrzuty/EdytorZobrazowania2_E2.png`, `_E2_glowica.png` |
| E3 | **zrobiony i zaakceptowany 2026-10-22** | **Wspólny kod symboli:** kształty z `PanelView` wydzielone do `LcsWidzew/UI/PulpitSymbole.cs` (semafor, wjazdowy z trójkątem końca wyjazdu, tarcza, powtarzacz / tarcza ostrzegawcza, PK, kozioł, wykolejnica, przerwa, przejazd, peron, pole numeru toru, barwy) – pulpit symulatora woła je, nowy edytor dołącza ten sam plik (stary edytor też, przez `PanelView`); zrzuty pulpitu przed i po identyczne co do piksela. Model `Lcs.Model/ObrSymbole.cs`: rodzaje elementów, położenie na torze (T) / w węźle / wolne, kierunek, domyślne miejsca opisów jak na pulpicie (sygnalizator – z prawej patrząc w kierunku ostrza, zwrotnica – po stronie przeciwnej do odgałęzienia, odsunięta od iglicy), uwagi (sygnalizator bez nazwy, rozjazd bez numeru, kozioł nie na końcu). Paleta z ikonami rysowanymi kodem pulpitu: 1 semafor, 2 tarcza, 3 powtarzacz, 4 PK, 5 przerwa, 6 wykolejnica, 7 kozioł, 8 przejazd, 9 peron, N numer toru, O opis odcinka, X napis; cień symbolu pod kursorem; ostrze domyślnie w stronę środka obrazu, spacja w narzędziu – odwrotnie. Opis wpisywany na płótnie (Enter / Esc), E – opis zaznaczonego, na rozjeździe – numer zwrotnicy z „+” (na wprost / na bok – przerwa na nieczynnym ramieniu jak na pulpicie, 9 j.). Wskaż: klik / Shift na symbolu, przeciąganie symbolu wzdłuż toru, przeciąganie opisu (krok 2 j.), spacja – obrót zaznaczonych (opis domyślny przechodzi na drugą stronę), Del; symbole i opisy jadą z torem przy przesuwaniu, rzędach, kopiuj / wklej. PPM symbolu: obróć, opis, opis w miejscu domyślnym, odmiana (semafor: z tarczą manewrową / pociągowy / wjazdowy; powtarzacz semafora / tarcza ostrzegawcza), usuń; PPM rozjazdu: numer, „+”; PPM toru: wstaw przerwę. Właściwości elementu: nazwa, odmiana, rozmiar peronu / zasięg przejazdu. ⚠ przy elementach z uwagą. Tory łączą się na załamaniach zaokrąglonym końcem (jak `PanelView`). Test: mijanka z semaforami A, B (wjazdowe), C1, C2, D1, D2, tarczą Tm1, wykolejnicą, kozłem, przerwami, przejazdem, peronem, numerami torów i zwrotnic – `--test` 152 sprawdzenia (E1–E3), 0 błędów; zrzuty `zrzuty/EdytorZobrazowania2_E3*.png`, porównanie ze wzorem `_E3_porownanie_GrM.png`. **Różnice wobec GrM do decyzji:** napisy względnie mniejsze, „+” bliżej numeru, tor jaśniejszy – tak rysuje pulpit symulatora (Ie-104 paleta, `PanelView`), edytor tego nie zmienia; opisy odcinków w barwie pulpitu (szare), nie turkusowe jak w tabeli § 2 |
| E4 | **zrobiony i zaakceptowany 2026-10-22** | `Lcs.Model/ObrPrzypinanie.cs` (bez WPF): 1) kotwice po opisach – zwrotnica „12” = rozjazd 12, sygnalizator / wykolejnica po nazwie (jednoznacznie w torach posterunków obrazu i szlaków do nich); 2) **łańcuchy** torów rysunku (przez załamania i skrzyżowania, do rozjazdów i końców) – tor UT z ramion zakotwiczonych rozjazdów (iglica / na wprost – tor zasadniczy, km maleje / rośnie wg `Dir`; odgałęzienie – tor odgałęźny od km 0 albo od końca przy `BranchAtEnd`), z nazwanych sygnalizatorów i numerów torów na łańcuchu; km rozkładane proporcjonalnie między kotwicami, koniec bez kotwicy = koniec toru UT; kierunek km z rozjazdów, potem z kolejności kotwic, potem z kierunków semaforów; sprzeczności (dwa tory, zła kolejność km) – do wyjaśnienia; 3) rozjazd bez numeru – wolny rozjazd UT na torze zasadniczym (jeden – pewne), drugi przebieg z nową kotwicą; 4) symbole bez nazwy, przerwy, kozły, przejazdy – najbliższy wolny obiekt UT tego rodzaju na torze łańcucha (kozioł do 25 m, inne do 40 m); nazwany sygnalizator: zgodność toru i kierunku z UT; opis spoza UT, ale obiekt UT w tym miejscu – przypięty (opis może być inny, § 11 pkt 4, uwaga w Sprawdź). Przypięcie odcinka: tor UT i km w węźle A i B (`Tor.S0` / `S1`, może maleć); węzła rozjazdu – rozjazd UT; elementu – obiekt UT; stany pewne / potwierdzone / do wyjaśnienia (z powodem i kandydatami) / brak; potwierdzone i „bez przypięcia” zostają przy kolejnym F8 i są kotwicami. Nieumieszczone: tory (brakujące km), rozjazdy, sygnalizatory (bez odstępowych – E6), wykolejnice, kozły, liczniki osi / złącza, perony, przejazdy. `Lcs.Model/ObrSprawdz.cs` – Sprawdź (F7): uwagi rysunku, bez przypięcia / do wyjaśnienia / obiekt usunięty z UT (błędy), opis różny od UT z „Weź z UT” (także numer rozjazdu przypiętego z sąsiedztwa), km dwa razy na obrazie, strefa zarezerwowana, Ie-104 § 4 ust. 15 pkt 1 i 3, obraz poglądowy, nieumieszczone. Okno: przyciski i F8 / F7, zakładka „Do wyjaśnienia” (pozycje z powodem, przyciski kandydatów, „bez przypięcia”, najazd – zaznaczenie na obrazie i obiekt w podglądzie UT; potem wynik Sprawdź z „Weź z UT”), zakładka UT: podgląd UT (tory przypięte ciemne, wskazany obiekt pomarańczowy) i nieumieszczone po grupach; ⚠ na płótnie także przy pozycjach do wyjaśnienia. Test: mijanka z UT testu (tor 1 ze szlakami, tor 2 z kozłami, łączniki 1/2 i 3/4, A, B, F) – rozjazdy 1–3 po numerach, 4 z sąsiedztwa, km 400 → 800 między rozjazdami 1 i 3, 0 → 478 na torze 2, semafor bez nazwy → kandydat F → potwierdzone, przerwa bez licznika → bez przypięcia, Weź z UT, lista pusta, Sprawdź bez uwag, przypięcia zostają po przesunięciu toru; `--test` 196 sprawdzeń (E1–E4), 0 błędów; zrzuty `zrzuty/EdytorZobrazowania2_E4*.png`. **Ograniczenia:** rozjazdy krzyżowe i skrzyżowania UT nie są jeszcze przypinane (E7), szlak i odstępowe – E6 |
| E5 | **zrobiony i zaakceptowany 2026-10-22** | `Lcs.Model/ObrKompilacja.cs` – obrazy v2 → `ZobProject` w pamięci: kolejne odcinki łańcucha z tym samym torem UT, ciągłym km i stylem = jedna łamana `ZobTor` (km na końcu w rozjeździe – dokładnie km rozjazdu z UT, żeby mapa km ↔ obraz w `DoSymulatora` była jak w starym edytorze), symbole w swoich punktach, opisy przesunięte – `Opis`; elementy bez przypięcia pominięte (uwaga), „bez przypięcia” – bez uwagi. `Lcs.Model/ObrZWersji1.cs` – plik starego edytora → v2: łamane torów przez pióro (rozjazdy i skrzyżowania same, km dzielony wzdłuż łamanej; `ObrGraf.Podziel` dzieli też km przypięcia), zwrotnice → węzeł + numer + „+” wg `Zas`, sygnalizatory wg rodzaju i funkcji z UT (wjazdowy, z tarczą manewrową wg komory B, tarcza, powtarzacz, PK), kozły, przerwy, numery torów, przejazdy, strzałki blokady (nowy element `strzalkaBlokady`, kształt neutralny ze wspólnego `PulpitSymbole.StrzalkaNeutralna` / `GrotBlokady` – pulpit nadal identyczny co do piksela), opisy odcinków, perony, napisy; przypięcia potwierdzone; plik v1 zostaje, propozycja zapisu `*.v2.zob.json`. F8 na obrazie z v1: koniec toru bez kotwicy bierze km z przypięcia potwierdzonego (tor narysowany w części), nie koniec toru UT. Okno: „Podgląd pulpitu” (F9 – okno podglądu i kod pulpitu symulatora dołączone ze starego edytora; **przed usunięciem starego edytora w E8 okno podglądu trzeba przenieść do wspólnego miejsca**), „Do symulatora…” (Data/projekty, bez nazwy „.v2”, `Data/station.json` nietknięty). Test: `mijanka_testowa.zob.json` (v1) otwarta w nowym edytorze – **eksport do symulatora identyczny ze starym edytorem** (porównanie drzewa JSON bez różnic), także po F8; podgląd pulpitu; „Do symulatora…”; mijanka z E4 – kompilacja, eksport i podgląd (blokady i semafory jak na pulpicie); `--test` 213 sprawdzeń (E1–E5), 0 błędów; zrzuty `zrzuty/EdytorZobrazowania2_E5_wersja1.png`, `_E5_podglad.png` |
| E6 | **zrobiony i zaakceptowany 2026-10-22** | Decyzje właściciela 2026-10-22: kolumna przy krawędzi (GrM 194 / 208 / 228) = **dalsze odstępy SBL** (jeden element z rzędem), czerwone pole „228-91922” = **okno numeru pociągu dla szlaku** (czerwone z „odstęp-numer”, gdy szlak zajęty, szare puste, gdy wolny; tylko posterunek z PIP), klocek „koniec szlaku” = strzałka + nazwa sąsiada + semafor wjazdowy. Elementy (`Lcs.Model/ObrSymbole.cs`): `odstepySbl` przy końcu toru (pól w rzędzie, w kolumnie, kierunek kolumny; numery pól od stacji), `polePip`, klocek `koniecSzlaku` (makro: strzałka zaraz za polami, semafor wjazdowy 40 j. dalej, napis „sąsiad” pod torem). `Lcs.Model/ObrSzlak.cs`: odstępy z UT (semafor wjazdowy → odstępowe → koniec szlaku), numery pól z nazw odstępowych, nazwa sąsiada „Andrzejów LK 17” z `UtLineEnd.Sasiad` i `UtLine.Linia`; F8 wpisuje je tylko do pustych opisów, „Sprawdź” – różnice z „Weź z UT”, pola bez odstępu w UT – ostrzeżenie. F8: szlak po torze i km elementu (`Linie`), km symbolu w środku odcinka – między kotwicami łańcucha (poprawka). Nieumieszczone: semafor odstępowy szlaku bez odstępów na obrazie. Kompilacja: tor za semaforem wjazdowym zastąpiony kreskami pól z km odstępów (pierwsze pole od punktu semafora, odstępowy na zewnętrznym końcu pola), okna PIP → `ZobObraz.PolaPip` → `display.trainFields` (`DoSymulatora`, klucz tylko gdy są). **Pulpit:** `Station.TrainFields`, `PanelView.DrawTrainField` – istniejące układy bez zmian (zrzuty identyczne co do piksela). Test: mijanka z UT testu – odstępy 4 + 3 (po prawej 4 + 2 w dół), klocki na obu końcach, dwa okna; F8 – 1A / 2A, „S60” i „Andrzejów LK 17” z UT, nazwa wpisana ręcznie zostaje, Weź z UT; kreski km 150 → 60 → 0; plik symulatora z oknami; podgląd pulpitu; `--test` 245 sprawdzeń (E1–E6), 0 błędów; pulpit symulatora z `#fault=secOcc:1A-0` – czerwone kreski i okno „S60”. Zrzuty `zrzuty/EdytorZobrazowania2_E6*.png`, `zrzuty/Pulpit_E6_okno_PIP.png`. **Ograniczenia:** pola bez odstępu w UT – na pulpicie tylko napis (UT mijanki ma jeden odstępowy); numer semafora odstępowego pulpit pisze dla kierunku ustawionego (jak dotąd) |
| uwagi 2026-10-22 | **zrobione** | Uwagi właściciela po E6: (1) „obracanie nie działa” – spacja w narzędziu symbolu obraca teraz właśnie wstawiony symbol (i ustawia kierunek następnych), wybór symbolu z palety zaczyna wstawianie od nowa; (2) „po co ta strzałka” – decyzja: **do wyboru przy semaforze** – odmiana „semafor wjazdowy bez końca przebiegu” (bez trójkąta), na pulpicie przez `display.noEndMark` → `SignalDef.NoEndMark` (trójkąt tylko przy wyborze końca przebiegu i przy nastawionym przebiegu; istniejące układy bez zmian co do piksela); (3) „nie mogę poruszać się swobodnie” – przeciągnięcie PPM przesuwa widok (klik PPM – menu), strzałki bez zaznaczenia, Shift / Ctrl + kółko. Test `--test` 254 sprawdzenia, 0 błędów |
| szlaki wewnętrzne 2026-10-22 | **zrobione** | Pytanie właściciela o blokady na szlakach wewnętrznych między stacjami obszaru LCS. Sprawdzone na `projekty_ut/lcs_testowe` (TG – 1GW / 2GW – TW). (1) **Odczyt obrazów starego edytora** – eksport był różny: stary edytor zapisuje dalszy ciąg toru za krawędzią jako łamaną zerowej długości (np. na obrazie TG tor TW km 0–100 w punkcie przy krawędzi) – teraz `ObrWezel.Poza` (km torów zebrane w punkcie, kompilacja oddaje je jak stary edytor); końce rozjazdów krzyżowych (ab / cd) – km z UT (`Krzyzowe.Wezly`), nie liniowo. Eksport `lcs_testowe` (v1) identyczny ze starym edytorem. (2) **Odstępy SBL** liczone po całej drodze szlaku (`ObrSzlak.OdstepyUt` – kawałki po kolei od semafora wjazdowego stacji obrazu, także przez tor stacji, tor szlakowy i tor drugiej stacji), koniec – semafor wjazdowy drugiej stacji; semafor wjazdowy szukany na torze pod odstępami albo na posterunku obrazu; pole na kilku torach – kilka kawałków kreski. (3) **Nazwa sąsiada** szlaku wewnętrznego – nazwa drugiej stacji obszaru („Testowo Wschodnie LK 1”). Test `RunSzlakWewnetrzny` – `--test` 263 sprawdzenia, 0 błędów. Pulpit: blokady szlaku wewnętrznego jak dotąd (`DoSymulatora` / `PanelView` bez zmian) |
| E7 | **zrobiony i zaakceptowany 2026-10-22** | `Lcs.Model/ObrKlocki.cs`: klocki wbudowane – łącznik „[” (ukos – pion – ukos do następnej prowadnicy rzędu, brak – 96 j.), łącznik ukośny, rozjazd krzyżowy (tor przez punkt + ukos, węzeł z elementem `rozjazdKrzyzowy`), koniec toru (z kozłem); 4 warianty (spacja – odbicie w poziomie / pionie); **wstawianie ze sklejaniem**: węzeł klocka w miejscu węzła – ten węzeł, na torze – tor dzieli się (rozjazd sam), odcinki pokrywające tor – scalone; własne klocki z zaznaczenia (bez przypięć, punkt zaczepienia – lewy górny węzeł), biblioteka `projekty_ut/klocki_zobrazowania.json`. `Lcs.Model/ObrSzkic.cs` – szkic na życzenie: wybrane tory UT (zakładka UT, bez zaznaczenia – nienarysowane), prostokąt na płótnie; rzędy w kolejności w poprzek głównej osi UT, położenie wzdłuż z UT, łączniki ukosem 45° (drugi rozjazd tam, gdzie kończy się ukos), sygnalizatory (bez odstępowych), kozły, numery torów i zwrotnic, przypięcia „pewne”; osobna warstwa `ObrObraz.Szkic` (szara, przerywana, w ramce), przeciąganie jako całość, pasek **Przyjmij / Przyjmij i edytuj / Odrzuć** (Esc), obiekty już na obrazie pomijane. Okno: klocki w palecie, „Moje klocki”, „Zapisz zaznaczenie…”, cień klocka pod kursorem. Przy okazji (pytanie właściciela o blokadę wewnętrzną): koniec szlaku strzałki blokady – semafor wjazdowy narysowany najbliżej / na posterunku obrazu (wcześniej po torze – na szlaku wewnętrznym strzałka trafiała do końca drugiej stacji); sprawdzone na 8 strzałkach `lcs_testowe` (4 na 1GW / 2GW) – jak w starym edytorze. Test: głowice mijanki z klocków, F8 i Sprawdź bez błędów; własny klocek w drugim obrazie; szkic – prostokąt, 4 rozjazdy co 45°, przesunięcie, Odrzuć / Ctrl+Z, Przyjmij i edytuj, poprawka rzędu, F8 i Sprawdź bez błędów; `--test` 294 sprawdzenia, 0 błędów. Zrzuty `zrzuty/EdytorZobrazowania2_E7_*.png` |
| szlak wewnętrzny na jednym obrazie 2026-10-22 | **zrobione** | Uwaga właściciela „dalej nie mogę zrobić szlaku wewnętrznego”: gdy obie stacje są na jednym obrazie, tor szlaku idzie od stacji do stacji i nie ma końca – a koniec szlaku i odstępy SBL dawały się postawić tylko przy końcu toru. Teraz: **koniec szlaku** klikiem w dowolnym miejscu toru – strzałka blokady i semafor wjazdowy w stronę stacji (gdy do rozjazdu go nie ma), bez nazwy sąsiada (druga stacja jest na obrazie); **odstępy SBL** klikiem na torze – tor dzieli się w tym punkcie, pola idą w stronę stacji, bez kolumny; **strzałka blokady** osobno w palecie; strona stacji sama (`ObrSymbole.StronaStacji` – po stronie rozjazdu, dwa – bliższy), spacja – odwrotnie (także po wstawieniu). Przy końcu innego toru obok – wygrywa tor bliższy kliknięcia. Przypinanie (F8) dwóch stacji na jednym obrazie: te same numery zwrotnic, nazwy semaforów, numery torów i nazwy odcinków w obu stacjach – rozstrzyga poprzednie pewne przypięcie, potem posterunek najbliższej kotwicy rysunku (tylko gdy każda stacja ma już kotwicę – inaczej człowiek potwierdza jedną); **tor przez kilka torów UT** (tor stacji – szlak – tor drugiej stacji jedną linią) dzieli się przy F8 w węzłach najbliższych styków torów (droga po stykach także przez tory bez kotwic). Odstępy SBL: semafor wjazdowy – narysowany najbliżej wewnętrznego końca rzędu. Test: obrazy TG i TW z `lcs_testowe` na jednym obrazie bez przypięć, 1GW połączony piórem – F8: semafory do swoich stacji, strzałki obu stacji do 1GW z różnymi końcami, koniec szlaku i odstępy klikiem w środku toru, spacja – pola do drugiej stacji, Sprawdź bez nowych błędów; `--test` 305 sprawdzeń, 0 błędów. Zrzut `zrzuty/EdytorZobrazowania2_szlak_wewnetrzny_jeden_obraz.png` |
| lista „Do wyjaśnienia” 2026-10-22 | **zrobione** | Uwaga właściciela „nie mogę nadać numeru toru”: na obrazie Łódź Fabryczna 19 torów „brak kotwicy”, a w UT wiele torów bez numeru – przyciski kandydatów „tor bez numeru” nie do odróżnienia. Teraz kandydaci po kolei (`ObrPrzypinanie.KandydaciPoKolei`: tory rozjazdów na końcach łańcucha i sąsiednich odcinków, potem nienarysowane), tor bez numeru albo z powtórzonym numerem – z posterunkiem i długością („tor bez numeru (ŁF, 209 m)”), pierwszych 10, reszta pod „więcej (n)…”; powód mówi wprost: wybierz tor UT poniżej albo numer zwrotnicy / nazwa semafora / numer toru (N). Numer toru nadaje się w edytorze UT – na obrazie numer toru (N) musi zgadzać się z UT |
| E8 | **zrobiony i zaakceptowany 2026-10-22; stary edytor usunięty** | Wiele obrazów: **Kopiuj obraz** (zakładka obok, rysunek, symbole i przypięcia; `MainWindow.Wydruk.cs`), kolejność ◀ ▶, usuń. **Obraz poglądowy:** rodzaj w zakładce Obraz – poglądowy dostaje wszystkie posterunki UT (cały obszar, Ie-104 § 6); Sprawdź – ostrzeżenia o symbolach spoza § 6 ust. 4 (tarcza, wykolejnica, PK, opis odcinka, peron), **nieumieszczone na poglądowym – tylko tory, rozjazdy, semafory, szlaki, przejazdy** (`ObrPrzypinanie.Nieumieszczone`); eksport do symulatora bez zmian (poglądowy tylko w edytorze, jak w wersji 1). **PNG…** i **Drukuj…** jak w starym edytorze (`ObrView.Czysty` – bez siatki, prowadnic, stref, węzłów, ⚠, szkicu). **Wycofanie starego edytora przygotowane:** `PodgladWindow.cs` i pliki testowe (`Testy/*.json`) przeniesione do `Lcs.EdytorZobrazowania2` (stary edytor linkuje je stamtąd) – nowy edytor nie zależy już od folderu starego. Test: kopia mijanki → poglądowy, tarcza – ostrzeżenie, uproszczenie (tarcza, kozły) – Sprawdź bez ostrzeżeń § 6 i bez błędów, eksport bez różnic, podgląd pulpitu, PNG 1920 × 1080 bez siatki i strefy, wydruk na stronę, ◀ / Ctrl+Z, zapis i odczyt; `--test` 319 sprawdzeń, 0 błędów; stary edytor 0 błędów; selftest 25 z 26, 4 z 4. Zrzuty `zrzuty/EdytorZobrazowania2_E8*.png`. Przy okazji (uwaga właściciela „przerwy przy rozjazdach zbyt blisko”): przerwa 9 j. na nieczynnym ramieniu liczona wzdłuż toru jak na pulpicie (PanelView skraca całą łamaną) – przy krótkim skosie przechodzi za załamanie; wcześniej ucięta do 1/3 pierwszego odcinka, więc ledwo widoczna (zrzut `zrzuty/EdytorZobrazowania2_przerwa_rozjazdu.png`). Do decyzji właściciela: usunięcie `app/Lcs.EdytorZobrazowania` (folder, wpis w `Lcs.slnx`, `uruchom_edytor_zobrazowania.bat`, sekcja README); odczyt plików wersji 1 zostaje (`ZobDocument`, `ObrZWersji1`) |
| F8 przez styki torów UT 2026-10-26 | **zrobione** | Obraz ŁF właściciela: tor narysowany jedną linią przez styk dwóch torów UT (ten sam numer, np. tor stacji i tor szlaku) bez węzła w pobliżu – „sprzeczne kotwice”, tor nie szedł na pulpit. Teraz `PodzielNaTory` podaje punkt podziału (`PunktPodzialu`, cała jednostka rysunku na odcinku poziomym / pionowym / 45°), Przypnij dzieli odcinek (`ObrGraf.Podziel`) i liczy od nowa; sygnalizator na styku końców torów – zgodny z torem po drugiej stronie (`NaStyku`); rozjazd narysowany z odgałęzieniem UT na wprost – ramiona zamienione (`OdwrocRamiona`, `Ctx.Odwr`, `Ramie`). Test E4 (tor 2 rozcięty w UT w miejscu F); `--test` 346 / 0; ŁF: 43 → 9 błędów (dane właściciela). Zrzut `zrzuty/Pulpit_LF_po_F8_styki.png` |
| km nieaktualne po zmianie UT 2026-10-26 | **zrobione** | Na pytanie właściciela o powiązanie UT ↔ zobrazowanie: przypięcie toru trzymało km na sztywno, „Sprawdź” wykrywał tylko usunięty tor / obiekt. Teraz `ObrPrzypiecie.DlToru` (długość toru UT przy F8), `ObrPrzypinanie.KmNieaktualne` (kopia obrazu przypięta od nowa, porównanie km idących na pulpit: końce łańcucha, zmiana stylu / toru, bez rozjazdów i załamań; tor inny po F8 – zawsze), Sprawdź – błąd na łańcuch i tor, `ObrKompilacja` – uwaga; F8: koniec bez kotwicy z km potwierdzonym na starym końcu toru → nowy koniec toru, inne km w granicach toru. Test E4 (tor 1 mijanki +100 m w UT); `--test` 335 sprawdzeń, 0 błędów. Zrzut `zrzuty/EdytorZobrazowania2_km_nieaktualne.png` |
| tablica przebiegów (ROZWOJ.md etap 3) 2026-10-26 | **zrobione, do oceny właściciela** | Decyzja właściciela: przebiegi projektowane w tym edytorze, symulator tylko czyta. Jak w starym edytorze: klik na semafor / tarczę → Właściwości „Przebiegi od X” (✓ w tablicy, ★ zasadniczy, V, klik – droga na obrazie, Wszystkie od X, Testuj); okno „Przebiegi…” (F6) – cała tablica, Sprawdź, Testuj. `ObrProject.Przebiegi`, `TablicaPrzebiegow.cs`, `MainWindow.Przebiegi.cs`, `PrzebiegiWindow.cs`; eksport – pole `przebiegi`. Test `--test` 379 sprawdzeń, 0 błędów; zrzuty `zrzuty/EdytorZobrazowania2_przebiegi_*.png` |

## 11. Pytania do właściciela – odpowiedzi 2026-10-22

1. **Nazwa programu:** `EdytorObrazow.exe` („Edytor obrazów pulpitu”) czy `EdytorZobrazowania2.exe`? → **`EdytorZobrazowania2.exe`**.
2. **Rysowanie na co dzień (§ 5):** pióro + paleta symboli, klocki, czy oba naraz (propozycja)? → **oba naraz** (propozycja).
3. **Rozjazd powstający sam**, gdy tor odchodzi od innego toru – zgoda? Czy rozjazd ma być zawsze stawiany osobno? → **tak, sam**.
4. **Opisy po przypięciu:** zawsze z UT (np. numer zwrotnicy z UT nadpisuje wpisany) czy człowiek może mieć inny tekst na obrazie? → **człowiek może mieć inny tekst**; „Sprawdź” daje uwagę z „Weź z UT”.
5. **Kolumna PIP i pola numerów pociągów:** czy są częścią obrazu (rysuje człowiek) czy pulpit układa je sam przy szlaku? → **rysuje człowiek** (elementy z palety).
6. **Nazwa sąsiedniej stacji i numer linii** („Pruszków LK 447”) – brać z UT (`UtLineEnd.Sasiad`, `UtLine.Linia`) czy wpisywać na obrazie? → **z UT, do zmiany na obrazie**.
7. **Strefy zarezerwowane:** panel Polecenia / Historia w prawym górnym rogu (ISDR) – jaki rozmiar? Inne stałe miejsca (zegar, serwery)? → **domyślne**: panel Polecenia / Historia 480 × 160 j. w prawym górnym rogu, zmiana w zakładce Obraz.
8. **Kolejność etapów:** szlaki (E6) wcześniej – np. zaraz po E3? → **jak w planie** (E6 po E5).
9. **Klawisze 1…9** dla symboli – czy kolejność wg Ie-104 (semafor, tarcza, …) czy inna? → **domyślne wg Ie-104**: semafor, tarcza, powtarzacz, PK, przerwa, wykolejnica, kozioł, przejazd, peron.

---

## Prompt dla AI

Poniższy tekst jest samodzielny – można go dać agentowi AI, który ma zbudować nowy edytor etapami.

```text
Jesteś programistą C# 14 / WPF / .NET 10 w projekcie SUK – ogólnym symulatorze LCS (pulpity EbiScreen i komputery
zależności EBILock wg instrukcji PKP PLK Ie-20 i Ie-104). Repozytorium: C:\Users\micha\Documents\symulator\SUK.
Rozmawiasz z właścicielem po polsku; wszystkie teksty w programie są po polsku, prostym językiem.

ZADANIE
Zbuduj nowy edytor obrazów pulpitu (zobrazowania EbiScreen) wg PLAN_EDYTOR_ZOBRAZOWANIA_V2.md, etapami E1–E8 (§ 10).
Rób JEDEN etap na raz. Na koniec etapu pokaż zrzut ekranu i wynik testów, zaktualizuj stan w planie i krótki wpis
w ROZWOJ.md, i zatrzymaj się na akceptację właściciela. Etap E0 (decyzje z § 11 planu) musi być zamknięty przed E1 –
jeśli w planie są pytania bez odpowiedzi, zadaj je (AskUserQuestion), nie zgaduj.

PRZECZYTAJ NAJPIERW
- CLAUDE.md, ROZWOJ.md (§ 2 zasady, § 2a wygląd pulpitu, § 4 architektura), PLAN_EDYTOR_ZOBRAZOWANIA_V2.md (całość),
  PLAN_EDYTOR_ZOBRAZOWANIA.md (§ 2–3, § 9–11 – dotychczasowe decyzje, nadal ważne, jeśli V2 ich nie zmienia),
  PLAN_EDYTOR_UT.md § 7.
- Kod: app/Lcs.EdytorZobrazowania (obecny edytor – weź zachowania, które działają: cofnij/ponów ZobDocument.Change, zapis
  z .bak, Sprawdź, Podgląd pulpitu, Do symulatora, tryb --test), app/Lcs.Model (UtProject, UtPost, UtLine, UtLineEnd,
  Zob*.cs, DoSymulatora.cs), app/LcsWidzew (PanelView – tak rysuje pulpit; symbole w edytorze rysuj tym samym kodem albo
  wspólną klasą, nie kopią).
- Wzory wyglądu: Zrodla/scren pulpitu GrM.png, Zrodla/przebieg pociagowy GrM.png, Zrodla/SimRail_LCS_Widzew_wzor.webp,
  Zrodla/ISDR_klatki/*.jpg, Zrodla/Ie-104_symbole_*.png, Zrodla/Ie-104_paleta_barw.png, KOMPUTERY.pdf s. 147 (zdjęcie 46 –
  EbiScreen Psary; PDF do PNG: dotnet run --project tools/pdfr -- Zrodla/KOMPUTERY.pdf <katalog> 28 28 2.2).

ZASADY PROJEKTU (obowiązują zawsze)
1. Obrazy projektuje człowiek. Program może tylko proponować: „Szkic na życzenie” (§ 7 planu) jest szary, na osobnej
   warstwie i wchodzi do obrazu dopiero po „Przyjmij”. Nic nie trafia do obrazu samo.
2. UT (*.ut.json) jest tylko do odczytu. Nie zapisuj UT z edytora obrazów.
3. Nie nadpisuj app/LcsWidzew/Data/station.json. Eksport idzie do Data/projekty (jak dziś DoSymulatora).
4. Nie zaszywaj w kodzie nazw stacji ani kodów posterunków (LW, LF, GrM…). Wszystko z plików.
5. Znaczniki edytora (siatka, prowadnice, strefy, ⚠, przypięcia) nigdy nie trafiają na pulpit ani do PNG.
6. Siatka 8 j., obraz 1920 × 1080 j., kierunki tylko 0° / 45° / 90°.
7. Logika i symbole zgodne z Ie-104 / Ie-20 / Zrodla/KOMPUTERY_notatki.md i ROZWOJ.md § 2a. W razie wątpliwości – pytaj
   właściciela, nie zgaduj. Nie zmieniaj ustaleń z § 2a bez zgody.
8. Stary edytor (app/Lcs.EdytorZobrazowania) zostaje i działa. Nie usuwaj go, dopóki właściciel nie zdecyduje (E8).
9. W repo pracują równolegle inne sesje: nie ruszaj cudzych zmian, commituj tylko swoje pliki i tylko na prośbę.

ARCHITEKTURA (z planu)
- Nowy projekt app/<nazwa z E0> (WPF, net10.0-windows), dopisany do app/Lcs.slnx, launcher .bat w katalogu SUK.
- Model w Lcs.Model (pliki Obr*.cs): plik *.zob.json "version": 2 – obrazy z grafem: wezly, odcinki (pkt załamań, styl),
  elementy (typ, na: odcinek/węzeł, t, kier, opisy z przesunięciem d), przypiecia oddzielnie (stan: pewne / potwierdzone /
  doWyjasnienia / brak), rzedy (prowadnice), strefy, szkic.
- Odczyt wersji 1 (obecny ZobProject) → wersja 2. Eksport: kompilacja wersji 2 + przypięć → ZobProject w pamięci →
  istniejące DoSymulatora i PodgladWindow (bez zmian w nich do E5; nowe elementy szlaków – w E6).
- Rysowanie bez UT, bez odrzucania kliknięć. Błędy i niepewności to uwagi (⚠ na płótnie + lista), nie blokady.
- Przypinanie (F8): kotwice po nazwach z opisów → tory i km między kotwicami → kandydaci po sąsiedztwie i kolejności →
  lista „do wyjaśnienia”. Przypięcia idą za elementami przy edycji.

KRYTERIA AKCEPTACJI KAŻDEGO ETAPU
- dotnet build -c Release nowego programu bez błędów i bez nowych ostrzeżeń (przy zamkniętym programie).
- Test „jak użytkownik”: <exe> --test wynik.txt zrzut.png buduje obraz KLIKAMI (symulowane zdarzenia myszy i klawiatury
  na prawdziwym oknie, jak Test*.cs w obecnym edytorze) na app/Lcs.EdytorZobrazowania/Testy/mijanka.ut.json –
  bez wpisywania danych do modelu z pominięciem interfejsu. Wynik: „błędów: 0”, zrzut zapisany i pokazany.
- Gdy etap dotyka eksportu: selftest symulatora na app/LcsWidzew/Testy/station_test.json bez zmian
  (WYNIK: nastawione 25 z 26, kontrola zależności 4 z 4) i porównanie eksportu z eksportem starego edytora (E5).
- Kryterium „gotowe, gdy” z tabeli § 10 planu spełnione i pokazane na zrzucie.
- Stan etapu wpisany w PLAN_EDYTOR_ZOBRAZOWANIA_V2.md, skrót w ROZWOJ.md, obsługa w README.md.

STYL KODU
- Pisz jak otaczający kod: polskie nazwy domenowe (Tor, Wezel, Odcinek, Przypiecie), komentarze /// po polsku z odwołaniem
  do paragrafu planu lub Ie-104, okna budowane w kodzie C# jak w obecnych edytorach.
- Logikę (graf, przypinanie, kompilacja, sprawdzanie) trzymaj w Lcs.Model bez WPF – da się ją testować bez okna.

GDY NIE WIESZ
- Symbol, kolor, kształt, znaczenie oznaczenia, zachowanie srk → zapytaj właściciela (AskUserQuestion), podając zrzut lub
  wycinek wzoru. Nie wymyślaj symboli.
```
