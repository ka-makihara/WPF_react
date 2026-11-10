using System.IO;
using WpfLcuCtrlLib;

namespace WpfApp1_cmd.Services;

// =====================================================================
//  ユニット一覧(転送ユニット選択画面の行)作成サービス
//
//  モジュールの UpdateCommon.inf(装置側)とアップデートデータ側のバージョンを比較し、
//  画面に表示する UnitVersion の定義・ログメッセージを作成する。
//
//  処理の流れ(Create):
//    1. BuildInputs        : 入力スナップショット/UpdateCommon.inf から比較用データを作る
//    2. SelectCandidates   : バージョンが異なるユニットを候補として抽出(同一バージョンはスキップ)
//    3. ResolveGroup       : unitLink.json の定義からユニットの所属グループ名を決定
//    4. BuildModel         : 更新ファイルの有無・Attribute・選択可否から行データを作成
//    5. GetGroupsToDeselect: ユーザーモード時、グループ内に未選択があればグループ全体を未選択にする
//    6. Project            : 作成できた行/できなかった行/FuserPath クリア対象に振り分け
//    7. CreateDiagnostics  : アプリログ/デバッグ出力用のメッセージを作成
//
//  WPF には依存しない(UnitVersion モデルの生成は呼び出し側 ViewModel が行う)。
// =====================================================================

#region 公開する入出力

/// <summary>
///  更新ファイル(UpdateCommon.inf の Path)がアップデートデータに存在しない場合の扱い(--fileExist)
/// </summary>
public enum UnitVersionMissingFileMode
{
	/// <summary>一覧に表示しない(--fileExist=strict)</summary>
	Exclude,
	/// <summary>一覧に表示するが選択不可(既定)</summary>
	ListOnly,
	/// <summary>一覧に表示し選択も可能(--fileExist=test)</summary>
	Selectable
}

/// <summary>アップデートデータ側のユニット名とバージョン</summary>
public sealed record UpdateVersionInputSnapshot(string Name, string Version);

/// <summary>unitLink.json のグループ定義(構成ユニット名と、機種別のグループ名)</summary>
public sealed record UnitVersionGroupInputSnapshot(
	IReadOnlyList<string> Components,
	IReadOnlyList<KeyValuePair<string, string>> Names);

/// <summary>既存ユニット行のグループ名と選択状態</summary>
public sealed record UnitGroupSelection(string? GroupName, bool? IsSelected);

/// <summary>
///  ユニット一覧作成の入力
/// </summary>
/// <param name="UpdateVersions">アップデートデータ側のユニット/バージョン</param>
/// <param name="Groups">unitLink.json のグループ定義</param>
/// <param name="UpdateCommon">比較対象の UpdateCommon.inf(装置側、または管理者モードではアップデートデータ側)</param>
/// <param name="ExistingUnits">モジュールに既に存在するユニット行(グループ選択統一で使用)</param>
/// <param name="MachineType">装置の機種(グループ名の決定に使用)</param>
/// <param name="DataFolder">アップデートデータのルートフォルダ</param>
/// <param name="IgnoreVersion">true の場合、同一バージョンでも一覧に含める(--ignore_version)</param>
/// <param name="IsModuleSelected">モジュールが選択されているか(行の初期選択に反映)</param>
/// <param name="IsBackupData">バックアップデータを使用中か(true なら初期選択しない)</param>
/// <param name="IsBaseUpdateInfo">装置側情報が無くアップデートデータ側を使ったか(現バージョンを "Undefined" とする)</param>
/// <param name="UnifyGroupSelection">グループ単位で選択状態を揃えるか(ユーザーモード)</param>
/// <param name="NotUpdateAttribute">アップデート禁止を表す Attribute 値</param>
/// <param name="MissingFileMode">更新ファイルが無い場合の扱い</param>
/// <param name="LcuName">ログ出力用 LCU 名</param>
/// <param name="MachineName">ログ出力用 装置名</param>
/// <param name="ModuleName">ログ出力用 モジュール名</param>
public sealed record UnitVersionListRequest(
	IReadOnlyList<UpdateVersionInputSnapshot> UpdateVersions,
	IReadOnlyList<UnitVersionGroupInputSnapshot> Groups,
	IniFileParser UpdateCommon,
	IReadOnlyList<UnitGroupSelection> ExistingUnits,
	string MachineType,
	string DataFolder,
	bool IgnoreVersion,
	bool IsModuleSelected,
	bool IsBackupData,
	bool IsBaseUpdateInfo,
	bool UnifyGroupSelection,
	int NotUpdateAttribute,
	UnitVersionMissingFileMode MissingFileMode,
	string LcuName,
	string MachineName,
	string ModuleName);

/// <summary>
///  ユニット一覧作成の結果
/// </summary>
/// <param name="ItemsToCreate">画面に追加するユニット行</param>
/// <param name="ExistingUnitsToDeselect">選択解除すべき既存ユニット行のインデックス</param>
/// <param name="FuserPathsToClear">ファイルが無いため UpdateCommon.inf から消す FuserPath(ユニット名, 元のパス)</param>
/// <param name="Diagnostics">ログメッセージ</param>
public sealed record UnitVersionListResult(
	IReadOnlyList<UnitVersionPreparedDefinition> ItemsToCreate,
	IReadOnlyList<int> ExistingUnitsToDeselect,
	IReadOnlyList<(string Name, string? FuserPath)> FuserPathsToClear,
	IReadOnlyList<UnitVersionPreparationDiagnostic> Diagnostics);

/// <summary>画面に追加するユニット行の定義</summary>
public sealed record UnitVersionPreparedDefinition(
	string Name,
	bool IsSelected,
	int Attribute,
	string? Path,
	string? FuserPath,
	string? CurrentVersion,
	string? NewVersion,
	long Size,
	string? UnitGroup,
	bool IsUpdateFileMissing = false);

public enum UnitVersionDiagnosticDestination
{
	/// <summary>アプリのログ画面</summary>
	ApplicationLog,
	/// <summary>Debug.WriteLine のみ</summary>
	Debug
}

public enum UnitVersionPreparationDiagnosticPhase
{
	/// <summary>一覧作成時に出力</summary>
	Preparation,
	/// <summary>UpdateCommon.inf 更新時に出力</summary>
	IniUpdate
}

public sealed record UnitVersionPreparationDiagnostic(
	UnitVersionPreparationDiagnosticPhase Phase,
	UnitVersionDiagnosticDestination Destination,
	string Message);

#endregion

#region 内部処理の入出力(テストから各段階を検証するため public)

public sealed record UpdateVersionCandidate(string Name, string Version);

public sealed record MachineUnitVersion(string Name, string Version);

public sealed record UnitVersionSelectionResult(
	IReadOnlyList<UpdateVersionCandidate> Candidates,
	IReadOnlyList<UpdateVersionCandidate> SkippedSameVersion)
{
	public IReadOnlyList<UpdateVersionCandidate> SkippedNotOnMachine { get; init; } = [];
}

public sealed record UnitVersionGroupDefinition(
	IReadOnlyList<string> Components,
	IReadOnlyDictionary<string, string> Names);

/// <summary>UpdateCommon.inf の 1 セクション(ユニット)</summary>
public sealed record UnitVersionDefinition(
	string Name,
	string? Path,
	string? FuserPath,
	string? Attribute,
	string? CurrentVersion);

public sealed record UnitVersionPreparationInputs(
	IReadOnlyList<UpdateVersionCandidate> UpdateVersions,
	IReadOnlyList<UnitVersionGroupDefinition> Groups,
	IReadOnlyList<UnitVersionDefinition> MachineUnits,
	IReadOnlyDictionary<string, string> FuserPaths);

public enum UnitVersionBuildOutcome
{
	Created,
	UpdateFileNotFound,
	UpdatePathIsDirectory,
	InvalidAttribute
}

public sealed record UnitVersionBuildRequest(
	string UpdateFilePath,
	string? FuserFilePath,
	string? Attribute,
	string? MachineVersion,
	string? NewVersion,
	string? UnitGroup,
	bool IsSelected,
	bool IsBackupData,
	bool IsBaseUpdateInfo,
	int NotUpdateAttribute,
	string? Path = null,
	string? FuserPath = null,
	bool AllowMissingUpdateFile = false);

public sealed record UnitVersionBuildResult(
	UnitVersionBuildOutcome Outcome,
	int Attribute = 0,
	string? Path = null,
	string? FuserPath = null,
	bool ClearFuserPath = false,
	string? CurrentVersion = null,
	string? NewVersion = null,
	string? UnitGroup = null,
	bool IsSelected = false,
	long Size = 0,
	bool IsUpdateFileMissing = false);

public sealed record UnitVersionPreparationRequest(
	IReadOnlyList<UpdateVersionCandidate> UpdateVersions,
	IReadOnlyList<UnitVersionDefinition> MachineUnits,
	IReadOnlyList<UnitGroupSelection> ExistingUnits,
	IReadOnlyList<UnitVersionGroupDefinition> Groups,
	string MachineType,
	string DataFolder,
	bool IgnoreVersion,
	bool IsModuleSelected,
	bool IsBackupData,
	bool IsBaseUpdateInfo,
	bool UnifyGroupSelection,
	int NotUpdateAttribute,
	bool AllowMissingUpdateFile = false);

public sealed record UnitVersionPreparationItem(
	string UnitName,
	string? SourcePath,
	string NewVersion,
	string? Attribute,
	UnitVersionBuildResult BuildResult);

public sealed record UnitVersionPreparationResult(
	IReadOnlyList<UpdateVersionCandidate> SkippedSameVersion,
	IReadOnlyList<UnitVersionPreparationItem> Items,
	IReadOnlyList<int> ExistingUnitsToDeselect,
	IReadOnlyList<int> NewItemsToDeselect)
{
	public IReadOnlyList<UpdateVersionCandidate> SkippedNotOnMachine { get; init; } = [];
}

public sealed record UnitVersionPreparationProjection(
	IReadOnlyList<UpdateVersionCandidate> SkippedSameVersion,
	IReadOnlyList<UnitVersionPreparationItem> ItemsNotCreated,
	IReadOnlyList<UnitVersionPreparedDefinition> ItemsToCreate,
	IReadOnlyList<(string Name, string? FuserPath)> FuserPathsToClear,
	IReadOnlyList<int> ExistingUnitsToDeselect)
{
	public IReadOnlyList<UpdateVersionCandidate> SkippedNotOnMachine { get; init; } = [];
}

#endregion

/// <summary>
///  ユニット一覧(転送ユニット選択画面の行)を作成する。処理の流れはファイル先頭のコメントを参照。
/// </summary>
public sealed class UnitVersionListService
{
	/// <summary>
	///  ユニット一覧を作成する(1〜7 の全段階を実行)。
	/// </summary>
	public UnitVersionListResult Create(
		UnitVersionListRequest request,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);
		cancellationToken.ThrowIfCancellationRequested();

		UnitVersionPreparationInputs inputs = BuildInputs(
			request.UpdateVersions,
			request.Groups,
			request.UpdateCommon,
			cancellationToken);

		UnitVersionPreparationResult preparation = Prepare(
			new UnitVersionPreparationRequest(
				inputs.UpdateVersions,
				inputs.MachineUnits,
				request.ExistingUnits,
				inputs.Groups,
				request.MachineType,
				request.DataFolder,
				request.IgnoreVersion,
				request.IsModuleSelected,
				request.IsBackupData,
				request.IsBaseUpdateInfo,
				request.UnifyGroupSelection,
				request.NotUpdateAttribute,
				request.MissingFileMode != UnitVersionMissingFileMode.Exclude),
			cancellationToken);

		UnitVersionPreparationProjection projection = Project(
			preparation,
			inputs.FuserPaths,
			cancellationToken);

		IReadOnlyList<UnitVersionPreparationDiagnostic> diagnostics = CreateDiagnostics(
			projection,
			request.LcuName,
			request.MachineName,
			request.ModuleName,
			cancellationToken,
			request.MissingFileMode == UnitVersionMissingFileMode.Selectable);

		return new(
			projection.ItemsToCreate,
			projection.ExistingUnitsToDeselect,
			projection.FuserPathsToClear,
			diagnostics);
	}

	/// <summary>
	///  作成したユニット行のデバッグ出力用メッセージ
	/// </summary>
	public string CreateCreatedVersionMessage(
		string? moduleName,
		string? versionName,
		string? currentVersion,
		string? newVersion) =>
		$"[UnitVersion] {moduleName}:{versionName}={currentVersion}=>{newVersion}";

	#region 1. 入力の構築

	/// <summary>
	///  入力スナップショットと UpdateCommon.inf から、比較用のデータを作る。
	///  UpdateCommon.inf の各セクションを UnitVersionDefinition に、FuserPath を名前引きの辞書にする。
	/// </summary>
	internal UnitVersionPreparationInputs BuildInputs(
		IReadOnlyList<UpdateVersionInputSnapshot> updateVersions,
		IReadOnlyList<UnitVersionGroupInputSnapshot> groups,
		IniFileParser parser,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(updateVersions);
		ArgumentNullException.ThrowIfNull(groups);
		ArgumentNullException.ThrowIfNull(parser);
		cancellationToken.ThrowIfCancellationRequested();

		List<UpdateVersionCandidate> versionCandidates = new(updateVersions.Count);
		foreach (UpdateVersionInputSnapshot update in updateVersions)
		{
			cancellationToken.ThrowIfCancellationRequested();
			versionCandidates.Add(new UpdateVersionCandidate(update.Name, update.Version));
		}

		List<UnitVersionGroupDefinition> groupDefinitions = new(groups.Count);
		foreach (UnitVersionGroupInputSnapshot group in groups)
		{
			cancellationToken.ThrowIfCancellationRequested();
			Dictionary<string, string> names = new(StringComparer.Ordinal);
			foreach (KeyValuePair<string, string> entry in group.Names)
			{
				cancellationToken.ThrowIfCancellationRequested();
				names.Add(entry.Key, entry.Value);
			}
			groupDefinitions.Add(new UnitVersionGroupDefinition(group.Components, names));
		}

		List<UnitVersionDefinition> machineUnits = [];
		Dictionary<string, string> fuserPaths = new(StringComparer.Ordinal);
		foreach (string unit in parser.SectionCount())
		{
			cancellationToken.ThrowIfCancellationRequested();
			string fuserPath = parser.GetValue(unit, "FuserPath");
			machineUnits.Add(new UnitVersionDefinition(
				unit,
				parser.GetValue(unit, "Path"),
				fuserPath,
				parser.GetValue(unit, "Attribute"),
				parser.GetValue(unit, "Version")));
			fuserPaths.TryAdd(unit, fuserPath);
		}

		cancellationToken.ThrowIfCancellationRequested();
		return new(versionCandidates, groupDefinitions, machineUnits, fuserPaths);
	}

	#endregion

	#region 2〜5. 一覧の準備(候補抽出・グループ決定・行作成・グループ選択統一)

	/// <summary>
	///  候補抽出→グループ決定→行作成→グループ選択統一 を行う。
	///  同名ユニットが UpdateCommon.inf に複数ある場合は先頭の定義を使う。
	/// </summary>
	internal UnitVersionPreparationResult Prepare(
		UnitVersionPreparationRequest request,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(request.UpdateVersions);
		ArgumentNullException.ThrowIfNull(request.MachineUnits);
		ArgumentNullException.ThrowIfNull(request.ExistingUnits);
		ArgumentNullException.ThrowIfNull(request.Groups);
		ArgumentNullException.ThrowIfNull(request.MachineType);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.DataFolder);
		cancellationToken.ThrowIfCancellationRequested();

		Dictionary<string, UnitVersionDefinition> machineUnitsByName =
			new(StringComparer.Ordinal);
		Dictionary<string, string> currentVersionsByName =
			new(StringComparer.Ordinal);
		foreach (UnitVersionDefinition unit in request.MachineUnits)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (machineUnitsByName.TryAdd(unit.Name, unit))
			{
				currentVersionsByName.Add(unit.Name, unit.CurrentVersion ?? "");
			}
		}

		UnitVersionSelectionResult selection = SelectCandidates(
			request.UpdateVersions,
			currentVersionsByName,
			request.IgnoreVersion,
			cancellationToken);
		IReadOnlyDictionary<string, UnitVersionGroupDefinition> groupsByComponent =
			CreateGroupIndex(request.Groups, cancellationToken);
		List<UnitVersionPreparationItem> preparedItems = [];
		foreach (UpdateVersionCandidate candidate in selection.Candidates)
		{
			cancellationToken.ThrowIfCancellationRequested();
			UnitVersionDefinition unit = machineUnitsByName[candidate.Name];
			string group = ResolveGroup(
				candidate.Name,
				request.MachineType,
				groupsByComponent,
				cancellationToken);
			UnitVersionBuildResult buildResult = BuildModel(
				new UnitVersionBuildRequest(
					request.DataFolder + unit.Path,
					request.DataFolder + unit.FuserPath,
					unit.Attribute,
					unit.CurrentVersion,
					candidate.Version,
					group,
					request.IsModuleSelected,
					request.IsBackupData,
					request.IsBaseUpdateInfo,
					request.NotUpdateAttribute,
					unit.Path,
					unit.FuserPath,
					request.AllowMissingUpdateFile),
				cancellationToken);
			preparedItems.Add(new UnitVersionPreparationItem(
				candidate.Name,
				unit.Path,
				candidate.Version,
				unit.Attribute,
				buildResult));
		}
		UnitVersionPreparationItem[] items = preparedItems.ToArray();

		List<int> existingUnitsToDeselect = [];
		List<int> newItemsToDeselect = [];
		if (request.UnifyGroupSelection)
		{
			// 既存行と新規作成行を 1 つの並びとして扱い、グループ単位で未選択を伝播させる
			List<(UnitGroupSelection Selection, int? ExistingIndex, int? ItemIndex)> groupMembers = [];
			for (int index = 0; index < request.ExistingUnits.Count; index++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				groupMembers.Add((request.ExistingUnits[index], index, null));
			}
			for (int index = 0; index < items.Length; index++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				UnitVersionBuildResult buildResult = items[index].BuildResult;
				if (buildResult.Outcome == UnitVersionBuildOutcome.Created)
				{
					groupMembers.Add((
						new UnitGroupSelection(buildResult.UnitGroup, buildResult.IsSelected),
						null,
						index));
				}
			}

			foreach (int groupIndex in GetGroupsToDeselect(
				groupMembers,
				member => member.Selection,
				cancellationToken))
			{
				cancellationToken.ThrowIfCancellationRequested();
				(int? ExistingIndex, int? ItemIndex) member =
					(groupMembers[groupIndex].ExistingIndex, groupMembers[groupIndex].ItemIndex);
				if (member.ExistingIndex.HasValue)
				{
					existingUnitsToDeselect.Add(member.ExistingIndex.Value);
				}
				if (member.ItemIndex.HasValue)
				{
					newItemsToDeselect.Add(member.ItemIndex.Value);
				}
			}
		}

		cancellationToken.ThrowIfCancellationRequested();
		return new(
			selection.SkippedSameVersion,
			items,
			existingUnitsToDeselect,
			newItemsToDeselect)
		{
			SkippedNotOnMachine = selection.SkippedNotOnMachine
		};
	}

	/// <summary>
	///  2. アップデートデータ側と装置側でバージョンが異なるユニットを候補として抽出する。
	///  装置側に存在しないユニットは対象外。ignoreVersion=true なら同一バージョンも候補に含める。
	/// </summary>
	internal UnitVersionSelectionResult SelectCandidates(
		IReadOnlyList<UpdateVersionCandidate> updateVersions,
		IReadOnlyList<MachineUnitVersion> machineVersions,
		bool ignoreVersion,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(updateVersions);
		ArgumentNullException.ThrowIfNull(machineVersions);

		cancellationToken.ThrowIfCancellationRequested();
		Dictionary<string, string> currentVersions = new(StringComparer.Ordinal);
		foreach (MachineUnitVersion unit in machineVersions)
		{
			cancellationToken.ThrowIfCancellationRequested();
			currentVersions.TryAdd(unit.Name, unit.Version);
		}

		return SelectCandidates(updateVersions, currentVersions, ignoreVersion, cancellationToken);
	}

	/// <inheritdoc cref="SelectCandidates(IReadOnlyList{UpdateVersionCandidate}, IReadOnlyList{MachineUnitVersion}, bool, CancellationToken)"/>
	internal UnitVersionSelectionResult SelectCandidates(
		IReadOnlyList<UpdateVersionCandidate> updateVersions,
		IReadOnlyDictionary<string, string> currentVersions,
		bool ignoreVersion,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(updateVersions);
		ArgumentNullException.ThrowIfNull(currentVersions);
		cancellationToken.ThrowIfCancellationRequested();

		List<UpdateVersionCandidate> candidates = [];
		List<UpdateVersionCandidate> skippedSameVersion = [];
		List<UpdateVersionCandidate> skippedNotOnMachine = [];

		foreach (UpdateVersionCandidate update in updateVersions)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (string.IsNullOrEmpty(update.Name))
			{
				continue;
			}

			if (!currentVersions.TryGetValue(update.Name, out string? currentVersion))
			{
				skippedNotOnMachine.Add(update);
				continue;
			}

			if (!ignoreVersion && currentVersion == update.Version)
			{
				skippedSameVersion.Add(update);
				continue;
			}

			candidates.Add(update);
		}

		cancellationToken.ThrowIfCancellationRequested();
		return new(candidates, skippedSameVersion)
		{
			SkippedNotOnMachine = skippedNotOnMachine
		};
	}

	/// <summary>
	///  3. グループ定義を「構成ユニット名 → グループ定義」の辞書にする(同名は先勝ち)。
	/// </summary>
	internal IReadOnlyDictionary<string, UnitVersionGroupDefinition> CreateGroupIndex(
		IReadOnlyList<UnitVersionGroupDefinition> groups,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(groups);
		cancellationToken.ThrowIfCancellationRequested();

		Dictionary<string, UnitVersionGroupDefinition> groupsByComponent =
			new(StringComparer.Ordinal);
		foreach (UnitVersionGroupDefinition group in groups)
		{
			cancellationToken.ThrowIfCancellationRequested();
			foreach (string component in group.Components)
			{
				cancellationToken.ThrowIfCancellationRequested();
				groupsByComponent.TryAdd(component, group);
			}
		}

		cancellationToken.ThrowIfCancellationRequested();
		return groupsByComponent;
	}

	/// <summary>
	///  3. ユニットのグループ名を決定する。機種名の定義を優先し、無ければ "*"(全機種共通)、
	///  どちらも無ければ空文字を返す。
	/// </summary>
	internal string ResolveGroup(
		string unitName,
		string machineType,
		IReadOnlyList<UnitVersionGroupDefinition> groups,
		CancellationToken cancellationToken = default) =>
		ResolveGroup(unitName, machineType, CreateGroupIndex(groups, cancellationToken), cancellationToken);

	/// <inheritdoc cref="ResolveGroup(string, string, IReadOnlyList{UnitVersionGroupDefinition}, CancellationToken)"/>
	internal string ResolveGroup(
		string unitName,
		string machineType,
		IReadOnlyDictionary<string, UnitVersionGroupDefinition> groupsByComponent,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(unitName);
		ArgumentNullException.ThrowIfNull(machineType);
		ArgumentNullException.ThrowIfNull(groupsByComponent);

		cancellationToken.ThrowIfCancellationRequested();
		if (!groupsByComponent.TryGetValue(unitName, out UnitVersionGroupDefinition? group))
		{
			return "";
		}

		cancellationToken.ThrowIfCancellationRequested();
		return group.Names.TryGetValue(machineType, out string? machineGroup)
			? machineGroup
			: group.Names.TryGetValue("*", out string? defaultGroup)
				? defaultGroup
				: "";
	}

	/// <summary>
	///  4. 1 ユニット分の行データを作る。
	///  <list type="bullet">
	///   <item>更新ファイルが無い: AllowMissingUpdateFile=false なら UpdateFileNotFound(除外)、true なら未選択の行を作る</item>
	///   <item>パスがフォルダ: UpdatePathIsDirectory(除外)</item>
	///   <item>Attribute が数値でない: InvalidAttribute(除外)</item>
	///   <item>FuserPath のファイルが無い: FuserPath を空にし ClearFuserPath=true</item>
	///   <item>初期選択: モジュール選択中・禁止でない・新バージョンあり・バックアップでない・ファイルあり・現バージョン判明 の全てを満たす場合</item>
	///   <item>現バージョン: アップデートデータ側の情報を使った場合 "Undefined"、装置側が空なら "N/A"</item>
	///  </list>
	/// </summary>
	internal UnitVersionBuildResult BuildModel(
		UnitVersionBuildRequest request,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentException.ThrowIfNullOrWhiteSpace(request.UpdateFilePath);
		cancellationToken.ThrowIfCancellationRequested();

		bool updateFileExists = System.IO.File.Exists(request.UpdateFilePath);
		cancellationToken.ThrowIfCancellationRequested();
		if (!updateFileExists)
		{
			if (!request.AllowMissingUpdateFile
				|| Directory.Exists(request.UpdateFilePath))
			{
				return new(UnitVersionBuildOutcome.UpdateFileNotFound);
			}
		}
		else
		{
			FileAttributes attributes = System.IO.File.GetAttributes(request.UpdateFilePath);
			cancellationToken.ThrowIfCancellationRequested();
			if ((attributes & FileAttributes.Directory) == FileAttributes.Directory)
			{
				return new(UnitVersionBuildOutcome.UpdatePathIsDirectory);
			}
		}

		if (!int.TryParse(request.Attribute, out int attribute))
		{
			return new(UnitVersionBuildOutcome.InvalidAttribute);
		}
		string updatePath = request.Path ?? "";
		string fuserPath = request.FuserPath ?? "";
		bool fuserFileExists = fuserPath.Length == 0
			|| System.IO.File.Exists(request.FuserFilePath ?? "");
		cancellationToken.ThrowIfCancellationRequested();
		bool clearFuserPath = fuserPath.Length != 0 && !fuserFileExists;
		if (clearFuserPath)
		{
			fuserPath = "";
		}

		string? currentVersion;
		bool isSelected = request.IsSelected
			&& attribute != request.NotUpdateAttribute
			&& request.NewVersion != "N/A"
			&& !request.IsBackupData
			&& updateFileExists;
		if (request.IsBaseUpdateInfo)
		{
			currentVersion = "Undefined";
		}
		else if (string.IsNullOrEmpty(request.MachineVersion))
		{
			currentVersion = "N/A";
			isSelected = false;
		}
		else
		{
			currentVersion = request.MachineVersion;
		}

		long size = updateFileExists ? new FileInfo(request.UpdateFilePath).Length : 0;
		cancellationToken.ThrowIfCancellationRequested();
		return new(
			UnitVersionBuildOutcome.Created,
			attribute,
			updatePath,
			fuserPath,
			clearFuserPath,
			currentVersion,
			request.NewVersion ?? "N/A",
			request.UnitGroup ?? "",
			isSelected,
			size,
			!updateFileExists);
	}

	/// <summary>
	///  5. 未選択(false)のユニットを含むグループについて、そのグループの全ユニットのインデックスを返す。
	///  グループは出現順、グループ内はインデックス順。GroupName=null も 1 つのグループとして扱う。
	/// </summary>
	internal IReadOnlyList<int> GetGroupsToDeselect(
		IReadOnlyList<UnitGroupSelection> units,
		CancellationToken cancellationToken = default) =>
		GetGroupsToDeselect(units, static unit => unit, cancellationToken);

	/// <inheritdoc cref="GetGroupsToDeselect(IReadOnlyList{UnitGroupSelection}, CancellationToken)"/>
	internal IReadOnlyList<int> GetGroupsToDeselect<TUnit>(
		IReadOnlyList<TUnit> units,
		Func<TUnit, UnitGroupSelection> getSelection,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(units);
		ArgumentNullException.ThrowIfNull(getSelection);

		cancellationToken.ThrowIfCancellationRequested();
		Dictionary<GroupKey, GroupMembers> groups = [];
		List<GroupKey> groupOrder = [];
		for (int index = 0; index < units.Count; index++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			UnitGroupSelection unit = getSelection(units[index]);
			GroupKey groupKey = new(unit.GroupName);
			if (!groups.TryGetValue(groupKey, out GroupMembers? members))
			{
				members = new GroupMembers();
				groups.Add(groupKey, members);
				groupOrder.Add(groupKey);
			}

			members.Indices.Add(index);
			members.HasDeselectedUnit |= unit.IsSelected == false;
		}

		List<int> deselect = [];
		foreach (GroupKey groupKey in groupOrder)
		{
			cancellationToken.ThrowIfCancellationRequested();
			GroupMembers members = groups[groupKey];
			if (!members.HasDeselectedUnit)
			{
				continue;
			}

			foreach (int index in members.Indices)
			{
				cancellationToken.ThrowIfCancellationRequested();
				deselect.Add(index);
			}
		}

		cancellationToken.ThrowIfCancellationRequested();
		return deselect;
	}

	// null のグループ名も辞書キーにするためのラッパー
	private readonly record struct GroupKey(string? Name);

	private sealed class GroupMembers
	{
		public List<int> Indices { get; } = [];

		public bool HasDeselectedUnit { get; set; }
	}

	#endregion

	#region 6. 結果の振り分け

	/// <summary>
	///  6. 準備結果を「作成する行」「作成しない行」「FuserPath をクリアするユニット」に振り分ける。
	///  グループ選択統一で未選択にする行は IsSelected=false にする。
	/// </summary>
	internal UnitVersionPreparationProjection Project(
		UnitVersionPreparationResult preparation,
		IReadOnlyDictionary<string, string> originalFuserPaths,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(preparation);
		ArgumentNullException.ThrowIfNull(originalFuserPaths);
		cancellationToken.ThrowIfCancellationRequested();

		HashSet<int> itemsToDeselect = [];
		foreach (int index in preparation.NewItemsToDeselect)
		{
			cancellationToken.ThrowIfCancellationRequested();
			itemsToDeselect.Add(index);
		}

		List<UnitVersionPreparationItem> itemsNotCreated = [];
		List<UnitVersionPreparedDefinition> itemsToCreate = [];
		List<(string Name, string? FuserPath)> fuserPathsToClear = [];
		for (int index = 0; index < preparation.Items.Count; index++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			UnitVersionPreparationItem item = preparation.Items[index];
			UnitVersionBuildResult result = item.BuildResult;
			switch (result.Outcome)
			{
				case UnitVersionBuildOutcome.UpdateFileNotFound:
				case UnitVersionBuildOutcome.UpdatePathIsDirectory:
				case UnitVersionBuildOutcome.InvalidAttribute:
					itemsNotCreated.Add(item);
					continue;
				case UnitVersionBuildOutcome.Created:
					break;
				default:
					throw new ArgumentOutOfRangeException(
						nameof(item.BuildResult.Outcome),
						result.Outcome,
						"Unknown unit version build outcome.");
			}

			if (result.ClearFuserPath)
			{
				fuserPathsToClear.Add((
					item.UnitName,
					originalFuserPaths.GetValueOrDefault(item.UnitName, "")));
			}

			itemsToCreate.Add(new UnitVersionPreparedDefinition(
				item.UnitName,
				result.IsSelected && !itemsToDeselect.Contains(index),
				result.Attribute,
				result.Path,
				result.FuserPath,
				result.CurrentVersion,
				result.NewVersion,
				result.Size,
				result.UnitGroup,
				result.IsUpdateFileMissing));
		}

		cancellationToken.ThrowIfCancellationRequested();
		return new(
			preparation.SkippedSameVersion,
			itemsNotCreated,
			itemsToCreate,
			fuserPathsToClear,
			preparation.ExistingUnitsToDeselect)
		{
			SkippedNotOnMachine = preparation.SkippedNotOnMachine
		};
	}

	#endregion

	#region 7. ログメッセージ

	/// <summary>
	///  7. ログメッセージを作成する。
	///  <list type="bullet">
	///   <item>装置側に無い/同一バージョン     : アプリログ [INFO]</item>
	///   <item>更新ファイル無しで除外/表示のみ : アプリログ [WARNING]</item>
	///   <item>Attribute 不正                 : アプリログ [ERROR]</item>
	///   <item>FuserPath のファイル無し       : デバッグ出力(IniUpdate フェーズ)</item>
	///  </list>
	/// </summary>
	internal IReadOnlyList<UnitVersionPreparationDiagnostic> CreateDiagnostics(
		UnitVersionPreparationProjection projection,
		string lcuName,
		string machineName,
		string moduleName,
		CancellationToken cancellationToken = default,
		bool missingUpdateFileSelectable = false)
	{
		ArgumentNullException.ThrowIfNull(projection);
		ArgumentNullException.ThrowIfNull(lcuName);
		ArgumentNullException.ThrowIfNull(machineName);
		ArgumentNullException.ThrowIfNull(moduleName);
		cancellationToken.ThrowIfCancellationRequested();

		List<UnitVersionPreparationDiagnostic> diagnostics = [];
		foreach (UpdateVersionCandidate skipped in projection.SkippedNotOnMachine)
		{
			cancellationToken.ThrowIfCancellationRequested();
			diagnostics.Add(new(
				UnitVersionPreparationDiagnosticPhase.Preparation,
				UnitVersionDiagnosticDestination.ApplicationLog,
				$"[INFO] {lcuName};{machineName};{moduleName}:{skipped.Name}=skip (not in machine UpdateCommon.inf; not required)"));
		}

		foreach (UpdateVersionCandidate skipped in projection.SkippedSameVersion)
		{
			cancellationToken.ThrowIfCancellationRequested();
			diagnostics.Add(new(
				UnitVersionPreparationDiagnosticPhase.Preparation,
				UnitVersionDiagnosticDestination.ApplicationLog,
				$"[INFO] {lcuName};{machineName};{moduleName}:{skipped.Name}=sameVersion"));
		}

		foreach (UnitVersionPreparationItem item in projection.ItemsNotCreated)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (item.BuildResult.Outcome == UnitVersionBuildOutcome.UpdateFileNotFound)
			{
				diagnostics.Add(new(
					UnitVersionPreparationDiagnosticPhase.Preparation,
					UnitVersionDiagnosticDestination.ApplicationLog,
					$"[WARNING] {lcuName};{machineName};{moduleName}:{item.UnitName}={item.SourcePath} Update file not found (excluded)"));
			}
			else if (item.BuildResult.Outcome == UnitVersionBuildOutcome.InvalidAttribute)
			{
				diagnostics.Add(new(
					UnitVersionPreparationDiagnosticPhase.Preparation,
					UnitVersionDiagnosticDestination.ApplicationLog,
					$"[ERROR] {moduleName}:{item.UnitName} has invalid Attribute={item.Attribute}"));
			}
		}

		foreach (UnitVersionPreparedDefinition definition in projection.ItemsToCreate)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (definition.IsUpdateFileMissing)
			{
				diagnostics.Add(new(
					UnitVersionPreparationDiagnosticPhase.Preparation,
					UnitVersionDiagnosticDestination.ApplicationLog,
					$"[WARNING] {lcuName};{machineName};{moduleName}:{definition.Name}={definition.Path} Update file not found ({(missingUpdateFileSelectable ? "test: selectable" : "not selectable")})"));
			}
		}

		foreach ((string unitName, string? fuserPath) in projection.FuserPathsToClear)
		{
			cancellationToken.ThrowIfCancellationRequested();
			diagnostics.Add(new(
				UnitVersionPreparationDiagnosticPhase.IniUpdate,
				UnitVersionDiagnosticDestination.Debug,
				$"[UnitVersion] {moduleName}:{unitName}={fuserPath} FuserPath Not Found"));
		}

		cancellationToken.ThrowIfCancellationRequested();
		return diagnostics;
	}

	#endregion
}
