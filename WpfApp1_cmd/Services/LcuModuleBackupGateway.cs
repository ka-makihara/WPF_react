using WpfLcuCtrlLib;

namespace WpfApp1_cmd.Services;

public sealed class LcuModuleBackupGateway(LcuCtrl lcuCtrl) : IModuleBackupGateway
{
	public Task<bool> CreateFoldersAsync(
		IReadOnlyList<string> folders,
		string lcuRoot,
		CancellationToken cancellationToken) =>
		lcuCtrl.CreateFtpFolders(folders.ToList(), lcuRoot, token: cancellationToken);

	public Task<bool> DownloadFilesAsync(
		string machineName,
		string IpAddress,
		int position,
		IReadOnlyList<string> files,
		string localPath,
		CancellationToken cancellationToken) =>
		lcuCtrl.GetMachineFiles(
			machineName,
			IpAddress,
			position,
			files.ToList(),
			files.ToList(),
			localPath,
			cancellationToken);
}
