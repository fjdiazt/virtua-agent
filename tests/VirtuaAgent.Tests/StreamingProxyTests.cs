using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VirtuaAgent.OpenAi;
using VirtuaAgent.Tracing;
using VirtuaAgent.Upstream;

namespace VirtuaAgent.Tests;

public sealed class StreamingProxyTests
{
    [Fact]
    public async Task StreamTrueReturnsOpenAiCompatibleEventStream()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IOpenAiCompatibleUpstreamClient>();
                    services.RemoveAll<ITraceStore>();
                    services.AddSingleton<IOpenAiCompatibleUpstreamClient>(new FakeStreamingUpstreamClient());
                    services.AddSingleton<ITraceStore>(new NoopTraceStore());
                });
            });

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/v1/chat/completions", new ChatCompletionRequest
        {
            Model = "local-model",
            Stream = true,
            Messages = [new ChatMessageDto { Role = "user", Content = "hello" }]
        }, JsonOptions.Default);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("text/event-stream", response.Content.Headers.ContentType!.MediaType);
        Assert.True(response.Headers.Contains("Virtua-Agent-Run-Id"));
        Assert.Contains(response.Headers.GetValues("Link"), value => value.Contains("rel=\"monitor\"", StringComparison.Ordinal));

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("""data: {"id":"chatcmpl_1","object":"chat.completion.chunk","choices":[{"delta":{"content":"hi"}}]}""", body);
        Assert.Contains("data: [DONE]", body);
    }

    private sealed class FakeStreamingUpstreamClient : IOpenAiCompatibleUpstreamClient
    {
        public Task<ModelListResponse> ListModelsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ModelListResponse());

        public Task<ChatCompletionResponse> ChatAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async Task StreamChatAsync(ChatCompletionRequest request, Stream output, CancellationToken cancellationToken = default)
        {
            var bytes = Encoding.UTF8.GetBytes("""
            data: {"id":"chatcmpl_1","object":"chat.completion.chunk","choices":[{"delta":{"content":"hi"}}]}

            data: [DONE]

            """);
            await output.WriteAsync(bytes, cancellationToken);
        }
    }

    private sealed class NoopTraceStore : ITraceStore
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CreateRunAsync(RunRecord run, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AppendEventAsync(string runId, TraceEventRecord traceEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AppendReasoningAsync(string runId, ReasoningRecord reasoning, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CompleteRunAsync(string runId, string responseJson, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task FailRunAsync(string runId, string errorJson, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<RunRecord?> GetRunAsync(string runId, CancellationToken cancellationToken = default) => Task.FromResult<RunRecord?>(null);
        public Task<IReadOnlyList<RunRecord>> ListRunsAsync(string? status, string? clientId, int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<RunRecord>>([]);
        public Task<int> ClearRunsAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }
}
