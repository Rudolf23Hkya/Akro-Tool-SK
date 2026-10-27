using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Atletika_SutaznyPlan_Generator.Models;
using System.Collections.Generic;
using System.Linq;

namespace Atletika_SutaznyPlan_Generator.ViewModels
{
    public class ExerciseGridViewModel : ViewModelBase
    {
        private readonly ExerciseImageRepository _repo;
        private readonly ImageSource? _placeholder;

        public Rulebook Rulebook { get; }
        public Category Category { get; private set; }
        public int SlotIndex { get; }

        // null = no team table (Individuálna zostava); null Inv table = age group without Inv
        private readonly ExerciseTable? _teamTable;
        private readonly ExerciseTable? _invTable;

        private readonly HashSet<int> _blockedInvRows;
        private readonly HashSet<int> _blockedOtherRows;
        private readonly bool _lockTableToggle;

        public bool CanToggleIndividual => !_lockTableToggle && IsTableToggleVisible;

        // Only an age group with both tables needs the toggle
        public bool IsTableToggleVisible => _teamTable != null && _invTable != null;

        public ObservableCollection<ExerciseCardVm> Exercises { get; } = new();
        public ObservableCollection<ExerciseCardVm> IndividualExercises { get; } = new();

        public ObservableCollection<ExerciseCardVm> VisibleExercises
            => ShowIndividualTable ? IndividualExercises : Exercises;

        private ExerciseTable VisibleTable => ShowIndividualTable ? _invTable! : _teamTable!;

        // Grid size of the shown table
        public int VisibleRows => VisibleTable.Rows;
        public int VisibleColumns => VisibleTable.Cols;

        private bool _showIndividualTable;
        public bool ShowIndividualTable
        {
            get => _showIndividualTable;
            set
            {
                if (_lockTableToggle && value != _showIndividualTable)
                    return;

                if ((value && _invTable is null) || (!value && _teamTable is null))
                    return;

                if (SetProperty(ref _showIndividualTable, value))
                {
                    OnPropertyChanged(nameof(VisibleExercises));
                    OnPropertyChanged(nameof(VisibleRows));
                    OnPropertyChanged(nameof(VisibleColumns));
                    OnPropertyChanged(nameof(WindowTitle));
                    OnPropertyChanged(nameof(ToggleText));
                }
            }
        }

        public string ToggleText
            => ShowIndividualTable
                ? "Zobrazuje sa: individuálna zostava"
                : "Prepnúť na individuálnu zostavu";

        public string WindowTitle
        {
            get
            {
                var category = ShowIndividualTable ? Atletika_SutaznyPlan_Generator.Models.Category.Inv : Category;
                // Show the table name only when it differs from the category (e.g. "WP – Pár")
                var name = VisibleTable.DisplayName == category.ToString()
                    ? category.ToString()
                    : $"{category} – {VisibleTable.DisplayName}";

                return $"Cvičenia: {name} ({Rulebook.ToSlovakLabel()})";
            }
        }

        public ICommand ExerciseClickedCommand { get; }

        public event Action<ExerciseCardVm>? ExerciseSelected;
        public event Action? RequestClose;

        public ExerciseGridViewModel(
            ExerciseImageRepository repo,
            Rulebook rulebook,
            Category category,
            int slotIndex,
            ImageSource? placeholder,
            IEnumerable<int>? blockedInvRows = null,
            IEnumerable<int>? blockedOtherRows = null,
            bool startWithIndividualTable = false,
            bool lockTableToggle = false)
        {
            _repo = repo;
            Rulebook = rulebook;
            Category = category;
            SlotIndex = slotIndex;
            _placeholder = placeholder;
            _blockedInvRows = blockedInvRows?.ToHashSet() ?? new HashSet<int>();
            _blockedOtherRows = blockedOtherRows?.ToHashSet() ?? new HashSet<int>();
            _lockTableToggle = lockTableToggle;

            var ageGroup = AgeGroups.Get(rulebook);
            _teamTable = ageGroup.IsCategoryAllowed(category) ? ageGroup.GetTable(category) : null;
            _invTable = ageGroup.InvTable;

            _showIndividualTable = _teamTable is null || (startWithIndividualTable && _invTable != null);

            ExerciseClickedCommand = new RelayCommand(ExerciseClicked);

            LoadFromRepository();
        }

        private void LoadFromRepository()
        {
            if (_teamTable != null)
                LoadTableInto(Exercises, Category);
            if (_invTable != null)
                LoadTableInto(IndividualExercises, Atletika_SutaznyPlan_Generator.Models.Category.Inv);

            OnPropertyChanged(nameof(VisibleExercises));
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(ToggleText));
        }

        private void LoadTableInto(ObservableCollection<ExerciseCardVm> target, Category category)
        {
            target.Clear();

            var cells = _repo.GetTable(Rulebook, category);
            var blockedRowsForThisTable = category == Category.Inv
                ? _blockedInvRows
                : _blockedOtherRows;

            foreach (var cell in cells)
            {
                if (cell.IsEmpty)
                {
                    target.Add(new ExerciseCardVm
                    {
                        X = cell.Col,
                        Y = cell.Row,
                        Rulebook = cell.Rulebook,
                        Category = cell.Category,
                        IsEmptyCell = true,
                        IsSelectable = false
                    });
                    continue;
                }

                var img = WpfImageLoader.Load(cell.ImagePath) ?? _placeholder;
                var isBlocked = blockedRowsForThisTable.Contains(cell.Row);

                var card = new ExerciseCardVm
                {
                    Image = img,
                    ImagePath = cell.ImagePath,
                    X = cell.Col,
                    Y = cell.Row,
                    Rulebook = cell.Rulebook,
                    Category = cell.Category,
                    IsSelectable = !isBlocked,
                    IsBlockedByRowRule = isBlocked
                };

                card.Label = BuildExerciseGridLabel(card, cell.Difficulty);
                target.Add(card);
            }
        }

        private void ExerciseClicked(object? parameter)
        {
            if (parameter is not ExerciseCardVm ex)
                return;

            if (!ex.IsSelectable)
                return;

            if (string.IsNullOrWhiteSpace(ex.ImagePath))
            {
                MessageBox.Show("Pre toto políčko sa nenašiel obrázok v databáze.", "Chýba obrázok");
                return;
            }

            ExerciseSelected?.Invoke(ex);
            RequestClose?.Invoke();
        }

        private static string BuildExerciseGridLabel(ExerciseCardVm card, decimal difficulty)
        {
            var difficultyText = difficulty.ToString("0.000", CultureInfo.InvariantCulture);

            var exerciseId = card.Category == Category.Inv
                ? $"InvR{card.Y}"
                : $"R{card.Y}";

            if (card.Category == Category.Inv && exerciseId.StartsWith("Inv"))
                exerciseId = exerciseId[3..];

            return $"{exerciseId} - {difficultyText}";
        }
    }
}
