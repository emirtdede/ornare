# Ornare Personalization V1 — Real Installed-MSIX E2E Verification
param()

$ErrorActionPreference = "Stop"

$cli = (Get-Command ornare -ErrorAction Stop).Source
Write-Host "Using installed CLI from MSIX package: $cli"

$testRoot = "C:\Users\emir\Desktop\Ornare_E2E_Test"
if (Test-Path $testRoot) { Remove-Item $testRoot -Recurse -Force }
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null

# ==============================================================================
# FLOW A: Folder -> Aqua (#00E5FF) -> Apply -> Undo -> Reset
# ==============================================================================
Write-Host "`n=== Executing FLOW A ==="
$folderA = Join-Path $testRoot "FlowA_Folder"
New-Item -ItemType Directory -Path $folderA -Force | Out-Null

$desktopIniA = Join-Path $folderA "desktop.ini"
$ornareDirA = Join-Path $folderA ".ornare"

# 1. Before Apply verification:
$beforeDesktopExists = Test-Path $desktopIniA
$beforeOrnareExists = Test-Path $ornareDirA
$inspectBefore = & ornare inspect $folderA --json | ConvertFrom-Json
Write-Host "Before Apply: Desktop.ini exists=$beforeDesktopExists, .ornare exists=$beforeOrnareExists, isCustomized=$($inspectBefore.data.isCustomized)"

if ($beforeDesktopExists -or $beforeOrnareExists -or $inspectBefore.data.isCustomized) {
    throw "Flow A Pre-Apply check failed: folder is not pristine!"
}

# 2. Apply Aqua template + Color #00E5FF:
Write-Host "Applying Flow A (ornare-soft template with Aqua #00E5FF)..."
$applyOutA = & ornare apply $folderA --template ornare-soft --color "#00E5FF" --json | ConvertFrom-Json
if (!$applyOutA.success) { throw "Flow A Apply failed: $($applyOutA.messageKey)" }

# 3. Post-Apply Verification:
$afterDesktopExists = Test-Path $desktopIniA
$afterOrnareExists = Test-Path $ornareDirA
$icoFilesA = Get-ChildItem -Path $ornareDirA -Filter *.ico -Force
$desktopContentA = Get-Content $desktopIniA -Raw
$inspectAfterApplyA = & ornare inspect $folderA --json | ConvertFrom-Json

Write-Host "Post-Apply: Desktop.ini exists=$afterDesktopExists, ICO files count=$($icoFilesA.Count), Size=$($icoFilesA[0].Length) bytes"
Write-Host "isCustomized=$($inspectAfterApplyA.data.isCustomized), State=$($inspectAfterApplyA.data.state)"
Write-Host "desktop.ini content:`n$desktopContentA"

if (!$afterDesktopExists -or $icoFilesA.Count -eq 0 -or $icoFilesA[0].Length -lt 10000 -or !$inspectAfterApplyA.data.isCustomized) {
    throw "Flow A Post-Apply failed: ICO not generated, desktop.ini missing, or not customized!"
}

# 4. Undo Verification:
Write-Host "Executing Undo on Flow A..."
$undoOutA = & ornare undo $folderA --json | ConvertFrom-Json
if (!$undoOutA.success) { throw "Flow A Undo failed: $($undoOutA.messageKey)" }
$inspectAfterUndoA = & ornare inspect $folderA --json | ConvertFrom-Json
Write-Host "Post-Undo: isCustomized=$($inspectAfterUndoA.data.isCustomized)"
if ($inspectAfterUndoA.data.isCustomized) { throw "Flow A Undo failed to uncustomize folder!" }

# 5. Apply again -> Reset Verification:
Write-Host "Re-applying Flow A configuration..."
$reApplyOut = & ornare apply $folderA --template ornare-soft --color "#00E5FF" --json | ConvertFrom-Json
Write-Host "Executing Reset on Flow A..."
$resetOutA = & ornare reset $folderA --json | ConvertFrom-Json
if (!$resetOutA.success) { throw "Flow A Reset failed: $($resetOutA.messageKey)" }
$inspectAfterResetA = & ornare inspect $folderA --json | ConvertFrom-Json
Write-Host "Post-Reset: isCustomized=$($inspectAfterResetA.data.isCustomized)"
if ($inspectAfterResetA.data.isCustomized) { throw "Flow A Reset failed to uncustomize folder!" }
Write-Host "FLOW A PASSED VERIFICATION."

# ==============================================================================
# FLOW B: Folder Template -> Status Preset (status.important) -> Apply -> Verify
# ==============================================================================
Write-Host "`n=== Executing FLOW B ==="
$folderB = Join-Path $testRoot "FlowB_Folder"
New-Item -ItemType Directory -Path $folderB -Force | Out-Null

Write-Host "Applying Flow B (status.important preset)..."
$applyOutB = & ornare apply $folderB --preset status.important --json | ConvertFrom-Json
if (!$applyOutB.success) { throw "Flow B Apply failed: $($applyOutB.messageKey)" }

$desktopIniB = Join-Path $folderB "desktop.ini"
$ornareDirB = Join-Path $folderB ".ornare"
$icoFilesB = Get-ChildItem -Path $ornareDirB -Filter *.ico -Force
$inspectAfterApplyB = & ornare inspect $folderB --json | ConvertFrom-Json

Write-Host "Flow B Post-Apply: Desktop.ini exists=$(Test-Path $desktopIniB), ICO Size=$($icoFilesB[0].Length) bytes"
Write-Host "isCustomized=$($inspectAfterApplyB.data.isCustomized), State=$($inspectAfterApplyB.data.state)"

if (!$(Test-Path $desktopIniB) -or $icoFilesB.Count -eq 0 -or !$inspectAfterApplyB.data.isCustomized) {
    throw "Flow B verification failed!"
}
Write-Host "FLOW B PASSED VERIFICATION."

# ==============================================================================
# FLOW C: Existing Custom-Image Mode -> Select/Drop Image -> Apply
# ==============================================================================
Write-Host "`n=== Executing FLOW C ==="
$folderC = Join-Path $testRoot "FlowC_Folder"
New-Item -ItemType Directory -Path $folderC -Force | Out-Null

$testImage = [System.IO.Path]::GetFullPath("src/Ornare.Package/Images/StoreLogo.png")
Write-Host "Applying Flow C (custom image: $testImage)..."
$applyOutC = & ornare apply $folderC --source $testImage --mode contain --padding 10 --json | ConvertFrom-Json
if (!$applyOutC.success) { throw "Flow C Apply failed: $($applyOutC.messageKey)" }

$desktopIniC = Join-Path $folderC "desktop.ini"
$ornareDirC = Join-Path $folderC ".ornare"
$icoFilesC = Get-ChildItem -Path $ornareDirC -Filter *.ico -Force
$inspectAfterApplyC = & ornare inspect $folderC --json | ConvertFrom-Json

Write-Host "Flow C Post-Apply: Desktop.ini exists=$(Test-Path $desktopIniC), ICO Size=$($icoFilesC[0].Length) bytes"
Write-Host "isCustomized=$($inspectAfterApplyC.data.isCustomized), State=$($inspectAfterApplyC.data.state)"

if (!$(Test-Path $desktopIniC) -or $icoFilesC.Count -eq 0 -or !$inspectAfterApplyC.data.isCustomized) {
    throw "Flow C verification failed!"
}
Write-Host "FLOW C PASSED VERIFICATION."

# ==============================================================================
# PERSISTENCE CHECKS: Source Deletion, Rename, Same-Volume Move
# ==============================================================================
Write-Host "`n=== Executing PERSISTENCE CHECKS ==="

# 1. Source Deletion Persistence:
Write-Host "1. Testing Source Deletion Persistence..."
$tempSource = Join-Path $testRoot "temp_source_to_delete.png"
Copy-Item $testImage $tempSource
$folderPersist = Join-Path $testRoot "Persist_Folder"
New-Item -ItemType Directory -Path $folderPersist -Force | Out-Null

$applyPersist = & ornare apply $folderPersist --source $tempSource --json | ConvertFrom-Json
if (!$applyPersist.success) { throw "Apply for source deletion test failed!" }

Remove-Item $tempSource -Force
Write-Host "Original source image deleted from disk."
$inspectPersist1 = & ornare inspect $folderPersist --json | ConvertFrom-Json
$icoPersist = Get-ChildItem (Join-Path $folderPersist ".ornare") -Filter *.ico -Force
Write-Host "Customization valid after source deletion: $($inspectPersist1.data.isCustomized), ICO files: $($icoPersist.Count)"
if (!$inspectPersist1.data.isCustomized -or $icoPersist.Count -eq 0) {
    throw "Source deletion persistence failed!"
}
Write-Host "Source deletion persistence PASSED."

# 2. Rename Persistence:
Write-Host "2. Testing Rename Persistence..."
$folderRenamed = Join-Path $testRoot "Persist_Folder_Renamed"
Rename-Item -Path $folderPersist -NewName "Persist_Folder_Renamed"
$inspectRename = & ornare inspect $folderRenamed --json | ConvertFrom-Json
$icoRename = Get-ChildItem (Join-Path $folderRenamed ".ornare") -Filter *.ico -Force
Write-Host "Customization valid after rename: $($inspectRename.data.isCustomized), ICO files: $($icoRename.Count)"
if (!$inspectRename.data.isCustomized -or $icoRename.Count -eq 0) {
    throw "Rename persistence failed!"
}
Write-Host "Rename persistence PASSED."

# 3. Same-Volume Move Persistence:
Write-Host "3. Testing Same-Volume Move Persistence..."
$moveTargetDir = Join-Path $testRoot "SubDir"
New-Item -ItemType Directory -Path $moveTargetDir -Force | Out-Null
$folderMoved = Join-Path $moveTargetDir "Persist_Folder_Renamed"
Move-Item -Path $folderRenamed -Destination $folderMoved
$inspectMove = & ornare inspect $folderMoved --json | ConvertFrom-Json
$icoMove = Get-ChildItem (Join-Path $folderMoved ".ornare") -Filter *.ico -Force
Write-Host "Customization valid after move: $($inspectMove.data.isCustomized), ICO files: $($icoMove.Count)"
if (!$inspectMove.data.isCustomized -or $icoMove.Count -eq 0) {
    throw "Same-volume move persistence failed!"
}
Write-Host "Same-volume move persistence PASSED."

# Clean up test root
Remove-Item $testRoot -Recurse -Force
Write-Host "`n========================================================"
Write-Host "ALL E2E FLOWS (A, B, C) AND PERSISTENCE CHECKS PASSED!"
Write-Host "========================================================"
