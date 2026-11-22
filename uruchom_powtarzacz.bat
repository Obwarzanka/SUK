@echo off
setlocal
set "SUK_PROJECT=Lcs.Powtarzacz"
set "SUK_EXE=Powtarzacz"
call "%~dp0tools\uruchom_program.cmd" %*
exit /b %errorlevel%
