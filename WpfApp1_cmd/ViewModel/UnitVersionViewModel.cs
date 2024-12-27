using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using WpfApp1_cmd.Models;

namespace WpfApp1_cmd.ViewModel
{
	internal class UnitVersionViewModel : ViewModelBase
	{
		//public ICollection<Album> UnitVersions { get; set; }
		public ReactiveCollection<UpdateInfo> UpdateInfos { get; set; }

		public List<Artist>? Artists { get; set; }

		public UnitVersionViewModel(ReactiveCollection<UpdateInfo> updates)
		{
			UpdateInfos = updates;

			var cvs = CollectionViewSource.GetDefaultView(UpdateInfos);
            cvs.GroupDescriptions.Add(new PropertyGroupDescription("UnitGroup"));

		}
		public bool IsSelectedGroup
		{
			get;
			set;
		} = true;
	}
}
