# Support and Incident Playbook

## 1. Support principles

- Do not ask users to disable Windows security features.
- Do not prescribe deleting global Explorer/icon caches as a routine first step.
- Do not ask for private images or full folder contents when metadata/diagnostics are sufficient.
- Prefer reversible, targeted repair.

## 2. Minimum support information

Request or export only what is necessary:

- Ornare version;
- Windows edition/build and architecture (V1 initial release supports **Windows 11 x64**; ARM64 is pending physical hardware qualification);
- packaged install source;
- operation/error code;
- target type (local/cloud/removable/protected/network) without raw path when not required;
- relevant health state;
- whether issue reproduces on a disposable local test folder.

## 3. Triage order

1. Determine whether the OS and architecture are supported (**Windows 11 x64** on build 26100+).
2. Confirm app/package/Windows versions.
3. Reproduce on a disposable local folder.
4. Inspect Ornare health state and exported diagnostics.
5. Use targeted Repair only when ownership is provable.
6. Escalate to engineering with evidence if a P0/P1 signature exists.

## 4. P0 indicators

Immediately stop normal troubleshooting and escalate if Ornare appears to cause:

- Explorer crash/hang;
- deletion/corruption of unrelated files/settings;
- security-policy weakening;
- repeatable package-startup failure affecting supported builds;
- path traversal/reparse escape or unsafe external access;
- reset restoring the wrong external state.

## 5. User-facing error quality

Support instructions should reference stable localized error codes rather than internal exception text. Raw stack traces are engineering artifacts, not user-facing copy.
