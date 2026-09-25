# Ornare — Empirical Cold Start Latency Benchmark
# Measures 20 cold launches from Process.Start to valid HWND & IsWindowVisible (Win32 API)
# Polling interval: 5 ms

param(
    [int]$Iterations = 20,
    [string]$OutputFile = "production_artifacts/performance/cold-start-benchmark.json"
)

$ErrorActionPreference = "Stop"

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win32Window {
    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")]
    public static extern bool IsWindow(IntPtr hWnd);
}
"@

$exe = (Resolve-Path "src/Ornare.App/bin/x64/Release/net10.0-windows10.0.26100.0/Ornare.App.exe").Path
$sampleFolder = (Resolve-Path ".").Path

$timings = [System.Collections.Generic.List[double]]::new()

Write-Host "Running $Iterations cold start iterations..."

for ($i = 1; $i -le $Iterations; $i++) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $p = Start-Process -FilePath $exe -ArgumentList "--set-folder", "`"$sampleFolder`"" -PassThru
    
    $hWnd = [IntPtr]::Zero
    while ($sw.ElapsedMilliseconds -lt 10000) {
        $p.Refresh()
        $handle = $p.MainWindowHandle
        if ($handle -ne [IntPtr]::Zero -and [Win32Window]::IsWindow($handle) -and [Win32Window]::IsWindowVisible($handle)) {
            $hWnd = $handle
            break
        }
        [System.Threading.Thread]::Sleep(5) # 5ms polling interval
    }
    $sw.Stop()

    $elapsedMs = $sw.Elapsed.TotalMilliseconds
    $timings.Add($elapsedMs)
    Write-Host "Run ${i}: $([math]::Round($elapsedMs, 2)) ms (HWND: $hWnd)"

    if (!$p.HasExited) {
        $p.Kill()
        $p.WaitForExit()
    }
    # Cool down
    [System.Threading.Thread]::Sleep(300)
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
    PollingIntervalMs = 5
}

$perfDir = Split-Path $OutputFile -Parent
if (!(Test-Path $perfDir)) {
    New-Item -ItemType Directory -Force -Path $perfDir | Out-Null
}

$result | ConvertTo-Json | Set-Content $OutputFile
Write-Host "`n--- Cold Start Benchmark Results ---"
Write-Host "Min: $($result.MinMs) ms"
Write-Host "Max: $($result.MaxMs) ms"
Write-Host "P50: $($result.P50Ms) ms"
Write-Host "P95: $($result.P95Ms) ms"
Write-Host "Saved to: $OutputFile"
