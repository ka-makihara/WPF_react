using System.IO;
using WpfApp1_cmd.Services;
using WpfLcuCtrlLib;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class UnitVersionListService_BuildInputsTests
{
	[Fact]
	public void Build_ProjectsSnapshotsAndUpdateCommonSections()
	{
		string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".ini");
		File.WriteAllText(
			path,
			"[UnitA]\r\nPath=/a.bin\r\nFuserPath=/fuser.bin\r\nAttribute=2\r\nVersion=1.0");
		try
		{
			UnitVersionPreparationInputs result = new UnitVersionListService().BuildInputs(
				[new UpdateVersionInputSnapshot("UnitA", "2.0")],
				[
					new UnitVersionGroupInputSnapshot(
						["UnitA"],
						[new KeyValuePair<string, string>("*", "GroupA")])
				],
				new IniFileParser(path));

			Assert.Equal(new UpdateVersionCandidate("UnitA", "2.0"), Assert.Single(result.UpdateVersions));
			Assert.Equal(["UnitA"], Assert.Single(result.Groups).Components);
			Assert.Equal("GroupA", result.Groups[0].Names["*"]);
			UnitVersionDefinition unit = Assert.Single(result.MachineUnits);
			Assert.Equal("UnitA", unit.Name);
			Assert.Equal("/a.bin", unit.Path);
			Assert.Equal("/fuser.bin", unit.FuserPath);
			Assert.Equal("2", unit.Attribute);
			Assert.Equal("1.0", unit.CurrentVersion);
			Assert.Equal("/fuser.bin", result.FuserPaths["UnitA"]);
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Fact]
	public void Build_ThrowsWhenCancellationIsRequested()
	{
		string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".ini");
		File.WriteAllText(path, "[UnitA]\r\nVersion=1.0");
		try
		{
			using CancellationTokenSource cancellation = new();
			cancellation.Cancel();

			Assert.Throws<OperationCanceledException>(() =>
				new UnitVersionListService().BuildInputs(
					[],
					[],
					new IniFileParser(path),
					cancellation.Token));
		}
		finally
		{
			File.Delete(path);
		}
	}
}
