namespace WiseWizard.Core.Models;

/// <summary>
/// A stock/ETF symbol identifying a security (e.g. AAPL, VOO). The canonical key for
/// grouping data across the system. Always stored normalized (trimmed + uppercased).
/// </summary>
public readonly record struct Ticker
{
    /// <summary>The normalized symbol value.</summary>
    public string Value { get; }

    private Ticker(string value) => Value = value;

    /// <summary>
    /// Validates and normalizes a raw symbol string. A valid symbol is 1-10 characters
    /// after trimming, containing only ASCII letters, digits, '.' or '-'. Normalization
    /// trims surrounding whitespace and uppercases the result.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the symbol is null, empty, too long, or has invalid characters.</exception>
    public static Ticker Create(string? raw)
    {
        if (!TryValidate(raw, out var normalized, out var error))
        {
            throw new ArgumentException(error, nameof(raw));
        }

        return new Ticker(normalized);
    }

    /// <summary>
    /// Attempts to validate and normalize a raw symbol without throwing.
    /// </summary>
    public static bool TryCreate(string? raw, out Ticker ticker)
    {
        if (!TryValidate(raw, out var normalized, out _))
        {
            ticker = default;
            return false;
        }

        ticker = new Ticker(normalized);
        return true;
    }

    /// <summary>
    /// Shared validation/normalization used by both <see cref="Create"/> and <see cref="TryCreate"/>,
    /// so the no-throw path costs nothing (no exception for control flow).
    /// </summary>
    private static bool TryValidate(string? raw, out string normalized, out string? error)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "Ticker symbol must not be empty.";
            return false;
        }

        var candidate = raw.Trim().ToUpperInvariant();

        if (candidate.Length > 10)
        {
            error = $"Ticker symbol '{candidate}' exceeds 10 characters.";
            return false;
        }

        foreach (var c in candidate)
        {
            var ok = (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '.' || c == '-';
            if (!ok)
            {
                error = $"Ticker symbol '{candidate}' contains invalid character '{c}'.";
                return false;
            }
        }

        normalized = candidate;
        error = null;
        return true;
    }

    public override string ToString() => Value;
}
