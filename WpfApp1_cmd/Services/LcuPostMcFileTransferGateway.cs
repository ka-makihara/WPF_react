using WpfLcuCtrlLib;

namespace WpfApp1_cmd.Services;

public sealed class LcuPostMcFileTransferGateway(LcuCtrl lcuCtrl) : IPostMcFileTransferGateway
{
	public Task<string> SendAsync(
		PostMcFileTransferRequest request,
		CancellationToken cancellationToken)
	{
		string command = PostMcFile.Command(
			request.MachineName,
			request.ModulePosition,
			request.User,
			request.Password,
			request.MachineFiles.ToList(),
			request.LcuFiles.ToList());
		return lcuCtrl.LCU_Command(command, cancellationToken);
	}
}
