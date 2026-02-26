using System.Windows.Media;

namespace Atletika_SutaznyPlan_Generator.ViewModels
{
    /// <summary>
    /// Represents one of the 12 clickable "windows" (tiles) on the main screen.
    /// Later you can map this to your backend exercise groups.
    /// </summary>
    public class ExerciseCategoryVm : ViewModelBase
    {
        private int _id;
        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        private string _title = "";
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        private ImageSource? _image;
        public ImageSource? Image
        {
            get => _image;
            set => SetProperty(ref _image, value);
        }
    }
}
