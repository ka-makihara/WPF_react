using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class UnitVersionListService_SelectCandidatesTests
{
	[Fact]
	public void Select_ThrowsWhenCancelledDuringMachineVersionIndexing()
	{
		using CancellationTokenSource cancellation = new();
		IReadOnlyList<MachineUnitVersion> machineVersions = new CancellingMachineVersionList(
			() => cancellation.Cancel());

		Assert.Throws<OperationCanceledException>(() =>
			new UnitVersionListService().SelectCandidates(
				[],
				machineVersions,
				ignoreVersion: false,
				cancellation.Token));
	}

	[Fact]
	public void Select_OnlyReturnsMachineUnitsAndSkipsMatchingVersions()
	{
		UpdateVersionCandidate[] updates =
		[
			new("Same", "1.0"),
			new("Different", "2.0"),
			new("NotOnMachine", "3.0"),
			new("", "4.0")
		];
		MachineUnitVersion[] machineVersions =
		[
			new("Same", "1.0"),
			new("Different", "1.9")
		];

		UnitVersionSelectionResult result = new UnitVersionListService().SelectCandidates(
			updates,
			machineVersions,
			ignoreVersion: false);

		Assert.Equal("Different", Assert.Single(result.Candidates).Name);
		Assert.Equal("Same", Assert.Single(result.SkippedSameVersion).Name);
		Assert.Equal("NotOnMachine", Assert.Single(result.SkippedNotOnMachine).Name);
	}

	[Fact]
	public void Select_IncludesMatchingVersionWhenVersionCheckIsIgnored()
	{
		UpdateVersionCandidate[] updates = [new("Same", "1.0")];
		MachineUnitVersion[] machineVersions = [new("Same", "1.0")];

		UnitVersionSelectionResult result = new UnitVersionListService().SelectCandidates(
			updates,
			machineVersions,
			ignoreVersion: true);

		Assert.Equal("Same", Assert.Single(result.Candidates).Name);
		Assert.Empty(result.SkippedSameVersion);
	}

	[Fact]
	public void Select_UsesCurrentVersionIndexAndSkipsMatchingVersions()
	{
		UpdateVersionCandidate[] updates =
		[
			new("Same", "1.0"),
			new("Different", "2.0"),
			new("NotOnMachine", "3.0")
		];
		Dictionary<string, string> currentVersions = new(StringComparer.Ordinal)
		{
			["Same"] = "1.0",
			["Different"] = "1.5"
		};

		UnitVersionSelectionResult result = new UnitVersionListService().SelectCandidates(
			updates,
			currentVersions,
			ignoreVersion: false);

		Assert.Equal("Different", Assert.Single(result.Candidates).Name);
		Assert.Equal("Same", Assert.Single(result.SkippedSameVersion).Name);
		Assert.Equal("NotOnMachine", Assert.Single(result.SkippedNotOnMachine).Name);
	}

	private sealed class CancellingMachineVersionList(Action onFirstRead) : IReadOnlyList<MachineUnitVersion>
	{
		public int Count => 1;

		public MachineUnitVersion this[int index]
		{
			get
			{
				onFirstRead();
				return new("UnitA", "1.0");
			}
		}

		public IEnumerator<MachineUnitVersion> GetEnumerator()
		{
			yield return this[0];
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
			GetEnumerator();
	}
}
