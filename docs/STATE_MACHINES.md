# State Machines

State transitions are explicit; UI code must not infer operation truth from ad-hoc booleans.

## 1. Quick Panel state

```text
Hidden
  -> LoadingContext
  -> Idle

Idle
  -> DragValid
  -> DragInvalid
  -> Preparing (drop/paste/picker accepted)
  -> Closing

DragValid
  -> Idle (drag leave)
  -> Preparing (drop)

DragInvalid
  -> Idle

Preparing
  -> Committing
  -> Failed
  -> Idle (safe cancellation before commit)

Committing
  -> Refreshing
  -> NeedsRepair
  -> FailedAfterRollback

Refreshing
  -> Succeeded
  -> SucceededWithRefreshWarning

Succeeded
  -> Undoing
  -> Closing

SucceededWithRefreshWarning
  -> Repairing
  -> Closing

Undoing/Repairing
  -> Succeeded
  -> NeedsRepair
```

Rules:

- `Committing` is non-cancellable from ordinary UI.
- New shell activations while `Committing` become one pending target; they do not overwrite the current target state.
- Auto-close occurs only from successful stable states and pauses while pointer/focus interaction requires it.

## 2. Apply operation journal

```text
Prepared
  -> FilesystemCommitted
  -> Committed
  -> Finalized

Prepared -> Aborted        (no visible mutation)
Prepared -> NeedsRepair    (unexpected ambiguous filesystem state)
FilesystemCommitted -> Committed      (normal DB reconciliation)
FilesystemCommitted -> RolledBack     (safe semantic rollback of Ornare-owned mutation selected)
FilesystemCommitted -> NeedsRepair    (cannot prove completion or semantic rollback safely)
Committed -> Finalized                (cleanup complete)
```

No journal row is simply deleted because an exception occurred. State is first classified.

## 3. Folder health

```text
Healthy
MissingOwnedIcon
ConfigMismatch
ExternalIconConflict
TrackingDetached
MissingTarget
AccessUnavailable
NeedsReconciliation
UnsupportedLocation
```

Repair may automatically fix only conditions where Ornare can prove ownership and intended state. `ExternalIconConflict` requires conservative behavior; never overwrite an external change silently. Explicit `Stop tracking` transitions the tracked assignment to detached/removed metadata without changing live folder customization. Explicit reapply/direct apply captures the current external semantics as a fresh baseline before Ornare takes ownership again.

## 4. Manager data state

Every page uses explicit `Loading / Ready / Empty / Error` states. Loading is not represented as an empty collection, and an error is not represented as “no items.”
