using WpfLcuCtrlLib;

namespace WpfApp1_cmd.Services;

public sealed class LcuPeripheralFileDownloadGateway(LcuCtrl lcuCtrl)
	: ILcuPeripheralFileDownloadGateway
{
	public async Task SetLcuAsync(int lcuId, CancellationToken cancellationToken)
	{
		await lcuCtrl.LCU_Command(SetLcu.Command(lcuId), cancellationToken);
	}

	public Task<bool> DownloadMachineFileAsync(
		string machineName,
		string IpAddress,
		int position,
		string machineRelativeFile,
		string lcuRelativeFile,
		string targetDirectory,
		CancellationToken cancellationToken) =>
		lcuCtrl.GetMachineFile(
			machineName,
			IpAddress,
			position,
			machineRelativeFile,
			lcuRelativeFile,
			targetDirectory,
			cancellationToken);
}
