# 01. Wymagania i granice zakresu

## Dziewięć instrukcji organizacyjnych
| ID | Wymaganie | Pokrycie |
|---|---|---|
| R01 | Sam plan, poza Git | 00, AI_START i wszystkie karty |
| R02 | Nie wymyślać koła na nowo | 02-03: rdzeń i gotowe komponenty |
| R03 | Użyć subagentów do poznania źródeł | trzy analizy: kod/sieć; ruch/źródła; technologie/bezpieczeństwo |
| R04 | Symulator Urządzeń Komputerowych, SUK | cały pakiet; historyczne ścieżki bez zmiany |
| R05 | Dla człowieka i AI, grafiki z opisami | Markdown, HTML, PDF, SVG/PNG, manifest |
| R06 | Etapy osobno, oszczędzanie kontekstu | 12, AI_START, etapy/S00-S17 |
| R07 | Bezpieczeństwo publicznej infrastruktury | 05,09,11 i bramka S13 |
| R08 | Przeczytać Agent/Claude | AGENTS, CLAUDE, ROZWOJ, README i plany modułów/UT |
| R09 | Oszczędzać serwer i komputery klientów | 10: ograniczone kolejki, subskrypcje, pomiary |

## Funkcje docelowe
| ID | Funkcja | Etapy | Odbiór |
|---|---|---|---|
| F01 | Współpraca stycznych posterunków | S02, S04-S07, S13 | spójny świat i jeden pociąg po obu stronach |
| F02 | LCS, odcinki, nadzór, pomocniczy | S05-S06 | jawna własność i delegacja |
| F03 | Konta i statystyki | S03, S08 | trwałe konto, zdarzenia serwerowe |
| F04 | Bany, kick, blokada posterunku | S05, S08 | działają też po reconnect |
| F05 | Tekst i głos przez urządzenia | S09-S11 | dozwoleni odbiorcy i prawa na serwerze mediów |
| F06 | Informacje człowiek-AI | S09, S14-S15 | wspólny format i potwierdzenia |
| F07 | Ruch na nieobsadzonych posterunkach | S14-S16 | AI przez ten sam pipeline i KZ |
| F08 | Krzyżowanie, wyprzedzanie | S16 | scenariusze zaakceptowane przez właściciela |
| F09 | Singleplayer, AI tylko na stycznych | S02, S14-S15 | lokalnie, bez obowiązkowego konta i Internetu |
| F10 | Serwer liczy ruch i zależności | S01-S04 | brak API ustawiania stanu przez klienta |
| F11 | Bezpieczne łącze i wersja | S04, S12-S13 | TLS i walidacja protokołu/pakietów |
| F12 | Autoupdate | S12 | sprawdzony pakiet, bezpieczne przerwanie i odzyskanie |
| F13 | Windows, 20-40 osób, wzrost | S02, S07, S13, S17 | pomiar 40 klientów, test zapasu |

## Granice
Migracja sieci nie zmienia reguł srk. Wydzielenie rdzenia zachowuje zachowanie; zmiany blokad, kart urządzeń i procedur mają osobne scenariusze i zgodę merytoryczną właściciela. Nie projektujemy automatycznie zależności, zobrazowania ani stacji.

Nie przepisujemy edytorów ani nie migrujemy station.json właściciela. Adapter odczytu istniejącego formatu wystarcza dla pierwszej sieci. Docelowe pakiety obszaru zatwierdza człowiek.

Prywatna gra LAN/VPN może używać nowego bezpiecznego protokołu. Dawny Radmin/TCP nie stanowi docelowej bramy publicznej. Aktualne zlecenie rozszerza wcześniejszy kierunek Radmin opisany w ROZWOJ.md.

## F14. Własny moduł zależności każdego posterunku
USTALONE przez właściciela: każdy posterunek ma własny KZ. Dane zależności, stan, polecenia, rozkazy, meldunki, status i usterki są odrębne. LCS może obejmować wiele KZ i stanowisk. Fizyczny ruch pozostaje spójny na serwerze. Pokrycie: S01-S02, S04-S07 oraz S13, szczegóły w rozdziale 16. Odbiór obejmuje izolację stanu, uprawnień i usterek oraz komunikację dwóch sąsiednich KZ.

## F15. Po oddaniu: podgląd bez sterowania
Wymaganie właściciela. Zwykłe oddanie posterunku nie odbiera dyżurnemu LCS bieżących meldunków, ale odbiera prawo do komend tego posterunku. Stare żądania i potwierdzenia nie wykonują się po przekazaniu. Inne posiadane posterunki działają bez zmiany. Powrót/reconnect nie przywraca prawa sterowania samoczynnie. Odbiór: N24 i rozdział 17; etapy S05-S07/S13.

## Korekta po audycie źródeł - wersja 1.3
### F16. Dwie grupy EBILock w każdym KZ
USTALONE przez właściciela i potwierdzone w źródle: zestaw zasadniczy i gorąca rezerwa, oba liczące na autorytatywnym hoście. Grupy 1/2 mają stałe tożsamości, role mogą się zmieniać. Prywatny stan, ten sam uporządkowany strumień wejść, jawna synchronizacja i jedno źródło wyjść. To nie głosowanie MOR-3 2z2 pomiędzy grupami. Dwukanałowość przetwarzania ma osobny model wewnątrz grup.

Pokrycie: S02A granice kart/terenu/systemu nadrzędnego; S02B grupy; S02C synchronizacja/selektor/przełączenie; S02D transmisja i diagnostyka; S04/S06/S07/S08/S13 zachowują te gwarancje. Odbiór N25-N34/O11-O14. Pełne KZ lokalnego trybu offline używają tego samego modelu. Uproszczona styczna poza obszarem nie udaje pełnego posterunku.

R06 obejmuje 28 kart. F14 własny KZ, F15 podgląd bez sterowania i F16 dwie grupy obowiązują łącznie; jedno nie zastępuje drugiego.

## F17. AI ruchowe jako autorski algorytm SUK
USTALONE przez właściciela: automatyczny dyżurny i planista ruchu są tworzonym przez nas algorytmem opartym na danych, automatach stanów i zatwierdzonych regułach. **LLM nie jest elementem podejmowania decyzji, prowadzenia ruchu ani rezerwowym wykonawcą.** Dotyczy obu trybów gry. Bez zależności od API modelu, tokenów lub usługi językowej.

Pokrycie: 03/07/10/13, AI_START i S14-S16. Reguły, konfiguracja i wejścia są wersjonowane; wynik i przyczyna odtwarzalne. Rozmowy proceduralne mają parametry i szablony, a ewentualne STT/TTS pozostają osobną warstwą audio. Odbiór N35/O15 należy do S14-S16 po S13. Ta zmiana nie przyspiesza prac nad algorytmem kosztem infrastruktury.

## Wymagania uzupełniające 1.5
| ID | Wymaganie | Pokrycie i odbiór |
|---|---|---|
| R10 | Konkretne języki, biblioteki, frameworki i granice techniczne | Rozdziały 20/23, manifest; wersje blokowane przed implementacją |
| F18 | Pełny control panel administracji jako końcowy etap | Rozdział 21, S18 po S16/S17, N38/O17; minimalne S08 przed pilotem |
| F19 | Końcowy tryb dyspozytora: nadzór i koordynacja | Rozdział 22, S19 po S18, N39/O18; ViewGrant bez ControlLease |
| F20 | Przepisanie domenowej logiki KZ na Ada | S02A1-S02A4, rozdział 23, N36/N37/O16; C# pozostaje hostem/pulpitem/terenem |
| F21 | Dwie niezależne implementacje A/B, dwa osobne czaty | S02A2 oraz S02A3, wspólny zamrożony kontrakt, odrębny kod; po zamrożeniu integracja S02A4 |
| F22 | Losowanie zasadniczej grupy KZ przy starcie serwera | S02C, rozdziały 18/23, N40/O16; per posterunek, po Ready, zapis i replay bez ponownego losowania |

F20 zastępuje wcześniejszy projekt wspólnego kodu C# dla kanałów KZ. F21 odtwarza rozdział implementacji programów, a nie dwa niezależne fizyczne serwery. Dwa czaty i język Ada same nie dowodzą certyfikowanej różnorodności lub SIL. Źródłowe rozróżnienie dwóch grup i dwóch kanałów pozostaje zachowane. Liczba kart: 28.


## Wymaganie F23 - plan 1.6
| ID | Wymaganie | Pokrycie |
|---|---|---|
| F23 | Osobna aplikacja admina, indywidualny klucz i login/hasło, pierwsza rejestracja | S03/S08: infrastruktura N41A przed pilotem; S18: pełny WPF i N41B/O19. Lokalne generowanie zatwierdzone przez właściciela; preferowany niewyeksportowalny CNG/TPM. Serwer otrzymuje klucz publiczny i wydaje certyfikat. |


## Wymagania wykonania 1.8
| ID | Wymaganie właściciela | Pokrycie i odbiór |
|---|---|---|
| R11 | Każdy osobny czat wykonuje mały etap z dostępnym kontekstem | 26, manifest.jobs, czaty/ i prompty_czatow/; N45/O22, DAG, raporty i ścisły zakres jednego zadania |
| R12 | Gotowe prompty do niezależnych implementacji KZ według dokumentacji | 27 i prompty_kz/; S02A1–S02A4, N46/O23; dwa izolowane łańcuchy A/B z tym samym hashem specyfikacji |
| R13 | Ponowna analiza planu, korekty i końcowa kopia w głównym folderze | 28, RAPORT_WERYFIKACJI i SUMY_SHA256; po kontroli PDF oraz pakiet w D:\Projects\SUK, bez operacji Git |
| F24 | Program i każdy nowy moduł tworzą logi | 25, S00/S01 fundament, S06 trwały audyt, każdy następny moduł; N43/N44/O21, ograniczenia zasobów i redakcja |

To 37 śledzonych wymagań: R01–R13 i F01–F24. Pierwotne ograniczenie zapisu dokumentacji poza repo rozszerzono wyłącznie o końcową kopię planu w D:\Projects\SUK, wskazaną późniejszą odpowiedzią właściciela. Nie jest to zgoda na edycję aplikacji, śledzonych źródeł, commit lub push.
