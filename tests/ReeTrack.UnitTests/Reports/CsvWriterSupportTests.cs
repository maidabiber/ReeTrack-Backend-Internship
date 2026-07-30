using ReeTrack.Infrastructure.Reports.Writers;
using Xunit;

namespace ReeTrack.UnitTests.Reports;

public class CsvWriterSupportTests
{
    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("has,comma", "\"has,comma\"")]
    [InlineData("has\"quote", "\"has\"\"quote\"")]
    [InlineData("line\nbreak", "\"line\nbreak\"")]
    [InlineData("line\r\nbreak", "\"line\r\nbreak\"")]
    public void Escape_FollowsRfc4180(string input, string expected)
    {
        Assert.Equal(expected, CsvWriterSupport.Escape(input));
    }

    [Theory]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("+1", "'+1")]
    [InlineData("-1", "'-1")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("=cmd|'/c calc'!A1", "'=cmd|'/c calc'!A1")]
    [InlineData("=HYPERLINK(\"http://x\",\"y\")", "\"'=HYPERLINK(\"\"http://x\"\",\"\"y\"\")\"")] // guarded, then RFC-quoted for the quotes
    [InlineData("Normal name", "Normal name")]
    public void Escape_NeutralisesFormulaTriggers(string input, string expected)
    {
        Assert.Equal(expected, CsvWriterSupport.Escape(input));
    }

    [Theory]
    [InlineData(1.5, "1.5")]
    [InlineData(1, "1")]
    [InlineData(1.23456, "1.2346")]
    public void FormatDecimal_TrimsToAtMostFourDecimalPlaces(decimal value, string expected)
    {
        Assert.Equal(expected, CsvWriterSupport.FormatDecimal(value));
    }
}
