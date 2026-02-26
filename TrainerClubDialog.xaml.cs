using System.Windows;

namespace Atletika_SutaznyPlan_Generator.Views
{
    public partial class TrainerClubDialog : Window
    {
        public string TrainerName { get; set; }
        public string TrainerContact { get; set; }
        public string ClubName { get; set; }

        public TrainerClubDialog(string currentTrainerName, string currentTrainerContact, string currentClubName)
        {
            InitializeComponent();
            TrainerName = currentTrainerName;
            TrainerContact = currentTrainerContact;
            ClubName = currentClubName;
            DataContext = this;

            Loaded += (_, _) => TrainerTextBox.SelectAll();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}