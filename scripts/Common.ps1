# Shared helpers for the package scripts. Dot-source this file.

$script:PackageName = 'Yigisoft.WhichCOM'
$script:Publisher = 'CN=Yigisoft'
$script:ProviderProcess = 'WhichCOM.WidgetProvider'
$script:CertificateFriendlyName = 'WhichCOM development certificate'
$script:CodeSigningUsage = '1.3.6.1.5.5.7.3.3'

# Newest valid code signing certificate for the package publisher in the user's store.
function Get-SigningCertificate {
    Get-ChildItem 'Cert:\CurrentUser\My' |
        Where-Object {
            $_.Subject -eq $script:Publisher -and
            $_.HasPrivateKey -and
            $_.NotAfter -gt (Get-Date) -and
            $_.EnhancedKeyUsageList.ObjectId -contains $script:CodeSigningUsage
        } |
        Sort-Object NotAfter -Descending |
        Select-Object -First 1
}

function Test-PrivateKeyExportable($Certificate) {
    try {
        $key = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($Certificate)
        $key -is [System.Security.Cryptography.RSACng] -and
            ($key.Key.ExportPolicy -band [System.Security.Cryptography.CngExportPolicies]::AllowExport) -ne 0
    }
    catch {
        $false
    }
}

function Test-CertificateTrusted($Certificate) {
    foreach ($store in 'Cert:\LocalMachine\TrustedPeople', 'Cert:\LocalMachine\Root') {
        if (Get-ChildItem $store | Where-Object Thumbprint -eq $Certificate.Thumbprint) {
            return $true
        }
    }
    $false
}

function Test-Administrator {
    $identity = [System.Security.Principal.WindowsIdentity]::GetCurrent()
    [System.Security.Principal.WindowsPrincipal]::new($identity).IsInRole(
        [System.Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-RepoRoot {
    Split-Path -Parent $PSScriptRoot
}

function Get-ProviderProject {
    Join-Path (Get-RepoRoot) 'src\WhichCOM.WidgetProvider\WhichCOM.WidgetProvider.csproj'
}

function Get-ArtifactsDirectory {
    $directory = Join-Path (Get-RepoRoot) 'artifacts'
    New-Item -ItemType Directory -Force $directory | Out-Null
    $directory
}

function Test-DeveloperMode {
    $key = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock' -ErrorAction SilentlyContinue
    $key -and $key.AllowDevelopmentWithoutDevLicense -eq 1
}

function Assert-DeveloperMode {
    if (-not (Test-DeveloperMode)) {
        throw 'Developer Mode is off. Turn it on in Settings > System > For developers, then run this script again.'
    }
}

function Stop-Provider {
    Get-Process $script:ProviderProcess -ErrorAction SilentlyContinue | Stop-Process -Force
}

function Show-InstalledPackage {
    $package = Get-AppxPackage -Name $script:PackageName
    if ($package) {
        Write-Host "Installed: $($package.Name) $($package.Version) ($($package.Architecture), signature: $($package.SignatureKind))" -ForegroundColor Green
    }
    else {
        Write-Host "$($script:PackageName) is not installed." -ForegroundColor Yellow
    }
}

# signtool.exe and friends come from the Microsoft.Windows.SDK.BuildTools NuGet package,
# so the Windows SDK does not have to be installed.
function Get-SdkTool([string]$Name) {
    $architecture = if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'arm64' } else { 'x64' }

    $packages = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget\packages' }
    $tool = Get-ChildItem (Join-Path $packages 'microsoft.windows.sdk.buildtools') -Recurse -Filter $Name -ErrorAction SilentlyContinue |
        Where-Object { $_.Directory.Name -eq $architecture } |
        Sort-Object FullName -Descending |
        Select-Object -First 1

    if (-not $tool) {
        throw "$Name was not found. Build the solution once so that the NuGet packages are restored."
    }

    $tool.FullName
}
