using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class UpdateDataLoadService_ReadUpdateCommonTests
{
	[Theory]
	[InlineData("", false, 0)]
	[InlineData("\\fuser.bin", true, 0)]
	[InlineData("\\fuser.bin", false, 1)]
	public void Read_ChecksUpdateDataFuserPathEvenWhenPathIsDirectory(
		string fuserPath,
		bool fuserExists,
		int expectedMissingFiles)
	{
		string root = CreateTemporaryDirectory();
		try
		{
			Directory.CreateDirectory(Path.Combine(root, "folder"));
			if (fuserExists)
			{
				File.WriteAllText(Path.Combine(root, "fuser.bin"), "payload");
			}
			string infoPath = Path.Combine(root, "UpdateCommon.inf");
			File.WriteAllText(infoPath, $"[Unit]\nPath=\\folder\nFuserPath={fuserPath}\n");

			UpdateCommonInfoReadResult result = new UpdateDataLoadService()
				.ReadUpdateCommon(infoPath, root, _ => null);

			Assert.Null(result.ErrorMessage);
			Assert.Equal(expectedMissingFiles, result.MissingFiles.Count);
			if (expectedMissingFiles != 0)
			{
				Assert.Equal(root + fuserPath, Assert.Single(result.MissingFiles));
			}
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void Read_ReturnsFileEntriesAndSkipsDirectories()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			Directory.CreateDirectory(Path.Combine(root, "folder"));
			File.WriteAllText(Path.Combine(root, "update.bin"), "payload");
			string infoPath = Path.Combine(root, "UpdateCommon.inf");
			File.WriteAllText(infoPath,
				"[Existing]\nPath=\\update.bin\nAttribute=1\nVersion=2.3\nFuserPath=\\fuser.bin\n" +
				"[Directory]\nPath=\\folder\nVersion=1\n" +
				"[Missing]\nPath=\\missing.bin\nVersion=1\n");

			UpdateCommonInfoReadResult result = new UpdateDataLoadService().ReadUpdateCommon(infoPath, root, name => $"group-{name}");

			Assert.Collection(
				result.Entries,
				entry =>
				{
					Assert.Equal("Existing", entry.Name);
					Assert.Equal("1", entry.Attribute);
					Assert.Equal("\\update.bin", entry.Path);
					Assert.Equal("2.3", entry.Version);
					Assert.Equal("\\fuser.bin", entry.FuserPath);
					Assert.Equal("group-Existing", entry.UnitGroup);
				},
				entry =>
				{
					Assert.Equal("Missing", entry.Name);
					Assert.Equal("\\missing.bin", entry.Path);
					Assert.Equal("1", entry.Version);
					Assert.Equal("group-Missing", entry.UnitGroup);
				});
			Assert.Equal(
				[
					new UpdateCommonPathEntry("Existing", "\\update.bin", "\\fuser.bin"),
					new UpdateCommonPathEntry("Directory", "\\folder", ""),
					new UpdateCommonPathEntry("Missing", "\\missing.bin", "")
				],
				result.PathEntries);
			Assert.Equal(
				[Path.Combine(root, "fuser.bin"), Path.Combine(root, "missing.bin")],
				result.MissingFiles);
			Assert.Equal(
				UpdateDataLoadService.GetPeripheralSize(root),
				result.DataSize);
			Assert.Null(result.ErrorMessage);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void Read_ReturnsNoEntriesAndErrorWhenReadingFailsAfterAnEntry()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			string infoPath = Path.Combine(root, "UpdateCommon.inf");
			File.WriteAllText(
				infoPath,
				"[Existing]\nPath=\\existing.bin\n[Second]\nPath=\\second.bin\n");
			File.WriteAllText(Path.Combine(root, "existing.bin"), "data");
			File.WriteAllText(Path.Combine(root, "second.bin"), "data");
			int resolvedCount = 0;

			UpdateCommonInfoReadResult result = new UpdateDataLoadService().ReadUpdateCommon(
					infoPath,
					root,
					_ =>
					{
						if (++resolvedCount == 2)
						{
							throw new IOException("unit group lookup failed");
						}
						return null;
					});

			Assert.Empty(result.Entries);
			Assert.NotNull(result.ErrorMessage);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void Read_PropagatesCancellationRaisedDuringUnitGroupResolution()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			File.WriteAllText(Path.Combine(root, "update.bin"), "payload");
			string infoPath = Path.Combine(root, "UpdateCommon.inf");
			File.WriteAllText(infoPath, "[Update]\nPath=\\update.bin\n");
			using CancellationTokenSource cancellation = new();
			UpdateDataLoadService reader = new();

			Assert.Throws<OperationCanceledException>(() =>
				reader.ReadUpdateCommon(
					infoPath,
					root,
					_ =>
					{
						cancellation.Cancel();
						return null;
					},
					cancellation.Token));
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	private static string CreateTemporaryDirectory()
	{
		string path = Path.Combine(Path.GetTempPath(), $"UpdateCommonInfoReaderTests-{Guid.NewGuid():N}");
		Directory.CreateDirectory(path);
		return path;
	}
}
