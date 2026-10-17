# 08. Konta, administracja i statystyki

## Konto
REKOMENDACJA: Identity + OpenIddict, PostgreSQL na Windows. Rejestracja, potwierdzenie kontaktu, login, reset hasła, rotacja sesji, zmiana nazwy i usunięcie/dezaktywacja konta. E-mail nie musi być publicznym nickiem. Singleplayer nie wymaga konta.

Oddzielić accountId, displayName, deskId i rolę w konkretnej sesji. Role operatora nie dają uprawnień administracyjnych. Przy rejestracji i odzyskiwaniu kont limity oraz brak ujawniania istnienia adresu. Własny algorytm haseł i własny protokół logowania są poza planem.

## Działania administracyjne
| Działanie | Zakres i skutek |
|---|---|
| Kick ze stanowiska | unieważnia lease; świat nie jest resetowany |
| Blokada posterunku/obszaru | zabrania nowego i odbiera istniejący zakres |
| Zakaz sterowania | pozwala ewentualnie obserwować zgodnie z polityką |
| Mute tekst/głos | osobny typ i czas; egzekwowanie także w mediach |
| Ban konta | zamyka wszystkie sesje i blokuje wejście |
| Ban świata | ogranicza wskazany świat, jeśli polityka to przewiduje |
| Odwołanie sankcji | nowy wpis audytu, nie usunięcie starego |

Sankcja: sanctionId, konto, typ, zakres, start/end UTC, powód, autor, evidenceRef i wersja. Idempotentne ponowienie. Limity kompetencji moderatora, np. długość bana, ustala właściciel (D06). IP nie jest tożsamością; nie stosować wyłącznego bana IP do osób za wspólnym NAT.

Zmiana praw podnosi authorizationVersion/controlEpoch i rozłącza odpowiednie sesje. Sprawdzamy stan przed wykonaniem kolejki, podczas reconnect i wydawania tokenu głosu. Ukryty przycisk nie jest kontrolą dostępu.

## Model danych - zarys
Accounts/Identity, Sessions, Worlds, ContentPackages, AreaDefinitions, ControlLeases, Sanctions, AuditEntries, DomainEvents, CommunicationSessions, StatProjections i Outbox. To zarys tabel/odpowiedzialności, nie gotowa migracja SQL.

Trwałe konto oddzielamy od efemerycznego stanu każdego odcinka i ticku. Brak transakcji DB dla każdego kroku fizyki. Konfiguracja i sankcje są trwałe; migawki świata mają osobny zapis i wersję silnika.

## Statystyki serwerowe
Obecne liczniki Score są punktem odniesienia, nie gotowym rankingiem. Zdarzenie ma eventId, worldId, sessionEpoch, tick, typ, actorAccountId, deskId, controlLeaseId/epoch, trainId i schemat danych. Serwer wyznacza autora. Klient tylko odczytuje.

Zapis zdarzeń z kontrolą kolejności i ograniczonym buforem; worker utrwala je asynchronicznie. Dopiero utrwalone zdarzenia z unikalnym eventId budują projekcje statystyk. Outbox służy dalszemu przetwarzaniu po utrwaleniu; sam nie zapewnia atomowości pomiędzy pamięcią rdzenia a DB. Trwałość krytycznych komend i odtwarzanie wymagają decyzji S06.

Przetworzenie zdarzenia drugi raz nie nalicza wyniku drugi raz. ProjectionVersion umożliwia przeliczenie według tej samej reguły. Wyniki local/offline nie trafiają do rankingu serwerowego.

Proponowane miary na początek: czas obsady, liczba obsłużonych pociągów wg jawnej definicji, wykonane polecenia, zdarzenia/usterki szkoleniowe. Punktację błędów i odpowiedzialność przy zmianie dyżurnego ustala właściciel (D08). Sama różnica czasu przyjazdu nie dowodzi winy dyżurnego.

## Dane i audyt
Osobno: dziennik srk, historia rozmów, audyt kont/moderacji i log techniczny. Dostęp według potrzeby; żadnych tokenów, haseł ani pełnych ramek w logach. Domyślnie bez nagrania głosu. Okresy retencji, eksport/usunięcie danych i regulamin wymagają D07 przed publicznym udostępnieniem. Ten dokument nie ustala zgodności prawnej.

Kopie zapasowe szyfrowane, poza hostem gry, z testem odtworzenia. Audyt append-only na poziomie aplikacji nie oznacza kryptograficznej odporności administratora hosta. Krytyczny audyt warto wysyłać do oddzielnego magazynu.

## Zastosowanie sankcji w świecie
Trwały zapis sankcji poprzedza jej dystrybucję, ale odpowiedź administracyjna rozróżnia Saved i Applied. Komunikat sterujący podnosi wersję praw w sekwencji hosta, nie przez zapytanie DB z ticku. Test obejmuje ban równoczesny z wykonywaniem kolejki oraz niedostępny proces świata. Niedostępny świat nie może po odzyskaniu przyjmować komend przed synchronizacją bieżących sankcji.

Zdarzenia i audyt obejmują sourceKzId/postId oraz epokę modułu. Wspólny ranking nie wymaga scalania KZ. Sam restart jednego modułu nie może przypisać cudzych zdarzeń do innego posterunku.

## Korekta po audycie źródeł - wersja 1.3
Statystyki i książka wynikają z logicznego zatwierdzonego effectId, nie z osobnych wyników Group1/Group2. Diagnostyka nie nalicza drugiego przejazdu ani punktów. Hot failover utrzymuje sankcje/przydziały; restore nie wskrzesza historycznych praw. Odbiór N29/N33.

## Pełny panel na końcu
S08 dostarcza obowiązkowe minimalne narzędzia moderacji i audytu przed pilotem. Pełny control panel S18 powstaje po S16/S17 i używa tych samych kont, sankcji, ról, zdarzeń oraz rozróżnienia Saved/Applied. Nie powstaje druga baza kont. Rola dyspozytora S19 jest osobna i nie daje praw moderacyjnych ani sterowania urządzeniami.


## Dostęp administracji 1.6
Pełny interfejs SUK.Admin WPF powstaje w S18. S08 zapewnia wcześniej minimalne prywatne narzędzie nad tym samym API, z kluczem administratora/urządzenia i istniejącą bazą kont. Enrollment, konto ORAZ klucz, rotacja i revoke: rozdział 21/N41/O19. Minimum N41A poprzedza pilot, bez zależności od pełnego GUI.

## Diagnostyka i instruktor - 1.7
Istniejąca Komputerownia ma widoki kart/KZ i polecenia usterek. Użyć jej projektów widoków/projekcji, ale przydzielić osobno odczyt diagnostyki i mutacje instruktora. Nie utożsamiać aplikacji Komputerownia z nowym SUK.Admin ani z rolą dyżurnego. Stare pole t=serwis nie jest rolą potwierdzoną przez serwer; N42/O20 należy do minimalnej bezpiecznej administracji przed S13.
