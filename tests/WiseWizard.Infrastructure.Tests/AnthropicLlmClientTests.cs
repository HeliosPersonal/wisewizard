using System.Text.Json;
using WiseWizard.Core.Abstractions;
using WiseWizard.Infrastructure.Llm;

namespace WiseWizard.Infrastructure.Tests;

public class AnthropicLlmClientTests
{
    [Fact]
    public void BuildSubmitRequest_shapes_one_request_per_item()
    {
        var items = new[]
        {
            new BatchRequestItem { CustomId = "d1", Prompt = "prompt one" },
            new BatchRequestItem { CustomId = "d2", Prompt = "prompt two" },
        };

        var body = AnthropicLlmClient.BuildSubmitRequest("claude-haiku", 512, items);
        var json = body.ToJsonString();

        using var doc = JsonDocument.Parse(json);
        var requests = doc.RootElement.GetProperty("requests");
        Assert.Equal(2, requests.GetArrayLength());

        var first = requests[0];
        Assert.Equal("d1", first.GetProperty("custom_id").GetString());
        var prms = first.GetProperty("params");
        Assert.Equal("claude-haiku", prms.GetProperty("model").GetString());
        Assert.Equal(512, prms.GetProperty("max_tokens").GetInt32());
        var content = prms.GetProperty("messages")[0];
        Assert.Equal("user", content.GetProperty("role").GetString());
        Assert.Equal("prompt one", content.GetProperty("content").GetString());
    }

    [Fact]
    public void ParseBatchId_reads_id()
    {
        var json = """{"id":"msgbatch_123","type":"message_batch","processing_status":"in_progress"}""";
        Assert.Equal("msgbatch_123", AnthropicLlmClient.ParseBatchId(json));
    }

    [Fact]
    public void ParseBatchId_throws_when_missing()
    {
        Assert.Throws<KeyNotFoundException>(() => AnthropicLlmClient.ParseBatchId("""{"type":"x"}"""));
    }

    [Fact]
    public void ParseStatus_in_progress()
    {
        var json = """{"id":"b","processing_status":"in_progress"}""";
        Assert.Equal(BatchStatus.InProgress, AnthropicLlmClient.ParseStatus(json));
    }

    [Fact]
    public void ParseStatus_ended_all_succeeded_is_completed()
    {
        var json = """
            {"id":"b","processing_status":"ended",
             "request_counts":{"processing":0,"succeeded":3,"errored":0,"canceled":0,"expired":0}}
            """;
        Assert.Equal(BatchStatus.Completed, AnthropicLlmClient.ParseStatus(json));
    }

    [Fact]
    public void ParseStatus_ended_with_errors_is_failed()
    {
        var json = """
            {"id":"b","processing_status":"ended",
             "request_counts":{"processing":0,"succeeded":2,"errored":1,"canceled":0,"expired":0}}
            """;
        Assert.Equal(BatchStatus.Failed, AnthropicLlmClient.ParseStatus(json));
    }

    [Fact]
    public void ParseStatus_ended_with_expired_is_failed()
    {
        var json = """
            {"id":"b","processing_status":"ended",
             "request_counts":{"succeeded":0,"errored":0,"canceled":0,"expired":2}}
            """;
        Assert.Equal(BatchStatus.Failed, AnthropicLlmClient.ParseStatus(json));
    }

    [Fact]
    public void ParseStatus_ended_without_counts_is_completed()
    {
        var json = """{"id":"b","processing_status":"ended"}""";
        Assert.Equal(BatchStatus.Completed, AnthropicLlmClient.ParseStatus(json));
    }

    [Fact]
    public void ParseStatus_canceling_is_failed()
    {
        var json = """{"id":"b","processing_status":"canceling"}""";
        Assert.Equal(BatchStatus.Failed, AnthropicLlmClient.ParseStatus(json));
    }

    [Fact]
    public void ParseResults_maps_succeeded_lines_with_text_and_tokens()
    {
        var jsonl =
            """{"custom_id":"d1","result":{"type":"succeeded","message":{"content":[{"type":"text","text":"hello"}],"usage":{"input_tokens":12,"output_tokens":7}}}}""" +
            "\n" +
            """{"custom_id":"d2","result":{"type":"succeeded","message":{"content":[{"type":"text","text":"world"}],"usage":{"input_tokens":3,"output_tokens":1}}}}""" +
            "\n";

        var results = AnthropicLlmClient.ParseResults(jsonl);

        Assert.Equal(2, results.Count);
        Assert.Equal("d1", results[0].CustomId);
        Assert.Equal("hello", results[0].Text);
        Assert.Equal(12, results[0].InputTokens);
        Assert.Equal(7, results[0].OutputTokens);
        Assert.Equal("world", results[1].Text);
    }

    [Fact]
    public void ParseResults_skips_errored_lines_and_blank_lines()
    {
        var jsonl =
            "\n" +
            """{"custom_id":"d1","result":{"type":"errored","error":{"type":"x"}}}""" +
            "\n" +
            """{"custom_id":"d2","result":{"type":"succeeded","message":{"content":[{"type":"text","text":"ok"}],"usage":{"input_tokens":1,"output_tokens":1}}}}""";

        var results = AnthropicLlmClient.ParseResults(jsonl);

        var single = Assert.Single(results);
        Assert.Equal("d2", single.CustomId);
    }

    [Fact]
    public void ParseResults_handles_missing_usage_and_non_text_content()
    {
        var jsonl =
            """{"custom_id":"d1","result":{"type":"succeeded","message":{"content":[{"type":"tool_use","id":"t"}]}}}""";

        var results = AnthropicLlmClient.ParseResults(jsonl);

        var single = Assert.Single(results);
        Assert.Equal(string.Empty, single.Text);
        Assert.Equal(0, single.InputTokens);
        Assert.Equal(0, single.OutputTokens);
    }

    [Fact]
    public void ParseStatus_missing_processing_status_is_in_progress()
    {
        Assert.Equal(BatchStatus.InProgress, AnthropicLlmClient.ParseStatus("""{"id":"b"}"""));
    }

    [Fact]
    public void ParseStatus_ended_with_partial_counts_is_completed()
    {
        // request_counts present but missing errored/expired/canceled keys → treated as 0.
        var json = """{"id":"b","processing_status":"ended","request_counts":{"succeeded":1}}""";
        Assert.Equal(BatchStatus.Completed, AnthropicLlmClient.ParseStatus(json));
    }

    [Fact]
    public void ParseResults_line_without_result_type_is_skipped()
    {
        var jsonl = """{"custom_id":"d1","result":{"message":{"content":[{"type":"text","text":"x"}]}}}""";
        Assert.Empty(AnthropicLlmClient.ParseResults(jsonl));
    }

    [Fact]
    public void ParseResults_usage_missing_token_fields_defaults_zero()
    {
        var jsonl =
            """{"custom_id":"d1","result":{"type":"succeeded","message":{"content":[{"type":"text","text":"x"}],"usage":{}}}}""";
        var single = Assert.Single(AnthropicLlmClient.ParseResults(jsonl));
        Assert.Equal(0, single.InputTokens);
        Assert.Equal(0, single.OutputTokens);
    }

    [Fact]
    public void ParseResults_text_block_missing_text_property_yields_empty()
    {
        var jsonl =
            """{"custom_id":"d1","result":{"type":"succeeded","message":{"content":[{"type":"text"}]}}}""";
        Assert.Equal(string.Empty, Assert.Single(AnthropicLlmClient.ParseResults(jsonl)).Text);
    }

    [Fact]
    public void ParseResults_handles_message_without_content_array()
    {
        var jsonl =
            """{"custom_id":"d1","result":{"type":"succeeded","message":{"usage":{"input_tokens":5,"output_tokens":0}}}}""";

        var single = Assert.Single(AnthropicLlmClient.ParseResults(jsonl));

        Assert.Equal(string.Empty, single.Text);
        Assert.Equal(5, single.InputTokens);
    }

    [Fact]
    public void ParseResults_succeeded_without_message_is_skipped()
    {
        var jsonl = """{"custom_id":"d1","result":{"type":"succeeded"}}""";
        Assert.Empty(AnthropicLlmClient.ParseResults(jsonl));
    }

    [Fact]
    public void AnthropicOptions_resolves_model_per_tier()
    {
        var options = new AnthropicOptions
        {
            ApiKey = "k",
            CheapModel = "haiku",
            SynthesisModel = "sonnet",
        };

        Assert.Equal("haiku", options.ModelFor(ModelTier.Cheap));
        Assert.Equal("sonnet", options.ModelFor(ModelTier.Synthesis));
    }
}
