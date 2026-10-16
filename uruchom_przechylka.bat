@echo off
REM Kalkulator przechylki i krzywej przejsciowej - wymaga .NET 8 SDK
dotnet run --project "%~dp0app\Lcs.Przechylka\Lcs.Przechylka.csproj" -c Release -- %*
