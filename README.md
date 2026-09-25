# Ornare

Ornare is a Windows 11 utility whose core promise is simple:

> **Right-click a folder → Set Folder Icon → drop, choose, or paste an image → done.**

This repository is specification-first and enterprise-gated. Production source folders begin as scaffolds; implementation must prove the risky Windows vertical slice before feature expansion.

## Product principles

- Windows 11 native: WinUI 3, not a web shell.
- Premium, restrained, Apple-inspired product craft without copying Apple assets or platform UI.
- No card-grid dashboard, nested panels, or “box inside box” design.
- Fastest safe path from an image to a durable folder icon.
- Local-first/offline core: no account, ads, telemetry, image upload or AI dependency.
- English and Turkish; system language/theme followed by default.
- Windows 11 25H2 x64 + ARM64 are mandatory launch qualification targets; 26H1 is qualified only on real applicable supported hardware.
- Explorer integration is tiny, native, non-blocking and crash-resistant.
- Filesystem + SQLite correctness uses a durable operation journal and idempotent recovery, not a fictitious cross-store ACID transaction.
- Reset changes only Ornare-owned icon semantics and preserves unrelated current `desktop.ini` state.

## Start in Antigravity

1. Open the **Ornare** folder as the workspace.
2. Use the user-selected **Gemini 3.8 Flash (High)** configuration for implementation/review work.
3. Ensure `.agents/rules/` are active and Microsoft Learn MCP is available.
4. Read `GOAL_PROMPT.md`, `AGENTS.md`, and `docs/00_INDEX.md`.
5. Run `/bootstrap`.
6. Run `/quality-gate` for **Gate 0** and record evidence.
7. Run `/vertical-slice`.
8. Before feature expansion, run `/architecture-review`, `/security-review`, `/performance-baseline`, then Gate 1.
9. Run `/design-freeze` before building the full Manager UI.
10. Implement V1 in small traceable slices, then `/verify`, `/release-candidate`, `/ship-v1`.

## Critical automation rule

Scripts may automate deterministic engineering chores such as restore, build, test, formatting, dependency audit, packaging, signing orchestration, specification auditing and performance capture. **Scripts must never generate application source code, XAML UI, design-system components, visual layouts, icons, or production copy as a shortcut.** Product code/UI is deliberately authored and reviewed as source.

## Quality claim policy

No agent may claim “zero bugs,” “perfect,” “production-ready,” or a passed quality gate from inspection alone. State machines, invariants, traceability, tests, fault injection, real Windows hardware qualification, visual review and release evidence determine status. Missing evidence means **NOT PROVEN**, not passed.

## Project status

**Enterprise specification-ready. Implementation not yet started.**


## Source ownership

See `LICENSE.md` for the proprietary Ornare source notice and `THIRD-PARTY-NOTICES.md` for third-party licensing. Final public distribution terms remain a release/legal gate.
