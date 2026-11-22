# SUK {WERSJA} – symulator urządzeń komputerowych dla SimRail

Pulpit LCS, edytor zobrazowania, edytor układu torowego, moduł Łączność (EZP – czat dyżurnych)
i skrypty do budowania nowych stacji z pulpitów gry SimRail.

> **Pierwszy raz? Otwórz `INSTRUKCJA.md` – wszystko krok po kroku.** Poniżej skrót dla zaawansowanych.

## Wymagania

- Windows 10/11 x64
- [.NET Desktop Runtime 10.0](https://dotnet.microsoft.com/download/dotnet/10.0) (x64, co najmniej 10.0.13)
- [Python 3](https://www.python.org/downloads/) w PATH – tylko do skryptów stacji
- Do współpracy z grą: SimRail + [MelonLoader 0.7.x](https://github.com/LavaGang/MelonLoader/releases)

## Zawartość

| Katalog / plik | Co to jest |
|---|---|
| `programy\SUK\LcsWidzew.exe` | Pulpit LCS (symulator, pulpit zdalny SimRail, EDR) |
| `programy\EdytorZobrazowania2\EdytorZobrazowania2.exe` | Edytor zobrazowania (obrazy pulpitu wg Ie-104) |
| `programy\EdytorUT\EdytorUT.exe` | Edytor układu torowego (PUWG 1992, symbole Ig-10) |
| `programy\Lacznosc\Lacznosc.exe` | Łączność z posterunkami – EZP, czat dyżurnych SimRail, plan ruchu |
| `mod\SimRailMost.dll` | Mod MelonLoadera: stany z gry → SUK (UDP 47130), polecenia i czat (UDP 47131), zrzut pulpitu |
| `zainstaluj_mod.cmd` | Kopiuje mod do `Mods` gry |
| `stacje\` | Skrypty do budowania nowych stacji |

## Pierwsze uruchomienie

1. Zainstaluj MelonLoader w SimRail, uruchom grę raz i zamknij.
2. `zainstaluj_mod.cmd` – katalog gry jest wykrywany z bibliotek Steam
   (inny: ustaw zmienną `SIMRAIL_DIR` albo wpisz ją w `stacje\ustawienia.cmd`).
3. Uruchom grę i wejdź na posterunek jako dyżurny.

## Nowa stacja z pulpitu SimRail

Mod po wejściu na posterunek zapisuje rysunek pulpitu do
`<SimRail>\UserData\SimRailMost\pulpit_<Posterunek>_<ekran>_<data>.json`. Dalej:

```
stacje\nowa_stacja.cmd "Koluszki"            – zobrazowanie, pusty UT i pulpit SUK w stacje\Koluszki\
stacje\edytuj_zobrazowanie.cmd "Koluszki"    – poprawki obrazu w Edytorze zobrazowania
stacje\edytuj_ut.cmd "Koluszki"              – geometria torów w Edytorze UT (opcjonalnie), potem F8 „Przypnij do UT”
stacje\uruchom_pulpit.cmd "Koluszki"         – pulpit SUK z zobrazowania; w SUK: okno „SimRail” → „Pulpit zdalny SimRail”
```

`nowa_stacja.cmd` bez argumentu pokazuje listę zrzutów. Opcjonalny drugi argument to kod posterunku (np. `KO`).
Ponowne uruchomienie nie nadpisuje zobrazowania poprawionego w edytorze – nowa wersja z gry trafia obok jako
`<Posterunek>_z_gry_nowy.zob.json`. Zależności i tablicę przebiegów projektuje człowiek – skrypty ich nie generują.

**Rysunki stacji pochodzą z gry SimRail – tylko do własnego użytku, nie rozpowszechniaj plików ze `stacje\<Posterunek>\`.**

## Łączność (EZP)

`programy\Lacznosc\Lacznosc.exe` przy działającej grze z modem: lista posterunków sąsiednich, rozmowa,
EZP – opcje tekstowe z szablonów gry, podpowiedzi odpowiedzi i plan ruchu z publicznego API SimRail.
Wygląd bez gry: `Lacznosc.exe --demo` (dane przykładowe).

## Uwagi

- Wersja rozwojowa – możliwe błędy. Błędy programów trafiają do `bledy.log` obok exe.
- Projekty edytorów spoza `stacje\` zapisują się domyślnie w Dokumentach.
- Pełna dokumentacja obsługi: `README.md` w repozytorium.
