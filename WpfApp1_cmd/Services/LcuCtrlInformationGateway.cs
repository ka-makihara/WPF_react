using WpfLcuCtrlLib;

namespace WpfApp1_cmd.Services;

public sealed class LcuCtrlInformationGateway(LcuCtrl lcuCtrl) : ILcuInformationGateway
{
	public async Task<LcuFtpAccount?> GetFtpAccountAsync(
		CancellationToken cancellationToken)
	{
		string response = await lcuCtrl.LCU_Command(FtpData.Command(), cancellationToken);
		FtpData? account = FtpData.FromJson(response);
		if (account == null)
		{
			return null;
		}

		string? password = account.username != null && account.password != null
			? FtpData.GetPasswd(account.username, account.password)
			: account.password;
		return new(account.username, password);
	}

	public async Task<IReadOnlyList<LcuVersionEntry>?> GetVersionAsync(
		CancellationToken cancellationToken)
	{
		string response = await lcuCtrl.LCU_Command(LcuVersion.Command(), cancellationToken);
		List<LcuVersion>? versions = LcuVersion.FromJson(response);
		return versions?
			.Select(version => new LcuVersionEntry(version.itemName, version.itemVersion))
			.ToArray();
	}

	public async Task<IReadOnlyList<LcuDiskDrive>?> GetDiskInfoAsync(
		CancellationToken cancellationToken)
	{
		List<LcuDiskInfo>? drives = await lcuCtrl.LCU_DiskInfo(cancellationToken);
		return drives?
			.Select(drive => new LcuDiskDrive(drive.driveLetter, drive.free))
			.ToArray();
	}
}
