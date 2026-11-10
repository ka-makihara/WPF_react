using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class UnitVersionListService_ResolveGroupTests
{
	[Fact]
	public void Resolve_PrefersMachineSpecificGroup()
	{
		UnitVersionGroupDefinition[] groups =
		[
			new(["UnitA"], new Dictionary<string, string>
			{
				["*"] = "Default",
				["MachineA"] = "Specific"
			})
		];

		string result = new UnitVersionListService().ResolveGroup("UnitA", "MachineA", groups);

		Assert.Equal("Specific", result);
	}

	[Fact]
	public void Resolve_UsesDefaultGroupWhenMachineSpecificGroupIsMissing()
	{
		UnitVersionGroupDefinition[] groups =
		[
			new(["UnitA"], new Dictionary<string, string> { ["*"] = "Default" })
		];

		string result = new UnitVersionListService().ResolveGroup("UnitA", "MachineB", groups);

		Assert.Equal("Default", result);
	}

	[Fact]
	public void Resolve_ReturnsEmptyWhenUnitOrGroupNameIsUnavailable()
	{
		UnitVersionGroupDefinition[] groups =
		[
			new(["UnitB"], new Dictionary<string, string> { ["MachineA"] = "Specific" }),
			new(["UnitA"], new Dictionary<string, string>())
		];

		UnitVersionListService resolver = new();

		Assert.Equal("", resolver.ResolveGroup("UnitA", "MachineA", groups));
		Assert.Equal("", resolver.ResolveGroup("UnitC", "MachineA", groups));
	}

	[Fact]
	public void CreateIndex_PreservesFirstMatchingGroup()
	{
		UnitVersionGroupDefinition[] groups =
		[
			new(["UnitA"], new Dictionary<string, string> { ["*"] = "First" }),
			new(["UnitA"], new Dictionary<string, string> { ["*"] = "Second" })
		];
		UnitVersionListService resolver = new();
		IReadOnlyDictionary<string, UnitVersionGroupDefinition> index =
			resolver.CreateGroupIndex(groups);

		Assert.Equal("First", resolver.ResolveGroup("UnitA", "MachineA", index));
	}
}
