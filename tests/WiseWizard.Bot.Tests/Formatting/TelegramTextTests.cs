using WiseWizard.Bot.Formatting;

namespace WiseWizard.Bot.Tests.Formatting;

public class TelegramTextTests
{
    [Theory]
    [InlineData("_", "\\_")]
    [InlineData("*", "\\*")]
    [InlineData("[", "\\[")]
    [InlineData("]", "\\]")]
    [InlineData("(", "\\(")]
    [InlineData(")", "\\)")]
    [InlineData("~", "\\~")]
    [InlineData("`", "\\`")]
    [InlineData(">", "\\>")]
    [InlineData("#", "\\#")]
    [InlineData("+", "\\+")]
    [InlineData("-", "\\-")]
    [InlineData("=", "\\=")]
    [InlineData("|", "\\|")]
    [InlineData("{", "\\{")]
    [InlineData("}", "\\}")]
    [InlineData(".", "\\.")]
    [InlineData("!", "\\!")]
    [InlineData("\\", "\\\\")]
    public void Escape_escapes_every_markdownv2_special_character(string input, string expected)
    {
        Assert.Equal(expected, TelegramText.Escape(input));
    }

    [Fact]
    public void Escape_leaves_ordinary_text_untouched()
    {
        Assert.Equal("AAPL up 3 percent today", TelegramText.Escape("AAPL up 3 percent today"));
    }

    [Fact]
    public void Escape_handles_mixed_content()
    {
        // ':' and '%' are not MarkdownV2 specials; '+' and '.' are.
        Assert.Equal("P&L: \\+1\\.5%", TelegramText.Escape("P&L: +1.5%"));
    }

    [Fact]
    public void Escape_null_returns_empty()
    {
        Assert.Equal(string.Empty, TelegramText.Escape(null));
    }

    [Fact]
    public void Bold_wraps_escaped_text_in_asterisks()
    {
        Assert.Equal("*A\\.B*", TelegramText.Bold("A.B"));
    }
}
