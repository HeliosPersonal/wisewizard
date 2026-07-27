namespace WiseWizard.Core.Models;

/// <summary>
/// The traffic-light classification of a <see cref="Verdict"/>. Advisory only —
/// never a numeric price target and never an order.
/// </summary>
public enum Signal
{
    /// <summary>🟢 Keep holding the current Position.</summary>
    Hold,

    /// <summary>🟡 Something warrants the Owner's attention.</summary>
    Attention,

    /// <summary>🔴 The Owner should review this Position.</summary>
    Review,
}

/// <summary>
/// Conversions between <see cref="Signal"/> and its persisted / display representations.
/// The persisted form (hold/attention/review) is the contract used in the verdicts table.
/// </summary>
public static class SignalExtensions
{
    /// <summary>The lowercase token persisted in storage and used in the domain contract.</summary>
    public static string ToToken(this Signal signal) => signal switch
    {
        Signal.Hold => "hold",
        Signal.Attention => "attention",
        Signal.Review => "review",
        _ => throw new ArgumentOutOfRangeException(nameof(signal), signal, "Unknown signal."),
    };

    /// <summary>The emoji shown in the Daily digest.</summary>
    public static string ToEmoji(this Signal signal) => signal switch
    {
        Signal.Hold => "🟢",
        Signal.Attention => "🟡",
        Signal.Review => "🔴",
        _ => throw new ArgumentOutOfRangeException(nameof(signal), signal, "Unknown signal."),
    };

    /// <summary>Parses a persisted token back into a <see cref="Signal"/>.</summary>
    /// <exception cref="ArgumentException">Thrown when the token is not a known signal.</exception>
    public static Signal ParseSignal(string? token) => token?.Trim().ToLowerInvariant() switch
    {
        "hold" => Signal.Hold,
        "attention" => Signal.Attention,
        "review" => Signal.Review,
        _ => throw new ArgumentException($"Unknown signal token '{token}'.", nameof(token)),
    };
}
