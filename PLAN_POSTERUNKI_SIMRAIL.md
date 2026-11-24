# Plan: posterunki SimRail – tworzenie, przypisywanie elementów w edytorze, konwerter

Stan na 2026-11-24. Plan do akceptacji właściciela – nic z niego nie jest jeszcze zrobione.

## 1. Jak jest dziś

Łańcuch (narzędzia w `SimRailMost\narzedzia`, dane w `UserData\SimRailMost\suk`):

```
gra → mod: pulpit_<Posterunek>_<ekran>_<data>.json, obiekty_serwer_*.csv
  → pulpit_do_zob.py  → suk\edytor\<st>.zob.json  (rysunek do EdytorZobrazowania2)
                      → suk\edytor\<st>.simrail.json (mapa: obraz → id elementu/odcinka → railNetworkName, numery, geom, blokady)
                      → suk\edytor\<st>.ut.json (pusty UT)
  → [poprawki człowieka w EdytorZobrazowania2]
  → zob_do_suk.py     → suk\<st>_zob.json (pulpit SUK) + .simrail.json (ID gry → SUK, pole „posterunki”)
                      + .nieprzypisane.txt, .przebiegi_gry.json
  → LcsWidzew --station suk\<st>_zob.json (pulpit zdalny SimRail)
```

Posterunki zrobione: Koluszki (KO), Bełchów (BE), Płyćwia (PL), Rogów (RG). Zrzuty bez układu: Łazy (i Łazy Ła – pulpit domino).

Słabe miejsca:

1. **Tworzenie posterunku** – osobne skrypty na każdą stację (`konwertuj_Koluszki.cmd`, `konwertuj_Belchow_Plycwia_Rogow.cmd`,
   `uruchom_SUK_<st>.cmd`), kod posterunku zgadywany z nazwy, brak spisu posterunków. W `.ut.json` lista `posts` jest pusta,
   a w `.zob.json` obraz ma `posterunki: []` – edytor nie wie, czyj to obraz. Jeden układ = jeden posterunek gry
   (nie ma układu na kilka posterunków, np. Łazy + Łazy Ła).
2. **Przypisywanie elementów** – edytor nie zna ID gry (mapa jest obok i edytor jej „nie zna i nie zmienia”). Przypisanie
   odtwarza dopiero konwerter, heurystyką (ID z mapy → numer z opisu → położenie → sąsiedzi). Wynik widać dopiero
   w `nieprzypisane.txt` po konwersji (Koluszki 20 pozycji, Rogów 9, Bełchów 1, Płyćwia 1); nie da się w edytorze wskazać
   „ten odcinek = t40032” ani „bez obiektu gry”.
3. **Odświeżenie z gry** – gdy `.zob.json` istnieje, konwerter pisze obok `<st>_z_gry_nowy.zob.json`; zmian z gry
   (nowe/usunięte obiekty) nie da się nanieść na poprawiony rysunek inaczej niż ręcznie.
4. **Łączność** – posterunek w `%LocalAppData%\SUK\Lacznosc\ustawienia.json` (`Stacja: Koluszki`); na innych stacjach tylko
   „(podgląd)”.

Zasady, których plan się trzyma (CLAUDE.md, ustalenia z sesji):

- Bez nazw stacji i kodów w kodzie – wszystko z plików.
- Obrazy i przypisania projektuje człowiek: automat tylko **proponuje** (stany jak przy F8: pewne / do wyjaśnienia / brak),
  człowiek potwierdza.
- Odcinek pulpitu = element pulpitu gry (kawałek `t…` z własnym stanem), nie obwód `iz` (`tory_z_wlasnym_stanem()`).
- Przed stwierdzeniem błędu przypisania – sprawdzić geometrię w zrzucie pulpitu gry.

## 2. Etapy

### P1. Spis posterunków (rejestr) – konwerter

Plik `suk\posterunki.json` (dane, nie kod):

```json
{ "posterunki": [
  { "plik": "rogow", "nazwaGry": "Rogów", "kod": "RG", "uklad": "rogow",
    "ekrany": ["Nowy"], "sasiedzi": ["Płyćwia", "Koluszki"] },
  { "plik": "lazy", "nazwaGry": "Łazy", "kod": "LZ", "uklad": "lazy" },
  { "plik": "lazy_la", "nazwaGry": "Łazy Ła", "kod": "LA", "uklad": "lazy", "pulpit": "domino" }
] }
```

- `uklad` – posterunki z tym samym układem idą do jednego pulpitu SUK (pole `posterunki` w mapie dostaje oba).
- Kod: przy pierwszym utworzeniu propozycja z nazwy, potem tylko z rejestru (nie zmienia się przy odświeżeniu – od niego
  zależą id odcinków `KO:…` i dzienniki EDR).
- Odczyt dotychczasowych map → rejestr wypełniony raz dla 4 istniejących stacji (bez zmiany kodów).

Gotowe, gdy: rejestr istnieje, `konwertuj_posterunki.py` bierze kod i nazwę z niego, mapy 4 stacji bez różnic (`cmp`).

### P2. Jedno polecenie zamiast skryptów na stację – konwerter

`posterunek.cmd` (→ `narzedzia/posterunek.py`):

| polecenie | co robi |
|---|---|
| `posterunek nowy "Łazy"` | najnowszy zrzut pulpitu → wpis w rejestrze, `.ut.json` z **wpisem w `posts`** (kod, nazwa), `.zob.json` z `posterunki: [id]` na obrazach, mapa, pulpit SUK |
| `posterunek odswiez "Rogów"` | nowy zrzut → porównanie z mapą (P4), raport zmian; rysunku nie rusza |
| `posterunek uruchom rogow` | `zob_do_suk.py` + LcsWidzew (zastępuje `uruchom_SUK_<st>.cmd`) |
| `posterunek lista` | rejestr + stan: data zrzutu, liczba nieprzypisanych, czy pulpit SUK aktualny |

Stare `.cmd` zostają jako jednolinijkowe wywołania (nie psujemy przyzwyczajeń), opis w README.

Gotowe, gdy: Łazy zrobione od zera jednym poleceniem; Bełchów/Płyćwia/Rogów/Koluszki przez `uruchom` dają te same pliki co dziś.

### P3. Przypisywanie elementów w edytorze – EdytorZobrazowania2

Cel: przypisanie do obiektu gry widać i poprawia się **w edytorze**, nie dopiero w `nieprzypisane.txt`.

Decyzja do podjęcia (rekomendacja – **A**):

- **A. Mapa zostaje osobnym plikiem** (`<st>.simrail.json`), edytor ją czyta i zapisuje. Model zobrazowania zostaje ogólny
  (SimRail to jedno ze źródeł), Lcs.Model dostaje tylko czytnik/zapis mapy (`ObrGra.cs`, bez WPF).
- B. Pole `gra` w elemencie `.zob.json` – prościej, ale wiąże format zobrazowania z SimRail.

Co w edytorze (wzorem F8 / „Do wyjaśnienia”):

1. Zakładka **Gra** (obok UT): wczytuje mapę i najnowszy `obiekty_serwer_*.csv` / zrzut pulpitu z katalogu danych.
2. Przy każdym elemencie i odcinku stan: *z mapy* / *po numerze* / *po położeniu* / *po sąsiadach* / *do wyjaśnienia* /
   *bez obiektu gry* (świadomie) / *brak*. Na płótnie ⚠ przy „brak” i „do wyjaśnienia”, kolor odcinka wg stanu (przełącznik).
3. **Przypnij do gry (F9)** – te same reguły co dziś w `zob_do_suk.py` (jedno źródło reguł – patrz niżej), wynik jako
   propozycje; człowiek potwierdza, potwierdzone są kotwicami przy kolejnym F9.
4. Wybór ręczny: kandydaci po kolei (ten sam rodzaj, najbliżej położeniem, jeszcze nieużyci), z ID i nazwą gry
   (np. „t40032 · l1_kol_it102a”), podświetlenie elementu na podglądzie pulpitu gry (z geometrii zrzutu).
5. **Nieumieszczone z gry**: obiekty gry posterunku, których nie ma na rysunku (np. obwody `iz`, okna bl, przejazdy – z opisem,
   że są celowo poza mapą).
6. Sprawdź (F7) dostaje uwagi: ten sam obiekt gry na dwóch elementach jednego obrazu, element sterowalny bez obiektu gry,
   tor przepięty na obwód mimo własnego stanu.

Jedno źródło reguł: na start edytor uruchamia `zob_do_suk.py --tylko-przypiecia` (wynik: `<st>.przypiecia_gry.json`
z propozycjami i kandydatami) i tylko pokazuje/zapisuje wybory. Przeniesienie reguł do C# (`ObrGra.cs`) – osobny etap
po sprawdzeniu w grze, żeby nie mieć dwóch wersji heurystyki naraz.

Gotowe, gdy: na Rogowie 9 krótkich odcinków z `nieprzypisane.txt` widać w edytorze, każdy da się przypiąć albo oznaczyć
„bez obiektu gry”, po zapisie i konwersji `nieprzypisane.txt` jest pusty; `--test` edytora bez błędów (nowy test na małym
zrzucie pulpitu), mapy pozostałych stacji bez różnic.

### P4. Odświeżenie z gry bez utraty poprawek – konwerter

`posterunek odswiez` porównuje nowy zrzut z mapą po `railNetworkName`:

- **bez zmian** – nic;
- **nowy obiekt gry** – na liście „Nieumieszczone z gry” w edytorze (P3), z propozycją miejsca z geometrii zrzutu;
- **zniknął z gry** – element w edytorze dostaje „obiekt usunięty z gry” (błąd w Sprawdź), nic nie jest kasowane;
- **zmienione położenie / nazwa „tX;obwód”** – uwaga, bez automatycznej zmiany.

`<st>_z_gry_nowy.zob.json` przestaje być potrzebny (zostaje opcją `--pelny` do porównania na oko).

Gotowe, gdy: odświeżenie Koluszek z nowego zrzutu nie zmienia rysunku, a raport zgadza się z różnicą zrzutów.

### P5. Kilka posterunków w jednym układzie i Łączność – SUK

1. Mapa `posterunki: ["Łazy", "Łazy Ła"]` z rejestru; `SimRailSession` już obsługuje listę (`ObejmujeKtorys`) – sprawdzić
   na odtworzeniu (`kontrola_2026-11-23/odtworz.sh`) z danymi Łazów.
2. Łączność: domyślnie „(z gry)”; posterunek z mapy uruchomionego układu zamiast ręcznego `Stacja` (ustawienie zostaje
   jako nadpisanie). Zmiany w `ustawienia.json` użytkownika – tylko za zgodą.
3. Pulpit domino (Łazy Ła) – polecenia idą przez `DominoPanelButtonRenderer.SendOrder`; w rejestrze `pulpit: domino`,
   konwerter ma rysunek z innego źródła niż EBI – **osobne rozpoznanie** przed P5.3 (jaki zrzut daje mod dla domina).

### P6. Sprawdzenie

- Bez gry: odtworzenie migawek (atrapa moda) dla każdego posterunku z rejestru – zajętości i stany zgodne, brak „Alarm”
  dla własnych posterunków.
- W grze: po jednym przebiegu i zajętości na każdym posterunku; zgłoszenia jak „13–C102” sprawdzać z `zajetosci_serwer_*.csv`.
- Dokumentacja: README (obsługa `posterunek.cmd`, zakładka Gra), `PLAN_EDYTOR_ZOBRAZOWANIA_V2.md` (etap P3), ROZWOJ.md (skrót).

## 3. Kolejność i zależności

```
P1 rejestr ─► P2 jedno polecenie ─► P4 odświeżenie
      └──────► P3 edytor (zakładka Gra, F9) ──► P5 kilka posterunków / Łączność ─► P6
```

P1–P2 są małe (Python, bez zmian w SUK). P3 to największy etap (edytor + test). P5.3 (domino) wymaga rozpoznania.

## 4. Do decyzji właściciela

1. Mapa gry osobno (A) czy w `.zob.json` (B)? Rekomendacja: A.
2. Kody posterunków – zostają KO / BE / PL / RG? Kody dla Łazów i Łazów Ła.
3. Czy Łazy i Łazy Ła mają być jednym układem SUK (jeden pulpit), czy dwoma.
4. Czy przenosić reguły przypisania do C# od razu (P3), czy po sprawdzeniu w grze (rekomendacja: później).
5. Uwaga równoległych sesji: `zob_do_suk.py` i `pulpit_do_zob.py` edytują też inne sesje – przed P1/P4 uzgodnić, kto je zmienia.
