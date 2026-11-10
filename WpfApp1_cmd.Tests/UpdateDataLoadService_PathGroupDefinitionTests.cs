using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class UpdateDataLoadService_PathGroupDefinitionTests
{
	[Fact]
	public void Build_PreservesInputOrderNamesComponentsAndDefaultGroup()
	{
		UpdatePathGroupDefinition[] result = new UpdateDataLoadService()
			.BuildPathGroupDefinitions(
				[
					new UpdatePathGroupInput(
						["UnitA", "UnitB"],
						[
							new KeyValuePair<string, string>("*", "GroupA"),
							new KeyValuePair<string, string>("Machine", "MachineA")
						],
						"File"),
					new UpdatePathGroupInput(
						["UnitC"],
						[new KeyValuePair<string, string>("Machine", "MachineB")],
						"Folder")
				])
			.ToArray();

		Assert.Equal(2, result.Length);
		Assert.Equal(["UnitA", "UnitB"], result[0].Components);
		Assert.Equal("GroupA", result[0].GroupName);
		Assert.Equal(["GroupA", "MachineA"], result[0].GroupNames);
		Assert.Equal("File", result[0].Mode);
		Assert.Equal(["UnitC"], result[1].Components);
		Assert.Equal("", result[1].GroupName);
		Assert.Equal(["MachineB"], result[1].GroupNames);
		Assert.Equal("Folder", result[1].Mode);
	}

	[Fact]
	public void Build_ThrowsWhenCancellationIsRequested()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		Assert.Throws<OperationCanceledException>(() =>
			new UpdateDataLoadService().BuildPathGroupDefinitions([], cancellation.Token));
	}
}
