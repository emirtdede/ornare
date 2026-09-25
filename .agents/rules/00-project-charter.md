---
description: Non-negotiable Ornare product scope and decision hierarchy.
---
# Project Charter

- Build Ornare as a production Windows 11 folder-icon utility, not a prototype.
- Read root `AGENTS.md` and authoritative specs before implementation.
- Primary workflow is `Folder → Set Folder Icon → drop/choose/paste → done`.
- Do not add V1-excluded features before all V1 acceptance gates pass.
- Do not ask the owner to re-decide frozen choices.
- If a new fact forces a material change, research it, write an ADR, update impacted specs, then implement.
- Never optimize for easiest code at the expense of user experience, reliability or security.
