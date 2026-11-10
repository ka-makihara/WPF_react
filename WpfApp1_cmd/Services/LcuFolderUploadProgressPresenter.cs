using System.IO;

namespace WpfApp1_cmd.Services;

public sealed record LcuFolderUploadPresentation(
	string Message,
	double Progress,
	string DebugMessage);

public sealed class LcuFolderUploadProgressPresenter(
	TransferProgressCalculator progressCalculator)
{
	private readonly TransferProgressCalculator _progressCalculator =
		progressCalculator ?? throw new ArgumentNullException(nameof(progressCalculator));

	public LcuFolderUploadPresentation Create(
		string lcuName,
		LcuFolderUploadProgress progress,
		long totalBytes)
	{
		ArgumentNullException.ThrowIfNull(lcuName);
		ArgumentNullException.ThrowIfNull(progress);

		return new(
			$"Transfering {lcuName}:{Path.GetFileName(progress.LocalFolder)}",
			_progressCalculator.CalculateUploadProgress(
				progress.TotalTransferredBytes,
				totalBytes),
			$"[FTP Upload] {progress.LocalFolder} => {lcuName}/{progress.RemoteFolder} ({progress.FolderBytes} bytes)");
	}
}
