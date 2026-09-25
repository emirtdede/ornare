param(
  [ValidateSet('0','1','2','3','4','5','6')][string]$Gate,
  [string]$Root = (Split-Path -Parent $PSScriptRoot)
)
$ErrorActionPreference = 'Stop'

& (Join-Path $PSScriptRoot 'spec-audit.ps1') -Root $Root

$solution = Join-Path $Root 'Ornare.slnx'
if ([int]$Gate -ge 1) {
  if (-not (Test-Path $solution)) { throw 'Gate 1+ requires Ornare.slnx. Implementation/bootstrap has not produced it yet.' }
  dotnet restore $solution --locked-mode
  dotnet build $solution -c Release --no-restore
  dotnet test $solution -c Release --no-build
  $depDir = Join-Path $Root 'production_artifacts\quality'
  New-Item -ItemType Directory -Force -Path $depDir | Out-Null
  $depReport = Join-Path $depDir 'dependency-vulnerabilities.json'
  dotnet package list --project $solution --vulnerable --include-transitive --no-restore --format json --output-version 1 | Out-File -LiteralPath $depReport -Encoding utf8
  Write-Host "Dependency vulnerability report written to $depReport. NuGet locked restore/audit warnings and errors remain the automated enforcement path; review this JSON as gate evidence."
}

Write-Host "Automatable checks for Gate $Gate completed. This is NOT gate certification. Complete the human/device/evidence requirements in docs/QUALITY_GATES.md and record them with templates/QUALITY_GATE_REPORT.md."
