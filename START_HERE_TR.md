# Ornare — Başlangıç

Bu klasör Google Antigravity'nin projeyi sıfırdan, tutarlı, denetlenebilir ve yayınlanabilir biçimde geliştirmesi için hazırlanmıştır.

## Antigravity'de doğru başlangıç sırası

1. `Ornare` klasörünü workspace olarak aç.
2. Kullanıcının seçtiği **Gemini 3.8 Flash (High)** yapılandırmasını kullan.
3. `.agents/rules/` workspace kurallarının etkin olduğundan emin ol.
4. `.agents/mcp_config.json` içindeki Microsoft Learn MCP'yi Windows/WinUI/MSIX kararlarında resmi kaynak doğrulaması için kullan.
5. `GOAL_PROMPT.md`, `AGENTS.md` ve `docs/00_INDEX.md` dosyalarını oku.
6. `/bootstrap` çalıştır.
7. Gate 0 için `/quality-gate` çalıştır ve kanıtı `production_artifacts/quality/` altında kaydet.
8. `/vertical-slice` ile Explorer → uygulama → görsel → ICO → güvenli folder customization → refresh → undo/reset zincirini kanıtla.
9. Kapsamı büyütmeden önce `/architecture-review`, `/security-review`, `/performance-baseline` ve Gate 1'i tamamla.
10. `/design-freeze` ile Quick Panel görsel dilini gerçek ekran görüntüleri üzerinden kilitle.
11. Sonra özellikleri küçük dilimler halinde uygula; her değişikliği requirements + test + review kanıtıyla kapat.

## Değiştirilemez temel hedef

Kullanıcı bir klasöre sağ tıklar, **Set Folder Icon** komutuna basar, görseli sürükler/seçer/yapıştırır ve ikon mümkün olan en kısa sürede **güvenli ve kalıcı** olarak uygulanır.

UI sade, premium, rafine ve Apple'ın ürün tasarım disiplininden ilham alan kalite seviyesinde olmalıdır; ancak Apple UI Kit, SF Symbols, SF Pro veya Apple'a ait tasarım varlıkları Windows ürününde kullanılamaz/kopyalanamaz.

**Panel içinde panel, kart içinde kart, her şeyi rounded rectangle içine alma, dashboard-card estetiği, dekoratif gradient/aurora/particle/3D tilt ve anlamsız animasyon yasaktır.**

## Kurumsal doğruluk kuralı

“Hiç mantık hatası yok” ifadesi kod yazılmadan veya test kanıtı olmadan kabul edilmez. Ornare bunu şu mekanizmalarla hedefler: `LOGIC_INVARIANTS.md`, `STATE_MACHINES.md`, requirements traceability, fault injection, bağımsız review pass, gerçek 25H2 x64/ARM64 cihaz testi ve fail-closed quality gate'ler. Kanıt yoksa sonuç **NOT PROVEN** kabul edilir.
