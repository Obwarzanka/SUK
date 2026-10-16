@echo off
REM Edytor ukladu torowego (UT) - wymaga .NET 8 SDK
dotnet run --project "%~dp0app\Lcs.EdytorUT\Lcs.EdytorUT.csproj" -c Release -- %*
