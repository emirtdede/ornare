param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [string]$Solution = ''
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($Solution)) { $Solution = Join-Path $Root 'Ornare.slnx' }
if (-not (Test-Path $Solution)) { throw "Solution not found: $Solution" }

# Establish the exact locked graph first. Package inspection must not trigger a second, unlocked restore.
dotnet restore $Solution --locked-mode

dotnet package list --project $Solution --include-transitive --no-restore

$reportDir = Join-Path $Root 'production_artifacts\quality'
New-Item -ItemType Directory -Force -Path $reportDir | Out-Null
$report = Join-Path $reportDir 'dependency-vulnerabilities.json'
dotnet package list --project $Solution --vulnerable --include-transitive --no-restore --format json --output-version 1 | Out-File -LiteralPath $report -Encoding utf8

Write-Host "Dependency vulnerability report written to $report."
Write-Host 'NuGet restore audit is the enforcement path. Also review licenses, copied source, native assets and transitive notices before release.'
