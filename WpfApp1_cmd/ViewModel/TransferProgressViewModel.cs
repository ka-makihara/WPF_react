using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using WpfApp1_cmd.Core;

namespace WpfApp1_cmd.ViewModel
{
    public class TransferProgressViewModel : ViewModelBase
	{
		public ICommand TransferCancelCommand { get; }

		public bool _isCanceled;
		public bool IsCanceled
		{
			get => _isCanceled; set => _isCanceled = value;
		}

		private string _transferFile;
		public string TransferFile
		{
			get => _transferFile;
			set => SetProperty(ref _transferFile, value);
		}

		private int _transferValue = 0;
		public int TransferValue
		{
			get => _transferValue;
			set => SetProperty(ref _transferValue, value);
		}

		public TransferProgressViewModel(Action<TransferProgressViewModel> closeHandler)
		{
			TransferCancelCommand = new SimpleCommand<object>(o => true, o => closeHandler(this));

			TransferFile = "File Name";
			TransferValue = 0;
			IsCanceled = false;
		}

	}
}
