@echo off
rem Otwiera zobrazowanie stacji (stacje\<Posterunek>\edytor\<Posterunek>.zob.json) w Edytorze zobrazowania SUK.
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
set "ZOB=%SUK_STACJE%\%M%\edytor\%M%.zob.json"
if not exist "%ZOB%" ( echo Brak %ZOB% - najpierw nowa_stacja.cmd "%M%" & pause & exit /b 1 )
start "" "%SUK_PROGRAMY%\EdytorZobrazowania2\EdytorZobrazowania2.exe" "%ZOB%"
