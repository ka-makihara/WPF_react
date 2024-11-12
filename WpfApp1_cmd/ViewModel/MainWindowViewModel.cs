using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

using WpfApp1_cmd.Command;

namespace WpfApp1_cmd.ViewModel
{
    public class MainWindowViewModel : ViewModelBase
    {
        private ViewModelBase activeView = null;

        private ObservableCollection<UnitVersion> _versions;
        public ObservableCollection<UnitVersion> Versions
        {
            get => _versions;
            set => SetProperty(ref _versions, value);
        }

        public ViewModelBase ActiveView
        {
            get { return activeView; }
            set
            {
                if (activeView != value) {
                    activeView = value;
                    OnPropertyChanged("ActiveView");
                }
            }
        }

        private Dictionary<string, ViewModelBase> viewModeTable { get; }


        private bool flag = true;
        public bool Flag
        {
            get { return flag; }
            set { flag = value; ButtonCommand.DelegateCanExecute(); }
        }

        public DelegateCommand ButtonCommand { get; }
        public DelegateCommand<string> ScreenTransitionCommand { get; }
        public ReactiveCommand ButtonCommand2 { get; } = new ReactiveCommand();

        public ReactiveCommand CutCommand { get; private set; } = new ReactiveCommand();
        public ReactiveCommand CopyCommand { get; } = new ReactiveCommand();
        public ReactiveCommand PasteCommand { get; } = new ReactiveCommand();

        public ReactiveProperty<bool> FlagProperty1 { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> FlagProperty2 { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> FlagProperty3 { get; } = new ReactiveProperty<bool>(false);

        public MainWindowViewModel()
        {
            LoadUnitVersions();

            ButtonCommand = new DelegateCommand(async () =>
            {
                Flag = false;
                await Task.Delay(5000);
                Flag = true;
            }, canExecuteCommand);

            ScreenTransitionCommand = new DelegateCommand<string>(screenTransitionExecute);

            viewModeTable = new Dictionary<string, ViewModelBase>
            {
                { "AView", new AViewModel() },
                { "BView", new BViewModel() },
                { "CView", new CViewModel() },
                { "GView", new GViewModel(Versions) },
                { "HView", new GViewModel(Versions) },
                { "MView", new MachineViewModel() }
            };
            ActiveView = viewModeTable["AView"];

            ButtonCommand2.Subscribe(async () =>
            {
                Flag = false;
                await Task.Delay(1000);
                Flag = true;

                //FlagProperty1.Value = true;
                FlagProperty2.Value = true;
                //FlagProperty3.Value = true;
            });

            /*
            //FlagProperty1 が true になったら CutCommand を実行可とする
            CutCommand = FlagProperty1.ToReactiveCommand();

            //フラグが二つとも true になったら CutCommand を実行可とする
            CutCommand = FlagProperty1.CombineLatest(FlagProperty2, (x, y) => x && y).ToReactiveCommand();

            //フラグが三つとも true になったら CutCommand を実行可とする
            CutCommand = new[] { FlagProperty1, FlagProperty2, FlagProperty3 }
                .CombineLatest(x => x.All(y => y)).ToReactiveCommand();

            CutCommand = new[] { FlagProperty1, FlagProperty2, FlagProperty3 }
                .CombineLatestValuesAreAllTrue().ToReactiveCommand();
                //.CombineLatestValuesAreAllFalse().ToReactiveCommand();
            */
            //フラグが一つでも true になったら CutCommand を実行不可とする
            CutCommand = new[] { FlagProperty1, FlagProperty2, FlagProperty3 }
                .CombineLatest(x => x.Any(y => y)).ToReactiveCommand();

            CutCommand.Subscribe(() => CutCmdExecute());

            CopyCommand.Subscribe(() => { Debug.WriteLine("Copy"); });
            PasteCommand.Subscribe(() => { Debug.WriteLine("Paste"); });
        }

        private void CutCmdExecute()
        {
            Debug.WriteLine("Cut");
        }
        private bool canExecuteCommand()
        {
            return Flag;
        }

        private void screenTransitionExecute(string screenName)
        {
            ActiveView = viewModeTable[screenName];
        }
        private void LoadUnitVersions()
        {
            Versions = new ObservableCollection<UnitVersion>
            {
                new UnitVersion { IsSelected = true,  Name = "Unit1", CurVersion = "1.0.0", NewVersion = "1.0.1" },
                new UnitVersion { IsSelected = false, Name = "Unit2", CurVersion = "1.0.0", NewVersion = "1.0.1" },
                new UnitVersion { IsSelected = true,  Name = "Unit3", CurVersion = "1.0.0", NewVersion = "1.0.1" },
                new UnitVersion { IsSelected = false, Name = "Unit4", CurVersion = "1.0.0", NewVersion = "1.0.1" },
                new UnitVersion { IsSelected = true,  Name = "Unit5", CurVersion = "1.0.0", NewVersion = "1.0.1" },
            };
        }

        private string _textValue = "Hello, World!";
        public string TextValue 
        {
            get => _textValue;
            set => SetProperty(ref _textValue,value);
        }
    }
}
