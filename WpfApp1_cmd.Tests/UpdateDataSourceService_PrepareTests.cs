using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class UpdateDataSourceService_PrepareTests
{
	[Fact]
	public async Task PrepareAsync_UsesDefaultDataFolderForArchiveAndUpdateInfo()
	{
		string applicationDirectory = Path.Combine("application", "root");
		string? archiveFolderChecked = null;
		var service = CreateService(pathExists: path =>
		{
			archiveFolderChecked = Path.GetDirectoryName(path);
			return false;
		});

		UpdateDataSourceResult result = await service.PrepareAsync(
			"",
			applicationDirectory,
			@"C:\DecompBin",
			Path.Combine("Fuji", "System3", "Program", "Peripheral"),
			"UpdateCommon.inf");

		string expectedDataFolder = Path.Combine(applicationDirectory, "Data");
		Assert.Equal(expectedDataFolder, archiveFolderChecked);
		Assert.Equal(expectedDataFolder, result.DataFolder);
		Assert.Equal(Path.Combine(expectedDataFolder, "Peripheral.bin"), result.ArchivePath);
		Assert.Equal(
			Path.Combine(
				expectedDataFolder,
				"Fuji",
				"System3",
				"Program",
				"Peripheral",
				"UpdateCommon.inf"),
			result.UpdateCommonPath);
	}

	[Fact]
	public async Task PrepareAsync_UsesExtractedFolderAfterSuccessfulDecompression()
	{
		string configuredFolder = Path.Combine("package", "input");
		var service = CreateService(decompress: _ => 0);

		UpdateDataSourceResult result = await service.PrepareAsync(
			configuredFolder,
			"application",
			@"C:\DecompBin",
			"Fuji\\Peripheral",
			"UpdateCommon.inf");

		Assert.Equal(@"C:\DecompBin", result.DataFolder);
		Assert.Equal(
			Path.Combine(@"C:\DecompBin", "Fuji\\Peripheral", "UpdateCommon.inf"),
			result.UpdateCommonPath);
	}

	[Fact]
	public async Task PrepareAsync_PreservesConfiguredFolderWhenDecompressionDoesNotSucceed()
	{
		string configuredFolder = Path.Combine("package", "input");
		var service = CreateService(decompress: _ => -1);

		UpdateDataSourceResult result = await service.PrepareAsync(
			configuredFolder,
			"application",
			@"C:\DecompBin",
			"Fuji\\Peripheral",
			"UpdateCommon.inf");

		Assert.Equal(configuredFolder, result.DataFolder);
	}

	private static UpdateDataSourceService CreateService(
		Func<string, bool>? pathExists = null,
		Func<string, int>? decompress = null) =>
		new(
			pathExists ?? (_ => true),
			_ => 512,
			() => 1024,
			decompress ?? (_ => 0));
}
