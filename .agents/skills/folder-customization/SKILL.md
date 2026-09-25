---
name: folder-customization
description: Use for desktop.ini, folder attributes, icon application/reset/repair, durable relative icon files, shell refresh, and rollback logic.
---
# Folder Customization Skill

Read `docs/FOLDER_CUSTOMIZATION.md`.

Rules:

- baseline existing state before modification;
- merge icon fields, preserve unrelated desktop.ini content;
- use Ornare-owned hidden folder ICO + relative reference;
- canonical library is recovery/reuse layer, not active absolute dependency;
- use temp writes/atomic replacement where possible;
- rollback on failure;
- targeted shell notification/refresh only;
- reset restores prior state, not blindly default;
- no security-policy weakening for remote paths.

Study FolderColorizer MIT implementation for proven ideas, but audit every reused line and do not copy its legacy context-menu architecture.
