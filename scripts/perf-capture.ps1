param(
    [string]$ProfileName = 'Ornare-Performance'
)
$ErrorActionPreference = 'Stop'

Write-Host "Prepare a clean Release build and representative test scenario before recording: $ProfileName"
Write-Host 'Use Windows Performance Recorder (WPR) and Windows Performance Analyzer (WPA) according to docs/PERFORMANCE.md.'
Write-Host 'This helper intentionally does not invent a fixed WPR profile because provider/profile selection must match the measured scenario and installed Windows Performance Toolkit version.'
