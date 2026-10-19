@echo off
setlocal EnableExtensions DisableDelayedExpansion
rem Wspolny launcher. SDK wybieramy z app/global.json, niezaleznie od katalogu wywolania.
set "SUK_DOTNET=%~dp0..\.tools\dotnet\dotnet.exe"
if not exist "%SUK_DOTNET%" set "SUK_DOTNET=dotnet"
if exist "%~dp0..\.tools\dotnet\dotnet.exe" (
  set "DOTNET_CLI_HOME=%~dp0..\.tools\cli-home"
  if not defined NUGET_PACKAGES set "NUGET_PACKAGES=%~dp0..\.tools\nuget\packages"
)
set "DOTNET_CLI_TELEMETRY_OPTOUT=1"
set "DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0"
set "DOTNET_NOLOGO=1"
pushd "%~dp0..\app"
if errorlevel 1 exit /b 1

"%SUK_DOTNET%" --version >nul 2>&1
if errorlevel 1 goto sdk_missing
echo Budowanie aktualnej wersji %SUK_EXE%...
"%SUK_DOTNET%" build "%SUK_PROJECT%\%SUK_PROJECT%.csproj" -c Release --nologo
set "SUK_RESULT=%errorlevel%"
if not "%SUK_RESULT%"=="0" goto build_failed

rem Ten sam dotnet buduje i uruchamia DLL, takze z lokalnym runtime bez instalacji systemowej.
start "" /wait "%SUK_DOTNET%" "%CD%\%SUK_PROJECT%\bin\Release\net10.0-windows\%SUK_EXE%.dll" %*
set "SUK_RESULT=%errorlevel%"
if not "%SUK_RESULT%"=="0" goto run_failed
popd
exit /b 0

:sdk_missing
echo.
echo Nie znaleziono SDK wymaganego przez app\global.json.
echo Zainstaluj dokladnie .NET SDK 10.0.402 wskazane przez app\global.json.
echo https://dotnet.microsoft.com/download/dotnet/10.0
echo Obslugiwana jest tez lokalna kopia SDK w .tools\dotnet.
set "SUK_RESULT=1"
goto failed

:build_failed
echo.
echo Nie udalo sie zbudowac programu. Szczegoly bledu sa powyzej.
echo Jezeli program jest otwarty, zamknij go przed ponownym budowaniem.
goto failed

:run_failed
echo.
echo Program zakonczyl sie bledem. Kod: %SUK_RESULT%.

:failed
popd
rem Testy automatyczne moga wylaczyc pauze; przy dwukliku blad pozostaje widoczny.
if not defined SUK_NO_PAUSE pause
exit /b %SUK_RESULT%
