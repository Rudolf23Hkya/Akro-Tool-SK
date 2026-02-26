using System;
using System.Globalization;
using System.Windows.Data;
using Atletika_SutaznyPlan_Generator.Models;

namespace Atletika_SutaznyPlan_Generator.Converters
{
    public class RulebookToSlovakLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Rulebook rb ? rb.ToSlovakLabel() : "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}