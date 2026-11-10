using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WpfApp1_cmd.Services;

public sealed record UnitLinkConfigurationEntry(
	IReadOnlyDictionary<string, string> Name,
	IReadOnlyList<string> Components,
	string? Mode,
	IReadOnlyList<string> Files);

public sealed class UnitLinkConfigurationService
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = false
	};

	public async Task<IReadOnlyList<UnitLinkConfigurationEntry>> LoadOverridesAsync(
		string path,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		cancellationToken.ThrowIfCancellationRequested();
		if (!File.Exists(path))
		{
			return [];
		}

		string json = await File.ReadAllTextAsync(path, cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		return await Task.Run(
			() => ParseOverrides(json, cancellationToken),
			cancellationToken);
	}

	private static IReadOnlyList<UnitLinkConfigurationEntry> ParseOverrides(
		string json,
		CancellationToken cancellationToken)
	{
		UnitLinkConfigurationFile? configuration =
			JsonSerializer.Deserialize<UnitLinkConfigurationFile>(json, JsonOptions);
		if (configuration?.Units == null)
		{
			return [];
		}

		UnitLinkConfigurationEntry[] entries =
			new UnitLinkConfigurationEntry[configuration.Units.Count];
		for (int unitIndex = 0; unitIndex < configuration.Units.Count; unitIndex++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			UnitLinkConfigurationItem unit = configuration.Units[unitIndex];
			Dictionary<string, string> names = new(StringComparer.Ordinal);
			foreach (KeyValuePair<string, JsonElement> pair in unit.Name)
			{
				cancellationToken.ThrowIfCancellationRequested();
				names.Add(pair.Key, pair.Value.ToString());
			}

			entries[unitIndex] = new(
				names,
				CopyStrings(unit.Components, cancellationToken),
				unit.Mode,
				CopyStrings(unit.Files, cancellationToken));
		}

		cancellationToken.ThrowIfCancellationRequested();
		return entries;
	}

	private static string[] CopyStrings(
		IReadOnlyList<string> values,
		CancellationToken cancellationToken)
	{
		string[] copy = new string[values.Count];
		for (int index = 0; index < values.Count; index++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			copy[index] = values[index];
		}

		return copy;
	}

	private sealed class UnitLinkConfigurationFile
	{
		[JsonPropertyName("units")]
		public List<UnitLinkConfigurationItem>? Units { get; set; }
	}

	private sealed class UnitLinkConfigurationItem
	{
		[JsonPropertyName("name")]
		public Dictionary<string, JsonElement> Name { get; set; } = [];

		[JsonPropertyName("components")]
		public List<string> Components { get; set; } = [];

		[JsonPropertyName("mode")]
		public string? Mode { get; set; }

		[JsonPropertyName("files")]
		public List<string> Files { get; set; } = [];
	}
}
