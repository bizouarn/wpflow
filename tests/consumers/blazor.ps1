# Scenarios Blazor, appeles par run.ps1 (variables partagees : $root, $Version, Test-Scenario, Invoke-Dotnet...).
$accent = [char]0x00E9
$razor = Get-ReadmeBlock "D${accent}marrage rapide (Blazor)" 'razor'
$styleLink = (Get-ReadmeBlock "D${accent}marrage rapide (Blazor)" 'html').Trim()

$checkDir = Join-Path $Work 'browsercheck'
Invoke-Dotnet build (Join-Path $PSScriptRoot 'BrowserCheck') -c Release -o $checkDir --nologo
$check = Join-Path $checkDir 'BrowserCheck.dll'

# La page du graphe : le code du README, plus de quoi verifier le JSON, l'export et le mode de rendu.
function New-GraphPage([string]$pageDirective, [bool]$withRendererInfo) {
    $view = $razor.Replace('<FlowView ', '<FlowView @ref="_view" ')
    $renderer = if ($withRendererInfo) { '<p id="renderer">@RendererInfo.Name</p>' } else { '' }
    @"
$pageDirective
$view
<button id="export" @onclick="Export">export</button>
<p id="svg">@_svg</p>
<p id="json">@_json</p>
$renderer

@code {
    private FlowView? _view;
    private string _svg = "", _json = "";

    protected override void OnInitialized()
    {
        var typed = _nodes.Select(n => n with { Data = n.Data?.ToString() });
        var graph = GraphSerializer.FromJson(GraphSerializer.ToJson(typed, _edges, new Viewport(1.5, 2.5, 0.75)));
        _json = graph.Nodes.Count == 2 && graph.Edges.Count == 1 && graph.Viewport.Zoom == 0.75 ? "ok" : "different";
    }

    private void Export() => _svg = _view!.ExportSvg().Contains("<svg") ? "ok" : "vide";
}
"@
}

function Edit-File([string]$path, [string]$old, [string]$new) {
    $text = Read-Utf8 $path
    if (-not $text.Contains($old)) { throw "« $old » introuvable dans $path : le modele dotnet new a change." }
    Write-Utf8 $path ($text.Replace($old, $new))
}

function Get-FreePort {
    $listener = New-Object Net.Sockets.TcpListener([Net.IPAddress]::Loopback, 0)
    $listener.Start(); $port = $listener.LocalEndpoint.Port; $listener.Stop()
    return $port
}

# Projet neuf « Blazor Web App » vide. $pages : dossier (relatif) qui recoit Graph.razor.
function New-WebApp([string]$dir, [string[]]$arguments, [string]$graphDirective) {
    Invoke-Dotnet new blazor -n Consumer -o app -e --no-https @arguments
    Copy-Item (Join-Path $run 'nuget.config') $dir
    $app = Join-Path $dir 'app'
    $client = Join-Path $app 'Consumer.Client'
    $twoProjects = Test-Path $client
    $server = if ($twoProjects) { Join-Path $app 'Consumer' } else { $app }
    $ui = if ($twoProjects) { $client } else { Join-Path $server 'Components' }
    $packageProject = if ($twoProjects) { $client } else { $server }
    Invoke-Dotnet add $packageProject package Wpflow.Blazor --version $Version
    Write-Utf8 (Join-Path $ui 'Pages\Graph.razor') (New-GraphPage "@page `"/graph`"`n$graphDirective" $true)
    $appRazor = Get-ChildItem $server -Recurse -Filter 'App.razor' | Select-Object -First 1
    Edit-File $appRazor.FullName '</head>' "    $styleLink`n</head>"
    $layout = Get-ChildItem $app -Recurse -Filter 'MainLayout.razor' | Select-Object -First 1
    Edit-File $layout.FullName '@Body' "<nav><a id=`"to-home`" href=`"`">accueil</a> <a id=`"to-graph`" href=`"graph`">graphe</a></nav>`n@Body"
    return @{ Server = $server; AppRazor = $appRazor.FullName }
}

# Publie, lance en Production et passe le verificateur sur l'application.
function Test-PublishedServer([string]$dir, [string]$project, [string]$path, [string[]]$checkArguments = @(), [switch]$Navigate) {
    $out = Join-Path $dir 'publish'
    Invoke-Dotnet publish $project -c Release -o $out -warnaserror --nologo
    $port = Get-FreePort
    $base = "http://127.0.0.1:$port$path"
    $env:ASPNETCORE_ENVIRONMENT = 'Production'
    $server = Start-Process dotnet -ArgumentList "Consumer.dll", "--urls", "http://127.0.0.1:$port" -WorkingDirectory $out `
        -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $dir 'server.log') -RedirectStandardError (Join-Path $dir 'server.err')
    try {
        $ready = $false
        for ($i = 0; $i -lt 100 -and -not $ready; $i++) {
            try { $null = Invoke-WebRequest $base -UseBasicParsing -TimeoutSec 5; $ready = $true } catch { Start-Sleep -Milliseconds 300 }
        }
        if (-not $ready) { throw "le serveur ne repond pas sur $base" }
        if ($Navigate) { Invoke-BrowserCheck (@('--url', "${base}graph", '--navigate', $base) + $checkArguments) }
        else { Invoke-BrowserCheck (@('--url', "${base}graph") + $checkArguments) }
    }
    finally {
        if (-not $server.HasExited) { $server.Kill(); $server.WaitForExit() }
        Remove-Item Env:ASPNETCORE_ENVIRONMENT
    }
}

function Invoke-BrowserCheck([string[]]$arguments) {
    $output = @(& dotnet $check @arguments)
    if ($LASTEXITCODE -ne 0) { throw "verification navigateur :`n$($output -join "`n")" }
    $renderer = @($output | Where-Object { $_ -match '^INFO' }) -join ', ' -replace 'INFO  ', ''
    return "$(@($output | Where-Object { $_ -cmatch '^OK' }).Count) verifications $renderer"
}

Test-Scenario 'blazor-server-prerender' {
    param($dir)
    $app = New-WebApp $dir @('--interactivity', 'Server', '--all-interactive') ''
    Test-PublishedServer $dir $app.Server '/'
}

Test-Scenario 'blazor-server-sans-prerender' {
    param($dir)
    $app = New-WebApp $dir @('--interactivity', 'Server', '--all-interactive') ''
    Edit-File $app.AppRazor '<Routes @rendermode="InteractiveServer"' '<Routes @rendermode="new InteractiveServerRenderMode(prerender: false)"'
    Test-PublishedServer $dir $app.Server '/'
}

Test-Scenario 'blazor-interactivite-par-page' {
    param($dir)
    $app = New-WebApp $dir @('--interactivity', 'Server') '@rendermode InteractiveServer'
    Test-PublishedServer $dir $app.Server '/' -Navigate
}

Test-Scenario 'blazor-auto' {
    param($dir)
    $app = New-WebApp $dir @('--interactivity', 'Auto', '--all-interactive') ''
    Test-PublishedServer $dir $app.Server '/' @('--reload-renderer', 'WebAssembly')
}

Test-Scenario 'blazor-sous-chemin' {
    param($dir)
    $app = New-WebApp $dir @('--interactivity', 'Server', '--all-interactive') ''
    Edit-File $app.AppRazor '<base href="/" />' '<base href="/outil/" />'
    Edit-File (Join-Path $app.Server 'Program.cs') 'var app = builder.Build();' "var app = builder.Build();`napp.UsePathBase(`"/outil`");`napp.UseRouting();"
    Test-PublishedServer $dir $app.Server '/outil/'
}

Test-Scenario 'blazor-wasm-publie-elague' {
    param($dir)
    Invoke-Dotnet new blazorwasm -n Consumer -o app -e -f net10.0
    Copy-Item (Join-Path $run 'nuget.config') $dir
    $app = Join-Path $dir 'app'
    Invoke-Dotnet add $app package Wpflow.Blazor --version $Version
    Write-Utf8 (Join-Path $app 'Pages\Graph.razor') (New-GraphPage '@page "/graph"' $true)
    Edit-File (Join-Path $app 'wwwroot\index.html') '</head>' "    $styleLink`n</head>"
    $out = Join-Path $dir 'publish'
    Invoke-Dotnet publish $app -c Release -o $out -warnaserror -p:PublishTrimmed=true --nologo
    $port = Get-FreePort
    Invoke-BrowserCheck @('--url', "http://127.0.0.1:$port/graph", '--serve', (Join-Path $out 'wwwroot'))
}

# Pas de runtime ASP.NET Core 8 exige ici : une bibliotheque de composants net8.0 prouve la compilation sans avertissement.
Test-Scenario 'blazor-net8-compilation' {
    param($dir)
    # Le modele « razorclasslib » du SDK 10 ne propose plus net8.0 : le projet est ecrit ici.
    Copy-Item (Join-Path $run 'nuget.config') $dir
    $app = Join-Path $dir 'app'
    New-Item -ItemType Directory -Force $app | Out-Null
    Write-Utf8 (Join-Path $app 'Consumer.csproj') @"
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup><TargetFramework>net8.0</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>
  <ItemGroup><PackageReference Include="Wpflow.Blazor" Version="$Version" /></ItemGroup>
</Project>
"@
    Write-Utf8 (Join-Path $app '_Imports.razor') '@using Microsoft.AspNetCore.Components.Web'
    Write-Utf8 (Join-Path $app 'Graph.razor') (New-GraphPage '@page "/graph"' $false)
    Invoke-Dotnet build $app -c Release -warnaserror --nologo
    return 'compilation seule ; execution .NET 8 non verifiee par ce scenario'
}
