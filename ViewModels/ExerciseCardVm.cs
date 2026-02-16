using System.Windows.Media;

namespace Atletika_SutaznyPlan_Generator.ViewModels
{
    public class ExerciseCardVm : ViewModelBase
    {
        private ImageSource? _image;
        public ImageSource? Image
        {
            get => _image;
            set => SetProperty(ref _image, value);
        }

        private string _label = "";
        public string Label
        {
            get => _label;
            set => SetProperty(ref _label, value);
        }
    }
}