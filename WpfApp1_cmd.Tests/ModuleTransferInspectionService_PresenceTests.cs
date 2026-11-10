using System.IO;
using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class ModuleTransferInspectionService_PresenceTests
{
	[Fact]
	public void Inspect_ReportsPresenceForSelectedUnitFilesAndFuserPaths()
	{
		TransferredFilePresence[] results = new ModuleTransferInspectionService().InspectTransferredFiles(
			["unit.bin", "fuser.bin"],
			[
				new("UnitA", "unit.bin", "fuser.bin", true),
				new("UnitB", "missing.bin", null, true)
			]).ToArray();

		Assert.Collection(
			results,
			result =>
			{
				Assert.Equal("UnitA", result.UnitName);
				Assert.Equal("unit.bin", result.Path);
				Assert.False(result.IsFuserPath);
				Assert.True(result.Exists);
			},
			result =>
			{
				Assert.Equal("UnitA", result.UnitName);
				Assert.Equal("fuser.bin", result.Path);
				Assert.True(result.IsFuserPath);
				Assert.True(result.Exists);
			},
			result =>
			{
				Assert.Equal("UnitB", result.UnitName);
				Assert.Equal("missing.bin", result.Path);
				Assert.False(result.IsFuserPath);
				Assert.False(result.Exists);
			});
	}

	[Fact]
	public void Inspect_SkipsUnselectedAndUnnamedUnitsButProcessesIndeterminateSelection()
	{
		IReadOnlyList<TransferredFilePresence> results =
			new ModuleTransferInspectionService().InspectTransferredFiles(
				["a.bin", "b.bin", "c.bin"],
				[
					new("Unselected", "a.bin", null, false),
					new("", "b.bin", null, true),
					new("Indeterminate", "c.bin", null, null)
				]);

		TransferredFilePresence result = Assert.Single(results);
		Assert.Equal("Indeterminate", result.UnitName);
		Assert.Equal("c.bin", result.Path);
		Assert.True(result.Exists);
	}

	[Fact]
	public void InspectDirectory_MapsFilesRelativeToInspectionDirectory()
	{
		string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
		string filePath = Path.Combine(directory, "Fuji", "Peripheral", "UnitA", "program.bin");
		Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
		File.WriteAllText(filePath, "data");

		try
		{
			IReadOnlyList<TransferredFilePresence> results =
				new ModuleTransferInspectionService().InspectTransferredDirectory(
					directory,
					[new TransferredUnitFile(
						"UnitA",
						"/Fuji/Peripheral/UnitA/program.bin",
						null,
						true)]);

			TransferredFilePresence result = Assert.Single(results);
			Assert.True(result.Exists);
			Assert.Equal("/Fuji/Peripheral/UnitA/program.bin", result.Path);
		}
		finally
		{
			Directory.Delete(directory, recursive: true);
		}
	}

	[Fact]
	public void Inspect_ThrowsWhenCancelledBeforeInspecting()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		Assert.Throws<OperationCanceledException>(() =>
			new ModuleTransferInspectionService().InspectTransferredFiles(
				[],
				[],
				cancellation.Token));
	}

	[Fact]
	public void Inspect_ThrowsWhenCancelledWhileBuildingTransferredFileIndex()
	{
		using CancellationTokenSource cancellation = new();

		Assert.Throws<OperationCanceledException>(() =>
			new ModuleTransferInspectionService().InspectTransferredFiles(
				new CancellingReadOnlyList<string>(["unit.bin"], cancellation),
				[],
				cancellation.Token));
	}

	[Fact]
	public void Inspect_UsesOrdinalPathMatching()
	{
		IReadOnlyList<TransferredFilePresence> results =
			new ModuleTransferInspectionService().InspectTransferredFiles(
				["Unit.bin"],
				[new TransferredUnitFile("UnitA", "unit.bin", null, true)]);

		Assert.False(Assert.Single(results).Exists);
	}

	private sealed class CancellingReadOnlyList<T>(
		IReadOnlyList<T> items,
		CancellationTokenSource cancellation) : IReadOnlyList<T>
	{
		public int Count => items.Count;

		public T this[int index] => items[index];

		public IEnumerator<T> GetEnumerator()
		{
			foreach (T item in items)
			{
				yield return item;
				cancellation.Cancel();
			}
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
			GetEnumerator();
	}
}
