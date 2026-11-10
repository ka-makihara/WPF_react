using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class LcuPeripheralFileDownloadServiceTests
{
	[Fact]
	public async Task DownloadAsync_SetsTargetAndDownloadsWithExpectedPaths()
	{
		string targetRoot = Path.Combine(Path.GetTempPath(), $"LcuPeripheralDownload-{Guid.NewGuid():N}");
		string peripheralDirectory = "Fuji/System3/Program/Peripheral/";
		using CancellationTokenSource cancellation = new();
		var gateway = new FakeGateway();

		try
		{
			bool downloaded = await new LcuPeripheralFileDownloadService().DownloadAsync(
				new LcuPeripheralFileDownloadRequest(
					1,
					"machine-1",
					3,
					peripheralDirectory,
					"UpdateCommon.inf",
					targetRoot),
				gateway,
				cancellation.Token);

			Assert.True(downloaded);
			Assert.Equal(1, gateway.LcuId);
			Assert.Equal(cancellation.Token, gateway.SetToken);
			Assert.Equal("machine-1", gateway.MachineName);
			Assert.Equal(3, gateway.Position);
			Assert.Equal(peripheralDirectory + "UpdateCommon.inf", gateway.MachineRelativeFile);
			Assert.Equal("/MCFiles/" + peripheralDirectory + "UpdateCommon.inf", gateway.LcuRelativeFile);
			Assert.Equal(targetRoot, gateway.TargetDirectory);
			Assert.Equal(cancellation.Token, gateway.DownloadToken);
			Assert.True(Directory.Exists(targetRoot + peripheralDirectory));
		}
		finally
		{
			if (Directory.Exists(targetRoot))
			{
				Directory.Delete(targetRoot, recursive: true);
			}
		}
	}

	[Fact]
	public async Task DownloadAsync_DoesNotDownloadWhenLcuSelectionFails()
	{
		string targetRoot = Path.Combine(Path.GetTempPath(), $"LcuPeripheralDownload-{Guid.NewGuid():N}");
		var gateway = new FakeGateway
		{
			SetLcuException = new InvalidOperationException("Set LCU failed.")
		};

		await Assert.ThrowsAsync<InvalidOperationException>(() =>
			new LcuPeripheralFileDownloadService().DownloadAsync(
				new LcuPeripheralFileDownloadRequest(
					1,
					"machine-1",
					3,
					"Peripheral/",
					"UpdateCommon.inf",
					targetRoot),
				gateway,
				CancellationToken.None));

		Assert.Equal(0, gateway.DownloadCount);
		Assert.False(Directory.Exists(targetRoot));
	}

	[Fact]
	public async Task DownloadAsync_DoesNotCreateDirectoryWhenLcuSelectionCancels()
	{
		string targetRoot = Path.Combine(Path.GetTempPath(), $"LcuPeripheralDownload-{Guid.NewGuid():N}");
		using CancellationTokenSource cancellation = new();
		var gateway = new FakeGateway
		{
			OnSetLcu = () => cancellation.Cancel()
		};

		await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
			new LcuPeripheralFileDownloadService().DownloadAsync(
				new LcuPeripheralFileDownloadRequest(
					1,
					"machine-1",
					3,
					"Peripheral/",
					"UpdateCommon.inf",
					targetRoot),
				gateway,
				cancellation.Token));

		Assert.Equal(0, gateway.DownloadCount);
		Assert.False(Directory.Exists(targetRoot));
	}

	[Fact]
	public async Task DownloadAsync_PropagatesCancellationWhenDownloadReturnsFalse()
	{
		string targetRoot = Path.Combine(Path.GetTempPath(), $"LcuPeripheralDownload-{Guid.NewGuid():N}");
		using CancellationTokenSource cancellation = new();
		var gateway = new FakeGateway
		{
			DownloadResult = false,
			OnDownload = () => cancellation.Cancel()
		};

		try
		{
			await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
				new LcuPeripheralFileDownloadService().DownloadAsync(
					new LcuPeripheralFileDownloadRequest(
						1,
						"machine-1",
						3,
						"Peripheral/",
						"UpdateCommon.inf",
						targetRoot),
					gateway,
					cancellation.Token));
		}
		finally
		{
			if (Directory.Exists(targetRoot))
			{
				Directory.Delete(targetRoot, recursive: true);
			}
		}

		Assert.Equal(1, gateway.DownloadCount);
	}

	private sealed class FakeGateway : ILcuPeripheralFileDownloadGateway
	{
		public int LcuId { get; private set; }
		public CancellationToken SetToken { get; private set; }
		public string? MachineName { get; private set; }
		public int Position { get; private set; }
		public string? MachineRelativeFile { get; private set; }
		public string? LcuRelativeFile { get; private set; }
		public string? TargetDirectory { get; private set; }
		public CancellationToken DownloadToken { get; private set; }
		public int DownloadCount { get; private set; }
		public Exception? SetLcuException { get; init; }
		public bool DownloadResult { get; init; } = true;
		public Action? OnSetLcu { get; init; }
		public Action? OnDownload { get; init; }

		public Task SetLcuAsync(int lcuId, CancellationToken cancellationToken)
		{
			LcuId = lcuId;
			SetToken = cancellationToken;
			OnSetLcu?.Invoke();
			return SetLcuException == null
				? Task.CompletedTask
				: Task.FromException(SetLcuException);
		}

		public Task<bool> DownloadMachineFileAsync(
			string machineName,
			int position,
			string machineRelativeFile,
			string lcuRelativeFile,
			string targetDirectory,
			CancellationToken cancellationToken)
		{
			DownloadCount++;
			MachineName = machineName;
			Position = position;
			MachineRelativeFile = machineRelativeFile;
			LcuRelativeFile = lcuRelativeFile;
			TargetDirectory = targetDirectory;
			DownloadToken = cancellationToken;
			OnDownload?.Invoke();
			return Task.FromResult(DownloadResult);
		}
	}
}
