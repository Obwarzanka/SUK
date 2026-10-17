# 14. Źródła i ślad analizy
Stan dokumentacji: 17.10.2026. Bieżący odczyt kodu odnosi się do HEAD 5eddb5c5d58eccd5af6467f7ca2373a2daed6568 (plan 1.8). Oryginalna baza 4869fc i pośrednia 2120ab0 pozostają w historycznych rejestrach. Nie wykonywano fetch, pull, commit, push ani modyfikacji indeksu. Źródła zewnętrzne weryfikowano w oficjalnych dokumentacjach; należy odświeżyć je przy implementacji.

## Źródła lokalne
Repozytorium: D:\Projects\SUK. Rejestr plików z SHA-256 znajduje się w ZRODLA_LOKALNE.json. Dowody linii są w rozdziale 02.

AGENTS.md i CLAUDE.md: zasady pracy. ROZWOJ.md: kierunek i stan. README.md: obsługa. PLAN_EDYTOR_UT.md: nowy UT i późniejsze przekazywanie danych. PODZIAL_MODULOW.md: zaakceptowany podział silnika oraz odpowiedzi właściciela w §7.

Zrodla/KOMPUTERY_notatki.md: struktura LCS, zależności i blokady. Zrodla/EBILock_komputerownia.md: warstwy i oznaczenia pewności; nie wszystkie propozycje są potwierdzonym opisem urządzeń.

Ie-20, Ie-104 i Ig-10 pozostają normatywnymi materiałami domenowymi w repo. Nie przeprowadzono w tej pracy audytu każdej procedury w pełnej treści PDF. Ten plan nie wprowadza nowych reguł srk. Przed taką zmianą trzeba przeczytać właściwe fragmenty i uzgodnić interpretację z właścicielem; dla telefonogramów sprawdzić także właściwe źródła Ir-1, których katalog repo nie zawiera jako osobnego pliku.

## Źródła zewnętrzne
| ID | Oficjalne źródło | Wykorzystanie |
|---|---|---|
| E01 | [Microsoft: wsparcie .NET](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) | koniec .NET 8 i okres .NET 10 |
| E02 | [Windows Service](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/windows-service?view=aspnetcore-10.0) | hostowanie Windows |
| E03 | [Konfiguracja SignalR](https://learn.microsoft.com/en-us/aspnet/core/signalr/configuration?view=aspnetcore-10.0) | limity, bufory, reconnect |
| E04 | [Skalowanie SignalR](https://learn.microsoft.com/en-us/aspnet/core/signalr/scale?view=aspnetcore-10.0) | backplane dopiero przy potrzebie |
| E05 | [Redis backplane](https://learn.microsoft.com/en-us/aspnet/core/signalr/redis-backplane?view=aspnetcore-10.0) | ograniczenia trwałości |
| E06 | [Autoryzacja SignalR](https://learn.microsoft.com/en-us/aspnet/core/signalr/authn-and-authz?view=aspnetcore-10.0) | sesje i prawa długich połączeń |
| E07 | [OpenIddict, integracja OS](https://documentation.openiddict.com/integrations/operating-systems) | logowanie desktop/WPF |
| E08 | [RFC 8252](https://www.rfc-editor.org/rfc/rfc8252.html) | zewnętrzna przeglądarka i klient publiczny |
| E09 | [RFC 9700](https://www.rfc-editor.org/rfc/rfc9700.html) | aktualne praktyki OAuth |
| E10 | [OWASP WebSocket](https://cheatsheetseries.owasp.org/cheatsheets/WebSocket_Security_Cheat_Sheet.html) | autoryzacja, limity, monitorowanie |
| E11 | [Mumble Windows](https://www.mumble.info/downloads/) | gotowy serwer Windows |
| E12 | [Mumble Server scripting](https://www.mumble.info/documentation/mumble-server/scripting/) | API administracyjne Ice |
| E13 | [Mumble.proto](https://github.com/mumble-voip/mumble/blob/master/src/Mumble.proto) | kanały, nasłuch, VoiceTarget |
| E14 | [Wydanie LiveKit 1.13.10](https://github.com/livekit/livekit/releases/tag/v1.13.10) | oficjalne binarki Windows |
| E15 | [Kod LiveKit na Windows](https://github.com/livekit/livekit/blob/v1.13.10/pkg/service/server.go) | ograniczenie capacity management |
| E16 | [SDK C++ LiveKit](https://github.com/livekit/client-sdk-cpp) | kandydat adaptera Windows |
| E17 | [SDK JS LiveKit](https://github.com/livekit/client-sdk-js) | alternatywa WebView2 |
| E18 | [Porty LiveKit](https://docs.livekit.io/transport/self-hosting/ports-firewall/) | firewall, UDP, TURN |
| E19 | [Podpisy Velopack](https://docs.velopack.io/packaging/signing) | podpis aplikacji/instalatora |
| E20 | [Dystrybucja Velopack](https://docs.velopack.io/distributing/overview) | kanały i feed |
| E21 | [Konkretne wersje Velopack](https://docs.velopack.io/integrating/specific-version) | polityka downgrade |
| E22 | [TUF](https://theupdateframework.github.io/specification/v1.0.36/) | model ochrony metadanych |
| E23 | [ISDR dokumentacja](https://www.symulator.isdr.pl/download/dokumentacja.pdf) | inspiracja funkcjonalna, §2.4 s. 53-58 |
| E24 | [ISDR strona oficjalna](https://www.symulator.isdr.pl/) | konfiguracje wielu stanowisk/LCS |

Nie kopiowano kodu ani grafik ISDR. Schematy w tym pakiecie są własnymi diagramami architektury SUK, nie zobrazowaniem pulpitu. Konkretne wersje bibliotek należy dobrać i przypiąć w S00/S10/S12, sprawdzając stan wsparcia i licencje.

## Subagenci
architektura_i_siec: ILcsSession, transport, host, role, testy.
zrodla_i_ruch: Otoczenie, blokady, źródła domenowe i ISDR, granice AI.
technologie_i_bezpieczenstwo: pierwotne źródła .NET, SignalR, głosu, OIDC i aktualizacji.
Analizy były do odczytu, bez uruchamiania symulatora i zmian repo. Ostateczne rekomendacje zebrano i uporządkowano w tym pakiecie.

## Źródła uzupełnień bezpieczeństwa głosu
[E25: LiveKit - tokeny i brak odwołania JWT self-hosted](https://docs.livekit.io/home/server/generating-tokens).
[E26: MumbleServer.ice - ServerAuthenticator i ACL](https://github.com/mumble-voip/mumble/blob/master/src/murmur/MumbleServer.ice).
[E27: LiveKit JS SignalClient - obsługa sygnalizacji](https://github.com/livekit/client-sdk-js/blob/main/src/api/SignalClient.ts).

## Bezpośrednia weryfikacja poradnika dla inspektorów
Lokalny Poradnik TOM I ma 728 stron. Odczyt tekstu i weryfikacja odpowiednich stron: §1.5.8 s. 121-122 tabela 21; §1.5.8.1 s. 142-146 tabela 23/25 i rys. 77; definicje s. 434. Numery PDF i drukowane w tych miejscach są jednakowe. Podgląd po oddaniu jest wymaganiem właściciela; poradnik opisuje oddzielnie informacje i nastawianie, bez dosłownej obietnicy tej konkretnej polityki kont SUK. Szczegóły i granice interpretacji są w rozdziale 17.

## Korekta po audycie źródeł - wersja 1.3
Ponownie odczytano lokalne źródła programu. Poradnik tom I s. 123-126 §1.5.8: EBILock; s. 126: dwa zestawy, rezerwa, kanały i pętle. S. 140: MOR-3 2z2. S. 147 rys. 46: serwery EBISCREEN oddzielne od KZ. Ie-20 s. 9 §6/s.12 § 7 ust. 12; Ie-104 s. 5 § 3 ust. 3/s. 6 § 4 ust. 5. Szczegóły dowodów i konfliktów w rozdziale 19.

KOMPUTERY.pdf to wyciąg tego samego poradnika (strona pliku 7=druk 126,21=140), nie niezależne potwierdzenie. EBILock_komputerownia.md rozróżnia pewność; wtórne czasy restartu/powrotu i szczegóły kart nie stają się automatycznie wymaganiami. Pełnej DTR nie dostarczono ani nie zweryfikowano. Nie deklarujemy zgodności z nieodczytanym źródłem.

Plików źródeł, ROZWOJ i PODZIAL_MODULOW nie zmieniano. ZRODLA_LOKALNE.json pozwala porównać hashe z bazą. Sprzeczności opisano jawnie poza Git.

## Dodatkowa kontrola aktualnego repozytorium
Bieżące porównanie: 2120ab0 → 5eddb5c, 25 commitów, 123 zmienione ścieżki. Zmiany obejmują już KZ, karty, zasilanie, system nadrzędny, PIP, Komputerownię i edytor UT. Nie obowiązuje wcześniejsza ocena, że delta dotyczy tylko siatki/kalkulatora. Aktualny rejestr ZRODLA_KONTROLA_1_7.json zachowuje hashe wejść, historię różnic, HEAD i początkowy stan roboczy; nie nadpisuje oryginalnych rejestrów.

## Źródła techniczne uzupełnienia 1.5
Rozdział 20 odsyła do oficjalnych dokumentacji .NET/C# 14/WPF/SignalR/EF/OpenIddict/PostgreSQL 18/Npgsql oraz narzędzi telemetrii, głosu i aktualizacji. Rozdział 23 odsyła do GNAT/GPRbuild/Alire, interfejsu Ada-C i zaleceń Microsoft dla LibraryImport/SafeHandle oraz RNG. Wersje główne są celem planu; konkretne poprawki i zgodność blokuje przyszły etap. Nie deklarujemy wykonania toolchainu ani buildu.


## Dostęp administracji 1.6 - weryfikacja 17.10.2026
- [ASP.NET Core - certyfikaty i proxy](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/certauth?view=aspnetcore-10.0), [schematy autoryzacji](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/authorize-with-a-specific-scheme?view=aspnetcore-10.0).
- [Windows CNG](https://learn.microsoft.com/en-us/windows/win32/seccertenroll/cng-key-storage-providers), [atestacja TPM](https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/manage/component-updates/tpm-key-attestation).
- [OpenIddict mTLS/token binding](https://documentation.openiddict.com/configuration/mutual-tls-authentication), [unieważnianie tokenów](https://documentation.openiddict.com/configuration/token-storage.html).
- [RFC 8705](https://www.rfc-editor.org/rfc/rfc8705.html), [RFC 8252](https://www.rfc-editor.org/rfc/rfc8252.html).
Źródła opisują możliwości narzędzi, nie bezpieczeństwo implementacji SUK. Dokładna wersja i TPM wymagają prototypu.

## Źródła aplikacji do porównania 1.7
Bieżące AGENTS/CLAUDE, ROZWOJ, README, PODZIAL_MODULOW, PLAN_EDYTOR_UT, nowy HISTORIA_EDYTOR_UT i zaakceptowany PLAN_EDYTOR_ZOBRAZOWANIA oraz źródła C#/WPF i sygnaly_ie1.json są objęte rejestrem porównania. Rejestr śledzi także nieśledzony PDF zastany w repo, aby potwierdzić jego zachowanie bez zmian.

Dokumenty repo wskazują Zrodla/Ie-1_obowiazuje_od_2026-01-17__uz_1041_25__WCAG.pdf, ale pliku nie znaleziono w bieżącym checkout. Potwierdzono istnienie kodu i tabeli Data/sygnaly_ie1.json, nie audyt brakującego PDF. Źródło normatywne trzeba pozyskać i sprawdzić przed zamrożeniem właściwych reguł w S02A1. Pozostałe wcześniejsze źródła EBILock zachowują opisany zakres pewności. Nie wyszukiwano ani nie pobierano nowej wersji źródeł z Internetu w tym porównaniu.


## Baza pracy i dostarczenie 1.8
Bieżący odczyt wykonano w C:\Users\JohnE\.codex\worktrees\3315\SUK na niezmienionym HEAD5eddb5c. Rejestr ZRODLA_KONTROLA_1_8.json obejmuje hashe141 źródeł i zastany stan roboczy. Analiza kodu1.7 pozostaje aktualna dla tego HEAD; nowe wymagania dotyczą planu pracy, promptów i logów, nie wykonanej implementacji.
Właściciel wskazał D:\Projects\SUK jako końcowy katalog. Kopia zastanego PDF trafiła do archiwum/v1.7/PDF_zastany_w_D_Projects_SUK.pdf przed zastąpieniem. Kanoniczne materiały i archiwum pozostają poza repo; dopuszczona kopia dokumentacji w repo nie oznacza git add/commit/push. Nie zmieniono śledzonych plików aplikacji ani station.json.
