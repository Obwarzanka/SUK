# Profil usługi świata Windows — S02.03

Generator przygotowuje `profile.json`, `plan.json` i samodzielny `Install-WorldHostService.ps1`. Zapisuje wyłącznie nowy katalog wynikowy. Domyślnie odpytuje `sc.exe showsid SUK.WorldHost` (odczyt); jawny `-AccountSid` pozwala zbudować profil procesu testowego. Ani generator, ani domyślne uruchomienie wygenerowanego instalatora nie instalują i nie uruchamiają usługi.

```powershell
.\deploy\windows\WorldHost\New-WorldHostServicePlan.ps1 `
  -OutputDirectory 'D:\SUK-review\world-local' -WorldId 'local'
```

Konto docelowe to osobne wirtualne `NT SERVICE\SUK.WorldHost`, bez hasła. Nie jest administratorem ani LocalSystem. W środowisku domenowym konto wirtualne może korzystać z tożsamości komputera przy dostępie do zasobów sieciowych; nie stanowi to izolacji sieci. Ten host nie otwiera publicznego wejścia. Używa wyłącznie lokalnego Named Pipe do odczytu health. [Konta usług Microsoft](https://learn.microsoft.com/windows/security/identity-protection/access-control/service-accounts).

| Element | Domyślna lokalizacja i prawa konta usługi |
|---|---|
| Binaria | `%ProgramFiles%\SUK\WorldHost`, odczyt/wykonanie |
| Korzeń danych | `%ProgramData%\SUK\WorldHost\<worldId>`, odczyt/wykonanie |
| Zatwierdzone dane | `Inputs\station.json` i sidecary, odczyt/wykonanie |
| Konfiguracja | `Config\profile.json`, odczyt/wykonanie |
| Diagnostyka | `Logs`, Modify |
| Stan techniczny | `State`, Modify |

Każda gałąź i istniejące pliki otrzymują chronione ACL: konto usługi według tabeli, `SYSTEM` i `Administrators` FullControl; właścicielem jest Administrators. Nie są zachowywane dawne jawne ACE. Opcjonalny `-HealthClientSid` (wyłącznie pojedyncze konto `S-1-5-21-…` z RID od 1000 albo SID usługi `S-1-5-80-…`; grupy takie jak Users, INTERACTIVE czy Domain Users są odrzucane) przyznaje odczyt pipe health oraz jawnego `Config/profile.json`, z RX korzenia danych i Config. Te dodatkowe ACE nie dziedziczą się: nie dają dostępu do Inputs/Logs/State/binariów ani funkcji administracyjnych. Dzięki temu operator może użyć CLI health bez konta administratora. Konto bramy/publiczne role/konta gry powstaną w osobnych etapach.

Profil ustala start do 30 s, shutdown do 10 s, świeżość postępu ticku do 2 s i miękki limit working set 1024 MiB. Przekroczenie limitu pamięci powoduje utratę Ready i kontrolowane zatrzymanie; nie jest twardą kwotą Job Object. Kolejka świata pozostaje 1024 wejścia/1 MiB, diagnostyka 4096 rekordów/8 MiB i 16 plików po 64 MiB. Brak trwałego audytu/replay w tym etapie. `State` nie jest checkpointem umożliwiającym wznowienie ruchu.

Przy późniejszej, osobno zleconej instalacji operator przygotowuje zatwierdzone binaria i pliki wejściowe w wskazanych lokalizacjach. Generator nie kopiuje `station.json` właściciela. Profil FDD wymaga .NET Runtime 10.0.13 x64 lub nowszej poprawki 10.0.x, dostępnego dla konta usługi; lokalne SDK dewelopera nie jest instalacją runtime usługi.

Przed zmianą instalator weryfikuje tożsamość, service SID, brak istniejącej usługi, oczekiwane katalogi produkcyjne, wymagane pliki i brak reparse/junction w całych drzewach oraz przodkach. Rodzice binariów i DataRoot muszą mieć zaufanego właściciela (SYSTEM/Administrators/TrustedInstaller), bez niezaufanego DeleteChild/WRITE_DAC/TakeOwnership; sam chroniony ACL dziecka nie zapobiega jego podmianie przez rodzica. Instalator nie naprawia uprawnień cudzych katalogów nadrzędnych. Profil z testowym SID lub niestandardowymi katalogami nie przechodzi instalacji. Operator musi przejrzeć wszystkie trzy artefakty, zweryfikować wydanie/hashe i zabezpieczyć staging przed innymi użytkownikami. Kontrola ścieżek i ACL nie jest ochroną przed administratorem równocześnie podmieniającym dane.

Po osobnym zleceniu operator uruchamia wygenerowany skrypt jako administrator z `-Apply`. Instaluje nową usługę początkowo disabled, ustawia osobne konto, service SID i ograniczone przywileje, następnie recovery oraz opóźniony autostart. Nie wykonuje `Start-Service`, nie zmienia istniejącej usługi i nie wdraża aktualizacji. Błąd pozostawia jawny błąd skryptu; po utworzeniu SCM nie usuwa usługi ani danych automatycznie. [Składnia sc create](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/sc-create).

Recovery: po awariach restarty za 60, 300 i 900 s, następnie brak dalszej akcji. Licznik resetuje się po 24 h bez błędu — nie jest to ruchome okno dokładnie trzech restartów na dobę. Czwarty SC_ACTION_NONE zapobiega powtarzaniu ostatniego restartu bez końca. Recovery obejmuje crash i zatrzymanie z niezerowym kodem; żądany stop z kodem zero nie jest awarią. Konfigurację ustawia jawne Win32 `ChangeServiceConfig2`, bez uruchamiania polecenia naprawczego lub restartu komputera. [SERVICE_FAILURE_ACTIONS](https://learn.microsoft.com/en-us/windows/win32/api/winsvc/ns-winsvc-service_failure_actionsw), [flaga niezerowego zatrzymania](https://learn.microsoft.com/en-us/windows/win32/api/winsvc/ns-winsvc-service_failure_actions_flag).

Późniejsza próba operacyjna po instalacji: Start-Service → rzeczywisty `Lcs.WorldHost.exe --health "C:\ProgramData\SUK\WorldHost\local\Config\profile.json"` → Stop-Service → health niegotowy/niedostępny → kontrolowana awaria i nowa epoka → sprawdzenie recovery → reboot bez logowania. Klient health przyjmuje ścieżkę zaufanego profilu, sprawdza jego prawa, właściciela pipe i zgodność PID odpowiedzi z rzeczywistym procesem serwera; sama nazwa pipe nie potwierdza tożsamości. Sam stan `Running` SCM, istnienie pliku lub otwarta rura nie potwierdza Ready/świeżości. Odzyskanie checkpointu i automatyczne wznowienie ruchu nie są dostarczane w S02.03.

W tym etapie nie wykonuje się instalacji, zmian praw systemowych, autostartu, recovery pod SCM ani rebootu. Testy procesu konsolowego i generatora dowodzą wyłącznie opisanych części. Wymiar PC/TLS/backup D09 oraz publiczny pilot nadal wymagają późniejszych etapów.
