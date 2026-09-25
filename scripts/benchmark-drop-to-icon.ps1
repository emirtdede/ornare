# Ornare — Empirical Drop-to-Explorer-Visible Icon Benchmark
# Measures 20 complete end-to-end iterations: drop/input -> decode -> contain normalization -> 10-frame ICO -> desktop.ini commit -> SHChangeNotify

param(
    [int]$Iterations = 20,
    [string]$OutputFile = "production_artifacts/performance/drop-to-icon-benchmark.json"
)

$ErrorActionPreference = "Stop"

Write-Host "Running 20 iterations of full drop-to-icon pipeline benchmark..."

$outputLines = dotnet test tests/Ornare.IntegrationTests/Ornare.IntegrationTests.csproj -c Release --filter "FullyQualifiedName~PerformanceBenchmarkTests.BenchmarkPerformanceMetrics" --logger "console;verbosity=detailed"

$p50 = $null
$p95 = $null
$min = $null
$max = $null

foreach ($line in $outputLines) {
    Write-Host $line
    if ($line -match "Total Drop-to-Visible-Icon:\s*P50\s*=\s*([\d,.]+)\s*ms\s*\|\s*P95\s*=\s*([\d,.]+)\s*ms\s*\(Min\s*=\s*([\d,.]+)\s*ms,\s*Max\s*=\s*([\d,.]+)\s*ms\)") {
        $p50 = [double]($matches[1].Replace(',', '.'))
        $p95 = [double]($matches[2].Replace(',', '.'))
        $min = [double]($matches[3].Replace(',', '.'))
        $max = [double]($matches[4].Replace(',', '.'))
    }
}

if ($p50 -eq $null) {
    throw "Failed to extract Total Drop-to-Visible-Icon metrics from test output."
}

$result = [PSCustomObject]@{
    Iterations = $Iterations
    Target = "Drop to Explorer-Visible Icon Pipeline"
    MinMs = $min
    MaxMs = $max
    P50Ms = $p50
    P95Ms = $p95
    Status = if ($p95 -lt 500.0) { "PASS (< 500 ms budget)" } else { "FAIL" }
}

$perfDir = Split-Path $OutputFile -Parent
if (!(Test-Path $perfDir)) {
    New-Item -ItemType Directory -Force -Path $perfDir | Out-Null
}

$result | ConvertTo-Json | Set-Content $OutputFile
Write-Host "`n--- Drop-to-Icon Benchmark Results ---"
Write-Host "Min: $($result.MinMs) ms"
Write-Host "Max: $($result.MaxMs) ms"
Write-Host "P50: $($result.P50Ms) ms"
Write-Host "P95: $($result.P95Ms) ms"
Write-Host "Status: $($result.Status)"
Write-Host "Saved to: $OutputFile"
