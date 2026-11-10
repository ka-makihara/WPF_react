using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class UpdateDataSourceService_PrepareArchiveTests
{
	[Fact]
	public async Task PrepareAsync_ReturnsArchiveNotFoundWithoutCheckingSpace()
	{
		int sizeChecks = 0;
		var service = CreateService(
			pathExists: _ => false,
			getFileSize: _ =>
			{
				sizeChecks++;
				return 10;
			});

		PeripheralArchivePreparationResult result =
			await service.PrepareArchiveAsync("data");

		Assert.Equal(PeripheralArchivePreparationOutcome.ArchiveNotFound, result.Outcome);
		Assert.Equal(0, sizeChecks);
	}

	[Fact]
	public async Task PrepareAsync_ReportsInsufficientDiskSpaceAndSkipsDecompression()
	{
		int decompressions = 0;
		var service = CreateService(
			availableDiskSpace: 9,
			decompress: _ =>
			{
				decompressions++;
				return 0;
			});

		PeripheralArchivePreparationResult result =
			await service.PrepareArchiveAsync("data");

		Assert.Equal(PeripheralArchivePreparationOutcome.InsufficientDiskSpace, result.Outcome);
		Assert.Equal(10, result.ArchiveSize);
		Assert.Equal(9, result.AvailableDiskSpace);
		Assert.Equal(0, decompressions);
	}

	[Fact]
	public async Task PrepareAsync_ReportsDecompressionFailure()
	{
		var service = CreateService(decompress: _ => -1);

		PeripheralArchivePreparationResult result =
			await service.PrepareArchiveAsync("data");

		Assert.Equal(PeripheralArchivePreparationOutcome.DecompressionFailed, result.Outcome);
	}

	[Fact]
	public async Task PrepareAsync_AllowsExactSpaceAndReportsSuccessfulDecompression()
	{
		string? decompressedPath = null;
		var service = CreateService(
			availableDiskSpace: 10,
			decompress: path =>
			{
				decompressedPath = path;
				return 0;
			});

		PeripheralArchivePreparationResult result =
			await service.PrepareArchiveAsync("data");

		Assert.Equal(PeripheralArchivePreparationOutcome.Decompressed, result.Outcome);
		Assert.Equal(Path.Combine("data", "Peripheral.bin"), decompressedPath);
	}

	[Fact]
	public async Task PrepareAsync_DoesNotDecompressWhenCancelledBeforeChecks()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();
		int decompressions = 0;
		UpdateDataSourceService service = CreateService(
			decompress: _ =>
			{
				decompressions++;
				return 0;
			});

		await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
			service.PrepareArchiveAsync("data", cancellation.Token));

		Assert.Equal(0, decompressions);
	}

	private static UpdateDataSourceService CreateService(
		Func<string, bool>? pathExists = null,
		Func<string, long>? getFileSize = null,
		long availableDiskSpace = 10,
		Func<string, int>? decompress = null) =>
		new(
			pathExists ?? (_ => true),
			getFileSize ?? (_ => 10),
			() => availableDiskSpace,
			decompress ?? (_ => 0));
}
