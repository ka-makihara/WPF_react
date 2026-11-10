using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public class UpdateCommonService_SetVersionTests
{
	[Fact]
	public void SetVersion_UpdatesVersionInMatchingSection()
	{
		string[] lines =
		[
			"[First]",
			"Version=1.00",
			"[Second]",
			"Version=2.00"
		];

		UpdateCommonService.SetVersion(lines, "Second", "2.01");

		Assert.Equal("Version=1.00", lines[1]);
		Assert.Equal("Version=2.01", lines[3]);
	}

	[Fact]
	public void SetVersion_DoesNotChangeOtherSections()
	{
		string[] lines =
		[
			"[First]",
			"Version=1.00",
			"[Second]",
			"Version=2.00"
		];

		UpdateCommonService.SetVersion(lines, "Missing", "3.00");

		Assert.Equal(["[First]", "Version=1.00", "[Second]", "Version=2.00"], lines);
	}

	[Fact]
	public void SetVersion_OnlyChangesFirstVersionEntryInMatchingSection()
	{
		string[] lines =
		[
			"[First]",
			"Version=1.00",
			"[Second]",
			"Version=2.00",
			"Version=2.01"
		];

		UpdateCommonService.SetVersion(lines, "Second", "2.02");

		Assert.Equal("Version=2.02", lines[3]);
		Assert.Equal("Version=2.01", lines[4]);
	}

	[Fact]
	public void SetVersion_ThrowsWhenCancelledBeforeUpdating()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();
		string[] lines = ["[UnitA]", "Version=1.0"];

		Assert.Throws<OperationCanceledException>(() =>
			UpdateCommonService.SetVersion(
				lines,
				"UnitA",
				"2.0",
				cancellation.Token));

		Assert.Equal("Version=1.0", lines[1]);
	}

	[Fact]
	public void SetVersions_PreservesFirstSectionAndUpdateOrder()
	{
		string[] lines =
		[
			"[UnitA]",
			"Version=1.0",
			"[UnitA]",
			"Version=1.1",
			"[UnitB]",
			"Version=2.0"
		];

		UpdateCommonService.SetVersions(
			lines,
			[("UnitA", "3.0"), ("UnitA", "4.0"), ("UnitB", "5.0")]);

		Assert.Equal(
			["[UnitA]", "Version=4.0", "[UnitA]", "Version=1.1", "[UnitB]", "Version=5.0"],
			lines);
	}

	[Fact]
	public void SetVersions_PreservesExistingSearchAcrossFollowingSections()
	{
		string[] lines = ["[UnitA]", "[UnitB]", "Version=1.0"];

		UpdateCommonService.SetVersions(lines, [("UnitA", "2.0")]);

		Assert.Equal(["[UnitA]", "[UnitB]", "Version=2.0"], lines);
	}

	[Fact]
	public void SetVersions_DoesNotPartiallyUpdateWhenCancelled()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();
		string[] lines = ["[UnitA]", "Version=1.0"];

		Assert.Throws<OperationCanceledException>(() =>
			UpdateCommonService.SetVersions(
				lines,
				[("UnitA", "2.0")],
				cancellation.Token));

		Assert.Equal("Version=1.0", lines[1]);
	}
}
