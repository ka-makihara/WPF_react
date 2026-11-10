using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class UpdateDataSourceService_ExtractorTests
{
	[Fact]
	public void Extract_ReadsArchiveAndRestoresApplicationDirectory()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			string archivePath = Path.Combine(root, "Peripheral.bin");
			byte[] image = [1, 2, 3, 4];
			File.WriteAllBytes(archivePath, image);
			var imageExtractor = new FakeImageExtractor { Result = 1 };
			string? restoredDirectory = null;
			var extractor = new PeripheralArchiveExtractor(
				imageExtractor,
				directory => restoredDirectory = directory);

			int result = extractor.Extract(archivePath, @"C:\DecompBin", root);

			Assert.Equal(1, result);
			Assert.Equal(@"C:\DecompBin", imageExtractor.DestinationDirectory);
			Assert.Equal(image, imageExtractor.Image);
			Assert.Equal((uint)image.Length, imageExtractor.ImageSize);
			Assert.Equal(root, restoredDirectory);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Fact]
	public void Extract_RestoresApplicationDirectoryWhenNativeExtractionThrows()
	{
		string root = CreateTemporaryDirectory();
		try
		{
			string archivePath = Path.Combine(root, "Peripheral.bin");
			File.WriteAllBytes(archivePath, [1]);
			string? restoredDirectory = null;
			var extractor = new PeripheralArchiveExtractor(
				new FakeImageExtractor { Exception = new InvalidOperationException("extract failed") },
				directory => restoredDirectory = directory);

			InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
				() => extractor.Extract(archivePath, @"C:\DecompBin", root));

			Assert.Equal("extract failed", exception.Message);
			Assert.Equal(root, restoredDirectory);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	private static string CreateTemporaryDirectory()
	{
		string path = Path.Combine(Path.GetTempPath(), $"PeripheralArchiveExtractorTests-{Guid.NewGuid():N}");
		Directory.CreateDirectory(path);
		return path;
	}

	private sealed class FakeImageExtractor : IPeripheralArchiveImageExtractor
	{
		public int Result { get; init; }
		public Exception? Exception { get; init; }
		public string? DestinationDirectory { get; private set; }
		public byte[]? Image { get; private set; }
		public uint ImageSize { get; private set; }

		public int Extract(string destinationDirectory, byte[] image, uint imageSize)
		{
			DestinationDirectory = destinationDirectory;
			Image = image;
			ImageSize = imageSize;
			if (Exception != null)
			{
				throw Exception;
			}

			return Result;
		}
	}
}
