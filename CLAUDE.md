# CLAUDE.md — Akro Tool SK

Technical reference for working on this repo. Read this first, then open only the files the task needs.

## 1. What the app is

**Akro Tool SK** is a Windows desktop app (WPF, .NET 8) that lets **sports-acrobatics coaches** fill out the
official Slovak competition plan sheet (*"Súťažný plán – športová akrobacia"*) and export it as a **flattened PDF**.

Coach workflow:
1. On start, the app asks for the **event name**, then **trainer / contact / club** (two modal dialogs).
2. Coach picks **age group** (rulebook: *Do 8 rokov* / *Do 10 rokov* / *Do 14 rokov* / *Od 8 – 14 rokov* /
   *Individuálna zostava*), **discipline** (Combi / Dynamic / Balance) and **category** (WP, MP, MxP, WG, MG —
   Do 8 only pairs; Individuálna zostava has no category and a fixed discipline).
3. Coach enters up to 4 athletes (Top, Middle, Middle, Base) with names and birth years.
4. Coach clicks each **slot** ("Okno 1..12" for Do 10 / Do 14, "Okno 1..8" for the other groups). A pop-up grid shows
   the exercise pictures of the table used by the chosen category (Do 10 / Do 14: 6 rows × 5 columns), with a toggle
   to the **Individual (Inv)** table.
5. **Tlačiť** (Print) → Save dialog → fills `Cvicebny-plan_Template.pdf` with text + the pictures, flattens it.

UI language is **Slovak**. The owner writes issues/notes in **English and Hungarian**. Keep user-facing strings in Slovak.

Design priority from the owner: **stability on Windows** above everything else (that's why WPF). Prefer small,
conservative changes; don't introduce new frameworks/packages without asking.

## 2. Tech stack & build

| Item | Value |
|---|---|
| Project | `Akro Tool SK.csproj` (note the spaces in the file name — quote it in shell commands) |
| Solution | `Atletika_SutaznyPlan_Generator.sln` (old project name kept) |
| Framework | `net8.0-windows`, `UseWPF`, `Nullable` + `ImplicitUsings` enabled, `OutputType=WinExe` |
| Root namespace | `Atletika_SutaznyPlan_Generator` (old name; app was renamed to "Akro Tool SK" in issue #16 but namespaces were not) |
| NuGet | `itext` 9.5.0, `itext.bouncy-castle-adapter` 9.5.0 (iText Community, **AGPL**) |
| Deployment | ClickOnce offline (`<Install>true</Install>`, `<CreateDesktopShortcut>true</CreateDesktopShortcut>`), icon `assets\AkroTool_Icon.ico` |
| Distribution | Pre-built download linked in `README.txt` (Google Drive) |
| GitHub | `Rudolf23Hkya/Akro-Tool-SK` (renamed; the old `Atletika_SutaznyPlan_Generator_SK` URL redirects) |
| Upgrade plan | `docs/specification/` — phases for issues #26–#30, owner decisions, table reference data |
| License | AGPL-3.0 (`COPYING.txt`), third-party notes in `THIRD-PARTY-NOTICES.txt` |
| Tests | No unit tests. Windows UI smoke test: `tools/ui-smoke-test.ps1` (UI Automation; fills all Okno, prints a PDF) + `tools/render-pdf.ps1` (PDF → PNG via Windows.Data.Pdf) + `tools/check-database.ps1` (picture DB vs. tables). Run from PowerShell on the logged-in host. |

Build (Windows, normal dev): open the `.sln` in Visual Studio 2022, or `dotnet build "Akro Tool SK.csproj"`.

Building in a Linux/cloud container: no .NET SDK is pre-installed. WPF **compiles** (cannot run) on Linux:
```bash
curl -sSL -o dotnet-install.sh https://dot.net/v1/dotnet-install.sh
bash dotnet-install.sh --channel 8.0 --install-dir <scratch>/dotnet
<scratch>/dotnet/dotnet build "Akro Tool SK.csproj" -p:EnableWindowsTargeting=true \
    -p:CreateHardLinksForCopyFilesToOutputDirectoryIfPossible=true   # hard links: DataBase is ~684 MB
rm -rf bin obj   # afterwards
```
This compiles cleanly with 0 warnings — keep it that way, and run it before every push.
The app itself can only be run/tested on Windows — say so instead of claiming a UI change was verified.

Files copied to the output folder (all resolved relative to `AppDomain.CurrentDomain.BaseDirectory` at runtime):
- `Cvicebny-plan_Template.pdf` (Content, PreserveNewest)
- `assets\NotoSans-Bold.ttf` (Content, PreserveNewest) — the PDF font
- `COPYING.txt`, `THIRD-PARTY-NOTICES.txt`, `LICENSES\OFL-1.1.txt` (Content, PreserveNewest) — license texts (AGPL, font OFL)
- `DataBase\**\*.*` (None, PreserveNewest, also to publish dir) — exercise images
- `assets\sgf_logo1.png`, `sgf_logo2.png`, `AkroTool_Icon.ico` are **WPF Resources** (embedded, loaded with `pack://application:,,,/assets/...`)

Line endings: repo stores LF (`.gitattributes: * text=auto`), Windows checkouts get CRLF. Many files have a UTF-8 BOM — keep it.

## 3. Repository layout

```
App.xaml / App.xaml.cs            Application entry (StartupUri=MainWindow.xaml); OnStartup sets up wpf-binding-errors.log
AssemblyInfo.cs                   WPF ThemeInfo boilerplate
MainWindow.xaml(.cs)              Main screen; code-behind only opens dialogs + copyright box
ExerciseGridWindow.xaml(.cs)      Pop-up exercise picker (6x5 grid + Inv toggle)
EventNameDialog.xaml(.cs)         Modal: event name            (namespace ...Views)
TrainerClubDialog.xaml(.cs)       Modal: trainer/contact/club  (namespace ...Views)
AgeWarningDialog.xaml(.cs)        Modal: age warning before printing (#28)
SlotChangeDialog.xaml(.cs)        Modal: filled Okno clicked -> Jedno cvičenie / Zvlášť / Zrušiť (#29)
Converters/
  RulebookToSlovakLabelConverter  Rulebook enum -> "Do 10 rokov"/"Do 14 rokov" for the ComboBox
Models/
  Enums.cs                        Rulebook, Category enums + ToSlovakLabel() (label comes from AgeGroups)
  AgeGroups.cs                    Age-group definitions: tables (size, column values, empty cells), picks, DB folder/prefix, age limit
  ExerciseImageRepository.cs      Scans DataBase/ and indexes images by (rulebook, table id, row, col)
  WpfImageLoader.cs               Loads a frozen BitmapImage from an absolute path
  PdfPrinting/
    AcroFieldNames.cs             PDF AcroForm field-name constants + ExerciseSlotSelection / PickedExercise records
    TrainingPlanFormData.cs       Backend data model of the whole sheet (text fields + SlotCount ≤ 12 slots), difficulty/ID logic
    TrainingPlanPdfIText.cs       iText export: fill fields, place images, remove image fields, flatten
ViewModels/
  ViewModelBase.cs                INotifyPropertyChanged + SetProperty
  RelayCommand.cs                 ICommand (Action / Action<object?>)
  ExerciseCardVm.cs               One tile — used both for the main slots (Okno) AND the picker grid cells
  MainWindowViewModel.cs          All main-screen state + selection rules + PDF export
  ExerciseGridViewModel.cs        Pop-up grid state
DataBase/                         Exercise picture "database" (PNG files, ~684 MB) — see §5
Cvicebny-plan_Template.pdf        Fillable PDF template (1 page, 792x612 pt landscape)
assets/                           Icon, SGF logos (placeholder image), NotoSans-Bold.ttf
```

## 4. Architecture

MVVM-lite, hand-rolled (no MVVM framework, no DI container):

```
MainWindow.xaml ──DataContext (created in XAML)──> MainWindowViewModel
   │                                                   ├── ExerciseImageRepository (built once in ctor)
   │                                                   ├── TrainingPlanFormData FormData  (single source of truth for the PDF)
   │                                                   └── ObservableCollection<ExerciseCardVm> ExerciseSlots (12, UI mirror)
   │
   ├─ code-behind opens EventNameDialog / TrainerClubDialog (DataContext = the dialog itself)
   │
   └─ OpenSlotCommand ─> new ExerciseGridViewModel(...) + new ExerciseGridWindow (ShowDialog)
                            event ExerciseSelected(ExerciseCardVm) / event RequestClose
```

- **Two parallel representations of the 12 slots** must stay in sync:
  `FormData` (`ExerciseSlotSelection?[12]` via `SetSlot`/`ClearSlot`) is what gets printed;
  `ExerciseSlots` (`ExerciseCardVm`) is what's shown. `OpenSlot` and `ClearExerciseSelections` update both.
  Any new feature that changes a slot must update both and call `RecalculateExerciseSummary()`.
- View-model properties for header/athlete fields are thin wrappers that write straight into `FormData`.
  Birth years: UI uses a year ComboBox (`BirthYearOptions`, empty item first so it can be cleared), backend stores the
  year as `string` in the `Athlete*Dob` properties (issue #28).
- The VM shows `MessageBox` directly (confirmation on clearing, export errors). There is no dialog service; follow that style.
- Radio buttons bind to bool wrapper properties (`IsCombi`, `IsWP`, …) that set the underlying enum.

## 5. Exercise database (`DataBase/`)

```
DataBase/
  do10_db/   (Rulebook.DO_10_ROK, file prefix "10r")
  do14_db/   (Rulebook.DO_14_ROK, file prefix "14r")
    wp/ mp/ m_x_p/ wg/ mg/ inv/        (ExerciseTable.Id in AgeGroups.cs)
      <prefix>_<cat>_<COL>_<ROW>.png   e.g. 10r_mp_03_02.png = column 3, row 2
          input_4_crop/                    original uncropped source images (NOT indexed; kept on purpose, see below)
  AutoCrop_TableBar_Resize_v4_Dialog.jsx   Photoshop batch script used to crop input_4_crop -> final PNGs

  Owner's export for #27 (2026-10-28), read through index.csv — keep folder and file names as they are (D17):
  new_do8/       Do 8 rokov            index.csv + DUO-I-1.png …, IND-….png
  new_zat_do14/  Od 8 – 14 rokov       index.csv + PAR-/TRO-/STV-/IND-….png   ("zat" = začiatočníci)
  new_inv/       Individuálna zostava  index.csv + A1.png … H6.png, _preview_all.png (not indexed, kept on purpose)
```

- Do 10 / Do 14: 2 rulebooks × 6 categories × 30 images (5 columns × 6 rows) = 360 indexed PNGs, each 1032×536 RGBA.
- New groups (`AgeGroupDefinition.IndexFile = "index.csv"`): `ExerciseImageRepository.IndexFromCsv` finds the columns
  by header name (`id`, `row`, `column`, `value`|`difficulty`, `file` — order differs between the folders). Table =
  id prefix via `IndexTablesByIdPrefix` (`DUO`, `PAR`, `TRO`, `STV`, `IND`; empty map = only the Inv table, `new_inv`).
  Row = `Riadok I–V` → 1–5, `Pružnosť`/`Statika`/`Dynamika` → 1–3, `A–H` → 1–8. The CSV value is only checked; the
  table definition decides. Problems (unknown row, value mismatch, missing file, …) → `Problems`, traced at startup.
- The new pictures are cropped tight to the drawing (76–516 px, ratio 0.33–3 : 1), so the picker and Okno tiles show
  every picture in a fixed 1032×536 area (§8); the PDF fits them into the box anyway.
- Missing picture → SGF placeholder. The #27 mock (`MockImageRelativePath`) is still supported but no group uses it.
- **Database check:** `"Akro Tool SK.exe" --check-database` (or `tools/check-database.ps1`) writes
  `database-check.txt` next to the exe — pictures per table, missing positions, CSV problems; exit code 0 = OK.
- File-name category token: `wp, mp, mxp (folder m_x_p), wg, mg, inv`. **The filename order is COL then ROW.**
- `ExerciseImageRepository` regex: `^(?<prefix>[a-z0-9]+)_(?<cat>[a-z_]+)_(?<a>\d{2})_(?<b>\d{2})\.png$`, stores
  `col = a`, `row = b`. Only files directly inside a table folder are indexed (`input_4_crop` is ignored).
  The prefix must match the age group's `FilePrefix` or the file is skipped; positions outside the table or on an
  empty cell are skipped too.
- DB root is located by `ExerciseImageRepository.ResolveDbRoot()`: walks up to 8 parents from the exe dir looking for
  `DataBase|Database|database` containing any age-group folder. If not found → empty index (all tiles show placeholder).
- Grid coordinates throughout the code: **X = column, Y = row**, both 1-based; the table size comes from
  `AgeGroups.cs` (Do 10 / Do 14: 5 columns × 6 rows). `TrainingPlanFormData.SetSlot` validates against the table.
- The repo intentionally holds the **full** picture DB including `input_4_crop/` sources and the `.jsx` script —
  it's public SGF material, and the repo is not the install folder. Don't remove them or exclude them "to save space".
- Adding a new rulebook = new enum value + an `AgeGroupDefinition` in `AgeGroups.All` + DB folder.

## 6. Domain rules (implemented)

Located in `MainWindowViewModel` (counting/row blocking) and `ExerciseGridViewModel` (applying the block):

| Rule | Where | Issue |
|---|---|---|
| Exactly **TeamPicks + InvPicks** exercises (Do 10 / Do 14: **6 from the selected category table + 6 from the Individual (Inv) table**) | `OpenSlot` forces/locks the table toggle when one side is full | #1, #11 |
| **Only one exercise per row** per table (Inv rows and category rows are tracked separately) | `GetBlocked*RowsExcludingSlot` → `IsSelectable=false` / `IsBlockedByRowRule` (greyed, 0.45 opacity) | #15 |
| Re-opening an already filled slot excludes that slot from the counts/blocks, so it can be swapped freely | `*ExcludingSlot(slotIndex)` helpers | #1 |
| Changing rulebook or category clears all slots (Yes/No confirm **before** the change if any are set; "Nie" keeps the old selection) | `SelectedRulebook`/`SelectedBackendCategory` setters → `ConfirmClearExerciseSelections` / `ResetExerciseSlots` / `RestoreSelectionInUi` | (commit a03b6b1), Phase 1 |
| Printing blocked if fewer than `SlotCount` slots are set | `ExportFilledPdf` | #14 |
| **Difficulty** of an exercise = the table's column value; Do 10 / Do 14 = `column × 0.01` (col 1..5 → 0.010..0.050), independent of row/category | `ExerciseTable.GetDifficulty` in `AgeGroups.cs`, stored per slot in `ExerciseSlotSelection.Difficulty` | #12, #13 |
| **Exercise ID** = `R{row}`; Inv exercises print as `InvR{row}` on the PDF | `TrainingPlanFormData.GetSlotExerciseId` | #13 |
| Slot label after selection: `"{difficulty:0.000} - R{row} - {Category}"`; grid label: `"R{row} - {difficulty}"` | `BuildSlotLabel`, `BuildExerciseGridLabel` | #13 |
| Summary bar: `Obť.: <sum>` and `PickCounterText` = `<Cat>: n/{TeamPicks}   Inv: n/{InvPicks}` (Individuálna zostava: `Inv: n/8`) | `RecalculateExerciseSummary` | #12 |
| Categories per age group (`IsWPEnabled` …); switching to a group without the current category falls back to WP | `SelectedRulebook` setter → `ApplyCategory` | #27 |
| **Zvlášť** (one exercise per team member in one Okno): button on empty Okno of team age groups; picker opens once per member Base → Top (`GetTeamMembers`: pair Base/Top, trio Base/Middle(1st card)/Top, quartet Base/Middle(2nd)/Middle(1st)/Top); member 1 chooses row + table, the others only that row (same exercise allowed); "Zavrieť" cancels the whole Okno. Okno value = average rounded to 3 decimals (`ExerciseSlotSelection.Difficulty`), total = sum of Okno values. Tile shows ‹ › and a performer overlay (name ≥ 3 chars, else member number; resolved live from the athlete cards). Clicking a filled Okno in a team group opens `SlotChangeDialog` (Jedno cvičenie / Zvlášť / Zrušiť) | `OpenSlotSeparate`, `PickExercise`, `ApplySlotToCard`, `CycleSeparateExercise`, `UpdatePerformerText` | #29 |
| **Individuálna zostava**: only the Inv slot of the definition (8×6 table, 8 picks), no toggle, category + discipline radios disabled; PDF `category1 = Inv`, `discipline = Individuálna zostava`, `SOLO`, IDs `R{row}` | `AgeGroups.Individual()`, `UpdatePdfCategoryFields`, `GetSlotExerciseId` | #27 |
| "Clear table" button (Vyčistiť tabuľku) | `ClearTableCommand` | #21 |
| Unused athlete fields are **not** greyed out (owner decided coaches may deviate from rulebook assignments) | — | #17 |
| **Age check** before printing (warning only): age counts by year (`competition year − birth year`, competition year = current year); turning 10 during the year is already too old for *Do 10 rokov*. Limits per group (`AgeGroupDefinition.MinAge/MaxAge`): Do 8 max 7 · Do 10 max 9 · Do 14 max 13 · Od 8 – 14 min 8, max 13 · Individuálna zostava none. Only the cards the team uses are checked (pair: Top + Base, WG: + 1st Middle, MG: all four); empty years are skipped. Dialog `AgeWarningDialog` lists too old and too young separately: **OK** = don't print, **Tlačiť aj tak** = print. | `ConfirmAgeGroupOrPrintAnyway`, `GetTeamMembers` | #28 |

The difficulty rule (`column × 0.01`) is **confirmed correct** by the owner for Do 10 / Do 14 — don't change it.
The new age groups from #27 use per-table column values instead (see `docs/specification/01-age-group-tables.md`).

Team size text printed from category: WP/MP/MxP → `DUO`, WG → `TRIO`, MG → `QUARTET`, Inv → `SOLO` (`MapTeamSizeText`).

## 7. PDF export (`Models/PdfPrinting`)

Template `Cvicebny-plan_Template.pdf`: one landscape page, 16 picture boxes (2 rows × 8). AcroForm fields (names in `AcroFieldNames`):

| Field | Filled from | Notes |
|---|---|---|
| `eventName`, `clubName` | dialogs | |
| `trainer.name`, `trainer.contact` | TrainerClubDialog | |
| `category1` | `SelectedBackendCategory.ToString()` (`WP`, `MxP`, …); Individuálna zostava: `Inv` | |
| `category2` | `SelectedRulebook.ToSlovakLabel()` | |
| `discipline` | `SelectedRoutine.ToSlovakLabel()`; Individuálna zostava: `Individuálna zostava` | e.g. `Zmiešaná zostava` (issue #26) |
| `teamSizeText` | `MapTeamSizeText` | |
| `overallDifficulity` | sum of the Okno values, `0.000`, sk-SK (comma decimal); same on every page | |
| `athleteTop/athleteMiddle[1]/athleteMiddle[2]/athleteBase` `.name` / `.dateOfBirth` | athlete cards | `.dateOfBirth` holds only the birth year (issue #28) |
| `Image1_af_image.0..N-1` | picture boxes (button fields used only for their rectangles). N = boxes per page, counted from the template (16 since the owner's edit for #30; boxes 1–12 unchanged, 13–16 in the second row) | |
| `ExerciseDifficulity.k`, `ExerciseID.k` | per box: Okno value (only on `n.a` of a Zvlášť Okno, the rest blank) and ID (`R1`, `InvR1`, Zvlášť: `R1.Top`, `R1.Middle2`, `InvR1.Base`) | #30 |
| `BoxNumber.k` (optional) | box number `1`, `2.a`, `2.b`, … If the template has no such fields, the printed numbers 1..N are covered and redrawn where they differ (`ReplacePrintedBoxNumber`, geometry of the current template) | #30 |
| `catNum1`, `catNum2`, `invidualnaZostava`, `stalkovaZostava` | **intentionally left empty** — the coach fills them in by pen on the printed sheet (client's request). Don't fill them. | issue #2 |

Spelling of field names (`Difficulity`, `invidualna`) is baked into the template — **do not "fix" the constants** unless the template is changed too.

Export pipeline (`TrainingPlanPdfIText.ExportFromFormData`):
1. `FormData.ToHeaderFieldMap(culture)` (all non-box fields) + `FormData.ToPrintBoxes()` (boxes in routine order:
   Okno `n`, Zvlášť Okno → `n.a`, `n.b`, … Base → Top, issue #30 Variant B).
2. Boxes per page = number of `Image1_af_image.*` fields; pages = ceil(boxes / per page). Each page is a fresh copy of
   the template filled in memory (`FillPage`), then all pages are joined with iText `PdfMerger`.
3. Per page: embedded Unicode font from `BaseDirectory\Assets\NotoSans-Bold.ttf` (IDENTITY_H) — required for Slovak
   characters like `Č` (issue #20; Helvetica could not print them).
4. `FillTextFields`: header + per-box value/ID/BoxNumber, empty values skipped, `SetValue(value, font, fontSize)`
   keeping the field's own font size (0 = auto) → `RegenerateField()`.
5. `PlaceImagesIntoFieldRects`: for each image field, read its widget rect, scale image to fit with 3 pt margin,
   center it, `SetFixedPosition` on the page, then **remove the image field** (otherwise its appearance covers the picture).
6. Remove parent `Image1_af_image` (unused boxes print empty); template without `BoxNumber` fields: redraw differing
   box numbers (Helvetica 8.5 pt) and blank the unused ones on extra pages and on any page whose numbering differs
   (Zvlášť), so a sheet never reads "… 7, 8, 11, 12"; then `form.FlattenFields()`.
The 16-box template (2026-10-28) has no `BoxNumber` fields: boxes 1–12 are geometrically identical to the old 12-box
template, so the redraw geometry fits; the printed 13–16 are slightly smaller but still inside the white cover rect
(checked on the host at 3× zoom). A plan without Zvlášť prints like the pre-#30 output plus 4 empty boxes (13–16),
which keep their printed numbers.

`DumpFieldNames(templatePath)` is a debug helper (console output) to list template fields.
If the template is edited in Acrobat, re-check field names with it (or `pypdf`: `PdfReader(...).get_fields()`).

## 8. UI details

- **MainWindow**: 1300×850, min 1100×750 — both shrunk to the work area at startup and centered (`WindowSizing`). Rows: menu (Súbor: edit event name, edit trainer/club, export PDF, exit;
  Pomoc: Autorské práva) → top controls (Tlačiť, Vyčistiť tabuľku, Obť. counter, category counters; rulebook ComboBox;
  Disciplína GroupBox with 2 radio groups; 4 athlete cards) → 12-slot `ItemsControl` (`UniformGrid Columns=4`) → footer
  (Tréner / Názov klubu / Podujatie).
- The Okno tile template is **inline** in the `ItemsControl`: a `Grid` with the tile `Button` plus **sibling** overlay
  buttons (‹ ›, "Zvlášť") and the performer overlay — siblings, not nested, so their clicks never open the picker.
- **Picture area** of the Okno tile and the picker tile: a `Viewbox` around a fixed 1032×536 `Grid` (white), picture
  `Stretch=Uniform` inside (picker: margin 52,27 = the old 90 % size). The tile size never depends on the picture —
  needed for the tightly cropped pictures of the new groups; the classic 1032×536 pictures look as before.
- Summary bar `Obť.` shows 3 decimals (Zvlášť averages like 0.015).
- **ExerciseGridWindow**: 1250×850, min 1050×700. Header shows `WindowTitle` (e.g. `Cvičenia: WP (Do 10 rokov)`),
  a `ToggleButton` bound to `ShowIndividualTable` (disabled when `CanToggleIndividual` is false, hidden when the age
  group has only one table), and "Zavrieť". Title shows the table name when it differs, e.g. `Cvičenia: WP – Pár (Od 8 – 14 rokov)`.
  Grid: `UniformGrid` with `VisibleColumns`/`VisibleRows` from the table over `VisibleExercises` (switches between
  `Exercises` and `IndividualExercises`), top-aligned; empty cells are hidden placeholders (`IsEmptyCell`).
  Clicking a tile raises `ExerciseSelected` then `RequestClose`; closing with "Zavrieť" leaves the slot unchanged.
- Placeholder image for empty slots/missing pictures: `pack://application:,,,/assets/sgf_logo1.png`.
- Startup dialogs are shown from `MainWindow.ContentRendered` (once).

## 9. Known quirks / tech debt (verified by reading the code — confirm with the owner before "fixing")

1. Number formatting differs on purpose: UI labels use InvariantCulture (`0.010`), the PDF uses sk-SK (`0,010`).
2. `assets\sgf_logo2.png` is embedded as a Resource but not used (SGF material, kept).
3. Confirmation boxes (`MessageBox` Yes/No) show the button texts in the **Windows language** (Hungarian on the
   dev PC, Slovak on coaches' PCs); the custom dialogs (`AgeWarningDialog`, `SlotChangeDialog`) are always Slovak.

Fixed 2026-10-28 (PR "small issues"): font path case (`assets`), copyright box line breaks, `THIRD-PARTY-NOTICES.txt`
(real repo URL, Noto copyright) + `LICENSES/OFL-1.1.txt` added, license files copied next to the exe, footer
"Názov klubu", unused code removed (`PlaceholderImagePath`, `GetImagePathXY`, `SetSlotFromImagePath`,
`TryParseImageFileName`, two unused DataTemplates), binding log rotated above 1 MB, windows fit the screen's work
area (`WindowSizing.FitToWorkArea`, for 125–150 % scaling). Earlier: discipline names (#26), duplicated difficulty
rule (Phase 1).

**Rejected by the client — treat as non-existent, don't implement or "restore":** #6 (default/minimum birth date),
#23 (right-click removes an exercise), #5's comment about starting in fullscreen.

Fixed: `App.xaml.cs` previously had no namespace, so its `OnStartup` (binding-error log) never ran — now in
`Atletika_SutaznyPlan_Generator`.

## 10. Development history (from issues & commits)

Timeline (Feb–Mar 2026, single developer):
- Initial import with full picture DB, cropping via Photoshop script; console smoke-test app proved picture placement into the PDF.
- Backend set up (`TrainingPlanFormData`, `AcroFieldNames`, iText exporter), then MVVM UI with placeholders, Inv toggle,
  rulebook ComboBox labels, startup dialogs, full PDF fill.
- Issue #1 (pop-up selector, 6+6 rule) — original idea included click-ordering with number overlays; final design is
  per-slot selection instead.
- Issue #2 (questions for the federation, Hungarian): birth-year label, trailing zero in `0,010`, when to tick
  Individuálna/Stálková zostava, use of catNum1/catNum2, greying unused athletes, app name → **Akro Tool SK**.
- #9/#20: template fonts changed in Acrobat Pro; NotoSans-Bold embedded so `Č` prints.
- #11/#12/#13/#14/#15: selection limits, counters, IDs, print guard, row rule.
- #4/#7/#8/#5: installer/app icon, desktop shortcut, min window sizes.
- #18: open-sourced under AGPL (required by iText Community).
- #19: removed old MIT PDF library and `TrainingPlanPdf.cs`.
- PR #24 `polishing_n_repair` merged into `master`.

## 11. Conventions for changes

- Match existing style: file-scoped braces namespaces (`namespace X { ... }`), `SetProperty` pattern, `RelayCommand`,
  `// === Section ===` comment headers, Slovak UI strings, English code/comments.
- Keep `FormData` and `ExerciseSlots` in sync (§4). Keep X=column, Y=row.
- Don't rename PDF field constants or DB file/folder names without changing the template/DB.
- New assets that must exist next to the exe → `Content` + `CopyToOutputDirectory`; images shown in WPF → `Resource`.
- Never claim UI behavior was tested unless it was run on Windows; in the cloud only compile checks are possible.
- Main branch is `master`.
