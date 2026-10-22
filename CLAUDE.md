# CLAUDE.md — Akro Tool SK

Technical reference for working on this repo. Read this first, then open only the files the task needs.

## 1. What the app is

**Akro Tool SK** is a Windows desktop app (WPF, .NET 8) that lets **sports-acrobatics coaches** fill out the
official Slovak competition plan sheet (*"Súťažný plán – športová akrobacia"*) and export it as a **flattened PDF**.

Coach workflow:
1. On start, the app asks for the **event name**, then **trainer / contact / club** (two modal dialogs).
2. Coach picks **age group** (rulebook: *Do 10 rokov* / *Do 14 rokov*), **discipline** (Combi / Dynamic / Balance)
   and **category** (WP, MP, MxP, WG, MG).
3. Coach enters up to 4 athletes (Top, Middle, Middle, Base) with names and birth years.
4. Coach clicks each of the **12 slots** ("Okno 1..12"). A pop-up grid shows the exercise pictures from the database
   (6 rows × 5 columns) for the chosen category, with a toggle to the **Individual (Inv)** table.
5. **Tlačiť** (Print) → Save dialog → fills `Cvicebny-plan_Template.pdf` with text + the 12 pictures, flattens it.

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
| License | AGPL-3.0 (`COPYING.txt`), third-party notes in `THIRD-PARTY-NOTICES.txt` |
| Tests | None. (An old external `Atletika.SmokeTests` console app was only used for beta experiments; its sln reference was removed.) |

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
Converters/
  RulebookToSlovakLabelConverter  Rulebook enum -> "Do 10 rokov"/"Do 14 rokov" for the ComboBox
Models/
  Enums.cs                        Rulebook, Category enums + ToSlovakLabel()
  CategoryMap.cs                  Category -> DB folder name
  ExerciseImageRepository.cs      Scans DataBase/ and indexes images by (rulebook, category, row, col)
  WpfImageLoader.cs               Loads a frozen BitmapImage from an absolute path
  PdfPrinting/
    AcroFieldNames.cs             PDF AcroForm field-name constants + ExerciseSlotSelection record
    TrainingPlanFormData.cs       Backend data model of the whole sheet (text fields + 12 slots), difficulty/ID logic
    TrainingPlanPdfIText.cs       iText export: fill fields, place images, remove image fields, flatten
ViewModels/
  ViewModelBase.cs                INotifyPropertyChanged + SetProperty
  RelayCommand.cs                 ICommand (Action / Action<object?>)
  ExerciseCardVm.cs               One tile — used both for the 12 main slots AND the 30 grid cells
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
    wp/ mp/ m_x_p/ wg/ mg/ inv/        (CategoryMap.FolderName)
      <prefix>_<cat>_<COL>_<ROW>.png   e.g. 10r_mp_03_02.png = column 3, row 2
          input_4_crop/                    original uncropped source images (NOT indexed; kept on purpose, see below)
  AutoCrop_TableBar_Resize_v4_Dialog.jsx   Photoshop batch script used to crop input_4_crop -> final PNGs
```

- 2 rulebooks × 6 categories × 30 images (5 columns × 6 rows) = 360 indexed PNGs, each 1032×536 RGBA.
- File-name category token: `wp, mp, mxp (folder m_x_p), wg, mg, inv`. **The filename order is COL then ROW.**
- `ExerciseImageRepository` regex: `^(?<prefix>\d+r)_(?<cat>[a-z_]+)_(?<a>\d{2})_(?<b>\d{2})\.png$`, stores
  `col = a`, `row = b`. Only files directly inside the category folder are indexed (`input_4_crop` is ignored).
  The prefix must match the rulebook folder or the file is skipped.
- DB root is located by `MainWindowViewModel.ResolveDbRoot()`: walks up to 8 parents from the exe dir looking for
  `DataBase|Database|database` containing `do10_db` or `do14_db`. If not found → empty index (all tiles show placeholder).
- Grid coordinates throughout the code: **X = column (1..5), Y = row (1..6)**. `TrainingPlanFormData.SetSlot` validates these ranges.
- The repo intentionally holds the **full** picture DB including `input_4_crop/` sources and the `.jsx` script —
  it's public SGF material, and the repo is not the install folder. Don't remove them or exclude them "to save space".
- Adding a new rulebook = new enum value + `RulebookFolder`/`RulebookPrefix`/`ToSlovakLabel` cases + `TryParseImageFileName` + DB folder.

## 6. Domain rules (implemented)

Located in `MainWindowViewModel` (counting/row blocking) and `ExerciseGridViewModel` (applying the block):

| Rule | Where | Issue |
|---|---|---|
| Exactly **12** exercises: **6 from the selected category table + 6 from the Individual (Inv) table** | `OpenSlot` forces/locks the table toggle when one side already has 6 | #1, #11 |
| **Only one exercise per row** per table (Inv rows and category rows are tracked separately) | `GetBlocked*RowsExcludingSlot` → `IsSelectable=false` / `IsBlockedByRowRule` (greyed, 0.45 opacity) | #15 |
| Re-opening an already filled slot excludes that slot from the counts/blocks, so it can be swapped freely | `*ExcludingSlot(slotIndex)` helpers | #1 |
| Changing rulebook or category clears all 12 slots (with Yes/No confirm if any are set) | `SelectedRulebook`/`SelectedBackendCategory` setters → `ClearExerciseSelections()` | (commit a03b6b1) |
| Printing blocked if fewer than 12 slots are set | `ExportFilledPdf` | #14 |
| **Difficulty** of an exercise = `column × 0.01` (col 1..5 → 0.010..0.050), independent of row/category/rulebook | `TrainingPlanFormData.DefaultDifficulty` (swappable via `DifficultyRule`) **and duplicated** in `ExerciseGridViewModel.GetExerciseDifficulty` | #12, #13 |
| **Exercise ID** = `R{row}`; Inv exercises print as `InvR{row}` on the PDF | `TrainingPlanFormData.GetSlotExerciseId` | #13 |
| Slot label after selection: `"{difficulty:0.000} - R{row} - {Category}"`; grid label: `"R{row} - {difficulty}"` | `BuildSlotLabel`, `BuildExerciseGridLabel` | #13 |
| Summary bar: `Obť.: <sum>` and `<Cat>: n/6   Inv: n/6` | `RecalculateExerciseSummary` | #12 |
| "Clear table" button (Vyčistiť tabuľku) | `ClearTableCommand` | #21 |
| Unused athlete fields are **not** greyed out (owner decided coaches may deviate from rulebook assignments) | — | #17 |
| **Age check** before printing (warning only): age counts by year (`competition year − birth year`, competition year = current year); turning 10 during the year is already too old for *Do 10 rokov*. Dialog `AgeWarningDialog`: **OK** = don't print, **Tlačiť aj tak** = print. Other age groups are not checked yet. | `ConfirmAgeGroupOrPrintAnyway`, `GetMaxAgeInYear` | #28 |

The difficulty rule (`column × 0.01`) is **confirmed correct** by the owner — don't change it.

Team size text printed from category: WP/MP/MxP → `DUO`, WG → `TRIO`, MG → `QUARTET`, Inv → `SOLO` (`MapTeamSizeText`).

## 7. PDF export (`Models/PdfPrinting`)

Template `Cvicebny-plan_Template.pdf`: one landscape page. AcroForm fields (names in `AcroFieldNames`):

| Field | Filled from | Notes |
|---|---|---|
| `eventName`, `clubName` | dialogs | |
| `trainer.name`, `trainer.contact` | TrainerClubDialog | |
| `category1` | `SelectedBackendCategory.ToString()` (`WP`, `MxP`, …) | |
| `category2` | `SelectedRulebook.ToSlovakLabel()` | |
| `discipline` | `SelectedRoutine.ToSlovakLabel()` | e.g. `Zmiešaná zostava` (issue #26) |
| `teamSizeText` | `MapTeamSizeText` | |
| `overallDifficulity` | sum of 12 difficulties, `0.000`, sk-SK (comma decimal) | |
| `athleteTop/athleteMiddle[1]/athleteMiddle[2]/athleteBase` `.name` / `.dateOfBirth` | athlete cards | `.dateOfBirth` holds only the birth year (issue #28) |
| `Image1_af_image.0..11` | 12 slot images (button fields used only for their rectangles) | |
| `ExerciseDifficulity.0..11`, `ExerciseID.0..11` | per slot | |
| `catNum1`, `catNum2`, `invidualnaZostava`, `stalkovaZostava` | **intentionally left empty** — the coach fills them in by pen on the printed sheet (client's request). Don't fill them. | issue #2 |

Spelling of field names (`Difficulity`, `invidualna`) is baked into the template — **do not "fix" the constants** unless the template is changed too.

Export pipeline (`TrainingPlanPdfIText.ExportFromFormData`):
1. Open template with `PdfReader` → `PdfWriter(outputPath)`.
2. Create embedded Unicode font from `BaseDirectory\Assets\NotoSans-Bold.ttf` (IDENTITY_H) — required for Slovak
   characters like `Č` (issue #20; Helvetica could not print them).
3. `FillTextFields`: for each entry of `FormData.ToTextFieldMap(culture)` (empty values are skipped), `SetValue(value, font, fontSize)`
   keeping the field's own font size (0 = auto) → `RegenerateField()`.
4. `PlaceImagesIntoFieldRects`: for each image field, read its widget rect, scale image to fit with 3 pt margin,
   center it, `SetFixedPosition` on the page, then **remove the image field** (otherwise its appearance covers the picture).
5. Remove parent `Image1_af_image`, then `form.FlattenFields()`.

`DumpFieldNames(templatePath)` is a debug helper (console output) to list template fields.
If the template is edited in Acrobat, re-check field names with it (or `pypdf`: `PdfReader(...).get_fields()`).

## 8. UI details

- **MainWindow**: 1300×850, min 1100×750. Rows: menu (Súbor: edit event name, edit trainer/club, export PDF, exit;
  Pomoc: Autorské práva) → top controls (Tlačiť, Vyčistiť tabuľku, Obť. counter, category counters; rulebook ComboBox;
  Disciplína GroupBox with 2 radio groups; 4 athlete cards) → 12-slot `ItemsControl` (`UniformGrid Columns=4`) → footer
  (Tréner / Názov klubu / Podujatie).
- The 12-slot tile template is **inline** in the `ItemsControl`. `ExerciseCardTemplate` and `CategoryTileTemplate` in
  `Grid.Resources` are leftovers and not used.
- **ExerciseGridWindow**: 1250×850, min 1050×700. Header shows `WindowTitle` (e.g. `Cvičenia: WP (Do 10 rokov)`),
  a `ToggleButton` bound to `ShowIndividualTable` (disabled when `CanToggleIndividual` is false), and "Zavrieť".
  Grid: `UniformGrid Columns=5 Rows=6` over `VisibleExercises` (switches between `Exercises` and `IndividualExercises`).
  Clicking a tile raises `ExerciseSelected` then `RequestClose`; closing with "Zavrieť" leaves the slot unchanged.
- Placeholder image for empty slots/missing pictures: `pack://application:,,,/assets/sgf_logo1.png`.
- Startup dialogs are shown from `MainWindow.ContentRendered` (once).

## 9. Known quirks / tech debt (verified by reading the code — confirm with the owner before "fixing")

1. `Assets\NotoSans-Bold.ttf` path in `TrainingPlanPdfIText` uses capital `A` and a backslash; works only because
   Windows paths are case-insensitive.
2. (Fixed in #26) Discipline names: enum `RoutineType { Combi, Dynamic, Balance }`; the radio buttons and the PDF both
   show `RoutineTypeExtensions.ToSlovakLabel()` — *Zmiešaná zostava / Dynamická zostava / Balančná zostava*
   (Balance is also called *statická zostava*).
3. Difficulty rule duplicated in `ExerciseGridViewModel.GetExerciseDifficulty` and `TrainingPlanFormData.DefaultDifficulty`.
   Number formatting differs: UI labels use InvariantCulture (`0.010`), the PDF uses sk-SK (`0,010`), the summary bar uses `0.00`.
4. Copyright message box: missing `\n` between the source URL and "See COPYING…"; `THIRD-PARTY-NOTICES.txt` still has
   placeholder `https://your-repo.example` and references a non-existent `LICENSES/OFL-1.1.txt`.
5. Footer label typo `NázovKlubu` (missing space).
6. Unused code: `PlaceholderImagePath` const, `GetImagePathXY`, `SetSlotFromImagePath`/`TryParseImageFileName`
   (kept as utilities), the two unused DataTemplates in MainWindow, `sgf_logo2.png` resource.
7. `wpf-binding-errors.log` (next to the exe) is appended to on every run and never rotated.

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
