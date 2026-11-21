@echo off
setlocal
set "SUK_PROJECT=Lcs.Lacznosc"
set "SUK_EXE=Lacznosc"
call "%~dp0tools\uruchom_program.cmd" %*
exit /b %errorlevel%
