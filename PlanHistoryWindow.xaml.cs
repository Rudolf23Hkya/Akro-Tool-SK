using System.Windows;
using System.Windows.Input;
using Atletika_SutaznyPlan_Generator.ViewModels;

namespace Atletika_SutaznyPlan_Generator.Views
{
    // Súťažný plán history (issue #45); DataContext = PlanHistoryViewModel
    public partial class PlanHistoryWindow : Window
    {
        public PlanHistoryWindow()
        {
            InitializeComponent();
            WindowSizing.FitToWorkArea(this);
        }

        // Double-click on a plan = "Načítať"
        private void PlanItem_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is PlanHistoryViewModel vm && vm.LoadCommand.CanExecute(null))
                vm.LoadCommand.Execute(null);
        }
    }
}
