using System.Windows.Media;
using Atletika_SutaznyPlan_Generator.Models;

namespace Atletika_SutaznyPlan_Generator.ViewModels
{
    // Used BOTH for:
    // - 12 main slots (SlotIndex + Image)
    // - 6x5 exercise cells (X,Y + ImagePath)
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

        // ===== Backend metadata for a selected exercise cell =====
        public string? ImagePath { get; set; } // absolute path from ExerciseImageRepository
        public int X { get; set; }             // 1..5 (column)
        public int Y { get; set; }             // 1..6 (row)
        public Rulebook Rulebook { get; set; }
        public Category Category { get; set; }

        // ===== Main window slot metadata =====
        public int SlotIndex { get; set; }     // 0..11
    }
}