# Phase 2 — New age groups (#27)

**Goal:** add *Do 8 rokov*, *Od 8 – 14 rokov* and *Individuálna zostava* with the tables from
[01-age-group-tables.md](01-age-group-tables.md), working end-to-end with mock pictures.

Depends on: Phase 1.

## Parts

### 2.1 Enum values and definitions
- `Rulebook` gets `DO_8_ROK`, `OD_8_14_ROK`, `INDIVIDUAL` (order = ComboBox order, assumption A9):
  Do 8 rokov, Do 10 rokov, Do 14 rokov, Od 8 – 14 rokov, Individuálna zostava. Default stays Do 10 rokov.
- Definitions with the exact sizes, column values and empty cells from the reference document.
- `TryParseImageFileName` (utility, unused) gets the new prefixes too, or is removed — owner's choice in review.

### 2.2 Category availability per age group
- Each definition lists its allowed categories (`TeamTables.Keys`).
- Radio buttons bind `IsEnabled` to the definition: Do 8 → WG and MG disabled; Individuálna zostava → all five disabled.
- Switching to a group where the current category is not allowed (e.g. WG → Do 8): after the usual
  clear-confirmation the category becomes **WP**; on "Nie" nothing changes (fix from 1.6).

### 2.3 Do 8 rokov and Od 8 – 14 rokov
- 8 Okno on the main screen (5 team + 3 Inv), counters `WP: n/5   Inv: n/3`.
- WP/MP/MxP use the shared *Duo* (Do 8) or *Pár* (Od 8 – 14) table; WG → *Trojica*, MG → *Štvorica*.
- Empty cells are visible but not clickable.
- Row rule and team/Inv forcing work as today with 5/3 instead of 6/6.

### 2.4 Individuálna zostava
- One table *Individual* 8×6, 8 Okno, counter `Ind.: n/8`.
- No Inv toggle in the picker (hidden), category radios and discipline radios disabled (assumption A10).
- PDF (decision D11): `category1` = `Inv`, `category2` = `Individuálna zostava`,
  `discipline` = `Individuálna zostava`, `teamSizeText` = `SOLO`, IDs `R{row}` (assumption A8).
- Zvlášť button never shown (1 competitor).

### 2.5 Mock pictures (decision D9)
- `MockImageRelativePath = "do10_db/m_x_p/10r_mxp_01_01.png"` on the three new definitions.
- Repository: if a non-empty cell of such a group has no real picture → return the mock path.
  A real file always wins, so the owner's pictures replace the mocks automatically, file by file.
- When all pictures are delivered: set `MockImageRelativePath = null` (one-line change) so missing files show the
  SGF placeholder again, like in Do 10 / Do 14.

### 2.6 Printing with 8 Okno
- Okno 1–8 → boxes 1–8 (the first row of the sheet). Boxes 9–12 stay empty.
- Print guard requires 8 exercises for these groups.
- `overallDifficulity` = sum of the 8 values.

### 2.7 Real pictures (owner task O3)
- Owner exports pictures into the confirmed folders (D10).
- Check: the app lists no mock for any non-empty cell; spot-check each table in the picker.

## Acceptance
- Each new group: picker shows the right size, labels show the right column values (e.g. Do 8 Duo col 4 → `0.020`),
  empty cells are not clickable, row rule works, counters correct, 8 picks → PDF prints 8 boxes and the right total.
- Do 8: WG/MG disabled. Individuálna zostava: radios disabled, no Inv toggle, `SOLO` on the PDF.
- Do 10 / Do 14 unchanged (Phase 1 smoke test still passes).
