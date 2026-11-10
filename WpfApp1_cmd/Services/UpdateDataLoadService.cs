using System.IO;
using IniFileParser = WpfLcuCtrlLib.IniFileParser;

namespace WpfApp1_cmd.Services;

// =====================================================================
//  アップデートデータ読み込みサービス
//
//  アップデートデータ側の UpdateCommon.inf を読み込み、画面表示用のデータを作る。
//
//  処理の流れ(Load):
//    1. BuildPathGroupDefinitions: unitLink.json(Config.units)をグループ定義に変換
//    2. BuildUnitGroupLookup     : 構成ユニット名 → グループ名 の辞書を作る
//    3. ReadUpdateCommon         : UpdateCommon.inf の各セクション(ユニット)を読み、
//                                  参照ファイルの欠落確認とデータサイズ算出を行う
//    4. BuildPathGroups          : グループ単位の転送ファイル一覧(UpdatePaths)を作る
//  結果の反映:
//    - CreateLoadPresentation    : 読み込み結果からログ/エラーコードを決める
//    - CreateUpdateInfoProjections / CreatePathGroupProjections: 画面モデル用に変換
//  ユーティリティ:
//    - GetDirectorySize / GetPeripheralSize: アップデートデータのサイズ算出
// =====================================================================

#region 入出力

/// <summary>UpdateCommon.inf の 1 セクション(ユニット)</summary>
public sealed record UpdateCommonInfoEntry(
	string Name,
	string Attribute,
	string Path,
	string Version,
	string FuserPath,
	string? UnitGroup);

/// <summary>UpdateCommon.inf の 1 セクションのパス情報(フォルダ指定のセクションも含む)</summary>
public sealed record UpdateCommonPathEntry(
	string Name,
	string Path,
	string FuserPath);

/// <summary>
///  UpdateCommon.inf の読み込み結果
/// </summary>
/// <param name="Entries">ユニット一覧(Path がフォルダのセクションは除く)</param>
/// <param name="DataSize">アップデートデータのサイズ</param>
/// <param name="ErrorMessage">読み込みエラー(null なら成功)</param>
/// <param name="PathEntries">全セクションのパス情報</param>
/// <param name="MissingFiles">Path に指定されているが存在しないファイル</param>
public sealed record UpdateCommonInfoReadResult(
	IReadOnlyList<UpdateCommonInfoEntry> Entries,
	long DataSize,
	string? ErrorMessage,
	IReadOnlyList<UpdateCommonPathEntry> PathEntries,
	IReadOnlyList<string> MissingFiles);

/// <summary>unitLink.json の 1 定義(スナップショット)</summary>
public sealed record UpdatePathGroupInput(
	IReadOnlyList<string> Components,
	IReadOnlyList<KeyValuePair<string, string>> Names,
	string? Mode);

/// <summary>
///  転送ファイルのグループ定義
/// </summary>
/// <param name="Components">グループを構成するユニット名</param>
/// <param name="GroupName">グループ名(Names の "*" の値)</param>
/// <param name="GroupNames">機種別を含む全グループ名(同名グループの統合に使用)</param>
/// <param name="Mode">"File"=Path のファイルのみ / "Folder"=Path のフォルダ配下全て</param>
public sealed record UpdatePathGroupDefinition(
	IReadOnlyList<string> Components,
	string GroupName,
	IReadOnlyList<string> GroupNames,
	string? Mode);

public sealed record UpdatePathGroup(string GroupName, IReadOnlyList<UpdatePathFile> Files);

public sealed record UpdatePathFile(string? Name, string? Path, string? FusePath);

/// <summary>
///  アップデートデータ読み込み結果
/// </summary>
/// <param name="UpdateCommonExists">UpdateCommon.inf が存在したか</param>
/// <param name="Entries">ユニット一覧</param>
/// <param name="DataSize">アップデートデータのサイズ(読み込み失敗時は null)</param>
/// <param name="ReadError">読み込みエラー</param>
/// <param name="PathGroups">グループ単位の転送ファイル一覧</param>
/// <param name="MissingFiles">存在しない参照ファイル</param>
public sealed record UpdateDataLoadResult(
	bool UpdateCommonExists,
	IReadOnlyList<UpdateCommonInfoEntry> Entries,
	long? DataSize,
	string? ReadError,
	IReadOnlyList<UpdatePathGroup> PathGroups,
	IReadOnlyList<string> MissingFiles);

public enum UpdateDataLoadPresentationOutcome
{
	Loaded,
	ReadFailed,
	UpdateCommonMissing
}

/// <summary>読み込み結果を画面/ログへ反映するための情報</summary>
public sealed record UpdateDataLoadPresentation(
	UpdateDataLoadPresentationOutcome Outcome,
	long? DataSize,
	ErrorCode? ErrorCode,
	string? LogMessage);

/// <summary>UpdateInfo(画面モデル)作成用のデータ</summary>
public sealed record UpdateInfoProjection(
	string Name,
	string Attribute,
	string Path,
	string Version,
	string FuserPath,
	string? UnitGroup,
	bool IsVisible);

/// <summary>UnitPath(画面モデル)作成用のファイル</summary>
public sealed record UpdatePathFileProjection(
	string? Name,
	string Path,
	string FusePath);

/// <summary>UnitPath(画面モデル)作成用のグループ</summary>
public sealed record UpdatePathGroupProjection(
	string GroupName,
	IReadOnlyList<UpdatePathFileProjection> Files);

#endregion

/// <summary>
///  アップデートデータの UpdateCommon.inf を読み込み、ユニット一覧・転送ファイル一覧を作る。
///  処理の流れはファイル先頭のコメントを参照。
/// </summary>
public sealed class UpdateDataLoadService
{
	/// <summary>
	///  アップデートデータを読み込む(UpdateCommon.inf 読み込み → 転送ファイル一覧作成)。
	///  UpdateCommon.inf が無い場合は UpdateCommonExists=false を返す。
	/// </summary>
	public UpdateDataLoadResult Load(
		string updateCommonPath,
		string dataFolder,
		IReadOnlyList<UpdatePathGroupDefinition> unitLinks,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(updateCommonPath);
		ArgumentException.ThrowIfNullOrWhiteSpace(dataFolder);
		ArgumentNullException.ThrowIfNull(unitLinks);
		cancellationToken.ThrowIfCancellationRequested();

		if (!File.Exists(updateCommonPath))
		{
			return new(false, [], null, null, [], []);
		}

		IReadOnlyDictionary<string, string> unitGroupsByComponent =
			BuildUnitGroupLookup(unitLinks, cancellationToken);

		UpdateCommonInfoReadResult info = ReadUpdateCommon(
			updateCommonPath,
			dataFolder,
			unit => unitGroupsByComponent.GetValueOrDefault(unit),
			cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		if (info.ErrorMessage != null)
		{
			return new(
				true,
				info.Entries,
				null,
				info.ErrorMessage,
				[],
				info.MissingFiles);
		}

		IReadOnlyList<UpdatePathGroup> pathGroups = BuildPathGroups(
			info.PathEntries,
			dataFolder,
			unitLinks,
			cancellationToken);

		return new(
			true,
			info.Entries,
			info.DataSize,
			null,
			pathGroups,
			info.MissingFiles);
	}

	/// <summary>
	///  読み込み結果から、画面に反映するサイズ・エラーコード・ログメッセージを決める。
	///  <list type="bullet">
	///   <item>UpdateCommon.inf 無し: UPDATE_UNDEFINED_ERROR</item>
	///   <item>読み込みエラー     : UPDATE_READ_ERROR</item>
	///   <item>成功               : "Read {path}" をログ出力</item>
	///  </list>
	/// </summary>
	public UpdateDataLoadPresentation CreateLoadPresentation(
		UpdateDataLoadResult result,
		string updateCommonPath,
		string missingUpdateCommonMessage)
	{
		ArgumentNullException.ThrowIfNull(result);
		ArgumentNullException.ThrowIfNull(updateCommonPath);
		ArgumentNullException.ThrowIfNull(missingUpdateCommonMessage);

		if (!result.UpdateCommonExists)
		{
			return new(
				UpdateDataLoadPresentationOutcome.UpdateCommonMissing,
				null,
				WpfApp1_cmd.ErrorCode.UPDATE_UNDEFINED_ERROR,
				missingUpdateCommonMessage);
		}

		if (result.ReadError != null)
		{
			return new(
				UpdateDataLoadPresentationOutcome.ReadFailed,
				null,
				WpfApp1_cmd.ErrorCode.UPDATE_READ_ERROR,
				$"Read Error:{result.ReadError}");
		}

		return new(
			UpdateDataLoadPresentationOutcome.Loaded,
			result.DataSize,
			null,
			$"Read {updateCommonPath}");
	}

	#region 1〜2. グループ定義

	/// <summary>
	///  1. unitLink.json の定義をグループ定義に変換する。
	///  グループ名は Names の "*"(全機種共通)の値、無ければ空文字。
	/// </summary>
	public IReadOnlyList<UpdatePathGroupDefinition> BuildPathGroupDefinitions(
		IReadOnlyList<UpdatePathGroupInput> inputs,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(inputs);
		cancellationToken.ThrowIfCancellationRequested();

		UpdatePathGroupDefinition[] definitions = new UpdatePathGroupDefinition[inputs.Count];
		for (int index = 0; index < inputs.Count; index++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			UpdatePathGroupInput input = inputs[index];
			string[] components = new string[input.Components.Count];
			for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				components[componentIndex] = input.Components[componentIndex];
			}

			string[] names = new string[input.Names.Count];
			string groupName = "";
			for (int nameIndex = 0; nameIndex < input.Names.Count; nameIndex++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				KeyValuePair<string, string> entry = input.Names[nameIndex];
				names[nameIndex] = entry.Value;
				if (entry.Key == "*")
				{
					groupName = entry.Value;
				}
			}

			definitions[index] = new UpdatePathGroupDefinition(
				components,
				groupName,
				names,
				input.Mode);
		}

		cancellationToken.ThrowIfCancellationRequested();
		return definitions;
	}

	/// <summary>
	///  2. 「構成ユニット名 → グループ名」の辞書を作る(同名ユニットは先勝ち)。
	/// </summary>
	public IReadOnlyDictionary<string, string> BuildUnitGroupLookup(
		IReadOnlyList<UpdatePathGroupDefinition> definitions,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(definitions);
		cancellationToken.ThrowIfCancellationRequested();

		Dictionary<string, string> groupsByComponent = new(StringComparer.Ordinal);
		foreach (UpdatePathGroupDefinition definition in definitions)
		{
			cancellationToken.ThrowIfCancellationRequested();
			foreach (string component in definition.Components)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (component != null)
				{
					groupsByComponent.TryAdd(component, definition.GroupName);
				}
			}
		}

		cancellationToken.ThrowIfCancellationRequested();
		return groupsByComponent;
	}

	#endregion

	#region 3. UpdateCommon.inf の読み込み

	/// <summary>
	///  3. UpdateCommon.inf を読み込む。
	///  <list type="bullet">
	///   <item>Path がフォルダのセクションはユニット一覧に含めない(PathEntries には含める)</item>
	///   <item>Path のファイルが無い場合は MissingFiles に追加する(ユニット一覧には含める)</item>
	///   <item>データサイズは Peripheral.bin があればそのサイズ、無ければフォルダ全体のサイズ</item>
	///   <item>IO エラー等は例外にせず ErrorMessage に設定する</item>
	///  </list>
	/// </summary>
	/// <param name="resolveUnitGroup">ユニット名からグループ名を求める関数</param>
	public UpdateCommonInfoReadResult ReadUpdateCommon(
		string updateCommonPath,
		string dataFolder,
		Func<string, string?> resolveUnitGroup,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(updateCommonPath);
		ArgumentException.ThrowIfNullOrWhiteSpace(dataFolder);
		ArgumentNullException.ThrowIfNull(resolveUnitGroup);

		List<UpdateCommonInfoEntry> entries = [];
		List<string> missingFiles = [];
		try
		{
			cancellationToken.ThrowIfCancellationRequested();
			IniFileParser parser = new(updateCommonPath);
			List<UpdateCommonPathEntry> pathEntries = [];
			foreach (string section in parser.SectionCount())
			{
				cancellationToken.ThrowIfCancellationRequested();
				string relativePath = parser.GetValue(section, "Path");
				string fuserPath = parser.GetValue(section, "FuserPath");
				pathEntries.Add(new UpdateCommonPathEntry(section, relativePath, fuserPath));
				string fullPath = dataFolder + relativePath;
				if (Directory.Exists(fullPath))
				{
					continue;
				}
				if (!string.IsNullOrEmpty(relativePath) && !File.Exists(fullPath))
				{
					missingFiles.Add(fullPath);
				}

				entries.Add(new UpdateCommonInfoEntry(
					section,
					parser.GetValue(section, "Attribute"),
					relativePath,
					parser.GetValue(section, "Version"),
					fuserPath,
					resolveUnitGroup(section)));
			}

			return new UpdateCommonInfoReadResult(
				entries,
				GetPeripheralSize(dataFolder, cancellationToken),
				null,
				pathEntries,
				missingFiles);
		}
		catch (Exception exception) when (
			exception is IOException or UnauthorizedAccessException or ArgumentException)
		{
			return new UpdateCommonInfoReadResult([], 0, exception.Message, [], []);
		}
	}

	#endregion

	#region 4. 転送ファイル一覧(グループ単位)

	/// <summary>
	///  4. 指定した UpdateCommon.inf を読み込み、グループ単位の転送ファイル一覧を作る。
	/// </summary>
	public IReadOnlyList<UpdatePathGroup> BuildPathGroups(
		string updateCommonPath,
		string dataFolder,
		IReadOnlyList<UpdatePathGroupDefinition> unitLinks,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(updateCommonPath);
		ArgumentException.ThrowIfNullOrWhiteSpace(dataFolder);
		ArgumentNullException.ThrowIfNull(unitLinks);
		cancellationToken.ThrowIfCancellationRequested();

		IniFileParser parser = new(updateCommonPath);
		List<UpdateCommonPathEntry> pathEntries = [];
		foreach ((string name, string path, string fuserPath) in parser.GetPathList())
		{
			cancellationToken.ThrowIfCancellationRequested();
			pathEntries.Add(new UpdateCommonPathEntry(name, path, fuserPath));
		}

		return BuildPathGroups(pathEntries, dataFolder, unitLinks, cancellationToken);
	}

	/// <summary>
	///  4. パス情報からグループ単位の転送ファイル一覧を作る。
	///  <list type="bullet">
	///   <item>ファイルが存在しない/フォルダ、またはグループ定義に無いユニットは対象外</item>
	///   <item>GroupNames のいずれかが既存グループと一致すれば、そのグループにまとめる</item>
	///   <item>Mode="File": Path のファイルのみ追加</item>
	///   <item>Mode="Folder": Path のフォルダ配下の全ファイルを追加
	///         (UpdateCommon.inf にあるファイルはそのセクション名、無いファイルはグループ名で追加)</item>
	///   <item>同じ (名前, パス) は 1 グループ内で重複させない</item>
	///  </list>
	/// </summary>
	public IReadOnlyList<UpdatePathGroup> BuildPathGroups(
		IReadOnlyList<UpdateCommonPathEntry> pathEntries,
		string dataFolder,
		IReadOnlyList<UpdatePathGroupDefinition> unitLinks,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(pathEntries);
		ArgumentException.ThrowIfNullOrWhiteSpace(dataFolder);
		ArgumentNullException.ThrowIfNull(unitLinks);
		cancellationToken.ThrowIfCancellationRequested();

		Dictionary<string, UpdatePathGroupDefinition> unitLinksByComponent =
			new(StringComparer.Ordinal);
		foreach (UpdatePathGroupDefinition unitLink in unitLinks)
		{
			cancellationToken.ThrowIfCancellationRequested();
			foreach (string component in unitLink.Components)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (component != null)
				{
					unitLinksByComponent.TryAdd(component, unitLink);
				}
			}
		}

		// Folder モードで見つけたファイルを、UpdateCommon.inf のセクション名に戻すための索引
		Dictionary<string, List<string>> sectionsByPath =
			new(StringComparer.OrdinalIgnoreCase);
		foreach (UpdateCommonPathEntry entry in pathEntries)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (entry.Path.Length == 0)
			{
				continue;
			}

			if (!sectionsByPath.TryGetValue(entry.Path, out List<string>? sections))
			{
				sections = [];
				sectionsByPath.Add(entry.Path, sections);
			}
			sections.Add(entry.Name);
		}

		List<(string GroupName, IReadOnlyList<string> GroupNames, List<UpdatePathFile> Files)> groups = [];
		Dictionary<string, int> groupIndexesByName = new(StringComparer.OrdinalIgnoreCase);
		Dictionary<int, HashSet<(string? Name, string? Path)>> addedFilesByGroup = [];
		Dictionary<string, string[]> folderFilesByDirectory = new(StringComparer.OrdinalIgnoreCase);
		foreach (UpdateCommonPathEntry entry in pathEntries)
		{
			cancellationToken.ThrowIfCancellationRequested();
			string name = entry.Name;
			string path = entry.Path;
			string fuserPath = entry.FuserPath;
			if (string.IsNullOrEmpty(path))
			{
				continue;
			}

			string fullPath = dataFolder + path;
			if (!Path.Exists(fullPath) || Directory.Exists(fullPath))
			{
				continue;
			}

			if (!unitLinksByComponent.TryGetValue(name, out UpdatePathGroupDefinition? unitLink))
			{
				continue;
			}

			string groupName = unitLink.GroupName;
			int groupIndex = int.MaxValue;
			foreach (string candidateGroupName in unitLink.GroupNames)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (candidateGroupName == null)
				{
					continue;
				}

				if (groupIndexesByName.TryGetValue(candidateGroupName, out int candidateIndex))
				{
					groupIndex = Math.Min(groupIndex, candidateIndex);
				}
			}

			if (groupIndex == int.MaxValue)
			{
				groupIndex = groups.Count;
				groups.Add((groupName, unitLink.GroupNames, []));
				addedFilesByGroup.Add(groupIndex, []);
				if (groupName != null)
				{
					groupIndexesByName.TryAdd(groupName, groupIndex);
				}
			}
			List<UpdatePathFile> groupFiles = groups[groupIndex].Files;
			HashSet<(string? Name, string? Path)> addedFiles = addedFilesByGroup[groupIndex];

			if (unitLink.Mode == "File")
			{
				AddUnique(groupFiles, addedFiles, new UpdatePathFile(name, path, fuserPath));
			}
			else if (unitLink.Mode == "Folder")
			{
				string directory = dataFolder + Path.GetDirectoryName(path);
				if (!folderFilesByDirectory.TryGetValue(directory, out string[]? folderFiles))
				{
					folderFiles = EnumerateFiles(directory, cancellationToken).ToArray();
					folderFilesByDirectory.Add(directory, folderFiles);
				}

				foreach (string file in folderFiles)
				{
					cancellationToken.ThrowIfCancellationRequested();
					string relativePath = "/" + Path.GetRelativePath(dataFolder, file).Replace('\\', '/');
					if (sectionsByPath.TryGetValue(relativePath, out List<string>? matchingSections))
					{
						foreach (string section in matchingSections)
						{
							cancellationToken.ThrowIfCancellationRequested();
							AddUnique(
								groupFiles,
								addedFiles,
								new UpdatePathFile(section, relativePath, ""));
						}
					}
					else
					{
						AddUnique(
							groupFiles,
							addedFiles,
							new UpdatePathFile(groupName, relativePath, ""));
					}
				}
			}
		}

		UpdatePathGroup[] result = new UpdatePathGroup[groups.Count];
		for (int index = 0; index < groups.Count; index++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			(string groupName, _, List<UpdatePathFile> files) = groups[index];
			result[index] = new UpdatePathGroup(groupName, files);
		}

		cancellationToken.ThrowIfCancellationRequested();
		return result;
	}

	private static IEnumerable<string> EnumerateFiles(
		string directory,
		CancellationToken cancellationToken)
	{
		foreach (string file in Directory.EnumerateFiles(
			directory,
			"*",
			SearchOption.AllDirectories))
		{
			cancellationToken.ThrowIfCancellationRequested();
			yield return file;
		}
	}

	private static void AddUnique(
		List<UpdatePathFile> files,
		HashSet<(string? Name, string? Path)> addedFiles,
		UpdatePathFile file)
	{
		if (addedFiles.Add((file.Name, file.Path)))
		{
			files.Add(file);
		}
	}

	#endregion

	#region 画面モデル用への変換

	/// <summary>
	///  ユニット一覧を UpdateInfo(画面モデル)作成用データに変換する(全て表示対象)。
	/// </summary>
	public IReadOnlyList<UpdateInfoProjection> CreateUpdateInfoProjections(
		IReadOnlyList<UpdateCommonInfoEntry> entries,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(entries);
		cancellationToken.ThrowIfCancellationRequested();

		UpdateInfoProjection[] result = new UpdateInfoProjection[entries.Count];
		for (int index = 0; index < result.Length; index++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			UpdateCommonInfoEntry entry = entries[index];
			result[index] = new(
				entry.Name,
				entry.Attribute,
				entry.Path,
				entry.Version,
				entry.FuserPath,
				entry.UnitGroup,
				true);
		}

		cancellationToken.ThrowIfCancellationRequested();
		return result;
	}

	/// <summary>
	///  転送ファイル一覧を UnitPath(画面モデル)作成用データに変換する。
	///  null パスは空文字にし、重複ファイルと空グループは除く。
	/// </summary>
	public IReadOnlyList<UpdatePathGroupProjection> CreatePathGroupProjections(
		IReadOnlyList<UpdatePathGroup> groups,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(groups);
		cancellationToken.ThrowIfCancellationRequested();

		List<UpdatePathGroupProjection> result = new(groups.Count);
		foreach (UpdatePathGroup group in groups)
		{
			cancellationToken.ThrowIfCancellationRequested();
			List<UpdatePathFileProjection> files = new(group.Files.Count);
			HashSet<(string? Name, string Path)> existingFiles = [];
			foreach (UpdatePathFile file in group.Files)
			{
				cancellationToken.ThrowIfCancellationRequested();
				string path = file.Path ?? "";
				if (!existingFiles.Add((file.Name, path)))
				{
					continue;
				}

				files.Add(new(file.Name, path, file.FusePath ?? ""));
			}

			if (files.Count > 0)
			{
				result.Add(new(group.GroupName, files));
			}
		}

		cancellationToken.ThrowIfCancellationRequested();
		return result;
	}

	#endregion

	#region サイズ算出

	/// <summary>
	///  フォルダ配下(サブフォルダ含む)の全ファイルサイズの合計を返す。
	/// </summary>
	public static long GetDirectorySize(
		DirectoryInfo directory,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(directory);
		cancellationToken.ThrowIfCancellationRequested();

		long size = 0;
		Stack<DirectoryInfo> pendingDirectories = new();
		pendingDirectories.Push(directory);
		while (pendingDirectories.Count > 0)
		{
			cancellationToken.ThrowIfCancellationRequested();
			DirectoryInfo currentDirectory = pendingDirectories.Pop();
			foreach (FileInfo file in currentDirectory.EnumerateFiles())
			{
				cancellationToken.ThrowIfCancellationRequested();
				size += file.Length;
			}

			foreach (DirectoryInfo subdirectory in currentDirectory.EnumerateDirectories())
			{
				cancellationToken.ThrowIfCancellationRequested();
				pendingDirectories.Push(subdirectory);
			}
		}

		return size;
	}

	/// <summary>
	///  アップデートデータのサイズを返す。
	///  フォルダ直下に Peripheral.bin があればそのサイズ、無ければフォルダ全体のサイズ。
	/// </summary>
	public static long GetPeripheralSize(
		string path,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		cancellationToken.ThrowIfCancellationRequested();

		DirectoryInfo directory = new(path);
		foreach (FileInfo peripheralFile in directory.EnumerateFiles(UpdateDataSourceService.ArchiveFileName))
		{
			cancellationToken.ThrowIfCancellationRequested();
			return peripheralFile.Length;
		}

		return GetDirectorySize(directory, cancellationToken);
	}

	#endregion
}
