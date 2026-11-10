using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class ModuleBackupService_ResolveResultTests
{
	private readonly ModuleBackupService _resolver = new();

	[Fact]
	public void Resolve_SuccessWithoutErrorReturnsSucceeded()
	{
		BackupExecutionResult result = _resolver.ResolveResult(
			backupSucceeded: true,
			hasError: false,
			errorMessage: "ignored",
			requiredFailureMessage: "Backup required");

		Assert.Equal(BackupOutcome.Succeeded, result.Outcome);
		Assert.Null(result.FailureMessage);
	}

	[Theory]
	[InlineData(true, "Backup failed")]
	[InlineData(false, "Backup failed")]
	public void Resolve_ErrorUsesErrorMessage(
		bool backupSucceeded,
		string errorMessage)
	{
		BackupExecutionResult result = _resolver.ResolveResult(
			backupSucceeded,
			hasError: true,
			errorMessage,
			requiredFailureMessage: "Backup required");

		Assert.Equal(BackupOutcome.Failed, result.Outcome);
		Assert.Equal(errorMessage, result.FailureMessage);
	}

	[Fact]
	public void Resolve_FailedWithoutErrorUsesRequiredFailureMessage()
	{
		BackupExecutionResult result = _resolver.ResolveResult(
			backupSucceeded: false,
			hasError: false,
			errorMessage: null,
			requiredFailureMessage: "Backup required");

		Assert.Equal(BackupOutcome.Failed, result.Outcome);
		Assert.Equal("Backup required", result.FailureMessage);
	}
}
