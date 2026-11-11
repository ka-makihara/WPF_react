using WpfApp1_cmd;
using WpfApp1_cmd.Models;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class SupportedUnitSelectionStateTests
{
	[Fact]
	public void CheckSelectableUnitSelectionState_IgnoresUnsupportedUnits()
	{
		UnitVersion selected = new(false);
		selected.IsSelected.Value = true;
		UnitVersion unsupported = new(false) { IsSupported = false };

		bool? result = Utility.CheckSelectableUnitSelectionState([selected, unsupported]);

		Assert.True(result);
	}

	[Fact]
	public void CheckSelectableUnitSelectionState_ReportsPartialSelectionAmongSelectableUnits()
	{
		UnitVersion selected = new(false);
		selected.IsSelected.Value = true;
		UnitVersion unselected = new(false);
		UnitVersion unsupported = new(false) { IsSupported = false };

		bool? result = Utility.CheckSelectableUnitSelectionState(
			[selected, unselected, unsupported]);

		Assert.Null(result);
	}

	[Fact]
	public void CheckSelectableUnitSelectionState_ReturnsFalseWhenNoSupportedUnitsExist()
	{
		UnitVersion unsupported = new(false) { IsSupported = false };

		bool? result = Utility.CheckSelectableUnitSelectionState([unsupported]);

		Assert.False(result);
	}

	[Fact]
	public void CheckSelectableUnitSelectionState_IgnoresUpdateProhibitedUnits()
	{
		UnitVersion selected = new(false);
		selected.IsSelected.Value = true;
		UnitVersion prohibited = new(false) { Attribute = Define.NOT_UPDATE };
		prohibited.IsSelected.Value = true;

		bool? result = Utility.CheckSelectableUnitSelectionState([selected, prohibited]);

		Assert.True(result);
	}

	[Fact]
	public void CheckSelectableUnitSelectionState_ExcludesUpdateProhibitedUnitsFromPartialState()
	{
		UnitVersion selected = new(false);
		selected.IsSelected.Value = true;
		UnitVersion unselected = new(false);
		UnitVersion prohibited = new(false) { Attribute = Define.NOT_UPDATE };
		prohibited.IsSelected.Value = true;

		bool? result = Utility.CheckSelectableUnitSelectionState(
			[selected, unselected, prohibited]);

		Assert.Null(result);
	}

	[Fact]
	public void CheckSelectableUnitSelectionState_IgnoresUnitsWithMissingUpdateFiles()
	{
		UnitVersion selected = new(false);
		selected.IsSelected.Value = true;
		UnitVersion missingFile = new(false)
		{
			IsUpdateFileMissing = true
		};

		bool? result = Utility.CheckSelectableUnitSelectionState([selected, missingFile]);

		Assert.True(result);
	}

	[Fact]
	public void CheckSelectableUnitSelectionState_ReturnsFalseWhenNoUnitsCanBeUpdated()
	{
		UnitVersion missingFile = new(false)
		{
			IsUpdateFileMissing = true
		};

		bool? result = Utility.CheckSelectableUnitSelectionState([missingFile]);

		Assert.False(result);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(null)]
	public void MissingUpdateFile_CannotBeSelectedEvenThroughSelectAll(bool? selection)
	{
		UnitVersion missingFile = new(false) { IsUpdateFileMissing = true };
		ModuleInfo module = new();
		module.UnitVersions.Add(missingFile);

		module.UpdateChildren(selection);

		Assert.False(missingFile.IsSelected.Value);
		Assert.False(missingFile.IsUpdatable);
		Assert.True(missingFile.IsBlockedByMissingFile);
	}
}
