using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp1_cmd.Models
{
	internal class TransferResult
	{
		public string? LineName { get; set; }
		public string? MachineName { get; set; }
		public string? ModuleName { get; set; }
		public string? UnitName { get; set; }
		public bool? IsSuccess { get; set; }
	}
}
