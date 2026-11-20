# Propozycja numeracji ŁW – do akceptacji

Kopia: `Nowy obszar – odzyskany – propozycja numeracji.ut.json` (oryginał `Nowy obszar – odzyskany.ut.json` bez zmian).
Źródła: plan schematyczny Semaforka `Zrodla/Semaforek_Lodz_Widzew.png` (01.2017) i OpenRailwayMap / OSM (`ref` zwrotnic, wykolejnic, numery torów).
Zakres: tylko posterunek **ŁW**. ŁF i ŁW R3 pzs bez zmian. Twoje numery zostały, poza miejscami, gdzie różniły się od Semaforka i ORM (tabela 1).

„Sprawdź” (ta sama kontrola co przycisk w edytorze, `Kontrola.Run`): **49 błędów, 424 uwagi – tyle samo co w oryginale**. Wszystkie są wcześniejsze (geometria łuków i wstawek). Zmieniły się tylko numery w ich treści (np. „wstawka 34” to teraz „wstawka 60”). Nowych uwag nie ma.

Zrzuty (widok Schemat): `zrzuty/propozycja_numeracji_LW/1_…png` – `6_…png`, `3b_…png`.

Nazwy torów w tabelach są wg Semaforka. Numery torów ŁW nadano 2026-11-02 (lista w „propozycja zobrazowania ŁW.md”). Kilometraż jest tylko na torze 1 (l. 17), dlatego miejsce podaję w PUWG 1992.
Kierunek: „→ wsch.” = jazda w stronę Andrzejowa / Olechowa, „← zach.” = jazda w stronę Fabrycznej / Chojen.

## 1. Rozjazdy – zmienione numery

| było | jest | tor | X / Y | skąd |
|---|---|---|---|---|
| 82 | **56** | 1 | 432954 / 538228 | Semaforek, ORM |
| 81 | **57** | 3 | 432923 / 538337 | Semaforek, ORM |
| 80 | **58** | 3 | 432920 / 538349 | Semaforek, ORM |
| 79 | **59** | 1 | 432901 / 538460 | Semaforek, ORM |
| 34 | **60** | 3 | 432902 / 538428 | Semaforek, ORM |
| 33 | **61** | 5 | 432879 / 538510 | Semaforek, ORM |
| 78 | **62** | 1 | 432897 / 538474 | Semaforek, ORM |
| 77 | **63** | 2 | 432881 / 538566 | Semaforek, ORM |
| 56 | **64** | 2 | 432872 / 538603 | Semaforek, ORM |
| 57 | **65** | 204 | 432858 / 538689 | Semaforek, ORM |
| 84 | **44** | 7 (→ 7b) | 433011 / 537900 | Semaforek, ORM |
| 36 | **46** | 5 (→ 7) | 432998 / 537993 | Semaforek, ORM |
| 87 | **24** | 201 (→ TVU) | 433246 / 537104 | Semaforek, ORM |
| 24 | **101** | 201 | 433232 / 537222 | Semaforek, ORM – rozjazd nastawni CS |
| 86 | **103** | 23 | 433223 / 537269 | Semaforek, ORM – rozjazd nastawni CS |

Bez zmian (zgodne z Semaforkiem i ORM): 6–16, 19–23, 25–28, 42, 43, 45, 47–54, skrzyżowania 17, 18; ŁW R3 pzs 1–5.

Do Twojej decyzji (nie zmieniałem):
- **29** (tor 104 → 102, przy N102/N104) – na Semaforku i w ORM ma numer **28**, tak samo jak rozjazd na torze 7 do sekcji eksploatacji. W jednym posterunku nie może być dwóch „28”, więc zostawiłem 29.
- **101, 103** i skrzyżowanie **90** należą do nastawni CS (zaplecze ŁKA, poza LCS). Może lepiej przenieść te tory do osobnego posterunku „CS”.
- Rozjazdy na torach bez posterunku: 1, 2, 61, 62 zgadzają się z ORM. **92** ma w ORM numer **319**, a dla **91** ORM nie ma numeru.
- ŁF: ORM ma inaczej 27 → 26 i 37 → 35. Ten numer jest już zajęty w ŁF, więc zostawiłem Twoje.

## 2. Semafory

Zmienione nazwy (wjazdowe od Andrzejowa / Olechowa, bramka – miejsce bez zmian):

| było | jest | tor | X / Y | kierunek |
|---|---|---|---|---|
| B | **W** | 2 (l. 17) | 432793 / 538906 | ← zach. |
| F | **V** | 1 (l. 17) | 432789 / 538905 | ← zach. |
| J | **U** | 3 (l. 541) | 432783 / 538903 | ← zach. |
| K | **T** | 5 (l. 541) | 432780 / 538902 | ← zach. |

Nowe (funkcja „wyjazdowy”, komory domyślne – do uzupełnienia):

| nazwa | tor | X / Y | kierunek | miejsce |
|---|---|---|---|---|
| **R8** | 8 | 433060 / 537892 | → wsch. | przed 43 |
| **R6** | 6 | 433057 / 537891 | → wsch. | 50 m przed 43 |
| **R4** | 4 | 433050 / 537894 | → wsch. | przed 45, w linii z R6/R8 |
| **P102** | 102 | 433110 / 537863 | → wsch. | przed 42 |
| **P104** | 104 | 433096 / 537893 | → wsch. | 40 m przed 42 |

Bez zmian (zgodne z Semaforkiem): D, E, G, L1, L2, L3, L5, L7, M4, M6, M8, N102, N104, S1, S2, S3, S5, S7; ŁW R3 pzs B1, B101, B102, C1, C2.

Uwagi:
- **A** (l. 16, km 0,800), **H** (l. 540) i **B2** stoją na torach bez posterunku. Nazwy są dobre, ale te tory trzeba przypisać do ŁW (A, H) i ŁW R3 pzs (B2).
- Nie wstawiałem powtarzaczy ISpS3, ISpS7, I–IIISpG/H ani tarcz ostrzegawczych ToA, ToG, ToH, ToU, ToT. Są na szlaku albo poza narysowanymi torami.

## 3. Tarcze manewrowe (nowe; Tm10, Tm17 – Twoje, bez zmian)

Miejsca są przybliżone. Wziąłem je z położenia na Semaforku względem rozjazdu (plan nie jest w skali), po 15–70 m od rozjazdu. Wszystkie są na maszcie. Na Semaforku część wygląda na karzełkowe – do sprawdzenia.

| nazwa | tor | X / Y | kierunek | przy rozjeździe |
|---|---|---|---|---|
| Tm1 | 5 (pas od H) | 433346 / 536447 | → wsch. | przed 6 |
| Tm2 | pas od G (→ 3) | 433343 / 536490 | → wsch. | przed 7 |
| Tm3 | 5 (pas od H) | 433333 / 536523 | ← zach. | za 6 |
| Tm4 | 1 (km 4,407) | 433318 / 536648 | → wsch. | przed 9 |
| Tm5 | pas od G | 433311 / 536652 | ← zach. | za 8 |
| Tm6 | 2 | 433302 / 536738 | → wsch. | przed 11 |
| Tm7 | 2 | 433260 / 536920 | ← zach. | za 14 |
| Tm8 | 4 | 433263 / 536972 | → wsch. | przed skrzyżowaniem 17 |
| Tm9 | 3 | 433234 / 536986 | → wsch. | przed 16 |
| Tm11 | 1 (km 4,738) | 433244 / 536970 | ← zach. | za 15 |
| Tm12 | 5 | 433207 / 537083 | → wsch. | przed 20 |
| Tm13 | 3 | 433209 / 537095 | ← zach. | za 19 |
| Tm14 | 2 | 433186 / 537239 | → wsch. | przed 26 |
| Tm15 | 201 | 433237 / 537174 | ← zach. | za Wk24, w stronę 24 |
| Tm16 | łącznik 24 → TVU | 433244 / 537124 | ← zach. | za 24 |
| Tm18 | 104 | 433255 / 537404 | → wsch. | przed 29 (28 wg Semaforka) |
| Tm21 | 21 (CS) | 433027 / 537999 | → wsch. | przed 47 |
| Tm22 | 204 | 432991 / 538105 | ← zach. | za Wk49 |
| Tm23 | 2 | 432969 / 538181 | ← zach. | za 51 |
| Tm24 | 5 | 432955 / 538182 | ← zach. | za 52 |
| Tm28 | 5 | 432887 / 538475 | → wsch. | przed 61 |
| Tm29 | 2 | 432892 / 538514 | → wsch. | przed 63 |
| Tm30 | 3 | 432888 / 538490 | ← zach. | za 60 |
| Tm31 | 5 | 432875 / 538530 | ← zach. | za 61 |
| Tm32 | 1 (km 6,373) | 432883 / 538535 | ← zach. | za 62 |
| Tm33 | 204 | 432870 / 538638 | → wsch. | przed 65 |
| Tm34 | 2 | 432861 / 538653 | ← zach. | za 64 |
| Tm35 | 204 / 204a | 432852 / 538716 | ← zach. | za 65 |

Do sprawdzenia:
- Tm29 i Tm33. Na Semaforku symbol stoi między torami. Wybrałem tory 2 i 204 (tarcza pod torem = jazda na wschód). Wcześniejszy odczyt (`Semaforek_Widzew_odczyt.md`) podaje tory 1 i 2.
- Tm25, Tm26, Tm27 nie ma na Semaforku, więc ich nie wstawiałem.
- Tm100, Tm101 i Tm102–137 należą do nastawni CS, więc też ich nie wstawiałem.

## 4. Wykolejnice

| nazwa | tor | X / Y | kierunek (jazda, którą zatrzymuje) | skąd |
|---|---|---|---|---|
| Wk18 | 200 (6a) | – | bez zmian | Twoja |
| Wk28 | sekcja eksploatacji | – | bez zmian | Twoja |
| **Wk24** | 201 | 433240 / 537156 | ← zach. (z 201 w stronę 24) | miejsce z ORM |
| **Wk47** | 21 (CS) | 433043 / 537965 | → wsch. (w stronę 47) | miejsce z ORM |
| **Wk49** | 204 | 432993 / 538095 | ← zach. (z 204 w stronę 49) | miejsce z ORM |

Strona wykolejenia: w prawo, patrząc w stronę jazdy (`Side = 1`). ORM nie podaje kierunku ani strony – do sprawdzenia.
