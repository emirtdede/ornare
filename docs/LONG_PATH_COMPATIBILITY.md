# Ornare - Long Path Compatibility

This document describes what Ornare actually does with long folder paths. It is written from
the code, not from intent, because an earlier version of this file promised support that did
not exist. See ADR-0011 for the decision and its reasoning.

## Current behaviour

| Layer | Behaviour |
|-------|-----------|
| Folder path accepted by the product | Up to 259 characters (`MAX_PATH` minus the terminating null) |
| Longer path | Rejected with `ErrorCode.InvalidTarget` and a localized message |
| Child paths Ornare creates (`desktop.ini`, `.ornare\folder.ico`) | Passed through `LongPath.ToExtended` on every Win32 call |
| Managed `System.IO` calls | .NET adds the `\\?\` prefix itself on Windows, so `File.Copy`, `Directory.CreateDirectory` and friends are already long-path safe |
| App manifest | `<longPathAware>true</longPathAware>` in `app.manifest`, so the OS does not truncate before the process sees the path |

## Why the folder path is still bounded

A 259-character folder plus `\desktop.ini` is 271 characters. That is precisely the case where
the extended prefix is mandatory, and it is why `LongPath.ToExtended` exists and why it is used
on `MoveFileExW` and `DeleteFileW`. The folder path itself is bounded for a different reason:

- `SHGetSetFolderCustomSettings` is part of the shell customization contract and its behaviour
  past `MAX_PATH` is undocumented. The product calls it for Explorer invalidation, so a path it
  cannot handle would produce a folder that is written but never repainted.
- The permission and inheritance model (`FILE_ATTRIBUTE_READONLY` on the folder, hidden+system
  on `desktop.ini`) is documented only for the classic path form.

Rather than ship a customization that silently fails to render, Ornare refuses the write and
tells the user why. That is a deliberate, documented limit, not an unimplemented feature.

## Verification

- `tests/Ornare.Core.Tests/TargetFolderValidatorPathLimitTests.cs` pins both edges: a
  259-character path must fail on existence rather than length, and a 260-character path must be
  rejected as invalid.
- `tests/Ornare.IntegrationTests/DesktopIniEncodingTests.cs` covers the write path, including
  the temp-file naming that stays inside the target directory.

## Raising the limit later

Two changes are required together, and neither is enough alone:

1. `TargetFolderValidator.MaxSupportedPathLength` must be raised.
2. `SHGetSetFolderCustomSettings` must be replaced by a path-independent invalidation strategy
   (`SHChangeNotify` with `SHCNE_UPDATEITEM` only), so shell refresh no longer depends on the
   classic path form.

The second change is a packaging-level risk, so it belongs in an ADR, not in a quiet edit.
