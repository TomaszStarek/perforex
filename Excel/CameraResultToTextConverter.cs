using System;
using System.Globalization;
using System.Windows.Data;

namespace Wiring
{
    public class CameraResultToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool cameraResult)
                return cameraResult ? "Płyta: DOBRA" : "Płyta: ZŁA";

            return "Płyta: nieznana";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
