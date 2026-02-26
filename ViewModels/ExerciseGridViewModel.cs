using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Atletika_SutaznyPlan_Generator.Models;

namespace Atletika_SutaznyPlan_Generator.ViewModels
{
    public class ExerciseGridViewModel : ViewModelBase
    {
        private readonly ExerciseImageRepository _repo;
        private readonly ImageSource? _placeholder;

        public Rulebook Rulebook { get; }
        public Category Category { get; }
        public int SlotIndex { get; }

        public ObservableCollection<ExerciseCardVm> Exercises { get; } = new();
        public ObservableCollection<ExerciseCardVm> IndividualExercises { get; } = new();

        public ObservableCollection<ExerciseCardVm> VisibleExercises
            => ShowIndividualTable ? IndividualExercises : Exercises;

        private bool _showIndividualTable;
        public bool ShowIndividualTable
        {
            get => _showIndividualTable;
            set
            {
                if (SetProperty(ref _showIndividualTable, value))
                {
                    OnPropertyChanged(nameof(VisibleExercises));
                    OnPropertyChanged(nameof(WindowTitle));
                    OnPropertyChanged(nameof(ToggleText));
                }
            }
        }

        public bool CanToggleIndividual
            => Category != Atletika_SutaznyPlan_Generator.Models.Category.Inv;

        public string ToggleText
            => ShowIndividualTable
                ? "Zobrazuje sa: individuálna zostava"
                : "Prepnúť na individuálnu zostavu";

        public string WindowTitle
            => $"Cvičenia: {(ShowIndividualTable ? Atletika_SutaznyPlan_Generator.Models.Category.Inv : Category)} ({Rulebook.ToSlovakLabel()})";

        public ICommand ExerciseClickedCommand { get; }

        public event Action<ExerciseCardVm>? ExerciseSelected;
        public event Action? RequestClose;

        public ExerciseGridViewModel(
            ExerciseImageRepository repo,
            Rulebook rulebook,
            Category category,
            int slotIndex,
            ImageSource? placeholder)
        {
            _repo = repo;
            Rulebook = rulebook;
            Category = category;
            SlotIndex = slotIndex;
            _placeholder = placeholder;

            ExerciseClickedCommand = new RelayCommand(ExerciseClicked);

            LoadFromRepository();
        }

        private void LoadFromRepository()
        {
            LoadTableInto(Exercises, Category);
            LoadTableInto(IndividualExercises, Atletika_SutaznyPlan_Generator.Models.Category.Inv);

            OnPropertyChanged(nameof(VisibleExercises));
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(ToggleText));
        }

        private void LoadTableInto(ObservableCollection<ExerciseCardVm> target, Atletika_SutaznyPlan_Generator.Models.Category category)
        {
            target.Clear();

            var cells = _repo.GetTable(Rulebook, category, rows: 6, cols: 5);

            foreach (var cell in cells)
            {
                var img = WpfImageLoader.Load(cell.ImagePath) ?? _placeholder;

                target.Add(new ExerciseCardVm
                {
                    Label = $"{cell.Col:00}-{cell.Row:00}",
                    Image = img,
                    ImagePath = cell.ImagePath,
                    X = cell.Col,
                    Y = cell.Row,
                    Rulebook = cell.Rulebook,
                    Category = cell.Category
                });
            }
        }

        private void ExerciseClicked(object? parameter)
        {
            if (parameter is not ExerciseCardVm ex)
                return;

            if (string.IsNullOrWhiteSpace(ex.ImagePath))
            {
                MessageBox.Show("Pre toto políčko sa nenašiel obrázok v databáze.", "Chýba obrázok");
                return;
            }

            ExerciseSelected?.Invoke(ex);
            RequestClose?.Invoke();
        }
    }
}