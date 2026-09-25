# Logic Invariants — Non-Negotiable Correctness Rules

These invariants exist specifically to prevent hidden logic errors. An implementation that violates one is incorrect even if the happy path appears to work.

## Explorer/process invariants

- **INV-EXP-001**: Explorer never loads Ornare UI, Magick.NET, SQLite, network stack, or manager/business logic.
- **INV-EXP-002**: Shell callbacks never wait for app startup or image/file mutation work.
- **INV-EXP-003**: A shell invocation never proves a target safe; app process revalidates target/path/identity.

## Folder/data invariants

- **INV-FS-001**: Ornare never deletes or overwrites a file it cannot prove it owns.
- **INV-FS-002**: Existing unrelated `desktop.ini` semantics survive Apply, Reset, Undo, Repair, Upgrade and Uninstall.
- **INV-FS-003**: Reset changes only Ornare-owned icon semantics; it never overwrites the current live `desktop.ini` with an old whole-file snapshot.
- **INV-FS-004**: Source-image lifetime is irrelevant after successful Apply.
- **INV-FS-005**: A successful Apply leaves a valid folder-owned icon and a recoverable/tracked state.
- **INV-FS-006**: An operation that reports failure cannot knowingly leave an untracked successful mutation; if disk mutation may have committed, state becomes reconciliation/repair, not a false clean failure.
- **INV-FS-007**: Folder rename/move support is claimed only for qualified scenarios actually tested.
- **INV-FS-008**: Attribute rollback occurs only for attributes Ornare introduced and only when reverting cannot break other current customization.
- **INV-FS-009**: Reset/Undo never crosses an external-change ownership-epoch boundary automatically; a fresh explicit apply after conflict captures a fresh baseline.

## Transaction/recovery invariants

- **INV-TX-001**: Before first user-visible filesystem mutation, enough durable journal state exists to classify/reconcile the operation after process termination.
- **INV-TX-002**: Reconciliation is idempotent: running it twice yields the same correct result.
- **INV-TX-003**: Cancellation cannot interrupt the operation after the commit boundary in a way that leaves an unknowable state.
- **INV-TX-004**: Cleanup is always last and only removes proven-unreferenced Ornare-owned artifacts.

## Concurrency invariants

- **INV-CON-001**: At most one mutation operates on a folder identity at a time.
- **INV-CON-002**: Redirected activation cannot mutate operation/UI state concurrently with an active commit.
- **INV-CON-003**: Duplicate rapid invocation is safe and cannot duplicate/desynchronize history or assets.

## Imaging/security invariants

- **INV-IMG-001**: No supported image input can bypass configured resource limits.
- **INV-IMG-002**: SVG/vector processing cannot fetch network/external file resources.
- **INV-IMG-003**: Every ICO size is rendered from the normalized source, not recursively from a smaller generated frame.
- **INV-IMG-004**: Aspect ratio is never silently distorted; default placement is contain.

## UI invariants

- **INV-UI-001**: Golden path has no Apply confirmation button.
- **INV-UI-002**: Quick Panel has one primary intent and no nested card/panel dashboard structure.
- **INV-UI-003**: Success is shown only after durable operation state is known; a visual shell refresh is not the sole definition of persistence success.
- **INV-UI-004**: Error feedback preserves target context and never claims success after an exception.
- **INV-UI-005**: System theme/language are defaults; accessibility modes never depend on color alone.

## Privacy/release invariants

- **INV-PRV-001**: V1 core operation requires no account, cloud or image upload.
- **INV-PRV-002**: Image bytes are never included in diagnostic export.
- **INV-REL-001**: Public release is produced from an exact reviewed source commit with locked dependencies, SBOM, notices and signed package lineage.
- **INV-REL-002**: Zero P0/P1 defects at release candidate approval.

## Enforcement

Every vertical-slice, feature and release report states which invariants were affected and what evidence proves they still hold.
