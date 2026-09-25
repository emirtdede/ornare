# Fault Injection and Crash-Consistency Tests

The filesystem + SQLite behavior is treated as a transaction across systems that do not share one ACID transaction. Reliability is proven with deterministic fault injection.

## Apply operation injection points

Inject failure/crash after each stage:

1. after source validation;
2. after canonical asset creation;
3. after folder ICO temp write;
4. after folder ICO durable rename;
5. after reading current desktop.ini state;
6. after writing desktop.ini temp;
7. after desktop.ini replacement;
8. after attribute mutation;
9. after journal state transition;
10. after shell notification;
11. before DB assignment commit;
12. after DB commit but before obsolete asset cleanup.

Expected result for every point:

- no unrelated file loss;
- no invalid partial file presented as a valid ICO/config;
- restart/relaunch can classify state deterministically;
- reconciliation either completes the new state or restores the owned prior icon state;
- unrelated desktop.ini keys remain intact;
- obsolete Ornare asset cleanup is idempotent.

## Reset/Undo injection

Repeat equivalent injection around:

- reading current third-party/user changes;
- restoring baseline icon semantics;
- removing only Ornare-owned key/value/file state;
- DB/history update;
- shell refresh.

Reset must never restore an old full-file desktop.ini snapshot over unrelated changes made after Ornare first customized the folder.

## Concurrency tests

- two apply requests same folder;
- apply + reset same folder;
- rapid activation of folders A/B/C while A commits;
- folder renamed between preparation and commit;
- target replaced with junction/reparse point;
- application terminated during prepare vs commit;
- Explorer restart independent of app operation.

## Storage corruption

- malformed SQLite DB;
- migration interruption;
- canonical asset missing;
- folder-owned ICO missing;
- desktop.ini manually edited;
- folder removed while assignment remains.

The app must degrade to explicit `NeedsRepair`/reconciliation behavior, not fabricate successful state.
