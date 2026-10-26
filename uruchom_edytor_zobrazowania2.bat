@echo off
setlocal
set "SUK_PROJECT=Lcs.EdytorZobrazowania2"
set "SUK_EXE=EdytorZobrazowania2"
call "%~dp0tools\uruchom_program.cmd" %*
exit /b %errorlevel%
