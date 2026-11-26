# Auto-tests WPF a la vraie souris : demo principale, fenetre d'exemples, spike de performance, editeur de schema temoin, matrice des gestes, conditions hostiles.
# Demande un bureau interactif et deverrouille ; ne pas toucher la souris ni le clavier pendant l'execution.
# Code de sortie : 0 si tout passe ; 1 si un run echoue (ligne ECHEC, exception, delai, code de sortie non nul, journal vide).
param(
    [string]$Configuration = 'Release',
    [string]$Output = (Join-Path $PSScriptRoot '..\artifacts\selftest'),
    [int]$PerfNodes = 500,
    [switch]$NoBuild,
    [string[]]$Only    # noms des runs a executer (demo, examples, perf, scenarios, matrix, hostile) ; tous par defaut
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $NoBuild) {
    foreach ($project in 'demo\wpf\demo.csproj', 'tests\Scenarios.Wpf\Scenarios.Wpf.csproj') {
        dotnet build (Join-Path $root $project) -c $Configuration -v q --nologo
        if ($LASTEXITCODE -ne 0) { Write-Host 'Build en echec.'; exit 1 }
    }
}

$exe = Join-Path $root "demo\wpf\bin\$Configuration\net10.0-windows\demo.exe"
New-Item -ItemType Directory -Force $Output | Out-Null
$Output = (Resolve-Path $Output).Path

# TimeoutSeconds est une securite au-dessus du delai interne de la demo.
$scenariosExe = Join-Path $root "tests\Scenarios.Wpf\bin\$Configuration\net10.0-windows\Scenarios.Wpf.exe"
$runs = @(
    @{ Name = 'demo';     Log = 'demo.log';        TimeoutSeconds = 180; Env = @{ WPFLOW_SELFTEST = '1' } }
    @{ Name = 'examples'; Log = 'examples.log';    TimeoutSeconds = 300; Env = @{ WPFLOW_EXAMPLES = 'selftest' } }
    @{ Name = 'perf';     Log = 'perf.perf.log';   TimeoutSeconds = 240; Env = @{ WPFLOW_PERF = "$PerfNodes" } }
    @{ Name = 'scenarios'; Log = 'scenarios.log';  TimeoutSeconds = 600; Env = @{ WPFLOW_SCENARIOS = (Join-Path $Output 'scenarios.log') }; Exe = $scenariosExe }
    @{ Name = 'matrix';    Log = 'matrix.log';     TimeoutSeconds = 600; Env = @{ WPFLOW_MATRIX = (Join-Path $Output 'matrix.log') }; Exe = $scenariosExe }
    @{ Name = 'hostile';    Log = 'hostile.log';    TimeoutSeconds = 600; Env = @{ WPFLOW_HOSTILE = (Join-Path $Output 'hostile.log') }; Exe = $scenariosExe }
)
# Les variables qui choisissent un auto-test : une seule doit etre definie a la fois.
$modes = 'WPFLOW_SELFTEST', 'WPFLOW_EXAMPLES', 'WPFLOW_PERF', 'WPFLOW_SCENARIOS', 'WPFLOW_MATRIX', 'WPFLOW_HOSTILE'
function Clear-Variables([string[]]$names) { foreach ($v in $names) { Remove-Item "Env:$v" -ErrorAction SilentlyContinue } }

$failed = @()
if ($Only) { $runs = @($runs | Where-Object { $Only -contains $_.Name }) }
if ($runs.Count -eq 0) { Write-Host "Aucun run ne correspond a : $Only"; exit 1 }
foreach ($run in $runs) {
    Write-Host "== $($run.Name)"
    $log = Join-Path $Output $run.Log
    Remove-Item $log -ErrorAction SilentlyContinue
    Clear-Variables $modes
    $env:WPFLOW_SCREENSHOT = Join-Path $Output "$($run.Name).png"
    foreach ($key in $run.Env.Keys) { Set-Item "Env:$key" $run.Env[$key] }

    $program = if ($run.Exe) { $run.Exe } else { $exe }
    $process = Start-Process $program -PassThru
    $null = $process.Handle # sans cela, ExitCode reste vide apres WaitForExit
    $reason = $null
    if (-not $process.WaitForExit($run.TimeoutSeconds * 1000)) {
        $process.Kill()
        $reason = "delai de $($run.TimeoutSeconds) s depasse"
    }
    elseif ($process.ExitCode -ne 0) { $reason = "code de sortie $($process.ExitCode)" }

    $lines = @()
    if (Test-Path $log) { $lines = @(Get-Content $log -Encoding UTF8) }
    $bad = @($lines | Where-Object { $_ -cmatch '^(ECHEC|ERROR|WATCHDOG)' })
    if ($bad.Count -gt 0 -and -not $reason) { $reason = "$($bad.Count) ligne(s) en echec" }
    if ($lines.Count -eq 0 -and -not $reason) { $reason = 'journal vide ou absent' }

    if ($reason) {
        Write-Host "   ECHEC : $reason"
        $bad | ForEach-Object { Write-Host "   $_" }
        $failed += $run.Name
    }
    else {
        $ok = @($lines | Where-Object { $_ -cmatch '^OK' }).Count
        Write-Host "   OK ($ok verifications, $($lines.Count) lignes) : $log"
    }
}

Clear-Variables ($modes + 'WPFLOW_SCREENSHOT')
if ($failed.Count -gt 0) { Write-Host "Auto-tests en echec : $($failed -join ', ')"; exit 1 }
Write-Host 'Tous les auto-tests WPF passent.'
exit 0
