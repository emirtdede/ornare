---
name: image-pipeline
description: Use for image input validation, Magick.NET, format support, SVG security, normalization, resizing, ICO creation, clipboard image handling, or image quality testing.
---
# Image Pipeline Skill

Read `docs/IMAGE_PIPELINE.md` and `docs/SECURITY_PRIVACY.md`.

- Pin current secure Magick.NET version.
- Configure coder/resource policy before accepting untrusted input.
- No external network/delegate access for SVG.
- EXIF orient, sRGB policy, preserve alpha/aspect.
- Default = contain on transparent square, no destructive crop.
- Generate each ICO size from normalized source.
- Verify x64/ARM64 codecs, especially HEIC/AVIF.
- Fuzz/corrupt/resource-limit test inputs.
- Keep decoding off UI thread.
