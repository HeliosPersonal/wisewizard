using NSubstitute;
using WiseWizard.Bot.Handlers;
using WiseWizard.Core.Abstractions;
using WiseWizard.Core.Models;

namespace WiseWizard.Bot.Tests.Handlers;

public class ReportHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 26, 12, 0, 0, TimeSpan.Zero);

    private static Run FinishedRun(long id) => new()
    {
        RunId = id,
        Status = RunStatus.Finished,
        StartedAt = Now - TimeSpan.FromHours(1),
        FinishedAt = Now,
    };

    private static Verdict V(long runId, string ticker, Signal signal) => new()
    {
        RunId = runId,
        Ticker = Ticker.Create(ticker),
        Signal = signal,
        SummaryLine = $"{ticker} summary",
        FullReasoning = "reasoning",
        Sources = ["d1"],
        ChangeFromYesterday = "delta",
        CreatedAt = Now,
    };

    private static (ReportHandler Handler, RecordingGateway Gateway, IRunRepository Runs, IVerdictRepository Verdicts) Build()
    {
        var runs = Substitute.For<IRunRepository>();
        var verdicts = Substitute.For<IVerdictRepository>();
        var gateway = new RecordingGateway();
        return (new ReportHandler(runs, verdicts, gateway), gateway, runs, verdicts);
    }

    [Fact]
    public async Task Sends_digest_for_latest_finished_run()
    {
        var (handler, gateway, runs, verdicts) = Build();
        runs.GetLatestFinishedAsync(Arg.Any<CancellationToken>()).Returns(FinishedRun(7));
        verdicts.GetForRunAsync(7, Arg.Any<CancellationToken>())
            .Returns([V(7, "AAPL", Signal.Hold), V(7, "MSFT", Signal.Review)]);

        await handler.HandleAsync(42);

        var sent = Assert.Single(gateway.Sent);
        Assert.Contains("AAPL", sent.Text);
        Assert.Contains("MSFT", sent.Text);
        Assert.NotNull(sent.Buttons);
        Assert.Equal(2, sent.Buttons!.Count);
    }

    [Fact]
    public async Task Uses_only_the_latest_finished_run_id_for_verdicts()
    {
        var (handler, _, runs, verdicts) = Build();
        runs.GetLatestFinishedAsync(Arg.Any<CancellationToken>()).Returns(FinishedRun(9));
        verdicts.GetForRunAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns([V(9, "AAPL", Signal.Hold)]);

        await handler.HandleAsync(42);

        await verdicts.Received(1).GetForRunAsync(9, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sends_empty_state_when_no_finished_run()
    {
        var (handler, gateway, runs, verdicts) = Build();
        runs.GetLatestFinishedAsync(Arg.Any<CancellationToken>()).Returns((Run?)null);

        await handler.HandleAsync(42);

        var sent = Assert.Single(gateway.Sent);
        Assert.Contains("no digest available yet", sent.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Null(sent.Buttons);
        await verdicts.DidNotReceive().GetForRunAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sends_multiple_ordered_messages_when_digest_is_chunked()
    {
        var (handler, gateway, runs, verdicts) = Build();
        runs.GetLatestFinishedAsync(Arg.Any<CancellationToken>()).Returns(FinishedRun(7));
        var many = Enumerable.Range(0, 45).Select(i => V(7, $"TICK{i}", Signal.Hold)).ToList();
        verdicts.GetForRunAsync(7, Arg.Any<CancellationToken>()).Returns(many);

        await handler.HandleAsync(42);

        Assert.True(gateway.Sent.Count >= 3);
        var totalButtons = gateway.Sent.Sum(s => s.Buttons?.Count ?? 0);
        Assert.Equal(45, totalButtons);
    }
}
