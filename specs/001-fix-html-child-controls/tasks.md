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

- [ ] T001 Add `MultipleControlsPath` static property to `src/winforms-designer-mcp.Tests/TestHelpers.cs` pointing to `TestData/MultipleControls.Designer.cs` (mirror the pattern of existing `CSharpSamplePath`)

---

## Phase 2: Foundational — Failing Tests

**Goal**: Establish a clear, reproducible failure that proves the bug before any fix is attempted. These tests must fail on the current codebase.

- [ ] T002 In `src/winforms-designer-mcp.Tests/ToolTests.cs`, add `RenderHtml_GroupBox_ChildrenNestedInParentDiv` — renders `MultipleControlsPath`, reads the HTML string, asserts `data-name="radioButton1"` appears at a string index AFTER `data-name="groupBox1"` opens (confirming nesting); also assert the `controlCount` in the JSON response equals 23 (the 22 declared WinForms controls plus BackgroundWorker, all in `model.Controls`) to cover SC-6
- [ ] T003 In `src/winforms-designer-mcp.Tests/ToolTests.cs`, add `RenderHtml_TabControl_LeafControlsNestedInTabPage` — same HTML string, asserts `data-name="comboBox1"` appears between the `data-name="tabPage1"` open and the next sibling control's open tag, confirming 3-level nesting
- [ ] T004 Run `dotnet test --filter "RenderHtml_GroupBox|RenderHtml_TabControl"` and confirm both new tests FAIL — capture failure output in the terminal (no file write needed). Do NOT proceed to T005 if both tests pass; that would mean the tests are not targeting the real bug and must be revised.

---

## Phase 3: US1 — Diagnose and Fix Child Rendering

**Goal**: Make T002 and T003 pass. Child controls of all container types must appear correctly nested in the HTML output.

Independent test criterion: `dotnet test --filter RenderHtml_GroupBox` passes AND `dotnet test --filter RenderHtml_TabControl` passes.

- [ ] T005 [US1] Run `dotnet run --project src/winforms-designer-mcp -- render-html --file src/winforms-designer-mcp/TestData/MultipleControls.Designer.cs --output /tmp/mc-preview.html`, open `/tmp/mc-preview.html` in a browser and use DevTools to identify exactly why `radioButton1` and `comboBox1` are not visible (clip, z-index, overflow, or structural issue) — document finding in a comment in plan.md under a new "## Root Cause" heading
- [ ] T006 [US1] Apply the targeted fix to `src/winforms-designer-mcp/Tools/RenderFormHtmlTools.cs` and/or `src/winforms-designer-mcp/Templates/FormPreview.html` based on the confirmed root cause from T005 — the fix must be minimal and not change the rendering of flat forms
- [ ] T007 [US1] Run `dotnet test --filter "RenderHtml_GroupBox|RenderHtml_TabControl"` and confirm both tests now PASS; if any still fail, iterate on T006 before proceeding

---

## Phase 4: US2 — Regression Prevention

**Goal**: Confirm existing flat-form rendering is unchanged. The fix from Phase 3 must not break any currently-passing tests.

Independent test criterion: `dotnet test` passes with 0 failures.

- [ ] T008 [US2] In `src/winforms-designer-mcp.Tests/ToolTests.cs`, extend `RenderHtml_SavesFile` with positional nesting assertions: assert the string index of `data-name="button1"` is greater than the string index of `data-name="panel1"` (confirming button1 is nested inside panel1, matching the approach in T002); also assert `data-name="button1"` appears before `data-name="label1"` (a root-level sibling) to confirm button1 is not promoted to the form root — this test must pass without code changes since SampleForm nesting worked before the bug
- [ ] T009 [P] [US2] Run `dotnet test` (full suite) and confirm 0 failures — if any previously-passing test now fails, the fix in T006 caused a regression and must be revised
- [ ] T010 [P] [US2] Run `dotnet build -warnaserror` and confirm 0 errors and 0 warnings

---

## Phase 5: US3 — Sidebar, Property Inspector, and Language Parity

**Goal**: Confirm the sidebar tree and property inspector work for child controls (manual checks), and that C#/VB parity is maintained.

Independent test criterion: Visual browser verification passes for both child-control nesting cases.

- [ ] T011 [US3] Open `/tmp/mc-preview.html` in a browser (regenerate it after T006): click `radioButton1` in the canvas and confirm its properties appear in the sidebar property inspector panel
- [ ] T012 [US3] In the same browser session: click `radioButton1` in the sidebar control tree and confirm its corresponding div in the canvas becomes highlighted and scrolled into view
- [ ] T013 [P] [US3] Run `dotnet run --project src/winforms-designer-mcp -- render-html --file src/winforms-designer-mcp/TestData/SampleForm.Designer.vb --output /tmp/vb-preview.html`, open in browser and confirm `textBox1` (child of panel equivalent) is visible inside its parent container — verifies VB.NET parity
- [ ] T014 [P] [US3] In `src/winforms-designer-mcp.Tests/ToolTests.cs`, add `RenderHtml_VbSample_ChildControlsNestedInPanel` — renders `VbSamplePath`, reads HTML, applies the same positional-string nesting check as T002: asserts `data-name="button1"` (or the VB equivalent child control name — inspect `VbSamplePath` first) appears at an index after `data-name="panel1"` opens, confirming VB.NET parent-child rendering parity
- [ ] T015 [US3] Verify FR-6 (Visible/hidden toggle cascade): open `/tmp/mc-preview.html` in browser, enable "Respect Visible", and confirm that if a container control has `Visible=False` in the designer file, both the container div AND its child control divs become hidden — verifies the CSS `display:none` cascade through `overflow:hidden` container types is not broken by the fix from T006

---

## Phase 6: Polish

**Goal**: Final build and format validation.

- [ ] T016 Run `dotnet build -warnaserror` one final time — this gates the Phase 5 code additions (T014 adds a test method); confirm 0 errors and 0 warnings

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
