using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class NeximLcuDirectoryServiceTests
{
	[Fact]
	public async Task LoadActiveLcusAsync_ReturnsTypedTargetsAndReportsTheyWereLoaded()
	{
		var line = new NeximLineRecord(
			"LineA",
			10,
			0,
			1,
			21,
			[new NeximComputerRecord(21, "lcu-host")]);
		var repository = new FakeRepository(new NeximLcuRepositoryResult([line], null));
		List<string> logs = [];

		NeximLcuDirectoryResult result = await new NeximLcuDirectoryService(repository).LoadActiveLcusAsync(
			new NeximDatabaseSettings("db", "host", "user", "password"),
			item => logs.Add($"line:{item.Name}:{item.LineId}"),
			item => logs.Add($"computer:{item.ComputerId}:{item.Name}"));

		Assert.Equal([new NeximLcuTarget("LineA", "lcu-host", 21)], result.Targets);
		Assert.Null(result.ErrorMessage);
		Assert.Equal(["line:LineA:10", "computer:21:lcu-host"], logs);
	}

	[Fact]
	public async Task LoadActiveLcusAsync_PreservesPartialResultsAndSurfacesRepositoryError()
	{
		var line = new NeximLineRecord(
			"LineA",
			10,
			0,
			1,
			21,
			[new NeximComputerRecord(21, "lcu-host")]);
		var repository = new FakeRepository(
			new NeximLcuRepositoryResult([line], "database unavailable"));

		NeximLcuDirectoryResult result = await new NeximLcuDirectoryService(repository).LoadActiveLcusAsync(
			new NeximDatabaseSettings("db", "host", "user", "password"),
			_ => { },
			_ => { });

		Assert.Equal([new NeximLcuTarget("LineA", "lcu-host", 21)], result.Targets);
		Assert.Equal("database unavailable", result.ErrorMessage);
	}

	[Fact]
	public async Task LoadActiveLcusAsync_PreservesSeparatorsInNames()
	{
		var line = new NeximLineRecord(
			"Line=A",
			10,
			0,
			1,
			21,
			[new NeximComputerRecord(21, "lcu=host")]);
		var repository = new FakeRepository(new NeximLcuRepositoryResult([line], null));

		NeximLcuDirectoryResult result = await new NeximLcuDirectoryService(repository).LoadActiveLcusAsync(
			new NeximDatabaseSettings("db", "host", "user", "password"),
			_ => { },
			_ => { });

		Assert.Equal([new NeximLcuTarget("Line=A", "lcu=host", 21)], result.Targets);
	}

	[Fact]
	public async Task LoadActiveLcusAsync_ThrowsWhenCancelledBeforeRepositoryAccess()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();
		var repository = new FakeRepository(new NeximLcuRepositoryResult([], null));

		await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
			new NeximLcuDirectoryService(repository).LoadActiveLcusAsync(
				new NeximDatabaseSettings("db", "host", "user", "password"),
				_ => { },
				_ => { },
				cancellation.Token));
		Assert.False(repository.WasCalled);
	}

	[Fact]
	public async Task LoadActiveLcusAsync_StopsBeforeComputerCallbackWhenLineCallbackCancels()
	{
		using CancellationTokenSource cancellation = new();
		var line = new NeximLineRecord(
			"LineA",
			10,
			0,
			1,
			21,
			[new NeximComputerRecord(21, "lcu-host")]);
		var repository = new FakeRepository(new NeximLcuRepositoryResult([line], null));
		List<string> callbacks = [];

		await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
			new NeximLcuDirectoryService(repository).LoadActiveLcusAsync(
				new NeximDatabaseSettings("db", "host", "user", "password"),
				_ =>
				{
					callbacks.Add("line");
					cancellation.Cancel();
				},
				_ => callbacks.Add("computer"),
				cancellation.Token));

		Assert.Equal(["line"], callbacks);
	}

	[Fact]
	public async Task LoadActiveLcusAsync_DoesNotInvokeCallbacksWhenRepositoryCancels()
	{
		using CancellationTokenSource cancellation = new();
		var line = new NeximLineRecord(
			"LineA",
			10,
			0,
			1,
			21,
			[new NeximComputerRecord(21, "lcu-host")]);
		var repository = new FakeRepository(
			new NeximLcuRepositoryResult([line], null),
			_ => cancellation.Cancel());
		List<string> callbacks = [];

		await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
			new NeximLcuDirectoryService(repository).LoadActiveLcusAsync(
				new NeximDatabaseSettings("db", "host", "user", "password"),
				_ => callbacks.Add("line"),
				_ => callbacks.Add("computer"),
				cancellation.Token));

		Assert.Empty(callbacks);
	}

	private sealed class FakeRepository(
		NeximLcuRepositoryResult result,
		Action<CancellationToken>? onLoad = null) : INeximLcuRepository
	{
		public bool WasCalled { get; private set; }

		public Task<NeximLcuRepositoryResult> LoadActiveLcusAsync(
			NeximDatabaseSettings settings,
			CancellationToken cancellationToken)
		{
			WasCalled = true;
			onLoad?.Invoke(cancellationToken);
			return Task.FromResult(result);
		}
	}
}
