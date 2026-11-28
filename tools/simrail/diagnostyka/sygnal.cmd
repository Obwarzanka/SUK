@echo off
rem Sygnal zastepczy SimRail (przez mod SimRailMost), np. sygnal.cmd W63 SZ   albo   sygnal.cmd W63 OSZ
chcp 65001 >nul
py -I "%~dp0sygnal.py" %*
