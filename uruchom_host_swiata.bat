@echo off
setlocal
set "SUK_PROJECT=Lcs.WorldHost"
set "SUK_EXE=Lcs.WorldHost"
set "SUK_TARGET=net10.0"
call "%~dp0tools\uruchom_program.cmd" %*
exit /b %errorlevel%
