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

        public string WindowTitle => $"Cvičenia: {Category} ({Rulebook.ToSlovakLabel()})";

        public ObservableCollection<ExerciseCardVm> Exercises { get; } = new();

        public ICommand ExerciseClickedCommand { get; }

        // notify MainWindowViewModel + close the picker
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
            Exercises.Clear();

            // Backend expects 6 rows x 5 columns
            var cells = _repo.GetTable(Rulebook, Category, rows: 6, cols: 5);

            foreach (var cell in cells)
            {
                var img = WpfImageLoader.Load(cell.ImagePath) ?? _placeholder;

                Exercises.Add(new ExerciseCardVm
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