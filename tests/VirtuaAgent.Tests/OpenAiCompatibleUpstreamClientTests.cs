using RichardSzalay.MockHttp;
using VirtuaAgent.OpenAi;
using VirtuaAgent.Upstream;
using Microsoft.Extensions.Options;

namespace VirtuaAgent.Tests;

public sealed class OpenAiCompatibleUpstreamClientTests
{
    [Fact]
    public async Task ListModelsAsyncGetsAndParsesModels()
    {
        var mock = new MockHttpMessageHandler();
        mock.When(HttpMethod.Get, "http://upstream.test/v1/models")
            .Respond("application/json", """
            {
              "object": "list",
              "data": [
                { "id": "local-model", "object": "model", "created": 1, "owned_by": "local" }
              ]
            }
            """);

        var client = new OpenAiCompatibleUpstreamClient(
            new HttpClient(mock) { BaseAddress = new Uri("http://upstream.test") },
            Options.Create(new UpstreamOptions()));

        var response = await client.ListModelsAsync();

        Assert.Equal("local-model", response.Data[0].Id);
    }

    [Fact]
    public async Task ChatAsyncPostsOpenAiRequestAndParsesResponse()
    {
        var mock = new MockHttpMessageHandler();
        mock.When(HttpMethod.Post, "http://upstream.test/v1/chat/completions")
            .WithPartialContent(""""model":"local-model"""")
            .Respond("application/json", """
            {
              "id": "chatcmpl_upstream",
              "object": "chat.completion",
              "created": 1,
              "model": "local-model",
              "choices": [{ "index": 0, "message": { "role": "assistant", "content": "answer" }, "finish_reason": "stop" }]
            }
            """);

        var client = new OpenAiCompatibleUpstreamClient(
            new HttpClient(mock) { BaseAddress = new Uri("http://upstream.test") },
            Options.Create(new UpstreamOptions()));

        var response = await client.ChatAsync(new ChatCompletionRequest
        {
            Model = "local-model",
            Messages = [new ChatMessageDto { Role = "user", Content = "hello" }]
        });

        Assert.Equal("answer", response.Choices[0].Message.Content);
    }

    [Fact]
    public async Task StreamChatAsyncThrowsUpstreamMessageWhenRequestFails()
    {
        var mock = new MockHttpMessageHandler();
        mock.When(HttpMethod.Post, "http://upstream.test/v1/chat/completions")
            .Respond(System.Net.HttpStatusCode.BadRequest, "application/json", """
            {
              "error": {
                "message": "request exceeds the available context size",
                "type": "exceed_context_size_error",
                "code": 400
              }
            }
            """);

        var client = new OpenAiCompatibleUpstreamClient(
            new HttpClient(mock) { BaseAddress = new Uri("http://upstream.test") },
            Options.Create(new UpstreamOptions()));

        var ex = await Assert.ThrowsAsync<UpstreamRequestException>(() =>
            client.StreamChatAsync(new ChatCompletionRequest
            {
                Model = "local-model",
                Messages = [new ChatMessageDto { Role = "user", Content = "hello" }]
            }, Stream.Null));

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Contains("request exceeds the available context size", ex.Message, StringComparison.Ordinal);
        Assert.Equal("exceed_context_size_error", ex.UpstreamErrorType);
    }
}
