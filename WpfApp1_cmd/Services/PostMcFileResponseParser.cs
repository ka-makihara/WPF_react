using System.Text.Json;

namespace WpfApp1_cmd.Services;

public sealed record PostMcFileResponse(
	bool HasError,
	bool HasErrorData,
	int ErrorIndex,
	string? ErrorCode,
	string? ErrorMessage);

public static class PostMcFileResponseParser
{
	public static PostMcFileResponse Parse(string response)
	{
		using JsonDocument document = JsonDocument.Parse(response);
		if (document.RootElement.ValueKind == JsonValueKind.Null)
		{
			return new PostMcFileResponse(false, false, -1, null, null);
		}

		if (!document.RootElement.TryGetProperty("ftp", out JsonElement ftp) ||
			ftp.ValueKind != JsonValueKind.Object ||
			!ftp.TryGetProperty("data", out JsonElement data) ||
			data.ValueKind == JsonValueKind.Null)
		{
			return new PostMcFileResponse(true, false, -1, null, null);
		}

		if (data.ValueKind != JsonValueKind.Array)
		{
			return new PostMcFileResponse(true, false, -1, null, null);
		}

		int index = 0;
		foreach (JsonElement item in data.EnumerateArray())
		{
			if (item.ValueKind != JsonValueKind.Object ||
				!item.TryGetProperty("errorCode", out JsonElement errorCode) ||
				errorCode.ValueKind == JsonValueKind.Null ||
				(errorCode.ValueKind == JsonValueKind.String && errorCode.GetString()!.Length != 0))
			{
				string? errorCodeText = item.ValueKind == JsonValueKind.Object &&
					item.TryGetProperty("errorCode", out JsonElement code)
					? code.ToString()
					: null;
				string? errorMessage = item.ValueKind == JsonValueKind.Object &&
					item.TryGetProperty("errorMessage", out JsonElement message)
					? message.ToString()
					: null;
				return new PostMcFileResponse(true, true, index, errorCodeText, errorMessage);
			}

			index++;
		}

		return new PostMcFileResponse(false, data.GetArrayLength() > 0, -1, null, null);
	}
}
