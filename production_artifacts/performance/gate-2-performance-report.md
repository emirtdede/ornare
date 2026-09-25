# Ornare Performance Report — Gate 2 (Core V1 & Scale Baselines)

- **Phase**: Gate 2 Performance Baseline & Regression Validation
- **Date**: 2026-09-25
- **Host**: Windows 11 Pro 25H2 (x64, Intel/AMD Host, SSD Storage)
- **Data Source**: [`production_artifacts/performance/gate-2-benchmark-results.json`](file:///c:/Users/emir/Desktop/Ornare/production_artifacts/performance/gate-2-benchmark-results.json)
- **Status**: **PASS**

---

## 1. Performance Summary & Gate 1 Regression Check

Gate 2 introduces the Manager UI, SQLite persistent metadata storage, and folder health scanning. A core requirement of Gate 2 is that Manager additions **must not regress** the lightweight Quick Panel activation performance.

| Scenario | Gate 1 Baseline | Gate 2 Measured (P50) | Gate 2 Budget | Status |
|:---|:---:|:---:|:---:|:---:|
| **Quick Panel Cold Visible** | 412 ms | **361.7 ms** | $< 500$ ms | **PASS (Faster)** |
| **Quick Panel Warm Activation** | 225 ms | **210.5 ms** | $< 250$ ms | **PASS** |
| **Manager Cold Visible** | N/A | **405.3 ms** | $< 600$ ms | **PASS** |
| **Manager Warm Activation** | N/A | **230.3 ms** | $< 300$ ms | **PASS** |
| **Process Memory (Idle)** | 42 MB | **48.2 MB** | $< 80$ MB | **PASS** |
| **Background Daemons** | 0 | **0** | Strictly 0 | **PASS** |

### Key Regression Takeaway:
Quick Panel cold visible latency improved from 412 ms to 361.7 ms due to lazy database initialization and targeted single-instance routing. Quick Panel does **not** load the full icon library, does not query customized folders, and does not perform health scans on activation.

---

## 2. Scale & Database Query Latencies

Data scale benchmarks were executed against synthetic databases containing up to 1,000 assets and 1,000 folder assignments (`LibraryStorageAndScaleTests.cs` and `benchmark-gate2.ps1`):

| Scale Benchmark | Volume | Measured Latency | Target Budget | Efficiency Assessment |
|:---|:---:|:---:|:---:|:---|
| **Library Asset Insert (Batch)** | 100 assets | 32 ms | $< 100$ ms | 0.32 ms per asset record |
| **Library Asset Fetch** | 100 assets | **3 ms** | $< 20$ ms | Single indexed query (`last_used_at DESC`) |
| **Library Asset Insert (Batch)** | 1,000 assets | 180 ms | $< 500$ ms | 0.18 ms per asset record |
| **Library Asset Fetch** | 1,000 assets | **2 ms** | $< 50$ ms | Highly optimized SQLite B-tree traversal |
| **Favorites Query** | 50 items | **1 ms** | $< 10$ ms | Indexed PK lookup |
| **Recent Usage Query** | Top 20 items | **1 ms** | $< 10$ ms | Indexed `history.timestamp DESC` query |

---

## 3. Folder Health Scanning Performance

The health scan evaluates filesystem state against database metadata in a strictly read-only manner (checking directory attributes, `desktop.ini`, and `.ornare\folder.ico` presence):

| Health Scan Scenario | Target Volume | Measured Duration | Performance Rate |
|:---|:---:|:---:|:---:|
| **Health Scan 100 Folders** | 100 physical folders | **92 ms** | **0.92 ms / folder** |
| **Health Scan 1,000 Folders (Extrapolated)** | 1,000 folders | **920 ms** | **0.92 ms / folder** ($< 1.0$ s total) |

### Non-Blocking Architecture:
- Health scans in `ManagerWindow.xaml.cs` run completely asynchronously on background worker threads via `Task.Run` and progressive UI dispatch.
- Scanning never blocks the WinUI XAML render thread or UI pointer input.
- File system metadata reads use efficient low-overhead Win32 attribute queries without recursive directory walks.

---

## 4. Collection Virtualization & Memory Management

- **GridView Virtualization**: The Manager Library, Recent, and Favorites collections utilize WinUI 3's built-in `ItemsRepeater` / `GridView` UI virtualization. Only visible viewport tiles (approx. 20-30 tiles) instantiate XAML elements.
- **Bitmap Management**: Image decodes are cached by filesystem path and garbage-collected when references leave scope. The process memory stays under 50 MB even when scrolling through 1,000 assets.

---

## 5. Performance Gate Verdict

All measurements comfortably beat established budgets. No regressions observed.

**Overall Performance Verdict: PASS.**
