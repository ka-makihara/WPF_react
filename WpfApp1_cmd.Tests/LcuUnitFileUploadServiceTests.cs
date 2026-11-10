using System.IO;
using WpfApp1_cmd.Services;
using WpfLcuCtrlLib;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class LcuUnitFileUploadServiceTests
{
	[Fact]
	public async Task UploadSelectedAsync_SelectsFoldersAndUploadsThroughFolderService()
	{
		string root = Path.Combine(Path.GetTempPath(), $"LcuUnitUpload-{Guid.NewGuid():N}");
		string unitFolder = root + @"\Fuji\Peripheral\UnitA";
		Directory.CreateDirectory(unitFolder);
		await File.WriteAllTextAsync(Path.Combine(unitFolder, "program.bin"), "payload");
		var client = new FakeFileTransferClient();

		try
		{
			LcuUnitFileUploadResult result = await CreateService(client).UploadSelectedAsync(
				"lcu-host",
				"user",
				"password",
				[
					new UpdateFolderMachine(true,
					[
						new UpdateFolderModule(true,
						[
							new UpdateFolderUnit(
								true,
								@"\Fuji\Peripheral\UnitA\program.bin",
								"")
						])
					])
				],
				[new UpdateFolderFileInfo(@"\Fuji\Peripheral\UnitA\program.bin", null)],
				root,
				"/MCFiles",
				_ => Task.CompletedTask,
				_ => Task.FromResult(true),
				CancellationToken.None);

			Assert.Equal(LcuUnitFileUploadOutcome.Succeeded, result.Outcome);
			string folder = Assert.Single(result.Folders);
			Assert.Equal(unitFolder, folder);
			Assert.Equal(folder, Assert.Single(client.Session.UploadedLocalFolders));
			Assert.Equal("/MCFiles" + @"\Fuji\Peripheral\UnitA", Assert.Single(client.Session.UploadedRemoteFolders));
			Assert.True(client.Session.Disconnected);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public async Task UploadAsync_ReturnsExpectedFailureForTransferErrors()
	{
		var client = new FakeFileTransferClient { ConnectException = new IOException("connection failed") };

		LcuUnitFileUploadResult result = await CreateService(client).UploadAsync(
			"lcu-host",
			"user",
			"password",
			[],
			Path.GetTempPath(),
			"/MCFiles",
			_ => Task.CompletedTask,
			_ => Task.FromResult(true),
			CancellationToken.None);

		Assert.Equal(LcuUnitFileUploadOutcome.Failed, result.Outcome);
		Assert.IsType<IOException>(result.Error);
	}

	[Fact]
	public async Task UploadAsync_ReportsCancelledWhenConnectFailsAfterCancellation()
	{
		using CancellationTokenSource cancellation = new();
		var client = new FakeFileTransferClient
		{
			ConnectException = new IOException("connection cancelled"),
			OnConnect = () => cancellation.Cancel()
		};

		LcuUnitFileUploadResult result = await CreateService(client).UploadAsync(
			"lcu-host",
			"user",
			"password",
			[],
			Path.GetTempPath(),
			"/MCFiles",
			_ => Task.CompletedTask,
			_ => Task.FromResult(true),
			cancellation.Token);

		Assert.Equal(LcuUnitFileUploadOutcome.Cancelled, result.Outcome);
		Assert.Null(result.Error);
	}

	[Fact]
	public async Task UploadSelectedAsync_ReturnsCancelledWithoutConnectingWhenAlreadyCancelled()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();
		var client = new FakeFileTransferClient();

		LcuUnitFileUploadResult result = await CreateService(client).UploadSelectedAsync(
			"lcu-host",
			"user",
			"password",
			[],
			[],
			Path.GetTempPath(),
			"/MCFiles",
			_ => Task.CompletedTask,
			_ => Task.FromResult(true),
			cancellation.Token);

		Assert.Equal(LcuUnitFileUploadOutcome.Cancelled, result.Outcome);
		Assert.Empty(result.Folders);
		Assert.Null(client.Host);
	}

	private static LcuUnitFileUploadService CreateService(FakeFileTransferClient client) =>
		new(new UpdateFolderSelectionService(), new LcuFolderUploadService(client));

	private sealed class FakeFileTransferClient : IFileTransferClient
	{
		public string? Host { get; private set; }
		public string? User { get; private set; }
		public string? Password { get; private set; }
		public IOException? ConnectException { get; init; }
		public Action? OnConnect { get; init; }
		public FakeFileTransferSession Session { get; } = new();

		public Task<IFileTransferSession> ConnectAsync(
			string host,
			string user,
			string password,
			CancellationToken cancellationToken)
		{
			Host = host;
			User = user;
			Password = password;
			OnConnect?.Invoke();
			return ConnectException == null
				? Task.FromResult<IFileTransferSession>(Session)
				: Task.FromException<IFileTransferSession>(ConnectException);
		}
	}

	private sealed class FakeFileTransferSession : IFileTransferSession
	{
		public Task ClearStagingDirectoryAsync(CancellationToken cancellationToken) =>
			throw new NotSupportedException();

		public List<string> UploadedLocalFolders { get; } = [];
		public List<string> UploadedRemoteFolders { get; } = [];
		public bool Disconnected { get; private set; }

		public Task CreateDirectoryAsync(string remoteDirectory, CancellationToken cancellationToken) =>
			Task.CompletedTask;

		public Task DeleteDirectoryAsync(string remoteDirectory, CancellationToken cancellationToken) =>
			Task.CompletedTask;

		public Task<FileTransferResult> UploadFileAsync(
			string localFile,
			string remoteFile,
			CancellationToken cancellationToken) =>
			Task.FromResult(new FileTransferResult(localFile, remoteFile, true, false));

		public Task<IReadOnlyList<FileTransferResult>> UploadDirectoryAsync(
			string localDirectory,
			string remoteDirectory,
			CancellationToken cancellationToken)
		{
			UploadedLocalFolders.Add(localDirectory);
			UploadedRemoteFolders.Add(remoteDirectory);
			return Task.FromResult<IReadOnlyList<FileTransferResult>>(
				[new FileTransferResult(localDirectory, remoteDirectory, true, false)]);
		}

		public Task<IReadOnlyList<FileTransferResult>> DownloadFilesAsync(
			IReadOnlyList<FileTransferRequest> files,
			CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyList<FileTransferResult>>([]);

		public Task DisconnectAsync(CancellationToken cancellationToken)
		{
			Disconnected = true;
			return Task.CompletedTask;
		}
	}
}
