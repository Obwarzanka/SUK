@echo off
rem Stacje SUK z pulpitow SimRail - jedno polecenie (opis: stacje\CZYTAJ_STACJE.md):
rem   posterunek.cmd lista                         stacje i ich stan
rem   posterunek.cmd nowy "<posterunek>" [KOD]     nowa stacja z zapisanego pulpitu gry
rem   posterunek.cmd odswiez <stacja>              nowy rysunek z gry obok (Twoje poprawki zostaja) + lista zmian
rem   posterunek.cmd uruchom <stacja>              pulpit SUK do pulpitu zdalnego SimRail
rem   posterunek.cmd edytuj <stacja> [ut]          Edytor zobrazowania (albo Edytor UT)
setlocal EnableExtensions
chcp 65001 >nul
call "%~dp0ustawienia.cmd" || ( pause & exit /b 1 )
%SUK_PY% -I "%SUK_NARZEDZIA%\posterunek.py" %*
set "KOD=%errorlevel%"
if "%~1"=="" if not defined SUK_NO_PAUSE pause
exit /b %KOD%
