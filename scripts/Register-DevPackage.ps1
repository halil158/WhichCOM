<#
.SYNOPSIS
    Builds the widget provider and registers it for the current user without signing.

.DESCRIPTION
    The fastest way to test a change. It needs Developer Mode and no certificate, because the
    package is registered from a folder instead of being installed from an .msix file.
    Use Build-Package.ps1 and Install-Package.ps1 to test the real, signed package.

    Every run registers a copy of the build output with a higher version number:
      - Windows treats it as an update, so widgets stay pinned to the widget board.
      - The widget host keeps files of a registered package open. Registering a copy keeps the
        build output free, so the next build does not fail.

.PARAMETER Configuration
    Build configuration. Default: Debug.

.PARAMETER Platform
    x64 or ARM64. Default: the architecture of this computer.

.PARAMETER Reset
    Removes the package and restarts the widget board before registering. Needed after a change
    to a widget definition in the manifest (sizes, customization, names): the widget board keeps
    the definition it saw first. Widgets have to be pinned again.

.EXAMPLE
    ./scripts/Register-DevPackage.ps1

.EXAMPLE
    ./scripts/Register-DevPackage.ps1 -Reset
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [ValidateSet('x64', 'ARM64')]
    [string]$Platform = $(if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'ARM64' } else { 'x64' }),

    [switch]$Reset
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')

Assert-DeveloperMode

$project = Get-ProviderProject

Write-Host "Building ($Configuration, $Platform)..."
dotnet build $project --configuration $Configuration -p:Platform=$Platform --nologo --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

$manifest = Get-ChildItem (Join-Path (Split-Path $project) "bin\$Platform\$Configuration") -Recurse -Filter AppxManifest.xml |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
if (-not $manifest) { throw 'AppxManifest.xml was not found in the build output.' }

Assert-ProviderStarts $manifest.DirectoryName

# Version parts are 16 bit numbers: days since 2026-01-01 and half seconds of the day.
$now = Get-Date
$days = [int]($now.Date - [datetime]'2026-01-01').TotalDays
$ticks = [int][Math]::Floor($now.TimeOfDay.TotalSeconds / 2)
$version = "0.1.$days.$ticks"

$root = Join-Path (Get-ArtifactsDirectory) 'dev'
$layout = Join-Path $root $version
New-Item -ItemType Directory -Force $layout | Out-Null

robocopy $manifest.DirectoryName $layout /E /XF *.stale *.pdb /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "Copying the build output failed (robocopy exit code $LASTEXITCODE)." }

$layoutManifest = Join-Path $layout 'AppxManifest.xml'
$content = Get-Content $layoutManifest -Raw
$content = [regex]::Replace($content, '(<Identity\b[^>]*\bVersion=")[^"]*(")', "`${1}$version`${2}")
Set-Content $layoutManifest $content -Encoding utf8

# A signed installation cannot be replaced by a development registration.
$installed = Get-AppxPackage -Name $script:PackageName
if ($installed -and ($Reset -or -not $installed.IsDevelopmentMode)) {
    Write-Host 'Removing the installed package. Widgets have to be pinned again.' -ForegroundColor Yellow
    Stop-Provider
    $installed | Remove-AppxPackage
}

if ($Reset) {
    Write-Host 'Restarting the widget board...'
    Restart-WidgetHost
}

Write-Host "Registering version $version..."
Add-AppxPackage -Register $layoutManifest -ForceApplicationShutdown

# Older copies are no longer needed. Files the widget host still has open are removed next time.
Get-ChildItem $root -Directory |
    Where-Object Name -ne $version |
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

Show-InstalledPackage
Write-Host ''
Write-Host 'Open the widget board (Win + W), choose "Add widgets" and look for WhichCOM.'
