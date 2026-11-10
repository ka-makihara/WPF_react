using System.IO;
using System.Text;
using Xunit;
using WpfApp1_cmd.Services;

public class UpdateCommonService_WriteTests
{
	[Fact]
	public async Task WriteAsync_WritesUpdatedLinesWithoutChangingInput()
	{
		string directory = CreateTemporaryDirectory();
		try
		{
			string[] lines = ["[UnitA]", "Version=1.0"];
			var service = new UpdateCommonService(() => new DateTime(2026, 10, 27, 16, 0, 0));
			UpdateCommonService.SetVersion(lines, "UnitA", "2.0");
			string outputPath = Path.Combine(directory, "UpdateCommon.inf");

			await service.WriteAsync(lines, outputPath, "utf-8", false, CancellationToken.None);

			Assert.Equal(["[UnitA]", "Version=2.0"], await File.ReadAllLinesAsync(outputPath, Encoding.UTF8));
			Assert.Equal("Version=2.0", lines[1]);
		}
		finally
		{
			Directory.Delete(directory, recursive: true);
		}
	}

	[Fact]
	public async Task WriteAsync_AppendsInspectionTimestampOnlyWhenRequested()
	{
		string directory = CreateTemporaryDirectory();
		try
		{
			string outputPath = Path.Combine(directory, "UpdateCommon.inf");
			var service = new UpdateCommonService(() => new DateTime(2026, 10, 27, 16, 0, 0));

			await service.WriteAsync(["[UnitA]", "Version=1.0"], outputPath, "utf-8", true, CancellationToken.None);

			Assert.Equal(
				["[UnitA]", "Version=1.0", ";UpdateCommon.inf(2026/10/27 16:00:00)"],
				await File.ReadAllLinesAsync(outputPath, Encoding.UTF8));
		}
		finally
		{
			Directory.Delete(directory, recursive: true);
		}
	}

	[Fact]
	public async Task WriteAsync_DoesNotCreateDirectoryWhenAlreadyCancelled()
	{
		string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();
		string outputPath = Path.Combine(root, "nested", "UpdateCommon.inf");

		await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
			new UpdateCommonService().WriteAsync(
				["[UnitA]", "Version=1.0"],
				outputPath,
				"utf-8",
				appendInspectionTimestamp: true,
				cancellation.Token));

		Assert.False(Directory.Exists(root));
		Assert.False(File.Exists(outputPath));
	}

	[Fact]
	public async Task WriteAsync_CreatesNestedOutputDirectory()
	{
		string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
		try
		{
			string outputPath = Path.Combine(root, "nested", "UpdateCommon.inf");

			await new UpdateCommonService().WriteAsync(
				["[UnitA]", "Version=1.0"],
				outputPath,
				"utf-8",
				appendInspectionTimestamp: false,
				CancellationToken.None);

			Assert.Equal(
				["[UnitA]", "Version=1.0"],
				await File.ReadAllLinesAsync(outputPath, Encoding.UTF8));
		}
		finally
		{
			if (Directory.Exists(root))
			{
				Directory.Delete(root, recursive: true);
			}
		}
	}

	private static string CreateTemporaryDirectory()
	{
		string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(path);
		return path;
	}
}
