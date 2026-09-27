param()
$ErrorActionPreference = "Stop"

$testRoot = Join-Path $env:TEMP ("ornare_rc_e2e_" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $testRoot | Out-Null
Write-Host "Test folder created at: $testRoot"

try {
    Write-Host "`n--- STEP 1: INITIAL INSPECT JSON ---"
    $out1 = & ornare inspect $testRoot --json
    $out1
    $json1 = $out1 | ConvertFrom-Json
    if ($json1.success -ne $true -or $json1.data.isCustomized -ne $false) {
        throw "Initial inspect failed or was unexpectedly customized"
    }

    Write-Host "`n--- STEP 2: APPLY DRY-RUN JSON ---"
    $out2 = & ornare apply $testRoot --color "#2563EB" --dry-run --json
    $out2
    $json2 = $out2 | ConvertFrom-Json
    if ($json2.success -ne $true -or $json2.data.plannedActions.Count -eq 0) {
        throw "Dry run failed"
    }

    Write-Host "`n--- STEP 3: REAL APPLY COLOR ---"
    $out3 = & ornare apply $testRoot --color "#2563EB" --json
    $out3
    $json3 = $out3 | ConvertFrom-Json
    if ($json3.success -ne $true -or !(Test-Path (Join-Path $testRoot ".ornare/folder.ico"))) {
        throw "Apply failed or .ornare/folder.ico missing"
    }

    Write-Host "`n--- STEP 4: INSPECT AFTER APPLY ---"
    $out4 = & ornare inspect $testRoot --json
    $out4
    $json4 = $out4 | ConvertFrom-Json
    if ($json4.data.isCustomized -ne $true) {
        throw "Inspect after apply showed not customized"
    }

    Write-Host "`n--- STEP 5: UNDO ---"
    $out5 = & ornare undo $testRoot --json
    $out5
    $json5 = $out5 | ConvertFrom-Json
    if ($json5.success -ne $true) {
        throw "Undo failed"
    }

    Write-Host "`n--- STEP 6: APPLY-LAST ---"
    $out6 = & ornare apply-last $testRoot --json
    $out6
    $json6 = $out6 | ConvertFrom-Json
    if ($json6.success -ne $true) {
        throw "Apply-last failed"
    }

    Write-Host "`n--- STEP 7: RESET ---"
    $out7 = & ornare reset $testRoot --json
    $out7
    $json7 = $out7 | ConvertFrom-Json
    if ($json7.success -ne $true) {
        throw "Reset failed"
    }

    Write-Host "`n--- STEP 8: FINAL INSPECT ---"
    $out8 = & ornare inspect $testRoot --json
    $out8
    $json8 = $out8 | ConvertFrom-Json
    if ($json8.data.isCustomized -ne $false) {
        throw "Final inspect showed customized after reset"
    }

    Write-Host "`n=== ALL INSTALLED PACKAGE CLI LIFECYCLE STEPS PASSED ==="
}
finally {
    if (Test-Path $testRoot) {
        attrib -r -s $testRoot 2>$null
        $ini = Join-Path $testRoot "desktop.ini"
        if (Test-Path $ini) {
            attrib -r -s -h $ini 2>$null
        }
        Remove-Item $testRoot -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "Test folder cleaned up: $testRoot"
    }
}
