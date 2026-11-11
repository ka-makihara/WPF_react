using WpfApp1_cmd.Models;
using WpfApp1_cmd.ViewModel;
using WpfLcuCtrlLib;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class MachineViewModelTests
{
	public MachineViewModelTests()
	{
		Assert.True(Config.ReadConfig());
	}

	[Theory]
	[InlineData(false, false, false)]
	[InlineData(true, true, true)]
	[InlineData(true, false, null)]
	public void OpeningMachineBeforeUnitsAreLoaded_PreservesTreeSelection(
		bool firstSelected, bool secondSelected, bool? expectedAll)
	{
		LcuInfo lcu = new("test");
		MachineInfo machine = CreateMachine();
		lcu.Children.Add(machine);
		ModuleInfo first = new();
		ModuleInfo second = new();
		machine.Children.Add(first);
		machine.Children.Add(second);
		first.IsSelected.Value = firstSelected;
		second.IsSelected.Value = secondSelected;

		MachineViewModel viewModel = new(machine, machine.Children);

		Assert.Equal(expectedAll, viewModel.IsAllSelected.Value);
		Assert.Equal(firstSelected, first.IsSelected.Value);
		Assert.Equal(secondSelected, second.IsSelected.Value);
		Assert.Equal(expectedAll, machine.IsSelected.Value);
		Assert.Equal(expectedAll, lcu.IsSelected.Value);
	}

	[Fact]
	public void OpeningAndRefreshingMachine_DoesNotChangeUnitSelection()
	{
		MachineInfo machine = CreateMachine();
		ModuleInfo module = new();
		machine.Children.Add(module);
		UnitVersion unit = new(false);
		module.UnitVersions.Add(unit);
		module.IsSelected.Value = true;

		MachineViewModel viewModel = new(machine, machine.Children);
		viewModel.UpdateCheck();

		Assert.True(viewModel.IsAllSelected.Value);
		Assert.True(module.IsSelected.Value);
		Assert.False(unit.IsSelected.Value);
	}

	[Fact]
	public void AllCheckbox_StillUpdatesModulesAndUnits()
	{
		MachineInfo machine = CreateMachine();
		ModuleInfo module = new();
		machine.Children.Add(module);
		UnitVersion unit = new(false);
		module.UnitVersions.Add(unit);
		module.IsSelected.Value = false;
		MachineViewModel viewModel = new(machine, machine.Children);

		viewModel.IsAllSelected.Value = true;
		Assert.True(module.IsSelected.Value);
		Assert.True(unit.IsSelected.Value);

		viewModel.IsAllSelected.Value = false;
		Assert.False(module.IsSelected.Value);
		Assert.False(unit.IsSelected.Value);
	}

	private static MachineInfo CreateMachine() => new()
	{
		Machine = new Machine { MachineType = Config.Options.machineType.First() }
	};
}
