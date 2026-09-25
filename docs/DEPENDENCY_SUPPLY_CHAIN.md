# Dependency and Software Supply-Chain Standard

This specification follows secure-development principles from Microsoft SDL and NIST SSDF: dependencies are inventory-controlled, version-pinned, auditable and release-gated.

## 1. Dependency admission

A new production dependency requires documented answers:

- exact problem solved;
- why Windows/.NET built-in or a small owned implementation is worse;
- project/repository owner and maintenance activity;
- exact license and file/asset coverage;
- native/transitive dependencies;
- known vulnerability state;
- package size/startup/runtime cost;
- x64/ARM64 and Native AOT impact;
- uninstall/update implications.

No package is added solely because an agent knows it or because it is popular.

For SQLite, Ornare deliberately uses `Microsoft.Data.Sqlite.Core` plus `SQLitePCLRaw.bundle_winsqlite3` so the application uses Windows system `winsqlite3.dll`, reducing shipped native code and allowing Windows servicing to update the engine. Any switch back to bundled `e_sqlite3` requires an ADR, security/size/performance comparison and x64/ARM64 evidence.

## 2. Version control

- Pin .NET SDK through `global.json` with roll-forward disabled.
- Pin direct NuGet versions using `Directory.Packages.props`.
- Generate and commit `packages.lock.json` after projects exist.
- CI restores with locked mode; unexpected graph changes fail.
- Do not consume preview/RC packages in V1 without an ADR that documents the release-blocking reason.

## 3. Vulnerability policy

- Run NuGet audit including transitives on every PR/release.
- High/Critical advisories are release-blocking.
- Moderate advisories are release-blocking unless exploitability analysis demonstrates non-applicability and a time-bounded documented exception is approved.
- Low advisories are reviewed, not silently ignored.
- Native binaries/codecs receive the same review even when NuGet metadata is incomplete.

## 4. SBOM and inventory

Release artifacts include an SPDX-compatible SBOM produced with the repository-pinned Microsoft `sbom-tool` local .NET tool (baseline 4.1.5) or a formally approved replacement, plus:

- direct/transitive NuGet inventory;
- native DLL inventory with file hashes;
- OSS license/notice inventory;
- source-reuse provenance list and commit SHAs.

The SBOM tool is build tooling only; it does not become an application dependency.

## 5. Source reuse

Before copying OSS code:

1. record URL + exact commit SHA + file path;
2. preserve license/copyright requirements;
3. audit code in context rather than wholesale copy;
4. remove unrelated architecture/assets;
5. record modifications;
6. add regression/behavior tests;
7. update `THIRD-PARTY-NOTICES.md`.

## 6. Package sources

V1 uses explicit trusted package sources from `NuGet.Config`. Do not add ad-hoc feeds, local unsigned package sources, or agent-created mirrors to make restore succeed.

## 7. Release provenance

Release package, symbols, SBOM and notices must be attributable to the exact source commit/tag and build configuration. Signing secrets never enter the repository or agent-visible plain-text files.
