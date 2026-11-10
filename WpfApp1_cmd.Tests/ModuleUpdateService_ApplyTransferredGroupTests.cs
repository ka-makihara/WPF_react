using Xunit;
using WpfApp1_cmd.Services;

public class ModuleUpdateService_ApplyTransferredGroupTests
{
	[Theory]
	[InlineData("/Fuji/UnitA.bin", false)]
	[InlineData(@"\Fuji\UnitA.bin", false)]
	[InlineData("fuji/unita.BIN", false)]
	[InlineData("/Fuji/UnitA.bin", true)]
	public void ApplyTransferredGroup_NormalizesInfPathSeparatorsAndCasing(
		string infPath,
		bool matchesFuser)
	{
		string[] lines = ["[UnitA]", "Version=1.0"];
		IReadOnlyList<ModuleUpdateStateChange> changes = new ModuleUpdateService().ApplyTransferredGroup(
			[new ModuleUpdateUnit("UnitA", @"C:\Update\Fuji\UnitA.bin", "")],
			[new ModuleUpdateVersion("UnitA", matchesFuser ? "/Other.bin" : infPath,
				matchesFuser ? infPath : "", "1.0", "2.0")],
			@"C:\Update",
			lines);

		Assert.True(Assert.Single(changes).IsUpdateCommonVersionChanged);
		Assert.Equal(["[UnitA]", "Version=2.0"], lines);
	}

	[Fact]
	public void ApplyTransferredGroup_UpdatesInfForMatchingPathsAndReturnsUnitState()
	{
		string[] updateCommonLines = ["[UnitA]", "Version=1.0", "[UnitB]", "Version=1.0"];
		var units = new[]
		{
			new ModuleUpdateUnit("UnitA", @"C:\Update\Fuji\UnitA.bin", ""),
			new ModuleUpdateUnit("UnitB", @"C:\Update\Fuji\UnitB.bin", ""),
			new ModuleUpdateUnit("UnitC", @"C:\Update\Fuji\UnitC.bin", "")
		};
		var versions = new[]
		{
			new ModuleUpdateVersion("UnitA", @"\Fuji\UnitA.bin", "", "1.0", "2.0"),
			new ModuleUpdateVersion("UnitB", @"\Fuji\Other.bin", "", "1.0", "3.0"),
			new ModuleUpdateVersion("UnitC", @"\Fuji\UnitC.bin", "", "1.0", "4.0")
		};

		var changes = new ModuleUpdateService().ApplyTransferredGroup(
			units,
			versions,
			@"C:\Update",
			updateCommonLines);

		Assert.Equal(3, changes.Count);
		Assert.True(changes[0].IsUpdateCommonVersionChanged);
		Assert.False(changes[1].IsUpdateCommonVersionChanged);
		Assert.True(changes[2].IsUpdateCommonVersionChanged);
		Assert.Equal(["[UnitA]", "Version=2.0", "[UnitB]", "Version=1.0"], updateCommonLines);
	}

	[Fact]
	public void ApplyTransferredGroup_IgnoresUnitsWithoutMatchingVersionOrPath()
	{
		var changes = new ModuleUpdateService().ApplyTransferredGroup(
			[
				new ModuleUpdateUnit("MissingVersion", @"C:\Update\Fuji\file.bin", ""),
				new ModuleUpdateUnit("MissingPath", null, "")
			],
			[new ModuleUpdateVersion("MissingPath", null, null, "1.0", "2.0")],
			@"C:\Update",
			["[Unit]", "Version=1.0"]);

		Assert.Empty(changes);
	}

	[Fact]
	public void ApplyTransferredGroup_MatchesPathsWhenRootAndUnitCasingDiffer()
	{
		string[] updateCommonLines = ["[UnitA]", "Version=1.0"];

		IReadOnlyList<ModuleUpdateStateChange> changes =
			new ModuleUpdateService().ApplyTransferredGroup(
				[new ModuleUpdateUnit("UnitA", @"c:\UPDATE\Fuji\UnitA.bin", "")],
				[new ModuleUpdateVersion("UnitA", @"\Fuji\UnitA.bin", "", "1.0", "2.0")],
				@"C:\Update",
				updateCommonLines);

		Assert.True(Assert.Single(changes).IsUpdateCommonVersionChanged);
		Assert.Equal(["[UnitA]", "Version=2.0"], updateCommonLines);
	}

	[Fact]
	public void ApplyTransferredGroup_UsesFirstVersionWhenUnitNamesAreDuplicated()
	{
		IReadOnlyList<ModuleUpdateStateChange> changes =
			new ModuleUpdateService().ApplyTransferredGroup(
				[new ModuleUpdateUnit("UnitA", @"C:\Update\UnitA.bin", null)],
				[
					new ModuleUpdateVersion("UnitA", @"\UnitA.bin", null, "1.0", "2.0"),
					new ModuleUpdateVersion("UnitA", @"\UnitA.bin", null, "1.0", "3.0")
				],
				@"C:\Update",
				["[UnitA]", "Version=1.0"]);

		Assert.Equal("2.0", Assert.Single(changes).NewVersion);
	}

	[Fact]
	public void ApplyTransferredGroup_MatchesFirstVersionWithNullUnitName()
	{
		IReadOnlyList<ModuleUpdateStateChange> changes =
			new ModuleUpdateService().ApplyTransferredGroup(
				[new ModuleUpdateUnit(null, @"C:\Update\UnitA.bin", null)],
				[
					new ModuleUpdateVersion(null, @"\UnitA.bin", null, "1.0", "2.0"),
					new ModuleUpdateVersion(null, @"\UnitA.bin", null, "1.0", "3.0")
				],
				@"C:\Update",
				[]);

		Assert.Equal("2.0", Assert.Single(changes).NewVersion);
	}

	[Fact]
	public void ApplyTransferredGroup_ThrowsWhenCancelledBeforeProcessing()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		Assert.Throws<OperationCanceledException>(() =>
			new ModuleUpdateService().ApplyTransferredGroup(
				[],
				[],
				@"C:\Update",
				[],
				cancellation.Token));
	}
}
