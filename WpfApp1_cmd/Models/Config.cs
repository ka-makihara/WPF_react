﻿using System;
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

	public class Config
	{
		public List<UnitLink> units { get; set; }
		public Options Options { get; set; }
	}

	public class UnitLink
	{
		public Dictionary<string, object> name { get; set; }
		public List<string> components { get; set; }
		public string mode { get; set; }
		public List<string> files { get; set; }

		public bool ContainsName(string n)
		{
			if (this.name == null) return false;
			foreach (var item in this.name)
			{
				if (string.Compare(item.Value.ToString(), n, true) == 0)
				{
					return true;
				}
			}
			return false;
		}

		public string GetName(string? type = null)
		{
			string key = type ?? "*";
			if (this.name == null) return "";

			
			this.name.TryGetValue(key, out object? value);
			return value?.ToString() ?? "";
		}
	}
/*
	public class Config2
	{
		public List<UnitLink2> units { get; set; }
	}

	public class UnitLink2
	{
		public Dictionary<string, object> name { get; set; }
		public List<string> components { get; set; }
		public string mode { get; set; }
		public List<string> files { get; set; }
	}
*/
	public class Options
	{
		public string dataFolder { get; set; }
		public string ignore_unit { get; set; }
		public string ignore_version { get; set; }
		public string ignore_machineType { get; set; }
		public List<string> extentions { get; set; }
		public List<string> machineType { get; set; }

		public bool ContainsExt(string ext)
		{
			//return extentions.Contains(ext);
			if( ext == null || ext == "" ) return false;
			var rr = extentions.FirstOrDefault(x => string.Compare(x, ext, true) == 0);
			return (rr != null);
		}

		public bool GetDefaultOption(string opt, bool value = false)
		{
			if (opt == "ignore_unit")
			{
				return (ignore_unit == "true");
			}
			else if (opt == "ignore_version")
			{
				return (ignore_version == "true");
			}
			else if (opt == "ignore_machineType")
			{
				return (ignore_machineType == "true");
			}
			return value;
		}
	}
}
