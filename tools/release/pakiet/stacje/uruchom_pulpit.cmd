@echo off
rem Pulpit SUK stacji z ZOBRAZOWANIA + ID obiektow SimRail - do pulpitu zdalnego SimRail, bez UT.
rem Przy kazdym starcie pulpit jest budowany od nowa z edytor\<stacja>.zob.json (zmiany z edytora widac od razu).
rem Pulpit zdalny: w SUK okno "SimRail" -> "Pulpit zdalny SimRail" (gra z modem SimRailMost musi dzialac).
rem To samo co: posterunek.cmd uruchom <stacja>
setlocal EnableExtensions
chcp 65001 >nul
call "%~dp0ustawienia.cmd" || ( pause & exit /b 1 )
%SUK_PY% -I "%SUK_NARZEDZIA%\posterunek.py" uruchom %*
if errorlevel 1 pause
