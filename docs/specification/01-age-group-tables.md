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
| Duo | WP, MP, MxP | 5 rows × 7 cols | 0.01, 0.01, 0.01, 0.02, 0.02, 0.03, 0.03 | row 5 × col 7 (1 cell, D18) |
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

> **As delivered by the owner on 2026-10-28 (D17)** — replaces the `<prefix>_<table>_<COL>_<ROW>.png` scheme
> planned in D10. Details: [phase-6-real-pictures.md](phase-6-real-pictures.md).

```
DataBase/
  new_do8/        Do 8 rokov             index.csv, DUO-I-1.png …, IND-….png
  new_zat_do14/   Od 8 – 14 rokov        index.csv, PAR-….png, TRO-….png, STV-….png, IND-….png
  new_inv/        Individuálna zostava   index.csv, A1.png … H6.png, _preview_all.png (ignored)
```

| `index.csv` | Meaning |
|---|---|
| `id` prefix | Table: `DUO` Duo · `PAR` Pár · `TRO` Trojica · `STV` Štvorica · `IND` Inv (`new_inv`: no prefix, one table) |
| `row` | `Riadok I–V` → rows 1–5 · `Pružnosť` / `Statika` / `Dynamika` → rows 1 / 2 / 3 · `A–H` → rows 1–8 |
| `column` | Column number (1-based) |
| `value` / `difficulty` | Only checked against the column values above; the table definition decides |
| `file` | Picture file in the same folder |

- Shared tables (Duo, Pár) are stored **once**, not copied per category.
- Do 10 / Do 14 folders stay exactly as they are (`wp, mp, m_x_p, wg, mg, inv`).
- Empty cells have no line in the CSV. A missing picture shows the SGF placeholder (the mock of D9 is switched off).

## 4. Age warning limits (Phase 5)

Age in the competition year = competition year − birth year (competition year = current year, as today).

| Age group | Warn if age < | Warn if age > |
|---|---|---|
| Do 8 rokov | — | 7 |
| Do 10 rokov | — | 9 *(implemented)* |
| Do 14 rokov | — | 13 |
| Od 8 – 14 rokov | 8 | 13 |
| Individuálna zostava | — | — (no check) |
