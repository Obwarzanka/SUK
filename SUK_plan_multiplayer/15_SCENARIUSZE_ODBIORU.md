# 15. Scenariusze odbioru multiplayera SUK

Status wszystkich scenariuszy: **PROJEKT PRZYSZŁEGO TESTU  -  NIE PRZEPROWADZONO**. Dokument nie potwierdza gotowości kodu ani infrastruktury. Uzupełnia [protokół](04_PROTOKOL.md), [role](06_LCS_I_ROLE.md), [łączność i AI](07_LACZNOSC_I_AI.md) oraz [testy](11_TESTY_I_OPERACJE.md).

## Wspólne przygotowanie i odczyt przez AI

Świat testowy: `worldId = 8af2ae67-9d5e-4fd2-a7f6-a94639384021`; posterunki A/B, bez nazw rzeczywistych. Pakiet, zależności i zobrazowanie zatwierdza człowiek. Testy wykorzystują kopię danych. Przed próbą zapisuje się wersję programu, contentHash, sessionEpoch, konfigurację praw i rozstrzygnięte decyzje D01-D14 z [rejestru](13_DECYZJE_I_RYZYKA.md).

Dla AI: każdy scenariusz określa zdarzenia wejściowe, obserwowalne wyniki i materiał dowodowy. Nie uzupełniaj brakującej procedury kolejowej własnym domysłem. Bez wymaganej decyzji oznacz test „niedopuszczony do wykonania”. Wynik techniczny i zgodność srk oceniaj osobno. Dowody zawierają serverTick, correlationId oraz właściwe requestId/messageId; bez tokenów i haseł. Same zrzuty nie dowodzą pojedynczego skutku serwerowego.

## O01. Jeden pociąg na granicy A/B  -  S02/S04/S07

**Wejście:** dwa komputery, odcinkowi A i B, zatwierdzony szlak łączący, jeden pociąg o stałym trainId; dozwolony podgląd granicy.

**Czynności:** gracze prowadzą ruch według zatwierdzonego scenariusza. Pociąg przechodzi przez granicę; przez część próby czoło jest w B, koniec w A. Serwer publikuje meldunki. Jeden klient chwilowo spowalnia odbiór.

**Wynik:** jeden pociąg i jeden właściciel jego stanu; poprawna zajętość pod całym składem. Granica obszaru praw nie tworzy ani nie usuwa taboru. Po synchronizacji oba pulpity pokazują zgodne wersje wspólnych obiektów; wolny klient nie zatrzymuje świata.

**Dowód:** zapis osi czasu pociągu i odcinków, meldunki z tickami, zrzuty obu pulpitów.

**Decyzja srk:** przygotowanie przejazdu, oczekiwane zajętości i zwalnianie ustala właściciel.

## O02. Oddzielna autoryzacja końców blokady  -  S05

**Wejście:** A i B mają różnych operatorów i lease; dokładne blockEndId; zamknięta D03 dla wariantu Internal.

**Czynności:** operator A wysyła dozwoloną komendę na swój koniec. Następnie próbuje wskazać koniec B przez ID, etykietę i kontekst obrazu. B wykonuje własną czynność. Serwer sprawdza rozpoznany cel przed kolejką i przed wykonaniem.

**Wynik:** cudzy koniec odrzucony bez skutku; pageId nie zwiększa praw. Poprawna sekwencja obu operatorów daje wynik zgodny z ustalonym modelem blokady, bez automatycznego założenia wspólnego dyżurnego.

**Dowód:** koperty, odmowy, audyt celu i porównanie stanu przed/po.

**Decyzja srk:** kolejność i skutek Wbl/Pzk po rozdzieleniu końców określa D03; dokument ich nie ustanawia.

## O03. Odcinkowy, pomocniczy i nadzorca  -  S05

**Wejście:** zatwierdzone D01/D02; odcinkowy posiada A, pomocniczy ma wskazaną delegację, nadzorca podgląd A/B.

**Czynności:** każda osoba wykonuje dozwoloną czynność i próbę poza zakresem. Pomocniczy próbuje rozszerzyć delegację. Nadzorca próbuje sterować samym uprawnieniem podglądu. Administrator kont wykonuje analogiczną próbę bez roli operacyjnej.

**Wynik:** serwer respektuje macierz; podgląd i administracja kont nie dają sterowania. Nie powstają sprzeczne delegacje do tego samego urządzenia. Zatwierdzona pomoc pozostaje możliwa.

**Dowód:** wersja macierzy, lease, decyzje autoryzacji, zrzut podglądu nadzorcy.

**Decyzja srk:** katalog pomocy, odpowiedzialność i ewentualne przymusowe przejęcie wymagają D02.

## O04. Przekazanie podczas potwierdzania  -  S05/S06

**Wejście:** A ma właściciela X; Y może zażądać przekazania. Wybrane polecenie specjalne wymaga zatwierdzonego potwierdzenia.

**Czynności:** X inicjuje polecenie. Y żąda obszaru, X wyraża zgodę. Po atomowym przekazaniu X wysyła stare confirmationId i opóźnione żądanie. Y próbuje potwierdzić identyfikator X.

**Wynik:** controlEpoch rośnie; oba stare potwierdzenia i polecenia tracą uprawnienie. Nie wykonuje się ukryty skutek po zmianie obsady. Y może rozpocząć nową świadomą czynność zgodnie z bieżącym stanem. Przekazanie nie resetuje terenu.

**Dowód:** kolejność ticków, audit transferu, wyniki requestId i confirmationId, stan urządzenia.

**Decyzja srk:** wybór polecenia i dopuszczalny moment przekazania zatwierdza właściciel.

## O05. Utrata łącza i powrót operatora  -  S06/S07

**Wejście:** zatwierdzona D04; znane progi świeżości, rezerwacji i odzyskania. W świecie trwa ruch.

**Czynności:** zerwać łącze X, osobno zatrzymać dopływ meldunków przy żywym połączeniu. Wykonać wariant powrotu przed końcem rezerwacji oraz po prawidłowym przydziale Y. Spróbować odtworzyć stare żądania.

**Wynik:** nieaktualny pulpit pokazuje brak danych i blokuje komendy. Serwer kontynuuje świat bez transportowego resetu. Powrót wymaga synchronizacji; X nie odzyskuje obszaru Y. Brak podwójnej obsady i automatycznego replay.

**Dowód:** ślad sieci, świeżość ticków, epoki, historia przydziałów i stan przed/po.

**Decyzja srk:** działania po utracie obsady i przejęcie przez AI/nadzorcę wynikają wyłącznie z D04.

## O06. Niepewny wynik i ponowienie  -  S04/S06

**Wejście:** jedna dozwolona komenda, utrwalony stan początkowy, polityka trwałości D14.

**Czynności:** zerwać odpowiedź po przyjęciu żądania; ponowić identyczny requestId, następnie ten sam ID z inną treścią. Oddzielnie wymusić restart przed i po zapisie wyniku.

**Wynik:** w tej samej epoce identyczne ponowienie nie daje drugiego skutku; zmieniona treść jest odrzucona. Po restarcie obowiązuje nowa epoka i jawna resynchronizacja. UI rozróżnia Queued, Executed i wynik nieznany; nie ogłasza sukcesu po samym odebraniu ramki.

**Dowód:** liczba skutków w stanie, dziennik wyników, granice restartu i obraz UI.

**Decyzja srk:** wybór komendy testowej; polityka trwałości jest decyzją techniczną D14.

## O07. Człowiek rozmawia z AI  -  S14/S15

**Wejście:** zatwierdzony katalog D05 i zakres D12; człowiek na A, regułowy operator AI na B, skonfigurowane urządzenia.

**Czynności:** człowiek zestawia połączenie i nadaje dopuszczony komunikat z parametrami. AI odbiera, sprawdza kontekst i odpowiada. Powtórzyć z brakującym parametrem, niewłaściwym odbiorcą i obcym endpointId. Ewentualna czynność AI przechodzi zwykłą kolejkę komend.

**Wynik:** prawidłowa wymiana jest skorelowana; niepełna nie staje się zgodą. AI nie mutuje KZ, a obcy odbiorca nie otrzymuje rozmowy. Opóźnienie AI nie blokuje ticku.

**Dowód:** komunikaty domenowe, statusy rozmowy, odmowy ACL, wynik KZ i czasy.

**Decyzja srk:** treść, wymagane potwierdzenia i skutki uzgodnienia pochodzą z zatwierdzonego katalogu.

## O08. Delivered nie oznacza zgody ruchowej  -  S09/S11/S14

**Wejście:** rozmowa A/B, uzgodnione znaczenia Delivered, Read i AcceptedOperationally oraz warunki czynności.

**Czynności:** doręczyć tekst, pozostawić nieodczytany; potem odczytać bez akceptacji. Nadać zwykły tekst sugerujący zgodę. Przy głosie odtworzyć wypowiedź bez świadomego potwierdzenia; odłączyć odbiorcę przed akceptacją.

**Wynik:** transportowe dostarczenie, odczyt i rozpoznana mowa nie tworzą uzgodnienia. Dowolny tekst nie otwiera semafora. Prawidłowa akceptacja jest odrębnym zdarzeniem domenowym; późniejsza komenda nadal podlega KZ.

**Dowód:** oś statusów messageId, brak nieuprawnionych zmian świata, nagranie UI; bez nagrywania głosu bez odrębnej polityki.

**Decyzja srk:** kto i jak potwierdza konkretny komunikat określa D05.

## O09. Singleplayer offline i tylko styczne  -  S02/S15

**Wejście:** lokalny pakiet A ze zdefiniowanymi stycznymi, brak konta, sieć wyłączona; ten sam build rdzenia co host.

**Czynności:** uruchomić lokalnie zatwierdzony scenariusz, wymienić komunikat z sąsiadem i przeprowadzić pociąg. Sprawdzić listę utworzonych obszarów i procesów. Porównać kontrolowany przebieg z tym samym scenariuszem rdzenia na hoście.

**Wynik:** gra działa bez Internetu i chmury; symuluje zadeklarowany obszar oraz uproszczone styczne, bez rozszerzania świata. Zgodne wejścia dają oczekiwane wyniki wspólnego rdzenia. Lokalne osiągnięcia nie trafiają jako zaufane statystyki konta.

**Dowód:** wersje, dzienniki scenariuszy, zakres obszaru i brak wymaganych połączeń.

**Decyzja srk:** dopuszczalne uproszczenia sąsiada i oczekiwane wyprawienia zatwierdza właściciel.

## O10. Przyszłe krzyżowanie i wyprzedzanie AI  -  S16

**Wejście:** dopiero po S15; zamknięta D13, zatwierdzone warianty infrastruktury, rozkładu, opóźnień i miar jakości.

**Czynności:** w wariancie pierwszym podać konflikt pociągów przeciwnych, w drugim potrzebę wyprzedzenia. Zmienić dostępność toru po propozycji planisty. Równolegle człowiek przejmuje obszar zgodnie z procedurą.

**Wynik:** AI proponuje uzasadniony plan w budżecie CPU; aktualizuje go po zmianie warunków. Rezerwacja planisty nie utwierdza przebiegu. KZ odrzuca niewykonalne polecenie. Po utracie lease AI nie steruje dalej; nie współdzieli urządzenia z człowiekiem.

**Dowód:** wejścia planisty, uzasadnienie, czas obliczeń, komendy, odrzucenia KZ i historia lease.

**Decyzja srk:** miejsce, kolejność, priorytety i miary poprawności ustala D13. Do tego czasu scenariusz pozostaje niewykonywany i nie blokuje wcześniejszej infrastruktury.


## Uzupełnienie audytu 1.3 - scenariusze grup
### O11. Uszkodzenie aktywnej grupy podczas przebiegu
Warunki: pełny KZ, obie grupy liczą, potwierdzona ReadyReserve, utwierdzony przebieg i jeden pociąg. Zapisz inputSeq, selectionEpoch, effectId i prawa. Wstrzyknij niezgodność kanałów aktywnej grupy na określonej granicy. Oczekuj jednego przełączenia, zachowania utwierdzeń i świata oraz braku drugiego skutku. Powtórz bez gotowej rezerwy, według zatwierdzonego D17. Dowody: log faz, diagnostyka, delta świata i czasy; N25/N26/N28.

### O12. Stary plan, ZeroLO i rebuild
Warunki: zatwierdzona próba nieidempotentnego ZeroLO na kopii danych testowych. Zatrzymaj plan starej grupy, przełącz, zatwierdź skutek nową, dostarcz opóźniony i powielony plan. Odtwórz dawną grupę ze snapshotu/logu zawierającego ten skutek. Oczekuj jednego rozkazu, wyniku i wpisu; odtwarzanie bez zapisu do terenu. Warianty: luka logu, inny hash i zatrzymane obliczenia. Odbierz prawa między obliczeniem a zatwierdzeniem, przekaż Rejected do obu grup i przełącz: brak skutku oraz pozornego utwierdzenia; odrzucona kopia rezerwy nie odrzuca wykonanej logicznej komendy. Dowód: liczniki przyjęć/odrzuceń, watermark i brak Ready przed zgodnością; N29/N30.

### O13. Przekazanie LCS, ban i restore
A oddaje posterunek B, zachowując ViewGrant. W kolejce jest polecenie/potwierdzenie A sprzed przekazania. Przełącz grupę. Przy zwykłym przekazaniu oczekuj aktualnego podglądu A, odmowy jego sterowania, właściwych praw B i jednej statystyki. Osobny wariant zakazu sterowania danym posterunkiem zachowuje podgląd tylko według zatwierdzonej polityki sankcji. Wariant bana konta zamyka sesję i odcina dostęp; F15 nie daje wyjątku od bana. Checkpoint/restore podczas jazdy przez granicę KZ: nowe epoki, jeden pociąg, brak odtworzonych starych praw/skutków. Dowód: audyt praw/efektów, obie grupy i meldunki obu klientów; N24/N31/N33.

### O14. Pętla, diagnostyka i zasoby
Ręcznie zatwierdzona testowa pętla z dwoma kierunkami, koncentratorami i kartami. Przerwij jedną drogę, następnie drugi odcinek izolujący wybrane obiekty. Oczekuj osiągalności według danych, lokalnej utraty łączności i diagnostyki bez resetu LCS. Przełącz i odbuduj grupę podczas pełnego obciążenia; pełne zestawy stale liczą. Próba docelowa 40 klientów/8 h, osobna próba zapasu 80 i offline. Dowód: topologia, osiągalność, CPU/RAM/p99 faz i ograniczony log; N32/N34. Nierozstrzygnięte reakcje obiektów blokują zatwierdzenie scenariusza.

## O15. Odtwarzalny algorytm bez modelu językowego
Warunki: osobne zlecenie i ukończona infrastruktura S13; jawny pakiet danych, rozkład, wersja reguł i meldunki testowe. Nie używać station.json właściciela. Sprawdzić zależności i konfigurację modułu: brak wykonawcy/API LLM oraz zapasowego odwołania do modelu.

W S14 powtórzyć rozmowę proceduralną z tymi samymi parametrami, w S15 ten sam przypadek obsady na serwerze i offline, a w S16 zatwierdzony konflikt krzyżowania/wyprzedzania. Zamknąć dostęp do zewnętrznych modeli, pozostawiając multiplayerową sieć gry. Oczekiwane: te same decyzje/przyczyny przy tych samych wejściach, odpowiedzi z katalogu/szablonów i komendy wyłącznie przez prawa/KZ.

Warianty: brak danych, niedostępny wariant przejazdu i limit czasu CPU. Oczekiwany jawny wynik z zatwierdzonego profilu; brak „ratowania” decyzji LLM i brak ominięcia zależności. Materiał dowodowy: wersje, log decyzji/wejść, porównanie powtórzeń i obserwacja wywołań sieciowych. Odbiór N35; funkcje pozostają zaplanowane.

## O16. Ada, niezależne programy i losowany start
Warunki: zamrożony kontrakt S02A1, dwa osobne raporty i źródła A/B, ręcznie zatwierdzone kopie testowe. Najpierw N36 granice ABI, potem oba warianty i referencja na tych samych wejściach. Każdą rozbieżność uzasadnić specyfikacją; znany błąd starego C# nie staje się normą. Odbiór po zamrożeniu obu wariantów i brak produkcyjnego fallbacku C#.

Po S02B/C uruchomić oba A/B w obu grupach. Wymusić testowo wybór 1 i 2, a następnie sprawdzić rzeczywiste losowanie i zapis StartupSelection per KZ. Odtworzyć dziennik: te same role i decyzje bez ponownego RNG. Osobno nowy worldRun po restore, tylko jedna gotowa grupa, reconnect i oddanie obsady. Warianty N25-N34 zachowują jeden fizyczny skutek, utwierdzenia i kontrolę kanałów. Dowody: hashe/specyfikacje/raporty niezależnego autorstwa, porównanie wyników, log wyboru i skutków, użycie pamięci. N36/N37/N40.

## O17. Końcowy panel administracyjny
Warunki: S16/S17 ukończone oraz istniejące konta/moderacja S08. Uruchomić osobny WPF z ograniczoną rolą, spróbować obcej akcji oraz żądania bez wymaganego certyfikatu/tokena i nadać/odebrać rolę podczas sesji. Na kilku światach wydać tę samą sankcję dwukrotnie, odłączyć jeden świat i media. Oczekiwać odmów, jednej logicznej sankcji i osobnych Saved/Applied bez pozornego globalnego sukcesu. Niedostępny świat po powrocie synchronizuje sankcje przed komendami. Zwykły admin nie steruje KZ. Stronicowanie/eksport nie blokuje ticku. Dowód: audit, aktualne sesje/ACL, log usług i metryki projekcji; N38.

## O18. Końcowy dyspozytor
Warunki: S18 ukończony, zatwierdzony D19 i role rozłączne. Dyspozytor ogląda zakres okręgu/grafik i wydaje typowaną dyspozycję do człowieka oraz F17. Wstrzymać odbiorcę w Received: brak akceptacji i skutku KZ. Przetestować odmowę, zaakceptowaną realizację, zmianę warunków, anulowanie i reconnect. Dyspozytor bez ControlLease nie nastawia urządzeń ani nie przełącza grup; algorytm działa bez LLM przez zwykłe prawa/KZ. Nieaktualne dane są oznaczone i blokują czynności wymagające świeżego kontekstu. Dowód: zrzuty zakresu/grafiku i stanów, log dyspozycji/praw/efektów; N39.


## O19. Rejestracja i utrata klucza admina
Warunek: profil D18 i dwa konta/urządzenia. Samo testowo przejęte hasło przed enrollmentem nie daje dostępu bez zaproszenia/MFA i zatwierdzenia klucza. Powtórz/równocześnie zużyj zaproszenie, podmień klucz. Po poprawnej rejestracji sprawdź rzeczywisty token binding, pojedyncze dowody oraz parę cert A+konto B. Cofnij klucz/rolę/token przy otwartej sesji i oczekującej sankcji. Oczekuj odmów, mierzonego odcięcia i jednej sankcji Saved/Applied. Utrata TPM wymaga revoke i nowego enrollmentu, nie obejścia hasłem/eksportu starego klucza. Dowód: audyt userId/keyId/sessionId/requestId bez sekretów. N41A przed pilotem, N41B pełne S18. To scenariusz, nie wynik wykonania.

## O20. Zamknięcie starego wejścia Komputerowni
Warunki: nowa brama i minimalne prywatne narzędzie moderacji/instruktora. Z zewnątrz, bez konta, zgłosić tryb serwisowy i żądania diagnostyki/usterki, również przez zgodność ze starym protokołem. Oczekiwać odmowy bez danych chronionych i bez zmiany urządzeń. Konto tylko do podglądu próbuje mutacji - odmowa; instruktor działa tylko w swoim zakresie. Cofnąć klucz/rolę przy otwartym kanale i oczekującej usterce; ponowić requestId oraz przeciążyć żądaniami diagnostyki. Oczekiwać jednego autoryzowanego skutku, skutecznego revoke i zachowanego postępu ticku. Dowód: konfiguracja listenerów, decyzje praw, kontrolowany stan i metryki, bez sekretów. N42 przed S13. To projekt próby, nie opis wykonanego ataku.


## O21. Logi każdego modułu i awarie zapisu
Warunki: adaptery F24 dla wszystkich modułów zakresu. Wygenerować normalny błąd, odmowę i zdarzenie cyklu oraz nadmiar diagnostyki. Wprowadzić testowy sekret, CR/LF, rekord ponad limit, wolny/pełny dysk i crash. Porównać liczniki/JSONL, retencję i korelację; bez sekretów oraz bez nieograniczonej pamięci i blokady ticku. Oddzielnie sprawdzić krytyczny audyt i commit przed Applied według S06. Cztery konteksty KZ mają odrębną diagnostykę i jeden audyt logicznego skutku. Offline działa z lokalnym adapterem. Dowód: zredagowane logi, metryki, test odzyskania; N43/N44. To projekt, nie wynik.

## O22. Przekazanie jednego zadania nowemu czatowi
Warunki: ukończone i udokumentowane zależności wybranego jobId. Nowy czat dostaje wyłącznie prompty_czatow/jobId.md, odpowiednią kartę i wskazane wejścia. Musi wskazać wersję/hash specyfikacji, lokalny zakres, testy i wyjście bez odtwarzania całej rozmowy. Brak raportu lub niezakończona zależność blokuje realizację, a nie skłania do zgadywania. Raport nowego zadania pozwala rozpocząć następne. Dowód: kontrola pakietu i raportów; N45.

## O23. Dwa niezależne łańcuchy autorów KZ
Warunki: zatwierdzony i zamrożony neutralny pakiet S02A1. W osobnych środowiskach udostępnić A i B wyłącznie ich własną historię, neutralną specyfikację, ABI i przypadki. Oba używają tego samego hasha; brakująca reguła lub źródło wraca do integratora. W próbie negatywnej ujawnić obcy kod albo zmienić hash: zatrzymanie danego zakresu i udokumentowane odtworzenie czystego pakietu. Po zakończeniu zamrozić oba warianty, dopiero integrator S02A4 widzi oba i porównuje. Dowód: manifesty wejść, dostępu i wyników oraz raporty; N46. To nie dowód SIL ani test już wykonany.
