# 28. Końcowy przegląd planu1.8
Plan 1.8 | 17.10.2026 | audyt dokumentacji, bez implementacji.

## Zakres i wynik korekty
Sprawdzono 37 wymagań R01–R13/F01–F24.28 etapów nadrzędnych rozpisano na90 małych
zadań z osobnymi kartami i promptami oraz pełnym DAG.17 gotowych promptów KZ
obejmuje4 kroki specyfikacji, po5 kroków A/B i3 integracji. Podział nie jest
dowodem ukończenia żadnego etapu: wszystkie statusy pozostają planned.

## Znalezione i poprawione braki
- Wcześniejsze28 kart miało wiele podkroków na kartę; teraz jednostką wykonania jest
  jedno ID z manifest.jobs z jawnymi wejściami, dowodami i następcą.
- Szablony KZ nie były kompletem promptów dla małych zadań. Nowe pliki zawierają
  gotowy tekst startowy, izolację, bramkę źródeł/hash i konkretne wyjście.
- Nazwa czatu i inny folder nie izolują źródeł. Autor A/B wymaga czystego pakietu
  i zatwierdzonych instrukcji; sprzeczne AGENTS lub ujawnienie starego algorytmu
  blokują dany start zamiast prowadzić do potajemnego odczytu.
- Pamięciowy Rejestrator i bledy.log nie zapewniają trwałego audytu ani logów wszystkich
  modułów.25 określa gotowy stos, schemat, bounded diagnostic batch Ada i kolejki,
  redakcję, retencję oraz osobne gwarancje audytu i replay.
- Wczesny kontrakt nie wymaga gotowych przyszłych kanałów: sprawdza go stub/harness,
  a realne cztery konteksty są odbierane później.
- Sam status Applied w tekście diagnostycznym nie dowodzi trwałości. S06 ma osobną
  bramkę commit/checkpoint; awaria logowania nie resetuje utwierdzonych przebiegów.
- Ogólny zakaz kopii dokumentacji w repo wymagał wyjątku po odpowiedzi właściciela:
  końcowy katalog to D:\Projects\SUK. Zastany PDF zachowano przed zastąpieniem,
  bez git add/commit/push. Plik worktree C: pozostaje nietknięty.

## Niezmienniki i pozostałe bramki
Każdy posterunek ma własny KZ. Obie grupy1/2 wykonują niezależne Ada A+B,
cztery prywatne stany, jeden teren/ruch i jedno wyjście. Losuje się grupę1/2
przy starcie świata po Ready, nie algorytm A/B ani przy reconnect.
LCS po oddaniu ma podgląd bez sterowania. Ruchowe AI jest własnym algorytmem,
bez LLM. Admin ma osobny WPF i lokalny prywatny klucz CNG/TPM; serwer dostaje
publiczny. Pełny S18 i nadzorczy S19 są finałem. Minimum moderacji i ochrona
starego wejścia Komputerowni N42/O20 poprzedzają pilot.
Nie dopowiadamy reguł srk ani danych zależności. Brak wskazanego PDF Ie-1 nadal
jest bramką odpowiedniego zakresu specyfikacji. Macierz90 zadań i17 promptów
nie oznacza, że neutralna specyfikacja lub źródła Ada już istnieją.

## Kontrola artefaktów
Sprawdzane automatycznie: DAG etapów i zadań, pokrycie wszystkich28 etapów,
brak zależności A↔B, zgodność kart/promptów i hashy, kompletność odsyłaczy,
poprawność SVG i JSON, tekst/granice PDF oraz niezmienność źródeł.
Wizualne oględziny najnowszych PNG są osobnym krokiem. Wyniki zapisuje
RAPORT_WERYFIKACJI.json; same słowa tego rozdziału nie potwierdzają wykonania
przyszłych testów symulatora. Końcowa kopia ma porównane hashe z kanoniczną.


## Korekty po niezależnym przeglądzie
Usunięto konflikt pełnego AI_START z izolacją A/B: jest neutralny AI_START_AUTOR_KZ i jawny wyjątek ról. Dodano90 małych manifestów pakiety_czatow/ID.json, aby nie wymagać odczytu rejestru wszystkich zadań. Fundament logowania jest S01.01 przed nowymi modułami; nazwy indeksu sprawdzono po zmianie kolejności.
