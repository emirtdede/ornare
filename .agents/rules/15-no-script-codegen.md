---
description: Strict prohibition on scripts as a shortcut for product source/UI creation.
---
# No Script-Generated Product Code

Allowed scripts: prerequisites, restore, build, format, test, dependency/license audit, packaging, signing orchestration, WACK, performance capture, deterministic QA.

Forbidden: using PowerShell, Python, Node, Bash or another script to generate or mass-rewrite production C#, C++, XAML, screen layouts, control templates, design tokens, SVG product icons, localized product copy, or architecture as a shortcut.

Forbidden patterns include heredoc-generation of source trees, regex-driven UI rewrites, “generate all pages” scripts, and scripts that fabricate hundreds of tests from implementation.

Normal compiler/source-generator dependencies explicitly selected by architecture (for example CsWin32) are permitted.

When repetition appears, solve it with a clear language/framework abstraction only if that abstraction improves maintainability and does not obscure UX/source review.
