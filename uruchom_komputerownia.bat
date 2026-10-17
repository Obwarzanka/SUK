@echo off
REM Komputerownia LCS - podglad kart sterownikow obiektowych (laczy sie z dzialajacym symulatorem) - wymaga .NET 8 SDK
dotnet run --project "%~dp0app\Lcs.Komputerownia\Lcs.Komputerownia.csproj" -c Release -- %*
