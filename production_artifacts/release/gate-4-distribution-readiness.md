# Ornare V1 Distribution Models & Release Channel Architecture

This document defines the release channels, code signing requirements, packaging models, and public distribution policies for **Ornare V1** based on authoritative Microsoft Learn platform documentation.

---

## 1. Distribution Channels Overview

| Channel | Target Audience | Primary Distribution Vehicle | Code Signing Requirement | Target Status (Gate 4) |
|---|---|---|---|---|
| **Channel A — Microsoft Store** | General Consumer & Enterprise | MSIX Package via Partner Center | **Microsoft Store Signing** (Microsoft automatically re-signs package during ingestion) | **BLOCKED** (Awaiting Partner Center app reservation, package identity & operational contacts) |
| **Channel B — Vellium Website** | Direct download / power users | Direct MSIX / AppInstaller | **Publicly Trusted Code Signing** (Azure Artifact Signing or CA OV Certificate with RFC 3161 timestamp) | **BLOCKED** (Awaiting commercial signing certificate procurement) |
| **Channel C — Windows Package Manager (winget)** | CLI / IT Pro / automation | Winget package manifest pointing to Store or direct URL | Inherited from Channel A (Store ID) or Channel B (Direct URL) | **DEFERRED** (Post-launch activity) |

---

## 2. Channel A: Microsoft Store (Primary Channel)

### 2.1 Ingestion & Signing Architecture
According to official Microsoft Learn documentation (*Publish your first Windows app* & *Code signing options for Windows app developers*):
- When submitting an **MSIX package** to the Microsoft Store via Partner Center, developers **skip manual commercial code signing**.
- Microsoft automatically replaces the development signature with an official Microsoft-trusted Store certificate during the ingestion and certification process.
- **Zero SmartScreen Warnings:** Applications installed via the Microsoft Store are covered by Microsoft's certificate and never display SmartScreen warnings.
- **Crucial Clarification:** An external commercial CA code-signing certificate (EV/OV) is **NOT a requirement or blocker** for Microsoft Store submission. Candidate packages signed with a local development certificate (or unsigned MSBuild packages) are fully accepted by Partner Center.

### 2.2 Store Prerequisites Matrix

| Prerequisite | Technical Requirement | Project Status | Action Owner |
|---|---|---|---|
| **Partner Center Account** | Active Windows Developer registration | Pending confirmation | Organization Owner |
| **App Name Reservation** | Reserved name "Ornare" in Partner Center | **BLOCKED — User Action Required** | Organization Owner |
| **Package Identity** | `Package.appxmanifest` updated with Partner Center `Identity Name`, `Publisher`, and `PublisherDisplayName` | **BLOCKED — User Action Required** (Currently test identity `CN=OrnareDev`) | Engineering (post-reservation) |
| **Display Name & Description** | Short (100 char) and full (10,000 char) marketing descriptions | Drafted in documentation | Marketing / Product Owner |
| **Visual Assets** | App icon (50x50, 150x150, 44x44), Store logo (50x50, 300x300), Splash screen (620x300) | Generated & embedded in `src/Ornare.App/Assets/` | **VERIFIED** |
| **Screenshots** | Real running WinUI 3 application captures (Quick Panel, Manager, Turkish UI) | Genuine screenshots captured in `production_artifacts/design/` | **VERIFIED** |
| **Privacy Policy URL** | Publicly accessible HTTPS privacy policy URL | **BLOCKED — User Input Required** | Operations / Legal |
| **Support Contact URL / Email** | Publicly accessible HTTPS support page or monitored email | **BLOCKED — User Input Required** | Support / Operations |
| **Security Reporting Contact** | Designated vulnerability reporting inbox | **BLOCKED — User Input Required** | Security / Operations |
| **WACK Certification** | Windows App Certification Kit PASS | **PASS** (22 Passed, 0 Failures) | **VERIFIED** |

---

## 3. Channel B: Direct Vellium Website Distribution (Secondary Channel)

### 3.1 Direct Sideloading & Web Distribution Model
For users downloading Ornare directly from the Vellium website (`vellium.com`) as an `.msix` or `.appinstaller` bundle, Windows enforces Authenticode validation:
- The package **must be digitally signed** using a certificate chaining to a root Certificate Authority in the [Microsoft Trusted Root Program](https://learn.microsoft.com/en-us/security/trusted-root/participants-list).
- The signature must include a valid RFC 3161 compliant timestamp to ensure validity beyond certificate expiration.
- Without a trusted signature, Windows App Installer halts installation with error `0x800B0109` (*A certificate chain processed, but terminated in a root certificate which is not trusted by the trust provider*).

### 3.2 Production Signing Options (Microsoft Official Guidance)
Per Microsoft Learn (*Code signing options for Windows app developers*):
1. **Azure Artifact Signing (formerly Trusted Signing) — Microsoft's Recommended Solution:**
   - Managed cloud signing service integrated with CI/CD (GitHub Actions / Azure DevOps).
   - Starts at ~$9.99/month; requires no physical hardware security module (HSM).
   - Issues short-lived certificates (valid ~3 days) tied to a verified organization identity.
2. **Organization Validated (OV) Certificates:**
   - Traditional option from CAs such as DigiCert, Sectigo, or GlobalSign ($150–$300/year).
   - Since June 2023, CA/Browser Forum mandates private keys must be stored on a FIPS-compliant hardware token (HSM/USB).
3. **Extended Validation (EV) Certificates — No Longer Grant Instant Bypass:**
   - **Crucial Update (2024+):** EV certificates previously bypassed SmartScreen on first release. **Microsoft removed this behavior.** EV-signed binaries now go through the exact same reputation-building process as OV certificates.
   - Paying a premium ($400+/year) solely to avoid SmartScreen warnings is no longer justified.

### 3.3 Code-Signing Trust vs. SmartScreen Reputation (Distinct Concepts)
It is critical to distinguish between **signature validity** and **reputation**:
- **Code-Signing Trust (Binary Validation):** Ensures the binary has not been tampered with and verifies the legal identity of the publisher. This allows Windows to trust the package for installation without throwing fatal root-trust errors (`0x800B0109`).
- **SmartScreen Reputation (Dynamic Behavioral Telemetry):** SmartScreen evaluates the history, telemetry, and download volume associated with a signing identity.
  - **No certificate (OV, EV, or Artifact Signing) automatically or instantly eliminates SmartScreen warnings for brand-new files.**
  - Initial downloads of a newly signed version will still display a SmartScreen warning (*"Windows protected your PC"* / unrecognized app), though showing the verified publisher name.
  - Reputation accumulates over time as users download and run the application without security incidents. Signing consecutive releases with a consistent publisher identity allows reputation to carry across versions.
- **Never claim that purchasing an OV, EV, or Artifact Signing certificate "bypasses SmartScreen".**

### 3.4 Channel B Readiness Status
- **Status:** **BLOCKED**
- **Reason:** A commercial code-signing certificate (Azure Artifact Signing or CA OV certificate) has not yet been procured for Vellium.
- **Independence:** This blocker is isolated strictly to Channel B and does **not** block Channel A (Microsoft Store).

---

## 4. Multi-Architecture Packaging & Bundle Architecture

### 4.1 Architecture Requirements
Ornare V1 specification defines first-class support for:
1. **x64 (AMD64):** Intel/AMD 64-bit systems.
2. **ARM64 (AArch64):** Windows on ARM, Qualcomm Snapdragon X Elite / Copilot+ PCs.

### 4.2 Multi-Architecture MSIXBundle Model
- An `.msixbundle` encapsulates both `Ornare_1.0.0.0_x64.msix` and `Ornare_1.0.0.0_arm64.msix`.
- The Windows deployment subsystem inspects the device architecture and installs only the matching native package.
- **Architecture Invariant:** An ARM64 package must contain pure native ARM64 binaries (`Ornare.ShellExtension.dll` ARM64 + `Ornare.App.exe` ARM64). Packaging x64 binaries under emulation inside an ARM64 package is strictly prohibited.

### 4.3 Architecture Qualification Matrix

| Component | x64 Status | ARM64 Status | Operational Status / Evidence |
|---|---|---|---|
| **Managed .NET 10 Solution** | **VERIFIED** | **VERIFIED** | 6 üretim projesinin tamamı `win-x64` ve `win-arm64` için 0 hata/uyarı ile derlendi. |
| **Native C++ Shell Extension** | **VERIFIED** | **VERIFIED** | `VC.Tools.ARM64` ile derlendi; `AA64 machine (ARM64)` PE header, `/guard:cf`, `/sdl` doğrulandı. |
| **Native AOT Publish** | **VERIFIED** | **VERIFIED** | Strongly-typed bindings & source generation ile `win-x64` ve `win-arm64` 0 hata/uyarı ile derlendi. |
| **Package (.msix)** | **READY** | **READY** | `Ornare_1.0.0.0_x64.msix` ve `Ornare_1.0.0.0_arm64.msix` üretildi; her iki mimaride WACK PASS. |
| **Package Bundle (.msixbundle)** | **READY** | **READY** | `Ornare_1.0.0.0_x64_arm64.msixbundle` üretildi; bundle manifest x64 + ARM64 içerir. |
| **Runtime Execution** | **VERIFIED** | **NOT PROVEN** | x64 fiziksel Windows 11 üzerinde adli olarak kanıtlandı; ARM64 fiziksel cihaz testi beklenmektedir. |

---

## 5. Product Owner V1 Scope Decision & Release Policy

Per formal Product Owner decision (2026-09-25):

1. **V1 Initial Public Release Scope:**
   - Strictly scoped to **Windows 11 x64**.
   - Initial Microsoft Store candidate is strictly `Ornare_1.0.0.0_x64.msix`.
   - All public documentation, Store listings, README, and support channels explicitly specify supported platform as **Windows 11 x64**.
   - Zero public marketing claims or premature roadmap promises regarding ARM64.

2. **ARM64 Status & Retention:**
   - ARM64 implementation is 100% build-ready, AOT-qualified, packaged, and verified via WACK (`production_artifacts/release/candidate/Ornare_1.0.0.0_arm64.msix`).
   - Code, build projects, packaging pipelines, and artifacts are strictly preserved.
   - Retained in `production_artifacts/release/arm64-validation/` alongside the 19-point hardware validation protocol.
   - ARM64 public distribution will be included in a subsequent certified release once the 19-point validation protocol achieves 100% PASS on genuine Windows 11 ARM64 hardware (Snapdragon X Elite / Copilot+ PC).

3. **Multi-Architecture Bundle Retention:**
   - `Ornare_1.0.0.0_x64_arm64.msixbundle` is retained internally as an engineering milestone artifact and will serve as the deployment vehicle once physical ARM64 hardware qualification is achieved.
