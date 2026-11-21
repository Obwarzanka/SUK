# Reguły i ich źródła, v0.1.0

Numery PDF od 1. W odczytanych fragmentach Ie-1/Ie-20/Ie-104 numery drukowane są
zgodne z numerami stron PDF. `KOMPUTERY.pdf` PDF 7 odpowiada drukowanej s.126,
a PDF 21 drukowanej s.140. Hash źródła wskazuje `zrodla.json`.

## Reguły źródłowe

| ID | Treść i zakres | Konkretne źródło |
|---|---|---|
| N01 | Warstwa operatorska przetwarza polecenia, zależnościowa bezpiecznie realizuje i dozoruje przebiegi, sterowniki obsługują obiekty. | Ie-20 §6 ust.1–5, PDF 9. |
| N02 | Wybrana droga przebiegu jest utwierdzana; sygnał wymaga spełnionych warunków, a nie samego wysłania rozkazu. Nie projektuje to konkretnej ochrony. | Ie-20 §16 i §17 ust.2, PDF 22; §18 ust.1, PDF 24. |
| N03 | W prawidłowej pracy pierwsza oś na pierwszej sekcji za semaforem powoduje Stój. | Ie-20 §17 ust.2 pkt3, PDF 22. |
| N04 | Sekcyjne zwalnianie przebiegu następuje po zwolnieniu poprzedniej sekcji i opuszczeniu danej sekcji. Dopuszczalność wyjątków dla toru docelowego wymaga projektu/rozstrzygnięcia. | Ie-20 §18 ust.3, PDF 24. |
| N05 | Polecenie specjalne ma procedurę co najmniej dwóch etapów; po inicjalizacji nie wydaje się innych poleceń nastawczych, a wycofanie jest możliwe na każdym etapie. Jest to odpowiedzialność hosta/parsera. Doraźne zwolnienie i użycie Sz wymagają właściwej rejestracji. | Ie-20 §13 ust.7–9, PDF 19–20; §17 ust.2 pkt5 i ust.7, PDF 23–24. |
| N06 | Sz wygasa samoczynnie po około 90 s lub po poleceniu wyłączenia. | Ie-20 §17 ust.7 pkt1 i4, PDF 23–24. Dokładne 90 s w SUK jest parametrem projektu. |
| N07 | Zastopowana zwrotnica nie jest przestawiana do odstopowania; przestawianie z bocznikowaniem kontroli niezajętości jest odrębnym poleceniem specjalnym. | Ie-20 §15 ust.3,5–6, PDF 21–22. |
| N08 | Wyjazd na SBL zależy od jej kierunku i zajętości przystacyjnych odstępów. Zwykła zmiana kierunku wymaga wszystkich odstępów wolnych, braku przebiegów wyjazdowych i Stój na wjazdowych. | Ie-20 §20 ust.3–4, PDF 26. Nie utożsamiać odstępu z jednym czujnikiem/odcinkiem. |
| N09 | Awaryjna zmiana kierunku jest poleceniem specjalnym; odróżnia się kontrolę zajętości urządzenia od obowiązku stwierdzenia wolności szlaku przez obsługę. | Ie-20 §20 ust.5, PDF 26. Nie tworzy pełnego protokołu sieciowego KZ–KZ. |
| N10 | Półsamoczynna ma odrębne bloki pozwolenia, początkowy i końcowy; ich obsługa potwierdza wyprawienie/przybycie. | Ie-20 §20 ust.7–8, PDF 27–29. Wariant bezobsługowy ust.8 pkt10 nie jest regułą wszystkich blokad. |
| N11 | S1 to czerwone ciągłe; S2–S13a opisują podstawową prędkość i informację o następnym semaforze. | Ie-1 §4 ust.13 pkt1–17, PDF 17–29; macierz zał.1 PDF 208–210. |
| N12 | S3 rozróżnia zapowiedź prędkości następnego semafora i dwa kolejne wolne odstępy dla wskazanych rodzajów sygnalizatorów/SBL. | Ie-1 §4 ust.13 pkt3 lit.a–b, PDF 19–20. Nie jest uniwersalnym „następny ma S5”. |
| N13 | Sz to matowobiałe migające; dopuszczone są odmiany z czerwonym oraz bez czerwonego/na osobnym sygnalizatorze. Jazda wymaga ostrożności, zasadniczo do 40 km/h, z zakresem zależnym od sytuacji i rodzaju blokady. | Ie-1 §4 ust.13 pkt18, PDF 29–31. Krok 8 ruchu nadal poza tym zadaniem. |
| N14 | Ms1 na tarczy: niebieskie; Ms2: matowobiałe ciągłe. Na odpowiednio oznaczonym semaforze manewry zabrania S1, zezwala Ms2. | Ie-1 §8 ust.4 i6, PDF 47–48. Ms2 nie jest zezwoleniem pociągowym z prędkością klasy 0. |
| N15 | Os1/2/3/4: P/Z/Z*/P*. Sp1/2/3/4: odpowiednio PB/ZB/Z*B/P*B (od góry). Zapowiadają Stój/Vmax/≤100/40 lub60 na semaforze, do którego się odnoszą. | Os: Ie-1 §6 ust.15 PDF 41–42; Sp: §5 ust.5 PDF 34–35. Nie wolno przez to dodawać automatycznie kart Os/Sp. |
| N16 | Powtarzacze nie są stosowane na szlakach z blokadą wieloodstępową; najwyżej trzy w razie potrzeby. | Ie-1 §5 ust.2–4, PDF 33–34. |
| N17 | W21 podwyższa prędkość z sygnału 40/60/100 do wartości liczby ×10 km/h; świetlny ukazuje się razem z zezwoleniem. Stała tablica ma warunki dotyczące wszystkich właściwych przebiegów. | Ie-1 §17 ust.15 pkt23 lit.a–d, PDF 153–154. Warunki lit.d nie są sprawdzone samym ostrzeżeniem w bieżącym programie. |
| N18 | Brak komunikacji pulpitu/nastawnicy lub stan nieznany ma biały niemigający symbol. | Ie-104 §4 ust.11 pkt5, PDF 7. Brak kontroli położenia jest osobnym stanem; patrz O07. |
| N19 | G1–G4 to grupy informacji zobrazowania, nie komputery. Katalog producenta i zakres lokalny pozostają odrębne. | Ie-104 §3 ust.3–4 PDF 5; §4 ust.5 PDF 6. |
| N20 | EBILock: dwa zestawy zasadniczy/rezerwa i dwukanałowe programy; MOR-3 opisano osobno jako 2z2 z komparatorem. | KOMPUTERY PDF 7/druk126 oraz PDF21/druk140, obejrzane obrazy stron. Nie jest to podstawa do zgadywania czasu failover. |

## Macierz obrazów sygnałowych

Zakres tabeli: zał.1 Ie-1, największa dozwolona prędkość na odcinku **>100 km/h**.
Kolumna opisuje zapowiedź, nie stan kolejki pociągów. Klasa 0 oznacza Vmax.

| Przy tym semaforze | Następny Vmax | Następny ≤100 | Następny 40/60 | Następny Stój |
|---|---|---|---|---|
| Vmax (0) | S2 | S3 | S4 | S5 |
| 100 | S6 | S7 | S8 | S9 |
| 60 | S10a | S11a | S12a | S13a |
| 40 | S10 | S11 | S12 | S13 |

Podstawowe obrazy: S2/Z, S3/Z*, S4/P*, S5/P; w dolnych wierszach drugie
pomarańczowe ciągłe, a dla 100 pas Z i dla 60 pas P. `*` dotyczy poprzedzającego
światła. `dane_zrodlowe.json` rozpisuje wszystkie 16 komórek i miejsca w §4.
S1 nie jest kolejną kolumną prędkości następnego semafora: odmawia jazdy przy tym
semaforze. Macierz nie wyznacza warunków utwierdzenia lub fizycznej sprawności.

`null` w kolumnach `v`/`nast` istniejącego JSON przy S1, Sz i Ms nie oznacza Vmax.
Pusty kod jest technicznym oznaczeniem ciemnego odstępowego w przeciwnym kierunku;
nie jest normatywnym zezwoleniem ani SE/ERTMS. Obecne `CB*` dla Sz potwierdza jeden
obraz; nie opisuje wszystkich odmian z N13.

## Ustalenia projektu zachowane bez ponownego otwierania

| ID | Ustalenie | Podstawa projektu |
|---|---|---|
| P01 | Ogólny LCS; nazwy stacji wyłącznie w danych; zależności i obrazy projektuje człowiek. | ROZWOJ §1–2, AGENTS. |
| P02 | Ada A/B niezależnie, obie implementacje w obu grupach; cztery prywatne stany i jeden świat/ruch. | Rozdział 23 §1,3. |
| P03 | Zapisany wybór grupy po Ready; bez ponownego losowania przy replay/reconnect/zmianie obsady. | Rozdział 23 §3,8. |
| P04 | KZ rozpoznaje rozprucie z braku kontroli i zajętości. Bez włókien rezerwowych i bez samoczynnego obniżania zezwolenia przy usterce światła; zachowane opóźnienie rozkaz–meldunek. | PODZIAL_MODULOW §7 pkt1–5, decyzje 16.10.2026. To nie zakazuje niższego doboru z powodu braku pasów/W21. |
| P05 | VSet ustala projektant, także ponad wartości domyślne toru/zwrotnicy. Pasy i W21 ograniczają możliwość zasygnalizowania; bez nich niższy sygnał i uwaga dla jawnego nowego wyposażenia. | ROZWOJ §5a, decyzje 19.10.2026. |
| P06 | ZD jednoetapowe: 90 s przy zbliżaniu; ZDP inicjuje, ZDPP po minimum 5 s wykonuje zwolnienie natychmiast; OPS odwołuje. | ROZWOJ §5a, późniejsza poprawka decyzji 20.10.2026. Starszy opis ZDPP=90 s jest historyczny. |
| P07 | Krok 8 (docelowe kierowanie ruchem z fizycznych sygnałów) pozostaje odłożony; adapter korzysta tylko z zatwierdzonej projekcji. | PODZIAL_MODULOW §7, raport S02A.04. |
| P08 | Podgląd po oddaniu bez sterowania, AI ruchowe bez LLM, prywatny klucz administratora generowany lokalnie. | Decyzje planu 1.8; nie są regułami sygnalizacji ani zakresem implementacji tego zadania. |

## Co wymaga kontraktu zamiast odgadywania

Źródła podają znaczenie i zasady obsługi, ale nie pełną tabelę logiczną każdej odmiany
urządzeń. Własność, adresowanie, parametry opóźnień, powtarzanie planu, utrata danych
między KZ i snapshoty potrzebują wersjonowanych uzgodnień. Nie wprowadzać czasów
z rosyjskich opisów sprzętu jako polskiej normy, nie utożsamiać EBILock z MOR-3.
Stan istniejącego C# i wyniki regresji są materiałem integratora; zgodność z nimi nie
zatwierdza błędu merytorycznego. Otwarte przypadki wskazuje osobny rejestr.
