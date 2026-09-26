# Ornare Feature Implementation Pack

Bu paket mevcut Ornare mimarisine 27 ürün özelliği için deterministik entegrasyon planı verir. **26 özellik teknik implementasyon kapsamındadır; F11 `DEFERRED BY PRODUCT DECISION` durumundadır.** F19 desteği sıradan yerel sabit-disk NTFS klasörleriyle sınırlıdır. İlk public/runtime-certified kapsam Windows 11 x64'tür; ARM64 build/AOT/package regression korunur ancak fiziksel ARM64 runtime sertifikasyonu ayrı ve henüz `NOT PROVEN` durumundadır.

## Ana belgeler

- `00_SHARED_ARCHITECTURE_CONTRACT.md` — tüm özelliklerin değişmez mimari/performance/test sözleşmesi.
- `01_OSS_SOURCES_AND_LICENSE_POLICY.md` — açık kaynak/platform kullanımı ve lisans sınırları.
- `02_EXECUTION_ORDER.md` — uygulanacak kesin sıra ve bağımlılıklar.
- `03_GOAL_PROMPT.md` — Antigravity/kodlama ajanına verilecek ana otonom yürütme promptu.

## Özellik belgeleri

- `features/F01_folder-colors.md` — **Folder Colors** — IMPLEMENT
- `features/F02_custom-color-picker.md` — **Custom Color Picker** — IMPLEMENT
- `features/F03_folder-style-templates.md` — **Folder Style Templates** — IMPLEMENT
- `features/F04_status-presets.md` — **Status Presets** — IMPLEMENT
- `features/F05_priority-presets.md` — **Priority Presets** — IMPLEMENT
- `features/F06_contain-cover-fill.md` — **Contain / Cover / Fill** — IMPLEMENT
- `features/F07_transparent-padding.md` — **Transparent Padding** — IMPLEMENT
- `features/F08_background-color.md` — **Background Color** — IMPLEMENT
- `features/F09_svg-recoloring.md` — **SVG Recoloring** — IMPLEMENT
- `features/F10_adaptive-contrast.md` — **Adaptive Contrast / Palette Generation** — IMPLEMENT
- `features/F11_per-size-optical-rendering.md` — **Per-Size Optical Rendering** — DEFERRED BY PRODUCT DECISION
- `features/F12_ico-import.md` — **ICO Import** — IMPLEMENT
- `features/F13_ico-export.md` — **ICO Export** — IMPLEMENT
- `features/F14_png-export.md` — **PNG Export** — IMPLEMENT
- `features/F15_library-tags.md` — **Library Tags** — IMPLEMENT
- `features/F16_custom-collections.md` — **Custom Collections** — IMPLEMENT
- `features/F17_library-search.md` — **Library Search** — IMPLEMENT
- `features/F18_asset-deduplication.md` — **Asset Deduplication** — IMPLEMENT
- `features/F19_portable-folder-mode.md` — **Portable Folder Mode** — IMPLEMENT-LIMITED
- `features/F20_manager-folder-drag-drop.md` — **Folder Drag & Drop into Manager** — IMPLEMENT
- `features/F21_apply-last-icon.md` — **Apply Last Icon** — IMPLEMENT
- `features/F22_undo-from-explorer.md` — **Undo from Explorer** — IMPLEMENT
- `features/F23_reset-from-explorer.md` — **Reset from Explorer** — IMPLEMENT
- `features/F24_cli-json.md` — **CLI + Machine-Readable Output** — IMPLEMENT
- `features/F25_dry-run.md` — **Dry Run** — IMPLEMENT
- `features/F26_keyboard-first.md` — **Keyboard-First Workflow** — IMPLEMENT
- `features/F27_localization-expansion.md` — **Localization Expansion** — IMPLEMENT WITH EXTERNAL LINGUISTIC-QUALITY GATE

## Normatif yorum

Bir özellik belgesi ile paylaşılan mimari sözleşme çelişirse daha güvenli ve daha dar olan kural uygulanır; mevcut proje `LOGIC_INVARIANTS.md` kuralları her zaman feature kolaylığından üstündür. Canlı kodda sınıf/dosya adı değişmişse ajan aynı sorumluluğu güncel eşdeğer noktaya taşır, paralel ikinci mimari kurmaz.


## Teslimat/audit kapsamı

`99_FINAL_QA_AUDIT.md` içindeki puan **dokümantasyon paketinin iç tutarlılığı** içindir; Ornare uygulamasının henüz uygulanmamış 26 özelliğinin üretim sertifikası değildir. Canlı repo yolları, gerçek test sonuçları, yeni dil çevirilerinin insan/pro incelemesi, `Windows_11_FOLDER.svg` lisans/provenance durumu ve fiziksel ARM64 runtime yalnız gerçek kanıt üretildiğinde PASS olabilir.

Ajan hiçbir `FXX` placeholder dizini kullanmaz; her evidence yolu feature dosyasındaki gerçek iki haneli ID ile yazılır (`F18`, `F09` vb.).
