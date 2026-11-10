using System.IO;

namespace WpfApp1_cmd.Services;

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
