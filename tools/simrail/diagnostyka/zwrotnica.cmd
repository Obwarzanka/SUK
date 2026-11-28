@echo off
rem Kontroler zwrotnic SimRail (przez mod SimRailMost).
rem Bez argumentow - okno; z argumentami - wersja konsolowa, np. zwrotnica.cmd 71ab +   albo   zwrotnica.cmd -s La 12 -
if "%~1"=="" (
  start "" pyw -I "%~dp0zwrotnica_okno.py"
  exit /b
)
chcp 65001 >nul
py -I "%~dp0zwrotnica.py" %*
