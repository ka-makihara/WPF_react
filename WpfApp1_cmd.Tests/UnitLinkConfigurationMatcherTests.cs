using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class UnitLinkConfigurationMatcherTests
{
	[Fact]
	public void HasSameName_MatchesSameKeysAndOrdinalValues()
	{
		Dictionary<string, object> existing = new(StringComparer.Ordinal)
		{
			["name"] = "Control",
			["version"] = 2
		};
		Dictionary<string, string> configured = new(StringComparer.Ordinal)
		{
			["version"] = "2",
			["name"] = "Control"
		};

		Assert.True(UnitLinkConfigurationMatcher.HasSameName(existing, configured));
	}

	[Fact]
	public void HasSameName_RejectsDifferentCountOrValueCase()
	{
		Dictionary<string, object> existing = new(StringComparer.Ordinal)
		{
			["name"] = "Control"
		};

		Assert.False(UnitLinkConfigurationMatcher.HasSameName(
			existing,
			new Dictionary<string, string>(StringComparer.Ordinal)
			{
				["name"] = "Control",
				["extra"] = "value"
			}));
		Assert.False(UnitLinkConfigurationMatcher.HasSameName(
			existing,
			new Dictionary<string, string>(StringComparer.Ordinal)
			{
				["name"] = "control"
			}));
	}

	[Fact]
	public void HasSameName_RejectsDifferentKeys()
	{
		Assert.False(UnitLinkConfigurationMatcher.HasSameName(
			new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["Name"] = "Control"
			},
			new Dictionary<string, string>(StringComparer.Ordinal)
			{
				["name"] = "Control"
			}));
	}
}
