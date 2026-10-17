# 02. Stan istniejącego programu

Plan 1.8 | 17.10.2026 | analiza statyczna, bez uruchamiania programu.

**Stan analizowany:** HEAD `5eddb5c5d58eccd5af6467f7ca2373a2daed6568`, porównany z `2120ab0bcbedbc503a4a859eda26a7823644d0b7`. Pierwotna analiza dotyczyła `4869fc185a70e6f390bf2bf1ad07680fdb5c32ff`; wcześniejsze opisy i rejestry pozostają materiałem historycznym w archiwum. Ten rozdział zastępuje ich nieaktualne ustalenia. Rejestr ponownego odczytu dla tej rewizji: `ZRODLA_KONTROLA_1_7.json`.

Przeczytano zmiany oraz aktualne źródła wskazane poniżej. **Nie wykonano buildu, selftestu, testu penetracyjnego ani próby na dwóch komputerach.** Linie odnoszą się do analizowanego HEAD; przy implementacji trzeba je ponownie sprawdzić. Zastany nieśledzony `SUK_plan_multiplayer.pdf` w repozytorium pozostawiono nietknięty. Praca dokumentacyjna nie zmienia repozytorium, Git ani `ROZWOJ.md`.

## Co już wydzielono i wykorzystujemy

Ścieżki kodu w tabelach są względne wobec `app/LcsWidzew/`, chyba że wskazano dokument repozytorium.

| Element | Aktualny dowód | Znaczenie dla planu |
|---|---|---|
| Sesje lokalna/zdalna | `Sim/Session.cs:10–89`; `Net/LcsNet.cs:212–351` | Ten sam pulpit przez ILcsSession; interfejs sesji pozostał bez zmian w porównaniu. |
| Meldunki i jawny brak danych | `Sim/Meldunki.cs:31–88,103–130` | Istnieją DTO zobrazowania i KzLink.NoData; nie stanowią jeszcze pełnego protokołu świeżości/uprawnień. |
| Wykonanie po stronie serwera | `Net/LcsNet.cs:162–163`; `Sim/Pulpit/SystemNadrzedny.cs:294–338,450–469` | Zdalny klient przesyła tekst, wykonanie trafia do serwera. To użyteczna baza autorytatywności, bez dowodu bezpieczeństwa dostępu. |
| System nadrzędny | `Sim/Pulpit/SystemNadrzedny.cs:14–46,215–290,294–384,404–545`; `Sim/Desk.cs:24–63` | Parser, adresowanie, dwuetapowość, stanowiska, obszary i książka są wydzielone. Desk.cs jest obecnie fasadą, nie miejscem tej implementacji. |
| Osobna klasa KZ | `Sim/Simulator.cs:35–45`; `Sim/Kz/KomputerZaleznosci.cs:14–47`; `Sim/Kz/Fasada.cs:12–21` | KZ ma własną klasę i słowniki. Nadal powstaje jeden KZ dla całego wczytanego układu. |
| Przekazywanie za zgodą | `Sim/Pulpit/SystemNadrzedny.cs:472–506` | Zachowana podstawa przydziałów; nie jest jeszcze wersjonowanym modelem lease/delegacji. |
| Karty i zasilanie | `Sim/Urzadzenia/Urzadzenia.cs:45–75`; `Sim/Urzadzenia/KartaOdcinka.cs:37,61`; `Sim/Simulator.cs:122–125` | Istnieją karty odcinka/zwrotnicy/sygnalizatora i zasilanie. Aktualny podział opisuje ROZWOJ.md:61–80,187–199. Nie planować ponownego tworzenia tych samych klas. |
| PIP po zajętościach | `Sim/Pulpit/Pip.cs:66–118`; `Sim/Meldunki.cs:124` | Publikacja okien nie czyta już Trains.List; poprawa względem starej analizy. |
| Rozkład, Otoczenie, Rejestrator | `Sim/Simulator.cs:39–42,123–127` | Zachowujemy istniejące moduły, czas i kolejność cyklu jako materiał migracji. |
| Selftest i scenariusze | `App.xaml.cs:41–46`; `Sim/Rozklad.cs:29`; `ROZWOJ.md:84–90,186` | Istnieją punkty uruchomienia regresji. Ich obecność nie jest wynikiem prób wykonanych podczas tego audytu. |

Wydzielenie systemu nadrzędnego i cienkiej fasady KZ to rzeczywista zmiana struktury względem poprzedniej bazy. Nadal trzeba oddzielić obliczenia od fizycznych skutków, własność posterunkowych KZ i bezpieczne kontrakty. Samo przeniesienie metod do nowej klasy nie zalicza etapów multiplayera ani modelu dwóch grup.

## Nadal otwarte luki przed publicznym udostępnieniem

| Problem | Aktualny dowód | Wymagana praca w istniejącym planie |
|---|---|---|
| Surowy TCP, bez TLS i potwierdzonej tożsamości | `Net/LcsNet.cs:63–68,95–114,130–166` | Brama HTTPS/WSS i serwerowe ustalanie tożsamości; S03/S04. |
| Klient przesyła User i Role | `Net/LcsNet.cs:166,340`; `Sim/Pulpit/SystemNadrzedny.cs:517–521`; `Sim/State.cs:154` | Zastąpić deklarację klienta zweryfikowanym kontem; rola i zakres wynikają z serwera. |
| Nowy dostęp serwisowy bez uwierzytelnienia | `Net/LcsNet.cs:137–157`; `MainWindow.xaml.cs:283–299` | Ochrona/odseparowanie diagnostyki i usterek, bez obejścia SUK.Admin; S03/S04/S08/S13. |
| Wątek na klienta, ReadLine bez własnej granicy | `Net/LcsNet.cs:95–114` | Ograniczenia połączeń/ramek/prób, timeout i asynchroniczna obsługa; bez czekania w cyklu świata. |
| Synchroniczne WriteLine/Flush | `Net/LcsNet.cs:47,184–197,252` | Ograniczone kolejki i wysyłka poza cyklem; polityka wolnego odbiorcy. |
| Host zależy od Dispatcher i WPF | `Net/LcsNet.cs:8,50,61,114`; `MainWindow.xaml.cs:30,376–388` | Osobny host bez okna, zachowujący uporządkowany cykl. |
| Pełny stan nominalnie 5/s, Seq na publikację | `MainWindow.xaml.cs:388`; `Sim/Meldunki.cs:105–129` | Wspólny tick/epoki i protokół resynchronizacji, następnie delty po pomiarze. |
| Brak filtrowania według odrębnego prawa podglądu | `Sim/Meldunki.cs:109–128`; `Net/LcsNet.cs:123–124,184–197` | ViewGrant osobno od ControlLease, filtrowanie po stronie serwera. |
| Brak oceny wieku telegramu przy milczącym połączeniu | `Net/LcsNet.cs:298,333` | Świeżość/heartbeat i jawny stan nieaktualny, bez traktowania otwartego TCP jako aktualności. |
| Własność tylko posterunek → stanowisko | `Sim/Pulpit/SystemNadrzedny.cs:36–41,472–506` | Zakresy, odcinki, delegacja, epoki i odbieranie praw zgodnie z rozdziałami06/17. |
| Wyjątki dla globalnych komend i dopuszczanie nieznanego posterunku | `Sim/Pulpit/SystemNadrzedny.cs:305–306,429–445` | Jawna autoryzacja każdej operacji i rozwiązanego celu; brak domyślnej zgody. |
| Telefon trafia do wspólnego rejestratora | `Sim/Pulpit/SystemNadrzedny.cs:510–514`; `Net/LcsNet.cs:75,184` | Prywatne urządzenia/kanały i osobne prawa łączności; S09–S11. |
| Statystyki wspólne w pamięci | `Sim/Simulator.cs:22`; `Sim/Desk.cs:83` | Trwałe zdarzenia kont i idempotentne projekcje; S08. |
| Brak negocjacji dopuszczalnej wersji klienta | `Net/LcsNet.cs:123–140,240–248` | Zgodność protokołu i wydania; S04/S12. Odpowiedź serwisowa wersja=1 nie jest taką negocjacją. |
| Rosnący rejestrator w pamięci | `Sim/Rejestrator.cs:18–20,55,66,72` | Retencja i zapis poza tickiem; ograniczenie listy w UI nie ogranicza tych list. |

### Nowa powierzchnia serwisowa

`Handle` przyjmuje `t=serwis` przed sprawdzeniem stanowiska; ustawia `c.Serwis=true` bez konta, roli lub sekretu. Następne wiadomości pozwalają odczytać karty, stan KZ i analizę przebiegu, a `t=usterka` wywołuje `sim.Instruktor.ApplyFault` po sprawdzeniu wyłącznie nazwy rodzaju usterki. To **potwierdzona statycznie ścieżka dostępu**, bez wykonanej próby sieciowej.

Osobny port 47121 jest uruchamiany na loopback, lecz oba wejścia używają tej samej klasy LcsServer i tego samego Handle. Domyślny serwer stanowisk może nasłuchiwać na wszystkich interfejsach na 47120 (`Start:63–68`, `Serve:292–299`). Ograniczenie 47121 nie usuwa dostępu do wiadomości serwisowych przez 47120. Samo zabezpieczenie przyszłego Admin API nie wystarczy, jeżeli stara ścieżka pozostanie dostępna. Diagnostyka i usterki muszą podlegać zatwierdzonym kompetencjom oraz audytowi; obecność Komputerownia.exe nie jest poświadczeniem.

### Cele i końce blokad

`Controls` dopuszcza pusty lub nieznany posterunek. `CheckControl` akceptuje własność dowolnego końca blokady (`SystemNadrzedny.cs:429–445`), a `EndFor:123–140` potrafi wybrać konkretny koniec po etykiecie, kierunku lub posterunku. `Adresy:255–257,284–288` przekazuje ten wybór do KZ; `Sim/Kz/Blocks.cs:145–148` używa `b.Ends[e]`. Brakuje ponownego sprawdzenia prawa do wybranego końca. Zachować scenariusz odtworzenia; nie oznaczać go jako wykonanego exploitu.

Uproszczenie Wbl dla Internal nadal zakłada wspólnego dyżurnego i ustawia kierunek jednym poleceniem (`Sim/Kz/Blocks.cs:202–209`). Podział obsady obu końców wymaga zatwierdzonej decyzji domenowej i testu. KW potwierdza wszystkie alarmy jeszcze przed kontrolą zalogowania (`SystemNadrzedny.cs:305–306`); NN/oNN/AGR omijają kontrolę obszaru (`:434`). Nie przypisywać skuteczności dodatkowym globalnym skrótom bez sprawdzenia ich osiągalności przez parser.

## Jeden KZ i globalny kontekst — co pozostało

`Simulator.cs:43` tworzy jeden KomputerZaleznosci dla całego Station. Jego słowniki są teraz w `Sim/Kz/KomputerZaleznosci.cs:29–36`; `Sim/Kz/Fasada.cs:14–21` udostępnia je dawnym odbiorcom. Nie powstały jeszcze odrębne KZ posterunków, dwie grupy ani niezależne implementacje Ada.

`User/Page/Pending/CurDesk` znajdują się w `Sim/Pulpit/SystemNadrzedny.cs:29–41`. `As:464–469` czasowo podmienia kontekst stanowiska. PolecenieSystemowe (`:14`) rozwiązuje adresy, ale nie zawiera kontraktu requestId/tożsamości/lease/epok. Globalny LinkDown pozostaje w `Sim/Meldunki.cs:95–97`, a przyjmowanie polecenia sprawdza go w `SystemNadrzedny.cs:298`.

Dwukrotne wywołanie obecnego KZ nie tworzy bezpiecznie rezerwy: `Sim/Kz/Interlocking.cs:94–125` zmienia utwierdzenia i wywołuje rozkazy; `Sim/Kz/Commands.cs:179–184,213–216` steruje zasilaniem i zerowaniem; `Sim/Urzadzenia/KartaOdcinka.cs:37,61` publikuje zmianę statusu. S02A ma oddzielić obliczenia od skutków, a dalsze podkroki wdrożyć zatwierdzone grupy/kanały i pojedyncze wyjście. Samo filtrowanie meldunku lub dopisanie postId nie spełnia F14/F16.

## PIP i zależności klienta

Dawne stwierdzenie, że publikacja PIP bierze okna z Trains.List, jest nieaktualne. `Pip.Tick:66–86` śledzi zajętość Secs.TrainOcc, a `Pip.Okna:117–118` tworzy projekcję numerów. Ruch/rozkład inicjuje i zmienia numery przez `Trains.cs:115,136,269–288,350`. Pozostaje zadanie przeniesienia tych jawnych danych do docelowych kontraktów; nie należy ponownie projektować działającego śledzenia. Pozostałe przecieki ruchu i Otoczenie są osobnym zakresem, z zachowaniem odłożonego kroku 8.

Projekt nadal jest WinExe/net8.0-windows/WPF (`LcsWidzew.csproj:4–8`). Station zawiera Point/Rect (`Model/Station.cs:160–171`), Trains zawiera Color (`Sim/Trains.cs:16,52`), RealTimetable używa App.StationPath (`:19`), a sieć Dispatcher/App.LogError (`LcsNet.cs:8,50,114,272`). Zegar wykonania i Push nadal prowadzi okno (`MainWindow.xaml.cs:30,376–388`). Neutralne dane i usługi pozostają potrzebne do hosta bez UI. Lcs.Model jest modelem nowego edytora UT, nie automatycznie gotowym modelem wykonawczym symulatora.

## Aktualne zasady i kierunek migracji

AGENTS.md i CLAUDE.md są już zgodne: w linii14 wskazują build, selftest oraz EdytorUT.exe --test. Usunięto więc dawną rozbieżność tych dwóch instrukcji. Historyczne --edtest/fuzz/dfuzz pozostają w `PODZIAL_MODULOW.md:138–139`; `ROZWOJ.md:186` wyjaśnia ich usunięcie. S00 ustala aktualną bramkę, bez przywracania starych edytorów. Wyniki opisane w ROZWOJ są historią repozytorium, nie testami wykonanymi przez autorów tego planu. `ROZWOJ.md:210` nadal zastrzega brak próby Radmin na dwóch komputerach.

Dokończono kolejne klasy/kroki istniejącego podziału; nie planujemy ich tworzenia ponownie. Nie oznacza to odbioru docelowego multiplayera. Zatwierdzona migracja pozostaje: domena KZ w niezależnych Ada Kz_A/Kz_B, po oba kanały w każdej grupie 1/2, reszta C#/.NET. Stary C# służy autorowi neutralnej specyfikacji i integratorowi jako kontrolowane odniesienie, bez produkcyjnego fallbacku i bez wspólnego przepisu dla autorów A/B.

Nie zmieniają się decyzje dotyczące kont, bezpieczeństwa, głosu, aktualizacji ani osobnej aplikacji SUK.Admin. Rozdział 21 utrwala zatwierdzone lokalne generowanie prywatnego klucza administratora, preferencyjnie niewyeksportowalnego CNG/TPM; serwer otrzymuje klucz publiczny i wydaje certyfikat. Obowiązuje polityka konto AND klucz AND bieżące prawa. D18 dotyczy już tylko szczegółów enrollmentu, odzyskiwania, profilu TPM i rotacji; serwerowe generowanie lub wydawanie prywatnego klucza jest wykluczone. Żadnej z tych funkcji nie zaliczamy na podstawie obecnego TCP lub nowej Komputerowni. Nazwa produktu to Symulator Urządzeń Komputerowych — SUK; historyczne nazwy projektów nie nakazują teraz zmiany katalogów.

Obowiązuje zakaz zmian chronionych danych, projektowania zależności przez AI oraz nieautoryzowanego zapisu Git/wdrożenia. Rewizja 1.7 aktualizuje plan do stanu kodu, nie jest zgodą na implementację.



## Zakres późniejszej kopii1.8
Właściciel wskazał D:\Projects\SUK jako miejsce końcowej dokumentacji. To upoważnia do zastąpienia tam PDF po zachowaniu kopii i zapisania pakietu planu. Nie zmienia kodu ani wcześniejszych ustaleń analizy statycznej i nie obejmuje Git.
