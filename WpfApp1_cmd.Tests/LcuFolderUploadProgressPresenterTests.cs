using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class LcuFolderUploadProgressPresenterTests
{
	[Fact]
	public void Create_FormatsProgressMessageAndDebugOutput()
	{
		LcuFolderUploadPresentation presentation =
			new LcuFolderUploadProgressPresenter(new TransferProgressCalculator()).Create(
				"LineA",
				new LcuFolderUploadProgress(
					Path.Combine("UpdateData", "FolderA"),
					"\\remote\\FolderA",
					50,
					75),
				100);

		Assert.Equal("Transfering LineA:FolderA", presentation.Message);
		Assert.Equal(187.5, presentation.Progress);
		Assert.Equal(
			$"[FTP Upload] {Path.Combine("UpdateData", "FolderA")} => LineA/\\remote\\FolderA (50 bytes)",
			presentation.DebugMessage);
	}

	[Fact]
	public void Create_UsesZeroProgressWhenTotalBytesIsZero()
	{
		LcuFolderUploadPresentation presentation =
			new LcuFolderUploadProgressPresenter(new TransferProgressCalculator()).Create(
				"LineA",
				new LcuFolderUploadProgress("UpdateData", "/remote", 0, 0),
				0);

		Assert.Equal(0, presentation.Progress);
	}
}
