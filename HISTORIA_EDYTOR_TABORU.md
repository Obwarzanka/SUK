# Historia prac – tabor i fizyka jazdy

Plan: `PLAN_EDYTOR_TABORU.md` (wariant B, decyzje § 9).

## Etap B1 – biblioteka `Lcs.Tabor`, fizyka, katalog do kalibracji

**ZROBIONE 2026-10-19:**
- **Biblioteka `app/Lcs.Tabor`** (net8, bez WPF, w `Lcs.slnx`): jeszcze niepodłączona do symulatora (to etap B2).
  - `Tabor.cs`: `Pojazd` (seria, rodzaj, długość, masa, ładunek, masa napędna, masy wirujące, Vmax, charakterystyki `Charakterystyka` F(v) z mocą, opory a + b·v + c·v², masa hamująca G / P / R próżny i ładowny, hamulec ED, źródło, „do potwierdzenia”), `Sklad` (pozycje: seria, liczba, załadowanie 0–1; nastawienie hamulca), `TaborPlik` (`tabor.json`, zapis z `.bak`, `<układ>.tabor.json` uzupełnia plik wspólny, `Sprawdz()`).
  - `Trakcja.cs`: `Pociag` – sumy składu (długość, masa, masa zastępcza, λ, Vmax, czasy napełniania: EP 2 s / P i R 5 s / G 25 s); siła pociągowa z charakterystyki ograniczona przyczepnością Curtiusa–Kniefflera × warunki szyn; opór podstawowy; opór profilu (pochylenie ‰ = N/kN, plus opór łuku); hamowanie = KH · g · masa hamująca (KH = 0,07 → λ 100 % ≈ 0,65 m/s², do kalibracji) albo ED, ograniczone przyczepnością. `Jazda.Krok` – stały krok 0,1 s, napełnianie / luzowanie hamulca, BZT: sieć ↔ spalinowy z przełączeniem 20 s bez siły, EZT / lokomotywa elektryczna bez sieci – brak zasilania, na postoju bez staczania (rusza, gdy siła > opór + składowa ciężaru; inaczej „nie rusza”). Pomocnicze: `Rozpedzanie`, `DrogaHamowania`, `PredkoscRownowagi`, `MozeRuszyc`.
  - `Data/tabor.json` (kopiowany do programów): EN57, 36WEh (BZT), EU07, 111A, ET22, Eaos; składy EN57, 36WEh, EU07 + 6 × 111A (R), ET22 + 40 × Eaos ładowne (G). **Wszystkie dane przybliżone – do potwierdzenia przez właściciela.**
- **Testy** `app/Lcs.Tabor.Testy` (xunit, `dotnet test --project app/Lcs.Tabor.Testy/Lcs.Tabor.Testy.csproj -c Release`): 12 z 12 – katalog bez uwag, „Sprawdź” wykrywa błędy, zapis / odczyt / plik układu, sumy składu, przyspieszenie z równania ruchu i wpływ 10 ‰, pod górę wolniej / z góry szybciej, droga hamowania zależna od pochylenia i λ, ciężki towarowy nie rusza na 12 ‰, EZT bez sieci, przełączanie BZT, powtarzalność niezależna od klatki (0,5 s i 0,1 s).
- **Raport kalibracji** (`kalibracja_tabor.txt` obok testów) – do oceny przez właściciela:

```
EN57 (osobowy): 136 t, 65,0 m, λ 85 % (P), Vmax 110 km/h
  0 → 100 km/h: poziom 124 s / 2295 m; wzniesienie 10 ‰ 486 s / 11332 m; spadek 10 ‰ 83 s / 1425 m
  hamowanie nagłe 100 → 0: poziom 698 m; spadek 10 ‰ 830 m; wzniesienie 10 ‰ 603 m
  prędkość równowagi: 10 ‰ 101 km/h, 20 ‰ 67 km/h; rusza na: 10 ‰ tak, 20 ‰ tak

36WEh (osobowy, BZT): 158 t, 65,0 m, λ 89 % (P), Vmax 160 km/h
  0 → 100 km/h: poziom 51 s / 842 m; wzniesienie 10 ‰ 63 s / 1082 m; spadek 10 ‰ 43 s / 692 m
  hamowanie nagłe 100 → 0: poziom 550 m; spadek 10 ‰ 628 m; wzniesienie 10 ‰ 489 m
  prędkość równowagi: 10 ‰ 160 km/h, 20 ‰ 137 km/h; rusza na: 10 ‰ tak, 20 ‰ tak

EU07 + 6 × 111A (pospieszny): 338 t, 162,9 m, λ 137 % (R), Vmax 120 km/h
  0 → 100 km/h: poziom 85 s / 1481 m; wzniesienie 10 ‰ 134 s / 2532 m; spadek 10 ‰ 64 s / 1071 m
  hamowanie nagłe 100 → 0: poziom 493 m; spadek 10 ‰ 549 m; wzniesienie 10 ‰ 448 m
  prędkość równowagi: 10 ‰ 120 km/h, 20 ‰ 93 km/h; rusza na: 10 ‰ tak, 20 ‰ tak

ET22 + 40 × Eaos ładowne (towarowy): 3320 t, 580,8 m, λ 73 % (G), Vmax 100 km/h
  0 → 100 km/h: poziom nie osiąga (90 km/h); wzniesienie 10 ‰ nie osiąga (0 km/h); spadek 10 ‰ 214 s / 3394 m
  hamowanie nagłe 100 → 0: poziom 1072 m; spadek 10 ‰ 1325 m; wzniesienie 10 ‰ 897 m
  prędkość równowagi: 10 ‰ 0 km/h, 20 ‰ 0 km/h; rusza na: 10 ‰ nie, 20 ‰ nie
```

- Regresja: build `Lcs.slnx` Release bez błędów, selftest `station_test.json` 25 z 26 / 4 z 4 (symulator bez zmian w kodzie).
- Uwagi na B2: symulator ma zmienną klatkę – żeby ruch był powtarzalny, pociąg zbiera czas i liczy tylko pełne kroki 0,1 s (reszta przechodzi na następną klatkę). Na postoju pociąg nie stacza się wstecz (hamulec postojowy) – staczanie nie jest modelowane.

## Etap B2 – jazda w symulatorze z pochyleniami

**ZROBIONE 2026-10-19:**
- `Lcs.Core` korzysta z `Lcs.Tabor` (warstwa świata; kontrola `--boundary` hosta testowego – OK, bez WPF).
- **Rozkład:** pole `"sklad"` w `<układ>.rozklad.json` (`TimetableEntry.Sklad`, `RozkladPlik`). Pociąg ze składem dostaje fizykę (`Train.Fiz`, `Train.Jaz`), długość i Vmax ze składu. Nieznany skład – uwaga instruktora i ruch uproszczony. Pociąg bez składu – **jak dotąd** (decyzja § 9 pkt 1).
- **Plik taboru w symulatorze:** `Trains.Tabor` – `Data/tabor.json` obok programu (kopiowany z `Lcs.Tabor`) uzupełniony `<układ>.tabor.json`.
- **Ruch (`Sim/TrainsFizyka.cs`, `Trains.MoveFiz`):** kroki stałe 0,1 s z zapasem czasu między klatkami (ruch niezależny od klatki); pochylenie i opór łuku średnie pod całą długością składu (`Edge.Grad` ze znakiem wg kierunku jazdy, `Edge.Radius`); sieć z `elec` toru pod czołem – układ bez żadnego `elec` traktowany jako zelektryfikowany w całości (dzisiejsze pliki nie mają tych danych).
- **Automatyczny maszynista:** prędkość dozwolona pod składem (tor, zwrotnice, prędkość przebiegu, Sz, manewry) minus 3 km/h; krzywe hamowania służbowego (70 %) do ograniczeń przed czołem i do miejsca zatrzymania z czasem napełniania hamulca i **średnim pochyleniem na drodze hamowania** (na spadku zaczyna hamować wcześniej); ostatnie metry do sygnału „Stój” z ok. 5 km/h; pociąg nigdy nie mija miejsca zatrzymania (ograniczenie drogi jak w ruchu uproszczonym). Zdarzenie „Pociąg … nie może ruszyć – za mała siła pociągowa na wzniesieniu …” / „brak sieci trakcyjnej”.
- Wspólny kod ruchu: `Trains.Advance` (przesunięcie czoła, nowe kawałki toru, wyjazd z obszaru) – ruch uproszczony bez zmian w zachowaniu.
- **Tryb zrzutu `#stan`:** przy pociągu ze składem – nazwa składu, masa, pochylenie pod składem, prędkość docelowa maszynisty, siła pociągowa, stopień zahamowania.
- **Testy** `app/Lcs.Tabor.Testy/JazdaTesty.cs` (rdzeń bez okien, kopia mijanki testowej z pochyleniem toru 1 dopisanym w teście): EN57 pod górę / poziom / z góry, ciężki towarowy na spadku 10 ‰ staje przed A (bez minięcia „Stój”), na wzniesieniu 12 ‰ staje na szlaku i „nie może ruszyć”, pociąg bez składu – ten sam czas niezależnie od pochylenia, nieznany skład – uwaga. Razem z B1: **17 z 17**. Wynik:

```
EN57, mijanka (kopia z pochyleniem toru 1), od zatrzymania przed A do opuszczenia obszaru przez 1W:
  pod górę 8 ‰: 144 s
  poziom: 130 s
  z góry 8 ‰: 119 s
Prędkość najwyższa na szlaku przed A: 88 / 88 / 88 km/h
```

- **Zrzuty pulpitu** (kopia mijanki z pochyleniem ±8 ‰ i rozkładem ze składami EN57 i ET22 + 40 × Eaos): `zrzuty/Pulpit_fizyka_pod_gore_8.png`, `zrzuty/Pulpit_fizyka_z_gory_8.png`. Z `#stan`: EN57 pod górę 48 km/h przy 44 kN po 60 s, z góry 56 km/h; towarowy 3320 t pod górę 8 ‰ po zahamowaniu G luzuje hamulce ok. 50 s i dopiero rusza (360 kN przy oporze ok. 306 kN).
- Regresja: selftest `station_test.json` 25 z 26 / 4 z 4, KZ na żywo 0 naruszeń, **dziennik zdarzeń identyczny jak przed B2**; `EdytorZobrazowania.exe --test` 0 błędów; `Lcs.Core.TestHost --boundary` OK. `EdytorUT` nie był przebudowany (program był otwarty u właściciela, blokował `Lcs.Model.dll`) – nie zależy od `Lcs.Tabor` ani rdzenia, kod bez zmian.
- Nie sprawdzono: obsługa prawdziwą myszą (pulpit bez zmian), pliki właściciela (`Data/projekty/*.json`) nie mają `poch` ani `sklad` – jazda wg fizyki tylko tam, gdzie rozkład poda skład. Pochylenia dochodzą przez „Do symulatora…” z niwelety UT.

