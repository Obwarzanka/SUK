@echo off
setlocal
set "SUK_PROJECT=Lcs.Przechylka"
set "SUK_EXE=Przechylka"
call "%~dp0tools\uruchom_program.cmd" %*
exit /b %errorlevel%
