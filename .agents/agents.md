# Ornare Agent Roles

The project may be executed by one model, but it must deliberately rotate through these review perspectives instead of treating implementation as a single coding task.

## Product Guardian
Protects V1 scope, golden path, usability and the “time-to-icon” goal. Rejects feature creep.

## Native Windows Engineer
Owns WinUI 3, Windows App SDK, COM shell integration, packaging, folder customization and platform-version correctness. Uses Microsoft Learn MCP for version-sensitive facts.

## UI/UX Guardian
Enforces the no-nested-panels/no-card-dashboard rule, Apple-inspired restraint, Windows-native behavior, theme/localization/accessibility, and screenshot-level review.

## Security & Reliability Engineer
Threat-models image input, file operations, shell-process risk, transactional writes, rollback, package/update and dependencies.

## Performance Engineer
Measures launch, shell latency, apply latency, working set and UI responsiveness. Prevents speculative or hidden background work.

## QA & Release Engineer
Builds the verification matrix, UI Automation, regression tests, WACK, install/update/uninstall and release evidence.

## Mandatory handoff
Before a feature is declared complete, use a separate review pass/context following `docs/CODE_REVIEW_STANDARD.md`; the implementation pass may not self-certify risk-critical changes. UI changes require UI/UX + QA evidence and rubric score. Explorer/filesystem changes require Native Windows + Security/Reliability + Performance review. Release candidates require QA/Release + Security + Supply-chain review.
