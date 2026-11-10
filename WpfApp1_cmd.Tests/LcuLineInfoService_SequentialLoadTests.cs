using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class LcuLineInfoService_SequentialLoadTests
{
	[Fact]
	public async Task LoadSequentiallyAsync_ReturnsFalseWhenAlreadyCancelledWithNoTargets()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();
		var coordinator = new LcuLineInfoService();

		bool completed = await coordinator.LoadSequentiallyAsync(
			Array.Empty<string>(),
			_ => Task.FromResult(true),
			_ => { },
			(_, _) => { },
			(_, _) => { },
			(_, _) => { },
			cancellation.Token);

		Assert.False(completed);
	}

	[Fact]
	public async Task LoadSequentiallyAsync_DoesNotLoadAfterStartCallbackCancels()
	{
		using CancellationTokenSource cancellation = new();
		List<string> events = [];
		var coordinator = new LcuLineInfoService();

		bool completed = await coordinator.LoadSequentiallyAsync(
			["A"],
			target =>
			{
				events.Add($"load:{target}");
				return Task.FromResult(true);
			},
			target =>
			{
				events.Add($"start:{target}");
				cancellation.Cancel();
			},
			(target, succeeded) => events.Add($"complete:{target}:{succeeded}"),
			(target, _) => events.Add($"request-error:{target}"),
			(target, _) => events.Add($"error:{target}"),
			cancellation.Token);

		Assert.False(completed);
		Assert.Equal(["start:A"], events);
	}

	[Fact]
	public async Task LoadSequentiallyAsync_StopsWithoutReportingErrorWhenCancelled()
	{
		using CancellationTokenSource cancellation = new();
		List<string> events = [];
		var coordinator = new LcuLineInfoService();

		bool completed = await coordinator.LoadSequentiallyAsync(
			["A", "B"],
			target =>
			{
				events.Add($"load:{target}");
				cancellation.Cancel();
				return Task.FromCanceled<bool>(cancellation.Token);
			},
			target => events.Add($"start:{target}"),
			(target, succeeded) => events.Add($"complete:{target}:{succeeded}"),
			(target, _) => events.Add($"request-error:{target}"),
			(target, _) => events.Add($"error:{target}"),
			cancellation.Token);

		Assert.False(completed);
		Assert.Equal(["start:A", "load:A"], events);
	}

	[Fact]
	public async Task LoadSequentiallyAsync_DoesNotReportRequestErrorWhenCancelledWithHttpFailure()
	{
		using CancellationTokenSource cancellation = new();
		List<string> events = [];
		var coordinator = new LcuLineInfoService();

		bool completed = await coordinator.LoadSequentiallyAsync(
			["A"],
			_ =>
			{
				cancellation.Cancel();
				return Task.FromException<bool>(new HttpRequestException("offline"));
			},
			_ => { },
			(target, _) => events.Add($"complete:{target}"),
			(target, _) => events.Add($"request-error:{target}"),
			(target, _) => events.Add($"error:{target}"),
			cancellation.Token);

		Assert.False(completed);
		Assert.Empty(events);
	}

	[Fact]
	public async Task LoadSequentiallyAsync_LoadsTargetsInOrderAndReportsCompletion()
	{
		List<string> events = [];
		var coordinator = new LcuLineInfoService();

		bool completed = await coordinator.LoadSequentiallyAsync(
			["A", "B"],
			target =>
			{
				events.Add($"load:{target}");
				return Task.FromResult(true);
			},
			target => events.Add($"start:{target}"),
			(target, succeeded) => events.Add($"complete:{target}:{succeeded}"),
			(target, _) => events.Add($"request-error:{target}"),
			(target, _) => events.Add($"error:{target}"));

		Assert.True(completed);
		Assert.Equal(
			[
				"start:A", "load:A", "complete:A:True",
				"start:B", "load:B", "complete:B:True"
			],
			events);
	}

	[Fact]
	public async Task LoadSequentiallyAsync_ContinuesAfterHttpRequestException()
	{
		List<string> events = [];
		var coordinator = new LcuLineInfoService();

		bool completed = await coordinator.LoadSequentiallyAsync(
			["A", "B"],
			target => target == "A"
				? Task.FromException<bool>(new HttpRequestException("offline"))
				: Task.FromResult(true),
			target => events.Add($"start:{target}"),
			(target, succeeded) => events.Add($"complete:{target}:{succeeded}"),
			(target, exception) => events.Add($"request-error:{target}:{exception.Message}"),
			(target, _) => events.Add($"error:{target}"));

		Assert.True(completed);
		Assert.Equal(
			[
				"start:A", "request-error:A:offline", "complete:A:False",
				"start:B", "complete:B:True"
			],
			events);
	}

	[Fact]
	public async Task LoadSequentiallyAsync_StopsAfterUnexpectedException()
	{
		List<string> events = [];
		var coordinator = new LcuLineInfoService();

		bool completed = await coordinator.LoadSequentiallyAsync(
			["A", "B"],
			_ => Task.FromException<bool>(new InvalidOperationException("failed")),
			target => events.Add($"start:{target}"),
			(target, _) => events.Add($"complete:{target}"),
			(target, _) => events.Add($"request-error:{target}"),
			(target, exception) => events.Add($"error:{target}:{exception.Message}"));

		Assert.False(completed);
		Assert.Equal(["start:A", "error:A:failed"], events);
	}

	[Fact]
	public async Task LoadSequentiallyAsync_ReportsFalseResultAndContinues()
	{
		List<string> events = [];
		var coordinator = new LcuLineInfoService();

		bool completed = await coordinator.LoadSequentiallyAsync(
			["A", "B"],
			target => Task.FromResult(target != "A"),
			_ => { },
			(target, succeeded) => events.Add($"{target}:{succeeded}"),
			(_, _) => { },
			(_, _) => { });

		Assert.True(completed);
		Assert.Equal(["A:False", "B:True"], events);
	}
}
