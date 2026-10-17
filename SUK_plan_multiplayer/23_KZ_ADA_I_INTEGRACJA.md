# 23. KZ w Ada i integracja z SUK

**Plan 1.8, 17.10.2026. USTALONE przez właściciela:** komputer zależności przepisujemy na język Ada. Nie jest to otwarty wybór technologii. Ada 2022, natywna DLL Windows x64 oraz poniższy sposób integracji są rekomendacją wykonawczą. Dokument nie potwierdza implementacji, instalacji narzędzi ani testów. Nie upoważnia do zmian repozytorium, Git ani wdrożenia.

## 1. Zakres języka i odpowiedzialności

| W Ada | W istniejącym C#/.NET |
|---|---|
| Warunki przebiegów, utwierdzenia, zwalnianie i ochrona boczna | Pulpit WPF, edytory, zobrazowanie i PIP |
| Logiczne timery, Sz, zastopowania i zamknięcia | Rzeczywisty zegar wykonania i harmonogram świata |
| Logika blokad oraz stan lokalnego końca interfejsu KZ-KZ | Transport komunikatów, karty, teren i zasilanie |
| Stan obu grup i kanałów A/B, kontrola zgodności i domenowy selektor grup | Jeden ruch pociągów, rozkład, otoczenie i obsada AI |
| Stan proponowany/zatwierdzony oraz logiczny snapshot | Konta, autoryzacja, SignalR, baza, audio, aktualizator |
| Diagnostyka obliczeń i wybrany plan rozkazów | Bramka wykonania: prawa, fencing, effectId; audyt i obsługa procesu |

Ada otrzymuje czas symulacji i meldunki urządzeń; nie uruchamia własnego zegara ściennego, nie przesuwa pociągu i nie odczytuje listy Trains. Host wykonuje fizyczny skutek przez kartę dokładnie raz. Granica obejmuje wcześniejsze kroki PODZIAL_MODULOW, nie znosi zatwierdzonych reguł srk. Nowy język nie jest powodem projektowania zależności ani stacji automatycznie.

R02 oznacza ponowne wykorzystanie rozpoznanych algorytmów, danych projektowych, kontraktów i testów. Kod Ada będzie nową implementacją zatwierdzonej funkcji, a nie nowym modelem prowadzenia ruchu. WPF, ruchu i usług kont nie przepisujemy przy okazji.

## 2. Narzędzia i wynik budowania

Rekomendujemy Ada 2022 z przypiętym GNAT i gprbuild, budującymi natywnie dla Windows x86-64. Dokumentacja GNAT opisuje wybór wersji języka, a Alire dostarcza narzędzia dla Windows x64 i integrację z MSYS2. Nie utożsamiamy Alire z kompilatorem ani GNAT Studio z wymaganiem uruchomienia serwera. [Wersje Ada w GNAT](https://gcc.gnu.org/onlinedocs/gnat_ugn/Compiling-Different-Versions-of-Ada.html), [Alire na Windows](https://alire.ada.dev/docs/getting-started).

S02A1 sprawdza granicę na neutralnym technicznym stubie i harnessie, bez implementowania reguł przyszłego A/B. Zapisze dokładne wersje, pochodzenie, hashe instalatorów, konfigurację .gpr, ustawienia sprawdzeń oraz listę bibliotek wykonawczych. Narzędzia można pozyskać przez Alire albo utrzymywany pakiet GNAT; konkretną dystrybucję zatwierdza prototyp i weryfikacja warunków redystrybucji. Nie używamy nieprzypiętego „latest”. Klient końcowy nie instaluje kompilatora ani MSYS2.

Artefakt: jedna SukKz.dll dla wszystkich kontekstów, nagłówek C, adapter .NET, manifest engineVersion/abiVersion i niezbędne zależności wykonawcze. GPRbuild obsługuje bibliotekę dynamiczną i interfejs biblioteki autonomicznej; projekt ma jawnie ustalić Library_Kind, Library_Interface i sposób inicjalizacji. Nie kopiować niesprawdzonych nazw symboli bindera między konfiguracjami. [Projekty bibliotek GPRbuild](https://docs.adacore.com/gprbuild-docs/html/gprbuild_ug/gnat_project_manager.html#library-projects).

DLL ładuje wyizolowany SUK.WorldHost C#. Brama internetowa i zwykły pulpit multiplayer nie wywołują jej bezpośrednio. Singleplayer korzysta z tego samego adaptera i biblioteki lokalnie, bez konta i Internetu. Nie dodajemy procesu ani IPC dla każdego KZ.

## 3. Grupy i kanały

**Nowe ustalenie właściciela:** powstają dwie implementacje Ada, Kz_A i Kz_B, w dwóch osobnych przyszłych czatach. Autor A nie czyta kodu B, a autor B kodu A. Oba czaty dostają tę samą zamrożoną neutralną specyfikację, kontrakty, zatwierdzone źródła/reguły i katalog prób. Pełny stary kod C# analizuje zespół specyfikacji i integracji; nie służy autorom A/B jako wspólny przepis tłumaczony linia w linię. Nie dziedziczą kontekstu implementacyjnego drugiego. Wspólne mogą być DTO, definicje i adapter ABI; nie wspólny silnik warunków zależności ukryty pod dwoma nazwami pakietów.

Każdy pełny KZ ma Group1 i Group2. **Każda grupa wykonuje oba kanały A i B**, a nie tylko jeden z nich: G1/A, G1/B, G2/A, G2/B. Cztery stany dynamiczne są prywatne; definicje wspólne są niemutowalne. Jedna DLL i jeden runtime GNAT zawierają pakiety Kz_A, Kz_B oraz koordynator AdaGroupRuntime. Dwa czaty są sposobem organizacji pracy, nie dowodem niezależności zespołów, różnorodności wszystkich błędów ani SIL.

Jeden wykonawca świata wywołuje Evaluate raz na KZ i cykl; AdaGroupRuntime wykonuje oba algorytmy w obu grupach na uporządkowanym CycleInput. Nie tworzymy Ada task ani wątku systemowego na grupę, kanał czy posterunek. Nie kopiujemy terenu, ruchu i rozkładu do Ada.

AdaGroupRuntime porównuje kanały, kwalifikuje zdrowie/synchronizację i prowadzi **domenowy wybór grupy**. C# OutputGate nie rozstrzyga zależności: weryfikuje pochodzenie, epoki, effectId i aktualne prawa, po czym dopuszcza jeden wybrany plan do kart. Brak zgodności A/B nie może zostać pominięty przez wybór „głównego algorytmu”.

Przy starcie WorldHost, **dopiero po Ready obu grup**, C# wyznacza per KZ `RandomNumberGenerator.GetInt32(2) + 1` i zapisuje StartupSelection jako jawne wejście Ada. Rejestr obejmuje worldRunId, kzId, chosenGroup, selectionEpoch, czas oraz hashe konfiguracji/definicji/algorytmów. Metoda zwraca wartości od zera do górnej granicy wyłącznie. [Dokumentacja RNG](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.randomnumbergenerator.getint32?view=net-10.0).

Replay korzysta z zapisanego wyboru bez ponownego losowania. Nowy przebieg serwera po odtworzeniu najpierw uzgadnia obie grupy, a potem zapisuje nowe losowanie dla nowego worldRunId; nie wykonuje historycznych wyjść. Restart jednej grupy, reconnect i przekazanie stanowiska nie losują grupy ponownie. Nie wybiera jej klient. Brak dwóch gotowych grup blokuje zwykłą procedurę startowego losowania; trybu zdegradowanego nie domyślamy się poza zatwierdzoną polityką.

Grupy 1/2, kanały A/B, EBILock, MOR-3 i fizyczne HA Windows pozostają rozdzielone. Ada i dwa czaty nie odtwarzają automatycznie niezależnego sprzętu ani certyfikowanego systemu bezpieczeństwa.

## 4. C ABI i własność danych

Rekomendujemy wersjonowane C ABI, eksporty Ada z Convention => C i stałą nazwą zewnętrzną. Interfaces.C służy odwzorowaniu typów C; rekordy mają uzgodnioną reprezentację. [Interfejs Ada-C](https://learn.adacore.com/courses/intro-to-ada/chapters/interfacing_with_c.html).

Nagłówek C jest kontraktem: int32_t/uint32_t/int64_t/uint64_t, jawne długości i liczby elementów. Nie przekazujemy Ada String, kontenerów, nieograniczonych tablic, klas .NET, bool o nieustalonej reprezentacji ani obiektów runtime. Testy potwierdzają sizeof, wyrównanie i offsetof po obu stronach. Win64 ma jeden natywny sposób wywołania; jawna konwencja C pozostaje częścią deklaracji. [GNAT i Windows DLL](https://docs.adacore.com/gnat_ugn-docs/html/gnat_ugn/gnat_ugn/platform_specific_information.html#microsoft-windows-topics).

C# jeden raz czyta i waliduje plik projektu. Do Ada przekazuje wersjonowane rekordy i tablice definicji, statusów oraz znormalizowanych komend. Nie piszemy drugiego parsera JSON ani komend Ie-104. Ada kopiuje inicjalizujące definicje do własnej pamięci i traktuje je jako tylko do odczytu; nadal kontroluje zakresy, odwołania i limity. DLL nie zatrzymuje wskaźników do przypiętej pamięci zarządzanej po zakończeniu wywołania.

Przykład szkicu, bez implementacji i pełnych definicji domenowych:

~~~c
#include <stdint.h>
typedef void *suk_context;
typedef void *suk_result;
typedef struct SukDefinitionsV1 SukDefinitionsV1;
typedef struct SukCycleV1 SukCycleV1;
typedef struct {
    uint32_t kind, object_id;
    uint64_t effect_hi, effect_lo;
    int64_t argument;
} SukOutputV1;

uint32_t suk_abi_version(void);
int32_t suk_runtime_start(void);
int32_t suk_context_create_v1(
    const SukDefinitionsV1 *defs, suk_context *created);
int32_t suk_evaluate_v1(
    suk_context ctx, const SukCycleV1 *input,
    suk_result *result);
int32_t suk_result_size_v1(suk_result result, uint32_t *count);
int32_t suk_result_copy_v1(
    suk_result result, SukOutputV1 *dst,
    uint32_t capacity, uint32_t *required);
void suk_result_release_v1(suk_result result);
void suk_context_release_v1(suk_context ctx);
int32_t suk_runtime_stop(void);
~~~

SukDefinitionsV1 i SukCycleV1 muszą mieć struct_size, schema_version oraz opisane pary pointer/count; żaden wskaźnik nie jest adresem urządzenia udostępnianym klientowi. Status funkcji jest ustalonym kodem int32, nie tekstem ani LastError. Kody obejmują OK, InvalidArgument, AbiMismatch, BufferTooSmall, LimitExceeded, InvalidState i InternalFault.

Evaluate wykonuje obliczenie tylko raz i zwraca stabilny, niemutowalny ResultHandle. Size/Copy czytają ten sam wynik. BufferTooSmall zwraca required bez częściowego zapisu i bez ponownego Evaluate, zmiany timera lub nowego effectId. Limity obejmują wejścia, wyniki, aktywne uchwyty i pamięć; przepełnienie jest jawnym błędem.

Ada alokuje i zwalnia własny wynik przez eksport release. Host nigdy nie używa do niego Marshal.FreeHGlobal ani free z innej biblioteki. ResultHandle posiada samodzielną kopię wyniku; jego odczyt nie zależy od późniejszej mutacji kontekstu KZ.

## 5. Adapter .NET i cykl życia

Adapter używa LibraryImport oraz jawnego UnmanagedCallConv z CallConvCdecl; buforom towarzyszą długości. Publiczne API aplikacji ukrywa wskaźniki. SafeHandle zarządza ContextHandle i ResultHandle; ReleaseHandle wywołuje odpowiedni eksport Ada. Przy out/return używanym przez generator trzeba zapewnić wymagany publiczny konstruktor bezparametrowy. [Generator P/Invoke](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/pinvoke-source-generation), [SafeHandle i zalecenia](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/best-practices), [Konstruktor SafeHandle](https://learn.microsoft.com/en-us/dotnet/core/compatibility/interop/8.0/safehandle-constructor).

Inicjalizacja runtime/elaboracja odbywa się jawnie raz dla biblioteki w procesie, przed tworzeniem kontekstów. Konfiguracja nie może równocześnie wykonać automatycznej i ręcznej inicjalizacji. Wrapper wywołuje procedury inicjalizacji/finalizacji wygenerowane przez właściwy binder; mogą mieć nazwy zależne od biblioteki. Nie inicjalizujemy GNAT ponownie dla każdego KZ. Nie tworzymy zadań w DllMain. [Elaboracja biblioteki](https://gcc.gnu.org/onlinedocs/gcc-16.1.0/gnat_ugn/Ada-DLLs-and-Elaboration.html).

Shutdown: zatrzymanie nowych wywołań, zakończenie bieżącego cyklu, jawne Dispose wyników i kontekstów, kontrola liczników zasobów, finalizacja bindera. DLL pozostaje załadowana do końca procesu. Ponieważ finalizer SafeHandle może działać na innym wątku, release musi być bezpieczny i nie wywoływać Evaluate; runtime_stop odmawia finalizacji przy żywych uchwytach lub wywołaniach. Nie wolno rozładować kodu przed możliwym późnym release.

Każde wejście Ada ma osłonę wyjątków zamieniającą przechwytywalny błąd na status i ograniczoną diagnostykę. Żaden wyjątek Ada nie może przejść przez P/Invoke. Prototyp obejmuje błędy elaboracji i zamykania. Naruszenie pamięci, awaria natywna lub zawieszenie mogą zatrzymać cały WorldHost; nie obiecujemy ich naprawy przez catch w C# ani przedstawienia jako lokalnej usterki KZ. Watchdog wykrywa brak postępu; odzyskanie procesu podlega S06.

## 6. Zatwierdzanie i zapis

Evaluate proponuje przejście i rozkazy; nie wykonuje fizycznych skutków. Osobny eksport przyjęcia uporządkowanej decyzji Committed/Rejected aktualizuje zatwierdzony stan kontekstów według rozdziału 18. Kontrola zgodności kanałów, domenowa decyzja selektora Ada i fencing hosta są różnymi krokami. Wszystkie kanały otrzymują tę samą decyzję; rebuild odtwarza również decyzje, bez odgrywania rozkazów.

Snapshot Ada jest logicznym, wersjonowanym formatem z wersją algorytmu, danymi identyfikującymi definicje, stanem grup/kanałów i sekwencjami wejść. Nie jest zrzutem surowego heapu, adresów, rekordów z paddingiem ani uchwytów. Po restore powstają nowe uchwyty i właściwe epoki. Snapshot zostaje częścią wspólnego checkpointu terenu, ruchu, selektora Ada, watermarks hosta i wiadomości; sam plik Ada nie odtwarza świata.

## 7. Etapy i dowody

Zachowujemy istniejące identyfikatory. Obowiązkowa ścieżka rozszerza się do:

**S02A → S02A1 → {S02A2 i S02A3 niezależnie} → S02A4 → S02B → S02C → S02D → S04.**

- **S02A1 - toolchain, wspólna specyfikacja i ABI:** przypięty GNAT/gprbuild Windows x64; minimalna DLL i adapter; test inicjalizacji, układu rekordów, błędnego ABI, limitów, uchwytów, GC/shutdown i brakujących zależności. Specyfikacja nie zawiera kodu przyszłego A ani B.
- **S02A2 - implementacja A:** pierwszy osobny czat pisze Kz_A ze wspólnej specyfikacji. Małe części: model/timery, przebiegi/utwierdzenia/ochrona, sygnały/blokady. Oddaje wyniki i ograniczenia; nie przekazuje autorowi B swojego kodu.
- **S02A3 - implementacja B:** drugi osobny czat pisze Kz_B z tej samej bazy wymagań i testów, bez odczytu Kz_A. Test zależności pakietów wyklucza wspólną implementację reguł udającą dwa kanały. Kolejność kart nie daje zgody na skopiowanie A.
- **S02A4 - integracja, zgodność i migracja:** dopiero tutaj integrator porównuje A/B oraz referencję C# na identycznych strumieniach. Bada przyjęcia/odrzucenia, rozkazy, zwalnianie, timery i zdarzenia; normalizuje tylko nieistotne identyfikatory. Niezgodności rozstrzyga ze specyfikacją i właścicielem, nie większością trzech wyników. A4 przygotowuje adapter docelowy i porównane implementacje, ale nie zalicza jeszcze grup ani failover. Włączenie kompletnego runtime Ada do normalnej gry następuje po S02B/C/D i odbiorze. C# zostaje czasowo narzędziem odniesienia, bez długotrwałego produkcyjnego drugiego silnika lub awaryjnego przełączania języka.
- **S02B:** obie grupy zawierają już oba wykonujące się algorytmy Ada A/B. S02C/S02D/S06/S13 zachowują kryteria przełączania, transmisji, praw i odtwarzania oraz próby losowania i replay.

Obowiązują selftest 25/26 i kontrola 4/4 na kopii, scenariusze usterek, ochrony bocznej i blokad oraz testy N25-N34. Dodajemy ABI fuzz, BufferTooSmall bez ponownego obliczenia, przerwane kopiowanie, wycieki, odrzucenie komendy po utracie praw i awarię całego procesu natywnego. Regresja nie zatwierdza znanego błędu: różnicę merytoryczną opisuje i rozstrzyga właściciel.

S13 mierzy koszt Ada, kopiowania i wszystkich grup/kanałów przy 40 klientach, próbę zapasu 80 oraz offline. Bez wyników prototypu nie podajemy przyspieszenia, zużycia pamięci ani gotowości wydania. Wybór Ada nie stanowi nowej normy srk ani dowodu niezawodności.


## 8. Uściślenia odbioru 1.5
Qualification=Ready jest niezależne od roli. Przed losowaniem obie grupy mają Role=Unassigned i zablokowane wyjścia. Zapisane StartupSelection nadaje role w Ada; C# OutputGate otwiera się po zatwierdzeniu, bez własnego wyboru zastępczej grupy.

Ponowne prawa dotyczą nowej intencji operatora. Autonomiczne dozorowanie już zatwierdzonego przebiegu nie jest uzależnione od późniejszego lease dawnego dyżurnego. Committed nie potwierdza fizycznego wykonania; to potwierdzają urządzenia. Próba obejmuje oddanie/ban podczas przekładania i podczas zwalniania.

Manifest zgodności wiąże abiMajor/minor, schemat definicji/wejść, wersje Kz_A, Kz_B i AdaGroupRuntime oraz snapshotSchema każdej implementacji. Restore odrzuca nieobsługiwaną kombinację przed zmianą aktywnego stanu. A/B mogą używać różnych reprezentacji prywatnych; porównuje się kanoniczny wynik domenowy, nie surowy snapshot A z B.


StartupSelection jest unikalne dla pary (worldRunId, kzId). Ponowienie zapisu/przekazania tego samego wejścia nie losuje kolejnej wartości: używa istniejącego wyboru i identyfikatora. Zatwierdzony wybór zostaje częścią checkpointu i dziennika zanim odblokujemy wyjścia.

## Zamrożenie reguł z nowej bazy - 1.7
Referencją zespołu specyfikacji/integracji jest teraz Sim/Kz/KomputerZaleznosci/Interlocking/Blocks/Commands na 5eddb5c oraz SystemNadrzedny i karty, nie stare położenie metod w Simulator. Autorzy A/B nadal otrzymują neutralną specyfikację, bez kodu drugiego autora i bez wspólnego przepisywania starego algorytmu.

Do definicji i manifestu zgodności włączyć zatwierdzony hash Data/sygnaly_ie1.json, macierz doboru sygnałów, zależność od następnego semafora/SBL, znaczenie VSet oraz zakres parametrów. C# odczytuje dane raz i przekazuje znormalizowany kontrakt; prywatne algorytmy A/B pozostają niezależne. Wskazany PDF Ie-1 jest obecnie niedostępny, więc tabela C# nie stanowi sama dowodu normatywnego. Właściwe źródło i istniejące otwarte kwestie ≤100 km/h/Os/Sp trzeba zweryfikować przed zamrożeniem tych przypadków.

Istniejące karty i fasada nie zaliczają S02A: przed dwoma obliczeniami trzeba domknąć niemutowalne wejścia, bierne plany i jeden skutek oraz bezpieczne projekcje dla odłożonego kroku 8. Nowy pomiar regresji S00 ma bazować na aktualnym kodzie i wszystkich nowych kontrolach, z jawnymi różnicami względem dawnej wersji.


## Wykonanie w małych czatach - 1.8
Dwie linie autorskie A/B mogą być kontynuowane w kolejnych czatach własnego wariantu według pojedynczych ID z26. Nie scalamy historii A i B ani nie udostępniamy im istniejącego algorytmu C#. Gotowe prompty27 wskazują neutralny pakiet o identycznym hashu i dozwolone poprzednie artefakty. Raport i hash każdego kroku zachowuje się do zamrożenia wariantu. Każde Evaluate może oddać ograniczone rekordy diagnostyczne według25; logger C#/I/O nie jest callbackiem algorytmu. Diagnostyka błędu nie zmienia kanonicznego wyniku domenowego i nie stanowi obejścia bramki wyjść.
