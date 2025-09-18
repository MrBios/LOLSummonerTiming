using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Input;

namespace LOLSummonerTiming
{
    // Converts System.Windows.Input.Key to a user-friendly string for binding display.
    public sealed class KeyToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Key k)
            {
                if (k == Key.None)
                    return string.Empty; // show empty when not set

                // Display a nicer name for some keys if needed
                return k.ToString();
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // One-way converter for display only
            return Binding.DoNothing;
        }
    }
}
