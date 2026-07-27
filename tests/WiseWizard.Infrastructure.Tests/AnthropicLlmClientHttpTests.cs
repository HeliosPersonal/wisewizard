using System.Net;
using System.Text;
using WiseWizard.Core.Abstractions;
using WiseWizard.Infrastructure.Llm;

namespace WiseWizard.Infrastructure.Tests;

public class AnthropicLlmClientHttpTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(responder(request));
        }
    }

    private static HttpResponseMessage Ok(string body)
        => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static AnthropicOptions Options() => new()
    {
        ApiKey = "test-key",
        CheapModel = "haiku",
        SynthesisModel = "sonnet",
        CheapInputPerMillionUsd = 0.25m,
        CheapOutputPerMillionUsd = 1.25m,
        SynthesisInputPerMillionUsd = 3m,
        SynthesisOutputPerMillionUsd = 15m,
        MaxTokens = 256,
    };

    private static (AnthropicLlmClient client, StubHandler handler) Build(
        Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new StubHandler(responder);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.test") };
        return (new AnthropicLlmClient(http, Options()), handler);
    }

    [Fact]
    public async Task SubmitBatch_posts_and_returns_id()
    {
        var (client, handler) = Build(_ => Ok("""{"id":"msgbatch_1","processing_status":"in_progress"}"""));

        var id = await client.SubmitBatchAsync(
            ModelTier.Cheap,
            [new BatchRequestItem { CustomId = "d1", Prompt = "p" }]);

        Assert.Equal("msgbatch_1", id);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(AnthropicLlmClient.BatchesPath, request.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetBatchStatus_gets_and_maps()
    {
        var (client, handler) = Build(_ => Ok("""{"id":"b","processing_status":"in_progress"}"""));

        var status = await client.GetBatchStatusAsync("b");

        Assert.Equal(BatchStatus.InProgress, status);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.EndsWith("/b", request.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetBatchResults_gets_results_path_and_maps()
    {
        var jsonl =
            """{"custom_id":"d1","result":{"type":"succeeded","message":{"content":[{"type":"text","text":"hi"}],"usage":{"input_tokens":1,"output_tokens":2}}}}""";
        var (client, handler) = Build(_ => Ok(jsonl));

        var results = await client.GetBatchResultsAsync("b");

        var single = Assert.Single(results);
        Assert.Equal("hi", single.Text);
        var request = Assert.Single(handler.Requests);
        Assert.EndsWith("/b/results", request.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task SubmitBatch_throws_on_http_error()
    {
        var (client, _) = Build(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.SubmitBatchAsync(ModelTier.Synthesis, [new BatchRequestItem { CustomId = "d", Prompt = "p" }]));
    }
}
