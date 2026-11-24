# ADR-0011: Bound the customizable folder path at MAX_PATH, extend every child path

- Status: Accepted
- Date: 2026-11-24

## Context

An earlier version of `docs/LONG_PATH_COMPATIBILITY.md` promised support for paths beyond
`MAX_PATH`, while `TargetFolderValidator` rejected anything longer than 259 characters. The
contradiction was found during the pre-deployment audit: a document that describes behaviour the
code does not have is worse than no document, because it hides the failure.

Two separate path lengths are involved and they were being conflated:

1. The **folder being customized**. This is what the user selects and what Windows persists
   inside `desktop.ini` and the shell customization settings.
2. The **files Ornare writes inside that folder** (`desktop.ini`, `.ornare\folder.ico`). These
   are always longer than the folder path, so a folder close to the limit produces a child path
   past it.

## Decision

- The folder path is bounded at 259 characters. A longer path is rejected with
  `ErrorCode.InvalidTarget` and a localized message; the reason is recorded as diagnostic detail.
- Every child path is extended with `\\?\` (or `\\?\UNC\`) before it reaches a Win32 function.
  `Ornare.Core.LongPath.ToExtended` is the single helper that does this, and it is called
  unconditionally so no call site has to reason about length.
- The bound exists because `SHGetSetFolderCustomSettings` is the documented shell customization
  entry point and its behaviour past `MAX_PATH` is not specified. Writing a customization that
  never repaints would be a silent failure.

## Consequences

- A user with a 300-character folder gets a clear refusal instead of an icon that never appears.
- A user with a 250-character folder gets a working icon; the 262-character `desktop.ini` path is
  handled by the extended prefix rather than failing with `ERROR_PATH_NOT_FOUND`.
- Raising the limit later requires replacing the shell invalidation call, not just changing a
  constant. That is recorded in `docs/LONG_PATH_COMPATIBILITY.md` so nobody raises the constant
  alone.

## Alternatives considered

- **Raise the constant to 32,767 and rely on .NET.** Rejected: managed I/O would work, but the
  shell hook would fail, producing a false "Applied" state.
- **Keep the promise and drop the shell hook.** Rejected: without invalidation, Explorer keeps the
  cached icon and the user sees no change.
- **Leave the document as it was.** Rejected: it is a false claim in the repository, and the audit
  standard treats documentation that contradicts code as a defect.
