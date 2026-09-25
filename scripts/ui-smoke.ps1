param(
    [string]$App = 'Ornare',
    [string]$Root = (Split-Path -Parent $PSScriptRoot)
)
$ErrorActionPreference = 'Stop'
if (-not (Get-Command winapp -ErrorAction SilentlyContinue)) {
    throw 'winapp CLI is required for this helper.'
}

$outDir = Join-Path $Root 'production_artifacts\design\ui-captures'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$outFile = Join-Path $outDir 'ornare-smoke.png'

winapp ui inspect -a $App --interactive
winapp ui screenshot -a $App --output $outFile
Write-Host "UIA inspection completed and screenshot written to $outFile. This helper does not generate or edit product UI source."
