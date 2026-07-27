using System.Text;
using WiseWizard.Bot.Formatting;
using WiseWizard.Core.Services;

namespace WiseWizard.Bot.Handlers;

/// <summary>
/// Transport surface for the Watchlist commands (AC-10). Parses <c>/watch SYM [note]</c>,
/// <c>/unwatch SYM</c> and <c>/watchlist</c>, delegates the decision to <see cref="WatchlistService"/>
/// (which owns validity and the domain invariants), and renders the domain's outcome back to the
/// Owner. This handler never decides whether a change is valid.
/// </summary>
public sealed class WatchlistCommandHandler(WatchlistService watchlist, ITelegramGateway gateway)
{
    private readonly WatchlistService _watchlist = watchlist;
    private readonly ITelegramGateway _gateway = gateway;

    public async Task HandleWatchAsync(long chatId, string arguments, CancellationToken ct = default)
    {
        var (symbol, note) = SplitSymbolAndNote(arguments);
        if (symbol is null)
        {
            await Reply(chatId, "Usage: /watch SYMBOL [note]", ct);
            return;
        }

        var result = await _watchlist.AddAsync(symbol, note, ct);
        await Reply(chatId, DescribeAdd(result.Outcome, symbol), ct);
    }

    public async Task HandleUnwatchAsync(long chatId, string arguments, CancellationToken ct = default)
    {
        var (symbol, _) = SplitSymbolAndNote(arguments);
        if (symbol is null)
        {
            await Reply(chatId, "Usage: /unwatch SYMBOL", ct);
            return;
        }

        var removed = await _watchlist.RemoveAsync(symbol, ct);
        await Reply(
            chatId,
            removed ? $"Removed {symbol.ToUpperInvariant()} from your watchlist."
                    : $"{symbol.ToUpperInvariant()} was not on your watchlist.",
            ct);
    }

    public async Task HandleListAsync(long chatId, CancellationToken ct = default)
    {
        var entries = await _watchlist.GetAllAsync(ct);
        if (entries.Count == 0)
        {
            await Reply(chatId, "Your watchlist is empty.", ct);
            return;
        }

        var sb = new StringBuilder();
        sb.Append("Watchlist:");
        foreach (var entry in entries)
        {
            sb.Append('\n').Append("• ").Append(entry.Ticker.Value);
            if (!string.IsNullOrWhiteSpace(entry.Note))
            {
                sb.Append(" — ").Append(entry.Note);
            }
        }

        await Reply(chatId, sb.ToString(), ct);
    }

    private static string DescribeAdd(WatchlistAddOutcome outcome, string symbol)
    {
        var sym = symbol.ToUpperInvariant();
        return outcome switch
        {
            WatchlistAddOutcome.Added => $"Added {sym} to your watchlist.",
            WatchlistAddOutcome.AlreadyOnWatchlist => $"{sym} is already on your watchlist.",
            WatchlistAddOutcome.InvalidSymbol => $"'{symbol}' is not a valid ticker symbol.",
            WatchlistAddOutcome.WatchlistFull => "Your watchlist is full — remove a ticker before adding another.",
            WatchlistAddOutcome.NoteTooLong => "That note is too long — keep it under 280 characters.",
            WatchlistAddOutcome.AlreadyOwned => $"{sym} is already an owned position, so it was not added.",
            _ => "Unable to update your watchlist.",
        };
    }

    /// <summary>Splits the command arguments into the symbol and an optional free-text note.</summary>
    private static (string? Symbol, string? Note) SplitSymbolAndNote(string arguments)
    {
        var trimmed = arguments.Trim();
        if (trimmed.Length == 0)
        {
            return (null, null);
        }

        var space = trimmed.IndexOf(' ', StringComparison.Ordinal);
        if (space < 0)
        {
            return (trimmed, null);
        }

        var symbol = trimmed[..space];
        var note = trimmed[(space + 1)..].Trim();
        return (symbol, note.Length == 0 ? null : note);
    }

    private Task Reply(long chatId, string text, CancellationToken ct) =>
        _gateway.SendTextAsync(chatId, TelegramText.Escape(text), null, ct);
}
