using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class DialogHostShowCoordinatorTests
{
	[Fact]
	public async Task ShowAsync_SerializesDialogsForTheSameIdentifier()
	{
		string identifier = Guid.NewGuid().ToString();
		List<string> showOrder = [];
		TaskCompletionSource firstDialogStarted = new(
			TaskCreationOptions.RunContinuationsAsynchronously);
		TaskCompletionSource releaseFirstDialog = new(
			TaskCreationOptions.RunContinuationsAsynchronously);

		Task<object?> firstDialog = DialogHostShowCoordinator.ShowAsync(
			identifier,
			async () =>
			{
				showOrder.Add("first");
				firstDialogStarted.SetResult();
				await releaseFirstDialog.Task;
				return "first";
			});
		await firstDialogStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

		Task<object?> secondDialog = DialogHostShowCoordinator.ShowAsync(
			identifier,
			() =>
			{
				showOrder.Add("second");
				return Task.FromResult<object?>("second");
			});

		await Task.Delay(50);
		Assert.Equal(["first"], showOrder);

		releaseFirstDialog.SetResult();
		Assert.Equal("first", await firstDialog);
		Assert.Equal("second", await secondDialog);
		Assert.Equal(["first", "second"], showOrder);
	}

	[Fact]
	public async Task ShowAsync_ReleasesIdentifierAfterDialogFails()
	{
		string identifier = Guid.NewGuid().ToString();

		await Assert.ThrowsAsync<InvalidOperationException>(() =>
			DialogHostShowCoordinator.ShowAsync(
				identifier,
				() => throw new InvalidOperationException("dialog failed")));

		object? result = await DialogHostShowCoordinator.ShowAsync(
			identifier,
			() => Task.FromResult<object?>("shown"));

		Assert.Equal("shown", result);
	}
}
