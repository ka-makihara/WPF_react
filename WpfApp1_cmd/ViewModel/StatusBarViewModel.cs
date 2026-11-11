using System.Collections.Specialized;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Reactive.Bindings;
using WpfApp1_cmd.Models;
using WpfApp1_cmd.Resources;

namespace WpfApp1_cmd.ViewModel;

public sealed class StatusBarViewModel : ViewModelBase, IDisposable
{
	private readonly CompositeDisposable subscriptions = new();
	private readonly List<object> operations = [];
	private readonly Dictionary<object, string> operationLabels = [];
	private ReactiveCollection<LcuInfo>? items;
	private int unitCount;
	private string selection = "";
	private string dataFolder = "";

	public string Operation => operations.Count == 0
		? Resource.StatusReady : operationLabels[operations[^1]];
	public int UnitCount
	{
		get => unitCount;
		private set => SetProperty(ref unitCount, value);
	}
	public ReactivePropertySlim<bool> HasTransferSelection { get; } = new(false);
	public string Selection
	{
		get => selection;
		private set => SetProperty(ref selection, value);
	}
	public string DataFolder
	{
		get => dataFolder;
		set => SetProperty(ref dataFolder, value);
	}

	public void SetSelection(CheckableItem? item)
	{
		Selection = item switch
		{
			ModuleInfo module => string.Join(" / ",
				module.Parent?.Parent?.Name, module.Parent?.Name, module.Name),
			MachineInfo machine => string.Join(" / ", machine.Parent?.Name, machine.Name),
			LcuInfo lcu => lcu.Name ?? "",
			_ => ""
		};
	}

	public void SetItems(ReactiveCollection<LcuInfo> value)
	{
		items = value;
		ObserveItems();
	}

	public IDisposable BeginOperation(string label)
	{
		object key = new();
		operations.Add(key);
		operationLabels.Add(key, label);
		OnPropertyChanged(nameof(Operation));
		return Disposable.Create(() =>
		{
			operations.Remove(key);
			operationLabels.Remove(key);
			OnPropertyChanged(nameof(Operation));
		});
	}

	private void ObserveItems()
	{
		subscriptions.Clear();
		if (items == null)
		{
			return;
		}
		ObserveCollection(items);
		foreach (LcuInfo lcu in items)
		{
			ObserveSelection(lcu);
			ObserveCollection(lcu.Children);
			foreach (MachineInfo machine in lcu.Children)
			{
				ObserveSelection(machine);
				ObserveCollection(machine.Children);
				foreach (ModuleInfo module in machine.Children)
				{
					ObserveSelection(module);
					ObserveCollection(module.UnitVersions);
					foreach (UnitVersion unit in module.UnitVersions)
					{
						ObserveSelection(unit);
					}
				}
			}
		}
		UpdateCount();
	}

	private void ObserveCollection(INotifyCollectionChanged collection)
	{
		subscriptions.Add(Observable.FromEventPattern<NotifyCollectionChangedEventHandler,
			NotifyCollectionChangedEventArgs>(
				handler => collection.CollectionChanged += handler,
				handler => collection.CollectionChanged -= handler)
			.Subscribe(_ => ObserveItems()));
	}

	private void ObserveSelection(CheckableItem item) =>
		subscriptions.Add(item.IsSelected.Skip(1).Subscribe(_ => UpdateCount()));

	private void UpdateCount()
	{
		ModuleInfo[] selectedModules = items == null ? [] : items
			.Where(lcu => lcu.IsSelected.Value != false)
			.SelectMany(lcu => lcu.Children)
			.Where(machine => machine.IsSelected.Value != false)
			.SelectMany(machine => machine.Children)
			.Where(module => module.IsSelected.Value != false)
			.ToArray();
		UnitCount = selectedModules
			.SelectMany(module => module.UnitVersions)
			.Count(unit => unit.IsUpdatable && unit.IsSelected.Value == true);
		HasTransferSelection.Value = UnitCount > 0 ||
			selectedModules.Any(module => module.UnitVersions.Count == 0 &&
				module.IsSelected.Value == true);
	}

	public void Dispose()
	{
		subscriptions.Dispose();
		HasTransferSelection.Dispose();
	}
}
