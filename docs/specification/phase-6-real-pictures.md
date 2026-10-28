# Phase 6 — Real pictures for the new age groups (#27, O3)

**Goal:** replace the mock pictures of *Do 8 rokov*, *Od 8 – 14 rokov* and *Individuálna zostava* with the owner's
pictures (uploaded 2026-10-28), without renaming them.

Depends on: Phase 2.

## What the owner delivered

| Folder | Age group | Tables (id prefix in `index.csv`) | Pictures |
|---|---|---|---|
| `new_do8` | Do 8 rokov | `DUO` (Duo), `IND` (Inv) | 34 + 20 |
| `new_zat_do14` | Od 8 – 14 rokov (*zat* = začiatočníci) | `PAR`, `TRO`, `STV`, `IND` | 38 + 30 + 30 + 22 |
| `new_inv` | Individuálna zostava | no prefix (one table) | 48 (+ `_preview_all.png`) |

- Every folder has an `index.csv` (one line per picture). The formats differ: column order, `value` vs. `difficulty`,
  row names `Riadok I–V` (team tables), `Pružnosť / Statika / Dynamika` (Inv 3×8) or `A–H` (Individual).
- Unlike the classic pictures (all 1032×536), the new ones are **cropped tight to the drawing**: 76–516 px wide,
  from tall (0.33 : 1) to wide (3 : 1). In a PDF box they print at 185–674 dpi, so none is too small.

## Owner decisions (2026-10-28)

Recorded as D17–D20 in [00-decisions-and-open-questions.md](00-decisions-and-open-questions.md).
- Folder and file names stay as uploaded; the app reads `index.csv` (replaces the layout of D10 for these groups).
- *Do 8 Duo* row V has only 6 exercises → R5/S7 is an empty cell.
- No tooltip with the exercise names from the CSV.
- `_preview_all.png` stays in `new_inv` (the app ignores it).

## Parts

### 6.1 Reading `index.csv`
- `AgeGroupDefinition.IndexFile = "index.csv"` and `IndexTablesByIdPrefix` (id prefix → table; empty = every line
  belongs to the Inv table). Do 10 / Do 14 keep the classic file-name layout (`IndexFile = null`).
- `ExerciseImageRepository.IndexFromCsv` finds the columns by header name (`id`, `row`, `column`,
  `value`|`difficulty`, `file`), parses the row name and the column number, and indexes the file.
- The CSV value is **only checked**; the table definition decides the difficulty. Unknown tables/rows, positions
  outside the table, value mismatches and missing files are collected in `Problems` and traced at startup.
  A re-export into the same folders needs no code change.

### 6.2 Fixed picture area
- Picker tile and Okno tile: a `Viewbox` around a fixed 1032×536 grid (white), picture `Stretch=Uniform` inside.
  The tile size never depends on the picture, and tall or wide pictures are shown whole.
- The picker keeps the old 90 % picture size (margin 52,27 inside the grid). Classic pictures have exactly this
  shape, so Do 10 / Do 14 look as before.

### 6.3 Mock off
- `MockImageRelativePath` removed from the three definitions; a missing picture shows the SGF placeholder again.
  The property stays (unused).

### 6.4 Database checker
- `Akro Tool SK.exe --check-database` writes `database-check.txt` next to the exe and exits without a window:
  pictures per table, missing positions, CSV problems. Exit code 0 = complete, 1 = something to look at.
- `tools/check-database.ps1` runs it on the built app and prints the report.

### 6.5 Smoke test
- `tools/ui-smoke-test.ps1` waits up to 10 s for the controls of the Save dialog (it sometimes typed too early).

## Implementation notes (2026-10-28)
- Checker: all 19 tables complete, `index.csv: no problems`. Od 8 – 14 Inv *Dynamika* column 8 has no value in
  the CSV, so it isn't checked (the table uses 0.07).
- Verified on the Windows host with `tools/ui-smoke-test.ps1`: Do 8 (WP, MxP + Zvlášť), Od 8 – 14 (WP, WG + Zvlášť),
  Individuálna zostava, Do 10 and Do 14 MG; picker and Okno screenshots checked, PDFs rendered (also on the
  16-box template, see Phase 4).
- **Open point (owner, optional):** 53 pictures, mostly Od 8 – 14 *Trojica*, have a faint pink/yellow paper tint in
  the background (up to 80 % of the pixels slightly coloured, e.g. `TRO-I-6.png`, `TRO-II-1.png`). Hardly visible
  on screen, but clearly visible on the printed sheet as a light pink rectangle. A re-export with a pure white
  background fixes it; the app needs no change.

## Acceptance
- `--check-database` reports every table complete and no CSV problems.
- Each new group: every non-empty cell shows its own picture in the picker, whole and in equal-size tiles;
  the Okno tiles keep their size; the PDF places every picture into its box.
- Do 10 / Do 14 pickers and PDFs unchanged.
