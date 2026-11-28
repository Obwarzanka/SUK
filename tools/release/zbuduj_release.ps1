<#
.SYNOPSIS
  Buduje paczkę wydania SUK: pulpit, edytor zobrazowania, edytor UT, Łączność (EZP), mod SimRailMost i skrypty stacji.
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools\release\zbuduj_release.ps1 -Wersja 0.1.0
  Wynik: artifacts\release\SUK-0.1.0-win-x64.zip
.NOTES
  Programy: framework-dependent (wymagają .NET Desktop Runtime 10). Budowanie idzie do osobnego --artifacts-path,
  więc nie koliduje z otwartym programem w app\*\bin\Release.
  Mod jest brany z repozytorium SimRailMost (-MostDir; trzeba go wcześniej zbudować: dotnet build -c Release), konwertery stacji
  z tools\simrail. Gotowe stacje (-Stacje, domyślnie Koluszki) z -StacjeDir (układ jak po posterunek nowy, z posterunki.json);
  surowe zrzuty pulpitów z gry nie trafiają.
#>
param(
    [Parameter(Mandatory)][string]$Wersja,
    [string]$MostDir = (Join-Path $PSScriptRoot '..\..\..\SimRailMost'),
    [string[]]$Stacje = @('Koluszki'),
    [string]$StacjeDir = (Join-Path ($(if ($env:SIMRAIL_DIR) { $env:SIMRAIL_DIR } else { 'D:\SteamLibrary\steamapps\common\SimRail' })) 'UserData\SimRailMost\suk\stacje'),
    [switch]$BezZip
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$app = Join-Path $repo 'app'
$wyj = Join-Path $repo 'artifacts\release'
$nazwa = "SUK-$Wersja-win-x64"
$cel = Join-Path $wyj $nazwa
$budowa = Join-Path $wyj '_budowa'
$MostDir = (Resolve-Path $MostDir).Path

$dotnet = Join-Path $repo '.tools\dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) { $dotnet = 'dotnet' }
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'; $env:DOTNET_NOLOGO = '1'

if (Test-Path $cel) { Remove-Item $cel -Recurse -Force }
New-Item -ItemType Directory -Force $cel | Out-Null

# 1. Programy
$programy = [ordered]@{
    'SUK'                 = 'LcsWidzew\LcsWidzew.csproj'
    'EdytorZobrazowania2' = 'Lcs.EdytorZobrazowania2\Lcs.EdytorZobrazowania2.csproj'
    'EdytorUT'            = 'Lcs.EdytorUT\Lcs.EdytorUT.csproj'
    'Lacznosc'            = 'Lcs.Lacznosc\Lcs.Lacznosc.csproj'
}
Push-Location $app
try {
    foreach ($p in $programy.GetEnumerator()) {
        Write-Host "== $($p.Key)" -ForegroundColor Cyan
        & $dotnet publish $p.Value -c Release -r win-x64 --self-contained false `
            --artifacts-path $budowa -o (Join-Path $cel "programy\$($p.Key)") `
            -p:Version=$Wersja -p:DebugType=none -p:GenerateDocumentationFile=false --nologo
        if ($LASTEXITCODE -ne 0) { throw "Budowanie $($p.Key) nie powiodło się" }
    }
} finally { Pop-Location }

# Pliki robocze, które nie powinny trafić do paczki
Get-ChildItem (Join-Path $cel 'programy') -Recurse -Include 'bledy.log', 'ostatni_uklad.txt', 'selftest.txt*', '*.bak' |
    Remove-Item -Force
foreach ($k in 'Testy', 'TestResults') {
    Get-ChildItem (Join-Path $cel 'programy') -Recurse -Directory -Filter $k | Remove-Item -Recurse -Force
}

# 2. Mod SimRailMost
$dll = Join-Path $MostDir 'bin\Release\SimRailMost.dll'
if (-not (Test-Path $dll)) { throw "Brak $dll – zbuduj mod: dotnet build -c Release w $MostDir" }
$zrodlaMod = Get-ChildItem $MostDir -Filter *.cs | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($zrodlaMod.LastWriteTime -gt (Get-Item $dll).LastWriteTime) {
    Write-Warning "SimRailMost.dll jest starszy niż $($zrodlaMod.Name) – przebuduj mod, jeśli zmiany mają trafić do wydania."
}
# Wersja z MelonInfo zapisanego w DLL (źródła mogą być już nowsze niż zbudowany mod)
$bajty = [Text.Encoding]::Latin1.GetString([IO.File]::ReadAllBytes($dll))
$wersjaMod = [regex]::Match($bajty, 'SimRailMost.(\d+\.\d+\.\d+)').Groups[1].Value
New-Item -ItemType Directory -Force (Join-Path $cel 'mod') | Out-Null
Copy-Item $dll (Join-Path $cel 'mod')

# 3. Skrypty stacji: szablon z repozytorium + konwertery z tools\simrail (jedno źródło – to samo, czego używa posterunek.cmd w repo)
$pakiet = Join-Path $PSScriptRoot 'pakiet'
Copy-Item (Join-Path $pakiet '*') $cel -Recurse -Force
$simrail = Join-Path $repo 'tools\simrail'
foreach ($py in 'posterunek.py', 'pulpit_do_suk.py', 'pulpit_do_zob.py', 'zob_do_suk.py', 'pulpit_png.py', 'pusty_ut.py') {
    Copy-Item (Join-Path $simrail $py) (Join-Path $cel 'stacje\narzedzia')
}
# 3b. Gotowe stacje (dopracowane zobrazowanie + mapa ID gry + katalog przebiegów) w układzie stacje\<układ>\ jak po posterunek nowy,
#     z wpisami w stacje\posterunki.json. Źródło: <SimRail>\UserData\SimRailMost\suk\stacje (-StacjeDir). uruchom_pulpit.cmd przebudowuje
#     <układ>_zob.json z edytor\<układ>.zob.json, a mapę posterunków i przebiegi gry bierze z plików obok (zob_do_suk.py ich nie kasuje).
$rejZr = Join-Path $StacjeDir 'posterunki.json'
if (-not (Test-Path $rejZr)) { throw "Brak rejestru posterunków $rejZr (posterunek.cmd lista)" }
$rej = Get-Content $rejZr -Raw -Encoding UTF8 | ConvertFrom-Json
$rejPaczka = [ordered]@{ opis = $rej.opis; posterunki = @() }
foreach ($st in $Stacje) {
    $kat = Join-Path $cel "stacje\$st"; $ed = Join-Path $kat 'edytor'
    New-Item -ItemType Directory -Force $ed | Out-Null
    $pliki = "edytor\$st.zob.json", "edytor\$st.simrail.json", "edytor\$st.ut.json",
             "${st}_zob.json", "${st}_zob.simrail.json", "${st}_zob.przebiegi_gry.json"
    foreach ($p in $pliki) {
        $z = Join-Path (Join-Path $StacjeDir $st) $p
        if (-not (Test-Path $z)) { throw "Brak pliku stacji $st`: $z" }
        Copy-Item $z (Join-Path $kat $p)
    }
    $wpisy = @($rej.posterunki | Where-Object { $_.uklad -eq $st })
    if ($wpisy.Count -eq 0) { throw "Układ $st nie ma wpisu w $rejZr" }
    $rejPaczka.posterunki += $wpisy
    Write-Host "== stacja $st ($(($wpisy | ForEach-Object { "$($_.nazwaGry) $($_.kod)" }) -join ', '))" -ForegroundColor Cyan
}
$rejPaczka | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $cel 'stacje\posterunki.json') -Encoding UTF8

$czytaj = Join-Path $cel 'CZYTAJ.md'
(Get-Content $czytaj -Raw -Encoding UTF8).Replace('{WERSJA}', $Wersja) | Set-Content $czytaj -Encoding UTF8 -NoNewline

# 4. Dokumentacja i znacznik wersji
Copy-Item (Join-Path $repo 'README.md') (Join-Path $cel 'DOKUMENTACJA.md')
$commit = (git -C $repo rev-parse --short HEAD) 2>$null
$brudne = (git -C $repo status --porcelain) 2>$null
@(
    "SUK $Wersja"
    "commit: $commit$(if ($brudne) { ' (+ niezatwierdzone zmiany)' })"
    "SimRailMost: $wersjaMod (MelonLoader 0.7.x)"
    "zbudowano: $(Get-Date -Format 'yyyy-MM-dd HH:mm')"
) | Set-Content (Join-Path $cel 'WERSJA.txt') -Encoding UTF8

# 5. ZIP
if (-not $BezZip) {
    $zip = Join-Path $wyj "$nazwa.zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path $cel -DestinationPath $zip -CompressionLevel Optimal
    $hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLower()
    "$hash  $nazwa.zip" | Set-Content "$zip.sha256" -Encoding ASCII
    Write-Host "Paczka: $zip ($([math]::Round((Get-Item $zip).Length / 1MB, 1)) MB)" -ForegroundColor Green
    Write-Host "SHA256: $hash"
}
if ($brudne) { Write-Warning 'Repozytorium ma niezatwierdzone zmiany – zatwierdź je przed oznaczeniem wydania tagiem.' }
