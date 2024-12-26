using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp1_cmd.Models
{
	/*
	 * unitLink.json のデシリアライズ用の構造体
	 * 変数名はjsonのキー名と一致している必要があるので変更時は注意
	 */

	public class UnitLink
	{
		public List<UnitComponent> units { get; set; }
	}
	public class UnitComponent
	{
		public string name { get; set; }
		public List<string> components { get; set; }
	}
}
