using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace WpfApp1_cmd.ValueConverter
{
	/// <summary>
	///  値によって色を変換する
	/// </summary>
	internal class ErrCodeToColorConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if( value is ErrorCode code)
			{
				if(code != ErrorCode.OK)
				{
					return Brushes.Red;
				}
				else
				{
					return Brushes.Black;
				}
			}
			else
			{
				return Brushes.Black;
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
