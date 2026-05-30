param(
    [string]$Root = (Split-Path -Parent $MyInvocation.MyCommand.Path),
    [string]$PluginsPath = "time-tracking-toggl",
    [string]$OutputPath = "dist"
)

$ErrorActionPreference = "Stop"

function Test-RequiredFiles {
    param([string]$PluginFolder)

    $required = @("manifest.json", "color.png", "outline.png", "skills")
    $missing = @()

    foreach ($item in $required) {
        $fullPath = Join-Path $PluginFolder $item
        if (-not (Test-Path $fullPath)) {
            $missing += $item
        }
    }

    return $missing
}

$rootPath = Resolve-Path $Root
$pluginsRoot = Join-Path $rootPath $PluginsPath
$distRoot = Join-Path $rootPath $OutputPath

if (-not (Test-Path $pluginsRoot)) {
    throw "Plugins path not found: $pluginsRoot"
}

New-Item -ItemType Directory -Path $distRoot -Force | Out-Null

$pluginDirs = Get-ChildItem -Path $pluginsRoot -Directory

if ($pluginDirs.Count -eq 0) {
    throw "No plugin folders found under: $pluginsRoot"
}

foreach ($plugin in $pluginDirs) {
    $pluginFolder = $plugin.FullName
    $missing = Test-RequiredFiles -PluginFolder $pluginFolder

    if ($missing.Count -gt 0) {
        Write-Warning "Skipping '$($plugin.Name)' - missing: $($missing -join ', ')"
        continue
    }

    $zipName = "$($plugin.Name).zip"
    $zipPath = Join-Path $distRoot $zipName

    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }

    Push-Location $pluginFolder
    try {
        Compress-Archive -Path "manifest.json", "color.png", "outline.png", "skills" -DestinationPath $zipPath -Force
        Write-Host "Created $zipPath"
    }
    finally {
        Pop-Location
    }
}
