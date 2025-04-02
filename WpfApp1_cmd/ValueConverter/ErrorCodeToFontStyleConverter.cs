using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace WpfApp1_cmd.ValueConverter
{
	internal class ErrorCodeToFontStyleConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			if(value is ErrorCode code)
			{
				if (code != ErrorCode.OK)
				{
					return FontStyles.Oblique;
				}
				else
				{
					return FontStyles.Normal;
				}
			}
			else
			{
				return FontStyles.Normal;
			}
		}
		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
