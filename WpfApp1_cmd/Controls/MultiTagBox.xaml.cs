using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WpfApp1_cmd.Controls
{
	/// <summary>
	/// MultiTagBox.xaml の相互作用ロジック
	/// </summary>
	public partial class MultiTagBox : UserControl
	{
		public MultiTagBox()
		{
			InitializeComponent();
		}

		#region 依存関係プロパティ

		/// <summary>
		/// 選択候補のタグ一覧のコレクションを設定または取得します。
		/// </summary>
		public IEnumerable ItemsSource
		{
			get { return (IEnumerable)GetValue(ItemsSourceProperty); }
			set
			{
				SetValue(ItemsSourceProperty, value);
				Items.ToList().ForEach(t => { t.PropertyChanged += new System.ComponentModel.PropertyChangedEventHandler(tagBoxItem_PropertyChanged); });
				SetText();
			}
		}

		public static readonly DependencyProperty ItemsSourceProperty =
			DependencyProperty.Register("ItemsSource", typeof(IEnumerable), typeof(MultiTagBox), new UIPropertyMetadata(null));

		/// <summary>
		/// 表示されている文字列
		/// </summary>
		public string Text
		{
			get { return (string)GetValue(TextProperty); }
			private set { SetValue(TextProperty, value); }
		}

		public static readonly DependencyProperty TextProperty =
			DependencyProperty.Register("Text", typeof(string), typeof(MultiTagBox), new UIPropertyMetadata(string.Empty));


		/// <summary>
		/// デフォルトの文字列
		/// </summary>
		public string DefaultText
		{
			get { return (string)GetValue(DefaultTextProperty); }
			set { SetValue(DefaultTextProperty, value); }
		}

		public static readonly DependencyProperty DefaultTextProperty =
			DependencyProperty.Register("DefaultText", typeof(string), typeof(MultiTagBox), new UIPropertyMetadata(string.Empty));

		#endregion

		#region タグ操作

		/// <summary>
		/// ItemsSource を MultiTagBoxItemCollection として取得します。
		/// </summary>
		public MultiTagBoxItemCollection Items
		{
			get
			{
				if (this.ItemsSource == null) // なければ作る
				{
					this.ItemsSource = new MultiTagBoxItemCollection(new string[] { });
				}
				return (this.ItemsSource as MultiTagBoxItemCollection);
			}
		}

		/// <summary>
		/// 指定した文字列をタグ一覧に加えます。
		/// </summary>
		/// <param name="tag"></param>
		/// <param name="isSelected"></param>
		public void AddItem(string tag, bool isSelected = true)
		{
			var item = new MultiTagBoxItem(tag) { IsSelected = isSelected };
			item.PropertyChanged += new System.ComponentModel.PropertyChangedEventHandler(tagBoxItem_PropertyChanged);
			Items.Add(item);
			SetText();
		}

		/// <summary>
		/// 選択されているタグの一覧を設定または取得します。
		/// </summary>
		public IEnumerable<string> SelectedTags
		{
			get { return Items.Where(t => t.IsSelected).Select(t2 => t2.Title).ToList(); }
			set
			{
				Items.ToList().ForEach((t) =>
				{
					if (value.Any((t2) => t2.Equals(t.Title)))
					{
						t.IsSelected = true;
					}
				});
				SetText();
			}
		}

		#endregion

		#region タグ選択変更時の動作

		/// <summary>
		/// タグの選択内容をテキストボックスに反映します。
		/// </summary>
		private void SetText()
		{
			this.Text = (this.ItemsSource != null) ? this.ItemsSource.ToString() : this.DefaultText;

			if (string.IsNullOrEmpty(this.Text))
			{
				this.Text = this.DefaultText;
			}
		}

		private void CheckBox_Click(object sender, RoutedEventArgs e)
		{
			SetText();
		}

		private void baseComboBox_DropDownClosed(object sender, EventArgs e)
		{
			SetText();
		}

		private void tagBoxItem_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			SetText();
		}

		#endregion
	}
}

