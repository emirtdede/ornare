# Native C++ Shell-Extension Hardening

The Explorer shell extension is the highest blast-radius Ornare binary because Windows loads it into `explorer.exe`. These controls are release-blocking unless an ADR documents a platform/toolchain incompatibility and an equivalent mitigation.

## Build baseline

For project-owned C++ code:

- C++ standard: use the minimum modern standard required by the implementation; do not chase language novelty.
- `/W4` and `/WX` for compiler warnings.
- linker warnings treated as errors for project-owned release output where supported.
- `/GS` stack protection.
- `/sdl` additional MSVC security checks.
- compiler `/guard:cf` and linker `/GUARD:CF`.
- `/DYNAMICBASE` and `/HIGHENTROPYVA` on 64-bit targets.
- `/NXCOMPAT`.
- `/CETCOMPAT` when supported/compatible for the target binary and toolchain.
- release optimization must not suppress diagnostics or security checks merely to reduce size.

Do not copy these flags blindly into unrelated third-party build systems. Verify the actual final PE characteristics/load configuration.

## Static/dynamic analysis

- Run MSVC `/analyze` in CI or pre-release on shell-owned native code.
- Maintain an x64 AddressSanitizer test configuration where the current MSVC toolchain supports the exercised native paths.
- Use Application Verifier or equivalent targeted native runtime diagnostics when investigating heap/handle/COM lifetime failures.
- Any Explorer crash/hang attributable to Ornare is P0 and requires dump/root-cause analysis before release.

## ABI/lifetime rules

- No exception crosses a COM ABI boundary.
- Every COM/Win32 failure is checked and translated to a deterministic result.
- Prefer RAII (`wil`, WRL/C++/WinRT smart wrappers, or small owned wrappers chosen deliberately) over raw ownership.
- Do not add a large native framework merely for convenience.
- Keep callbacks bounded and side-effect-light; no database, UI initialization, imaging, network, or recursive filesystem traversal.
- Avoid static initialization that can run under Explorer loader-sensitive paths.
- `DllMain` performs no expensive/blocking work.

## Release evidence

Gate 5 evidence records:

- exact compiler/linker versions;
- effective command-line or project security settings;
- final DLL architecture and hashes;
- PE mitigation verification (CFG/ASLR/DEP/CET as applicable);
- `/analyze` result;
- sanitizer/native diagnostic result for the qualified test configuration;
- Explorer stress result and crash/hang attribution summary.
