using System.IO;
using WpfApp1_cmd.Services;
using WpfLcuCtrlLib;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class UnitVersionListService_CreateTests
{
	[Theory]
	[InlineData(UnitVersionMissingFileMode.Exclude, 0, "(excluded)")]
	[InlineData(UnitVersionMissingFileMode.ListOnly, 1, "(not selectable)")]
	[InlineData(UnitVersionMissingFileMode.Selectable, 1, "(test: selectable)")]
	public void Create_RunsAllStepsAndHandlesMissingUpdateFile(
		UnitVersionMissingFileMode mode,
		int expectedMissingItems,
		string expectedLogSuffix)
	{
		string root = Path.Combine(Path.GetTempPath(), $"UnitVersionList-{Guid.NewGuid():N}");
		Directory.CreateDirectory(root);
		string iniPath = Path.Combine(root, "UpdateCommon.inf");
		File.WriteAllText(Path.Combine(root, "exists.bin"), "payload");
		File.WriteAllText(
			iniPath,
			"[UnitA]\r\nPath=exists.bin\r\nAttribute=0\r\nVersion=1.0\r\n" +
			"[UnitB]\r\nPath=missing.bin\r\nAttribute=0\r\nVersion=1.0\r\n" +
			"[UnitC]\r\nPath=exists.bin\r\nAttribute=0\r\nVersion=3.0\r\n");
		try
		{
			UnitVersionListResult result = new UnitVersionListService().Create(
				new UnitVersionListRequest(
					[
						new UpdateVersionInputSnapshot("UnitA", "2.0"),
						new UpdateVersionInputSnapshot("UnitB", "2.0"),
						new UpdateVersionInputSnapshot("UnitC", "3.0"),
						new UpdateVersionInputSnapshot("NotOnMachine", "4.0")
					],
					[],
					new IniFileParser(iniPath),
					[],
					"MachineA",
					root + Path.DirectorySeparatorChar,
					IgnoreVersion: false,
					IsModuleSelected: true,
					IsBackupData: false,
					IsBaseUpdateInfo: false,
					UnifyGroupSelection: false,
					NotUpdateAttribute: 2,
					MissingFileMode: mode,
					LcuName: "LCU",
					MachineName: "Machine",
					ModuleName: "Module"));

			UnitVersionPreparedDefinition unitA = Assert.Single(result.ItemsToCreate, item => item.Name == "UnitA");
			Assert.True(unitA.IsSelected);
			Assert.False(unitA.IsUpdateFileMissing);
			Assert.Equal(expectedMissingItems, result.ItemsToCreate.Count(item => item.Name == "UnitB"));
			Assert.All(
				result.ItemsToCreate.Where(item => item.Name == "UnitB"),
				item => Assert.False(item.IsSelected));
			Assert.DoesNotContain(result.ItemsToCreate, item => item.Name == "UnitC");
			Assert.Contains(result.Diagnostics, d => d.Message == "[INFO] LCU;Machine;Module:UnitC=sameVersion");
			Assert.DoesNotContain(result.ItemsToCreate, item => item.Name == "NotOnMachine");
			Assert.Contains(result.Diagnostics, d => d.Message ==
				"[INFO] LCU;Machine;Module:NotOnMachine=skip (not in machine UpdateCommon.inf; not required)");
			Assert.Contains(result.Diagnostics, d => d.Message.Contains(":UnitB=") && d.Message.EndsWith(expectedLogSuffix));
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}
}
