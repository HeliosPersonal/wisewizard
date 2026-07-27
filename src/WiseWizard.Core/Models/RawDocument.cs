namespace WiseWizard.Core.Models;

/// <summary>The origin category of a <see cref="RawDocument"/>.</summary>
public enum SourceKind
{
    SecFiling,
    News,
    MarketData,
}

/// <summary>
/// A single unprocessed item collected from a Source during ingestion (a news article,
/// a filing, a metrics snapshot), keyed to a Ticker and a Run.
/// </summary>
public sealed record RawDocument
{
    /// <summary>Stable id for the document; also the citation key referenced by a Verdict.</summary>
    public required string DocumentId { get; init; }
    public required long RunId { get; init; }
    public required Ticker Ticker { get; init; }
    public required SourceKind Source { get; init; }
    public string? Url { get; init; }
    public required string Title { get; init; }
    public required string Content { get; init; }
    public DateTimeOffset? PublishedAt { get; init; }
    public required DateTimeOffset FetchedAt { get; init; }

    /// <summary>Hash of the salient content, used to deduplicate within a Run.</summary>
    public required string ContentHash { get; init; }
}
