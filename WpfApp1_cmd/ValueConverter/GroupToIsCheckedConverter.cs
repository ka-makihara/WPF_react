using System;
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
	/// <summary>
	/// グループのチェック状態を取得するコンバータ
	///   ※グループヘッダーにつけられたチェックボックスの状態(IsChecked)のプロパティにバインドする時のコンバーター
	///   　value にはグループのアイテム(UnitVersionのリスト)が入る
	/// </summary>
	internal class GroupToIsCheckedConverter : IValueConverter, IMultiValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var group = value as ReadOnlyObservableCollection<object>;
			if (group != null)
			{
				var unitVersions = new ReadOnlyObservableCollection<UnitVersion>(
					new ObservableCollection<UnitVersion>(group.Cast<UnitVersion>())
				);
				//Debug.WriteLine($"GroupToIsCheckedConverter:{unitVersions[0].Name}");
				return Utility.CheckState(unitVersions.ToList());
			}
			return false;
		}

		/// <summary>
		///  values[0]=グループのアイテム、values[1]以降は再評価のトリガー用(値は使用しない)
		/// </summary>
		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			return Convert(values.Length > 0 ? values[0] : null, targetType, parameter, culture);
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
