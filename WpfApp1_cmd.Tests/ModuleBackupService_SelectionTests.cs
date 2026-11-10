using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class ModuleBackupService_SelectionTests
{
	[Fact]
	public void Build_SelectsOnlyModulesUnderSelectedLcusAndMachinesWithControllers()
	{
		BackupSelectionLcu[] lcus =
		[
			new("LineA", true, true, true,
			[
				new("MachineA", true, true,
				[
					new("ModuleA", true),
					new("ModuleB", false)
				]),
				new("MachineB", true, false, [new("ModuleC", true)]),
				new(null, true, true, [new("ModuleD", true)]),
				new("MachineC", false, true, [new("ModuleE", true)])
			]),
			new("LineB", true, false, true,
			[
				new("MachineD", true, true, [new("ModuleF", true)])
			]),
			new("LineC", true, true, false,
			[
				new("MachineE", true, true, [new("ModuleG", true)])
			])
		];

		IReadOnlyList<ModuleBackupSelection> result =
			new ModuleBackupService().BuildSelections(lcus);

		ModuleBackupSelection selected = Assert.Single(result);
		Assert.Equal(0, selected.LcuIndex);
		Assert.Equal(0, selected.MachineIndex);
		Assert.Equal(0, selected.ModuleIndex);
		Assert.Equal("LineA", selected.LcuName);
		Assert.Equal("MachineA", selected.MachineName);
		Assert.Equal("ModuleA", selected.ModuleName);
	}

	[Fact]
	public void Build_ReturnsEmptyWhenNoModuleIsSelected()
	{
		BackupSelectionLcu[] lcus =
		[
			new("LineA", true, true, true,
			[
				new("MachineA", true, true, [new("ModuleA", false)])
			])
		];

		Assert.Empty(new ModuleBackupService().BuildSelections(lcus));
	}

	[Fact]
	public void Build_ThrowsWhenCancelledBeforeBuildingSelections()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		Assert.Throws<OperationCanceledException>(() =>
			new ModuleBackupService().BuildSelections([], cancellation.Token));
	}

	[Fact]
	public void Build_ThrowsWhenCancelledWhileEnumeratingLcus()
	{
		using CancellationTokenSource cancellation = new();
		BackupSelectionLcu[] lcus =
		[
			new(
				"LineA",
				true,
				true,
				true,
				new CancellingReadOnlyList<BackupSelectionMachine>(
					[],
					cancellation))
		];

		Assert.Throws<OperationCanceledException>(() =>
			new ModuleBackupService().BuildSelections(lcus, cancellation.Token));
	}

	private sealed class CancellingReadOnlyList<T>(
		IReadOnlyList<T> items,
		CancellationTokenSource cancellation) : IReadOnlyList<T>
	{
		public int Count
		{
			get
			{
				cancellation.Cancel();
				return items.Count;
			}
		}

		public T this[int index] => items[index];

		public IEnumerator<T> GetEnumerator() => items.GetEnumerator();

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
			GetEnumerator();
	}
}
