# Les auto-tests WPF a la vraie souris, sans deranger : ils tournent dans le Bac a sable Windows, qui a son propre bureau,
# donc sa propre souris et son propre clavier. La fenetre du bac a sable est placee hors des ecrans sans prendre le focus ;
# elle apparait une seconde au demarrage. Ne pas la reduire (« Win+D » comme « Win+M » reduit tout) : un bureau distant
# reduit ne recoit plus de saisie.
#
#   ./tests/run-wpf-selftests-sandbox.ps1                  # tous les auto-tests
#   ./tests/run-wpf-selftests-sandbox.ps1 -Only matrix     # un seul (demo, examples, perf, scenarios, matrix)
#
# Demande la fonctionnalite « Bac a sable Windows » et la commande wsb (Windows 11 24H2). Le bac a sable est coupe du
# reseau et ne voit que trois dossiers : les binaires copies et le runtime .NET (lecture seule), et le dossier des journaux.
# Les resultats et le code de sortie sont ceux de run-wpf-selftests.ps1. Les temps de l'essai « perf » ne sont pas
# comparables a ceux du poste (machine virtuelle).
param(
    [string]$Configuration = 'Release',
    [string]$Output = (Join-Path $PSScriptRoot '..\artifacts\selftest'),
    [int]$PerfNodes = 500,
    [switch]$NoBuild,
    [string]$Only,                 # un seul nom de run ; tous par defaut
    [int]$MemoryMB = 6144,
    [int]$TimeoutMinutes = 40
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

if (-not (Get-Command wsb -ErrorAction SilentlyContinue)) {
    Write-Host 'Commande wsb introuvable : activez le Bac a sable Windows (Windows 11 24H2).'; exit 1
}
function Get-Sandboxes { @(((wsb list --raw) -join '' | ConvertFrom-Json).WindowsSandboxEnvironments | Where-Object { $_ }) }
# Une seule instance a la fois, et celle qui tourne n'est pas a nous : on ne l'arrete jamais.
if ((Get-Sandboxes).Count -gt 0) { Write-Host 'Un Bac a sable Windows tourne deja : fermez-le puis relancez.'; exit 1 }

$projects = @{ 'demo\wpf' = 'demo.csproj'; 'tests\Scenarios.Wpf' = 'Scenarios.Wpf.csproj' }
if (-not $NoBuild) {
    foreach ($folder in $projects.Keys) {
        dotnet build (Join-Path $root "$folder\$($projects[$folder])") -c $Configuration -v q --nologo
        if ($LASTEXITCODE -ne 0) { Write-Host 'Build en echec.'; exit 1 }
    }
}

# Chemins sous le depot : le bac a sable refuse les noms courts de Windows (ceux de %TEMP% en sont parfois).
$work = Join-Path $root 'artifacts\sandbox'
$stage = Join-Path $work 'stage'
Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $stage, $Output | Out-Null
$Output = (Resolve-Path $Output).Path
foreach ($folder in $projects.Keys) {
    $target = Join-Path $stage "$folder\bin\$Configuration\net10.0-windows"
    New-Item -ItemType Directory -Force $target | Out-Null
    Copy-Item (Join-Path $root "$folder\bin\$Configuration\net10.0-windows\*") $target -Recurse
}
New-Item -ItemType Directory -Force (Join-Path $stage 'tests') | Out-Null
Copy-Item (Join-Path $PSScriptRoot 'run-wpf-selftests.ps1') (Join-Path $stage 'tests')

$only = if ($Only) { "-Only $Only" } else { '' }
@"
`$env:DOTNET_ROOT = 'C:\dotnet'
powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\stage\tests\run-wpf-selftests.ps1 -NoBuild -Configuration $Configuration -PerfNodes $PerfNodes -Output C:\out $only *> C:\out\console.txt
`$LASTEXITCODE | Set-Content C:\out\exitcode.txt
"@ | Set-Content (Join-Path $stage 'in-sandbox.ps1')

$done = Join-Path $Output 'exitcode.txt'
$console = Join-Path $Output 'console.txt'
Remove-Item $done, $console -ErrorAction SilentlyContinue

function Format-Folder($from, $to, $readOnly) {
    "<MappedFolder><HostFolder>$([Security.SecurityElement]::Escape($from))</HostFolder><SandboxFolder>$to</SandboxFolder><ReadOnly>$readOnly</ReadOnly></MappedFolder>"
}
$dotnet = Split-Path (Get-Command dotnet).Source
$folders = (Format-Folder $stage 'C:\stage' 'true') + (Format-Folder $Output 'C:\out' 'false') + (Format-Folder $dotnet 'C:\dotnet' 'true')
$config = '<Configuration><Networking>Disable</Networking><ClipboardRedirection>Disable</ClipboardRedirection>' +
    '<PrinterRedirection>Disable</PrinterRedirection><AudioInput>Disable</AudioInput><VideoInput>Disable</VideoInput>' +
    "<MemoryInMB>$MemoryMB</MemoryInMB><MappedFolders>$folders</MappedFolders></Configuration>"

Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System; using System.Runtime.InteropServices;
public static class SandboxWindow {
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr window);
}
'@

$started = (wsb start --raw --config $config) -join ''
try { $id = ($started | ConvertFrom-Json).Id } catch { $id = $null }
if (-not $id) { Write-Host "Le Bac a sable n'a pas demarre : $started"; exit 1 }

$code = 1
try {
    # Ouvrir la session (les tests demandent un bureau), puis ecarter la fenetre : la notre porte notre identifiant.
    $previous = [SandboxWindow]::GetForegroundWindow()
    $null = Start-Process wsb -ArgumentList "connect --id $id" -WindowStyle Hidden -PassThru
    $window = [IntPtr]::Zero
    for ($i = 0; $i -lt 240 -and $window -eq [IntPtr]::Zero; $i++) {
        Start-Sleep -Milliseconds 250
        $mine = Get-CimInstance Win32_Process -Filter "Name='WindowsSandboxRemoteSession.exe'" |
            Where-Object { $_.CommandLine -like "*$id*" } | Select-Object -First 1
        if ($mine) { $window = (Get-Process -Id $mine.ProcessId).MainWindowHandle }
    }
    if ($window -eq [IntPtr]::Zero) { throw 'La fenetre du Bac a sable reste introuvable.' }
    $screens = [System.Windows.Forms.SystemInformation]::VirtualScreen
    function Hide-Window {
        [void][SandboxWindow]::ShowWindow($window, 4) # affichee sans activation
        # A droite de tous les ecrans, a une taille fixe : c'est la resolution du bureau des tests. 0x10 : sans activation.
        [void][SandboxWindow]::SetWindowPos($window, [IntPtr]1, $screens.Right + 200, 0, 1920, 1080, 0x10)
    }
    Hide-Window
    [void][SandboxWindow]::SetForegroundWindow($previous)

    # La session met quelques secondes a s'ouvrir : jusque-la, exec echoue.
    $ready = $false
    for ($i = 0; $i -lt 60 -and -not $ready; $i++) {
        Start-Sleep 2
        $ErrorActionPreference = 'Continue'
        wsb exec --id $id -r ExistingLogin -c 'cmd.exe /c exit 0' 2>&1 | Out-Null
        $ready = $LASTEXITCODE -eq 0
        $ErrorActionPreference = 'Stop'
    }
    if (-not $ready) { throw "La session du Bac a sable ne s'est pas ouverte." }
    Hide-Window
    Write-Host "Auto-tests en cours dans le Bac a sable $id (fenetre hors ecran). Journaux : $Output"
    $runner = Start-Process wsb -WindowStyle Hidden -PassThru -ArgumentList `
        "exec --id $id -r ExistingLogin -c `"powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\stage\in-sandbox.ps1`""

    $until = (Get-Date).AddMinutes($TimeoutMinutes)
    $shown = 0
    while ((Get-Date) -lt $until -and -not (Test-Path $done)) {
        Start-Sleep 5
        $lines = @(Get-Content $console -ErrorAction SilentlyContinue)
        if ($lines.Count -gt $shown) { $lines[$shown..($lines.Count - 1)] | Write-Host; $shown = $lines.Count }
        if (-not (@(Get-Sandboxes).Id -contains $id)) { throw "Le Bac a sable $id s'est arrete avant la fin." }
    }
    if (-not (Test-Path $done)) { throw "Delai de $TimeoutMinutes minutes depasse." }
    Start-Sleep 1
    $lines = @(Get-Content $console -ErrorAction SilentlyContinue)
    if ($lines.Count -gt $shown) { $lines[$shown..($lines.Count - 1)] | Write-Host }
    $code = [int](Get-Content $done)
}
catch { Write-Host "ECHEC : $($_.Exception.Message)" }
finally {
    $ErrorActionPreference = 'Continue'
    wsb stop --id $id 2>&1 | Out-Null
    # La fenetre, hors ecran, survivrait a l'arret : on ferme la notre, et elle seule.
    Get-CimInstance Win32_Process -Filter "Name='WindowsSandboxRemoteSession.exe'" |
        Where-Object { $_.CommandLine -like "*$id*" } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -Confirm:$false -ErrorAction SilentlyContinue }
}
exit $code
