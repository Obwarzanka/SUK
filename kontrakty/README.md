# Kontrakty SUK

Neutralne, wersjonowane artefakty maszynowe wspólne dla C#, Ada i testów. Utworzone w zadaniu S00.03 (18.10.2026) jako wstępny katalog; raport: `SUK_plan_multiplayer/raporty/S00.03.md`.

## Dlaczego osobny katalog
- Poza `app/`: autor Kz_A/Kz_B nie może czytać starego algorytmu C# z `app/`, a musi znać wspólne kontrakty (25 §5, 27). Ten katalog nie zawiera kodu algorytmów, więc może wejść do neutralnego pakietu S02A1.04.
- Poza `SUK_plan_multiplayer/`: plan opisuje i uzasadnia, a pliki tutaj są wejściem programów i testów.
- Aktywne pliki budowania .NET leżą w `app/` (katalog rozwiązania `Lcs.slnx`, kopiowany przez `build_win.sh`): `app/global.json`, `app/Directory.Packages.props`, `app/nuget.config`. Ich opis i sumy są w `stos/wersje_stosu.json` i `katalog_kontraktow.json`.

## Zawartość
| Plik | Kontrakt | Status |
|---|---|---|
| `katalog_kontraktow.json` | rejestr wszystkich kontraktów planu: właściciel (zadanie), konsumenci, rozdziały, pliki i sumy | wstępny |
| `logi/rekord_logu_v1.schema.json` | schemat rekordu JSONL (JSON Schema Draft-07) | wstępny |
| `logi/katalog_zdarzen_v1.json` | zakresy eventId 1000–9999 i prefiksy eventCode | wstępny |
| `logi/budzety_logow_v1.json` | budżety logowania do pomiaru (25 §5–§7) | wstępny |
| `stos/wersje_stosu.json` | zamrożone wersje .NET/NuGet/PostgreSQL, kandydaci, licencje | zamrożony |
| `stos/toolchain_ada.json` | Alire/GNAT/gprbuild z sumami archiwów | kandydat (S02A1.02) |
| `profile/profil_pomiaru_v1.json` | host i czasy odniesienia z S00.02, stan D09 | wstępny |
| `profile/profil_wdrozenia_v1.json` | stan net8 i docelowe wdrożenie (self-contained klient) | wstępny |
| `testy/` | narzędzie sprawdzające i rekordy poprawne/błędne | – |

## Zasady
- Bez sekretów, kluczy, haseł, tokenów i danych osób. Bez kodu zależności, zobrazowań i reguł srk.
- Zmiana znaczenia pola = nowa wersja główna (nowy plik `*_v2`); nowe pole opcjonalne = zmiana zgodna.
- Po każdej zmianie zaktualizuj `sha256_lf` w `katalog_kontraktow.json` (suma po zamianie CRLF→LF) i uruchom sprawdzenie.
- Pozycja `planowany` w katalogu niczego nie zalicza; istnienie potwierdza plik z sumą i raport zadania.
- Końce linii LF wymusza `.gitattributes` w tym katalogu.

## Sprawdzenie
```
python3 kontrakty/testy/sprawdz_kontrakty.py --wynik wynik.txt
```
Wymaga Pythona ≥ 3.8 i pakietu `jsonschema` ≥ 3.2. Narzędzie tylko czyta pliki repo (bez sieci). Kod wyjścia 0 = `błędów: 0`.
