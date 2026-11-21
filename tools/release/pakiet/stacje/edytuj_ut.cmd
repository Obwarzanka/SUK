@echo off
rem Otwiera uklad torowy stacji (stacje\<Posterunek>\edytor\<Posterunek>.ut.json) w Edytorze UT.
setlocal EnableExtensions
chcp 65001 >nul
if "%~1"=="" ( echo Uzycie: edytuj_ut.cmd "Nazwa posterunku" & pause & exit /b 1 )
call "%~dp0ustawienia.cmd" || ( pause & exit /b 1 )
set "M=%~1"
set "M=%M: =_%"
set "M=%M:-=_%"
set "M=%M:.=_%"
set "UT=%SUK_STACJE%\%M%\edytor\%M%.ut.json"
if not exist "%UT%" ( echo Brak %UT% - najpierw nowa_stacja.cmd "%~1" & pause & exit /b 1 )
start "" "%SUK_PROGRAMY%\EdytorUT\EdytorUT.exe" "%UT%"
