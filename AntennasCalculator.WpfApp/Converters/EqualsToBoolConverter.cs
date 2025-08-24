using System;
using System.Globalization;
using System.Windows.Data;

namespace AntennasCalculator.WpfApp.Converters;

/// <summary>
/// Binds RadioButton.IsChecked to a string property by comparing it to ConverterParameter.
/// Convert:   returns true iff value.ToString() == ConverterParameter.ToString()
/// ConvertBack: when checked==true, returns ConverterParameter; otherwise Binding.DoNothing.
/// </summary>
public sealed class EqualsToBoolConverter : IValueConverter
{
	public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (parameter is null) return false;
		var a = value?.ToString() ?? string.Empty;
		var b = parameter.ToString() ?? string.Empty;
		return string.Equals(a, b, StringComparison.Ordinal);
	}

	public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is bool b && b)
			return parameter?.ToString();
		return Binding.DoNothing;
	}
}