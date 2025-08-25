using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace AntennasCalculator.WpfApp.Converters
{
	public sealed class BoolToBrushConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var b = value is bool v && v;
			return b ? Brushes.LimeGreen : Brushes.IndianRed;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}

	public sealed class BoolToStatusConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var b = value is bool v && v;
			return b ? "Present" : "Missing";
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}