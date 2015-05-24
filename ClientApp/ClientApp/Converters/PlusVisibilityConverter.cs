using System;
using System.Globalization;
using System.Windows.Data;

namespace ClientApp
{
    public class PlusVisibliltyConverter : IValueConverter
    {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if((bool)value)
            {
                return 0;
            }
            else
            {
                return 45;
            }
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}