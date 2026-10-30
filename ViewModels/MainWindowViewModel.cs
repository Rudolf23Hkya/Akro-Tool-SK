using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Atletika_SutaznyPlan_Generator.Models;
using Atletika_SutaznyPlan_Generator.Models.PdfPrinting;
using Atletika_SutaznyPlan_Generator.Models.PlanHistory;
using Microsoft.Win32;

namespace Atletika_SutaznyPlan_Generator.ViewModels
{
    public enum RoutineType { Combi, Dynamic, Balance }

    public static class RoutineTypeExtensions
    {
        // Same text in the app and on the printed sheet (issue #26)
        public static string ToSlovakLabel(this RoutineType routine) => routine switch
        {
            RoutineType.Combi => "Zmiešaná zostava",
            RoutineType.Dynamic => "Dynamická zostava",
            RoutineType.Balance => "Balančná zostava",
            _ => routine.ToString()
        };
    }

    public class MainWindowViewModel : ViewModelBase
    {
        // === Placeholder (WPF Resource) ===

        private const string PlaceholderPackUri = "pack://application:,,,/assets/sgf_logo1.png";
        private readonly ImageSource? _placeholderImage = LoadPackImage(PlaceholderPackUri);

        // === Backend objects ===
        private readonly ExerciseImageRepository _repo;
        private readonly PlanHistoryStore _history = new(PlanHistoryStore.DefaultDirectory());
        public TrainingPlanFormData FormData { get; } = new();

        private bool HasAnySelectedExercise()
            => ExerciseSlots.Any(s => !string.IsNullOrWhiteSpace(s.ImagePath));

        // === Rulebook (age category) ===
        // Same order as AgeGroups.All
        public ObservableCollection<Rulebook> Rulebooks { get; } =
            new(AgeGroups.All.Select(g => g.Rulebook));

        private Rulebook _selectedRulebook = Rulebook.DO_10_ROK;
        public Rulebook SelectedRulebook
        {
            get => _selectedRulebook;
            set
            {
                if (_selectedRulebook == value) return;

                // Ask BEFORE changing anything: on "Nie" the old age group stays selected
                if (!ConfirmClearExerciseSelections(ChangeSelectionMessage, ChangeSelectionTitle))
                {
                    RestoreSelectionInUi(nameof(SelectedRulebook));
                    return;
                }

                ApplyRulebook(value);

                // E.g. WG -> Do 8 rokov (pairs only): fall back to WP, the Okno are cleared anyway
                if (CurrentAgeGroup.HasTeamTables && !CurrentAgeGroup.IsCategoryAllowed(_selectedBackendCategory))
                    ApplyCategory(Category.WP);

                UpdatePdfCategoryFields();
                ResetExerciseSlots();
            }
        }

        // Sets the age group without asking (the setter asks first; loading a plan from history asks once)
        private void ApplyRulebook(Rulebook value)
        {
            _selectedRulebook = value;
            OnPropertyChanged(nameof(SelectedRulebook));
            OnPropertyChanged(nameof(CurrentAgeGroup));
            OnPropertyChanged(nameof(TeamPicks));
            OnPropertyChanged(nameof(InvPicks));

            OnPropertyChanged(nameof(IsWPEnabled));
            OnPropertyChanged(nameof(IsMPEnabled));
            OnPropertyChanged(nameof(IsMxPEnabled));
            OnPropertyChanged(nameof(IsWGEnabled));
            OnPropertyChanged(nameof(IsMGEnabled));
            OnPropertyChanged(nameof(IsDisciplineEnabled));
        }

        // === Selected age group definition (tables, number of Okno, limits) ===
        public AgeGroupDefinition CurrentAgeGroup => AgeGroups.Get(SelectedRulebook);
        public int TeamPicks => CurrentAgeGroup.TeamPicks;
        public int InvPicks => CurrentAgeGroup.InvPicks;

        private const string ChangeSelectionMessage =
            "Zmena kategórie alebo vekovej skupiny vymaže všetky zvolené cviky. Pokračovať?";
        private const string ChangeSelectionTitle = "Potvrdenie zmeny";

        private bool ConfirmClearExerciseSelections(string message, string title)
        {
            if (!HasAnySelectedExercise())
                return true;

            var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning);
            return result == MessageBoxResult.Yes;
        }

        // The ComboBox / radio button already shows the rejected value; re-read the old one after the
        // current binding update has finished (a synchronous PropertyChanged would be ignored).
        private void RestoreSelectionInUi(params string[] propertyNames)
        {
            Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
            {
                foreach (var name in propertyNames)
                    OnPropertyChanged(name);
            }));
        }

        // Empty Okno for the current age group (number of Okno can differ between age groups)
        private void ResetExerciseSlots()
        {
            FormData.ResetSlots(CurrentAgeGroup.SlotCount);
            SeedSlots();
            RecalculateExerciseSummary();
        }

        // === Printing ===
        public ICommand ExportPdfCommand { get; }
        public ICommand ClearTableCommand { get; }

        // === Routine (Combi/Dynamic/Balance) maps into FormData.Discipline ===
        private RoutineType _selectedRoutine = RoutineType.Balance;
        public RoutineType SelectedRoutine
        {
            get => _selectedRoutine;
            set
            {
                if (!SetProperty(ref _selectedRoutine, value)) return;
                OnPropertyChanged(nameof(IsCombi));
                OnPropertyChanged(nameof(IsDynamic));
                OnPropertyChanged(nameof(IsBalance));

                // backend priority: store as string
                UpdatePdfCategoryFields();
            }
        }

        // Individuálna zostava has a fixed discipline on the PDF (decision D11)
        public bool IsDisciplineEnabled => CurrentAgeGroup.HasTeamTables;

        public bool IsCombi { get => SelectedRoutine == RoutineType.Combi; set { if (value) SelectedRoutine = RoutineType.Combi; } }
        public bool IsDynamic { get => SelectedRoutine == RoutineType.Dynamic; set { if (value) SelectedRoutine = RoutineType.Dynamic; } }
        public bool IsBalance { get => SelectedRoutine == RoutineType.Balance; set { if (value) SelectedRoutine = RoutineType.Balance; } }

        // === Backend Category selection (ONE shared selection across WP/MP/MxP/WG/MG) ===
        private Category _selectedBackendCategory = Category.WP;
        public Category SelectedBackendCategory
        {
            get => _selectedBackendCategory;
            set
            {
                if (_selectedBackendCategory == value) return;

                // Ask BEFORE changing anything: on "Nie" the old category stays selected
                if (!ConfirmClearExerciseSelections(ChangeSelectionMessage, ChangeSelectionTitle))
                {
                    RestoreSelectionInUi(nameof(SelectedBackendCategory),
                        nameof(IsWP), nameof(IsMP), nameof(IsMxP), nameof(IsWG), nameof(IsMG));
                    return;
                }

                ApplyCategory(value);
                UpdatePdfCategoryFields();
                ResetExerciseSlots();
            }
        }

        private void ApplyCategory(Category value)
        {
            _selectedBackendCategory = value;
            OnPropertyChanged(nameof(SelectedBackendCategory));

            OtherCatLabel = value.ToString();

            OnPropertyChanged(nameof(IsWP));
            OnPropertyChanged(nameof(IsMP));
            OnPropertyChanged(nameof(IsMxP));
            OnPropertyChanged(nameof(IsWG));
            OnPropertyChanged(nameof(IsMG));
        }

        // Categories offered by the selected age group (e.g. Do 8 rokov: pairs only)
        public bool IsWPEnabled => CurrentAgeGroup.IsCategoryAllowed(Category.WP);
        public bool IsMPEnabled => CurrentAgeGroup.IsCategoryAllowed(Category.MP);
        public bool IsMxPEnabled => CurrentAgeGroup.IsCategoryAllowed(Category.MxP);
        public bool IsWGEnabled => CurrentAgeGroup.IsCategoryAllowed(Category.WG);
        public bool IsMGEnabled => CurrentAgeGroup.IsCategoryAllowed(Category.MG);

        public bool IsWP { get => SelectedBackendCategory == Category.WP; set { if (value) SelectedBackendCategory = Category.WP; } }
        public bool IsMP { get => SelectedBackendCategory == Category.MP; set { if (value) SelectedBackendCategory = Category.MP; } }
        public bool IsMxP { get => SelectedBackendCategory == Category.MxP; set { if (value) SelectedBackendCategory = Category.MxP; } }
        public bool IsWG { get => SelectedBackendCategory == Category.WG; set { if (value) SelectedBackendCategory = Category.WG; } }
        public bool IsMG { get => SelectedBackendCategory == Category.MG; set { if (value) SelectedBackendCategory = Category.MG; } }

        // === Main slots / Okno (slotIndex 0..SlotCount-1, 12 for Do 10 / Do 14) ===
        public ObservableCollection<ExerciseCardVm> ExerciseSlots { get; } = new();

        // === Common header fields (mapped into backend FormData) ===
        public string EventName
        {
            get => FormData.EventName ?? "";
            set
            {
                if (FormData.EventName == value) return;
                FormData.EventName = value;
                OnPropertyChanged();
            }
        }

        public string TrainerName
        {
            get => FormData.TrainerName ?? "";
            set { FormData.TrainerName = value; OnPropertyChanged(); }
        }

        public string TrainerContact
        {
            get => FormData.TrainerContact ?? "";
            set
            {
                if (FormData.TrainerContact == value) return;
                FormData.TrainerContact = value;
                OnPropertyChanged();
            }
        }

        public string ClubName
        {
            get => FormData.ClubName ?? "";
            set { FormData.ClubName = value; OnPropertyChanged(); }
        }

        // === Athlete fields (only the birth year is entered and printed, issue #28) ===
        // First item is empty so a chosen year can be cleared again.
        public ObservableCollection<string> BirthYearOptions { get; } = new(
            new[] { "" }.Concat(
                Enumerable.Range(0, 61).Select(i => (DateTime.Now.Year - i).ToString(CultureInfo.InvariantCulture))));

        public string TopName
        {
            get => FormData.AthleteTopName ?? "";
            set
            {
                if (FormData.AthleteTopName == value) return;
                FormData.AthleteTopName = value;
                OnPropertyChanged();
                RefreshPerformerTexts();
            }
        }

        public string TopBirthYear
        {
            get => FormData.AthleteTopDob ?? "";
            set
            {
                if (FormData.AthleteTopDob == value) return;
                FormData.AthleteTopDob = value;
                OnPropertyChanged();
            }
        }

        public string Middle1Name
        {
            get => FormData.AthleteMiddle1Name ?? "";
            set
            {
                if (FormData.AthleteMiddle1Name == value) return;
                FormData.AthleteMiddle1Name = value;
                OnPropertyChanged();
                RefreshPerformerTexts();
            }
        }

        public string Middle1BirthYear
        {
            get => FormData.AthleteMiddle1Dob ?? "";
            set
            {
                if (FormData.AthleteMiddle1Dob == value) return;
                FormData.AthleteMiddle1Dob = value;
                OnPropertyChanged();
            }
        }

        public string Middle2Name
        {
            get => FormData.AthleteMiddle2Name ?? "";
            set
            {
                if (FormData.AthleteMiddle2Name == value) return;
                FormData.AthleteMiddle2Name = value;
                OnPropertyChanged();
                RefreshPerformerTexts();
            }
        }

        public string Middle2BirthYear
        {
            get => FormData.AthleteMiddle2Dob ?? "";
            set
            {
                if (FormData.AthleteMiddle2Dob == value) return;
                FormData.AthleteMiddle2Dob = value;
                OnPropertyChanged();
            }
        }

        public string BaseName
        {
            get => FormData.AthleteBaseName ?? "";
            set
            {
                if (FormData.AthleteBaseName == value) return;
                FormData.AthleteBaseName = value;
                OnPropertyChanged();
                RefreshPerformerTexts();
            }
        }

        public string BaseBirthYear
        {
            get => FormData.AthleteBaseDob ?? "";
            set
            {
                if (FormData.AthleteBaseDob == value) return;
                FormData.AthleteBaseDob = value;
                OnPropertyChanged();
            }
        }

        // === Commands ===
        public ICommand OpenSlotCommand { get; }
        public ICommand SeparateSlotCommand { get; }
        public ICommand ShowPreviousExerciseCommand { get; }
        public ICommand ShowNextExerciseCommand { get; }
        public ICommand OpenHistoryCommand { get; }
        public ICommand ExitCommand { get; }


        // === Summary info row ===
        private decimal _obtaznost = 0.00m;
        public decimal Obtaznost
        {
            get => _obtaznost;
            set
            {
                if (!SetProperty(ref _obtaznost, value)) return;
            }
        }

        private int _otherCatCount = 0;
        public int OtherCatCount
        {
            get => _otherCatCount;
            set
            {
                if (!SetProperty(ref _otherCatCount, value)) return;
            }
        }

        private string _otherCatLabel = Category.WP.ToString();
        public string OtherCatLabel
        {
            get => _otherCatLabel;
            set
            {
                if (!SetProperty(ref _otherCatLabel, value)) return;
            }
        }

        // "WP: 2/6   Inv: 1/6" — Individuálna zostava has only the one table: "Inv: 3/8"
        public string PickCounterText => CurrentAgeGroup.HasTeamTables
            ? $"{OtherCatLabel}: {OtherCatCount}/{TeamPicks}   Inv: {InvCount}/{InvPicks}"
            : $"Inv: {InvCount}/{InvPicks}";

        private int _invCount = 0;
        public int InvCount
        {
            get => _invCount;
            set
            {
                if (!SetProperty(ref _invCount, value)) return;
            }
        }

        public MainWindowViewModel()
        {
            // Locate DB root (must contain an age-group folder like do10_db)
            var dbRoot = ExerciseImageRepository.ResolveDbRoot();
            _repo = new ExerciseImageRepository(dbRoot);

            // Database problems (e.g. a value in index.csv that differs from the table) go to the trace log
            foreach (var problem in _repo.Problems)
                System.Diagnostics.Trace.TraceWarning("DataBase: " + problem);

            //Printing
            OpenSlotCommand = new RelayCommand(OpenSlot);
            SeparateSlotCommand = new RelayCommand(OpenSlotSeparate);
            ShowPreviousExerciseCommand = new RelayCommand(p => CycleSeparateExercise(p, -1));
            ShowNextExerciseCommand = new RelayCommand(p => CycleSeparateExercise(p, +1));
            OpenHistoryCommand = new RelayCommand(OpenHistory);
            ExitCommand = new RelayCommand(() => Application.Current.Shutdown());
            ExportPdfCommand = new RelayCommand(() => ExportFilledPdf());
            ClearTableCommand = new RelayCommand(() =>
            {
                if (ConfirmClearExerciseSelections("Naozaj chcete vymazať všetky zvolené cviky?", "Vyčistiť tabuľku"))
                    ResetExerciseSlots();
            });

            ResetExerciseSlots();

            UpdatePdfCategoryFields();
        }

        private void SeedSlots()
        {
            ExerciseSlots.Clear();
            for (int i = 0; i < FormData.SlotCount; i++)
            {
                ExerciseSlots.Add(new ExerciseCardVm
                {
                    SlotIndex = i,
                    Label = $"Okno {i + 1}",
                    Image = _placeholderImage,
                    CanSeparate = CurrentAgeGroup.HasTeamTables
                });
            }
        }

        private void OpenSlot(object? parameter)
        {
            if (parameter is not ExerciseCardVm slot)
                return;

            // Filled Okno in a team age group: one exercise again, Zvlášť, or keep it (decision D8)
            if (!string.IsNullOrWhiteSpace(slot.ImagePath) && CurrentAgeGroup.HasTeamTables)
            {
                var dialog = new Views.SlotChangeDialog(slot.SlotIndex + 1, CanSeparateSlot(slot.SlotIndex))
                {
                    Owner = Application.Current?.MainWindow
                };
                dialog.ShowDialog();

                if (dialog.Choice == Views.SlotChangeChoice.Separate)
                {
                    OpenSlotSeparate(slot);
                    return;
                }

                if (dialog.Choice != Views.SlotChangeChoice.Single)
                    return;
            }

            GetTableRule(slot, out var forceIndividualTable, out var lockTableToggle);

            var chosen = PickExercise(slot.SlotIndex, forceIndividualTable, lockTableToggle);
            if (chosen?.ImagePath is null)
                return;

            FormData.SetSlot(
                slot.SlotIndex,
                chosen.Rulebook,
                chosen.Category,
                chosen.X,
                chosen.Y,
                chosen.ImagePath);

            ApplySlotToCard(slot);
            RecalculateExerciseSummary();
        }

        // === Zvlášť: one exercise per team member in one Okno (issue #29) ===
        // The picker opens once per member (Top -> Base, issue #46), always on the Inv table (issue #43).
        // The first member chooses the row, the others can only choose from that row (the same exercise is allowed).
        // Closing the picker at any step cancels the whole Okno (assumption A7).
        private void OpenSlotSeparate(object? parameter)
        {
            if (parameter is not ExerciseCardVm slot || !CanSeparateSlot(slot.SlotIndex))
                return;

            var members = GetTeamMembers();

            ExerciseCardVm? first = null;
            var picks = new List<(int X, string ImagePath, int MemberIndex, string? Position)>(members.Count);

            for (int k = 0; k < members.Count; k++)
            {
                var subtitle = $"{GetPickerName(members[k], k)} ({members[k].Role}) – {k + 1}/{members.Count}";

                var chosen = PickExercise(slot.SlotIndex, startWithIndividualTable: true, lockTableToggle: true,
                                          onlyRow: first?.Y, subtitle: subtitle);

                if (chosen?.ImagePath is null)
                    return;

                first ??= chosen;
                picks.Add((chosen.X, chosen.ImagePath, k, members[k].Position));
            }

            FormData.SetSlot(slot.SlotIndex, first!.Rulebook, first.Category, first.Y, picks);

            ApplySlotToCard(slot);
            RecalculateExerciseSummary();
        }

        // Zvlášť uses an Inv pick (issue #43): possible in team age groups while one is left, not counting this Okno
        private bool CanSeparateSlot(int slotIndex)
            => CurrentAgeGroup.HasTeamTables
               && CurrentAgeGroup.InvTable != null
               && CountInvExercisesExcludingSlot(slotIndex) < InvPicks;

        // 6 + 6 rule (#1, #11): an empty Okno opens the table that still needs exercises
        private void GetTableRule(ExerciseCardVm slot, out bool forceIndividualTable, out bool lockTableToggle)
        {
            var invCategoryCount = CountInvExercisesExcludingSlot(slot.SlotIndex);
            var otherCategoryCount = CountOtherCategoryExercisesExcludingSlot(slot.SlotIndex);

            var isUnsetSlot = string.IsNullOrWhiteSpace(slot.ImagePath);

            forceIndividualTable = false;
            lockTableToggle = false;

            if (isUnsetSlot)
            {
                if (otherCategoryCount >= TeamPicks && invCategoryCount < InvPicks)
                {
                    forceIndividualTable = true;
                    lockTableToggle = true;
                }
                else if (invCategoryCount >= InvPicks && otherCategoryCount < TeamPicks)
                {
                    forceIndividualTable = false;
                    lockTableToggle = true;
                }
            }
        }

        // Shows the picker; returns the clicked tile or null when closed with "Zavrieť"
        private ExerciseCardVm? PickExercise(int slotIndex, bool startWithIndividualTable, bool lockTableToggle,
                                             int? onlyRow = null, string? subtitle = null)
        {
            var gridVm = new ExerciseGridViewModel(
                _repo,
                SelectedRulebook,
                SelectedBackendCategory,
                slotIndex,
                _placeholderImage,
                GetBlockedInvRowsExcludingSlot(slotIndex),
                GetBlockedOtherCategoryRowsExcludingSlot(slotIndex),
                startWithIndividualTable: startWithIndividualTable,
                lockTableToggle: lockTableToggle,
                onlyRow: onlyRow,
                subtitle: subtitle);

            ExerciseCardVm? chosen = null;
            gridVm.ExerciseSelected += ex => chosen = ex;

            var win = new ExerciseGridWindow
            {
                DataContext = gridVm,
                Owner = Application.Current?.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            gridVm.RequestClose += () =>
            {
                win.DialogResult = true;
                win.Close();
            };

            win.ShowDialog();
            return chosen;
        }

        // Copies FormData's slot into the tile (FormData is what gets printed, the tile is what is shown)
        private void ApplySlotToCard(ExerciseCardVm card)
        {
            var s = FormData.Slots[card.SlotIndex];
            if (s is null)
                return;

            card.ImagePath = s.ImagePath;
            card.X = s.X;
            card.Y = s.Y;
            card.Rulebook = s.Rulebook;
            card.Category = s.Category;

            card.SeparateImages = s.Exercises
                .Select(e => WpfImageLoader.Load(e.ImagePath) ?? _placeholderImage)
                .ToList();
            card.ShownExerciseIndex = 0;
            card.Image = card.SeparateImages[0];

            card.IsSeparate = s.IsSeparate;
            card.CanSeparate = false;
            card.Label = BuildSlotLabel(card.SlotIndex);
            UpdatePerformerText(card);
        }

        // ‹ › on a Zvlášť Okno: show the previous / next member's exercise (wraps around)
        private void CycleSeparateExercise(object? parameter, int step)
        {
            if (parameter is not ExerciseCardVm card || !card.IsSeparate || card.SeparateImages.Count == 0)
                return;

            var count = card.SeparateImages.Count;
            card.ShownExerciseIndex = ((card.ShownExerciseIndex + step) % count + count) % count;
            card.Image = card.SeparateImages[card.ShownExerciseIndex];
            UpdatePerformerText(card);
        }

        // === Team members for Zvlášť ===
        // Role = shown in the app; Position = printed in the exercise ID (R1.Top, decision D13)
        private sealed record TeamMember(string Role, string Name, string Position, string BirthYear);

        // The athlete cards the team uses, in order Top -> Base (issue #46): pair = Top, Base;
        // trio = Top, Middle (1st card), Base; quartet = Top, Middle (1st card), Middle (2nd card), Base —
        // the two Middles print as Middle1 / Middle2 (A11)
        private IReadOnlyList<TeamMember> GetTeamMembers() => SelectedBackendCategory switch
        {
            Category.WG => new[]
            {
                new TeamMember("Top", TopName, "Top", TopBirthYear),
                new TeamMember("Middle", Middle1Name, "Middle", Middle1BirthYear),
                new TeamMember("Base", BaseName, "Base", BaseBirthYear)
            },
            Category.MG => new[]
            {
                new TeamMember("Top", TopName, "Top", TopBirthYear),
                new TeamMember("Middle", Middle1Name, "Middle1", Middle1BirthYear),
                new TeamMember("Middle", Middle2Name, "Middle2", Middle2BirthYear),
                new TeamMember("Base", BaseName, "Base", BaseBirthYear)
            },
            _ => new[]
            {
                new TeamMember("Top", TopName, "Top", TopBirthYear),
                new TeamMember("Base", BaseName, "Base", BaseBirthYear)
            }
        };

        // A name counts only with at least 3 characters (issue #29)
        private static bool HasValidName(TeamMember member) => member.Name.Trim().Length >= 3;

        // Picker header: the name, or "Pretekár 1", "Pretekár 2", ...
        private static string GetPickerName(TeamMember member, int index)
            => HasValidName(member) ? member.Name.Trim() : $"Pretekár {index + 1}";

        // Okno overlay: the name, or just the member number (issue #29), plus the role
        private void UpdatePerformerText(ExerciseCardVm card)
        {
            var s = FormData.Slots[card.SlotIndex];
            if (s is null || !s.IsSeparate)
            {
                card.PerformerText = "";
                return;
            }

            var memberIndex = s.Exercises[card.ShownExerciseIndex].MemberIndex;
            var members = GetTeamMembers();
            if (memberIndex < 0 || memberIndex >= members.Count)
            {
                card.PerformerText = "";
                return;
            }

            var member = members[memberIndex];
            var name = HasValidName(member) ? member.Name.Trim() : (memberIndex + 1).ToString(CultureInfo.InvariantCulture);
            card.PerformerText = $"{name} ({member.Role})";
        }

        // Names are not stored in the Okno, so a changed name shows up immediately
        private void RefreshPerformerTexts()
        {
            foreach (var card in ExerciseSlots)
                UpdatePerformerText(card);
        }

        private void ExportFilledPdf()
        {
            var selectedExerciseCount = FormData.Slots.Count(s => s != null);

            if (selectedExerciseCount < FormData.SlotCount)
            {
                MessageBox.Show(
                    $"Na vytlačenie Súťažného plánu musíte zvoliť {FormData.SlotCount} cvičení.",
                    "Chýbajúce cvičenia",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            if (!ConfirmAgeGroupOrPrintAnyway())
                return;

            try
            {
                var templatePdfPath = GetTemplatePdfPath();

                var suggestedBaseName = BuildSafeFileName(
                    string.IsNullOrWhiteSpace(FormData.EventName)
                        ? "sutazny_plan"
                        : FormData.EventName);

                var saveDialog = new SaveFileDialog
                {
                    Title = "Uložiť vyplnený PDF plán",
                    Filter = "PDF súbor (*.pdf)|*.pdf",
                    DefaultExt = ".pdf",
                    AddExtension = true,
                    FileName = $"{suggestedBaseName}_{DateTime.Now:yyyy-MM-dd}.pdf"
                };

                if (saveDialog.ShowDialog() != true)
                    return;

                TrainingPlanPdfIText.ExportFromFormData(
                    templatePdfPath,
                    saveDialog.FileName,
                    FormData,
                    new CultureInfo("sk-SK"));

                // Only a plan that was really printed goes into the history (issue #45); never blocks printing
                _history.Save(BuildHistoryEntry());

                MessageBox.Show(
                    $"PDF bolo úspešne vytvorené:\n{saveDialog.FileName}",
                    "Export hotový",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Nepodarilo sa vytvoriť PDF.\n\n{ex.Message}",
                    "Chyba pri exporte",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        // === Age group check (issue #28) ===
        // Age counts by year: an athlete who turns N during the competition year counts as N,
        // even if the competition is before the birthday. Not a hard block, only a warning.
        // Limits come from AgeGroupDefinition.MinAge / MaxAge (Do 10: max 9 — turning 10 already means the higher
        // category; Od 8 – 14: 8..13; Individuálna zostava: none). Only the cards the team uses are checked.
        private bool ConfirmAgeGroupOrPrintAnyway()
        {
            var minAge = CurrentAgeGroup.MinAge;
            var maxAge = CurrentAgeGroup.MaxAge;
            if (minAge is null && maxAge is null)
                return true;

            var competitionYear = DateTime.Now.Year;

            // Top first, like the cards on screen
            var athletes = GetTeamMembers()
                .Select(m => (role: m.Role, name: m.Name, birthYear: m.BirthYear,
                              age: int.TryParse(m.BirthYear, NumberStyles.Integer, CultureInfo.InvariantCulture, out var year)
                                  ? competitionYear - year
                                  : (int?)null))
                .Where(a => a.age.HasValue)
                .ToList();

            static string Describe((string role, string name, string birthYear, int? age) a)
                => string.IsNullOrWhiteSpace(a.name)
                    ? $"{a.role} ({a.birthYear})"
                    : $"{a.name} – {a.role} ({a.birthYear})";

            var tooOld = athletes.Where(a => maxAge.HasValue && a.age > maxAge).Select(Describe).ToList();
            var tooYoung = athletes.Where(a => minAge.HasValue && a.age < minAge).Select(Describe).ToList();

            if (tooOld.Count == 0 && tooYoung.Count == 0)
                return true;

            var message = $"Rok narodenia týchto pretekárov nezodpovedá vekovej skupine {SelectedRulebook.ToSlovakLabel()}:";

            if (tooOld.Count > 0)
                message += "\n\n" + string.Join("\n", tooOld) +
                           $"\n\nV roku {competitionYear} dovŕšia {maxAge + 1} alebo viac rokov.";

            if (tooYoung.Count > 0)
                message += "\n\n" + string.Join("\n", tooYoung) +
                           $"\n\nV roku {competitionYear} ešte nedovŕšia {minAge} rokov.";

            var dialog = new Views.AgeWarningDialog(message)
            {
                Owner = Application.Current?.MainWindow
            };

            return dialog.ShowDialog() == true;
        }

        // === Súťažný plán history (issue #45) ===
        // Every printed plan is saved; the history window loads one back or removes it.

        private const string ExerciseGoneReason = "cvičenie už v databáze nie je";
        private const string InvalidSlotReason = "uložené údaje nie sú platné";

        private void OpenHistory()
        {
            var historyVm = new PlanHistoryViewModel(_history, ConfirmReplaceCurrentPlan);
            var win = new Views.PlanHistoryWindow
            {
                DataContext = historyVm,
                Owner = Application.Current?.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            historyVm.RequestClose += () => win.DialogResult = true;

            win.ShowDialog();

            if (historyVm.ChosenEntry != null)
                LoadPlan(historyVm.ChosenEntry);
        }

        private PlanHistoryEntry BuildHistoryEntry() => new()
        {
            SavedAt = DateTimeOffset.Now,
            EventName = EventName,
            TrainerName = TrainerName,
            TrainerContact = TrainerContact,
            ClubName = ClubName,
            Rulebook = SelectedRulebook.ToString(),
            Category = SelectedBackendCategory.ToString(),
            Routine = SelectedRoutine.ToString(),
            Athletes = new PlanHistoryAthletes
            {
                Top = new PlanHistoryAthlete { Name = TopName, BirthYear = TopBirthYear },
                Middle1 = new PlanHistoryAthlete { Name = Middle1Name, BirthYear = Middle1BirthYear },
                Middle2 = new PlanHistoryAthlete { Name = Middle2Name, BirthYear = Middle2BirthYear },
                Base = new PlanHistoryAthlete { Name = BaseName, BirthYear = BaseBirthYear }
            },
            Slots = FormData.Slots.OfType<ExerciseSlotSelection>().Select(s => new PlanHistorySlot
            {
                Okno = s.SlotIndex + 1,
                Table = s.Category.ToString(),
                Row = s.Y,
                Exercises = s.Exercises.Select(e => new PlanHistoryExercise
                {
                    Col = e.X,
                    Member = e.MemberIndex >= 0 ? e.MemberIndex : null,
                    Position = e.Position
                }).ToList()
            }).ToList()
        };

        /// <summary>
        /// Age group, category and routine of a saved plan; false when the plan can't be loaded
        /// (unknown age group or category, or written by a newer version of the app).
        /// </summary>
        public static bool TryReadSelection(PlanHistoryEntry entry, out Rulebook rulebook, out Category category,
                                            out RoutineType routine)
        {
            category = Category.WP;
            routine = RoutineType.Balance;

            if (entry.FormatVersion > PlanHistoryEntry.CurrentFormatVersion
                || !TryParseName(entry.Rulebook, out rulebook))
            {
                rulebook = default;
                return false;
            }

            var savedRulebook = rulebook;
            var group = AgeGroups.All.FirstOrDefault(g => g.Rulebook == savedRulebook);
            if (group is null)
                return false;

            if (group.HasTeamTables)
                return TryParseName(entry.Category, out category) && group.IsCategoryAllowed(category)
                       && TryParseName(entry.Routine, out routine);

            // Individuálna zostava: no category, fixed discipline — keep the stored radios if they are readable
            if (!TryParseName(entry.Category, out category) || category == Category.Inv)
                category = Category.WP;
            if (!TryParseName(entry.Routine, out routine))
                routine = RoutineType.Balance;
            return true;
        }

        // Enum values are stored by name only (a number like "1" is not accepted)
        private static bool TryParseName<TEnum>(string? name, out TEnum value) where TEnum : struct, Enum
        {
            value = default;
            return name != null && Enum.GetNames<TEnum>().Contains(name) && Enum.TryParse(name, out value);
        }

        private bool ConfirmReplaceCurrentPlan()
        {
            var hasData = HasAnySelectedExercise()
                          || new[] { TopName, Middle1Name, Middle2Name, BaseName }.Any(n => !string.IsNullOrWhiteSpace(n));
            if (!hasData)
                return true;

            var result = MessageBox.Show("Načítať plán? Aktuálne údaje sa nahradia.", "Načítať plán",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            return result == MessageBoxResult.Yes;
        }

        // Sets everything without the "clear all Okno?" questions of the setters (the user confirmed once).
        // Each Okno goes through FormData.SetSlot + ApplySlotToCard like a picked one, so both stay in sync.
        private void LoadPlan(PlanHistoryEntry entry)
        {
            if (!TryReadSelection(entry, out var rulebook, out var category, out var routine))
                return;

            ApplyRulebook(rulebook);
            ApplyCategory(category);
            SelectedRoutine = routine;
            UpdatePdfCategoryFields();

            EventName = entry.EventName ?? "";
            TrainerName = entry.TrainerName ?? "";
            TrainerContact = entry.TrainerContact ?? "";
            ClubName = entry.ClubName ?? "";

            var athletes = entry.Athletes;
            TopName = athletes?.Top?.Name ?? "";
            TopBirthYear = athletes?.Top?.BirthYear ?? "";
            Middle1Name = athletes?.Middle1?.Name ?? "";
            Middle1BirthYear = athletes?.Middle1?.BirthYear ?? "";
            Middle2Name = athletes?.Middle2?.Name ?? "";
            Middle2BirthYear = athletes?.Middle2?.BirthYear ?? "";
            BaseName = athletes?.Base?.Name ?? "";
            BaseBirthYear = athletes?.Base?.BirthYear ?? "";

            ResetExerciseSlots();

            var problems = new List<string>();
            foreach (var saved in (entry.Slots ?? new List<PlanHistorySlot>()).Where(s => s != null).OrderBy(s => s.Okno))
            {
                var reason = RestoreSlot(saved);
                if (reason != null)
                    problems.Add($"Okno {saved.Okno} sa nepodarilo načítať — {reason}.");
            }

            foreach (var card in ExerciseSlots)
                ApplySlotToCard(card);
            RecalculateExerciseSummary();

            if (problems.Count > 0)
                MessageBox.Show(string.Join("\n", problems), "Načítanie plánu",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        // Puts one saved Okno into FormData; returns why it could not be restored, or null.
        // The same rules as when picking apply (team / Inv limits, one Okno per row, Zvlášť only on Inv).
        private string? RestoreSlot(PlanHistorySlot saved)
        {
            var index = saved.Okno - 1;
            if (index < 0 || index >= FormData.SlotCount || FormData.Slots[index] != null)
                return InvalidSlotReason;

            if (!TryParseName(saved.Table, out Category table))
                return InvalidSlotReason;

            var group = CurrentAgeGroup;
            var isInv = table == Category.Inv;
            if (isInv ? group.InvTable is null : !group.HasTeamTables || table != SelectedBackendCategory)
                return InvalidSlotReason;

            // 6 + 6 rule (#1, #11) and one Okno per row (#15)
            var sameTable = FormData.Slots.OfType<ExerciseSlotSelection>()
                .Where(s => (s.Category == Category.Inv) == isInv)
                .ToList();
            if (sameTable.Count >= (isInv ? InvPicks : TeamPicks) || sameTable.Any(s => s.Y == saved.Row))
                return InvalidSlotReason;

            var exercises = saved.Exercises?.Where(e => e != null).ToList() ?? new List<PlanHistoryExercise>();
            var picks = new List<(int X, string ImagePath, int MemberIndex, string? Position)>(exercises.Count);

            if (exercises.Count == 1)
            {
                var path = GetRestorableImagePath(table, saved.Row, exercises[0].Col);
                if (path is null)
                    return ExerciseGoneReason;

                picks.Add((exercises[0].Col, path, -1, null));
            }
            else
            {
                // Zvlášť: Inv table only (#43), one exercise per team member in Top -> Base order (#46)
                var members = GetTeamMembers();
                if (!isInv || !group.HasTeamTables || exercises.Count != members.Count)
                    return InvalidSlotReason;

                for (int k = 0; k < members.Count; k++)
                {
                    if ((exercises[k].Member ?? k) != k)
                        return InvalidSlotReason;

                    var path = GetRestorableImagePath(table, saved.Row, exercises[k].Col);
                    if (path is null)
                        return ExerciseGoneReason;

                    picks.Add((exercises[k].Col, path, k, members[k].Position));
                }
            }

            try
            {
                FormData.SetSlot(index, SelectedRulebook, table, saved.Row, picks);
                return null;
            }
            catch (ArgumentException)
            {
                return InvalidSlotReason;
            }
        }

        // Only positions that exist in the table and have a picture can be picked, so only those are restored
        private string? GetRestorableImagePath(Category table, int row, int col)
            => CurrentAgeGroup.GetTable(table).HasExercise(row, col)
                ? _repo.GetImagePath(SelectedRulebook, table, row, col)
                : null;

        private static string GetTemplatePdfPath()
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Cvicebny-plan_Template.pdf");

            if (!File.Exists(path))
                throw new FileNotFoundException("PDF šablóna sa nenašla.", path);

            return path;
        }

        private static string MapTeamSizeText(Category category) => category switch
        {
            Category.WP or Category.MP or Category.MxP => "DUO",
            Category.WG => "TRIO",
            Category.MG => "QUARTET",
            Category.Inv => "SOLO",
            _ => ""
        };

        private void UpdatePdfCategoryFields()
        {
            FormData.Category2 = SelectedRulebook.ToSlovakLabel();

            if (CurrentAgeGroup.HasTeamTables)
            {
                FormData.Category1 = SelectedBackendCategory.ToString();
                FormData.TeamSizeText = MapTeamSizeText(SelectedBackendCategory);
                FormData.Discipline = SelectedRoutine.ToSlovakLabel();
            }
            else
            {
                // Individuálna zostava (decision D11)
                FormData.Category1 = Category.Inv.ToString();
                FormData.TeamSizeText = MapTeamSizeText(Category.Inv);
                FormData.Discipline = CurrentAgeGroup.Label;
            }

            // Optional, only if something in the UI binds directly to these
            OnPropertyChanged(nameof(FormData));
        }

        private static string BuildSafeFileName(string name)
        {
            var result = name.Trim();

            foreach (var ch in Path.GetInvalidFileNameChars())
                result = result.Replace(ch, '_');

            return string.IsNullOrWhiteSpace(result) ? "sutazny_plan" : result;
        }


        private static ImageSource? LoadPackImage(string packUri)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(packUri, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        private string BuildSlotLabel(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= ExerciseSlots.Count || FormData.Slots[slotIndex] is null)
                return $"Okno {slotIndex + 1}";

            var difficulty = FormData.GetSlotDifficulty(slotIndex).ToString("0.000", CultureInfo.InvariantCulture);
            var exerciseId = FormData.GetSlotExerciseId(slotIndex);
            var sourceCategory = ExerciseSlots[slotIndex].Category;

            if (sourceCategory == Category.Inv && exerciseId.StartsWith("Inv"))
                exerciseId = exerciseId.Substring(3);

            // Zvlášť Okno: difficulty is the average of its exercises
            var suffix = FormData.Slots[slotIndex]!.IsSeparate ? " - Zvlášť" : "";

            return $"{difficulty} - {exerciseId} - {sourceCategory}{suffix}";
        }

        private void RecalculateExerciseSummary()
        {
            InvCount = ExerciseSlots.Count(s =>
                !string.IsNullOrWhiteSpace(s.ImagePath) &&
                s.Category == Category.Inv);

            OtherCatCount = ExerciseSlots.Count(s =>
                !string.IsNullOrWhiteSpace(s.ImagePath) &&
                s.Category != Category.Inv);

            Obtaznost = FormData.OverallDifficulty;

            // The "Zvlášť" button of an empty Okno disappears once all Inv picks are used (issue #43)
            foreach (var card in ExerciseSlots)
                card.CanSeparate = string.IsNullOrWhiteSpace(card.ImagePath) && CanSeparateSlot(card.SlotIndex);

            OnPropertyChanged(nameof(PickCounterText));
        }

        private int CountInvExercisesExcludingSlot(int slotIndex)
        {
            return ExerciseSlots.Count(s =>
                s.SlotIndex != slotIndex &&
                !string.IsNullOrWhiteSpace(s.ImagePath) &&
                s.Category == Category.Inv);
        }

        private int CountOtherCategoryExercisesExcludingSlot(int slotIndex)
        {
            return ExerciseSlots.Count(s =>
                s.SlotIndex != slotIndex &&
                !string.IsNullOrWhiteSpace(s.ImagePath) &&
                s.Category != Category.Inv);
        }

        private HashSet<int> GetBlockedInvRowsExcludingSlot(int slotIndex)
        {
            return ExerciseSlots
                .Where(s =>
                    s.SlotIndex != slotIndex &&
                    !string.IsNullOrWhiteSpace(s.ImagePath) &&
                    s.Category == Category.Inv)
                .Select(s => s.Y)
                .ToHashSet();
        }

        private HashSet<int> GetBlockedOtherCategoryRowsExcludingSlot(int slotIndex)
        {
            return ExerciseSlots
                .Where(s =>
                    s.SlotIndex != slotIndex &&
                    !string.IsNullOrWhiteSpace(s.ImagePath) &&
                    s.Category != Category.Inv)
                .Select(s => s.Y)
                .ToHashSet();
        }
    }
}