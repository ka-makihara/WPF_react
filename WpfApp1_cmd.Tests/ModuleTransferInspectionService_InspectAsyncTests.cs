using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class ModuleTransferInspectionService_InspectAsyncTests
{
	[Fact]
	public async Task InspectAsync_DownloadFailureSkipsFileInspection()
	{
		bool downloadCalled = false;
		var service = new ModuleTransferInspectionService();

		ModuleTransferInspectionResult result = await service.InspectAsync(
			CreateRequest(["/Peripheral/unit.bin"]),
			new ModuleTransferInspectionContext("machine", 2),
			new FakeInspectionGateway((_, _, _, _, _, _) =>
			{
				downloadCalled = true;
				return Task.FromResult(false);
			}),
			CancellationToken.None);

		Assert.True(downloadCalled);
		Assert.False(result.DownloadSucceeded);
		Assert.Empty(result.FileResults);
	}

	[Fact]
	public async Task InspectAsync_ThrowsCancellationWhenDownloadReturnsFalseAfterCancellation()
	{
		using CancellationTokenSource cancellation = new();
		bool gatewayCalled = false;
		var service = new ModuleTransferInspectionService();

		await Assert.ThrowsAsync<OperationCanceledException>(() =>
			service.InspectAsync(
				CreateRequest(["/Peripheral/unit.bin"]),
				new ModuleTransferInspectionContext("machine", 2),
				new FakeInspectionGateway((_, _, _, _, _, token) =>
				{
					gatewayCalled = true;
					Assert.Equal(cancellation.Token, token);
					cancellation.Cancel();
					return Task.FromResult(false);
				}),
				cancellation.Token));

		Assert.True(gatewayCalled);
	}

	[Fact]
	public async Task InspectAsync_DoesNotDownloadWhenAlreadyCancelled()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();
		bool gatewayCalled = false;
		var service = new ModuleTransferInspectionService();

		await Assert.ThrowsAsync<OperationCanceledException>(() =>
			service.InspectAsync(
				CreateRequest(["/Peripheral/unit.bin"]),
				new ModuleTransferInspectionContext("machine", 2),
				new FakeInspectionGateway((_, _, _, _, _, _) =>
				{
					gatewayCalled = true;
					return Task.FromResult(false);
				}),
				cancellation.Token));

		Assert.False(gatewayCalled);
	}

	[Fact]
	public async Task InspectAsync_DownloadsFilesThenReturnsInspectionResults()
	{
		string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
		string dataFolder = Path.Combine(root, "source") + Path.DirectorySeparatorChar;
		string inspectionFolder = Path.Combine(root, "inspection") + Path.DirectorySeparatorChar;
		string relativeFile = "/Peripheral/unit.bin";
		string sourceFile = dataFolder + relativeFile;
		string inspectionFile = inspectionFolder + relativeFile;
		Directory.CreateDirectory(Path.GetDirectoryName(sourceFile)!);
		Directory.CreateDirectory(Path.GetDirectoryName(inspectionFile)!);
		await File.WriteAllTextAsync(sourceFile, "verified");
		await File.WriteAllTextAsync(inspectionFile, "verified");

		try
		{
			bool downloadCalled = false;
			var request = new ModuleTransferInspectionRequest(
				[relativeFile],
				["/MCFiles/Peripheral/unit.bin"],
				[new ModuleInspectionUnit("UnitA", relativeFile, null)],
				dataFolder,
				inspectionFolder);
			var service = new ModuleTransferInspectionService();

			ModuleTransferInspectionResult result = await service.InspectAsync(
				request,
				new ModuleTransferInspectionContext("machine", 3),
				new FakeInspectionGateway((machineName, position, machineFiles, lcuFiles, targetPath, _) =>
				{
					downloadCalled = true;
					Assert.Equal("machine", machineName);
					Assert.Equal(3, position);
					Assert.Equal(request.MachineFiles, machineFiles);
					Assert.Equal(request.LcuFiles, lcuFiles);
					Assert.Equal(inspectionFolder, targetPath);
					return Task.FromResult(true);
				}),
				CancellationToken.None);

			Assert.True(downloadCalled);
			Assert.True(result.DownloadSucceeded);
			Assert.Collection(
				result.FileResults,
				inspection =>
				{
					Assert.Equal("UnitA", inspection.UnitName);
					Assert.Equal(relativeFile, inspection.FilePath);
					Assert.True(inspection.IsMatch);
				});
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	private static ModuleTransferInspectionRequest CreateRequest(
		IReadOnlyList<string> machineFiles) =>
		new(
			machineFiles,
			[],
			[],
			@"C:\Data\",
			@"C:\Inspection\");

	private sealed class FakeInspectionGateway(
		Func<string, int, IReadOnlyList<string>, IReadOnlyList<string>, string, CancellationToken, Task<bool>> download)
		: IModuleTransferInspectionGateway
	{
		public Task<bool> DownloadFilesAsync(
			string machineName,
			int modulePosition,
			IReadOnlyList<string> machineFiles,
			IReadOnlyList<string> lcuFiles,
			string targetDirectory,
			CancellationToken cancellationToken) =>
			download(
				machineName,
				modulePosition,
				machineFiles,
				lcuFiles,
				targetDirectory,
				cancellationToken);
	}
}
