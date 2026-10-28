# Phase 7 — Okno hover bug, full-screen picker, smaller PDF (#40, #39, #37)

Status: **implemented** on `feature/phase-7-issues-37-39-40` · 2026-10-28.
Owner's answers: Q1 **maximized** (not true full screen), Q2 **300 dpi**, Q3 **yes — without removing any needed
element**. Added on the owner's request: 7.4, a batch clean-up of the picture database.

Order: **7.1 (#40) first** (a visible bug, smallest change), then 7.2 (#39), then 7.3 (#37). Each part is its own
commit; one PR for the phase.

## 7.1 Blue bars above/below the Okno on hover (#40)

**Cause (found in `MainWindow.xaml`):** the Okno tile is a `Button` whose content (the black-bordered tile) is
**centered**, not stretched (WPF default for buttons). The tile takes the height the picture area needs for the cell
**width**. With 8 Okno (2 rows) the cells are taller than that, so there is empty button area above and below the
tile. `Background="Transparent"` hides it, but on mouse-over the default button template paints its light-blue
highlight there. With 12 Okno (3 rows) the cells have about the tile's shape, so you hardly see it (it can still show
up with other window sizes).

The same cause also misplaces the overlays: they are laid out over the **cell**, not the tile, so on the new groups
the *Zvlášť* button sits on the line between picture and label, and the performer badge sits on the tile's top edge
(visible in the issue screenshot).

**Fix:** `HorizontalContentAlignment="Stretch"` + `VerticalContentAlignment="Stretch"` on the Okno button. The tile then
fills the cell, the hover highlight has nowhere to show, and the overlays line up with the tile again. The picture
stays centered in its white area (fixed 1032×536 shape from Phase 6), so 8-Okno tiles get some white above/below the
picture. Do 10 / Do 14 look the same.

**Check:** hover screenshots of Do 8 / Od 8 – 14 / Individuálna zostava / Do 10, with an empty, a filled and a Zvlášť
Okno; overlays (‹ ›, Zvlášť, performer badge) on the tile.

## 7.2 Picker in full screen (#39)

Today the picker opens at 1250×850 and the table sits in a vertical `ScrollViewer`. Tables with many rows
already need scrolling (Individuálna zostava, 8 rows: rows 6–8 hidden on a 1080p screen). Simply maximizing makes
the tiles wider and therefore taller, so **every** 6-row table would need scrolling.

**Proposal:**
1. Open the picker **maximized** (`WindowState="Maximized"`): title bar and taskbar stay visible, the Windows
   buttons and *Zavrieť* work as today. (CLAUDE.md lists "start in fullscreen" from #5 as rejected — that was the
   **main** window; #39 is about the picker only.)
2. **The whole table always fits, no scrolling:** the grid is scaled to the window (a `Viewbox` around the tile grid;
   width or height, whichever is tighter), centered horizontally, top-aligned. Labels scale with the tiles, so they
   are larger on large screens.
3. The minimum size stays; restoring the window (un-maximize) still gives the 1250×850 size.

**Check:** every table (Do 10 / Do 14 6×5, Do 8 Duo 5×7, Pár 5×8, Inv 3×8, Individual 8×6) fully visible at 1080p
100 % and at 125 % / 150 % scaling; Zvlášť picker subtitle and the Inv toggle still in the header.

## 7.3 Smaller PDF (#37)

**Measured** (the exported files from the 2026-10-28 tests):

| Plan | Size today |
|---|---|
| Do 10, 12 boxes, 1 page | **11.1 MB** |
| Do 14 MG with Zvlášť, 27 boxes, 2 pages | **18.9 MB** |
| Do 8 / Od 8 – 14 / Individuálna zostava, 1 page | 0.6–0.9 MB |
| The template alone | 0.46 MB |

Where the 11.1 MB of the Do 10 plan go:

| Part | Size |
|---|---|
| 6 classic pictures copied **unchanged** from the PNG files (iText passes RGB PNGs through) | 9.7 MB |
| 6 classic pictures re-compressed by iText (the RGBA ones) | 0.96 MB |
| 4 fonts embedded completely **in the template** (Acrobat) | 0.40 MB |
| Everything else (text, lines, logos, Noto subset) | 0.03 MB |

The classic pictures are 1032×536 px (1.6–2.2 MB each) but are printed in a box only ~69 pt wide, i.e. at about
1080 dpi. They are enlarged scans: a 4.5× zoom shows no visible difference between the original, 600 dpi and 300 dpi.
**No picture in the database contains real colour** (all 582 checked), so grayscale loses nothing.

**Proposal:**
1. **Pictures at print resolution, grayscale, on white:** before placing a picture, scale it to the box size at
   **300 dpi** (≈ 288 px wide; never enlarged), compose it on white (no transparency mask) and embed it as 8-bit
   grayscale (lossless Flate, no JPEG artefacts on lines). Done with the WPF imaging classes already in .NET — **no
   new package**. 12 pictures: 23.8 MB on disk → about **0.12 MB** in the PDF.
2. **Same picture used twice** (Zvlášť members with the same exercise) is embedded once per page.
3. **Extra pages share the template's fonts and logos** (iText *smart mode* when the pages are joined), so every
   extra page adds little more than its pictures.
4. Optional (Q3): turn near-white pixels into pure white while converting. This removes the pink background of the
   14 pictures from Phase 6 and the grey scan noise **on the printed sheet**, without touching the files.

**Expected result:** Do 10 ≈ **0.6 MB** (from 11.1 MB), the 2-page Do 14 plan ≈ **0.7–0.8 MB** (from 18.9 MB).
The remaining ~0.4 MB is the template's own fonts. Optional owner task O6: save the template in Acrobat with
*Save as Other → Reduced Size PDF* / PDF Optimizer (subset the fonts) → about 0.2 MB per plan.

**Check:** sizes before/after for every age group and the 2-page Zvlášť plan; rendered PDFs side by side (pictures
sharp, same position and size in the box); Do 10 boxes/text unchanged apart from the pictures.

## 7.4 Picture database clean-up (owner's request, instead of AI image generation)

Generative AI was rejected for this: it redraws the rulebook drawings (positions, labels like `0/4 180°` can change),
isn't repeatable and needs one-by-one work. Instead `tools/clean-pictures.ps1` processes all 582 pictures in one run
(~30 s), with the same tone rule as the PDF export (`PrintImage`):
- grayscale (no picture has real colour) → pink / green / blue paper tints disappear,
- paper whitening relative to each picture's own paper tone; every stroke darker than the ramp keeps its exact value
  (the Inv pictures have light-grey, broken leg outlines — a fixed "light → white" rule would erase them),
- speck removal only for dots ≤ 2–4 px with no other ink within 6 px (circled red on the review sheets). On the
  current database it removes **nothing** — no dot is isolated enough to be sure, so nothing is touched,
- same size and file names; 8-bit grayscale PNG.

Step 1 writes `DataBase_clean\` (git-ignored) with review sheets `_review\*.png` (before | after | removed, flagged
pictures first) and `report.csv`; `DataBase\` is not touched. Step 2, after the owner's approval:
`.\tools\clean-pictures.ps1 -Apply` copies the cleaned files over the originals (one commit; originals stay in git).

Result: **654 MB → 24.9 MB** (classic 1032×536 pictures: ~1.8 MB → ~40 KB each, visually identical); no picture
flagged. Checked sheets: Od 8 – 14 (all tints gone, "alebo", arrows, `+`, `180°`, `3/4`, dashed lines kept), Do 8 Inv
(outlines kept), Do 10 WP (identical).

## Implementation notes (2026-10-28)
- 7.1: `HorizontalContentAlignment`/`VerticalContentAlignment="Stretch"` on the Okno button. Hover screenshots: no
  blue bars; badge and ‹ › sit on the tile. `tools/ui-smoke-test.ps1` now takes `0_hover_empty` / `2_hover_filled`.
- 7.2: picker `WindowState="Maximized"`; tiles have a fixed size (240×152) and the grid sits in a `Viewbox`
  (no `ScrollViewer`). Label font 12 → 14, because an 8-row table is scaled to ~0.7. Individuálna zostava shows all
  8 rows on 1080p.
- 7.3: `Models/PdfPrinting/PrintImage.cs` (WPF imaging, no new package): composite on white → grayscale →
  whitening at **full resolution** (before scaling, so thin grey lines can't be averaged into the paper and then
  erased) → area-average downscale to 300 dpi of the box → raw 8-bit gray (Flate). One `PdfImageXObject` per
  picture and page; output `PdfWriter` in smart mode.
- Sizes on the host, with the **original** (uncleaned) database: Do 14 MG + 5 Zvlášť, 2 pages **18.9 MB → 683 KB**;
  Od 8 – 14 WG 563 KB; Do 8 542 KB; Individuálna zostava 527 KB (the template's own fonts are ~400 KB of each).
  Pink backgrounds are gone on paper; all labels, arrows and dashed lines kept.
