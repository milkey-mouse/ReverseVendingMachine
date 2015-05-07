using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Data;
using System.Windows.Media;

namespace ClientApp
{
    public class BarcodeConverter : IValueConverter
    {
        //W Wide - Black
        //N Narrow - Black
        //w Wide - White
        //n Narrow - White
        Dictionary<char, string> _codes = new Dictionary<char, string>
        {
            {'0',"NnNwWnWnN"},
            {'1',"WnNwNnNnW"},
            {'2',"NnWwNnNnW"},
            {'3',"WnWwNnNnN"},
            {'4',"NnNwWnNnW"},
            {'5',"WnNwWnNnN"},
            {'6',"NnWwWnNnN"},
            {'7',"NnNwNnWnW"},
            {'8',"WnNwNnWnN"},
            {'9',"NnWwNnWnN"},
        };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string code;
            int narrow = 1, wide = 3;
            if (!_codes.TryGetValue((char)value, out code)) return null;

            code += 'n';
            var result = from i in Enumerable.Range(0, code.Length)
                         select new
                         {
                             color = (code[i] == 'W' || code[i] == 'N') ? Brushes.Black : Brushes.Transparent,
                             width = (code[i] == 'n' || code[i] == 'N') ? narrow : wide
                         };
            return result;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}