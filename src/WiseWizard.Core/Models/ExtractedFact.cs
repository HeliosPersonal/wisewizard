namespace WiseWizard.Core.Models;

/// <summary>Cheap-tier sentiment classification of an <see cref="ExtractedFact"/>.</summary>
public enum FactSentiment
{
    Positive,
    Neutral,
    Negative,
}

/// <summary>Cheap-tier materiality band of an <see cref="ExtractedFact"/>.</summary>
public enum FactMateriality
{
    Low,
    Medium,
    High,
}

/// <summary>
/// A structured statement distilled by the cheap-tier model from one Raw document:
/// what was said about a Ticker, its sentiment and how material it is. The
/// <see cref="DocumentId"/> is the evidence link a Verdict cites.
/// </summary>
public sealed record ExtractedFact
{
    public long Id { get; init; }
    public required long RunId { get; init; }
    public required string DocumentId { get; init; }
    public required Ticker Ticker { get; init; }
    public required string Fact { get; init; }
    public required FactSentiment Sentiment { get; init; }
    public required FactMateriality Materiality { get; init; }
}
