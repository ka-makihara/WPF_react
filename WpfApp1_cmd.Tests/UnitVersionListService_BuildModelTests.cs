using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class UnitVersionListService_BuildModelTests
{
	[Fact]
	public void Build_ThrowsWhenCancellationIsRequestedBeforeFileInspection()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		Assert.Throws<OperationCanceledException>(() =>
			new UnitVersionListService().BuildModel(
				new UnitVersionBuildRequest(
					Path.Combine(Path.GetTempPath(), "missing.bin"),
					null,
					"0",
					"1.0",
					"2.0",
					"GroupA",
					IsSelected: true,
					IsBackupData: false,
					IsBaseUpdateInfo: false,
					NotUpdateAttribute: 2),
				cancellation.Token));
	}

	[Fact]
	public void Build_CreatesVersionAndClearsMissingOptionalFuserPath()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			string updateFile = Path.Combine(root, "update.bin");
			File.WriteAllText(updateFile, "payload");

			UnitVersionBuildResult result = new UnitVersionListService().BuildModel(
				new UnitVersionBuildRequest(
					updateFile,
					Path.Combine(root, "missing-fuser.bin"),
					"0",
					"1.0",
					"2.0",
					"GroupA",
					IsSelected: true,
					IsBackupData: false,
					IsBaseUpdateInfo: false,
					NotUpdateAttribute: 2,
					Path: "update.bin",
					FuserPath: "fuser.bin"));

			Assert.Equal(UnitVersionBuildOutcome.Created, result.Outcome);
			Assert.Equal(0, result.Attribute);
			Assert.Equal("update.bin", result.Path);
			Assert.Equal("", result.FuserPath);
			Assert.True(result.ClearFuserPath);
			Assert.Equal("1.0", result.CurrentVersion);
			Assert.Equal("2.0", result.NewVersion);
			Assert.Equal("GroupA", result.UnitGroup);
			Assert.True(result.IsSelected);
			Assert.Equal(new FileInfo(updateFile).Length, result.Size);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void Build_HandlesMissingUpdateFileAccordingToMode(bool allowMissing)
	{
		string root = CreateTemporaryDirectory();
		try
		{
			UnitVersionBuildResult result = new UnitVersionListService().BuildModel(
				new UnitVersionBuildRequest(
					Path.Combine(root, "missing.bin"),
					null,
					"0",
					"1.0",
					"2.0",
					"GroupA",
					IsSelected: true,
					IsBackupData: false,
					IsBaseUpdateInfo: false,
					NotUpdateAttribute: 2,
					Path: "missing.bin",
					AllowMissingUpdateFile: allowMissing));

			if (!allowMissing)
			{
				Assert.Equal(UnitVersionBuildOutcome.UpdateFileNotFound, result.Outcome);
				return;
			}

			Assert.Equal(UnitVersionBuildOutcome.Created, result.Outcome);
			Assert.True(result.IsUpdateFileMissing);
			Assert.False(result.IsSelected);
			Assert.Equal(0, result.Size);
			Assert.Equal("1.0", result.CurrentVersion);
			Assert.Equal("2.0", result.NewVersion);
			Assert.Equal("missing.bin", result.Path);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Theory]
	[InlineData("2", "2.0", false, false, false, false, "1.0")]
	[InlineData("0", "N/A", false, false, false, false, "1.0")]
	[InlineData("0", "2.0", true, false, false, false, "1.0")]
	[InlineData("0", "2.0", false, true, false, false, "1.0")]
	[InlineData("0", "2.0", true, false, true, false, "")]
	[InlineData("0", "2.0", true, false, false, true, "")]
	public void Build_DeterminesSelectionAndCurrentVersion(
		string attribute,
		string newVersion,
		bool initiallySelected,
		bool isBackupData,
		bool isBaseUpdateInfo,
		bool noMachineVersion,
		string machineVersion)
	{
		string root = CreateTemporaryDirectory();
		try
		{
			string updateFile = Path.Combine(root, "update.bin");
			File.WriteAllText(updateFile, "payload");

			UnitVersionBuildResult result = new UnitVersionListService().BuildModel(
				new UnitVersionBuildRequest(
					updateFile,
					null,
					attribute,
					noMachineVersion ? "" : machineVersion,
					newVersion,
					null,
					initiallySelected,
					isBackupData,
					isBaseUpdateInfo,
					NotUpdateAttribute: 2));

			Assert.Equal(UnitVersionBuildOutcome.Created, result.Outcome);
			Assert.Equal(
				initiallySelected
					&& attribute != "2"
					&& newVersion != "N/A"
					&& !isBackupData
					&& !noMachineVersion,
				result.IsSelected);
			Assert.Equal(
				isBaseUpdateInfo ? "Undefined" : noMachineVersion ? "N/A" : machineVersion,
				result.CurrentVersion);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void Build_SkipsMissingUpdateFile()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			UnitVersionBuildResult result = new UnitVersionListService().BuildModel(
				new UnitVersionBuildRequest(
					Path.Combine(root, "missing.bin"),
					null,
					"0",
					"1.0",
					"2.0",
					"",
					true,
					false,
					false,
					2,
					Path: "update.bin",
					FuserPath: "fuser.bin"));

			Assert.Equal(UnitVersionBuildOutcome.UpdateFileNotFound, result.Outcome);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void Build_UsesExistingFuserPath()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			string updateFile = Path.Combine(root, "update.bin");
			string fuserFile = Path.Combine(root, "fuser.bin");
			File.WriteAllText(updateFile, "payload");
			File.WriteAllText(fuserFile, "fuser");

			UnitVersionBuildResult result = new UnitVersionListService().BuildModel(
				new UnitVersionBuildRequest(
					updateFile,
					fuserFile,
					"0",
					"",
					"2.0",
					"",
					true,
					false,
					false,
					2,
					Path: "update.bin",
					FuserPath: "fuser.bin"));

			Assert.Equal(UnitVersionBuildOutcome.Created, result.Outcome);
			Assert.Equal(0, result.Attribute);
			Assert.Equal("fuser.bin", result.FuserPath);
			Assert.False(result.ClearFuserPath);
			Assert.Equal("N/A", result.CurrentVersion);
			Assert.False(result.IsSelected);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void Build_ReportsInvalidAttributeInsteadOfCreatingAnUpdateUnit()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			string updateFile = Path.Combine(root, "update.bin");
			File.WriteAllText(updateFile, "payload");

			UnitVersionBuildResult result = new UnitVersionListService().BuildModel(
				new UnitVersionBuildRequest(
					updateFile,
					null,
					"invalid",
					"1.0",
					"2.0",
					"",
					true,
					false,
					false,
					2));

			Assert.Equal(UnitVersionBuildOutcome.InvalidAttribute, result.Outcome);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	private static string CreateTemporaryDirectory()
	{
		string path = Path.Combine(Path.GetTempPath(), $"UnitVersionModelBuilderTests-{Guid.NewGuid():N}");
		Directory.CreateDirectory(path);
		return path;
	}
}
