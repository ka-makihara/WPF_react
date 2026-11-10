using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class UpdateDataLoadService_PathGroupTests
{
	[Fact]
	public void Build_GroupsFileEntriesAndRetainsFusePath()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			File.WriteAllText(Path.Combine(root, "first.bin"), "one");
			File.WriteAllText(Path.Combine(root, "second.bin"), "two");
			string infoPath = Path.Combine(root, "UpdateCommon.inf");
			File.WriteAllText(infoPath,
				"[First]\nPath=\\first.bin\nFuserPath=\\fuser.bin\n" +
				"[Second]\nPath=\\second.bin\n");

			IReadOnlyList<UpdatePathGroup> result = new UpdateDataLoadService().BuildPathGroups(
				infoPath,
				root,
				[
					new(["First"], "Group", ["Group", "Alias"], "File"),
					new(["Second"], "Group", ["Group", "Alias"], "File")
				]);

			UpdatePathGroup group = Assert.Single(result);
			Assert.Equal("Group", group.GroupName);
			Assert.Equal(
				[
					new UpdatePathFile("First", "\\first.bin", "\\fuser.bin"),
					new UpdatePathFile("Second", "\\second.bin", "")
				],
				group.Files);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void Build_ExpandsFolderAndSkipsMissingAndDirectoryEntries()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			Directory.CreateDirectory(Path.Combine(root, "folder", "nested"));
			Directory.CreateDirectory(Path.Combine(root, "directory-entry"));
			File.WriteAllText(Path.Combine(root, "folder", "nested", "anchor.bin"), "anchor");
			File.WriteAllText(Path.Combine(root, "folder", "nested", "included.bin"), "one");
			File.WriteAllText(Path.Combine(root, "folder", "extra.bin"), "two");
			string infoPath = Path.Combine(root, "UpdateCommon.inf");
			File.WriteAllText(infoPath,
				"[Folder]\nPath=/folder/nested/anchor.bin\n" +
				"[NamedFile]\nPath=/folder/nested/included.bin\n" +
				"[Directory]\nPath=/directory-entry\n" +
				"[Missing]\nPath=/missing.bin\n");

			IReadOnlyList<UpdatePathGroup> result = new UpdateDataLoadService().BuildPathGroups(
				infoPath,
				root,
				[
					new(["Folder"], "FolderGroup", ["FolderGroup"], "Folder"),
					new(["NamedFile"], "FolderGroup", ["FolderGroup"], "File"),
					new(["Directory"], "DirectoryGroup", ["DirectoryGroup"], "File"),
					new(["Missing"], "MissingGroup", ["MissingGroup"], "File")
				]);

			UpdatePathGroup group = Assert.Single(result);
			Assert.Equal("FolderGroup", group.GroupName);
			Assert.Equal(2, group.Files.Count);
			Assert.Contains(group.Files, file =>
				file.Name == "NamedFile" &&
				file.Path == "/folder/nested/included.bin");
			Assert.Contains(group.Files, file =>
				file.Name == "Folder" &&
				file.Path == "/folder/nested/anchor.bin");
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void Build_ExpandsFolderWhenDataFolderUsesDifferentCasing()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			Directory.CreateDirectory(Path.Combine(root, "folder"));
			File.WriteAllText(Path.Combine(root, "folder", "included.bin"), "payload");
			string infoPath = Path.Combine(root, "UpdateCommon.inf");
			File.WriteAllText(infoPath, "[Folder]\nPath=/folder/included.bin\n");
			string differentCasingRoot = root.ToUpperInvariant();

			IReadOnlyList<UpdatePathGroup> result = new UpdateDataLoadService().BuildPathGroups(
				infoPath,
				differentCasingRoot,
				[new(["Folder"], "FolderGroup", ["FolderGroup"], "Folder")]);

			UpdatePathGroup group = Assert.Single(result);
			Assert.Equal(
				new UpdatePathFile("Folder", "/folder/included.bin", ""),
				Assert.Single(group.Files));
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void Build_UsesFirstUnitLinkForDuplicateComponents()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			File.WriteAllText(Path.Combine(root, "first.bin"), "one");
			string infoPath = Path.Combine(root, "UpdateCommon.inf");
			File.WriteAllText(infoPath, "[Update]\nPath=/first.bin\n");

			IReadOnlyList<UpdatePathGroup> result = new UpdateDataLoadService().BuildPathGroups(
				infoPath,
				root,
				[
					new(["Update"], "FirstGroup", ["FirstGroup"], "File"),
					new(["Update"], "SecondGroup", ["SecondGroup"], "Folder")
				]);

			UpdatePathGroup group = Assert.Single(result);
			Assert.Equal("FirstGroup", group.GroupName);
			Assert.Equal(
				new UpdatePathFile("Update", "/first.bin", ""),
				Assert.Single(group.Files));
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void Build_MergesGroupsWhenCurrentAliasesContainPreviousGroupName()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			File.WriteAllText(Path.Combine(root, "first.bin"), "one");
			File.WriteAllText(Path.Combine(root, "second.bin"), "two");
			string infoPath = Path.Combine(root, "UpdateCommon.inf");
			File.WriteAllText(infoPath,
				"[First]\nPath=/first.bin\n[Second]\nPath=/second.bin\n");

			IReadOnlyList<UpdatePathGroup> result = new UpdateDataLoadService().BuildPathGroups(
				infoPath,
				root,
				[
					new(["First"], "FirstGroup", ["Alias"], "File"),
					new(["Second"], "SecondGroup", ["firstgroup"], "File")
				]);

			UpdatePathGroup group = Assert.Single(result);
			Assert.Equal("FirstGroup", group.GroupName);
			Assert.Equal(2, group.Files.Count);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void Build_ThrowsWhenCancelledBeforeBuilding()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		Assert.Throws<OperationCanceledException>(() =>
			new UpdateDataLoadService().BuildPathGroups(
				"UpdateCommon.inf",
				@"C:\data",
				[],
				cancellation.Token));
	}

	private static string CreateTemporaryDirectory()
	{
		string path = Path.Combine(Path.GetTempPath(), $"UpdatePathGroupBuilderTests-{Guid.NewGuid():N}");
		Directory.CreateDirectory(path);
		return path;
	}
}
