# Odczyt planu schematycznego st. Łódź Widzew (Semaforek, stan 01.2017, rys. A. Karwat)

Plik: `Semaforek_Lodz_Widzew.png` (8284×1186), źródło: https://semaforek.kolej.org.pl/wiki/images/2/2b/Lodz_Widzew.png

Konwencja planu: sygnalizatory dla jazdy na **zachód nad torem**, na **wschód pod torem**; tarcza „○⊗” = jazda na wschód, „⊗○” = na zachód.

## Zachód (od Fabrycznej / tunel, Niciarniana)
- Szlak 4 tory: l. 017 (tor 2 góra, tor 1), l. 458 (tory 102, 101). SBL w tunelu: 26RN, 21R, 26DN, 21D (szlak), 26R, 21RN, 26D, 21DN.
- Wjazdowe (na wschód): **B2, B1, B102, B101** (za p.o. Niciarniana).
- Zwr. 1: 101 → 102; zwr. 2 (na 102) → 3 (na torze 1), dalej tor 11 ślepy; zwr. 4 (tor 1) → 5 (tor 2).
- Drogowskazowe na zachód: **C2, C1** (za zwr. 5).
- Drogowskazowe na wschód: **D** (tor 2), **E** (tor 1).
- l. 540 (Chojny/Dąbrowa) tory 2a/1a: ToG, ToH, powtarzacze I–IIISpG, I–IIISpH, wjazdowe **G** (górny tor), **H** (dolny).
- Pasy od góry w głowicy: tor 2 | tor 1 | pas 3 (od G → tor 3) | pas 4 (od H → tor 5).
- Rozjazdy: 6 (pas 4) → 7 (pas 3); 8 (pas 3) → 9 (tor 1); 10 (tor 1) → 11 (tor 2); 12 (tor 2) → 13 (tor 1); 14 (tor 2) → drabinka w górę: 17 (krzyżowy, tor 4 / l. 016), 18 (krzyżowy, tor 200 / 6), 21 (początek toru 8), 24 (→ 201 / Wk24 / zaplecze; → tory 102/104 przez przejazd kat. F km 5,076 z TVU); 22 (tor 6) → 23 (tor 4); 25 (tor 4) → 26 (tor 2); 15 (tor 1) → 16 (tor 3); 19 (tor 3) → 20 (tor 5); 27 (tor 5) → 28 (tor 7), przy 28 Wk28 i Tm17 – tory sekcji eksploatacji.
- l. 016 (Kutno/Zgierz): ToA, wjazdowy **A**, wchodzi w tor 4. Tor 200 (dł. 150 m) z Wk18.
- Tarcze: Tm1 (pas 4, W→E), Tm2 (pas 3, E), Tm3 (W), Tm4 (E), Tm5 (W), Tm6 (tor 1, E), Tm7 (tor 2, W), Tm8 (tor 4, E), Tm9 (tor 3, E), Tm10 (tor 200, E), Tm11 (tor 1, W), Tm12 (tor 5, E), Tm13 (tor 3, W), Tm14 (tor 2, E), Tm15, Tm16 (przy 24), Tm17, Tm18 (tory 102/104), Tm100, Tm101.
- Wyjazdowe na zachód: **M8, M6, M4, L2, L1, L3, L5, L7**.

## Środek
- Tory i dł. użyteczne: 8 (738), 6 (736/737), 4 (642/696), 2 (693/753), 1 (796/888), 3 (759/850), 5 (635/641), 7 (517/604); 102 (393/394), 104 (395/396), 204 (549/546), 204a (169), 202 (100), 7b.
- Perony: 3 (między 4 i 2), 2 (1/3), 1 (5/7). Nastawnia „ŁW” na południu przy zwr. 20.
- Zaplecze ŁKA – **nastawnia „CS”**: tory 21–28, 23a–26c, zwr. 101–116, Tm102–Tm107, Tm128–Tm137 (poza sterowaniem LCS – pokazać jako obszar CS).
- Tory 102/104: N102, N104 (zach.), P102, P104 (wsch.), zwr. 42.

## Wschód (Andrzejów pbsz / Koluszki, Olechów)
- Wyjazdowe na wschód: **R8, R6, R4, S2, S1, S3** (+ISpS3), **S5, S7** (+ISpS7).
- 43 (tor 6/8), 45 (tor 4), 47 (od 102/104, Tm137, Wk47), 48, 49 (→ tor 204, Wk49, Tm22), 50 (wejście w tor 2), 44 → 46 (tor 7 → tor 5, tor 7b ślepy).
- Pasy: tor 2 | tor 1 | tor 3 | tor 5. Rozjazdy: 51 (tor 2) → 53 (tor 1); 52 (tor 5) → 54 (tor 3); 56 (tor 1) → 57 (tor 3); 58 (tor 3) → 59 (tor 1); 60 (tor 3) → 61 (tor 5); 62 (tor 1) → 63 (tor 2); 64 (tor 2) → 65 (tor 204a, Tm35).
- Wyjazd: tor 2 → **W**, tor 1 → **V** (l. 017), tor 3 → **U**, tor 5 → **T** (l. 541). ToU, ToT. SBL: 82N, 82, 81, 81N.
- Tarcze: Tm22 (204, W), Tm23 (tor 2, W), Tm24 (tor 5/3, W), Tm28 (tor 5, E), Tm29 (tor 1, E), Tm30 (tor 3, W), Tm31 (tor 5, W), Tm32 (tor 1, W), Tm33 (tor 2, E), Tm34 (tor 2, W), Tm35 (204a, W).

## Do zrobienia (następna sesja)
1. Ręcznie narysowany układ pulpitu (siatka, 45°) wg powyższej topologii → generator `station.json` (zastępuje schemat z OSM).
2. Typ sygnalizatora „drogowskazowy” (C, D, E) w modelu; przebiegi pociągowe sygnał → sygnał.
3. Obszar nastawni CS jako przekazany; przejazd kat. F (TVU).
