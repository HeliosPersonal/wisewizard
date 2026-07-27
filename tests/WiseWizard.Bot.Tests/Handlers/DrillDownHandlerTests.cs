using NSubstitute;
using WiseWizard.Bot.Handlers;
using WiseWizard.Core.Abstractions;
using WiseWizard.Core.Models;

namespace WiseWizard.Bot.Tests.Handlers;

public class DrillDownHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 26, 12, 0, 0, TimeSpan.Zero);

    private static Run FinishedRun(long id) => new()
    {
        RunId = id,
        Status = RunStatus.Finished,
        StartedAt = Now - TimeSpan.FromHours(1),
        FinishedAt = Now,
    };

    private static Verdict V(long runId, string ticker) => new()
    {
        RunId = runId,
        Ticker = Ticker.Create(ticker),
        Signal = Signal.Review,
        SummaryLine = "s",
        FullReasoning = "deep reasoning here",
        Sources = ["doc-1", "doc-2"],
        ChangeFromYesterday = "downgraded",
        CreatedAt = Now,
    };

    private static (DrillDownHandler Handler, RecordingGateway Gateway, IRunRepository Runs, IVerdictRepository Verdicts) Build()
    {
        var runs = Substitute.For<IRunRepository>();
        var verdicts = Substitute.For<IVerdictRepository>();
        var gateway = new RecordingGateway();
        return (new DrillDownHandler(runs, verdicts, gateway), gateway, runs, verdicts);
    }

    [Fact]
    public async Task Sends_full_reasoning_and_sources_and_acks()
    {
        var (handler, gateway, runs, verdicts) = Build();
        runs.GetLatestFinishedAsync(Arg.Any<CancellationToken>()).Returns(FinishedRun(7));
        verdicts.GetAsync(7, Ticker.Create("AAPL"), Arg.Any<CancellationToken>())
            .Returns(V(7, "AAPL"));

        await handler.HandleAsync(42, "cb1", Ticker.Create("AAPL"));

        var sent = Assert.Single(gateway.Sent);
        Assert.Contains("deep reasoning here", sent.Text);
        Assert.Contains("doc\\-1", sent.Text);
        Assert.Equal("cb1", Assert.Single(gateway.Acked));
    }

    [Fact]
    public async Task Sends_no_verdict_when_ticker_absent_in_run()
    {
        var (handler, gateway, runs, verdicts) = Build();
        runs.GetLatestFinishedAsync(Arg.Any<CancellationToken>()).Returns(FinishedRun(7));
        verdicts.GetAsync(7, Ticker.Create("TSLA"), Arg.Any<CancellationToken>())
            .Returns((Verdict?)null);

        await handler.HandleAsync(42, "cb2", Ticker.Create("TSLA"));

        var sent = Assert.Single(gateway.Sent);
        Assert.Contains("no verdict", sent.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TSLA", sent.Text);
        Assert.Equal("cb2", Assert.Single(gateway.Acked));
    }

    [Fact]
    public async Task Sends_no_verdict_when_no_finished_run()
    {
        var (handler, gateway, runs, verdicts) = Build();
        runs.GetLatestFinishedAsync(Arg.Any<CancellationToken>()).Returns((Run?)null);

        await handler.HandleAsync(42, "cb3", Ticker.Create("AAPL"));

        var sent = Assert.Single(gateway.Sent);
        Assert.Contains("no verdict", sent.Text, StringComparison.OrdinalIgnoreCase);
        await verdicts.DidNotReceive().GetAsync(Arg.Any<long>(), Arg.Any<Ticker>(), Arg.Any<CancellationToken>());
        Assert.Single(gateway.Acked);
    }
}
