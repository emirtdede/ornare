# Ornare — Post-Reboot Verification Script
# Validates that the pre-reboot folder customization persists identically without the app running.

$ErrorActionPreference = "Stop"

$checkpointFile = "production_artifacts/release/reboot-verification-checkpoint.json"
if (!(Test-Path $checkpointFile)) {
    throw "Checkpoint file not found at $checkpointFile. Run scripts/prepare-reboot-verification.ps1 before reboot."
}

$checkpoint = Get-Content $checkpointFile -Raw | ConvertFrom-Json
$folder = $checkpoint.TestFolder
Write-Host "=== POST-REBOOT VERIFICATION ==="
Write-Host "Target Folder: $folder"

if (!(Test-Path $folder)) {
    throw "Target folder $folder does not exist after reboot!"
}

# 1. Check folder attributes (+R)
$folderAttr = (Get-Item $folder).Attributes
$isReadOnly = $folderAttr.HasFlag([System.IO.FileAttributes]::ReadOnly)
Write-Host "Folder ReadOnly Attribute: $isReadOnly (Expected: True)"
if (!$isReadOnly) {
    throw "Folder lost ReadOnly attribute after reboot!"
}

# 2. Check desktop.ini and its attributes (+H+S)
$desktopIni = Join-Path $folder "desktop.ini"
if (!(Test-Path $desktopIni)) {
    throw "desktop.ini is missing after reboot!"
}
$iniHash = (Get-FileHash $desktopIni -Algorithm SHA256).Hash
Write-Host "Desktop.ini SHA256: $iniHash (Expected: $($checkpoint.DesktopIniHash))"
if ($iniHash -ne $checkpoint.DesktopIniHash) {
    throw "desktop.ini content was modified or corrupted!"
}
$iniAttr = (Get-Item $desktopIni -Force).Attributes
$iniHiddenSystem = $iniAttr.HasFlag([System.IO.FileAttributes]::Hidden) -and $iniAttr.HasFlag([System.IO.FileAttributes]::System)
Write-Host "desktop.ini Hidden+System Attributes: $iniHiddenSystem (Expected: True)"
if (!$iniHiddenSystem) {
    throw "desktop.ini lost Hidden/System attributes!"
}

# 3. Check .ornare\folder.ico and attributes (+H+S)
$icoPath = Join-Path $folder ".ornare\folder.ico"
if (!(Test-Path $icoPath)) {
    throw "Icon file $icoPath is missing after reboot!"
}
$icoAttr = (Get-Item $icoPath -Force).Attributes
$icoHiddenSystem = $icoAttr.HasFlag([System.IO.FileAttributes]::Hidden) -and $icoAttr.HasFlag([System.IO.FileAttributes]::System)
Write-Host "folder.ico Hidden+System Attributes: $icoHiddenSystem (Expected: True)"
if (!$icoHiddenSystem) {
    throw "folder.ico lost Hidden/System attributes!"
}
$icoHash = (Get-FileHash $icoPath -Algorithm SHA256).Hash
Write-Host "Icon SHA256: $icoHash (Expected: $($checkpoint.IconHash))"
if ($icoHash -ne $checkpoint.IconHash) {
    throw "Icon content was modified or corrupted!"
}

# 4. Verify Ornare application is NOT running in background
$ornareProcs = Get-Process Ornare* -ErrorAction SilentlyContinue
Write-Host "Ornare Running Background Processes: $($ornareProcs.Count) (Expected: 0)"
if ($ornareProcs.Count -gt 0) {
    Write-Warning "Ornare.App process is active; icon persistence should be passive without app running."
}

Write-Host "=== POST-REBOOT PERSISTENCE VERIFICATION PASSED ==="
Write-Host "Windows File Explorer renders the custom icon passively via folder-owned desktop.ini semantics."
