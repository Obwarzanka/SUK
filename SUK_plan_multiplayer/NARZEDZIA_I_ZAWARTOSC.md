# Narzędzia i pliki pakietu
Pliki 00-28, AUDYT_PROMPTOW, AI_START, SZABLON_PRZEKAZANIA, manifest oraz 28 kart etapy/ to materiały docelowe. START.html i PDF zawierają wspólną treść merytoryczną. grafiki/ zawiera 16 diagramów w SVG/PNG i alternatywny zapis logiki w Mermaid. SVG jest źródłem wyglądu; Mermaid nie jest automatycznie renderowany do identycznej grafiki.

**Źródłem aktualnej treści są Markdown i manifest.json.** opracuj_dokumentacje.py uruchamia jedynie renderuj_html.cjs i renderuj_pdf.py; nie odtwarza już starej treści z literalnych ciągów. Dawny generator i wersja 1.2 są w archiwum/v1.2. Skrypty _korekta_tresci_1_3.py, _grupy_i_etapy_1_3.py i _grafiki_i_korekta_1_3.py są historycznym zapisem migracji, nie bieżącym poleceniem regeneracji i nie należy ich ponawiać nad ręcznie poprawionym planem.

HTML czyta dokumenty wskazane w manifest, odtwarza _tresc.json/_ast.json, zamienia SVG na PNG przez sharp i wykonuje kontrolę w Chrome. PDF składa tokeny przez ReportLab. sprawdz_dokumentacje.py sprawdza DAG, odwołania, tekst/granice stron i hashe źródeł oraz renderuje PDF do podglad/. Wizualne sprawdzenie najnowszych obrazów jest osobnym krokiem.

Zależności pochodzą z lokalnego runtime Codex: marked/sharp/playwright, Python reportlab/pypdf/pdfplumber/Pillow, Poppler i Arial Windows. Ścieżki narzędzi są lokalne. RAPORT_WERYFIKACJI.json opisuje kontrolę dokumentacji, nie odbiór symulatora. ZRODLA_LOKALNE.json utrzymuje punkt odniesienia; nie odświeżać hashy, aby ukryć zmianę źródła.

W tej pracy nie uruchamiano buildu/testów SUK ani wdrożenia. Narzędzia nie implementują funkcji SUK. Właściciel wyraźnie zlecił końcową kopię dokumentacji w D:\Projects\SUK bez operacji Git.


Korekta 1.4 wprowadza F17: autorski algorytm ruchu bez LLM, N35/O15 i 26 wymagań w macierzy. Aktualne źródło treści nadal stanowią Markdown oraz manifest; materiały 1.3 zachowano w archiwum/v1.3.


Wersja 1.5 obejmuje 32 wymagania, 28 kart, 12 diagramów i 18 scenariuszy. pakiety_kontekstu/ zawiera szablon wspólnej specyfikacji oraz oddzielne starty A/B. Poprzednia wersja 1.4 zachowana w archiwum/v1.4. Język KZ=Ada; dwa osobne czaty A/B; losowanie roli przy starcie; końcowe S18/S19.


Historyczna wersja 1.6: 33 wymagania, 28 etapów, 13 diagramów i 19 scenariuszy. Wersja 1.5 zachowana w archiwum/v1.5. Rozdział 21 obejmuje osobny WPF i indywidualne klucze; diagram 13 rozdziela enrollment od sesji. Historycznych skryptów _aktualizacja/_etapy/_grafiki 1.5 i _zamknij_1_5.py nie uruchamiać nad nowym planem.

Wersja 1.6.1 utrwala zgodę właściciela na lokalne generowanie prywatnego klucza administratora, preferencyjnie niewyeksportowalnego CNG/TPM. Serwer otrzymuje klucz publiczny i wydaje certyfikat. Liczby wymagań/etapów/diagramów/scenariuszy pozostają bez zmian. Stan 1.6 zachowano w archiwum/v1.6. Wersję 1.6.1 zamykał _zamknij_1_6_1.py; jest to obecnie skrypt historyczny.

Wersja 1.7 porównuje aplikację 2120ab0→5eddb5c. Bieżące: 59 dokumentów treści, 33 wymagania, 28 kart, 14 diagramów i 20 scenariuszy. Dodano rozdział 24, diagram 14, N42/O20 i ZRODLA_KONTROLA_1_7.json. Dawne rejestry zachowano. Narzędzie kontroli korzysta z aktywnego review_registry manifestu, obsługuje przeniesione/usunięte źródła i porównuje zastany stan roboczy/untracked PDF zamiast żądać jego usunięcia. Wersja 1.6.1 w archiwum/v1.6.1; finalizator _zamknij_1_7.py, historycznych migracji nie ponawiać.


## Aktualna wersja1.8
28 etapów,90 zadań i17 gotowych promptów KZ.16 diagramów i23 projekty scenariuszy.37 wymagań. Kanoniczny pakiet poza repo, kopia końcowa na zlecenie właściciela w D:\Projects\SUK. Skrypty _zadania_i_prompty_1_8.py i starsze migracje są historyczne; nie ponawiaj ich nad poprawkami. Bieżące eksporty tworzy opracuj_dokumentacje.py z manifestu/Markdown. Stan1.7 zachowano w archiwum/v1.7.
