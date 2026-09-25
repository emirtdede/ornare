# ADR 0003 — No script-generated product code or UI

Status: Accepted

## Decision

Scripts are permitted for deterministic developer operations but may not generate production C#/C++/XAML, layouts, design-system components, SVG UI assets or application copy.

## Rationale

The project owner explicitly requires deliberate source-level implementation and rejects shortcuts that produce unreviewed/generic UI. Normal compiler/source-generator behavior of required platform tooling (for example CsWin32) is allowed because it is a declared build dependency, not an agent shortcut for authoring the product.
