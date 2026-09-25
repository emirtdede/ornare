# Enterprise Engineering Standards

This document is normative for all production source written for Ornare.

## 1. Core principles

1. Correctness and user-data integrity outrank convenience.
2. Prefer the smallest architecture that makes invariants explicit and testable.
3. Every side effect must have a defined failure mode, ownership model, and recovery behavior.
4. Do not duplicate platform behavior that Windows already implements reliably.
5. Avoid speculative abstractions. Introduce an interface only at a real boundary: OS/native API, persistence, imaging, time/identity, or a behavior that requires substitution in tests.
6. No hidden global mutable state.
7. No fire-and-forget work unless ownership, lifetime, cancellation, exception handling, and shutdown behavior are explicit.

## 2. C# / .NET standards

- Nullable reference types are enabled and warnings are errors.
- SDK analyzers and code style run in builds. Suppressions require a local justification or a documented project-wide rationale; never blanket-disable analyzer families.
- Public/internal APIs use descriptive domain types instead of strings/booleans where ambiguity is possible.
- Prefer immutable records/value objects for operation inputs and results where appropriate.
- Exceptions represent exceptional failures; expected user/domain outcomes use typed result/error values.
- Never use `async void` except framework event handlers. Event handlers delegate immediately to testable async methods and handle errors.
- Pass `CancellationToken` through cancellable I/O/CPU operations; do not create cancellation that can interrupt a filesystem commit after the irreversible boundary.
- Never block the UI thread with `.Result`, `.Wait()`, synchronous image decode, database work, or filesystem traversal.
- Use `ConfigureAwait` only intentionally; WinUI UI continuations must marshal explicitly where needed.
- Prefer `x:Bind` to reflection-based binding on hot/first-frame surfaces.
- Keep startup constructors cheap. Heavy initialization is explicit and lazy.
- `IDisposable`/`IAsyncDisposable` ownership is clear. Native handles use safe wrappers or deterministic RAII-style disposal.
- Do not use reflection/dynamic serialization on the startup path without a demonstrated need and AOT analysis.

## 3. C++ shell-extension standards

The shell DLL is high-risk because it runs inside Explorer.

- C++20/23 features may be used only when supported by the pinned MSVC toolchain.
- `/W4` and warnings-as-errors for project-owned code.
- Release/native shell builds must enable and verify applicable MSVC mitigations: `/GS`, `/sdl`, compiler `/guard:cf`, linker `/GUARD:CF`, `/DYNAMICBASE`, `/NXCOMPAT`, `/HIGHENTROPYVA` for 64-bit images, and `/CETCOMPAT` when compatible with the supported toolchain/architectures. Do not assume project defaults; inspect the final PE/load-config metadata in release evidence.
- Run MSVC static analysis (`/analyze`) for the shell project in CI/pre-release. Maintain an x64 AddressSanitizer test configuration for native code paths where the toolchain supports it; ASan artifacts are test-only and are never shipped.
- Prefer RAII for COM, HANDLEs, strings, and allocated memory. No naked ownership.
- Validate HRESULTs and Win32 error returns. Do not continue after an invariant-breaking failure.
- No exceptions may cross COM ABI boundaries.
- No managed runtime, imaging, DB, network, heavy logging, or recursive filesystem work inside Explorer.
- `GetState`, `GetTitle`, `GetIcon`, and other menu callbacks must be deterministic, bounded, and side-effect free apart from minimal safe platform calls.
- Invocation must revalidate selected input in the app process; data from Explorer is never trusted as a completed authorization/validation step.

## 4. File-system and persistence standards

- Canonicalize paths before authorization/operation decisions.
- Revalidate target identity immediately before mutation.
- Treat reparse points, junctions, symlinks, cloud placeholders, and race-prone path changes as explicit cases.
- Never follow an unexpected reparse point during cleanup.
- Writes use temporary files plus replace/rename where the filesystem supports safe replacement.
- A successful return is permitted only after the product's durable state is internally consistent or a recoverable journal/reconciliation record exists.
- Never delete a file merely because its name resembles an Ornare file; verify ownership/provenance.

## 5. State-machine rule

Each user-visible operation must have an explicit state model. For icon application:

`Idle -> Validating -> Preparing -> Committing -> Refreshing -> Succeeded`

Possible terminal/nonterminal branches:

- validation failure -> `Failed` with no mutation;
- preparation failure -> `Failed` with no user-visible mutation;
- commit failure -> rollback/reconciliation -> `Failed` or `NeedsRepair`;
- shell refresh uncertainty after durable commit -> `SucceededWithRefreshWarning`, never rollback a correct disk state merely because rendering confirmation is uncertain.

Cancellation is accepted during `Validating` and `Preparing`. Once the commit boundary begins, user cancellation does not interrupt it mid-write. The operation must reach a durable success state, a proven-safe semantic rollback, or an explicit recoverable `NeedsRepair`/journal state; it may never disappear into an ambiguous partial-success state.

## 6. Error handling

Every error has:

- stable internal error code;
- category;
- operation correlation ID generated locally;
- safe technical detail for diagnostics;
- localized user-facing mapping;
- retryability classification;
- whether repair/reconciliation is required.

Never display raw paths/HRESULTs in the primary UI. Details may expose technical information only on explicit user request and with privacy-aware redaction.

## 7. Threading and concurrency

- One mutation at a time per folder identity.
- Quick-panel activation is single-instance but re-entrant-safe.
- While an icon commit is in progress, a new activation is queued as one pending request; duplicate requests for the same target may be coalesced. Do not cancel an operation after commit starts.
- Database writes are serialized through a narrow storage boundary; no background polling writer.
- UI state is mutated only from the UI dispatcher/thread.

## 8. Tests and source changes

- Bug fix: add a regression test reproducing the defect before/with the fix whenever technically feasible.
- Refactor: behavior must remain covered; do not mix large unrelated refactors into a feature change.
- Platform workaround: document the Windows version/API behavior and create an ADR if it changes architecture or support policy.
- Any change to folder-owned filename format, DB schema, desktop.ini semantics, package identity, activation protocol, or image pipeline version requires migration/compatibility review.

## 9. Source hygiene

Forbidden:

- placeholders presented as completed work;
- swallowed exceptions;
- silent fallback to less secure behavior;
- unexplained magic constants;
- copy/pasted third-party code without provenance;
- broad `catch (Exception)` used to report success;
- reflection-heavy service discovery;
- god classes/pages;
- script-generated application source or XAML.

## 10. Build reproducibility

- `global.json` pins the .NET SDK.
- central package management pins direct package versions.
- NuGet lock files are committed once projects exist; CI uses locked restore.
- Release builds are deterministic where supported.
- Toolchain changes are deliberate and recorded.
