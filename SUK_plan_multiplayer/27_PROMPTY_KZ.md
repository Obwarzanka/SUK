# 27. Gotowe prompty niezależnych KZ
Plan 1.8 | Prompty przyszłej pracy, bez implementacji w tym zleceniu.

## Jak uruchomić
Po odebraniu zależności otwórz odpowiedni plik, skopiuj cały blok text do nowego czatu
i udostępnij wskazany pakiet. Jeden prompt realizuje jedno ID. Nie wklejaj całego
PDF ani historii drugiej implementacji. Manifest.jobs i czaty/ID.md zawierają
cel, wejścia, dowody i następny krok. Wszystkie zadania pozostają planned.

| ID | Cel | Gotowy prompt |
|---|---|---|
| S02A1.01 | Neutralna specyfikacja źródeł | [S02A1.01](prompty_kz/S02A1.01.md) |
| S02A1.02 | Kontrakt danych i ABI | [S02A1.02](prompty_kz/S02A1.02.md) |
| S02A1.03 | Katalog przypadków | [S02A1.03](prompty_kz/S02A1.03.md) |
| S02A1.04 | Stub i zamrożenie pakietu | [S02A1.04](prompty_kz/S02A1.04.md) |
| S02A2.01 | Własny kontekst i cykl życia | [S02A2.01](prompty_kz/S02A2.01.md) |
| S02A2.02 | Statusy i polecenia urządzeń | [S02A2.02](prompty_kz/S02A2.02.md) |
| S02A2.03 | Przebiegi i utwierdzenia | [S02A2.03](prompty_kz/S02A2.03.md) |
| S02A2.04 | Blokady, timery i sąsiad | [S02A2.04](prompty_kz/S02A2.04.md) |
| S02A2.05 | Odbiór i zamrożenie wariantu | [S02A2.05](prompty_kz/S02A2.05.md) |
| S02A3.01 | Własny kontekst i cykl życia | [S02A3.01](prompty_kz/S02A3.01.md) |
| S02A3.02 | Statusy i polecenia urządzeń | [S02A3.02](prompty_kz/S02A3.02.md) |
| S02A3.03 | Przebiegi i utwierdzenia | [S02A3.03](prompty_kz/S02A3.03.md) |
| S02A3.04 | Blokady, timery i sąsiad | [S02A3.04](prompty_kz/S02A3.04.md) |
| S02A3.05 | Odbiór i zamrożenie wariantu | [S02A3.05](prompty_kz/S02A3.05.md) |
| S02A4.01 | Zamrożenie wejść integracji | [S02A4.01](prompty_kz/S02A4.01.md) |
| S02A4.02 | Porównanie i adapter | [S02A4.02](prompty_kz/S02A4.02.md) |
| S02A4.03 | Migracja i zamknięcie | [S02A4.03](prompty_kz/S02A4.03.md) |

## Neutralna specyfikacja i dwa łańcuchy
S02A1.01–.04 przygotowuje źródła, kontrakt/ABI, przypadki i techniczny stub oraz
zamrożony pakiet ReadyForAuthors. Brak wskazanego PDF Ie-1 lub niezatwierdzona
interpretacja nie może zostać zastąpiona domysłem. Podpis/hash pliku nie jest
zatwierdzeniem treści: potrzebny jest jawny odbiór specyfikacji.
A realizuje S02A2.01–.05, B realizuje S02A3.01–.05. Można kontynuować własną
linię w kolejnych czatach, przekazując raport i własny kod. Dwa pierwotnie
oddzielne zespoły/linie autorskie nigdy nie dzielą historii algorytmu.
Po zamrożeniu obu integrator wykonuje S02A4.01–.03; nie wybiera „głównego”
algorytmu w razie rozbieżności. Losowaniu podlega grupa1/2, nie program A/B.

## Izolacja i dostęp
Autorzy dostają identyczny neutralny manifest z hashami: zatwierdzone źródła/reguły,
definicje danych i przypadki oraz ABI, bez pseudokodu tłumaczącego stary C#.
Przyszły zespół specyfikacji przygotuje osobne pakiety robocze i zatwierdzone
instrukcje źródłowe dla autorów. Bieżąca dokumentacja nie jest tym pakietem.
Folder i nazwa czatu nie ograniczają odczytu; udostępnianie i uprawnienia środowiska
trzeba rzeczywiście ograniczyć i odnotować. Jeżeli odziedziczone AGENTS/ROZWOJ
wymuszają ujawnienie referencyjnego algorytmu, autor nie rozpoczyna w takim
worktree; integrator przygotowuje zgodne środowisko zamiast ukrywać konflikt.
Przypadkowy dostęp zgłasza się przed dalszą pracą i ocenia wpływ/odtworzenie
czystego zadania. Nie deklarujemy certyfikowanej niezależności ani SIL.

## Logi, wynik i kontynuacja
Każdy wariant zwraca DiagnosticBatch25 z limitem, bez pliku, sieci, bazy i callbacku
loggera w Evaluate. W grupach1/2 działają oba programy: cztery prywatne stany,
jeden fizyczny świat i jedno źródło zatwierdzonego skutku. Wspólny kod kontraktu
nie może ukrywać wspólnego silnika reguł. Raport każdego zadania zawiera wejścia,
hash specyfikacji, własne artefakty, wyniki testów i braki; integrator otrzymuje
zamrożone hashe dopiero po zakończeniu obu linii.
Kontynuacja: [A](prompty_kz/KONTYNUACJA_A.md), [B](prompty_kz/KONTYNUACJA_B.md).


Autorzy A/B rozpoczynają od AI_START_AUTOR_KZ.md. Pełny AI_START i jego nakazy czytania02/19/24 dotyczą pozostałych ról. Konflikt nadrzędnych instrukcji wymaga przygotowania zgodnego środowiska, nie jest ignorowany.
