using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace WpfApp1_cmd.Services;

// =====================================================================
//  アップデートデータの取得元決定サービス
//
//  起動時にアップデートデータ(UpdateCommon.inf と更新ファイル群)の置き場所を決める。
//    1. データフォルダ: --dataFolder 指定値、未指定ならアプリフォルダ\Data
//    2. データフォルダに Peripheral.bin(圧縮アーカイブ)があれば、空き容量を確認して展開する
//       展開に成功した場合は展開先(C:\DecompBin)をデータフォルダとする
//    3. データフォルダ + Peripheral パス + UpdateCommon.inf を読み込み対象とする
//    4. アーカイブ処理結果からログ/エラーコードを作る
//
//  Peripheral.bin の展開は mcAccount.dll(ネイティブ)で行う(PeripheralArchiveExtractor)。
// =====================================================================

/// <summary>Peripheral.bin の準備結果</summary>
public enum PeripheralArchivePreparationOutcome
{
	/// <summary>Peripheral.bin が無い(展開不要)</summary>
	ArchiveNotFound,
	/// <summary>C ドライブの空き容量不足で展開しなかった</summary>
	InsufficientDiskSpace,
	/// <summary>展開に失敗した</summary>
	DecompressionFailed,
	/// <summary>展開に成功した</summary>
	Decompressed
}

public sealed record PeripheralArchivePreparationResult(
	PeripheralArchivePreparationOutcome Outcome,
	long ArchiveSize = 0,
	long AvailableDiskSpace = 0);

/// <summary>
///  取得元の決定結果
/// </summary>
/// <param name="DataFolder">アップデートデータのルートフォルダ(展開した場合は展開先)</param>
/// <param name="UpdateCommonPath">読み込む UpdateCommon.inf のフルパス</param>
/// <param name="ArchivePath">Peripheral.bin のパス(存在しない場合もある)</param>
/// <param name="ArchivePreparation">Peripheral.bin の準備結果</param>
public sealed record UpdateDataSourceResult(
	string DataFolder,
	string UpdateCommonPath,
	string ArchivePath,
	PeripheralArchivePreparationResult ArchivePreparation);

/// <summary>
///  アーカイブ準備結果を画面/ログへ反映するための情報
/// </summary>
/// <param name="ArchiveSize">アップデートデータサイズとして表示する値(null なら更新しない)</param>
/// <param name="ErrorCode">設定するエラーコード(null なら設定しない)</param>
/// <param name="LogMessage">ログ出力するメッセージ(null なら出力しない)</param>
public sealed record UpdateDataSourceDiagnostic(
	long? ArchiveSize,
	ErrorCode? ErrorCode,
	string? LogMessage);

/// <summary>
///  アップデートデータの取得元(データフォルダ・UpdateCommon.inf)を決定する。
///  ファイルシステム/展開処理はコンストラクタで注入する(テストで差し替えるため)。
/// </summary>
public sealed class UpdateDataSourceService
{
	/// <summary>アップデートデータの圧縮アーカイブ名</summary>
	public const string ArchiveFileName = "Peripheral.bin";

	private readonly Func<string, bool> _pathExists;
	private readonly Func<string, long> _getFileSize;
	private readonly Func<long> _getAvailableDiskSpace;
	private readonly Func<string, int> _decompress;

	/// <param name="pathExists">パスの存在確認</param>
	/// <param name="getFileSize">ファイルサイズ取得</param>
	/// <param name="getAvailableDiskSpace">展開先ドライブの空き容量取得</param>
	/// <param name="decompress">Peripheral.bin の展開(戻り値が負なら失敗)</param>
	public UpdateDataSourceService(
		Func<string, bool> pathExists,
		Func<string, long> getFileSize,
		Func<long> getAvailableDiskSpace,
		Func<string, int> decompress)
	{
		ArgumentNullException.ThrowIfNull(pathExists);
		ArgumentNullException.ThrowIfNull(getFileSize);
		ArgumentNullException.ThrowIfNull(getAvailableDiskSpace);
		ArgumentNullException.ThrowIfNull(decompress);
		_pathExists = pathExists;
		_getFileSize = getFileSize;
		_getAvailableDiskSpace = getAvailableDiskSpace;
		_decompress = decompress;
	}

	/// <summary>
	///  取得元を決定する(必要なら Peripheral.bin を展開する)。
	/// </summary>
	/// <param name="configuredDataFolder">--dataFolder の指定値(空ならアプリフォルダ\Data)</param>
	/// <param name="applicationDirectory">アプリの実行フォルダ</param>
	/// <param name="extractionDirectory">Peripheral.bin の展開先</param>
	/// <param name="peripheralRelativePath">データフォルダから UpdateCommon.inf までの相対パス</param>
	/// <param name="updateCommonFileName">UpdateCommon.inf のファイル名</param>
	public async Task<UpdateDataSourceResult> PrepareAsync(
		string configuredDataFolder,
		string applicationDirectory,
		string extractionDirectory,
		string peripheralRelativePath,
		string updateCommonFileName,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(configuredDataFolder);
		ArgumentException.ThrowIfNullOrWhiteSpace(applicationDirectory);
		ArgumentException.ThrowIfNullOrWhiteSpace(extractionDirectory);
		ArgumentException.ThrowIfNullOrWhiteSpace(peripheralRelativePath);
		ArgumentException.ThrowIfNullOrWhiteSpace(updateCommonFileName);
		cancellationToken.ThrowIfCancellationRequested();

		string dataFolder = string.IsNullOrWhiteSpace(configuredDataFolder)
			? Path.Combine(applicationDirectory, "Data")
			: configuredDataFolder;
		PeripheralArchivePreparationResult archivePreparation =
			await PrepareArchiveAsync(dataFolder, cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		string archivePath = Path.Combine(dataFolder, ArchiveFileName);

		if (archivePreparation.Outcome == PeripheralArchivePreparationOutcome.Decompressed)
		{
			dataFolder = extractionDirectory;
		}

		string updateCommonPath = Path.Combine(
			dataFolder,
			peripheralRelativePath
				.Replace('/', Path.DirectorySeparatorChar)
				.TrimStart(Path.DirectorySeparatorChar),
			updateCommonFileName);
		return new UpdateDataSourceResult(
			dataFolder,
			updateCommonPath,
			archivePath,
			archivePreparation);
	}

	/// <summary>
	///  データフォルダの Peripheral.bin を確認し、空き容量が足りれば展開する。
	/// </summary>
	public async Task<PeripheralArchivePreparationResult> PrepareArchiveAsync(
		string dataFolder,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(dataFolder);
		cancellationToken.ThrowIfCancellationRequested();
		string archivePath = Path.Combine(dataFolder, ArchiveFileName);
		(bool Exists, long ArchiveSize, long AvailableDiskSpace) archiveInfo =
			await Task.Run(() =>
			{
				bool exists = _pathExists(archivePath);
				if (!exists)
				{
					return (false, 0L, 0L);
				}

				return (true, _getFileSize(archivePath), _getAvailableDiskSpace());
			}, cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();

		if (!archiveInfo.Exists)
		{
			return new(PeripheralArchivePreparationOutcome.ArchiveNotFound);
		}

		long archiveSize = archiveInfo.ArchiveSize;
		long availableDiskSpace = archiveInfo.AvailableDiskSpace;
		if (availableDiskSpace < archiveSize)
		{
			return new(
				PeripheralArchivePreparationOutcome.InsufficientDiskSpace,
				archiveSize,
				availableDiskSpace);
		}

		cancellationToken.ThrowIfCancellationRequested();
		if (_decompress(archivePath) < 0)
		{
			return new(
				PeripheralArchivePreparationOutcome.DecompressionFailed,
				archiveSize,
				availableDiskSpace);
		}

		return new(
			PeripheralArchivePreparationOutcome.Decompressed,
			archiveSize,
			availableDiskSpace);
	}

	/// <summary>
	///  アーカイブ準備結果から、表示サイズ・エラーコード・ログメッセージを作る。
	///  <list type="bullet">
	///   <item>アーカイブ無し: 何もしない</item>
	///   <item>空き容量不足: LCU_UPDATE_DISK_FULL</item>
	///   <item>展開失敗/成功: ログのみ</item>
	///  </list>
	/// </summary>
	public UpdateDataSourceDiagnostic CreateArchivePreparationDiagnostic(
		PeripheralArchivePreparationResult result,
		string archivePath)
	{
		ArgumentNullException.ThrowIfNull(result);
		ArgumentNullException.ThrowIfNull(archivePath);

		return result.Outcome switch
		{
			PeripheralArchivePreparationOutcome.ArchiveNotFound =>
				new(null, null, null),
			PeripheralArchivePreparationOutcome.InsufficientDiskSpace =>
				new(
					result.ArchiveSize,
					ErrorCode.LCU_UPDATE_DISK_FULL,
					$"Not enough disk space:{result.AvailableDiskSpace} < UpdateData::{result.ArchiveSize}"),
			PeripheralArchivePreparationOutcome.DecompressionFailed =>
				new(result.ArchiveSize, null, $"DeCompress {archivePath} Fail."),
			PeripheralArchivePreparationOutcome.Decompressed =>
				new(result.ArchiveSize, null, $"DeCompress {archivePath} OK"),
			_ => throw new ArgumentOutOfRangeException(
				nameof(result),
				result.Outcome,
				"Unknown archive preparation outcome.")
		};
	}
}

#region Peripheral.bin の展開(ネイティブ DLL 呼び出し)

/// <summary>アーカイブイメージを展開する処理(テストで差し替えるためのインターフェース)</summary>
public interface IPeripheralArchiveImageExtractor
{
	int Extract(string destinationDirectory, byte[] image, uint imageSize);
}

/// <summary>
///  Peripheral.bin を読み込み、指定フォルダへ展開する。
///  ネイティブ側がカレントディレクトリを変更するため、展開後にアプリフォルダへ戻す。
/// </summary>
public sealed class PeripheralArchiveExtractor
{
	private readonly IPeripheralArchiveImageExtractor _imageExtractor;
	private readonly Action<string> _setCurrentDirectory;

	public PeripheralArchiveExtractor(
		IPeripheralArchiveImageExtractor imageExtractor,
		Action<string> setCurrentDirectory)
	{
		ArgumentNullException.ThrowIfNull(imageExtractor);
		ArgumentNullException.ThrowIfNull(setCurrentDirectory);
		_imageExtractor = imageExtractor;
		_setCurrentDirectory = setCurrentDirectory;
	}

	/// <returns>ネイティブ関数の戻り値(負なら失敗)</returns>
	public int Extract(
		string archivePath,
		string destinationDirectory,
		string applicationDirectory)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
		ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
		ArgumentException.ThrowIfNullOrWhiteSpace(applicationDirectory);

		byte[] image = File.ReadAllBytes(archivePath);
		try
		{
			return _imageExtractor.Extract(
				destinationDirectory,
				image,
				checked((uint)image.Length));
		}
		finally
		{
			_setCurrentDirectory(applicationDirectory);
		}
	}
}

/// <summary>mcAccount.dll の ExtractFolderFromImage による展開</summary>
public sealed class McAccountPeripheralArchiveImageExtractor : IPeripheralArchiveImageExtractor
{
	[DllImport("mcAccount.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
	private static extern int ExtractFolderFromImage(
		StringBuilder destinationDirectory,
		byte[] image,
		uint imageSize,
		UIntPtr errorInfo);

	[StructLayout(LayoutKind.Sequential)]
	private struct ErrorInfo
	{
		public long ErrorCode;
		public long SubErrorCode;
	}

	public int Extract(string destinationDirectory, byte[] image, uint imageSize)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
		ArgumentNullException.ThrowIfNull(image);
		if (imageSize > image.Length)
		{
			throw new ArgumentOutOfRangeException(nameof(imageSize));
		}

		IntPtr errorInfo = Marshal.AllocHGlobal(Marshal.SizeOf<ErrorInfo>());
		try
		{
			Marshal.StructureToPtr(default(ErrorInfo), errorInfo, false);
			StringBuilder targetDirectory = new(destinationDirectory, 1024);
			return ExtractFolderFromImage(
				targetDirectory,
				image,
				imageSize,
				(UIntPtr)errorInfo);
		}
		finally
		{
			Marshal.FreeHGlobal(errorInfo);
		}
	}
}

#endregion
