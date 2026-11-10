using System.Xml.Serialization;
using WpfApp1_cmd.Services;
using Xunit;

namespace WpfApp1_cmd.Tests;

public sealed class LcuLineInfoService_ResponseTests
{
	[Fact]
	public void Read_ReturnsErrorResponseWhenErrorCodeIsPresent()
	{
		LcuLineInfoResponseResult<TestLineInfo> result =
			new LcuLineInfoService().ReadResponse<TestLineInfo>(
				"""{"errorCode":"E42"}""",
				data => data?.Machines != null);

		Assert.Equal(LcuLineInfoResponseOutcome.ErrorResponse, result.Outcome);
		Assert.Null(result.Data);
	}

	[Fact]
	public void Read_DeserializesValidXml()
	{
		LcuLineInfoResponseResult<TestLineInfo> result =
			new LcuLineInfoService().ReadResponse<TestLineInfo>(
				"<lines><machine>MachineA</machine></lines>",
				data => data?.Machines != null);

		Assert.Equal(LcuLineInfoResponseOutcome.Parsed, result.Outcome);
		Assert.NotNull(result.Data?.Machines);
		Assert.Equal("MachineA", Assert.Single(result.Data.Machines));
	}

	[Fact]
	public void Read_ReturnsInvalidContentWhenRequiredDataIsMissing()
	{
		LcuLineInfoResponseResult<TestLineInfo> result =
			new LcuLineInfoService().ReadResponse<TestLineInfo>(
				"""<lines xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:nil="true" />""",
				data => data?.Machines != null);

		Assert.Equal(LcuLineInfoResponseOutcome.InvalidContent, result.Outcome);
	}

	[Fact]
	public void Read_PropagatesMalformedXml()
	{
		Assert.Throws<InvalidOperationException>(() =>
			new LcuLineInfoService().ReadResponse<TestLineInfo>(
				"<lines>",
				data => data?.Machines != null));
	}

	[XmlRoot("lines")]
	public sealed class TestLineInfo
	{
		[XmlElement("machine")]
		public List<string>? Machines { get; set; }
	}
}
