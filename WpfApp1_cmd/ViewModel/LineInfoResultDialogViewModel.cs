using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using WpfApp1_cmd.Core;

namespace WpfApp1_cmd.ViewModel
{
	internal class LineInfoResultDialogViewModel : ViewModelBase
	{
		public ICommand CloseCommand { get; }

		public ReactiveCollection<LineInfoResult> LineInfoResults { get; }

		public LineInfoResultDialogViewModel(Action<LineInfoResultDialogViewModel> closeHandler)
		{
			LineInfoResults =
			[
				new("Line1", "Result1", "Detail1"),
				new("Line2", "Result2", "Detail2"),
				new("Line3", "Result3", "Detail3"),
				new("Line4", "Result4", "Detail4"),
				new("Line5", "Result5", "Detail5"),
				new("Line6", "Result6", "Detail6"),
				new("Line7", "Result7", "Detail7"),
				new("Line8", "Result8", "Detail8"),
				new("Line9", "Result9", "Detail9"),
				new("Line10", "Result10", "Detail10"),
			];
			CloseCommand = new SimpleCommand<object>(o => true, o => closeHandler(this));
		}
	}

	public record LineInfoResult(string Line, string Result, string Detail);
}
