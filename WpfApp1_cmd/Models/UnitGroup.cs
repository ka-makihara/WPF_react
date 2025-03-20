using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp1_cmd.Models
{
	internal class UnitGroup
	{
		private bool? _status;
		public bool? Status
		{
			get => _status;
			set
			{
				if( _status != value)
				{
					_status = value;
				}
			}
		}
		public List<UnitVersion>? Units { get; set; }

		public void UpdateStatus()
		{
			if( Units == null) { return; }
			Status = Utility.CheckState(Units);
		}

		public UnitGroup(UnitVersion ver)
		{
			Units ??= [];
			Units.Add(ver);
		}

		/// <summary>
		/// ユニットの選択状態を統一
		/// </summary>
		/// <param name="state"></param>
		public void UnificationUnitState(bool? state=null)
		{
			if (Units == null) { return; }

			if( state == null)
			{
				int fc = Units.Count(x => x.IsSelected.Value == false);
				if (fc != 0)
				{
					Units.ForEach(x => x.IsSelected.Value = false);
				}
			}
			else
			{
				Units.ForEach(x => x.IsSelected.Value = state);
			}
		}
	}
}

