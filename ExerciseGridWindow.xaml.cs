using System.Windows;

namespace Atletika_SutaznyPlan_Generator
{
    public partial class ExerciseGridWindow : Window
    {
        public ExerciseGridWindow()
        {
            InitializeComponent();
            WindowSizing.FitToWorkArea(this);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
