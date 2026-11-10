using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class UpdateDataLoadService_SizeTests
{
	[Fact]
	public void GetDirectorySize_IncludesFilesInNestedDirectories()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			Directory.CreateDirectory(Path.Combine(root, "nested"));
			File.WriteAllBytes(Path.Combine(root, "root.bin"), [1, 2, 3]);
			File.WriteAllBytes(Path.Combine(root, "nested", "child.bin"), [4, 5]);

			long size = UpdateDataLoadService.GetDirectorySize(new DirectoryInfo(root));

			Assert.Equal(5, size);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void GetPeripheralSize_UsesPeripheralArchiveSizeWhenPresent()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			File.WriteAllBytes(Path.Combine(root, "Peripheral.bin"), [1, 2, 3, 4]);
			File.WriteAllBytes(Path.Combine(root, "other.bin"), [5, 6]);

			long size = UpdateDataLoadService.GetPeripheralSize(root);

			Assert.Equal(4, size);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void GetPeripheralSize_UsesDirectorySizeWhenArchiveIsMissing()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			File.WriteAllBytes(Path.Combine(root, "update.bin"), [1, 2, 3]);

			long size = UpdateDataLoadService.GetPeripheralSize(root);

			Assert.Equal(3, size);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void GetDirectorySize_ThrowsWhenCancelledBeforeTraversal()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			using CancellationTokenSource cancellation = new();
			cancellation.Cancel();

			Assert.Throws<OperationCanceledException>(() =>
				UpdateDataLoadService.GetDirectorySize(
					new DirectoryInfo(root),
					cancellation.Token));
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	private static string CreateTemporaryDirectory()
	{
		string path = Path.Combine(Path.GetTempPath(), $"UpdateDataSizeServiceTests-{Guid.NewGuid():N}");
		Directory.CreateDirectory(path);
		return path;
	}
}
