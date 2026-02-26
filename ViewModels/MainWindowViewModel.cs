using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Atletika_SutaznyPlan_Generator;

namespace Atletika_SutaznyPlan_Generator.ViewModels
{
    public enum RoutineType { Combi, Tempo, Balans }
    public enum FormationType { WP, MP, MxP }
    public enum GroupType { WG, MG }

    public class MainWindowViewModel : ViewModelBase
    {
        // Change this to switch the default placeholder everywhere.
        // Make sure the file exists in your project under /Assets and has Build Action = Resource.
        private const string PlaceholderImagePath = "Assets/SGF_logo1.png";
        // --- Top left: category
        public ObservableCollection<string> Categories { get; } =
            new() { "Kategória 1", "Kategória 2" };

        private string? _selectedCategory;
        public string? SelectedCategory
        {
            get => _selectedCategory;
            set => SetProperty(ref _selectedCategory, value);
        }

        // --- Discipline selections (radio buttons)
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
            }
        }

        private FormationType _selectedFormation = FormationType.MxP;
        public FormationType SelectedFormation
        {
            get => _selectedFormation;
            set
            {
                if (!SetProperty(ref _selectedFormation, value)) return;
                OnPropertyChanged(nameof(IsWP));
                OnPropertyChanged(nameof(IsMP));
                OnPropertyChanged(nameof(IsMxP));
            }
        }

        private GroupType _selectedGroup = GroupType.WG;
        public GroupType SelectedGroup
        {
            get => _selectedGroup;
            set
            {
                if (!SetProperty(ref _selectedGroup, value)) return;
                OnPropertyChanged(nameof(IsWG));
                OnPropertyChanged(nameof(IsMG));
            }
        }

        // Radio-friendly bools
        public bool IsCombi { get => SelectedRoutine == RoutineType.Combi; set { if (value) SelectedRoutine = RoutineType.Combi; } }
        public bool IsTempo { get => SelectedRoutine == RoutineType.Tempo; set { if (value) SelectedRoutine = RoutineType.Tempo; } }
        public bool IsBalans { get => SelectedRoutine == RoutineType.Balans; set { if (value) SelectedRoutine = RoutineType.Balans; } }

        public bool IsWP { get => SelectedFormation == FormationType.WP; set { if (value) SelectedFormation = FormationType.WP; } }
        public bool IsMP { get => SelectedFormation == FormationType.MP; set { if (value) SelectedFormation = FormationType.MP; } }
        public bool IsMxP { get => SelectedFormation == FormationType.MxP; set { if (value) SelectedFormation = FormationType.MxP; } }

        public bool IsWG { get => SelectedGroup == GroupType.WG; set { if (value) SelectedGroup = GroupType.WG; } }
        public bool IsMG { get => SelectedGroup == GroupType.MG; set { if (value) SelectedGroup = GroupType.MG; } }

        // --- Athlete cards (bind to your existing TextBoxes/DatePickers)
        private string _topName = "Tina Muster";
        public string TopName { get => _topName; set => SetProperty(ref _topName, value); }

        private DateTime? _topBirth = new DateTime(2009, 1, 9);
        public DateTime? TopBirth { get => _topBirth; set => SetProperty(ref _topBirth, value); }

        private string _middle1Name = "Maria MAX";
        public string Middle1Name { get => _middle1Name; set => SetProperty(ref _middle1Name, value); }

        private DateTime? _middle1Birth = new DateTime(2005, 1, 20);
        public DateTime? Middle1Birth { get => _middle1Birth; set => SetProperty(ref _middle1Birth, value); }

        private string _middle2Name = "Name";
        public string Middle2Name { get => _middle2Name; set => SetProperty(ref _middle2Name, value); }

        private DateTime? _middle2Birth = new DateTime(2005, 1, 1);
        public DateTime? Middle2Birth { get => _middle2Birth; set => SetProperty(ref _middle2Birth, value); }

        private string _baseName = "abeth Langename";
        public string BaseName { get => _baseName; set => SetProperty(ref _baseName, value); }

        private DateTime? _baseBirth = new DateTime(2005, 2, 11);
        public DateTime? BaseBirth { get => _baseBirth; set => SetProperty(ref _baseBirth, value); }

        // --- Footer
        private string _trainerName = "Anna Malá";
        public string TrainerName { get => _trainerName; set => SetProperty(ref _trainerName, value); }

        private string _clubName = "Rebels Gym";
        public string ClubName { get => _clubName; set => SetProperty(ref _clubName, value); }

        private string _eventName = "December Acro Open Košice";
        public string EventName { get => _eventName; set => SetProperty(ref _eventName, value); }

        // --- Main selector: 12 clickable "windows" (tiles)
        public ObservableCollection<ExerciseCategoryVm> ExerciseCategories { get; } = new();

        // --- Commands (menu + buttons)
        public ICommand LoadPreviousTeamCommand { get; }
        public ICommand OpenCategoryCommand { get; }
        public ICommand ExitCommand { get; }

        public MainWindowViewModel()
        {
            LoadPreviousTeamCommand = new RelayCommand(LoadPreviousTeam);
            OpenCategoryCommand = new RelayCommand(OpenCategory);
            ExitCommand = new RelayCommand(() => Application.Current.Shutdown());

            SeedDemoCategories();
            SelectedCategory = Categories.Count > 0 ? Categories[0] : null;
        }

        private void LoadPreviousTeam()
        {
            // TODO: replace with real load logic
            TopName = "Loaded Top";
            Middle1Name = "Loaded Middle";
            Middle2Name = "Loaded Middle 2";
            BaseName = "Loaded Base";
        }

        private void OpenCategory(object? parameter)
        {
            if (parameter is not ExerciseCategoryVm cat)
                return;

            // For now: open a demo grid window with placeholder exercise buttons.
            // Later you can feed it from your backend.
            var gridVm = new ExerciseGridViewModel(cat);
            var win = new ExerciseGridWindow
            {
                DataContext = gridVm,
                Owner = Application.Current?.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            win.ShowDialog();
        }

        private void SeedDemoCategories()
        {
            ExerciseCategories.Clear();

            // You can rename these to your real 12 exercise "windows" later.
            for (int i = 1; i <= 12; i++)
            {
                ExerciseCategories.Add(new ExerciseCategoryVm
                {
                    Id = i,
                    Title = $"Okno {i}",
                    Image = LoadPackImage(PlaceholderImagePath)
                });
            }
        }

        private static BitmapImage? LoadPackImage(string relativePath)
        {
            try
            {
                // Image file should have Build Action = Resource
                return new BitmapImage(new Uri($"pack://application:,,,/{relativePath}", UriKind.Absolute));
            }
            catch
            {
                return null;
            }
        }
    }
}