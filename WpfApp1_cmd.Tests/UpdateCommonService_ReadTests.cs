using System.IO;
using System.Text;
using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class UpdateCommonService_ReadTests
{
	[Fact]
	public void Read_ReturnsParserAndLinesForExistingFile()
	{
		string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".ini");
		File.WriteAllText(path, "[UnitA]\r\nVersion=2.0", Encoding.UTF8);
		try
		{
			UpdateCommonSourceReadResult result =
				new UpdateCommonService().Read(path, "utf-8");

			Assert.NotNull(result.Parser);
			Assert.Equal("2.0", result.Parser.GetValue("UnitA", "Version"));
			string[] lines = Assert.IsType<string[]>(result.Lines);
			Assert.Equal(["[UnitA]", "Version=2.0"], lines);
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Fact]
	public void Read_ReturnsEmptyResultWhenFileDoesNotExist()
	{
		string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".ini");

		UpdateCommonSourceReadResult result =
			new UpdateCommonService().Read(path, "utf-8");

		Assert.Null(result.Parser);
		Assert.Null(result.Lines);
	}

	[Fact]
	public void Read_ThrowsWhenCancellationIsRequested()
	{
		string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".ini");
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		Assert.Throws<OperationCanceledException>(() =>
			new UpdateCommonService().Read(path, "utf-8", cancellation.Token));
	}
}
