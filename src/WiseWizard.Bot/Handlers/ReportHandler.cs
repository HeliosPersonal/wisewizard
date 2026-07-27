using WiseWizard.Bot.Formatting;
using WiseWizard.Core.Abstractions;
using WiseWizard.Core.Models;

namespace WiseWizard.Bot.Handlers;

/// <summary>
/// Handles <c>/report</c> (AC-01/AC-04/AC-06/AC-09): resolves the latest FINISHED Run only
/// (never an in-progress Run), loads its Verdicts, renders the digest into ordered chunks and
/// sends each in order. When no completed Run exists, sends the graceful empty-state message.
/// </summary>
public sealed class ReportHandler(
    IRunRepository runs,
    IVerdictRepository verdicts,
    ITelegramGateway gateway)
{
    private readonly IRunRepository _runs = runs;
    private readonly IVerdictRepository _verdicts = verdicts;
    private readonly ITelegramGateway _gateway = gateway;

    public async Task HandleAsync(long chatId, CancellationToken ct = default)
    {
        var run = await _runs.GetLatestFinishedAsync(ct);

        IReadOnlyList<Verdict> forRun =
            run is null ? [] : await _verdicts.GetForRunAsync(run.RunId, ct);

        var messages = DigestFormatter.Format(forRun);
        foreach (var message in messages)
        {
            var buttons = message.Buttons.Count == 0 ? null : message.Buttons;
            await _gateway.SendTextAsync(chatId, message.Text, buttons, ct);
        }
    }
}
