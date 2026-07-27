using WiseWizard.Core.Models;
using WiseWizard.Infrastructure.Llm;
using WiseWizard.Infrastructure.Persistence;

namespace WiseWizard.Infrastructure.Tests;

/// <summary>Direct unit tests of the internal enum↔token mappers for full branch coverage.</summary>
public class PersistenceMappingTests
{
    [Theory]
    [InlineData(RunStatus.Pending, "pending")]
    [InlineData(RunStatus.Ingesting, "ingesting")]
    [InlineData(RunStatus.Extracting, "extracting")]
    [InlineData(RunStatus.Synthesizing, "synthesizing")]
    [InlineData(RunStatus.Persisting, "persisting")]
    [InlineData(RunStatus.Finished, "finished")]
    [InlineData(RunStatus.Failed, "failed")]
    public void RunStatus_round_trips(RunStatus status, string token)
    {
        Assert.Equal(token, RunRepository.ToToken(status));
        Assert.Equal(status, RunRepository.FromToken(token));
    }

    [Fact]
    public void RunStatus_ToToken_throws_on_unknown()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RunRepository.ToToken((RunStatus)99));
    }

    [Fact]
    public void RunStatus_FromToken_throws_on_unknown()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RunRepository.FromToken("bogus"));
    }

    [Theory]
    [InlineData(FactSentiment.Positive, "positive")]
    [InlineData(FactSentiment.Neutral, "neutral")]
    [InlineData(FactSentiment.Negative, "negative")]
    public void Sentiment_round_trips(FactSentiment sentiment, string token)
    {
        Assert.Equal(token, ExtractedFactRepository.ToSentimentToken(sentiment));
        Assert.Equal(sentiment, ExtractedFactRepository.FromSentimentToken(token));
    }

    [Theory]
    [InlineData(FactMateriality.Low, "low")]
    [InlineData(FactMateriality.Medium, "medium")]
    [InlineData(FactMateriality.High, "high")]
    public void Materiality_round_trips(FactMateriality materiality, string token)
    {
        Assert.Equal(token, ExtractedFactRepository.ToMaterialityToken(materiality));
        Assert.Equal(materiality, ExtractedFactRepository.FromMaterialityToken(token));
    }

    [Fact]
    public void Fact_mappers_throw_on_unknown()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ExtractedFactRepository.ToSentimentToken((FactSentiment)9));
        Assert.Throws<ArgumentOutOfRangeException>(() => ExtractedFactRepository.FromSentimentToken("x"));
        Assert.Throws<ArgumentOutOfRangeException>(() => ExtractedFactRepository.ToMaterialityToken((FactMateriality)9));
        Assert.Throws<ArgumentOutOfRangeException>(() => ExtractedFactRepository.FromMaterialityToken("x"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("null")]
    public void DeserializeBatchIds_empty_when_blank_or_null(string? json)
    {
        Assert.Empty(RunRepository.DeserializeBatchIds(json));
    }

    [Fact]
    public void DeserializeBatchIds_reads_map()
    {
        var map = RunRepository.DeserializeBatchIds("""{"cheap":"c1"}""");
        Assert.Equal("c1", map["cheap"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("null")]
    public void DeserializeSources_empty_when_blank_or_null(string? json)
    {
        Assert.Empty(VerdictRepository.DeserializeSources(json));
    }

    [Fact]
    public void DeserializeSources_reads_array()
    {
        Assert.Equal(new[] { "d1", "d2" }, VerdictRepository.DeserializeSources("""["d1","d2"]"""));
    }

    [Fact]
    public void AnthropicOptions_ModelFor_throws_on_unknown_tier()
    {
        var options = new AnthropicOptions { ApiKey = "k", CheapModel = "c", SynthesisModel = "s" };
        Assert.Throws<ArgumentOutOfRangeException>(() => options.ModelFor((Core.Abstractions.ModelTier)9));
    }
}
