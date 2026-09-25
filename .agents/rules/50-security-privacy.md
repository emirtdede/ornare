---
description: Security, privacy and user-data integrity requirements.
---
# Security & Privacy

Follow `docs/THREAT_MODEL.md` and `docs/SECURE_SDLC.md` in addition to the domain security specification.

- Treat every image and folder path as untrusted input.
- Magick.NET must use explicit resource/coder/network restrictions.
- Prevent SVG external resource/network loading.
- Validate reparse points/path state at operation time.
- File operations are transactional/rollback-aware.
- Never overwrite/delete unrelated files.
- No telemetry, analytics, ads, login, image upload or cloud dependency in V1.
- Diagnostics are user-triggered; redact paths by default; never include image bytes.
- Secrets never enter repo/MCP config/package/logs.
- NuGet/native dependencies require vulnerability and license review.
- Do not weaken Windows trust/security policy to support remote folders.
