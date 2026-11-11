using System.IO;
using System.Text;
using WpfApp1_cmd.Services;
using Xunit;

public class UpdateCommonService_DeployTests
{
	[Fact]
	public async Task DeployAsync_WritesAndUploadsFileThenDownloadsForInspection()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			string localRoot = Path.Combine(root, "out");
			string inspectionRoot = Path.Combine(root, "inspect");
			var gateway = new FakeDeploymentGateway
			{
				UploadResult = true,
				DownloadResult = true
			};
			var service = new UpdateCommonService(() => new DateTime(2026, 10, 27, 16, 0, 0));
			var request = CreateRequest(localRoot, inspectionRoot, verifyAfterUpload: true);

			UpdateCommonDeploymentResult result = await service.DeployAsync(
				request,
				gateway,
				CancellationToken.None);

			Assert.True(result.UploadSucceeded);
			Assert.True(result.InspectionDownloadSucceeded);
			Assert.Equal(Path.Combine(localRoot, "UpdateCommon.inf"), result.LocalFilePath);
			Assert.Equal(
				Path.Combine(inspectionRoot, "Fuji", "System3", "Program", "Peripheral", "UpdateCommon.inf"),
				result.InspectionFilePath);
			Assert.Equal("machine-1", gateway.UploadMachineName);
			Assert.Equal("192.168.0.1", gateway.UploadIpAddress);
			Assert.Equal(4, gateway.UploadPosition);
			Assert.Equal("Fuji/System3/Program/Peripheral/UpdateCommon.inf", gateway.UploadMachinePath);
			Assert.Equal("UpdateCommon.inf", gateway.UploadLcuPath);
			Assert.Equal(localRoot, gateway.UploadLocalDirectory);
			Assert.Equal("machine-1", gateway.DownloadMachineName);
			Assert.Equal("192.168.0.1", gateway.DownloadIpAddress);
			Assert.Equal("Fuji/System3/Program/Peripheral/UpdateCommon.inf", gateway.DownloadMachinePath);
			Assert.Equal("UpdateCommon.inf", gateway.DownloadLcuPath);
			Assert.Equal(
				["[UnitA]", "Version=2.0", ";UpdateCommon.inf(2026/10/27 16:00:00)"],
				await File.ReadAllLinesAsync(result.LocalFilePath, Encoding.UTF8));
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public async Task DeployAsync_SkipsInspectionWhenDisabledAndReturnsUploadFailure()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			var gateway = new FakeDeploymentGateway { UploadResult = false };
			UpdateCommonDeploymentResult result = await new UpdateCommonService()
				.DeployAsync(
					CreateRequest(
						Path.Combine(root, "out"),
						Path.Combine(root, "inspect"),
						verifyAfterUpload: false),
					gateway,
					CancellationToken.None);

			Assert.False(result.UploadSucceeded);
			Assert.False(result.InspectionDownloadSucceeded);
			Assert.Equal(1, gateway.UploadCount);
			Assert.Equal(0, gateway.DownloadCount);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public async Task DeployAsync_PropagatesCancellationWhenUploadReturnsFalse()
	{
		string root = CreateTemporaryDirectory();
		using CancellationTokenSource cancellation = new();
		try
		{
			var gateway = new FakeDeploymentGateway
			{
				UploadResult = false,
				OnUpload = () => cancellation.Cancel()
			};

			await Assert.ThrowsAsync<OperationCanceledException>(() =>
				new UpdateCommonService()
					.DeployAsync(
						CreateRequest(
							Path.Combine(root, "out"),
							Path.Combine(root, "inspect"),
							verifyAfterUpload: true),
						gateway,
						cancellation.Token));

			Assert.Equal(0, gateway.DownloadCount);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public async Task DeployAsync_PropagatesCancellationWhenInspectionDownloadReturnsFalse()
	{
		string root = CreateTemporaryDirectory();
		using CancellationTokenSource cancellation = new();
		try
		{
			var gateway = new FakeDeploymentGateway
			{
				UploadResult = true,
				DownloadResult = false,
				OnDownload = () => cancellation.Cancel()
			};

			await Assert.ThrowsAsync<OperationCanceledException>(() =>
				new UpdateCommonService()
					.DeployAsync(
						CreateRequest(
							Path.Combine(root, "out"),
							Path.Combine(root, "inspect"),
							verifyAfterUpload: true),
						gateway,
						cancellation.Token));
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	private static UpdateCommonDeploymentRequest CreateRequest(
		string localRoot,
		string inspectionRoot,
		bool verifyAfterUpload) =>
		new(
			"machine-1",
			"192.168.0.1",
			4,
			"Fuji/System3/Program/Peripheral",
			["[UnitA]", "Version=2.0"],
			localRoot,
			inspectionRoot,
			"UpdateCommon.inf",
			"utf-8",
			true,
			verifyAfterUpload);

	private static string CreateTemporaryDirectory()
	{
		string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(path);
		return path;
	}

	private sealed class FakeDeploymentGateway : IUpdateCommonDeploymentGateway
	{
		public bool UploadResult { get; init; }
		public bool DownloadResult { get; init; }
		public Action? OnUpload { get; init; }
		public Action? OnDownload { get; init; }
		public int UploadCount { get; private set; }
		public int DownloadCount { get; private set; }
		public string? UploadMachineName { get; private set; }
		public string? UploadIpAddress { get; private set; }
		public int UploadPosition { get; private set; }
		public string? UploadMachinePath { get; private set; }
		public string? UploadLcuPath { get; private set; }
		public string? UploadLocalDirectory { get; private set; }
		public string? DownloadMachineName { get; private set; }
		public string? DownloadIpAddress { get; private set; }
		public string? DownloadMachinePath { get; private set; }
		public string? DownloadLcuPath { get; private set; }

		public Task<bool> UploadAsync(
			string machineName,
			string IpAddress,
			int position,
			string machineRelativePath,
			string lcuRelativePath,
			string localDirectory,
			CancellationToken cancellationToken)
		{
			UploadCount++;
			UploadMachineName = machineName;
			UploadIpAddress = IpAddress;
			UploadPosition = position;
			UploadMachinePath = machineRelativePath;
			UploadLcuPath = lcuRelativePath;
			UploadLocalDirectory = localDirectory;
			OnUpload?.Invoke();
			return Task.FromResult(UploadResult);
		}

		public Task<bool> DownloadAsync(
			string machineName,
			string IpAddress,
			int position,
			string machineRelativePath,
			string lcuRelativePath,
			string localDirectory,
			CancellationToken cancellationToken)
		{
			DownloadCount++;
			DownloadMachineName = machineName;
			DownloadIpAddress = IpAddress;
			DownloadMachinePath = machineRelativePath;
			DownloadLcuPath = lcuRelativePath;
			OnDownload?.Invoke();
			return Task.FromResult(DownloadResult);
		}
	}
}
