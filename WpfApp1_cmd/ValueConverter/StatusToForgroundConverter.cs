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
	/// 値(ErrorCode)によってForeground色を変換する
	/// </summary>
	internal class StatusToForgroundConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if( value is ErrorCode code)
			{
				if (code != ErrorCode.OK)
				{
					return Brushes.Red;
				}
				else
				{
					return Brushes.White;
				}
			}
			else if( value is ItemStatus status)
			{
				switch(status)
				{
					case ItemStatus.OK:            return Brushes.White;
					case ItemStatus.NG:            return Brushes.Red;
					case ItemStatus.UNKNOWN:       return Brushes.Yellow;
					case ItemStatus.NOT_SUPPORTED: return Brushes.Red;
					default:                       return Brushes.White;
				}
			}
			else
			{
				return Brushes.White;
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
