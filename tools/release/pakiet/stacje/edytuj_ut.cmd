@echo off
rem Otwiera uklad torowy stacji (stacje\<stacja>\edytor\<stacja>.ut.json) w Edytorze UT.
rem To samo co: posterunek.cmd edytuj <stacja> ut
setlocal EnableExtensions
chcp 65001 >nul
call "%~dp0ustawienia.cmd" || ( pause & exit /b 1 )
if "%~1"=="" ( %SUK_PY% -I "%SUK_NARZEDZIA%\posterunek.py" edytuj "" ut ) else ( %SUK_PY% -I "%SUK_NARZEDZIA%\posterunek.py" edytuj %1 ut )
if errorlevel 1 pause
