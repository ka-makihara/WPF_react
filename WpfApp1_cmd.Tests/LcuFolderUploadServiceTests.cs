using WpfApp1_cmd.Services;
using WpfLcuCtrlLib;
using Xunit;

public class LcuFolderUploadServiceTests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ClearModuleFilesAsync_ClearsAllStagingContentsAndDisconnectsOnFailure(bool failDelete)
	{
		var client = new FakeFileTransferClient();
		client.Session.FailDelete = failDelete;
		Task Clear() => new LcuFolderUploadService(client).ClearModuleFilesAsync(
			"lcu", "user", "password", CancellationToken.None);
		if (failDelete)
			await Assert.ThrowsAsync<IOException>(Clear);
		else
			await Clear();

		Assert.Equal(["clear:/MCFiles"],
			client.Session.DirectoryOperations);
		Assert.True(client.Session.Disconnected);
	}

	[Fact]
	public async Task ClearModuleFilesAsync_DoesNotDeleteWhenCancelledDuringConnect()
	{
		using CancellationTokenSource cancellation = new();
		var client = new FakeFileTransferClient { OnConnect = () => cancellation.Cancel() };
		await Assert.ThrowsAsync<OperationCanceledException>(() =>
			new LcuFolderUploadService(client).ClearModuleFilesAsync(
				"lcu", "user", "password", cancellation.Token));
		Assert.Empty(client.Session.DirectoryOperations);
		Assert.True(client.Session.Disconnected);
	}

	[Fact]
	public async Task UploadModuleFilesAsync_ReuploadsSharedFileForEveryModule()
	{
		var client = new FakeFileTransferClient();
		var service = new LcuFolderUploadService(client);
		FileTransferRequest[] files = [new(@"C:\Data\unit.bin", "/MCFiles/shared/unit.bin")];
		for (int index = 0; index < 2; index++)
		{
			await service.UploadModuleFilesAsync("lcu", "user", "password", files, CancellationToken.None);
			await service.ClearModuleFilesAsync("lcu", "user", "password", CancellationToken.None);
		}
		Assert.Equal(
			["upload:/MCFiles/shared/unit.bin", "clear:/MCFiles",
			 "upload:/MCFiles/shared/unit.bin", "clear:/MCFiles"],
			client.Session.DirectoryOperations);
		Assert.Equal(4, client.ConnectCount);
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(false, true)]
	public async Task UploadModuleFilesAsync_ReportsFailedOrSkippedUpload(bool success, bool skipped)
	{
		var client = new FakeFileTransferClient();
		client.Session.FileUploadResult = new("local", "/MCFiles/unit.bin", success, skipped);
		await Assert.ThrowsAsync<IOException>(() =>
			new LcuFolderUploadService(client).UploadModuleFilesAsync(
				"lcu", "user", "password",
				[new FileTransferRequest("local", "/MCFiles/unit.bin")],
				CancellationToken.None));
		Assert.True(client.Session.Disconnected);
		Assert.DoesNotContain(client.Session.DirectoryOperations, operation => operation.StartsWith("file:"));
	}

	[Fact]
	public async Task UploadAsync_UploadsEachFolderReportsProgressAndDisconnects()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			string folder = Path.Combine(root, "Fuji", "Program");
			Directory.CreateDirectory(folder);
			await File.WriteAllBytesAsync(Path.Combine(folder, "file.bin"), [1, 2, 3, 4]);
			var client = new FakeFileTransferClient();
			List<LcuFolderUploadProgress> progress = [];

			LcuFolderUploadResult result = await new LcuFolderUploadService(client).UploadAsync(
				"lcu-host",
				"user",
				"password",
				[folder],
				root,
				"/MCFiles",
				item =>
				{
					progress.Add(item);
					return Task.CompletedTask;
				},
				_ => Task.FromResult(true),
				CancellationToken.None);

			Assert.Equal(LcuFolderUploadOutcome.Succeeded, result.Outcome);
			Assert.Equal(4, result.TotalTransferredBytes);
			Assert.Equal("lcu-host", client.Host);
			Assert.Equal("user", client.User);
			Assert.Equal("password", client.Password);
			Assert.Equal(folder, Assert.Single(client.Session.UploadedLocalFolders));
			Assert.Equal("/MCFiles" + folder[root.Length..], Assert.Single(client.Session.UploadedRemoteFolders));
			Assert.Equal(4, Assert.Single(progress).FolderBytes);
			Assert.True(client.Session.Disconnected);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public async Task UploadAsync_RetriesFailedFolderAndStopsWhenContinuationIsDeclined()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			string folder = Path.Combine(root, "Fuji");
			Directory.CreateDirectory(folder);
			var client = new FakeFileTransferClient();
			client.Session.UploadResults.Enqueue(
				[new FileTransferResult("a", "b", IsSuccess: false, IsSkipped: false)]);
			client.Session.UploadResults.Enqueue(
				[new FileTransferResult("a", "b", IsSuccess: true, IsSkipped: false)]);

			LcuFolderUploadResult result = await new LcuFolderUploadService(client).UploadAsync(
				"host",
				"user",
				"password",
				[folder],
				root,
				"/root",
				_ => Task.CompletedTask,
				_ => Task.FromResult(false),
				CancellationToken.None);

			Assert.Equal(LcuFolderUploadOutcome.Cancelled, result.Outcome);
			Assert.Equal(2, client.Session.UploadedLocalFolders.Count);
			Assert.True(client.Session.Disconnected);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public async Task UploadAsync_RejectsFolderOutsideRootWithMatchingPrefix()
	{
		string root = CreateTemporaryDirectory();
		string sibling = root + "_outside";
		Directory.CreateDirectory(sibling);
		var client = new FakeFileTransferClient();
		try
		{
			await Assert.ThrowsAsync<ArgumentException>(() =>
				new LcuFolderUploadService(client).UploadAsync(
					"host",
					"user",
					"password",
					[sibling],
					root,
					"/MCFiles",
					_ => Task.CompletedTask,
					_ => Task.FromResult(true),
					CancellationToken.None));

			Assert.Empty(client.Session.UploadedLocalFolders);
			Assert.True(client.Session.Disconnected);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
			Directory.Delete(sibling, recursive: true);
		}
	}

	[Fact]
	public async Task UploadAsync_DoesNotConnectWhenAlreadyCancelled()
	{
		string root = CreateTemporaryDirectory();
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();
		var client = new FakeFileTransferClient();
		try
		{
			await Assert.ThrowsAsync<OperationCanceledException>(() =>
				new LcuFolderUploadService(client).UploadAsync(
					"host",
					"user",
					"password",
					[],
					root,
					"/MCFiles",
					_ => Task.CompletedTask,
					_ => Task.FromResult(true),
					cancellation.Token));

			Assert.Equal(0, client.ConnectCount);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public async Task UploadAsync_StopsBeforeProgressWhenUploadCompletesAfterCancellation()
	{
		string root = CreateTemporaryDirectory();
		using CancellationTokenSource cancellation = new();
		try
		{
			string folder = Path.Combine(root, "Fuji");
			Directory.CreateDirectory(folder);
			var client = new FakeFileTransferClient
			{
				OnUpload = () => cancellation.Cancel()
			};
			int progressCount = 0;

			await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
				new LcuFolderUploadService(client).UploadAsync(
					"host",
					"user",
					"password",
					[folder],
					root,
					"/MCFiles",
					_ =>
					{
						progressCount++;
						return Task.CompletedTask;
					},
					_ => Task.FromResult(true),
					cancellation.Token));

			Assert.Equal(0, progressCount);
			Assert.True(client.Session.Disconnected);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public async Task UploadAsync_DisconnectsWhenCancellationOccursDuringConnect()
	{
		string root = CreateTemporaryDirectory();
		using CancellationTokenSource cancellation = new();
		var client = new FakeFileTransferClient
		{
			OnConnect = () => cancellation.Cancel()
		};
		try
		{
			await Assert.ThrowsAsync<OperationCanceledException>(() =>
				new LcuFolderUploadService(client).UploadAsync(
					"host",
					"user",
					"password",
					[],
					root,
					"/MCFiles",
					_ => Task.CompletedTask,
					_ => Task.FromResult(true),
					cancellation.Token));

			Assert.True(client.Session.Disconnected);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	private static string CreateTemporaryDirectory()
	{
		string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(path);
		return path;
	}

	private sealed class FakeFileTransferClient : IFileTransferClient
	{
		public string? Host { get; private set; }
		public string? User { get; private set; }
		public string? Password { get; private set; }
		public int ConnectCount { get; private set; }
		public Action? OnConnect { get; init; }
		public Action? OnUpload { get; init; }
		public FakeFileTransferSession Session { get; } = new();

		public Task<IFileTransferSession> ConnectAsync(
			string host,
			string user,
			string password,
			CancellationToken cancellationToken)
		{
			ConnectCount++;
			Host = host;
			User = user;
			Password = password;
			OnConnect?.Invoke();
			Session.OnUpload = OnUpload;
			return Task.FromResult<IFileTransferSession>(Session);
		}
	}

	private sealed class FakeFileTransferSession : IFileTransferSession
	{
		public List<string> DirectoryOperations { get; } = [];
		public bool FailDelete { get; set; }
		public FileTransferResult? FileUploadResult { get; set; }
		public List<string> UploadedLocalFolders { get; } = [];
		public List<string> UploadedRemoteFolders { get; } = [];
		public Queue<IReadOnlyList<FileTransferResult>> UploadResults { get; } = new();
		public bool Disconnected { get; private set; }
		public Action? OnUpload { get; set; }

		public Task CreateDirectoryAsync(string remoteDirectory, CancellationToken cancellationToken)
		{
			DirectoryOperations.Add($"create:{remoteDirectory}");
			return Task.CompletedTask;
		}

		public Task DeleteDirectoryAsync(string remoteDirectory, CancellationToken cancellationToken)
		{
			DirectoryOperations.Add($"delete:{remoteDirectory}");
			return FailDelete ? Task.FromException(new IOException("delete failed")) : Task.CompletedTask;
		}

		public Task ClearStagingDirectoryAsync(CancellationToken cancellationToken)
		{
			DirectoryOperations.Add("clear:/MCFiles");
			return FailDelete ? Task.FromException(new IOException("delete failed")) : Task.CompletedTask;
		}

		public Task<FileTransferResult> UploadFileAsync(
			string localFile,
			string remoteFile,
			CancellationToken cancellationToken)
		{
			DirectoryOperations.Add($"upload:{remoteFile}");
			return Task.FromResult(FileUploadResult ?? new FileTransferResult(localFile, remoteFile, true, false));
		}

		public Task<IReadOnlyList<FileTransferResult>> UploadDirectoryAsync(
			string localDirectory,
			string remoteDirectory,
			CancellationToken cancellationToken)
		{
			UploadedLocalFolders.Add(localDirectory);
			UploadedRemoteFolders.Add(remoteDirectory);
			OnUpload?.Invoke();
			IReadOnlyList<FileTransferResult> result = UploadResults.Count > 0
				? UploadResults.Dequeue()
				: [new FileTransferResult(localDirectory, remoteDirectory, true, false)];
			return Task.FromResult(result);
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
