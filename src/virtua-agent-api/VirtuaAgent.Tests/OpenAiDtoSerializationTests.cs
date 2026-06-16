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
          "top_p": 0.8,
          "top_k": 40,
          "min_p": 0.05,
          "repeat_penalty": 1.1,
          "max_tokens": 64,
          "stream": true,
          "orchestration": { "include_virtua_agent": true, "store": false }
        }
        """;

        var request = JsonSerializer.Deserialize<ChatCompletionRequest>(json, JsonOptions.Default)!;

        Assert.Equal("local-model", request.Model);
        Assert.Equal(0.8, request.TopP);
        Assert.Equal(40, request.TopK);
        Assert.Equal(0.05, request.MinP);
        Assert.Equal(1.1, request.RepeatPenalty);
        Assert.True(request.Stream);
        Assert.True(request.Orchestration!.IncludeVirtuaAgent);
        Assert.False(request.Orchestration.Store);

        var serialized = JsonSerializer.Serialize(request, JsonOptions.Default);
        Assert.Contains("\"top_p\":0.8", serialized, StringComparison.Ordinal);
        Assert.Contains("\"top_k\":40", serialized, StringComparison.Ordinal);
        Assert.Contains("\"min_p\":0.05", serialized, StringComparison.Ordinal);
        Assert.Contains("\"repeat_penalty\":1.1", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public void ChatRequestDeserializesPipelineSamplingFields()
    {
        const string json = """
        {
          "model": "virtua-agent/editor",
          "messages": [{ "role": "user", "content": "hello" }],
          "orchestration": {
            "pipeline": {
              "default_top_p": 0.9,
              "default_top_k": 40,
              "default_min_p": 0.05,
              "default_repeat_penalty": 1.1,
              "stages": [
                {
                  "type": "single_agent",
                  "agent": {
                    "top_p": 0.8,
                    "top_k": 32,
                    "min_p": 0.03,
                    "repeat_penalty": 1.2
                  }
                }
              ]
            }
          }
        }
        """;

        var request = JsonSerializer.Deserialize<ChatCompletionRequest>(json, JsonOptions.Default)!;
        var pipeline = request.Orchestration!.Pipeline!;
        var agent = pipeline.Stages[0].Agent!;

        Assert.Equal(0.9, pipeline.DefaultTopP);
        Assert.Equal(40, pipeline.DefaultTopK);
        Assert.Equal(0.05, pipeline.DefaultMinP);
        Assert.Equal(1.1, pipeline.DefaultRepeatPenalty);
        Assert.Equal(0.8, agent.TopP);
        Assert.Equal(32, agent.TopK);
        Assert.Equal(0.03, agent.MinP);
        Assert.Equal(1.2, agent.RepeatPenalty);
    }
}
