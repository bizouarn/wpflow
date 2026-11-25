# Auto-tests WPF a la vraie souris : demo principale, fenetre d'exemples, spike de performance.
# Demande un bureau interactif et deverrouille ; ne pas toucher la souris ni le clavier pendant l'execution.
# Code de sortie : 0 si tout passe ; 1 si un run echoue (ligne ECHEC, exception, delai, code de sortie non nul, journal vide).
param(
    [string]$Configuration = 'Release',
    [string]$Output = (Join-Path $PSScriptRoot '..\artifacts\selftest'),
    [int]$PerfNodes = 500,
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $NoBuild) {
    dotnet build (Join-Path $root 'demo\wpf\demo.csproj') -c $Configuration -v q --nologo
    if ($LASTEXITCODE -ne 0) { Write-Host 'Build en echec.'; exit 1 }
}

$exe = Join-Path $root "demo\wpf\bin\$Configuration\net10.0-windows\demo.exe"
New-Item -ItemType Directory -Force $Output | Out-Null
$Output = (Resolve-Path $Output).Path

# TimeoutSeconds est une securite au-dessus du delai interne de la demo.
$runs = @(
    @{ Name = 'demo';     Log = 'demo.log';        TimeoutSeconds = 180; Env = @{ WPFLOW_SELFTEST = '1' } }
    @{ Name = 'examples'; Log = 'examples.log';    TimeoutSeconds = 300; Env = @{ WPFLOW_EXAMPLES = 'selftest' } }
    @{ Name = 'perf';     Log = 'perf.perf.log';   TimeoutSeconds = 240; Env = @{ WPFLOW_PERF = "$PerfNodes" } }
)

$failed = @()
foreach ($run in $runs) {
    Write-Host "== $($run.Name)"
    $log = Join-Path $Output $run.Log
    Remove-Item $log -ErrorAction SilentlyContinue
    foreach ($v in 'WPFLOW_SELFTEST', 'WPFLOW_EXAMPLES', 'WPFLOW_PERF') { Remove-Item "Env:$v" -ErrorAction SilentlyContinue }
    $env:WPFLOW_SCREENSHOT = Join-Path $Output "$($run.Name).png"
    foreach ($key in $run.Env.Keys) { Set-Item "Env:$key" $run.Env[$key] }

    $process = Start-Process $exe -PassThru
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

foreach ($v in 'WPFLOW_SCREENSHOT', 'WPFLOW_SELFTEST', 'WPFLOW_EXAMPLES', 'WPFLOW_PERF') { Remove-Item "Env:$v" -ErrorAction SilentlyContinue }
if ($failed.Count -gt 0) { Write-Host "Auto-tests en echec : $($failed -join ', ')"; exit 1 }
Write-Host 'Tous les auto-tests WPF passent.'
exit 0
