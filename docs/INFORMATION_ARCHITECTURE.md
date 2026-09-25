# Information Architecture

## Quick surface

No navigation hierarchy. One context, one action.

- Folder identity
- Assign image
- Recent/Favorites shortcuts when space permits
- Reset when applicable
- Open Ornare / Settings low-emphasis action

## Manager navigation

Maximum primary sections:

1. Library
2. Recent
3. Favorites
4. Folders
5. Settings

Do not add a Home dashboard unless user research proves it creates value. Opening the manager should land on the most contextually useful section (initially Library or Folders, decision can be validated in usability testing without affecting architecture).

## Folders section

Contains health/repair status in context. Do not create a separate “Health dashboard” for V1.

## Settings groups

- Appearance: System / Light / Dark override, default System.
- Language: System default / English / Türkçe if manual override is shipped.
- Behavior: close-after-success and similar user-facing behavior only if testing shows a need.
- Storage/Diagnostics: library usage, diagnostics export, repair/reconcile.
- About: version, licenses, privacy, acknowledgments.

Keep settings small. If a setting only exists because implementation is uncertain, fix the implementation instead of exposing it.
