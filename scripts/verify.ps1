param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [string]$Solution = ''
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($Solution)) { $Solution = Join-Path $Root 'Ornare.slnx' }

if (-not (Test-Path $Solution)) {
    throw "Solution not found: $Solution. Run Antigravity /bootstrap first."
}

& (Join-Path $PSScriptRoot 'spec-audit.ps1') -Root $Root

dotnet restore $Solution --locked-mode
dotnet build $Solution -c Release --no-restore
dotnet test $Solution -c Release --no-build

dotnet package list --project $Solution --vulnerable --include-transitive --no-restore --format json --output-version 1

Write-Host 'Automated verification completed. This is not release certification: UI, Explorer, ARM64 runtime, WACK, performance, security and evidence gates remain mandatory.'
