# Przypadki i otwarte decyzje, v0.1.0

Ten katalog opisuje oczekiwania i źródła. Nie oznacza wykonania przyszłych testów Ada.
Ręczne dane nie projektują nowego posterunku ani zależności. Macierz w JSON jest
deklaratywną transkrypcją zweryfikowanej tabeli źródła, nie wspólnym algorytmem A/B.

## Przypadki o znanej podstawie

| ID | Jawne dane / sytuacja | Oczekiwane znaczenie | Podstawa |
|---|---|---|---|
| T01–T16 | Cztery klasy × cztery zapowiedzi, zakres >100 km/h | Każda komórka macierzy z REGULY i JSON; dokładne światła oraz pas | N11, zał.1 s.208–210 |
| T17 | Stój na semaforze | S1/C; brak pociągowego zezwolenia | N11 |
| T18 | Tarcza manewrowa: zabronione/dozwolone | Ms1/N oraz Ms2/B, odrębne od jazdy pociągowej | N14 |
| T19 | Wybrana odmiana Sz razem z czerwonym | C + B*; JSON nie obejmuje pozostałych odmian | N13 |
| T20 | Brak pasa/W21, wyposażenie jawne, żądane 60 | Niższa klasa 40 i uwaga; zachowanie uzgodnione w projekcie | P05 |
| T21 | Żądane 120/130, pas zielony i świetlny W21 | Podstawowa klasa 100 + liczba 12/13 | N17, P05 |
| T22 | Brak ręcznego VSet, tor bez wpisanego ograniczenia i brak ograniczenia zwrotnic | Żądanie sygnału 0=Vmax; nie wymusza domyślnych 60 używanych przez obecny ruch | P05 i słownik README |
| T23 | Ręczny VSet dodatni przy innych wartościach domyślnych geometrii | Żądanie z VSet; zdolność sygnalizacji sprawdzana osobno | P05 |
| T24 | Rozkaz przekładania wysłany, brak końcowego potwierdzenia kontroli | Wysłanie nie oznacza gotowości ani fizycznego wykonania | N02 |
| T25 | Pierwsza oś na pierwszej sekcji prawidłowego przebiegu pociągowego | Samoczynne Stój | N03 |
| T26 | Sekcja opuszczona, poprzednia jeszcze niezwolniona | Brak przesłanki do zwykłego sekcyjnego zwolnienia | N04; szczegóły wyjątków O09 |
| T27 | Pierwszy odstęp ma kilka odcinków; dalszy w tym odstępie zajęty | Nie traktować pierwszego odstępu jako wolnego na podstawie pierwszego czujnika | N08 |
| T28 | Nowe polecenie zwykłej zmiany kierunku, szlak nie jest wolny | Nie spełnia warunków zwykłej zmiany; AZK ma odrębną procedurę | N08–N09 |
| T29 | Ten sam bierny wynik kopiowany ponownie / BufferTooSmall | Ten sam wynik bez nowego kroku/timera/efektu | Wymaganie rozdz.23 §4–6 |
| T30 | Odrzucona propozycja przebiegu | Brak zatwierdzonych pozornych utwierdzeń i brak skutków kart | Wymaganie rozdz.23 §6 |
| T31 | Diagnostyka przepełniona w jednym z czterech kontekstów | Licznik pominięć; kwalifikacja/błąd nadal w wyniku; jeden logiczny commit | F24 / rozdz.25 §5 |
| T32 | Nieznany lub niejednoznaczny właściciel celu nowego polecenia | Brak domyślnego KZ i brak nowego sterowania | P01, granica S02A.01 |

Przypadki T29–T32 są wymaganiami kontraktu, nie potwierdzeniem gotowego Ada.
Normatywne obrazy Os/Sp są rozpisane w JSON, lecz ich karty i docelowe zachowanie
wobec Sz/uszkodzenia/ciemnego sygnału nie są zaliczone przez T01–T32.

## Rejestr otwarty przed zamrożeniem odpowiednich reguł

| ID | Pytanie / granica dowodu | Stan i potrzebna decyzja |
|---|---|---|
| O01 | Pełny dobór dla największej dozwolonej prędkości pociągu na odcinku ≤100 km/h | Źródło Ie-1 jest już dostępne; odczytano znaczenie poszczególnych sygnałów. Zał.1 ma jawne ograniczenie >100. Brak zatwierdzonej kompletnej macierzy ≤100; nie stosować jej mechanicznie. Potrzebne scenariusze/interpretacja właściciela przed zamrożeniem tej części. |
| O02 | `stripes=null` (stare dane) a jawne wyposażenie | Zachowanie historyczne może sygnalizować klasę wyższą od żądania, np. 50→60, 80→100. Właściciel wcześniej pozostawił stare dane działające. Nie uznawać tego za normę nowego KZ; potrzebna jawna polityka migracji/wykluczenia. |
| O03 | Stała W21 dopuszcza więcej niż projektowane VSet; świetlna liczba poza uzgodnionymi wartościami | Ie-1 §17 ust.15 pkt23 lit.d wymaga warunków dla odpowiednich przebiegów. Ostrzeżenie w C# nie dowodzi ich spełnienia. Nie zamrażać takiej konfiguracji jako poprawnej. Właściciel/projektant ma rozstrzygnąć odrzucenie lub korektę danych oraz zakres liczb/zaokrągleń. |
| O04 | Os/Sp a brak danych, Sz, ciemny semafor, usterka i ewentualny SE | Znaczenia Os1–4/Sp1–4 źródłowo potwierdzone (N15); komplet kart i kontrakt statusów nie powstaje tutaj. SE jest poza uzgodnionym modelem. Nie podszywać Os/Sp kodami S. Potrzebny osobny zakres wykonawczy. |
| O05 | Następny semafor: zatwierdzony kod logiczny czy potwierdzony obraz fizyczny; wymiana między KZ | Obecna referencja używa logicznego kodu. Nie dowodzi to docelowej reakcji na zwłokę karty/utratę stycznej. Potrzebny kontrakt świeżości, fazy, sekwencji, braku danych i reakcji, bez czytania prywatnego KZ. |
| O06 | ZCZ = 120 s | **Pozostawione do późniejszego rozstrzygnięcia przez właściciela w tym czacie 2.11.2026.** Nie zatwierdzać 120 s przez aktualizację dokumentu. Nie mylić z uzgodnionym ZD=90 s przy zbliżaniu ani natychmiastowym ZDPP. |
| O07 | Biały migający brak kontroli a biały niemigający stan nieznany | Zachować oba odrębne stany. Ie-104 N18 dotyczy nieznanego stanu/braku komunikacji; wcześniejsze ustalenie właściciela dotyczy braku kontroli. Uściślenie kombinacji usterek pozostaje otwarte. |
| O08 | Historyczne timeouty 5/12/3/5/6/180 s, alarm nastawiania 20 s i usunięcie zakończonego przebiegu po >8 s | Nie wszystkie mają źródło lub osobne zatwierdzenie. Zanotowane w audycie integratora, nie awansowane do normy; uzgodnić profil i granice równości timerów przed implementacją właściwych przypadków. |
| O09 | Zwolnienie końcowego toru do sygnalizatora już przy zajęciu a §18 ust.3 Ie-20; droga ochronna i wyjątki manewrów | Wymaga projektu i jawnej interpretacji. Nie przepisywać historycznego wyjątku jako ogólnej reguły normatywnej. |
| O10 | Uprawnione zwykłe i awaryjne komunikaty KZ–KZ, utrata/duplikacja i niejednoznaczna własność zasilania/odcinków | Obecne schematy nie są pełnym protokołem. Zamykanie danych i kontraktu w kolejnych właściwych kartach; brak automatycznego przypisania. |
| O11 | Limity ABI/DiagnosticBatch, wersje, reprezentacja, snapshoty, sekwencje i gotowość grup | S02A1.02: kontrakt, S02A1.03: katalog przypadków, S02A1.04: stub i zamrożenie. Proponowane 8 rekordów/4KiB nie jest jeszcze zmierzonym profilem. |
| O12 | W21a i kompletność zapowiedzi podwyższonej prędkości następnego semafora | Ie-1 §17 ust.15 pkt24, PDF155–156, określa znaczenie i ograniczony zakres stosowania W21a. Nie ma go w obecnej tabeli. W tej pracy odnotowano źródło, bez objęcia implementacją; macierz i obsługa W21 nie zaliczają W21a. |

Otwarte przypadki blokują zamrożenie i implementację odpowiedniej funkcji, nie
sporządzenie niniejszego rejestru ani techniczną pracę nad kontraktem kolejnej karty.
Zmiana decyzji musi podać autora, datę, zakres, źródło i nowe hashe. Wynik istniejącego
testu C# nie zamyka sam żadnego z O01–O12.
