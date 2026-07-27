using WiseWizard.Bot.Formatting;
using WiseWizard.Core.Abstractions;

namespace WiseWizard.Bot.Handlers;

/// <summary>
/// Handles <c>/portfolio</c> (AC-03): reads the current Positions snapshot and the session state
/// (for snapshot age / stale note), renders the summary and sends it to the Owner. Empty portfolio
/// is rendered as a plain empty-state message.
/// </summary>
public sealed class PortfolioHandler(
    IPositionsRepository positions,
    IBrokerSessionRepository sessions,
    ITelegramGateway gateway,
    IClock clock)
{
    private readonly IPositionsRepository _positions = positions;
    private readonly IBrokerSessionRepository _sessions = sessions;
    private readonly ITelegramGateway _gateway = gateway;
    private readonly IClock _clock = clock;

    public async Task HandleAsync(long chatId, CancellationToken ct = default)
    {
        var current = await _positions.GetCurrentAsync(ct);
        var session = await _sessions.GetAsync(ct);

        var message = PortfolioFormatter.Format(current, session, _clock.UtcNow);
        await _gateway.SendTextAsync(chatId, message.Text, null, ct);
    }
}
