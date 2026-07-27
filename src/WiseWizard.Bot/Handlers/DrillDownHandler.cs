using WiseWizard.Bot.Formatting;
using WiseWizard.Core.Abstractions;
using WiseWizard.Core.Models;

namespace WiseWizard.Bot.Handlers;

/// <summary>
/// Handles a <c>detail:{ticker}</c> callback (AC-02/AC-02b/AC-06): resolves the latest FINISHED
/// Run, looks up that Ticker's Verdict in it, and sends the full reasoning + cited Sources — or a
/// plain "no verdict" message when the Ticker is absent from (or there is no) latest Run. Always
/// acknowledges the tap so the client spinner stops.
/// </summary>
public sealed class DrillDownHandler(
    IRunRepository runs,
    IVerdictRepository verdicts,
    ITelegramGateway gateway)
{
    private readonly IRunRepository _runs = runs;
    private readonly IVerdictRepository _verdicts = verdicts;
    private readonly ITelegramGateway _gateway = gateway;

    public async Task HandleAsync(long chatId, string callbackId, Ticker ticker, CancellationToken ct = default)
    {
        var run = await _runs.GetLatestFinishedAsync(ct);
        var verdict = run is null ? null : await _verdicts.GetAsync(run.RunId, ticker, ct);

        var message = verdict is null
            ? DetailFormatter.FormatAbsent(ticker)
            : DetailFormatter.Format(verdict);

        await _gateway.SendTextAsync(chatId, message.Text, null, ct);
        await _gateway.AnswerCallbackAsync(callbackId, ct);
    }
}
