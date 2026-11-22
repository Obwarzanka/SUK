@echo off
rem Pulpit SUK stacji z ZOBRAZOWANIA + ID obiektow SimRail - do pulpitu zdalnego SimRail, bez UT.
rem Przy kazdym starcie pulpit jest budowany od nowa z edytor\<Posterunek>.zob.json (zmiany z edytora widac od razu).
rem Pulpit zdalny: w SUK okno "SimRail" -> "Pulpit zdalny SimRail" (gra z modem SimRailMost musi dzialac).
setlocal EnableExtensions
chcp 65001 >nul
call "%~dp0ustawienia.cmd" || ( pause & exit /b 1 )
set "M=%~1"
if not defined M (
  echo Twoje stacje:
  for /d %%d in ("%SUK_STACJE%\*") do if exist "%%d\edytor" echo    %%~nxd
  echo.
  set /p "M=Wpisz nazwe stacji z listy i nacisnij Enter: "
)
if not defined M exit /b 1
set "M=%M: =_%"
set "M=%M:-=_%"
set "M=%M:.=_%"
set "ST=%SUK_STACJE%\%M%"
if not exist "%ST%\edytor\%M%.zob.json" ( echo Brak zobrazowania - najpierw nowa_stacja.cmd "%M%" & pause & exit /b 1 )
%SUK_PY% -I "%SUK_NARZEDZIA%\zob_do_suk.py" "%ST%\edytor\%M%.zob.json" "%ST%\%M%_zob.json"
if errorlevel 1 ( echo Blad budowania pulpitu z zobrazowania & pause & exit /b 1 )
start "" "%SUK_PROGRAMY%\SUK\LcsWidzew.exe" --station "%ST%\%M%_zob.json"
