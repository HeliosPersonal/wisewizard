using System.Reflection;
using NSubstitute;
using WiseWizard.Core.Abstractions;
using WiseWizard.Core.Models;
using WiseWizard.Core.Services;

namespace WiseWizard.Core.Tests;

public class NightlyRunOrchestratorTests
{
    private static readonly DateTimeOffset Start = new(2026, 7, 26, 23, 0, 0, TimeSpan.Zero);

    private static readonly TierPricing CheapPricing = new() { InputPerMillionUsd = 0.25m, OutputPerMillionUsd = 1.25m };
    private static readonly TierPricing SynthPricing = new() { InputPerMillionUsd = 3m, OutputPerMillionUsd = 15m };

    /// <summary>A test harness with substitutes and a tiny in-memory Run store for resume tests.</summary>
    private sealed class Harness
    {
        public IRunRepository Runs { get; }
        public IExtractedFactRepository Facts { get; } = Substitute.For<IExtractedFactRepository>();
        public IVerdictRepository Verdicts { get; } = Substitute.For<IVerdictRepository>();
        public IRawDocumentRepository Raw { get; } = Substitute.For<IRawDocumentRepository>();
        public ILlmClient Llm { get; } = Substitute.For<ILlmClient>();
        public IOwnerNotifier Notifier { get; } = Substitute.For<IOwnerNotifier>();
        public MutableClock Clock { get; } = new(Start);
        public PipelineOptions Options { get; }
        public NightlyRunOrchestrator Orchestrator { get; }

        private readonly Dictionary<long, Run> _store = new();
        private long _nextId = 1;

        public Harness(PipelineOptions? options = null)
        {
            Options = options ?? new PipelineOptions { CheapPricing = CheapPricing, SynthesisPricing = SynthPricing };

            Runs = Substitute.For<IRunRepository>();
            Runs.CreateAsync(Arg.Any<Run>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                {
                    var run = ci.Arg<Run>()! with { RunId = _nextId++ };
                    _store[run.RunId] = run;
                    return run;
                });
            Runs.UpdateAsync(Arg.Any<Run>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                {
                    var run = ci.Arg<Run>()!;
                    _store[run.RunId] = run;
                    return Task.CompletedTask;
                });
            Runs.GetAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
                .Returns(ci => _store.GetValueOrDefault(ci.Arg<long>()));
            Runs.GetResumableAsync(Arg.Any<CancellationToken>())
                .Returns(_ => (IReadOnlyList<Run>)_store.Values
                    .Where(r => r.Status is not (RunStatus.Finished or RunStatus.Failed))
                    .ToList());

            var cheapStep = new CheapTierExtractionStep(Llm, Raw);
            var synthStep = new SynthesisStep(Llm, Facts, Verdicts, Clock);
            Orchestrator = new NightlyRunOrchestrator(
                Runs, Facts, Verdicts, Llm, cheapStep, synthStep, Notifier, Clock, Options);
        }

        public Run Stored(long id) => _store[id];

        public void SeedResumable(Run run) => _store[run.RunId] = run;
    }

    private sealed class MutableClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = now;
    }

    private static RawDocument Doc(string id, string ticker, long runId = 1)
        => new()
        {
            DocumentId = id,
            RunId = runId,
            Ticker = Ticker.Create(ticker),
            Source = SourceKind.News,
            Title = "T",
            Content = "C",
            FetchedAt = Start,
            ContentHash = "h",
        };

    private static ExtractedFact Fact(string docId, string ticker, long runId = 1)
        => new()
        {
            RunId = runId,
            DocumentId = docId,
            Ticker = Ticker.Create(ticker),
            Fact = "f",
            Sentiment = FactSentiment.Positive,
            Materiality = FactMateriality.High,
        };

    private static BatchResultItem CheapResult(string docId, bool relevant = true, long input = 10, long output = 5)
        => new()
        {
            CustomId = docId,
            Text = relevant
                ? "RELEVANT: yes\nFACT: a fact\nSENTIMENT: positive\nMATERIALITY: high"
                : "RELEVANT: no\nFACT: NONE",
            InputTokens = input,
            OutputTokens = output,
        };

    private static BatchResultItem SynthResult(string ticker, string sources, Signal signal = Signal.Hold, long input = 20, long output = 10)
        => new()
        {
            CustomId = ticker,
            Text = $"SIGNAL: {signal.ToToken()}\nSUMMARY: s\nREASONING: r\nSOURCES: {sources}",
            InputTokens = input,
            OutputTokens = output,
        };

    // --- AC-01 happy path ---
    [Fact]
    public async Task Happy_full_run_produces_one_verdict_per_ticker_and_finishes()
    {
        var h = new Harness();
        var aapl = Ticker.Create("AAPL");

        h.Raw.GetForRunAsync(1, null, Arg.Any<CancellationToken>()).Returns([Doc("d1", "AAPL")]);
        h.Facts.GetForRunTickerAsync(1, aapl, Arg.Any<CancellationToken>()).Returns([Fact("d1", "AAPL")]);
        h.Verdicts.GetPreviousAsync(aapl, 1, Arg.Any<CancellationToken>()).Returns((Verdict?)null);

        h.Llm.SubmitBatchAsync(ModelTier.Cheap, Arg.Any<IReadOnlyList<BatchRequestItem>>(), Arg.Any<CancellationToken>())
            .Returns("cheap-1");
        h.Llm.SubmitBatchAsync(ModelTier.Synthesis, Arg.Any<IReadOnlyList<BatchRequestItem>>(), Arg.Any<CancellationToken>())
            .Returns("synth-1");
        h.Llm.GetBatchStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(BatchStatus.Completed);
        h.Llm.GetBatchResultsAsync("cheap-1", Arg.Any<CancellationToken>()).Returns([CheapResult("d1")]);
        h.Llm.GetBatchResultsAsync("synth-1", Arg.Any<CancellationToken>()).Returns([SynthResult("AAPL", "d1")]);

        var run = await h.Orchestrator.StartRunAsync([aapl]);
        Assert.Equal(RunStatus.Extracting, h.Stored(run.RunId).Status);

        await h.Orchestrator.PollAndAdvanceAsync(run.RunId); // extracting -> synthesizing
        Assert.Equal(RunStatus.Synthesizing, h.Stored(run.RunId).Status);

        var final = await h.Orchestrator.PollAndAdvanceAsync(run.RunId); // synthesizing -> finished

        Assert.Equal(RunStatus.Finished, final!.Status);
        Assert.NotNull(final.FinishedAt);
        await h.Verdicts.Received(1).UpsertAsync(
            Arg.Is<Verdict>(v => v != null && v.Ticker == aapl && v.HasEvidence), Arg.Any<CancellationToken>());
        await h.Facts.Received(1).AddRangeAsync(
            Arg.Is<IReadOnlyList<ExtractedFact>>(f => f != null && f.Count == 1), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateRunAsync_creates_pending_run_with_universe_and_no_batch()
    {
        var h = new Harness();
        var aapl = Ticker.Create("AAPL");

        var run = await h.Orchestrator.CreateRunAsync([aapl]);

        var stored = h.Stored(run.RunId);
        Assert.Equal(RunStatus.Pending, stored.Status);
        // No cheap batch submitted yet — ingestion happens before extraction begins.
        Assert.False(stored.BatchIds.ContainsKey(PipelineOptions.CheapBatchKey));
        await h.Llm.DidNotReceive().SubmitBatchAsync(
            Arg.Any<ModelTier>(), Arg.Any<IReadOnlyList<BatchRequestItem>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BeginExtractionAsync_submits_cheap_batch_and_moves_to_extracting()
    {
        var h = new Harness();
        var aapl = Ticker.Create("AAPL");
        h.Raw.GetForRunAsync(1, null, Arg.Any<CancellationToken>()).Returns([Doc("d1", "AAPL")]);
        h.Llm.SubmitBatchAsync(ModelTier.Cheap, Arg.Any<IReadOnlyList<BatchRequestItem>>(), Arg.Any<CancellationToken>())
            .Returns("cheap-1");

        var created = await h.Orchestrator.CreateRunAsync([aapl]);
        var extracting = await h.Orchestrator.BeginExtractionAsync(created);

        Assert.Equal(RunStatus.Extracting, extracting.Status);
        Assert.Equal("cheap-1", extracting.BatchIds[PipelineOptions.CheapBatchKey]);
        // The Universe recorded at creation is preserved through extraction.
        Assert.True(extracting.BatchIds.ContainsKey("universe"));
    }

    [Fact]
    public async Task CreateRunAsync_rejects_null_universe()
    {
        var h = new Harness();
        await Assert.ThrowsAsync<ArgumentNullException>(() => h.Orchestrator.CreateRunAsync(null!));
    }

    [Fact]
    public async Task BeginExtractionAsync_rejects_null_run()
    {
        var h = new Harness();
        await Assert.ThrowsAsync<ArgumentNullException>(() => h.Orchestrator.BeginExtractionAsync(null!));
    }

    // --- AC-02 delta present ---
    [Fact]
    public async Task Delta_present_when_previous_verdict_exists()
    {
        var h = new Harness();
        var aapl = Ticker.Create("AAPL");
        Verdict? captured = null;

        h.Raw.GetForRunAsync(1, null, Arg.Any<CancellationToken>()).Returns([Doc("d1", "AAPL")]);
        h.Facts.GetForRunTickerAsync(1, aapl, Arg.Any<CancellationToken>()).Returns([Fact("d1", "AAPL")]);
        h.Verdicts.GetPreviousAsync(aapl, 1, Arg.Any<CancellationToken>()).Returns(new Verdict
        {
            RunId = 0,
            Ticker = aapl,
            Signal = Signal.Hold,
            SummaryLine = "was hold",
            FullReasoning = "r",
            Sources = ["d0"],
            ChangeFromYesterday = "x",
            CreatedAt = Start,
        });
        _ = h.Verdicts.UpsertAsync(Arg.Do<Verdict>(v => captured = v), Arg.Any<CancellationToken>());

        h.Llm.SubmitBatchAsync(Arg.Any<ModelTier>(), Arg.Any<IReadOnlyList<BatchRequestItem>>(), Arg.Any<CancellationToken>())
            .Returns("cheap-1", "synth-1");
        h.Llm.GetBatchStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(BatchStatus.Completed);
        h.Llm.GetBatchResultsAsync("cheap-1", Arg.Any<CancellationToken>()).Returns([CheapResult("d1")]);
        h.Llm.GetBatchResultsAsync("synth-1", Arg.Any<CancellationToken>()).Returns([SynthResult("AAPL", "d1", Signal.Review)]);

        var run = await h.Orchestrator.StartRunAsync([aapl]);
        await h.Orchestrator.PollAndAdvanceAsync(run.RunId);
        await h.Orchestrator.PollAndAdvanceAsync(run.RunId);

        Assert.NotNull(captured);
        Assert.Contains("hold", captured!.ChangeFromYesterday);
        Assert.Contains("review", captured.ChangeFromYesterday);
    }

    // --- AC-03 batch failure -> fail + alert + prior intact ---
    [Fact]
    public async Task Cheap_batch_failure_fails_run_and_alerts_without_verdicts()
    {
        var h = new Harness();
        var aapl = Ticker.Create("AAPL");
        h.Raw.GetForRunAsync(1, null, Arg.Any<CancellationToken>()).Returns([Doc("d1", "AAPL")]);
        h.Llm.SubmitBatchAsync(ModelTier.Cheap, Arg.Any<IReadOnlyList<BatchRequestItem>>(), Arg.Any<CancellationToken>())
            .Returns("cheap-1");
        h.Llm.GetBatchStatusAsync("cheap-1", Arg.Any<CancellationToken>()).Returns(BatchStatus.Failed);

        var run = await h.Orchestrator.StartRunAsync([aapl]);
        var final = await h.Orchestrator.PollAndAdvanceAsync(run.RunId);

        Assert.Equal(RunStatus.Failed, final!.Status);
        Assert.Contains("Cheap-tier batch failed", final.FailureReason);
        await h.Notifier.Received(1).NotifyAsync(AlertKind.RunFailed, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await h.Verdicts.DidNotReceive().UpsertAsync(Arg.Any<Verdict>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Synthesis_batch_failure_fails_run_and_alerts()
    {
        var h = new Harness();
        var aapl = Ticker.Create("AAPL");
        h.Raw.GetForRunAsync(1, null, Arg.Any<CancellationToken>()).Returns([Doc("d1", "AAPL")]);
        h.Facts.GetForRunTickerAsync(1, aapl, Arg.Any<CancellationToken>()).Returns([Fact("d1", "AAPL")]);
        h.Verdicts.GetPreviousAsync(aapl, 1, Arg.Any<CancellationToken>()).Returns((Verdict?)null);
        h.Llm.SubmitBatchAsync(Arg.Any<ModelTier>(), Arg.Any<IReadOnlyList<BatchRequestItem>>(), Arg.Any<CancellationToken>())
            .Returns("cheap-1", "synth-1");
        h.Llm.GetBatchStatusAsync("cheap-1", Arg.Any<CancellationToken>()).Returns(BatchStatus.Completed);
        h.Llm.GetBatchStatusAsync("synth-1", Arg.Any<CancellationToken>()).Returns(BatchStatus.Failed);
        h.Llm.GetBatchResultsAsync("cheap-1", Arg.Any<CancellationToken>()).Returns([CheapResult("d1")]);

        var run = await h.Orchestrator.StartRunAsync([aapl]);
        await h.Orchestrator.PollAndAdvanceAsync(run.RunId);
        var final = await h.Orchestrator.PollAndAdvanceAsync(run.RunId);

        Assert.Equal(RunStatus.Failed, final!.Status);
        Assert.Contains("Synthesis-tier batch failed", final.FailureReason);
        await h.Verdicts.DidNotReceive().UpsertAsync(Arg.Any<Verdict>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Wall_clock_timeout_fails_run()
    {
        var h = new Harness();
        var aapl = Ticker.Create("AAPL");
        h.Raw.GetForRunAsync(1, null, Arg.Any<CancellationToken>()).Returns([Doc("d1", "AAPL")]);
        h.Llm.SubmitBatchAsync(ModelTier.Cheap, Arg.Any<IReadOnlyList<BatchRequestItem>>(), Arg.Any<CancellationToken>())
            .Returns("cheap-1");
        h.Llm.GetBatchStatusAsync("cheap-1", Arg.Any<CancellationToken>()).Returns(BatchStatus.InProgress);

        var run = await h.Orchestrator.StartRunAsync([aapl]);
        h.Clock.UtcNow = Start + TimeSpan.FromHours(21); // exceeds 20h ceiling

        var final = await h.Orchestrator.PollAndAdvanceAsync(run.RunId);

        Assert.Equal(RunStatus.Failed, final!.Status);
        Assert.Contains("wall-clock", final.FailureReason);
        await h.Notifier.Received(1).NotifyAsync(AlertKind.RunFailed, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // --- AC-04 advisory only: orchestrator never depends on a broker reader ---
    [Fact]
    public void Orchestrator_never_depends_on_broker_reader()
    {
        var ctorParams = typeof(NightlyRunOrchestrator)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(p => p.ParameterType);

        Assert.DoesNotContain(typeof(IBrokerReader), ctorParams);

        // No field, property, or method in the type references IBrokerReader either.
        var referencesBroker = typeof(NightlyRunOrchestrator)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Any(f => f.FieldType == typeof(IBrokerReader));
        Assert.False(referencesBroker);
    }

    // --- AC-06 no previous -> new marker ---
    [Fact]
    public async Task No_previous_verdict_marks_new()
    {
        var h = new Harness();
        var aapl = Ticker.Create("AAPL");
        Verdict? captured = null;

        h.Raw.GetForRunAsync(1, null, Arg.Any<CancellationToken>()).Returns([Doc("d1", "AAPL")]);
        h.Facts.GetForRunTickerAsync(1, aapl, Arg.Any<CancellationToken>()).Returns([Fact("d1", "AAPL")]);
        h.Verdicts.GetPreviousAsync(aapl, 1, Arg.Any<CancellationToken>()).Returns((Verdict?)null);
        _ = h.Verdicts.UpsertAsync(Arg.Do<Verdict>(v => captured = v), Arg.Any<CancellationToken>());

        h.Llm.SubmitBatchAsync(Arg.Any<ModelTier>(), Arg.Any<IReadOnlyList<BatchRequestItem>>(), Arg.Any<CancellationToken>())
            .Returns("cheap-1", "synth-1");
        h.Llm.GetBatchStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(BatchStatus.Completed);
        h.Llm.GetBatchResultsAsync("cheap-1", Arg.Any<CancellationToken>()).Returns([CheapResult("d1")]);
        h.Llm.GetBatchResultsAsync("synth-1", Arg.Any<CancellationToken>()).Returns([SynthResult("AAPL", "d1")]);

        var run = await h.Orchestrator.StartRunAsync([aapl]);
        await h.Orchestrator.PollAndAdvanceAsync(run.RunId);
        await h.Orchestrator.PollAndAdvanceAsync(run.RunId);

        Assert.Equal(DeltaComputer.NewMarker, captured!.ChangeFromYesterday);
    }

    // --- AC-07 cost ceiling -> stop + alert ---
    [Fact]
    public async Task Cost_ceiling_after_cheap_tier_stops_and_alerts()
    {
        var options = new PipelineOptions
        {
            CostCeilingUsd = 0.000001m, // tiny ceiling
            CheapPricing = CheapPricing,
            SynthesisPricing = SynthPricing,
        };
        var h = new Harness(options);
        var aapl = Ticker.Create("AAPL");

        h.Raw.GetForRunAsync(1, null, Arg.Any<CancellationToken>()).Returns([Doc("d1", "AAPL")]);
        h.Llm.SubmitBatchAsync(ModelTier.Cheap, Arg.Any<IReadOnlyList<BatchRequestItem>>(), Arg.Any<CancellationToken>())
            .Returns("cheap-1");
        h.Llm.GetBatchStatusAsync("cheap-1", Arg.Any<CancellationToken>()).Returns(BatchStatus.Completed);
        h.Llm.GetBatchResultsAsync("cheap-1", Arg.Any<CancellationToken>())
            .Returns([CheapResult("d1", input: 1_000_000, output: 1_000_000)]);

        var run = await h.Orchestrator.StartRunAsync([aapl]);
        var final = await h.Orchestrator.PollAndAdvanceAsync(run.RunId);

        Assert.Equal(RunStatus.Failed, final!.Status);
        Assert.Contains("Cost ceiling", final.FailureReason);
        await h.Notifier.Received(1).NotifyAsync(AlertKind.RunFailed, Arg.Any<string>(), Arg.Any<CancellationToken>());
        // Synthesis batch is never submitted once the ceiling is hit.
        await h.Llm.DidNotReceive().SubmitBatchAsync(
            ModelTier.Synthesis, Arg.Any<IReadOnlyList<BatchRequestItem>>(), Arg.Any<CancellationToken>());
        await h.Verdicts.DidNotReceive().UpsertAsync(Arg.Any<Verdict>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cost_ceiling_after_synthesis_tier_stops_before_publishing()
    {
        var options = new PipelineOptions
        {
            CostCeilingUsd = 0.01m,
            CheapPricing = CheapPricing,
            SynthesisPricing = SynthPricing,
        };
        var h = new Harness(options);
        var aapl = Ticker.Create("AAPL");

        h.Raw.GetForRunAsync(1, null, Arg.Any<CancellationToken>()).Returns([Doc("d1", "AAPL")]);
        h.Facts.GetForRunTickerAsync(1, aapl, Arg.Any<CancellationToken>()).Returns([Fact("d1", "AAPL")]);
        h.Verdicts.GetPreviousAsync(aapl, 1, Arg.Any<CancellationToken>()).Returns((Verdict?)null);
        h.Llm.SubmitBatchAsync(Arg.Any<ModelTier>(), Arg.Any<IReadOnlyList<BatchRequestItem>>(), Arg.Any<CancellationToken>())
            .Returns("cheap-1", "synth-1");
        h.Llm.GetBatchStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(BatchStatus.Completed);
        // cheap tier is cheap (small tokens); synthesis tier is expensive -> exceeds after synthesis.
        h.Llm.GetBatchResultsAsync("cheap-1", Arg.Any<CancellationToken>()).Returns([CheapResult("d1", input: 1000, output: 100)]);
        h.Llm.GetBatchResultsAsync("synth-1", Arg.Any<CancellationToken>())
            .Returns([SynthResult("AAPL", "d1", input: 1_000_000, output: 1_000_000)]);

        var run = await h.Orchestrator.StartRunAsync([aapl]);
        await h.Orchestrator.PollAndAdvanceAsync(run.RunId); // cheap ok -> synthesizing
        var final = await h.Orchestrator.PollAndAdvanceAsync(run.RunId); // synthesis exceeds

        Assert.Equal(RunStatus.Failed, final!.Status);
        Assert.Contains("Cost ceiling", final.FailureReason);
        await h.Verdicts.DidNotReceive().UpsertAsync(Arg.Any<Verdict>(), Arg.Any<CancellationToken>());
    }

    // --- AC-08 resume after restart -> no duplicate verdicts / no repeated steps ---
    [Fact]
    public async Task Resume_continues_pending_synthesis_without_repeating_extraction()
    {
        var h = new Harness();
        var aapl = Ticker.Create("AAPL");

        // Simulate a persisted Run mid-synthesis after a restart: extraction already done,
        // synthesis batch id already saved.
        h.SeedResumable(new Run
        {
            RunId = 42,
            Status = RunStatus.Synthesizing,
            StartedAt = Start,
            BatchIds = new Dictionary<string, string>
            {
                ["cheap"] = "cheap-1",
                ["synthesis"] = "synth-1",
                ["universe"] = "[\"AAPL\"]",
                ["no_evidence"] = "[]",
            },
            CostCheapUsd = 0.001m,
            CostTotalUsd = 0.001m,
            TokensCheap = 100,
            TokensTotal = 100,
        });

        h.Facts.GetForRunTickerAsync(42, aapl, Arg.Any<CancellationToken>()).Returns([Fact("d1", "AAPL", 42)]);
        h.Verdicts.GetPreviousAsync(aapl, 42, Arg.Any<CancellationToken>()).Returns((Verdict?)null);
        h.Llm.GetBatchStatusAsync("synth-1", Arg.Any<CancellationToken>()).Returns(BatchStatus.Completed);
        h.Llm.GetBatchResultsAsync("synth-1", Arg.Any<CancellationToken>()).Returns([SynthResult("AAPL", "d1")]);

        await h.Orchestrator.ResumeAsync();

        Assert.Equal(RunStatus.Finished, h.Stored(42).Status);
        // Extraction was NOT re-run: cheap batch neither submitted nor its results re-fetched.
        await h.Llm.DidNotReceive().SubmitBatchAsync(
            ModelTier.Cheap, Arg.Any<IReadOnlyList<BatchRequestItem>>(), Arg.Any<CancellationToken>());
        await h.Facts.DidNotReceive().AddRangeAsync(Arg.Any<IReadOnlyList<ExtractedFact>>(), Arg.Any<CancellationToken>());
        // Verdict upserted exactly once (idempotent by (run_id, ticker)).
        await h.Verdicts.Received(1).UpsertAsync(Arg.Any<Verdict>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Resume_extracting_without_cheap_batch_id_fails()
    {
        var h = new Harness();
        h.SeedResumable(new Run
        {
            RunId = 50,
            Status = RunStatus.Extracting,
            StartedAt = Start,
            BatchIds = new Dictionary<string, string>(), // no cheap batch id persisted
        });

        var final = await h.Orchestrator.PollAndAdvanceAsync(50);

        Assert.Equal(RunStatus.Failed, final!.Status);
        Assert.Contains("Cheap-tier batch id missing", final.FailureReason);
    }

    [Fact]
    public async Task Resume_extracting_without_universe_key_still_advances()
    {
        var h = new Harness();
        // Extracting run with a cheap batch id but no persisted universe/no_evidence keys.
        h.SeedResumable(new Run
        {
            RunId = 51,
            Status = RunStatus.Extracting,
            StartedAt = Start,
            BatchIds = new Dictionary<string, string> { ["cheap"] = "cheap-1" },
        });
        h.Raw.GetForRunAsync(51, null, Arg.Any<CancellationToken>()).Returns([Doc("d1", "AAPL", 51)]);
        h.Llm.GetBatchStatusAsync("cheap-1", Arg.Any<CancellationToken>()).Returns(BatchStatus.Completed);
        h.Llm.GetBatchResultsAsync("cheap-1", Arg.Any<CancellationToken>()).Returns([CheapResult("d1")]);

        var result = await h.Orchestrator.PollAndAdvanceAsync(51);

        // Empty universe → no synthesis batch → advances straight to finished.
        Assert.Equal(RunStatus.Finished, result!.Status);
        await h.Llm.DidNotReceive().SubmitBatchAsync(
            ModelTier.Synthesis, Arg.Any<IReadOnlyList<BatchRequestItem>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Resume_extracting_with_literal_null_universe_json_advances()
    {
        var h = new Harness();
        // Persisted universe JSON is the literal "null" → deserialize yields null, coalesced to empty.
        h.SeedResumable(new Run
        {
            RunId = 60,
            Status = RunStatus.Extracting,
            StartedAt = Start,
            BatchIds = new Dictionary<string, string> { ["cheap"] = "cheap-1", ["universe"] = "null" },
        });
        h.Raw.GetForRunAsync(60, null, Arg.Any<CancellationToken>()).Returns([Doc("d1", "AAPL", 60)]);
        h.Llm.GetBatchStatusAsync("cheap-1", Arg.Any<CancellationToken>()).Returns(BatchStatus.Completed);
        h.Llm.GetBatchResultsAsync("cheap-1", Arg.Any<CancellationToken>()).Returns([CheapResult("d1")]);

        var result = await h.Orchestrator.PollAndAdvanceAsync(60);

        Assert.Equal(RunStatus.Finished, result!.Status);
    }

    [Fact]
    public async Task Synthesizing_with_literal_null_no_evidence_json_finishes()
    {
        var h = new Harness();
        var aapl = Ticker.Create("AAPL");
        h.SeedResumable(new Run
        {
            RunId = 61,
            Status = RunStatus.Synthesizing,
            StartedAt = Start,
            BatchIds = new Dictionary<string, string> { ["synthesis"] = "synth-1", ["no_evidence"] = "null" },
        });
        h.Facts.GetForRunTickerAsync(61, aapl, Arg.Any<CancellationToken>()).Returns([Fact("d1", "AAPL", 61)]);
        h.Verdicts.GetPreviousAsync(aapl, 61, Arg.Any<CancellationToken>()).Returns((Verdict?)null);
        h.Llm.GetBatchStatusAsync("synth-1", Arg.Any<CancellationToken>()).Returns(BatchStatus.Completed);
        h.Llm.GetBatchResultsAsync("synth-1", Arg.Any<CancellationToken>()).Returns([SynthResult("AAPL", "d1")]);

        var result = await h.Orchestrator.PollAndAdvanceAsync(61);

        Assert.Equal(RunStatus.Finished, result!.Status);
    }

    [Fact]
    public async Task Synthesis_batch_in_progress_leaves_status_unchanged()
    {
        var h = new Harness();
        h.SeedResumable(new Run
        {
            RunId = 52,
            Status = RunStatus.Synthesizing,
            StartedAt = Start,
            BatchIds = new Dictionary<string, string>
            {
                ["synthesis"] = "synth-1",
                ["no_evidence"] = "[]",
            },
        });
        h.Llm.GetBatchStatusAsync("synth-1", Arg.Any<CancellationToken>()).Returns(BatchStatus.InProgress);

        var result = await h.Orchestrator.PollAndAdvanceAsync(52);

        Assert.Equal(RunStatus.Synthesizing, result!.Status);
        await h.Verdicts.DidNotReceive().UpsertAsync(Arg.Any<Verdict>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Synthesizing_without_no_evidence_key_still_finishes()
    {
        var h = new Harness();
        var aapl = Ticker.Create("AAPL");
        h.SeedResumable(new Run
        {
            RunId = 53,
            Status = RunStatus.Synthesizing,
            StartedAt = Start,
            BatchIds = new Dictionary<string, string> { ["synthesis"] = "synth-1" }, // no no_evidence key
        });
        h.Facts.GetForRunTickerAsync(53, aapl, Arg.Any<CancellationToken>()).Returns([Fact("d1", "AAPL", 53)]);
        h.Verdicts.GetPreviousAsync(aapl, 53, Arg.Any<CancellationToken>()).Returns((Verdict?)null);
        h.Llm.GetBatchStatusAsync("synth-1", Arg.Any<CancellationToken>()).Returns(BatchStatus.Completed);
        h.Llm.GetBatchResultsAsync("synth-1", Arg.Any<CancellationToken>()).Returns([SynthResult("AAPL", "d1")]);

        var result = await h.Orchestrator.PollAndAdvanceAsync(53);

        Assert.Equal(RunStatus.Finished, result!.Status);
        await h.Verdicts.Received(1).UpsertAsync(Arg.Any<Verdict>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PollAndAdvance_on_finished_run_is_noop()
    {
        var h = new Harness();
        h.SeedResumable(new Run { RunId = 7, Status = RunStatus.Finished, StartedAt = Start, FinishedAt = Start });

        var result = await h.Orchestrator.PollAndAdvanceAsync(7);

        Assert.Equal(RunStatus.Finished, result!.Status);
    }

    [Fact]
    public async Task PollAndAdvance_on_non_batch_status_returns_unchanged()
    {
        // A resumable Run in a status with no pending batch (e.g. Persisting) just returns as-is.
        var h = new Harness();
        h.SeedResumable(new Run { RunId = 8, Status = RunStatus.Persisting, StartedAt = Start });

        var result = await h.Orchestrator.PollAndAdvanceAsync(8);

        Assert.Equal(RunStatus.Persisting, result!.Status);
    }

    [Fact]
    public async Task PollAndAdvance_on_missing_run_returns_null()
    {
        var h = new Harness();
        Assert.Null(await h.Orchestrator.PollAndAdvanceAsync(999));
    }

    [Fact]
    public async Task PollAndAdvance_still_in_progress_leaves_status_unchanged()
    {
        var h = new Harness();
        var aapl = Ticker.Create("AAPL");
        h.Raw.GetForRunAsync(1, null, Arg.Any<CancellationToken>()).Returns([Doc("d1", "AAPL")]);
        h.Llm.SubmitBatchAsync(ModelTier.Cheap, Arg.Any<IReadOnlyList<BatchRequestItem>>(), Arg.Any<CancellationToken>())
            .Returns("cheap-1");
        h.Llm.GetBatchStatusAsync("cheap-1", Arg.Any<CancellationToken>()).Returns(BatchStatus.InProgress);

        var run = await h.Orchestrator.StartRunAsync([aapl]);
        var result = await h.Orchestrator.PollAndAdvanceAsync(run.RunId);

        Assert.Equal(RunStatus.Extracting, result!.Status);
    }

    // --- AC-09 empty evidence for a ticker: run still finishes for the rest ---
    [Fact]
    public async Task Ticker_with_no_documents_records_no_fresh_evidence_and_run_finishes()
    {
        var h = new Harness();
        var aapl = Ticker.Create("AAPL");
        var msft = Ticker.Create("MSFT");

        h.Raw.GetForRunAsync(1, null, Arg.Any<CancellationToken>()).Returns([Doc("d1", "AAPL")]);
        h.Facts.GetForRunTickerAsync(1, aapl, Arg.Any<CancellationToken>()).Returns([Fact("d1", "AAPL")]);
        h.Facts.GetForRunTickerAsync(1, msft, Arg.Any<CancellationToken>()).Returns([]); // no fresh docs
        h.Verdicts.GetPreviousAsync(Arg.Any<Ticker>(), 1, Arg.Any<CancellationToken>()).Returns((Verdict?)null);

        h.Llm.SubmitBatchAsync(Arg.Any<ModelTier>(), Arg.Any<IReadOnlyList<BatchRequestItem>>(), Arg.Any<CancellationToken>())
            .Returns("cheap-1", "synth-1");
        h.Llm.GetBatchStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(BatchStatus.Completed);
        h.Llm.GetBatchResultsAsync("cheap-1", Arg.Any<CancellationToken>()).Returns([CheapResult("d1")]);
        h.Llm.GetBatchResultsAsync("synth-1", Arg.Any<CancellationToken>()).Returns([SynthResult("AAPL", "d1")]);

        var run = await h.Orchestrator.StartRunAsync([aapl, msft]);
        await h.Orchestrator.PollAndAdvanceAsync(run.RunId);
        var final = await h.Orchestrator.PollAndAdvanceAsync(run.RunId);

        Assert.Equal(RunStatus.Finished, final!.Status);
        // Only AAPL gets a Verdict; MSFT gets none (no invented conclusion).
        await h.Verdicts.Received(1).UpsertAsync(Arg.Any<Verdict>(), Arg.Any<CancellationToken>());
        await h.Verdicts.Received(1).UpsertAsync(
            Arg.Is<Verdict>(v => v != null && v.Ticker == aapl), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_finishes_when_no_ticker_has_any_facts()
    {
        var h = new Harness();
        var aapl = Ticker.Create("AAPL");
        h.Raw.GetForRunAsync(1, null, Arg.Any<CancellationToken>()).Returns([Doc("d1", "AAPL")]);
        // Cheap tier finds nothing relevant.
        h.Facts.GetForRunTickerAsync(1, aapl, Arg.Any<CancellationToken>()).Returns([]);
        h.Llm.SubmitBatchAsync(ModelTier.Cheap, Arg.Any<IReadOnlyList<BatchRequestItem>>(), Arg.Any<CancellationToken>())
            .Returns("cheap-1");
        h.Llm.GetBatchStatusAsync("cheap-1", Arg.Any<CancellationToken>()).Returns(BatchStatus.Completed);
        h.Llm.GetBatchResultsAsync("cheap-1", Arg.Any<CancellationToken>()).Returns([CheapResult("d1", relevant: false)]);

        var run = await h.Orchestrator.StartRunAsync([aapl]);
        var final = await h.Orchestrator.PollAndAdvanceAsync(run.RunId);

        // No synthesis batch submitted; the Run advances straight to finished.
        Assert.Equal(RunStatus.Finished, final!.Status);
        await h.Llm.DidNotReceive().SubmitBatchAsync(
            ModelTier.Synthesis, Arg.Any<IReadOnlyList<BatchRequestItem>>(), Arg.Any<CancellationToken>());
        await h.Verdicts.DidNotReceive().UpsertAsync(Arg.Any<Verdict>(), Arg.Any<CancellationToken>());
    }
}
