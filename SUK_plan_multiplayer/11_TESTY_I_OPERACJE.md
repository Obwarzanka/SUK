# 11. Testy i eksploatacja Windows

## Co sprawdzono w tej pracy
Przeczytano zasady i kod, wykonano trzy analizy subagentów, zweryfikowano źródła zewnętrzne i wygenerowano dokumentację. Brak zmian kodu, buildów, testów symulacji, uruchomienia serwera, instalowania usług i testów dwóch komputerów. Nie ma nowego EXE. Weryfikacja PDF/HTML dotyczy dokumentów.

## Regresja podczas przyszłej implementacji
Przed zmianą zachowania S00 utrwala aktualny wynik na kopii Testy/station_test.json. Oczekiwanie wg aktualnej dokumentacji: WYNIK: nastawione 25 z 26; kontrola zależności 4 z 4. Brak 2A wynika z zajętego odstępu i jest opisanym odrzuceniem; nie zmieniać logiki, żeby uzyskać 26/26.

Stare --edtest, --editshot fuzz 300 7 i dfuzz 300 7 są historyczne. Aktualny test to EdytorUT.exe --test wynik.txt zrzut.png, błędów: 0. Bieżące AGENTS i CLAUDE są już zgodne co do EdytorUT --test; historyczny akapit PODZIAL_MODULOW §4 nie przywraca usuniętych trybów. S00 sprawdza bieżące polecenia. Nie zaliczać nieistniejącego polecenia ani odtwarzać starego edytora.

Przyszłe kroki:
1. Sprawdź, że symulator/edytor jest zamknięty; nie zabijaj użytkownikowi procesu bez uzgodnienia.
2. Buduj testowo do osobnego katalogu, przy równoległych procesach różne katalogi.
3. Uruchom selftest na Testy/station_test.json, scenariusze usterki/zima/manewry/ic_05/ic_15/ic_18 oraz test nowego edytora; gdy dotyczy także Komputerownia --test w warunkach opisanych w README.
4. Przy czystych przenosinach porównaj znormalizowany dziennik i skutki scenariuszy, nie tylko końcową liczbę.
5. Przy zmianach UI/meldunków wykonaj zrzut pulpitu i zweryfikuj brak danych.
6. Zbuduj Release do zwykłego katalogu uruchamiania: dotnet build -c Release w app/LcsWidzew i, gdy dotyczy, w app/Lcs.EdytorUT, app/Lcs.Komputerownia oraz app/Lcs.Przechylka.
7. Aktualizuj ROZWOJ/plan UT dopiero gdy osobne zlecenie implementacji obejmuje zmiany repo.

Wynik sprzed zmiany jest dowodem odniesienia, nie pretekstem do utrwalenia ujawnionej luki. Poprawka bezpieczeństwa może świadomie zmienić odrzucanie komend; opisać różnicę i zatwierdzony oczekiwany wynik.

## Macierz testów nowej infrastruktury
| ID | Próba | Oczekiwany wynik |
|---|---|---|
| N01 | dwa PC i kilka ról na jednym świecie | identyczny stan wspólnych obiektów |
| N02 | zmieniony klient wysyła pozycję/sygnał | brak takiego API, odmowa |
| N03 | cudzy posterunek i przeciwny koniec blokady | odmowa bez zmiany stanu |
| N04 | STW/NN/KW i inne globalne ścieżki | zgodnie z jawną macierzą, bez obejść |
| N05 | komenda w kolejce podczas przekazania/bana | stara epoka nie wykonuje się |
| N06 | 1000 powtórzeń requestId i zmieniony payload | jeden skutek; konflikt treści odrzucony |
| N07 | zerwanie przed/po wyniku i przed potwierdzeniem | wynik rozpoznany lub jawnie nieznany, bez replay |
| N08 | luka delty, nowy zakres i nowa epoka | pełny resync, brak starego sterowania |
| N09 | wolny klient, zalew, duża/zła ramka | limity, świat kontynuuje, pamięć ograniczona |
| N10 | brak ticku przy żywym łączu | pulpit bez danych, brak komend |
| N11 | token wygasa/ban na otwartym połączeniu | odebranie sterowania i mediów |
| N12 | cudzy odbiorca i obcy klient głosowy | brak podsłuchu i nadawania |
| N13 | uszkodzona aktualizacja, feed, zły klucz | brak uruchomienia niezweryfikowanej wersji |
| N14 | restart/awaria procesu/bazy/dysku | nowa epoka, jawna polityka odzyskania |
| N15 | powtórzone zdarzenia statystyk | brak podwójnego naliczania |
| N16 | 40 klientów 8 h i próba 80 | cele z rozdziału 10 i raport surowych metryk |
| N17 | offline singleplayer | ten sam rdzeń, bez logowania, tylko styczne AI |

Testy sieci obejmują RTT 80 ms jako profil podstawowy oraz 200 ms, jitter, utratę pakietów, zerwanie, NAT i restrykcyjny firewall jako próby degradacji. TCP maskuje część strat przez retransmisję; nie opisujemy samego procentu strat jako opóźnienia aplikacji.

## Usługi i utrzymanie
Osobne konta/ACL: SUK.Server, SUK.WorldHost, PostgreSQL i serwer głosu. Uruchomienie bez interaktywnego logowania, kontrola praw kont, katalogi danych poza binariami, rotacja logów i limit dysku. Restart z backoff, bez pętli crash co sekundę. Osobne statusy liveness, readiness i świeżość świata.

Metryki: czas Tick i zaległość, kolejki, liczba klientów, egress, resync, błędy autoryzacji, czasy DB, retencja, głos, ważność certyfikatów i wolne miejsce. Log zawiera correlationId, identyfikatory świata/epoki/żądania, bez sekretów. Alarmy trafiają do operatora usługi; brak cichego przekraczania limitów.

HTTPS na 443; porty głosu tylko według wybranej konfiguracji. Przy WebRTC porty UDP/TURN mogą być inne. Zajęcie 443 przez kilka usług wymaga jawnej konfiguracji routingu i certyfikatów, nie wyłączenia szyfrowania. Administracyjne API mediów wyłącznie lokalnie/prywatnie.

## Kopie i odzyskanie
Zakres: baza kont i sankcji, konfiguracje, zatwierdzone pakiety, zapisy świata, klucze/sekrety według oddzielnej polityki. Kopia zaszyfrowana i poza hostem. Proponowane cele pilota: RPO danych trwałych ≤ 15 min, RTO ≤ 60 min, do uzgodnienia i testu. Odtwarzanie świata wymaga własnej strategii dziennika/migawki; te cele nie obiecują zachowania ostatniego ticku.

Test odtwarzania na czystym środowisku obejmuje też najnowsze bany. Host po restarcie nie ufa starym lease i tworzy nową epokę. Wznowienie ruchu po odtworzeniu wymaga weryfikacji stanu i zatwierdzonej procedury, nie automatycznego odegrania żądań z klienta.

## Bramka publicznego pilota S13
Zaliczone S00-S12, zamknięte decyzje blokujące, test dwóch PC, testy N01-N16, 40 klientów/8 h, odzyskanie backupu, rotacja certyfikatu, przegląd bezpieczeństwa, instrukcja moderatora i powrotu po awarii. Zakres posterunków musi działać bez nowego AI albo być jawnie ograniczony; nie pozorować ruchu na nieobsadzonym pełnym posterunku uproszczonym Otoczenie.

## Uzupełnienia negatywne po przeglądzie
N06 obejmuje równoległe duplikaty, restart samej bramy i próbę ponowienia po wygaśnięciu cache. N05/N11 obejmują granicę Applied sankcji i kolejkę hosta. N12 obejmuje ponowne użycie starego JWT mediów oraz zmianę nicku/certyfikatu. N13 obejmuje wygasłe metadane i cofnięty zegar. N14 odróżnia trwały zapis wyniku od trwałego skutku w odtworzonym świecie; świeża epoka nie może powtórzyć UUID z backupu.

## N18-N23. Własny KZ każdego posterunku
N18: w świecie A/B istnieją dwa odrębne konteksty KZ z własnymi danymi i stanem, a wspólny pociąg nie jest sklonowany.
N19: komenda obcego KZ i podrobiony komunikat sąsiada są odrzucone, bez zmiany stanu.
N20: zatwierdzona usterka albo utrata łącza jednego KZ zachowuje pozostałe moduły i lokalizuje brak danych.
N21: sąsiednia blokada działa przez jawne komunikaty między dwoma KZ; nie korzysta z prywatnych słowników sąsiada.
N22: spójny checkpoint zawiera wszystkie KZ, komunikaty oczekujące, teren, ruch, obsadę i zgodne wersje; nie odtwarza mieszanki ticków.
N23: zmiana obsady/człowiek-AI/LCS nie scala ani nie resetuje KZ. Singleplayer używa tego samego modelu modułów w lokalnym świecie.

Bramka S13 obejmuje N18-N23 oraz rozdział 16; wcześniejsza macierz N01-N16 pozostaje obowiązkowa. Realistyczne skutki odcięcia KZ i blokad wymagają zatwierdzonego scenariusza źródłowego. Logiczna izolacja usterek nie oznacza przetrwania crash całego procesu.

N24: dyżurny LCS po oddaniu A nadal widzi aktualny A i działania Y, nie może sterować A ani starym confirmationId, zachowuje własne B. Reconnect odtwarza podgląd bez odzyskania sterowania. Próbować także parsera tekstowego, zmienionego klienta i globalnej komendy obejmującej A. Szczegółowy projekt próby w rozdziale 17, jeszcze nieprzeprowadzony.
Bramka S13 obejmuje N24.

## Korekta po audycie źródeł - wersja 1.3
### Obowiązkowe próby dwóch grup - jeszcze niewykonane
| ID | Próba | Wymagany dowód |
|---|---|---|
| N25 | Wiele KZ i obie grupy normalnie pracują | Prywatne stany, ten sam strumień wejść, aktualny watermark i wykonane obliczenia rezerwy; teren/ruch raz |
| N26 | Awaria/niezgodność kanałów grupy aktywnej przy gotowej rezerwie | Jedno przełączenie, zachowane utwierdzenia, zmiana selectionEpoch, brak drugiego skutku |
| N27 | Awaria tylko rezerwy | Sprawna aktywna grupa pracuje, alarm degradacji, brak fałszywego Ready |
| N28 | Obie grupy uszkodzone lub rezerwa niegotowa | Brak kwalifikowanego nowego wyjścia, reakcje z D17; bez wymyślonego resetu LCS |
| N29 | Stary plan i powtórzenie ZeroLO/rozkazu/komunikatu blokady | Stara epoka i duplikat odrzucone; jeden skutek/wynik/wpis; wspólna decyzja logiczna nie myli odrzuconej repliki z odrzuconą komendą |
| N30 | Restart, luka historii, inny hash lub zatrzymane obliczenia | Jawny stan synchronizacji, snapshot+log bez zapisu do terenu, Ready po dowodzie zgodności |
| N31 | Checkpoint podczas jazdy i przełączania | Obie grupy/kanały/selektor i jeden świat spójne; nowa epoka po restore; brak replay |
| N32 | Jedna/dwie przerwy pętli i awaria koncentratora | Osiągalność zgodna z ręczną topologią, alternatywna droga, lokalna diagnostyka |
| N33 | Przekazanie LCS, ban i potwierdzenie przy przełączeniu | Przy oddaniu podgląd zachowany, przy banie konta dostęp zamknięty; stare sterowanie odrzucone, prawa przy skutku, bez pozornych utwierdzeń |
| N34 | Pełne grupy i rebuild przy 40/80 klientach oraz offline | Czasy faz, CPU/RAM, p99, ograniczony log i brak podwójnego ruchu |

S13 wymaga N18-N34 oraz wcześniejszych właściwych testów, w tym N24. Sama udana sieć nie zalicza grup. Awaria grupy, kanałów, synchronizacji, pulpit-KZ, pętli i procesu ma odrębną diagnostykę. Czasy przełączenia są mierzone; nie wpisywać bez źródła ani 90 sekund, ani zerowej przerwy.

## N35. Algorytm ruchu bez LLM - odbiór S14-S16
Sprawdzić zbudowany moduł operatora, konfigurację i ścieżkę wywołań: nie zawierają wykonawcy LLM, klienta API modelu, klucza ani mechanizmu odwołania do modelu przy błędzie. Nie wystarczy deklaracja „nie użyliśmy modelu w jednej próbie”.

Odtworzyć te same dane, meldunki, czas symulacji, wersję reguł i deterministyczny limit wariantów/operacji: decyzje, przyczyny oraz komunikaty proceduralne są zgodne. S14 sprawdza regułowe rozmowy i odmowy, S15 tę samą logikę lokalnie offline i w hoście serwera, S16 zatwierdzone przypadki krzyżowania/wyprzedzania. Na serwerze odciąć ewentualne zewnętrzne usługi modeli, zachowując sieć gry; w singleplayerze wyłączyć sieć. W obu przypadkach algorytm działa.

Zasymulować niepełne dane i wyczerpanie budżetu: jawny wynik domenowy, brak LLM i brak obejścia KZ. Próba dotyczy rozwoju algorytmu po S13; nie jest nowym wymaganiem wdrożenia pełnego AI przed pilotem infrastruktury. Dowody: przegląd zależności/konfiguracji, log wejść i przyczyn, porównanie powtórzeń oraz profil CPU. Nie uruchamiano jej podczas tej korekty dokumentacji.

Próba limitów N35 rozróżnia limit liczby wariantów od awaryjnego limitu CPU. Ten drugi daje zapisane zdarzenie przerwania, bez częściowego planu; replay uwzględnia to zdarzenie. Sam jednakowy timeout zegarowy na różnych komputerach nie jest dowodem odtwarzalności.

## Nowe próby 1.5 - projekty, bez wykonania
| ID | Zakres i odbiór |
|---|---|
| N36 | Granica Ada-C#: wersja ABI/układ pól/limity/liczby/uchwyty, inicjalizacja i shutdown, brakująca DLL, BufferTooSmall bez powtórnego Evaluate, własność pamięci, wyjątki i brak narastających wycieków. Awaria natywna odróżniona od logicznej usterki KZ. |
| N37 | Dwa niezależne warianty A/B: wspólny hash specyfikacji, odrębne źródła/czaty, brak wspólnego silnika reguł, golden/differential przeciw specyfikacji i zaakceptowanej referencji C#. Porównać przyjęcia/odmowy, timery, utwierdzenia/zwolnienia, rozkazy i istotne zdarzenia; bez głosowania większością lub maskowania różnic. |
| N38 | S18 WPF/Admin API: konto ORAZ klucz i zakres każdej akcji, stronicowanie/limity eksportów, audyt ról/sankcji, Saved/Applied per świat, idempotencja, odebranie sesji i mediów, brak konsoli i domyślnego ControlLease. |
| N39 | S19 dyspozytor: projekcje i świeżość/ograniczony grafik, częściowy obszar, cykl dyspozycji do człowieka/algorytmu, Received nie realizuje, odmowa/anulowanie/reconnect, role admin/dyspozytor bez dziedziczenia. |
| N40 | Start świata: deterministycznie wstrzyknięte wybory 1 i 2 w testach, produkcyjne losowanie per KZ po obu Ready, zapis StartupSelection przed sterowaniem, replay bez nowego RNG. Nowy worldRun po restore losuje po uzgodnieniu; restart jednej grupy/reconnect/przekazanie nie zmieniają wyboru. Brak Ready testuje odrębną politykę degradacji. |

S02A1-A4 odbierają N36/N37, S02C odbiera N40, S13 wymaga N36/N37/N40 przed publicznym pilotem. N35 pozostaje po S13 w algorytmie ruchu. N38/N39 należą do końcowych S18/S19; nie dodawać zależności od finału do wcześniejszego pilota.

Powtarzalność obu wyjść testuje się także przy mieszanym wzorze grup zasadniczych między posterunkami. Losowość jest zapisywanym wejściem, a nie przyczyną ukrytej różnicy replay. Dwa niezależne programy nie gwarantują odporności na wspólny błąd specyfikacji; niezgodność obu grup nie daje prawa wyboru dowolnego kanału.

## Uściślenia nowych prób
N33 obejmuje oddanie/ban podczas przekładania urządzeń i zwalniania przebiegu: nowe komendy dawnego operatora są odrzucane, a autonomiczne dozorowanie wcześniej zatwierdzonego KZ działa dalej. Nie mylić zatwierdzenia intencji z fizycznym meldunkiem wykonania.

N37 porównuje kanoniczne wyniki domenowe A/B, dopuszczając różne reprezentacje prywatne; nie wymaga identycznego surowego hasha snapshotów obu programów. N40 przed wyborem sprawdza Ready+Unassigned obu grup i zablokowane wyjścia, a po zapisanym wyborze nadanie ról przez Ada oraz dopuszczenie jednego źródła przez host.

Zgodność ABI i schematów snapshotu A/B/koordynatora weryfikuje się przed restore. Niedopasowanie nie może częściowo nadpisać już aktywnego świata.


## N41. Klucz administratora - projekt 1.6
**N41A S03/S08, przed S13:** dwa konta/urządzenia, lokalny klucz Windows, kontrolowany enrollment i rzeczywiste mTLS/token binding w przypiętej wersji. Odrzuć: sam token, sam certyfikat, token gry/zły audience, cert A+token B, brak zaproszenia/MFA/zgody, replay/równoczesne zużycie zaproszenia i podmianę klucza. Revoke podczas otwartej sesji i oczekującej operacji, niedostępny wiarygodny status, brak wejścia przez publiczną bramę. Jedna rzeczywista sankcja Saved/Applied. Pełne GUI nie jest potrzebne.

**N41B S18:** WPF, rotacja/wygaśnięcie, utrata/reset TPM, odzyskanie po utracie wszystkich kluczy, podszyty nagłówek proxy i rotacja wystawcy. Osobno atestacja, jeśli wymagana. Brak wsparcia biblioteki nie pozwala zaliczyć próby przez wyłączenie bindingu/TLS. Mierzyć czas odcięcia i koszt walidacji. Testów tych nie wykonano.

## Nowa baza regresji aplikacji - 1.7
S00 ma dopiero uruchomić nowy pomiar na bazie 5eddb5c, używając bieżących danych i zachowując pełny dziennik. Dokumenty repo deklarują: nastawione 25/26 i zależności 4/4, ochrona boczna 4/4, karty zwrotnic 8/8, sygnały 9/9, zasilanie 8/8, wykrywanie celowych naruszeń 2/2, kontrola KZ bez naruszeń oraz PIP 1526/1526 dla opisanej próby. Kod ma te kontrole; nie uruchomiono ich w tym audycie. Liczby PIP zależą od przypadku i czasu; tolerancje dla innych rozkładów muszą być jawne, bez sztucznego wymuszania wyniku.

Nie wymagać identycznego dziennika z oryginalnego 4869fc sprzed kart: PODZIAL_MODULOW §8-15 opisuje celowe zmiany. Czyste późniejsze przenosiny porównujemy z NOWĄ zamrożoną bazą i listą zaakceptowanych różnic. Komputerownia --test wymaga działającej symulacji z aktywnym przebiegiem; jej test podglądu nie dowodzi zabezpieczenia protokołu ani poprawności wszystkich zależności srk. Nie wykonano tutaj także testu dwóch PC, rzeczywistej myszy ani drukarki.

## N42. Stara ścieżka serwisowa i prywatne API - projekt odbioru
Przed S13: nieautoryzowane serwis/karty/kz/kzprzebieg/usterka na publicznym wejściu i przez zgodność ze starym protokołem są odrzucone bez skutku. Odczyt prywatnej diagnostyki wymaga właściwego konta/klucza i zakresu; osoba z prawem podglądu nie wprowadza usterki. Rola instruktora ma osobną kompetencję, zakres i audyt. Cofnięcie konta/klucza/roli przy otwartym kanale i w kolejce odcina nowe skutki. requestId zapobiega powtórnej usterce, a limity diagnostyki nie zatrzymują ticku. Nie istnieje publiczne „awaryjne” obejście starego LcsServer. O20 określa scenariusz; N42 jest wymaganiem przyszłych S04/S08/S13, nie wynikiem wykonanego testu.


## Obowiązkowe próby 1.8 - jeszcze niewykonane
| ID | Próba | Dowód odbioru |
|---|---|---|
| N43 | Każdy nowy moduł emituje poprawny rekord, błędne pole, sekret, wielowierszowy tekst; kolejka i rotacja pod obciążeniem | Jawny moduleId, schemat, korelacja, brak sekretów i wstrzyknięć, limity bajtów/liczby, licznik drop i brak blokady ticku |
| N44 | Wolny/pełny/niedostępny dysk, crash pomiędzy przyjęciem, commit i Applied; diagnostyka czterech kontekstów | Rozdzielone gwarancje25/S06, brak cichej utraty audytu, brak Applied przed właściwą trwałością, jeden skutek i brak resetu utwierdzeń wskutek błędu loggera |
| N45 | Nowy pusty czat otrzymuje jeden prompt i wskazany pakiet | Znajduje wejścia, kontrakty, źródła i kryteria bez historii rozmowy; respektuje zależności, nie zalicza planned jako done; wyjściowy raport wystarcza następnemu |
| N46 | Prompt A/B, zmieniony hash, brakujące źródło i przypadkowe udostępnienie obcego kodu | Start blokowany w danym zakresie, zgłoszenie integratorowi, brak tłumaczenia starego C# lub kopiowania drugiego wariantu; zachowane dwa izolowane łańcuchy |

N43 jest kryterium każdego modułu od S01; N44 dojrzewa z trwałą ścieżką S06 i obowiązuje przed pilotem S13. N45/N46 są próbami organizacji wykonania, a nie wynikami funkcjonalnych testów SUK. W tym zleceniu sprawdzamy ich kompletność w dokumentacji; właściwe wykonanie wymaga przyszłej implementacji i raportów.
