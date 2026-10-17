# 13. Decyzje i ryzyka

## Ustalenia już rozstrzygnięte
Nazwa SUK; dokumentacja poza Git; żadnych zmian kodu w tej pracy; serwer Windows; start 20-40 graczy; Koluszki/Łódź Widzew/styczne jako dane; autorytet serwera dla ruchu i srk; singleplayer lokalny z AI stycznych; najpierw infrastruktura, nowe AI potem.

Nie pytamy ponownie o zatwierdzone w PODZIAL_MODULOW.md §7 rozprucie/brak kontroli, brak modelu włókien rezerwowych, oznaczanie utraty kontroli, odłożenie manewrów i akceptację opóźnienia cyklu.

## Rejestr decyzji
| ID | Do decyzji | Propozycja wyjściowa | Blokuje |
|---|---|---|---|
| D01 | granica odcinka LCS | najpierw grupy całych posterunków; wnętrze osobno | S05 |
| D02 | dokładne prawa pomocniczego/nadzorcy | podgląd i jawna delegacja, bez domyślnego przymusu | S05 |
| D03 | Wbl/Pzk po rozdzieleniu końców Internal | ustalić wg źródeł i scenariuszy, bez zgadywania | S05/S13 |
| D04 | utrata obsady i odzyskanie sterowania | rezerwacja 30 s jako hipoteza, potem jawna procedura | S06 |
| D05 | pierwsze urządzenia i telefonogramy | jeden uzgodniony telefon + radio; katalog od właściciela | S09 |
| D06 | kompetencje moderatorów i odwołania | osobne kick/mute/ban/zakaz obszaru | S08 |
| D07 | dane, retencja, nagrania, prywatność | brak nagrań głosu domyślnie, minimalizacja | S08/S13 |
| D08 | statystyki i odpowiedzialność przy przekazaniu | zdarzenia serwera, bez nieuzgodnionego rankingu | S08 |
| D09 | środowisko, TLS, backup i PC referencyjny | Windows 4 vCPU/8 GiB jako próba, nie wymóg końcowy | S00/S13 |
| D10 | dostawca audio i licencje | Mumble prototyp; LiveKit alternatywa | S10 |
| D11 | framework i pełne zaufanie aktualizacji | Velopack kandydat, utrzymywane zabezpieczenie metadanych | S12 |
| D12 | zakres pełnych posterunków AI | odróżnić świat serwera od stycznych uproszczonych | S14/S15 |
| D13 | zasady decyzji krzyżowania/wyprzedzania | reguły właściciela, zatwierdzone scenariusze | S16 |
| D14 | trwałość komend i zapisu świata | jasno określone okno utraty, nowa epoka po awarii | S06/S13 |

Te decyzje nie są potrzebne do ukończenia niniejszej dokumentacji. Karta etapu ma wskazać tylko pytania blokujące jego wykonanie; nie zadawać wszystkich przy każdym nowym czacie.

## Rejestr ryzyk
| Ryzyko | Znaczenie | Ograniczenie |
|---|---|---|
| Silnik powiązany z WPF | host trudno wydzielić bez regresji | neutralne DTO, scenariusze i dzienniki S01-S02 |
| Błędna autoryzacja końca blokady | ingerencja w cudzy obszar | test hipotezy i katalog komend |
| Procedura srk wymyślona przy okazji sieci | niezgodne zachowanie dydaktyczne | decyzje właściciela i źródła, osobne zmiany |
| Zbyt ciężki audio adapter | koszt klienta lub własny fork | prototyp porównawczy i licencje |
| Updater chroni tylko plik, nie feed | podmiana/cofnięcie wydania | pełna bramka S12 |
| Wspólne CPU sieci/głosu/świata | wzajemne zakłócenia | izolacja, limity, test 40/80 |
| Rozproszony świat za wcześnie | duża złożoność spójności | najpierw jeden writer; skalować niezależne światy |
| Cicha utrata logów i statystyk | fałszywe wyniki i brak audytu | trwałość, deduplikacja, tryb awarii |
| AI ruchowe błędnie interpretowane jako LLM | niezgodny kierunek projektu, koszt i utrata offline | F17: autorski algorytm danych/reguł; LLM wykluczony z operatora i planisty |
| Za mały zakres świata w pilocie | brak drożności bez obsady | ograniczony scenariusz albo pełna obsada; AI dopiero później |

## Granice pewności
To projekt na podstawie odczytu kodu, nie pomiar obecnego serwera. Nie potwierdzono produkcyjnej integracji WPF z Mumble/LiveKit, kombinacji aktualizatora z pełnym modelem TUF, wydajności hosta 4 vCPU ani gotowości nowych pakietów Koluszek. Każda taka rzecz ma własny etap, wynik i dowód zamiast założenia „zadziała”.

## Uzupełnienie właściciela: odrębne KZ
USTALONE: każdy posterunek ma własny moduł zależności. Nie jest już otwartą opcją wspólny KZ dla całego LCS. Jeden wspólny proces to rekomendacja wykonawcza, a nie scalenie modułów.

D15: zatwierdzić mapę własności urządzeń, granice modułów i przypadki przebiegów/funkcji obejmujących granicę posterunków. Każdy posterunek ma jeden własny kontekst KZ; dane i procedury graniczne ustala człowiek. Blokuje odpowiedni podkrok S01/S02 i odbiór S13. Nie generować automatycznie tablic zależności przy podziale istniejącego pakietu.

Ryzyko migracji: pozostawienie globalnych mutable słowników może pozornie wyodrębnić klasy, zachowując jeden KZ. Odbiór wymaga testu prywatności stanów i braku bezpośredniego odczytu sąsiada. Spójna migawka obejmuje wiele KZ oraz wspólny ruch i komunikaty w drodze.

USTALONE F15: po oddaniu kontroli podgląd pozostaje, sterowanie znika. D02 nie otwiera ponownie tej zasady. Podgląd nadzorcy nie daje prawa wydania komendy; przejęcie wymaga osobnego procesu. Poradnik sprawdzono na s. 122 i 142-146, rozróżniając dosłowną treść od wymagania właściciela - rejestr w rozdziale 17.

## Korekta po audycie źródeł - wersja 1.3
### D16. Szczegóły synchronizacji i przełączenia
Dwie grupy są już rozstrzygnięte. Przed S02B/S02C zatwierdzić profil diagnostyki, opóźnienia, Ready, powrót po naprawie i ręczną ingerencję. Snapshot+log/selectionEpoch to projekt SUK, nie odtworzony protokół producenta. Propozycja: naprawiona grupa wraca jako rezerwa, bez automatycznego powrotu na aktywną tylko z racji numeru.

### D17. Transmisja i reakcje obiektów
Przed próbą braku obu grup w S02C zatwierdzić odpowiedni profil reakcji obiektów. Przed S02D zatwierdzić ręczną mapę pętla-koncentrator-sterownik-karta-obiekt oraz reakcje na utratę obu dróg/statusu/obu grup. Nie projektować jej automatycznie ani przyjmować rosyjskich limitów jako polskiej specyfikacji. Brak rozstrzygnięcia blokuje właściwy scenariusz i publiczny odbiór, nie uzasadnia wymyślenia reakcji.

Największe ujawnione ryzyko: podwójny fizyczny skutek starego kodu po uruchomieniu grup. Dlatego S02A poprzedza S02B. Dalsze ryzyka: pomylenie MOR-3 z EBILock, grup z kanałami lub serwerami EBISCREEN. Wspólny proces/kod nie zapewnia odporności na crash Windows ani wspólny błąd algorytmu.

## Rozstrzygnięte F17; zakres D12/D13
Technologia AI ruchowego jest ustalona: autorski algorytm danych, automatów stanów i reguł, bez LLM. D12 określa granice sąsiada i obsady, D13 reguły decyzji/miary jakości, D05 katalog rozmów. Nie są to otwarte pytania o użycie modelu językowego. Nie wprowadzać go później jako opcjonalnego operatora lub trybu awaryjnego w ramach tego planu.

## D18-D20: szczegóły nowych etapów
D18: macierz kompetencji panelu, zatwierdzone operacje utrzymaniowe, retencja/eksport i wymagania ponownego uwierzytelnienia. Pełny panel jest ustalony, a S08 nie może stracić podstawowej moderacji.
D19: katalog dyspozycji, parametry, odbiór/akceptacja/realizacja/anulowanie, kryteria ukończenia oraz zakres działania algorytmu F17. Tryb oznacza nadzór i koordynację, bez automatycznego ControlLease.
D20: zamrożona neutralna specyfikacja, jednostki/limity/formaty ABI, wersje GNAT/gprbuild i dystrybucja DLL, procedura review po zamrożeniu A/B. Ada i dwa osobne zlecenia nie są już otwartym pytaniem.

F22 jest ustalone. Polityka startu przy braku dwóch gotowych grup pozostaje D16. Nie losujemy wadliwego zestawu dla spełnienia pozornej losowości i nie nadajemy praw klientowi wynikiem losowania.


## D18 - dostęp administracji 1.6
ZATWIERDZONE przez właściciela 17.10.2026: osobna aplikacja i indywidualny klucz generowany lokalnie na komputerze administratora, preferencyjnie niewyeksportowalny CNG/TPM. Serwer rejestruje publiczny klucz i wydaje certyfikat. Miejsce generowania jest rozstrzygnięte; wcześniejsze serwerowe wydawanie klucza prywatnego zastąpiono tym ustaleniem. D18 określa profil TPM/atestacji, zaproszenia/zgody, pierwszego właściciela, utratę wszystkich urządzeń, rotację i ochronę wystawcy. Samo hasło nie rejestruje niezatwierdzonego klucza ani nie odzyskuje pełnego dostępu.

## Aktualne źródła i bramki - 1.7
Nie otwierać ponownie decyzji o własnych KZ, Ada, niezależnych A/B, dwóch grupach, losowaniu startu, ViewGrant bez ControlLease, AI bez LLM i lokalnym kluczu administracji. Wykonane w źródłach kroki kart/zasilania/PIP/fasady są nową bazą, a nie powodem ponownej implementacji.

Nowe bramki: odcięcie starej nieautoryzowanej ścieżki serwisowej przed publicznym pilotem (N42); pozyskanie i weryfikacja wskazanego PDF Ie-1 przed normatywnym odbiorem/modyfikacją jego reguł; rozstrzygnięcie istniejących pytań ≤100 km/h i sygnalizatorów bez kart na podstawie źródeł i właściciela. Plan edytora zobrazowania jest zaakceptowany, wykonanie Z0-Z6 i konwersja UT+obraz do symulatora nadal przyszłe.
