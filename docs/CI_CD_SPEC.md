# CI/CD Specification

CI/CD is fail-closed and evidence-producing. Release builds must be attributable to an exact source commit and locked dependency graph.

## Pull request / continuous integration

On Windows runners with the pinned toolchain:

1. checkout clean source;
2. validate `global.json` SDK and repository configuration;
3. run `dotnet tool restore` for repository-pinned build tools;
4. restore NuGet packages in locked mode once lock files exist;
5. run NuGet vulnerability audit including transitive dependencies;
6. verify no unapproved package-source change;
7. build Release x64 with warnings-as-errors/analyzers;
8. build/cross-compile ARM64 artifacts where supported;
9. run unit + non-interactive integration tests;
10. run formatting/code-style validation;
11. validate localization key parity and no hard-coded user-visible production strings according to the localization rule set;
12. validate OSS/provenance/notice inventory changes;
13. fail if generated product source/UI artifacts are detected where project policy forbids them.

Do not make GPU-specific pixel-perfect screenshots a universal PR gate. Use semantic UIA tests plus targeted visual evidence.

## Nightly / pre-release

Interactive Windows machines/VMs run:

- packaged x64 tests;
- ARM64 runtime suite on real ARM64 Windows hardware;
- `winapp ui` UIA suite;
- drag/drop/paste real-input scenarios;
- Explorer context-menu stress;
- filesystem fault-injection suite;
- image security corpus;
- clean install/update/uninstall;
- performance capture on named benchmark profiles;
- WACK for release-candidate packages;
- SBOM/native binary/license inventory generation.

26H1 qualification is performed on actual supported 26H1 hardware/architecture when available; it is not substituted with a mislabeled 25H2 VM.

## Static/security analysis

- .NET SDK analyzers are mandatory.
- C++ shell project uses `/W4` + `/WX`, MSVC `/analyze`, and the native mitigation baseline in `NATIVE_CPP_HARDENING.md`; release evidence verifies expected PE mitigations rather than assuming project defaults.
- Run an AddressSanitizer native test build on supported x64 CI/test infrastructure for shell-owned code paths; never ship sanitizer builds.
- secrets scanning must run in the hosting CI platform when available.
- a security finding is not suppressed solely to make CI green.

## Release

Release pipeline requires:

- immutable signed/tagged source commit;
- pinned SDK + locked package restore;
- clean x64 + ARM64 release outputs;
- exact package candidate tested before signing/submission;
- SBOM (SPDX-compatible) generated with the repository-pinned Microsoft `sbom-tool` or formally approved replacement;
- `THIRD-PARTY-NOTICES` generated/reviewed;
- symbols retained securely;
- artifact hashes recorded;
- signing credentials obtained only from protected CI/Store facilities;
- final package/signature verification.

## Secrets

No `.pfx` private key, Store token, MCP secret, signing password or production credential is committed or written into agent-readable repository files.
