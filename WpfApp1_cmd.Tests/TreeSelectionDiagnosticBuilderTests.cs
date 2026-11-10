using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class TreeSelectionDiagnosticBuilderTests
{
	[Fact]
	public void Build_FormatsTreeInDepthFirstOrderAndPreservesNullableState()
	{
		IReadOnlyList<string> messages = new TreeSelectionDiagnosticBuilder().Build(
			[
				new TreeSelectionLcuInput(
					"LineA",
					true,
					[
						new TreeSelectionMachineInput(
							"MachineA",
							null,
							[
								new TreeSelectionModuleInput(
									"ModuleA",
									false,
									[
										new TreeSelectionUnitInput("UnitA", true),
										new TreeSelectionUnitInput("UnitB", null)
									])
							])
					])
			]);

		Assert.Equal(
			[
				"LineA:True",
				"    MachineA:Null",
				"       ModuleA:False",
				"          UnitA:True",
				"          UnitB:Null"
			],
			messages);
	}

	[Fact]
	public void Build_ReturnsNoMessagesForEmptyTree()
	{
		Assert.Empty(new TreeSelectionDiagnosticBuilder().Build([]));
	}
}
