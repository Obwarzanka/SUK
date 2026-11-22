@echo off
rem Nowa stacja SUK z pulpitu SimRail.
rem   1. W grze (z modem SimRailMost) wejdz na posterunek jako dyzurny - mod zapisze pulpit_<Posterunek>_*.json.
rem   2. nowa_stacja.cmd "<Posterunek>" [KOD]   np.  nowa_stacja.cmd Koluszki   albo   nowa_stacja.cmd "Lodz Widzew" LW
rem Wynik: stacje\<Posterunek>\ - zobrazowanie do edytora, pusty UT i pulpit SUK do pulpitu zdalnego SimRail.
rem Ponowne uruchomienie NIE nadpisuje zobrazowania poprawionego w edytorze - nowa wersja z gry trafia obok (*_z_gry_nowy.zob.json).
setlocal EnableExtensions
chcp 65001 >nul
call "%~dp0ustawienia.cmd" || goto koniec

set "NAZWA=%~1"
set "KOD=%~2"
if defined NAZWA goto mam_nazwe
rem Dwuklik bez argumentu: lista stacji z zapisanych pulpitow i pytanie o nazwe.
if not exist "%SUK_ZRZUTY%\pulpit_*.json" goto diagnoza
echo Stacje, ktorych pulpit zapisal mod (wejdz w grze na posterunek jako dyzurny, zeby dodac nowa):
powershell -NoProfile -Command "Get-ChildItem -LiteralPath $env:SUK_ZRZUTY -Filter 'pulpit_*.json' | ForEach-Object { if ($_.Name -match '^pulpit_(.+)_[^_]+_\d{8}_\d{6}\.json$') { '   ' + $Matches[1] } } | Sort-Object -Unique"
echo.
set /p "NAZWA=Wpisz nazwe stacji z listy (np. Koluszki) i nacisnij Enter: "
if not defined NAZWA goto koniec
:mam_nazwe
rem Mod zamienia w nazwie pliku znaki inne niz litery i cyfry na "_".
set "MASKA=%NAZWA: =_%"
set "MASKA=%MASKA:-=_%"
set "MASKA=%MASKA:.=_%"
set "PLIK="
for /f "delims=" %%f in ('dir /b /o-d "%SUK_ZRZUTY%\pulpit_%MASKA%_*.json" 2^>nul') do if not defined PLIK set "PLIK=%%f"
if not defined PLIK (
  echo Brak zrzutu pulpitu "%NAZWA%" w %SUK_ZRZUTY%
  if not exist "%SUK_ZRZUTY%\pulpit_*.json" goto diagnoza
  echo Nazwa musi byc taka jak na liscie - uruchom nowa_stacja.cmd bez argumentu ^(dwuklik^).
  goto koniec
)
echo Zrzut pulpitu: %PLIK%

set "ST=%SUK_STACJE%\%MASKA%"
set "ED=%ST%\edytor"
if not exist "%ED%" mkdir "%ED%"

rem Projekt ukladu SUK z siatki pulpitu EBI (propozycja do poprawienia, bez szlakow i zaleznosci).
%SUK_PY% -I "%SUK_NARZEDZIA%\pulpit_do_suk.py" "%SUK_ZRZUTY%\%PLIK%" "%ST%\%MASKA%_simrail.json" %KOD% || goto blad

rem Pusty UT - geometrie torow (PUWG 1992) buduje sie w Edytorze UT.
if not exist "%ED%\%MASKA%.ut.json" %SUK_PY% -I "%SUK_NARZEDZIA%\pusty_ut.py" "%NAZWA%" "%ED%\%MASKA%.ut.json" || goto blad

rem Zobrazowanie do Edytora zobrazowania.
if exist "%ED%\%MASKA%.zob.json" (
  %SUK_PY% -I "%SUK_NARZEDZIA%\pulpit_do_zob.py" "%SUK_ZRZUTY%\%PLIK%" "%ED%\%MASKA%_z_gry_nowy.zob.json" "%MASKA%.ut.json" || goto blad
  echo Zobrazowanie juz istnieje - nowa wersja z gry zapisana obok: %MASKA%_z_gry_nowy.zob.json
) else (
  %SUK_PY% -I "%SUK_NARZEDZIA%\pulpit_do_zob.py" "%SUK_ZRZUTY%\%PLIK%" "%ED%\%MASKA%.zob.json" "%MASKA%.ut.json" || goto blad
)

rem Pulpit SUK z zobrazowania (bez UT) - to samo robi uruchom_pulpit.cmd przy kazdym starcie.
%SUK_PY% -I "%SUK_NARZEDZIA%\zob_do_suk.py" "%ED%\%MASKA%.zob.json" "%ST%\%MASKA%_zob.json" || goto blad

echo.
echo Gotowe: %ST%
echo Dalej: dwuklik edytuj_zobrazowanie.cmd albo od razu uruchom_pulpit.cmd ^(nazwa stacji: %MASKA%^)
echo Rysunek pochodzi z gry SimRail - tylko do wlasnego uzytku, nie rozpowszechniaj plikow stacji.
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

:blad
echo.
echo Konwersja nie powiodla sie - szczegoly powyzej.

:koniec
if not defined SUK_NO_PAUSE pause
