param([string]$Root = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'

$required = @(
  'AGENTS.md','GOAL_PROMPT.md','LICENSE.md','global.json','Directory.Build.props','Directory.Packages.props','NuGet.Config',
  'THIRD-PARTY-NOTICES.md','docs/00_INDEX.md','docs/PRODUCT_SPEC.md','docs/ARCHITECTURE.md','docs/LOGIC_INVARIANTS.md','docs/STATE_MACHINES.md',
  'docs/REQUIREMENTS_TRACEABILITY.md','docs/ENGINEERING_STANDARDS.md','docs/QUALITY_GATES.md','docs/VISUAL_REFERENCE_SPEC.md',
  'docs/UI_REVIEW_RUBRIC.md','docs/THREAT_MODEL.md','docs/FAULT_INJECTION_TESTS.md','docs/DEPENDENCY_SUPPLY_CHAIN.md',
  'docs/PLATFORM_COMPATIBILITY.md','docs/TEST_PLAN.md','docs/ACCEPTANCE_CRITERIA.md',
  'docs/NATIVE_CPP_HARDENING.md','docs/SQLITE_PROVIDER.md','docs/RELEASE_EVIDENCE_MODEL.md','docs/PRE_CODE_FINAL_AUDIT.md',
  '.agents/rules/05-requirements-traceability.md','.agents/rules/65-quality-gates.md',
  '.agents/workflows/vertical-slice.md','.agents/workflows/quality-gate.md',
  'templates/Directory.Packages.props.reference'
)

$errors = New-Object System.Collections.Generic.List[string]
foreach ($relative in $required) {
  if (-not (Test-Path (Join-Path $Root $relative))) { $errors.Add("Missing required file: $relative") }
}

$legacySqliteProvider = 'SQLitePCLRaw.provider.' + 'winsqlite3'
$legacySqliteCore = 'SQLitePCLRaw.' + 'core'
$knownContradictions = @(
  '26H1 x64 + ARM64 mandatory',
  '26H1 x64 and ARM64 mandatory',
  'restore the old desktop.ini file wholesale',
  'restore the full desktop.ini snapshot over the live file',
  $legacySqliteProvider,
  $legacySqliteCore
)

$scanRoots = @(
  (Join-Path $Root 'AGENTS.md'),
  (Join-Path $Root 'GOAL_PROMPT.md'),
  (Join-Path $Root 'docs'),
  (Join-Path $Root '.agents\rules'),
  (Join-Path $Root '.agents\workflows'),
  (Join-Path $Root 'templates\Directory.Packages.props.reference')
)
$scanFiles = foreach ($item in $scanRoots) {
  if (Test-Path $item -PathType Leaf) { Get-Item $item }
  elseif (Test-Path $item -PathType Container) { Get-ChildItem $item -Recurse -File -Include *.md,*.reference }
}
foreach ($file in $scanFiles) {
  $text = Get-Content -LiteralPath $file.FullName -Raw
  foreach ($phrase in $knownContradictions) {
    if ($text.IndexOf($phrase, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
      $relative = [IO.Path]::GetRelativePath($Root, $file.FullName)
      $errors.Add("Known contradictory wording [$phrase] in $relative")
    }
  }
}

# Every V1 requirement in traceability must appear in acceptance criteria.
$traceText = Get-Content -LiteralPath (Join-Path $Root 'docs\REQUIREMENTS_TRACEABILITY.md') -Raw
$acceptanceText = Get-Content -LiteralPath (Join-Path $Root 'docs\ACCEPTANCE_CRITERIA.md') -Raw
$requirementIds = [regex]::Matches($traceText, 'ORN-[A-Z]+-\d+') | ForEach-Object Value | Sort-Object -Unique
foreach ($id in $requirementIds) {
  if ($acceptanceText.IndexOf($id, [StringComparison]::Ordinal) -lt 0) {
    $errors.Add("Requirement has no acceptance-criteria reference: $id")
  }
}

# Active package baseline and reference template must not drift apart.
try {
  [xml]$activePackages = Get-Content -LiteralPath (Join-Path $Root 'Directory.Packages.props') -Raw
  [xml]$referencePackages = Get-Content -LiteralPath (Join-Path $Root 'templates\Directory.Packages.props.reference') -Raw
  $activeMap = @{}
  foreach ($node in $activePackages.Project.ItemGroup.PackageVersion) { $activeMap[$node.Include] = $node.Version }
  $referenceMap = @{}
  foreach ($node in $referencePackages.Project.ItemGroup.PackageVersion) { $referenceMap[$node.Include] = $node.Version }
  if ($activeMap.Count -ne $referenceMap.Count) { $errors.Add('Package baseline/reference package count differs.') }
  foreach ($name in $activeMap.Keys) {
    if (-not $referenceMap.ContainsKey($name) -or $referenceMap[$name] -ne $activeMap[$name]) {
      $errors.Add("Package baseline/reference drift: $name active=$($activeMap[$name]) reference=$($referenceMap[$name])")
    }
  }
} catch {
  $errors.Add("Package baseline XML validation failed: $($_.Exception.Message)")
}

if ($errors.Count -gt 0) {
  $errors | ForEach-Object { Write-Error $_ }
  throw "Specification audit failed with $($errors.Count) finding(s)."
}

Write-Host "Specification audit passed: $($requirementIds.Count) traced requirements have acceptance references; package baselines align; required enterprise controls are present; no known contradiction pattern matched."
