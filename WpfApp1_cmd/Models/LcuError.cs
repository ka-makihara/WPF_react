using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp1_cmd.Models
{
	internal class LcuError
	{
	}

	internal class LcuErrMsg
	{
		public required string errorMsg { get; init; }
		public required string errorStatus { get; init; }
	}
}
