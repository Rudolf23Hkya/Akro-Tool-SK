# Phase 1 — Data-driven foundation (refactor, no visible change)

**Goal:** remove the hard-coded assumptions (12 Okno, 6 + 6 picks, 6×5 tables, difficulty = column × 0.01,
one table per category) so that Phases 2 and 3 only *add data and features* instead of rewriting rules.

**Visible behaviour for Do 10 / Do 14 must stay exactly the same** — this is the acceptance criterion.

## Where the assumptions live today

| Assumption | Places |
|---|---|
| 12 Okno | `SeedSlots`, `TrainingPlanFormData._slots[12]` + `SetSlot`/`ClearSlot` range checks, `ToTextFieldMap`/`ToImageFieldList` loops, print guard "12 cvičení", confirm text "všetkých 12" |
| 6 + 6 picks | `OpenSlot` (force/lock table toggle), XAML counter text `/6` |
| 6 rows × 5 cols | `ExerciseGridViewModel.LoadTableInto` (`rows: 6, cols: 5`), XAML `UniformGrid Columns="5" Rows="6"`, `SetSlot` range checks, comments in `ExerciseCardVm`/`ExerciseSlotSelection` |
| difficulty = col × 0.01 | `TrainingPlanFormData.DefaultDifficulty`/`DifficultyRule`, `ExerciseGridViewModel.GetExerciseDifficulty` (duplicate), `RecalculateExerciseSummary` |
| table = category folder | `ExerciseImageRepository` index key `(Rulebook, Category, row, col)`, `CategoryMap`, `RulebookFolder/RulebookPrefix` |

## Parts

### 1.1 Age-group and table definitions
New file `Models/AgeGroups.cs` (namespace `Atletika_SutaznyPlan_Generator.Models`), plain records, no framework:

```csharp
public sealed record ExerciseTable(
    string Id,                       // folder + file token, e.g. "wp", "duo", "inv"
    string DisplayName,              // e.g. "WP", "Pár", "Inv"
    int Rows, int Cols,
    IReadOnlyList<decimal> ColumnValues,              // length == Cols
    IReadOnlySet<(int row, int col)> EmptyCells);

public sealed record AgeGroupDefinition(
    Rulebook Rulebook,
    string Label,                    // "Do 10 rokov"
    string DbFolder, string FilePrefix,
    IReadOnlyDictionary<Category, ExerciseTable> TeamTables,   // which table each category uses
    ExerciseTable? InvTable,         // null = no Inv toggle (Individuálna zostava)
    int TeamPicks, int InvPicks,     // 6/6, 5/3, 8/0
    int? MinAge, int? MaxAge,        // used in Phase 5
    string? MockImageRelativePath);  // Phase 2; null for Do 10 / Do 14

public static class AgeGroups { public static AgeGroupDefinition Get(Rulebook rb) … }
```

- `SlotCount = TeamPicks + InvPicks` (computed).
- `ExerciseTable.GetDifficulty(col)` and `IsEmpty(row, col)`.
- In Phase 1 only **Do 10** and **Do 14** are defined (data from [01-age-group-tables.md §2.1](01-age-group-tables.md)).
- `RulebookExtensions.ToSlovakLabel()` reads `Label` from the definition (one place for names).

### 1.2 Repository keyed by table
- Index key becomes `(Rulebook, tableId, row, col)`; folders and prefixes come from the definitions
  (removes the `RulebookFolder` / `RulebookPrefix` switches).
- Do 10 / Do 14 table ids are the existing folder names, so **no DB file or folder is renamed**.
- File regex prefix generalised from `\d+r` to `[a-z0-9]+` (needed for the proposed `ir` prefix; still matches `10r`/`14r`).
- `GetTable(rb, table)` returns `Rows × Cols` cells; an empty cell has `IsEmpty = true` and no image.
- `ResolveDbRoot()` accepts any known group folder, not only `do10_db`/`do14_db`.

### 1.3 Difficulty from the table
- Each selected exercise stores its own `Difficulty` (taken from `ColumnValues[col-1]` at pick time).
- Remove `DifficultyRule` / `DefaultDifficulty` and the duplicate `GetExerciseDifficulty` (fixes CLAUDE.md quirk #3, first half).
- For Do 10 / Do 14 the values are identical to `column × 0.01` → no visible change.

### 1.4 Variable number of Okno
- `TrainingPlanFormData` gets the slot count from the selected age group (max 12, the template has 12 boxes).
- `SeedSlots()` builds `SlotCount` tiles; range checks use the definition.
- Counters: `"{Cat}: n/{TeamPicks}   Inv: n/{InvPicks}"` (no hard-coded `/6` in XAML).
- Print guard text: `"Na vytlačenie Súťažného plánu musíte zvoliť {SlotCount} cvičení."`
- Confirm text: `"…vymaže všetky zvolené cviky. Pokračovať?"` (no number).
- `OpenSlot` force/lock logic uses `TeamPicks`/`InvPicks`.

### 1.5 Exercise picker with any table size
- `UniformGrid` `Rows`/`Columns` bound to the table size.
- Empty cells: blank tile, no label, not clickable (new `IsEmptyCell` on `ExerciseCardVm`; different look from the
  greyed row-blocked tiles).
- Window title shows the table name when it differs from the category, e.g. `Cvičenia: WP – Pár (Od 8 – 14 rokov)`.
- Header tile size: tiles keep the picture ratio; with 8 columns the window may need to scroll vertically —
  accepted (the ScrollViewer is already there).

### 1.6 Fix: "Nie" in the clear-confirmation must keep the old selection
See [00-decisions-and-open-questions.md §4](00-decisions-and-open-questions.md#4-found-while-reading-the-code-bug-will-be-fixed-in-phase-1).
Ask **before** storing the new age group / category; on "Nie" re-raise `PropertyChanged` for the old value so the
ComboBox / radio button jumps back.

## Acceptance
- Build: 0 warnings.
- Smoke test Do 10 and Do 14: 12 Okno, 6×5 grids, labels `R1 - 0.010`, counters `/6`, row rule, 6 + 6 forcing,
  printing → **same PDF content as before** (compare a before/after export of the same selection).
- Change age group with Okno filled → "Nie" keeps everything (age group, Okno, PDF label) unchanged.

## Risks
- Largest refactor of the plan. Mitigation: no new behaviour in this phase, before/after PDF comparison.
