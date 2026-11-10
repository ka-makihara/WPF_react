using System.Runtime.InteropServices;
using System.Text;

namespace WpfApp1_cmd.Services;

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
