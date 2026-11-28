@echo off
rem Nowa stacja SUK z pulpitu SimRail.
rem   1. W grze (z modem SimRailMost) wejdz na posterunek jako dyzurny - mod zapisze pulpit_<Posterunek>_*.json.
rem   2. nowa_stacja.cmd "<Posterunek>" [KOD]   np.  nowa_stacja.cmd Koluszki   albo   nowa_stacja.cmd "Lodz Widzew" LW
rem      (to samo co posterunek.cmd nowy ...; kod bez podania - z pulpitu gry, potem staly w stacje\posterunki.json)
rem Wynik: stacje\<Posterunek>\ - zobrazowanie do edytora, pusty UT i pulpit SUK do pulpitu zdalnego SimRail.
rem Ponowne uruchomienie NIE nadpisuje zobrazowania poprawionego w edytorze - nowa wersja z gry trafia obok (*_z_gry_nowy.zob.json).
setlocal EnableExtensions
chcp 65001 >nul
call "%~dp0ustawienia.cmd" || goto koniec

rem Bez zapisanego pulpitu - diagnoza (na ktorym etapie sie urwalo).
if not exist "%SUK_ZRZUTY%\pulpit_*.json" goto diagnoza
rem Rejestr stacji (posterunki.json), zobrazowanie do edytora, pusty UT i pulpit SUK - posterunek.py nowy.
%SUK_PY% -I "%SUK_NARZEDZIA%\posterunek.py" nowy %*
goto koniec

:diagnoza
rem Zaden pulpit nie zostal zapisany - pokaz, na ktorym etapie sie urwalo.
echo.
echo BRAK ZAPISANYCH PULPITOW.
echo Katalog gry:      %SIMRAIL_DIR%
echo Szukam zrzutow w: %SUK_ZRZUTY%
echo.
if not exist "%SIMRAIL_DIR%\Mods\SimRailMost.dll" (
  echo [X] Nie ma moda: %SIMRAIL_DIR%\Mods\SimRailMost.dll
  echo     Zrob Krok 5 ^(zainstaluj_mod.cmd^). Jesli gra jest w innym folderze, wpisz go w stacje\ustawienia.cmd.
  goto koniec
)
echo [OK] Mod jest w Mods.
if not exist "%SUK_ZRZUTY%" (
  echo [X] Mod ani razu sie nie uruchomil - brak folderu UserData\SimRailMost.
  echo     MelonLoader nie dziala ^(Krok 3^) albo gra startuje z innego folderu niz powyzszy.
  goto log
)
echo [OK] Mod dziala ^(folder UserData\SimRailMost istnieje^), ale nie zapisal pulpitu.
echo     Pulpit zapisuje sie tylko z KOMPUTEROWEGO pulpitu EBI ^(ekrany^).
echo     Nastawnie przekaznikowe Domino ^(np. Lazy La^) i mechaniczne nie maja pulpitu EBI - z nich stacji sie nie zrobi.
echo     Sprawdz tez: wejscie jako DYZURNY ^(nie maszynista^), pulpit widoczny na ekranie, odczekane 10 s.
:log
set "ML_LOG=%SIMRAIL_DIR%\MelonLoader\Latest.log"
if not exist "%ML_LOG%" (
  echo.
  echo [X] Brak logu MelonLoadera: %ML_LOG% - MelonLoader nie jest zainstalowany ^(Krok 3^).
  goto koniec
)
echo.
echo Ostatnie wpisy moda w MelonLoader\Latest.log ^(wyslij je autorowi^):
powershell -NoProfile -Command "$l = Select-String -LiteralPath $env:ML_LOG -Pattern 'SimRailMost' -Encoding UTF8 | Select-Object -Last 12; if ($l) { $l | ForEach-Object { '   ' + $_.Line } } else { '   (brak - mod nie zostal wczytany przez MelonLoadera)' }"
goto koniec

:koniec
if not defined SUK_NO_PAUSE pause
