using System;
using Atletika_SutaznyPlan_Generator.ViewModels;

namespace Atletika_SutaznyPlan_Generator.ViewModels
{
    public class AthleteVm : ViewModelBase
    {
        private string _role = "";
        public string Role
        {
            get => _role;
            set => SetProperty(ref _role, value);
        }

        private string _name = "";
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private DateTime? _birthDate;
        public DateTime? BirthDate
        {
            get => _birthDate;
            set => SetProperty(ref _birthDate, value);
        }
    }
}