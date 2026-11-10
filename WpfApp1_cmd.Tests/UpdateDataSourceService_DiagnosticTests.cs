using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class UpdateDataSourceService_DiagnosticTests
{
	private readonly UpdateDataSourceService _service = new(_ => false, _ => 0, () => 0, _ => 0);

	[Fact]
	public void CreateArchivePreparationDiagnostic_ArchiveNotFoundDoesNotChangeSizeOrLog()
	{
		UpdateDataSourceDiagnostic diagnostic = Create(
			PeripheralArchivePreparationOutcome.ArchiveNotFound);

		Assert.Null(diagnostic.ArchiveSize);
		Assert.Null(diagnostic.ErrorCode);
		Assert.Null(diagnostic.LogMessage);
	}

	[Fact]
	public void CreateArchivePreparationDiagnostic_InsufficientSpaceSetsErrorAndFormatsLog()
	{
		UpdateDataSourceDiagnostic diagnostic = Create(
			PeripheralArchivePreparationOutcome.InsufficientDiskSpace,
			archiveSize: 200,
			availableDiskSpace: 100);

		Assert.Equal(200, diagnostic.ArchiveSize);
		Assert.Equal(ErrorCode.LCU_UPDATE_DISK_FULL, diagnostic.ErrorCode);
		Assert.Equal(
			"Not enough disk space:100 < UpdateData::200",
			diagnostic.LogMessage);
	}

	[Theory]
	[InlineData(PeripheralArchivePreparationOutcome.DecompressionFailed, "DeCompress archive.bin Fail.")]
	[InlineData(PeripheralArchivePreparationOutcome.Decompressed, "DeCompress archive.bin OK")]
	public void CreateArchivePreparationDiagnostic_DecompressionOutcomeFormatsLog(
		PeripheralArchivePreparationOutcome outcome,
		string expectedLog)
	{
		UpdateDataSourceDiagnostic diagnostic = Create(outcome, archiveSize: 300);

		Assert.Equal(300, diagnostic.ArchiveSize);
		Assert.Null(diagnostic.ErrorCode);
		Assert.Equal(expectedLog, diagnostic.LogMessage);
	}

	private UpdateDataSourceDiagnostic Create(
		PeripheralArchivePreparationOutcome outcome,
		long archiveSize = 0,
		long availableDiskSpace = 0) =>
		_service.CreateArchivePreparationDiagnostic(
			new PeripheralArchivePreparationResult(
				outcome,
				archiveSize,
				availableDiskSpace),
			"archive.bin");
}
