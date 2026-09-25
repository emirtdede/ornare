# Ornare — Empirical Warm Activation Latency Benchmark
# Measures 30 iterations of redirecting activation from a secondary process to an existing running Ornare instance

param(
    [int]$Iterations = 30,
    [string]$OutputFile = "production_artifacts/performance/warm-activation-benchmark.json"
)

$ErrorActionPreference = "Stop"

$exe = (Resolve-Path "src/Ornare.App/bin/x64/Release/net10.0-windows10.0.26100.0/Ornare.App.exe").Path
$baseFolder = (Resolve-Path ".").Path

Write-Host "1. Launching persistent primary Ornare instance..."
$primary = Start-Process -FilePath $exe -ArgumentList "--set-folder", "`"$baseFolder`"" -PassThru
Start-Sleep -Milliseconds 2500
$primary.Refresh()
if ($primary.HasExited) {
    throw "Primary instance failed to stay alive."
}

$timings = [System.Collections.Generic.List[double]]::new()
Write-Host "2. Running $Iterations warm activation iterations..."

try {
    for ($i = 1; $i -le $Iterations; $i++) {
        $subFolder = Join-Path $baseFolder "src"
        if ($i % 2 -eq 0) { $subFolder = Join-Path $baseFolder "tests" }

        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $sec = Start-Process -FilePath $exe -ArgumentList "--set-folder", "`"$subFolder`"" -PassThru
        
        # Wait for secondary process to complete redirect and terminate
        $sec.WaitForExit(5000)
        $sw.Stop()

        $elapsedMs = $sw.Elapsed.TotalMilliseconds
        $timings.Add($elapsedMs)
        Write-Host "Warm Iteration ${i}: $([math]::Round($elapsedMs, 2)) ms (Sec PID: $($sec.Id), ExitCode: $($sec.ExitCode))"
        [System.Threading.Thread]::Sleep(100)
    }
}
finally {
    if (!$primary.HasExited) {
        $primary.Kill()
        $primary.WaitForExit()
    }
}

$sorted = $timings | Sort-Object
$min = $sorted[0]
$max = $sorted[-1]
$p50Index = [int]($Iterations * 0.50)
$p95Index = [int]($Iterations * 0.95)
if ($p95Index -ge $Iterations) { $p95Index = $Iterations - 1 }
$p50 = $sorted[$p50Index]
$p95 = $sorted[$p95Index]

$result = [PSCustomObject]@{
    Iterations = $Iterations
    MinMs = [math]::Round($min, 2)
    MaxMs = [math]::Round($max, 2)
    P50Ms = [math]::Round($p50, 2)
    P95Ms = [math]::Round($p95, 2)
}

$perfDir = Split-Path $OutputFile -Parent
if (!(Test-Path $perfDir)) {
    New-Item -ItemType Directory -Force -Path $perfDir | Out-Null
}

$result | ConvertTo-Json | Set-Content $OutputFile
Write-Host "`n--- Warm Activation Benchmark Results ---"
Write-Host "Min: $($result.MinMs) ms"
Write-Host "Max: $($result.MaxMs) ms"
Write-Host "P50: $($result.P50Ms) ms"
Write-Host "P95: $($result.P95Ms) ms"
Write-Host "Saved to: $OutputFile"
