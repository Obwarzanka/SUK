@echo off
REM Uruchomienie symulatora LCS Lodz Widzew (wymaga .NET 8 SDK)
dotnet run --project "%~dp0app\LcsWidzew\LcsWidzew.csproj" -c Release
