# Consomme les paquets Wpflow comme un utilisateur : fabrique les paquets dans une source locale, cree des projets
# neufs hors de la solution (dotnet new), installe les paquets, copie le « Demarrage rapide » du README, compile sans
# avertissement, puis execute. Aucune reference de projet.
#
#   ./tests/consumers/run.ps1                       # WPF et Blazor, paquets du depot
#   ./tests/consumers/run.ps1 -Only wpf
#   ./tests/consumers/run.ps1 -FromNuget -Version 1.1.0   # apres publication : paquets de nuget.org
#
# WPF : ouvre des fenetres (bureau interactif requis). Blazor : navigateur installe, WPFLOW_BROWSER_CHANNEL (chrome par defaut).
param(
    [ValidateSet('all', 'wpf', 'blazor')][string]$Only = 'all',
    [switch]$FromNuget,
    [string]$Version,
    [string]$Work = (Join-Path ([IO.Path]::GetTempPath()) 'wpflow-consumers')
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ($FromNuget -and -not $Version) { throw '-FromNuget demande -Version.' }
# Version neuve a chaque essai : impossible de tester par erreur un ancien paquet.
if (-not $Version) { $Version = "99.0.0-consumer.$(Get-Date -Format 'yyyyMMddHHmmss')" }

New-Item -ItemType Directory -Force $Work | Out-Null
$run = Join-Path $Work (Get-Date -Format 'yyyyMMdd-HHmmss')
$feed = Join-Path $run 'feed'
New-Item -ItemType Directory -Force $feed | Out-Null

# Cache NuGet isole de celui du poste, garde d'un essai a l'autre pour les dependances ; les paquets Wpflow en sont retires.
$env:NUGET_PACKAGES = Join-Path $Work 'nuget-cache'
Get-ChildItem $env:NUGET_PACKAGES -Filter 'wpflow.*' -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force

$utf8 = New-Object Text.UTF8Encoding($false)
function Write-Utf8([string]$path, [string]$content) { [IO.File]::WriteAllText($path, $content, $utf8) }
function Read-Utf8([string]$path) { [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8) }

function Invoke-Dotnet {
    $output = & dotnet @args 2>&1
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($args -join ' ')`n$(($output | Select-Object -Last 25) -join "`n")" }
}

if (-not $FromNuget) {
    Write-Host "== paquets $Version"
    foreach ($p in 'Wpflow.Core', 'Wpflow.Wpf', 'Wpflow.Blazor') {
        Invoke-Dotnet pack (Join-Path $root "src\$p") -c Release "-p:Version=$Version" -o $feed --nologo -v q
    }
}

# Les paquets Wpflow ne viennent QUE de la source testee.
$wpflowSource = if ($FromNuget) { 'nuget.org' } else { 'local' }
$localLine = if ($FromNuget) { '' } else { "<add key=`"local`" value=`"$feed`" />" }
$localMap = if ($FromNuget) { '' } else { '<packageSource key="local"><package pattern="Wpflow.*" /></packageSource>' }
$nugetMap = if ($FromNuget) { '<package pattern="*" /><package pattern="Wpflow.*" />' } else { '<package pattern="*" />' }
Write-Utf8 (Join-Path $run 'nuget.config') @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources><clear />$localLine<add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources>
  <packageSourceMapping>$localMap<packageSource key="nuget.org">$nugetMap</packageSource></packageSourceMapping>
</configuration>
"@

$results = New-Object Collections.Generic.List[object]
function Test-Scenario([string]$name, [scriptblock]$body) {
    Write-Host "== $name"
    $dir = Join-Path $run $name
    New-Item -ItemType Directory -Force $dir | Out-Null
    Push-Location $dir
    try {
        $note = & $body $dir
        $results.Add([pscustomobject]@{ Name = $name; Ok = $true; Note = "$note" })
        Write-Host "   OK $note"
    }
    catch {
        $results.Add([pscustomobject]@{ Name = $name; Ok = $false; Note = $_.Exception.Message })
        Write-Host "   ECHEC $($_.Exception.Message)"
    }
    finally { Pop-Location }
}

# Le bloc de code numero $index (a partir de 0) d'un langage dans une section « ## ... » du README.
function Get-ReadmeBlock([string]$section, [string]$language) {
    $readme = Read-Utf8 (Join-Path $root 'README.md')
    # Chaines entre apostrophes : entre guillemets, « $( » serait une sous-expression PowerShell.
    $m = [regex]::Match($readme, '(?ms)^## ' + [regex]::Escape($section) + '\r?$(.*?)(?=^## |\z)')
    if (-not $m.Success) { throw "Section « $section » introuvable dans le README." }
    $block = [regex]::Match($m.Groups[1].Value, "(?ms)^``````$language\r?\n(.*?)^``````")
    if (-not $block.Success) { throw "Bloc $language introuvable dans la section « $section »." }
    return $block.Groups[1].Value
}

if ($Only -ne 'blazor') { . (Join-Path $PSScriptRoot 'wpf.ps1') }
if ($Only -ne 'wpf') { . (Join-Path $PSScriptRoot 'blazor.ps1') }

Write-Host ''
$results | ForEach-Object { Write-Host ("{0,-6} {1}" -f $(if ($_.Ok) { 'OK' } else { 'ECHEC' }), $_.Name) }
$failed = @($results | Where-Object { -not $_.Ok })
Write-Host "$($results.Count - $failed.Count)/$($results.Count) scenarios reussis (paquets $Version depuis $wpflowSource ; dossier $run)"
if ($failed.Count -gt 0 -or $results.Count -eq 0) { exit 1 }
exit 0
