using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class BackupDataService_SelectionTests
{
	[Fact]
	public void ResolveUpdateCommonPath_PrefersFileInSelectedFolder()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			string directPath = Path.Combine(root, "UpdateCommon.inf");
			File.WriteAllText(directPath, "");

			string? result = new BackupDataService().ResolveUpdateCommonPath(
				root,
				"UpdateCommon.inf",
				"Fuji/System3/Program/Peripheral/");

			Assert.Equal(directPath, result);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void ResolveUpdateCommonPath_FindsNestedPeripheralFile()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			string nestedDirectory = Path.Combine(root, "Fuji", "System3", "Program", "Peripheral");
			Directory.CreateDirectory(nestedDirectory);
			string nestedPath = Path.Combine(nestedDirectory, "UpdateCommon.inf");
			File.WriteAllText(nestedPath, "");

			string? result = new BackupDataService().ResolveUpdateCommonPath(
				root,
				"UpdateCommon.inf",
				"Fuji/System3/Program/Peripheral/");

			Assert.Equal(nestedPath, result);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void GetDataFolder_ReturnsDirectoryContainingUpdateCommon()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			string nestedDirectory = Path.Combine(root, "Fuji", "Peripheral");
			Directory.CreateDirectory(nestedDirectory);
			string infoPath = Path.Combine(nestedDirectory, "UpdateCommon.inf");

			string dataFolder = new BackupDataService().GetDataFolder(infoPath);

			Assert.Equal(nestedDirectory, dataFolder);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void Read_ParsesBackupMetadata()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			string infoPath = Path.Combine(root, "UpdateCommon.inf");
			File.WriteAllText(infoPath, "#UpdateInfo=Line1;Machine1;Module1;20261028\n[unit]\n");

			BackupDataSelectionResult result = new BackupDataService().Read(infoPath);

			Assert.True(result.UpdateCommonExists);
			Assert.True(result.IsBackupData);
			Assert.Equal("Line1", result.LcuName);
			Assert.Equal("Machine1", result.MachineName);
			Assert.Equal("Module1", result.ModuleName);
			Assert.Equal("20261028", result.Timestamp);
			Assert.Null(result.ErrorMessage);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void Read_RejectsMalformedBackupMetadata()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			string infoPath = Path.Combine(root, "UpdateCommon.inf");
			File.WriteAllText(infoPath, "#UpdateInfo=Line1;Machine1\n");

			BackupDataSelectionResult result = new BackupDataService().Read(infoPath);

			Assert.True(result.IsBackupData);
			Assert.NotNull(result.ErrorMessage);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Theory]
	[InlineData("MissingLine", "Machine1", "Module1", BackupDataTargetMatch.LcuNotFound)]
	[InlineData("Line1", "MissingMachine", "Module1", BackupDataTargetMatch.MachineNotFound)]
	[InlineData("Line1", "Machine1", "MissingModule", BackupDataTargetMatch.ModuleNotFound)]
	[InlineData("Line1", "Machine1", "Module1", BackupDataTargetMatch.Found)]
	public void MatchTarget_IdentifiesBackupTarget(
		string lcuName,
		string machineName,
		string moduleName,
		BackupDataTargetMatch expected)
	{
		BackupDataTarget[] targets =
		[
			new("Line1", [new("Machine1", ["Module1", "Module2"])])
		];

		BackupDataTargetMatch result = new BackupDataService().MatchTarget(
			targets,
			lcuName,
			machineName,
			moduleName);

		Assert.Equal(expected, result);
	}

	[Fact]
	public void ResolveTarget_ReturnsIndexesForResolvedBackupTarget()
	{
		BackupDataTarget[] targets =
		[
			new("Line0", []),
			new("Line1",
			[
				new("Machine0", []),
				new("Machine1", ["Module0", "Module1"])
			])
		];

		BackupDataTargetMatchResult result = new BackupDataService().ResolveTarget(
			targets,
			"Line1",
			"Machine1",
			"Module1");

		Assert.Equal(BackupDataTargetMatch.Found, result.Outcome);
		Assert.Equal(1, result.LcuIndex);
		Assert.Equal(1, result.MachineIndex);
		Assert.Equal(1, result.ModuleIndex);
	}

	[Fact]
	public void CreateCandidate_CombinesPathMetadataAndTargetResolution()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			string infoPath = Path.Combine(root, "UpdateCommon.inf");
			File.WriteAllText(infoPath, "#UpdateInfo=Line1;Machine1;Module1;20261028\n");
			BackupDataTarget[] targets =
			[
				new("Line1", [new("Machine1", ["Module1"])])
			];

			BackupDataCandidate? candidate = new BackupDataService().CreateCandidate(
				root,
				"UpdateCommon.inf",
				"Fuji/Peripheral/",
				targets);

			Assert.NotNull(candidate);
			Assert.Equal(infoPath, candidate.UpdateCommonPath);
			Assert.Equal(root, candidate.DataFolder);
			Assert.True(candidate.Metadata.IsBackupData);
			Assert.Equal(BackupDataTargetMatch.Found, candidate.Target?.Outcome);
			Assert.Equal(BackupDataCandidateOutcome.BackupData, candidate.Outcome);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Theory]
	[InlineData("", BackupDataCandidateOutcome.StandardUpdate)]
	[InlineData("#UpdateInfo=Line1;Machine1\n", BackupDataCandidateOutcome.MetadataError)]
	[InlineData("#UpdateInfo=MissingLine;Machine1;Module1;20261028\n", BackupDataCandidateOutcome.TargetMismatch)]
	public void CreateCandidate_ClassifiesSelection(
		string updateCommonContent,
		BackupDataCandidateOutcome expected)
	{
		string root = CreateTemporaryDirectory();
		try
		{
			File.WriteAllText(Path.Combine(root, "UpdateCommon.inf"), updateCommonContent);
			BackupDataTarget[] targets =
			[
				new("Line1", [new("Machine1", ["Module1"])])
			];

			BackupDataCandidate? candidate = new BackupDataService().CreateCandidate(
				root,
				"UpdateCommon.inf",
				"Fuji/Peripheral/",
				targets);

			Assert.NotNull(candidate);
			Assert.Equal(expected, candidate.Outcome);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	private static string CreateTemporaryDirectory()
	{
		string path = Path.Combine(Path.GetTempPath(), $"BackupDataServiceTests-{Guid.NewGuid():N}");
		Directory.CreateDirectory(path);
		return path;
	}
}
