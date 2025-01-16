using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp1_cmd.Controls
{
	public class MultiTagBoxItem : INotifyPropertyChanged
	{
		/// <summary>
		/// タグ名を指定して、MultiTagBoxItem のインスタンスを初期化します。
		/// </summary>
		/// <param name="t"></param>
		public MultiTagBoxItem(string t) { Title = t; }

		/// <summary>
		/// タグの文字列
		/// </summary>
		public string Title { get; set; }

		private bool _IsSelected = false;
		/// <summary>
		/// タグが選択されているかどうかを設定または取得します。
		/// </summary>
		public bool IsSelected { get { return _IsSelected; } set { _IsSelected = value; OnPropertyChanged("IsSelected"); } }

		/// <summary>
		/// タグ文字列を返します。
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return Title;
		}

		#region INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged(string name)
		{
			if (PropertyChanged != null)
			{
				PropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs(name));
			}
		}
		#endregion
	}
}

