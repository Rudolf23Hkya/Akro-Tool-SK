# Phase 8 — Zvlášť rework: Inv table only, Top → Base order, greyed value cells (#43, #46, #44)

Status: **implemented** on `feature/phase-8-issues-43-44-46` · 2026-10-30, with the proposals for Q7–Q8 in [00-decisions-and-open-questions.md](00-decisions-and-open-questions.md#3-open-questions).

All three issues change the Zvlášť feature from Phases 3/4, so they go into **one PR** (one commit per part).
Order: **8.1 (#46)** first — it changes the member list every other part uses — then **8.2 (#43)**, then **8.3 (#44)**.
Phase 9 (#45, history) comes after this phase, because the saved plan format must describe the final Zvlášť model.

## 8.1 Member order Top → Base (#46)

**Today:** the picker opens for the members **Base → Top** (assumption A4), and the boxes print in the same order
(`2.a` = Base … `2.d` = Top, A13).

**Wanted:** choosing **and** printing in the order **Top, Middle 1, Middle 2, Base** — `2.a` = Top, `2.b` = Middle 1,
`2.c` = Middle 2, `2.d` = Base.

| Category | Picker / box order (new) |
|---|---|
| WP / MP / MxP (pair) | Top, Base |
| WG (trio) | Top, Middle (1st card), Base |
| MG (quartet) | Top, Middle1 (1st card), Middle2 (2nd card), Base |

**Change:** only the order of the list in `MainWindowViewModel.GetTeamMembers()`. Everything else follows from it:
- `OpenSlotSeparate` loops over that list → picker order and subtitle `… – 1/4` change automatically.
- `PickedExercise.MemberIndex` is an index into that list → the performer overlay and ‹ › still match.
- `ToPrintBoxes` writes the exercises in the stored order → `n.a` = Top. The IDs keep their positions
  (`InvR1.Top`, `InvR1.Middle1`, `InvR1.Middle2`, `InvR1.Base`), so nothing changes in the PDF code.
- Only the **first** box (`n.a`, now Top) gets the Okno value — unchanged rule.
- Fallback names follow the new order: `Pretekár 1` = Top.
- The age check (`ConfirmAgeGroupOrPrintAnyway`) uses the same list; the order does not matter there.

Docs: A4 / A13 in the decisions file and CLAUDE.md §6/§7 ("Base → Top" → "Top → Base").

**Check:** MG Zvlášť → picker subtitles Top, Middle1, Middle2, Base; tile ‹ › starts at Top; printed boxes
`2.a … 2.d` with IDs `.Top`, `.Middle1`, `.Middle2`, `.Base`, value only under `2.a`.

## 8.2 Zvlášť only from the Inv table (#43)

**Today:** Zvlášť works on **both** tables (decision D6); the first member chooses the table.

**Wanted:** the Zvlášť button **always opens the Inv table**, locked; a Zvlášť Okno can never use the team table.
This replaces D6 (new decision D21).

**Change:**
1. `OpenSlotSeparate`: the first member's picker opens with `startWithIndividualTable: true, lockTableToggle: true`
   instead of `GetTableRule`. Members 2..n stay as today (Inv, locked, only the first member's row).
2. Zvlášť is only possible while an Inv pick is left (6 for Do 10 / Do 14, 3 for Do 8 / Od 8 – 14), counted
   **without** the Okno being edited (`CountInvExercisesExcludingSlot`):
   - **Empty Okno:** the *Zvlášť* button is shown only when an Inv pick is left (Q7). `CanSeparate` is refreshed for
     every empty tile in `RecalculateExerciseSummary`, so it disappears when the last Inv pick is used and comes back
     when one is freed.
   - **Filled Okno → `SlotChangeDialog`:** the *Zvlášť* choice is disabled with the tooltip
     *Všetky Inv cvičenia sú už zvolené* when no Inv pick is left. (A filled **Inv** Okno always frees its own pick,
     so there it is always allowed.)
3. The row rule needs no change: the picker already blocks Inv rows used by other Okno.
4. The `Zvlášť` tooltip (D15) gets a hint: *Zvoliť cvičenie zvlášť pre každého pretekára (individuálne cvičenia)*.

No saved plans exist yet (history comes in Phase 9), so there is no old Zvlášť-from-team-table data to migrate.

**Check:** Do 10 WP: Zvlášť opens Inv, toggle disabled; after 6 Inv Okno no *Zvlášť* button on empty Okno and the
dialog choice is disabled on a filled team Okno; clearing one Inv Okno brings the button back. Same on Do 8 / Od 8 – 14
(3 Inv picks). Individuálna zostava: still no Zvlášť.

## 8.3 Greyed value cell under `n.b`, `n.c`, `n.d` (#44)

**Today:** under a Zvlášť Okno only the `n.a` box has a value in the *Obťažnosť* line; the other boxes are **blank**,
which reads like a missing value.

**Wanted:** the *Obťažnosť* cell under `n.b`, `n.c`, `n.d` is **greyed out**.

**Change (`TrainingPlanPdfIText`):**
1. `PrintBox` gets a flag `IsContinuation` (true for `n.b`, `n.c`, …), set in `ToPrintBoxes`.
2. In `FillPage`, for such a box read the widget rectangle of `ExerciseDifficulity.k` (same way as the picture rects)
   and, after `FlattenFields()`, draw a filled light-grey rectangle there with `PdfCanvas` (inset ~0.5 pt so the table
   lines stay visible). Grey tone: Q8, proposal 80 % white (`DeviceGray 0.8`) — visible on paper and on a b/w copier.
3. Empty boxes (13–16 or the rest of an extra page) stay white — only continuation boxes are greyed.
4. Works with any number of pages; the template is not changed.

**Check:** render the 2-page Do 14 MG plan with 5 Zvlášť Okno (`tools/render-pdf.ps1`, 3× zoom): grey cell under every
`.b/.c/.d` box, value under every `.a` box, table lines intact, normal boxes unchanged.

## Done when
- `dotnet build` 0 warnings; UI smoke test extended for Zvlášť (Inv lock, button hidden when Inv is full, member order).
- PDFs rendered and checked for Do 10 pair, Do 14 MG (2 pages), Do 8.
- CLAUDE.md §6/§7 and the decisions file updated (D6 → D21, A4, A13); #43, #44, #46 closed after the owner's test.

## Implementation notes (2026-10-30)
- 8.1: only the order in `GetTeamMembers()` changed; the age check no longer needs `.Reverse()` (the list is Top first).
- 8.2: `CanSeparateSlot(slotIndex)` (team age group + Inv table + an Inv pick left without this Okno) drives the
  empty-Okno button (`RecalculateExerciseSummary`), the `SlotChangeDialog` choice (`IsEnabled` + tooltip, shown on
  disabled) and guards `OpenSlotSeparate`. Every Zvlášť picker opens on Inv with the toggle locked.
- 8.3: `PrintBox.IsContinuation`; the `ExerciseDifficulity.k` widget rects are read before flattening and filled
  with `DeviceGray 0.8` (inset 0.5 pt) after `FlattenFields()`.
- `tools/ui-smoke-test.ps1`: Zvlášť Okno always take an Inv row; checks Inv forced + toggle locked, member 1 = Top,
  last = Base, the Zvlášť button only while an Inv pick is left, and the change dialog (disabled on a team Okno when
  Inv is full, enabled on an Inv Okno).
- Host tests passed: Do 14 MG with Zvlášť on Okno 1, 3, 5, 7, 9 (27 boxes, 2 pages, 673 KB — grey cells under every
  `.b/.c/.d`, values under `.a`, empty boxes white, table lines intact); Do 8 MxP with Zvlášť on Okno 1–3 (button gone
  on Okno 4–8); Do 10 WP without Zvlášť (unchanged).
