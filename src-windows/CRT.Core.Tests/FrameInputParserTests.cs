using CRT.Core.Models;
using CRT.Core.Parsing;
using Xunit;

namespace CRT.Core.Tests;

public class FrameInputParserTests
{
    [Theory]
    [InlineData("123", 123)]
    [InlineData("  1,234 frames ", 1234)]
    [InlineData("abc", 0)]
    [InlineData("", 0)]
    [InlineData("...", 0)]
    public void PlainAndStripped(string input, int expected)
    {
        Assert.Equal(expected, FrameInputParser.ParseFrameInput(input, 60m));
    }

    [Theory]
    [InlineData("1.5", 90)]     // 1.5 s × 60
    [InlineData("1.2.3", 74)]   // dots collapse to 1.23 → 73.8 → 74
    [InlineData("0.5s", 30)]
    public void DecimalMeansSeconds(string input, int expected)
    {
        Assert.Equal(expected, FrameInputParser.ParseFrameInput(input, 60m));
    }

    [Fact]
    public void DecimalWithZeroFramerate_IsZero()
    {
        Assert.Equal(0, FrameInputParser.ParseFrameInput("1.5", 0m));
    }

    [Fact]
    public void RoundingIsHalfEven_LikePythonDecimal()
    {
        Assert.Equal(0, FrameInputParser.ParseFrameInput("0.5", 1m));
        Assert.Equal(2, FrameInputParser.ParseFrameInput("1.5", 1m));
    }

    [Fact]
    public void DebugInfo_CmtString()
    {
        string debugInfo = "prefix junk {\"cmt\": \"12.345\", \"docid\": \"abcDEF123-_\", \"fmt\": \"244\"}";
        // 12.345 × 60 = 740.7 → 741
        Assert.Equal(741, FrameInputParser.ParseFrameInput(debugInfo, 60m));
    }

    [Fact]
    public void DebugInfo_CmtNumber()
    {
        Assert.Equal(300, FrameInputParser.ParseFrameInput("{\"cmt\": 10}", 30m));
    }

    [Fact]
    public void DebugInfo_Invalid_ThrowsExactMessage()
    {
        var ex = Assert.Throws<ValidationException>(
            () => FrameInputParser.ParseFrameInput("{ not json \"cmt\"", 60m));
        Assert.Equal("The debug info provided is invalid.\nPlease re-enter debug info.", ex.Message);
    }

    [Fact]
    public void DebugInfo_MissingCmtKey_Throws()
    {
        // Contains the literal "cmt" characters (as a value) but no cmt key.
        var ex = Assert.Throws<ValidationException>(
            () => FrameInputParser.ParseFrameInput("{\"note\": \"cmt\"}", 60m));
        Assert.Equal(FrameInputParser.InvalidDebugInfoMessage, ex.Message);
    }

    [Theory]
    [InlineData("60", "60")]
    [InlineData("", "0")]
    [InlineData("abc", "0")]
    [InlineData("29.97fps", "29.97")]
    [InlineData("2..5", "2.5")]
    [InlineData("60.", "60.0")]
    [InlineData(".", "0")]
    public void CleanFramerate_Rules(string input, string expected)
    {
        Assert.Equal(
            decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture),
            FrameInputParser.CleanFramerate(input));
    }

    [Fact]
    public void IsDebugInfo_Detection()
    {
        Assert.True(FrameInputParser.IsDebugInfo("{\"cmt\": \"1\"}"));
        Assert.False(FrameInputParser.IsDebugInfo("plain 123"));
        Assert.False(FrameInputParser.IsDebugInfo("cmt without braces"));
    }
}

public class DebugInfoTests
{
    [Fact]
    public void ExtractIds_StringAndNumericFmt()
    {
        Assert.Equal(("vid123", "244"),
            DebugInfo.ExtractIds("junk {\"cmt\": \"1\", \"docid\": \"vid123\", \"fmt\": \"244\"}"));
        Assert.Equal(("vid123", "244"),
            DebugInfo.ExtractIds("{\"cmt\": \"1\", \"docid\": \"vid123\", \"fmt\": 244}"));
    }

    [Fact]
    public void ExtractIds_MissingFields_ReturnsNull()
    {
        Assert.Null(DebugInfo.ExtractIds("{\"cmt\": \"1\", \"docid\": \"vid123\"}"));
        Assert.Null(DebugInfo.ExtractIds("{\"cmt\": \"1\", \"fmt\": \"244\"}"));
        Assert.Null(DebugInfo.ExtractIds("no braces at all"));
        Assert.Null(DebugInfo.ExtractIds("{ broken json"));
    }
}
