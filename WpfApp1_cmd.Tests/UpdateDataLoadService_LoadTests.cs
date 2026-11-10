using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class UpdateDataLoadService_LoadTests
{
	[Fact]
	public void Load_ReturnsUpdateInfoAndPathGroups()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			File.WriteAllText(Path.Combine(root, "update.bin"), "payload");
			string infoPath = Path.Combine(root, "UpdateCommon.inf");
			File.WriteAllText(infoPath, "[Update]\nPath=/update.bin\nVersion=2.3\n");
			var service = CreateService();

			UpdateDataLoadResult result = service.Load(
				infoPath,
				root,
				[new(["Update"], "UpdateGroup", ["UpdateGroup"], "File")]);

			Assert.True(result.UpdateCommonExists);
			Assert.Null(result.ReadError);
			Assert.NotNull(result.DataSize);
			Assert.Empty(result.MissingFiles);
			Assert.Equal("Update", Assert.Single(result.Entries).Name);
			UpdatePathGroup group = Assert.Single(result.PathGroups);
			Assert.Equal("UpdateGroup", group.GroupName);
			Assert.Equal("/update.bin", Assert.Single(group.Files).Path);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void Load_IncludesMetadataForMissingFilesWithoutAddingTransferPaths()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			string infoPath = Path.Combine(root, "UpdateCommon.inf");
			File.WriteAllText(infoPath, "[Update]\nPath=/missing.bin\nVersion=2.3\n");

			UpdateDataLoadResult result = CreateService().Load(
				infoPath,
				root,
				[new(["Update"], "UpdateGroup", ["UpdateGroup"], "File")]);

			Assert.Equal("Update", Assert.Single(result.Entries).Name);
			Assert.Empty(result.PathGroups);
			Assert.Equal([root + "/missing.bin"], result.MissingFiles);
			Assert.Null(result.ReadError);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void Load_ReportsMissingUpdateCommonWithoutReading()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			UpdateDataLoadResult result = CreateService().Load(
				Path.Combine(root, "missing.inf"),
				root,
				[]);

			Assert.False(result.UpdateCommonExists);
			Assert.Empty(result.Entries);
			Assert.Null(result.DataSize);
			Assert.Null(result.ReadError);
			Assert.Empty(result.PathGroups);
			Assert.Empty(result.MissingFiles);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void Load_LeavesUnitGroupUnsetWhenNoLinkMatches()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			File.WriteAllText(Path.Combine(root, "update.bin"), "payload");
			string infoPath = Path.Combine(root, "UpdateCommon.inf");
			File.WriteAllText(infoPath, "[Update]\nPath=/update.bin\nVersion=2.3\n");

			UpdateDataLoadResult result = CreateService().Load(
				infoPath,
				root,
				[]);

			Assert.Null(Assert.Single(result.Entries).UnitGroup);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void Load_ReturnsReadErrorWithoutBuildingPathGroupsWhenDataFolderIsUnavailable()
	{
		string root = CreateTemporaryDirectory();
		string missingDataFolder = Path.Combine(root, "missing");
		try
		{
			string infoPath = Path.Combine(root, "UpdateCommon.inf");
			File.WriteAllText(infoPath, "[Update]\nPath=/update.bin\nVersion=2.3\n");

			UpdateDataLoadResult result = CreateService().Load(
				infoPath,
				missingDataFolder,
				[new(["Update"], "UpdateGroup", ["UpdateGroup"], "File")]);

			Assert.True(result.UpdateCommonExists);
			Assert.NotNull(result.ReadError);
			Assert.Null(result.DataSize);
			Assert.Empty(result.PathGroups);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void Load_ThrowsWhenCancelledBeforeReading()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		Assert.Throws<OperationCanceledException>(() =>
			CreateService().Load(
				"UpdateCommon.inf",
				@"C:\data",
				[],
				cancellation.Token));
	}

	private static UpdateDataLoadService CreateService() =>
		new();

	private static string CreateTemporaryDirectory()
	{
		string path = Path.Combine(Path.GetTempPath(), $"UpdateDataLoadServiceTests-{Guid.NewGuid():N}");
		Directory.CreateDirectory(path);
		return path;
	}
}
