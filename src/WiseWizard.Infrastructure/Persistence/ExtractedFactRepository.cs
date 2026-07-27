using Dapper;
using WiseWizard.Core.Abstractions;
using WiseWizard.Core.Models;

namespace WiseWizard.Infrastructure.Persistence;

/// <summary>
/// Dapper/PostgreSQL implementation of <see cref="IExtractedFactRepository"/> over the
/// <c>extracted_facts</c> table. <see cref="AddRangeAsync"/> inserts every fact inside a single
/// transaction; sentiment and materiality map to/from the fixed text tokens in data-model.md.
/// </summary>
public sealed class ExtractedFactRepository(IDbConnectionFactory factory) : IExtractedFactRepository
{
    private readonly IDbConnectionFactory _factory = factory;

    public async Task AddRangeAsync(IReadOnlyList<ExtractedFact> facts, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(facts);

        if (facts.Count == 0)
        {
            return;
        }

        await using var connection = await _factory.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        const string sql = """
            INSERT INTO extracted_facts (run_id, document_id, ticker, fact, sentiment, materiality)
            VALUES (@RunId, @DocumentId, @Ticker, @Fact, @Sentiment, @Materiality);
            """;

        foreach (var fact in facts)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new
                {
                    fact.RunId,
                    fact.DocumentId,
                    Ticker = fact.Ticker.Value,
                    fact.Fact,
                    Sentiment = ToSentimentToken(fact.Sentiment),
                    Materiality = ToMaterialityToken(fact.Materiality),
                },
                transaction: transaction,
                cancellationToken: ct));
        }

        await transaction.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<ExtractedFact>> GetForRunTickerAsync(
        long runId, Ticker ticker, CancellationToken ct = default)
    {
        await using var connection = await _factory.OpenAsync(ct);

        const string sql = """
            SELECT id AS Id, run_id AS RunId, document_id AS DocumentId, ticker AS Ticker,
                   fact AS Fact, sentiment AS Sentiment, materiality AS Materiality
            FROM extracted_facts
            WHERE run_id = @RunId AND ticker = @Ticker
            ORDER BY id;
            """;

        var rows = await connection.QueryAsync<FactRow>(new CommandDefinition(
            sql,
            new { RunId = runId, Ticker = ticker.Value },
            cancellationToken: ct));

        return rows.Select(Map).ToList();
    }

    internal static string ToSentimentToken(FactSentiment sentiment) => sentiment switch
    {
        FactSentiment.Positive => "positive",
        FactSentiment.Neutral => "neutral",
        FactSentiment.Negative => "negative",
        _ => throw new ArgumentOutOfRangeException(nameof(sentiment), sentiment, "Unknown sentiment."),
    };

    internal static FactSentiment FromSentimentToken(string token) => token switch
    {
        "positive" => FactSentiment.Positive,
        "neutral" => FactSentiment.Neutral,
        "negative" => FactSentiment.Negative,
        _ => throw new ArgumentOutOfRangeException(nameof(token), token, "Unknown sentiment token."),
    };

    internal static string ToMaterialityToken(FactMateriality materiality) => materiality switch
    {
        FactMateriality.Low => "low",
        FactMateriality.Medium => "medium",
        FactMateriality.High => "high",
        _ => throw new ArgumentOutOfRangeException(nameof(materiality), materiality, "Unknown materiality."),
    };

    internal static FactMateriality FromMaterialityToken(string token) => token switch
    {
        "low" => FactMateriality.Low,
        "medium" => FactMateriality.Medium,
        "high" => FactMateriality.High,
        _ => throw new ArgumentOutOfRangeException(nameof(token), token, "Unknown materiality token."),
    };

    private static ExtractedFact Map(FactRow r) => new()
    {
        Id = r.Id,
        RunId = r.RunId,
        DocumentId = r.DocumentId,
        Ticker = Ticker.Create(r.Ticker),
        Fact = r.Fact,
        Sentiment = FromSentimentToken(r.Sentiment),
        Materiality = FromMaterialityToken(r.Materiality),
    };

    private sealed record FactRow
    {
        public long Id { get; init; }
        public long RunId { get; init; }
        public string DocumentId { get; init; } = string.Empty;
        public string Ticker { get; init; } = string.Empty;
        public string Fact { get; init; } = string.Empty;
        public string Sentiment { get; init; } = string.Empty;
        public string Materiality { get; init; } = string.Empty;
    }
}
