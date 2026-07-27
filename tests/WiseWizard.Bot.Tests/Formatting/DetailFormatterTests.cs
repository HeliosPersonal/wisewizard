using WiseWizard.Bot.Formatting;
using WiseWizard.Core.Models;

namespace WiseWizard.Bot.Tests.Formatting;

public class DetailFormatterTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 26, 12, 0, 0, TimeSpan.Zero);

    private static Verdict V(IReadOnlyList<string> sources) => new()
    {
        RunId = 7,
        Ticker = Ticker.Create("AAPL"),
        Signal = Signal.Review,
        SummaryLine = "review needed",
        FullReasoning = "Margins compressed 2.5% on FX headwinds.",
        Sources = sources,
        ChangeFromYesterday = "downgraded from hold",
        CreatedAt = Now,
    };

    [Fact]
    public void Renders_signal_ticker_reasoning_and_sources()
    {
        var msg = DetailFormatter.Format(V(["doc-1", "doc-2"]));

        Assert.Contains("🔴", msg.Text);
        Assert.Contains("AAPL", msg.Text);
        Assert.Contains("Margins compressed 2\\.5", msg.Text);
        Assert.Contains("doc\\-1", msg.Text);
        Assert.Contains("doc\\-2", msg.Text);
        Assert.Empty(msg.Buttons);
    }

    [Fact]
    public void Includes_change_from_yesterday()
    {
        var msg = DetailFormatter.Format(V(["doc-1"]));
        Assert.Contains("downgraded from hold", msg.Text);
    }

    [Fact]
    public void Absent_ticker_produces_no_verdict_message_and_no_reasoning()
    {
        var msg = DetailFormatter.FormatAbsent(Ticker.Create("TSLA"));

        Assert.Contains("no verdict", msg.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TSLA", msg.Text);
        Assert.Contains("latest", msg.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(msg.Buttons);
    }

    [Fact]
    public void Empty_sources_renders_none_cited()
    {
        var msg = DetailFormatter.Format(V([]));
        Assert.Contains("none cited", msg.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Reasoning_special_characters_are_escaped()
    {
        var v = V(["doc-1"]) with { FullReasoning = "risk: high (see 10-K)!" };
        var msg = DetailFormatter.Format(v);

        // ':' is not a MarkdownV2 special; parentheses, '-' and '!' are.
        Assert.Contains("risk: high \\(see 10\\-K\\)\\!", msg.Text);
    }
}
