using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Atletika_SutaznyPlan_Generator.Models;
using Atletika_SutaznyPlan_Generator.Models.PdfPrinting;
using Microsoft.Win32;

namespace Atletika_SutaznyPlan_Generator.ViewModels
{
    public enum RoutineType { Combi, Tempo, Balans }

    public class MainWindowViewModel : ViewModelBase
    {
        private const string PlaceholderImagePath = "assets/sgf_logo1.png";
        // === Placeholder (WPF Resource) ===

        private const string PlaceholderPackUri = "pack://application:,,,/assets/sgf_logo1.png";
        private readonly ImageSource? _placeholderImage = LoadPackImage(PlaceholderPackUri);

        // === Backend objects ===
        private readonly ExerciseImageRepository _repo;
        public TrainingPlanFormData FormData { get; } = new();

        private bool HasAnySelectedExercise()
            => ExerciseSlots.Any(s => !string.IsNullOrWhiteSpace(s.ImagePath));

        // === Rulebook (age category) ===
        public ObservableCollection<Rulebook> Rulebooks { get; } =
            new(Enum.GetValues(typeof(Rulebook)).Cast<Rulebook>());

        private Rulebook _selectedRulebook = Rulebook.DO_10_ROK;
        public Rulebook SelectedRulebook
        {
            get => _selectedRulebook;
            set
            {
                if (!SetProperty(ref _selectedRulebook, value)) return;
                UpdatePdfCategoryFields();
                ClearExerciseSelections();
            }
        }
        private void ClearExerciseSelections()
        {
            if (HasAnySelectedExercise())
            {
                var result = MessageBox.Show(
                    "Zmena kategórie alebo vekovej skupiny vymaže všetkých 12 zvolených cvikov. Pokračovať?",
                    "Potvrdenie zmeny",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                    return;
            }

            for (int i = 0; i < ExerciseSlots.Count; i++)
            {
                var slot = ExerciseSlots[i];
                FormData.ClearSlot(i);
                slot.Image = _placeholderImage;
                slot.ImagePath = null;
                slot.X = 0;
                slot.Y = 0;
                slot.Rulebook = default;
                slot.Category = default;
                slot.Label = $"Okno {i + 1}";
            }

            RecalculateExerciseSummary();
        }

        // === Printing ===
        public ICommand ExportPdfCommand { get; }

        // === Routine (Combi/Tempo/Balans) maps into FormData.Discipline ===
        private RoutineType _selectedRoutine = RoutineType.Balans;
        public RoutineType SelectedRoutine
        {
            get => _selectedRoutine;
            set
            {
                if (!SetProperty(ref _selectedRoutine, value)) return;
                OnPropertyChanged(nameof(IsCombi));
                OnPropertyChanged(nameof(IsTempo));
                OnPropertyChanged(nameof(IsBalans));

                // backend priority: store as string
                FormData.Discipline = _selectedRoutine.ToString();
            }
        }

        public bool IsCombi { get => SelectedRoutine == RoutineType.Combi; set { if (value) SelectedRoutine = RoutineType.Combi; } }
        public bool IsTempo { get => SelectedRoutine == RoutineType.Tempo; set { if (value) SelectedRoutine = RoutineType.Tempo; } }
        public bool IsBalans { get => SelectedRoutine == RoutineType.Balans; set { if (value) SelectedRoutine = RoutineType.Balans; } }

        // === Backend Category selection (ONE shared selection across WP/MP/MxP/WG/MG) ===
        private Category _selectedBackendCategory = Category.WP;
        public Category SelectedBackendCategory
        {
            get => _selectedBackendCategory;
            set
            {
                if (!SetProperty(ref _selectedBackendCategory, value)) return;

                OtherCatLabel = value.ToString();

                OnPropertyChanged(nameof(IsWP));
                OnPropertyChanged(nameof(IsMP));
                OnPropertyChanged(nameof(IsMxP));
                OnPropertyChanged(nameof(IsWG));
                OnPropertyChanged(nameof(IsMG));

                UpdatePdfCategoryFields();
                ClearExerciseSelections();
            }
        }

        public bool IsWP { get => SelectedBackendCategory == Category.WP; set { if (value) SelectedBackendCategory = Category.WP; } }
        public bool IsMP { get => SelectedBackendCategory == Category.MP; set { if (value) SelectedBackendCategory = Category.MP; } }
        public bool IsMxP { get => SelectedBackendCategory == Category.MxP; set { if (value) SelectedBackendCategory = Category.MxP; } }
        public bool IsWG { get => SelectedBackendCategory == Category.WG; set { if (value) SelectedBackendCategory = Category.WG; } }
        public bool IsMG { get => SelectedBackendCategory == Category.MG; set { if (value) SelectedBackendCategory = Category.MG; } }

        // === Main 12 slots (slotIndex 0..11) ===
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

        // === Athlete fields (UI stays DateTime?, backend stays string?) ===
        private static readonly CultureInfo SlovakCulture = new("sk-SK");

        private static DateTime? ParseDob(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            string[] formats = { "d.M.yyyy", "dd.MM.yyyy", "d.M.yy", "dd.MM.yy" };

            return DateTime.TryParseExact(value, formats, SlovakCulture, DateTimeStyles.None, out var parsed)
                ? parsed
                : null;
        }

        private static string? FormatDob(DateTime? value)
            => value?.ToString("d.M.yyyy", SlovakCulture);

        public string TopName
        {
            get => FormData.AthleteTopName ?? "";
            set
            {
                if (FormData.AthleteTopName == value) return;
                FormData.AthleteTopName = value;
                OnPropertyChanged();
            }
        }

        public DateTime? TopBirth
        {
            get => ParseDob(FormData.AthleteTopDob);
            set
            {
                var formatted = FormatDob(value);
                if (FormData.AthleteTopDob == formatted) return;
                FormData.AthleteTopDob = formatted;
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
            }
        }

        public DateTime? Middle1Birth
        {
            get => ParseDob(FormData.AthleteMiddle1Dob);
            set
            {
                var formatted = FormatDob(value);
                if (FormData.AthleteMiddle1Dob == formatted) return;
                FormData.AthleteMiddle1Dob = formatted;
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
            }
        }

        public DateTime? Middle2Birth
        {
            get => ParseDob(FormData.AthleteMiddle2Dob);
            set
            {
                var formatted = FormatDob(value);
                if (FormData.AthleteMiddle2Dob == formatted) return;
                FormData.AthleteMiddle2Dob = formatted;
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
            }
        }

        public DateTime? BaseBirth
        {
            get => ParseDob(FormData.AthleteBaseDob);
            set
            {
                var formatted = FormatDob(value);
                if (FormData.AthleteBaseDob == formatted) return;
                FormData.AthleteBaseDob = formatted;
                OnPropertyChanged();
            }
        }

        // === Commands ===
        public ICommand OpenSlotCommand { get; }
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
            // Locate DB root (must contain do10_db / do14_db)
            var dbRoot = ResolveDbRoot();
            _repo = new ExerciseImageRepository(dbRoot);

            //Printing
            OpenSlotCommand = new RelayCommand(OpenSlot);
            ExitCommand = new RelayCommand(() => Application.Current.Shutdown());
            ExportPdfCommand = new RelayCommand(() => ExportFilledPdf());

            SeedSlots();
            RecalculateExerciseSummary();

            FormData.Discipline = SelectedRoutine.ToString();
            UpdatePdfCategoryFields();
        }

        private void SeedSlots()
        {
            ExerciseSlots.Clear();
            for (int i = 0; i < 12; i++)
            {
                ExerciseSlots.Add(new ExerciseCardVm
                {
                    SlotIndex = i,
                    Label = $"Okno {i + 1}",
                    Image = _placeholderImage
                });
            }
        }

        private void OpenSlot(object? parameter)
        {
            if (parameter is not ExerciseCardVm slot)
                return;

            var blockedInvRows = GetBlockedInvRowsExcludingSlot(slot.SlotIndex);
            var blockedOtherRows = GetBlockedOtherCategoryRowsExcludingSlot(slot.SlotIndex);

            var invCategoryCount = CountInvExercisesExcludingSlot(slot.SlotIndex);
            var otherCategoryCount = CountOtherCategoryExercisesExcludingSlot(slot.SlotIndex);

            var isUnsetSlot = string.IsNullOrWhiteSpace(slot.ImagePath);

            bool forceIndividualTable = false;
            bool lockTableToggle = false;

            if (isUnsetSlot)
            {
                if (otherCategoryCount >= 6 && invCategoryCount < 6)
                {
                    forceIndividualTable = true;
                    lockTableToggle = true;
                }
                else if (invCategoryCount >= 6 && otherCategoryCount < 6)
                {
                    forceIndividualTable = false;
                    lockTableToggle = true;
                }
            }

            var gridVm = new ExerciseGridViewModel(
                _repo,
                SelectedRulebook,
                SelectedBackendCategory,
                slot.SlotIndex,
                _placeholderImage,
                blockedInvRows,
                blockedOtherRows,
                startWithIndividualTable: forceIndividualTable,
                lockTableToggle: lockTableToggle);

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

            if (chosen?.ImagePath is null)
                return;

            FormData.SetSlot(
                slot.SlotIndex,
                chosen.Rulebook,
                chosen.Category,
                chosen.X,
                chosen.Y,
                chosen.ImagePath);

            slot.Image = chosen.Image;
            slot.ImagePath = chosen.ImagePath;
            slot.X = chosen.X;
            slot.Y = chosen.Y;
            slot.Rulebook = chosen.Rulebook;
            slot.Category = chosen.Category;
            slot.Label = BuildSlotLabel(slot.SlotIndex);

            RecalculateExerciseSummary();
        }

        private void ExportFilledPdf()
        {
            var selectedExerciseCount = FormData.Slots.Count(s => s != null);

            if (selectedExerciseCount < 12)
            {
                MessageBox.Show(
                    "Na vytlačenie Súťažného plánu musíte zvoliť 12 cvičení.",
                    "Chýbajúce cvičenia",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

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
            FormData.Category1 = SelectedBackendCategory.ToString();
            FormData.Category2 = SelectedRulebook.ToSlovakLabel();
            FormData.TeamSizeText = MapTeamSizeText(SelectedBackendCategory);

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

        private static string ResolveDbRoot()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);

            for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
            {
                foreach (var folderName in new[] { "DataBase", "Database", "database" })
                {
                    var candidate = Path.Combine(dir.FullName, folderName);

                    if (Directory.Exists(Path.Combine(candidate, "do10_db")) ||
                        Directory.Exists(Path.Combine(candidate, "do14_db")))
                    {
                        return candidate;
                    }
                }
            }

            // fallback (repo will just index nothing)
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        private string BuildSlotLabel(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= ExerciseSlots.Count || FormData.Slots[slotIndex] is null)
                return $"Okno {slotIndex + 1}";

            var difficulty = FormData.GetSlotDifficulty(slotIndex).ToString("0.000", CultureInfo.InvariantCulture);
            var exerciseId = FormData.GetSlotExerciseId(slotIndex);
            var sourceCategory = ExerciseSlots[slotIndex].Category;

            if (sourceCategory == Category.Inv && exerciseId.Length > 3)
                exerciseId = exerciseId.Substring(3);

            return $"{difficulty} - {exerciseId} - {sourceCategory}";
        }

        private void RecalculateExerciseSummary()
        {
            InvCount = ExerciseSlots.Count(s =>
                !string.IsNullOrWhiteSpace(s.ImagePath) &&
                s.Category == Category.Inv);

            OtherCatCount = ExerciseSlots.Count(s =>
                !string.IsNullOrWhiteSpace(s.ImagePath) &&
                s.Category != Category.Inv);

            Obtaznost = FormData.Slots
                .Where(s => s != null)
                .Select(s => FormData.DifficultyRule(s!.Y, s!.X))
                .Aggregate(0m, (acc, d) => acc + d);
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