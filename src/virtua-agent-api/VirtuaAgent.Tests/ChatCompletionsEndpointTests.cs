using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VirtuaAgent.ModelEndpoints;
using VirtuaAgent.PipelineModels;
using VirtuaAgent.OpenAi;
using VirtuaAgent.Tracing;
using VirtuaAgent.Upstream;

namespace VirtuaAgent.Tests;

public sealed class ChatCompletionsEndpointTests
{
    [Fact]
    public async Task PostChatCompletionsReturnsOpenAiResponseWithVirtuaAgentHeaders()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IOpenAiCompatibleUpstreamClient>();
                    services.RemoveAll<ITraceStore>();
                    services.AddSingleton<IOpenAiCompatibleUpstreamClient>(new FakeUpstreamClient());
                    services.AddSingleton<ITraceStore>(new RecordingTraceStore());
                });
            });

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/v1/chat/completions", new ChatCompletionRequest
        {
            Model = "local-model",
            Messages = [new ChatMessageDto { Role = "user", Content = "hello" }]
        }, JsonOptions.Default);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("Virtua-Agent-Run-Id"));
        Assert.Contains(response.Headers.GetValues("Link"), value => value.Contains("rel=\"monitor\"", StringComparison.Ordinal));

        var body = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(JsonOptions.Default);
        Assert.Equal("answer", body!.Choices[0].Message.Content);
        Assert.Null(body.VirtuaAgent);
    }

    [Fact]
    public async Task PostChatCompletionsUsesSelectedEndpoint()
    {
        var upstream = new FakeUpstreamClient();
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IOpenAiCompatibleUpstreamClient>();
                    services.RemoveAll<ITraceStore>();
                    services.RemoveAll<IModelEndpointStore>();
                    services.AddSingleton<IOpenAiCompatibleUpstreamClient>(upstream);
                    services.AddSingleton<ITraceStore>(new RecordingTraceStore());
                    services.AddSingleton<IModelEndpointStore>(new InMemoryModelEndpointStore(
                        new ModelEndpointDefinition { Id = "llamacpp", Name = "llama.cpp", BaseUrl = "http://llama.test" }));
                });
            });

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/v1/chat/completions", new ChatCompletionRequest
        {
            EndpointId = "llamacpp",
            Model = "local-model",
            Messages = [new ChatMessageDto { Role = "user", Content = "hello" }]
        }, JsonOptions.Default);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("llamacpp", upstream.EndpointIds.Single());
        Assert.Null(upstream.Requests[0].EndpointId);
    }

    [Fact]
    public async Task MultimodalRequestTraceRedactsImagePayload()
    {
        var traceStore = new RecordingTraceStore();
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IOpenAiCompatibleUpstreamClient>();
                    services.RemoveAll<ITraceStore>();
                    services.AddSingleton<IOpenAiCompatibleUpstreamClient>(new FakeUpstreamClient());
                    services.AddSingleton<ITraceStore>(traceStore);
                });
            });

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/v1/chat/completions", new ChatCompletionRequest
        {
            Model = "vision-model",
            Messages =
            [
                new ChatMessageDto
                {
                    Role = "user",
                    Content = ChatMessageContent.FromParts(
                    [
                        ChatMessageContentPart.FromText("Describe this image."),
                        ChatMessageContentPart.FromImageUrl("data:image/png;base64,AAAABASE64")
                    ])
                }
            ]
        }, JsonOptions.Default);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var run = Assert.Single(traceStore.Runs);
        Assert.Equal("Describe this image. [image_url]", run.Preview);
        Assert.Contains("\"url\":\"[image_url redacted]\"", run.RequestJson, StringComparison.Ordinal);
        Assert.DoesNotContain("AAAABASE64", run.RequestJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StreamTruePipelineReturnsFinalAnswerStreamWithVirtuaAgentHeaders()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IOpenAiCompatibleUpstreamClient>();
                    services.RemoveAll<ITraceStore>();
                    services.AddSingleton<IOpenAiCompatibleUpstreamClient>(new FakeUpstreamClient());
                    services.AddSingleton<ITraceStore>(new RecordingTraceStore());
                });
            });

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/v1/chat/completions", new ChatCompletionRequest
        {
            Model = "local-model",
            Stream = true,
            Messages = [new ChatMessageDto { Role = "user", Content = "hello" }],
            Orchestration = new VirtuaAgent.PipelineModels.OrchestrationRequestDto
            {
                IncludeVirtuaAgent = true,
                Store = true,
                Pipeline = new VirtuaAgent.PipelineModels.PipelineRequestDto
                {
                    Stages =
                    [
                        new VirtuaAgent.PipelineModels.PipelineStageRequestDto
                        {
                            Type = "single_agent",
                            Repeat = 1
                        }
                    ]
                }
            }
        }, JsonOptions.Default);

        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("Virtua-Agent-Run-Id"));
        Assert.Contains("text/event-stream", response.Content.Headers.ContentType!.MediaType);
        Assert.Contains("answer", body);
        Assert.Contains("data: [DONE]", body);
    }

    [Fact]
    public async Task StreamTruePipelineStreamsStageReasoningBeforeFinalAnswer()
    {
        var traceStore = new RecordingTraceStore();
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IOpenAiCompatibleUpstreamClient>();
                    services.RemoveAll<ITraceStore>();
                    services.AddSingleton<IOpenAiCompatibleUpstreamClient>(new FakeUpstreamClient("final answer"));
                    services.AddSingleton<ITraceStore>(traceStore);
                });
            });

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/v1/chat/completions", new ChatCompletionRequest
        {
            Model = "local-model",
            Stream = true,
            Messages = [new ChatMessageDto { Role = "user", Content = "hello" }],
            Orchestration = new OrchestrationRequestDto
            {
                Pipeline = new PipelineRequestDto
                {
                    Stages =
                    [
                        new PipelineStageRequestDto
                        {
                            Type = "single_agent",
                            Agent = new AgentRequestDto { Model = "local-model" }
                        }
                    ]
                }
            }
        }, JsonOptions.Default);

        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"reasoning\":\"stage reasoning\"", body);
        Assert.Contains("\"virtua_agent\":{\"stage_index\":0,\"execution_index\":0,\"iteration_index\":0,\"label\":\"Stage 1\"}", body);
        Assert.Contains("\"content\":\"final answer\"", body);
        Assert.Contains("data: [DONE]", body);
        var reasoning = Assert.Single(traceStore.Reasonings);
        Assert.Equal("Stage 1", reasoning.Label);
        Assert.Equal("stage reasoning", reasoning.Content);
    }

    [Fact]
    public async Task StreamTruePipelineTurnsThinkContentIntoStageReasoning()
    {
        var traceStore = new RecordingTraceStore();
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IOpenAiCompatibleUpstreamClient>();
                    services.RemoveAll<ITraceStore>();
                    services.AddSingleton<IOpenAiCompatibleUpstreamClient>(new FakeThinkingUpstreamClient());
                    services.AddSingleton<ITraceStore>(traceStore);
                });
            });

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/v1/chat/completions", new ChatCompletionRequest
        {
            Model = "local-model",
            Stream = true,
            Messages = [new ChatMessageDto { Role = "user", Content = "hello" }],
            Orchestration = new OrchestrationRequestDto
            {
                Pipeline = new PipelineRequestDto
                {
                    Stages =
                    [
                        new PipelineStageRequestDto
                        {
                            Type = "single_agent",
                            Name = "Draft",
                            Agent = new AgentRequestDto { Model = "local-model" }
                        }
                    ]
                }
            }
        }, JsonOptions.Default);

        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"reasoning\":\"hidden thought\"", body);
        Assert.Contains("\"stage_index\":0", body);
        Assert.Contains("\"execution_index\":0", body);
        Assert.Contains("\"iteration_index\":0", body);
        Assert.Contains("\"stage_name\":\"Draft\"", body);
        Assert.Contains("\"label\":\"Draft\"", body);
        Assert.Contains("\"content\":\"final answer\"", body);
        Assert.DoesNotContain("<think>", body);
        var reasoning = Assert.Single(traceStore.Reasonings);
        Assert.Equal("Draft", reasoning.Label);
        Assert.Equal("hidden thought", reasoning.Content);
    }

    [Fact]
    public async Task PresetModelRunsConfiguredPipelineFromNormalChatRequest()
    {
        var upstream = new FakeUpstreamClient("draft answer", "corrected answer");
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["PipelinePresets:0:Id"] = "virtua-agent/editor",
                        ["PipelinePresets:0:Pipeline:Stages:0:Type"] = "single_agent",
                        ["PipelinePresets:0:Pipeline:Stages:0:Agent:Model"] = "local-model",
                        ["PipelinePresets:0:Pipeline:Stages:1:Type"] = "single_agent",
                        ["PipelinePresets:0:Pipeline:Stages:1:Instructions"] = "Correct spelling only.",
                        ["PipelinePresets:0:Pipeline:Stages:1:Agent:Model"] = "local-model"
                    });
                });
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IOpenAiCompatibleUpstreamClient>();
                    services.RemoveAll<ITraceStore>();
                    services.AddSingleton<IOpenAiCompatibleUpstreamClient>(upstream);
                    services.AddSingleton<ITraceStore>(new RecordingTraceStore());
                });
            });

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/v1/chat/completions", new ChatCompletionRequest
        {
            Model = "virtua-agent/editor",
            Temperature = 0.7,
            Messages = [new ChatMessageDto { Role = "user", Content = "hello" }]
        }, JsonOptions.Default);

        var body = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(JsonOptions.Default);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("corrected answer", body!.Choices[0].Message.Content);
        Assert.Equal(2, upstream.Requests.Count);
        Assert.All(upstream.Requests, request => Assert.Equal("local-model", request.Model));
        Assert.All(upstream.Requests, request => Assert.Equal(0.7, request.Temperature));
        Assert.Contains(upstream.Requests[1].Messages, message => message.Role == "user" && message.Content.AsText().Contains("draft answer", StringComparison.Ordinal));
        Assert.Contains(upstream.Requests[1].Messages, message => message.Role == "user" && message.Content.AsText().Contains("Correct spelling only.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SqliteVirtuaAgentModelRunsPipelineFromNormalChatRequest()
    {
        var upstream = new FakeUpstreamClient("draft answer", "corrected answer");
        var pipelineStore = new InMemoryPipelineModelStore();
        await pipelineStore.SaveAsync(new PipelineModelDefinition
        {
            Id = "virtua-agent/sqlite-editor",
            OwnedBy = "virtua-agent",
            Pipeline = new PipelineRequestDto
            {
                Stages =
                [
                    new PipelineStageRequestDto
                    {
                        Type = "single_agent",
                        Agent = new AgentRequestDto { Model = "local-model" }
                    },
                    new PipelineStageRequestDto
                    {
                        Type = "single_agent",
                        Instructions = "Correct spelling only.",
                        Agent = new AgentRequestDto { Model = "local-model" }
                    }
                ]
            }
        });
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IOpenAiCompatibleUpstreamClient>();
                    services.RemoveAll<ITraceStore>();
                    services.RemoveAll<IPipelineModelStore>();
                    services.AddSingleton<IOpenAiCompatibleUpstreamClient>(upstream);
                    services.AddSingleton<ITraceStore>(new RecordingTraceStore());
                    services.AddSingleton<IPipelineModelStore>(pipelineStore);
                });
            });

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/v1/chat/completions", new ChatCompletionRequest
        {
            Model = "virtua-agent/sqlite-editor",
            Messages = [new ChatMessageDto { Role = "user", Content = "hello" }]
        }, JsonOptions.Default);
        var body = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(JsonOptions.Default);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("corrected answer", body!.Choices[0].Message.Content);
        Assert.Equal(2, upstream.Requests.Count);
    }

    [Fact]
    public async Task PresetPipelineRejectsStageModelThatIsVirtuaAgentModel()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["PipelinePresets:0:Id"] = "virtua-agent/editor",
                        ["PipelinePresets:0:Pipeline:Stages:0:Type"] = "single_agent",
                        ["PipelinePresets:0:Pipeline:Stages:0:Agent:Model"] = "virtua-agent/editor"
                    });
                });
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IOpenAiCompatibleUpstreamClient>();
                    services.RemoveAll<ITraceStore>();
                    services.AddSingleton<IOpenAiCompatibleUpstreamClient>(new FakeUpstreamClient());
                    services.AddSingleton<ITraceStore>(new RecordingTraceStore());
                });
            });

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/v1/chat/completions", new ChatCompletionRequest
        {
            Model = "virtua-agent/editor",
            Messages = [new ChatMessageDto { Role = "user", Content = "hello" }]
        }, JsonOptions.Default);
        var body = await response.Content.ReadFromJsonAsync<OpenAiErrorResponse>(JsonOptions.Default);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("nested_pipeline_model", body!.Error.Code);
        Assert.Equal("pipeline.stages[0].agent.model", body.Error.Param);
    }

    private sealed class FakeUpstreamClient(params string[] answers) : IOpenAiCompatibleUpstreamClient
    {
        private int _index;

        public List<ChatCompletionRequest> Requests { get; } = [];
        public List<string> EndpointIds { get; } = [];

        public Task<ModelListResponse> ListModelsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ModelListResponse());

        public Task<ChatCompletionResponse> ChatAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            var answer = answers.Length == 0 ? "answer" : answers[_index++];
            return Task.FromResult(new ChatCompletionResponse
            {
                Id = "chatcmpl_test",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = request.Model ?? "",
                Choices =
                [
                    new ChatCompletionChoiceDto
                    {
                        Index = 0,
                        Message = new ChatMessageDto { Role = "assistant", Content = answer },
                        FinishReason = "stop"
                    }
                ]
            });
        }

        public Task<ChatCompletionResponse> ChatAsync(ChatCompletionRequest request, ModelEndpointDefinition endpoint, CancellationToken cancellationToken = default)
        {
            EndpointIds.Add(endpoint.Id);
            return ChatAsync(request, cancellationToken);
        }

        public Task StreamChatAsync(ChatCompletionRequest request, Stream output, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async Task StreamChatAsync(ChatCompletionRequest request, Func<string, CancellationToken, Task> onDataAsync, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            var answer = answers.Length == 0 ? "answer" : answers[_index++];
            await onDataAsync($$"""{"id":"chatcmpl_test","object":"chat.completion.chunk","created":1,"model":"{{request.Model}}","choices":[{"index":0,"delta":{"reasoning":"stage reasoning"},"finish_reason":null}]}""", cancellationToken);
            await onDataAsync($$"""{"id":"chatcmpl_test","object":"chat.completion.chunk","created":1,"model":"{{request.Model}}","choices":[{"index":0,"delta":{"content":"{{answer}}"},"finish_reason":null}]}""", cancellationToken);
            await onDataAsync("""{"id":"chatcmpl_test","object":"chat.completion.chunk","created":1,"model":"local-model","choices":[{"index":0,"delta":{},"finish_reason":"stop"}]}""", cancellationToken);
            await onDataAsync("[DONE]", cancellationToken);
        }
    }

    private sealed class RecordingTraceStore : ITraceStore
    {
        public List<RunRecord> Runs { get; } = [];
        public List<ReasoningRecord> Reasonings { get; } = [];

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CreateRunAsync(RunRecord run, CancellationToken cancellationToken = default)
        {
            Runs.Add(run);
            return Task.CompletedTask;
        }
        public Task AppendEventAsync(string runId, TraceEventRecord traceEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AppendReasoningAsync(string runId, ReasoningRecord reasoning, CancellationToken cancellationToken = default)
        {
            Reasonings.Add(reasoning);
            return Task.CompletedTask;
        }
        public Task CompleteRunAsync(string runId, string responseJson, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task FailRunAsync(string runId, string errorJson, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<RunRecord?> GetRunAsync(string runId, CancellationToken cancellationToken = default) => Task.FromResult<RunRecord?>(null);
        public Task<IReadOnlyList<RunRecord>> ListRunsAsync(string? status, string? clientId, int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<RunRecord>>([]);
        public Task<int> ClearRunsAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class FakeThinkingUpstreamClient : IOpenAiCompatibleUpstreamClient
    {
        public Task<ModelListResponse> ListModelsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ModelListResponse());

        public Task<ChatCompletionResponse> ChatAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task StreamChatAsync(ChatCompletionRequest request, Stream output, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async Task StreamChatAsync(ChatCompletionRequest request, Func<string, CancellationToken, Task> onDataAsync, CancellationToken cancellationToken = default)
        {
            await onDataAsync("""{"id":"chatcmpl_test","object":"chat.completion.chunk","created":1,"model":"local-model","choices":[{"index":0,"delta":{"content":"<think>hidden "},"finish_reason":null}]}""", cancellationToken);
            await onDataAsync("""{"id":"chatcmpl_test","object":"chat.completion.chunk","created":1,"model":"local-model","choices":[{"index":0,"delta":{"content":"thought</think>final answer"},"finish_reason":null}]}""", cancellationToken);
            await onDataAsync("""{"id":"chatcmpl_test","object":"chat.completion.chunk","created":1,"model":"local-model","choices":[{"index":0,"delta":{},"finish_reason":"stop"}]}""", cancellationToken);
            await onDataAsync("[DONE]", cancellationToken);
        }
    }

    private sealed class InMemoryPipelineModelStore : IPipelineModelStore
    {
        private readonly Dictionary<string, PipelineModelDefinition> _models = new(StringComparer.OrdinalIgnoreCase);

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<PipelineModelDefinition>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PipelineModelDefinition>>(_models.Values.ToList());
        public Task<PipelineModelDefinition?> GetAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(_models.GetValueOrDefault(id));
        public Task SaveAsync(PipelineModelDefinition model, CancellationToken cancellationToken = default)
        {
            _models[model.Id] = model;
            return Task.CompletedTask;
        }
        public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(_models.Remove(id));
    }

    private sealed class InMemoryModelEndpointStore(params ModelEndpointDefinition[] endpoints) : IModelEndpointStore
    {
        private readonly Dictionary<string, ModelEndpointDefinition> _endpoints = endpoints.ToDictionary(endpoint => endpoint.Id, StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyList<ModelEndpointDefinition>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ModelEndpointDefinition>>(_endpoints.Values.ToList());

        public Task<ModelEndpointDefinition?> GetAsync(string id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_endpoints.GetValueOrDefault(id));

        public Task<ModelEndpointDefinition> SaveAsync(SaveModelEndpointRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
