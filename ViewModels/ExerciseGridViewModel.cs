using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Atletika_SutaznyPlan_Generator.ViewModels
{
    /// <summary>
    /// ViewModel for the exercise picker window (6x5 grid of clickable images).
    /// Backend hookup can replace SeedDemoExercises later.
    /// </summary>
    public class ExerciseGridViewModel : ViewModelBase
    {
        // Keep this in sync with MainWindowViewModel.PlaceholderImagePath (or refactor to a shared helper later).
        private const string PlaceholderImagePath = "Assets/SGF_logo1.png";
        public ExerciseCategoryVm Category { get; }

        public string WindowTitle => $"Cvičenia: {Category.Title}";

        public ObservableCollection<ExerciseCardVm> Exercises { get; } = new();

        public ICommand ExerciseClickedCommand { get; }

        public ExerciseGridViewModel(ExerciseCategoryVm category)
        {
            Category = category;
            ExerciseClickedCommand = new RelayCommand(ExerciseClicked);

            SeedDemoExercises();
        }

        private void SeedDemoExercises()
        {
            Exercises.Clear();

            // 6 x 5 = 30
            for (int i = 1; i <= 30; i++)
            {
                Exercises.Add(new ExerciseCardVm
                {
                    Label = $"{i:00}",
                    Image = LoadPackImage(PlaceholderImagePath)
                });
            }
        }

        private void ExerciseClicked(object? parameter)
        {
            if (parameter is not ExerciseCardVm ex)
                return;

            MessageBox.Show($"Klik: {Category.Title} / {ex.Label}");
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