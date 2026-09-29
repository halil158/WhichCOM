<#
.SYNOPSIS
    Draws the widget cards with made-up data in the web browser.

.DESCRIPTION
    Expands every card template with example data and opens one web page that shows all widgets
    in all sizes, in light and dark mode and in both languages. Use it to check a change to a
    template without installing the package.

    The page is an approximation. The widget board uses its own fonts and spacing, so look at the
    real widgets too before a change is finished. The page loads the Adaptive Cards renderer from
    unpkg.com and therefore needs an internet connection.

.PARAMETER NoOpen
    Writes the page without opening it.

.EXAMPLE
    ./scripts/Preview-Cards.ps1
#>
[CmdletBinding()]
param(
    [switch]$NoOpen
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')

$root = Get-RepoRoot
$page = Join-Path (Get-ArtifactsDirectory) 'preview\index.html'

dotnet run --project (Join-Path $root 'tools\WhichCOM.CardPreview') --configuration Release -- `
    (Join-Path $root 'src\WhichCOM.WidgetProvider\Templates') $page
if ($LASTEXITCODE -ne 0) { throw 'The preview could not be created.' }

if (-not $NoOpen) {
    Start-Process $page
}
