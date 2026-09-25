# Versioning and Release Policy

## 1. Product versioning

Use Semantic Versioning semantics for product releases:

- **MAJOR:** intentionally incompatible user-visible persistence/protocol behavior or major product generation;
- **MINOR:** backward-compatible feature release;
- **PATCH:** backward-compatible defect/security/reliability fix.

Windows package versions must satisfy MSIX numeric-version requirements while mapping deterministically to the product version. Document the mapping once packaging is implemented.

## 2. Pre-1.0 policy

V1 public launch should be `1.0.0`. Internal/test packages may use clearly non-public build metadata or CI build numbers. Do not publish ambiguous production-like versions before release approval.

## 3. Schema and protocol versions

Version independently where required:

- SQLite schema;
- image-pipeline/cache version;
- folder-owned asset naming semantics;
- shell-to-app activation argument schema;
- diagnostics schema.

Changing one does not automatically require a product MAJOR version, but it always requires compatibility/migration analysis.

## 4. Compatibility

Updates must preserve previously supported folder customizations unless a tested migration explicitly changes them. Never make an old folder-owned icon unreadable merely because cache/internal DB format changes.

## 5. Release lineage

Every released artifact must be traceable to:

`source commit/tag -> locked dependency graph -> CI build -> test/review evidence -> SBOM -> signed package hash`.

Do not rebuild a supposedly identical public release from a different commit and reuse the same version.

## 6. Servicing

During initial V1, the latest public release line receives fixes. If multiple supported release lines become necessary, define an explicit servicing/EOL policy before doing so.
