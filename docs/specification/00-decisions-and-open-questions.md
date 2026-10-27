# Decisions and open questions

## 1. Owner decisions

### 2026-10-27 — first round

| # | Topic | Decision |
|---|---|---|
| D1 | Table sizes / values of the new age groups | As described by the owner — recorded in [01-age-group-tables.md](01-age-group-tables.md). The rulebook PDFs will be uploaded to `docs/`. |
| D2 | Categories in the new age groups | **Do 8 rokov:** only WP / MP / MxP (WG and MG disabled), all three use the *Duo* table. **Od 8 – 14 rokov:** WP / MP / MxP → *Pár* table, WG → *Trojica*, MG → *Štvorica*. |
| D3 | Individuálna zostava | One extra item in the age-group list, one table for all ages, **no age check**. |
| D4 | Age warning limits | Do 8: max 7 · Do 10: max 9 · Do 14: max 13 · Od 8 – 14: min 8, max 13 (age = competition year − birth year). Warn when too old **and** when too young. |
| D5 | Zvlášť score rounding | Average of the Okno's exercises, **rounded to 3 decimals per Okno**; the total is the sum of the rounded Okno values (printed numbers always add up). |
| D6 | Which tables allow Zvlášť | **Both** — the team table and the Inv table. |
| D7 | Printing Zvlášť exercises | Use free picture boxes on the sheet (details: D12). |
| D8 | Changing a filled Okno | Zvlášť button only on **empty** Okno. Clicking a filled Okno asks: *Jedno cvičenie* / *Zvlášť* / *Zrušiť*. |
| D9 | Mock pictures | Until the owner's pictures exist, every exercise of the new groups shows `DataBase/do10_db/m_x_p/10r_mxp_01_01.png` (owner's choice, issue #27). Done **in code**, not by copying files (copying would add ~550 MB). |

### 2026-10-27 — second round

| # | Topic | Decision |
|---|---|---|
| D10 | DB folder / file names for the new groups (was Q1) | **Confirmed** as proposed in [01-age-group-tables.md §3](01-age-group-tables.md#3-database-layout-for-the-new-groups). |
| D11 | Individuálna zostava on the PDF (was Q2) | `category1` = **`Inv`**, `discipline` = **`Individuálna zostava`** (`teamSizeText` stays `SOLO`). |
| D12 | Picture boxes on the sheet (was Q3) | The owner adds **4 more boxes** to the template → **16 boxes** on the page. Boxes are used **dynamically** depending on the age group (8 or 12 Okno + Zvlášť exercises). If more exercises must be printed, the app adds a **second page** (more if needed) that is an **exact copy of the first page made from the same template** — only the exercise boxes differ. → No capacity limit for Zvlášť any more. |
| D13 | Who performs a Zvlášť exercise (was Q4) | **No names** on the sheet. The performer's **position** goes into the exercise's ID field: `R1.Top`, `R1.Base`, `R1.Middle` (Inv table: `InvR1.Top` …). |

### 2026-10-27 — third round

| # | Topic | Decision |
|---|---|---|
| D14 | How the boxes of one Zvlášť Okno are connected (was Q5) | **Variant B — in order** (explained in §3): boxes are filled in routine order, the app writes the box numbers (`1`, `2.a`, `2.b`, `3`, …). The template's printed box numbers become text fields. |
| D15 | Name of the button (was Q6) | **Zvlášť**, tooltip *Zvoliť cvičenie zvlášť pre každého pretekára*. |
| D16 | Which cards the age check looks at (Phase 5.3) | **Only the cards the team uses** — pair: Top + Base, trio: + 1st Middle, quartet: all four. |

Earlier decisions that still apply (from `CLAUDE.md`): difficulty `column × 0.01` is correct **for Do 10 / Do 14**;
`catNum1`, `catNum2`, `invidualnaZostava`, `stalkovaZostava` stay empty on the PDF; unused athlete fields are not
greyed out; #6, #23 and the fullscreen idea from #5 are rejected.

## 2. Assumptions made in this spec (please confirm or correct)

| # | Assumption | Where it matters |
|---|---|---|
| A1 | The Od 8 – 14 *Trojica* and *Štvorica* tables have **different pictures** (same size and values). | Phase 2, DB folders |
| A2 | Every age group has **one** Inv table shared by all categories (like Do 10 / Do 14 today). | Phase 1/2 |
| A3 | Zvlášť is available in **every team age group** (Do 8, Do 10, Do 14, Od 8 – 14), not in Individuálna zostava (1 person). | Phase 3 |
| A4 | Team member order for Zvlášť "from Base to Top": pair = Base, Top · trio = Base, Middle, Top (the **first** Middle card) · quartet = Base, Middle (2nd card), Middle (1st card), Top. | Phase 3 |
| A5 | Fallback name in the app when a name is empty or shorter than 3 characters: **"Pretekár 1"**, "Pretekár 2", … (numbered in the Base → Top order). | Phase 3 |
| A6 | Rounding is "normal" rounding (0.0125 → 0.013, `MidpointRounding.AwayFromZero`). | Phase 3 |
| A7 | Closing the picker in the middle of a Zvlášť sequence cancels the **whole** sequence; the Okno keeps its previous content. | Phase 3 |
| A8 | Individuálna zostava exercise ID on the PDF is `R{row}` (no `Inv` prefix, even though `category1` = `Inv`). | Phase 2 |
| A9 | Combo box labels **"Do 8 rokov"**, **"Od 8 – 14 rokov"**, **"Individuálna zostava"**. Order: Do 8, Do 10, Do 14, Od 8 – 14, Individuálna zostava. Default stays Do 10 rokov. | Phase 2 |
| A10 | Individuálna zostava: the discipline radios (Zmiešaná / Dynamická / Balančná) are **disabled**, because the printed discipline is fixed to `Individuálna zostava` (D11). | Phase 2 |
| A11 | Quartet (MG) with two Middles: IDs `R1.Middle1` / `R1.Middle2` so the two can be told apart (D13 shows only `R1.Middle`). | Phase 4 |
| A12 | The 4 new boxes get the next field numbers: `Image1_af_image.12–15`, `ExerciseDifficulity.12–15`, `ExerciseID.12–15`; the box numbers become `BoxNumber.0–15`. | Phase 4 (O4) |
| A13 | A box number is the plain Okno number (`3`) for a normal Okno and `{n}.a`, `{n}.b`, … for a Zvlášť Okno (letters in Base → Top order). The parts of one Zvlášť Okno may continue on the next page if the page is full. | Phase 4 |

## 3. Open questions

None at the moment. The background of D14 is kept below for reference.

### Background of D14 (was Q5)

Example: *Do 10 rokov*, WP pair, 12 Okno. **Okno 2** is a Zvlášť Okno from row 3 of the WP table:
Base does the exercise in column 1 (0.010), Top the one in column 3 (0.030) → Okno 2 is worth **0.020**.
That is 13 exercises for 16 boxes. Which boxes do they go into?

**Variant A — fixed boxes.** Okno *n* is always box *n*; the extra exercises go into the boxes after the last Okno.

| Box (number printed on the sheet) | 1 | **2** | 3 | … | 12 | **13** | 14–16 |
|---|---|---|---|---|---|---|---|
| Picture | Okno 1 | Okno 2, Base | Okno 3 | … | Okno 12 | Okno 2, Top | empty |
| ID | R5 | R3.Base | InvR1 | … | … | R3.Top | |
| Obťažnosť | 0,030 | **0,020** | 0,010 | … | … | *(blank)* | |

The judge sees box **13** but nothing says it belongs to box **2**: the ID `R3.Top` gives the table row (3), not the
Okno number (2). So box 13 would need an extra mark like **"2.b"** (and box 2 maybe **"2.a"**).
Simple for the template: the numbers 1–16 stay printed text.

**Variant B — in order.** The boxes are filled one after another in routine order, and the app writes the box
number itself.

| Box number (written by the app) | 1 | **2.a** | **2.b** | 3 | … | 12 | – |
|---|---|---|---|---|---|---|---|
| Picture | Okno 1 | Okno 2, Base | Okno 2, Top | Okno 3 | … | Okno 12 | empty |
| ID | R5 | R3.Base | R3.Top | InvR1 | … | … | |
| Obťažnosť | 0,030 | **0,020** | *(blank)* | 0,010 | … | … | |

The sheet reads in routine order, and the parts of a Zvlášť Okno sit next to each other.
For the template: the box numbers 1–16 must become **text fields** (e.g. `BoxNumber.0–15`) instead of printed text.

**Chosen: Variant B** (D14). It matches "use the boxes dynamically" (D12) and the sheet reads like the routine.
The cost is 16 small text fields in Acrobat.

## 4. Found while reading the code (bug, fixed in Phase 1)

**Answering "Nie" to the clear-confirmation still changes the age group / category.**
`SelectedRulebook` and `SelectedBackendCategory` store the new value first and only then call
`ClearExerciseSelections()`, which returns early on "Nie" without restoring the old value. Result: e.g. *Do 14 rokov*
is selected while the Okno still hold *Do 10 rokov* exercises, and they get printed with the wrong label.
With the new groups (8 vs 12 Okno, different tables) this would become a crash, so the fix is part of Phase 1.
