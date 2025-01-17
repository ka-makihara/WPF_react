using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using WpfApp1_cmd.Models;

namespace WpfApp1_cmd.ViewModel
{
	internal class TransferResultWindowViewModel : ViewModelBase
	{
		public ReactiveCollection<ResultData> ResultDataList { get; set; }

		public ReactiveCollection<NameData> LineNames { get; set; }
		public ReactivePropertySlim<NameData> SelectedLineName { get; set; }

		public ObservableCollection<NameData> MachineNames { get; set; }
		public ReactivePropertySlim<NameData> SelectedMachineName { get; set; }

		public ObservableCollection<NameData> ModuleNames { get; set; }
		public ReactivePropertySlim<NameData> SelectedModuleName { get; set; }

		public ObservableCollection<NameData> StatusList { get; set; }
		public ReactivePropertySlim<NameData> SelectedStatus { get; set; }

		public ICollectionView FilteredResultDataList { get; set; }

		// CloseAction は ViewModel から View を閉じるための Action
		public Action CloseAction { get; set; }
		public ReactiveCommandSlim OKCommand { get; } = new ReactiveCommandSlim();

		public TransferResultWindowViewModel(string resultData)
		{
			string path = @"C:\Users\ka.makihara\Desktop\UnitTransferResult\result.txt";
			resultData = System.IO.File.ReadAllText(path);

			// OK ボタンが押されたときの処理(クローズ)
			OKCommand.Subscribe(_ =>
			{
				if( CloseAction != null )
				{ CloseAction(); }
			});

			StatusList =
			[
				new() {Name="OK"},
				new() {Name="NG"},
				new() {Name="Skip"}
			];
			SelectedStatus = new ReactivePropertySlim<NameData>();
			SelectedStatus.Subscribe(_ => ApplyFilter());

			SelectedLineName = new ReactivePropertySlim<NameData>();
			SelectedLineName.Subscribe(_ => ApplyFilter());

			SelectedMachineName = new ReactivePropertySlim<NameData>();
			SelectedMachineName.Subscribe(_ => ApplyFilter());

			SelectedModuleName = new ReactivePropertySlim<NameData>();
			SelectedModuleName.Subscribe(_ => ApplyFilter());

			string[] wordList = resultData.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);
			string[] data = wordList.Where(x => x.Contains("[Transfer]")).ToArray();

			ResultDataList = [];

			foreach (var st in data)
			{
				var s1 = st.Substring(st.IndexOf(" "));
				int idx = s1.IndexOf("=");
				if (idx != -1)
				{
					var s2 = s1.Substring(0, idx).Split(";");	//line, machine, module,unitを分割
					var status = s1[(idx+1)..];                 // = 後の「結果」を取得

					var n = StatusList.FirstOrDefault(x => x.Name == status);
					if (s2.Length > 3 && n != null)
					{
						ResultDataList.Add(new ResultData { LineName = s2[0], MachineName = s2[1], ModuleName = s2[2], UnitName = s2[3], Status = status });
					}
				}
			}
			LineNames = new ReactiveCollection<NameData>();
			var names = ResultDataList.Select(x => x.LineName).Distinct().ToList();
			foreach (var lineName in names)
			{
				LineNames.Add(new NameData { Name = lineName });
			}

			MachineNames = new ReactiveCollection<NameData>();
			names = ResultDataList.Select(x => x.MachineName).Distinct().ToList();
			foreach (var lineName in names)
			{
				MachineNames.Add(new NameData { Name = lineName });
			}

			ModuleNames = new ReactiveCollection<NameData>();
			names = ResultDataList.Select(x => x.ModuleName).Distinct().ToList();
			foreach (var lineName in names)
			{
				ModuleNames.Add(new NameData { Name = lineName });
			}

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
				if (x is ResultData result)
				{
					bool lineFilter = SelectedLineName.Value == null || result.LineName == SelectedLineName.Value.Name;
                    bool machineFilter = SelectedMachineName.Value == null || result.MachineName == SelectedMachineName.Value.Name;
					bool moduleFilter = SelectedModuleName.Value == null || result.ModuleName == SelectedModuleName.Value.Name;
					bool statusFilter = SelectedStatus.Value == null || result.Status == SelectedStatus.Value.Name;

					return lineFilter && machineFilter && moduleFilter && statusFilter;
				}
				return false;
			};
			FilteredResultDataList.Refresh();
		}
	}

	public class ResultData
	{
		public string LineName { get; set; }
		public string MachineName { get; set; }
		public string ModuleName { get; set; }
		public string UnitName { get; set; }
		public string Status { get; set; }
	}

	public class NameData
	{
		public string Name { get; set; }
	}
}

