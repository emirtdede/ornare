# Release and Distribution Specification

## 1. Channels

Primary: Microsoft Store.

Secondary after Store path is stable: official website signed MSIX/AppInstaller if there is a justified distribution need.

Potential discoverability channel after official package exists: WinGet.

## 2. Package

Produce MSIX bundle containing x64 and ARM64 application/shell components. Manifest declares only required capabilities/extensions.

## 3. Signing

- Store MSIX release: submit the unsigned/development-signed candidate through Partner Center as required; Microsoft Store certification/publishing re-signs Store-distributed MSIX packages. Do not buy a certificate solely for Store MSIX distribution.
- Website/sideload release: use a trusted production signing solution. Prefer Microsoft Azure Artifact Signing when available/appropriate; otherwise use a currently accepted CA-backed signing option. Re-evaluate current Microsoft guidance before launch.
- EV certificates are not selected merely to bypass SmartScreen; Microsoft no longer recommends EV specifically for instant SmartScreen reputation.
- Self-signed certificates are development/test only.
- Never commit signing keys/cert private keys.

## 4. Update behavior

V1 should rely on the package/Store update model rather than introducing a custom updater. Custom update framework is out of scope unless distribution requirements prove it necessary.

## 5. Certification/release gates

A release candidate must pass:

- clean Release builds for x64/ARM64;
- unit/integration/UI suites;
- WACK;
- vulnerability audit;
- license/provenance inventory;
- golden-path matrix;
- Explorer stress test;
- startup/apply performance captures;
- Light/Dark/High Contrast review;
- English/Turkish review;
- accessibility smoke passes;
- clean install/update/uninstall tests;
- no P0/P1 defects;
- final name/Store/trademark clearance gate.

## 6. Rollback/recovery

Package update must preserve DB and existing folder customizations. Schema migrations require downgrade/repair consideration. Never ship an update that changes folder-owned file naming/desktop.ini semantics without migration tests.

## 7. Release artifacts

- MSIX bundle;
- symbols/debug artifacts in secure retention;
- SBOM/dependency inventory where tooling supports it;
- `THIRD-PARTY-NOTICES`;
- release notes;
- privacy statement;
- support/diagnostics instructions.
