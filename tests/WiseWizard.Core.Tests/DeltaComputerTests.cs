using WiseWizard.Core.Models;
using WiseWizard.Core.Services;

namespace WiseWizard.Core.Tests;

public class DeltaComputerTests
{
    private static SynthesisOutput Synth(Signal signal, string summary = "summary")
        => new()
        {
            Signal = signal,
            SummaryLine = summary,
            FullReasoning = "reasoning",
            CitedDocumentIds = ["d1"],
        };

    private static Verdict Prev(Signal signal)
        => new()
        {
            RunId = 1,
            Ticker = Ticker.Create("AAPL"),
            Signal = signal,
            SummaryLine = "old summary",
            FullReasoning = "old reasoning",
            Sources = ["d0"],
            ChangeFromYesterday = "x",
            CreatedAt = DateTimeOffset.UnixEpoch,
        };

    [Fact]
    public void No_previous_marks_new()
    {
        var delta = DeltaComputer.Compute(Synth(Signal.Hold), previous: null);
        Assert.Equal(DeltaComputer.NewMarker, delta);
    }

    [Fact]
    public void Signal_changed_reports_transition()
    {
        var delta = DeltaComputer.Compute(Synth(Signal.Review, "needs review"), Prev(Signal.Hold));

        Assert.Contains("hold", delta);
        Assert.Contains("review", delta);
        Assert.Contains("needs review", delta);
    }

    [Fact]
    public void Signal_unchanged_reports_unchanged()
    {
        var delta = DeltaComputer.Compute(Synth(Signal.Hold, "still fine"), Prev(Signal.Hold));

        Assert.Contains("unchanged", delta);
        Assert.Contains("still fine", delta);
    }
}
