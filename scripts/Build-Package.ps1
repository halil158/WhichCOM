<#
.SYNOPSIS
    Builds the WhichCOM MSIX package and signs it with the local development certificate.

.DESCRIPTION
    The package is written to the artifacts folder, which is ignored by git.
    Create the certificate first with New-DevCert.ps1. With -NoSign the package stays unsigned;
    that is what the CI build does.

.PARAMETER Platform
    x64 or ARM64. Default: the architecture of this computer.

.PARAMETER NoSign
    Skips signing.

.PARAMETER PfxPath
    Signs with a .pfx file instead of the certificate store. The password is taken from
    -Password or from the environment variable WHICHCOM_CERT_PASSWORD.

.EXAMPLE
    ./scripts/Build-Package.ps1
#>
[CmdletBinding()]
param(
    [ValidateSet('x64', 'ARM64')]
    [string]$Platform = $(if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'ARM64' } else { 'x64' }),

    [switch]$NoSign,

    [string]$PfxPath,

    [securestring]$Password
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')

$artifacts = Get-ArtifactsDirectory
$packageDirectory = Join-Path $artifacts 'package\'

if (Test-Path $packageDirectory) {
    Remove-Item $packageDirectory -Recurse -Force
}

Write-Host "Building the package (Release, $Platform)..."
dotnet build (Get-ProviderProject) `
    --configuration Release `
    -p:Platform=$Platform `
    -p:GenerateAppxPackageOnBuild=true `
    -p:AppxPackageDir=$packageDirectory `
    --nologo --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

$built = Get-ChildItem $packageDirectory -Recurse -Filter *.msix | Select-Object -First 1
if (-not $built) { throw 'The build did not produce an .msix file.' }

$package = Join-Path $artifacts "WhichCOM_$Platform.msix"
Copy-Item $built.FullName $package -Force
Remove-Item $packageDirectory -Recurse -Force

if ($NoSign) {
    Write-Host "Unsigned package: $package" -ForegroundColor Yellow
    return
}

$signtool = Get-SdkTool 'signtool.exe'
$arguments = @('sign', '/fd', 'SHA256', '/q')

if ($PfxPath) {
    if (-not $Password -and $env:WHICHCOM_CERT_PASSWORD) {
        $Password = ConvertTo-SecureString $env:WHICHCOM_CERT_PASSWORD -AsPlainText -Force
    }
    if (-not $Password) {
        throw 'A password is required for the .pfx file. Pass -Password or set WHICHCOM_CERT_PASSWORD.'
    }

    $plain = [System.Net.NetworkCredential]::new('', $Password).Password
    $arguments += @('/f', (Resolve-Path $PfxPath).Path, '/p', $plain)
}
else {
    $certificate = Get-SigningCertificate
    if (-not $certificate) {
        throw "No certificate for $($script:Publisher) was found. Run ./scripts/New-DevCert.ps1 first."
    }

    $arguments += @('/sha1', $certificate.Thumbprint)
}

& $signtool @arguments $package
if ($LASTEXITCODE -ne 0) { throw 'Signing failed.' }

Write-Host "Signed package: $package" -ForegroundColor Green
