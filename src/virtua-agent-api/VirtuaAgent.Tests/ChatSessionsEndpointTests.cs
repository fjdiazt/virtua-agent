using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VirtuaAgent.ChatSessions;
using VirtuaAgent.OpenAi;

namespace VirtuaAgent.Tests;

public sealed class ChatSessionsEndpointTests
{
    [Fact]
    public async Task AppendCurrentMessageAsyncPersistsRowsInOrder()
    {
        await using var store = new SqliteChatSessionStore("Data Source=:memory:");
        await store.InitializeAsync();

        await store.AppendCurrentMessageAsync(new SaveChatSessionMessageRequest { Role = "user", Content = "first" });
        await store.AppendCurrentMessageAsync(new SaveChatSessionMessageRequest
        {
            Role = "assistant",
            Content = "second",
            Reasoning = new Dictionary<string, string> { ["Model reasoning"] = "thought" }
        });

        var messages = await store.ListCurrentMessagesAsync();

        Assert.Equal(["first", "second"], messages.Select(message => message.Content));
        Assert.Equal("thought", messages[1].Reasoning!["Model reasoning"]);
    }

    [Fact]
    public async Task ClearCurrentMessagesAsyncDeletesCurrentRows()
    {
        await using var store = new SqliteChatSessionStore("Data Source=:memory:");
        await store.InitializeAsync();
        await store.AppendCurrentMessageAsync(new SaveChatSessionMessageRequest { Role = "user", Content = "hello" });

        var deleted = await store.ClearCurrentMessagesAsync();
        var messages = await store.ListCurrentMessagesAsync();

        Assert.Equal(1, deleted);
        Assert.Empty(messages);
    }

    [Fact]
    public async Task GetMessagesReturnsSavedMessages()
    {
        var store = new InMemoryChatSessionStore();
        await store.AppendCurrentMessageAsync(new SaveChatSessionMessageRequest { Role = "user", Content = "hello" });
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IChatSessionStore>();
                    services.AddSingleton<IChatSessionStore>(store);
                });
            });

        var response = await factory.CreateClient().GetAsync("/v1/chat-sessions/current/messages");
        var messages = await response.Content.ReadFromJsonAsync<List<ChatSessionMessageDto>>(JsonOptions.Default);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("hello", messages!.Single().Content);
    }

    [Fact]
    public async Task PostMessageValidatesRole()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IChatSessionStore>();
                    services.AddSingleton<IChatSessionStore>(new InMemoryChatSessionStore());
                });
            });

        var response = await factory.CreateClient().PostAsJsonAsync("/v1/chat-sessions/current/messages", new SaveChatSessionMessageRequest
        {
            Role = "system",
            Content = "hello"
        }, JsonOptions.Default);
        var error = await response.Content.ReadFromJsonAsync<OpenAiErrorResponse>(JsonOptions.Default);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_chat_role", error!.Error.Code);
    }

    [Fact]
    public async Task DeleteMessagesClearsSavedMessages()
    {
        var store = new InMemoryChatSessionStore();
        await store.AppendCurrentMessageAsync(new SaveChatSessionMessageRequest { Role = "user", Content = "hello" });
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IChatSessionStore>();
                    services.AddSingleton<IChatSessionStore>(store);
                });
            });

        var deleteResponse = await factory.CreateClient().DeleteAsync("/v1/chat-sessions/current/messages");
        var messages = await store.ListCurrentMessagesAsync();

        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        Assert.Empty(messages);
    }

    private sealed class InMemoryChatSessionStore : IChatSessionStore
    {
        private readonly List<ChatSessionMessageDto> _messages = [];

        public Task<IReadOnlyList<ChatSessionMessageDto>> ListCurrentMessagesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ChatSessionMessageDto>>(_messages.OrderBy(message => message.CreatedAt).ToList());

        public Task<ChatSessionMessageDto> AppendCurrentMessageAsync(SaveChatSessionMessageRequest request, CancellationToken cancellationToken = default)
        {
            var message = new ChatSessionMessageDto
            {
                Id = "msg_" + Guid.NewGuid().ToString("N"),
                Role = request.Role,
                Content = request.Content,
                Reasoning = request.Reasoning,
                CreatedAt = DateTimeOffset.UtcNow.AddTicks(_messages.Count)
            };
            _messages.Add(message);
            return Task.FromResult(message);
        }

        public Task<int> ClearCurrentMessagesAsync(CancellationToken cancellationToken = default)
        {
            var count = _messages.Count;
            _messages.Clear();
            return Task.FromResult(count);
        }
    }
}
