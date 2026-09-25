param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [string]$OutputDir = (Join-Path $Root "production_artifacts/performance")
)

$ErrorActionPreference = "Stop"
$exe = Join-Path $Root "src/Ornare.App/bin/x64/Release/net10.0-windows10.0.26100.0/Ornare.App.exe"

if (!(Test-Path $exe)) {
    throw "Ornare.App.exe not found at $exe. Please compile Release first."
}

if (!(Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
}

$results = @{
    Timestamp = (Get-Date).ToString("o")
    HostEnvironment = "Windows 11 x64"
    Measurements = @{}
}

# 1. Quick Panel Cold-Start vs Manager Cold-Start
Write-Host "--- Measuring Cold-Start Latencies ---"
$measureStartup = {
    param($argsList)
    Get-Process "Ornare.App" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 300

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $p = Start-Process -FilePath $exe -ArgumentList $argsList -PassThru
    $found = $false
    for ($i = 0; $i -lt 50; $i++) {
        Start-Sleep -Milliseconds 25
        $p.Refresh()
        if ($p.MainWindowHandle -ne [IntPtr]::Zero) {
            $found = $true
            break
        }
    }
    $sw.Stop()
    if (!$p.HasExited) { $p.Kill(); $p.WaitForExit() }
    return $sw.ElapsedMilliseconds
}

# Quick Panel Cold-Start
$qpRuns = @()
for ($i = 0; $i -lt 3; $i++) {
    $ms = & $measureStartup @("--set-folder", "`"$([System.IO.Path]::GetTempPath())`"")
    $qpRuns += $ms
}
$results.Measurements["QuickPanelColdVisibleMs"] = @{
    Runs = $qpRuns
    P50 = ($qpRuns | Measure-Object -Average).Average
    Min = ($qpRuns | Measure-Object -Minimum).Minimum
    Max = ($qpRuns | Measure-Object -Maximum).Maximum
}
Write-Host "Quick Panel Cold Visible (ms): $($results.Measurements['QuickPanelColdVisibleMs'].P50)"

# Manager Cold-Start
$mgrRuns = @()
for ($i = 0; $i -lt 3; $i++) {
    $ms = & $measureStartup @("--manager")
    $mgrRuns += $ms
}
$results.Measurements["ManagerColdVisibleMs"] = @{
    Runs = $mgrRuns
    P50 = ($mgrRuns | Measure-Object -Average).Average
    Min = ($mgrRuns | Measure-Object -Minimum).Minimum
    Max = ($mgrRuns | Measure-Object -Maximum).Maximum
}
Write-Host "Manager Cold Visible (ms): $($results.Measurements['ManagerColdVisibleMs'].P50)"

# 2. Manager Warm Activation
Write-Host "--- Measuring Warm Activation Latency ---"
# Launch Instance 1
Get-Process "Ornare.App" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
$p1 = Start-Process -FilePath $exe -ArgumentList @("--manager") -PassThru
Start-Sleep -Milliseconds 800

$warmRuns = @()
for ($i = 0; $i -lt 3; $i++) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $p2 = Start-Process -FilePath $exe -ArgumentList @("--manager") -PassThru -Wait
    $sw.Stop()
    $warmRuns += $sw.ElapsedMilliseconds
}
if (!$p1.HasExited) { $p1.Kill(); $p1.WaitForExit() }

$results.Measurements["ManagerWarmActivationMs"] = @{
    Runs = $warmRuns
    P50 = ($warmRuns | Measure-Object -Average).Average
    Min = ($warmRuns | Measure-Object -Minimum).Minimum
    Max = ($warmRuns | Measure-Object -Maximum).Maximum
}
Write-Host "Manager Warm Activation (ms): $($results.Measurements['ManagerWarmActivationMs'].P50)"

# 3. Storage and Scale Benchmarks (In-Memory / Fixture DB)
Write-Host "--- Measuring Storage & Scale Benchmarks ---"
Add-Type -Path (Resolve-Path "src/Ornare.App/bin/x64/Release/net10.0-windows10.0.26100.0/SQLitePCLRaw.core.dll").Path
Add-Type -Path (Resolve-Path "src/Ornare.App/bin/x64/Release/net10.0-windows10.0.26100.0/SQLitePCLRaw.provider.winsqlite3.dll").Path
Add-Type -Path (Resolve-Path "src/Ornare.App/bin/x64/Release/net10.0-windows10.0.26100.0/SQLitePCLRaw.batteries_v2.dll").Path
Add-Type -Path (Resolve-Path "src/Ornare.App/bin/x64/Release/net10.0-windows10.0.26100.0/Microsoft.Data.Sqlite.dll").Path
Add-Type -Path (Resolve-Path "src/Ornare.Storage/bin/Release/net10.0/Ornare.Storage.dll").Path
Add-Type -Path (Resolve-Path "src/Ornare.Core/bin/Release/net10.0/Ornare.Core.dll").Path
Add-Type -Path (Resolve-Path "src/Ornare.Windows/bin/Release/net10.0-windows10.0.26100.0/Ornare.Windows.dll").Path
[SQLitePCL.Batteries_V2]::Init()

$tempDb = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "ornare_perf_scale_$([Guid]::NewGuid().ToString('N')).db")
$storage = New-Object Ornare.Storage.SqliteStorageService
$storage.InitializeAsync($tempDb).GetAwaiter().GetResult()

# Benchmark 100 Assets Insert & Fetch
$sw = [System.Diagnostics.Stopwatch]::StartNew()
for ($i = 0; $i -lt 100; $i++) {
    $sha = "hash_100_" + $i.ToString("D4")
    $ico = "C:\fake\path\" + $sha + ".ico"
    $storage.AddOrUpdateAssetAsync($sha, $ico, "PNG").GetAwaiter().GetResult() | Out-Null
}
$sw.Stop()
$results.Measurements["LibraryInsert100AssetsMs"] = $sw.ElapsedMilliseconds

$sw.Restart()
$assets100 = $storage.GetAllAssetsAsync().GetAwaiter().GetResult()
$sw.Stop()
$results.Measurements["LibraryFetch100AssetsMs"] = $sw.ElapsedMilliseconds
Write-Host "Library 100 Assets Fetch (ms): $($results.Measurements['LibraryFetch100AssetsMs'])"

# Benchmark 1,000 Assets Insert & Fetch
$sw.Restart()
for ($i = 100; $i -lt 1000; $i++) {
    $sha = "hash_1000_" + $i.ToString("D4")
    $ico = "C:\fake\path\" + $sha + ".ico"
    $storage.AddOrUpdateAssetAsync($sha, $ico, "PNG").GetAwaiter().GetResult() | Out-Null
}
$sw.Stop()
$results.Measurements["LibraryInsert900RemainingAssetsMs"] = $sw.ElapsedMilliseconds

$sw.Restart()
$assets1000 = $storage.GetAllAssetsAsync().GetAwaiter().GetResult()
$sw.Stop()
$results.Measurements["LibraryFetch1000AssetsMs"] = $sw.ElapsedMilliseconds
Write-Host "Library 1,000 Assets Fetch (ms): $($results.Measurements['LibraryFetch1000AssetsMs'])"

# Benchmark Health Scan 100 Folders & 1,000 Folders
Write-Host "--- Measuring Health Scan Performance ---"
$fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) "ornare_perf_health_fixtures"
if (Test-Path $fixtureRoot) { Remove-Item $fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue }
New-Item -ItemType Directory -Force -Path $fixtureRoot | Out-Null

$dummyIcon = Join-Path $fixtureRoot "source.ico"
[System.IO.File]::WriteAllBytes($dummyIcon, [byte[]]@(0, 0, 1, 0, 1, 0, 16, 16, 0, 0, 1, 0, 32, 0, 40, 0, 0, 0, 22, 0, 0, 0))

$folders100 = @()
for ($i = 0; $i -lt 100; $i++) {
    $f = Join-Path $fixtureRoot "Folder_$($i.ToString('D4'))"
    New-Item -ItemType Directory -Force -Path $f | Out-Null
    $sub = Join-Path $f ".ornare"
    New-Item -ItemType Directory -Force -Path $sub | Out-Null
    Copy-Item $dummyIcon (Join-Path $sub "folder.ico") -Force
    Set-Content (Join-Path $f "desktop.ini") "[.ShellClassInfo]`r`nIconResource=.ornare\folder.ico,0`r`n"
    $folders100 += $f
}

# Scan 100 folders
$sw.Restart()
$scanned100 = 0
foreach ($f in $folders100) {
    $fa = New-Object Ornare.Core.FolderAssignment
    $fa.FolderPath = $f
    $fa.CurrentAssetHash = "dummyhash"
    $health = [Ornare.Windows.FolderHealthEngine]::CheckHealth($f, $fa)
    $scanned100++
}
$sw.Stop()
$results.Measurements["HealthScan100FoldersMs"] = $sw.ElapsedMilliseconds
Write-Host "Health Scan 100 Folders (ms): $($results.Measurements['HealthScan100FoldersMs'])"

# Extrapolate/benchmark 1,000 folders calculation
$results.Measurements["HealthScan1000FoldersEstimatedMs"] = [math]::Round($sw.ElapsedMilliseconds * 10, 1)
Write-Host "Health Scan 1,000 Folders Estimated (ms): $($results.Measurements['HealthScan1000FoldersEstimatedMs'])"

# Clean up perf fixtures
try {
    Remove-Item $tempDb -Force -ErrorAction SilentlyContinue
    Remove-Item $fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
} catch {}

$outFile = Join-Path $OutputDir "gate-2-benchmark-results.json"
$results | ConvertTo-Json -Depth 5 | Set-Content $outFile -Encoding utf8
Write-Host "Gate 2 Benchmark complete! Results saved to: $outFile"
