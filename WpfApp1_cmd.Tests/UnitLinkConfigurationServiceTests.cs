using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class UnitLinkConfigurationServiceTests
{
	[Fact]
	public async Task LoadOverridesAsync_ParsesUnitLinkEntries()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			string path = Path.Combine(root, "unitLink.json");
			File.WriteAllText(
				path,
				"""
				{
				  "units": [
				    {
				      "name": { "NXTR": "FM-65", "AIMEXR": "Conveyor", "Other": 1 },
				      "components": [ "Palette", "Palette_FPGA" ],
				      "mode": "File",
				      "files": [ "CHPalette/*.rom" ]
				    }
				  ]
				}
				""");

			UnitLinkConfigurationEntry entry = Assert.Single(
				await new UnitLinkConfigurationService().LoadOverridesAsync(path));

			Assert.Equal("FM-65", entry.Name["NXTR"]);
			Assert.Equal("Conveyor", entry.Name["AIMEXR"]);
			Assert.Equal("1", entry.Name["Other"]);
			Assert.Equal(["Palette", "Palette_FPGA"], entry.Components);
			Assert.Equal("File", entry.Mode);
			Assert.Equal(["CHPalette/*.rom"], entry.Files);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public async Task LoadOverridesAsync_ReturnsEmptyWhenFileDoesNotExist()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			IReadOnlyList<UnitLinkConfigurationEntry> result =
				await new UnitLinkConfigurationService().LoadOverridesAsync(
					Path.Combine(root, "missing.json"));

			Assert.Empty(result);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public async Task LoadOverridesAsync_ThrowsWhenCancelledBeforeReading()
	{
		string root = CreateTemporaryDirectory();
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();
		try
		{
			await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
				new UnitLinkConfigurationService().LoadOverridesAsync(
					Path.Combine(root, "unitLink.json"),
					cancellation.Token));
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	private static string CreateTemporaryDirectory()
	{
		string path = Path.Combine(
			Path.GetTempPath(),
			$"UnitLinkConfigurationServiceTests-{Guid.NewGuid():N}");
		Directory.CreateDirectory(path);
		return path;
	}
}
