using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp1_cmd.Controls
{
	public class MultiTagBoxItemCollection : ObservableCollection<MultiTagBoxItem>
	{
		/// <summary>
		/// タグ文字列のリストを指定して、コレクションを初期化します。
		/// </summary>
		/// <param name="nodes"></param>
		public MultiTagBoxItemCollection(IEnumerable<string> nodes) : this(nodes.Select(t => new MultiTagBoxItem(t)))
		{
		}

		/// <summary>
		/// MultiTagBoxItem のリストを指定して、コレクションを初期化します。
		/// </summary>
		/// <param name="nodes"></param>
		public MultiTagBoxItemCollection(IEnumerable<MultiTagBoxItem> nodes) : base(nodes)
		{
		}

		/// <summary>
		/// 選択されているタグをカンマ区切りで返します。
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return ToString(", ");
		}

		/// <summary>
		/// 選択されているタグを、指定した区切り文字列で結合して返します。
		/// </summary>
		/// <param name="separater"></param>
		/// <returns></returns>
		public string ToString(string separater)
		{
			var selectedItems = this.Items.Where(t => t.IsSelected).Select(t2 => t2.Title);
			return string.Join(separater, selectedItems);
		}

		/// <summary>
		/// 選択されているタグを、指定した先頭文字列と末尾文字列を付加し、結合して返します。
		/// </summary>
		/// <param name="head"></param>
		/// <param name="tail"></param>
		/// <returns></returns>
		public string ToString(string head, string tail)
		{
			return head + ToString(tail + head) + tail;
		}

		/// <summary>
		/// カンマ区切りのタグ文字列から、リストのタグを選択状態にします。
		/// </summary>
		/// <param name="str"></param>
		public void Restore(string str)
		{
			if (string.IsNullOrEmpty(str))
			{
				var tags = str.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
				tags = Array.ConvertAll(tags, (t) => t.Trim());
				foreach (var item in this)
				{
					item.IsSelected = tags.Contains(item.Title);
				}
			}
		}
	}
}

