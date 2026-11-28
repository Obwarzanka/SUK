# Plan: przejazdy – urządzenia zdalnej kontroli (powtarzacze) i SSP

Stan: **P0 w toku (2026-11-21)** – właściciel wybrał start od P0. Mod SimRailMost 0.11.2: `DiagPrzejazdy.cs` (pełny stan przejazdu z pulpitu i ze świata 3D do `stany_diag_*.jsonl`, rodzaje `przejazdP0` / `przejazdP0swiat`, tylko odczyt, bez łatek), raport `tools/simrail/diagnostyka/przejazdy_p0.py` (dawniej `SimRailMost/narzedzia`) → `UserData/SimRailMost/przejazdy_P0.md`. Polecenia z pulpitu gry łapie istniejący podsłuch `SendRPCCall`. Wgrane do Mods 2026-11-21 20:42 (Release). Pierwsza gra (20:54): każdy wpis `przejazdP0` = NullReferenceException – `CrossingStateStruct` ma w grze `op_Equality`, więc `v == null` rzuca; poprawione (`is null`, każde pole osobno) i dodane przejazdy 3D (`CrossingNetworkHolder`, co 10 s, rodzaj `przejazdP0_3d` – także przejazdy spoza pulpitu). Wgrane 21:12, **sprawdzone w grze 22:25–22:46 – działa**; tabela barw w § 2a. Podgląd na żywo: `tools/simrail/diagnostyka/przejazdy_podglad.py`. **P0 zrobione** poza awarią SSP i `SwitchOffTracks` (nie wystąpiły). Pozostałe etapy – nie zaczęte.

**Stan 2026-11-22 (00:20) – powtarzacz jako OSOBNY PROGRAM** (poprawka właściciela: „powtarzacze miały być w osobnym programie”), nie sprawdzone w grze:
- **`app/Lcs.Powtarzacz`** (`Powtarzacz.exe`, `uruchom_powtarzacz.bat`, w `Lcs.slnx`): pola przejazdów – symbol § 13 + części § 14 (3, 4a, 4b, 5), alarm awarii z kwitowaniem (F5), dziennik zdarzeń i usterek (plik w `%LOCALAPPDATA%\SUK\Powtarzacz`), wybór przejazdów (domyślnie: UZK z pulpitu gry + SSP kat. B/C do 15 km, np. LK1 do Rokicin). `--demo`, `--shot`. Obsługa – README „Powtarzacz zdalnej kontroli przejazdów”.
- **Mod 0.12.0** (w Mods 00:20): `PowtarzaczUdp.cs` – co 1 s stan wszystkich przejazdów (pulpit + `CrossingNetworkHolder`) na UDP 127.0.0.1:47134 (`UdpPortPowtarzacz`, 0 – wyłączone), typ „przejazdy”, części po 80. Dla SUK dalej pole `pzd` (barwy).
- **SUK (LcsWidzew)** – tylko barwy wg § 13: rogatki w ruchu różowe migające (ruch z rogatek wj/wy, bo gra nie ustawia Switch w stanie ogólnym), utwierdzony – końce żółte, uszkodzony – czerwony ⇄ biały, km szary. `CrossingDef` + `cat`, `game` (jawna nazwa w grze). Wyszarzanie ZAMK/OTW i Akt/deAkt/OtwAw w SUK **cofnięte** – powtarzacz jest w osobnym programie.
- Selftest bez zmian (25 z 26, 5 z 5); powtarzacz sprawdzony na demo i na datagramach testowych UDP.
- **Zostaje:** test w grze; P6 (własny symulator przejazdu – może zasilać ten sam program powtarzacza); P5 (książka E1758 – dziś plik dziennika).

**Widok RASP-UZK (2026-11-22, prośba właściciela „zobacz jak działa UZK Kombudu RASP albo Elestera i spróbuj odwzorować”):**
- Źródła: Ie-119 § 18 (UZK: usterki krytyczne / kat. 1 / kat. 2, alarm dźwiękowy do potwierdzenia – cichy, po ≤ 1 min głośny; rejestrator ≥ 30 dni; polecenia specjalne dwuetapowe: wył./wł. czujników toru, aktywacja / deaktywacja; bez resetu z UZK; ≤ 10 ssp na UZK); DTR-2002/RASP-4F 2.0 (Kombud: RASP-UZK – PC przemysłowy, ≤ 8 ssp, 3 ekrany, polecenia „Zezwolenie na ster. LOKALNE”, „Sterowanie AUTOMATYCZNE”, „T1/T2 ZAŁ./WYŁ.”, „ZEROWANIE”, komunikaty tabele 5–8, „Zaakceptowano !” Ctrl+Enter, archiwum 6 mies.); wiki TD2 „Instrukcja RASP-UZK” (barwy pól 4.4Ft – nieoficjalne). Elester ERP-6 / ERP-7 (SPA-5): tylko „kolorowy ekran dotykowy LCD” – opisu ekranu brak, nieodwzorowany.
- `Lcs.Powtarzacz/RaspPanel.cs`: EKRAN I / II / III, komunikaty RASP przy zmianie stanu, przełącznik widoku (ustawienie `Widok`, `--widok rasp`). Mapowanie z gry: Broken → usterka kat. I, brak danych 5 s → BRAK TRANSMISJI, sygnały → OSTRZEGANIE / OCZEKIWANIE i S12, rogatki wj / wy → N1 / N3, SwitchOffTracks → TOR I / II WYŁ.; STEROWANIE stale „OK auto”, ZASILANIE „OK 230V”.

**Rozpoznanie stref i sterowania (2026-11-22, kod gry, bez wysyłania):**
- Konfiguracja przejazdu w danych sceny: `SceneryRailroadCrossing.AwareTracks` / `KonfiguracjaPrzejazdu` – lista stref: `AwareSignal` (sygnalizator), `AwareTrack` (tor), `AwareDir` (kierunek), `AwareACC` + `AwareAccPosition` (licznik osi i jego położenie). Trzyma ją serwer SRK (`Il2CppSerwer`: `DataSRK.PhysicalModelCollection`, `PhysicalCrossing` z `UszkodzenieElementuPrzebiegu()`, `KontrolerLicznikaOsi`). W grze wieloosobowej serwer jest u SimRail – na komputerze dyżurnego najpewniej pusto (sprawdza to mod 0.12.1, `przejazdStrefy`/`srk`).
- Na kliencie: `Crossing.trainNearby` (pociąg w pobliżu), `ClosedId` – mod 0.12.1 zapisuje przy zmianie; liczniki osi z pulpitu gry – `przejazdStrefy`/`liczniki`.
- Polecenie przejazdu do serwera: `SendRPCCall` → `SendMessage` (NetMQ) 7 ramek: `03`, `02` (IndividualOrder), `02`, nazwa („L1_103.742_A”), kod polecenia („K” – zamknij), id (8 B), napis. ZAMK z SUK na 102,026 (2026-11-21 20:55) **nie wyszedł do serwera** – wyjątek w kliencie przed `SendMessage`. Czy serwer przyjąłby ramkę dla przejazdu SSP – nieznane (logika serwera w kodzie natywnym); sprawdzić można tylko próbą (decyzja właściciela – serwer publiczny).
- Właściciel zgodził się na **jedną próbę ZAMK** (2026-11-22). Mod 0.12.2 (`ProbaSerwera.cs`, w Mods 00:34): plik `UserData/SimRailMost/proba_serwer.txt` z „ZAMK L1_102.026_B” → jedna ramka jak z pulpitu gry (id = ostatnie id gry + 1), wynik w logu i `stany_diag` („probaSerwera”). Wynik próby: **jeszcze nie wykonana**.

**Ustalenia z gry (2026-11-21):** w Koluszkach przejazdy 102,026, 102,272 (linia 1) i 21,996 (linia 17) są na pulpicie z `hideOrders` + `hideAuth` – sam stan, bez poleceń i bez obszaru obsługi, czyli w praktyce **powtarzacz (UZK)**, choć flagi `remote` / `automatic` mają `false`. ZAMK z SUK na 102,026 – gra rzuca NullReferenceException w `SendRPCCall` (brak okna poleceń dla takiego przejazdu) → w SUK (P4) dla przejazdu z `hideOrders` nie pokazywać ZAMK / OTW. Właściciel: na trasie jest pewnie więcej przejazdów niż na pulpicie – lista z `przejazdP0_3d`.

Prośba właściciela: „zapoznaj się z powtarzaczami i urządzeniami zdalnej kontroli do przejazdów kolejowych i przygotuj plan odwzorowania tego”.

## 1. Co mówią przepisy (źródła w `Zrodla/`)

**Urządzenie zdalnej kontroli (UZK, „powtarzacz”)** jest częścią samoczynnej sygnalizacji przejazdowej (SSP):
- Poradnik dla inspektorów, t. I, rozdz. o SSP: elementy SSP to aparatura sterująco-kontrolna, urządzenia oddziaływania (czujniki), urządzenia dla użytkowników drogi, **urządzenia zdalnej kontroli (powtarzacza)**, powiązania z urządzeniami kontroli prowadzenia pociągów (TOP) i zasilanie.
- Ie-20 § 11: urządzenia zdalnej kontroli SSP są plombowane. Ie-20 § 21 ust. 8: przy obsłudze z nastawni rogatek albo **powtarzacza zdalnej kontroli działania SSP** usterki i naprawy wpisuje się do wspólnej książki kontroli urządzeń.
- Kolejność działania SSP (Poradnik): czujnik włączający w strefie zbliżania → ostrzeganie i zamykanie półrogatek (kat. B) → po zjechaniu z czujnika wyłączającego, z opóźnieniem 5–6 s: najpierw półrogatki wyjazdowe, potem wjazdowe, potem gaszenie świateł. Wyłączenie dźwięku po wjeździe pierwszej osi na czujnik strefy 2.

**Zobrazowanie na pulpicie LCS / EBI (Ie-104):**

| Urządzenie | Symbol i barwy | Polecenia (Ie-104, tabele poleceń) |
|---|---|---|
| **Przejazd obsługiwany** (rogatki z nastawni), § 13 | symbol przejazdu + część „3a” (utwierdzenie w przebiegu). Barwy wg priorytetu: biały – brak danych, **różowy** – otwarty, **różowy migający** – stan pośredni rogatek, szary – zamknięty; „3a” żółta – przejazd utwierdzony w przebiegu. Opis: km (pełne km) albo numer, szary. | Zamk, Otw, **Przerwa** (zatrzymanie ruchu zapór) – zwykłe; **ZamkAw**, **OtwAw** – specjalne |
| **SSP z UZK** (przejazd samoczynny), § 14 | część „3” – gotowość SSP, „4a / 4b” – część dla każdego toru, „5” – awaryjne załączenie ostrzegania. Stan podstawowy – wszystko szare. Awaria SSP (brak gotowości) – „3” na przemian czerwony / biały. Manualna aktywacja dla toru (wyjazd na sygnał zastępczy) – „4a/4b” czerwone. Wyłączone czujniki toru – „4a/4b” białe. Manualne załączenie ostrzegania (przełączenie na tryb obsługiwany) – „5” czerwone. Brak danych – wszystko białe. | **Akt**, **deAkt** (dla toru), ZamkAw, OtwAw – specjalne |
| SSP na obrazie poglądowym | sam symbol: awaria – czerwony migający na przemian z białym, gotowość – szary; bez opisów | – |

Dla porządku: **powtarzacze sygnalizatorów pociągowych** (Ie-104 pkt 5, trójkąt z pustym wnętrzem) to inna rzecz – SUK już je ma (`sp` / `to`, pole „Powtarza” w UT). Ten plan dotyczy powtarzacza **stanu przejazdu**.

## 2. Co ma gra (SimRail) – z metadanych `MelonLoader/Il2CppAssemblies` i zrzutów pulpitu

- **Element pulpitu `EBI300RoadCrossing`** – flagi: `automatic` (SSP), **`remote` (zdalna kontrola)**, `lights`, **`hasTOP`** (tarcza ostrzegawcza przejazdowa), `hideOrders`, `hideAuth`, `height`.
- **Renderer `EBI300RoadCrossingRenderer`**: `roadCrossingState` (`CrossingStateStruct`), `confirmed`, `manualReleased`, `closedWithoutConfirmationTimer` + `closedTimeIndicator` (licznik czasu zamknięcia bez potwierdzenia), `authLabel` / `authAreaState` (przejazd jako obszar obsługi: Mine / Other / Free / UPA / Requesting…), części `barriers`, `lights`, `body`, `mask`.
- **`CrossingStateStruct`**: `StateSignals`, `StateBarriersEntrance`, `StateBarriersExit`, `StateOverall`, `VelocityHere`, `VelocityNext`, `SwitchOffTracks` (tory z wyłączonym oddziaływaniem → „4a/4b”).
- **`CrossingState`** (flagi): Unknown 1, Open 2, Switch 4, Close 8, Hold 32, Broken 128.
- **`RailRoadCrossingType`** (flagi): Samodzielny 2, Automatyczny 4, Otwarty 16; złożone A = 26, BC = 22, StationA = 25, StationBC = 21, F = 42. Bity 1, 8, 32 bez nazwy (prawdopodobnie „stacyjny”, „rogatkowy”, „F”) – do potwierdzenia.
- **Konfiguracja stref**: `SceneryRailroadCrossingAwareTrack` – `AwareSignal`, `AwareDir`, `AwareTrack`, `AwareACC`, `AwareAccPosition` (sygnalizator / licznik osi włączający przejazd).
- **Polecenia `Il2Cpp.Order`**: OpenCrossing 74, CloseCrossing 75, EmergencyCloseCrossing 76, **SwitchOffTOP 77**, **ManualReleaseCrossingRouteConfirmation 78**, CloseEntryBarriers 93, CloseExitBarriers 94, OpenEntryBarriers 95, OpenExitBarriers 96, **StopBarriersSwitching 97** (= Przerwa).
- Serwer: `PhysicalCrossing` (`utwierdzony`, `skasowane_przestawianie`), `PrzejazdDrogowy` (`typ_przejazdu`, `Stan`).
- **Koluszki (LEWA)**: 6 przejazdów, wszystkie `remote = false`, `automatic = false`, `hasTOP = false`; 24.396 i 25.357 – `lights = true`; 102.026, 102.272, 21.996 – `hideOrders`, `hideAuth` (tylko podgląd). Przy 103.742 są obiekty-sygnalizatory `1803_KO_*kps` (A, B, M, N, R1, R2, T, U1, U2) – znaczenie nieznane (powiązanie przebiegów z przejazdem?).
- **W grze wieloosobowej pole stanu jest 0** – mod czyta barwy symbolu (`Stany.WidokPrzejazdu`). Dla SSP (części 4a/4b, TOP, licznik) barwy trzeba dopiero rozpoznać.

**Nie mamy jeszcze ani jednego zrzutu przejazdu z `remote` / `automatic` / `hasTOP`.** To pierwsza rzecz do zrobienia.

## 2a. Wynik P0 – barwy pulpitu gry → znaczenie (gra 2026-11-21 22:25–22:46, Koluszki, 148 zmian)

Wszystkie 6 przejazdów pulpitu Koluszek (3 obsługiwane: 103,742, 24,395, 25,537; 3 UZK: 102,026, 102,272, 21,996) – pełne cykle zamknięcie / otwarcie. Odczyt co 1 s, więc miganie widać jako przemienność barwy i „ukryta”.

| Stan w grze (`StateOverall` / sygnały / rogatki wj, wy) | Rogatki (`barriers`) | Korpus (`body`) | Światła (`lights`, tylko 24,395 i 25,537) | Inne | Znaczenie wg Ie-104 § 13 | Zgodne? |
|---|---|---|---|---|---|---|
| 0 (brak danych; start, wyjście z gry) | biała | biały | biała | tło białe | brak danych | tak |
| Open / Open / Open, Open | różowa | różowy | ukryta | – | otwarty | tak |
| sygnały Close lub Switch, rogatki w ruchu (Switch) | różowa ⇄ ukryta | różowy ⇄ ukryty | czerwona (od zamknięcia sygnałów) | – | stan pośredni rogatek – różowy migający | tak |
| Close / Close / Close, Close | szara | szary | czerwona | – | zamknięty | tak |
| jw. + `confirmed` | **żółta** | szary | czerwona | – | utwierdzony w przebiegu („3a” żółta) – w grze żółknie część rogatek | tak (inne miejsce „3a”) |
| otwieranie (rogatki wy, potem wj → Switch) | różowa ⇄ ukryta | różowy ⇄ ukryty | czerwona, gaśnie przy Open | – | stan pośredni | tak |

Ustalenia:
- **Powtarzacz (UZK) w grze ma ten sam symbol co przejazd obsługiwany** (§ 13), tylko bez poleceń i bez napisu obszaru (`napisObszaru` „-”; obsługiwane: „MIEJSCOWY”, przed wczytaniem „LOKALNY”). Części SSP z § 14 (3 gotowość, 4a/4b tory, 5 awaryjne ostrzeganie) **nie występują** – SUK musi je wyliczać ze stanu: „3” ← brak `Broken`, „4a/4b” ← `SwitchOffTracks`, „5” ← sygnały Close bez pociągu (do ustalenia).
- **Nie wystąpiły**: `Broken` (awaria), `Hold`, `manualReleased`, licznik zamknięcia bez potwierdzenia, niepuste `SwitchOffTracks`, napis obszaru inny niż MIEJSCOWY. Barw awarii nie znamy.
- Kolejność zamykania: sygnały → rogatki wjazdowe → wyjazdowe; zamknięcie trwa ok. 8 s, potwierdzenie ok. 2 s później; otwieranie: wyjazdowe → wjazdowe, ok. 7 s.
- Nazwa w świecie kończy się kategorią: `L1_102.026_B` – **102,026 i 102,272 to kat. B (SSP z półrogatkami)**, czyli UZK = powtarzacz SSP zgodnie z przepisami. 21,996 – kat. A.
- Świat 3D: 483 przejazdy na mapie. Na LK1 Koluszki → Rokiciny: 108,644 B, 109,347 A, 110,905 B, 114,334 A – działają, ale nie ma ich na żadnym pulpicie gry.
- SUK dziś: przestawianie żółte migające – **niezgodne** (gra i Ie-104: różowe migające); brak żółtej „3a” przy `confirmed`.

## 3. Co jest dziś w SUK i modzie

- Mod SimRailMost: `pz` (StateOverall albo stan z barw), `pzo` (opis barw), polecenia ZAMK / OTW / AZAMK (`Polecenia.cs`).
- SUK: `CrossingDef` (Name, Kind Y/T, X, Y0, Y1), `CrossInd` (Stan, Info), dopasowanie do gry po km (≤ 0,5 km), panel ZAMK / OTW.
- Różnice z Ie-104 już teraz: przestawianie rogatek jest **żółte** migające (przepis: **różowe** migające), brak części „3a” (utwierdzenie w przebiegu), brak symbolu SSP, brak TOP, brak alarmu awarii i kwitowania.
- Własny symulator: ROZWOJ.md etap 6 „przejazdy (SSP, dróżnicy)” i `KartaPrzejazdu` w PODZIAL_MODULOW.md – nie zaczęte. UT ma przejazd z kategorią A–F / P i rogatkami.

## 4. Etapy

| Etap | Zakres | Gotowe, gdy |
|---|---|---|
| **P0 – rozpoznanie w grze** (mod) | Zrzut pełnego stanu przejazdu do `stany_diag`: cała `CrossingStateStruct`, `confirmed`, `manualReleased`, `closedWithoutConfirmationTimer`, `authAreaState`, flagi elementu, barwy wszystkich części (`lights`, `mask`, `closedTimeIndicator`). Przegląd zrzutów pulpitów innych posterunków w poszukiwaniu `remote` / `automatic` / `hasTOP`. Podsłuch poleceń z pulpitu gry na takim przejeździe (SwitchOffTOP, ManualRelease…, StopBarriers, rogatki wjazdowe / wyjazdowe). | tabela: barwa / pole gry → znaczenie wg Ie-104, lista poleceń przyjmowanych przez grę |
| **P1 – model** | `CrossingDef` rozszerzony: rodzaj (obsługiwany / SSP), kategoria, `Zdalna` (UZK), tory „4a/4b”, `Top`, jawna nazwa w grze (zamiast dopasowania po km). UT → „Do symulatora…” → układ; edytor zobrazowania: odmiana symbolu „przejazd SSP”. | stary układ czyta się bez zmian; nowy zapisuje pola |
| **P2 – protokół mod → SUK** (mod 0.12) | Nowe pola UDP (zgodne wstecz): stany sygnałów / rogatek wjazdowych / wyjazdowych, `SwitchOffTracks`, potwierdzenie, zwolnienie ręczne, licznik, flagi typu. | SUK starszy ignoruje pola, nowy je pokazuje |
| **P3 – pulpit SUK: zobrazowanie** | Przejazd obsługiwany dokładnie wg § 13 (różowy migający, „3a” żółta). SSP wg § 14 (3 / 4a / 4b / 5) i obraz poglądowy. **Powtarzacz UZK**: awaria SSP → alarm dźwiękowy + zdarzenie, **kwitowanie** awarii przez dyżurnego, licznik czasu zamknięcia jak w grze. | zrzuty porównane z Ie-104, zdarzenia w historii |
| **P4 – polecenia** | Zamk / Otw (są), **Przerwa** → StopBarriersSwitching, ZamkAw → EmergencyCloseCrossing, **Wył. TOP** → SwitchOffTOP, potwierdzenie → ManualReleaseCrossingRouteConfirmation, rogatki wjazdowe / wyjazdowe osobno (tylko jeśli P0 pokaże, że gra je przyjmuje). Akt / deAkt / OtwAw – gra nie ma odpowiedników; pokazać jako niedostępne albo odwzorować (decyzja). Polecenia specjalne czerwone, z potwierdzeniem jak SZI; napis historii jak w grze („ZAMK 103.742”). | polecenia działają w grze, niemożliwe → „Odebrane – niemożliwe” |
| **P5 – książka kontroli urządzeń** | Ie-20 § 21 ust. 8: awaria SSP z UZK i jej naprawa jako wpis (w zdarzeniach albo w EDR, jeśli powstanie książka E1758). | wpis przy awarii i po ustąpieniu |
| **P6 – własny symulator (bez gry)** | `KartaPrzejazdu` w `Sim/Urzadzenia`: czujniki włączające / wyłączające z UT (tor, kierunek), czas ostrzegania, opóźnienie 5–6 s, kolejność półrogatek, TOP, usterki instruktora (awaria, wyłączone czujniki), manualna aktywacja przy sygnale zastępczym, uzależnienie przejazdu obsługiwanego z przebiegiem (utwierdzenie „3a”). To jest etap 6 z ROZWOJ.md. | selftest bez regresji (25 z 26, 5 z 5), testy karty przejazdu |

Kolejność: P0 → P2 → P3 → P4 dają działający powtarzacz w pulpicie zdalnym SimRail. P1 jest potrzebny przed P3 tylko dla układów rysowanych w edytorach. P6 jest niezależny od gry.

## 5. Do decyzji właściciela

**Decyzje (2026-11-22):**
1. Zakres: **P1–P4 i P6 razem**.
2. Rozwiązane w P0 – powtarzacze SSP są w Koluszkach (102,026, 102,272 kat. B).
3. Przestawianie rogatek w SUK → **różowe migające** (jak gra i Ie-104).
4. Akt / deAkt / OtwAw → **widoczne, wyszarzone, z dopiskiem „brak w grze”**.
5. (nowe) Przejazdy SSP z LK1 w stronę Rokicin (108,644 B, 110,905 B …), których nie ma na pulpicie gry → **SUK pokazuje je jako powtarzacze** (stan ze świata 3D, bez poleceń).
6. (nowe) **Powtarzacz UZK – osobny program** (nie w pulpicie SUK); stan z gry przez własny port UDP moda; C# WPF w `SUK/app`. W SUK zostają tylko barwy § 13.

Pierwotne pytania:

1. Zakres: najpierw pulpit zdalny SimRail (P0–P4), czy od razu też własny symulator (P6)?
2. Który posterunek SimRail ma przejazd z SSP i zdalną kontrolą? Potrzebny zrzut pulpitu z gry (mod robi go sam po wejściu na posterunek).
3. Barwa przestawiania rogatek: zmienić żółtą na różową migającą (Ie-104)?
4. Akt / deAkt / OtwAw – pokazywać jako niedostępne, czy odwzorować innymi poleceniami gry?
