# Phase 4 — Printing Zvlášť exercises (#30)

**Goal:** print every exercise of every Okno, including all exercises of Zvlášť Okno, on the 16 picture boxes of
the sheet, and continue on extra pages when 16 boxes are not enough (decision D12).
Only the first exercise of a Zvlášť Okno gets a difficulty value — the Okno average; the others are blank (issue #30).
The ID field says who performs the exercise: `R1.Top`, `R1.Base`, … (decision D13).

Depends on: Phase 3 and owner task O4 (template edit). Release together with Phase 3.

## Space on the sheet

Today: boxes 1–8 fill the first picture row; boxes 9–12 use the left half of the second row; the right half is free.
After the owner's edit: **16 boxes** (4 new ones in the free area).

| Situation | Exercises | Pages |
|---|---|---|
| 8-Okno group, no Zvlášť | 8 | 1 (boxes 9–16 empty) |
| 12-Okno group, no Zvlášť | 12 | 1 |
| WP, 12 Okno, all Zvlášť | 24 | 2 |
| MG, 12 Okno, all Zvlášť (worst case) | 48 | 3 |

So "a second page" is implemented as "as many pages as needed" (max. 3 with today's groups).
**No capacity limit in the app** — the Zvlášť button is never disabled for lack of space.

## Parts

### 4.1 Template edit (owner, in Acrobat) — task O4
- Add **4 boxes** in the free area, same structure as boxes 1–12 (picture + *Obťažnosť* + *Číslo riadku cv. tvaru*).
- Field names continue the existing numbering (assumption A12):
  `Image1_af_image.12–15`, `ExerciseDifficulity.12–15`, `ExerciseID.12–15`.
- The printed box numbers 1–12 are replaced by **text fields `BoxNumber.0–15`** (one per box, D14);
  the app writes `1`, `2.a`, `2.b`, … into them.
- After the edit the field list is checked with `TrainingPlanPdfIText.DumpFieldNames` (or by the remote session).
- The app reads the number of boxes from the template (count of `Image1_af_image.*`), so it keeps working with
  12 boxes until the new template arrives.

### 4.2 Print list
Build one ordered list of printed exercises from `FormData`:

| Exercise | ID field | Obťažnosť field |
|---|---|---|
| normal Okno | `R{row}` / `InvR{row}` (unchanged) | Okno value |
| Zvlášť Okno, 1st member | `R{row}.{Position}` | **Okno average** |
| Zvlášť Okno, other members | `R{row}.{Position}` | *blank* |

`{Position}` = `Base`, `Top`, `Middle` (`Middle1` / `Middle2` in a quartet, assumption A11).
Inv table: `InvR{row}.{Position}`.

**Order and box numbers (D14, Variant B):** the list is in routine order — Okno 1, Okno 2, … and inside a Zvlášť Okno
the members in Base → Top order. Item *k* goes to box *k* (box index restarts on every page).
Box number text: `{n}` for a normal Okno, `{n}.a`, `{n}.b`, … for a Zvlášť Okno (assumption A13).

Example (Do 10, WP, Okno 2 is Zvlášť from row 3):

| Box | 1 | 2 | 3 | 4 | … | 13 |
|---|---|---|---|---|---|---|
| `BoxNumber` | 1 | 2.a | 2.b | 3 | … | 12 |
| `ExerciseID` | R5 | R3.Base | R3.Top | InvR1 | … | … |
| `ExerciseDifficulity` | 0,030 | 0,020 | *(blank)* | 0,010 | … | … |

### 4.3 Pages
- Split the print list into chunks of *box count* (16).
- Every page = the same template, filled with the **same header** (event, club, trainer, contact, categories,
  discipline, team size, athletes, *Obťažnosť spolu*) and its own chunk of boxes.
- Each page is filled and flattened separately in memory, then the pages are joined into the output file with
  iText's `PdfMerger` (part of the `itext` package already used — **no new package**).
- Unused boxes on the last page: picture fields removed, text fields left empty → blank boxes.
- *Obťažnosť spolu* is the total of the whole plan on every page.

### 4.4 Verification
- Export test PDFs (12 Okno no Zvlášť; WP with one Zvlášť Okno; MG with Zvlášť everywhere → 3 pages), render them to PNG
  with the Windows PDF API (no extra tools needed) and check placement, IDs, values and page headers.
- Compare the "no Zvlášť" export with the Phase 2 output — must be identical (apart from the 4 new empty boxes).

## Implementation notes (2026-10-27)
- Implemented **before** the template edit, so it works with both templates:
  - **current 12-box template** (no `BoxNumber` fields): 12 boxes per page; where a box number differs from the printed
    one (e.g. box 3 = `2.b`) the printed number is covered with white and redrawn (Helvetica 8.5 pt, same spot);
    unused boxes on extra pages get their number blanked. A plan without Zvlášť prints exactly as before.
  - **edited template**: the app counts `Image1_af_image.*` (→ 16 per page) and fills `BoxNumber.k` when present.
- IDs: `R{row}.{Position}` with `Base`, `Middle`, `Top`; MG uses `Middle2` / `Middle1` (A11) — the 2nd Middle card
  chooses before the 1st (Base → Top order).
- A Zvlášť Okno may continue on the next page (`12.a` on page 1, `12.b` on page 2) — assumption A13.
- Verified on the Windows host: WP with Zvlášť on Okno 2 (2 pages) and on Okno 12 (split), MG with Zvlášť on all
  12 Okno on a **simulated** 16-box template with `BoxNumber` fields (48 boxes → 3 pages, total 0,156),
  Do 10 without Zvlášť still pixel-identical.
- The simulated template was generated with a scratch iText tool (not in the repo); the real edit stays with the owner (O4).
- **2026-10-28:** the owner's 16-box template replaced `Cvicebny-plan_Template.pdf` (O4 done). It adds exactly
  `Image1_af_image.12–15`, `ExerciseDifficulity.12–15`, `ExerciseID.12–15` (A12 held); all other fields are unchanged.
  It has **no `BoxNumber` fields** — the numbers 1–16 are printed text, so the redraw fallback is used
  (13–16 are slightly smaller, 6.8 vs 8.2 pt glyph height, but sit inside the cover rect).
- **2026-10-28, tested on the host** with the real template: Do 10 (12 boxes), Do 14 MG with 5 Zvlášť Okno
  (27 boxes → 2 pages), Od 8 – 14 WG, Do 8 MxP (Zvlášť) and Individuálna zostava — all print correctly; the redrawn
  numbers over the printed 13–16 leave no traces (3× zoom). Found: with Zvlášť on a single page the unused boxes kept
  their printed numbers, so the sheet read "… 7, 8, 11, 12, …". Now unused numbers are blanked on every renumbered
  page too (not only on extra pages); a plan without Zvlášť is unchanged (pixel-identical).

## Acceptance
- Every exercise appears exactly once; IDs show the position for Zvlášť exercises.
- Only the first box of a Zvlášť Okno has a difficulty value; *Obťažnosť spolu* matches the app on every page.
- More than 16 exercises → additional pages that look exactly like page 1 except for the boxes.
- Box numbers on the sheet match the app's Okno (`2.a`, `2.b` next to each other).
- Works with the old 12-box template (more pages, numbers stay the printed 1–12) and the new 16-box template.
