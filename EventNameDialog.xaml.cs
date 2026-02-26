using System.Windows;

namespace Atletika_SutaznyPlan_Generator.Views
{
    public partial class EventNameDialog : Window
    {
        public string EventName { get; set; }

        public EventNameDialog(string currentEventName)
        {
            InitializeComponent();
            EventName = currentEventName;
            DataContext = this;
            Loaded += (_, _) => EventNameTextBox.SelectAll();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}