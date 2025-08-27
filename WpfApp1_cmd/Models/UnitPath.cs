using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp1_cmd.Models
{
	/// <summary>
	/// ユニットのグループとそのパスを管理するクラス。
	///   ・UpdateCommon.inf の Path, FuserPathをグループ化
	///
	///   ・config.yaml の グループ情報を元にグループ化する
	///      (ex. NXT_TrayUnitM_IF,NXT_TrayUnitM_PLC,NXT_TrayUnitM_Conv --> NXT_TrayUnitM グループ)
	/// </summary>
	public class UnitPath
	{
		public required string GroupName { get; set; }
		public List<(string? name, string? path, string? upath)>? units { get; set; }

		public void AddUnitFile(string basePath, (string? name, string path, string fusePath) unit)
		{
			units ??= [];
			for (int i = 0; i < units.Count; i++)
			{
				if (units[i].name == unit.name && units[i].path == basePath + unit.path)
				{
					return; // 既に存在する場合は追加しない
				}
			}
			if( unit.fusePath != "")
			{
				units.Add((unit.name, basePath + unit.path, basePath + unit.fusePath));
			}
			else
			{
				units.Add((unit.name, basePath + unit.path, ""));
			}
		}
		public int FileCount()
		{
			int cnt = 0;

			foreach (var unit in units)
			{
				if (unit.path != null && unit.path != "")
				{
					cnt++;
				}
				if(unit.upath != null && unit.upath != "")
				{
					cnt++;
				}
			}
			return cnt;
		}
	}
}
