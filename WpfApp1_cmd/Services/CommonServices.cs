namespace WpfApp1_cmd.Services
{
using System.Collections.Concurrent;

public static class DialogHostShowCoordinator
{
	private static readonly ConcurrentDictionary<string, SemaphoreSlim> DialogLocks =
		new(StringComparer.Ordinal);

	public static async Task<object?> ShowAsync(
		string identifier,
		Func<Task<object?>> showDialog)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
		ArgumentNullException.ThrowIfNull(showDialog);

		SemaphoreSlim dialogLock = DialogLocks.GetOrAdd(
			identifier,
			static _ => new SemaphoreSlim(1, 1));
		await dialogLock.WaitAsync();
		try
		{
			return await showDialog();
		}
		finally
		{
			dialogLock.Release();
		}
	}
}
}

namespace WpfApp1_cmd.Services
{
using System.Runtime.InteropServices;
using System.Text;

public sealed record MachineFtpCredentials(string User, string Password)
{
	public bool IsAvailable =>
		!string.IsNullOrEmpty(User) &&
		!string.IsNullOrEmpty(Password);

	public static MachineFtpCredentials Empty { get; } = new("", "");
}

public sealed record NeximDatabaseSettings(
	string Name,
	string Host,
	string User,
	string Password);

public sealed class McAccountProvider
{
	private readonly Func<string> _getMachineUser;
	private readonly Func<string> _getMachinePassword;
	private readonly Func<string> _getDatabaseName;
	private readonly Func<string> _getDatabaseUser;
	private readonly Func<string> _getDatabasePassword;

	public McAccountProvider()
		: this(
			ReadMachineUserFromNativeLibrary,
			ReadMachinePasswordFromNativeLibrary,
			ReadDatabaseNameFromNativeLibrary,
			ReadDatabaseUserFromNativeLibrary,
			ReadDatabasePasswordFromNativeLibrary)
	{
	}

	public McAccountProvider(
		Func<string> getMachineUser,
		Func<string> getMachinePassword)
		: this(
			getMachineUser,
			getMachinePassword,
			() => "",
			() => "",
			() => "")
	{
	}

	public McAccountProvider(
		Func<string> getMachineUser,
		Func<string> getMachinePassword,
		Func<string> getDatabaseName,
		Func<string> getDatabaseUser,
		Func<string> getDatabasePassword)
	{
		ArgumentNullException.ThrowIfNull(getMachineUser);
		ArgumentNullException.ThrowIfNull(getMachinePassword);
		ArgumentNullException.ThrowIfNull(getDatabaseName);
		ArgumentNullException.ThrowIfNull(getDatabaseUser);
		ArgumentNullException.ThrowIfNull(getDatabasePassword);
		_getMachineUser = getMachineUser;
		_getMachinePassword = getMachinePassword;
		_getDatabaseName = getDatabaseName;
		_getDatabaseUser = getDatabaseUser;
		_getDatabasePassword = getDatabasePassword;
	}

	public MachineFtpCredentials GetCredentials()
	{
		try
		{
			return new MachineFtpCredentials(
				_getMachineUser() ?? "",
				_getMachinePassword() ?? "");
		}
		catch (DllNotFoundException)
		{
			return MachineFtpCredentials.Empty;
		}
		catch (EntryPointNotFoundException)
		{
			return MachineFtpCredentials.Empty;
		}
		catch (BadImageFormatException)
		{
			return MachineFtpCredentials.Empty;
		}
	}

	public NeximDatabaseSettings GetNeximDatabaseSettings(
		Func<string, string, string?> getOption)
	{
		ArgumentNullException.ThrowIfNull(getOption);

		string? databaseName = getOption("--dbName", "");
		string? databaseHost = getOption("--dbHost", "");
		string? databaseUser = getOption("--dbUser", "");
		string? databasePassword = getOption("--dbPass", "");

		return new NeximDatabaseSettings(
			!string.IsNullOrEmpty(databaseName)
				? databaseName
				: ReadNativeValueOrDefault(_getDatabaseName, "XEPDB1"),
			!string.IsNullOrEmpty(databaseHost)
				? databaseHost
				: "localhost:1521",
			!string.IsNullOrEmpty(databaseUser)
				? databaseUser
				: ReadNativeValueOrDefault(_getDatabaseUser, ""),
			!string.IsNullOrEmpty(databasePassword)
				? databasePassword
				: ReadNativeValueOrDefault(_getDatabasePassword, ""));
	}

	private static string ReadNativeValueOrDefault(Func<string> getValue, string defaultValue)
	{
		try
		{
			return getValue() ?? defaultValue;
		}
		catch (DllNotFoundException)
		{
			return defaultValue;
		}
		catch (EntryPointNotFoundException)
		{
			return defaultValue;
		}
		catch (BadImageFormatException)
		{
			return defaultValue;
		}
	}

	private static string ReadMachineUserFromNativeLibrary() => ReadNativeString(GetMcUser);
	private static string ReadMachinePasswordFromNativeLibrary() => ReadNativeString(GetMcPassword);
	private static string ReadDatabaseNameFromNativeLibrary() => ReadNativeString(GetNeximDatabaseName);
	private static string ReadDatabaseUserFromNativeLibrary() => ReadNativeString(GetNeximDatabaseUser);
	private static string ReadDatabasePasswordFromNativeLibrary() => ReadNativeString(GetNeximDatabasePassword);

	private static string ReadNativeString(Func<StringBuilder, int, int> nativeGetter)
	{
		StringBuilder buffer = new(1024);
		nativeGetter(buffer, buffer.Capacity);
		return buffer.ToString();
	}

	[DllImport("mcAccount.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "getMcUser")]
	private static extern int GetMcUser(StringBuilder value, int length);

	[DllImport("mcAccount.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "getMcPass")]
	private static extern int GetMcPassword(StringBuilder value, int length);

	[DllImport("mcAccount.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "getNeximDBName")]
	private static extern int GetNeximDatabaseName(StringBuilder value, int length);

	[DllImport("mcAccount.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "getNeximDBUser")]
	private static extern int GetNeximDatabaseUser(StringBuilder value, int length);

	[DllImport("mcAccount.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "getNeximDBPass")]
	private static extern int GetNeximDatabasePassword(StringBuilder value, int length);
}
}

namespace WpfApp1_cmd.Services
{


public sealed class OperationProgressMonitor
{
	private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

	public async Task<bool> WaitAsync(
		Task operation,
		Func<bool> isRunning,
		Func<bool> isCancelRequested,
		Func<Task> pauseForConfirmation,
		Func<Task<bool>> confirmCancellation,
		Func<Task> resumeAfterDeclinedCancellation,
		Action onCancellationConfirmed)
	{
		ArgumentNullException.ThrowIfNull(operation);
		ArgumentNullException.ThrowIfNull(isRunning);
		ArgumentNullException.ThrowIfNull(isCancelRequested);
		ArgumentNullException.ThrowIfNull(pauseForConfirmation);
		ArgumentNullException.ThrowIfNull(confirmCancellation);
		ArgumentNullException.ThrowIfNull(resumeAfterDeclinedCancellation);
		ArgumentNullException.ThrowIfNull(onCancellationConfirmed);

		while (!operation.IsCompleted && isRunning())
		{
			if (isCancelRequested())
			{
				await pauseForConfirmation();
				if (operation.IsCompleted)
				{
					return false;
				}

				if (await confirmCancellation())
				{
					if (operation.IsCompleted)
					{
						return false;
					}

					onCancellationConfirmed();
					return true;
				}

				if (operation.IsCompleted)
				{
					return false;
				}

				await resumeAfterDeclinedCancellation();
				if (operation.IsCompleted)
				{
					return false;
				}
			}

			await Task.Delay(PollInterval);
		}

		return false;
	}
}
}

namespace WpfApp1_cmd.Services
{


public sealed record TreeSelectionUnitInput(string? Name, bool? IsSelected);

public sealed record TreeSelectionModuleInput(
	string? Name,
	bool? IsSelected,
	IReadOnlyList<TreeSelectionUnitInput> Units);

public sealed record TreeSelectionMachineInput(
	string? Name,
	bool? IsSelected,
	IReadOnlyList<TreeSelectionModuleInput> Modules);

public sealed record TreeSelectionLcuInput(
	string? Name,
	bool? IsSelected,
	IReadOnlyList<TreeSelectionMachineInput> Machines);

public sealed class TreeSelectionDiagnosticBuilder
{
	public IReadOnlyList<string> Build(IReadOnlyList<TreeSelectionLcuInput> lcus)
	{
		ArgumentNullException.ThrowIfNull(lcus);

		List<string> messages = [];
		foreach (TreeSelectionLcuInput lcu in lcus)
		{
			ArgumentNullException.ThrowIfNull(lcu);
			messages.Add($"{lcu.Name}:{FormatSelection(lcu.IsSelected)}");
			foreach (TreeSelectionMachineInput machine in lcu.Machines)
			{
				ArgumentNullException.ThrowIfNull(machine);
				messages.Add($"    {machine.Name}:{FormatSelection(machine.IsSelected)}");
				foreach (TreeSelectionModuleInput module in machine.Modules)
				{
					ArgumentNullException.ThrowIfNull(module);
					messages.Add($"       {module.Name}:{FormatSelection(module.IsSelected)}");
					foreach (TreeSelectionUnitInput unit in module.Units)
					{
						ArgumentNullException.ThrowIfNull(unit);
						messages.Add($"          {unit.Name}:{FormatSelection(unit.IsSelected)}");
					}
				}
			}
		}

		return messages;
	}

	private static string FormatSelection(bool? value) =>
		value switch
		{
			true => "True",
			false => "False",
			null => "Null"
		};
}
}

namespace WpfApp1_cmd.Services
{


public sealed record TreeSelectionDecision(
	bool? Selection,
	bool PropagateToChildren);

public sealed class TreeSelectionPolicy
{
	public TreeSelectionDecision Evaluate(ItemStatus status, bool? requestedSelection) =>
		status == ItemStatus.NOT_SUPPORTED
			? new(false, false)
			: new(requestedSelection, true);
}
}

namespace WpfApp1_cmd.Services
{


public sealed class ViewCacheKeyRemovalBuilder
{
	public IReadOnlyList<string> Build(
		IEnumerable<string> keys,
		string viewName,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(keys);
		ArgumentNullException.ThrowIfNull(viewName);
		cancellationToken.ThrowIfCancellationRequested();

		List<string> keysToRemove = [];
		foreach (string key in keys)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (key.Contains(viewName))
			{
				keysToRemove.Add(key);
			}
		}

		cancellationToken.ThrowIfCancellationRequested();
		return keysToRemove;
	}
}
}
