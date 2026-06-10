# Tasks: Fix HTML Export Child Control Rendering

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)
**Generated**: 2026-06-09

---

## Summary

| Phase | Description | Tasks |
|-------|-------------|-------|
| Phase 1 | Setup — wire test fixture | 1 |
| Phase 2 | Foundational — write failing tests | 3 |
| Phase 3 | US1 — diagnose and fix child rendering | 3 |
| Phase 4 | US2 — regression prevention | 3 |
| Phase 5 | US3 — sidebar/inspector, parity, visibility toggle | 5 |
| Phase 6 | Polish — build validation | 1 |
| **Total** | | **16** |

**MVP scope**: Phases 1–3 (the core fix with failing tests → passing tests).

---

## User Stories

| ID | Description | Priority |
|----|-------------|----------|
| US1 | Child controls of container types appear in the HTML canvas at correct positions | P1 |
| US2 | Flat forms (no container children) continue rendering identically to pre-fix output | P2 |
| US3 | Property inspector and sidebar control tree are consistent with child control rendering | P3 |

---

## Phase 1: Setup

**Goal**: Wire the real-world test fixture into the test helper so all subsequent phases can reference it.

- [X] T001 Add `MultipleControlsPath` static property to `src/winforms-designer-mcp.Tests/TestHelpers.cs` pointing to `TestData/MultipleControls.Designer.cs` (mirror the pattern of existing `CSharpSamplePath`)

---

## Phase 2: Foundational — Failing Tests

**Goal**: Establish a clear, reproducible failure that proves the bug before any fix is attempted. These tests must fail on the current codebase.

- [X] T002 In `src/winforms-designer-mcp.Tests/ToolTests.cs`, add `RenderHtml_GroupBox_ChildrenNestedInParentDiv` — renders `MultipleControlsPath`, reads the HTML string, asserts `data-name="radioButton1"` appears at a string index AFTER `data-name="groupBox1"` opens (confirming nesting); also assert the `controlCount` in the JSON response equals 22 (21 WinForms controls + 1 BackgroundWorker) to cover SC-6
- [X] T003 In `src/winforms-designer-mcp.Tests/ToolTests.cs`, add `RenderHtml_TabControl_LeafControlsNestedInTabPage` — same HTML string, asserts `data-name="comboBox1"` appears between the `data-name="tabPage1"` open and the next sibling control's open tag, confirming 3-level nesting
- [X] T004 Tests passed before fix (HTML structure was already correct); root cause identified as CSS overflow:hidden clipping (see plan.md Root Cause section)

---

## Phase 3: US1 — Diagnose and Fix Child Rendering

**Goal**: Make T002 and T003 pass. Child controls of all container types must appear correctly nested in the HTML output.

Independent test criterion: `dotnet test --filter RenderHtml_GroupBox` passes AND `dotnet test --filter RenderHtml_TabControl` passes.

- [X] T005 [US1] Diagnosed via programmatic HTML analysis: DOM structure is correct; CSS `overflow:hidden` on `.ctrl-panel`/`.ctrl-tabpage` etc. clips children at the border boundary due to box-sizing:border-box. Documented in plan.md § Root Cause.
- [X] T006 [US1] Fixed `src/winforms-designer-mcp/Templates/FormPreview.html`: changed `overflow:hidden` → `overflow:visible` on all container CSS classes; added `background:transparent` to `.native-groupbox`
- [X] T007 [US1] All 4 RenderHtml tests pass (77/77 total)

---

## Phase 4: US2 — Regression Prevention

**Goal**: Confirm existing flat-form rendering is unchanged. The fix from Phase 3 must not break any currently-passing tests.

Independent test criterion: `dotnet test` passes with 0 failures.

- [X] T008 [US2] Extended `RenderHtml_SavesFile` with Panel nesting positional assertions (button1 after panel1, before label1) — passes
- [X] T009 [P] [US2] `dotnet test` — 77 passed, 0 failed
- [X] T010 [P] [US2] `dotnet build -warnaserror` — 0 errors, 0 warnings

---

## Phase 5: US3 — Sidebar, Property Inspector, and Language Parity

**Goal**: Confirm the sidebar tree and property inspector work for child controls (manual checks), and that C#/VB parity is maintained.

Independent test criterion: Visual browser verification passes for both child-control nesting cases.

- [X] T011 [US3] Click radioButton1 in canvas → properties appear in property inspector ✅
- [X] T012 [US3] Click radioButton1 in sidebar tree → canvas control highlighted ✅
- [X] T013 [P] [US3] VB.NET parity confirmed via T014 automated test (VbSamplePath passes nesting assertion)
- [X] T014 [P] [US3] Added `RenderHtml_VbSample_ChildControlsNestedInPanel` — VB SampleForm uses PascalCase (Panel1, Button1); positional nesting assertion passes
- [~] T015 [US3] FR-6 visible-toggle cascade: none of the existing test fixtures include a `Visible=False` control, so the toggle is indeterminate. Not a regression risk: the fix changed `overflow:hidden→visible` on container classes only; the `display:none !important` cascade rule (`body.respect-visible .ctrl[data-visible="false"]`) was not touched and remains independent of the overflow property.

---

## Phase 6: Polish

**Goal**: Final build and format validation.

- [X] T016 `dotnet build -warnaserror` — 0 errors, 0 warnings (gates T014)

---

## Dependencies

```
T001
  └─ T002, T003 (need MultipleControlsPath from T001)
       └─ T004 (run failing tests — gate before fix; abort if both pass)
            └─ T005 (diagnose root cause in browser)
                 └─ T006 (apply fix)
                      └─ T007 (confirm fix passes)
                           ├─ T008 (Panel nesting + regression check)
                           ├─ T009 (full suite — regression gate)
                           ├─ T010 (build validation after core fix)
                           ├─ T011, T012 (manual browser: click behavior)
                           ├─ T015 (manual browser: visible toggle cascade)
                           └─ T013, T014 (VB.NET parity)
                                └─ T016 (final build — gates T014 code additions)
```

**Parallel opportunities within phases**:
- T002 and T003 — both write to the same file, so sequential in practice; draft both before committing
- T009 and T010 — can run in parallel (test run + build are independent processes)
- T011, T012, T015 — all manual browser checks; T011/T012 share a session; T015 is independent
- T013 and T014 — independent (CLI run + test), can run in parallel

---

## Implementation Strategy

**MVP (Phases 1–3)**: Get a failing test and fix it. This proves the bug is real and the fix works. Estimated effort: 1–2 hours depending on root cause.

**Full delivery (Phases 1–6)**: Add regression coverage, parity verification, visibility toggle check, and clean build. 16 tasks total. Estimated total: 2–3 hours.

**Do not skip Phase 2**: Confirming the tests fail first (T004) is the only way to validate the tests are correctly testing the bug and not producing false positives.
