using System.IO;
using System.Text;
using IniFileParser = WpfLcuCtrlLib.IniFileParser;

namespace WpfApp1_cmd.Services;

// =============================================================================
// ModuleUpdateService
// -----------------------------------------------------------------------------
// 役割:
//   モジュール単位のアップデート転送 (準備 → 転送 → 状態反映 → 進捗表示) を扱うサービス。
//   転送後の検証は ModuleTransferInspectionService が担当する。
//
// 処理の流れ:
//   1. DecideUpdateInfoSource : どの UpdateCommon.inf を基準にするか決める
//                               (装置の情報 / 更新データ (管理者のみ) / 利用不可)
//   2. LoadUpdateInfoAsync    : 装置から UpdateCommon.inf をダウンロードして読み込む
//   3. BuildTransferPreparation / CreateTransferPlans
//                             : 転送ファイル一覧をグループ単位の転送計画
//                               (装置側パス / LCU 側パスの対) に変換する
//   4. ExecuteAsync           : グループごとに転送を実行する
//        4-1. TransferBatchesAsync  : ファイルを MaximumFilesPerBatch 件ずつに分けて送信
//        4-2. ApplyTransferredGroup : 転送できたユニットのバージョンを
//                                     UpdateCommon.inf の行配列へ反映
//   5. CreateProgress         : 転送済み件数から進捗率と表示用メッセージを作る
//
// 備考:
//   - 装置との通信や画面更新はデリゲートで受け取り、本サービスは通信方式や UI に依存しない。
// =============================================================================

#region 型定義

/// <summary>UpdateCommon.inf の読込結果の種類。</summary>
public enum ModuleUpdateInfoLoadOutcome
{
	/// <summary>読込不要 (既に取得済み)。</summary>
	AlreadyAvailable,
	/// <summary>ダウンロードして読み込んだ。</summary>
	Loaded,
	/// <summary>ダウンロードに失敗した。</summary>
	DownloadFailed,
	/// <summary>ダウンロード後にファイルが見つからなかった。</summary>
	FileNotFound
}

/// <summary>UpdateCommon.inf の読込結果。Loaded の場合のみ Parser と Lines が設定される。</summary>
public sealed record ModuleUpdateInfoLoadResult(
	ModuleUpdateInfoLoadOutcome Outcome,
	string LocalFilePath,
	IniFileParser? Parser = null,
	string[]? Lines = null);

/// <summary>バージョン比較の基準とする UpdateCommon.inf の取得元。</summary>
public enum ModuleUpdateInfoSource
{
	/// <summary>装置から取得したもの。</summary>
	Machine,
	/// <summary>更新データ側のもの (装置に無く、管理者の場合)。</summary>
	UpdateData,
	/// <summary>利用できない。</summary>
	Unavailable
}

/// <summary>取得元の判定結果。IgnoreVersion=true の場合はバージョン比較を行わない。</summary>
public sealed record ModuleUpdateInfoDecision(
	ModuleUpdateInfoSource Source,
	bool IgnoreVersion);

/// <summary>転送したユニット (UpdateCommon.inf へのバージョン反映判定用)。</summary>
public sealed record ModuleUpdateUnit(
	string? Name,
	string? Path,
	string? FuserPath);

/// <summary>ユニットの現在/新バージョン。Path/FuserPath はデータフォルダからの相対パス。</summary>
public sealed record ModuleUpdateVersion(
	string? Name,
	string? Path,
	string? FuserPath,
	string? CurrentVersion,
	string? NewVersion);

/// <summary>
/// 転送後のユニット状態。IsUpdateCommonVersionChanged=true の場合は
/// UpdateCommon.inf の Version を新バージョンへ書き換えたことを示す。
/// </summary>
public sealed record ModuleUpdateStateChange(
	string? Name,
	string? Path,
	string? FuserPath,
	string? CurrentVersion,
	string? NewVersion,
	bool IsUpdateCommonVersionChanged);

/// <summary>
/// グループ単位の転送計画。MachineFiles と LcuFiles は同じ並びで対応する。
/// </summary>
/// <param name="MachineFiles">データフォルダからの相対パス (装置側の配置先)。</param>
/// <param name="LcuFiles">LCU 上の送信元パス。</param>
public sealed record ModuleFileTransferPlan(
	string GroupName,
	IReadOnlyList<string> MachineFiles,
	IReadOnlyList<string> LcuFiles,
	IReadOnlyList<UpdateFileListEntry> Units);

/// <summary>転送の準備結果 (転送計画とバージョン情報のスナップショット)。</summary>
public sealed record ModuleUpdateTransferPreparation(
	IReadOnlyList<ModuleFileTransferPlan> Plans,
	IReadOnlyList<ModuleUpdateVersion> Versions);

/// <summary>1 グループ分のバッチ送信結果。</summary>
public sealed record ModuleFileTransferBatchResult(
	bool IsSuccess,
	int TransferredFileCount);

/// <summary>
/// 転送全体の結果。失敗時は FailedGroupName に失敗したグループ名が入る。
/// FailedGroupCount は送信で失敗した場合のみ 1 (開始不可で中断した場合は 0)。
/// </summary>
public sealed record ModuleUpdateTransferResult(
	bool IsSuccess,
	int TransferredFileCount,
	int FailedGroupCount,
	string? FailedGroupName);

/// <summary>進捗表示用の値 (進捗率とデバッグ出力用メッセージ)。</summary>
public sealed record ModuleTransferProgressPresentation(
	double Progress,
	string DebugMessage);

#endregion

/// <summary>
/// モジュール単位のアップデート転送 (準備・転送・状態反映・進捗表示) を行うサービス。
/// </summary>
/// <param name="progressCalculator">進捗率の計算 (省略時は既定の計算方法)。</param>
public sealed class ModuleUpdateService(TransferProgressCalculator? progressCalculator = null)
{
	/// <summary>1 回の送信で扱う最大ファイル数。</summary>
	public const int MaximumFilesPerBatch = 10;

	private readonly TransferProgressCalculator _progressCalculator =
		progressCalculator ?? new TransferProgressCalculator();

	#region 1. UpdateCommon.inf の取得元判定

	/// <summary>
	/// バージョン比較の基準とする UpdateCommon.inf の取得元を決める。
	/// 装置に情報があれば装置を使う。無い場合、管理者なら更新データ側を使い、バージョン比較は行わない。
	/// 管理者でなければ利用不可とする。
	/// </summary>
	public ModuleUpdateInfoDecision DecideUpdateInfoSource(
		bool hasMachineUpdateInfo,
		bool isAdministrator,
		bool ignoreVersion)
	{
		if (hasMachineUpdateInfo)
		{
			return new(ModuleUpdateInfoSource.Machine, ignoreVersion);
		}

		return isAdministrator
			? new(ModuleUpdateInfoSource.UpdateData, true)
			: new(ModuleUpdateInfoSource.Unavailable, ignoreVersion);
	}

	#endregion

	#region 2. 装置の UpdateCommon.inf 読込

	/// <summary>
	/// 装置の UpdateCommon.inf を一時フォルダへダウンロードして読み込む。
	/// shouldLoad=false の場合はダウンロードせず AlreadyAvailable を返す。
	/// 読込後、ダウンロードした Peripheral 配下の一時フォルダは削除する。
	/// </summary>
	/// <param name="peripheralRelativePath">一時フォルダからの Peripheral フォルダの相対パス。</param>
	/// <param name="downloadAsync">ダウンロード処理 (引数はダウンロード先フォルダ)。</param>
	public async Task<ModuleUpdateInfoLoadResult> LoadUpdateInfoAsync(
		bool shouldLoad,
		string temporaryDirectory,
		string peripheralRelativePath,
		string fileName,
		string encodingName,
		Func<string, CancellationToken, Task<bool>> downloadAsync,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(temporaryDirectory);
		ArgumentException.ThrowIfNullOrWhiteSpace(peripheralRelativePath);
		ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
		ArgumentException.ThrowIfNullOrWhiteSpace(encodingName);
		ArgumentNullException.ThrowIfNull(downloadAsync);
		cancellationToken.ThrowIfCancellationRequested();

		string peripheralDirectory = Path.Combine(
			new[] { temporaryDirectory }.Concat(
				peripheralRelativePath.Split(
					['/', '\\'],
					StringSplitOptions.RemoveEmptyEntries))
				.ToArray());
		string localFilePath = Path.Combine(peripheralDirectory, fileName);
		if (!shouldLoad)
		{
			return new(ModuleUpdateInfoLoadOutcome.AlreadyAvailable, localFilePath);
		}

		cancellationToken.ThrowIfCancellationRequested();
		string downloadTarget = Path.EndsInDirectorySeparator(temporaryDirectory)
			? temporaryDirectory
			: temporaryDirectory + Path.DirectorySeparatorChar;
		bool downloaded = await downloadAsync(downloadTarget, cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		if (!downloaded)
		{
			return new(ModuleUpdateInfoLoadOutcome.DownloadFailed, localFilePath);
		}

		if (!System.IO.File.Exists(localFilePath))
		{
			return new(ModuleUpdateInfoLoadOutcome.FileNotFound, localFilePath);
		}

		cancellationToken.ThrowIfCancellationRequested();
		IniFileParser parser = new(localFilePath);
		string[] lines = System.IO.File.ReadAllLines(
			localFilePath,
			Encoding.GetEncoding(encodingName));
		if (Directory.Exists(peripheralDirectory))
		{
			await Task.Run(() => Directory.Delete(peripheralDirectory, recursive: true));
		}

		return new(ModuleUpdateInfoLoadOutcome.Loaded, localFilePath, parser, lines);
	}

	#endregion

	#region 3. 転送計画の作成

	/// <summary>
	/// 転送ファイル一覧から転送計画を作り、バージョン情報のスナップショットと合わせて返す。
	/// スナップショットにより、転送中に元の一覧が変更されても影響を受けない。
	/// </summary>
	public ModuleUpdateTransferPreparation BuildTransferPreparation(
		IReadOnlyList<UpdateFileGroupResult> fileGroups,
		IReadOnlyList<ModuleUpdateVersion> versions,
		string dataFolder,
		string lcuRoot,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(fileGroups);
		ArgumentNullException.ThrowIfNull(versions);
		cancellationToken.ThrowIfCancellationRequested();

		IReadOnlyList<ModuleFileTransferPlan> plans = CreateTransferPlans(
			fileGroups,
			dataFolder,
			lcuRoot,
			cancellationToken);
		ModuleUpdateVersion[] versionSnapshot = new ModuleUpdateVersion[versions.Count];
		for (int index = 0; index < versions.Count; index++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			versionSnapshot[index] = versions[index];
		}

		cancellationToken.ThrowIfCancellationRequested();
		return new(plans, versionSnapshot);
	}

	/// <summary>
	/// グループごとに、各ユニットの Path と FusePath (あれば) を
	/// 「装置側相対パス (dataFolder を除いた部分)」と「LCU 側パス (lcuRoot + 相対パス)」の対に変換する。
	/// Path が無いユニットがあれば ArgumentException を送出する。
	/// </summary>
	public static IReadOnlyList<ModuleFileTransferPlan> CreateTransferPlans(
		IReadOnlyList<UpdateFileGroupResult> groups,
		string dataFolder,
		string lcuRoot,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(groups);
		ArgumentException.ThrowIfNullOrWhiteSpace(dataFolder);
		ArgumentNullException.ThrowIfNull(lcuRoot);

		List<ModuleFileTransferPlan> plans = new(groups.Count);
		foreach (UpdateFileGroupResult group in groups)
		{
			cancellationToken.ThrowIfCancellationRequested();
			List<string> machineFiles = [];
			List<string> lcuFiles = [];
			foreach (UpdateFileListEntry unit in group.Units)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (unit.Path == null)
				{
					throw new ArgumentException($"Transfer path for '{unit.Name}' is missing.");
				}

				AddTransferPath(unit.Path, dataFolder, lcuRoot, machineFiles, lcuFiles);
				if (!string.IsNullOrEmpty(unit.FusePath))
				{
					cancellationToken.ThrowIfCancellationRequested();
					AddTransferPath(unit.FusePath, dataFolder, lcuRoot, machineFiles, lcuFiles);
				}
			}

			cancellationToken.ThrowIfCancellationRequested();
			plans.Add(new ModuleFileTransferPlan(
				group.GroupName,
				machineFiles.ToArray(),
				lcuFiles.ToArray(),
				group.Units));
		}

		cancellationToken.ThrowIfCancellationRequested();
		return plans;
	}

	private static void AddTransferPath(
		string path,
		string dataFolder,
		string lcuRoot,
		List<string> machineFiles,
		List<string> lcuFiles)
	{
		string relativePath = path[dataFolder.Length..];
		machineFiles.Add(relativePath);
		lcuFiles.Add(lcuRoot + relativePath);
	}

	#endregion

	#region 4. 転送実行

	/// <summary>
	/// 転送計画をグループ順に実行する。各グループで以下を行う。
	///   1. canStartGroup で開始可否を確認 (false なら中断)
	///   2. TransferBatchesAsync でファイルを分割送信 (失敗したら中断)
	///   3. ApplyTransferredGroup で UpdateCommon.inf の行配列へバージョンを反映
	///   4. onGroupSucceeded で呼出し元へ状態変化を通知
	/// </summary>
	/// <param name="updateCommonLines">バージョンを反映する UpdateCommon.inf の行配列 (その場で更新)。</param>
	/// <param name="onBatchSucceeded">バッチ送信成功ごとの通知 (送信件数)。進捗更新用。</param>
	public async Task<ModuleUpdateTransferResult> ExecuteAsync(
		IReadOnlyList<ModuleFileTransferPlan> plans,
		IReadOnlyList<ModuleUpdateVersion> versions,
		string dataFolder,
		string[] updateCommonLines,
		Func<ModuleFileTransferPlan, CancellationToken, Task<bool>> canStartGroup,
		Func<ModuleFileTransferPlan, IReadOnlyList<string>, IReadOnlyList<string>, CancellationToken, Task<bool>> sendBatch,
		Action<ModuleFileTransferPlan, int> onBatchSucceeded,
		Func<ModuleFileTransferPlan, IReadOnlyList<ModuleUpdateStateChange>, Task> onGroupSucceeded,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(plans);
		ArgumentNullException.ThrowIfNull(versions);
		ArgumentException.ThrowIfNullOrWhiteSpace(dataFolder);
		ArgumentNullException.ThrowIfNull(updateCommonLines);
		ArgumentNullException.ThrowIfNull(canStartGroup);
		ArgumentNullException.ThrowIfNull(sendBatch);
		ArgumentNullException.ThrowIfNull(onBatchSucceeded);
		ArgumentNullException.ThrowIfNull(onGroupSucceeded);

		int transferredCount = 0;
		foreach (ModuleFileTransferPlan plan in plans)
		{
			cancellationToken.ThrowIfCancellationRequested();
			bool canStart = await canStartGroup(plan, cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();
			if (!canStart)
			{
				return new ModuleUpdateTransferResult(false, transferredCount, 0, plan.GroupName);
			}

			ModuleFileTransferBatchResult batchResult = await TransferBatchesAsync(
				plan.MachineFiles,
				plan.LcuFiles,
				(machineFiles, lcuFiles, token) => sendBatch(plan, machineFiles, lcuFiles, token),
				count => onBatchSucceeded(plan, count),
				cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();
			transferredCount += batchResult.TransferredFileCount;
			if (!batchResult.IsSuccess)
			{
				return new ModuleUpdateTransferResult(
					false,
					transferredCount,
					1,
					plan.GroupName);
			}

			ModuleUpdateUnit[] units = new ModuleUpdateUnit[plan.Units.Count];
			for (int index = 0; index < plan.Units.Count; index++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				UpdateFileListEntry unit = plan.Units[index];
				units[index] = new ModuleUpdateUnit(unit.Name, unit.Path, unit.FusePath);
			}

			cancellationToken.ThrowIfCancellationRequested();
			IReadOnlyList<ModuleUpdateStateChange> stateChanges = await Task.Run(
				() => ApplyTransferredGroup(
					units,
					versions,
					dataFolder,
					updateCommonLines,
					cancellationToken),
				cancellationToken);
			await onGroupSucceeded(plan, stateChanges);
			cancellationToken.ThrowIfCancellationRequested();
		}

		cancellationToken.ThrowIfCancellationRequested();
		return new ModuleUpdateTransferResult(true, transferredCount, 0, null);
	}

	/// <summary>
	/// ファイルを MaximumFilesPerBatch 件ずつに分けて sendBatch で送信する。
	/// ファイルが 0 件でも空のバッチを 1 回送信する。失敗したバッチで中断する。
	/// </summary>
	public async Task<ModuleFileTransferBatchResult> TransferBatchesAsync(
		IReadOnlyList<string> machineFiles,
		IReadOnlyList<string> lcuFiles,
		Func<IReadOnlyList<string>, IReadOnlyList<string>, CancellationToken, Task<bool>> sendBatch,
		Action<int> onBatchSucceeded,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(machineFiles);
		ArgumentNullException.ThrowIfNull(lcuFiles);
		ArgumentNullException.ThrowIfNull(sendBatch);
		ArgumentNullException.ThrowIfNull(onBatchSucceeded);
		if (machineFiles.Count != lcuFiles.Count)
		{
			throw new ArgumentException("Machine and LCU file lists must have the same number of entries.");
		}

		int transferredCount = 0;
		int batchCount = Math.Max(1, (machineFiles.Count + MaximumFilesPerBatch - 1) / MaximumFilesPerBatch);
		for (int batchIndex = 0; batchIndex < batchCount; batchIndex++)
		{
			cancellationToken.ThrowIfCancellationRequested();

			int start = batchIndex * MaximumFilesPerBatch;
			int count = Math.Min(MaximumFilesPerBatch, machineFiles.Count - start);
			string[] machineBatch = new string[count];
			string[] lcuBatch = new string[count];
			for (int index = 0; index < count; index++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				machineBatch[index] = machineFiles[start + index];
				lcuBatch[index] = lcuFiles[start + index];
			}

			cancellationToken.ThrowIfCancellationRequested();
			bool batchSucceeded = await sendBatch(machineBatch, lcuBatch, cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();
			if (!batchSucceeded)
			{
				return new ModuleFileTransferBatchResult(false, transferredCount);
			}

			transferredCount += count;
			onBatchSucceeded(count);
		}

		return new ModuleFileTransferBatchResult(true, transferredCount);
	}

	/// <summary>
	/// 転送できたグループのユニットについて、状態変化を作成し UpdateCommon.inf の Version を更新する。
	/// ユニットの転送パス (データフォルダからの相対) がバージョン情報の Path / FuserPath と
	/// 一致した場合のみ、UpdateCommon.inf を新バージョンへ書き換える。
	/// 名前の無いユニットには、名前の無い最初のバージョン情報を対応付ける。
	/// </summary>
	public IReadOnlyList<ModuleUpdateStateChange> ApplyTransferredGroup(
		IReadOnlyList<ModuleUpdateUnit> units,
		IReadOnlyList<ModuleUpdateVersion> versions,
		string dataFolder,
		string[] updateCommonLines,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(units);
		ArgumentNullException.ThrowIfNull(versions);
		ArgumentException.ThrowIfNullOrWhiteSpace(dataFolder);
		ArgumentNullException.ThrowIfNull(updateCommonLines);
		cancellationToken.ThrowIfCancellationRequested();

		Dictionary<string, ModuleUpdateVersion> versionsByName =
			new(StringComparer.Ordinal);
		ModuleUpdateVersion? versionWithoutName = null;
		foreach (ModuleUpdateVersion version in versions)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (version.Name == null)
			{
				versionWithoutName ??= version;
			}
			else
			{
				versionsByName.TryAdd(version.Name, version);
			}
		}

		List<ModuleUpdateStateChange> changes = new(units.Count);
		List<(string UnitName, string Version)> updateCommonVersions = [];
		foreach (ModuleUpdateUnit unit in units)
		{
			cancellationToken.ThrowIfCancellationRequested();
			ModuleUpdateVersion? version = unit.Name == null
				? versionWithoutName
				: versionsByName.GetValueOrDefault(unit.Name);
			if (version == null || unit.Path == null)
			{
				continue;
			}

			string? relativePath = NormalizeUpdatePath(Path.GetRelativePath(dataFolder, unit.Path));
			bool matchesUpdatePath =
				string.Equals(relativePath, NormalizeUpdatePath(version.Path), StringComparison.OrdinalIgnoreCase) ||
				string.Equals(relativePath, NormalizeUpdatePath(version.FuserPath), StringComparison.OrdinalIgnoreCase);

			if (matchesUpdatePath && version.Name != null && version.NewVersion != null)
			{
				updateCommonVersions.Add((version.Name, version.NewVersion));
			}

			changes.Add(new ModuleUpdateStateChange(
				unit.Name,
				unit.Path,
				unit.FuserPath,
				version.CurrentVersion,
				version.NewVersion,
				matchesUpdatePath));
		}

		UpdateCommonService.SetVersions(
			updateCommonLines,
			updateCommonVersions,
			cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		return changes;
	}

	private static string? NormalizeUpdatePath(string? path) =>
		string.IsNullOrEmpty(path) ? null : path.Replace('\\', '/').TrimStart('/');

	#endregion

	#region 5. 進捗表示

	/// <summary>
	/// 転送済みバイト数から進捗率を計算し、デバッグ出力用メッセージと合わせて返す。
	/// </summary>
	public ModuleTransferProgressPresentation CreateProgress(
		long transferred,
		long total,
		long displayedTransferredCount,
		long count)
	{
		double progress = _progressCalculator.CalculateModuleProgress(transferred, total);
		return new(
			progress,
			$"Progress={progress} Transfered={displayedTransferredCount} cnt={count}");
	}

	#endregion
}
