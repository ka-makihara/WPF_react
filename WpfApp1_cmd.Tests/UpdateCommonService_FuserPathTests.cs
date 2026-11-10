using System.IO;
using WpfApp1_cmd.Services;
using WpfLcuCtrlLib;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class UpdateCommonService_FuserPathTests
{
	[Fact]
	public void ClearMissingFuserPaths_ClearsAllSpecifiedValues()
	{
		string path = CreateIniFile(
			"[UnitA]\r\nFuserPath=/fuser/a.bin\r\n[UnitB]\r\nFuserPath=/fuser/b.bin");
		try
		{
			IniFileParser parser = new(path);

			new UpdateCommonService().ClearMissingFuserPaths(
				parser,
				[("UnitA", "/fuser/a.bin"), ("UnitB", "/fuser/b.bin")]);

			Assert.Equal("", parser.GetValue("UnitA", "FuserPath"));
			Assert.Equal("", parser.GetValue("UnitB", "FuserPath"));
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Fact]
	public void ClearMissingFuserPaths_DoesNotChangeParserWhenAlreadyCancelled()
	{
		string path = CreateIniFile("[UnitA]\r\nFuserPath=/fuser/a.bin");
		try
		{
			IniFileParser parser = new(path);
			using CancellationTokenSource cancellation = new();
			cancellation.Cancel();

			Assert.Throws<OperationCanceledException>(() =>
				new UpdateCommonService().ClearMissingFuserPaths(
					parser,
					[("UnitA", "/fuser/a.bin")],
					cancellation.Token));

			Assert.Equal("/fuser/a.bin", parser.GetValue("UnitA", "FuserPath"));
		}
		finally
		{
			File.Delete(path);
		}
	}

	private static string CreateIniFile(string contents)
	{
		string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".ini");
		File.WriteAllText(path, contents);
		return path;
	}
}
