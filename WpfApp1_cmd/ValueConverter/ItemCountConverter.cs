using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Markup;
using System.Globalization;

namespace WpfApp1_cmd.ValueConverter
{
	[ValueConversion(typeof(int), typeof(string))]
	[MarkupExtensionReturnType(typeof(ItemCountConverter))]
	internal class ItemCountConverter : MarkupExtension, IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is int itemCount)
			{
				if (itemCount != 0)
				{
					return $"({itemCount} software)";
				}
				else
				{
					return "(No software)";
				}
			}
			return string.Empty ;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return Binding.DoNothing;
		}

		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			return this;
		}
	}
}
