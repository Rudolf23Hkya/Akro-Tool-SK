using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Atletika_SutaznyPlan_Generator.Models;
using Atletika_SutaznyPlan_Generator.Models.PdfPrinting;

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

        // === Rulebook (age category) ===
        public ObservableCollection<Rulebook> Rulebooks { get; } =
            new(Enum.GetValues(typeof(Rulebook)).Cast<Rulebook>());

        private Rulebook _selectedRulebook = Rulebook.DO_10_ROK;
        public Rulebook SelectedRulebook
        {
            get => _selectedRulebook;
            set => SetProperty(ref _selectedRulebook, value);
        }

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
        private Category _selectedBackendCategory = Category.MxP;
        public Category SelectedBackendCategory
        {
            get => _selectedBackendCategory;
            set
            {
                if (!SetProperty(ref _selectedBackendCategory, value)) return;
                OnPropertyChanged(nameof(IsWP));
                OnPropertyChanged(nameof(IsMP));
                OnPropertyChanged(nameof(IsMxP));
                OnPropertyChanged(nameof(IsWG));
                OnPropertyChanged(nameof(IsMG));
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

        public MainWindowViewModel()
        {
            // Locate DB root (must contain do10_db / do14_db)
            var dbRoot = ResolveDbRoot();
            _repo = new ExerciseImageRepository(dbRoot);

            OpenSlotCommand = new RelayCommand(OpenSlot);
            ExitCommand = new RelayCommand(() => Application.Current.Shutdown());

            SeedSlots();
            FormData.Discipline = SelectedRoutine.ToString();
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

            var gridVm = new ExerciseGridViewModel(
                _repo,
                SelectedRulebook,
                SelectedBackendCategory,
                slot.SlotIndex,
                _placeholderImage);

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

            // Update backend form data + UI tile
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
    }
}