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
					new("ModuleA", "192.168.0.1", true),
					new("ModuleB", "192.168.0.2", false)
				]),
				new("MachineB", true, false, [new("ModuleC", "192.168.0.3", true)]),
				new(null, true, true, [new("ModuleD", "192.168.0.4", true)]),
				new("MachineC", false, true, [new("ModuleE", "192.168.0.5", true)])
			]),
			new("LineB", true, false, true,
			[
				new("MachineD", true, true, [new("ModuleF", "192.168.0.6", true)])
			]),
			new("LineC", true, true, false,
			[
				new("MachineE", true, true, [new("ModuleG", "192.168.0.7", true)])
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
		Assert.Equal("192.168.0.1", selected.IPAddress);
		Assert.Equal("ModuleA", selected.ModuleName);
	}

	[Fact]
	public void Build_ReturnsEmptyWhenNoModuleIsSelected()
	{
		BackupSelectionLcu[] lcus =
		[
			new("LineA", true, true, true,
			[
				new("MachineA", true, true, [new("ModuleA", "192.168.0.1", false)])
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
