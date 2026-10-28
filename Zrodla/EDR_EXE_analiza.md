# Analiza EDR.exe (inżynieria wsteczna) – 2026-10-28

Plik: `C:\Users\micha\Desktop\Raport\EDR.exe` (3,2 MB). Zdekompilowany ILSpy 11.2 (`ilspycmd -p`) do katalogu tymczasowego, kodu nie kopiowano do projektu.
Cel: zobaczyć, jak autor EDR do SimRail / TD2 rozwiązał dziennik ruchu, i wyciągnąć wnioski do `PLAN_EDR.md`.

> Uwaga prawna: to cudzy program. Bierzemy z niego **pomysły i reguły** (wynikają z przepisów PLK: R-146/Ir-1/Ie-? – i tak są wspólne), nie przepisujemy kodu.

---

## 1. Co to jest

| Cecha | Wartość |
|---|---|
| Technologia | C# .NET Framework 4.0, WinForms, x86 (PE32), bez zaciemniania (czysty kod, nazwy klas i metod zachowane) |
| Przestrzeń nazw | `EdrLocal`, User-Agent `EDRLocal/1.0.10.199`, marka „SimKol” (mutex `Local\SimKol_EDR_Configurator_…`) |
| Rozmiar | 287 plików `.cs`, ok. 55 tys. linii, z czego ok. połowa to **testy wbudowane w exe** (`--td2192-tests`, `--sepe179-tests` …, numer = wersja/zgłoszenie) |
| Zasoby osadzone | WebView2 (Core, WinForms, Loader x86/x64) – rozpakowywane przy starcie (`BundledWebView2`), `SepeBridge.js`, `Vehicles.json` (katalog pojazdów), `Td2Identity.json`, ikony PNG |
| Dane | `%LOCALAPPDATA%\EDR\dane\…\edr.json` (cały stan w jednym JSON), profile posterunków `%LOCALAPPDATA%\EDR\posterunki\<symulator>\` |
| Język | całość po polsku, kultura `pl-PL` wymuszona w wątku |

### Tryby uruchomienia (`Program.Main`)
- okno wyboru trybu (`ModeForm`): **lokalnie** (ćwiczenia, ręczny rozkład/CSV), **symulator** (SimRail albo TD2), **konfigurator** (administrator posterunków);
- `--demo` – baza przykładowa „Jaworzyna Śląska” (3 szlaki, 38 pociągów, użytkownicy DEMO/PODGLAD, hasło `demo`);
- `--capture plik.png` – zrzut głównego okna bez interakcji (to samo co nasze `--screenshot`);
- `--data katalog`, `--training` (sesja nietrwała), `--self-test`, ~70 przełączników `--…-tests`.
- Jedna baza = jedno okno: mutex `Local\EDRLocal_<SHA256 ścieżki>`.

---

## 2. Architektura

```
Program ── ModeForm / LoginForm / LocationForm / StartupForm ── MainForm (wykaz + dzienniki)
   │
   └── Engine (cała logika ruchowa, ~2600 linii)
          ├── Store (AppState w edr.json, transakcje, .bak)
          ├── SimRailSession (ISimulatorFeed: SimRailProvider | Td2Provider)
          ├── SepeBackgroundWriter (WebView2 + SepeBridge.js → oficjalne SEPE w grze)
          └── JournalReport (wyciąg: PDF rastrowy / CSV / HTML / druk A4)
```

### 2.1 Model danych (`AppState`, Version = 1)
- `Stations[]` – posterunek: `Sections[]` (szlaki = dzienniki), `Tracks[]` (tory stacyjne), `Checks[]` / `RequiredChecks[]` (sprawdzenia), `JournalTabs[]` (para `LeftId`/`RightId` = dwa dzienniki w jednej zakładce), `TrackSettings[]`.
- `Section` (szlak): `Name`, `Line`, `Tracks` (liczba torów szlakowych), `NormalIncomingTrack`, `ArrivalEven` (przyjmowane parzyste), `Basis` (rodzaj blokady), `Crossings[]` (dróżnicy), `LineTracks[]` (ustawienia torów), `JournalName`, `Conducted` (czy prowadzony).
- `Train` – pozycja wykazu: plan/rzeczywiste/prognoza przyjazdu i odjazdu, `From/To` (sąsiedzi), `LineIn/LineOut`, `Origin/Destination`, `Carrier`, `Track/Platform`, flagi `Runs` (K/NK), `Dangerous` (TN), `HighRisk` (TWR), `Extraordinary`, `QualityTracked`, `Stopped` (POCIĄG STÓJ), `Ready*` (gotowość do odjazdu, maszynista), `Consist` (plan składu: seria, długość, brutto), `Profile` (nazwa, uwagi eksploatacyjne), `DelayReason`, `Analysis`.
- `JournalEntry` – **jeden wiersz dziennika**: `SectionId` (albo `"TEL"` = dziennik telefoniczny), `Type` (`Pociąg`, `Telefonogram`, `Służbowy`, `Doba` …), `Direction` (`Z` = przyjęcie, `DO` = wyprawienie), `Number`, `Track`, `LineTrack`, `Permission` (droga wolna), `Left`, `Arrived`, `Text` (uwagi), `Notification` + `CrossingNotifications[]`, `Effect` (skutek telefonogramu), `Telephone` (dane wzoru), `ReturnOfId` (jazda do km i z powrotem), `Cancelled` + `CancelReason` (wykreślenie).
- `Duties[]` (dyżury), `History[]` (audyt), `Services[]` (usługi/zamówienia), `StationTrackOccupations[]` (ręczna zajętość torów stacyjnych), `Users[]`.

### 2.2 Zapis i historia zmian – ważny pomysł
- `Store.Commit(action)`: kopia stanu → zmiana → walidacja → zapis do `edr.json.tmp` → `File.Replace` z kopią `.bak` → podmiana stanu. **Atomowo, nigdy nie psuje pliku.**
- Każda zmiana przechodzi przez `Engine.Change(akcja, encja, edit)`, który dopisuje `Audit` z **pełnym JSON stanu przed i po** (bez haseł).
- **Wyciąg z historią** (`ReportHistory`) nie trzyma wersji w wierszu: odtwarza je, porównując `Before/After` z audytu dla każdego wiersza. Proste, ale plik rośnie kwadratowo (u nich i tak działa, bo sesje są krótkie).
- Wpisów się nie kasuje: tylko **wykreślenie z przyczyną**. Wpisów „Służbowy” (dyżur) nie można wykreślić, wykreślonego wpisu nie można edytować.

> U nas: wersje są w wierszu (`EdrWersja`) – to lepsze niż ich audyt pełnego stanu. Brakuje nam **zapisu do pliku** (otwarte w § 7 planu) – ich schemat `tmp → Replace + .bak` warto przyjąć 1:1.

---

## 3. Reguły ruchowe zaszyte w `Engine` (najcenniejsze)

### 3.1 Parzystość numeru (`TrainNumber`)
Numer zmienia się o ±1 na ostatniej cyfrze wg szlaku: `parzysty = (kierunek == Z) ? ArrivalEven : !ArrivalEven`. Dla TD2 numer bez zmian. Numer < 2 znaków albo nie liczbowy – bez zmian.
→ U nas to samo (E2). Zgodne.

### 3.2 Tor szlakowy z uwag (`JournalLineTracks`)
Tor szlakowy nie jest polem do wyboru – wynika z kierunku zasadniczego, a **zmienia go tekst w uwagach**:
- `po torze lewym` / `po lewym`, `po torze prawym`,
- `po (zamkniętym) torze nr [N]` / `numer N`.
Sprzeczności (dwa numery, lewy i prawy naraz, numer sprzeczny z lewym/prawym, numer spoza szlaku) → błąd z komunikatem. Bez wskazania i przy niejednoznacznym kierunku zasadniczym – komunikat „Wskaż w uwagach »Po torze nr [numer]«”.
→ **Do przejęcia.** U nas „po torze lewym” jest, numer toru z uwag i kontrola sprzeczności – warto dodać.

### 3.3 Stan toru szlakowego (`LineStatus`)
- `ZAMKNIĘTY` – ostatni skuteczny (wg `EffectiveAt`) telefonogram z efektem `Zamknij` / `ZamknijPoPrzejezdzie` (aż do `Otwórz`). Dla „zamknięcia po przejeździe pociągu” moment skuteczności = przyjazd tego pociągu (`ClosureEffectiveAt`).
- `ZAJĘTY: numery` – wiersze „Pociąg” z odjazdem bez przyjazdu.
- inaczej `WOLNY`.

### 3.4 Bieżący rodzaj zapowiadania (`CurrentBasis`)
Ostatni skuteczny telefonogram z efektem `Telefoniczne` (wzór 16 → telefoniczne, 16a → radiotelefoniczne) albo `Normalne` (17/17a). Brak – rodzaj zasadniczy z konfiguracji toru.

### 3.5 Które godziny są wymagane (`DocumentationFields`) – reguła do porównania z naszą
- **Droga wolna**: przy zapowiadaniu telefonicznym **albo** gdy szlak jednotorowy / ruch jednotorowy wprowadzony / któryś tor zamknięty. Nie przy powrocie ze szlaku.
- **Odjechał**: przy wyprawieniu, a przy przyjęciu także przy blokadzie półsamoczynnej i telefonicznym.
- **Przyjechał**: przy przyjęciu, a przy wyprawieniu także przy blokadzie półsamoczynnej i telefonicznym.
→ U nas (E1/E3) zależy od `type` i ustawień. **Nowe dla nas: droga wolna również przy ruchu jednotorowym i przy zamkniętym drugim torze**, nawet przy SBL.

### 3.6 Wpis godzin (`JournalTime`)
Przyjmuje `HH:mm`, `H.mm`, `HHmm`/`Hmm` (bez dwukropka) i pełną datę `dd.MM.yyyy HH:mm`. **Przejście przez północ**: jeśli poprzednia godzina ≥ 18:00, a wpisana < 06:00 – następny dzień. Przyjazd wcześniejszy niż odjazd → błąd z podpowiedzią o północy.
→ Do przejęcia: wpisywanie „0812” bez dwukropka i automatyczna północ.

### 3.7 Rejestracja pociągu z wykazu (`RegisterScheduledTrain`)
- Pierwszy wpis = przyjęcie (`Z`), drugi = wyprawienie (`DO`); pociąg zaczynający bieg → od razu wyprawienie; kończący → brak wyprawienia; oba są → nic.
- Szlak z **nazwy sąsiada + numeru linii** (`SectionRouting`: „Sąsiad L. 17” / „(LK 17)” / „linia 17”); gdy nie jest jednoznaczny – komunikat „Użyj polecenia przyjęcia / wyprawienia na inny szlak”.
- Nie dopuszcza dwóch aktywnych wpisów tego samego pociągu w tym samym kierunku.
→ U nas zgodne (E5 poprawka 1). Rozpoznawanie „L. 17” w nazwie sąsiada – możliwy dodatek.

### 3.8 Dyżur
`BeginDuty` / `EndDuty` – nie w przyszłości, zdanie między początkiem a teraz, obserwator nie przyjmuje. Na każdym prowadzonym dzienniku **i w telefonicznym** dopisuje „Dyżur przyjął: …” / „Dyżur zdał: …” (w wyciągu dokleja „dnia … o godzinie …”). Role: `Koordynator` (administrator), dyżurny, `Obserwator` (tylko odczyt, „TRYB OBSERWATORA”).

### 3.9 Dróżnicy (`CrossingRules`)
Ustawienia przejazdu: `Mark` / `Kilometre` / `Name` (oznaczenie w kolumnie), `AlongTrack` (Jednotorowych / Dwutorowych / Wszystkich), `NotificationMode` (Godzina / ukośnik), `Trains` (Parzystych / Nieparzystych / wszystkich), `InJournal`.
Powiadomienie wymagane **tylko przy wyprawieniu** (`DO`), nie przy powrocie ze szlaku, z filtrem parzystości. Ukośnik `/` albo godzina. **Uwaga: w ich kodzie „Dwutorowych” = godzina, „Jednotorowych” = ukośnik** – u nas (E5) odwrotnie. Do sprawdzenia z właścicielem / podręcznikiem rozdz. 7.4.

### 3.10 Jazda do km i z powrotem (`WorkTrip`, `ReturnOfId`)
Cztery gotowe szablony uwag (po zamkniętym torze z postojem / do km i z powrotem / do km / z wjazdem w km do posterunku). Wiersz powrotu dziedziczy tor szlakowy, w wyciągu jest zawsze bezpośrednio pod wierszem wyjazdu, wykreślenie jednego wykreśla parę.
→ U nas jest (E5). **Szablony tekstu uwag** – warto dodać jako listę do wyboru.

---

## 4. Telefonogramy (`TelephoneTemplates`) – gotowe wzory

22 wzory z numerami wg Ir-1 (to odpowiedź na naszą otwartą sprawę „wzory poda właściciel” – **do pokazania właścicielowi jako propozycja, nie wpisywać samemu**):

| Nr | Treść | Skutek w stanie szlaku |
|---|---|---|
| 9 / 10 | wstrzymanie / odwołanie wstrzymania wyprawiania pociągów | – |
| 11 / 12 | nie wyprawiać pociągu X / żadnego do przejazdu pociągu z pierwszeństwem | – |
| 16 / 16a | wprowadzenie telefonicznego / radiotelefonicznego zapowiadania po torze | `Telefoniczne` |
| 17 / 17a | odwołanie zapowiadania | `Normalne` |
| 21 / 21a | zamknięcie toru od godziny / po przejeździe pociągu (z powodu / celem) | `Zamknij` / `ZamknijPoPrzejezdzie` |
| 022 | zakończenie robót (ograniczenia prędkości, kierownik robót) | – |
| 22 | otwarcie toru | `Otwórz` |
| 23 / 24 | wprowadzenie / żądanie ruchu jednotorowego dwukierunkowego (ostatni pociąg, godzina) | `RuchJednotorowy` |
| 25 | przywrócenie ruchu dwutorowego | `RuchDwutorowy` |
| 26 / 27 | ruch dwukierunkowy przy czynnych torach / przywrócenie jednokierunkowego | `RuchDwukierunkowy` / `RuchJednokierunkowy` |
| 28 / 29 | posterunek otwarty / zamknięty | – |
| 30 | zatrzymać pociągi – pociąg bez sygnału końca | – |
| 31 / 32 | wstrzymanie / przywrócenie trakcji elektrycznej na torze | `TrakcjaWylaczona` / `TrakcjaPrzywrocona` |
| OWN | bez szablonu | – |

Budowa: wzór = funkcja `Body(dane)` składająca zdanie z pól (`EffectiveAt`, `Track`, `TrainNumber`, `PassingTrainNumber`, `Reason`, `ForPurpose`, `ClosureMode`, `LastTrainDeparture`, `SpeedRestrictions`, `WorkSupervisor…`), kierunek „od A do B” zależnie od nadaję/odbieram, oraz `Effect(id)` – skutek dla stanu szlaku z momentem skuteczności.
→ Nasz plan (`Dane/telefonogramy.json` z polami) pasuje do tego modelu: **wzór = tekst z polami + skutek + godzina skuteczności**. Ich kod twardo w C#; my lepiej w JSON, jak planujemy.

---

## 5. Okno i wygląd

### 5.1 Układ (`MainForm`, `JournalPane`)
- U góry **wykaz pociągów** (kolumny: K, NK, Przyjazd plan., +/-, Przyjazd rzecz., Rodz., Nr poc. nieparzysty, Z kierunku post., Nr poc. parzysty, W kierunku post., Linie, P. obl., P. zam., Typ post., Peron/Tor, Odjazd plan., +/-, Odjazd rzecz., Stacja początkowa, Stacja końcowa, Przewoźnik).
- Dzienniki: **„Dzienniki na dole” / „z prawej” / „w osobnym oknie”** (ustawienie `Layout`), zoom UI (`UiZoom`), „Otwórz drugie okno”.
- Kolumny dziennika ruchu: Nr pociągu (nieparzysty, parzysty), Tor stacy., Droga wolna, Poc. odjechał, Poc. przyjech., Uwagi, „O jeździe pociągu zawiadomiono” (podkolumny na dróżników).
- Dziennik telefoniczny: Nr kol., Data, Adres i treść, Czas zakończenia, Nadał / Odebrał.
- Panel szczegółów pociągu (zakładki): Opis pociągu, Trasa (29 kolumn, jak w SEPE), Plan przejścia składu (Z / NA pociąg), Opóźnienia (IZ opóźnienia pierwotnego, kody opóźnień), Przesyłki nadzwyczajne i TWR, Dane ładunku (UN/RID), Usługi (wnioski, zgody), Skomunikowania (pociągi ZDĄŻAJĄCE / OCZEKUJĄCE).
- Polecenia na pociągu: „Poc. STÓJ + Prognoza…”, „Odwołaj pociąg STÓJ / Prognoza…”, „Gotowość pociągu do odjazdu”, „Analiza pociągu”, „Brak zgody na usługę”, „Lokalny opis pociągu”.
- „Bieżąca sytuacja ruchowa na szlakach”, „Licznik: Czas bez wypadku spow. przez pracownika PLK”, legenda.

### 5.2 Kolory (z `EdrPalette` i legendy) – do porównania z naszymi z filmu
| Znaczenie | RGB |
|---|---|
| Przyjazd (tło) | 215,228,242 |
| Odjazd (tło) | 255,243,232 |
| Nr parzysty / jakość (zielony) | 192,220,192 |
| Postój | 255,211,168 |
| Bez postoju | 227,227,227 |
| Peron | 166,202,240 |
| **Pole wymagane** | 151,186,205 |
| Krem (formularze) | 255,255,225 |
| TN (towary niebezpieczne) | 190,86,133 |
| TWR | 255,128,64 |
| Nagłówek kolumny (gradient) | 255,239,199 → 253,212,168 |
| Wstęga zaznaczenia | 51,153,255 |

Legenda (`LegendForm`): intermodalne do 120 km/h, skomunikowania (`Nr pociągu*`), przesyłki nadzwyczajne, TN, TWR, postój handlowy, POCIĄG STÓJ, POCIĄG STÓJ z prognozą, uwagi przewoźnika, wstęga (przyjazd – kończy bieg / odjazd – rozpoczyna / przejazd bez zatrzymania / z postojem), typ postoju: ph – handlowy, pt – manewrowy.

### 5.3 Wyciąg (`JournalReport`)
- Rysowany w GDI na A4 (827×1169 px), czcionka **Times New Roman**, nagłówki z kratkami jak w druku R-146 (g. / m. pod każdą godziną, „nieparzysty / parzysty”).
- „Wyciąg z dziennika ruchu|telefonicznego [z historią zmian]”, „Posterunek:”, „za okres:”, „Szlak z i do:”, wiersze „Północ dnia …”, „Brak wpisów w wybranym okresie.”, wykreślone z „Wykreślono: przyczyna”, stopka „n / m”, podpis „Wyciąg sporządził: … Jednostka: … dnia … godz. …”.
- Zapis: **PDF** (własny `RasterPdf` – strony jako obrazy), **CSV**, **HTML** (strony jako PNG base64), druk.
→ U nas E6 zrobione; brakuje zapisu do pliku – ich sposób (PDF z obrazów stron, bez bibliotek) jest najprostszy do zrobienia samemu.

---

## 6. Integracja z grami (nam niepotrzebna, ale pokazuje skąd dane)

### SimRail (publiczne API)
| Adres | Dane |
|---|---|
| `api.simrail.eu:8084/servers-open` | serwery |
| `api.simrail.eu:8084/stations-open?serverCode=` | posterunki obsadzone |
| `api.simrail.eu:8084/trains-open?serverCode=` | pociągi w ruchu (pojazdy) |
| `api.simrail.eu:8082/api/getEDRTimetables?serverCode=` | **rozkład pod EDR** (punkty z planem, rzeczywistym, `isConfirmed`, `isStoped`, `stopTypeNumber`, peron, tor, linia) |
| `api.simrail.eu:8082/api/getAllTimetables` | plan składu |
| `api.simrail.eu:8082/api/getTime`, `getTimeZone` | czas gry |

Każdy wiersz wykazu ma klucz `sr:serwer:nrPociągu:punkt:indeksPunktu:data` – **ten sam pociąg przejeżdżający dwa razy przez posterunek ma dwa wiersze** (indeks punktu w trasie). To rozwiązuje nasze ograniczenie E2/2 (pociąg wracający przez ten sam posterunek).

### SEPE (oficjalny EDR w grze)
Link z gry `https://api.simrail.eu:8093/?serverCode=…&stationId=…&key=…` (tylko hosty simrail.eu, porty 8092/8093). EDR.exe otwiera go w **ukrytym WebView2** (tryb prywatny, bez DevTools, blokada nawigacji poza simrail) i skryptem `SepeBridge.js` **wypełnia okno „enterTimeModal”** (data, godzina, minuty przyjazdu/odjazdu), sprawdza numer pociągu i plan, klika „Zapisz” / „Zapisz z postojem”, wprowadza/odwołuje POCIĄG STÓJ. Kolejka z ponawianiem (`SepeRetry`, `SepeChronologyRetry`).

### TD2 (Train Driver 2)
`api.td2.info.pl/?method=getTrainsOnline` i `readFromSWDR&value=getTimetables;<region>` – rozkłady i pociągi online; numery bez zmiany parzystości.

### Licznik
`edr-licznik.jever.pl/api/v1/counters/<simulator>` – wspólny licznik „czas bez wypadku”.

---

## 7. Wnioski dla naszego EDR (do decyzji właściciela)

Mamy już E1–E7. Z EDR.exe warto **rozważyć** (nic z tego nie jest zrobione):

1. **Zapis dzienników do pliku** – `edr.json` obok układu / sesji, zapis przez `tmp` + `File.Replace` z `.bak`, walidacja wersji przy odczycie, przy błędzie nie nadpisywać oryginału. (Otwarte w § 7 planu.)
2. **Wykreślenie wpisu z przyczyną** zamiast kasowania; wpisów o dyżurze nie można wykreślać; wykreślony – tylko do odczytu, w wyciągu „Wykreślono: …”.
3. **Tor szlakowy z uwag**: „po torze nr N”, kontrola sprzeczności z lewym/prawym i z liczbą torów.
4. **Droga wolna wymagana także przy ruchu jednotorowym i przy zamkniętym torze** (nie tylko przy blokadzie półsamoczynnej/telefonicznej).
5. **Wpis godziny bez dwukropka** („0812”) i automatyczne przejście przez północ (poprzednia ≥ 18:00, nowa < 06:00 → następna doba).
6. **Wzory telefonogramów** – lista 22 wzorów z § 4 jako propozycja dla właściciela; model: tekst z polami + skutek (`Zamknij`, `Telefoniczne`, `RuchJednotorowy`, `TrakcjaWylaczona` …) + godzina skuteczności. „Zamknięcie po przejeździe pociągu” – skuteczne z chwilą przyjazdu tego pociągu.
7. **Dróżnicy – sprawdzić regułę godzina / ukośnik**: w EDR.exe „Dwutorowych” = godzina, „Jednotorowych” = ukośnik (u nas odwrotnie). Powiadomienie tylko przy wyprawieniu, z filtrem parzystych/nieparzystych.
8. **Szablony uwag dla jazdy do km** (4 gotowe teksty) – do wyboru w oknie przyjęcia/wyprawienia.
9. **Wiersz wykazu na każdy przejazd przez posterunek** (klucz z indeksem punktu trasy) – rozwiązuje ograniczenie E2/2 i E7 „trasa – jeden plan na posterunek”.
10. **Układ okna**: dzienniki na dole / z prawej / w osobnym oknie, zoom.
11. **Tryb obserwatora** (rola tylko do odczytu, np. dla instruktora na drugim stanowisku).
12. **Zapis wyciągu do pliku**: PDF z obrazów stron (bez zewnętrznych bibliotek) i CSV.
13. Poza planem, ale EDR.exe ma: POCIĄG STÓJ z prognozą, gotowość do odjazdu (godzina zgłoszenia, wpisania, maszynista), plan składu (seria, długość, brutto plan./rzecz.), skomunikowania, usługi, kody opóźnień. Wymagałyby danych z rozkładu/edytora rozkładu.
14. **Testy wbudowane w exe** z przełącznikiem na każdy etap – mamy to samo (`--edrtest`), potwierdza dobry kierunek.

Czego **nie** przejmować: audytu z pełnym stanem przed/po przy każdej zmianie (plik rośnie bardzo szybko; nasze wersje w wierszu są lepsze), twardo zaszytych wzorów w kodzie, logiki SEPE/WebView2 (dotyczy tylko gry).
