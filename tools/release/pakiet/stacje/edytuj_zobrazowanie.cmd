@echo off
rem Otwiera zobrazowanie stacji (stacje\<stacja>\edytor\<stacja>.zob.json) w Edytorze zobrazowania SUK.
rem Przed otwarciem liczy stan przypiec do obiektow gry (zakladka Gra w edytorze).
rem To samo co: posterunek.cmd edytuj <stacja>
setlocal EnableExtensions
chcp 65001 >nul
call "%~dp0ustawienia.cmd" || ( pause & exit /b 1 )
%SUK_PY% -I "%SUK_NARZEDZIA%\posterunek.py" edytuj %1
if errorlevel 1 pause
