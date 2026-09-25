# Windows Platform Compatibility Policy

## Official V1 baseline

- Minimum supported client: **Windows 11 25H2 (build family 26200)**.
- Architectures: **x64 and ARM64** are first-class for 25H2.
- Windows 10 is unsupported.
- Windows 11 24H2 is not a launch support commitment because Home/Pro servicing ends in October 2026, near the product development/release window.

## Build architecture rule

Ornare application/package projects are architecture-specific **x64** and **ARM64**. Do not add an AnyCPU application/package configuration: Windows App SDK contains native components and release behavior must be proven per architecture. A dependency whose package name says `AnyCPU` (for example Magick.NET-Q16-AnyCPU) is acceptable only when its own upstream support matrix includes the actual runtime architectures and packaged native assets are verified.

## Windows 11 26H1

Windows 11 26H1 (build family 28000) is treated as a compatibility platform. Microsoft describes it as a platform release for specific new silicon and not an in-place feature update from 24H2/25H2.

Therefore release qualification does **not invent a mandatory 26H1 x64 machine** if Microsoft/OEMs do not provide a representative supported device. Test 26H1 on real commercially supported architecture/hardware available to the project, and retain results separately from 25H2.

## Build SDK vs runtime minimum

The Windows SDK used to compile may be a newer supported SDK (currently 28000-series) while the package/runtime minimum remains 25H2. Do not accidentally raise the OS minimum by referencing APIs without version guards/compatibility review.

## Long paths

`SHGetSetFolderCustomSettings` documentation states a `MAX_PATH`-bounded folder path parameter. Therefore V1 must explicitly test long folder paths.

Policy:

1. If the preferred shell API supports the target path, use it.
2. A direct Unicode `desktop.ini` implementation for longer paths may be enabled only after tests prove Explorer behavior, preservation, rename/move and reset safety on 25H2/26H1.
3. If not proven, fail clearly as an unsupported target rather than truncating paths or silently using unsafe path manipulation.

## Filesystems/locations

`TARGET_ELIGIBILITY.md` is authoritative. The primary launch class is ordinary local NTFS directories. Cloud/removable filesystems require explicit qualification; target reparse-point directories, UNC/SMB/WebDAV/HTTP, volume roots and protected locations requiring elevation are not promised by V1.
