# Privacy Statement Draft — Ornare

> Release/legal review required before publication. This draft reflects the V1 technical specification, not a substitute for jurisdiction-specific legal advice.

## Ornare application behavior

Ornare is designed to work locally on the user's Windows device.

The Ornare application itself does not require an account and does not intentionally collect or transmit:

- images selected or pasted by the user;
- folder contents;
- folder paths;
- usage analytics;
- advertising identifiers;
- behavioral telemetry.

Images are processed locally to create Windows folder-icon assets. Local metadata required for history, favorites, repair and customization ownership is stored on the device.

## Diagnostics

Diagnostics are not uploaded automatically. If the user explicitly exports a diagnostic report for support, the export follows `LOGGING_DIAGNOSTICS.md` and excludes image bytes and raw user paths by default.

## Network behavior

Core icon customization does not require network access. Operating-system, Microsoft Store, package-update, certificate-validation or platform services may have their own network behavior and privacy terms independent of Ornare.

## Changes

If a future version introduces any cloud feature, account, telemetry or remote processing, this statement and the product consent model must be updated before that feature ships.
