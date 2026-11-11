using Xunit;
using WpfApp1_cmd.Services;

public class ModuleUpdateService_ExecuteTests
{
	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task ExecuteAsync_InspectionUsesNewVersionOnlyForSuccessfullyUpdatedInf(bool transferSucceeds)
	{
		string[] names = ["Updated", "PathMismatch", "NotSelected", "Failed"];
		ModuleUpdateVersion[] versions = names.Select(name =>
			new ModuleUpdateVersion(name, $"/{name}.bin", "", "1.0", "2.0")).ToArray();
		UpdateCommonVersionUnit[] expected = names.Select(name =>
			new UpdateCommonVersionUnit(name, "1.0")).ToArray();
		UnitVersionIndexLookup lookup = new UnitVersionIndexLookupBuilder().Build(names);
		string[] lines = names.SelectMany(name =>
			new[] { $"[{name}]", "Version=1.0", $"Path=/old-{name}.bin" }).ToArray();
		List<ModuleUpdateVersion> transferredVersions = [];
		ModuleFileTransferPlan first = new(
			"first",
			["Updated.bin", "Other.bin"],
			["MCFiles/Updated.bin", "MCFiles/Other.bin"],
			[
				new UpdateFileListEntry("Updated", @"C:\Update\Updated.bin", ""),
				new UpdateFileListEntry("PathMismatch", @"C:\Update\Other.bin", "")
			]);
		ModuleFileTransferPlan failed = new(
			"failed",
			["Failed.bin"],
			["MCFiles/Failed.bin"],
			[new UpdateFileListEntry("Failed", @"C:\Update\Failed.bin", "")]);

		await CreateService().ExecuteAsync(
			[first, failed],
			versions,
			@"C:\Update",
			lines,
			(_, _) => Task.FromResult(true),
			(plan, _, _, _) => Task.FromResult(plan.GroupName == "first" && transferSucceeds),
			(_, _) => { },
			(_, changes) =>
			{
				foreach (ModuleUpdateStateChange change in changes)
				{
					if (change.IsUpdateCommonVersionChanged && lookup.FindIndex(change.Name) is int index)
					{
						transferredVersions.Add(versions[index]);
						expected[index] = new UpdateCommonVersionUnit(expected[index].Name, change.NewVersion);
					}
				}
				return Task.CompletedTask;
			},
			CancellationToken.None);

		lines = UpdateCommonService.SetTransferredFilePaths(lines, transferredVersions);
		Assert.Equal(transferSucceeds ? "Path=/Updated.bin" : "Path=/old-Updated.bin", lines[2]);
		Assert.Contains("Path=/old-PathMismatch.bin", lines);
		Assert.Contains("Path=/old-NotSelected.bin", lines);
		Assert.Contains("Path=/old-Failed.bin", lines);
		Assert.Equal(transferSucceeds ? "2.0" : "1.0", expected[0].CurrentVersion);
		Assert.All(expected.Skip(1), unit => Assert.Equal("1.0", unit.CurrentVersion));
		Assert.All(versions, unit => Assert.Equal("1.0", unit.CurrentVersion));
		string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"Inspection-{Guid.NewGuid():N}.inf");
		try
		{
			await System.IO.File.WriteAllLinesAsync(path, lines);
			UpdateCommonVersionInspectionResult inspection = new UpdateCommonService().Inspect(path, expected);
			Assert.Equal(names.Length, inspection.Entries.Count);
			Assert.All(inspection.Entries, entry => Assert.Equal(UpdateCommonVersionStatus.Matched, entry.Status));

			lines[1] = "Version=unexpected";
			await System.IO.File.WriteAllLinesAsync(path, lines);
			inspection = new UpdateCommonService().Inspect(path, expected);
			Assert.Equal(UpdateCommonVersionStatus.Mismatched, inspection.Entries[0].Status);
		}
		finally
		{
			System.IO.File.Delete(path);
		}
	}

	[Fact]
	public async Task ExecuteAsync_SendsBatchesAndReportsProgressBeforeGroupCompletion()
	{
		var plan = CreatePlan("group", 11);
		List<int> batchSizes = [];
		List<string> events = [];
		var service = CreateService();

		ModuleUpdateTransferResult result = await service.ExecuteAsync(
			[plan],
			[],
			"root",
			[],
			(_, _) =>
			{
				events.Add("start");
				return Task.FromResult(true);
			},
			(_, machineFiles, lcuFiles, _) =>
			{
				Assert.Equal(machineFiles.Count, lcuFiles.Count);
				batchSizes.Add(machineFiles.Count);
				return Task.FromResult(true);
			},
			(_, count) => events.Add($"batch:{count}"),
			(_, changes) =>
			{
				Assert.Empty(changes);
				events.Add("complete");
				return Task.CompletedTask;
			},
			CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.Equal(11, result.TransferredFileCount);
		Assert.Equal(0, result.FailedGroupCount);
		Assert.Equal([10, 1], batchSizes);
		Assert.Equal(["start", "batch:10", "batch:1", "complete"], events);
	}

	[Fact]
	public async Task ExecuteAsync_StopsAfterFailureAndDoesNotCompleteFailedGroup()
	{
		var first = CreatePlan("first", 1);
		var second = CreatePlan("second", 11);
		int completedGroups = 0;
		int sentBatches = 0;

		ModuleUpdateTransferResult result = await CreateService().ExecuteAsync(
			[first, second],
			[],
			"root",
			[],
			(_, _) => Task.FromResult(true),
			(_, _, _, _) => Task.FromResult(++sentBatches < 2),
			(_, _) => { },
			(_, _) =>
			{
				completedGroups++;
				return Task.CompletedTask;
			},
			CancellationToken.None);

		Assert.False(result.IsSuccess);
		Assert.Equal(1, result.TransferredFileCount);
		Assert.Equal(1, result.FailedGroupCount);
		Assert.Equal("second", result.FailedGroupName);
		Assert.Equal(1, completedGroups);
		Assert.Equal(2, sentBatches);
	}

	[Fact]
	public async Task ExecuteAsync_DoesNotSendGroupWhenStartIsDeclined()
	{
		int sendCount = 0;
		var plan = CreatePlan("cancelled", 1);

		ModuleUpdateTransferResult result = await CreateService().ExecuteAsync(
			[plan],
			[],
			"root",
			[],
			(_, _) => Task.FromResult(false),
			(_, _, _, _) =>
			{
				sendCount++;
				return Task.FromResult(true);
			},
			(_, _) => { },
			(_, _) => Task.CompletedTask,
			CancellationToken.None);

		Assert.False(result.IsSuccess);
		Assert.Equal("cancelled", result.FailedGroupName);
		Assert.Equal(0, sendCount);
	}

	[Fact]
	public async Task ExecuteAsync_PropagatesCancellationWhenStartCallbackReturnsFalseAfterCancellation()
	{
		using CancellationTokenSource cancellation = new();
		int sendCount = 0;

		await Assert.ThrowsAsync<OperationCanceledException>(() =>
			CreateService().ExecuteAsync(
				[CreatePlan("cancelled", 1)],
				[],
				"root",
				[],
				(_, token) =>
				{
					Assert.Equal(cancellation.Token, token);
					cancellation.Cancel();
					return Task.FromResult(false);
				},
				(_, _, _, _) =>
				{
					sendCount++;
					return Task.FromResult(true);
				},
				(_, _) => { },
				(_, _) => Task.CompletedTask,
				cancellation.Token));

		Assert.Equal(0, sendCount);
	}

	[Fact]
	public async Task ExecuteAsync_ThrowsWhenCancelledBeforeProcessingEmptyPlans()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		await Assert.ThrowsAsync<OperationCanceledException>(() =>
			CreateService().ExecuteAsync(
				[],
				[],
				"root",
				[],
				(_, _) => Task.FromResult(true),
				(_, _, _, _) => Task.FromResult(true),
				(_, _) => { },
				(_, _) => Task.CompletedTask,
				cancellation.Token));
	}

	[Fact]
	public async Task ExecuteAsync_StopsWhenCancelledWhileCreatingStateUnits()
	{
		using CancellationTokenSource cancellation = new();
		ModuleFileTransferPlan plan = new(
			"group",
			["machine-file"],
			["lcu-file"],
			new CancellingUnitList(
				[new UpdateFileListEntry("UnitA", @"C:\Data\UnitA.bin", "")],
				cancellation));
		int completedGroups = 0;

		await Assert.ThrowsAsync<OperationCanceledException>(() =>
			CreateService().ExecuteAsync(
				[plan],
				[],
				@"C:\Data",
				[],
				(_, _) => Task.FromResult(true),
				(_, _, _, _) => Task.FromResult(true),
				(_, _) => { },
				(_, _) =>
				{
					completedGroups++;
					return Task.CompletedTask;
				},
				cancellation.Token));

		Assert.Equal(0, completedGroups);
	}

	private static ModuleUpdateService CreateService() => new();

	private static ModuleFileTransferPlan CreatePlan(string name, int count) =>
		new(
			name,
			Enumerable.Range(0, count).Select(index => $"machine-{index}").ToArray(),
			Enumerable.Range(0, count).Select(index => $"lcu-{index}").ToArray(),
			[]);

	private sealed class CancellingUnitList(
		IReadOnlyList<UpdateFileListEntry> items,
		CancellationTokenSource cancellation) : IReadOnlyList<UpdateFileListEntry>
	{
		public int Count => items.Count;

		public UpdateFileListEntry this[int index]
		{
			get
			{
				UpdateFileListEntry item = items[index];
				cancellation.Cancel();
				return item;
			}
		}

		public IEnumerator<UpdateFileListEntry> GetEnumerator() => items.GetEnumerator();

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
			GetEnumerator();
	}
}
