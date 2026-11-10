using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class UpdateCommonService_InspectTests
{
	[Fact]
	public void Inspect_ClassifiesVersionMatchesMismatchesAndUnknownUnits()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			string infoPath = Path.Combine(root, "UpdateCommon.inf");
			File.WriteAllText(
				infoPath,
				"[Matched]\nVersion=1.0\n[Mismatched]\nVersion=2.0\n[Unknown]\nVersion=3.0\n");
			UpdateCommonVersionUnit[] units =
			[
				new("Matched", "1.0"),
				new("Mismatched", "1.1")
			];

			UpdateCommonVersionInspectionResult result =
				new UpdateCommonService().Inspect(infoPath, units);

			Assert.True(result.FileExists);
			Assert.Null(result.ReadError);
			Assert.Collection(
				result.Entries,
				entry =>
				{
					Assert.Equal("Matched", entry.Section);
					Assert.Equal(UpdateCommonVersionStatus.Matched, entry.Status);
				},
				entry =>
				{
					Assert.Equal("Mismatched", entry.Section);
					Assert.Equal(UpdateCommonVersionStatus.Mismatched, entry.Status);
					Assert.Equal("1.1", entry.CurrentVersion);
				},
				entry =>
				{
					Assert.Equal("Unknown", entry.Section);
					Assert.Equal(UpdateCommonVersionStatus.UnitNotFound, entry.Status);
				});
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void Inspect_ReportsMissingFile()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			UpdateCommonVersionInspectionResult result =
				new UpdateCommonService().Inspect(
					Path.Combine(root, "missing.inf"),
					[]);

			Assert.False(result.FileExists);
			Assert.Null(result.ReadError);
			Assert.Empty(result.Entries);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void Inspect_ReportsMalformedIniReadError()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			string infoPath = Path.Combine(root, "UpdateCommon.inf");
			File.WriteAllText(infoPath, "Version=1.0\n");

			UpdateCommonVersionInspectionResult result =
				new UpdateCommonService().Inspect(infoPath, []);

			Assert.True(result.FileExists);
			Assert.NotNull(result.ReadError);
			Assert.Empty(result.Entries);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void Inspect_UsesFirstUnitWhenNamesAreDuplicated()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			string infoPath = Path.Combine(root, "UpdateCommon.inf");
			File.WriteAllText(infoPath, "[UnitA]\nVersion=1.0\n");
			UpdateCommonVersionUnit[] units =
			[
				new("UnitA", "1.0"),
				new("UnitA", "2.0")
			];

			UpdateCommonVersionInspectionResult result =
				new UpdateCommonService().Inspect(infoPath, units);

			Assert.Equal(UpdateCommonVersionStatus.Matched, Assert.Single(result.Entries).Status);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void Inspect_ThrowsWhenCancelledBeforeInspecting()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		Assert.Throws<OperationCanceledException>(() =>
			new UpdateCommonService().Inspect(
				"UpdateCommon.inf",
				[],
				cancellation.Token));
	}

	private static string CreateTemporaryDirectory()
	{
		string path = Path.Combine(Path.GetTempPath(), $"UpdateCommonVersionInspectorTests-{Guid.NewGuid():N}");
		Directory.CreateDirectory(path);
		return path;
	}
}
