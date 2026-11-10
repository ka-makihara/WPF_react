using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class ModuleUpdateService_LoadUpdateInfoTests
{
	[Fact]
	public async Task LoadAsync_SkipsDownloadWhenInfoIsAlreadyAvailable()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			bool downloaded = false;
			ModuleUpdateInfoLoadResult result = await new ModuleUpdateService().LoadUpdateInfoAsync(
				shouldLoad: false,
				root,
				"Fuji/Peripheral/",
				"UpdateCommon.inf",
				"utf-8",
				(_, _) =>
				{
					downloaded = true;
					return Task.FromResult(true);
				},
				CancellationToken.None);

			Assert.Equal(ModuleUpdateInfoLoadOutcome.AlreadyAvailable, result.Outcome);
			Assert.False(downloaded);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public async Task LoadAsync_ThrowsWhenAlreadyAvailableAndCancellationWasRequested()
	{
		string root = CreateTemporaryDirectory();
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();
		try
		{
			await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
				new ModuleUpdateService().LoadUpdateInfoAsync(
					shouldLoad: false,
					root,
					"Fuji/Peripheral/",
					"UpdateCommon.inf",
					"utf-8",
					(_, _) => Task.FromResult(false),
					cancellation.Token));
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public async Task LoadAsync_LoadsDownloadedFileAndCleansOnlyPeripheralDirectory()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			string unrelatedDirectory = Path.Combine(root, "Keep");
			Directory.CreateDirectory(unrelatedDirectory);
			string retainedFile = Path.Combine(unrelatedDirectory, "keep.txt");
			File.WriteAllText(retainedFile, "keep");
			CancellationToken requestedToken = new();

			ModuleUpdateInfoLoadResult result = await new ModuleUpdateService().LoadUpdateInfoAsync(
				shouldLoad: true,
				root,
				"Fuji/Peripheral/",
				"UpdateCommon.inf",
				"utf-8",
				(targetPath, token) =>
				{
					Assert.Equal(requestedToken, token);
					string file = Path.Combine(
						targetPath,
						"Fuji",
						"Peripheral",
						"UpdateCommon.inf");
					Directory.CreateDirectory(Path.GetDirectoryName(file)!);
					File.WriteAllText(file, "[Unit]");
					return Task.FromResult(true);
				},
				requestedToken);

			Assert.Equal(ModuleUpdateInfoLoadOutcome.Loaded, result.Outcome);
			Assert.Equal("[Unit]", Assert.Single(result.Lines!));
			Assert.NotNull(result.Parser);
			Assert.False(Directory.Exists(Path.Combine(root, "Fuji", "Peripheral")));
			Assert.True(File.Exists(retainedFile));
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public async Task LoadAsync_ReturnsDownloadFailureWithoutLoading()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			ModuleUpdateInfoLoadResult result = await new ModuleUpdateService().LoadUpdateInfoAsync(
				shouldLoad: true,
				root,
				"Fuji/Peripheral/",
				"UpdateCommon.inf",
				"utf-8",
				(_, _) => Task.FromResult(false),
				CancellationToken.None);

			Assert.Equal(ModuleUpdateInfoLoadOutcome.DownloadFailed, result.Outcome);
			Assert.Null(result.Parser);
			Assert.Null(result.Lines);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public async Task LoadAsync_ThrowsCancellationWhenDownloadReturnsFalseAfterCancellation()
	{
		string root = CreateTemporaryDirectory();
		using CancellationTokenSource cancellation = new();
		try
		{
			await Assert.ThrowsAsync<OperationCanceledException>(() =>
				new ModuleUpdateService().LoadUpdateInfoAsync(
					shouldLoad: true,
					root,
					"Fuji/Peripheral/",
					"UpdateCommon.inf",
					"utf-8",
					(_, token) =>
					{
						Assert.Equal(cancellation.Token, token);
						cancellation.Cancel();
						return Task.FromResult(false);
					},
					cancellation.Token));
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public async Task LoadAsync_ThrowsWhenCancelledAfterDownloadCompletes()
	{
		string root = CreateTemporaryDirectory();
		using CancellationTokenSource cancellation = new();
		try
		{
			await Assert.ThrowsAsync<OperationCanceledException>(() =>
				new ModuleUpdateService().LoadUpdateInfoAsync(
					shouldLoad: true,
					root,
					"Fuji/Peripheral/",
					"UpdateCommon.inf",
					"utf-8",
					(targetPath, _) =>
					{
						string file = Path.Combine(
							targetPath,
							"Fuji",
							"Peripheral",
							"UpdateCommon.inf");
						Directory.CreateDirectory(Path.GetDirectoryName(file)!);
						File.WriteAllText(file, "[Unit]");
						cancellation.Cancel();
						return Task.FromResult(true);
					},
					cancellation.Token));
			Assert.True(cancellation.IsCancellationRequested);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public async Task LoadAsync_DoesNotStartDownloadWhenAlreadyCancelled()
	{
		string root = CreateTemporaryDirectory();
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();
		try
		{
			await Assert.ThrowsAsync<OperationCanceledException>(() =>
				new ModuleUpdateService().LoadUpdateInfoAsync(
					shouldLoad: true,
					root,
					"Fuji/Peripheral/",
					"UpdateCommon.inf",
					"utf-8",
					(_, _) =>
					{
						return Task.FromResult(false);
					},
					cancellation.Token));

		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public async Task LoadAsync_ReportsMissingDownloadedFile()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			ModuleUpdateInfoLoadResult result = await new ModuleUpdateService().LoadUpdateInfoAsync(
				shouldLoad: true,
				root,
				"Fuji/Peripheral/",
				"UpdateCommon.inf",
				"utf-8",
				(_, _) => Task.FromResult(true),
				CancellationToken.None);

			Assert.Equal(ModuleUpdateInfoLoadOutcome.FileNotFound, result.Outcome);
			Assert.Null(result.Parser);
			Assert.Null(result.Lines);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	private static string CreateTemporaryDirectory()
	{
		string path = Path.Combine(Path.GetTempPath(), $"ModuleUpdateInfoLoadServiceTests-{Guid.NewGuid():N}");
		Directory.CreateDirectory(path);
		return path;
	}
}
