using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace WpfApp1_cmd.Models
{
	/*
	 * unitLink.json のデシリアライズ用の構造体
	 * 変数名はjsonのキー名と一致している必要があるので変更時は注意
	 */

	// ConfigTemp は Config の一時的な読み込み用のクラス(static class に値を読み込むため)
	class ConfigTemp
	{
		public List<UnitLink>? units { get; set; }
		public Options? Options { get; set; }
	}

	public static class Config
	{
		public static List<UnitLink> units { get; set; }
		public static Options Options { get; set; }

		public static bool ReadConfig()
		{
			var assembly = System.Reflection.Assembly.GetExecutingAssembly();
			var srcName = $"{assembly.GetName().Name}.Resources.config.yaml";
			string str = "";

			using (var stream = assembly.GetManifestResourceStream(srcName))
			{
				using (var reader = new StreamReader(stream))
				{
					str = reader.ReadToEnd();
				}
			}
			var deserializer = new DeserializerBuilder().Build();
			var config = deserializer.Deserialize<ConfigTemp>(str);

			if (config.units != null)
			{
				units = config.units;
			}
			if( config.Options != null)
			{
				Options = config.Options;
			}

			return true;
		}
	}

	public class UnitLink
	{
		public Dictionary<string, object> name { get; set; }
		public List<string> components { get; set; }
		public string? mode { get; set; }
		public List<string> files { get; set; }

		public bool ContainsName(string? n)
		{
			if( n == null || n == "") return false;
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
		public string inspection { get; set; }
		public List<string> extentions { get; set; }
		public List<string> machineType { get; set; }

		public bool ContainsExt(string ext)
		{
			//return extentions.Contains(ext);
			if( ext == null || ext == "" ) return false;
			var rr = extentions.FirstOrDefault(x => string.Compare(x, ext, true) == 0);
			return (rr != null);
		}

		public bool ContainsMachineType(string type)
		{
			if (type == null || type == "") return false;
			var rr = machineType.FirstOrDefault(x => string.Compare(x, type, true) == 0);
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
			else if (opt == "inspection")
			{
				return (inspection == "true");
			}
			return value;
		}
	}
}
