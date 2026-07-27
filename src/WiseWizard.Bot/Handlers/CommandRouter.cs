using WiseWizard.Bot.Auth;
using WiseWizard.Core.Models;

namespace WiseWizard.Bot.Handlers;

/// <summary>
/// Routes transport-neutral inbound updates to the right handler, enforcing the Owner allowlist
/// FIRST (AC-05): a non-Owner update is dropped with no data access and no reply. Commands map to
/// <c>/portfolio</c>, <c>/report</c>, <c>/watch</c>, <c>/unwatch</c>, <c>/watchlist</c>; a
/// <c>detail:{ticker}</c> callback routes to the drill-down. Unknown Owner commands are ignored.
/// </summary>
public sealed class CommandRouter(
    OwnerAuthorizer authorizer,
    PortfolioHandler portfolio,
    ReportHandler report,
    WatchlistCommandHandler watchlist,
    DrillDownHandler drillDown)
{
    /// <summary>The callback-data prefix for a per-Ticker drill-down button.</summary>
    public const string DetailPrefix = "detail:";

    private readonly OwnerAuthorizer _authorizer = authorizer;
    private readonly PortfolioHandler _portfolio = portfolio;
    private readonly ReportHandler _report = report;
    private readonly WatchlistCommandHandler _watchlist = watchlist;
    private readonly DrillDownHandler _drillDown = drillDown;

    public async Task RouteMessageAsync(IncomingMessage message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!_authorizer.IsOwner(message.ChatId))
        {
            return; // AC-05: reveal nothing.
        }

        var (command, arguments) = SplitCommand(message.Text);
        switch (command)
        {
            case "/portfolio":
                await _portfolio.HandleAsync(message.ChatId, ct);
                break;
            case "/report":
                await _report.HandleAsync(message.ChatId, ct);
                break;
            case "/watch":
                await _watchlist.HandleWatchAsync(message.ChatId, arguments, ct);
                break;
            case "/unwatch":
                await _watchlist.HandleUnwatchAsync(message.ChatId, arguments, ct);
                break;
            case "/watchlist":
                await _watchlist.HandleListAsync(message.ChatId, ct);
                break;
            default:
                break; // Unknown command from the Owner: ignored.
        }
    }

    public async Task RouteCallbackAsync(IncomingCallback callback, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(callback);

        if (!_authorizer.IsOwner(callback.ChatId))
        {
            return; // AC-05: drop before resolving any Verdict.
        }

        if (!callback.Data.StartsWith(DetailPrefix, StringComparison.Ordinal))
        {
            return;
        }

        var raw = callback.Data[DetailPrefix.Length..];
        if (!Ticker.TryCreate(raw, out var ticker))
        {
            return;
        }

        await _drillDown.HandleAsync(callback.ChatId, callback.CallbackId, ticker, ct);
    }

    // Splits "/watch AAPL my note" into ("/watch", "AAPL my note"); strips an optional @botname.
    private static (string Command, string Arguments) SplitCommand(string text)
    {
        var trimmed = (text ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return (string.Empty, string.Empty);
        }

        var space = trimmed.IndexOf(' ', StringComparison.Ordinal);
        var command = space < 0 ? trimmed : trimmed[..space];
        var arguments = space < 0 ? string.Empty : trimmed[(space + 1)..].Trim();

        var at = command.IndexOf('@', StringComparison.Ordinal);
        if (at >= 0)
        {
            command = command[..at];
        }

        return (command.ToLowerInvariant(), arguments);
    }
}
