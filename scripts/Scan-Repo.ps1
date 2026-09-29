<#
.SYNOPSIS
    Scans every file that git would commit for secrets and personal data.

.DESCRIPTION
    Run this before the first commit and before every push. It checks the tracked and the
    untracked (not ignored) files for:
      - certificate and key files
      - the name of the current user and of this computer
      - everything gitleaks finds with the rules in .gitleaks.toml

    Nothing is modified and nothing leaves this computer.

.EXAMPLE
    ./scripts/Scan-Repo.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    $files = @(git ls-files --cached --others --exclude-standard) | Where-Object { Test-Path $_ -PathType Leaf }
    $findings = [System.Collections.Generic.List[object]]::new()

    # 1. Files that must never be committed.
    $forbidden = '\.(pfx|p12|key|pem|snk|pvk|cer|crt|der|kdbx)$'
    foreach ($file in $files) {
        if ($file -match $forbidden) {
            $findings.Add([pscustomobject]@{ Check = 'forbidden-file'; File = $file; Line = ''; Text = 'certificate or key file' })
        }
    }

    # 2. Names taken from this computer at run time, so that they are not written into this script.
    $names = @($env:USERNAME, $env:COMPUTERNAME) |
        Where-Object { $_ -and $_.Length -ge 4 } |
        ForEach-Object { [regex]::Escape($_) }

    if ($names.Count -gt 0) {
        $hits = $files | Select-String -Pattern $names -AllMatches -ErrorAction SilentlyContinue
        foreach ($match in $hits) {
            $findings.Add([pscustomobject]@{
                Check = 'local-name'
                File  = Resolve-Path -Relative $match.Path
                Line  = $match.LineNumber
                Text  = $match.Line.Trim()
            })
        }
    }

    # 3. gitleaks.
    $gitleaks = Get-Command gitleaks -ErrorAction SilentlyContinue
    if (-not $gitleaks) {
        Write-Warning 'gitleaks is not installed; only the basic checks were run. Install: winget install Gitleaks.Gitleaks'
    }
    else {
        $report = Join-Path ([System.IO.Path]::GetTempPath()) "whichcom-gitleaks-$PID.json"
        try {
            & $gitleaks.Source dir . --no-banner --log-level error --report-format json --report-path $report --exit-code 0 | Out-Null

            $tracked = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
            foreach ($file in $files) { [void]$tracked.Add(($file -replace '\\', '/')) }

            foreach ($leak in @(Get-Content $report -Raw | ConvertFrom-Json)) {
                $relative = ($leak.File -replace '\\', '/') -replace '^\./', ''
                if ($tracked.Contains($relative)) {
                    $findings.Add([pscustomobject]@{ Check = $leak.RuleID; File = $relative; Line = $leak.StartLine; Text = $leak.Match })
                }
            }
        }
        finally {
            Remove-Item $report -ErrorAction SilentlyContinue
        }
    }

    Write-Host "Scanned $($files.Count) files."
    if ($findings.Count -eq 0) {
        Write-Host 'No secrets or personal data found.' -ForegroundColor Green
        exit 0
    }

    Write-Host "$($findings.Count) finding(s):" -ForegroundColor Yellow
    $findings | Sort-Object File, Line | Format-Table Check, File, Line, Text -AutoSize -Wrap
    exit 1
}
finally {
    Pop-Location
}
