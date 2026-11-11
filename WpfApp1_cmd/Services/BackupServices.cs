namespace WpfApp1_cmd.Services
{
using System.IO;

// =============================================================================
// BackupDataService
// -----------------------------------------------------------------------------
// 役割:
//   転送元フォルダとして選択されたフォルダが「通常の更新データ」か
//   「バックアップデータ」かを判定し、画面に出す確認/エラー表示を作るサービス。
//
// 処理の流れ (フォルダ選択時):
//   1. ResolveUpdateCommonPath : 選択フォルダ直下、または Peripheral 相対パス配下の
//                                UpdateCommon.inf を探す
//   2. Read                    : UpdateCommon.inf の 1 行目を読み、バックアップ時に付加した
//                                「#UpdateInfo=LCU名;装置名;モジュール名;日時」があるか確認する
//   3. CreateCandidate         : 1, 2, 4 をまとめて判定結果 (BackupDataCandidate) を作る
//   4. ResolveTarget           : バックアップ元の LCU/装置/モジュールが現在の構成に存在するか確認する
//   5. CreatePresentation      : 判定結果からログ・ダイアログ表示内容を作る
//      ShouldLoadBackupData    : 確認ダイアログの結果から読込を続けるか判定する
//   6. PrepareTargetModuleAsync: 読込後、バックアップ元モジュールのバージョン情報を準備する
//
// 備考:
//   - バックアップデータの作成側 (メタデータ行の付加) は ModuleBackupService.BackupAsync。
// =============================================================================

#region 型定義

/// <summary>
/// UpdateCommon.inf のメタデータ読込結果。
/// IsBackupData=true の場合、LcuName/MachineName/ModuleName/Timestamp にバックアップ元が入る。
/// ErrorMessage は読込失敗・形式不正の場合に設定される。
/// </summary>
public sealed record BackupDataSelectionResult(
	bool UpdateCommonExists,
	bool IsBackupData,
	string? LcuName,
	string? MachineName,
	string? ModuleName,
	string? Timestamp,
	string? ErrorMessage);

/// <summary>バックアップ元の照合結果。</summary>
public enum BackupDataTargetMatch
{
	/// <summary>LCU/装置/モジュールすべて見つかった。</summary>
	Found,
	/// <summary>LCU が見つからない。</summary>
	LcuNotFound,
	/// <summary>装置が見つからない。</summary>
	MachineNotFound,
	/// <summary>モジュールが見つからない。</summary>
	ModuleNotFound
}

/// <summary>バックアップ元の照合結果と、見つかった場合の各インデックス。</summary>
public sealed record BackupDataTargetMatchResult(
	BackupDataTargetMatch Outcome,
	int LcuIndex = -1,
	int MachineIndex = -1,
	int ModuleIndex = -1);

/// <summary>選択フォルダの判定結果。</summary>
public enum BackupDataCandidateOutcome
{
	/// <summary>通常の更新データ。</summary>
	StandardUpdate,
	/// <summary>現在の構成に一致するバックアップデータ。</summary>
	BackupData,
	/// <summary>メタデータの読込失敗/形式不正。</summary>
	MetadataError,
	/// <summary>バックアップ元が現在の構成に存在しない。</summary>
	TargetMismatch
}

/// <summary>選択フォルダの判定結果一式。DataFolder は UpdateCommon.inf のあるフォルダ。</summary>
public sealed record BackupDataCandidate(
	string UpdateCommonPath,
	string DataFolder,
	BackupDataSelectionResult Metadata,
	BackupDataTargetMatchResult? Target,
	BackupDataCandidateOutcome Outcome);

/// <summary>照合用の LCU 構成 (LCU 名と配下の装置)。</summary>
public sealed record BackupDataTarget(
	string Name,
	IReadOnlyList<BackupDataMachine> Machines);

/// <summary>照合用の装置構成 (装置名と配下のモジュール名)。</summary>
public sealed record BackupDataMachine(
	string Name,
	IReadOnlyList<string> Modules);

/// <summary>判定結果の画面表示上の種類。</summary>
public enum BackupDataCandidatePresentationOutcome
{
	/// <summary>通常の更新データ (表示なし)。</summary>
	StandardUpdate,
	/// <summary>バックアップデータの読込確認が必要。</summary>
	BackupDataConfirmationRequired,
	/// <summary>UpdateCommon.inf が見つからない。</summary>
	MissingUpdateCommon,
	/// <summary>メタデータ不正。</summary>
	MetadataError,
	/// <summary>バックアップ元が構成に無い。</summary>
	TargetMismatch
}

/// <summary>
/// 判定結果の表示内容。ErrorCode/LogMessage はエラー時、Dialog* はダイアログ表示時に設定される。
/// </summary>
public sealed record BackupDataCandidatePresentation(
	BackupDataCandidatePresentationOutcome Outcome,
	ErrorCode? ErrorCode,
	string? LogMessage,
	string? DialogTitle,
	string? DialogMessage,
	string? DialogDetail,
	string? DialogInformation,
	bool IsError);

/// <summary>バックアップデータ読込後の対象モジュール準備結果。</summary>
public enum BackupModulePreparationOutcome
{
	/// <summary>準備完了。</summary>
	Ready,
	/// <summary>装置の UpdateCommon.inf を取得できず、管理者でもない。</summary>
	UpdateInfoUnavailable,
	/// <summary>バージョン情報を作成できなかった。</summary>
	VersionInfoUnavailable
}

/// <summary>対象モジュール準備の結果と各ステップの成否。</summary>
public sealed record BackupModulePreparationResult(
	BackupModulePreparationOutcome Outcome,
	bool UpdateInfoLoaded,
	bool VersionInfoCreated);

#endregion

/// <summary>
/// 選択フォルダが更新データかバックアップデータかを判定し、表示内容を作るサービス。
/// </summary>
public sealed class BackupDataService
{
	#region 1. UpdateCommon.inf の特定

	/// <summary>UpdateCommon.inf のあるフォルダ (更新データのルート) を返す。</summary>
	public string GetDataFolder(string updateCommonPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(updateCommonPath);
		return Path.GetDirectoryName(updateCommonPath)
			?? throw new ArgumentException(
				"UpdateCommon.inf must have a parent directory.",
				nameof(updateCommonPath));
	}

	/// <summary>
	/// 選択フォルダ直下の UpdateCommon.inf、無ければ「選択フォルダ + Peripheral 相対パス」配下を探す。
	/// どちらにも無ければ null。
	/// </summary>
	public string? ResolveUpdateCommonPath(
		string selectedFolder,
		string updateCommonFileName,
		string peripheralRelativePath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(selectedFolder);
		ArgumentException.ThrowIfNullOrWhiteSpace(updateCommonFileName);
		ArgumentException.ThrowIfNullOrWhiteSpace(peripheralRelativePath);

		string directPath = Path.Combine(selectedFolder, updateCommonFileName);
		if (File.Exists(directPath))
		{
			return directPath;
		}

		string nestedPath = Path.Combine(
			selectedFolder,
			peripheralRelativePath
				.Replace('/', Path.DirectorySeparatorChar)
				.TrimStart(Path.DirectorySeparatorChar),
			updateCommonFileName);
		return File.Exists(nestedPath) ? nestedPath : null;
	}

	#endregion

	#region 2. メタデータ読込

	/// <summary>
	/// UpdateCommon.inf の 1 行目にバックアップのメタデータ
	/// (「UpdateInfo=LCU名;装置名;モジュール名;日時」) があるか読み取る。
	/// 無ければ通常の更新データ (IsBackupData=false)、項目数が 4 でなければ形式不正とする。
	/// </summary>
	public BackupDataSelectionResult Read(string updateCommonPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(updateCommonPath);
		if (!File.Exists(updateCommonPath))
		{
			return new(false, false, null, null, null, null, "UpdateCommon.inf not found.");
		}

		string? firstLine;
		try
		{
			firstLine = File.ReadLines(updateCommonPath).FirstOrDefault();
		}
		catch (IOException exception)
		{
			return new(true, false, null, null, null, null, exception.Message);
		}
		catch (UnauthorizedAccessException exception)
		{
			return new(true, false, null, null, null, null, exception.Message);
		}

		if (firstLine == null || !firstLine.Contains("UpdateInfo="))
		{
			return new(true, false, null, null, null, null, null);
		}

		string[] markerParts = firstLine.Split('=', 2);
		if (markerParts.Length != 2)
		{
			return new(true, true, null, null, null, null, "Backup metadata is malformed.");
		}

		string metadata = markerParts[1];
		string[] fields = metadata.Split(';');
		if (fields.Length != 4)
		{
			return new(true, true, null, null, null, null, "Backup metadata is malformed.");
		}

		return new(true, true, fields[0], fields[1], fields[2], fields[3], null);
	}

	#endregion

	#region 3. 判定 (まとめ処理)

	/// <summary>
	/// 選択フォルダから UpdateCommon.inf を探し、メタデータ読込と構成照合を行って判定結果を返す。
	/// UpdateCommon.inf が見つからない場合は null。
	/// </summary>
	public BackupDataCandidate? CreateCandidate(
		string selectedFolder,
		string updateCommonFileName,
		string peripheralRelativePath,
		IReadOnlyList<BackupDataTarget> targets)
	{
		ArgumentNullException.ThrowIfNull(targets);

		string? updateCommonPath = ResolveUpdateCommonPath(
			selectedFolder,
			updateCommonFileName,
			peripheralRelativePath);
		if (updateCommonPath == null)
		{
			return null;
		}

		BackupDataSelectionResult metadata = Read(updateCommonPath);
		BackupDataTargetMatchResult? target = null;
		BackupDataCandidateOutcome outcome;
		if (metadata.ErrorMessage != null)
		{
			outcome = BackupDataCandidateOutcome.MetadataError;
		}
		else if (!metadata.IsBackupData)
		{
			outcome = BackupDataCandidateOutcome.StandardUpdate;
		}
		else
		{
			target = ResolveTarget(
				targets,
				metadata.LcuName ?? "",
				metadata.MachineName ?? "",
				metadata.ModuleName ?? "");
			outcome = target.Outcome == BackupDataTargetMatch.Found
				? BackupDataCandidateOutcome.BackupData
				: BackupDataCandidateOutcome.TargetMismatch;
		}

		return new BackupDataCandidate(
			updateCommonPath,
			GetDataFolder(updateCommonPath),
			metadata,
			target,
			outcome);
	}

	#endregion

	#region 4. バックアップ元の照合

	/// <summary>ResolveTarget の照合結果の種類のみを返す。</summary>
	public BackupDataTargetMatch MatchTarget(
		IReadOnlyList<BackupDataTarget> targets,
		string lcuName,
		string machineName,
		string moduleName) =>
		ResolveTarget(targets, lcuName, machineName, moduleName).Outcome;

	/// <summary>
	/// LCU → 装置 → モジュールの順に名前で探し (完全一致・先頭優先)、
	/// 見つからなかった階層を返す。すべて見つかれば各インデックスを返す。
	/// </summary>
	public BackupDataTargetMatchResult ResolveTarget(
		IReadOnlyList<BackupDataTarget> targets,
		string lcuName,
		string machineName,
		string moduleName)
	{
		ArgumentNullException.ThrowIfNull(targets);
		ArgumentNullException.ThrowIfNull(lcuName);
		ArgumentNullException.ThrowIfNull(machineName);
		ArgumentNullException.ThrowIfNull(moduleName);

		int lcuIndex = -1;
		for (int index = 0; index < targets.Count; index++)
		{
			if (targets[index].Name == lcuName)
			{
				lcuIndex = index;
				break;
			}
		}
		if (lcuIndex < 0)
		{
			return new(BackupDataTargetMatch.LcuNotFound);
		}

		IReadOnlyList<BackupDataMachine> machines = targets[lcuIndex].Machines;
		int machineIndex = -1;
		for (int index = 0; index < machines.Count; index++)
		{
			if (machines[index].Name == machineName)
			{
				machineIndex = index;
				break;
			}
		}
		if (machineIndex < 0)
		{
			return new(BackupDataTargetMatch.MachineNotFound);
		}

		IReadOnlyList<string> modules = machines[machineIndex].Modules;
		int moduleIndex = -1;
		for (int index = 0; index < modules.Count; index++)
		{
			if (modules[index] == moduleName)
			{
				moduleIndex = index;
				break;
			}
		}
		return moduleIndex < 0
			? new(BackupDataTargetMatch.ModuleNotFound)
			: new(BackupDataTargetMatch.Found, lcuIndex, machineIndex, moduleIndex);
	}

	#endregion

	#region 5. 表示内容の作成

	/// <summary>
	/// 判定結果から、ログ出力とダイアログ表示の内容を作る。
	/// candidate=null は UpdateCommon.inf が見つからなかったことを示す。
	/// バックアップデータの場合は「読み込むか」の確認ダイアログ内容を返す。
	/// </summary>
	public BackupDataCandidatePresentation CreatePresentation(
		BackupDataCandidate? candidate,
		string selectedPath,
		string errorTitle,
		string informationTitle,
		string undefinedUpdateErrorMessage,
		string backupDataErrorMessage)
	{
		ArgumentNullException.ThrowIfNull(selectedPath);
		ArgumentNullException.ThrowIfNull(errorTitle);
		ArgumentNullException.ThrowIfNull(informationTitle);
		ArgumentNullException.ThrowIfNull(undefinedUpdateErrorMessage);
		ArgumentNullException.ThrowIfNull(backupDataErrorMessage);

		if (candidate == null)
		{
			ErrorCode errorCode = WpfApp1_cmd.ErrorCode.UPDATE_UNDEFINED_ERROR;
			return new(
				BackupDataCandidatePresentationOutcome.MissingUpdateCommon,
				errorCode,
				$"[ERROR] Path={selectedPath} Undefined UpdateCommon.inf",
				errorTitle,
				$"ErrorCode=0x {(int)errorCode:X}",
				undefinedUpdateErrorMessage,
				"UpdateCommon.inf not exist.",
				true);
		}

		BackupDataSelectionResult metadata = candidate.Metadata;
		switch (candidate.Outcome)
		{
			case BackupDataCandidateOutcome.MetadataError:
			{
				ErrorCode errorCode = WpfApp1_cmd.ErrorCode.BACKUP_DATA_MISSMATCH;
				return new(
					BackupDataCandidatePresentationOutcome.MetadataError,
					errorCode,
					$"[ERROR] Backup data read failed:{metadata.ErrorMessage}",
					errorTitle,
					$"ErrorCode=0x{(int)errorCode:X}",
					backupDataErrorMessage,
					metadata.ErrorMessage,
					true);
			}
			case BackupDataCandidateOutcome.TargetMismatch:
			{
				ErrorCode errorCode = WpfApp1_cmd.ErrorCode.BACKUP_DATA_MISSMATCH;
				return new(
					BackupDataCandidatePresentationOutcome.TargetMismatch,
					errorCode,
					null,
					errorTitle,
					$"ErrorCode=0x{(int)errorCode:X}",
					backupDataErrorMessage,
					$"{metadata.LcuName ?? ""}-{metadata.MachineName ?? ""}-{metadata.ModuleName ?? ""}",
					true);
			}
			case BackupDataCandidateOutcome.BackupData:
				return new(
					BackupDataCandidatePresentationOutcome.BackupDataConfirmationRequired,
					null,
					null,
					informationTitle,
					$"{metadata.LcuName ?? ""}={metadata.MachineName ?? ""}={metadata.ModuleName ?? ""}",
					"Backup Data",
					"Load Backup Data ?",
					false);
			case BackupDataCandidateOutcome.StandardUpdate:
				return new(
					BackupDataCandidatePresentationOutcome.StandardUpdate,
					null,
					null,
					null,
					null,
					null,
					null,
					false);
			default:
				throw new ArgumentOutOfRangeException(
					nameof(candidate),
					candidate.Outcome,
					"Unknown backup data candidate outcome.");
		}
	}

	/// <summary>確認ダイアログの結果が CANCEL 以外なら読込を続ける。</summary>
	public bool ShouldLoadBackupData(string? dialogResult) =>
		dialogResult != "CANCEL";

	#endregion

	#region 6. バックアップ元モジュールの準備

	/// <summary>
	/// バックアップデータの読込が決まった後、バックアップ元モジュールの表示を準備する。
	/// 装置の UpdateCommon.inf を取得し (失敗しても管理者なら続行)、バージョン情報を作成する。
	/// 処理中は setBackupSelectionMode(false) とし、終了時 (例外時も) に true へ戻す。
	/// </summary>
	public async Task<BackupModulePreparationResult> PrepareTargetModuleAsync(
		Func<CancellationToken, Task<bool>> loadUpdateInfoAsync,
		Func<CancellationToken, Task<bool>> createVersionInfoAsync,
		bool isAdministrator,
		Action<bool> setBackupSelectionMode,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(loadUpdateInfoAsync);
		ArgumentNullException.ThrowIfNull(createVersionInfoAsync);
		ArgumentNullException.ThrowIfNull(setBackupSelectionMode);

		try
		{
			setBackupSelectionMode(false);
			cancellationToken.ThrowIfCancellationRequested();
			bool updateInfoLoaded = await loadUpdateInfoAsync(cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();
			if (!updateInfoLoaded && !isAdministrator)
			{
				return new(
					BackupModulePreparationOutcome.UpdateInfoUnavailable,
					false,
					false);
			}

			cancellationToken.ThrowIfCancellationRequested();
			bool versionInfoCreated = await createVersionInfoAsync(cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();
			return versionInfoCreated
				? new(BackupModulePreparationOutcome.Ready, updateInfoLoaded, true)
				: new(
					BackupModulePreparationOutcome.VersionInfoUnavailable,
					updateInfoLoaded,
					false);
		}
		finally
		{
			setBackupSelectionMode(true);
		}
	}

	#endregion
}
}

namespace WpfApp1_cmd.Services
{
using WpfLcuCtrlLib;

public sealed class LcuModuleBackupGateway(LcuCtrl lcuCtrl) : IModuleBackupGateway
{
	public Task<bool> CreateFoldersAsync(
		IReadOnlyList<string> folders,
		string lcuRoot,
		CancellationToken cancellationToken) =>
		lcuCtrl.CreateFtpFolders(folders.ToList(), lcuRoot, token: cancellationToken);

	public Task<bool> DownloadFilesAsync(
		string machineName,
		string IpAddress,
		int position,
		IReadOnlyList<string> files,
		string localPath,
		CancellationToken cancellationToken) =>
		lcuCtrl.GetMachineFiles(
			machineName,
			IpAddress,
			position,
			files.ToList(),
			files.ToList(),
			localPath,
			cancellationToken);
}
}

namespace WpfApp1_cmd.Services
{
using System.IO;
using System.Text;

// =============================================================================
// ModuleBackupService
// -----------------------------------------------------------------------------
// 役割:
//   選択されたモジュールの周辺ユニットデータを装置からダウンロードしてバックアップするサービス。
//
// 処理の流れ:
//   1. BuildSelections     : ツリーの選択状態から、バックアップ対象のモジュール一覧を作る
//   2. ExecuteBatchAsync   : 対象モジュールを順に処理する。モジュールごとに
//                            「日時_LCU名_装置名_モジュール名」フォルダを作り、backupModule を呼ぶ
//   3. ExecuteModuleAsync  : 1 モジュール分。装置の UpdateCommon.inf を取得してから BackupAsync を呼ぶ
//   4. BuildUpdatePaths / CreateBackupFiles
//                          : UpdateCommon.inf の Path/FuserPath と UpdateCommon.inf 自身から
//                            ダウンロードするファイルとフォルダを決める (重複除去)
//   5. BackupAsync         : フォルダを作成してファイルをダウンロードし、
//                            UpdateCommon.inf の先頭に「#UpdateInfo=LCU名;装置名;モジュール名;日時」を付加する
//                            (この行により、後でバックアップデータとして読み込める。BackupDataService 参照)
//   6. ResolveResult / CreateResultPresentation
//                          : 成否を判定し、エラーダイアログの表示内容を作る
//
// 備考:
//   - 装置との通信は IModuleBackupGateway に委譲する。
//   - getCurrentTime はフォルダ名/メタデータの日時をテストで固定するための差し替え口。
// =============================================================================

#region 型定義

/// <summary>選択ツリー上のモジュール。</summary>
public sealed record BackupSelectionModule(string? Name, string? IpAddress, bool IsSelected);

/// <summary>選択ツリー上の装置。</summary>
public sealed record BackupSelectionMachine(
	string? Name,
	bool HasModules,
	bool IsSelected,
	IReadOnlyList<BackupSelectionModule> Modules);

/// <summary>選択ツリー上の LCU。HasController=false の LCU は対象外。</summary>
public sealed record BackupSelectionLcu(
	string? Name,
	bool HasMachines,
	bool HasController,
	bool IsSelected,
	IReadOnlyList<BackupSelectionMachine> Machines);

/// <summary>バックアップ対象モジュール (ツリー上の位置と名前)。</summary>
public sealed record ModuleBackupSelection(
	int LcuIndex,
	int MachineIndex,
	int ModuleIndex,
	string? LcuName,
	string MachineName,
	string? IPAddress,
	string? ModuleName);

/// <summary>一括バックアップの 1 件分。Context は呼出し元が任意の情報を持たせるためのもの。</summary>
public sealed record ModuleBackupWorkItem<TContext>(
	string LcuName,
	string MachineName,
	string ModuleName,
	string IpAddress,
	TContext Context);

/// <summary>一括バックアップの結果。</summary>
public enum ModuleBackupBatchOutcome
{
	/// <summary>全件成功。</summary>
	Succeeded,
	/// <summary>途中のモジュールで失敗。</summary>
	Failed,
	/// <summary>キャンセル、または継続確認で中止。</summary>
	Cancelled
}

/// <summary>一括バックアップの結果。StoppedAt は中断したモジュール (成功時は null)。</summary>
public sealed record ModuleBackupBatchResult<TContext>(
	ModuleBackupBatchOutcome Outcome,
	ModuleBackupWorkItem<TContext>? StoppedAt);

/// <summary>1 モジュール分のバックアップ結果。</summary>
public enum ModuleBackupExecutionOutcome
{
	Succeeded,
	UpdateInfoUnavailable,
	FolderCreationFailed,
	DownloadFailed
}

/// <summary>ダウンロードするファイル ('/' 区切りの相対パスと、その親フォルダ)。</summary>
public sealed record ModuleBackupFile(
	string RelativePath,
	string RelativeDirectory);

/// <summary>バックアップ時の装置との通信窓口。</summary>
public interface IModuleBackupGateway
{
	/// <summary>LCU 上にダウンロード用のフォルダを作成する。</summary>
	Task<bool> CreateFoldersAsync(
		IReadOnlyList<string> folders,
		string lcuRoot,
		CancellationToken cancellationToken);

	/// <summary>装置のファイルを localPath 配下へダウンロードする。</summary>
	Task<bool> DownloadFilesAsync(
		string machineName,
		string IpAddress,
		int position,
		IReadOnlyList<string> files,
		string localPath,
		CancellationToken cancellationToken);
}

/// <summary>
/// 1 モジュール分のバックアップ要求。
/// UpdatePaths はダウンロードするファイル、BackupPath は保存先フォルダ。
/// </summary>
public sealed record ModuleBackupRequest(
	string LcuName,
	string MachineName,
	string ModuleName,
	string IpAddress,
	int Position,
	string BackupPath,
	IReadOnlyList<string> UpdatePaths,
	string UpdateCommonPath,
	string LcuRoot,
	string TextEncoding);

/// <summary>BackupAsync の結果。</summary>
public enum ModuleBackupResult
{
	Succeeded,
	FolderCreationFailed,
	DownloadFailed
}

/// <summary>バックアップ全体の成否と、失敗時に表示するメッセージ。</summary>
public sealed record BackupExecutionResult(
	BackupOutcome Outcome,
	string? FailureMessage);

/// <summary>バックアップ結果の表示内容。ShowDialog=false なら表示不要。</summary>
public sealed record BackupResultPresentation(
	bool ShowDialog,
	string? DialogTitle,
	string? Message,
	string? Detail,
	bool IsCancelled);

#endregion

/// <summary>
/// モジュールの周辺ユニットデータをバックアップするサービス (対象抽出・一括実行・ダウンロード・結果表示)。
/// </summary>
/// <param name="getCurrentTime">現在時刻の取得関数 (省略時は DateTime.Now)。</param>
public sealed class ModuleBackupService(Func<DateTime>? getCurrentTime = null)
{
	#region 1. 対象モジュールの抽出

	/// <summary>
	/// 選択中の LCU (装置あり・コントローラあり) → 選択中の装置 (モジュールあり・名前あり) →
	/// 選択中のモジュールを順に辿り、バックアップ対象の一覧を作る。
	/// </summary>
	public IReadOnlyList<ModuleBackupSelection> BuildSelections(
		IReadOnlyList<BackupSelectionLcu> lcus,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(lcus);
		cancellationToken.ThrowIfCancellationRequested();

		List<ModuleBackupSelection> selections = [];
		for (int lcuIndex = 0; lcuIndex < lcus.Count; lcuIndex++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			BackupSelectionLcu lcu = lcus[lcuIndex];
			if (!lcu.HasMachines || !lcu.HasController || !lcu.IsSelected)
			{
				continue;
			}

			for (int machineIndex = 0; machineIndex < lcu.Machines.Count; machineIndex++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				BackupSelectionMachine machine = lcu.Machines[machineIndex];
				if (!machine.HasModules || machine.Name == null || !machine.IsSelected)
				{
					continue;
				}

				for (int moduleIndex = 0; moduleIndex < machine.Modules.Count; moduleIndex++)
				{
					cancellationToken.ThrowIfCancellationRequested();
					if (!machine.Modules[moduleIndex].IsSelected)
					{
						continue;
					}

					selections.Add(new(
						lcuIndex,
						machineIndex,
						moduleIndex,
						lcu.Name,
						machine.Name,
						machine.Modules[moduleIndex].IpAddress,
						machine.Modules[moduleIndex].Name));
				}
			}
		}

		cancellationToken.ThrowIfCancellationRequested();
		return selections;
	}

	#endregion

	#region 2. 複数モジュールの一括バックアップ

	/// <summary>
	/// 対象モジュールを順にバックアップする。保存先は
	/// 「backupRoot\日時_LCU名_装置名_モジュール名」(名前は ':' より前の部分、日時は全件共通)。
	/// 各モジュールの後に continueAfterModule で継続確認し、false なら Cancelled で終了する。
	/// キャンセル時は例外を送出せず Cancelled を返す。
	/// </summary>
	public async Task<ModuleBackupBatchResult<TContext>> ExecuteBatchAsync<TContext>(
		IReadOnlyList<ModuleBackupWorkItem<TContext>> workItems,
		string backupRoot,
		Func<ModuleBackupWorkItem<TContext>, string, CancellationToken, Task<bool>> backupModule,
		Func<ModuleBackupWorkItem<TContext>, CancellationToken, Task<bool>> continueAfterModule,
		Action<ModuleBackupWorkItem<TContext>>? onModuleStarted,
		Action<ModuleBackupWorkItem<TContext>>? onModuleCompleted,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(workItems);
		ArgumentException.ThrowIfNullOrWhiteSpace(backupRoot);
		ArgumentNullException.ThrowIfNull(backupModule);
		ArgumentNullException.ThrowIfNull(continueAfterModule);

		if (cancellationToken.IsCancellationRequested)
		{
			return new(ModuleBackupBatchOutcome.Cancelled, workItems.FirstOrDefault());
		}

		string timestamp = (getCurrentTime?.Invoke() ?? DateTime.Now).ToString("yyyyMMdd_HHmmss_");
		foreach (ModuleBackupWorkItem<TContext> workItem in workItems)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return new(ModuleBackupBatchOutcome.Cancelled, workItem);
			}

			onModuleStarted?.Invoke(workItem);
			if (cancellationToken.IsCancellationRequested)
			{
				return new(ModuleBackupBatchOutcome.Cancelled, workItem);
			}

			string backupPath = Path.Combine(
				backupRoot,
				$"{timestamp}{GetNamePart(workItem.LcuName)}_{GetNamePart(workItem.MachineName)}_{workItem.ModuleName}");
			await Task.Run(
				() => Directory.CreateDirectory(backupPath),
				cancellationToken);
			if (cancellationToken.IsCancellationRequested)
			{
				return new(ModuleBackupBatchOutcome.Cancelled, workItem);
			}

			try
			{
				bool backupSucceeded = await backupModule(workItem, backupPath, cancellationToken);
				if (cancellationToken.IsCancellationRequested)
				{
					return new(ModuleBackupBatchOutcome.Cancelled, workItem);
				}
				if (!backupSucceeded)
				{
					return new(ModuleBackupBatchOutcome.Failed, workItem);
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				return new(ModuleBackupBatchOutcome.Cancelled, workItem);
			}

			onModuleCompleted?.Invoke(workItem);
			if (cancellationToken.IsCancellationRequested)
			{
				return new(ModuleBackupBatchOutcome.Cancelled, workItem);
			}

			try
			{
				bool shouldContinue = await continueAfterModule(workItem, cancellationToken);
				if (cancellationToken.IsCancellationRequested || !shouldContinue)
				{
					return new(ModuleBackupBatchOutcome.Cancelled, workItem);
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				return new(ModuleBackupBatchOutcome.Cancelled, workItem);
			}
		}

		if (cancellationToken.IsCancellationRequested)
		{
			return new(
				ModuleBackupBatchOutcome.Cancelled,
				workItems.Count > 0 ? workItems[^1] : null);
		}

		return new(ModuleBackupBatchOutcome.Succeeded, null);
	}

	private static string GetNamePart(string name) => name.Split(':')[0];

	#endregion

	#region 3. 1 モジュールのバックアップ

	/// <summary>
	/// 1 モジュール分のバックアップ。loadUpdateInfo で装置の UpdateCommon.inf を取得し、
	/// 成功した場合のみ createRequest で要求を作って BackupAsync を実行する。
	/// </summary>
	public async Task<ModuleBackupExecutionOutcome> ExecuteModuleAsync(
		Func<CancellationToken, Task<bool>> loadUpdateInfo,
		Func<ModuleBackupRequest> createRequest,
		IModuleBackupGateway gateway,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(loadUpdateInfo);
		ArgumentNullException.ThrowIfNull(createRequest);
		ArgumentNullException.ThrowIfNull(gateway);

		cancellationToken.ThrowIfCancellationRequested();
		bool updateInfoLoaded = await loadUpdateInfo(cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		if (!updateInfoLoaded)
		{
			return ModuleBackupExecutionOutcome.UpdateInfoUnavailable;
		}

		ModuleBackupResult backupResult = await BackupAsync(
			createRequest(),
			gateway,
			cancellationToken);
		return backupResult switch
		{
			ModuleBackupResult.Succeeded => ModuleBackupExecutionOutcome.Succeeded,
			ModuleBackupResult.FolderCreationFailed => ModuleBackupExecutionOutcome.FolderCreationFailed,
			ModuleBackupResult.DownloadFailed => ModuleBackupExecutionOutcome.DownloadFailed,
			_ => throw new ArgumentOutOfRangeException(nameof(backupResult), backupResult, null)
		};
	}

	#endregion

	#region 4. バックアップ対象ファイルの決定

	/// <summary>UpdateCommon.inf の各ユニットの Path と FuserPath (あれば) を並べる。</summary>
	public IReadOnlyList<string> BuildUpdatePaths(
		IReadOnlyList<UpdateCommonPathEntry> updateInfoPaths,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(updateInfoPaths);
		cancellationToken.ThrowIfCancellationRequested();

		List<string> paths = [];
		foreach (UpdateCommonPathEntry entry in updateInfoPaths)
		{
			cancellationToken.ThrowIfCancellationRequested();
			paths.Add(entry.Path);
			if (entry.FuserPath.Length > 0)
			{
				paths.Add(entry.FuserPath);
			}
		}

		cancellationToken.ThrowIfCancellationRequested();
		return paths;
	}

	/// <summary>
	/// ダウンロードするファイル一覧を作る。UpdateCommon.inf 自身を末尾に加え、
	/// 区切りを '/' に統一して前後の '/' を除き、大文字小文字を区別せず重複を除く。
	/// </summary>
	public static IReadOnlyList<ModuleBackupFile> CreateBackupFiles(
		IEnumerable<string> updateCommonPaths,
		string updateCommonPath,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(updateCommonPaths);
		ArgumentNullException.ThrowIfNull(updateCommonPath);
		cancellationToken.ThrowIfCancellationRequested();

		HashSet<string> uniquePaths = new(StringComparer.OrdinalIgnoreCase);
		List<ModuleBackupFile> files = [];
		foreach (string path in updateCommonPaths.Append(updateCommonPath))
		{
			cancellationToken.ThrowIfCancellationRequested();
			string normalizedPath = NormalizePath(path);
			if (normalizedPath.Length == 0 || !uniquePaths.Add(normalizedPath))
			{
				continue;
			}

			int separator = normalizedPath.LastIndexOf('/');
			files.Add(new ModuleBackupFile(
				normalizedPath,
				separator < 0 ? "" : normalizedPath[..separator]));
		}

		cancellationToken.ThrowIfCancellationRequested();
		return files;
	}

	private static string NormalizePath(string path) =>
		path.Replace('\\', '/').Trim('/');

	#endregion

	#region 5. ダウンロードとメタデータ付加

	/// <summary>
	/// バックアップ本体。保存先にフォルダを作成し、LCU 上にもフォルダを作成してからファイルをダウンロードする。
	/// 最後に保存した UpdateCommon.inf の先頭へ
	/// 「#UpdateInfo=LCU名;装置名;モジュール名;yyyy/MM/dd HH:mm:ss」行を追加する。
	/// </summary>
	public async Task<ModuleBackupResult> BackupAsync(
		ModuleBackupRequest request,
		IModuleBackupGateway gateway,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(gateway);
		cancellationToken.ThrowIfCancellationRequested();

		(IReadOnlyList<string> Folders, IReadOnlyList<string> Files) backupPlan =
			await Task.Run(() =>
			{
				IReadOnlyList<ModuleBackupFile> backupFiles = CreateBackupFiles(
					request.UpdatePaths,
					request.UpdateCommonPath,
					cancellationToken);
				List<string> folders = [];
				HashSet<string> uniqueFolders = new(StringComparer.OrdinalIgnoreCase);
				string[] files = new string[backupFiles.Count];
				for (int index = 0; index < backupFiles.Count; index++)
				{
					cancellationToken.ThrowIfCancellationRequested();
					ModuleBackupFile file = backupFiles[index];
					files[index] = file.RelativePath;
					if (file.RelativeDirectory.Length > 0
						&& uniqueFolders.Add(file.RelativeDirectory))
					{
						folders.Add(file.RelativeDirectory);
					}
				}

				cancellationToken.ThrowIfCancellationRequested();
				return ((IReadOnlyList<string>)folders, (IReadOnlyList<string>)files);
			}, cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();

		await Task.Run(() =>
		{
			foreach (string folder in backupPlan.Folders)
			{
				cancellationToken.ThrowIfCancellationRequested();
				Directory.CreateDirectory(Path.Combine(
					request.BackupPath,
					folder.Replace('/', Path.DirectorySeparatorChar)));
			}
		}, cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();

		bool foldersCreated = await gateway.CreateFoldersAsync(
			backupPlan.Folders,
			request.LcuRoot,
			cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		if (!foldersCreated)
		{
			return ModuleBackupResult.FolderCreationFailed;
		}

		bool filesDownloaded = await gateway.DownloadFilesAsync(
			request.MachineName,
			request.IpAddress,
			request.Position,
			backupPlan.Files,
			request.BackupPath,
			cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		if (!filesDownloaded)
		{
			return ModuleBackupResult.DownloadFailed;
		}

		string infoFile = Path.Combine(
			request.BackupPath,
			request.UpdateCommonPath
				.TrimStart('/', '\\')
				.Replace('/', Path.DirectorySeparatorChar));
		string[] data = await File.ReadAllLinesAsync(
			infoFile,
			Encoding.GetEncoding(request.TextEncoding),
			cancellationToken);
		string timestamp = (getCurrentTime?.Invoke() ?? DateTime.Now).ToString("yyyy/MM/dd HH:mm:ss");
		string[] updatedData =
		[
			$"#UpdateInfo={request.LcuName};{request.MachineName};{request.ModuleName};{timestamp}",
			.. data
		];
		await File.WriteAllLinesAsync(
			infoFile,
			updatedData,
			Encoding.GetEncoding(request.TextEncoding),
			cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();

		return ModuleBackupResult.Succeeded;
	}

	#endregion

	#region 6. 結果の判定と表示

	/// <summary>
	/// バックアップ成功かつエラーなしなら成功。それ以外は失敗とし、
	/// エラーがあればそのメッセージ、無ければ requiredFailureMessage を失敗理由とする。
	/// </summary>
	public BackupExecutionResult ResolveResult(
		bool backupSucceeded,
		bool hasError,
		string? errorMessage,
		string requiredFailureMessage)
	{
		ArgumentNullException.ThrowIfNull(requiredFailureMessage);

		if (backupSucceeded && !hasError)
		{
			return new(BackupOutcome.Succeeded, null);
		}

		return new(
			BackupOutcome.Failed,
			hasError ? errorMessage : requiredFailureMessage);
	}

	/// <summary>
	/// バックアップ結果からダイアログ表示内容を作る。成功時は表示なし、
	/// キャンセル時はキャンセルメッセージ、失敗時は requiredMessage を表示する。
	/// </summary>
	public BackupResultPresentation CreateResultPresentation(
		BackupOutcome outcome,
		string errorTitle,
		string cancelledMessage,
		string requiredMessage,
		string? failureMessage)
	{
		ArgumentNullException.ThrowIfNull(errorTitle);
		ArgumentNullException.ThrowIfNull(cancelledMessage);
		ArgumentNullException.ThrowIfNull(requiredMessage);

		return outcome switch
		{
			BackupOutcome.Succeeded => new(false, null, null, null, false),
			BackupOutcome.Cancelled => new(
				true,
				errorTitle,
				cancelledMessage,
				failureMessage ?? requiredMessage,
				true),
			BackupOutcome.Failed => new(
				true,
				errorTitle,
				requiredMessage,
				failureMessage ?? requiredMessage,
				false),
			_ => throw new ArgumentOutOfRangeException(
				nameof(outcome),
				outcome,
				"Unknown backup outcome.")
		};
	}

	#endregion
}
}
