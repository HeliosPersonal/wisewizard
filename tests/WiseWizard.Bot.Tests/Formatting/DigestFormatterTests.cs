using WiseWizard.Bot.Formatting;
using WiseWizard.Core.Models;

namespace WiseWizard.Bot.Tests.Formatting;

public class DigestFormatterTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 26, 12, 0, 0, TimeSpan.Zero);

    private static Verdict V(string ticker, Signal signal, string summary) => new()
    {
        RunId = 7,
        Ticker = Ticker.Create(ticker),
        Signal = signal,
        SummaryLine = summary,
        FullReasoning = "reasoning",
        Sources = ["d1"],
        ChangeFromYesterday = "delta",
        CreatedAt = Now,
    };

    [Fact]
    public void Empty_verdicts_returns_single_no_digest_message_without_buttons()
    {
        var chunks = DigestFormatter.Format([]);

        var chunk = Assert.Single(chunks);
        Assert.Contains("no digest available yet", chunk.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(chunk.Buttons);
    }

    [Fact]
    public void Single_verdict_renders_emoji_ticker_and_summary_line()
    {
        var chunks = DigestFormatter.Format([V("AAPL", Signal.Hold, "steady as she goes")]);

        var chunk = Assert.Single(chunks);
        Assert.Contains("🟢", chunk.Text);
        Assert.Contains("AAPL", chunk.Text);
        Assert.Contains("steady as she goes", chunk.Text);
    }

    [Fact]
    public void Each_ticker_gets_a_detail_button_with_detail_prefix()
    {
        var chunks = DigestFormatter.Format([
            V("AAPL", Signal.Hold, "a"),
            V("MSFT", Signal.Review, "b"),
        ]);

        var buttons = chunks.SelectMany(c => c.Buttons).ToList();
        Assert.Equal(2, buttons.Count);
        Assert.Contains(buttons, b => b.CallbackData == "detail:AAPL");
        Assert.Contains(buttons, b => b.CallbackData == "detail:MSFT");
    }

    [Fact]
    public void Special_characters_in_summary_are_escaped()
    {
        var chunks = DigestFormatter.Format([V("AAPL", Signal.Hold, "up 1.5% (nice)")]);

        var chunk = Assert.Single(chunks);
        Assert.Contains("1\\.5", chunk.Text);
        Assert.Contains("\\(nice\\)", chunk.Text);
    }

    [Fact]
    public void More_than_twenty_tickers_are_chunked_and_no_ticker_is_dropped()
    {
        var verdicts = Enumerable.Range(0, 45)
            .Select(i => V($"TICK{i}", Signal.Hold, $"summary {i}"))
            .ToList();

        var chunks = DigestFormatter.Format(verdicts);

        Assert.True(chunks.Count >= 3, "45 tickers should span at least 3 chunks of ≤20");
        Assert.All(chunks, c => Assert.True(c.Buttons.Count <= 20));

        var allButtons = chunks.SelectMany(c => c.Buttons).ToList();
        Assert.Equal(45, allButtons.Count);
        foreach (var v in verdicts)
        {
            Assert.Contains(allButtons, b => b.CallbackData == $"detail:{v.Ticker.Value}");
        }
    }

    [Fact]
    public void Oversized_content_is_chunked_under_the_character_ceiling()
    {
        // Long summaries so the character ceiling (not the 20-line cap) forces the split.
        var big = new string('x', 600);
        var verdicts = Enumerable.Range(0, 10)
            .Select(i => V($"T{i}", Signal.Attention, big))
            .ToList();

        var chunks = DigestFormatter.Format(verdicts);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.True(c.Text.Length <= 4000, $"chunk length {c.Text.Length} exceeds 4000"));

        var allButtons = chunks.SelectMany(c => c.Buttons).ToList();
        Assert.Equal(10, allButtons.Count);
    }

    [Fact]
    public void Verdicts_are_ordered_by_signal_severity_then_ticker()
    {
        var chunks = DigestFormatter.Format([
            V("ZZZ", Signal.Hold, "z"),
            V("AAA", Signal.Review, "a"),
            V("MMM", Signal.Attention, "m"),
            V("BBB", Signal.Review, "b"),
        ]);

        var text = string.Join("\n", chunks.Select(c => c.Text));
        // Review (🔴) first, ordered AAA then BBB; then Attention (🟡) MMM; then Hold (🟢) ZZZ.
        var iAaa = text.IndexOf("AAA", StringComparison.Ordinal);
        var iBbb = text.IndexOf("BBB", StringComparison.Ordinal);
        var iMmm = text.IndexOf("MMM", StringComparison.Ordinal);
        var iZzz = text.IndexOf("ZZZ", StringComparison.Ordinal);

        Assert.True(iAaa < iBbb);
        Assert.True(iBbb < iMmm);
        Assert.True(iMmm < iZzz);
    }
}
