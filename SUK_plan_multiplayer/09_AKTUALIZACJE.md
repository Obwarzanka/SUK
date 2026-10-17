# 09. Wersje i aktualizacje

## Dwie różne potrzeby
Serwer kontroluje zgodność klienta i danych. Updater pomaga użytkownikowi pobrać oraz bezpiecznie zastosować wydanie. Numer wersji lub hash zgłoszony przez EXE nie dowodzi, że klient nie został zmodyfikowany. Autorytet serwera pozostaje wymagany.

Na datę planu .NET 8 ma wsparcie do 10.11.2026; .NET 10 LTS do 14.11.2028 według [polityki Microsoft](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core). Rekomendowana migracja klienta i hosta do .NET 10 w S01, z testem zgodności. Nie publikować nowej infrastruktury na runtime bez wsparcia.

## Macierz zgodności
Oddzielne pola: clientVersion/buildId, protocolVersion, engineVersion, contentSchemaVersion, contentHash i releaseChannel. Świat przypina wersję silnika i treści. Klient otrzymuje minimalną obsługiwaną wersję i czytelny komunikat. Niezgodnego protokołu nie próbujemy „naprawić” przez pomijanie pól.

Przy zmianie niekompatybilnej nowe sesje trafiają na nową wersję; stare są kontrolowanie kończone w oknie utrzymania. Żadnej cichej podmiany logiki w aktywnym świecie. Starsze wydanie może zachować offline singleplayer, o ile użytkownik zna ryzyko i jego dane są zgodne; publiczna brama odmawia nieobsługiwanej wersji.

## Framework i łańcuch zaufania
Velopack jest kandydatem do pakowania, kanałów i instalacji Windows. [Podpisywanie Velopack](https://docs.velopack.io/packaging/signing) nie jest samo dowodem ochrony wszystkich metadanych feedu. Nie zakładamy gotowego połączenia Velopack + TUF dla .NET: S12 musi potwierdzić utrzymywaną integrację lub wybrać inny gotowy mechanizm.

Wymagania odbioru:
1. Podpis aplikacji i instalatora oraz weryfikacja wydawcy.
2. Uwierzytelnione metadane wiążą wersję, kanał, długość, SHA-256 i czas ważności.
3. Kotwica zaufania jest dostarczona z zaufanym instalatorem, nie pobrana z tego samego niezaufanego feedu.
4. Ochrona przed rollback/freeze, rotacja i unieważnienie klucza.
5. Klucz publikujący poza publicznym hostem, z minimalnym dostępem procesu wydania.
6. Brak uruchomienia paczki przed pełnym sprawdzeniem.

[TUF](https://theupdateframework.github.io/specification/v1.0.36/) jest istniejącym modelem ochrony aktualizacji. Użyć utrzymywanego rozwiązania, nie pisać „uproszczonego TUF”. Jeśli wybrany framework nie spełnia bramki, nie zaliczać S12 na podstawie samego HTTPS.

## Obieg wydania
Budowanie w kontrolowanym środowisku, testy, spis zależności/SBOM, podpis, publikacja niezmiennych artefaktów, a dopiero potem podpisanych metadanych kanału. Canary dla testerów i kontrolowane zwiększanie odbiorców. Pakiety danych obszaru mają własny manifest i schemaVersion.

Klient sprawdza dostępność na starcie z cache i odstępem, pobiera w tle z limitem pasma, weryfikuje i proponuje zastosowanie po zakończeniu służby. Nie restartuje sam podczas obsługi posterunku. Aktualizacja bezpieczeństwa może blokować nowe wejście do świata zgodnie z komunikatem serwera.

Dane użytkownika, układy i konfiguracja pozostają poza wymienianymi plikami programu. Własnych plików station.json nie nadpisuje instalator. Migracje zapisów robią kopię i znają zgodność wstecz.

## Odporność na błąd
Testujemy przerwany download, brak miejsca, utratę prądu, uszkodzoną deltę, plik z błędnym podpisem, stare metadane, zły kanał i zmianę klucza. Delta ma fallback do pełnego zweryfikowanego pakietu. Stan instalacji musi być możliwy do naprawy bez utraty projektów.

Odzyskanie po wadliwym wydaniu rekomendujemy jako nowe podpisane wydanie z wcześniejszym sprawdzonym kodem, z wyższą wersją i zgodnym formatem danych. Dowolny downgrade podważa ochronę przed cofnięciem. Awaryjna wymiana klucza ma oddzielną udokumentowaną procedurę.

Dodatkowy test bezpieczeństwa: wygasłe metadane oraz cofnięcie zegara po wcześniejszej poprawnej aktualizacji. Implementacja korzysta z istniejącej ochrony zaufanego czasu/wersji zgodnej z wybranym frameworkiem, zamiast ufać bezwarunkowo bieżącej dacie systemowej.

## Korekta po audycie źródeł - wersja 1.3
Obie grupy mają zgodny zestaw wersji algorytmów, danych, schematów i kontraktów. Inny hash blokuje Ready. Nie deklarować niezależnego rolling update grup jako potwierdzonej funkcji EBILock. Aktualizacja przechodzi kontrolowany cykl zgodności/odtworzenia; odbiór synchronizacji i przełączenia powtarza się także offline.

## Artefakty Ada
Manifest wydania i zgodności obejmuje SukKz.dll, runtime/toolchain GNAT, wersję ABI, implementacje A/B, konfigurację reguł i schemat danych. Klientowy oraz serwerowy host lokalny używają tego samego zatwierdzonego zestawu. Nie podmieniamy DLL w trakcie cyklu ani nie pobieramy natywnego kodu z paczki posterunku. Restart i losowanie nowej grupy wymagają zwykłej procedury uzgodnienia/odtworzenia świata.


## SUK.Admin 1.6
Osobny podpisany pakiet/kanał wydania i zgodność z Admin API. Brak wspólnych prywatnych kluczy w dystrybucji; instalator i logi nie eksportują klucza. Inny komputer wymaga nowego enrollmentu. Podpis pakietu nie zastępuje tożsamości admina.

## Dane i dodatkowe aplikacje obecne w bazie - 1.7
Macierz zgodności obejmuje również istniejącą Komputerownię i tabelę Data/sygnaly_ie1.json. Pole wersja=1 starego serwisu nie zastępuje handshake'u i kontroli dopuszczonego protokołu. Przyszłe UT/.zob.json mają zatwierdzony plan, lecz adapter do station.json i schemat pakietów gry wymagają dopiero odbioru. Nie deklarować aktualizacji/konwersji danych, której nie zaimplementowano.
