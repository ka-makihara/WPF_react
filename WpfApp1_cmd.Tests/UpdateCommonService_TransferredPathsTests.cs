using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class UpdateCommonService_TransferredPathsTests
{
	[Fact]
	public void SetTransferredFilePaths_UpdatesOnlySuccessfulUnitAndAddsFuserPath()
	{
		string[] original =
		[
			"[Updated]", "Attribute=0", "Version=2.0", "Path=/old.bin",
			"[Unchanged]", "Version=1.0", "Path=/keep.bin", "FuserPath=/keep-fuser.bin"
		];

		string[] result = UpdateCommonService.SetTransferredFilePaths(original,
			[new("Updated", "/new.bin", "/new-fuser.bin", "1.0", "2.0")]);

		Assert.Equal(
		[
			"[Updated]", "Attribute=0", "Version=2.0", "Path=/new.bin", "FuserPath=/new-fuser.bin",
			"[Unchanged]", "Version=1.0", "Path=/keep.bin", "FuserPath=/keep-fuser.bin"
		], result);
		Assert.Equal("Path=/old.bin", original[3]);
	}

	[Fact]
	public void SetTransferredFilePaths_ClearsOldFuserAndAddsMissingPathWithinSection()
	{
		string[] result = UpdateCommonService.SetTransferredFilePaths(
			["[Updated]", "Version=2.0", "FUserPath=/old-fuser.bin", "[Other]", "Path=/keep.bin"],
			[new("Updated", "/new.bin", "", "1.0", "2.0")]);

		Assert.Equal(
			["[Updated]", "Version=2.0", "FUserPath=", "Path=/new.bin", "[Other]", "Path=/keep.bin"],
			result);
	}

	[Fact]
	public void SetTransferredFilePaths_CancellationDoesNotMutateInput()
	{
		string[] original = ["[Updated]", "Path=/old.bin"];
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();
		Assert.Throws<OperationCanceledException>(() =>
			UpdateCommonService.SetTransferredFilePaths(original,
				[new("Updated", "/new.bin", "", "1.0", "2.0")], cancellation.Token));
		Assert.Equal(["[Updated]", "Path=/old.bin"], original);
	}
}
