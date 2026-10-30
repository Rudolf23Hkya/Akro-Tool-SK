# Phase 9 — Súťažný plán history (#45) — LAST phase

Status: **implemented** on `feature/phase-9-plan-history-45` · 2026-10-30, with the proposals for Q9–Q12 in
[00-decisions-and-open-questions.md](00-decisions-and-open-questions.md#3-open-questions). Done **last** on the owner's
request (issue title "Do this last"), after Phase 8, so the saved format already contains the final Zvlášť model
(Inv only, Top → Base). Implementation notes: [9.7](#97-implementation-notes-2026-10-30).

**Goal:** every printed plan is saved automatically; a history window (from the menu bar) lists them newest first and
can **load** a plan back into the app or **remove** it. Only the app data is saved — no PDFs.

## 9.1 Storage — JSON files, no new package

- Folder: `%LOCALAPPDATA%\Akro Tool SK\history\`, **one file per printed plan**
  (`yyyyMMdd-HHmmss-<short id>.json`). Not next to the exe: ClickOnce installs each update into a new folder, so data
  there would be lost on the next update.
- `System.Text.Json` (part of .NET 8) — no SQLite or other NuGet package (ground rule: no new packages).
  One file per entry keeps it simple and robust: a damaged file only loses that entry, removing = deleting one file.
- Written atomically (temp file + `File.Move(..., overwrite)`), so a crash never leaves a half-written entry.
- Any error while saving or reading history is traced (`Trace.TraceWarning`) and **never** blocks printing.

New file `Models/PlanHistory/PlanHistoryStore.cs` (`Save`, `LoadAll`, `Delete`) + `PlanHistoryEntry.cs` (records).

## 9.2 What is saved

```json
{
  "formatVersion": 1,
  "savedAt": "2026-10-30T14:05:12+01:00",
  "eventName": "…", "trainerName": "…", "trainerContact": "…", "clubName": "…",
  "rulebook": "DO_10_ROK", "category": "MxP", "routine": "Combi",
  "athletes": { "top":     { "name": "…", "birthYear": "2016" },
                "middle1": { "name": "",  "birthYear": "" },
                "middle2": { "name": "",  "birthYear": "" },
                "base":    { "name": "…", "birthYear": "2015" } },
  "slots": [
    { "okno": 1, "table": "MxP", "row": 3, "exercises": [ { "col": 2 } ] },
    { "okno": 2, "table": "Inv", "row": 1, "exercises": [ { "col": 1, "member": 0, "position": "Top" },
                                                          { "col": 4, "member": 1, "position": "Base" } ] }
  ]
}
```

- Enums are stored **by name** (not number), so adding enum values later can't shift them.
- Pictures are **not** stored as paths (the install folder changes with every update); on load the picture is looked
  up again in the repository by (age group, table, row, column).
- Difficulties are **not** stored; they are recomputed from the table on load, exactly as when picking.
- `formatVersion` lets a later version read old entries.

## 9.3 Saving automatically on print

In `ExportFilledPdf`, **after** the PDF was written successfully (not on "Zrušiť" in the save dialog, not on an
export error, not when the age warning stops printing): build the entry from `FormData` + the selected
age group / category / routine and call `PlanHistoryStore.Save`.

Duplicates (Q9): proposal — if the plan is **identical** to the newest entry (printed twice in a row), only that
entry's date is updated; otherwise a new entry is added.

## 9.4 History window

- Menu bar: new top-level item **História** → *Súťažné plány…* (between *Súbor* and *Pomoc*).
- New modal window `PlanHistoryWindow.xaml(.cs)` + `ViewModels/PlanHistoryViewModel.cs`, same style as the other
  dialogs (Slovak, `RelayCommand`, `WindowSizing.FitToWorkArea`).
- `ListBox`, **newest at the top**. Each item shows:
  - line 1 (bold): the team member names in the order **Top, Middle 1, Middle 2, Base**, only the cards the category
    uses (pair: Top, Base · trio: Top, Middle 1, Base · quartet: all four; Individuálna zostava: Q12), joined with
    ` · `; empty names shown as `—`.
  - line 2: `MxP / Do 10 rokov` (category / age group; Individuálna zostava: just the age group).
  - line 3 (small, grey) — proposal Q11: date of printing `30.10.2026 14:05` and the event name.
- **Month separator:** between two neighbouring items a **thick separator line** when the older one was printed at
  least one calendar month before the newer one (`older.SavedAt.AddMonths(1) <= newer.SavedAt`). Done with a
  `IsSeparatorAbove` flag on the item VM, computed after sorting — no special list control.
- Buttons: **Načítať** (also double-click), **Odstrániť**, **Zavrieť**. Both actions disabled without a selection.
- **Odstrániť:** Yes/No confirmation (`Naozaj chcete odstrániť tento súťažný plán z histórie?`), then the file is
  deleted and the list (and separators) rebuilt.
- Empty history: the list shows *Zatiaľ nebol vytlačený žiadny súťažný plán.*

## 9.5 Loading a plan

1. If the current plan has any Okno set or any athlete name: confirm *Načítať plán? Aktuálne údaje sa nahradia.*
2. Set age group, category, routine **without** the "clear all Okno?" confirmations of the setters — a new internal
   method `ApplyLoadedPlan(entry)` on `MainWindowViewModel` that sets the backing fields, calls
   `ResetExerciseSlots()` once and raises `OnPropertyChanged` for the radios / ComboBox.
3. Athlete names and birth years; header fields (event, trainer, contact, club) — proposal Q10: yes, loaded too
   (they were printed; they can be changed in *Súbor* as usual).
4. Each Okno: resolve the picture(s) in the repository, `FormData.SetSlot(...)` (Zvlášť: the multi-exercise overload),
   then `ApplySlotToCard` — the same path as picking, so `FormData` and `ExerciseSlots` stay in sync (CLAUDE.md §4).
   Finally `RecalculateExerciseSummary()` and `RefreshPerformerTexts()`.
5. **Robustness:** an Okno that can't be restored (unknown table/position, empty cell, value now outside the table,
   row-rule conflict) is left empty and listed in one message after loading
   (*Okno 3 sa nepodarilo načítať — cvičenie už v databáze nie je.*). An entry whose age group or category is unknown
   is shown in the list greyed out with *nedá sa načítať*, Načítať disabled; Odstrániť still works.
6. A missing picture file only shows the SGF placeholder, as everywhere else.

## 9.6 Tests and docs
- Smoke test extension (`tools/ui-smoke-test.ps1`): print a Do 10 plan → history file exists → *Vyčistiť tabuľku* →
  *História* → *Načítať* → print again → both PDFs rendered with `tools/render-pdf.ps1` must look the same. Also a
  Zvlášť plan and an Od 8 – 14 plan; removal; month separator (two entries with edited `savedAt`).
- The smoke test uses its own history folder (env var `AKRO_TOOL_HISTORY_DIR`, read only if set) so it never touches
  the coach's real history.
- CLAUDE.md: new §6 row, §3 layout (new files), §8 (menu + window), storage location.

## Done when
- Printing adds an entry; load restores every field and Okno (incl. Zvlášť) so a re-print gives the same PDF;
  remove works with confirmation; newest first; month separators correct.
- Damaged/unknown entries never crash the app or block printing.
- `dotnet build` 0 warnings; smoke test passes on the Windows host; owner test → close #45.

## 9.7 Implementation notes (2026-10-30)

- As planned, with these details:
  - A missing picture can't be picked (the picker refuses it), so an Okno whose picture is gone is **not** restored
    with the placeholder; it stays empty and is listed (*Okno n sa nepodarilo načítať — cvičenie už v databáze nie je.*).
    Other failures (row rule, limits, wrong table, Zvlášť member count) say *uložené údaje nie sú platné*.
  - A damaged JSON file is skipped (traced), it does not appear in the list. Remove it by hand from the folder.
  - Individuálna zostava stores the hidden category / routine radios too; on load they are restored when readable.
  - Zvlášť positions are written to the file for reading only; on load they come from the current team members.
- Smoke test `-HistoryRoundTrip` passed on the host for Do 10 rokov WP, Od 8 – 14 rokov MG with two Zvlášť Okno,
  and Individuálna zostava: entry saved, table cleared and a name changed, fake older / unknown / damaged entries,
  unknown entry removed, plan loaded (all labels and the name back), re-printed PDF identical in rendered pixels,
  re-print only updated the date (still one entry), month separator visible.
