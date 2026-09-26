# Third-Party Software Notices and Information

This file contains third-party software notices and licenses for Ornare V1 Release Candidate.

Ornare incorporates components from the open source projects listed below.

---

## 1. Magick.NET (Magick.NET-Q16-AnyCPU & Magick.NET.Core)
- **Version:** 14.17.1
- **License:** Apache License, Version 2.0
- **Copyright:** Copyright (c) Dirk Lemstra
- **Project URL:** https://github.com/dlemstra/Magick.NET
- **License Text:**
```
Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
```

---

## 2. Microsoft.Data.Sqlite.Core
- **Version:** 10.0.12
- **License:** MIT License
- **Copyright:** Copyright (c) .NET Foundation and Contributors
- **Project URL:** https://github.com/dotnet/efcore
- **License Text:**
```
The MIT License (MIT)

Copyright (c) .NET Foundation and Contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

---

## 3. SQLitePCLRaw (bundle_winsqlite3, core, provider.winsqlite3)
- **Version:** 2.1.11 / 2.1.12
- **License:** Apache License, Version 2.0
- **Copyright:** Copyright (c) Eric Sink
- **Project URL:** https://github.com/ericsink/SQLitePCL.raw
- **Notice:** Ornare utilizes the Windows system `winsqlite3.dll` engine via `SQLitePCLRaw.bundle_winsqlite3`.

---

## 4. Microsoft Windows App SDK (Microsoft.WindowsAppSDK)
- **Version:** 2.5.1
- **License:** Microsoft Software License Terms
- **Copyright:** Copyright (c) Microsoft Corporation
- **Project URL:** https://github.com/microsoft/WindowsAppSDK

---

## 5. CommunityToolkit.WinUI.Animations
- **Version:** 8.2.251219
- **License:** MIT License
- **Copyright:** Copyright (c) .NET Foundation and Contributors
- **Project URL:** https://github.com/CommunityToolkit/Windows

---

## 6. WinUIEx
- **Version:** 2.9.3
- **License:** MIT License
- **Copyright:** Copyright (c) Morten Nielsen
- **Project URL:** https://github.com/dotMorten/WinUIEx

---

## 7. Microsoft.Windows.CsWin32 (Build-Time Code Generator)
- **Version:** 0.3.333
- **License:** MIT License
- **Copyright:** Copyright (c) Microsoft Corporation
- **Project URL:** https://github.com/microsoft/CsWin32

---

## 8. Fluent UI System Icons (Microsoft)
- **Commit:** `a563cf9166f4f91aa617557ed272612b7f0a2f72`
- **License:** MIT License
- **Copyright:** Copyright (c) Microsoft Corporation
- **Project URL:** https://github.com/microsoft/fluentui-system-icons
- **Copied Assets:**
  - `ic_fluent_clock_24_regular.svg` (Clock badge for status.in-progress)
  - `ic_fluent_checkmark_circle_24_regular.svg` (Checkmark Circle badge for status.done)
  - `ic_fluent_archive_24_regular.svg` (Archive badge for status.archive)
  - `ic_fluent_important_24_regular.svg` (Important badge for status.important)
- **License Text:**
```
The MIT License (MIT)

Copyright (c) Microsoft Corporation

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

---

## Reference Architecture Provenance
- `microsoft/vscode-explorer-command` (MIT): Architectural reference for out-of-process Windows 11 `IExplorerCommand` context menu invocation.
- `dszharikov/FolderColorizer` (MIT): Reference for Windows `desktop.ini` folder customization mechanics and attributes.

---

## Asset Policy Compliance Verification
- No Apple proprietary assets, Apple Design Resources, SF Symbols, or SF Pro fonts are used in this repository or binary releases.
- All product branding and application tile graphics are original custom artwork created for Ornare.
- Segoe Fluent Icons and system fonts used are standard Windows 11 platform assets provided by the operating system.
