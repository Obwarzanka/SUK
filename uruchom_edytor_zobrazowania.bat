@echo off
setlocal
set "SUK_PROJECT=Lcs.EdytorZobrazowania"
set "SUK_EXE=EdytorZobrazowania"
call "%~dp0tools\uruchom_program.cmd" %*
exit /b %errorlevel%
