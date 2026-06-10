# Feature Specification: Fix HTML Export Child Control Rendering

**Feature Directory**: `specs/001-fix-html-child-controls`
**Created**: 2026-06-09
**Status**: Draft

---

## Overview

The `render_form_html` tool generates an interactive HTML preview of a WinForms form. When a form contains container controls (such as panels, group boxes, or tab pages) that have child controls nested inside them, those child controls do not appear in the rendered HTML output. The parent container renders correctly, but its children are absent from the visual preview.

This feature fixes the HTML export so that all controls in the form's control hierarchy — at every level of nesting — are correctly rendered and visible in the browser preview.

---

## Problem Statement

Users who invoke `render_form_html` on a WinForms designer file with nested controls see an incomplete preview. Container controls appear as empty shells with no child content, making the preview misleading and unusable for forms with any meaningful layout hierarchy. The tool's value depends entirely on accurately representing the full visual structure of the form.

---

## User Scenarios & Testing

### Scenario 1: Panel with child controls

**Given** a WinForms designer file where a `Panel` contains several child controls (e.g., labels, text boxes, and a button)  
**When** the user runs `render_form_html` on that file  
**Then** the HTML output shows the panel boundary AND all child controls positioned within it

### Scenario 2: GroupBox with child controls

**Given** a WinForms designer file where a `GroupBox` contains radio buttons or check boxes  
**When** the user runs `render_form_html` on that file  
**Then** the HTML output shows the GroupBox border with its legend text AND all child controls inside it

### Scenario 3: TabControl with TabPages containing controls

**Given** a WinForms designer file with a `TabControl` whose `TabPage` children each contain controls  
**When** the user runs `render_form_html` on that file  
**Then** the first tab page and its child controls are visible by default, and switching tabs reveals the child controls of each tab page

### Scenario 4: Deeply nested containers

**Given** a form with multiple levels of nesting (e.g., a panel inside a tab page, with buttons inside the panel)  
**When** the user runs `render_form_html` on that file  
**Then** all controls at every nesting depth are rendered correctly in the HTML output

### Scenario 5: Flat form (no child controls)

**Given** a WinForms designer file where all controls are direct children of the form (no container nesting)  
**When** the user runs `render_form_html` on that file  
**Then** the HTML output is unchanged — this fix must not regress forms without nested controls

### Scenario 6: Control tree sidebar reflects hierarchy

**Given** a form with nested controls  
**When** the user views the HTML preview in a browser  
**Then** the sidebar control tree shows the correct parent-child hierarchy, and clicking a child node in the tree highlights the corresponding child control in the canvas

---

## Functional Requirements

### FR-1: Child controls of all container types must render

All control types that can contain children must render their child controls in the HTML output. This includes at minimum: Panel, GroupBox, TabPage, TabControl, SplitContainer, FlowLayoutPanel, and TableLayoutPanel. Unknown container types (any control with children not in the known list) must also render their children.

### FR-2: Child control positions are relative to their parent container

Each child control's rendered position in the HTML preview is relative to its parent container's top-left corner, matching WinForms layout semantics. A child at `Location(10, 20)` inside a panel appears 10px from the panel's left edge and 20px from its top edge.

### FR-3: All nesting depths are supported

The fix applies recursively. A child control that is itself a container must also render its own children, at any depth of nesting that appears in the designer file.

### FR-4: No regressions for existing rendering

Controls that currently render correctly — leaf controls (Button, TextBox, Label, etc.) and root-level controls — continue to render correctly after the fix. The visual output for flat forms must be identical to the pre-fix output.

### FR-5: Control tree sidebar remains consistent with canvas

The sidebar's hierarchical control tree and the canvas rendering of controls must always agree on which controls exist. A control visible in the tree must also be visible (or hideable via the "Respect Visible" toggle) in the canvas.

### FR-6: Visible/hidden toggle cascades to children

When a container control has `Visible = False`, toggling the "Respect Visible" option hides the container AND all of its children. Children of a hidden container must not remain visible when the parent is hidden.

---

## Success Criteria

1. A WinForms form with at least two levels of control nesting (container with children) produces an HTML preview where all child controls are visually present and positioned correctly inside their parent container.
2. A form with three or more levels of nesting produces an HTML preview where controls at every depth are visible.
3. For a form where all controls are direct children of the form (no container nesting), every control's rendered HTML element appears in the output with the same CSS `left`, `top`, `width`, and `height` values as derived from its `Location` and `Size` properties — no controls are added, removed, or repositioned compared to the pre-fix output.
4. A user can click any child control in the canvas and see its properties appear in the property inspector panel.
5. A user can click a child control's entry in the sidebar tree and the corresponding canvas control becomes highlighted and scrolled into view.
6. The control count displayed in the header bar matches the total number of controls in the form, regardless of nesting depth.

---

## Out of Scope

- Changing the visual styling or layout of the HTML preview beyond what is necessary to make child controls visible.
- Adding new control type mappings (e.g., new native HTML element renderings for types not currently handled).
- Modifying the WinForms parser or writer — this fix is limited to the HTML rendering layer.
- Rendering controls that are genuinely absent from the parsed `FormModel` (parser correctness is a separate concern).

---

## Assumptions

- The parser (`CSharpDesignerFileParser` / `VbDesignerFileParser`) already correctly builds the `FormModel.RootControls` hierarchy, including `ControlNode.Children`. The bug is in the HTML rendering layer, not the parsing layer.
- Child control `Location` property values in the parsed model are container-relative, consistent with WinForms semantics.
- The fix must preserve C# and VB.NET parity — both file types go through the same HTML rendering code, so fixing one fixes both.

---

## Dependencies

- `Tools/RenderFormHtmlTools.cs` — primary file to change
- `Templates/FormPreview.html` — may require CSS changes if child control visibility is a styling issue
- `src/winforms-designer-mcp.Tests/` — new or updated tests to cover nested control rendering
