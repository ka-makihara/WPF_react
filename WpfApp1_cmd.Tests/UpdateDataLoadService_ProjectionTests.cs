using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class UpdateDataLoadService_ProjectionTests
{
	private readonly UpdateDataLoadService _service = new();

	[Fact]
	public void CreateUpdateInfoProjections_PreservesEntryValuesAndVisibility()
	{
		UpdateInfoProjection projection = Assert.Single(
			_service.CreateUpdateInfoProjections(
				[new("UnitA", "A", "/unit.bin", "1.0", "/fuser.bin", "Control")]));

		Assert.Equal("UnitA", projection.Name);
		Assert.Equal("A", projection.Attribute);
		Assert.Equal("/unit.bin", projection.Path);
		Assert.Equal("1.0", projection.Version);
		Assert.Equal("/fuser.bin", projection.FuserPath);
		Assert.Equal("Control", projection.UnitGroup);
		Assert.True(projection.IsVisible);
	}

	[Fact]
	public void CreatePathGroupProjections_NormalizesNullsAndDeduplicatesByNameAndPath()
	{
		UpdatePathGroupProjection projection = Assert.Single(
			_service.CreatePathGroupProjections(
			[
				new("Control",
				[
					new("UnitA", "/unit.bin", "/fuser.bin"),
					new("UnitA", "/unit.bin", "/different-fuser.bin"),
					new("UnitB", null, null)
				]),
				new("Empty", [])
			]));

		Assert.Equal("Control", projection.GroupName);
		Assert.Collection(
			projection.Files,
			file =>
			{
				Assert.Equal("UnitA", file.Name);
				Assert.Equal("/unit.bin", file.Path);
				Assert.Equal("/fuser.bin", file.FusePath);
			},
			file =>
			{
				Assert.Equal("UnitB", file.Name);
				Assert.Equal("", file.Path);
				Assert.Equal("", file.FusePath);
			});
	}

	[Fact]
	public void CreatePathGroupProjections_DoesNotMergeDifferentNamesOrPaths()
	{
		UpdatePathGroupProjection projection = Assert.Single(
			_service.CreatePathGroupProjections(
			[
				new("Control",
				[
					new("UnitA", "/unit.bin", ""),
					new("UnitB", "/unit.bin", ""),
					new("UnitA", "/other.bin", "")
				])
			]));

		Assert.Equal(3, projection.Files.Count);
	}

	[Fact]
	public void CreatePathGroupProjections_ThrowsWhenCancelled()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		Assert.Throws<OperationCanceledException>(() =>
			_service.CreatePathGroupProjections([], cancellation.Token));
	}
}
