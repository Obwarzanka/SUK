@echo off
setlocal
set "SUK_PROJECT=LcsWidzew"
set "SUK_EXE=LcsWidzew"
call "%~dp0tools\uruchom_program.cmd" %*
exit /b %errorlevel%
