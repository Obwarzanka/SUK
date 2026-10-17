# Audyt zgodności promptów SUK — plan 1.8
Data: 17.10.2026. Status: **AUDYT ZAKOŃCZONY; integracja wymagań z dokumentacją 1.8 sprawdzona; kontrola wizualna i techniczna artefaktów w RAPORT_WERYFIKACJI.json**.

Zakres: wszystkie instrukcje organizacyjne i doprecyzowania funkcji właściciela, łącznie **37 wymagań: R01–R13/F01–F24**. Sprawdzono bieżące Markdown, manifest 1.8, karty oraz pakiety przyszłych czatów KZ. Istnieje 28 kart etapów. „Pokryte” oznacza opisane i zaplanowane, nie zaimplementowane. Numery w tabeli wskazują rozdziały Markdown. Ten raport ocenia treść planu; nie potwierdza działania symulatora, testów, certyfikacji SIL ani jakości eksportów. Kanoniczna dokumentacja pozostaje poza repo; właściciel dopuścił końcową kopię w D:\Projects\SUK bez operacji Git.

## Macierz: prompt → wymaganie → dokumenty/etapy → ocena
| ID | Wymaganie właściciela | Pliki / etapy | Ocena i granica |
|---|---|---|---|
| R01 | Tylko plan, bez operacji Git | 00, AI_START, karty | Pokryte; późniejsze R13 dopuszcza końcową kopię dokumentacji w D:\Projects\SUK. Bez kodu, commit/push i wdrożenia. |
| R02 | Wykorzystać istniejące rozwiązania | 02,03,20,23; S01,S10,S12 | Pokryte; zachować dane/reguły, WPF i gotowe biblioteki. Ada jest nową implementacją zatwierdzonej funkcji. |
| R03 | Analiza źródeł przez subagentów | 14,19 | Pokryte analizami i audytem źródeł; niezależne A/B to osobne przyszłe zlecenia. |
| R04 | Nazwa SUK | 00,01 | Pokryte: Symulator Urządzeń Komputerowych. |
| R05 | Dokument dla ludzi i AI, grafiki z opisami | 00, AI_START, grafiki | Pokryte Markdown, opisami logiki i eksportami; kontrolę wizualną prowadzi autor. |
| R06 | Etapy wykonywane oddzielnie | 12, manifest, etapy, SZABLON_PRZEKAZANIA | Pokryte: 28 kart, zależności, małe podkroki i minimalny kontekst. |
| R07 | Bezpieczeństwo infrastruktury publicznej | 05,09,11; S03–S13 | Pokryte; publiczny pilot po bramkach bezpieczeństwa, głosu i aktualizatora. |
| R08 | Uwzględnić Agent/Claude i zasady projektu | 02,14, AI_START | Pokryte; chroniony station.json, źródła srk i historyczna rozbieżność testów jawne. |
| R09 | Oszczędzać zasoby bez utraty funkcji | 10,20; S07,S10,S13 | Pokryte budżetami i pomiarami; bez obietnic wydajności bez prototypu. |
| R10 | Konkretny stos języków i bibliotek | 20,23, manifest; S00,S02A1 | Pokryte: Ada KZ, C#14/.NET10/WPF/ASP.NET, PostgreSQL/EF/Npgsql, Identity/OpenIddict; dokładne wersje przypiąć. |
| F01 | Ruch między stycznymi posterunkami | 03,04,16; S02,S04–S07 | Pokryte; komunikaty graniczne i jeden fizyczny pociąg. |
| F02 | LCS, odcinki, pomocniczy i nadzór | 06,17; S05,S06 | Pokryte; zakresy praw i podgląd niezależne od grup KZ. |
| F03 | Konta i statystyki | 08; S03,S08 | Pokryte; autor i wyniki ze zdarzeń serwera, bez podwójnego naliczania. |
| F04 | Bany, kick, zakazy posterunku | 05,06,08; S05,S08 | Pokryte; bieżące sesje, reconnect, prawa i media objęte sankcją. |
| F05 | Tekst/głos przez symulowane urządzenia | 07,20; S09–S11 | Pokryte; Mumble pierwszy kandydat, integracja WPF/ACL pozostaje bramką prototypu. |
| F06 | Informacja człowiek–algorytm | 07; S09,S14,S15 | Pokryte; typowane komunikaty i rozdzielone potwierdzenia transportowe/operacyjne. |
| F07 | Późniejsza obsada pustych posterunków MP | 07,13; S14–S16 | Pokryte; wcześniejszy pilot ma jawny scenariusz obsady. |
| F08 | Późniejsze krzyżowanie/wyprzedzanie | 07,13; S16 | Pokryte po odbiorze infrastruktury. |
| F09 | Offline: AI tylko stycznych | 03,07,16,23; S14,S15 | Pokryte; ten sam rdzeń i Ada, bez konta/Internetu. |
| F10 | Ruch i srk autorytatywne na serwerze MP | 03,04,16,23; S01–S04 | Pokryte; klient przekazuje intencję, obie grupy liczą na serwerze. |
| F11 | Zabezpieczone łącze i zgodność wersji | 04,05,09; S04,S12,S13 | Pokryte; deklaracja wersji nie dowodzi uczciwości EXE. |
| F12 | Automatyczne aktualizacje | 09,20; S12 | Pokryte; Velopack kandydatem, zaufanie metadanych/rollback/freeze osobną bramką; bez restartu podczas służby. |
| F13 | Windows, początkowo 20–40, wzrost | 00,10,20; S13,S17 | Pokryte; 40 klientów/8h i próba 80 to przyszłe pomiary. |
| F14 | Własny KZ każdego posterunku | 03,16; S01–S07,S13 | Pokryte; logiczna odrębność, bez klonowania świata. |
| F15 | Oddanie: podgląd pozostaje, sterowanie znika | 06,17; S05–S07,S13; N24 | Pokryte: ViewGrant bez ControlLease; nie obejmuje wyjątku od bana konta. |
| F16 | Dwie grupy 1/2: aktywna i gorąca rezerwa | 18,19,23; S02A–S02D; N25–N34 | Pokryte; rzeczywiste obliczenia/synchronizacja, nie wskaźnik atrapowy. |
| F17 | Własny algorytm ruchowy bez LLM | 07, AI_START; S14–S16; N35/O15 | Pokryte w MP/offline; reguły/dane, bez LLM również awaryjnie. STT/TTS osobno. |
| F18 | Pełny końcowy panel administracyjny | 21; S18 po S16/S17; N38/O17 | Pokryte; minimum moderacji S08 nadal przed pilotem. |
| F19 | Dyspozytor: tylko nadzór/koordynacja | 22; S19 po S18; N39/O18 | Pokryte; ostatni etap, bez automatycznego ControlLease i obchodzenia KZ. |
| F20 | Przepisanie domeny KZ na Ada | 20,23; S02A1–S02A4; N36 | Pokryte; reszta C#, wersjonowane C ABI, LibraryImport/SafeHandle, jeden WorldHost. |
| F21 | Niezależne A/B w dwóch przyszłych czatach | 23, pakiety_kontekstu, S02A2/S02A3/S02A4; N37 | Pokryte; wspólna zamrożona specyfikacja, odrębne źródła/historie, integracja dopiero po zamrożeniu obu. |
| F22 | Losowanie zasadniczej grupy per KZ | 18,23; S02C; N40/O16 | Pokryte; po Ready obu grup, zapis StartupSelection, replay bez ponownego RNG. |
| F23 | Osobna aplikacja admina, klucz indywidualny + konto, pierwsza rejestracja | 03/05/20/21; S03/S08/S18; N41/O19 | Pokryte; lokalne generowanie klucza zatwierdzone przez właściciela 17.10.2026, preferowany niewyeksportowalny CNG/TPM; serwer tylko publiczny klucz i certyfikat. |

| R11 | Jeden mały etap z kontekstem na osobny czat | 26, manifest.jobs, czaty/, prompty_czatow/ |90 zadań, DAG, wejścia i raporty; N45/O22. |
| R12 | Gotowe prompty generowania niezależnych KZ | 27, prompty_kz/ |17 promptów: spec4, A5, B5, integracja3; N46/O23, źródła/hash i izolacja. |
| R13 | Końcowy przegląd, korekty i kopia w głównym folderze | 28, raport kontroli, sumy | D:\Projects\SUK wskazane przez właściciela; poprzedni PDF zachowany poza repo. |
| F24 | Logi programu i każdego nowego modułu | 25, S00/S01/S06 i wszystkie następne | MEL, bounded kolejki i batch Ada, osobny audyt/replay, redakcja i N43/N44/O21. |

## Grupy, niezależność i pojedynczy skutek
F16, F20 i F21 obowiązują łącznie. W każdym logicznym KZ grupa1 i grupa2 wykonują **A+B**, łącznie cztery prywatne stany dynamiczne. Jedna SukKz.dll zawiera odrębne Kz_A/Kz_B i koordynator; nie są to dwa zamienne silniki awaryjne. Wspólne mogą być kontrakty, dane i adapter techniczny, nie implementacja reguł zależności. Stary C# jest odniesieniem autora specyfikacji/integratora; autorzy A/B nie korzystają z niego jako przepisu.

Oba czaty otrzymują identyczny neutralny pakiet o zamrożonym hashu, bez kodu, patchy i historii drugiego wariantu. Integrator porównuje oba dopiero po zamrożeniu wyników. Porównuje się kanoniczne wyniki i istotne stany domenowe, nie surowe snapshoty lub identyczność prywatnej pamięci A/B. Zgodność A/B nie wystarcza: oba mogą dzielić błąd specyfikacji. Dwa czaty, Ada i symulowana redundancja nie potwierdzają SIL.

Klient adresuje logiczny KZ. Ada kwalifikuje kanały i wybiera grupę, a C# OutputGate sprawdza fencing, effectId i aktualne prawa nowych intencji. Autonomiczne dozorowanie zatwierdzonego stanu trwa po oddaniu lub banie dawnego operatora. Jeden teren/ruch przyjmuje jeden zatwierdzony skutek, także ZeroLO, telegram, zapis książki i statystykę. Grupy nie tworzą dwóch pociągów ani rankingów.

## Uprawnienia, odtwarzanie i zasoby
Zwykłe oddanie pozostawia ViewGrant oraz odbiera ControlLease; przełączenie grupy i reconnect tego nie odwracają. Ban konta zamyka sesje i odcina dostęp, również podgląd/media. Ani administrator, ani dyspozytor nie otrzymuje sterowania z samej nazwy roli.

Checkpoint wiąże grupy/kanały, selektor, kolejki/watermarks i wspólny teren/ruch. Manifest zgodności obejmuje ABI, schematy wejść/definicji, wersje obu algorytmów/koordynatora i snapshotSchema każdej implementacji; nieobsługiwany restore jest odrzucany. Replay odtwarza zapisany wybór grupy. **Nowy worldRun po restore** najpierw uzgadnia stan, następnie zapisuje nowe losowanie; historyczne wyjścia nie są wykonywane ponownie. Przed wyborem obie grupy mają Ready oraz Role=Unassigned, zablokowane wyjścia; dopiero zapisany i zatwierdzony wybór nadaje role w Ada. Restart jednej grupy, reconnect i przekazanie stanowiska nie losują ponownie.

Mierzyć CPU/pamięć, ABI/kopiowanie, synchronizację oraz odbudowę wszystkich czterech kontekstów. Współdzielić niemutowalne definicje, bez kopii Trains/Rozklad. Nie zakładać konkretnego mnożnika kosztu. Singleplayer stosuje ten sam rdzeń; nie rozszerza AI poza styczne. Dwie grupy na jednym Windows/VPS nie chronią przed awarią procesu, dysku lub hosta; backup/RPO/RTO są odrębne.

## Zweryfikowana kolejność odbioru
S02A oddziela karty/wyjścia. S02A1 ustala specyfikację/toolchain/ABI na neutralnym stubie i harnessie, bez wcześniejszego pisania A/B. **S02A2 i S02A3 zależą wyłącznie od S02A1; S02A4 od obu**, bez zależności autora B od A. A4 porównuje i integruje algorytmy, nie zalicza grup/failover ani kompletnego runtime gry. S02B następuje po A4, dalej S02C/S02D; S04 zależy od S02D i S03.

S13 wymaga N25–N34 oraz **N36/N37/N40** i wcześniejszych właściwych prób. N35 pozostaje odbiorem algorytmu S14–S16 po S13; N38/N39 należą dopiero do S18/S19. S08 dostarcza działające minimum sankcji przed pilotem. S18 zależy od S16 i S17, S19 jest ostatni.

Ocena potwierdza pokrycie 33 wymagań treścią planu. Wszystkie etapy i próby programu pozostają zaplanowane. Kontrola aktualności, wyglądu i technicznej poprawności grafik/HTML/PDF należy do odrębnego raportu autora; w tym audycie nie uruchamiano renderowania ani symulatora.


## Kontrola 1.6
EXE nie jest sekretem. Dostęp wymaga konta ORAZ zarejestrowanego klucza ORAZ uprawnienia. Enrollment i odzyskanie nie opierają się na samym haśle; brak cichego obejścia lub bearer fallback. N41A jest przed pilotem, pełne GUI/N41B w S18. CNG/CSR nie dowodzi TPM, TPM nie oznacza odporności przejętej sesji Windows. Cookies/CSRF nadal chronią strony logowania.

## Potwierdzenie 1.6.1
Właściciel zaakceptował lokalne generowanie prywatnego klucza administratora, preferencyjnie niewyeksportowalnego z ochroną CNG/TPM. To obowiązujące ustalenie, nie opcja oczekująca na D18. Serwer otrzymuje klucz publiczny i wydaje certyfikat; nie generuje, nie wydaje i nie przechowuje prywatnego klucza administratora. D18 nadal obejmuje szczegóły rejestracji/odzyskania, profilu urządzeń, atestacji, rotacji i kompetencji. Nie potwierdzono implementacji ani wyników prób.

## Porównanie aktualnej aplikacji - 1.7
Macierz 33 wymagań pozostaje obowiązująca. Zweryfikowano statycznie 25 commitów i 123 ścieżki względem poprzedniego odczytu. Rozdziały 02/19/24 rozdzielają istniejące w C# moduły od przyszłych bramek multiplayera. S02A nie powtarza wykonanych kart/PIP/fasady; wszystkie etapy Sxx nadal wymagają odbioru. PIP nie ma już dawnego przecieku z Trains.List do okien. Nowa ścieżka serwis/usterka jest objęta N42/O20. S00 nie jest zaliczony przez ten audyt, a wyniki testów w repo są wcześniejszymi deklaracjami. Wszystkie zatwierdzone decyzje Ada/AI/LCS/administracji zachowano.
