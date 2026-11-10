using System.IO;
using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class ModuleBackupService_ExecuteModuleTests
{
	[Fact]
	public async Task ExecuteAsync_ThrowsCancellationWhenUpdateInfoLoadReturnsFalseAfterCancellation()
	{
		using CancellationTokenSource cancellation = new();
		bool requestCreated = false;

		await Assert.ThrowsAsync<OperationCanceledException>(() =>
			CreateService().ExecuteModuleAsync(
				_ =>
				{
					cancellation.Cancel();
					return Task.FromResult(false);
				},
				() =>
				{
					requestCreated = true;
					return CreateRequest(Path.GetTempPath());
				},
				new FakeGateway(),
				cancellation.Token));

		Assert.False(requestCreated);
	}

	[Fact]
	public async Task ExecuteAsync_DoesNotCreateRequestWhenUpdateInfoIsUnavailable()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			bool requestCreated = false;
			var gateway = new FakeGateway();
			ModuleBackupExecutionOutcome result = await CreateService().ExecuteModuleAsync(
				_ => Task.FromResult(false),
				() =>
				{
					requestCreated = true;
					return CreateRequest(root);
				},
				gateway,
				CancellationToken.None);

			Assert.Equal(ModuleBackupExecutionOutcome.UpdateInfoUnavailable, result);
			Assert.False(requestCreated);
			Assert.Equal(0, gateway.CreateFoldersCount);
			Assert.Equal(0, gateway.DownloadCount);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Theory]
	[InlineData(true, true, ModuleBackupExecutionOutcome.Succeeded)]
	[InlineData(false, true, ModuleBackupExecutionOutcome.FolderCreationFailed)]
	[InlineData(true, false, ModuleBackupExecutionOutcome.DownloadFailed)]
	public async Task ExecuteAsync_MapsBackupOutcome(
		bool createFoldersResult,
		bool downloadResult,
		ModuleBackupExecutionOutcome expected)
	{
		string root = CreateTemporaryDirectory();
		try
		{
			var gateway = new FakeGateway
			{
				CreateFoldersResult = createFoldersResult,
				DownloadResult = downloadResult
			};
			ModuleBackupExecutionOutcome result = await CreateService().ExecuteModuleAsync(
				_ => Task.FromResult(true),
				() => CreateRequest(root),
				gateway,
				CancellationToken.None);

			Assert.Equal(expected, result);
			Assert.Equal(1, gateway.CreateFoldersCount);
			Assert.Equal(createFoldersResult ? 1 : 0, gateway.DownloadCount);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	private static ModuleBackupService CreateService() => new(() => new DateTime(2026, 10, 28, 11, 0, 0));

	private static ModuleBackupRequest CreateRequest(string backupRoot) =>
		new(
			"LineA",
			"MachineA",
			"ModuleA",
			1,
			backupRoot,
			["/Peripheral/program.bin"],
			"/Peripheral/UpdateCommon.inf",
			"/MCFiles/",
			"utf-8");

	private static string CreateTemporaryDirectory()
	{
		string path = Path.Combine(Path.GetTempPath(), $"ModuleBackupExecution-{Guid.NewGuid():N}");
		Directory.CreateDirectory(path);
		return path;
	}

	private sealed class FakeGateway : IModuleBackupGateway
	{
		public bool CreateFoldersResult { get; init; } = true;
		public bool DownloadResult { get; init; } = true;
		public int CreateFoldersCount { get; private set; }
		public int DownloadCount { get; private set; }

		public Task<bool> CreateFoldersAsync(
			IReadOnlyList<string> folders,
			string lcuRoot,
			CancellationToken cancellationToken)
		{
			CreateFoldersCount++;
			return Task.FromResult(CreateFoldersResult);
		}

		public async Task<bool> DownloadFilesAsync(
			string machineName,
			int position,
			IReadOnlyList<string> files,
			string localPath,
			CancellationToken cancellationToken)
		{
			DownloadCount++;
			if (DownloadResult)
			{
				string updateCommonPath = Path.Combine(localPath, "Peripheral", "UpdateCommon.inf");
				Directory.CreateDirectory(Path.GetDirectoryName(updateCommonPath)!);
				await File.WriteAllLinesAsync(
					updateCommonPath,
					["[UnitA]", "Version=1.0"],
					cancellationToken);
			}
			return DownloadResult;
		}
	}
}
