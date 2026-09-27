param()
$ErrorActionPreference = "Stop"

$tempRoot = Join-Path $env:TEMP ("ornare_persist_" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tempRoot | Out-Null
$folderA = Join-Path $tempRoot "TestFolderA"
New-Item -ItemType Directory -Path $folderA | Out-Null

$sourceImg = Join-Path $tempRoot "source_image.png"
# Create a dummy PNG image
Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap 256, 256
$g = [System.Drawing.Graphics]::FromImage($bmp)
$brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 37, 99, 235))
$g.FillEllipse($brush, 10, 10, 236, 236)
$g.Dispose()
$bmp.Save($sourceImg, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host "Created source image: $sourceImg"

try {
    # 1. Apply image
    Write-Host "`n1. Applying icon to TestFolderA using source image..."
    $applyRes = & ornare apply $folderA --source $sourceImg --json | ConvertFrom-Json
    if ($applyRes.success -ne $true) { throw "Apply image failed: $($applyRes | ConvertTo-Json)" }
    Write-Host "Applied successfully. Operation ID: $($applyRes.operationId)"

    # Verify folder.ico exists
    $icoPath = Join-Path $folderA ".ornare\folder.ico"
    if (!(Test-Path $icoPath)) { throw "folder.ico does not exist at $icoPath" }

    # 2. Source Deletion Persistence
    Write-Host "`n2. Deleting source image to verify Source Deletion Persistence..."
    Remove-Item $sourceImg -Force
    if (Test-Path $sourceImg) { throw "Source image still exists!" }
    $insp1 = & ornare inspect $folderA --json | ConvertFrom-Json
    if ($insp1.data.isCustomized -ne $true -or !(Test-Path $icoPath)) {
        throw "Source deletion persistence failed!"
    }
    Write-Host "Source deletion persistence VERIFIED: folder.ico remains intact and functional."

    # 3. Rename Persistence
    Write-Host "`n3. Renaming folder to verify Rename Persistence..."
    $folderARenamed = Join-Path $tempRoot "TestFolderA_Renamed"
    Rename-Item -Path $folderA -NewName "TestFolderA_Renamed"
    $icoRenamed = Join-Path $folderARenamed ".ornare\folder.ico"
    if (!(Test-Path $icoRenamed)) { throw "folder.ico not found after rename" }
    $insp2 = & ornare inspect $folderARenamed --json | ConvertFrom-Json
    if ($insp2.data.isCustomized -ne $true) {
        throw "Rename persistence failed: inspect reported not customized"
    }
    Write-Host "Rename persistence VERIFIED: FileId tracked and relative desktop.ini intact."

    # 4. Same-Volume Move Persistence
    Write-Host "`n4. Moving folder to new subfolder on same volume..."
    $subDir = Join-Path $tempRoot "SubDir"
    New-Item -ItemType Directory -Path $subDir | Out-Null
    $folderMoved = Join-Path $subDir "TestFolderA_Moved"
    Move-Item -Path $folderARenamed -Destination $folderMoved
    $icoMoved = Join-Path $folderMoved ".ornare\folder.ico"
    if (!(Test-Path $icoMoved)) { throw "folder.ico not found after same-volume move" }
    $insp3 = & ornare inspect $folderMoved --json | ConvertFrom-Json
    if ($insp3.data.isCustomized -ne $true) {
        throw "Same-volume move persistence failed!"
    }
    Write-Host "Same-volume move persistence VERIFIED: Folder assignment and icon remain valid."

    Write-Host "`n=== ALL THREE PERSISTENCE VERIFICATIONS PASSED ==="
}
finally {
    if (Test-Path $tempRoot) {
        Get-ChildItem -Path $tempRoot -Recurse | ForEach-Object {
            attrib -r -s $_.FullName 2>$null
            if ($_.Name -eq "desktop.ini") {
                attrib -r -s -h $_.FullName 2>$null
            }
        }
        Remove-Item $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
