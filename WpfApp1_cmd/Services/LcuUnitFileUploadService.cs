using System.IO;

namespace WpfApp1_cmd.Services;

public enum LcuUnitFileUploadOutcome
{
	Succeeded,
	Cancelled,
	Failed
}

public sealed record LcuUnitFileUploadResult(
	LcuUnitFileUploadOutcome Outcome,
	IReadOnlyList<string> Folders,
	Exception? Error = null);

public sealed class LcuUnitFileUploadService(
	UpdateFolderSelectionService folderSelectionService,
	LcuFolderUploadService folderUploadService)
{
	public async Task<LcuUnitFileUploadResult> UploadSelectedAsync(
		string host,
		string user,
		string password,
		IReadOnlyList<UpdateFolderMachine> machines,
		IReadOnlyList<UpdateFolderFileInfo> updateDataFiles,
		string dataFolder,
		string remoteRoot,
		Func<LcuFolderUploadProgress, Task> onFolderUploaded,
		Func<CancellationToken, Task<bool>> canContinue,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(machines);
		ArgumentNullException.ThrowIfNull(updateDataFiles);

		List<string> folders;
		try
		{
			folders = await Task.Run(
				() => folderSelectionService.CreateSelectedFolders(
					machines,
					updateDataFiles,
					dataFolder,
					cancellationToken),
				cancellationToken);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			return new(LcuUnitFileUploadOutcome.Cancelled, []);
		}

		return await UploadAsync(
			host,
			user,
			password,
			folders,
			dataFolder,
			remoteRoot,
			onFolderUploaded,
			canContinue,
			cancellationToken);
	}

	public async Task<LcuUnitFileUploadResult> UploadAsync(
		string host,
		string user,
		string password,
		IReadOnlyList<string> folders,
		string dataFolder,
		string remoteRoot,
		Func<LcuFolderUploadProgress, Task> onFolderUploaded,
		Func<CancellationToken, Task<bool>> canContinue,
		CancellationToken cancellationToken)
	{
		try
		{
			LcuFolderUploadResult result = await folderUploadService.UploadAsync(
				host,
				user,
				password,
				folders,
				dataFolder,
				remoteRoot,
				onFolderUploaded,
				canContinue,
				cancellationToken);
			return new(
				result.Outcome == LcuFolderUploadOutcome.Succeeded
					? LcuUnitFileUploadOutcome.Succeeded
					: LcuUnitFileUploadOutcome.Cancelled,
				folders);
		}
		catch (Exception exception) when (
			exception is FluentFTP.Exceptions.FtpException or
			IOException or
			UnauthorizedAccessException or
			InvalidOperationException or
			ArgumentException or
			System.Net.Sockets.SocketException)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return new(LcuUnitFileUploadOutcome.Cancelled, folders);
			}

			return new(LcuUnitFileUploadOutcome.Failed, folders, exception);
		}
	}
}
