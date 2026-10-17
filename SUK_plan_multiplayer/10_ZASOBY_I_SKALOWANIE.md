# 10. Zasoby i wzrost skali
DO POMIARU. Liczba graczy nie wyznacza sama rozmiaru serwera. Ważne są liczba obiektów i pociągów, aktywne rozmowy, zakresy obserwacji, częstotliwość zmian i wymagany zapas CPU.

## Pierwsze środowisko prób
Windows, 4 vCPU, 8 GiB RAM, SSD to proponowany punkt startu pomiarów, nie potwierdzenie wystarczalności ani minimalne wymaganie produkcyjne. Osobno mierzymy system, bramę, PostgreSQL, świat i głos. Uwzględnić licencję Windows, backup, transfer i monitoring. Nie podajemy niezweryfikowanej ceny hostingu.

Limit publicznej obsady wynika z testu. Plan startuje od 20-40 graczy; badanie 80 klientów sprawdza zapas i punkt degradacji, nie oznacza obietnicy obsługi 80.

## Oszczędzanie po stronie serwera
- Jedna symulacja świata niezależna od liczby pulpitów.
- Wspólne niemutowalne projekcje, z filtrowaniem według uprawnień; nie kopiować pełnego świata dla każdej klatki klienta.
- Najpierw pomiar pełnych migawek, potem subskrypcje i delty. Stałe zasoby oddzielnie przez HTTPS z cache/hash.
- Ograniczone kolejki i bufory; wolny klient odłączany lub resynchronizowany.
- Grupowanie zapisu zdarzeń, indeksy pod realne zapytania, projekcje statystyk poza tickiem.
- Głos wysyłany do rzeczywistych odbiorców urządzenia, nie globalnie.
- Ograniczony horyzont i budżet CPU algorytmu ruchu; LLM wykluczony z podejmowania decyzji.
- Brak uruchamiania podkładów ortofoto/edytora na serwerze gry.

Nie obniżamy dokładności sprawdzania zależności dla wydajności. Jeśli tick jest zbyt drogi, profilujemy algorytm i strukturę danych albo ograniczamy przyjęty rozmiar świata. Nie pomijamy konfliktów i nie przenosimy wiążącej fizyki do klienta.

## Oszczędzanie klienta
Klient rysuje potrzebny pulpit z meldunków. Układ i zasoby cache według contentHash; limit pamięci i dysku. Meldunki trafiają do bufora modelu widoku; WPF nie przebudowuje wszystkich kontrolek 5 razy/s. Aktualizować zmienione elementy, mierzyć koszt layout/render i dispatcher. Ukryty podgląd ma rzadsze odświeżanie.

Animacje i przesuwanie prezentacyjne mogą działać płynniej od 5 Hz, ale nie udają aktualnego stanu po przekroczeniu ważności meldunku. Multiplayer nie uruchamia lokalnie drugiej pełnej symulacji dla przewidywania wyniku poleceń. Editorowy cache ortofoto 900 MB nie staje się budżetem klienta gry.

WebView2 dla głosu, jeżeli wybrany, wymaga pomiaru osobnego procesu pamięci/CPU, blokady nawigacji do obcych adresów i wąskiego mostka JS. Wybór wygodnego SDK nie może ukrywać kosztu na słabszym komputerze.

## Model transferu
egress_stanu = odbiorcy × średni rozmiar wiadomości × częstotliwość.
DO POMIARU: wszystkie rozmiary poniżej są założeniami rachunkowymi, bez narzutu TCP/TLS.

| Scenariusz dla 40 osób i 5 Hz | Egress | Na godzinę |
|---|---|---|
| Pełna ramka 50 KiB | 81,92 Mbit/s | 36,864 GB |
| Delta 2 KiB | 3,277 Mbit/s | 1,475 GB |
| Delta 5 KiB | 8,192 Mbit/s | 3,686 GB |

Głos: przy założeniu 40 kbit/s strumienia z narzutem, 8 nadających i średnio 3 odbiorcach: 0,96 Mbit/s egress, około 0,432 GB/h. Rozmowa wszystkich ze wszystkimi ma inny koszt. Rezerwa transferu obejmuje także pobrania aktualizacji i paczek.

## Proponowane cele odbioru
| Metryka | Cel do uzgodnienia i sprawdzenia |
|---|---|
| Zwykła obsada | 40 klientów, pełny planowany ruch, 8 h |
| Zapas | 80 klientów, pomiar punktu degradacji |
| Tick pracy | p99 < 50 ms przy kroku 100 ms |
| Zaległość świata | nie narasta; alarm i kontrolowana degradacja |
| Wynik komendy | p95 ≤ 300 ms przy RTT 80 ms; raport p99 |
| Aktualność pulpitu | p95 ≤ 500 ms w warunkach testu |
| CPU całego hosta | średnio < 70% przy 40 klientach, ze sprawdzonym zapasem |
| RAM | < 75% hosta, bez trwałego wzrostu po rozgrzaniu |
| Głos | cel p95 ≤ 250 ms, mierzony end-to-end |
| Klient WPF | brak długich blokad UI; ustalić budżet na wskazanym PC referencyjnym |

Obciążenie ma zawierać prawdziwe liczenie rdzenia i realistyczne zmiany. 40 bezczynnych połączeń to nie test 40 dyżurnych. Zakres pociągów/obiektów zapisujemy w metadanych wyniku.

## Wzrost
Najpierw optymalizacja i mocniejszy host. Później osobne procesy niezależnych światów rozdzielone pomiędzy hosty Windows. Brama zna właściciela świata, sesje i budżety. KZ jednego świata nadal ma jednego wykonawcę.

Dopiero dowiedziony limit jednego dużego połączonego świata uzasadnia plan podziału geograficznego. Wymaga on osobnego protokołu przekazania pociągu, blokady i obsługi partycji. To nie jest automatyczny efekt dołożenia Redis. SignalR backplane nie utrwala świata, a utrata połączenia z nim może zgubić wiadomości.

S17 skaluje niezależne światy i dokumentuje limit pojedynczego okręgu. Nie obiecuje bezkosztowego rozproszenia Koluszek i Widzewa na dwa serwery.

## Budżet wielu KZ
Koszt świata rośnie także z liczbą niezależnych modułów KZ. Benchmark podaje liczbę posterunków/KZ, obiektów każdego z nich i intensywność komunikatów blokady. Kod bibliotek jest wspólny, lecz stan zależności jest prywatny. Kolejne KZ nie wymagają od razu kolejnych usług Windows. Nie zmniejszamy realizmu przez scalenie wszystkich zależności dla oszczędności CPU.

Benchmark uwzględnia równoczesny podgląd LCS przez dyżurnego po oddaniu oraz aktywne sterowanie odcinkowego. Oszczędność subskrypcji nie może usuwać wymaganego podglądu F15.

## Korekta po audycie źródeł - wersja 1.3
Budżet obejmuje od początku obie obliczające grupy każdego KZ oraz kanały. Referencyjny profil rozdziału 18 to cztery konteksty obliczeń na pełny KZ, ale jeden teren/ruch/parser. To nie twierdzenie o czterokrotnym koszcie całego serwera. Cele ticku nadal wymagają pomiaru na tym profilu.

Współdzielić niemutowalne tablice; zachować prywatne stany. Bez osobnego wątku, procesu, bazy i połączenia klienta na grupę. Ograniczyć log wejść, snapshoty i tempo rebuild. Zbyt stara rezerwa jest OutOfSync zamiast powiększania historii bez limitu. W normalnej pracy stale liczy, nie budzi się dopiero po awarii.

S13: pełny pakiet obszaru i wszystkie grupy, 40 klientów/8 h, 80 w oddzielnej próbie zapasu, failover i rebuild podczas obciążenia. Mierzyć osobno kanały, selektor, karty, teren/ruch, delty, log i pamięć. PC gracza: te same pełne KZ obszaru i tylko styczne AI. Nie redukować dwóch grup kosztem F16 bez nowego ustalenia właściciela.

## Koszt algorytmicznego dyżurnego
F17 nie wymaga infrastruktury inferencji LLM, modeli językowych, GPU do takich modeli ani opłat za tokeny. Mierzymy zwykły koszt algorytmu: liczbę wariantów, czas planowania, pamięć stanu i ponowne obliczenia po zmianie meldunków. Priorytety i horyzont wynikają z zatwierdzonego profilu; ich skrócenie nie może omijać procedur ani zależności. Brak wyniku daje jawny stan algorytmu i ograniczone ponowienie, bez odwołania do LLM.


Odtwarzalność planowania opiera się na wersjonowanym limicie wariantów/operacji i stałej kolejności ich oceny. Limit CPU jest osobnym zabezpieczeniem: jawnie przerywa krok bez częściowego skutku i zapisuje zdarzenie do odtworzenia; szczegóły w 07.

## Koszt docelowego stosu
Mierzymy obie niezależne implementacje Ada w każdej grupie: A/B×grupa 1/2, przy jednym terenie/ruchu. Koszt przekraczania C ABI ograniczamy wsadowymi wejściami/wyjściami, bez P/Invoke per obiekt i bez JSON w gorącej granicy natywnej. Liczba kontekstów nie jest mnożnikiem kosztu całego świata.

S18 ma stronicowane projekcje poza tickiem i ograniczone eksporty. S19 korzysta z ograniczonego okna czasu/danych grafiku, buforuje geometrię i delty. Sam podgląd dyspozytora nie uruchamia drugiej fizyki lub kopii KZ. Początkowy budżet hosta pozostaje do pomiaru również z natywną pamięcią Ada.

## Budżet istniejącej diagnostyki - 1.7
Komputerownia dodaje żądania kart/KZ/sprawdzenia przebiegu i usterek do starego LcsServer. Nowy host uwzględnia także te operacje w limitach i pomiarach: częstotliwość, rozmiar projekcji, uczciwa kolejka, aktualność oraz odrębny budżet użytkownika serwisowego. Nie wykonywać kosztownego serializowania pełnej diagnostyki na wątku ticku dla każdego odbiorcy. Wzrost liczby narzędzi nie oznacza potrzeby osobnej kopii świata ani KZ.


## Budżet logowania - 1.8
Kolejki ograniczamy jednocześnie liczbą i bajtami, również diagnostykę każdej repliki Ada. Dla ruchliwych zdarzeń stosujemy poziomy, agregację i jawną liczbę odrzuconych rekordów. Pomiary obejmują cztery konteksty na KZ, wolny dysk, pełną kolejkę i wiele klientów; nie logujemy pełnego stanu co tick. Proponowane profile rozdziału25 są do pomiaru, nie obietnicą. Trwały audyt/commit nie używa polityki drop diagnostyki.
