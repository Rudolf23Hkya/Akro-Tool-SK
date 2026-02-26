using System;
using System.Windows;
using Atletika_SutaznyPlan_Generator.ViewModels;
using Atletika_SutaznyPlan_Generator.Views;

namespace Atletika_SutaznyPlan_Generator
{
    public partial class MainWindow : Window
    {
        private bool _startupDialogsShown;

        public MainWindow()
        {
            InitializeComponent();
            ContentRendered += MainWindow_ContentRendered;
        }

        private void MainWindow_ContentRendered(object? sender, EventArgs e)
        {
            if (_startupDialogsShown)
                return;

            _startupDialogsShown = true;

            if (DataContext is not MainWindowViewModel vm)
                return;

            if (string.IsNullOrWhiteSpace(vm.EventName))
            {
                ShowEditEventNameDialog();
            }

            if (string.IsNullOrWhiteSpace(vm.TrainerName) ||
                string.IsNullOrWhiteSpace(vm.ClubName))
            {
                ShowEditTrainerClubDialog();
            }
        }

        private void EditEventName_Click(object sender, RoutedEventArgs e)
        {
            ShowEditEventNameDialog();
        }

        private void EditTrainerClub_Click(object sender, RoutedEventArgs e)
        {
            ShowEditTrainerClubDialog();
        }

        private void ShowEditEventNameDialog()
        {
            if (DataContext is not MainWindowViewModel vm)
                return;

            var dlg = new EventNameDialog(vm.EventName)
            {
                Owner = this
            };

            if (dlg.ShowDialog() == true)
            {
                vm.EventName = dlg.EventName?.Trim() ?? "";
            }
        }

        private void ShowEditTrainerClubDialog()
        {
            if (DataContext is not MainWindowViewModel vm)
                return;

            var dlg = new TrainerClubDialog(vm.TrainerName, vm.TrainerContact, vm.ClubName)
            {
                Owner = this
            };

            if (dlg.ShowDialog() == true)
            {
                vm.TrainerName = dlg.TrainerName?.Trim() ?? "";
                vm.TrainerContact = dlg.TrainerContact?.Trim() ?? "";
                vm.ClubName = dlg.ClubName?.Trim() ?? "";
            }
        }
    }
}