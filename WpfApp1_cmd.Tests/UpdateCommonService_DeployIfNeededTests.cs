using System.IO;
using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class UpdateCommonService_DeployIfNeededTests
{
	[Fact]
	public async Task DeployIfNeededAsync_SkipsDeploymentWhenNoFilesTransferred()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			var gateway = new FakeGateway();
			ModuleUpdateCommonDeploymentResult result = await CreateService().DeployIfNeededAsync(
				CreateRequest(root, transferredAtStart: 2, transferredAtEnd: 2, verify: true),
				gateway,
				CancellationToken.None);

			Assert.False(result.WasDeployed);
			Assert.True(result.UploadSucceeded);
			Assert.False(result.InspectionDownloadSucceeded);
			Assert.Null(result.Inspection);
			Assert.Equal(0, gateway.UploadCount);
			Assert.Equal(0, gateway.DownloadCount);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public async Task DeployIfNeededAsync_DeploysAndInspectsAfterSuccessfulDownload()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			var gateway = new FakeGateway
			{
				UploadResult = false,
				DownloadResult = true
			};
			ModuleUpdateCommonDeploymentResult result = await CreateService().DeployIfNeededAsync(
				CreateRequest(root, transferredAtStart: 1, transferredAtEnd: 2, verify: true),
				gateway,
				CancellationToken.None);

			Assert.True(result.WasDeployed);
			Assert.False(result.UploadSucceeded);
			Assert.True(result.InspectionDownloadSucceeded);
			Assert.NotNull(result.Inspection);
			Assert.True(result.Inspection.FileExists);
			Assert.Collection(
				result.Inspection.Entries,
				entry =>
				{
					Assert.Equal("UnitA", entry.Section);
					Assert.Equal(UpdateCommonVersionStatus.Matched, entry.Status);
				});
			Assert.Equal(1, gateway.UploadCount);
			Assert.Equal(1, gateway.DownloadCount);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public async Task DeployIfNeededAsync_ThrowsWhenCancelledEvenWhenNoDeploymentIsNeeded()
	{
		string root = CreateTemporaryDirectory();
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();
		try
		{
			var gateway = new FakeGateway();

			await Assert.ThrowsAsync<OperationCanceledException>(() =>
				CreateService().DeployIfNeededAsync(
					CreateRequest(root, transferredAtStart: 2, transferredAtEnd: 2, verify: true),
					gateway,
					cancellation.Token));

			Assert.Equal(0, gateway.UploadCount);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	private static UpdateCommonService CreateService() => new();

	private static ModuleUpdateCommonDeploymentRequest CreateRequest(
		string root,
		int transferredAtStart,
		int transferredAtEnd,
		bool verify) =>
		new(
			transferredAtStart,
			transferredAtEnd,
			new UpdateCommonDeploymentRequest(
				"machine-1",
				4,
				"Peripheral",
				["[UnitA]", "Version=2.0"],
				Path.Combine(root, "out"),
				Path.Combine(root, "inspection"),
				"UpdateCommon.inf",
				"utf-8",
				false,
				verify),
			[new UpdateCommonVersionUnit("UnitA", "2.0")]);

	private static string CreateTemporaryDirectory()
	{
		string path = Path.Combine(Path.GetTempPath(), $"ModuleUpdateCommonDeployment-{Guid.NewGuid():N}");
		Directory.CreateDirectory(path);
		return path;
	}

	private sealed class FakeGateway : IUpdateCommonDeploymentGateway
	{
		public bool UploadResult { get; init; } = true;
		public bool DownloadResult { get; init; }
		public int UploadCount { get; private set; }
		public int DownloadCount { get; private set; }

		public Task<bool> UploadAsync(
			string machineName,
			int position,
			string machineRelativePath,
			string lcuRelativePath,
			string localDirectory,
			CancellationToken cancellationToken)
		{
			UploadCount++;
			return Task.FromResult(UploadResult);
		}

		public Task<bool> DownloadAsync(
			string machineName,
			int position,
			string machineRelativePath,
			string lcuRelativePath,
			string localDirectory,
			CancellationToken cancellationToken)
		{
			DownloadCount++;
			if (DownloadResult)
			{
				string path = Path.Combine(
					localDirectory,
					machineRelativePath.Replace('/', Path.DirectorySeparatorChar));
				Directory.CreateDirectory(Path.GetDirectoryName(path)!);
				File.WriteAllLines(path, ["[UnitA]", "Version=2.0"]);
			}
			return Task.FromResult(DownloadResult);
		}
	}
}
