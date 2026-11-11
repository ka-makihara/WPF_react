using WpfApp1_cmd.Models;
using WpfApp1_cmd.Resources;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class ConfigSupportTests
{
	[Fact]
	public void ReadConfig_DeserializesSupportFlags()
	{
		Assert.True(Config.ReadConfig());

		Assert.NotEmpty(Config.units);
		Assert.False(Config.units.Single(unit => unit.GetName() == "Copla").support);
		Assert.False(Config.units.Single(unit => unit.GetName() == "TrayPLC").support);
		Assert.True(Config.units.Single(unit => unit.GetName() == "FM-65").support);
	}

	[Fact]
	public void IsUnitSupported_ReturnsConfiguredSupportFlag()
	{
		Assert.True(Config.ReadConfig());
		List<UnitLink> originalUnits = Config.units;
		Config.units =
		[
			CreateUnitLink("supported-unit", support: true),
			CreateUnitLink("unsupported-unit", support: false)
		];

		try
		{
			Assert.True(Config.IsUnitSupported("supported-unit"));
			Assert.False(Config.IsUnitSupported("unsupported-unit"));
			Assert.True(Config.IsUnitSupported("unconfigured-unit"));
		}
		finally
		{
			Config.units = originalUnits;
		}
	}

	[Fact]
	public void SupportDefaultsToTrue_ForConfigurationsWithoutFlag()
	{
		Assert.True(new UnitLink().support);
	}

	[Fact]
	public void UnsupportedUnit_CannotBeSelectedAndShowsUnsupportedStatus()
	{
		UnitVersion unit = new(sel: true)
		{
			IsSupported = false
		};

		unit.IsSelected.Value = true;

		Assert.False(unit.IsSelected.Value);
		Assert.False(unit.IsUpdatable);
		Assert.Equal(Resource.NotApplicable, unit.UpdateStatus);
	}

	private static UnitLink CreateUnitLink(string component, bool support) => new()
	{
		name = new Dictionary<string, object>(),
		components = [component],
		files = [],
		support = support
	};
}
