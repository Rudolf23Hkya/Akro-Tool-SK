using System.Windows;

namespace Atletika_SutaznyPlan_Generator.Views
{
    // Shown before printing when a birth year does not fit the age group (issue #28).
    // OK closes the dialog without printing; "Tlačiť aj tak" returns true.
    public partial class AgeWarningDialog : Window
    {
        public string Message { get; }

        public AgeWarningDialog(string message)
        {
            InitializeComponent();
            Message = message;
            DataContext = this;
        }

        private void PrintAnyway_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
