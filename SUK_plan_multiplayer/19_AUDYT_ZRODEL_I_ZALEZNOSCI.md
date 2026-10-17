# 19. Audyt źródeł i zależności wdrożenia

**Status:** plan SUK 1.8, aktualizacja 2026-10-17. Rejestr dowodów źródłowych oraz zależności przyszłej implementacji. Stan kodu sprawdzono statycznie na HEAD `5eddb5c5d58eccd5af6467f7ca2373a2daed6568`, porównując zmiany od `2120ab0bcbedbc503a4a859eda26a7823644d0b7`. Wykonane wcześniej moduły C# odróżniamy od nieukończonych etapów multiplayera i Ada. Nie uruchamiano w tej pracy programu, buildów ani testów i nie potwierdzono działania grup lub serwera publicznego. Dokumentacja pozostaje poza Git; nie zmieniono programu ani dokumentów repozytorium. Rejestr aktualnej kontroli: `ZRODLA_KONTROLA_1_7.json`.

## 1. Jak AI i człowiek mają oceniać dowód

Pierwszeństwo przy ustalaniu, co rzeczywiście mówi źródło, ma odczyt konkretnej strony, nie skrót notatki. PEWNE oznacza w tym rejestrze potwierdzoną treść wskazanego źródła w jego zakresie; nie dowodzi kompletności modelu ani zgodności wszystkich odmian sprzętu. WTÓRNE oznacza opis pośredni, w tym materiały dotyczące rosyjskiej odmiany EBILock. DOMYSŁ oznacza interpretację wymagającą rozstrzygnięcia. WYMAGANIE WŁAŚCICIELA jest ustaleniem dla SUK i powinno pozostać oznaczone także wtedy, gdy nie znaleziono dosłownego odpowiednika w podręczniku.

Źródła merytoryczne, obowiązujące ustalenia projektu i rekomendacje infrastruktury to odrębne poziomy. Liczba 20-40 graczy, Windows oraz jeden proces świata na początek nie są twierdzeniami instrukcji srk. Dwie symulowane grupy nie stanowią same przez się dwóch niezależnych fizycznych serwerów.

## 2. Rejestr dowodów

Numery PDF są liczone od pierwszej strony pliku, od 1. TOM I ma taką samą numerację drukowaną i PDF w poniższych miejscach. KOMPUTERY.pdf jest 56-stronicowym wyciągiem obejmującym drukowane strony 120-175; nie stanowi niezależnego drugiego źródła.

| ID | Źródło i lokalizacja | Ustalenie oraz granica pewności |
|---|---|---|
| E01 | Poradnik TOM I, PDF/druk 126, §1.5.8, część Poziom zależnościowy; KOMPUTERY.pdf PDF 7 / druk 126 | PEWNE: EBILock ma dwa zestawy o identycznej strukturze sprzętowej: zasadniczy i gorącą rezerwę. Przetwarzanie jest dwukanałowe przez programy różnych zespołów; zgodność kontrolują KZ i sterowniki. Przy niezgodności funkcje nastawcze przejmuje rezerwa. Brak czasu i szczegółowego protokołu przełączenia. |
| E02 | Poradnik TOM I, PDF/druk 124, §1.5.8, rysunek 70; KOMPUTERY.pdf PDF 5 / druk 124 | PEWNE: poziom operatorski EbiScreen jest oddzielony od zależnościowego i sterowników. Na schemacie przy komputerze zależnościowym zaznaczono gorącą rezerwę. Podsystem operatorski przyjmuje polecenia, przekazuje je KZ, pokazuje stany i rejestruje zdarzenia. |
| E03 | Poradnik TOM I, PDF/druk 140, §1.5.8, opis MOR-3 i rysunek 74; KOMPUTERY.pdf PDF 21 / druk 140 | PEWNE: MOR-3 opisano jako 2z2, z porównywaniem wyjść w bezpiecznym komparatorze i przejściem w stan bezpieczny przy niezgodności. Nie jest to opis EBILock ani MOR-2zs. |
| E04 | Ie-20_WCAG.pdf, PDF/druk 12, rozdział 2, §7 ust.12 | PEWNE: rezerwowy zestaw komputerowy zapewnia bezpieczne przejęcie sterowania i rejestracji przy awarii zasadniczego. Instrukcja nie ustala tu szczegółowej budowy dwóch grup konkretnej odmiany EBILock. |
| E05 | Ie-20_WCAG.pdf, PDF/druk 9, §6 ust.1-5 | PEWNE: system nadrzędny, zależnościowy i sterowniki mają odmienne zadania. To podstawa rozdzielenia pulpitu, obliczeń zależnościowych oraz urządzeń. Nie wynika stąd dowolność zastępowania meldunków urządzeń listą pociągów. |
| E06 | Ie-104_2025_WCAG.pdf, PDF/druk 6, §4 ust.5; PDF/druk 5, §3 ust.3-4 | PEWNE: G1-G4 to grupy informacji zobrazowania: statusy urządzeń, infrastruktura, informacje o pociągach, stany operacyjne. Nie są grupami komputerów. Pełny katalog zobrazowania konkretnego typu urządzeń opracowuje producent. |
| E07 | Poradnik TOM I, PDF/druk 147, §1.5.8.1, zdjęcie 46 | Potwierdzone wizualnie: osobne pola dla serwerów EbiScreen300 1/2 i komputera zależnościowego 1/2. Podwójny poziom operatorski i podwójny KZ nie mogą zostać scalone w jedną parę znaczków. Zdjęcie nie wyjaśnia samodzielnie znaczenia kolorów, automatu przełączania ani synchronizacji. |
| E08 | Poradnik TOM I, PDF/druk 122, §1.5.8, tabela 21 pkt 5; PDF/druk 145, §1.5.8.1, tabela 25 | PEWNE: możliwość przejścia na nastawianie miejscowe stacji/grup obiektów oraz osobne funkcje nastawiania i odbioru meldunków całego okręgu. Nie znaleziono dosłownej reguły zachowania podglądu po przekazaniu. W SUK regułę tę ustalił właściciel. |

Krótki cytat kontrolny E01: „jeden pracuje jako zasadniczy, a drugi stanowi gorącą rezerwę”. Cytat E04: „rezerwowy zestaw komputerowy zapewniający bezpieczne przejęcie procesów sterowania i rejestracji”. Pozostałe ustalenia tabeli są parafrazami, nie cytatami.

Źródła lokalne znajdują się w `D:\Projects\SUK\Zrodla\`: `Poradnik_dla_inspektorow_TOM_I_Automatyka_sterowania_ruchem_kolejowym_i_prowadzenie_ruchu_kolejowego (1).pdf`, `KOMPUTERY.pdf`, `Ie-20_WCAG.pdf`, `Ie-104_2025_WCAG.pdf`. Miejsca E01-E04 i E07 obejrzano także po renderowaniu. Wzór `SimRail_LCS_Widzew_wzor.webp` jest odniesieniem wyglądu; nie dowodzi architektury rezerwy.

## 3. Synchronizacja i rzeczy niepotwierdzone

`Zrodla/EBILock_komputerownia.md` §2.1, linie 117-120, oznacza jako WTÓRNE opisy Ashm/Gł8: bieżące kopiowanie stanu do rezerwy, przełączenie bez zatrzymania urządzeń, samoczynne przełączenie pętli oraz restart około 1,5 minuty. Dotyczą materiałów o rosyjskiej odmianie; polski TOM I nie potwierdza tych szczegółów. Nie przenosić tego czasu do ustawień SUK jako normy.

Polskie źródło potwierdza przejęcie funkcji nastawczych. Nie podaje dokładnego formatu synchronizacji, warunków dopuszczenia rezerwy, dopuszczalnej różnicy stanów, czasu przełączenia, automatycznego powrotu ani reakcji przy awarii obu grup. Własność pojedynczego wyjścia do terenu, epoki i spójny checkpoint są wymaganiami projektu technicznego; ich konkretne rozwiązanie nie jest cytatem z DTR.

Diagram §1.2 i tabela usterek §7.4 opracowania EBILock nie mogą zmieniać wtórnych szczegółów w pewniki przez pominięcie etykiety. W szczególności samo stwierdzenie, że ruch trwa dalej, nie daje bezwarunkowej gwarancji ciągłości przy dowolnej awarii. Link Railway Technology wskazany w opracowaniu nie został ponownie potwierdzony - przy odczycie zwrócił błąd.

## 4. Konflikty i obowiązujące rozstrzygnięcia

| Miejsce | Konflikt / ryzyko interpretacji | Zasada dla dalszego etapu |
|---|---|---|
| KOMPUTERY_notatki.md, początek; ROZWOJ.md etap 7 | Ogólny opis 2z2/komparatora może zostać błędnie przypisany EBILock. | Rozróżnić E01 i E03. Dwa zestawy EBILock oraz dwa programy nie są automatycznie tym samym schematem co MOR-3. |
| EBILock_komputerownia.md §7 i starszy szkic PODZIAL_MODULOW.md kroku 6 | Propozycje włókien rezerwowych i przechodzenia na niższy sygnał pozostały w opisie. | Odpowiedź właściciela w PODZIAL_MODULOW.md §7 pkt 2 wyklucza te funkcje. Nie implementować ich przez skopiowanie wcześniejszego szkicu. |
| PODZIAL_MODULOW.md §5 i §7 pkt 1 | Starsze pytanie o rozprucie było już rozstrzygnięte. | KZ rozstrzyga je z braku kontroli i zajętości, zgodnie z odpowiedzią właściciela. Nie otwierać ponownie zamkniętego pytania. |
| PODZIAL_MODULOW.md §7 pkt 3 oraz Ie-104 §4 ust.11 pkt 5, PDF/druk 7 | Ustalenie właściciela wskazuje biały migający przy braku kontroli; Ie-104 dla nieznanego stanu/braku komunikacji wskazuje biały niemigający. | Przed implementacją rozdzielić konkretne stany: utratę kontroli położenia i brak aktualnych danych. Nie ignorować źródła ani samowolnie odwracać ustalenia; niejasny przypadek wyjaśnia właściciel. |
| PODZIAL_MODULOW.md §7 pkt 4-5 | Manewry według tarcz odłożono; zaakceptowano opóźnienie rozkaz-meldunek o jeden cykl. | Nie zmieniać modelu manewrów ani wartości kroku zegara podczas samego wydzielania hosta. Zgoda na opóźnienie cyklu nie ustala wszystkich timeoutów. |
| Aktualna regresja a historyczny PODZIAL_MODULOW §4 | AGENTS i CLAUDE są już zgodne z ROZWOJ: test EdytoraUT. Dawne --edtest/fuzz/dfuzz pozostały w historycznym opisie. | S00 mierzy bieżącą regresję; rozbieżność instrukcji zamknięta, nie przywraca usuniętego edytora. |
| PODZIAL_MODULOW.md §4, zasada osobnego commitu | Historyczny tryb realizacji nie jest zgodą na zapis Git w tej pracy. | Aktualne zlecenie dotyczy planu poza Git, bez implementacji i commitów. |

WYMAGANIA WŁAŚCICIELA pozostające zamknięte: każdy posterunek ma własny KZ; jego dwie grupy są odwzorowywane na serwerze; po oddaniu sterowania dyżurny LCS zachowuje podgląd i traci sterowanie. Nie scalać KZ w ramach LCS i nie usuwać grup w imię oszczędności. Oddzielić symulację nadmiarowości od odporności fizycznej infrastruktury SUK na awarię procesu lub hosta.

## 5. Mapowanie zatwierdzonego podziału 0-10 na etapy multiplayera

Podstawą pozostaje `D:\Projects\SUK\PODZIAL_MODULOW.md`, zwłaszcza §7-15. Według aktualnej dokumentacji i odczytu kodu istnieją kroki 0-4 oraz 5, 6, 6a, 7, 9 i 10. Krok 8 właściciel odłożył. Potwierdzenie obecności implementacji nie jest ponownym potwierdzeniem wyników jej testów. Nie oznaczamy etapów S00-S19 jako ukończonych na podstawie samych przenosin kodu.

Wiersze tabeli rozróżniają istniejącą funkcję C# oraz pozostały zakres przyszłej migracji. S02A to odbiór i domknięcie tych granic po S01/S02, przed kontraktem Ada S02A1; nie ponowne pisanie kart, PIP i systemu nadrzędnego.

| Krok wcześniejszego planu | Stan i element do zachowania | Pozostała zależność nowego planu |
|---|---|---|
| 0 | Istnieje pomiar odniesienia i pełny dziennik scenariusza. | S00 odświeża bazę na wskazanym HEAD i kopii danych, z hashami oraz aktualnymi kontrolami. Nie porównuje bezwarunkowo z zachowaniem sprzed kart. |
| 1 | Rejestrator jest wydzielony. | S01/S02 zachowują jeden rejestr świata i tożsamość źródła; S08 wykorzystuje zatwierdzone zdarzenia. Obliczające kanały nie tworzą wielokrotnie skutków wspólnego dziennika. |
| 2 | Instruktor i kierowanie usterek do warstw istnieją. | Zachować; rozróżnić usterkę urządzenia, logicznego KZ, grupy i właściwego łącza. Zmiany wchodzą do uporządkowanego wejścia świata. |
| 3 | Rozklad i Otoczenie są osobnymi klasami. | Zachować jeden rozkład/ruch. Dostęp Otoczenie do mutowalnego BlockState zastąpić jawnymi komunikatami; nie nazywać dzisiejszych metod kompletnym interfejsem KZ-KZ. S14/S15 rozwijają istniejące moduły. |
| 4 | KartaOdcinka przyjmuje osie, obsługuje stan OT/LO i zerowanie. | S02A zastępuje bezpośredni zapis do S.Secs niezmiennym statusem z jednoznacznymi odbiorcami. Jeden teren dostarcza to samo uporządkowane wejście wszystkim właściwym kontekstom. |
| 5 | KartaZwrotnicy i KartaWykolejnicy istnieją; ruch korzysta z iglic i wykolejnicy w terenie. KZ rozstrzyga rozprucie z braku kontroli i zajętości. | S02A odbiera istniejące zachowanie i zamienia bezpośrednie wywołania kart przez KZ na OutputPlan. Nie odtwarza ich algorytmów od początku. |
| 6 | KartaSygnalizatora rozdziela sygnał zlecony i wyświetlany; istnieją Sygnaly.cs i Data/sygnaly_ie1.json. | Zachować rozdzielenie. S02A odłącza skutki, a S02A1 weryfikuje źródło i zamraża reguły/tabelę w specyfikacji. Nie dodawać wykluczonych włókien rezerwowych ani niższego sygnału. |
| 6a | Osobna Komputerownia i podgląd KZ/kart istnieją, wraz z rejestrem rozkazów/statusów i kontrolą niezmienników. | Wykorzystać do obserwowalności S02A/S02D i odbioru S13. Podgląd nie dowodzi izolacji, działania grup ani bezpiecznego publicznego protokołu. |
| 7 | Zasilanie jest urządzeniem, a Power kopią statusu w KZ; istnieją NN/ONN/AGR. | S02A odłącza natychmiastowe skutki od obliczeń KZ i zachowuje jawne fazy. Mapa zasilania i awarii wynika z zaprojektowanych domen; nie klonować jednej globalnej szafy na każdy kanał. |
| 8 | Pełny ruch bez przecieków jest odłożony. Część odczytów już skierowano do terenu, ale pozostają LockType, Routes/VSet i fallback Sigs.Aspect. | S02A zapewnia jawny adapter zatwierdzonej projekcji obecnego zachowania. Zmiany manewrów, granic jazdy i prędkości z sygnału mają osobny zakres po decyzji właściciela. Ruch nie dostaje prywatnych stanów kanałów Ada. |
| 9 | SystemNadrzedny zawiera parser, potwierdzenia, stanowiska/obszary, książkę i PIP; PIP śledzi zajętości zamiast czytać Trains.List. | S02A odbiera granicę i jawny kontekst polecenia; S04/S05/S06 domykają prawa, adresowanie i odzyskanie. Nie kopiować parsera/PIP/praw do grup i nie traktować aktualnego DTO jako gotowego kontraktu publicznego. |
| 10 | KomputerZaleznosci jest osobną klasą w Sim/Kz/, Simulator koordynuje moduły i ma fasadę zgodności. | S02A domyka prywatne konteksty, usuwanie mutowalnych referencji i granicę wejść/wyjść. Obecnie nadal jeden KZ obejmuje cały Station. Wydzielenie klasy nie spełnia F14/F16/F20. |

PIP nie odgaduje numeru z samej zajętości: numery dostaje przy wyprawieniu, podstawieniu i zmianie numeru według rozkładu (`Trains.cs:114,137,270,281,289,350`), a śledzenie wykonuje `Sim/Pulpit/Pip.cs:66-86` po `S.Secs[].TrainOcc`. `Meldunki.cs:124` pobiera okna z PIP. Pozostaje dostosowanie do zatwierdzonej projekcji wielu KZ; nie należy przywracać bezpośredniego odczytu Trains.List.

Aktualne AGENTS/CLAUDE wymagają testu EdytorUT. Historyczne `--edtest/fuzz/dfuzz` w PODZIAL_MODULOW §4 nie są obowiązującym poleceniem przywrócenia usuniętego edytora. S00 ustala wykonalną bramkę zgodnie z rozdziałem 11.

## 6. Pozostałe zależności potwierdzone w kodzie

Poniższe ścieżki odnoszą się do `D:\Projects\SUK\app\LcsWidzew\` na HEAD `5eddb5c5d58eccd5af6467f7ca2373a2daed6568`.

- **Jeden globalny KZ i mutowalna fasada:** `Sim/Simulator.cs:43` tworzy jedną instancję; `Sim/Kz/KomputerZaleznosci.cs:29-46` obejmuje wszystkie obiekty Station. `Sim/Kz/Fasada.cs:14-21` zwraca te same słowniki i Power. `Sim/Kz/Interlocking.cs:99` nadaje lokalne R+seq bez kzId. Potrzebne prywatne konteksty, mapa właścicieli, zakresowane identyfikatory oraz projekcje zamiast obcych referencji. Nie tworzyć pełnego Simulator/Trains/Rozklad per KZ.
- **Wyjścia nadal wykonują skutki:** `Sim/Kz/Interlocking.cs:241`, `KomputerZaleznosci.cs:299,381` wywołują karty. `Sim/Kz/Commands.cs:179-184,216` zleca NN/AGR/ZeroLO; `Sim/Urzadzenia/Zasilanie.cs:47-50` i `KartaOdcinka.cs:61` zmieniają teren od razu i publikują status. Sama obecność kart nie daje obliczeń bez skutków ani idempotencji. Logi/alarmy KZ też powstają przez wspólne usługi; trzeba rozróżnić diagnostykę kanału od jednokrotnego zdarzenia domenowego.
- **Statusy nie są jeszcze CycleInput:** `KartaOdcinka.cs:37` zapisuje S.Secs, pozostałe karty wywołują `S.Kz.Status...` (`KartaZwrotnicy.cs:74,131`, `KartaSygnalizatora.cs:48`, `Zasilanie.cs:44`). S02A zbiera statusy na określonej granicy fazy, zamraża dane i kieruje je według właścicieli; nie rozsyła referencji do kart czterem kontekstom.
- **Fazy wymagają zachowania bazowego scenariusza:** `Sim/Simulator.cs:118-127`: czas → system nadrzędny → karty i blokady → rozkład/instruktor → ruch → sygnały/przebiegi/kody → PIP/alarmy. Rozkazy kart zwrotnic/sygnalizatorów wykonują się w kolejnym cyklu, ale NN/AGR/ZeroLO mają ścieżki natychmiastowe. Przy migracji nie zastępować tej kolejności starszym szkicem PODZIAL_MODULOW §3.9; zmianę faz lub opóźnienia odbiera się osobno.
- **Blokady i następny sygnał korzystają ze wspólnej pamięci:** `Sim/State.cs:51-70` ma BlockState obu końców; `Sim/Kz/Blocks.cs:117,124,133` przekazuje go do Otoczenie, które go mutuje (`Sim/Otoczenie.cs:55-82`). Internal nadal pozwala ustawić kierunek jednym Wbl (`Blocks.cs:202-209`). `KomputerZaleznosci.cs:361-365,414` odczytuje/rekurencyjnie oblicza następny sygnał z globalnego Sigs. Potrzebne zatwierdzone interfejsy lokalnych końców blokad i uzależnień sygnałów/ochrony między KZ, z jawną fazą, sekwencją i reakcją na brak danych. Nie zmieniać D03 przez samo rozdzielenie klas.
- **Przecieki ruchu i Otoczenie:** `Sim/Trains.cs:155,171,248,364` czyta LockType/Routes/VSet; `:84,184` ma fallback do Sigs. KZ nadal zna referencję Trains (`KomputerZaleznosci.cs:21`, `Blocks.cs:78`), a `Otoczenie.SzlakZajetyUSasiada` sięga do listy pociągów (`Otoczenie.cs:37-40`). Adapter obecnego zachowania oraz jawny meldunek stycznej są wymagane przed Ada; pełnego kroku 8 nie zaliczamy.
- **Kontekst polecenia nadal wymaga migracji:** `Sim/Pulpit/SystemNadrzedny.cs:14` ma rozwiązane obiekty i koniec szlaku, ale nie docelową kopertę tożsamości/epok/żądania. `:464-469` podmienia wspólne User/Page/CurDesk. Parser/Pending/PIP pozostają pojedyncze, poza grupami; rozwiązany serwerowo cel postId/kzId i jawny kontekst zastępują stan ambientowy. Szczegóły praw i wyjątków komend odbierają S04/S05/S08, bez uznania przenosin za gotowe zabezpieczenie.

### Tabela sygnałów a dostępność źródła

`Data/sygnaly_ie1.json` oraz `Sim/Sygnaly.cs:40-73` istnieją. Bieżące ROZWOJ/PODZIAL_MODULOW opisują je jako implementację opartą na Ie-1, lecz pliku PDF Ie-1 wymienionego w tych dokumentach nie znaleziono w aktualnym `Zrodla`. W tym audycie nie potwierdzono tabeli przez odczyt tego PDF. To granica dowodu, nie rozstrzygnięcie niezgodności tabeli.

S02A1 musi pozyskać lub wskazać zatwierdzone źródło, zweryfikować wydanie i odpowiednie miejsca, uzgodnić macierz sygnałów oraz zapisać hash źródła/tabeli. Pakiet specyfikacji obejmie znaczenie SignalSpeed/VSet, zależność następnego semafora i odmiany SBL. Host parsuje zatwierdzone dane raz i przekazuje znormalizowane, wersjonowane definicje przez ABI; nie planujemy dwóch parserów JSON w Ada. Autorzy A/B otrzymują neutralną specyfikację i testy, bez kodu drugiej implementacji ani starego C# jako przepisu.

## 7. Obowiązkowa bramka przed Ada i grupami

**Odbiór S02A po S01/S02** wymaga wykorzystania istniejących modułów i udowodnienia następujących granic:

1. Prywatny stan per postId/kzId, jeden właściciel obiektu sterowanego i jeden teren/ruch. Komenda lub usterka A nie zmienia prywatnego stanu B; przejazd granicy nie kopiuje pociągu. Fasada nie ujawnia mutowalnych słowników obcych kontekstów.
2. Niezmienny, numerowany CycleInput: statusy kart, jawne komunikaty stycznych, czas oraz znormalizowana intencja. Brak odczytu Trains lub prywatnego KZ sąsiada przez algorytm; kolejność rejestracji słowników nie zmienia znaczenia wejść.
3. Obliczenie daje OutputPlan i proponowane przejście, bez wykonania kart, NN/AGR/ZeroLO, komunikatu wychodzącego ani wspólnej książki/statystyki. Wielokrotne obliczenie kontrolne nie mnoży skutków. Stan zatwierdzony powstaje z uporządkowanej decyzji Committed/Rejected, wspólnej dla przyszłych grup/kanałów; nie dopuszczać pozornych utwierdzeń po odmowie wykonania.
4. Jawna faza przyjęcia i wykonania rozkazów oraz statusów, w tym dzisiejsze ścieżki natychmiastowe. Scenariusz różnicowy dowodzi zachowania bazowego lub dokumentuje osobno zatwierdzoną zmianę. Adapter odłożonego kroku 8 dostarcza zatwierdzoną projekcję bez uzależnienia ruchu od stanów A/B.
5. Interfejsy blokad, następnego sygnału i innych zatwierdzonych uzależnień przekraczających KZ; brak współdzielonego BlockState obu posterunków. Próba obejmuje dwóch operatorów, oba końce i przerwę interfejsu. Brak danych nie jest pozytywnym potwierdzeniem.
6. Aktualny pomiar odniesienia z rozdziału 11: selftest, kontrola zależności/ochrony, kart, sygnałów, zasilania, PIP i niezmienników oraz ich celowych naruszeń; scenariusze, logi i hashe. Nowe kontrole są obecne w `Sim/SelfTest.cs:121-141,204,236,328`, lecz nie uruchomiono ich w tej pracy. PODZIAL_MODULOW §8 dokumentuje zamierzone zmiany czasu; nie wymagać identycznego dziennika względem bazy sprzed kart. Wyniki PIP dla różnych scenariuszy i znane opóźnienia przy zajętym torze raportować jawnie (§14).

**S02A1** odbiera techniczny harness ABI i zamrożony pakiet specyfikacji, w tym weryfikację źródła Ie-1. **S02A2 i S02A3** są niezależnymi implementacjami A/B w osobnych przyszłych czatach; obie zależą od S02A1. **S02A4** porównuje je i referencję C# na takich samych strumieniach, bez automatycznego uznawania starego wyniku za normę. Nie obejmuje jeszcze gotowego przełączania grup.

Przed pierwszym testowym wykonaniem grup w **S02B** muszą być odebrane granice S02A i integracja S02A4: oba A/B w każdej grupie, cztery prywatne stany, wspólne niezmienne wejścia i odłączone wyjścia. Obliczenia bez skutków nie wymagają już gotowego selektora. Podłączenie do terenu wymaga **S02C**: selektora domenowego Ada oraz C# OutputGate z epokami/effectId i jednokrotnym skutkiem. Checkpoint grup i jednego świata jest kryterium S02C; pełna trwałość i odzyskanie z rzeczywistymi prawami należą do **S06**. **S02D** odbiera transmisję/diagnostykę, **S04/S05** prawa i adresowanie, **S13** całość przed pilotem. Szczegóły nadal opisują 18/23; niewyjaśnionych reakcji przełączania nie zastępować wymyślonym timeoutem.

Nie oznaczać S02A, Ada, grup, failover, checkpointu ani multiplayera publicznego jako ukończonych dlatego, że powstał katalog Sim/Kz/, karty lub podgląd. Niniejsza aktualizacja nie zawiera nowych wyników działania.

## Ustalenia właściciela zachowane w planie 1.7

Ada, dwa osobne czaty implementacji oraz losowanie zasadniczej grupy przy starcie są decyzjami projektowymi właściciela. Poradnik potwierdza dwa zestawy i dwa programy różnych zespołów, lecz nie wskazuje Ada ani losowania. Nie przypisujemy tych decyzji producentowi. Dwa programy A/B działają w każdej grupie 1/2; źródłowe rozróżnienie jest zachowane.

Łańcuch pozostaje: S02 → S02A → S02A1 → {S02A2 i S02A3 niezależnie} → S02A4 → S02B → S02C → S02D → S04. C# jest czasowym punktem odniesienia inwentaryzacji i testów, nie docelowym wspólnym programem kanałów ani produkcyjnym silnikiem zapasowym. Implementacja oraz zapisy Git wymagają odrębnego zlecenia właściciela.
