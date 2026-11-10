using Oracle.ManagedDataAccess.Client;

namespace WpfApp1_cmd.Services;

public sealed class OracleNeximLcuRepository : INeximLcuRepository
{
	public async Task<NeximLcuRepositoryResult> LoadActiveLcusAsync(
		NeximDatabaseSettings settings,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(settings);
		cancellationToken.ThrowIfCancellationRequested();

		OracleConnectionStringBuilder connectionString = new()
		{
			UserID = settings.User,
			Password = settings.Password,
			DataSource = $"{settings.Host}/{settings.Name}"
		};
		List<NeximLineRecord> lines = [];

		try
		{
			await using OracleConnection connection = new(connectionString.ConnectionString);
			await connection.OpenAsync(cancellationToken);
			List<(string Name, int LineId, int JobId, int ActiveFlag, int LcuId)> lineRows = [];
			using (OracleCommand command = new(
				"select * from LINE WHERE JOBID=0 AND ACTIVEFLG=1",
				connection))
			await using (OracleDataReader reader =
				(OracleDataReader)await command.ExecuteReaderAsync(cancellationToken))
			{
				while (await reader.ReadAsync(cancellationToken))
				{
					lineRows.Add((
						reader.GetString(2),
						reader.GetInt32(1),
						reader.GetInt32(0),
						reader.GetInt32(6),
						reader.GetInt32(18)));
				}
			}

			foreach (var line in lineRows)
			{
				cancellationToken.ThrowIfCancellationRequested();
				int recordIndex = lines.Count;
				lines.Add(new NeximLineRecord(
					line.Name,
					line.LineId,
					line.JobId,
					line.ActiveFlag,
					line.LcuId,
					[]));

				List<NeximComputerRecord> computers = [];
				using (OracleCommand command = new(
					"select * from COMPUTER WHERE COMPUTERID=:computerId",
					connection))
				{
					command.Parameters.Add("computerId", line.LcuId);
					await using OracleDataReader reader =
						(OracleDataReader)await command.ExecuteReaderAsync(cancellationToken);
					while (await reader.ReadAsync(cancellationToken))
					{
						computers.Add(new NeximComputerRecord(
							reader.GetInt32(0),
							reader.GetString(1)));
					}
				}

				lines[recordIndex] = lines[recordIndex] with { Computers = computers };
			}

			return new NeximLcuRepositoryResult(lines, null);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception exception)
		{
			cancellationToken.ThrowIfCancellationRequested();
			return new NeximLcuRepositoryResult(lines, exception.Message);
		}
	}
}
