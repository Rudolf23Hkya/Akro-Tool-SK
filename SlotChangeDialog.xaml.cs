using System.Windows;

namespace Atletika_SutaznyPlan_Generator.Views
{
    public enum SlotChangeChoice { Cancel, Single, Separate }

    // Shown when a filled Okno is clicked in a team age group (issue #29, decision D8):
    // redo it as one exercise for the team, as "Zvlášť" (one per team member), or keep it.
    // "Zvlášť" uses an Inv pick (issue #43), so it is disabled when none is left.
    public partial class SlotChangeDialog : Window
    {
        public string Message { get; }

        public bool CanSeparate { get; }

        public string SeparateToolTip => CanSeparate
            ? "Zvoliť individuálne cvičenie zvlášť pre každého pretekára"
            : "Všetky Inv cvičenia sú už zvolené";

        public SlotChangeChoice Choice { get; private set; } = SlotChangeChoice.Cancel;

        public SlotChangeDialog(int oknoNumber, bool canSeparate)
        {
            InitializeComponent();
            Message = $"Okno {oknoNumber} je už vyplnené. Ako ho chcete zmeniť?";
            CanSeparate = canSeparate;
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
