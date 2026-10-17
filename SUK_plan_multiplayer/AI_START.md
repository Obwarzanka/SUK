# Instrukcja dla AI rozpoczynającej etap SUK

To dokument planowania. Nie wykonuj implementacji tylko dlatego, że odczytałeś kartę. Aktualna praca właściciela zakazuje zmian kodu, commitów i push. Właściciel wyraźnie zlecił końcową kopię dokumentacji do D:\Projects\SUK. Do wdrożenia etapu potrzebne jest osobne zlecenie. Samo zlecenie implementacji nadal nie oznacza zgody na publikację lub zapis Git.

## Wyjątek autorów KZ A/B
Autor S02A2/S02A3 czyta wyłącznie AI_START_AUTOR_KZ.md i zatwierdzony neutralny pakiet. Dalsze obowiązki lektur02/19/24/pełnego źródłowego ROZWOJ w tej instrukcji dotyczą autora specyfikacji, integratora i pozostałych ról, nie autorów A/B. Nie jest to uchylenie nadrzędnych instrukcji: sprzeczne środowisko wymaga poprawnego pakietu przed startem.

## Minimalny pakiet kontekstu
1. Odczytaj bieżącą prośbę właściciela.
2. Przeczytaj AGENTS.md, CLAUDE.md, odpowiednie sekcje ROZWOJ.md i aktualny stan repo.
3. Przeczytaj 00_START.md, pakiety_czatow/ID.json i jedną kartę czaty/ID.md. Nie wczytuj całego manifestu; pakiet zawiera wybrane jobs[ID], inwarianty i bezpośrednie zależności.
4. Dociągnij tylko dokumenty i pliki kodu wymienione w tej karcie.
5. Sprawdź zależności: ich status musi być potwierdzony raportem poprzedniego wykonawcy i kodem, nie samym planem.
6. Zapytaj tylko o nierozstrzygniętą decyzję blokującą bieżący krok. Nie pytaj ponownie o Windows, nazwę i 20-40 graczy.
7. Zrób pomiar odniesienia i jeden mały podkrok. Nie rozszerzaj go na dalsze etapy.

## Niezmienne
SUK jest ogólnym symulatorem. Nazwy stacji należą do danych. station.json właściciela jest chroniony; testy na kopii. Człowiek projektuje zależności i zobrazowanie. Pulpit rysuje zobrazowanie, KZ liczy z układu. Ruch i KZ multiplayera są autorytatywne na serwerze. Singleplayer używa tego samego rdzenia, AI tylko na stycznych. Bez domyślnych uprawnień dla nieznanego celu.

Nie utożsamiaj Identyfikatora wersji klienta z dowodem uczciwości. Nie scalaj delt przez zgubienie ich baz. Nie przenoś czekania na sieć/DB do ticku. Nie zapisuj tokenów i haseł w dokumentacji.

## Konflikty do odnotowania
AGENTS i CLAUDE wskazują już aktualną regresję; --edtest/fuzz/dfuzz pozostały tylko w historycznym §4 PODZIAL_MODULOW. S00 mierzy aktualną bramkę po nowych kartach/KZ/PIP. Starszy PODZIAL_MODULOW wymaga commitów, lecz nowsza prośba właściciela zabrania ich. Nie aktualizuj ROZWOJ w trakcie planowania. Przy implementacji ponownie zweryfikuj polecenia i uzgodniony zakres.

## Przekazanie po podkroku
Zapisz poza Git, w raporcie etapu:
- etap/podkrok, data, stan Ukończony/Częściowy/Zablokowany i wersja planu;
- HEAD przed/po i lista rzeczywiście zmienionych plików, bez sekretów;
- spełnione wymagania i kryteria, polecenia testów i ścieżki wyników;
- znane błędy, czego nie przetestowano, otwarte decyzje;
- zmiany kontraktów i zgodność;
- dokładny następny mały krok i minimalne pliki do przeczytania;
- osobno: build, testy, commit/push, deployment i weryfikacja uruchomionej usługi.

Użyj wzoru SZABLON_PRZEKAZANIA.md. Statusy w manifest.json w chwili wydania planu wszystkie oznaczają „zaplanowany”; nie traktuj ich jak realizacji.

## Nowe wymaganie nadrzędne F14
Każdy posterunek ma własny moduł KZ. „Jeden autorytatywny świat” oznacza wspólny serwer, czas i fizykę, nie globalny komputer zależności. Przeczytaj 16_KZ_KAZDEGO_POSTERUNKU.md przy etapach S01-S07, S13 i rozwoju AI. Nie klonuj całego Simulator wraz z pociągami per posterunek. LCS agreguje wiele KZ.

F15 jest rozstrzygnięte: po oddaniu sterowania dyżurny LCS zachowuje ViewGrant i traci ControlLease do posterunku. Nie proponuj utraty podglądu jako „prostszego” wariantu. Wczytaj rozdział 17 przy S05-S07/S13. Samo oglądanie, pozycja nadzorcy ani reconnect nie przywraca sterowania.

## Korekta po audycie źródeł - wersja 1.3
F16: każdy pełny KZ ma dwie obliczające grupy, zasadniczą i gorącą rezerwę. Nie odraczać do HA/AI/rozwoju po pilocie. Dla S02A-S02D i zmian cyklu/restore przeczytać 18 i 19. Rozróżniać zestawy EBILock, kanały wewnętrzne, serwery EBISCREEN i 2z2 MOR-3.

Nie wywołuj starego Tick dwa razy nad wspólnymi urządzeniami. Obliczenia bez skutków → selektor → karty → jeden teren/ruch. Nie projektuj nowych reguł srk. Zrealizuj zatwierdzoną funkcję w dwóch niezależnych implementacjach Ada według zamrożonej specyfikacji. Wspólne są kontrakty i infrastruktura, nie kod algorytmów zależnościowych.

Aktualne decyzje PODZIAL_MODULOW §7 obowiązują: brak zapasowych włókien i automatycznego obniżania sygnałów, zaakceptowane opóźnienie cyklu, manewry według sygnałów później, rozprucie i utrata kontroli zgodnie z ustaleniami. Stare notatki nie przywracają odrzuconych pomysłów. Patrz 19.

## F17: znaczenie AI w funkcjach SUK
Automatyczny dyżurny i planista ruchu to autorski algorytm oparty na danych, regułach i automatach stanów, rozwijany na istniejącym Otoczenie/Rozklad. **Nie implementuj LLM, agenta LLM, API modelu językowego ani awaryjnego odwołania do modelu do prowadzenia ruchu.** To obowiązuje serwer i singleplayer offline, odpowiedzi proceduralne oraz wybór krzyżowania/wyprzedzania.

Algorytm odbiera jawne dane i meldunki dostępne jego roli, wybiera działanie według wersjonowanych reguł i przekazuje zwykłe polecenie przez prawa/KZ. Zapisuje przyczynę i wejścia do odtworzenia. STT/TTS, jeżeli później zlecone, są osobną warstwą audio bez władzy decyzyjnej. „AI” jako adresat tego dokumentu oznacza narzędzie programistyczne; nie jest technologią operatora SUK.

## Nowe ustalenia 1.5 - technologia i niezależność
KZ przepisujemy na Ada. Nie proponuj powrotu do wspólnego silnika C# jako docelowego uproszczenia. Reszta aplikacji pozostaje C#/.NET. Przeczytaj 20 i 23. Ada nie zastępuje algorytmu ruchu F17 i nie zmienia zakazu LLM.

Autor wariantu A wykonuje tylko S02A2 w osobnym nowym czacie; autor B tylko S02A3 w drugim nowym czacie. Obaj otrzymują tę samą zamrożoną specyfikację/ABI/dane/scenariusze. Nie wczytują kodu, patchy ani historii implementacji drugiego. Po zamrożeniu obu hashy integrator S02A4 może porównać wyniki i kod. Nie tworzymy teraz tych czatów ani implementacji: przygotowujemy instrukcje dla przyszłych zleceń.

W każdej grupie 1/2 uruchom A+B, nie kopiuj A do drugiego kanału. Losuj aktywną grupę per posterunek przy starcie świata po Ready i zapisz wybór. Nie losuj przy ponowieniu/reconnect/przekazaniu. S18 pełny admin i S19 dyspozytor są końcowymi etapami; minimalne S08 nadal poprzedza publiczny pilot.


## Doprecyzowanie 1.6
F23: osobny SUK.Admin WPF i prywatne API, konto ORAZ indywidualny klucz ORAZ kompetencja. EXE nie jest sekretem. ZATWIERDZONE przez właściciela 17.10.2026: prywatny klucz powstaje lokalnie na komputerze administratora, preferencyjnie niewyeksportowalny CNG/TPM; serwer otrzymuje publiczny klucz i wydaje certyfikat. Nie otwieraj ponownie wyboru miejsca generowania i nie implementuj serwerowego wydawania prywatnego klucza. Czytaj 21. Użyj sprawdzonego mTLS/token binding biblioteki po prototypie, nie własnych podpisów komend. S03/S08 przygotowują infrastrukturę N41A przed S13; pełny WPF/N41B pozostają w S18. Granice KZ/Ada i końcowy S19 bez zmian.

## Bieżący punkt startu aplikacji - 1.7
Przeczytaj 24_POROWNANIE_Z_NOWA_WERSJA i aktualny 02. Baza to 5eddb5c; 0-7/9/10 w podziale aplikacji są już obecne w źródłach, w tym KZ jako klasa, system nadrzędny, PIP i karty. Nie pisz ponownie tych modułów. S02A jest odbiorem i domknięciem pozostałych granic, a nie zleceniem ich nowej implementacji. Jeden KZ całego Station nadal nie spełnia per-posterunek/Ada/grup. Krok 8 odłożony; jego przejściowy adapter nie może sięgać do prywatnych kontekstów A/B.

AGENTS i CLAUDE są już zgodne co do aktualnych testów. S00 musi dopiero zmierzyć nową bazę; wyniki opisane w repo nie są testami tego audytu. Nowy N42/O20 blokuje pozostawienie starego serwisowego wejścia bez autoryzacji. Brak wskazanego PDF Ie-1 jest jawną bramką źródłową, nie zaproszeniem do wymyślania reguł. Zastany PDF bieżącego worktree C:\Users\JohnE\.codex\worktrees\3315\SUK pozostaje nietknięty. Właściciel później zlecił zastąpienie kopii planu w D:\Projects\SUK po kontroli; poprzedni PDF zachowano poza repo. Nie wykonuje się git add/commit/push. Wszystkie wcześniejsze decyzje właściciela pozostają w mocy.


## Start po podziale 1.8
Rozdział26 i manifest.jobs są właściwym poziomem wykonania. Wybierz jedno ID zadania, przeczytaj czaty/ID.md i prompty_czatow/ID.md. Stara karta etapu jest opisem nadrzędnym, nie poleceniem wykonania wszystkich jej podkroków naraz. Nie wczytuj całego PDF ani całego katalogu kodu do jednego czatu. Raporty i artefakty zależności muszą istnieć; planned nie oznacza ready lub done.
S02A2 i S02A3 to osobne linie A/B; nowy czat kontynuuje wyłącznie własny wariant. Prompty27 wskazują dozwolony neutralny pakiet i zakazane źródła. Samo otwarcie całego repo lub drugiego wariantu łamie izolację nawet przy innej nazwie czatu.
F24: każdy nowy moduł otrzymuje adapter logów, wersjonowane pola i próby limitów/redakcji według25. Ada zwraca pasywną ograniczoną diagnostykę do hosta; nie zapisuje pliku ani nie wykonuje callbacku I/O w Evaluate. Log techniczny nie zastępuje trwałego audytu/commit journal. Brak logów jest niespełnionym kryterium etapu.
