@echo off
setlocal
set "SUK_PROJECT=Lcs.EdytorTaboru"
set "SUK_EXE=EdytorTaboru"
call "%~dp0tools\uruchom_program.cmd" %*
exit /b %errorlevel%
