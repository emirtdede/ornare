# Image Pipeline Specification

## 1. User-facing policy

Users provide ordinary images. Ornare creates the Windows icon. ICO conversion is an implementation detail.

Guaranteed V1 formats after packaged-decoder verification:

- PNG
- JPG/JPEG/JPE
- WebP
- BMP/DIB
- TIFF/TIF (first image/page policy)
- GIF (first frame)
- ICO
- SVG (secure rasterization)
- TGA
- AVIF / HEIC / HEIF only when the shipped Magick.NET/native codec configuration is verified across x64 and ARM64.

Do not market “all image formats.” Additional formats may be best-effort only after security and test coverage.

## 2. Decode security

Magick.NET/ImageMagick must run with an explicit restrictive policy.

Default policy intentions:

- explicit coder allowlist;
- no network delegates;
- no arbitrary external SVG resource fetch;
- no Ghostscript/delegate path unless specifically audited and required;
- max width/height: 16,384 px;
- max decoded area: 100 megapixels;
- max frames/pages processed: 32, with V1 selecting first relevant frame/page;
- operation memory target cap: 256 MB;
- bounded temp-disk usage;
- decode timeout around 10 seconds, validated by implementation feasibility;
- reject decompression bombs/resource-exhaustion inputs.

Limits are engineering defaults and can be tightened based on benchmark/security testing.

## 3. Normalization

- Apply EXIF orientation.
- Convert to a stable sRGB rendering intent/profile policy.
- Preserve alpha.
- Never distort aspect ratio.
- Default layout = contain in transparent square canvas.
- Do not silently crop faces/logos.
- Optional Fill/Adjust can be introduced only if UI remains simple; not required for golden path.

## 4. ICO output

Generate each target frame from the normalized high-quality source, not by repeatedly shrinking the previous frame.

Target sizes:

- 16
- 20
- 24
- 32
- 40
- 48
- 64
- 96
- 128
- 256 px

Use appropriate high-quality resampling. Test real Explorer Small/Medium/Large/Extra Large views and high DPI.

## 5. Small-size quality

At small sizes, automatic downsampling can lose clarity. V1 must at least apply high-quality resampling, appropriate padding, alpha handling, and sharpening only if objective A/B review proves it improves results. Do not invent aggressive filters that distort logos.

## 6. Hashing

Canonical asset identity uses SHA-256 over a well-defined normalized source representation or original bytes + normalization version. The exact scheme must be versioned so pipeline changes do not cause accidental collisions/reuse of incompatible renders.

## 7. Clipboard

Accept:

- bitmap/image clipboard payload;
- copied local image file(s), exactly one valid image for V1.

Do not fetch an HTTP URL just because it is present on clipboard.

## 8. Error handling

Distinguish:

- not an image;
- unsupported codec;
- damaged image;
- resource limit exceeded;
- animated/multipage policy;
- decoder/native component failure.

Messages should be actionable and localized.
