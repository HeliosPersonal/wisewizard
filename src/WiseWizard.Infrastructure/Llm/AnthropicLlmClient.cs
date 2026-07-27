using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using WiseWizard.Core.Abstractions;

namespace WiseWizard.Infrastructure.Llm;

/// <summary>
/// <see cref="ILlmClient"/> implemented over the Anthropic Message Batches API using
/// <see cref="HttpClient"/> and System.Text.Json (ADR-0005). All request-building and
/// response-parsing lives in the <c>internal static</c> pure methods so it can be unit-tested
/// against saved fixtures with zero network; the HTTP plumbing here is a thin shell.
/// </summary>
public sealed class AnthropicLlmClient(HttpClient httpClient, AnthropicOptions options) : ILlmClient
{
    internal const string BatchesPath = "/v1/messages/batches";

    private readonly HttpClient _httpClient = httpClient;
    private readonly AnthropicOptions _options = options;

    public async Task<string> SubmitBatchAsync(
        ModelTier tier, IReadOnlyList<BatchRequestItem> items, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(items);

        var body = BuildSubmitRequest(_options.ModelFor(tier), _options.MaxTokens, items);

        using var response = await _httpClient.PostAsJsonAsync(BatchesPath, body, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return ParseBatchId(json);
    }

    public async Task<BatchStatus> GetBatchStatusAsync(string batchId, CancellationToken ct = default)
    {
        using var response = await _httpClient.GetAsync($"{BatchesPath}/{batchId}", ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return ParseStatus(json);
    }

    public async Task<IReadOnlyList<BatchResultItem>> GetBatchResultsAsync(
        string batchId, CancellationToken ct = default)
    {
        // The results endpoint returns a JSON-Lines stream (one result object per line).
        using var response = await _httpClient.GetAsync($"{BatchesPath}/{batchId}/results", ct);
        response.EnsureSuccessStatusCode();

        var jsonl = await response.Content.ReadAsStringAsync(ct);
        return ParseResults(jsonl);
    }

    /// <summary>Builds the Message Batches submit request body for a model + a set of items.</summary>
    internal static JsonObject BuildSubmitRequest(
        string model, int maxTokens, IReadOnlyList<BatchRequestItem> items)
    {
        var requests = new JsonArray();
        foreach (var item in items)
        {
            requests.Add(new JsonObject
            {
                ["custom_id"] = item.CustomId,
                ["params"] = new JsonObject
                {
                    ["model"] = model,
                    ["max_tokens"] = maxTokens,
                    ["messages"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["role"] = "user",
                            ["content"] = item.Prompt,
                        },
                    },
                },
            });
        }

        return new JsonObject { ["requests"] = requests };
    }

    /// <summary>Extracts the batch id from a submit/status response body.</summary>
    internal static string ParseBatchId(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("Anthropic batch response had no id.");
    }

    /// <summary>Maps the <c>processing_status</c> field to a <see cref="BatchStatus"/>.</summary>
    internal static BatchStatus ParseStatus(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var processing = root.TryGetProperty("processing_status", out var ps) ? ps.GetString() : null;

        // "ended" means the batch finished processing; whether any request failed is decided by
        // the per-request result counts.
        if (processing == "ended")
        {
            if (root.TryGetProperty("request_counts", out var counts))
            {
                var errored = counts.TryGetProperty("errored", out var e) ? e.GetInt64() : 0;
                var expired = counts.TryGetProperty("expired", out var x) ? x.GetInt64() : 0;
                var canceled = counts.TryGetProperty("canceled", out var c) ? c.GetInt64() : 0;
                if (errored + expired + canceled > 0)
                {
                    return BatchStatus.Failed;
                }
            }

            return BatchStatus.Completed;
        }

        if (processing == "canceling")
        {
            return BatchStatus.Failed;
        }

        // "in_progress" or anything not yet ended.
        return BatchStatus.InProgress;
    }

    /// <summary>Parses the JSON-Lines results stream into correlated <see cref="BatchResultItem"/>s.</summary>
    internal static IReadOnlyList<BatchResultItem> ParseResults(string jsonl)
    {
        var results = new List<BatchResultItem>();

        foreach (var rawLine in jsonl.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            var customId = root.GetProperty("custom_id").GetString()
                ?? throw new InvalidOperationException("Batch result line had no custom_id.");

            var result = root.GetProperty("result");
            var resultType = result.TryGetProperty("type", out var t) ? t.GetString() : null;

            // Skip non-succeeded results (errored/expired/canceled) — the item simply has no output.
            if (resultType != "succeeded" || !result.TryGetProperty("message", out var message))
            {
                continue;
            }

            var text = ExtractText(message);
            long inputTokens = 0;
            long outputTokens = 0;
            if (message.TryGetProperty("usage", out var usage))
            {
                inputTokens = usage.TryGetProperty("input_tokens", out var it) ? it.GetInt64() : 0;
                outputTokens = usage.TryGetProperty("output_tokens", out var ot) ? ot.GetInt64() : 0;
            }

            results.Add(new BatchResultItem
            {
                CustomId = customId,
                Text = text,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
            });
        }

        return results;
    }

    private static string ExtractText(JsonElement message)
    {
        if (!message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        foreach (var block in content.EnumerateArray())
        {
            if (block.TryGetProperty("type", out var type) && type.GetString() == "text"
                && block.TryGetProperty("text", out var textEl))
            {
                return textEl.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }
}
