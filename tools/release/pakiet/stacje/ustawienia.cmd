@echo off
rem Wspolne ustawienia skryptow stacji. Wywolywany przez pozostale skrypty (call ustawienia.cmd).
rem Katalog gry: zmienna SIMRAIL_DIR albo wykrycie z bibliotek Steam. Mozesz tez wpisac sciezke ponizej na stale:
rem set "SIMRAIL_DIR=D:\SteamLibrary\steamapps\common\SimRail"

set "SUK_REL=%~dp0.."
for %%i in ("%SUK_REL%") do set "SUK_REL=%%~fi"
set "SUK_PROGRAMY=%SUK_REL%\programy"
set "SUK_NARZEDZIA=%~dp0narzedzia"
set "SUK_STACJE=%~dp0"
if "%SUK_STACJE:~-1%"=="\" set "SUK_STACJE=%SUK_STACJE:~0,-1%"

if not defined SIMRAIL_DIR (
  for /f "usebackq delims=" %%d in (`powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0narzedzia\znajdz_simrail.ps1"`) do set "SIMRAIL_DIR=%%d"
)
if not defined SIMRAIL_DIR (
  echo Nie znaleziono katalogu SimRail. Ustaw zmienna SIMRAIL_DIR albo wpisz ja w stacje\ustawienia.cmd.
  exit /b 1
)
set "SUK_ZRZUTY=%SIMRAIL_DIR%\UserData\SimRailMost"

set "SUK_PY=python"
where python >nul 2>&1 || set "SUK_PY=py -3"
%SUK_PY% --version >nul 2>&1 || (
  echo Brak Pythona 3. Zainstaluj go z https://www.python.org/downloads/ ^(zaznacz "Add python.exe to PATH"^).
  exit /b 1
)
exit /b 0
