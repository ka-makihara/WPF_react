using WpfLcuCtrlLib;

namespace WpfApp1_cmd.Services;

public sealed class LcuModuleTransferInspectionGateway(LcuCtrl lcuCtrl)
	: IModuleTransferInspectionGateway
{
	public Task<bool> DownloadFilesAsync(
		string machineName,
		string IpAddress,
		int modulePosition,
		IReadOnlyList<string> machineFiles,
		IReadOnlyList<string> lcuFiles,
		string targetDirectory,
		CancellationToken cancellationToken) =>
		lcuCtrl.GetMachineFiles(
			machineName,
			IpAddress,
			modulePosition,
			machineFiles.ToList(),
			lcuFiles.ToList(),
			targetDirectory,
			cancellationToken);
}
