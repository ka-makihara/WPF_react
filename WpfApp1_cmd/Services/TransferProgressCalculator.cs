namespace WpfApp1_cmd.Services;

public sealed class TransferProgressCalculator
{
	private const double UploadStageMaximum = 250.0;
	private const double ModuleStageStart = 250.0;
	private const double TotalProgressMaximum = 1000.0;
	private const double ModuleStageRange = TotalProgressMaximum - ModuleStageStart;

	public double CalculateUploadProgress(long transferredBytes, long totalBytes) =>
		totalBytes > 0
			? ((double)transferredBytes / totalBytes) * UploadStageMaximum
			: 0;

	public double CalculateModuleProgress(long transferredCount, long totalCount)
	{
		if (totalCount <= 0)
		{
			return ModuleStageStart;
		}

		double progress = ModuleStageStart
			+ ((double)transferredCount / totalCount) * ModuleStageRange;
		return Math.Min(progress, TotalProgressMaximum);
	}
}
