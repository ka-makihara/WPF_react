using System.IO;
using WpfApp1_cmd.Services;
using WpfLcuCtrlLib;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class UnitVersionListService_CreateTests
{
	[Theory]
	[InlineData(true, true, true)]
	[InlineData(false, true, false)]
	[InlineData(true, false, false)]
	public void Create_ChecksUpdateDataPathsInsteadOfMachinePaths(
		bool updateExists, bool fuserExists, bool expectedSelected)
	{
		string root = Path.Combine(Path.GetTempPath(), $"UnitVersionList-{Guid.NewGuid():N}");
		Directory.CreateDirectory(root);
		try
		{
			if (updateExists)
			{
				File.WriteAllText(Path.Combine(root, "update.bin"), "payload");
			}
			if (fuserExists)
			{
				File.WriteAllText(Path.Combine(root, "fuser.bin"), "payload");
			}
			string iniPath = Path.Combine(root, "UpdateCommon.inf");
			File.WriteAllText(iniPath,
				"[Palette_MFU63_FPGA]\nPath=\\machine-only.jam\nAttribute=0\nVersion=1.0\n");

			UnitVersionListResult result = new UnitVersionListService().Create(
				new UnitVersionListRequest(
					[new("Palette_MFU63_FPGA", "2.0", "\\update.bin", "\\fuser.bin")],
					[],
					new IniFileParser(iniPath),
					[],
					"MachineA",
					root,
					IgnoreVersion: true,
					IsModuleSelected: true,
					IsBackupData: false,
					IsBaseUpdateInfo: false,
					UnifyGroupSelection: false,
					NotUpdateAttribute: 2,
					MissingFileMode: UnitVersionMissingFileMode.ListOnly,
					LcuName: "LCU",
					MachineName: "Machine_1",
					ModuleName: "1-L"));

			UnitVersionPreparedDefinition unit = Assert.Single(result.ItemsToCreate);
			Assert.Equal(expectedSelected, unit.IsSelected);
			Assert.Equal(!expectedSelected, unit.IsUpdateFileMissing);
			Assert.Equal("\\update.bin", unit.Path);
			Assert.Equal(fuserExists ? "\\fuser.bin" : "", unit.FuserPath);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Theory]
	[InlineData(UnitVersionMissingFileMode.Exclude, 0)]
	[InlineData(UnitVersionMissingFileMode.ListOnly, 1)]
	[InlineData(UnitVersionMissingFileMode.Selectable, 1)]
	public void Create_RunsAllStepsAndHandlesMissingUpdateFile(
		UnitVersionMissingFileMode mode,
		int expectedMissingItems)
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
			Assert.DoesNotContain(result.Diagnostics, d => d.Message.Contains("Update file not found"));
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}
}
