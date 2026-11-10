using System.Text.Json;
using Xunit;
using WpfApp1_cmd.Services;

public class PostMcFileResponseParserTests
{
	[Theory]
	[InlineData("""{"ftp":{"data":[{"errorCode":"","errorMessage":""}]}}""")]
	[InlineData("""{"ftp":{"data":[]}}""")]
	[InlineData("null")]
	public void Parse_ReturnsNoErrorForSuccessfulResponses(string response)
	{
		PostMcFileResponse result = PostMcFileResponseParser.Parse(response);

		Assert.False(result.HasError);
	}

	[Fact]
	public void Parse_ReturnsFirstErrorDetails()
	{
		PostMcFileResponse result = PostMcFileResponseParser.Parse(
			"""{"ftp":{"data":[{"errorCode":"","errorMessage":""},{"errorCode":"E42","errorMessage":"transfer failed"}]}}""");

		Assert.True(result.HasError);
		Assert.True(result.HasErrorData);
		Assert.Equal(1, result.ErrorIndex);
		Assert.Equal("E42", result.ErrorCode);
		Assert.Equal("transfer failed", result.ErrorMessage);
	}

	[Theory]
	[InlineData("""{"ftp":{"data":null}}""")]
	[InlineData("""{"ftp":{}}""")]
	[InlineData("{}")]
	public void Parse_ReportsErrorWhenFtpDataIsMissing(string response)
	{
		PostMcFileResponse result = PostMcFileResponseParser.Parse(response);

		Assert.True(result.HasError);
		Assert.False(result.HasErrorData);
	}

	[Fact]
	public void Parse_RejectsMalformedJson()
	{
		Assert.ThrowsAny<JsonException>(() => PostMcFileResponseParser.Parse("{"));
	}
}
