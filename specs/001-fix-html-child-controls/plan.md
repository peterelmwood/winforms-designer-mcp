# Implementation Plan: Fix HTML Export Child Control Rendering

**Feature**: [spec.md](./spec.md)
**Branch**: main
**Created**: 2026-06-09

---

## Technical Context

### Affected Files

| File | Role |
|------|------|
| `src/winforms-designer-mcp/Tools/RenderFormHtmlTools.cs` | Primary rendering code |
| `src/winforms-designer-mcp/Templates/FormPreview.html` | CSS/template (may need CSS changes) |
| `src/winforms-designer-mcp.Tests/ToolTests.cs` | Test coverage (needs new assertions) |
| `src/winforms-designer-mcp/TestData/SampleForm.Designer.cs` | Contains nested controls (panel1 → button1, textBox1) — smoke test |
| `src/winforms-designer-mcp/TestData/MultipleControls.Designer.cs` | **Primary complex test fixture** (sourced from dotnet/winforms integration suite) |

### Data Flow

```
.Designer.cs/.vb
    → Parser → FormModel { Controls (flat), RootControls (tree) }
    → RenderFormHtmlTools.RenderFormHtml()
        → RenderHtmlControls(model.RootControls)  ← renders canvas HTML
        → RenderTreeNodes(model.RootControls)       ← renders sidebar
        → EmitControlDataJson(model)                ← renders JS data
    → FormPreview.html template substitution
    → .html file
```

### Key Rendering Logic

`RenderHtmlControls` iterates `controls` in reverse Z-order. For each control it:
1. Opens a `<div class="ctrl ctrl-{type}">` with absolute positioning
2. Calls `RenderControlInner` to render the control's inner HTML
3. **Recursively calls `RenderHtmlControls(ctrl.Children, ...)` for child controls**
4. Closes the `</div>`

The parser is confirmed correct: `panel1.Children = [button1, textBox1]` for the SampleForm.

## Root Cause

**Diagnosis (2026-06-10):** The C# rendering code (`RenderHtmlControls`) and the sidebar tree (`RenderTreeNodes`) correctly generate nested HTML for all container types at every depth. Tests T002/T003/T008/T014 all pass before any code change, confirming the DOM structure is correct.

The visual bug is caused by **`overflow: hidden` on container CSS classes combined with `box-sizing: border-box` and explicit borders**:

- CSS classes `.ctrl-panel`, `.ctrl-tabpage`, `.ctrl-splitcontainer`, `.ctrl-flowlayoutpanel`, `.ctrl-tablelayoutpanel` all have `overflow: hidden` and `border: 1px dashed #bbb`
- With `box-sizing: border-box`, a container specified at `width: 260px` has a CSS content/padding area of only 258px (after subtracting 2×1px borders)
- WinForms `Location` coordinates are relative to the parent's **client area** (inside the border), so a child at `Location.X = 258` is valid in WinForms but maps to `left: 258px` in CSS, placing it exactly at the overflow clipping boundary
- Controls near or at the edge of their parent container can be partially or fully clipped (invisible) in the browser preview

**Secondary issue:** The `<fieldset class="native-groupbox">` element has no explicit `background: transparent`. Some browser UA stylesheets render `<fieldset>` with a non-transparent background that can obscure child controls rendered after it in the DOM.

**Fix:** Change `overflow: hidden` → `overflow: visible` on all container CSS classes; add `background: transparent` to `.native-groupbox`. These changes ensure all child controls are always visible in the design preview, regardless of their exact coordinates relative to the parent boundary.

### Suspected Root Causes (to be confirmed by failing test)

The bug likely manifests as one or more of the following:

**Root Cause A — CSS `overflow: hidden` clipping**
Container types (Panel, SplitContainer, FlowLayoutPanel, TableLayoutPanel, TabPage) have `overflow: hidden` in the CSS. While children positioned within bounds are visible, children that extend even slightly beyond container bounds are clipped entirely if `box-sizing` and borders interact unexpectedly. More critically, `overflow: hidden` on an absolutely-positioned parent also suppresses the `overflow: visible` that `.ctrl` sets, which can cause visual masking of children positioned at the boundary.

**Root Cause B — `GroupBox` fieldset stacking**
`RenderControlInner` for GroupBox renders `<fieldset class="native-groupbox">` with `position: absolute` and no explicit `background: transparent`. Some browsers render a default fieldset background that covers sibling child controls rendered after it.

**Root Cause C — Container `<div>` size defaults**
For any container whose `Size` property is missing from the parsed model, the renderer defaults to `width:75px; height:23px`. Any child control would be clipped by `overflow: hidden` at this tiny size, making all children invisible.

**Root Cause D — Z-index stacking context**
Child controls' z-index values are computed relative to the siblings list (e.g., `zIndex = controls.Count - i`). These values restart at low numbers inside each container. A container at z-index 1 with children at z-index 1 or 2 may be visually covered by a sibling at z-index 3 in the parent layer if z-index contexts aren't isolated.

---

## Research

### Decision: Use `MultipleControls.Designer.cs` from dotnet/winforms as the primary test fixture

**Source**: `https://github.com/dotnet/winforms/blob/main/src/test/integration/WinformsControlsTest/MultipleControls.Designer.cs`  
**License**: MIT (The .NET Foundation)  
**Added to**: `src/winforms-designer-mcp/TestData/MultipleControls.Designer.cs`

This file was chosen because:
- Zero `resources.ApplyResources` calls — all coordinates are explicit `new System.Drawing.Point/Size(...)`
- Exercises the exact control hierarchies that exercise the bug:
  - **3-level**: `tabControl1` → `tabPage1/2` → `comboBox1` / `checkBox1,2`
  - **2-level**: `groupBox1` → `radioButton1,2`
- Uses 16 distinct WinForms control types
- All child coordinates fall within their parent bounds (no pathological edge clipping)
- Is an active real-world test file maintained by the dotnet/winforms team

**Alternatives considered**:
- `Panels.Designer.cs` from same repo — rejected: uses `resources.ApplyResources` for all Size/Location (no explicit coordinates)
- `FlexGridShowcaseDemo` — rejected: uses third-party Ribbon control outside `System.Windows.Forms`
- `formatting-utility/Form1.Designer.cs` — rejected: flat form, no container nesting

**Concrete control hierarchy for test assertions**:
```
MultipleControls (746×458)
├── progressBar1    (0,0)   331×27
├── button1         (15,50)  88×27
├── label1          (15,84)  38×15
├── maskedTextBox1  (15,104) 116×23
├── richTextBox1    (15,135) 116×110
├── textBox1        (15,264) 116×23
├── linkLabel1      (62,84)   60×15
├── linkLabel2      (378,255) 108×21
├── checkedListBox1 (378,49)  140×112
├── numericUpDown1  (378,197) 140×23
├── domainUpDown1   (378,227) 140×23
├── checkedListBox2 (525,49)  140×58
├── tabControl1     (141,50)  233×115
│   ├── tabPage1    (4,24)    225×87
│   │   └── comboBox1 (22,30) 140×23
│   └── tabPage2    (4,24)    225×87
│       ├── checkBox1 (8,22)   83×19
│       └── checkBox2 (8,50)  153×19
└── groupBox1       (146,197) 206×93
    ├── radioButton1 (19,29)   99×20
    └── radioButton2 (19,55)   99×20
```

### Decision: Diagnostic-first approach

**Rationale**: The exact root cause cannot be confirmed without a failing test, because for the SampleForm's specific coordinates all children fall within container bounds. The plan begins with writing a targeted failing test to expose the bug, then fixes based on confirmed diagnosis.

**Alternatives considered**:
- Fixing all suspected root causes preemptively → risks over-engineering; fix only what the test proves broken
- Adding a new complex TestData file → useful but secondary; the inline test string approach is faster for diagnosis

---

## Data Model

No new domain entities. The existing models are correct:

```
FormModel
  ├── RootControls: List<ControlNode>   ← hierarchical (used by renderer)
  └── Controls: List<ControlNode>       ← flat (used by property inspector)

ControlNode
  ├── Name: string
  ├── ControlType: string
  ├── Properties: Dictionary<string, string>
  ├── Children: List<ControlNode>       ← populated by parser; must render in HTML
  └── Events: List<EventWiring>
```

The fix must not change these models.

---

## Interface Contracts

### MCP Tool: `render_form_html`

The public interface is unchanged. The tool's output contract is:

**Input**: `{ filePath: string, outputPath: string }`

**Output JSON**:
```json
{
  "success": true,
  "format": "html",
  "outputPath": "/path/to/form.html",
  "formName": "Form1",
  "dimensions": { "width": 296, "height": 261 },
  "controlCount": 5,
  "message": "Interactive HTML preview saved to ..."
}
```

**HTML contract** (the fix makes these explicit):
- Every control in `FormModel.Controls` must appear as a `.ctrl[data-name="{name}"]` element in the HTML
- Child controls must be nested inside their parent container's `.ctrl` div
- Child control `left`/`top` CSS values must equal the child's `Location.X` / `Location.Y` property values (container-relative coordinates)
- The sidebar tree must mirror the control hierarchy (already works; must not regress)

---

## Implementation Tasks

### Task 1 — Write failing tests for child control rendering

**File**: `src/winforms-designer-mcp.Tests/ToolTests.cs`

Add a `TestHelpers` path property for the new complex test fixture:
```csharp
public static string MultipleControlsPath =>
    Path.Combine(AppContext.BaseDirectory, "TestData", "MultipleControls.Designer.cs");
```

Add two new test facts against `MultipleControls.Designer.cs`:

**Test A — GroupBox children appear inside parent div**:
1. Call `RenderFormHtmlTools.RenderFormHtml` with `MultipleControlsPath`
2. Read the generated HTML as a string
3. Find the index of `data-name="groupBox1"` opening tag
4. Find the next index of `data-name="groupBox1"` in a closing context (the matching `</div>`)
5. Assert that `data-name="radioButton1"` and `data-name="radioButton2"` both appear BETWEEN those indices
6. Assert they do NOT appear BEFORE the groupBox1 opening tag (i.e., they are truly nested)

**Test B — TabControl/TabPage/leaf chain is correctly nested**:
1. Same HTML string
2. Assert ordering: `data-name="tabControl1"` appears, then `data-name="tabPage1"`, then `data-name="comboBox1"`, before the tabControl1's closing region
3. Assert `data-name="comboBox1"` does NOT appear as a direct child of the form canvas (it must be inside tabPage1)

**Assertion approach** (no HTML parser needed — string positional checks suffice):
```csharp
var gb = html.IndexOf("data-name=\"groupBox1\"");
var rb1 = html.IndexOf("data-name=\"radioButton1\"");
Assert.True(rb1 > gb, "radioButton1 should appear after groupBox1 opens");
// Also verify radioButton1 is before the NEXT root-level control after groupBox1
```

**Acceptance**: The new tests fail before the fix and pass after.

Also add a smoke-test extension to `RenderHtml_SavesFile`:
- After `Assert.Contains("<!DOCTYPE html>", content)`, assert `Assert.Contains("data-name=\"groupBox1\"", content)` and `Assert.Contains("data-name=\"radioButton1\"", content)`.

### Task 2 — Wire `MultipleControls.Designer.cs` into build output (TestData copy)

**Already done**: The file is at `src/winforms-designer-mcp/TestData/MultipleControls.Designer.cs`.

Verify the `.csproj` already has `<Compile Remove="TestData\**" />` and copies TestData to output (confirmed in AGENTS.md). No `.csproj` change needed — the existing glob handles it.

### Task 3 — Diagnose and fix `RenderHtmlControls` / CSS

After Task 1 produces a failing test, diagnose the exact root cause by:
1. Rendering the test form and inspecting the HTML visually in a browser
2. Using browser DevTools to identify why children don't appear (clipped? hidden? wrong position?)

**If Root Cause A (overflow:hidden clipping):**
- Add `overflow: visible` to `.ctrl-groupbox` (already the default but make explicit)
- For panel-type containers: either remove `overflow: hidden` or add a minimum-size guard. Consider whether `overflow: visible` is acceptable for panels (controls won't be clipped at the panel boundary, but the visual fidelity is better than invisible children)
- Alternatively, adjust the rendering to compute an explicit container size from the parser model

**If Root Cause B (GroupBox fieldset):**
- Add `background: transparent` to `.native-groupbox` CSS in `FormPreview.html`
- Or replace the `<fieldset>` with a positioned `<div>` that only shows the legend label

**If Root Cause C (missing Size defaults too small):**
- Change the fallback: inspect the child's coordinates to compute a bounding box for containers that have no explicit Size
- Add a warning log entry when a container control's size defaults

**If Root Cause D (z-index stacking context):**
- Apply `isolation: isolate` to each `.ctrl` container OR ensure child z-index values are higher than parent within the same stacking context

The fix should be minimal and targeted to the confirmed root cause.

### Task 4 — Ensure C# and VB.NET parity

Run the same assertions against `SampleForm.Designer.vb` to confirm child control rendering works for both languages. The rendering layer is language-agnostic (both parsers produce the same `FormModel`), but confirm this holds.

### Task 5 — Update existing `RenderHtml_SavesFile` test

Extend the existing test to assert not just `<!DOCTYPE html>` but also:
- `data-name="panel1"` appears in the output
- `data-name="button1"` appears in the output (verifying it survived rendering)

This is a weaker assertion than Task 1 (doesn't verify nesting) but serves as a smoke test.

---

## Validation Criteria

Before closing this feature:

- [ ] `dotnet test` passes with 0 failures
- [ ] `dotnet build -warnaserror` passes with 0 errors and 0 warnings
- [ ] New test in Task 1 specifically asserts child controls are nested inside parent `.ctrl` div
- [ ] Test covers at least Panel and GroupBox container types
- [ ] Both C# and VB.NET forms produce correct child rendering
- [ ] Clicking a child control in the browser preview highlights it in the sidebar (manual check)
- [ ] The property inspector shows child control properties when clicked (manual check)
- [ ] Control count in header bar matches total controls in form (including children)

---

## Build & Test Commands

```bash
dotnet build -warnaserror          # Must pass before merging
dotnet test                        # Run all tests
dotnet test --filter RenderHtml    # Run only HTML rendering tests
```

To manually inspect the HTML output:
```bash
dotnet run --project src/winforms-designer-mcp -- render-html \
  --file src/winforms-designer-mcp/TestData/SampleForm.Designer.cs \
  --output /tmp/preview.html
# Then open /tmp/preview.html in a browser
```
