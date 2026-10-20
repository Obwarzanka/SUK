#requires -Version 5.1
<#
.SYNOPSIS
Tworzy profil i plan instalacji. Nie instaluje ani nie uruchamia usługi.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OutputDirectory,
    [ValidatePattern('\A[A-Za-z0-9_-]{1,48}\z')][string]$WorldId = 'local',
    [string]$DataRoot,
    [string]$BinaryDirectory,
    [ValidateSet('podstawowy','usterki','zima','manewry','ic_05','ic_08','ic_15','ic_18')][string]$Scenario = 'podstawowy',
    [string]$AccountSid,
    [string]$HealthClientSid
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$serviceName = 'SUK.WorldHost'
$account = 'NT SERVICE\SUK.WorldHost'
function Full-LocalPath([string]$Path) {
    if ($Path -notmatch '^[A-Za-z]:[\\/]' -or $Path -match '[\x00-\x1f"<>|?*]' -or $Path.Substring(2).Contains(':')) {
        throw 'Wymagana bezwzględna ścieżka lokalna bez strumieni alternatywnych i znaków sterujących.'
    }
    $full = [IO.Path]::GetFullPath($Path).TrimEnd('\')
    if ($full.Length -le 3) { throw 'Korzeń woluminu nie jest katalogiem profilu.' }
    $full
}
function Assert-NoReparse([string]$Path) {
    $part = $Path
    while ($part) {
        if (Test-Path -LiteralPath $part) {
            if ((Get-Item -LiteralPath $part -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw "Punkt ponownej analizy w ścieżce: $part"
            }
        }
        $parent = [IO.Path]::GetDirectoryName($part)
        if ($parent -eq $part) { break }
        $part = $parent
    }
}
function Valid-Sid([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { throw 'Pusty SID.' }
    ([Security.Principal.SecurityIdentifier]::new($Value)).Value
}
if (!$DataRoot) { $DataRoot = Join-Path $env:ProgramData "SUK\WorldHost\$WorldId" }
if (!$BinaryDirectory) { $BinaryDirectory = Join-Path $env:ProgramFiles 'SUK\WorldHost' }
$DataRoot = Full-LocalPath $DataRoot
$BinaryDirectory = Full-LocalPath $BinaryDirectory
$OutputDirectory = Full-LocalPath $OutputDirectory
foreach ($path in @($DataRoot,$BinaryDirectory,$OutputDirectory)) { Assert-NoReparse $path }
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Katalog wynikowy musi być nowy; istniejący plan pozostaje bez zmian.' }
if (!$AccountSid) {
    # Wyłącznie zapytanie: nie tworzy konta, usługi, wpisów SCM ani ACL.
    $sidOutput = & "$env:SystemRoot\System32\sc.exe" showsid $serviceName 2>&1
    if ($LASTEXITCODE -ne 0) { throw 'Nie odczytano SID usługi przez sc.exe showsid.' }
    $sidMatch = [regex]::Match(($sidOutput -join "`n"), 'S-1-5-80-(?:\d+-){4}\d+')
    if (!$sidMatch.Success) { throw 'Odpowiedź showsid nie zawiera SID usługi.' }
    $AccountSid = $sidMatch.Value
}
# Ta sama reguła co ServiceProfile.IndividualSid: pojedyncze konto S-1-5-21 (RID od 1000) albo SID usługi S-1-5-80.
# Grupy ogólne (Users, INTERACTIVE, ALL SERVICES, Domain Users) i konta współdzielone są odrzucane.
function Test-IndividualSid([string]$Value) {
    if ($Value -cnotmatch '\AS-1-5-(?:21-(?:[0-9]{1,10}-){3}|80-(?:[0-9]{1,10}-){4})[0-9]{1,10}\z') { return $false }
    $parts = @($Value.Split('-') | Select-Object -Skip 3)
    foreach ($part in $parts) { $number = [uint32]0; if (![uint32]::TryParse($part, [ref]$number)) { return $false } }
    return ($parts[0] -eq '80' -or [uint32]$parts[-1] -ge 1000)
}
$AccountSid = Valid-Sid $AccountSid
if (!(Test-IndividualSid $AccountSid)) { throw 'Niedozwolony SID konta usługi: wymagane pojedyncze konto lub SID usługi.' }
if ($HealthClientSid) {
    $HealthClientSid = Valid-Sid $HealthClientSid
    if (!(Test-IndividualSid $HealthClientSid)) { throw 'Niedozwolony SID odbiorcy health: wymagane pojedyncze konto lub SID usługi, nie grupa.' }
}
$profile = [ordered]@{
    SchemaVersion = 1; ServiceName = $serviceName; AccountSid = $AccountSid
    HealthClientSid = $(if ($HealthClientSid) { $HealthClientSid } else { $null })
    DataRoot = $DataRoot; WorldId = $WorldId; StationFile = 'Inputs/station.json'
    Scenario = $Scenario; PipeName = "SUK.WorldHost.$WorldId.health"
    StartupTimeoutSeconds = 30; ShutdownTimeoutSeconds = 10; FreshnessSeconds = 2
    MaxWorkingSetMiB = 1024
}
$plan = [ordered]@{
    SchemaVersion = 1; ServiceName = $serviceName; Account = $account
    BinaryDirectory = $BinaryDirectory; Binary = (Join-Path $BinaryDirectory 'Lcs.WorldHost.exe')
    ProfileFile = (Join-Path $DataRoot 'Config\profile.json'); Profile = $profile
    Recovery = [ordered]@{ ResetAfterErrorFreeSeconds = 86400; RestartDelaySeconds = @(60,300,900); Then = 'None'; NonCrashFailures = $true }
    Limits = [ordered]@{ WorkingSet = 'Miękki limit 1024 MiB: utrata Ready i kontrolowane zatrzymanie, bez gwarancji twardej kwoty'; QueueCount = 1024; QueueBytes = 1048576; LogQueueBytes = 8388608; LogFiles = 16; LogFileBytes = 67108864 }
    DeploymentPerformed = $false
}
$installer = @'
#requires -Version 5.1
# Wygenerowany plan. Domyślne uruchomienie NICZEGO nie instaluje.
[CmdletBinding()]
param([switch]$Apply)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$plan = ConvertFrom-Json -InputObject @"
__PLAN_JSON__
"@
if (!$Apply) {
    $plan | ConvertTo-Json -Depth 12
    Write-Output 'Wyłącznie podgląd. Instalacja wymaga osobnego zlecenia operatora i parametru -Apply.'
    return
}
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (!$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Instalację wykonuje administrator.' }
function Assert-NoReparse([string]$Path) {
    $part = [IO.Path]::GetFullPath($Path)
    while ($part) {
        if (Test-Path -LiteralPath $part) {
            if ((Get-Item -LiteralPath $part -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Punkt ponownej analizy: $part" }
        }
        $parent = [IO.Path]::GetDirectoryName($part)
        if ($parent -eq $part) { break }
        $part = $parent
    }
}
function Get-SafeTree([string]$Path) {
    Assert-NoReparse $Path
    $item = Get-Item -LiteralPath $Path -Force
    $item
    if ($item.PSIsContainer) {
        # Sprawdzenie następuje PRZED wejściem do każdego dziecka, bez -Recurse przez junction.
        foreach ($child in Get-ChildItem -LiteralPath $Path -Force) { Get-SafeTree $child.FullName }
    }
}
function Assert-SafeParents([string]$Path) {
    $danger = [Security.AccessControl.FileSystemRights]'DeleteSubdirectoriesAndFiles,ChangePermissions,TakeOwnership'
    $trusted = @('S-1-5-18','S-1-5-32-544','S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464')
    $parent = [IO.Path]::GetDirectoryName($Path)
    while ($parent) {
        Assert-NoReparse $parent
        if (Test-Path -LiteralPath $parent) {
            $acl = (Get-Item -LiteralPath $parent -Force).GetAccessControl()
            if ($acl.GetOwner([Security.Principal.SecurityIdentifier]).Value -notin $trusted) { throw "Niezaufany właściciel przodka: $parent" }
            foreach ($rule in $acl.GetAccessRules($true,$true,[Security.Principal.SecurityIdentifier])) {
                if ($rule.AccessControlType -ne [Security.AccessControl.AccessControlType]::Allow -or ($rule.PropagationFlags -band [Security.AccessControl.PropagationFlags]::InheritOnly)) { continue }
                if (($rule.FileSystemRights -band $danger) -and $rule.IdentityReference.Value -notin $trusted) { throw "Przodek umożliwia podmianę dziecka przez niezaufany SID: $parent" }
            }
        }
        $parent = [IO.Path]::GetDirectoryName($parent)
    }
}
function Run-Sc([string[]]$Arguments) {
    & "$env:SystemRoot\System32\sc.exe" @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Błąd sc.exe ($LASTEXITCODE). Instalacja nie została ukończona." }
}
# Wszystkie warunki przed pierwszą zmianą. Instalator nie aktualizuje istniejącej usługi.
if ($plan.ServiceName -ne 'SUK.WorldHost' -or $plan.Account -ne 'NT SERVICE\SUK.WorldHost') { throw 'Niezgodna tożsamość usługi.' }
if (Get-Service -Name $plan.ServiceName -ErrorAction SilentlyContinue) { throw 'Usługa istnieje. Aktualizacja wymaga odrębnej procedury.' }
$sidText = & "$env:SystemRoot\System32\sc.exe" showsid $plan.ServiceName 2>&1
if ($LASTEXITCODE -ne 0) { throw 'Nie odczytano SID usługi.' }
$sid = [regex]::Match(($sidText -join "`n"), 'S-1-5-80-(?:\d+-){4}\d+').Value
if (!$sid -or $sid -ne $plan.Profile.AccountSid) { throw 'AccountSid nie odpowiada wirtualnemu kontu usługi. Profil testowy nie podlega instalacji.' }
$expectedData = [IO.Path]::GetFullPath((Join-Path $env:ProgramData ("SUK\WorldHost\" + $plan.Profile.WorldId)))
$expectedBinaries = [IO.Path]::GetFullPath((Join-Path $env:ProgramFiles 'SUK\WorldHost'))
if ($plan.Profile.DataRoot -ne $expectedData -or $plan.BinaryDirectory -ne $expectedBinaries) { throw 'Plan testowy/niestandardowe katalogi nie podlegają instalacji.' }
$inputs = Join-Path $plan.Profile.DataRoot 'Inputs'
foreach ($path in @($plan.BinaryDirectory,$plan.Profile.DataRoot,$inputs,$plan.ProfileFile,$PSCommandPath)) { Assert-NoReparse $path }
Assert-SafeParents $plan.BinaryDirectory
Assert-SafeParents $plan.Profile.DataRoot
if (!(Test-Path -LiteralPath $plan.Binary -PathType Leaf) -or !(Test-Path -LiteralPath (Join-Path $inputs 'station.json') -PathType Leaf)) { throw 'Operator musi wcześniej przygotować zatwierdzone binaria i Inputs/station.json wraz z wymaganymi plikami danych.' }
if (Test-Path -LiteralPath $plan.ProfileFile) { throw 'Config/profile.json już istnieje; instalator go nie nadpisze.' }
$binaryTree = @(Get-SafeTree $plan.BinaryDirectory)
$dataTree = @(Get-SafeTree $plan.Profile.DataRoot)
# Kolejność zabezpiecza korzenie i każde istniejące dziecko. Nie zostawiamy starych jawnych ACE.
function Set-PrivateAcl([string]$Path, [Security.AccessControl.FileSystemRights]$AccountRights) {
    Assert-NoReparse $Path
    $item = Get-Item -LiteralPath $Path -Force
    if ($item.PSIsContainer) {
        $acl = [Security.AccessControl.DirectorySecurity]::new()
        $inherit = [Security.AccessControl.InheritanceFlags]'ContainerInherit,ObjectInherit'
    } else {
        $acl = [Security.AccessControl.FileSecurity]::new()
        $inherit = [Security.AccessControl.InheritanceFlags]::None
    }
    $acl.SetAccessRuleProtection($true, $false)
    $admin = [Security.Principal.SecurityIdentifier]::new('S-1-5-32-544')
    $acl.SetOwner($admin)
    foreach ($entry in @(@('S-1-5-18',[Security.AccessControl.FileSystemRights]::FullControl),@('S-1-5-32-544',[Security.AccessControl.FileSystemRights]::FullControl),@($plan.Profile.AccountSid,$AccountRights))) {
        $rule = [Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.SecurityIdentifier]::new([string]$entry[0]),[Security.AccessControl.FileSystemRights]$entry[1],$inherit,[Security.AccessControl.PropagationFlags]::None,[Security.AccessControl.AccessControlType]::Allow)
        $acl.AddAccessRule($rule)
    }
    # Operator health odczytuje jawny profil. ACE bez dziedziczenia: żadnego dostępu do Inputs/Logs/State.
    if ($plan.Profile.HealthClientSid) {
        $healthRights = if ($Path -eq $plan.Profile.DataRoot -or $Path -eq (Join-Path $plan.Profile.DataRoot 'Config')) { 'ReadAndExecute' } elseif ($Path -eq $plan.ProfileFile) { 'Read' } else { $null }
        if ($healthRights) {
            $rule = [Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.SecurityIdentifier]::new($plan.Profile.HealthClientSid),[Security.AccessControl.FileSystemRights]$healthRights,[Security.AccessControl.InheritanceFlags]::None,[Security.AccessControl.PropagationFlags]::None,[Security.AccessControl.AccessControlType]::Allow)
            $acl.AddAccessRule($rule)
        }
    }
    Set-Acl -LiteralPath $Path -AclObject $acl
}
Set-PrivateAcl $plan.Profile.DataRoot ReadAndExecute
foreach ($directory in @('Inputs','Config','Logs','State')) {
    $path = Join-Path $plan.Profile.DataRoot $directory
    Assert-NoReparse $path
    if (!(Test-Path -LiteralPath $path)) { New-Item -ItemType Directory -Path $path | Out-Null }
}
$utf8 = [Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText($plan.ProfileFile, ($plan.Profile | ConvertTo-Json -Depth 8), $utf8)
foreach ($item in $binaryTree) { Set-PrivateAcl $item.FullName ReadAndExecute }
foreach ($item in @(Get-SafeTree $plan.Profile.DataRoot)) {
    $relative = $item.FullName.Substring($plan.Profile.DataRoot.Length).TrimStart('\')
    $rights = if ($relative -match '^(Logs|State)(\\|$)') { 'Modify' } else { 'ReadAndExecute' }
    Set-PrivateAcl $item.FullName $rights
}
# Dokładnie cztery akcje: 3 restarty, potem NONE powtarzane dla kolejnych awarii.
# Win32 użyty jawnie, aby nie polegać na niejednoznacznym pustym elemencie składni sc failure.
Add-Type -TypeDefinition @"
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
public static class SukWorldRecovery {
    [StructLayout(LayoutKind.Sequential)] struct ActionItem { public int Type; public uint Delay; }
    [StructLayout(LayoutKind.Sequential)] struct Actions { public uint Reset; public IntPtr Reboot; public IntPtr Command; public uint Count; public IntPtr Items; }
    [StructLayout(LayoutKind.Sequential)] struct Flag { [MarshalAs(UnmanagedType.Bool)] public bool Enabled; }
    [DllImport("advapi32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern IntPtr OpenSCManager(string machine, string database, uint access);
    [DllImport("advapi32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern IntPtr OpenService(IntPtr manager, string name, uint access);
    [DllImport("advapi32.dll", EntryPoint="ChangeServiceConfig2W", SetLastError=true)] static extern bool SetActions(IntPtr service, int level, ref Actions value);
    [DllImport("advapi32.dll", EntryPoint="ChangeServiceConfig2W", SetLastError=true)] static extern bool SetFlag(IntPtr service, int level, ref Flag value);
    [DllImport("advapi32.dll")] static extern bool CloseServiceHandle(IntPtr handle);
    public static void Configure(string name) {
        IntPtr manager=OpenSCManager(null,null,1), service=IntPtr.Zero, items=IntPtr.Zero;
        if(manager==IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        try {
            service=OpenService(manager,name,0x12); // CHANGE_CONFIG | START dla akcji restartu.
            if(service==IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            int size=Marshal.SizeOf(typeof(ActionItem)); items=Marshal.AllocHGlobal(4*size);
            uint[] delays={60000,300000,900000,0};
            for(int i=0;i<4;i++) Marshal.StructureToPtr(new ActionItem { Type=i<3?1:0, Delay=delays[i] },IntPtr.Add(items,i*size),false);
            var actions=new Actions { Reset=86400, Count=4, Items=items };
            if(!SetActions(service,2,ref actions)) throw new Win32Exception(Marshal.GetLastWin32Error());
            var flag=new Flag { Enabled=true };
            if(!SetFlag(service,4,ref flag)) throw new Win32Exception(Marshal.GetLastWin32Error());
        } finally { if(items!=IntPtr.Zero) Marshal.FreeHGlobal(items); if(service!=IntPtr.Zero) CloseServiceHandle(service); CloseServiceHandle(manager); }
    }
}
"@
$commandLine = '"' + $plan.Binary + '" --service-profile "' + $plan.ProfileFile + '"'
# Nowa usługa najpierw disabled. Błąd późniejszego kroku nie uruchomi niepełnej instalacji.
Run-Sc @('create',$plan.ServiceName,'type=','own','start=','disabled','binPath=',$commandLine,'obj=',$plan.Account,'DisplayName=','SUK — host świata')
Run-Sc @('sidtype',$plan.ServiceName,'unrestricted')
Run-Sc @('privs',$plan.ServiceName,'SeChangeNotifyPrivilege')
[SukWorldRecovery]::Configure($plan.ServiceName)
Run-Sc @('config',$plan.ServiceName,'start=','delayed-auto')
Write-Output 'Zarejestrowano usługę z opóźnionym autostartem. Nie uruchomiono jej w tej procedurze.'
Write-Output 'Następnie: osobna próba Start-Service, health, Stop-Service, awaria, restart i reboot bez logowania.'
'@
# Here-string instalatora jest rozwijany przez jego PowerShell; JSON musi pozostać literałem.
$jsonPlan = $plan | ConvertTo-Json -Depth 12
$jsonPlanForTemplate = $jsonPlan.Replace('`','``').Replace('$','`$')
$installer = $installer.Replace('__PLAN_JSON__', $jsonPlanForTemplate)
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$utf8 = [Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'profile.json'), ($profile | ConvertTo-Json -Depth 8), $utf8)
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'plan.json'), $jsonPlan, $utf8)
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'Install-WorldHostService.ps1'), $installer, [Text.UTF8Encoding]::new($true))
Write-Output "Utworzono profil, plan i skrypt instalacji: $OutputDirectory"
Write-Output 'Nie zmieniono SCM, kont, ACL, danych świata ani stanu usług.'
