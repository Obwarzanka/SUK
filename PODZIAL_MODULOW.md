# Podział klasy `Simulator` na moduły – plan (zaakceptowany)

Stan: plan zaakceptowany przez właściciela 2026-10-16. Zrobione: kroki 0–4 (2026-10-16), krok 5 i 6a bez sygnalizatorów (2026-10-17). Każdy krok kończy się kompletem testów (niżej).

Podstawa: `ROZWOJ.md` („Plan podziału programu na moduły”, etap 7), `Zrodla/EBILock_komputerownia.md` rozdz. 4 i 7.

---

## 1. Co jest dziś (z przeglądu kodu)

`Simulator` (partial: Simulator, Interlocking, Blocks, Commands, Desk, Meldunki) trzyma wszystko w jednym miejscu: `Secs`, `Sws`, `Sigs`, `Ders`, `Blocks`, `Routes`, `Power`, dziennik i alarmy, stanowiska. `Trains` to osobna klasa, ale sięga wprost do środka komputera zależności.

Przecieki, które podział musi usunąć (miejsca w kodzie):

| Kto | Co robi źle | Gdzie |
|---|---|---|
| Ruch | czyta `Sigs[].Aspect` (sygnał **zlecony** przez KZ, nie wyświetlany w terenie) | `Trains.Permits`, `Extend` |
| Ruch | ustawia `Sws[].Rozpr/Damaged/Pos` przy najechaniu | `Trains.Extend` |
| Ruch | ustawia `Secs[].TrainOcc`, liczy błąd licznika osi i pierwszy przejazd | `Trains.Occupancy` |
| Ruch | czyta utwierdzenia `Secs[].LockType` (gdzie wolno jechać manewrem, PK) | `Trains.InShuntRoute`, `Lookahead` |
| Ruch | czyta przebiegi `Routes` (prędkość VSet, cofanie manewru) | `Trains.EdgeLimit`, `ShuntReverseCheck` |
| Ruch | jedzie po `Sws[].Pos` (stan w KZ, nie iglice) | `Simulator.NextByState` |
| Ruch / otoczenie | sąsiedni posterunek czyta i pisze `Blocks[]` (żądanie Pzk) | `Trains.Neighbours` |
| Instruktor | usterki piszą wprost w stan KZ (`NoCtrl`, `Frozen`, `LampFault`, `NoData`, `Fault`) | `Trains.ApplyFault` |
| KZ | pyta pociągi „czy jest pociąg na szlaku” | `Blocks.cs:76` (`Trains.OnBlock`), `Blocks.cs:139` (`NbrPending`) |
| KZ | przy otwieraniu semafora zagląda w usterkę żarówki | `Interlocking.cs:174` (`LampFault`) |
| KZ | przekładanie zwrotnicy liczy sam (czas, zamarznięcie) | `Simulator.UpdateSwitches`, `MoveSwitch` |
| Pulpit | numery pociągów (okienka PIP) bierze z listy pociągów | `Meldunki.cs:125` |

Pulpit jest już oddzielony (`ILcsSession`, `Indication`) – to zostaje bez zmian, także format sieciowy.

---

## 2. Moduły i katalogi

Przestrzeń nazw zostaje `LcsWidzew.Sim` (mniej zmian w UI). Podział na katalogi i klasy:

| Moduł | Klasa (katalog) | Zawartość dzisiejsza → docelowa |
|---|---|---|
| **Pulpit / system nadrzędny** (EbiScreen) | `SystemNadrzedny` (`Sim/Pulpit/`) | parser poleceń (`Codes`, `Resolve`, `JoinNames`), polecenia dwuetapowe (`Pending`), meldunki `Publish`, stanowiska i obszary (`Desk.cs`), książka, PIP (numery pociągów) |
| **Rejestrator zdarzeń** (usługa wspólna) | `Rejestrator` (`Sim/Rejestrator.cs`) | `Log`, `Alarm`, `Remark`, `Events`, `Alarms`, zegar |
| **Komputer zależności** | `KomputerZaleznosci` (`Sim/Kz/`) | tablica przebiegów, nastawianie i zwalnianie, utwierdzenia, ochrona boczna, zastopowania i zamknięcia, Sz i liczniki czasu, logika blokady liniowej i SBL (sygnały odstępowe), rozstrzyganie „rozprucie / brak kontroli”. **Nie zna pociągów.** |
| **Urządzenia przytorowe** (sterowniki z kartami + teren) | `Urzadzenia` + `KartaZwrotnicy`, `KartaSygnalizatora`, `KartaOdcinka`, `KartaWykolejnicy`, później `KartaPrzejazdu` (`Sim/Urzadzenia/`) | czas przekładania, nadzór czasu, iglice, rozprucie mechaniczne, brak kontroli; zlecony → wyświetlany sygnał, włókna; licznik osi / obwód torowy, zerowanie, pierwszy przejazd; zasilanie (sieć, UPS, agregat, NN) |
| **Ruch pociągów** | `Trains` (`Sim/Ruch/`) | jazda, maszynista; widzi **tylko** urządzenia przytorowe |
| **Rozkład jazdy** | `Rozklad` (`Sim/Rozklad/`) | kolejka, scenariusze ruchu, `BaseTimetable`, `RealTimetable` (GTFS) |
| **Otoczenie** | `Otoczenie` (`Sim/Otoczenie/`) | sąsiednie posterunki spoza LCS: wyprawianie pociągów na szlak, odpowiedzi na Wbl/Pzk, blok końcowy, „posterunek nie odpowiada” |
| **Multiplayer i łączność** | bez zmian: `Net/LcsNet.cs`; telefon i przekazywanie obszaru – z `Desk.cs` do `SystemNadrzedny` | – |
| **Instruktor** | `Instruktor` (`Sim/Instruktor/`) | lista usterek, `ApplyFault` (kieruje usterkę do właściwego modułu), scenariusze usterek |

`Simulator` zostaje jako **fasada**: tworzy moduły, prowadzi cykl `Tick` i przekazuje dotychczasowe właściwości do UI i sieci, żeby okna nie zmieniały się razem z logiką. Po ostatnim kroku fasada jest cienka.

---

## 3. Komunikaty między modułami (interfejsy)

Wszystko w jednym procesie, wywołania synchroniczne w ustalonej kolejności cyklu. Komunikaty to małe rekordy – można je potem wysyłać przez sieć albo nagrywać.

### 3.1 Pulpit → KZ: polecenie systemowe
```csharp
record PolecenieSystemowe(string Kod, IReadOnlyList<string> Obiekty, string Stanowisko, bool Potwierdzone);
CmdResult KomputerZaleznosci.Wykonaj(PolecenieSystemowe p);
```
Pulpit robi tylko: parsowanie tekstu Ie-104, sprawdzenie obszaru sterowania stanowiska, dwuetapowość (inicjacja → potwierdzenie w czasie). KZ sprawdza warunki zależności i wykonuje.

### 3.2 KZ → Pulpit: meldunek
Bez zmian: `Indication` (`Meldunki.cs`). Budowany ze stanu KZ (zależności) + statusów kart (położenia, zajętości, sygnały wyświetlane, usterki).

### 3.3 KZ → Urządzenia: rozkazy
```csharp
void Urzadzenia.Rozkaz(RozkazZwrotnicy r);      // (Num, Cel "+"/"-")
void Urzadzenia.Rozkaz(RozkazSygnalizatora r);  // (Sig, Aspect: Stop/Train/Shunt/Sz, Sygnal: "S1".."S5"/null)
void Urzadzenia.Rozkaz(RozkazWykolejnicy r);    // (Name, Zalozona)
void Urzadzenia.Rozkaz(RozkazZerowania r);      // (Sec) – licznik osi, ZeroLO po potwierdzeniu
```
Utwierdzenie, zastopowanie, zamknięcie **nie idą do karty** – KZ po prostu nie wysyła rozkazu (EBILock_komputerownia 4.3).

### 3.4 Urządzenia → KZ: statusy (co cykl)
```csharp
record StatusZwrotnicy(string Num, string Pol /* "+", "-", null = brak kontroli */, bool Przekladanie,
                       bool PrzekroczonyCzas, bool Lokalnie, bool BrakDanych);
record StatusSygnalizatora(string Sig, Aspect Wyswietlany, string SygnalWyswietlany,
                           bool UszkZabraniajacy /* włókno główne czerwone */, bool NieZapalilZezwalajacego, bool BrakDanych);
record StatusOdcinka(string Sec, StanOdcinka Stan /* Wolny, Zajety, Uszkodzony, WymagaZerowania, Oczekuje */, bool BrakDanych);
record StatusWykolejnicy(string Name, bool? Zalozona /* null = w ruchu */);
record StatusZasilania(bool Siec, bool Ups, bool Agregat, bool NN);
```
KZ trzyma u siebie ostatni status (dzisiejsze `SwState.Pos`, `SecOcc` itd. stają się kopią statusu), więc kod przebiegów prawie się nie zmienia.

### 3.5 Ruch ↔ Urządzenia (fizyka, nie zależności)
```csharp
// Ruch → teren
void Urzadzenia.Osie(IReadOnlySet<string> zajeteTory);           // tory (edge) pod pociągami w tym cyklu
void Urzadzenia.Najazd(string zwrotnica, string zGalezi);         // najechanie od strony gałęzi (rozprucie, jeśli iglice inaczej)
// teren → Ruch
Aspect Urzadzenia.SygnalWidziany(string sig);                     // wyświetlany, ciemny = Stop
string Urzadzenia.Iglice(string zwrotnica);                       // "+", "-", null (w ruchu / rozpruta)
bool   Urzadzenia.WykolejnicaNaTorze(string name);
```
Ruch **nie czyta** `Routes`, `Secs`, `Sws`, `Blocks` komputera zależności.

### 3.6 KZ ↔ Otoczenie (blokada liniowa)
```csharp
// KZ → sąsiad
void Otoczenie.Blokada(string szlak, KomunikatBlokady k);  // Wbl, Pzk (odpowiedź), Zwbl, Po, Ko, Poz
// sąsiad → KZ
void KomputerZaleznosci.Blokada(string szlak, KomunikatBlokady k);
```
Sąsiad (`Otoczenie`) sam decyduje, czy dać pozwolenie (dziś `NbrPending`), wyprawia pociągi na szlak przez `Rozklad`/`Trains`, a o zajętości szlaku wie z urządzeń (odcinki szlakowe), nie z listy pociągów.

### 3.7 Instruktor → warstwa
```csharp
record Usterka(string Warstwa /* teren, karta, kz, otoczenie, zasilanie, lacze */, string Rodzaj, string Obiekt, bool Usun);
```
Przypisanie dzisiejszych usterek wg tabeli 7.4 `EBILock_komputerownia.md`:
`secOcc`, `loMiscount` → karta odcinka; `swNoCtrl`, `swFrozen`, `swRozpr` → karta/teren zwrotnicy; `sigLamp`, `sigNodata` → karta sygnalizatora; `mains*` → zasilanie; `nbrSilent` → otoczenie; `kzLink` → łącze pulpit–KZ.

### 3.8 Rejestrator (usługa wspólna)
`IRejestrator.Log(...)`, `Alarm(...)`, `Remark(...)` – dostają go wszystkie moduły. Zapis jak dziś (kolumny okna ZDARZENIA).

### 3.9 Kolejność cyklu `Tick(dt)`
1. Instruktor (usterki z kolejki scenariusza)
2. Rozkład → Otoczenie (wyprawienia, odpowiedzi blokady)
3. Ruch (jazda wg sygnałów **widzianych**, iglic) → `Osie`, `Najazd`
4. Urządzenia: przebieg czasu w kartach (przekładanie, zasilanie, liczniki) → statusy
5. KZ: statusy + polecenia → przebiegi, blokady → rozkazy
6. Urządzenia przyjmują rozkazy (skutek widoczny w następnym cyklu – jak w rzeczywistości)
7. Pulpit: meldunek

Dziś kolejność to: zasilanie, zwrotnice, blokady, pociągi, sygnały, przebiegi. Krok 6 dodaje opóźnienie jednego cyklu (0,2 s) między rozkazem a meldunkiem – sprawdzę, czy selftest nadal daje 25 z 26.

---

## 4. Kolejność kroków

Każdy krok to osobny commit. Warunek przyjęcia każdego kroku:
- `dotnet build -c Release` (do osobnego katalogu testowego, przy zamkniętym symulatorze);
- `--selftest` na `Testy/station_test.json`: **`nastawione 25 z 26`, `kontrola zależności 4 z 4`**;
- `--edtest`: Widzew 979 przebiegów;
- `--editshot p.png fuzz 300 7` i `dfuzz 300 7`: po 0 błędów;
- `--editshot p.png testowo out.json`, potem `mijanka out.json 5`: bez błędów;
- dodatkowo w krokach 1–3 (czyste przenosiny): **dziennik zdarzeń selftestu identyczny jak przed zmianą** (porównanie plików – wykrywa niezamierzoną zmianę zachowania);
- zrzut pulpitu (`--screenshot`) w krokach, które dotykają meldunków;
- aktualizacja `ROZWOJ.md`.

| Krok | Co | Zmiana zachowania? |
|---|---|---|
| **0** | Pomiar odniesienia: wszystkie testy + zapis dziennika selftestu „przed”. | nie |
| **1** | `Rejestrator` – wydzielenie dziennika, alarmów, zegara. | nie |
| **2** | `Instruktor` – lista usterek, `ApplyFault`, scenariusze usterek z `Trains.cs`. Na razie pisze w te same pola. | nie |
| **3** | `Rozklad` i `Otoczenie` – kolejka, scenariusze ruchu, GTFS; sąsiednie posterunki (`Neighbours`, `NbrPending`, odpowiedzi na Wbl z `Blocks.cs`). Komunikaty blokady wg 3.6. | nie |
| **4** | **Karta odcinka** (OT / LO): ruch zgłasza `Osie`, karta liczy zajętość, błąd zliczenia, zerowanie, pierwszy przejazd. KZ czyta `StatusOdcinka`. `Trains.OnBlock` w KZ → zajętość odcinków szlaku. | możliwa drobna (cykl) |
| **5** | **Karta zwrotnicy i wykolejnicy**: rozkaz → czas przekładania (z typu napędu, ręczna ×4 jak dziś), zamarznięcie, nadzór czasu, brak kontroli, rozprucie mechaniczne przez `Najazd`. KZ rozstrzyga rozprucie / brak kontroli (pyt. P4). Ruch jedzie po `Iglice`, nie po `Sws[].Pos`. | tak – patrz pytania |
| **6** | **Karta sygnalizatora**: rozkaz sygnału → sygnał wyświetlany; włókno główne „czerwone”, brak sygnału zezwalającego → niższy / „Stój”, brak danych → ciemny. Ruch widzi `SygnalWidziany`. KZ zgłasza §26 z `StatusSygnalizatora` (nie z usterki). | tak – patrz pytania |
| **6a** | Okno **„Komputerownia / karty”** (zakładka w podglądzie KZ albo osobne okno): każde urządzenie ze swoją kartą i stanami (rozkaz, status, czas, usterki). Zrzut do akceptacji. | tylko nowe okno |
| **7** | **Zasilanie** jako urządzenie (sieć, UPS, agregat, NN) – wpływa na karty (NN0 → brak przekładania). | nie |
| **8** | Ruch bez przecieków: manewry wg tarcz i końców przebiegu widzianych w terenie zamiast `LockType`, prędkość z sygnału (S10/S13…) zamiast `Route.VSet`. | **tak** – osobna decyzja |
| **9** | **System nadrzędny**: parser poleceń, dwuetapowość, stanowiska, PIP → `SystemNadrzedny`; KZ dostaje `PolecenieSystemowe`. PIP śledzi numery po zajętościach, nie z listy pociągów. | nie (pulpit ten sam) |
| **10** | `Simulator` jako cienka fasada; KZ w `Sim/Kz/`. Przygotowanie pod transmisję (pętle) i rezerwę komputera (P1) – osobny etap. | nie |

Kroki 1–3 i 9–10 to przenosiny kodu. Kroki 4–8 zmieniają, kto o czym decyduje – tam mogą się zmienić drobne zachowania i wtedy mówię wprost, co.

---

## 5. Pytania do właściciela przed krokami 5, 6 i 8

1. **Rozprucie (P4).** Proponuję: karta zgłasza tylko „brak kontroli bez rozkazu”, a KZ rozstrzyga – odcinek zajęty → rozprucie, wolny → niespodziewany brak kontroli. Skutek: dzisiejsza usterka instruktora „sygnalizacja rozprucia” bez pojazdu stałaby się „brakiem kontroli”. Czy tak? Czy usterka ma zostać jako „rozprucie przy zajętym odcinku” (instruktor zajmuje odcinek)?
2. **Sygnalizator (P5).** Przepalone włókno główne czerwone: świeci rezerwowe i jest tylko alarm (czerwony migający)? Brak światła zezwalającego: sygnał niższy (np. S5 zamiast S2) czy od razu „Stój”? Dziś: KZ nie otwiera semafora i zgłasza §26.
3. **Brak danych ze sterownika (P2).** Sygnalizator bez łączności – ciemny czy czerwony? Zwrotnica – zostaje, brak kontroli na pulpicie?
4. **Ruch manewrowy (krok 8).** Czy maszynista ma się kierować wyłącznie tarczami (Ms2 / „Stój”) i wskaźnikami w terenie, a granicę jazdy znać z polecenia dyżurnego (dziś: jedzie tylko w utwierdzonym przebiegu manewrowym)? Krok 8 mogę odłożyć na koniec.
5. **Opóźnienie jednego cyklu** między rozkazem a meldunkiem (0,2 s) – akceptowalne?

---

## 6. Ryzyka i ustalenia robocze

- W repo są niezacommitowane zmiany innych sesji w `Sim/Blocks.cs` i `Sim/Commands.cs`. Przed krokiem 0 trzeba, żeby nikt inny nie edytował `Sim/` – inaczej przenosiny pomieszają cudze zmiany. Commituję tylko swoje pliki.
- `station.json` właściciela nie jest ruszany; testy na `Testy/station_test.json` i kopiach.
- Format sieciowy (`Indication`, polecenia tekstowe) zostaje – stanowiska ze starszą wersją nadal się łączą do kroku 9.
- Poza zakresem (do osobnej sprawy): opisy scenariuszy w `Trains.Scenarios` mają zaszyte nazwy stacji Widzew/Fabryczna i obiekty `LW:S1`, `LW:11` – wbrew zasadzie ogólnego symulatora.

---

## 7. Odpowiedzi właściciela (2026-10-16)

1. **Rozprucie:** „niespodziewany brak kontroli” to po prostu brak kontroli. Jeśli przy braku kontroli nastąpi zajętość odcinka – pojawia się sygnalizacja rozprucia. (KZ rozstrzyga z zajętości – zgodnie z propozycją.)
2. **Włókna sygnalizatora:** „nic z tego” – nie modelujemy włókien rezerwowych ani przechodzenia na sygnał niższy.
3. **Brak łączności ze sterownikiem:** na pulpicie biały migający – brak kontroli wg Ie-20.
4. **Manewry wg tarcz (krok 8):** później.
5. **Opóźnienie jednego cyklu** rozkaz → meldunek: akceptowalne.

---

## 8. Krok 5 – karta zwrotnicy i wykolejnicy (zrobione 2026-10-17)

- `Sim/Urzadzenia/KartaZwrotnicy.cs`: `KartaZwrotnicy` – teren (iglice, przekładanie, zamarznięcie, uszkodzenie układu kontroli,
  uszkodzenie iglic po rozpruciu), rozkaz `Rozkaz(„+”/„-”)` wykonywany w następnym cyklu (opóźnienie zaakceptowane, § 7 pkt 5),
  status `Simulator.StatusZwrotnicy(num, położenie z kontrolą | null, przekładanie, przekroczony czas)`; `KartaWykolejnicy` – rozkaz
  założenia / zdjęcia z czasem `Cfg.SwTime`, status `StatusWykolejnicy`.
- Cykl: `UpdatePower` → **`Urzadzenia.Tick`** (karty: rozkazy, przekładanie, statusy) → blokady → ruch → sygnały → przebiegi.
- KZ: `SwState` to kopia statusu; `Moving` – przestawienie zlecone karcie (do meldunku), `Frozen` – przekroczony czas przekładania
  (alarm §24 ust. 6). **Rozprucie rozstrzyga KZ** (§ 7 pkt 1): brak kontroli bez przekładania przy zajętym odcinku zwrotnicowym →
  rozprucie, przy wolnym → „niespodziewany brak kontroli”. KSR kasuje rozprucie; jeśli iglice dalej bez kontroli – brak kontroli
  („po skasowaniu rozprucia”), nie ponowne rozprucie (`SwState.KsrDone`).
- Ruch jedzie po iglicach (`Simulator.NextInTerrain` → `Urzadzenia.Iglice`), najechanie od gałęzi – `Urzadzenia.Najazd`
  (karta: iglice dociśnięte, uszkodzone); wykolejnica w terenie – `Urzadzenia.WykolejnicaNaTorze`.
- Usterki instruktora `swNoCtrl`, `swFrozen`, `swRozpr` idą do karty. **Zmiana:** `swRozpr` to teraz „uszkodzenie iglic” – przy wolnym
  odcinku brak kontroli, przy zajętym rozprucie; usunięcie usterki przywraca kontrolę, ale sygnalizację rozprucia kasuje dopiero KSR.
- **Zmiana zachowania:** wykolejnica w przebiegu zdejmuje się w czasie przekładania (dotąd natychmiast); kontrola zwrotnicy
  przychodzi jeden cykl później. Selftest: część sygnałów zezwalających o 5 s później (krok sprawdzania 5 s), wynik bez zmian.
- Testy: `WYNIK: nastawione 25 z 26; kontrola zależności 4 z 4`, `OCHRONA BOCZNA … 4 z 4`, nowa linia **`KARTY ZWROTNIC: kontrola 8 z 8`**
  (brak kontroli przy wolnym odcinku, najechanie przy zajętym → rozprucie, KSR → brak kontroli, naprawa, zamarznięcie z alarmem
  i powrotem, kontrola po czasie przekładania). Scenariusze manewry / ic_05 identyczne; usterki / zima – te same alarmy, postój
  pociągu 18100 o 1 s później.

## 9. Krok 6 i 6a – ustalenia (2026-10-17)

- **Sygnał przy przebiegu** (właściciel): z prędkości przebiegu (zwrotnice na bok, tor) i sygnału **następnego semafora** – np. wjazdowy A
  przy B na „Stój” – S5; gdy B poda sygnał zezwalający, A zmienia się samoczynnie. Ręczne nadpisanie w tablicy przebiegów.
  Dokładna tabela S1–S13 (komory, miganie, pasy 60 / 100, prędkość tu / przy następnym) – **z Ie-1, którą właściciel doda do `Zrodla/`**;
  do tego czasu krok 6 czeka (bez zgadywania sygnałów).
- KZ wysyła karcie sygnalizatora rozkaz z konkretnym sygnałem (S1…S13, Ms1 / Ms2, Sz); karta zapala komory i melduje sygnał wyświetlany.
- **Podgląd kart – osobny program** (`Komputerownia.exe`; właściciel: „musi być jako osobna apka”): łączy się przez sieć z działającym
  symulatorem (jak stanowisko, port 47120 – wybór agenta, właściciel nie rozstrzygnął) i dla każdej karty pokazuje rozkaz od KZ,
  status do KZ, świecące komory semafora, czasy; może też wprowadzać usterki kart.

## 10. Krok 6a – program Komputerownia (zrobione 2026-10-17, bez kart sygnalizatorów)

- Osobny program `app/Lcs.Komputerownia` (`Komputerownia.exe`, `uruchom_komputerownia.bat`), bez odwołań do kodu symulatora – tylko protokół sieciowy.
- Protokół (`Net/LcsNet.cs`): klient „serwis” zamiast „hello” – bez stanowiska dyżurnego; żądania `karty` (odpowiedź `KomputerowniaStan`:
  zegar, posterunki, wiersze kart, usterki do wprowadzenia) i `usterka` (key, target, clear → `Instruktor.ApplyFault`, nieznana – odrzucona).
- Symulator zawsze otwiera port serwisowy **127.0.0.1:47121** (osobny `LcsServer` tylko na tym komputerze; zajęty – bez portu); serwer stanowisk (47120)
  też przyjmuje Komputerownię. Tryb zrzutu: `#serwis=PORT`.
- Wiersz karty (`Sim/Urzadzenia/Komputerownia.cs`): rozkaz od KZ (ostatni + czas – nowe pola `OstRozkaz`, `OstRozkazT` w kartach), status do KZ,
  teren, usterki, „nietypowa”. Okno: filtry, tabela odświeżana co 0,5 s w miejscu, panel karty z przyciskami usterek.
- Test `Komputerownia.exe --test` z symulatorem w tle: połączenie, 194 odcinki / 72 zwrotnice / 5 wykolejnic, brak kontroli LW:57 z usterką,
  wprowadzenie i usunięcie usterki z programu, odrzucenie nieznanej, panel karty – **błędów: 0**. Poprawka przy okazji: `#fault=rodzaj:POST:obiekt`
  dzielony tylko na pierwszym dwukropku.
- Do zrobienia z krokiem 6: karty sygnalizatorów (rozkaz z sygnałem S1…S13, świecące komory, sygnał wyświetlany) – po Ie-1.
