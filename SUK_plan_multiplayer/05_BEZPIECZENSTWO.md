# 05. Bezpieczeństwo infrastruktury
REKOMENDACJA. Celem jest ograniczenie zaufania i sprawdzone zachowanie po błędzie. Serwerowa symulacja chroni przed fałszowaniem stanu przez klienta, lecz nie wyklucza nadużywania dozwolonych poleceń. Potrzebne są jednocześnie uprawnienia, moderacja i audyt.

## Granice zaufania
Internet obejmuje niezaufany klient, także przerobiony EXE. Publiczne są tylko HTTPS/WSS i niezbędne porty wybranego głosu. Baza, Named Pipes, panel operacyjny, porty administracyjne i API zarządzania głosem pozostają prywatne. RDP przez VPN administracyjny, z MFA tam, gdzie użyte środowisko to zapewnia, i ograniczeniem adresów; nie szeroko otwarty 3389.

Brama i świat pracują na osobnych kontach usług bez praw administratora. Klucze aktualizacji pozostają poza hostem publicznym. Lokalne ACL ograniczają pliki konfiguracji i IPC. Oddzielenie procesów nie chroni przed pełnym przejęciem Windows.

## Model zagrożeń
| Zagrożenie | Kontrola | Dowód odbioru |
|---|---|---|
| Podszycie pod rolę | OIDC i role wyznaczone przez serwer | klientowy User/Role ignorowany |
| Sterowanie obcym obszarem/końcem blokady | autoryzacja rozpoznanego celu i controlEpoch | test każdego typu komendy i obu końców |
| Replay/duplikat | requestId, epoka, pamięć wyników | 1000 kopii daje jeden skutek |
| Podsłuch/MITM | TLS 1.2/1.3 z prawidłową walidacją | zły certyfikat nie łączy |
| Zalew połączeń i komend | limity per konto/IP/świat i kolejki | świat nadal wykonuje tick |
| Wolny odbiorca | wysyłka poza tickiem, resync/rozłączenie | brak narastania pamięci |
| Fałszywe zgłoszenie wersji | zgodność i walidacja serwera | zmieniony klient nie ustawia stanu |
| Bezpośredni klient głosowy | ACL mediów i krótkie przydziały | próba wejścia do cudzego kanału odrzucona |
| Kradzież konta moderatora | MFA, rozdział ról, audyt | krytyczne akcje mają autora i ślad |
| Podmiana aktualizacji | podpisany łańcuch wydania | zły pakiet/metadane odrzucone |
| Niebezpieczna paczka danych | schemat, limit, bez kodu, sandbox rozpakowania | traversal/ZIP bomb odrzucone |
| Fałszowanie punktów | zdarzenia wyłącznie z rdzenia | brak endpointu dodawania wyniku |

To własny model SUK, zgodny z zasadą autoryzacji każdej akcji i limitowania WebSocket opisaną w [OWASP](https://cheatsheetseries.owasp.org/cheatsheets/WebSocket_Security_Cheat_Sheet.html).

## Limity początkowe do sprawdzenia
| Granica | Propozycja S04/S13 |
|---|---|
| Polecenie wejściowe | 16 KiB, JSON max depth 16, jawny schemat |
| Tekst urządzenia | 2000 znaków, bez HTML/załączników |
| Handshake | 10 s, limit nieuwierzytelnionych połączeń |
| Komendy | 10/s na konto, burst 20; oddzielne reguły potwierdzeń |
| Wiadomości tekstowe | 2/s, burst 5; osobno dzwonienie i PTT |
| Kolejka komend konta | 64; nadmiar jawnie odrzucony |
| Kolejka świata | 4096; telemetria długości i wieku |
| Bufor meldunków | limit bajtów per klient, np. 1 MiB; resync po przepełnieniu |
| Połączenia | per konto/świat; IP jako sygnał pomocniczy ze względu na wspólny NAT |

To nie są wartości udowodnione ani wszystkie ustawienia SignalR. W S04 osobno konfigurujemy MaximumReceiveMessageSize, bufory transportu, limity reverse proxy, request body i rozmiary odpowiedzi. Duże zasoby pobiera się przez kontrolowany HTTPS, bez podnoszenia wszystkich limitów.

Rate limiting nie może arbitralnie usuwać już przyjętego istotnego zdarzenia. Polecenie przyjęte do wykonania otrzymuje wynik albo jawny stan niepewności po awarii. Przy zalewie zachować możliwość obsługi uprawnionego operatora; priorytety komend awaryjnych wymagają uzgodnienia domenowego.

## Logowanie i sesje
Logowanie desktop: [RFC 8252](https://www.rfc-editor.org/rfc/rfc8252.html), PKCE i przeglądarka systemowa; Identity + OpenIddict. Tokenów nie utrwalamy w zwykłym JSON, logach ani publikowanych adresach. Klient .NET używa nagłówka Authorization. Jeżeli wybrane SDK przeglądarkowe wymaga krótkotrwałego tokenu w query WSS, S10 weryfikuje redakcję we wszystkich logach/proxy oraz brak utrwalenia w historii i telemetrii. WPF używa chronionego magazynu Windows. Tokeny odświeżania rotują, zmiana hasła/ban unieważnia sesje zgodnie z polityką.

Sprawdzenie praw następuje przy każdej akcji i przed jej wykonaniem. Principal SignalR nie jest bieżącą bazą przydziałów. Długie połączenia muszą zostać zamknięte po unieważnieniu. W interfejsie webowym stosować ochronę CSRF i jawną listę Origin; desktopowy Origin nie stanowi dowodu tożsamości.

Konta administracyjne mają osobne role operacyjne i MFA. Reset hasła jest jednorazowy, z czasem ważności i limitem prób. Odpowiedzi nie ujawniają, czy dany e-mail istnieje. CAPTCHA dodawać dopiero przy faktycznej potrzebie ochrony publicznej rejestracji.

## Utrzymanie i awarie
Windows: aktualizacje systemu i runtime w oknie serwisowym, firewall deny-by-default, automatyczne odnawianie certyfikatu z alarmem, ograniczone konta, kontrolowane zależności i skan paczek. DDoS objętościowy wymaga ochrony dostawcy; same limity aplikacji nie wystarczą.

Brak bazy blokuje nowe przydziały i akcje wymagające trwałego audytu. Bieżąca symulacja może kontynuować tylko według zatwierdzonej polityki z ograniczonym buforem zdarzeń; przepełnienie prowadzi do kontrolowanej pauzy świata. Głos i statystyki nie mogą zawieszać ticku. Awaria zapisu jest alarmem, nie cichą utratą.

Przed publiczną betą: testy negatywne, przegląd zależności i niezależny przegląd bezpieczeństwa adekwatny do zakresu. Żadna znana krytyczna luka w ścieżce publicznej bez usunięcia albo jawnego wyłączenia danej funkcji. Nie obniżać kontroli, by „zaliczyć” etap.

## Izolacja komputerów zależności
Rejestr obiektów mapuje każdy cel do dokładnego posterunku/KZ. Nazwa obrazu i deklarowany kzId nie zwiększają uprawnień. API sterowania sąsiednią blokadą nie daje dostępu do prywatnych słowników KZ. Testowanie obejmuje komendy na obcy moduł, podrobiony komunikat KZ-KZ i kontrolowane odcięcie jednego modułu.

Limity per moduł zapobiegają zagłodzeniu innych KZ, ale jedno wspólne wykonanie świata nadal ma budżet. Nie uznajemy izolacji w obiektach C# za sandbox bezpieczeństwa dla obcego kodu; pakiety posterunków nie zawierają wykonywalnych wtyczek.

## Korekta po audycie źródeł - wersja 1.3
Nie tworzyć publicznego portu/tokenu na każdą grupę. Przełączenie, wstrzykiwanie usterek i diagnostyka należą do ograniczonego interfejsu serwera/instruktora. Zwykły klient nie narzuca aktywnej grupy, wejść rezerwy ani wyniku kontroli. Audyt logicznego skutku i diagnostyka grup mają odrębne identyfikatory.

Selektor odcina stare plany przez selectionEpoch/groupRunId/effectId. Podpisany pakiet nie nadaje prawa do obiektu. Rebuild ma limity CPU, pamięci i historii. Symulacja nie jest certyfikowanym systemem srk ani deklaracją SIL.

## Konkretne środki i interfejsy 1.5
Transport: HTTPS/WSS przez sprawdzony stos .NET/SChannel/Kestrel, prawidłowa walidacja certyfikatu i klucz prywatny serwera w chronionym magazynie. TLS 1.3 preferowany, ewentualny TLS 1.2 według potwierdzonej macierzy Windows/klientów. Nie budujemy własnego protokołu RSA ani nie ukrywamy wspólnego sekretu w EXE. Szyfrowanie chroni transmisję; poprawnie zaszyfrowana lub podpisana niedozwolona komenda nadal jest odrzucana przez prawa i KZ.

SukKz.dll jest zaufanym artefaktem wydania z weryfikowanym hashem; ładowanie wyłącznie z chronionego katalogu aplikacji, bez ścieżki podanej przez klienta. Interop kontroluje rozmiary/liczby/wskaźniki, czas życia uchwytów, kody błędów i zgodność ABI. Błąd pamięci natywnej może zakończyć cały proces świata; nie nazywać tego lokalną usterką logiczną jednego posterunku. Brama publiczna pozostaje osobnym procesem.

Pełny S18 to osobny SUK.Admin WPF: prywatne API VPN/mTLS, token właściwego konta związany z certyfikatem i bieżące kompetencje. Indywidualny klucz urządzenia, zatwierdzany enrollment po MFA i revoke: rozdział 21. Cookies/CSRF pozostają przy stronach logowania OIDC. Bez konsoli SQL/powłoki. Dyspozytor S19 ma własną rolę i zakres obserwacji; administrator nie dziedziczy jej automatycznie i odwrotnie. Szczegóły 20-23.

## Nowa powierzchnia serwisowa - stan 1.7
**Potwierdzone statycznie:** Net/LcsNet.cs:137-157 pozwala zadeklarować tryb serwisowy, pobierać diagnostykę i przekazać usterkę do Instruktor.ApplyFault bez logowania/roli. Ta sama klasa obsługuje domyślne publiczne wiązanie IPAddress.Any (:63-68) i stanowiska uruchamiane w MainWindow.xaml.cs:292. Oddzielny lokalny port 47121 nie zamyka tej drogi na porcie stanowisk 47120. Nie wykonano próby sieciowej ani exploitu.

Przed publicznym pilotem trzeba usunąć lub zabezpieczyć WSZYSTKIE stare wejścia serwisowe: publiczny listener odmawia, prywatny wymaga konta/klucza oraz kompetencji. Odczyt diagnostyki nie upoważnia do usterek; administrator nie otrzymuje automatycznie praw instruktora lub sterowania KZ. N42/O20 sprawdza bezpośrednie żądanie i próbę obejścia przez starszy protokół, cofnięcie praw, idempotencję oraz log bez sekretów. Zatwierdzony lokalny klucz SUK.Admin i ochrona nowego API nie zabezpieczają automatycznie dawnego LcsServer.


## Logi nie są kanałem bocznym - 1.8
F24 wymaga redakcji przed kolejką/sinkiem: bez haseł, JWT, cookie, prywatnych kluczy, kodów rejestracji i pełnych treści rozmów. Pola tekstowe mają typy/limity i kodowanie JSONL przeciw wstrzyknięciu wierszy. ACL rozdziela dostęp do diagnostyki i audytu; moderator nie otrzymuje automatycznie pełnych logów hosta. Retencja i eksport obejmują ochronę danych. Zapełnienie dysku i kolejki jest testowanym stanem, a nie cichym catch. Szczegóły25, N43/N44.
