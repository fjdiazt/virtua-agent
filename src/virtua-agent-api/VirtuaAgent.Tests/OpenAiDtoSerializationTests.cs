using System.Text.Json;
using VirtuaAgent.OpenAi;

namespace VirtuaAgent.Tests;

public sealed class OpenAiDtoSerializationTests
{
    [Fact]
    public void ChatRequestDeserializesOpenAiFieldsAndVirtuaAgentExtension()
    {
        const string json = """
        {
          "model": "local-model",
          "messages": [{ "role": "user", "content": "hello" }],
          "temperature": 0.7,
          "max_tokens": 64,
          "stream": true,
          "orchestration": { "include_virtua_agent": true, "store": false }
        }
        """;

        var request = JsonSerializer.Deserialize<ChatCompletionRequest>(json, JsonOptions.Default)!;

        Assert.Equal("local-model", request.Model);
        Assert.True(request.Stream);
        Assert.True(request.Orchestration!.IncludeVirtuaAgent);
        Assert.False(request.Orchestration.Store);
    }
}
