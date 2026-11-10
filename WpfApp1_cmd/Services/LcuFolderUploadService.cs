using System.IO;
using WpfLcuCtrlLib;

namespace WpfApp1_cmd.Services;

public sealed record LcuFolderUploadProgress(
	string LocalFolder,
	string RemoteFolder,
	long FolderBytes,
	long TotalTransferredBytes);

public enum LcuFolderUploadOutcome
{
	Succeeded,
	Cancelled
}

public sealed record LcuFolderUploadResult(
	LcuFolderUploadOutcome Outcome,
	long TotalTransferredBytes);

public sealed class LcuFolderUploadService(IFileTransferClient fileTransferClient)
{
	/// <summary>モジュール転送前に必要ファイルを再配置する。共有ファイルも毎回上書きする。</summary>
	public async Task UploadModuleFilesAsync(
		string host,
		string user,
		string password,
		IReadOnlyList<FileTransferRequest> files,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(host);
		ArgumentNullException.ThrowIfNull(files);
		cancellationToken.ThrowIfCancellationRequested();
		IFileTransferSession session = await fileTransferClient.ConnectAsync(
			host, user, password, cancellationToken);
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			foreach (FileTransferRequest file in files)
			{
				cancellationToken.ThrowIfCancellationRequested();
				FileTransferResult result = await session.UploadFileAsync(
					file.LocalPath, file.RemotePath, cancellationToken);
				cancellationToken.ThrowIfCancellationRequested();
				if (!result.IsSuccess)
				{
					throw new IOException($"FTP staging failed: {file.LocalPath} -> {file.RemotePath}");
				}
			}
		}
		finally
		{
			await session.DisconnectAsync(CancellationToken.None);
		}
	}

	/// <summary>モジュール成功後に過去の残存分を含めMCFiles配下を空にする。MCFiles自体は残す。</summary>
	public async Task ClearModuleFilesAsync(
		string host,
		string user,
		string password,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(host);
		cancellationToken.ThrowIfCancellationRequested();
		IFileTransferSession session = await fileTransferClient.ConnectAsync(
			host, user, password, cancellationToken);
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			await session.ClearStagingDirectoryAsync(cancellationToken);
		}
		finally
		{
			await session.DisconnectAsync(CancellationToken.None);
		}
	}

	public async Task<LcuFolderUploadResult> UploadAsync(
		string host,
		string user,
		string password,
		IReadOnlyList<string> folders,
		string localRoot,
		string remoteRoot,
		Func<LcuFolderUploadProgress, Task> onFolderUploaded,
		Func<CancellationToken, Task<bool>> canContinue,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(host);
		ArgumentNullException.ThrowIfNull(folders);
		ArgumentException.ThrowIfNullOrWhiteSpace(localRoot);
		ArgumentNullException.ThrowIfNull(remoteRoot);
		ArgumentNullException.ThrowIfNull(onFolderUploaded);
		ArgumentNullException.ThrowIfNull(canContinue);

		cancellationToken.ThrowIfCancellationRequested();
		IFileTransferSession session = await fileTransferClient.ConnectAsync(
			host,
			user,
			password,
			cancellationToken);
		long totalTransferredBytes = 0;
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			string normalizedLocalRoot = Path.GetFullPath(localRoot);
			foreach (string folder in folders)
			{
				cancellationToken.ThrowIfCancellationRequested();
				string normalizedFolder = Path.GetFullPath(folder);
				string relativeFolder = Path.GetRelativePath(normalizedLocalRoot, normalizedFolder);
				if (Path.IsPathRooted(relativeFolder)
					|| relativeFolder == ".."
					|| relativeFolder.StartsWith(
						$"..{Path.DirectorySeparatorChar}",
						StringComparison.Ordinal)
					|| relativeFolder.StartsWith(
						$"..{Path.AltDirectorySeparatorChar}",
						StringComparison.Ordinal))
				{
					throw new ArgumentException($"Folder '{folder}' is outside local root '{localRoot}'.", nameof(folders));
				}

				string remoteFolder = relativeFolder == "."
					? remoteRoot
					: remoteRoot + Path.DirectorySeparatorChar + relativeFolder;
				while (true)
				{
					IReadOnlyList<FileTransferResult> results = await session.UploadDirectoryAsync(
						folder,
						remoteFolder,
						cancellationToken);
					cancellationToken.ThrowIfCancellationRequested();
					bool hasFailure = results.Any(result => !result.IsSuccess && !result.IsSkipped);
					if (!hasFailure)
					{
						break;
					}

					cancellationToken.ThrowIfCancellationRequested();
				}

				long folderBytes = await Task.Run(
					() => UpdateDataLoadService.GetDirectorySize(
						new DirectoryInfo(folder),
						cancellationToken),
					cancellationToken);
				cancellationToken.ThrowIfCancellationRequested();
				totalTransferredBytes += folderBytes;
				await onFolderUploaded(new LcuFolderUploadProgress(
					folder,
					remoteFolder,
					folderBytes,
					totalTransferredBytes));
				cancellationToken.ThrowIfCancellationRequested();

				bool shouldContinue = await canContinue(cancellationToken);
				cancellationToken.ThrowIfCancellationRequested();
				if (!shouldContinue)
				{
					return new LcuFolderUploadResult(
						LcuFolderUploadOutcome.Cancelled,
						totalTransferredBytes);
				}
			}

			cancellationToken.ThrowIfCancellationRequested();
			return new LcuFolderUploadResult(
				LcuFolderUploadOutcome.Succeeded,
				totalTransferredBytes);
		}
		finally
		{
			await session.DisconnectAsync(CancellationToken.None);
		}
	}
}
