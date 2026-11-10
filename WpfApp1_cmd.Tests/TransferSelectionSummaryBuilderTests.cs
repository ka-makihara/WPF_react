using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class TransferSelectionSummaryBuilderTests
{
	[Fact]
	public void CalculateFtpTransferSize_MultipliesDataSizeBySelectedLcuCount()
	{
		TransferSelectionLcu[] lcus =
		[
			new(true, []),
			new(false, []),
			new(true, [new(false, [])]),
			new(false, [])
		];

		long result = new TransferSelectionSummaryBuilder().CalculateFtpTransferSize(
			100,
			lcus);

		Assert.Equal(200, result);
	}

	[Fact]
	public void CalculateFtpTransferSize_ReturnsZeroWhenNoLcuIsSelected()
	{
		long result = new TransferSelectionSummaryBuilder().CalculateFtpTransferSize(
			100,
			[new(false, [])]);

		Assert.Equal(0, result);
	}

	[Fact]
	public void CalculateFtpTransferSize_MultipliesDataSizeBySelectedLcuCountValue()
	{
		long result = new TransferSelectionSummaryBuilder().CalculateFtpTransferSize(100, 3);

		Assert.Equal(300, result);
	}

	[Fact]
	public void CalculateFtpTransferSize_RejectsNegativeSelectedLcuCount()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() =>
			new TransferSelectionSummaryBuilder().CalculateFtpTransferSize(100, -1));
	}

	[Fact]
	public void CountSelectedUnits_CountsSelectedUnitsUnderPartiallySelectedParents()
	{
		TransferSelectionLcu[] lcus =
		[
			new(true,
			[
				new(true,
				[
					new(true,
					[
						new(true, "UnitA", "a.bin", null),
						new(false, "UnitB", "b.bin", null)
					]),
					new(false,
					[
						new(true, "UnitC", "c.bin", null)
					])
				]),
				new(false,
				[
					new(true, [new(true, "UnitD", "d.bin", null)])
				])
			]),
			new(false,
			[
				new(true,
				[
					new(true, [new(true, "UnitE", "e.bin", null)])
				])
			])
		];

		int result = new TransferSelectionSummaryBuilder().CountSelectedUnits(lcus);

		Assert.Equal(1, result);
	}

	[Fact]
	public void CountSelectedUnits_ThrowsWhenCancelledWhileEnumeratingMachines()
	{
		using CancellationTokenSource cancellation = new();
		TransferSelectionLcu[] lcus =
		[
			new(
				true,
				new CancellingReadOnlyList<TransferSelectionMachine>(
					[new(false, [])],
					cancellation))
		];

		Assert.Throws<OperationCanceledException>(() =>
			new TransferSelectionSummaryBuilder().CountSelectedUnits(
				lcus,
				cancellation.Token));
	}

	[Fact]
	public void Build_CountsSelectedHierarchyAndCollectsUniqueFileAndFuserPaths()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			TransferSelectionLcu[] lcus =
			[
				new(true,
				[
					new(true,
					[
						new(true,
						[
							new(true, "UnitA", "\\Fuji\\Peripheral\\unit.bin", "\\Fuji\\Peripheral\\fuser.bin"),
							new(false, "UnitB", "\\Fuji\\Peripheral\\ignored.bin", null)
						]),
						new(false, [new(true, "UnitC", "\\Fuji\\Peripheral\\ignored2.bin", null)])
					])
				]),
				new(false, [new(true, [new(true, [new(true, "UnitD", "ignored.bin", null)])])])
			];

			TransferSelectionSummary result = new TransferSelectionSummaryBuilder().Build(
				lcus,
				[],
				root);

			Assert.Equal(1, result.LcuCount);
			Assert.Equal(1, result.MachineCount);
			Assert.Equal(1, result.ModuleCount);
			Assert.Equal(1, result.UnitCount);
			Assert.Equal(2, result.FileCount);
			Assert.Equal(
				[
					root + "\\Fuji\\Peripheral\\unit.bin",
					root + "\\Fuji\\Peripheral\\fuser.bin"
				],
				result.Paths);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void Build_ExpandsFolderUnitsAndCountsDuplicateFolderFilesOnce()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			string folder = Path.Combine(root, "Fuji", "Peripheral", "folder");
			Directory.CreateDirectory(folder);
			string firstFile = Path.Combine(folder, "first.bin");
			string secondFile = Path.Combine(folder, "second.bin");
			File.WriteAllText(firstFile, "1");
			File.WriteAllText(secondFile, "2");
			TransferSelectionLcu[] lcus =
			[
				new(true,
				[
					new(true,
					[
						new(true,
						[
							new(true, "FolderA", "\\Fuji\\Peripheral\\folder\\first.bin", null),
							new(true, "FolderB", "\\Fuji\\Peripheral\\folder\\second.bin", null)
						])
					])
				])
			];

			TransferSelectionSummary result = new TransferSelectionSummaryBuilder().Build(
				lcus,
				[new(["FolderA", "FolderB"], "Folder")],
				root);

			Assert.Equal(2, result.UnitCount);
			Assert.Equal(2, result.FileCount);
			Assert.Equal(2, result.Paths.Count);
			Assert.Contains(firstFile, result.Paths);
			Assert.Contains(secondFile, result.Paths);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void Build_UsesFirstConfiguredGroupForDuplicateComponents()
	{
		TransferSelectionLcu[] lcus =
		[
			new(true,
			[
				new(true,
				[
					new(true,
					[
						new(true, "UnitA", "unit.bin", null)
					])
				])
			])
		];
		TransferSelectionGroup[] groups =
		[
			new(["UnitA"], "File"),
			new(["UnitA"], "Folder")
		];

		TransferSelectionSummary summary =
			new TransferSelectionSummaryBuilder().Build(lcus, groups, @"C:\data\");

		Assert.Equal(1, summary.FileCount);
		Assert.Equal([@"C:\data\unit.bin"], summary.Paths);
	}

	[Fact]
	public void Build_DeduplicatesPathsButKeepsRegularFileCountForDuplicates()
	{
		TransferSelectionLcu[] lcus =
		[
			new(true,
			[
				new(true,
				[
					new(true,
					[
						new(true, "UnitA", "unit.bin", "fuser.bin"),
						new(true, "UnitB", "unit.bin", "fuser.bin")
					])
				])
			])
		];

		TransferSelectionSummary summary =
			new TransferSelectionSummaryBuilder().Build(lcus, [], @"C:\data\");

		Assert.Equal(4, summary.FileCount);
		Assert.Equal([@"C:\data\unit.bin", @"C:\data\fuser.bin"], summary.Paths);
	}

	[Fact]
	public void Build_ThrowsWhenCancelledBeforeBuilding()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		Assert.Throws<OperationCanceledException>(() =>
			new TransferSelectionSummaryBuilder().Build(
				[],
				[],
				@"C:\data\",
				cancellation.Token));
	}

	[Fact]
	public void Build_ThrowsWhenCancelledWhileEnumeratingMachines()
	{
		using CancellationTokenSource cancellation = new();
		TransferSelectionLcu[] lcus =
		[
			new(
				true,
				new CancellingReadOnlyList<TransferSelectionMachine>(
					[new(false, [])],
					cancellation))
		];

		Assert.Throws<OperationCanceledException>(() =>
			new TransferSelectionSummaryBuilder().Build(
				lcus,
				[],
				@"C:\data\",
				cancellation.Token));
	}

	[Fact]
	public void Build_ThrowsWhenCancelledWhileEnumeratingModules()
	{
		using CancellationTokenSource cancellation = new();
		TransferSelectionLcu[] lcus =
		[
			new(
				true,
				[
					new(
						true,
						new CancellingReadOnlyList<TransferSelectionModule>(
							[new(false, [])],
							cancellation))
				])
		];

		Assert.Throws<OperationCanceledException>(() =>
			new TransferSelectionSummaryBuilder().Build(
				lcus,
				[],
				@"C:\data\",
				cancellation.Token));
	}

	private static string CreateTemporaryDirectory()
	{
		string path = Path.Combine(Path.GetTempPath(), $"TransferSelectionSummaryBuilderTests-{Guid.NewGuid():N}");
		Directory.CreateDirectory(path);
		return path;
	}

	private sealed class CancellingReadOnlyList<T>(
		IReadOnlyList<T> items,
		CancellationTokenSource cancellation) : IReadOnlyList<T>
	{
		public int Count => items.Count;

		public T this[int index]
		{
			get
			{
				cancellation.Cancel();
				return items[index];
			}
		}

		public IEnumerator<T> GetEnumerator()
		{
			cancellation.Cancel();
			return items.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
			GetEnumerator();
	}
}
