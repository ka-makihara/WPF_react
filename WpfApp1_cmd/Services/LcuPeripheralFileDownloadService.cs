using System.IO;

namespace WpfApp1_cmd.Services;

public interface ILcuPeripheralFileDownloadGateway
{
	Task SetLcuAsync(int lcuId, CancellationToken cancellationToken);

	Task<bool> DownloadMachineFileAsync(
		string machineName,
		string IpAddress,
		int position,
		string machineRelativeFile,
		string lcuRelativeFile,
		string targetDirectory,
		CancellationToken cancellationToken);
}

public sealed record LcuPeripheralFileDownloadRequest(
	int LcuId,
	string MachineName,
	string IpAddress,
	int Position,
	string PeripheralRelativeDirectory,
	string FileName,
	string TargetDirectory);

public sealed class LcuPeripheralFileDownloadService
{
	public async Task<bool> DownloadAsync(
		LcuPeripheralFileDownloadRequest request,
		ILcuPeripheralFileDownloadGateway gateway,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(gateway);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.MachineName);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.PeripheralRelativeDirectory);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.FileName);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.TargetDirectory);

		string machineRelativeFile = request.PeripheralRelativeDirectory + request.FileName;
		string lcuRelativeFile = "/MCFiles/" + machineRelativeFile;
		string targetDirectory = request.TargetDirectory + request.PeripheralRelativeDirectory;

		cancellationToken.ThrowIfCancellationRequested();
		await gateway.SetLcuAsync(request.LcuId, cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		await Task.Run(
			() => Directory.CreateDirectory(targetDirectory),
			cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		bool downloaded = await gateway.DownloadMachineFileAsync(
			request.MachineName,
			request.IpAddress,
			request.Position,
			machineRelativeFile,
			lcuRelativeFile,
			request.TargetDirectory,
			cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		return downloaded;
	}
}
