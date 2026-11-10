using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class McAccountCredentialProviderTests
{
	[Fact]
	public void GetCredentials_ReturnsBothCredentials()
	{
		var provider = new McAccountProvider(
			() => "machine-user",
			() => "machine-password");

		MachineFtpCredentials credentials = provider.GetCredentials();

		Assert.Equal("machine-user", credentials.User);
		Assert.Equal("machine-password", credentials.Password);
		Assert.True(credentials.IsAvailable);
	}

	[Fact]
	public void GetCredentials_ReturnsEmptyCredentialsWhenNativeLibraryIsUnavailable()
	{
		var provider = new McAccountProvider(
			() => throw new DllNotFoundException(),
			() => "machine-password");

		MachineFtpCredentials credentials = provider.GetCredentials();

		Assert.Equal(MachineFtpCredentials.Empty, credentials);
		Assert.False(credentials.IsAvailable);
	}

	[Fact]
	public void GetCredentials_DoesNotSuppressUnexpectedErrors()
	{
		var provider = new McAccountProvider(
			() => throw new InvalidOperationException("unexpected"),
			() => "machine-password");

		Assert.Throws<InvalidOperationException>(() => provider.GetCredentials());
	}

	[Fact]
	public void GetNeximDatabaseSettings_UsesOptionsWhenProvided()
	{
		var provider = new McAccountProvider(
			() => "machine-user",
			() => "machine-password",
			() => "dll-db",
			() => "dll-user",
			() => "dll-password");

		NeximDatabaseSettings settings = provider.GetNeximDatabaseSettings(
			(name, _) => name switch
			{
				"--dbName" => "option-db",
				"--dbHost" => "db-host:1521",
				"--dbUser" => "option-user",
				"--dbPass" => "option-password",
				_ => ""
			});

		Assert.Equal(new NeximDatabaseSettings(
			"option-db",
			"db-host:1521",
			"option-user",
			"option-password"), settings);
	}

	[Fact]
	public void GetNeximDatabaseSettings_UsesNativeValuesAndHostDefaultWhenOptionsMissing()
	{
		var provider = new McAccountProvider(
			() => "machine-user",
			() => "machine-password",
			() => "dll-db",
			() => "dll-user",
			() => "dll-password");

		NeximDatabaseSettings settings = provider.GetNeximDatabaseSettings(
			(_, _) => "");

		Assert.Equal(new NeximDatabaseSettings(
			"dll-db",
			"localhost:1521",
			"dll-user",
			"dll-password"), settings);
	}

	[Fact]
	public void GetNeximDatabaseSettings_UsesLegacyFallbacksWhenNativeLibraryIsUnavailable()
	{
		var provider = new McAccountProvider(
			() => "machine-user",
			() => "machine-password",
			() => throw new DllNotFoundException(),
			() => throw new DllNotFoundException(),
			() => throw new DllNotFoundException());

		NeximDatabaseSettings settings = provider.GetNeximDatabaseSettings(
			(_, _) => "");

		Assert.Equal(new NeximDatabaseSettings(
			"XEPDB1",
			"localhost:1521",
			"",
			""), settings);
	}
}
