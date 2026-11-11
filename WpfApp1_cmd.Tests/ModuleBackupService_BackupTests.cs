using System.IO;
using System.Text;
using Xunit;
using WpfApp1_cmd.Services;

public class ModuleBackupService_BackupTests
{
	[Fact]
	public async Task BackupAsync_CreatesPlanDownloadsFilesAndAddsBackupMetadata()
	{
		string backupPath = CreateTemporaryDirectory();
		try
		{
			var gateway = new FakeModuleBackupGateway();
			var service = new ModuleBackupService(() => new DateTime(2026, 10, 27, 16, 0, 0));
			var request = CreateRequest(backupPath);

			ModuleBackupResult result = await service.BackupAsync(request, gateway, CancellationToken.None);

			Assert.Equal(ModuleBackupResult.Succeeded, result);
			Assert.Equal(
				["Fuji/Data", "Fuji/System3/Program/Peripheral"],
				gateway.CreatedFolders);
			Assert.Equal(
				[
					"Fuji/Data/unit.dat",
					"Fuji/System3/Program/Peripheral/UpdateCommon.inf"
				],
				gateway.DownloadedFiles);
			Assert.Equal("machine-1", gateway.MachineName);
			Assert.Equal("192.168.0.1", gateway.IpAddress);
			Assert.Equal(3, gateway.Position);
			Assert.Equal(
				[
					"#UpdateInfo=lcu-1;machine-1;module-1;2026/10/27 16:00:00",
					"[Unit]",
					"Path=Fuji/Data/unit.dat"
				],
				await File.ReadAllLinesAsync(Path.Combine(
					backupPath,
					"Fuji",
					"System3",
					"Program",
					"Peripheral",
					"UpdateCommon.inf"),
					Encoding.UTF8));
		}
		finally
		{
			Directory.Delete(backupPath, recursive: true);
		}
	}

	[Fact]
	public async Task BackupAsync_DoesNotDownloadWhenFolderCreationFails()
	{
		string backupPath = CreateTemporaryDirectory();
		try
		{
			var gateway = new FakeModuleBackupGateway
			{
				CreateFoldersResult = false
			};

			ModuleBackupResult result = await new ModuleBackupService()
				.BackupAsync(CreateRequest(backupPath), gateway, CancellationToken.None);

			Assert.Equal(ModuleBackupResult.FolderCreationFailed, result);
			Assert.Empty(gateway.DownloadedFiles);
		}
		finally
		{
			Directory.Delete(backupPath, recursive: true);
		}
	}

	[Fact]
	public async Task BackupAsync_DoesNotCreateFoldersWhenAlreadyCancelled()
	{
		string backupPath = Path.Combine(
			Path.GetTempPath(),
			$"ModuleBackup-{Guid.NewGuid():N}");
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();
		var gateway = new FakeModuleBackupGateway();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
			new ModuleBackupService().BackupAsync(
				CreateRequest(backupPath),
				gateway,
				cancellation.Token));

		Assert.Empty(gateway.CreatedFolders);
		Assert.False(Directory.Exists(backupPath));
	}

	[Fact]
	public async Task BackupAsync_PropagatesCancellationWhenFolderCreationReturnsFalse()
	{
		string backupPath = CreateTemporaryDirectory();
		using CancellationTokenSource cancellation = new();
		var gateway = new FakeModuleBackupGateway
		{
			CreateFoldersResult = false,
			OnCreateFolders = () => cancellation.Cancel()
		};

		try
		{
			await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
				new ModuleBackupService().BackupAsync(
					CreateRequest(backupPath),
					gateway,
					cancellation.Token));

			Assert.Empty(gateway.DownloadedFiles);
		}
		finally
		{
			Directory.Delete(backupPath, recursive: true);
		}
	}

	[Fact]
	public async Task BackupAsync_PropagatesCancellationWhenDownloadReturnsFalse()
	{
		string backupPath = CreateTemporaryDirectory();
		using CancellationTokenSource cancellation = new();
		var gateway = new FakeModuleBackupGateway
		{
			DownloadResult = false,
			OnDownload = () => cancellation.Cancel()
		};

		try
		{
			await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
				new ModuleBackupService().BackupAsync(
					CreateRequest(backupPath),
					gateway,
					cancellation.Token));
		}
		finally
		{
			Directory.Delete(backupPath, recursive: true);
		}
	}

	private static ModuleBackupRequest CreateRequest(string backupPath) =>
		new(
			"lcu-1",
			"machine-1",
			"module-1",
			"192.168.0.1",
			3,
			backupPath,
			["Fuji/Data/unit.dat"],
			"/Fuji/System3/Program/Peripheral/UpdateCommon.inf",
			"/MCFiles/",
			"utf-8");

	private static string CreateTemporaryDirectory()
	{
		string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(path);
		return path;
	}

	private sealed class FakeModuleBackupGateway : IModuleBackupGateway
	{
		public bool CreateFoldersResult { get; init; } = true;
		public bool DownloadResult { get; init; } = true;
		public Action? OnCreateFolders { get; init; }
		public Action? OnDownload { get; init; }
		public List<string> CreatedFolders { get; private set; } = [];
		public List<string> DownloadedFiles { get; private set; } = [];
		public string? MachineName { get; private set; }
		public string? IpAddress { get; private set; }
		public int Position { get; private set; }

		public Task<bool> CreateFoldersAsync(
			IReadOnlyList<string> folders,
			string lcuRoot,
			CancellationToken cancellationToken)
		{
			CreatedFolders = folders.ToList();
			OnCreateFolders?.Invoke();
			return Task.FromResult(CreateFoldersResult);
		}

		public async Task<bool> DownloadFilesAsync(
			string machineName,
			string IpAddress,
			int position,
			IReadOnlyList<string> files,
			string localPath,
			CancellationToken cancellationToken)
		{
			MachineName = machineName;
			this.IpAddress = IpAddress;
			Position = position;
			DownloadedFiles = files.ToList();
			OnDownload?.Invoke();
			if (!DownloadResult)
			{
				return false;
			}

			foreach (string file in files)
			{
				string localFile = Path.Combine(
					localPath,
					file.Replace('/', Path.DirectorySeparatorChar));
				Directory.CreateDirectory(Path.GetDirectoryName(localFile)!);
				await File.WriteAllLinesAsync(
					localFile,
					["[Unit]", "Path=Fuji/Data/unit.dat"],
					Encoding.UTF8,
					cancellationToken);
			}

			return DownloadResult;
		}
	}
}
