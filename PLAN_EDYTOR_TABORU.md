# Edytor taboru i ruch pociągów z pochyleniami – plan do akceptacji

Projekt z 2026-10-19. **Stan: zaakceptowany 2026-10-19 – wariant B (§ 8), odpowiedzi na pytania § 7 – propozycje agenta (§ 9). B1 zrobiony 2026-10-19 (`HISTORIA_EDYTOR_TABORU.md`), następny B2 – jazda w symulatorze.**

Decyzje właściciela (2026-10-19):
1. Pochylenia **mają wpływać** na ruch pociągów.
2. Składy są **zdefiniowane wcześniej** w edytorze taboru: lokomotywy, wagony, EZT, SZT, BZT.
3. Edytor to **osobny program** (`EdytorTaboru.exe`), tak jak edytor UT i edytor zobrazowania.
4. Dane pojazdów: agent wpisuje **przykłady prawdziwych serii z przybliżonymi danymi**, oznaczone „do potwierdzenia”. Właściciel je poprawia.
5. **BZT** to bitrakcyjny zespół trakcyjny (np. 36WEh). Pod siecią jedzie jako elektryczny, poza siecią jako spalinowy.
6. **Pełna trakcja:** siła pociągowa z charakterystyki F(v), opory ruchu, pochylenie, łuki, przyczepność, hamowanie z procentu masy hamującej. Jazdę prowadzi automatyczny maszynista.

---

## 1. Stan obecny (do zmiany)

- `Sim/Trains.cs`: wszystkie pociągi przyspieszają ze stałym 0,5 m/s² i hamują z 0,8 m/s². Długość i Vmax biorą tylko z kategorii (`Cats`: ŁKA, PR, IC, TOW, MAN…), a rozkład GTFS może podać długość (`Len`) i pojazd (`Veh` – tylko tekst).
- `station.json`: tory mają pochylenie `poch` [‰ od A do B] z niwelety UT (eksport „Do symulatora…”) i elektryfikację `elec`. Ruch tego nie używa.
- Promienia łuku na torach symulatora nie ma (jest w UT).

## 2. Dane: plik taboru

Plik `Data/tabor.json`, wspólny dla edytora taboru i symulatora. Edytor zapisuje go z kopią `.bak`. Układ może mieć własny plik `<układ>.tabor.json` obok pliku układu (jak `.zaleznosci.json`, `.rozklad.json`), który uzupełnia albo zastępuje wpisy wspólne.

**Pojazd** (seria), rodzaje:
- lokomotywa elektryczna, lokomotywa spalinowa;
- wagon pasażerski, wagon towarowy (masa własna i ładunek, pusty / ładowny);
- EZT (elektryczny zespół trakcyjny), SZT (spalinowy), BZT (bitrakcyjny – dwa źródła zasilania).

Pola pojazdu:
- oznaczenie serii, nazwa, rodzaj;
- długość [m], masa własna [t], masa ładunku lub pasażerów [t], masa na osiach napędnych [t], liczba osi;
- współczynnik mas wirujących (np. 1,06 dla wagonu, 1,15–1,25 dla lokomotywy);
- Vmax [km/h];
- **charakterystyka trakcyjna F(v):** punkty (v [km/h], F [kN]) – wykres jak w dokumentacji pojazdu. Dla BZT dwie charakterystyki: „sieć” i „spalinowy”. Dodatkowo moc [kW] i siła rozruchowa jako podpowiedź do szybkiego wpisania krzywej F = min(F0, P/v);
- **opory ruchu:** wzór a + b·v + c·v² [N/kN], z gotowymi wzorami do wyboru (EZT, wagon pasażerski, towarowy pusty / ładowny, lokomotywa);
- **hamulec:** masa hamująca [t] w nastawieniach G / P / R (R+Mg), hamulec elektrodynamiczny (tak / nie, siła [kN]);
- uwagi i źródło danych („do potwierdzenia”).

**Skład** – lista pojazdów po kolei: seria, liczba (np. 2 × EN57 w trakcji wielokrotnej), stan ładowny / próżny, nastawienie hamulca. Program sam liczy sumy: długość, masę, procent masy hamującej λ, Vmax (najniższa w składzie), siłę pociągową z pojazdów napędnych.

**Przypisanie do pociągu:**
- `<układ>.rozklad.json`: nowe pole `"sklad": "id składu"`;
- kategoria pociągu ma skład domyślny (ŁKA → EZT, TOW → lokomotywa z wagonami…) – ustawienie w pliku taboru;
- GTFS: `Veh` (np. „ED74”) wskazuje serię, jeśli taka jest w katalogu.

## 3. Fizyka jazdy (`Model/Trakcja.cs` w `Lcs.Core`, wspólna dla symulatora i edytora)

Na każdym kroku czasu dt:
- siła pociągowa F(v) według nastawy maszynisty (0–100 %), ograniczona przyczepnością μ(v)·m_nap·g (wzór Curtiusa–Kniefflera, μ0 do ustawienia: sucho / mokro);
- opór podstawowy: suma a + b·v + c·v² po pojazdach (N/kN × ciężar);
- **opór od pochylenia: i [‰] = i [N/kN]**, liczony pod całą długością składu – każdy kawałek toru z własnym `poch` ma udział proporcjonalny do masy pociągu na tym kawałku (pociąg „przegięty” na załomie profilu);
- opór łuku: 650 / (R − 55) N/kN (wzór Röckla, do potwierdzenia) – wymaga promienia na torach w `station.json` (eksport `R` z UT, etap T5);
- przyspieszenie a = (F − W) / (m · (1 + współczynnik mas wirujących));
- hamowanie: opóźnienie z procentu masy hamującej λ i nastawienia (G / P / R), czas napełniania cylindrów, hamulec ED w zespołach trakcyjnych, plus wpływ pochylenia (na spadku droga hamowania dłuższa).

**BZT:** na torze z `elec` charakterystyka „sieć”, bez sieci – „spalinowy”. Przełączenie trwa (np. 20 s, opuszczenie pantografu) – w tym czasie brak siły pociągowej.
**EZT na torze bez sieci:** brak siły pociągowej (toczy się) i uwaga w zdarzeniach.

**Automatyczny maszynista** (zastępuje dzisiejsze `Lookahead` + `SpeedLimit` przez prędkość docelową):
- jedzie z prędkością dopuszczalną (sygnał, tor, zwrotnice, prędkość przebiegu VSet) z marginesem;
- przewiduje drogę hamowania do najbliższego ograniczenia i do sygnału „Stój” **z uwzględnieniem pochyleń na tej drodze**;
- na spadku hamuje, żeby nie przekroczyć prędkości; na wzniesieniu ciężki towarowy może zwolnić poniżej dozwolonej – tak ma być;
- ruszanie pod górę: gdy siła nie wystarcza, pociąg stoi i zdarzenie „pociąg nie może ruszyć” (scenariusz dla instruktora).

**Zgodność wstecz:** pociąg bez przypisanego składu i bez składu domyślnego dla kategorii jedzie jak dotąd (stałe 0,5 / 0,8 m/s²). Selftest na `station_test.json` (25 z 26, 4 z 4) i scenariusze nie zmieniają wyniku, dopóki nie dostaną składów.

## 4. Edytor taboru (`app/Lcs.EdytorTaboru`, `EdytorTaboru.exe`)

- **Zakładka „Pojazdy”:** lista serii po rodzajach, formularz pól z § 2. Wykres F(v) z tabelą punktów: klik dodaje punkt, przeciąganie przesuwa, przycisk „z mocy i siły rozruchowej”. Na tym samym wykresie opory na poziomie i na pochyleniu 5 / 10 / 15 ‰.
- **Zakładka „Składy”:** pojazdy przeciągane z listy, kolejność, liczba, ładowny / próżny, hamulec. Na górze sumy: długość, masa, λ, Vmax, a = F/m przy ruszaniu.
- **Podgląd jazdy:** wykres v(s) i v(t) dla składu: rozpędzanie do Vmax na poziomie i na pochyleniu, droga hamowania z Vmax. Opcja „przejazd próbny” po profilu toru z układu symulatora (wybór pliku i trasy). Wykres liczy ten sam kod co symulator.
- „Sprawdź”: brak charakterystyki u pojazdu napędnego, skład bez pojazdu napędnego, λ poniżej wymaganego dla kategorii (wartości do potwierdzenia), siła pociągowa za mała, żeby ruszyć składem na wybranym pochyleniu, długość większa niż tory stacyjne (uwaga).
- Cofnij / ponów, zapis z `.bak`, test „jak użytkownik” `EdytorTaboru.exe --test wynik.txt zrzut.png` (`błędów: 0`).

## 5. Etapy

| Etap | Zakres | Gotowe, gdy |
|---|---|---|
| T0 | Ten plan, zaakceptowany przez właściciela, i ustalenie źródeł wzorów (opory, hamowanie, przyczepność). | decyzje w § 7 |
| T1 | Model taboru (`Model/Tabor.cs`): pojazdy, składy, plik `tabor.json`, sumy składu; przykładowy katalog (EN57, ED74, EN76 / 36WEa, SA134, 36WEh – BZT, EU07, ET22, SM42, wagony pasażerskie i towarowe) z oznaczeniem „do potwierdzenia”. | plik wczytuje się i zapisuje; sumy składu zgodne z ręcznym przeliczeniem |
| T2 | Fizyka (`Model/Trakcja.cs`) i test bez okien: rozpędzanie, opory, pochylenie, hamowanie, przyczepność, BZT. | testy liczbowe: np. czas rozpędzania EN57 do 100 km/h na poziomie i na 10 ‰ wg danych z katalogu |
| T3 | Edytor taboru: okno, pojazdy, wykres F(v), składy, podgląd jazdy, „Sprawdź”, test. | skład zbudowany klikami w teście; zrzut |
| T4 | Symulator: pociąg ze składem jedzie wg fizyki, pochylenia z `poch`, automatyczny maszynista; `sklad` w rozkładzie, składy domyślne kategorii; podgląd pociągu (masa, siła, prędkość) w oknie pociągów. | mijanka testowa: ten sam pociąg pod górę i z góry – inne czasy przejazdu; selftest 25 z 26 / 4 z 4 bez zmian |
| T5 | Łuki: `R` w eksporcie „Do symulatora…”, opór łuku; zdarzenia „nie może ruszyć”, EZT bez sieci. | zrzuty i wpisy w zdarzeniach |
| T6 | Rozkład GTFS: `Veh` → seria z katalogu; scenariusz instruktora „pociąg ciężki na wzniesieniu”. | Widzew z rozkładem rzeczywistym – czasy przejazdu z fizyki |

Po każdym etapie: build, testy (selftest, `EdytorUT --test`, `EdytorZobrazowania --test`, nowy `EdytorTaboru --test`), zrzut, wpis w historii (`HISTORIA_EDYTOR_TABORU.md`) i skrót w `ROZWOJ.md`.

## 6. Czego plan nie obejmuje

- Kabiny maszynisty i ręcznego prowadzenia pociągu.
- Zużycia energii, sieci trakcyjnej z sekcjami i podstacjami.
- Wagonów w ruchu manewrowym z doczepianiem i odczepianiem (rozrząd) – osobny temat.
- Grafiki pojazdów – na pulpicie pociąg wygląda jak dziś.

## 7. Pytania do właściciela

1. **Zgodność wstecz:** pociągi bez składu jadą po staremu (stałe przyspieszenie) – czy tak zostawić, czy po T4 wszystkie kategorie dostają składy domyślne (zmieni czasy w scenariuszach)?
2. **Źródła wzorów:** czy masz instrukcję o hamulcach / procencie masy hamującej (np. PKP PLK, PKP Intercity) albo podręcznik trakcji? Bez tego wpiszę wzory podręcznikowe jako „do potwierdzenia”.
3. **Automatyczny maszynista:** jedzie „na styk” do prędkości dozwolonej czy z zapasem (np. −5 km/h) i hamuje łagodnie (służbowo ok. 0,5–0,7 m/s²)?
4. **Plik taboru:** jeden wspólny `Data/tabor.json` plus opcjonalny plik przy układzie – zgoda?
5. **Lista serii na start (T1):** EN57, ED74, 36WEa (Impuls), SA134, 36WEh (BZT), EU07, ET22, SM42, wagony 111A, Z1, Eaos, Falns – dopisać / skreślić?
6. **Warunki szyn:** sucho / mokro / liście jako ustawienie instruktora (przyczepność) – teraz czy później?

---

## 8. Plan agenta (wariant B) i porównanie z planem z § 5 (wariant A)

Wariant A (§ 5) ułożyłem wprost z odpowiedzi właściciela. Wariant B to kolejność, którą wybrałbym sam. Treść jest ta sama (pojazdy, składy, fizyka, BZT, edytor), a zmienia się kolejność i miejsce kodu.

### Wariant B – najpierw jazda w symulatorze, potem edytor

| Etap | Zakres | Gotowe, gdy |
|---|---|---|
| B1 | Biblioteka **`Lcs.Tabor`** (osobna, bez WPF, net8): model pojazdu i składu, plik `tabor.json`, fizyka (F(v), opory, pochylenie, przyczepność, hamowanie). Na start 4 serie do kalibracji: EN57, 36WEh (BZT), EU07 + wagony 111A, ET22 + Eaos ładowne. Testy liczbowe bez okien. | rozpędzanie i droga hamowania zgodne z danymi wpisanymi do katalogu (± 5 %) |
| B2 | Symulator: pociąg ze składem jedzie wg fizyki z `poch`, automatyczny maszynista z hamowaniem na spadku; **stały krok fizyki 0,1 s** (powtarzalność dla rejestratora i gry sieciowej); `sklad` w rozkładzie. Katalog do czasu edytora wpisuje się w JSON. | mijanka: ten sam skład pod górę i z góry – różne czasy; ciężki towarowy zwalnia na wzniesieniu; selftest 25 z 26 / 4 z 4 bez zmian |
| B3 | `EdytorTaboru.exe` – wersja podstawowa: lista pojazdów, formularz, tabela punktów F(v), składy z sumami, „Sprawdź”, zapis, test. | skład zbudowany klikami w teście |
| B4 | Edytor – wykresy F(v) i oporów, podgląd jazdy v(s) / v(t), przejazd próbny po profilu układu (ten sam kod co B2). | wykres = czas przejazdu w symulatorze |
| B5 | Rozszerzenie: pozostałe serie, łuki (`R` z UT), GTFS `Veh` → seria, składy domyślne kategorii, zdarzenia „nie może ruszyć”, EZT bez sieci, scenariusz instruktora. | Widzew z rozkładem rzeczywistym na fizyce |

### Porównanie

| Sprawa | A (§ 5) | B (agent) |
|---|---|---|
| Kiedy widać pochylenia w ruchu | po T4 – najpierw cały edytor (T3) | po B2 – edytor później |
| Pierwsza ocena fizyki przez właściciela | na wykresach w edytorze | na pulpicie, na prawdziwym pociągu |
| Miejsce kodu | `Model/Tabor.cs`, `Model/Trakcja.cs` w `Lcs.Core` | osobna biblioteka `Lcs.Tabor`, używana przez `Lcs.Core` i edytor |
| Zgodność z planem multiplayer (S02 – host świata, KZ bez dostępu do pociągów) | możliwa, ale tabor miesza się z rdzeniem KZ | tabor jako część warstwy świata; kontrola `--boundary` łatwiejsza |
| Powtarzalność ruchu (rejestrator, sieć) | nie opisana | stały krok 0,1 s, wynik ten sam przy każdym uruchomieniu |
| Katalog na start | ok. 12 serii, wszystkie „do potwierdzenia” | 4 serie do kalibracji, reszta w B5 |
| Wpisywanie danych przed edytorem | nie trzeba – edytor jest wcześniej | przez ok. 2 etapy ręcznie w JSON (agent wpisuje przykłady) |
| Ryzyko | duży edytor przed sprawdzeniem fizyki w ruchu – możliwe przeróbki pól | edytor dochodzi później, pola są już sprawdzone w ruchu |
| Wspólne | decyzje właściciela, wzory z § 3, zgodność wstecz (pociąg bez składu po staremu), pytania z § 7 | to samo |

**Zalecenie agenta:** wariant B. Szybciej pokazuje to, o co chodzi (pochylenia w ruchu), wcześniej wyłapuje złe wzory i pasuje do planu multiplayer (jeden świat fizyczny poza KZ). Jeśli edytor ma być pierwszy (np. żeby od razu wpisywać własne dane), lepszy jest wariant A z jednym wyjątkiem: biblioteka `Lcs.Tabor` i stały krok fizyki z B.

## 9. Decyzje właściciela (2026-10-19)

Realizujemy **wariant B** (§ 8). Na pytania z § 7 przyjęto propozycje agenta:
1. Pociąg bez składu (i bez składu domyślnego kategorii) jedzie po staremu – stałe 0,5 / 0,8 m/s². Składy domyślne kategorii dopiero w B5.
2. Wzory podręcznikowe oznaczone „do potwierdzenia”: opory a + b·v + c·v² [N/kN, v w km/h], przyczepność Curtiusa–Kniefflera, opór łuku Röckla, hamowanie z masy hamującej ze współczynnikiem do kalibracji.
3. Automatyczny maszynista jedzie z zapasem ok. 3 km/h poniżej prędkości dozwolonej i hamuje służbowo (ok. 70 % pełnego hamowania).
4. Wspólny plik `tabor.json` (w bibliotece `Lcs.Tabor`, kopiowany do programów) plus opcjonalny `<układ>.tabor.json` obok układu.
5. Na start (B1) cztery składy do kalibracji: EN57, 36WEh (BZT), EU07 + 111A, ET22 + Eaos; pozostałe serie w B5.
6. Warunki szyn (sucho / mokro / liście) – parametr fizyki od B1, ustawienie instruktora później.
