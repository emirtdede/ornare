# ADR-0014: Localization stays dictionary-driven, and the XAML guard is per property

- Status: Accepted
- Date: 2026-11-24

## Context

Every window contains English text in markup that the constructor overwrites from
`AppResources`. The audit and the competitor comparison both counted this as duplication: 120
literals, 0 `x:Uid`, and a second copy of the strings in `Strings/*/Resources.resw`.

The obvious Microsoft pattern is `x:Uid` plus `.resw` entries named `Uid.Property`. It is what
Files App does, and it is the pattern a reviewer expects when they see "WinUI 3" and "0 x:Uid".
Adopting it here was considered twice and rejected twice, for reasons that are specific to this
product rather than to the framework:

1. **The letter of the pattern does not remove the code.** `x:Uid` resolves once, during
   `InitializeComponent`, against the PRI resource for the process language. Ornare changes
   language at runtime without restarting, and a user can switch theme and language while the
   manager is open. `x:Uid` does not re-resolve, so the assignment code must stay anyway. The
   result would be three copies (`x:Uid`, the `.resw` mirror, the code assignment) instead of two.
2. **The CLI cannot use `ResourceManager`.** `Ornare.Cli` is published with Native AOT and reads
   the same copy for `--json` output. `AppResources` is a plain dictionary precisely so the CLI
   carries no PRI resource manager. Making `.resw` authoritative would either duplicate the
   catalog for the CLI or force a non-AOT dependency into a tool that is meant to be a small
   automation entry point.
3. **The `.resw` file is a package requirement, not a translation workflow.** It exists so the
   MSIX declares six languages and so tooling can diff the catalogue. It is verified equal to the
   dictionaries by `RuntimeLocaleDictionariesHaveMatchingKeysAndPlaceholders`, so it cannot drift
   silently. Adding `.Text` variants for 120 controls would double it to ~700 keys per locale
   with no user-visible gain.

What the duplication actually cost was not drift but **undetected gaps**: the guard accepted any
assignment on an element, so `AccentHexBox.Text = ...` excused an English
`AutomationProperties.Name`. That is a correctness defect and it is fixed.

## Decision

1. Keep `AppResources` as the single source of copy. (Originally this also kept
   `Strings/*/Resources.resw` as a "verified mirror"; the revision below removes it because the
   mirror was neither complete nor verified.)
2. Keep the XAML literals as design-time fallbacks, and enforce at test time that every literal
   is overwritten **on the exact property**:
   - `Text`, `Content`, `Header`, `PlaceholderText` require `Element.Property = ...`;
   - `AutomationProperties.Name` requires `AutomationProperties.SetName(Element, ...)`.
3. Treat a literal that is not language (a hex sample such as `#0F6CBD`) as an explicit,
   commented allow-list entry, not as a silent exception.

## Consequences

- The guard now fails for an English accessible name, which is how six real defects were found
  (both accent-hex fields, the scale picker, the cloud toggle, both "Remove Emblem" buttons).
- Adding a string still means editing the dictionary and the code assignment. That cost is
  accepted and stated in `docs/LOCALIZATION.md`; it is smaller than maintaining three copies.
- A future move to `x:Uid` remains possible if the app ever restarts on language change and the
  CLI stops sharing the catalogue. The decision is reversible because the copy itself is already
  in one place.

## Revision: the `.resw` mirror was removed (audit finding 3.4)

The paragraph above claimed the `.resw` mirror was "verified equal to the dictionaries ... so it
cannot drift silently". That was wrong, and the first-run audit caught it.

- It held **259 keys per locale against 436** in the runtime dictionaries, so it could not be
  equal by construction.
- The test named as its verifier, `RuntimeLocaleDictionariesHaveMatchingKeysAndPlaceholders`,
  read only the C# dictionaries. The `.resw`-based tests compared each `.resw` file against the
  English `.resw` file, never against the dictionary they were supposed to mirror.
- No build produced a `resources.pri`, so the mirror was not even reachable at runtime.

A second copy that is incomplete, unverified and unused is worse than no copy: a translator or
reviewer cannot tell which file ships. `src/Ornare.App/Strings` is therefore deleted, the tests
read `AppResources.cs` and `AppResources.Extended.cs` directly through
`RuntimeResourceSource`, and both `NoStaleReswCopyOfTheRuntimeCopyExists` and step 5 of
`scripts/quality-gate.ps1` fail if the tree returns.

The MSIX language declaration does not depend on `.resw`: `Package.appxmanifest` lists
`<Resource Language="..."/>` entries directly.

## Alternatives considered

- **Migrate all 120 literals to `x:Uid` now.** Rejected: 720 new mirrored `.resw` entries, a
  weakened parity test (extra keys), and no change to what any user sees.
- **Delete the literals and leave the attributes unset.** Rejected: a missed assignment would
  ship as blank text, which is worse than an English fallback, and the guard could no longer see
  what needs localizing.
- **Generate strongly typed accessors from `.resw` at build time.** Rejected by the repository
  contract on generated product source, and unnecessary because `EveryReferencedResourceKeyExists`
  already fails on a typo.
