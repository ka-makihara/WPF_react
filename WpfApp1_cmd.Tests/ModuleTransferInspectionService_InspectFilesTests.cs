using System.IO;
using Xunit;
using WpfApp1_cmd.Services;

namespace WpfApp1_cmd.Tests;

public sealed class ModuleTransferInspectionService_InspectFilesTests
{
	[Fact]
	public void Inspect_IdenticalFile_ReturnsSuccessAndMatchingUnit()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			string sourceRoot = Path.Combine(root, "source");
			string inspectionRoot = Path.Combine(root, "inspection");
			string relativeFile = "\\Fuji\\Peripheral\\unit\\program.bin";
			WriteFile(sourceRoot + relativeFile, [1, 2, 3]);
			WriteFile(inspectionRoot + relativeFile, [1, 2, 3]);

			var service = new ModuleTransferInspectionService();
			IReadOnlyList<ModuleFileInspectionResult> results = service.InspectFiles(
				[relativeFile],
				[new ModuleInspectionUnit("UnitA", "\\Fuji\\Peripheral\\unit", null)],
				sourceRoot,
				inspectionRoot);

			Assert.Single(results);
			Assert.Equal("UnitA", results[0].UnitName);
			Assert.True(results[0].IsMatch);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void Inspect_DifferentOrMissingFile_ReturnsFailure()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			string sourceRoot = Path.Combine(root, "source");
			string inspectionRoot = Path.Combine(root, "inspection");
			string relativeFile = "\\Fuji\\Peripheral\\unit\\program.bin";
			WriteFile(sourceRoot + relativeFile, [1, 2, 3]);
			WriteFile(inspectionRoot + relativeFile, [1, 2, 4]);

			var service = new ModuleTransferInspectionService();
			IReadOnlyList<ModuleFileInspectionResult> different = service.InspectFiles(
				[relativeFile],
				[],
				sourceRoot,
				inspectionRoot);
			IReadOnlyList<ModuleFileInspectionResult> missing = service.InspectFiles(
				["\\Fuji\\Peripheral\\unit\\missing.bin"],
				[],
				sourceRoot,
				inspectionRoot);

			Assert.False(different[0].IsMatch);
			Assert.False(missing[0].IsMatch);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void CompareFiles_ReturnsFirstDifferentBytePosition()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			string first = Path.Combine(root, "first.bin");
			string second = Path.Combine(root, "second.bin");
			File.WriteAllBytes(first, [1, 2, 3, 4]);
			File.WriteAllBytes(second, [1, 2, 9, 4]);

			Assert.Equal(3, ModuleTransferInspectionService.CompareFiles(first, second));
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void CompareFiles_ThrowsWhenCancelledBeforeReading()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			string first = Path.Combine(root, "first.bin");
			string second = Path.Combine(root, "second.bin");
			File.WriteAllBytes(first, [1]);
			File.WriteAllBytes(second, [1]);
			using CancellationTokenSource cancellation = new();
			cancellation.Cancel();

			Assert.Throws<OperationCanceledException>(() =>
				ModuleTransferInspectionService.CompareFiles(
					first,
					second,
					cancellation.Token));
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void Inspect_StopsWhilePreparingUnitPathsWhenCancelled()
	{
		using CancellationTokenSource cancellation = new();
		IReadOnlyList<ModuleInspectionUnit> units = new CancellingUnitList(
			() => cancellation.Cancel());

		Assert.Throws<OperationCanceledException>(() =>
			new ModuleTransferInspectionService().InspectFiles(
				[],
				units,
				@"C:\Data\",
				@"C:\Inspection\",
				cancellation.Token));
	}

	private static string CreateTemporaryDirectory()
	{
		string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(path);
		return path;
	}

	private static void WriteFile(string path, byte[] contents)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		File.WriteAllBytes(path, contents);
	}

	private sealed class CancellingUnitList(Action onFirstRead) : IReadOnlyList<ModuleInspectionUnit>
	{
		public int Count => 1;

		public ModuleInspectionUnit this[int index]
		{
			get
			{
				onFirstRead();
				return new("UnitA", @"\Peripheral\UnitA", null);
			}
		}

		public IEnumerator<ModuleInspectionUnit> GetEnumerator()
		{
			yield return this[0];
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
			GetEnumerator();
	}
}
