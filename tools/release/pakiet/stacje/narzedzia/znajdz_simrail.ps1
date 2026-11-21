# Wypisuje katalog gry SimRail znaleziony w bibliotekach Steam (pusty wynik, gdy nie ma).
$ErrorActionPreference = 'SilentlyContinue'
$kandydaci = New-Object System.Collections.Generic.List[string]
$steam = (Get-ItemProperty 'HKCU:\Software\Valve\Steam').SteamPath
if (-not $steam) { $steam = (Get-ItemProperty 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam').InstallPath }
if ($steam) {
    $kandydaci.Add((Join-Path $steam 'steamapps\common\SimRail'))
    $vdf = Join-Path $steam 'steamapps\libraryfolders.vdf'
    if (Test-Path $vdf) {
        foreach ($m in [regex]::Matches((Get-Content $vdf -Raw), '"path"\s+"([^"]+)"')) {
            $kandydaci.Add((Join-Path ($m.Groups[1].Value -replace '\\\\', '\') 'steamapps\common\SimRail'))
        }
    }
}
$kandydaci.Add('C:\Program Files (x86)\Steam\steamapps\common\SimRail')
foreach ($k in $kandydaci) {
    if (Test-Path (Join-Path $k 'SimRail.exe')) { (Resolve-Path $k).Path; exit 0 }
}
exit 0
