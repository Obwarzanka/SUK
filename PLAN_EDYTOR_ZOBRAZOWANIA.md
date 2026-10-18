# Edytor zobrazowania (etap 8) – plan do akceptacji

Projekt z 2026-10-17. **Stan: zaakceptowany przez właściciela 2026-10-17** z propozycjami z § 8 (decyzje w § 9). **Z0–Z3 zrobione 2026-10-18, Z4 i Z5 – 2026-10-19** (szczegóły w `HISTORIA_EDYTOR_UT.md`, decyzje do Z4–Z5 – § 10). Następny: Z6 – podgląd „jak na pulpicie” (`PanelView`) w edytorze.

Podstawa: decyzje właściciela z `PLAN_EDYTOR_UT.md` § 7 (2026-10-17), Ie-104 § 4 ust. 8–19, § 5, § 6, § 8–9, zasady z `ROZWOJ.md` § 2 i § 2a.

---

## 1. Założenia (z decyzji właściciela)

1. **Osobny program** `EdytorZobrazowania.exe` (`app/Lcs.EdytorZobrazowania`), wspólna biblioteka modelu `Lcs.Model`.
2. **UT tylko do odczytu.** Edytor zobrazowania niczego nie zmienia w `*.ut.json`. Tory, rozjazdy, urządzenia, odcinki i ich nazwy
   pochodzą z UT. Brak czegoś w UT (np. numeru zwrotnicy) poprawia się w edytorze UT.
3. **Każdy element obrazu jest przypięty do obiektu UT** – przez jego stały identyfikator (`Id` w UT: tory, rozjazdy, skrzyżowania,
   urządzenia, odcinki, perony, przejazdy mają 8-znakowe `Id`). Wyjątek: napisy i ramki bez znaczenia ruchowego (np. „Peron 3”, tytuł).
4. **Rysuje człowiek.** Program nie układa obrazu sam i nie proponuje rysunku z UT. Może tylko: pokazać, czego jeszcze nie ma
   na obrazie, przyciągać do siatki i do narysowanych linii, sprawdzać zgodność obrazu z UT („Sprawdź”).
5. **Skosy i symbole jak w poprzedniej wersji:** siatka 8, tor poziomo, pionowo i pod 45°, łącznik rozjazdowy „ukos 45° – pion –
   ukos 45°”, symbole takie, jak rysuje pulpit (`PanelView`) – zgodne z Ie-104 / Ie-20 i wzorami SimRail / EbiScreen (`ROZWOJ.md` § 2a).
   Z kodu starego edytora (commit `d2c7431`) bierzemy tylko zachowanie narzędzi, nie kod (zasada z `CLAUDE.md`).
6. **Znaczniki edytora** (siatka, przypięcia, numery uwag) są tylko w edytorze, nigdy na pulpicie.

## 2. Plik `*.zob.json`

Osobny plik obok projektu UT (np. `widzew.ut.json` → `widzew.zob.json`), poprzednia wersja w `.bak`, jak w edytorze UT.
Jeden plik zawiera wszystkie obrazy obszaru (szczegółowe i poglądowe).

```json
{
  "version": 1,
  "ut": "widzew.ut.json",                 // ścieżka względna do projektu UT
  "utZapis": "2026-10-17T15:20:00",      // czas zapisu UT, z którym obraz był ostatnio zgodny (ostrzeżenie przy zmianie)
  "siatka": 8,                            // krok przyciągania [j.]
  "obrazy": [
    {
      "id": "o1", "nazwa": "Łódź Widzew", "rodzaj": "szczegolowy",   // albo „pogladowy”
      "posterunki": ["LW"],              // posterunki z UT pokazane na obrazie (Ie-104 § 5 ust. 1)
      "szer": 1920, "wys": 1080,         // wymiary obrazu [j.] – 1 j. = 1 piksel monitora przy powiększeniu 1
      "tory": [
        { "id": "t1", "ut": { "tor": "a1b2c3d4", "s0": 0.0, "s1": 412.5 }, "pkt": [[96, 520], [480, 520], [528, 472]] }
      ],
      "zwrotnice":     [ { "ut": "e5f6a7b8", "p": [480, 520], "opis": [470, 506] } ],
      "sygnalizatory": [ { "ut": "c9d0e1f2", "p": [432, 520], "strona": -1, "opis": [432, 504] } ],
      "wykolejnice":   [ { "ut": "...", "p": [...] } ],
      "kozly":         [ { "ut": "...", "p": [...] } ],
      "przerwy":       [ { "ut": "id licznika osi / złącza", "tor": "t1", "p": [304, 520] } ],
      "odcinki":       [ { "ut": "id odcinka", "opis": [200, 536] } ],
      "numeryTorow":   [ { "ut": "id toru UT", "p": [640, 520] } ],
      "perony":        [ { "ut": "id peronu", "prost": [560, 532, 160, 24] } ],
      "przejazdy":     [ { "ut": "id przejazdu", "p": [...], "opis": [...] } ],
      "szlaki":        [ { "ut": "id szlaku", "p": [...], "opis": [...] } ],
      "napisy":        [ { "tekst": "ŁÓDŹ WIDZEW", "p": [960, 40], "rodzaj": "posterunek" } ]
    }
  ]
}
```

- **Tor obrazu = kawałek toru UT** (tor + kilometraż od–do) narysowany łamaną. Długości w UT i na obrazie nie muszą się zgadzać –
  to schemat. Jeden tor UT może być na obrazie kilkoma kawałkami (np. przerwany na granicy obrazów), a jeden kawałek nie może łączyć
  dwóch torów UT (przejście między torami UT jest zawsze w węźle: rozjazd, styk, skrzyżowanie).
- Zwrotnica, sygnalizator, wykolejnica, kozioł, przerwa izolacyjna – punkt na linii toru obrazu, przypięty do obiektu UT. Kierunek
  symbolu (w lewo / w prawo na obrazie) wynika z kierunku działania w UT i z tego, w którą stronę biegnie łamana – nie wpisuje się go.
- Współrzędne obrazu w jednostkach `j.` (całkowite, krok siatki 8), niezależne od metrów UT.

## 3. Przypięcie do UT i zgodność

| Element obrazu | Obiekt UT | Co bierze z UT | Co ustala człowiek |
|---|---|---|---|
| tor obrazu | `UtTrack` + km od–do | numer toru, kategoria (gz / gd / boczny), elektryfikacja | łamana |
| zwrotnica | `UtTurnout` / `UtCrossing` | numer, położenie zasadnicze „+”, która gałąź jest „+” / „−”, ręczna | miejsce, opis |
| sygnalizator | `UtDevice` (semafor, tarcza manewrowa, ostrzegawcza, powtarzacz, PK) | nazwa, kierunek działania, funkcja semafora* | miejsce, strona opisu |
| wykolejnica, kozioł | `UtDevice` | nazwa | miejsce |
| przerwa izolacyjna | licznik osi / złącze (`UtDevice`) | granica odcinków | miejsce na linii |
| opis odcinka | `UtSection` | nazwa, rodzaj kontroli (LO / OT) | miejsce opisu |
| peron, przejazd | `UtPlatform`, `UtLevelCrossing` | nazwa, km, rodzaj | prostokąt / miejsce |
| szlak, strzałka blokady | szlak* | numer szlaku, oznaczenia końców | miejsce strzałki i opisu |
| napis | – | – | tekst, miejsce |

\* do dodania w UT (§ 6).

**„Sprawdź” (zgodność z UT i z Ie-104):**
- przypięcie do obiektu, którego nie ma w UT (usunięty, rozcięty tor) – z przyciskiem „Przypnij ponownie”;
- obiekty UT posterunków obrazu, których nie ma na obrazie (lista „Nieumieszczone” – jak w starym edytorze);
- kolejność wzdłuż toru: zwrotnice, sygnalizatory i przerwy na torze obrazu w tej samej kolejności co kilometraż w UT;
- zwrotnica: do jej punktu dochodzą tory obrazu, które w UT są jej ostrzem, gałęzią „+” i gałęzią „−”;
- kawałki torów obrazu nie zachodzą na siebie (ten sam km toru UT dwa razy na jednym obrazie);
- Ie-104 § 4 ust. 15: odstęp torów ≥ 2 wysokości opisu (obraz poglądowy ≥ 1), symbol urządzenia sąsiaduje w poziomie tylko z odcinkiem
  (np. tarcza manewrowa obok wykolejnicy – uwaga), strzałka blokady nie obok semafora wjazdowego, opis jednoznacznie przy symbolu;
- obraz poglądowy: tylko symbole z § 6 ust. 4 (odcinki, rozjazdy, sygnalizatory pociągowe, blokady, przejazdy, bocznice, numery torów).

## 4. Okno i narzędzia

- **Płótno** w jednostkach obrazu: czarne tło jak na pulpicie, siatka 8 (co 5 – grubsza), przybliżenie, przesuwanie. Zakładki
  obrazów (jak na pulpicie: obraz poglądowy i szczegółowe). Cofnij / ponów, zapis, „zapisz jako”.
- **Panel UT** (z lewej): drzewo obiektów UT posterunków obrazu – tory, rozjazdy, sygnalizatory, wykolejnice, kozły, odcinki, perony,
  przejazdy, szlaki – z oznaczeniem „na obrazie” / „brak”. Klik na pozycji podświetla miejsce na obrazie; przeciągnięcie na płótno
  zaczyna rysowanie tego obiektu.
- **Podgląd UT** (małe okno albo dolny pasek): schemat UT z zaznaczonym obiektem – żeby było widać, który kawałek toru się rysuje.
- **Narzędzia** (klawisze jak w starym edytorze):
  - **Tor (T):** wybór kawałka toru UT (z panelu albo klik na węźle obrazu – lista torów UT wychodzących z tego węzła, których jeszcze
    nie ma), potem punkty łamanej. Shift – poziomo, Ctrl – 45°, przyciąganie do siatki i do węzłów obrazu. Koniec: klik na węźle,
    PPM, Enter, dwuklik. Tor kończy się zawsze w węźle UT (rozjazd, koniec toru, styk) albo na krawędzi obrazu.
  - **Łącznik (R):** dwie zwrotnice łącznika UT – „ukos 45° – pion – ukos 45°” między dwoma narysowanymi torami.
  - **Zwrotnica (Z), Sygnalizator (S), Wykolejnica (W), Kozioł (K), Przerwa (P):** wybór obiektu UT, klik na linii toru obrazu.
    Symbol siada na linii, opis domyślnie po stronie jak na pulpicie, można go przeciągnąć.
  - **Opis odcinka (O), Numer toru (N), Peron, Przejazd, Szlak (B), Napis (X).**
  - **Wskaż:** zaznaczanie (klik, Shift + prostokąt), przesuwanie myszą i strzałkami o krok siatki, Del – zdjęcie z obrazu
    (obiekt w UT zostaje), panel właściwości: dane z UT (tylko do odczytu) i „Przypnij do innego obiektu UT”.
  - **Miarka (M)** w jednostkach obrazu.
- **Podgląd „jak na pulpicie”:** obraz rysowany kodem pulpitu (`PanelView`) ze stanem zasadniczym (szary), a także przykładowym
  przebiegiem i zajętością – żeby ocenić obraz przed użyciem w symulatorze (§ 5, etap Z6).

## 5. Etapy

| Etap | Zakres | Gotowe, gdy |
|---|---|---|
| Z0 | W UT: funkcja semafora, posterunki, szlaki (§ 6). | edytor UT zapisuje i pokazuje je; test UT bez błędów |
| Z1 | Szkielet: projekt `Lcs.EdytorZobrazowania`, model `Zob*` w `Lcs.Model`, plik `*.zob.json`, płótno z siatką, zakładki obrazów, wczytanie UT, panel UT, cofnij / zapis. | pusty obraz do mijanki z UT zapisuje się i wczytuje; zrzut |
| Z2 | Tor (łamana przypięta do kawałka toru UT), węzły, łącznik ukos–pion–ukos, przyciąganie. | mijanka: wszystkie tory UT narysowane klikami (test „jak użytkownik”) |
| Z3 | Zwrotnice, sygnalizatory, wykolejnice, kozły – symbole z `PanelView`, kierunek z UT. | mijanka z semaforami i zwrotnicami; „Sprawdź” – brak nieumieszczonych |
| Z4 | Przerwy izolacyjne, opisy odcinków, numery torów, perony, przejazdy, szlaki i strzałki blokady, napisy. | obraz mijanki kompletny wg Ie-104 § 5 ust. 2 |
| Z5 | Kilka obrazów, obraz poglądowy, „Sprawdź” (§ 3), wydruk / PNG. | dwa posterunki na dwóch obrazach + poglądowy; „Sprawdź” bez uwag |
| Z6 | Podgląd „jak na pulpicie” (`PanelView`) – wymaga przekształcenia UT + obraz → model symulatora (początek etapu 9). | obraz z edytora wygląda tak samo jak na pulpicie symulatora |

Po każdym etapie: build, test edytora „jak użytkownik” (`EdytorZobrazowania.exe --test wynik.txt zrzut.png`, `błędów: 0`), zrzut,
wpis w historii i skrót w `ROZWOJ.md`, jak przy edytorze UT.

**Uwaga:** symulator czyta `station.json`. Od 2026-10-18 „Do symulatora…” przekształca UT + obraz → `station.json` w `Data/projekty`
(`Lcs.Model/DoSymulatora.cs`, początek etapu 9) – obraz z edytora jest na pulpicie symulatora, od 2026-10-19 także z elementami Z4.

## 6. Co trzeba dodać w UT przed rysowaniem (etap Z0)

1. **Funkcja semafora** (decyzja § 7 pkt 4): wjazdowy, wyjazdowy, drogowskazowy, odstępowy, powtarzacz, PK – pole w panelu urządzenia.
   Bez niej nie wiadomo, który symbol pulpitu narysować (np. trójkąt końca przebiegu wyjazdowego przy wjazdowym).
2. **Posterunki** (decyzja § 7 pkt 5): obszary w UT, tory przypisane do posterunku, urządzenia dziedziczą posterunek z toru.
   Obraz szczegółowy pokazuje posterunki, a „Nieumieszczone” liczy się dla nich.
3. **Szlaki i blokady liniowe** (propozycja – pytanie 3): szlak = tory szlakowe UT od granicy posterunku, numer szlaku, typ blokady
   (SBL 2- / 3- / 4-stawna, półsamoczynna, telefoniczna), posterunek sąsiedni, oznaczenia końców (np. 1A / 1G), odstępy
   (z semaforów odstępowych UT). Bez tego na obrazie nie ma czego przypiąć do strzałki blokady.

## 7. Czego plan nie obejmuje

- Przebiegów i tablicy zależności (etap 3 w `ROZWOJ.md`, projektuje człowiek – osobny edytor albo część etapu 9).
- Przekształcenia UT + obraz → model symulatora (etap 9). Etap Z6 robi tylko tyle, ile trzeba do podglądu.
- Przenoszenia istniejącego zobrazowania ze `station.json` (Widzew, Fabryczna) – pytanie 7.

## 8. Pytania do właściciela

1. **Plik:** osobny `*.zob.json` obok UT, wszystkie obrazy obszaru w jednym pliku? (proponuję tak)
2. **Wymiary obrazu:** 1920 × 1080 j. (1 j. = 1 piksel przy powiększeniu 1), siatka 8 – czy inny rozmiar (np. dwa monitory obok siebie)?
3. **Szlaki i blokady** definiujemy w UT (§ 6 pkt 3), a obraz tylko rysuje strzałkę i opis? (proponuję tak – to dane srk, nie rysunek)
4. **Przerwy izolacyjne:** stawia człowiek na linii toru (program sprawdza kolejność z UT) czy program wstawia je sam w miejscu
   wynikającym z kilometrażu UT, a człowiek tylko przesuwa? (proponuję: stawia człowiek – zgodnie z zasadą „rysuje człowiek”)
5. **Obraz poglądowy:** rysowany osobno w tym edytorze (zalecam – Ie-104 § 6: inny poziom szczegółowości) czy jak dziś –
   pomniejszone obrazy szczegółowe?
6. **Ten sam obiekt UT na dwóch obrazach** (np. wspólna głowica, szlak wewnętrzny między posterunkami) – dozwolony? Sterowanie
   z obu obrazów czy tylko z obrazu posterunku, do którego należy?
7. **Istniejące zobrazowanie Widzew / Fabryczna** w `station.json`: rysujesz od nowa w nowym edytorze, czy coś przenosimy
   (np. tylko jako podkład do odrysowania)?
8. **Kolejność:** najpierw Z0 (funkcja semafora, posterunki, szlaki w UT), potem Z1–Z6?

## 9. Decyzje właściciela (2026-10-17)

Plan zaakceptowany z propozycjami z § 8:
1. Osobny plik `*.zob.json` obok UT, wszystkie obrazy obszaru w jednym pliku.
2. Obraz 1920 × 1080 j., siatka 8.
3. Szlaki i blokady liniowe definiowane w UT; obraz rysuje tylko strzałkę i opis.
4. Przerwy izolacyjne stawia człowiek na linii toru, program sprawdza kolejność z UT.
5. Obraz poglądowy rysowany osobno w edytorze zobrazowania.
6. Ten sam obiekt UT może być na dwóch obrazach; sterowanie – według obszaru sterowania posterunku, do którego należy.
7. Zobrazowanie Widzew / Fabryczna ze `station.json` rysowane od nowa (można je pokazać jako podkład do odrysowania).
8. Kolejność: Z0 (funkcja semafora, posterunki, szlaki w UT), potem Z1–Z6.

## 10. Decyzje właściciela do etapu Z4 (2026-10-19)

1. **Numery torów i strzałki blokady** stawia człowiek w edytorze zobrazowania; pulpit bierze ich miejsce z obrazu. Gdzie nie są postawione – pulpit liczy jak dotąd (numer na środku najdłuższego kawałka toru odcinka, strzałka za semaforem wjazdowym).
2. **Opisy odcinków** – „do dostosowania w edytorze”: miejsce opisu każdego odcinka ustawia się w edytorze (domyślnie nad torem, Ie-104 § 8 ust. 1 pkt 3), a w obrazie jest ustawienie, czy opisy idą na pulpit (na żądanie wg Ie-104 § 5 ust. 2, zawsze, nie). *Odczytanie do potwierdzenia przez właściciela.*
3. **Tor bez kontroli niezajętości** (linia przerywana, Ie-104 § 8 ust. 1) oznacza człowiek – pole w panelu toru obrazu; nie wynika samo z odcinków UT.
4. **Obraz poglądowy na pulpicie – jak dotąd** (2026-10-19): zakładka „Obraz poglądowy” w symulatorze pokazuje pomniejszone obrazy szczegółowe; obraz poglądowy z edytora rysuje się i sprawdza w edytorze, „Do symulatora…” go nie przenosi.
5. **Ten sam obiekt UT na dwóch obrazach szczegółowych – na pulpicie na obu** (2026-10-19): pulpit rysuje obiekt na każdym obrazie, na którym go narysowano; sterowanie wg obszaru sterowania posterunku (§ 9 pkt 6).

