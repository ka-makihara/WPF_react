using System.IO;
using System.Net.Http;
using System.Xml.Serialization;

namespace WpfApp1_cmd.Services;

// =============================================================================
// LcuLineInfoService
// -----------------------------------------------------------------------------
// 役割:
//   LCU のライン情報取得・応答解析・順次読込と、結果の診断文生成をまとめる。
// 処理の流れ:
//   1. LoadSequentiallyAsync : LCU を順に読み込み、開始・完了・例外を呼出し元へ通知する。
//   2. GetLineInfoAsync      : 通信窓口から応答を取得し、ReadResponse で XML を解析する。
//   3. Create*              : DB取得・ライン取得・ツリー構築の進捗と診断文を作る。
// 備考:
//   - 通信は ILcuLineInfoGateway、画面操作はコールバックへ委譲する。
//   - HTTP例外時は次のLCUへ継続し、その他の例外・キャンセル時は読込を中断する。
// =============================================================================

#region 型定義

public interface ILcuLineInfoGateway
{
	Task<string> GetLinesAsync(CancellationToken cancellationToken);
}

public enum LcuLineInfoResponseOutcome
{
	Parsed,
	ErrorResponse,
	InvalidContent
}

public sealed record LcuLineInfoResponseResult<T>(
	LcuLineInfoResponseOutcome Outcome,
	T? Data);

public enum LcuLineInfoDiagnosticDestination
{
	ApplicationLog,
	Debug
}

public sealed record LcuLineInfoDiagnostic(
	LcuLineInfoDiagnosticDestination Destination,
	string Message);

public sealed record LcuLineInfoResponsePresentation(
	bool Succeeded,
	IReadOnlyList<LcuLineInfoDiagnostic> Diagnostics);

public sealed record LcuLineInfoDirectoryPresentation(
	bool Succeeded,
	IReadOnlyList<NeximLcuTarget> Targets,
	IReadOnlyList<LcuLineInfoDiagnostic> Diagnostics);

#endregion

/// <summary>LCU ライン情報の取得・解析・順次読込と診断内容の生成を行う。</summary>
public sealed class LcuLineInfoService
{
	#region 1. LCU の順次読込

	/// <summary>対象を順に読み込み、HTTP例外は通知して継続、それ以外の例外は通知して中断する。</summary>
	public async Task<bool> LoadSequentiallyAsync<T>(
		IEnumerable<T> targets,
		Func<T, Task<bool>> loadTargetAsync,
		Action<T> onLoadStarted,
		Action<T, bool> onLoadCompleted,
		Action<T, HttpRequestException> onRequestError,
		Action<T, Exception> onLoadError,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(targets);
		ArgumentNullException.ThrowIfNull(loadTargetAsync);
		ArgumentNullException.ThrowIfNull(onLoadStarted);
		ArgumentNullException.ThrowIfNull(onLoadCompleted);
		ArgumentNullException.ThrowIfNull(onRequestError);
		ArgumentNullException.ThrowIfNull(onLoadError);

		if (cancellationToken.IsCancellationRequested)
		{
			return false;
		}

		foreach (T target in targets)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return false;
			}

			onLoadStarted(target);
			if (cancellationToken.IsCancellationRequested)
			{
				return false;
			}

			bool loadSucceeded = true;
			try
			{
				loadSucceeded = await loadTargetAsync(target);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				return false;
			}
			catch (HttpRequestException exception)
			{
				if (cancellationToken.IsCancellationRequested)
				{
					return false;
				}

				loadSucceeded = false;
				onRequestError(target, exception);
			}
			catch (Exception exception)
			{
				if (cancellationToken.IsCancellationRequested)
				{
					return false;
				}

				onLoadError(target, exception);
				return false;
			}

			if (cancellationToken.IsCancellationRequested)
			{
				return false;
			}

			onLoadCompleted(target, loadSucceeded);
			if (cancellationToken.IsCancellationRequested)
			{
				return false;
			}
		}

		return !cancellationToken.IsCancellationRequested;
	}

	#endregion

	#region 2. ライン情報取得と応答解析

	/// <summary>ライン情報を取得して解析する。通信・検証後にもキャンセルを確認する。</summary>
	public async Task<LcuLineInfoResponseResult<T>> GetLineInfoAsync<T>(
		ILcuLineInfoGateway gateway,
		Func<T?, bool> isValid,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(gateway);
		ArgumentNullException.ThrowIfNull(isValid);
		cancellationToken.ThrowIfCancellationRequested();
		string response = await gateway.GetLinesAsync(cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		LcuLineInfoResponseResult<T> result = ReadResponse(response, isValid);
		cancellationToken.ThrowIfCancellationRequested();
		return result;
	}

	/// <summary>エラー応答を判別し、XML を型へ変換して必須内容を検証する。</summary>
	public LcuLineInfoResponseResult<T> ReadResponse<T>(
		string response,
		Func<T?, bool> isValid)
	{
		ArgumentNullException.ThrowIfNull(response);
		ArgumentNullException.ThrowIfNull(isValid);

		if (response.Contains("errorCode", StringComparison.Ordinal))
		{
			return new LcuLineInfoResponseResult<T>(
				LcuLineInfoResponseOutcome.ErrorResponse,
				default);
		}

		XmlSerializer serializer = new(typeof(T));
		T? data = (T?)serializer.Deserialize(new StringReader(response));
		if (!isValid(data))
		{
			return new LcuLineInfoResponseResult<T>(
				LcuLineInfoResponseOutcome.InvalidContent,
				data);
		}

		return new LcuLineInfoResponseResult<T>(
			LcuLineInfoResponseOutcome.Parsed,
			data);
	}

	#endregion

	#region 3. 表示内容と診断メッセージ

	/// <summary>DB から取得したライン情報のログを作る。</summary>
	public LcuLineInfoDiagnostic CreateDirectoryLineDiagnostic(NeximLineRecord line) 
	{
		ArgumentNullException.ThrowIfNull(line);
		return new(
			LcuLineInfoDiagnosticDestination.ApplicationLog,
			$"{line.Name}::LineID={line.LineId} JobID={line.JobId} ActiveFlag={line.ActiveFlag} LcuID={line.LcuId}");
	}

	/// <summary>DB から取得したコンピューター情報のログを作る。</summary>
	public LcuLineInfoDiagnostic CreateDirectoryComputerDiagnostic(NeximComputerRecord computer)
	{
		ArgumentNullException.ThrowIfNull(computer);
		return new(
			LcuLineInfoDiagnosticDestination.ApplicationLog,
			$"ComputerID={computer.ComputerId} LcuName={computer.Name}");
	}

	/// <summary>DB の取得結果を対象 LCU 一覧と診断へ変換する。</summary>
	public LcuLineInfoDirectoryPresentation CreateDirectoryPresentation(
		NeximLcuDirectoryResult result)
	{
		ArgumentNullException.ThrowIfNull(result);
		if (result.ErrorMessage != null)
		{
			return new(
				false,
				result.Targets,
				[
					new(
						LcuLineInfoDiagnosticDestination.ApplicationLog,
						$"[ERROR] NeximDB LCU list={result.ErrorMessage}")
				]);
		}

		return new(true, result.Targets, []);
	}

	/// <summary>読込例外のログを作る。</summary>
	public LcuLineInfoDiagnostic CreateLoadErrorDiagnostic(Exception exception)
	{
		ArgumentNullException.ThrowIfNull(exception);
		return new(
			LcuLineInfoDiagnosticDestination.ApplicationLog,
			$"[ERROR] LoadLineInfo={exception.Message}");
	}

	/// <summary>対象 LCU の読込例外を進捗表示用に整形する。</summary>
	public string CreateTargetLoadProgressMessage(string? host, Exception exception)
	{
		ArgumentNullException.ThrowIfNull(exception);
		return $"{host}={exception.Message}";
	}

	/// <summary>LCU 読込開始の進捗メッセージを作る。</summary>
	public string CreateLoadStartedProgressMessage(string? host) =>
		$"reading {host}";

	/// <summary>装置・モジュールのツリー構築進捗を整形する。</summary>
	public string CreateTreeProgressMessage(LcuLineTreeProgress progress)
	{
		ArgumentNullException.ThrowIfNull(progress);
		return progress.Stage switch
		{
			LcuLineTreeProgressStage.Machine => $"Machine={progress.Name}",
			LcuLineTreeProgressStage.Module => $"Module={progress.Name}",
			_ => throw new ArgumentOutOfRangeException(
				nameof(progress.Stage),
				progress.Stage,
				"Unknown LCU line tree progress stage.")
		};
	}

	/// <summary>応答解析結果を成否と従来の診断ログへ変換する。</summary>
	public LcuLineInfoResponsePresentation CreateResponsePresentation(
		LcuLineInfoResponseOutcome outcome,
		string? lcuName)
	{
		return outcome switch
		{
			LcuLineInfoResponseOutcome.Parsed => new(true, []),
			LcuLineInfoResponseOutcome.ErrorResponse =>
				new(
					false,
					[
						new(
							LcuLineInfoDiagnosticDestination.ApplicationLog,
							$"[ERROR] {lcuName};get lines error"),
						new(
							LcuLineInfoDiagnosticDestination.ApplicationLog,
							$"[LineInfo] {lcuName};get linesInfomation error")
					]),
			LcuLineInfoResponseOutcome.InvalidContent => new(false, []),
			_ => throw new ArgumentOutOfRangeException(
				nameof(outcome),
				outcome,
				"Unknown LCU line information response outcome.")
		};
	}

	/// <summary>読込中断のログを作る。</summary>
	public LcuLineInfoDiagnostic CreateLoadCancelledDiagnostic() =>
		new(
			LcuLineInfoDiagnosticDestination.ApplicationLog,
			"[ERROR] LoadLineInfo=Canceled");

	/// <summary>LCU ごとの読込結果をデバッグ出力用に整形する。</summary>
	public LcuLineInfoDiagnostic CreateLoadResultDiagnostic(
		string? lcuName,
		bool succeeded) =>
		new(
			LcuLineInfoDiagnosticDestination.Debug,
			$"[LineInfo] {lcuName}={succeeded}");

	/// <summary>選択された装置・モジュール・ユニット件数のログを作る。</summary>
	public LcuLineInfoDiagnostic CreateSelectionSummaryDiagnostic(
		string? lcuName,
		LcuSelectionSummary summary)
	{
		ArgumentNullException.ThrowIfNull(summary);
		return new(
			LcuLineInfoDiagnosticDestination.ApplicationLog,
			$"[LineInfo] {lcuName};Machine={summary.MachineCount};" +
			$"Module={summary.ModuleCount};Unit={summary.UnitCount}");
	}

	/// <summary>非対応機種を除外したログを作る。</summary>
	public LcuLineInfoDiagnostic CreateSkippedMachineDiagnostic(
		string? lcuName,
		string? machineName,
		string? machineType) =>
		new(
			LcuLineInfoDiagnosticDestination.ApplicationLog,
			$"[LineInfo] {lcuName};{machineName}=Skip:{machineType}");

	#endregion
}
