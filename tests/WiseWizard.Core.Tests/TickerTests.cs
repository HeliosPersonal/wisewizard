using WiseWizard.Core.Models;

namespace WiseWizard.Core.Tests;

public class TickerTests
{
    [Theory]
    [InlineData("aapl", "AAPL")]
    [InlineData("  voo  ", "VOO")]
    [InlineData("BRK.B", "BRK.B")]
    [InlineData("brk-b", "BRK-B")]
    [InlineData("Msft", "MSFT")]
    public void Create_NormalizesTrimAndUppercase(string raw, string expected)
    {
        var ticker = Ticker.Create(raw);
        Assert.Equal(expected, ticker.Value);
        Assert.Equal(expected, ticker.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsEmpty(string? raw)
    {
        Assert.Throws<ArgumentException>(() => Ticker.Create(raw));
    }

    [Fact]
    public void Create_RejectsTooLong()
    {
        Assert.Throws<ArgumentException>(() => Ticker.Create("ABCDEFGHIJK")); // 11 chars
    }

    [Theory]
    [InlineData("AA PL")]
    [InlineData("AA$L")]
    [InlineData("A@B")]
    [InlineData("café")]
    public void Create_RejectsInvalidCharacters(string raw)
    {
        Assert.Throws<ArgumentException>(() => Ticker.Create(raw));
    }

    [Fact]
    public void Create_AcceptsMaxLength()
    {
        var ticker = Ticker.Create("ABCDEFGHIJ"); // 10 chars
        Assert.Equal("ABCDEFGHIJ", ticker.Value);
    }

    [Fact]
    public void TryCreate_ReturnsTrueAndValue_ForValid()
    {
        var ok = Ticker.TryCreate("nvda", out var ticker);
        Assert.True(ok);
        Assert.Equal("NVDA", ticker.Value);
    }

    [Fact]
    public void TryCreate_ReturnsFalse_ForInvalid()
    {
        var ok = Ticker.TryCreate("bad symbol!", out var ticker);
        Assert.False(ok);
        Assert.Equal(default, ticker);
    }

    [Fact]
    public void Equality_IsValueBasedAfterNormalization()
    {
        Assert.Equal(Ticker.Create("aapl"), Ticker.Create("AAPL"));
        Assert.NotEqual(Ticker.Create("aapl"), Ticker.Create("msft"));
    }
}
