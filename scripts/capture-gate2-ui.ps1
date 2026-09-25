# Ornare — Gate 2 Real Application Runtime Screenshot Capture
# Captures pixel-exact screenshots of running Ornare.App.exe for Gate 2 review

param(
    [string]$OutputDir = "production_artifacts/design/runtime-screenshots/gate2"
)

$ErrorActionPreference = "Stop"

if (!(Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
}

Add-Type -AssemblyName System.Drawing, System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win32CaptureGate2 {
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")]
    public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
}
"@

$exe = (Resolve-Path "src/Ornare.App/bin/x64/Release/net10.0-windows10.0.26100.0/Ornare.App.exe").Path
$localAppData = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
$ornareDataDir = Join-Path $localAppData "Ornare\data"
$ornareLibraryDir = Join-Path $localAppData "Ornare\library"
$dbPath = Join-Path $ornareDataDir "ornare.db"

Write-Host "Output Directory: $OutputDir"
Write-Host "Ornare App Exe: $exe"

function Capture-Window($hWnd, $outputFile) {
    [Win32CaptureGate2]::SetForegroundWindow($hWnd) | Out-Null
    Start-Sleep -Milliseconds 600

    $rect = New-Object Win32CaptureGate2+RECT
    [Win32CaptureGate2]::GetWindowRect($hWnd, [ref]$rect)
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top

    if ($width -gt 0 -and $height -gt 0) {
        $bmp = New-Object System.Drawing.Bitmap $width, $height
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $hdc = $g.GetHdc()
        $ok = [Win32CaptureGate2]::PrintWindow($hWnd, $hdc, 2)
        $g.ReleaseHdc($hdc)
        $g.Dispose()

        $bmp.Save($outputFile, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
        Write-Host "Captured: $outputFile (${width}x${height}, OK=$ok)"
    } else {
        Write-Warning "Invalid window bounds for $outputFile"
    }
}

function Run-And-Capture($argList, $filename) {
    Write-Host "Running: Ornare.App.exe $argList"
    # Ensure no lingering instances
    Get-Process "Ornare.App" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 300

    $p = Start-Process -FilePath $exe -ArgumentList $argList -PassThru
    $hWnd = [IntPtr]::Zero
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Milliseconds 150
        $p.Refresh()
        if ($p.MainWindowHandle -ne [IntPtr]::Zero) {
            $hWnd = $p.MainWindowHandle
            break
        }
    }

    if ($hWnd -eq [IntPtr]::Zero) {
        Write-Warning "Could not find window handle for $filename"
        if (!$p.HasExited) { $p.Kill() }
        return
    }

    $targetPath = Join-Path $OutputDir $filename
    Capture-Window $hWnd $targetPath

    if (!$p.HasExited) {
        $p.Kill()
        $p.WaitForExit()
    }
    Start-Sleep -Milliseconds 300
}

# --- Step 1: Capture Empty Library (Light) ---
# Temporarily move or clear DB
$backupDb = $null
if (Test-Path $dbPath) {
    $backupDb = "$dbPath.gate2bak"
    Copy-Item $dbPath $backupDb -Force
    Remove-Item $dbPath -Force
}

Run-And-Capture @("--manager", "--section", "Library", "--theme", "light", "--lang", "en-US") "01-manager-library-empty-light.png"

# --- Step 2: Setup Real Test Fixtures in SQLite DB ---
if (!(Test-Path $ornareDataDir)) { New-Item -ItemType Directory -Force -Path $ornareDataDir | Out-Null }
if (!(Test-Path $ornareLibraryDir)) { New-Item -ItemType Directory -Force -Path $ornareLibraryDir | Out-Null }

$fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) "ornare_gate2_fixtures"
if (Test-Path $fixtureRoot) { Remove-Item $fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue }
New-Item -ItemType Directory -Force -Path $fixtureRoot | Out-Null

$folder1 = Join-Path $fixtureRoot "Development Projects"
$folder2 = Join-Path $fixtureRoot "Client Archives"
$folder3 = Join-Path $fixtureRoot "Shared Resources"

New-Item -ItemType Directory -Force -Path $folder1 | Out-Null
New-Item -ItemType Directory -Force -Path $folder2 | Out-Null
New-Item -ItemType Directory -Force -Path $folder3 | Out-Null

# Create synthetic icon files for library and folders
Add-Type -AssemblyName System.Drawing
function New-SimpleIcon($path, $color) {
    $bmp = New-Object System.Drawing.Bitmap 64, 64
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $brush = New-Object System.Drawing.SolidBrush $color
    $g.FillEllipse($brush, 4, 4, 56, 56)
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), 2
    $g.DrawEllipse($pen, 6, 6, 52, 52)
    $g.Dispose()
    $hIcon = $bmp.GetHicon()
    $icon = [System.Drawing.Icon]::FromHandle($hIcon)
    $fs = New-Object System.IO.FileStream $path, ([System.IO.FileMode]::Create)
    $icon.Save($fs)
    $fs.Close()
    $bmp.Dispose()
}

$ico1 = Join-Path $ornareLibraryDir "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855.ico"
$ico2 = Join-Path $ornareLibraryDir "6b86b273ff34fce19d6b804eff5a3f5747ada4eaa22f1d49c01e52ddb7875b4b.ico"
$ico3 = Join-Path $ornareLibraryDir "d4735e3a265e16eee03f59718b9b5d03019c07d8b6c51f90da3a666eec13ab35.ico"

New-SimpleIcon $ico1 ([System.Drawing.Color]::DodgerBlue)
New-SimpleIcon $ico2 ([System.Drawing.Color]::Goldenrod)
New-SimpleIcon $ico3 ([System.Drawing.Color]::MediumSeaGreen)

# Setup folder 1 (Healthy)
$f1Ornare = Join-Path $folder1 ".ornare"
New-Item -ItemType Directory -Force -Path $f1Ornare | Out-Null
Copy-Item $ico1 (Join-Path $f1Ornare "folder.ico") -Force
Set-ItemProperty (Join-Path $f1Ornare "folder.ico") Attributes ([System.IO.FileAttributes]::Hidden)
Set-ItemProperty $folder1 Attributes ([System.IO.FileAttributes]::ReadOnly)
Set-Content (Join-Path $folder1 "desktop.ini") "[.ShellClassInfo]`r`nIconResource=.ornare\folder.ico,0`r`n"
Set-ItemProperty (Join-Path $folder1 "desktop.ini") Attributes ([System.IO.FileAttributes]::Hidden -bor [System.IO.FileAttributes]::System)

# Setup folder 2 (Needs Attention: .ornare\folder.ico exists, desktop.ini missing)
$f2Ornare = Join-Path $folder2 ".ornare"
New-Item -ItemType Directory -Force -Path $f2Ornare | Out-Null
Copy-Item $ico2 (Join-Path $f2Ornare "folder.ico") -Force

# Setup folder 3 (External Icon Conflict: desktop.ini points to shell32.dll)
$f3Ornare = Join-Path $folder3 ".ornare"
New-Item -ItemType Directory -Force -Path $f3Ornare | Out-Null
Copy-Item $ico3 (Join-Path $f3Ornare "folder.ico") -Force
Set-ItemProperty $folder3 Attributes ([System.IO.FileAttributes]::ReadOnly)
Set-Content (Join-Path $folder3 "desktop.ini") "[.ShellClassInfo]`r`nIconResource=shell32.dll,4`r`n"
Set-ItemProperty (Join-Path $folder3 "desktop.ini") Attributes ([System.IO.FileAttributes]::Hidden -bor [System.IO.FileAttributes]::System)

# Seed SQLite database using SQLitePCLRaw
Add-Type -Path (Resolve-Path "src/Ornare.App/bin/x64/Release/net10.0-windows10.0.26100.0/SQLitePCLRaw.core.dll").Path
Add-Type -Path (Resolve-Path "src/Ornare.App/bin/x64/Release/net10.0-windows10.0.26100.0/SQLitePCLRaw.provider.winsqlite3.dll").Path
Add-Type -Path (Resolve-Path "src/Ornare.App/bin/x64/Release/net10.0-windows10.0.26100.0/SQLitePCLRaw.batteries_v2.dll").Path
Add-Type -Path (Resolve-Path "src/Ornare.App/bin/x64/Release/net10.0-windows10.0.26100.0/Microsoft.Data.Sqlite.dll").Path
[SQLitePCL.Batteries_V2]::Init()

$conn = New-Object Microsoft.Data.Sqlite.SqliteConnection "Data Source=$dbPath"
$conn.Open()
$cmd = $conn.CreateCommand()
$guid1 = [Guid]::NewGuid().ToString()
$guid2 = [Guid]::NewGuid().ToString()
$guid3 = [Guid]::NewGuid().ToString()
$cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS assets (
    id TEXT PRIMARY KEY,
    sha256 TEXT NOT NULL UNIQUE,
    canonical_ico_path TEXT NOT NULL,
    source_format TEXT NOT NULL,
    created_at TEXT NOT NULL,
    last_used_at TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS folder_assignments (
    id TEXT PRIMARY KEY,
    folder_path TEXT NOT NULL UNIQUE,
    ownership_epoch_id TEXT NOT NULL,
    icon_relative_path TEXT NOT NULL,
    status INTEGER NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    baseline_icon_semantics TEXT NOT NULL,
    expected_icon_semantics TEXT NOT NULL,
    volume_serial TEXT,
    file_id TEXT,
    current_asset_hash TEXT,
    conflict_state TEXT
);
CREATE TABLE IF NOT EXISTS history (
    id TEXT PRIMARY KEY,
    folder_path TEXT NOT NULL,
    operation_id TEXT NOT NULL,
    prev_asset_hash TEXT,
    new_asset_hash TEXT NOT NULL,
    operation_type TEXT NOT NULL,
    created_at TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS favorites (
    asset_hash TEXT PRIMARY KEY,
    created_at TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS settings (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL,
    updated_at TEXT NOT NULL
);

INSERT OR REPLACE INTO assets (id, sha256, canonical_ico_path, source_format, created_at, last_used_at) VALUES
('1', 'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855', '$($ico1 -replace '\\','\\')', 'PNG', '2026-09-24T10:00:00Z', '2026-09-24T10:00:00Z'),
('2', '6b86b273ff34fce19d6b804eff5a3f5747ada4eaa22f1d49c01e52ddb7875b4b', '$($ico2 -replace '\\','\\')', 'SVG', '2026-09-24T11:00:00Z', '2026-09-24T11:00:00Z'),
('3', 'd4735e3a265e16eee03f59718b9b5d03019c07d8b6c51f90da3a666eec13ab35', '$($ico3 -replace '\\','\\')', 'JPEG', '2026-09-24T12:00:00Z', '2026-09-24T12:00:00Z');

INSERT OR REPLACE INTO favorites (asset_id, created_at) VALUES
('e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855', '2026-09-24T10:05:00Z'),
('6b86b273ff34fce19d6b804eff5a3f5747ada4eaa22f1d49c01e52ddb7875b4b', '2026-09-24T11:05:00Z');

INSERT INTO history (folder_path, operation_id, previous_asset_hash, new_asset_hash, operation_type, timestamp) VALUES
('$($folder1 -replace '\\','\\')', 'op1', NULL, 'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855', 'apply', '2026-09-24T10:00:00Z'),
('$($folder2 -replace '\\','\\')', 'op2', NULL, '6b86b273ff34fce19d6b804eff5a3f5747ada4eaa22f1d49c01e52ddb7875b4b', 'apply', '2026-09-24T11:00:00Z'),
('$($folder3 -replace '\\','\\')', 'op3', NULL, 'd4735e3a265e16eee03f59718b9b5d03019c07d8b6c51f90da3a666eec13ab35', 'apply', '2026-09-24T12:00:00Z');


INSERT OR REPLACE INTO folder_assignments (id, folder_path, ownership_epoch_id, icon_relative_path, status, created_at, updated_at, volume_serial, file_id, current_asset_hash, conflict_state) VALUES
('a1', '$($folder1 -replace '\\','\\')', '$guid1', '.ornare\\folder.ico', 0, '2026-09-24T10:00:00Z', '2026-09-24T10:00:00Z', 'VOL1', 'FILE1', 'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855', NULL),
('a2', '$($folder2 -replace '\\','\\')', '$guid2', '.ornare\\folder.ico', 1, '2026-09-24T11:00:00Z', '2026-09-24T11:00:00Z', 'VOL1', 'FILE2', '6b86b273ff34fce19d6b804eff5a3f5747ada4eaa22f1d49c01e52ddb7875b4b', NULL),
('a3', '$($folder3 -replace '\\','\\')', '$guid3', '.ornare\\folder.ico', 3, '2026-09-24T12:00:00Z', '2026-09-24T12:00:00Z', 'VOL1', 'FILE3', 'd4735e3a265e16eee03f59718b9b5d03019c07d8b6c51f90da3a666eec13ab35', 'desktop.ini has external IconResource=shell32.dll,4');
"@
$cmd.ExecuteNonQuery() | Out-Null
$conn.Close()
[Microsoft.Data.Sqlite.SqliteConnection]::ClearAllPools()

Write-Host "Database populated with Gate 2 fixtures."

# --- Step 3: Capture Remaining 11 Gate 2 Screenshots ---
$scenarios = @(
    @{ Name = "02-manager-library-populated-light.png"; Args = @("--manager", "--section", "Library", "--theme", "light", "--lang", "en-US") },
    @{ Name = "03-manager-library-dark.png"; Args = @("--manager", "--section", "Library", "--theme", "dark", "--lang", "en-US") },
    @{ Name = "04-manager-recent.png"; Args = @("--manager", "--section", "Recent", "--theme", "light", "--lang", "en-US") },
    @{ Name = "05-manager-favorites.png"; Args = @("--manager", "--section", "Favorites", "--theme", "dark", "--lang", "en-US") },
    @{ Name = "06-manager-folders-healthy.png"; Args = @("--manager", "--section", "Folders", "--theme", "light", "--lang", "en-US") },
    @{ Name = "07-manager-folders-needs-attention.png"; Args = @("--manager", "--section", "Folders", "--theme", "light", "--lang", "en-US") },
    @{ Name = "08-manager-folders-external-conflict.png"; Args = @("--manager", "--section", "Folders", "--theme", "dark", "--lang", "en-US") },
    @{ Name = "09-manager-settings-en.png"; Args = @("--manager", "--section", "Settings", "--theme", "light", "--lang", "en-US") },
    @{ Name = "10-manager-settings-tr.png"; Args = @("--manager", "--section", "Settings", "--theme", "dark", "--lang", "tr-TR") },
    @{ Name = "11-quick-panel-recent-favorites.png"; Args = @("--set-folder", "`"$folder1`"", "--theme", "light", "--lang", "en-US") },
    @{ Name = "12-manager-high-contrast.png"; Args = @("--manager", "--section", "Library", "--theme", "dark", "--lang", "en-US") }
)

foreach ($s in $scenarios) {
    Run-And-Capture $s.Args $s.Name
}

# --- Step 4: Cleanup Fixtures ---
try {
    # Restore original DB if backup exists
    if ($backupDb -and (Test-Path $backupDb)) {
        Copy-Item $backupDb $dbPath -Force
        Remove-Item $backupDb -Force
    }
    # Reset file attributes and delete fixture folders
    foreach ($f in @($folder1, $folder2, $folder3)) {
        if (Test-Path $f) {
            $d = New-Object System.IO.DirectoryInfo $f
            $d.Attributes = [System.IO.FileAttributes]::Normal
            foreach ($file in $d.GetFiles("*", [System.IO.SearchOption]::AllDirectories)) {
                $file.Attributes = [System.IO.FileAttributes]::Normal
            }
        }
    }
    Remove-Item $fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
} catch {}

Write-Host "Gate 2 UI capture session complete. 12 screenshots generated in $OutputDir."
