@echo off
rem Kopiuje mod SimRailMost.dll do katalogu Mods gry SimRail (wymaga zainstalowanego MelonLoadera 0.7.x).
setlocal EnableExtensions
chcp 65001 >nul
call "%~dp0stacje\ustawienia.cmd" || ( pause & exit /b 1 )
if not exist "%SIMRAIL_DIR%\MelonLoader\net6\MelonLoader.dll" (
  echo W %SIMRAIL_DIR% nie ma MelonLoadera.
  echo Zainstaluj MelonLoader 0.7.x ^(https://github.com/LavaGang/MelonLoader/releases^), uruchom gre raz i zamknij ja.
  pause & exit /b 1
)
tasklist /fi "imagename eq SimRail.exe" 2>nul | find /i "SimRail.exe" >nul && ( echo Zamknij gre przed instalacja moda. & pause & exit /b 1 )
if not exist "%SIMRAIL_DIR%\Mods" mkdir "%SIMRAIL_DIR%\Mods"
copy /y "%~dp0mod\SimRailMost.dll" "%SIMRAIL_DIR%\Mods\" >nul || ( echo Nie udalo sie skopiowac moda. & pause & exit /b 1 )
echo Mod SimRailMost zainstalowany w %SIMRAIL_DIR%\Mods
echo Dane moda (zrzuty pulpitow, logi): %SIMRAIL_DIR%\UserData\SimRailMost
pause
