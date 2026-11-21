@echo off
rem Otwiera zobrazowanie stacji (stacje\<Posterunek>\edytor\<Posterunek>.zob.json) w Edytorze zobrazowania SUK.
setlocal EnableExtensions
chcp 65001 >nul
if "%~1"=="" ( echo Uzycie: edytuj_zobrazowanie.cmd "Nazwa posterunku" & pause & exit /b 1 )
call "%~dp0ustawienia.cmd" || ( pause & exit /b 1 )
set "M=%~1"
set "M=%M: =_%"
set "M=%M:-=_%"
set "M=%M:.=_%"
set "ZOB=%SUK_STACJE%\%M%\edytor\%M%.zob.json"
if not exist "%ZOB%" ( echo Brak %ZOB% - najpierw nowa_stacja.cmd "%~1" & pause & exit /b 1 )
start "" "%SUK_PROGRAMY%\EdytorZobrazowania2\EdytorZobrazowania2.exe" "%ZOB%"
