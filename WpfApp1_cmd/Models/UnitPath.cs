using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp1_cmd.Models
{
	public class UnitPath
	{
		public string GroupName { get; set; }
		public List<(string? name, string? path)>? units { get; set; }

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

			units.Add((unit.name, basePath + unit.path));
			if (unit.fusePath != "")
			{
				units.Add((unit.name, basePath + unit.fusePath));
			}
		}
	}
}
