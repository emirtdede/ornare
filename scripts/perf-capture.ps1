param(
    [string]$ProfileName = 'Ornare-Performance'
)
$ErrorActionPreference = 'Stop'

# This helper deliberately does not ship a fixed WPR profile: the providers and stacks must
# match the scenario being measured and the Windows Performance Toolkit version installed on
# the measuring machine. It prepares the state and points at the procedure instead.

$root = Split-Path -Parent $PSScriptRoot

Write-Host "Scenario: $ProfileName"
Write-Host ''
Write-Host '1. Produce a Release build of the packaged application (a Debug build measures the wrong thing):'
Write-Host '     pwsh ./scripts/create-package.ps1'
Write-Host ''
Write-Host '2. Record with Windows Performance Recorder, for example the first-run profile:'
Write-Host '     wpr -start GeneralProfile -start CPU -filemode'
Write-Host '     # run the scenario, then:'
Write-Host '     wpr -stop ornare-startup.etl'
Write-Host ''
Write-Host '3. Open the trace in Windows Performance Analyzer and read startup, Explorer latency and'
Write-Host '   image apply latency against the thresholds in the release gates.'
Write-Host ''
Write-Host 'The automated timing limits that CI enforces are the tests marked with'
Write-Host 'Category=Performance. Run them with:'
Write-Host "     dotnet test $([System.IO.Path]::Combine($root, 'Ornare.slnx')) -c Release --filter 'Category=Performance'"
Write-Host ''
Write-Host 'Recorded measurements and their environment belong in docs/QA_REPORT.md.'
