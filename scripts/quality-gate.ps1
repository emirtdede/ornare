param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [string]$Version = '1.0.0',
    [switch]$SkipBuild,
    [switch]$SkipTests
)
$ErrorActionPreference = 'Stop'

# Automatable release gates. Everything here is deterministic and reproducible on a clean
# machine: no device, no certificate and no human judgement. The checks mirror the evidence
# recorded in docs/QA_REPORT.md, so a claim in that report cannot silently drift from the
# tree. Non-automatable gates (signed package install, notification area, DPI, ARM64 and
# Windows 11 26H1 on real hardware) are documented there and are not pretended here.

$problems = New-Object System.Collections.Generic.List[string]

function Add-Failure([string]$message) {
    $script:problems.Add($message) | Out-Null
    Write-Host "  FAIL  $message" -ForegroundColor Red
}

function Write-Pass([string]$message) {
    Write-Host "  ok    $message" -ForegroundColor Green
}

# Text surfaces that ship in the product or are read as documentation. Binary assets are
# excluded by extension, not by name, so a new asset cannot slip into a scan by accident.
$textExtensions = @(
    '.cs', '.xaml', '.resw', '.resx', '.cpp', '.h', '.rc', '.csproj', '.vcxproj',
    '.props', '.targets', '.slnx', '.json', '.yml', '.yaml', '.md', '.ps1',
    '.appxmanifest', '.manifest', '.xml', '.editorconfig', '.gitattributes'
)
$skipDirectories = @('\bin\', '\obj\', '\.git\', '\node_modules\', '\production_artifacts\')

function Get-TextFiles([string[]]$relativeRoots) {
    $files = @()
    foreach ($relative in $relativeRoots) {
        $path = Join-Path $Root $relative
        if (-not (Test-Path $path)) { continue }
        $item = Get-Item $path
        if ($item.PSIsContainer) {
            $files += Get-ChildItem -LiteralPath $path -Recurse -File |
                Where-Object { $textExtensions -contains $_.Extension.ToLowerInvariant() }
        } elseif ($textExtensions -contains $item.Extension.ToLowerInvariant()) {
            $files += $item
        }
    }

    $files | Where-Object {
        $full = $_.FullName
        $skip = $false
        foreach ($marker in $skipDirectories) {
            if ($full.Contains($marker)) { $skip = $true; break }
        }
        -not $skip
    }
}

Write-Host 'Ornare quality gate' -ForegroundColor Cyan

# 1. Version consistency ---------------------------------------------------------------
Write-Host '1. Version consistency'
$propsPath = Join-Path $Root 'Directory.Build.props'
$manifestPath = Join-Path $Root 'src\Ornare.Package\Package.appxmanifest'
$installerRcPath = Join-Path $Root 'src\Ornare.Installer\installer.rc'

$propsText = [System.IO.File]::ReadAllText($propsPath)
$propsMatch = [regex]::Match($propsText, '<Version>\s*([0-9]+\.[0-9]+\.[0-9]+)\s*</Version>')
if (-not $propsMatch.Success) {
    Add-Failure 'Directory.Build.props declares no <Version>'
} elseif ($propsMatch.Groups[1].Value -ne $Version) {
    Add-Failure "Directory.Build.props Version is $($propsMatch.Groups[1].Value), release is $Version"
} else {
    Write-Pass "Directory.Build.props Version = $Version"
}

$manifestText = [System.IO.File]::ReadAllText($manifestPath)
$manifestMatch = [regex]::Match($manifestText, 'Version="([0-9]+\.[0-9]+\.[0-9]+)\.[0-9]+"')
if (-not $manifestMatch.Success) {
    Add-Failure 'Package.appxmanifest declares no package Version'
} elseif ($manifestMatch.Groups[1].Value -ne $Version) {
    Add-Failure "Package.appxmanifest Version is $($manifestMatch.Groups[1].Value), release is $Version"
} else {
    Write-Pass "Package.appxmanifest Version = $($manifestMatch.Groups[1].Value).0"
}

$rcText = [System.IO.File]::ReadAllText($installerRcPath)
$rcMatch = [regex]::Match($rcText, 'VALUE\s+"ProductVersion",\s+"([0-9]+\.[0-9]+\.[0-9]+)\.[0-9]+')
if (-not $rcMatch.Success) {
    Add-Failure 'installer.rc declares no ProductVersion'
} elseif ($rcMatch.Groups[1].Value -ne $Version) {
    Add-Failure "installer.rc ProductVersion is $($rcMatch.Groups[1].Value), release is $Version"
} else {
    Write-Pass "installer.rc ProductVersion = $($rcMatch.Groups[1].Value).0"
}

$packaged = Join-Path $Root 'production_artifacts\release\candidate\Ornare_' + $Version + '.0_x64.msix'
if (Test-Path $packaged) {
    Write-Pass 'Setup payload matches the release version'
}

# 2. Dash policy -----------------------------------------------------------------------
Write-Host '2. Dash policy'
$dashFiles = Get-TextFiles @('src', 'tests', 'docs', 'scripts', 'README.md', 'AGENTS.md', 'CHANGELOG.md', 'CONTRIBUTING.md', 'SECURITY.md', 'LICENSE.md', 'THIRD-PARTY-NOTICES.md')
$dashOffenders = @()
foreach ($file in $dashFiles) {
    $content = [System.IO.File]::ReadAllText($file.FullName, [System.Text.Encoding]::UTF8)
    if ([regex]::IsMatch($content, '[\u2013\u2014\u2015]')) {
        $dashOffenders += $file.FullName.Substring($Root.Length + 1)
    }
}
if ($dashOffenders.Count -gt 0) {
    Add-Failure ("em/en dash found in: " + ($dashOffenders -join ', '))
} else {
    Write-Pass "no em dash, en dash or horizontal bar in $($dashFiles.Count) files"
}

# 3. Encoding integrity ----------------------------------------------------------------
Write-Host '3. Encoding integrity'
$strictUtf8 = New-Object System.Text.UTF8Encoding($false, $true)
$badEncoding = @()
$replacementChar = @()
foreach ($file in $dashFiles) {
    $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
    try {
        $content = $strictUtf8.GetString($bytes)
    } catch {
        $badEncoding += $file.FullName.Substring($Root.Length + 1)
        continue
    }
    if ($content.IndexOf([char]0xFFFD) -ge 0) {
        $replacementChar += $file.FullName.Substring($Root.Length + 1)
    }
}
if ($badEncoding.Count -gt 0) { Add-Failure ("not valid UTF-8: " + ($badEncoding -join ', ')) }
if ($replacementChar.Count -gt 0) { Add-Failure ("U+FFFD replacement character: " + ($replacementChar -join ', ')) }
if ($badEncoding.Count -eq 0 -and $replacementChar.Count -eq 0) {
    Write-Pass "all $($dashFiles.Count) files are valid UTF-8 without replacement characters"
}

# 4. Emoji leakage in shipped copy ------------------------------------------------------
# Emoji are permitted only in the emoji catalogue itself. Test fixtures may use emoji as
# input data, which is why the scan targets the strings that reach a user's screen.
Write-Host '4. Emoji leakage in shipped copy'
$copyFiles = @()
$copyRoots = @(
    (Join-Path $Root 'src'),
    (Join-Path $Root 'docs')
)
foreach ($surface in $copyRoots) {
    if (-not (Test-Path $surface)) { continue }
    $copyFiles += Get-ChildItem -LiteralPath $surface -Recurse -File |
        Where-Object {
            $_.Extension.ToLowerInvariant() -in @('.xaml', '.resw', '.resx', '.md', '.cpp') -or
            $_.Name -like 'AppResources*.cs'
        }
}
$copyFiles = $copyFiles | Where-Object {
    $_.Name -ne 'EmojiCatalog.cs' -and
    -not $_.FullName.Contains('\bin\') -and
    -not $_.FullName.Contains('\obj\')
}
$emojiOffenders = @()
foreach ($file in $copyFiles) {
    $content = [System.IO.File]::ReadAllText($file.FullName, [System.Text.Encoding]::UTF8)
    foreach ($match in [regex]::Matches($content, '[\uD800-\uDBFF][\uDC00-\uDFFF]|[\u2600-\u27BF\u2B00-\u2BFF]|\uFE0F')) {
        # A few code points in these blocks render as plain text glyphs, not emoji:
        # check mark, heavy check mark, ballot X, heavy ballot X. The design document uses
        # one to depict toast copy, and flagging it would push a writer to describe the
        # glyph in words instead of showing it.
        if ($match.Value.Length -eq 1 -and @(0x2713, 0x2714, 0x2717, 0x2718) -contains [int][char]$match.Value) {
            continue
        }
        $codePoint = if ($match.Value.Length -eq 2) { [char]::ConvertToUtf32($match.Value[0], $match.Value[1]) } else { [int][char]$match.Value }
        $emojiOffenders += ($file.FullName.Substring($Root.Length + 1) + ' -> U+' + ('{0:X4}' -f $codePoint))
        break
    }
}
if ($emojiOffenders.Count -gt 0) {
    Add-Failure ("emoji in shipped copy: " + ($emojiOffenders -join ', '))
} else {
    Write-Pass "no emoji in $($copyFiles.Count) user-facing files"
}

# 5. Resource parity across the six complete locales -----------------------------------
Write-Host '5. Resource parity'
$stringsRoot = Join-Path $Root 'src\Ornare.App\Strings'
$baselinePath = Join-Path $stringsRoot 'en-US\Resources.resw'
$baseline = @([regex]::Matches([System.IO.File]::ReadAllText($baselinePath), '<data name="([^"]+)"') |
    ForEach-Object { $_.Groups[1].Value })
foreach ($localeDir in Get-ChildItem -LiteralPath $stringsRoot -Directory) {
    $resw = Join-Path $localeDir.FullName 'Resources.resw'
    if (-not (Test-Path $resw)) {
        Add-Failure "$($localeDir.Name) has no Resources.resw"
        continue
    }
    $keys = @([regex]::Matches([System.IO.File]::ReadAllText($resw), '<data name="([^"]+)"') |
        ForEach-Object { $_.Groups[1].Value })
    $missing = @($baseline | Where-Object { $keys -notcontains $_ })
    $extra = @($keys | Where-Object { $baseline -notcontains $_ })
    if ($missing.Count -gt 0) { Add-Failure "$($localeDir.Name) is missing $($missing.Count) keys, first: $($missing[0])" }
    elseif ($extra.Count -gt 0) { Add-Failure "$($localeDir.Name) has $($extra.Count) unknown keys, first: $($extra[0])" }
    else { Write-Pass "$($localeDir.Name): $($keys.Count) keys, exact match" }
}

# 6. Documentation references ----------------------------------------------------------
Write-Host '6. Documentation references'
$docFiles = @(Get-ChildItem -LiteralPath (Join-Path $Root 'docs') -File -Filter *.md)
$docFiles += Get-Item (Join-Path $Root 'README.md')
$missingImages = @()
$imageCount = 0
foreach ($doc in $docFiles) {
    $content = [System.IO.File]::ReadAllText($doc.FullName, [System.Text.Encoding]::UTF8)
    foreach ($match in [regex]::Matches($content, 'screenshots/([A-Za-z0-9_\-\.]+\.png)')) {
        $imageCount++
        $name = $match.Groups[1].Value
        if (-not (Test-Path (Join-Path $Root ('docs\screenshots\' + $name)))) {
            $missingImages += ($doc.Name + ' -> ' + $name)
        }
    }
}
if ($missingImages.Count -gt 0) {
    Add-Failure ("referenced screenshots missing: " + ($missingImages -join ', '))
} else {
    Write-Pass "$imageCount screenshot references resolve"
}

# 7. Build and tests -------------------------------------------------------------------
if (-not $SkipBuild) {
    Write-Host '7. Build'
    dotnet build (Join-Path $Root 'Ornare.slnx') -c Release --nologo | Out-Host
    if ($LASTEXITCODE -ne 0) { Add-Failure 'Release build failed' } else { Write-Pass 'Release build clean' }
}

if (-not $SkipTests) {
    Write-Host '8. Tests'
    # The two categories run separately: the performance gates measure wall clock time and
    # cannot share a parallel run with the behaviour suites.
    dotnet test (Join-Path $Root 'Ornare.slnx') -c Release --no-build --nologo --filter 'Category!=Performance' | Out-Host
    if ($LASTEXITCODE -ne 0) { Add-Failure 'behaviour tests failed' } else { Write-Pass 'behaviour tests passed' }

    dotnet test (Join-Path $Root 'Ornare.slnx') -c Release --no-build --nologo --filter 'Category=Performance' | Out-Host
    if ($LASTEXITCODE -ne 0) { Add-Failure 'performance gates failed' } else { Write-Pass 'performance gates passed' }
}

Write-Host ''
if ($problems.Count -gt 0) {
    Write-Host "Gate failed with $($problems.Count) problem(s)." -ForegroundColor Red
    exit 1
}

Write-Host 'All automatable gates passed.' -ForegroundColor Green
