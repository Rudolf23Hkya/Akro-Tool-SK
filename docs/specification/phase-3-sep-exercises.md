# Phase 3 — "Zvlášť" : one exercise per team member in one Okno (#29)

**Goal:** an Okno can hold one exercise **per team member** (all from the same table row) instead of one exercise
for the whole team. Button name **"Zvlášť"**, tooltip *Zvoliť cvičenie zvlášť pre každého pretekára* (D15).

Depends on: Phase 1. Release together with Phase 4 (printing).
Applies to every team age group (Do 8, Do 10, Do 14, Od 8 – 14 — assumption A3), both team and Inv tables (D6).

## Parts

### 3.1 Data model
Replace the single-exercise slot with a slot that holds 1…n exercises:

```csharp
public sealed record PickedExercise(int Col, string ImagePath, decimal Difficulty, int MemberIndex); // MemberIndex -1 = whole team
public sealed record SlotSelection(int SlotIndex, Rulebook Rulebook, string TableId, Category Category,
                                   int Row, IReadOnlyList<PickedExercise> Exercises)
{
    public bool IsSep => Exercises.Count > 1;
    public decimal Difficulty => Math.Round(Exercises.Average(e => e.Difficulty), 3, MidpointRounding.AwayFromZero);
}
```
- Okno difficulty = average rounded to 3 decimals (D5, A6). **Total = sum of the rounded Okno values.**
- Names are **not** stored in the slot — only `MemberIndex`; names are resolved when shown, so editing a name
  afterwards updates the Okno.
- `FormData` and `ExerciseSlots` stay in sync (CLAUDE.md §4).
- Summary bar shows the total with **3 decimals** (`0.000`), otherwise averages like 0.015 would be hidden.

### 3.2 Team members
| Category | Members (order Base → Top, A4) |
|---|---|
| WP, MP, MxP | Base, Top |
| WG | Base, Middle (1st Middle card), Top |
| MG | Base, Middle (2nd card), Middle (1st card), Top |

Display name: the name from the athlete card if it has **≥ 3 characters** (trimmed), otherwise
**"Pretekár {k}"** where k = position in the order above (A5). The role is always shown next to it.

### 3.3 Zvlášť button on empty Okno
- Small button **"Zvlášť"** in a corner of every **empty** Okno tile (team age groups only).
- Implementation note: a sibling of the tile button in a `Grid`, not nested inside it, so a click on "Zvlášť" never
  also opens the normal picker.
- Never disabled for lack of space on the sheet: extra exercises continue on a second page (D12, Phase 4).

### 3.4 Zvlášť picking sequence
1. The picker opens once **per member**, in Base → Top order.
2. Header: `Cvičenia: WP (Do 10 rokov)` + `Ján Novák (Base) – 1/2` (or `Pretekár 1 (Base) – 1/2`).
3. Member 1: normal rules — any row not used by another Okno of the same table; the team/Inv toggle follows the
   usual pick limits.
4. Members 2…n: table toggle **locked** to member 1's table, **only member 1's row** is selectable
   (other rows greyed like the row rule). Choosing the **same exercise** as another member is allowed.
5. Empty cells stay unselectable.
6. **Zavrieť** at any step cancels the whole sequence; the Okno keeps what it had before (A7).
7. After the last member: the Okno is saved, counters and total are recalculated.

A Zvlášť Okno counts as **one** pick for the team/Inv limit and blocks its row like a normal Okno.

### 3.5 Showing a Zvlášť Okno
- Picture area shows one member's exercise at a time; small **‹ ›** arrows on the left/right edge cycle through
  them (wrapping around).
- Overlay at the **top middle** of the picture: who performs the shown exercise — name, or the member number
  if there is no valid name (issue text), plus the role.
- Label below the picture: `{average:0.000} - R{row} - {Cat} - Zvlášť`

### 3.6 Clicking a filled Okno (D8)
Small dialog (custom window like `AgeWarningDialog`, **not** `MessageBox` — its button texts follow the Windows
language, e.g. Hungarian on the dev PC):

> *Okno {n} je už vyplnené. Ako ho chcete zmeniť?*  — **Jedno cvičenie** · **Zvlášť** · **Zrušiť**

- *Jedno cvičenie* → normal picker; result replaces the Okno.
- *Zvlášť* → Zvlášť sequence; result replaces the Okno.
- *Zrušiť* → nothing changes.
- For Individuálna zostava there is no dialog (no Zvlášť) — the click opens the picker as today.

### 3.7 Interaction with existing rules
- Changing age group or category clears all Okno including Zvlášť (existing confirm).
- *Vyčistiť tabuľku* clears Zvlášť Okno too.
- Re-opening an Okno still excludes that Okno from counts/blocks (issue #1 behaviour).

## Implementation notes (2026-10-27)
- `ExerciseSlotSelection` now holds `Exercises` (`PickedExercise`: column, difficulty, image, member index);
  `X` / `ImagePath` of the slot = the first exercise, so printing keeps working (only the first picture is printed
  until Phase 4).
- The picker header shows the member as `Ján Novák (Base) – 1/2`; the Okno overlay as `Ján Novák (Base)` or `1 (Base)`.
- The arrows and the Zvlášť button have Slovak automation names (*Predchádzajúce cvičenie* / *Ďalšie cvičenie* /
  *Zvlášť*), so the UI test script can drive them.
- `tools/ui-smoke-test.ps1 -SeparateOkno 1,7` fills Okno in Zvlášť mode and checks the row lock.

## Acceptance
- WP: Zvlášť on Okno 1 → two pickers (Base, Top); 2nd picker only allows member 1's row; result shows arrows, overlay
  names, average label; total updated with 3 decimals.
- MG with 0.01, 0.01, 0.01, 0.02 → Okno shows `0.013`.
- Zavrieť in the 2nd picker → Okno unchanged.
- Filled Okno click → 3-button dialog behaves as described.
- Names changed after picking → overlay updates.
- Do 10 / Do 14 single-exercise flow unchanged.
