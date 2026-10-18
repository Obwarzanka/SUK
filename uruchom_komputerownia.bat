@echo off
setlocal
set "SUK_PROJECT=Lcs.Komputerownia"
set "SUK_EXE=Komputerownia"
call "%~dp0tools\uruchom_program.cmd" %*
exit /b %errorlevel%
