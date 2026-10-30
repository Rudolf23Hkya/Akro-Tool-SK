using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using Atletika_SutaznyPlan_Generator.Models;
using Atletika_SutaznyPlan_Generator.Models.PlanHistory;

namespace Atletika_SutaznyPlan_Generator.ViewModels
{
    // === Súťažný plán history window (issue #45) ===
    // Lists the printed plans newest first; "Načítať" hands the chosen plan to the main window, "Odstrániť" deletes it.
    public class PlanHistoryViewModel : ViewModelBase
    {
        private readonly PlanHistoryStore _store;
        private readonly Func<bool> _confirmReplaceCurrentPlan;

        public ObservableCollection<PlanHistoryItemVm> Items { get; } = new();

        private PlanHistoryItemVm? _selectedItem;
        public PlanHistoryItemVm? SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (!SetProperty(ref _selectedItem, value)) return;
                _loadCommand.RaiseCanExecuteChanged();
                _removeCommand.RaiseCanExecuteChanged();
            }
        }

        public bool IsEmpty => Items.Count == 0;

        private readonly RelayCommand _loadCommand;
        private readonly RelayCommand _removeCommand;
        public ICommand LoadCommand => _loadCommand;
        public ICommand RemoveCommand => _removeCommand;

        // Set by "Načítať"; the main window loads it after this window is closed
        public PlanHistoryEntry? ChosenEntry { get; private set; }

        public event Action? RequestClose;

        public PlanHistoryViewModel(PlanHistoryStore store, Func<bool> confirmReplaceCurrentPlan)
        {
            _store = store;
            _confirmReplaceCurrentPlan = confirmReplaceCurrentPlan;

            _loadCommand = new RelayCommand(Load, () => SelectedItem?.CanLoad == true);
            _removeCommand = new RelayCommand(Remove, () => SelectedItem != null);

            Reload();
        }

        private void Reload()
        {
            Items.Clear();

            PlanHistoryItemVm? newer = null;
            foreach (var plan in _store.LoadAll())
            {
                var item = new PlanHistoryItemVm(plan);

                // Thick line where at least one calendar month lies between two neighbouring plans
                item.IsSeparatorAbove = newer != null && plan.Entry.SavedAt.AddMonths(1) <= newer.Plan.Entry.SavedAt;

                Items.Add(item);
                newer = item;
            }

            SelectedItem = null;
            OnPropertyChanged(nameof(IsEmpty));
        }

        private void Load()
        {
            if (SelectedItem is not { CanLoad: true } item)
                return;

            if (!_confirmReplaceCurrentPlan())
                return;

            ChosenEntry = item.Plan.Entry;
            RequestClose?.Invoke();
        }

        private void Remove()
        {
            if (SelectedItem is not { } item)
                return;

            var result = MessageBox.Show(
                "Naozaj chcete odstrániť tento súťažný plán z histórie?",
                "Odstrániť plán",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            if (!_store.Delete(item.Plan))
                MessageBox.Show(
                    "Súťažný plán sa nepodarilo odstrániť.",
                    "Chyba",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

            Reload();
        }
    }

    // One printed plan in the list: names (Top -> Base, only the cards the category uses), category / age group,
    // date of printing and event name
    public class PlanHistoryItemVm : ViewModelBase
    {
        public StoredPlan Plan { get; }

        public string NamesText { get; }
        public string CategoryText { get; }
        public string DetailsText { get; }

        // Unknown age group / category (or a newer file format): shown greyed, can only be removed
        public bool CanLoad { get; }

        private bool _isSeparatorAbove;
        public bool IsSeparatorAbove
        {
            get => _isSeparatorAbove;
            set => SetProperty(ref _isSeparatorAbove, value);
        }

        public PlanHistoryItemVm(StoredPlan plan)
        {
            Plan = plan;
            var entry = plan.Entry;

            CanLoad = MainWindowViewModel.TryReadSelection(entry, out var rulebook, out var category, out _);

            NamesText = BuildNamesText(entry, CanLoad ? rulebook : null, CanLoad ? category : null);

            if (CanLoad)
            {
                var group = AgeGroups.Get(rulebook);
                CategoryText = group.HasTeamTables ? $"{category} / {group.Label}" : group.Label;
            }
            else
            {
                CategoryText = $"{entry.Category} / {entry.Rulebook} – nedá sa načítať";
            }

            DetailsText = entry.SavedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(entry.EventName))
                DetailsText += " · " + entry.EventName.Trim();
        }

        // Pair: Top · Base; trio: Top · Middle 1 · Base; quartet: all four (empty name = "—").
        // Individuálna zostava (and unreadable plans): the filled cards in the order Top, Middle 1, Middle 2, Base.
        private static string BuildNamesText(PlanHistoryEntry entry, Rulebook? rulebook, Category? category)
        {
            var a = entry.Athletes;
            var top = a?.Top?.Name?.Trim() ?? "";
            var middle1 = a?.Middle1?.Name?.Trim() ?? "";
            var middle2 = a?.Middle2?.Name?.Trim() ?? "";
            var bottom = a?.Base?.Name?.Trim() ?? "";

            string[] names;
            if (rulebook is { } rb && AgeGroups.Get(rb).HasTeamTables)
            {
                names = category switch
                {
                    Category.WG => new[] { top, middle1, bottom },
                    Category.MG => new[] { top, middle1, middle2, bottom },
                    _ => new[] { top, bottom }
                };
            }
            else
            {
                names = new[] { top, middle1, middle2, bottom }.Where(n => n.Length > 0).ToArray();
                if (names.Length == 0)
                    names = new[] { "" };
            }

            return string.Join(" · ", names.Select(n => n.Length > 0 ? n : "—"));
        }
    }
}
