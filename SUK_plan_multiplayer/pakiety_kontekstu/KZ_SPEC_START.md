# Wspólny pakiet specyfikacji KZ - do przygotowania w S02A1
Status: SZABLON PRZYSZŁEJ PRACY, nie gotowa specyfikacja zależności.

Integrator przygotowuje neutralny opis zachowania na podstawie zatwierdzonych źródeł, danych i istniejących scenariuszy. Zawiera: zakres funkcji, typy/limity/wejścia/wyjścia, kolejność czasu i zatwierdzania, katalog błędów, zamknięte decyzje właściciela, wersję ABI oraz przypadki oczekiwane z ich podstawą. Nie projektuje sam tablic zależności ani nowych reguł.

Przed S02A2/S02A3 pakiet otrzymuje zatwierdzenie i hash. Autorzy A/B otrzymują identyczny pakiet. Specyfikacja nie jest kodem jednego wariantu i nie jest transkrypcją kroków C# przeznaczoną do kopiowania. Obecny silnik C# służy zespołowi specyfikacji/integracji jako odniesienie, z jawną obsługą znanych błędów i rozbieżności źródeł.

Niezbędne artefakty: specyfikacja PL, schemat C ABI, ręczne dane testowe, oczekiwane wyniki/zdarzenia z uzasadnieniem, limity i wzór raportu. Brak zatwierdzonego przypadku blokuje jego implementację zamiast pozostawienia AI swobody wymyślenia reguły.

Baza aktualizacji 1.7: referencja C# w Sim/Kz i Sim/Pulpit na 5eddb5c; istniejące karty i zasilanie, nie nowe implementacje. Pakiet zawiera zatwierdzoną macierz sygnałów i hash sygnaly_ie1.json, zależności sąsiednich sygnałów/blokad, fazy i jawne wyjątki momentu skutku. Brak PDF Ie-1 jest bramką weryfikacji źródła, nie dowodem zgodności tabeli. Autorzy A/B nadal nie czytają referencyjnego kodu ani kodu drugiego autora.
