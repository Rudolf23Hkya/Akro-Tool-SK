using System.Windows;

namespace Atletika_SutaznyPlan_Generator.Views
{
    public enum SlotChangeChoice { Cancel, Single, Separate }

    // Shown when a filled Okno is clicked in a team age group (issue #29, decision D8):
    // redo it as one exercise for the team, as "Zvlášť" (one per team member), or keep it.
    public partial class SlotChangeDialog : Window
    {
        public string Message { get; }

        public SlotChangeChoice Choice { get; private set; } = SlotChangeChoice.Cancel;

        public SlotChangeDialog(int oknoNumber)
        {
            InitializeComponent();
            Message = $"Okno {oknoNumber} je už vyplnené. Ako ho chcete zmeniť?";
            DataContext = this;
        }

        private void Single_Click(object sender, RoutedEventArgs e)
        {
            Choice = SlotChangeChoice.Single;
            DialogResult = true;
        }

        private void Separate_Click(object sender, RoutedEventArgs e)
        {
            Choice = SlotChangeChoice.Separate;
            DialogResult = true;
        }
    }
}
