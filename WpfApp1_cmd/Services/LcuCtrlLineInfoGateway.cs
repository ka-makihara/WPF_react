using WpfLcuCtrlLib;

namespace WpfApp1_cmd.Services;

public sealed class LcuCtrlLineInfoGateway(LcuCtrl lcuCtrl) : ILcuLineInfoGateway
{
	public Task<string> GetLinesAsync(CancellationToken cancellationToken) =>
		lcuCtrl.LCU_HttpGet("lines", cancellationToken);
}
