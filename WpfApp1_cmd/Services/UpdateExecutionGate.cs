namespace WpfApp1_cmd.Services;

public enum BackupOutcome
{
	Succeeded,
	Failed,
	Cancelled
}

public static class UpdateExecutionGate
{
	public static Func<Task<T>>? CreateUpdateOperation<T>(
		BackupOutcome backupOutcome,
		Func<Task<T>> startUpdate)
	{
		ArgumentNullException.ThrowIfNull(startUpdate);
		return backupOutcome == BackupOutcome.Succeeded ? startUpdate : null;
	}
}
