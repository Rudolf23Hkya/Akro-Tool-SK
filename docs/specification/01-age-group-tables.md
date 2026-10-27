# Age groups and exercise tables — reference data

Single source of truth for the numbers. The code (Phase 1) will hold exactly this data in one definitions class.
Source: the owner's answers of 2026-10-27; rulebook PDFs will be in `docs/`.

Notation: rows and columns are 1-based. **Column values** = difficulty of every exercise in that column.
**Empty cells** = positions with no exercise in the rulebook (shown as an empty, non-clickable tile).

## 1. Overview

| Age group (UI label) | Categories | Team table(s) | Inv table | Okno = team + Inv | Age warning |
|---|---|---|---|---|---|
| Do 8 rokov *(new)* | WP, MP, MxP | Duo | Inv 3×8 | **8** = 5 + 3 | max 7 |
| Do 10 rokov | WP, MP, MxP, WG, MG | one per category | Inv 6×5 | **12** = 6 + 6 | max 9 |
| Do 14 rokov | WP, MP, MxP, WG, MG | one per category | Inv 6×5 | **12** = 6 + 6 | max 13 |
| Od 8 – 14 rokov *(new)* | WP, MP, MxP, WG, MG | Pár, Trojica, Štvorica | Inv 3×8 | **8** = 5 + 3 | min 8, max 13 |
| Individuálna zostava *(new)* | — (radios disabled) | Individual 8×6 | — (no toggle) | **8** = 8 | none |

Common rules for all groups: **one Okno per table row** (row rule #15); every row of a table is used exactly
once, because the number of picks equals the number of rows (6/6, 5/3, 8). With Zvlášť (#29) one Okno still
uses one row, but holds one exercise per team member.

## 2. Tables

### 2.1 Do 10 rokov / Do 14 rokov (existing, unchanged)

| Table | Used by | Size | Column values | Empty cells |
|---|---|---|---|---|
| wp / mp / m_x_p / wg / mg | its own category | 6 rows × 5 cols | 0.01, 0.02, 0.03, 0.04, 0.05 | none |
| inv | all categories | 6 × 5 | 0.01, 0.02, 0.03, 0.04, 0.05 | none |

(= the current `column × 0.01` rule, which stays correct for these groups.)

### 2.2 Do 8 rokov (new)

| Table | Used by | Size | Column values (col 1 → last) | Empty cells |
|---|---|---|---|---|
| Duo | WP, MP, MxP | 5 rows × 7 cols | 0.01, 0.01, 0.01, 0.02, 0.02, 0.03, 0.03 | none |
| Inv | all | 3 rows × 8 cols | 0.01, 0.02, 0.03, 0.04, 0.05, 0.06, 0.07, 0.07 | rows 1–2 × cols 7–8 (4 cells) |

### 2.3 Od 8 – 14 rokov (new)

| Table | Used by | Size | Column values (col 1 → last) | Empty cells |
|---|---|---|---|---|
| Pár | WP, MP, MxP | 5 rows × 8 cols | 0.01, 0.01, 0.01, 0.02, 0.02, 0.03, 0.03, 0.03 | rows 4–5 × col 6 (2 cells) |
| Trojica | WG | 5 rows × 6 cols | 0.01, 0.01, 0.02, 0.02, 0.03, 0.03 | none |
| Štvorica | MG | 5 rows × 6 cols | 0.01, 0.01, 0.02, 0.02, 0.03, 0.03 | none |
| Inv | all | 3 rows × 8 cols | 0.01, 0.02, 0.03, 0.04, 0.05, 0.06, 0.07, 0.07 | rows 1–2 × col 8 (2 cells) |

### 2.4 Individuálna zostava (new)

| Table | Used by | Size | Column values | Empty cells |
|---|---|---|---|---|
| Individual | the single competitor | 8 rows × 6 cols | 0.01, 0.02, 0.03, 0.04, 0.05, 0.06 | none |

- 8 exercises, one per row. No Inv toggle. WP … MG radios disabled. Zvlášť not available.
- PDF: `category1` = `Inv`, `discipline` = `Individuálna zostava`, team size text `SOLO` (D11).

## 3. Database layout for the new groups

> **Confirmed by the owner (D10).** Same scheme as today:
> `<prefix>_<table>_<COL>_<ROW>.png`, 2-digit column then 2-digit row, 1032×536 PNG recommended
> (same ratio as today so the PDF boxes look the same).

```
DataBase/
  do8_db/          prefix 8r      duo/        8r_duo_01_01.png … 8r_duo_07_05.png
                                  inv/        8r_inv_01_01.png … 8r_inv_08_03.png   (no files for empty cells)
  od8_14_db/       prefix 814r    par/        814r_par_01_01.png …
                                  trojica/    814r_trojica_01_01.png …
                                  stvorica/   814r_stvorica_01_01.png …
                                  inv/        814r_inv_01_01.png …
  individual_db/   prefix ir      individual/ ir_individual_01_01.png … ir_individual_06_08.png
```

- Shared tables (Duo, Pár) are stored **once**, not copied per category.
- Do 10 / Do 14 folders stay exactly as they are (`wp, mp, m_x_p, wg, mg, inv`).
- Until the pictures exist, the app shows the mock picture `do10_db/m_x_p/10r_mxp_01_01.png` for every non-empty
  cell of the new groups (decision D9). A real file always wins over the mock.

## 4. Age warning limits (Phase 5)

Age in the competition year = competition year − birth year (competition year = current year, as today).

| Age group | Warn if age < | Warn if age > |
|---|---|---|
| Do 8 rokov | — | 7 |
| Do 10 rokov | — | 9 *(implemented)* |
| Do 14 rokov | — | 13 |
| Od 8 – 14 rokov | 8 | 13 |
| Individuálna zostava | — | — (no check) |
