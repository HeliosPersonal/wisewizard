using WiseWizard.Core.Models;

namespace WiseWizard.Core.Tests;

public class VerdictTests
{
    private static Verdict Make(params string[] sources) => new()
    {
        RunId = 1,
        Ticker = Ticker.Create("AAPL"),
        Signal = Signal.Hold,
        SummaryLine = "steady",
        FullReasoning = "reasoning",
        Sources = sources,
        ChangeFromYesterday = "new",
        CreatedAt = DateTimeOffset.UnixEpoch,
    };

    [Fact]
    public void HasEvidence_TrueWhenSourcesPresent()
    {
        Assert.True(Make("doc-1").HasEvidence);
        Assert.True(Make("doc-1", "doc-2").HasEvidence);
    }

    [Fact]
    public void HasEvidence_FalseWhenNoSources()
    {
        Assert.False(Make().HasEvidence);
    }
}
