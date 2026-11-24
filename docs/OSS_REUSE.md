# Ornare - Open Source Reuse and Provenance

Ornare ships only dependencies and assets whose licence permits redistribution. This document
records what is used, why, and how provenance is verified. A new dependency or asset must be
admitted here before it is referenced.

## Admission rules

1. The licence must permit commercial redistribution in a closed-source application
   (MIT, BSD, Apache-2.0, MS-PL and equivalent).
2. The exact version is pinned centrally in `Directory.Packages.props`; floating versions are
   not allowed.
3. The licence text and attribution are recorded in `THIRD-PARTY-NOTICES.md`.
4. No Apple-proprietary assets, fonts or glyph sets are used.
5. Code copied from another project must be accompanied by its licence and origin.
6. An asset (icon, SVG, emoji pipeline source) must have a documented origin and licence.

## Current dependencies

| Package | Version | Purpose | Licence |
|---------|---------|---------|---------|
| Microsoft.WindowsAppSDK | 2.5.1 | WinUI 3 runtime | MIT |
| Microsoft.Windows.CsWin32 | 0.3.333 | Source-generated P/Invoke | MIT |
| CommunityToolkit.WinUI.Animations | 8.2.251219 | WinUI animations | MIT |
| WinUIEx | 2.9.3 | Window helpers | MIT |
| Magick.NET-Q16-AnyCPU | 14.17.1 | Image decode/encode | Apache-2.0 |
| Microsoft.Data.Sqlite.Core | 10.0.12 | SQLite access | MIT |
| SQLitePCLRaw.bundle_winsqlite3 | 2.1.11 | Windows SQLite bundle | Apache-2.0 |
| System.CommandLine | 2.0.0 | CLI parsing | MIT |

`THIRD-PARTY-NOTICES.md` is the authoritative attribution file. If the two disagree, the
notices file wins and this table must be corrected.

## Assets

- Application and folder icons are original Ornare work or generated at build time from
  original vector templates.
- Emoji rendering uses the platform's own emoji font through DirectWrite; Ornare does not bundle
  Apple emoji artwork.
- No third-party icon pack is embedded.

## Verification

- A dependency update re-runs the licence check and updates both this document and
  `THIRD-PARTY-NOTICES.md` in the same change.
- An asset addition records its source and licence in this document.
