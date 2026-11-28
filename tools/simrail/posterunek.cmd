@echo off
rem Posterunki SimRail w SUK (wersja z repozytorium): posterunek.cmd lista | nowy "<posterunek gry>" [KOD] | odswiez | buduj | uruchom | edytuj <uklad> [ut]
rem Opis polecen: tools\simrail\README.md. Katalog gry: zmienna SIMRAIL_DIR albo wykrycie ze Steam.
setlocal
chcp 65001 >nul
python -I "%~dp0posterunek.py" %*
set "KOD=%errorlevel%"
if "%~1"=="" pause
exit /b %KOD%
