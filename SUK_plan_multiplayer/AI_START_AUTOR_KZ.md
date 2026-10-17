# Neutralny start autora wariantu Ada A lub B
Plan1.8 | instrukcja organizacji przyszłej pracy, nie zamrożona specyfikacja.

## Warunek środowiska
Dotyczy wyłącznie S02A2/S02A3. Autor używa zatwierdzonego neutralnego pakietu
ReadyForAuthors, własnego kodu i raportów. Nie czyta pełnego AI_START,02,19,24,
źródłowego algorytmu C# ani kodu/patchy/historii drugiej implementacji.
Przyszły integrator przygotuje neutralne instrukcje projektu z regułami
właściciela. Ten plik nie uchyla nadrzędnych instrukcji AGENTS/użytkownika:
jeśli wymagają odczytu obcego algorytmu lub pełnego referencyjnego ROZWOJ,
autor zatrzymuje dany start i zgłasza konflikt zamiast ignorować nakaz
albo czytać zakazany materiał. Potrzebne jest zgodne środowisko autorskie.
Nazwa czatu/folderu nie jest izolacją dostępu; manifest i faktyczne
uprawnienia pakietu muszą odpowiadać zatwierdzonej polityce.

## Jedno zadanie
Odczytaj pakiety_czatow/ID.json, swoją kartę czaty/ID.md oraz właściwy prompt
prompty_kz/ID.md. Sprawdź hash zatwierdzonej specyfikacji/ABI/danych,
raporty zależności i wyłącznie własną historię. Status planned nie oznacza ready.
Nie otwieraj całego repo lub całego manifestu dla wygody.
Brak reguły lub źródła kieruj do autora specyfikacji. Obie linie otrzymują
identyczny neutralny pakiet; zmiana kontraktu wymaga wersji i oceny obu wariantów.

## Niezmienne funkcji
Każdy posterunek ma osobny logiczny KZ. G1 i G2 wykonują oba niezależne
programy A/B z czterema prywatnymi stanami. Jeden teren/ruch i jedno wyjście
przez bramkę hosta; autor wariantu nie implementuje drugiego kanału,
koordynatora grup ani ruchu. Losuje się grupę1/2 przy starcie po Ready,
nie wybiera głównego algorytmu A/B. Dane zależności projektuje człowiek.
Logika KZ w Ada; algorytm prowadzenia ruchu jest osobnym modułem bez LLM.
Każde Evaluate zwraca ograniczony DiagnosticBatch z25; bez pliku, sieci,
bazy, zegara ściennego i callbacku loggera w obliczeniach.

## Odbiór i przekazanie
Testuj własny wariant neutralnym harness/przypadkami. Pełną regresję aplikacji
wykonuje później integrator z dostępem do obu zamrożonych wariantów.
Raport zawiera ID, wejścia i hash specyfikacji, własne pliki, wyniki/braki,
logi i następny mały krok. Zamroź własny wariant przed porównaniem.
Nie kopiuj kodu drugiego autora, nie projektuj danych automatycznie,
nie wykonuj git add/commit/push lub wdrożenia bez osobnego zlecenia.
