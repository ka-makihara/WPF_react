using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WpfApp1_cmd.Models;
using Reactive.Bindings;
using System.ComponentModel;
using System.Windows.Data;

namespace WpfApp1_cmd.ViewModel
{
	internal class TransferResultViewModel : ViewModelBase
	{
		public ReactiveCollection<LineData> LineNames { get; set; }
		public ReactivePropertySlim<LineData> SelectedLineName { get; set; }

		public ReactiveCollection<MachineData> MachineNames { get; set; }
		public ReactivePropertySlim<MachineData> SelectedMachineName { get; set; }

		public ReactiveCollection<TransferResult> ResultDataList { get; set; }
		public ICollectionView FilteredResultDataList { get; set; }

		public TransferResultViewModel()
		{
			LineNames = new ReactiveCollection<LineData>
            {
			//	new LineData { Name = "All", Id = 0 },
				new LineData { Name = "Line1", Id = 1 },
                new LineData { Name = "Line2", Id = 2 },
                new LineData { Name = "Line3", Id = 3 }
            };
			//SelectedLineName = new ReactivePropertySlim<LineData>(LineNames.First());
			SelectedLineName = new ReactivePropertySlim<LineData>();
			SelectedLineName.Subscribe(_ => ApplyFilter());

			MachineNames = new ReactiveCollection<MachineData>
            {
             //   new MachineData { Name = "All", Id = 0 },
                new MachineData { Name = "Machine1", Id = 1 },
                new MachineData { Name = "Machine2", Id = 2 },
                new MachineData { Name = "Machine3", Id = 3 }
            };
			//SelectedMachineName = new ReactivePropertySlim<MachineData>(MachineNames.First());
			SelectedMachineName = new ReactivePropertySlim<MachineData>();
			SelectedMachineName.Subscribe(_ => ApplyFilter());

			ResultDataList =
			[
				new() { LineName = "Line1", MachineName = "Machine1", ModuleName = "Module1", UnitName = "Unit2", IsSuccess = false },
				new() { LineName = "Line1", MachineName = "Machine1", ModuleName = "Module1", UnitName = "Unit2", IsSuccess = false },
				new() { LineName = "Line1", MachineName = "Machine2", ModuleName = "Module1", UnitName = "Unit2", IsSuccess = false },
				new() { LineName = "Line1", MachineName = "Machine3", ModuleName = "Module1", UnitName = "Unit2", IsSuccess = false },
				new() { LineName = "Line2", MachineName = "Machine2", ModuleName = "Module2", UnitName = "Unit1", IsSuccess = true },
				new() { LineName = "Line2", MachineName = "Machine2", ModuleName = "Module2", UnitName = "Unit1", IsSuccess = true },
				new() { LineName = "Line2", MachineName = "Machine1", ModuleName = "Module2", UnitName = "Unit1", IsSuccess = true },
				new() { LineName = "Line2", MachineName = "Machine2", ModuleName = "Module2", UnitName = "Unit1", IsSuccess = true },
				new() { LineName = "Line3", MachineName = "Machine3", ModuleName = "Module3", UnitName = "Unit1", IsSuccess = true },
				new() { LineName = "Line3", MachineName = "Machine1", ModuleName = "Module3", UnitName = "Unit1", IsSuccess = true },
				new() { LineName = "Line3", MachineName = "Machine2", ModuleName = "Module3", UnitName = "Unit1", IsSuccess = true },
				new() { LineName = "Line3", MachineName = "Machine3", ModuleName = "Module3", UnitName = "Unit1", IsSuccess = true }
			];
			FilteredResultDataList = CollectionViewSource.GetDefaultView(ResultDataList);
			ApplyFilter();
		}
		private void ApplyFilter()
		{
			if(FilteredResultDataList == null)
			{
				return;
			}
			FilteredResultDataList.Filter = x =>
			{
				if (x is TransferResult result)
				{
					bool lineFilter = SelectedLineName.Value == null /*|| SelectedLineName.Value.Name == "All"*/ || result.LineName == SelectedLineName.Value.Name;
                    bool machineFilter = SelectedMachineName.Value == null /*|| SelectedMachineName.Value.Name == "All"*/ || result.MachineName == SelectedMachineName.Value.Name;
                    return lineFilter && machineFilter;
					//return SelectedLineName.Value == null || SelectedLineName.Value.Name == "All" || result.LineName == SelectedLineName.Value.Name;
				}
				return false;
			};
			FilteredResultDataList.Refresh();
		}
	}

	public class LineData
    {
        public string Name { get; set; }
        public int Id { get; set; }
    }
	public class MachineData
    {
        public string Name { get; set; }
        public int Id { get; set; }
    }
}
