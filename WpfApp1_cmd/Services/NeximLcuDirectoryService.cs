namespace WpfApp1_cmd.Services;

public sealed record NeximComputerRecord(int ComputerId, string Name);

public sealed record NeximLineRecord(
	string Name,
	int LineId,
	int JobId,
	int ActiveFlag,
	int LcuId,
	IReadOnlyList<NeximComputerRecord> Computers);

public sealed record NeximLcuRepositoryResult(
	IReadOnlyList<NeximLineRecord> Lines,
	string? ErrorMessage);

public sealed record NeximLcuTarget(string LineName, string LcuName, int ComputerId);

public sealed record NeximLcuDirectoryResult(
	IReadOnlyList<NeximLcuTarget> Targets,
	string? ErrorMessage);

public interface INeximLcuRepository
{
	Task<NeximLcuRepositoryResult> LoadActiveLcusAsync(
		NeximDatabaseSettings settings,
		CancellationToken cancellationToken);
}

public sealed class NeximLcuDirectoryService(INeximLcuRepository repository)
{
	public async Task<NeximLcuDirectoryResult> LoadActiveLcusAsync(
		NeximDatabaseSettings settings,
		Action<NeximLineRecord> onLineLoaded,
		Action<NeximComputerRecord> onComputerLoaded,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(settings);
		ArgumentNullException.ThrowIfNull(onLineLoaded);
		ArgumentNullException.ThrowIfNull(onComputerLoaded);

		cancellationToken.ThrowIfCancellationRequested();
		NeximLcuRepositoryResult repositoryResult =
			await repository.LoadActiveLcusAsync(settings, cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		List<NeximLcuTarget> targets = [];
		foreach (NeximLineRecord line in repositoryResult.Lines)
		{
			cancellationToken.ThrowIfCancellationRequested();
			onLineLoaded(line);
			foreach (NeximComputerRecord computer in line.Computers)
			{
				cancellationToken.ThrowIfCancellationRequested();
				onComputerLoaded(computer);
				targets.Add(new NeximLcuTarget(line.Name, computer.Name, computer.ComputerId));
			}
		}

		cancellationToken.ThrowIfCancellationRequested();
		return new NeximLcuDirectoryResult(targets, repositoryResult.ErrorMessage);
	}
}
