﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using WpfApp1_cmd.Models;

namespace WpfApp1_cmd.ValueConverter
{
	internal class GroupToIsCheckedConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var group = value as ReadOnlyObservableCollection<object>;
			if (group != null)
			{
				var unitVersions = new ReadOnlyObservableCollection<UnitVersion>(
					new ObservableCollection<UnitVersion>(group.Cast<UnitVersion>())
				);
				Debug.WriteLine($"GroupToIsCheckedConverter:{unitVersions[0].Name}");
				return Utility.CheckState(unitVersions.ToList());
			}
			return false;
			//throw new NotImplementedException();
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
