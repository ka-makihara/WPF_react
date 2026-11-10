namespace WpfApp1_cmd.Services;

public enum InspectionDiagnosticDestination
{
	ApplicationLog,
	Debug
}

public sealed record InspectionDiagnostic(
	InspectionDiagnosticDestination Destination,
	string Message,
	bool IsError);

public sealed record ModuleInspectionDiagnostics(
	IReadOnlyList<InspectionDiagnostic> Entries,
	bool AllFilesMatch);

public sealed class UpdateInspectionDiagnosticService
{
	public IReadOnlyList<InspectionDiagnostic> CreateTransferredFilePresenceDiagnostics(
		IReadOnlyList<TransferredFilePresence> results)
	{
		ArgumentNullException.ThrowIfNull(results);

		List<InspectionDiagnostic> diagnostics = new(results.Count);
		foreach (TransferredFilePresence result in results)
		{
			string presence = result.Exists ? "is found in LCU." : "is not found in LCU.";
			string message = result.IsFuserPath
				? $"[INSPECTION] {result.Path} {presence}"
				: $"[INSPECTION] {result.UnitName}={result.Path} {presence}";
			diagnostics.Add(new(
				InspectionDiagnosticDestination.Debug,
				message,
				false));
		}

		return diagnostics;
	}

	public ModuleInspectionDiagnostics CreateModuleDiagnostics(
		string lcuName,
		string machineName,
		string moduleName,
		IReadOnlyList<ModuleFileInspectionResult> results,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(lcuName);
		ArgumentNullException.ThrowIfNull(machineName);
		ArgumentNullException.ThrowIfNull(moduleName);
		ArgumentNullException.ThrowIfNull(results);
		cancellationToken.ThrowIfCancellationRequested();

		List<InspectionDiagnostic> diagnostics = new(results.Count);
		bool allFilesMatch = true;
		foreach (ModuleFileInspectionResult result in results)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (!result.IsMatch)
			{
				diagnostics.Add(new(
					InspectionDiagnosticDestination.ApplicationLog,
					$"[INSPECTION] {lcuName};{machineName};{moduleName};{result.UnitName}=NG:DiffFile Inspection Error({result.FilePath})",
					true));
				allFilesMatch = false;
			}
			else
			{
				diagnostics.Add(new(
					InspectionDiagnosticDestination.ApplicationLog,
					$"[INSPECTION] {lcuName};{machineName};{moduleName};{result.UnitName};{result.FilePath}=OK:Inspection Success",
					false));
			}
		}

		cancellationToken.ThrowIfCancellationRequested();
		return new(diagnostics, allFilesMatch);
	}

	public IReadOnlyList<InspectionDiagnostic> CreateUpdateCommonDiagnostics(
		UpdateCommonVersionInspectionResult inspection,
		string updateCommonFile,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(inspection);
		ArgumentNullException.ThrowIfNull(updateCommonFile);
		cancellationToken.ThrowIfCancellationRequested();

		List<InspectionDiagnostic> diagnostics = [];
		if (!inspection.FileExists)
		{
			diagnostics.Add(new(
				InspectionDiagnosticDestination.ApplicationLog,
				$"[ERROR] UpdateCommon.inf is not exists: {updateCommonFile}",
				true));
			return diagnostics;
		}

		if (inspection.ReadError != null)
		{
			diagnostics.Add(new(
				InspectionDiagnosticDestination.ApplicationLog,
				$"iniファイル読み込みエラー: {inspection.ReadError}",
				true));
			return diagnostics;
		}

		foreach (UpdateCommonVersionEntry entry in inspection.Entries)
		{
			cancellationToken.ThrowIfCancellationRequested();
			diagnostics.Add(new(
				InspectionDiagnosticDestination.Debug,
				$"Section={entry.Section}, Version={entry.Version}",
				false));
			switch (entry.Status)
			{
				case UpdateCommonVersionStatus.Mismatched:
					diagnostics.Add(new(
						InspectionDiagnosticDestination.ApplicationLog,
						$"[ERROR] {entry.Section} version mismatch: {entry.CurrentVersion} != {entry.Version}",
						true));
					break;
				case UpdateCommonVersionStatus.Matched:
					diagnostics.Add(new(
						InspectionDiagnosticDestination.ApplicationLog,
						$"[INFO] {entry.Section}=sameVersion ({entry.CurrentVersion} == {entry.Version})",
						false));
					break;
				case UpdateCommonVersionStatus.UnitNotFound:
					diagnostics.Add(new(
						InspectionDiagnosticDestination.ApplicationLog,
						$"[INFO] {entry.Section}=skip (not in inspection targets)",
						false));
					break;
				default:
					throw new ArgumentOutOfRangeException(
						nameof(entry.Status),
						entry.Status,
						"Unknown UpdateCommon version status.");
			}
		}

		cancellationToken.ThrowIfCancellationRequested();
		return diagnostics;
	}
}
