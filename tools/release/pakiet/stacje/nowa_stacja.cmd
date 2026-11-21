@echo off
rem Nowa stacja SUK z pulpitu SimRail.
rem   1. W grze (z modem SimRailMost) wejdz na posterunek jako dyzurny - mod zapisze pulpit_<Posterunek>_*.json.
rem   2. nowa_stacja.cmd "<Posterunek>" [KOD]   np.  nowa_stacja.cmd Koluszki   albo   nowa_stacja.cmd "Lodz Widzew" LW
rem Wynik: stacje\<Posterunek>\ - zobrazowanie do edytora, pusty UT i pulpit SUK do pulpitu zdalnego SimRail.
rem Ponowne uruchomienie NIE nadpisuje zobrazowania poprawionego w edytorze - nowa wersja z gry trafia obok (*_z_gry_nowy.zob.json).
setlocal EnableExtensions
chcp 65001 >nul
if "%~1"=="" (
  echo Uzycie: nowa_stacja.cmd "Nazwa posterunku" [KOD]
  echo Dostepne zrzuty pulpitow:
  call "%~dp0ustawienia.cmd" && dir /b "%SUK_ZRZUTY%\pulpit_*.json" 2>nul
  goto koniec
)
call "%~dp0ustawienia.cmd" || goto koniec

set "NAZWA=%~1"
set "KOD=%~2"
rem Mod zamienia w nazwie pliku znaki inne niz litery i cyfry na "_".
set "MASKA=%NAZWA: =_%"
set "MASKA=%MASKA:-=_%"
set "MASKA=%MASKA:.=_%"
set "PLIK="
for /f "delims=" %%f in ('dir /b /o-d "%SUK_ZRZUTY%\pulpit_%MASKA%_*.json" 2^>nul') do if not defined PLIK set "PLIK=%%f"
if not defined PLIK (
  echo Brak zrzutu pulpitu "%NAZWA%" w %SUK_ZRZUTY%
  echo Wejdz w grze na ten posterunek jako dyzurny ^(mod SimRailMost musi byc w Mods^) i poczekaj kilka sekund.
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
echo Dalej:  edytuj_zobrazowanie.cmd "%NAZWA%"   potem   uruchom_pulpit.cmd "%NAZWA%"
echo Rysunek pochodzi z gry SimRail - tylko do wlasnego uzytku, nie rozpowszechniaj plikow stacji.
goto koniec

:blad
echo.
echo Konwersja nie powiodla sie - szczegoly powyzej.

:koniec
if not defined SUK_NO_PAUSE pause
