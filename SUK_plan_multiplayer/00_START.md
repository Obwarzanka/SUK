# SUK - plan multiplayera
Symulator Urządzeń Komputerowych | plan1.8 | 17.10.2026

Status: plan, bez implementacji aplikacji.28 etapów nadrzędnych,90 zadań dla osobnych
czatów,17 gotowych promptów KZ,16 diagramów i23 projekty scenariuszy.37 wymagań.
Właściciel zlecił końcowy PDF i pakiet w D:\Projects\SUK; kanoniczne materiały
oraz archiwum są w D:\Projects\SUK - Dokumetacja nie gitowa. Bez operacji Git.

## Od czego zacząć
Człowiek: START.html lub PDF. AI wykonujące przyszły etap: AI_START.md,
manifest.json i jedna karta czaty/ID.md wraz z promptem prompty_czatow/ID.md.
[26 - rejestr zadań](26_PRACA_W_OSOBNYCH_CZATACH.md) wskazuje kolejność, wejścia
i raporty. Nie trzeba wczytywać całej rozmowy lub całego PDF.
[27 - gotowe prompty KZ](27_PROMPTY_KZ.md) prowadzi autorów neutralnej specyfikacji,
A/B i integratora. [25 - logi](25_LOGOWANIE_I_DIAGNOSTYKA.md) obowiązuje każdy moduł.
[28 - końcowy audyt](28_AUDYT_KONCOWY_1_8.md) opisuje poprawione braki.

## Decyzje właściciela
Windows,20–40 osób początkowo: Koluszki/Łódź Widzew/styczne, potem wzrost.
Każdy posterunek ma osobny KZ. Obie grupy1/2 wykonują niezależne programy
Ada A+B: cztery prywatne stany i jeden fizyczny świat. Grupę zasadniczą losujemy
per KZ przy starcie po Ready; wybór zapisujemy do replay.
LCS po oddaniu posterunku ma podgląd i traci sterowanie. Ruchowe AI jest
autorskim algorytmem danych/reguł, bez LLM, także offline na stycznych.
Pełny SUK.Admin jest osobnym WPF z kluczem generowanym lokalnie CNG/TPM,
kontem i bieżącymi prawami; serwer otrzymuje klucz publiczny i wydaje certyfikat.
Minimalna moderacja przed pilotem; pełny panel S18 i dyspozytor nadzoru S19 na końcu.

## Stan kodu i technologia
Baza5eddb5c pozostaje taka sama jak w1.7. Istniejące karty, KZ C#, PIP i fasadę
wykorzystujemy w S02A. Nadal jeden globalny KZ; Ada/grupy/per-post są planem.
20/23 podają stos: Ada KZ, C#/.NET10/WPF i gotowe usługi; dokładne wersje wymagają
zamrożenia.25 dodaje MEL, gotowy sink JSONL i ograniczoną diagnostykę Ada.
Dziennik pulpitu, diagnostyka, trwały audyt i replay mają różne gwarancje.
Brak wskazanego PDF Ie-1 oraz stare wejście serwisowe Komputerowni pozostają
jawnymi bramkami. Nie wykonywano buildu/testów SUK, nie zmieniono station.json.
Poprzedni plan1.7 oraz zastany PDF zachowano w archiwum/v1.7.

## Pliki do pracy
01–24 zachowują wymagania, architekturę, bezpieczeństwo, źródła i porównanie kodu.
25–28 obejmują logi, zadania, prompty i audyt. etapy/ to karty nadrzędne;
czaty/ to właściwy mały zakres wykonania; prompty_czatow/ i prompty_kz/ to
gotowe teksty. raporty/ID.md są przyszłymi wynikami, nie są tworzone jako
pozornie ukończone przy wydaniu planu. Źródłem treści są Markdown i manifest.
