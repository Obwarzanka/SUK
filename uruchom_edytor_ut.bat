@echo off
setlocal
set "SUK_PROJECT=Lcs.EdytorUT"
set "SUK_EXE=EdytorUT"
call "%~dp0tools\uruchom_program.cmd" %*
exit /b %errorlevel%
