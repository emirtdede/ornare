# 🎨 Ornare v1.0.0 — Production Release Candidate

**Developed & Published by [Vellium](https://vellium.dev)**

---

## 🌟 Highlights / Öne Çıkanlar

- **Windows 11 Native Integration**: Out-of-process C++20 COM `IExplorerCommand` context menu extension (`Set Folder Icon`).
- **Live Shell Refresh**: Zero-restart instant icon update via targeted `SHChangeNotify` broadcasts. Never restarts `explorer.exe`.
- **Durable Architecture**: Multi-resolution ICOs stored relative inside hidden `.ornare/icon.ico` subfolders. Unaffected by folder rename or moves across drives.
- **Write-Ahead Journal**: 2-phase crash-resilient mutation journal (`journal.json`) with automatic recovery and idempotent SQLite sync.
- **Multi-Format Image Pipeline**: Full PNG, JPEG, WebP, BMP, and SVG support with Magick.NET downsampling (16x16 to 256x256).
- **100% Offline & Local-First**: Zero telemetry, zero analytics, zero network requests, no account requirement.
- **Bilingual**: Turkish (`tr-TR`) and English (`en-US`) system language detection.

---

## 📦 Distribution Assets / Dağıtım Dosyaları

| File | Architecture | Description |
| :--- | :--- | :--- |
| **`OrnareSetup.exe`** | x64 | Standalone Win32 setup wizard with embedded signed MSIX & 1-click cert setup |
| **`Ornare_1.0.0.0_x64.msix`** | x64 | Windows 11 signed MSIX application package |
| **`Ornare_1.0.0.0_arm64.msix`** | ARM64 | Native ARM64 signed MSIX application package |
| **`Ornare_1.0.0.0_x64_arm64.msixbundle`** | Multi-Arch | Unified multi-architecture bundle (x64 + ARM64) |
| **`VelliumDevCert.cer`** | Any | Public development certificate for offline validation |
| **`SHA256SUMS.txt`** | - | Cryptographic SHA-256 verification checksums |

---

## 🔐 Checksums (SHA-256)

```text
1876A24F6F3615E0F6DF8F1593F7D2F708F06B5BFA89097FFDBD5D17F0080791  Ornare_1.0.0.0_arm64.msix
B25B9E36FBC0E17D2A881AD3E71ED91FA6BF76BD7539AB80936AF719FA7D696F  Ornare_1.0.0.0_x64_arm64.msixbundle
11A567B62DB109E55530EF3FB517F8F2173474749F5E0677AC1852521823FC93  Ornare_1.0.0.0_x64.msix
5CDB16AB07CA47F3BBDCDAD2776E92FAE95121FD88E6C74E75A61BEEE26829F1  OrnareSetup.exe
```
