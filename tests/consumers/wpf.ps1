# Scenarios WPF, appeles par run.ps1 (variables partagees : $root, $Version, Test-Scenario, Invoke-Dotnet...).
$templates = Join-Path $PSScriptRoot 'wpf'

$quickstart = "D$([char]0x00E9)marrage rapide" # accent ecrit ainsi : PowerShell 5.1 lit mal l'UTF-8 sans BOM
$windowTag = Get-ReadmeBlock $quickstart 'xml'
$readmeCode = Get-ReadmeBlock $quickstart 'csharp'
$flowNamespace = [regex]::Match($windowTag, 'xmlns:flow="[^"]+"').Value
$flowElement = [regex]::Match($windowTag, '(?s)<flow:FlowView\b.*</flow:FlowView>').Value
if (-not $flowNamespace -or -not $flowElement) { throw 'XAML du demarrage rapide non reconnu.' }
$usings = ([regex]::Matches($readmeCode, '(?m)^using [^\r\n]+;') | ForEach-Object { $_.Value }) -join "`n"
$statements = [regex]::Replace($readmeCode, '(?m)^using [^\r\n]+;\r?$', '')

function New-Window([string]$xamlBody, [string]$extraNamespaces = '', [string]$resources = '') {
    @"
<Window x:Class="Consumer.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:local="clr-namespace:Consumer"
        $flowNamespace $extraNamespaces
        Width="900" Height="600">
$resources
$xamlBody
</Window>
"@
}

function New-WindowCode([string]$topUsings, [string]$body, [string]$view = 'Flow', [string]$extra = 'null') {
    @"
$topUsings
using System.Windows;

namespace Consumer;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        Probe.Watch();
        InitializeComponent();
$body
        Probe.Attach(this, () => $view, $extra);
    }
}
"@
}

# Projet neuf + paquet(s) ; renvoie le dossier du projet.
function New-WpfApp([string]$dir, [string]$tfm, [string[]]$packages = @()) {
    Invoke-Dotnet new wpf -n Consumer -o Consumer -f $tfm
    Copy-Item (Join-Path $run 'nuget.config') (Join-Path $dir 'nuget.config')
    $project = Join-Path $dir 'Consumer'
    Invoke-Dotnet add $project package Wpflow.Wpf --version $Version
    foreach ($p in $packages) {
        $name, $v = $p -split '@'
        Invoke-Dotnet add $project package $name --version $v
    }
    Copy-Item (Join-Path $templates 'Probe.cs') $project
    return $project
}

function Invoke-Probe([string]$exe, [string]$dir) {
    $report = Join-Path $dir 'probe.log'
    Remove-Item $report -ErrorAction SilentlyContinue
    $env:WPFLOW_PROBE = $report
    try {
        $process = Start-Process $exe -PassThru
        $null = $process.Handle
        if (-not $process.WaitForExit(90000)) { $process.Kill(); throw 'delai de 90 s depasse' }
    }
    finally { Remove-Item Env:WPFLOW_PROBE }
    if (-not (Test-Path $report)) { throw "aucun rapport (code de sortie $($process.ExitCode))" }
    $lines = @(Get-Content $report -Encoding UTF8)
    $bad = @($lines | Where-Object { $_ -cmatch '^(ECHEC|ERROR)' })
    if ($process.ExitCode -ne 0 -or $bad.Count -gt 0) { throw "code $($process.ExitCode)`n$($lines -join "`n")" }
    $runtime = ($lines | Where-Object { $_ -match '^INFO  runtime' }) -replace '^INFO  ', ''
    return "$(@($lines | Where-Object { $_ -cmatch '^OK' }).Count) verifications, $runtime"
}

function Build-AndProbe([string]$project, [string]$tfm, [string]$dir) {
    Invoke-Dotnet build $project -c Release -warnaserror --nologo
    $previous = $env:DOTNET_ROLL_FORWARD
    # Sans runtime .NET 8 sur le poste, l'application net8.0 tourne sur le runtime suivant (la sonde ecrit lequel).
    if (-not $env:DOTNET_ROLL_FORWARD) { $env:DOTNET_ROLL_FORWARD = 'Minor' }
    if (-not (& dotnet --list-runtimes | Select-String '^Microsoft\.WindowsDesktop\.App 8\.')) { $env:DOTNET_ROLL_FORWARD = 'Major' }
    try { Invoke-Probe (Join-Path $project "bin\Release\$tfm-windows\Consumer.exe") $dir }
    finally { $env:DOTNET_ROLL_FORWARD = $previous }
}

# 1. Le demarrage rapide du README, tel quel, en .NET 8 et .NET 10.
foreach ($tfm in 'net8.0', 'net10.0') {
    Test-Scenario "wpf-quickstart-$tfm" {
        param($dir)
        $project = New-WpfApp $dir $tfm
        Write-Utf8 (Join-Path $project 'MainWindow.xaml') (New-Window "    $flowElement")
        Write-Utf8 (Join-Path $project 'MainWindow.xaml.cs') (New-WindowCode $usings $statements)
        Build-AndProbe $project $tfm $dir
    }
}

# 2. Personnalisation par ressources, definies dans la fenetre puis dans App.xaml.
$customResources = Read-Utf8 (Join-Path $templates 'custom\Resources.xaml.txt')
foreach ($where in 'window', 'app') {
    Test-Scenario "wpf-custom-$where" {
        param($dir)
        $project = New-WpfApp $dir 'net10.0'
        Copy-Item (Join-Path $templates 'custom\Custom.cs') $project
        $resources = ''
        if ($where -eq 'window') { $resources = "    <Window.Resources>`n$customResources`n    </Window.Resources>" }
        else {
            $appXaml = Read-Utf8 (Join-Path $project 'App.xaml')
            $appXaml = $appXaml -replace '<Application\.Resources>', "<Application.Resources>`n$customResources"
            $appXaml = $appXaml -replace '(<Application x:Class="[^"]+")', "`$1 $flowNamespace"
            Write-Utf8 (Join-Path $project 'App.xaml') $appXaml
        }
        Write-Utf8 (Join-Path $project 'MainWindow.xaml') (New-Window "    <flow:FlowView x:Name=`"Flow`" FitViewOnInit=`"True`" />" '' $resources)
        Write-Utf8 (Join-Path $project 'MainWindow.xaml.cs') (New-WindowCode 'using Wpflow.Core;' '        Custom.Fill(Flow);' 'Flow' 'Custom.Check')
        Build-AndProbe $project 'net10.0' $dir
    }
}

# 3. MVVM avec les deux bibliotheques les plus repandues.
foreach ($mvvm in @(@{ Name = 'toolkit'; Package = 'CommunityToolkit.Mvvm@8.4.0' }, @{ Name = 'reactiveui'; Package = 'ReactiveUI@20.1.63' })) {
    Test-Scenario "wpf-mvvm-$($mvvm.Name)" {
        param($dir)
        $project = New-WpfApp $dir 'net10.0' @($mvvm.Package)
        Copy-Item (Join-Path $templates "mvvm\$($mvvm.Name).cs") (Join-Path $project 'ViewModels.cs')
        Copy-Item (Join-Path $templates 'mvvm\MvvmCheck.cs') $project
        $view = '    <flow:FlowView x:Name="Flow" FitViewOnInit="True" NodesSource="{Binding Nodes}" EdgesSource="{Binding Links}" ConnectCommand="{Binding ConnectCommand}" />'
        Write-Utf8 (Join-Path $project 'MainWindow.xaml') (New-Window $view)
        Write-Utf8 (Join-Path $project 'MainWindow.xaml.cs') (New-WindowCode '' '        DataContext = new MainViewModel();' 'Flow' 'MvvmCheck.Run')
        Build-AndProbe $project 'net10.0' $dir
    }
}

# 4. FlowView dans un UserControl d'une bibliotheque tierce, ressources comprises.
Test-Scenario 'wpf-library-usercontrol' {
    param($dir)
    $project = New-WpfApp $dir 'net10.0'
    Invoke-Dotnet new wpflib -n Graphs -o Graphs -f net10.0
    $library = Join-Path $dir 'Graphs'
    Remove-Item (Join-Path $library 'Class1.cs')
    Invoke-Dotnet add $library package Wpflow.Wpf --version $Version
    Copy-Item (Join-Path $templates 'library\GraphControl.xaml'), (Join-Path $templates 'library\GraphControl.xaml.cs') $library
    Invoke-Dotnet add $project reference $library
    Write-Utf8 (Join-Path $project 'MainWindow.xaml') (New-Window '    <graphs:GraphControl x:Name="Graph" />' 'xmlns:graphs="clr-namespace:Graphs;assembly=Graphs"')
    $check = '(view, check) => check("template de la bibliotheque resolu", Probe.Find<System.Windows.Controls.TextBlock>(view).Count(t => Equals(t.Tag, "library-card")) == 2)'
    Write-Utf8 (Join-Path $project 'MainWindow.xaml.cs') (New-WindowCode '' '' 'Graph.Flow' $check)
    Build-AndProbe $project 'net10.0' $dir
}

# 5. Publication autonome en un seul fichier.
Test-Scenario 'wpf-singlefile' {
    param($dir)
    $project = New-WpfApp $dir 'net10.0'
    Write-Utf8 (Join-Path $project 'MainWindow.xaml') (New-Window "    $flowElement")
    Write-Utf8 (Join-Path $project 'MainWindow.xaml.cs') (New-WindowCode $usings $statements)
    $out = Join-Path $dir 'publish'
    Invoke-Dotnet publish $project -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o $out --nologo
    if (@(Get-ChildItem $out -Filter '*.dll' | Where-Object Name -like 'Wpflow*').Count -gt 0) { throw 'les assemblies Wpflow ne sont pas integres au fichier unique' }
    Invoke-Probe (Join-Path $out 'Consumer.exe') $dir
}
