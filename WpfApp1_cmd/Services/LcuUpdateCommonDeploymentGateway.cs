using WpfLcuCtrlLib;

namespace WpfApp1_cmd.Services;

public sealed class LcuUpdateCommonDeploymentGateway(LcuCtrl lcuCtrl) : IUpdateCommonDeploymentGateway
{
	public Task<bool> UploadAsync(
		string machineName,
		string IpAddress,
		int position,
		string machineRelativePath,
		string lcuRelativePath,
		string localDirectory,
		CancellationToken cancellationToken) =>
		lcuCtrl.PutMachineFile(
			machineName,
			IpAddress,
			position,
			machineRelativePath,
			$"/MCFiles/{lcuRelativePath}",
			localDirectory,
			cancellationToken);

	public Task<bool> DownloadAsync(
		string machineName,
		string IpAddress,
		int position,
		string machineRelativePath,
		string lcuRelativePath,
		string localDirectory,
		CancellationToken cancellationToken) =>
		lcuCtrl.GetMachineFiles(
			machineName,
			IpAddress,
			position,
			[$"/{machineRelativePath}"],
			[lcuRelativePath],
			localDirectory,
			cancellationToken);
}
