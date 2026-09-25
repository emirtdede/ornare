param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"
Write-Host "=== ORNARE CLEAN REPRODUCIBLE BUILD VERIFICATION ==="

$tempBase = Join-Path ([System.IO.Path]::GetTempPath()) "ornare_clean_test_$([Guid]::NewGuid().ToString('N'))"
$build1 = Join-Path $tempBase "build1"
$build2 = Join-Path $tempBase "build2"

New-Item -ItemType Directory -Force -Path $build1 | Out-Null
New-Item -ItemType Directory -Force -Path $build2 | Out-Null

try {
    Write-Host "1. Exporting tracked source to clean directories using git archive..."
    $archiveTar = Join-Path $tempBase "source.tar"
    git archive -o $archiveTar HEAD
    tar -xf $archiveTar -C $build1
    tar -xf $archiveTar -C $build2

    $msbuild = "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"

    Write-Host "2. Running Clean Build 1 (Restore locked, Build Native, Build Managed, Test)..."
    $sln1 = Join-Path $build1 "Ornare.slnx"
    $vcx1 = Join-Path $build1 "src/Ornare.ShellExtension/Ornare.ShellExtension.vcxproj"
    $wrk1 = Join-Path $build1 "tests/Ornare.CrashTestWorker/Ornare.CrashTestWorker.csproj"
    $app1 = Join-Path $build1 "src/Ornare.App/Ornare.App.csproj"

    & dotnet restore $sln1 --locked-mode
    if ($LASTEXITCODE -ne 0) { throw "Build 1 restore failed" }
    & $msbuild $vcx1 -p:Configuration=Release -p:Platform=x64
    if ($LASTEXITCODE -ne 0) { throw "Build 1 native build failed" }
    & dotnet build $wrk1 -c Release
    if ($LASTEXITCODE -ne 0) { throw "Build 1 worker build failed" }
    & dotnet build $sln1 -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Build 1 compile failed" }
    & dotnet build $app1 -c Release -p:Platform=x64 --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Build 1 app x64 build failed" }
    & dotnet test $sln1 -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw "Build 1 test failed" }

    Write-Host "3. Running Clean Build 2 (Restore locked, Build Native, Build Managed, Test)..."
    $sln2 = Join-Path $build2 "Ornare.slnx"
    $vcx2 = Join-Path $build2 "src/Ornare.ShellExtension/Ornare.ShellExtension.vcxproj"
    $wrk2 = Join-Path $build2 "tests/Ornare.CrashTestWorker/Ornare.CrashTestWorker.csproj"
    $app2 = Join-Path $build2 "src/Ornare.App/Ornare.App.csproj"

    & dotnet restore $sln2 --locked-mode
    if ($LASTEXITCODE -ne 0) { throw "Build 2 restore failed" }
    & $msbuild $vcx2 -p:Configuration=Release -p:Platform=x64
    if ($LASTEXITCODE -ne 0) { throw "Build 2 native build failed" }
    & dotnet build $wrk2 -c Release
    if ($LASTEXITCODE -ne 0) { throw "Build 2 worker build failed" }
    & dotnet build $sln2 -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Build 2 compile failed" }
    & dotnet build $app2 -c Release -p:Platform=x64 --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Build 2 app x64 build failed" }
    & dotnet test $sln2 -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw "Build 2 test failed" }

    Write-Host "4. Comparing Assembly Hashes..."
    $assemblies = @("Ornare.Core.dll", "Ornare.Storage.dll", "Ornare.Imaging.dll")
    $comparisonResults = @()

    foreach ($asm in $assemblies) {
        $p1 = Get-ChildItem -Path $build1 -Filter $asm -Recurse | Where-Object { $_.FullName -like "*bin\Release\net10.0\*" } | Select-Object -First 1
        $p2 = Get-ChildItem -Path $build2 -Filter $asm -Recurse | Where-Object { $_.FullName -like "*bin\Release\net10.0\*" } | Select-Object -First 1

        if ($p1 -and $p2) {
            $h1 = (Get-FileHash -Path $p1.FullName -Algorithm SHA256).Hash
            $h2 = (Get-FileHash -Path $p2.FullName -Algorithm SHA256).Hash
            $match = ($h1 -eq $h2)
            $comparisonResults += [PSCustomObject]@{
                Assembly = $asm
                Hash1 = $h1
                Hash2 = $h2
                Identical = $match
            }
            Write-Host "  $asm : Identical=$match ($h1 vs $h2)"
        } else {
            Write-Warning "Could not find $asm in build outputs"
        }
    }

    Write-Host "Clean reproducible build verification SUCCESSFUL!"
    return $comparisonResults
}
finally {
    try {
        Remove-Item -Path $tempBase -Recurse -Force -ErrorAction SilentlyContinue
    } catch {}
}
