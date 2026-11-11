using Reactive.Bindings;
using WpfApp1_cmd;
using WpfApp1_cmd.Models;
using WpfApp1_cmd.Resources;
using WpfApp1_cmd.ViewModel;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class StatusBarViewModelTests
{
	[Fact]
	public void TransferSelection_TracksTreeBeforeAndAfterUnitsAreLoaded()
	{
		LcuInfo lcu = new("test");
		MachineInfo machine = new();
		ModuleInfo module = new();
		lcu.Children.Add(machine);
		machine.Children.Add(module);
		using StatusBarViewModel status = new();
		status.SetItems([]);
		Assert.False(status.HasTransferSelection.Value);
		status.SetItems([lcu]);
		Assert.True(status.HasTransferSelection.Value);

		module.IsSelected.Value = false;
		Assert.False(status.HasTransferSelection.Value);
		module.IsSelected.Value = true;
		UnitVersion unit = new(false);
		module.UnitVersions.Add(unit);
		Assert.False(status.HasTransferSelection.Value);
		unit.IsSelected.Value = true;
		Assert.True(status.HasTransferSelection.Value);
		lcu.IsSelected.Value = false;
		Assert.False(status.HasTransferSelection.Value);
		lcu.IsSelected.Value = true;
		Assert.True(status.HasTransferSelection.Value);
		unit.IsSelected.Value = false;
		Assert.False(status.HasTransferSelection.Value);
	}

	[Fact]
	public void TransferSelection_ExcludesUnavailableUnits()
	{
		LcuInfo lcu = new("test");
		MachineInfo machine = new();
		ModuleInfo module = new();
		lcu.Children.Add(machine);
		machine.Children.Add(module);
		module.UnitVersions.Add(new(false) { IsUpdateFileMissing = true });
		module.UnitVersions.Add(new(false) { IsSupported = false });
		module.UnitVersions.Add(new(true) { Attribute = Define.NOT_UPDATE });
		using StatusBarViewModel status = new();
		status.SetItems([lcu]);

		Assert.False(status.HasTransferSelection.Value);
		Assert.Equal(0, status.UnitCount);
	}

	[Fact]
	public void Selection_ShowsSelectedHierarchyAndClearsOnNull()
	{
		LcuInfo lcu = new("test") { Name = "LINE-1" };
		MachineInfo machine = new() { Name = "Machine_1", Parent = lcu };
		ModuleInfo module = new() { Name = "1-L", Parent = machine };
		using StatusBarViewModel status = new();

		status.SetSelection(module);
		Assert.Equal("LINE-1 / Machine_1 / 1-L", status.Selection);
		status.SetSelection(machine);
		Assert.Equal("LINE-1 / Machine_1", status.Selection);
		status.SetSelection(null);
		Assert.Empty(status.Selection);
	}

	[Fact]
	public void Count_TracksSelectionAndNewUnitsAndExcludesUnavailableUnits()
	{
		LcuInfo lcu = new("test");
		MachineInfo machine = new();
		ModuleInfo module = new();
		lcu.Children.Add(machine);
		machine.Children.Add(module);
		using StatusBarViewModel status = new();
		status.SetItems([lcu]);
		Assert.Equal(0, status.UnitCount);

		UnitVersion unit = new(true);
		module.UnitVersions.Add(unit);
		module.UnitVersions.Add(new(false) { IsUpdateFileMissing = true });
		module.UnitVersions.Add(new(false) { IsSupported = false });
		module.UnitVersions.Add(new(true) { Attribute = Define.NOT_UPDATE });
		Assert.Equal(1, status.UnitCount);
		unit.IsSelected.Value = false;
		Assert.Equal(0, status.UnitCount);
		unit.IsSelected.Value = true;
		Assert.Equal(1, status.UnitCount);
		lcu.IsSelected.Value = false;
		Assert.Equal(0, status.UnitCount);
		lcu.IsSelected.Value = true;
		Assert.Equal(1, status.UnitCount);
		module.UnitVersions.Remove(unit);
		Assert.Equal(0, status.UnitCount);
	}

	[Fact]
	public void ReplacingTree_UnsubscribesOldSelectionAndCountsNewTree()
	{
		ReactiveCollection<LcuInfo> oldTree = [];
		using StatusBarViewModel status = new();
		status.SetItems(oldTree);
		status.SetItems([]);
		oldTree.Add(new("old"));
		Assert.Equal(0, status.UnitCount);
	}

	[Fact]
	public void Operation_RestoresOuterStateAndReadyEvenWhenScopesEndOutOfOrder()
	{
		using StatusBarViewModel status = new();
		Assert.Equal(Resource.StatusReady, status.Operation);
		IDisposable outer = status.BeginOperation("Reading");
		IDisposable inner = status.BeginOperation("Transferring");
		Assert.Equal("Transferring", status.Operation);
		outer.Dispose();
		Assert.Equal("Transferring", status.Operation);
		inner.Dispose();
		Assert.Equal(Resource.StatusReady, status.Operation);
	}

	[Fact]
	public void Operation_RestoresReadyWhenOperationFails()
	{
		using StatusBarViewModel status = new();
		void FailOperation()
		{
			using IDisposable operation = status.BeginOperation("Reading");
			throw new InvalidOperationException("Read failed");
		}
		Assert.Throws<InvalidOperationException>(FailOperation);
		Assert.Equal(Resource.StatusReady, status.Operation);
	}
}
