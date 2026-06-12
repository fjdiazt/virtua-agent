# Virtua Agent API MVP Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Virtua Agent API MVP: OpenAI-compatible chat completions proxy with clean OpenAI streaming, separate Virtua Agent trace SSE, SQLite run storage, repeated `single_agent` orchestration, and a minimal Blazor trace UI.

**Architecture:** Use a .NET 10 ASP.NET Core app with thin Minimal API endpoints and service classes. Keep OpenAI compatibility, orchestration, upstream HTTP, trace persistence, and Blazor UI separated so the UI can be replaced later.

**Tech Stack:** .NET 10, ASP.NET Core Minimal APIs, Blazor Web App, SQLite via `Microsoft.Data.Sqlite`, xUnit, in-memory test doubles.

---

## File Structure

Create:

- `VirtuaAgent.sln` - solution file.
- `src/VirtuaAgent/VirtuaAgent.csproj` - ASP.NET Core/Blazor host.
- `src/VirtuaAgent/Program.cs` - app composition and endpoint registration only.
- `src/VirtuaAgent/appsettings.json` - upstream and SQLite config.
- `src/VirtuaAgent/OpenAi/*.cs` - OpenAI DTOs, error DTOs, streaming chunk DTOs.
- `src/virtua-agent-api/PipelineModels/*.cs` - Virtua Agent request extension DTOs and response metadata.
- `src/VirtuaAgent/Tracing/*.cs` - run identity, trace events, active streams, SQLite store.
- `src/VirtuaAgent/Upstream/*.cs` - OpenAI-compatible upstream client.
- `src/VirtuaAgent/Orchestration/*.cs` - pipeline models, executor, `single_agent` stage.
- `src/VirtuaAgent/Endpoints/*.cs` - Minimal API endpoint classes.
- `src/VirtuaAgent/Components/*` - Blazor UI pages/components.
- `tests/VirtuaAgent.Tests/VirtuaAgent.Tests.csproj` - unit/integration tests.
- `tests/VirtuaAgent.Tests/*Tests.cs` - tests by subsystem.

Do not create provider-specific llama.cpp names. The upstream is only an OpenAI-compatible HTTP endpoint.

## Task 1: Scaffold Solution

**Files:**
- Create: `VirtuaAgent.sln`
- Create: `src/VirtuaAgent/VirtuaAgent.csproj`
- Create: `tests/VirtuaAgent.Tests/VirtuaAgent.Tests.csproj`

- [ ] **Step 1: Create projects**

Run:

```powershell
dotnet new sln -n VirtuaAgent
dotnet new blazor -n VirtuaAgent -o src/VirtuaAgent --framework net10.0 --interactivity Server
dotnet new xunit -n VirtuaAgent.Tests -o tests/VirtuaAgent.Tests --framework net10.0
dotnet sln add src/VirtuaAgent/VirtuaAgent.csproj
dotnet sln add tests/VirtuaAgent.Tests/VirtuaAgent.Tests.csproj
dotnet add tests/VirtuaAgent.Tests/VirtuaAgent.Tests.csproj reference src/VirtuaAgent/VirtuaAgent.csproj
dotnet add src/VirtuaAgent/VirtuaAgent.csproj package Microsoft.Data.Sqlite
dotnet add tests/VirtuaAgent.Tests/VirtuaAgent.Tests.csproj package Microsoft.AspNetCore.Mvc.Testing
dotnet add tests/VirtuaAgent.Tests/VirtuaAgent.Tests.csproj package RichardSzalay.MockHttp
```

Expected: projects created, packages restored.

- [ ] **Step 2: Run baseline tests**

Run:

```powershell
dotnet test
```

Expected: PASS.

- [ ] **Step 3: Commit**

```powershell
git add VirtuaAgent.sln src tests
git commit -m "chore: scaffold Virtua Agent API app"
```

## Task 2: Define OpenAI and Virtua Agent DTOs

**Files:**
- Create: `src/VirtuaAgent/OpenAi/OpenAiDtos.cs`
- Create: `src/VirtuaAgent/OpenAi/OpenAiErrorDtos.cs`
- Create: `src/virtua-agent-api/PipelineModels/VirtuaAgentDtos.cs`
- Test: `tests/VirtuaAgent.Tests/OpenAiDtoSerializationTests.cs`

- [ ] **Step 1: Write serialization tests**

Create `tests/VirtuaAgent.Tests/OpenAiDtoSerializationTests.cs`:

```csharp
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
```

- [ ] **Step 2: Verify failing test**

Run:

```powershell
dotnet test --filter OpenAiDtoSerializationTests
```

Expected: FAIL because DTOs do not exist.

- [ ] **Step 3: Implement DTOs**

Create `src/VirtuaAgent/OpenAi/OpenAiDtos.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using VirtuaAgent.PipelineModels;

namespace VirtuaAgent.OpenAi;

public static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

public sealed record ChatCompletionRequest
{
    public string? Model { get; init; }
    public List<ChatMessageDto> Messages { get; init; } = [];
    public double? Temperature { get; init; }
    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; init; }
    public bool? Stream { get; init; }
    public OrchestrationRequestDto? Orchestration { get; init; }
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtraFields { get; init; }
}

public sealed record ChatMessageDto
{
    public string Role { get; init; } = "";
    public string Content { get; init; } = "";
}

public sealed record ChatCompletionResponse
{
    public string Id { get; init; } = "";
    public string Object { get; init; } = "chat.completion";
    public long Created { get; init; }
    public string Model { get; init; } = "";
    public List<ChatCompletionChoiceDto> Choices { get; init; } = [];
    public UsageDto? Usage { get; init; }
    public VirtuaAgentResponseDto? VirtuaAgent { get; init; }
}

public sealed record ChatCompletionChoiceDto
{
    public int Index { get; init; }
    public ChatMessageDto Message { get; init; } = new();
    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; init; }
}

public sealed record UsageDto
{
    [JsonPropertyName("prompt_tokens")]
    public int? PromptTokens { get; init; }
    [JsonPropertyName("completion_tokens")]
    public int? CompletionTokens { get; init; }
    [JsonPropertyName("total_tokens")]
    public int? TotalTokens { get; init; }
}
```

Create `src/virtua-agent-api/PipelineModels/VirtuaAgentDtos.cs`:

```csharp
using System.Text.Json.Serialization;

namespace VirtuaAgent.PipelineModels;

public sealed record OrchestrationRequestDto
{
    [JsonPropertyName("include_virtua_agent")]
    public bool IncludeVirtuaAgent { get; init; }
    public bool? Store { get; init; }
    public PipelineRequestDto? Pipeline { get; init; }
}

public sealed record PipelineRequestDto
{
    public List<PipelineStageRequestDto> Stages { get; init; } = [];
}

public sealed record PipelineStageRequestDto
{
    public string Type { get; init; } = "";
    public int Repeat { get; init; } = 1;
    [JsonPropertyName("agent_selection")]
    public string? AgentSelection { get; init; }
    public int? Seed { get; init; }
    public AgentRequestDto? Agent { get; init; }
    public List<AgentRequestDto> Agents { get; init; } = [];
}

public sealed record AgentRequestDto
{
    public string? Model { get; init; }
    public double? Temperature { get; init; }
    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; init; }
}

public sealed record VirtuaAgentResponseDto
{
    [JsonPropertyName("run_id")]
    public string RunId { get; init; } = "";
    [JsonPropertyName("trace_url")]
    public string TraceUrl { get; init; } = "";
}
```

Create `src/VirtuaAgent/OpenAi/OpenAiErrorDtos.cs`:

```csharp
using System.Text.Json.Serialization;

namespace VirtuaAgent.OpenAi;

public sealed record OpenAiErrorResponse(OpenAiError Error);

public sealed record OpenAiError
{
    public string Message { get; init; } = "";
    public string Type { get; init; } = "invalid_request_error";
    public string? Param { get; init; }
    public string? Code { get; init; }
    public object? VirtuaAgent { get; init; }
}
```

- [ ] **Step 4: Verify tests**

Run:

```powershell
dotnet test --filter OpenAiDtoSerializationTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/VirtuaAgent/OpenAi src/virtua-agent-api/PipelineModels tests/VirtuaAgent.Tests/OpenAiDtoSerializationTests.cs
git commit -m "feat: add OpenAI and Virtua Agent DTOs"
```

## Task 3: Add Trace Store and SQLite Schema

**Files:**
- Create: `src/VirtuaAgent/Tracing/RunModels.cs`
- Create: `src/VirtuaAgent/Tracing/ITraceStore.cs`
- Create: `src/VirtuaAgent/Tracing/SqliteTraceStore.cs`
- Test: `tests/VirtuaAgent.Tests/SqliteTraceStoreTests.cs`

- [ ] **Step 1: Write trace store tests**

Create `tests/VirtuaAgent.Tests/SqliteTraceStoreTests.cs`:

```csharp
using VirtuaAgent.Tracing;

namespace VirtuaAgent.Tests;

public sealed class SqliteTraceStoreTests
{
    [Fact]
    public async Task StoresRunAndEvents()
    {
        var store = new SqliteTraceStore("Data Source=:memory:");
        await store.InitializeAsync();

        var run = RunRecord.Started("run_test", "req_test", "client-a", "hello", true);
        await store.CreateRunAsync(run);
        await store.AppendEventAsync("run_test", TraceEventRecord.Create("stage_started", """{"stage_index":0}"""));
        await store.CompleteRunAsync("run_test", """{"ok":true}""");

        var loaded = await store.GetRunAsync("run_test");

        Assert.NotNull(loaded);
        Assert.Equal("completed", loaded!.Status);
        Assert.Single(loaded.Events);
        Assert.Equal("stage_started", loaded.Events[0].Type);
    }
}
```

- [ ] **Step 2: Run failing test**

Run:

```powershell
dotnet test --filter SqliteTraceStoreTests
```

Expected: FAIL because trace store does not exist.

- [ ] **Step 3: Implement trace models and store**

Create `src/VirtuaAgent/Tracing/RunModels.cs`:

```csharp
namespace VirtuaAgent.Tracing;

public sealed record RunRecord(
    string RunId,
    string RequestId,
    string? ClientId,
    string Status,
    string Preview,
    bool Store,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? RequestJson,
    string? ResponseJson,
    List<TraceEventRecord> Events)
{
    public static RunRecord Started(string runId, string requestId, string? clientId, string preview, bool store) =>
        new(runId, requestId, clientId, "running", preview, store, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null, []);
}

public sealed record TraceEventRecord(string Type, string Json, DateTimeOffset CreatedAt)
{
    public static TraceEventRecord Create(string type, string json) => new(type, json, DateTimeOffset.UtcNow);
}
```

Create `src/VirtuaAgent/Tracing/ITraceStore.cs`:

```csharp
namespace VirtuaAgent.Tracing;

public interface ITraceStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task CreateRunAsync(RunRecord run, CancellationToken cancellationToken = default);
    Task AppendEventAsync(string runId, TraceEventRecord traceEvent, CancellationToken cancellationToken = default);
    Task CompleteRunAsync(string runId, string responseJson, CancellationToken cancellationToken = default);
    Task FailRunAsync(string runId, string errorJson, CancellationToken cancellationToken = default);
    Task<RunRecord?> GetRunAsync(string runId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RunRecord>> ListRunsAsync(string? status, string? clientId, int limit, CancellationToken cancellationToken = default);
}
```

Create `src/VirtuaAgent/Tracing/SqliteTraceStore.cs` with SQLite tables `runs` and `trace_events`. Use one opened `SqliteConnection` per operation except `Data Source=:memory:` tests, where the class keeps a shared connection open.

Required SQL:

```sql
CREATE TABLE IF NOT EXISTS runs (
  run_id TEXT PRIMARY KEY,
  request_id TEXT NOT NULL,
  client_id TEXT NULL,
  status TEXT NOT NULL,
  preview TEXT NOT NULL,
  store INTEGER NOT NULL,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL,
  request_json TEXT NULL,
  response_json TEXT NULL,
  error_json TEXT NULL
);

CREATE TABLE IF NOT EXISTS trace_events (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  run_id TEXT NOT NULL,
  type TEXT NOT NULL,
  json TEXT NOT NULL,
  created_at TEXT NOT NULL
);
```

- [ ] **Step 4: Verify trace tests**

Run:

```powershell
dotnet test --filter SqliteTraceStoreTests
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/VirtuaAgent/Tracing tests/VirtuaAgent.Tests/SqliteTraceStoreTests.cs
git commit -m "feat: add SQLite trace store"
```

## Task 4: Add Active Trace SSE Hub

**Files:**
- Create: `src/VirtuaAgent/Tracing/ActiveTraceHub.cs`
- Test: `tests/VirtuaAgent.Tests/ActiveTraceHubTests.cs`

- [ ] **Step 1: Write hub test**

Create `tests/VirtuaAgent.Tests/ActiveTraceHubTests.cs`:

```csharp
using VirtuaAgent.Tracing;

namespace VirtuaAgent.Tests;

public sealed class ActiveTraceHubTests
{
    [Fact]
    public async Task SubscriberReceivesPublishedEvents()
    {
        var hub = new ActiveTraceHub();
        var stream = hub.Subscribe("run_test", CancellationToken.None);

        await hub.PublishAsync("run_test", TraceEventRecord.Create("run_started", """{"run_id":"run_test"}"""));

        var enumerator = stream.GetAsyncEnumerator();
        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal("run_started", enumerator.Current.Type);
        await enumerator.DisposeAsync();
    }
}
```

- [ ] **Step 2: Implement hub**

Create `src/VirtuaAgent/Tracing/ActiveTraceHub.cs`:

```csharp
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace VirtuaAgent.Tracing;

public sealed class ActiveTraceHub
{
    private readonly ConcurrentDictionary<string, List<Channel<TraceEventRecord>>> _subscribers = new();

    public async Task PublishAsync(string runId, TraceEventRecord traceEvent)
    {
        if (!_subscribers.TryGetValue(runId, out var subscribers)) return;
        foreach (var channel in subscribers.ToArray())
        {
            await channel.Writer.WriteAsync(traceEvent);
        }
    }

    public IAsyncEnumerable<TraceEventRecord> Subscribe(string runId, CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<TraceEventRecord>();
        var subscribers = _subscribers.GetOrAdd(runId, _ => []);
        lock (subscribers) subscribers.Add(channel);

        cancellationToken.Register(() =>
        {
            channel.Writer.TryComplete();
            lock (subscribers) subscribers.Remove(channel);
        });

        return channel.Reader.ReadAllAsync(cancellationToken);
    }
}
```

- [ ] **Step 3: Run test and commit**

```powershell
dotnet test --filter ActiveTraceHubTests
git add src/VirtuaAgent/Tracing/ActiveTraceHub.cs tests/VirtuaAgent.Tests/ActiveTraceHubTests.cs
git commit -m "feat: add active trace event hub"
```

Expected: PASS.

## Task 5: Add OpenAI-Compatible Upstream Client

**Files:**
- Create: `src/VirtuaAgent/Upstream/UpstreamOptions.cs`
- Create: `src/VirtuaAgent/Upstream/OpenAiCompatibleUpstreamClient.cs`
- Test: `tests/VirtuaAgent.Tests/OpenAiCompatibleUpstreamClientTests.cs`

- [ ] **Step 1: Write upstream client test**

Create a test using `RichardSzalay.MockHttp` that verifies:

- request is posted to `/v1/chat/completions`
- model is passed through
- response content is parsed from `choices[0].message.content`

Test body:

```csharp
[Fact]
public async Task ChatAsyncPostsOpenAiRequestAndParsesResponse()
{
    var mock = new MockHttpMessageHandler();
    mock.When(HttpMethod.Post, "http://upstream.test/v1/chat/completions")
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
        new HttpClient(mock) { BaseAddress = new Uri("http://upstream.test") });

    var response = await client.ChatAsync(new ChatCompletionRequest
    {
        Model = "local-model",
        Messages = [new ChatMessageDto { Role = "user", Content = "hello" }]
    });

    Assert.Equal("answer", response.Choices[0].Message.Content);
}
```

- [ ] **Step 2: Implement upstream client**

Create `OpenAiCompatibleUpstreamClient` with:

```csharp
public sealed class OpenAiCompatibleUpstreamClient(HttpClient httpClient)
{
    public async Task<ChatCompletionResponse> ChatAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("/v1/chat/completions", request, JsonOptions.Default, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(JsonOptions.Default, cancellationToken))!;
    }
}
```

Add streaming method later in Task 7.

- [ ] **Step 3: Run test and commit**

```powershell
dotnet test --filter OpenAiCompatibleUpstreamClientTests
git add src/VirtuaAgent/Upstream tests/VirtuaAgent.Tests/OpenAiCompatibleUpstreamClientTests.cs
git commit -m "feat: add OpenAI-compatible upstream client"
```

## Task 6: Add Chat Endpoint for Non-Streaming Proxy

**Files:**
- Create: `src/VirtuaAgent/Endpoints/ChatCompletionsEndpoint.cs`
- Modify: `src/VirtuaAgent/Program.cs`
- Test: `tests/VirtuaAgent.Tests/ChatCompletionsEndpointTests.cs`

- [ ] **Step 1: Write endpoint test**

Test:

- `POST /v1/chat/completions` returns OpenAI-compatible response.
- response has `Virtua-Agent-Run-Id`.
- response has `Link` with `rel="monitor"`.

Use `WebApplicationFactory<Program>` and mocked upstream client registered in test DI.

- [ ] **Step 2: Implement endpoint**

`ChatCompletionsEndpoint.HandleAsync` must:

1. Generate `run_id`.
2. Read `Virtua-Agent-Client-Id` and `Virtua-Agent-Request-Id`.
3. Create run in trace store.
4. Call upstream client.
5. Store response if enabled.
6. Add headers.
7. Return response with optional `Virtua Agent` body only when `include_virtua_agent=true`.

- [ ] **Step 3: Wire endpoint in Program**

`Program.cs` registration:

```csharp
app.MapPost("/v1/chat/completions", ChatCompletionsEndpoint.HandleAsync);
```

Make `Program` partial for tests:

```csharp
public partial class Program;
```

- [ ] **Step 4: Run test and commit**

```powershell
dotnet test --filter ChatCompletionsEndpointTests
git add src/VirtuaAgent/Endpoints src/VirtuaAgent/Program.cs tests/VirtuaAgent.Tests/ChatCompletionsEndpointTests.cs
git commit -m "feat: add non-streaming chat proxy endpoint"
```

## Task 7: Add OpenAI-Compatible Streaming Proxy

**Files:**
- Modify: `src/VirtuaAgent/Upstream/OpenAiCompatibleUpstreamClient.cs`
- Modify: `src/VirtuaAgent/Endpoints/ChatCompletionsEndpoint.cs`
- Test: `tests/VirtuaAgent.Tests/StreamingProxyTests.cs`

- [ ] **Step 1: Write stream test**

Test:

- request with `"stream": true` returns `text/event-stream`.
- chunks include upstream data unchanged.
- headers still include run id and monitor link.

Expected streamed body:

```text
data: {"id":"chatcmpl_1","object":"chat.completion.chunk","choices":[{"delta":{"content":"hi"}}]}

data: [DONE]
```

- [ ] **Step 2: Implement upstream stream passthrough**

Add method:

```csharp
public async Task StreamChatAsync(ChatCompletionRequest request, Stream output, CancellationToken cancellationToken)
{
    using var upstream = await httpClient.PostAsJsonAsync("/v1/chat/completions", request, JsonOptions.Default, cancellationToken);
    upstream.EnsureSuccessStatusCode();
    await using var upstreamStream = await upstream.Content.ReadAsStreamAsync(cancellationToken);
    await upstreamStream.CopyToAsync(output, cancellationToken);
}
```

- [ ] **Step 3: Endpoint streaming branch**

If `request.Stream == true`, set:

```csharp
http.Response.ContentType = "text/event-stream";
```

Then call upstream stream method. Do not add Virtua Agent JSON fields into OpenAI stream chunks.

- [ ] **Step 4: Run test and commit**

```powershell
dotnet test --filter StreamingProxyTests
git add src/VirtuaAgent/Upstream src/VirtuaAgent/Endpoints tests/VirtuaAgent.Tests/StreamingProxyTests.cs
git commit -m "feat: add OpenAI-compatible streaming proxy"
```

## Task 8: Add Virtua Agent trace SSE Endpoint

**Files:**
- Create: `src/VirtuaAgent/Endpoints/OrchestrationEventsEndpoint.cs`
- Modify: `src/VirtuaAgent/Program.cs`
- Test: `tests/VirtuaAgent.Tests/OrchestrationEventsEndpointTests.cs`

- [ ] **Step 1: Write SSE endpoint test**

Test should subscribe to `/v1/orchestrations/run_test/events`, publish one event through `ActiveTraceHub`, and assert response contains:

```text
event: stage_started
data: {"stage_index":0}
```

- [ ] **Step 2: Implement endpoint**

Endpoint:

```csharp
public static async Task HandleAsync(string runId, HttpContext context, ActiveTraceHub hub)
{
    context.Response.ContentType = "text/event-stream";
    await foreach (var traceEvent in hub.Subscribe(runId, context.RequestAborted))
    {
        await context.Response.WriteAsync($"event: {traceEvent.Type}\n", context.RequestAborted);
        await context.Response.WriteAsync($"data: {traceEvent.Json}\n\n", context.RequestAborted);
        await context.Response.Body.FlushAsync(context.RequestAborted);
    }
}
```

- [ ] **Step 3: Wire endpoint**

```csharp
app.MapGet("/v1/orchestrations/{runId}/events", OrchestrationEventsEndpoint.HandleAsync);
```

- [ ] **Step 4: Run test and commit**

```powershell
dotnet test --filter OrchestrationEventsEndpointTests
git add src/VirtuaAgent/Endpoints src/VirtuaAgent/Program.cs tests/VirtuaAgent.Tests/OrchestrationEventsEndpointTests.cs
git commit -m "feat: add Virtua Agent trace SSE endpoint"
```

## Task 9: Add Single-Agent Pipeline Orchestration

**Files:**
- Create: `src/VirtuaAgent/Orchestration/PipelineModels.cs`
- Create: `src/VirtuaAgent/Orchestration/PipelineExecutor.cs`
- Create: `src/VirtuaAgent/Orchestration/SingleAgentStageHandler.cs`
- Modify: `src/VirtuaAgent/Endpoints/ChatCompletionsEndpoint.cs`
- Test: `tests/VirtuaAgent.Tests/PipelineExecutorTests.cs`

- [ ] **Step 1: Write pipeline test**

Test:

- pipeline with two `single_agent` stages calls upstream twice.
- second call includes original prompt and previous answer.
- final response content is second upstream answer.

- [ ] **Step 2: Implement pipeline models**

Models:

```csharp
public sealed record PipelineDefinition(List<PipelineStageDefinition> Stages);
public sealed record PipelineStageDefinition(string Type, int Repeat, AgentRequestDto? Agent, string? AgentSelection, int? Seed, List<AgentRequestDto> Agents);
public sealed record PipelineContext(string RunId, List<ChatMessageDto> OriginalMessages)
{
    public string? CurrentAnswer { get; set; }
}
```

- [ ] **Step 3: Implement stage expansion**

Rules:

- Missing pipeline = no orchestration; raw proxy path.
- Stage type other than `single_agent` returns OpenAI-compatible error with code `stage_not_supported`.
- `repeat < 1` returns OpenAI-compatible validation error.
- `agent_selection=random` chooses from `agents`.
- `seed` makes random repeatable.

- [ ] **Step 4: Implement automatic prompt framing**

First stage request:

```csharp
messages = originalMessages;
```

Later stage request:

```csharp
messages =
[
    ..originalMessages,
    new ChatMessageDto { Role = "assistant", Content = previousAnswer },
    new ChatMessageDto { Role = "user", Content = "Revise and improve the previous answer while staying aligned with the original request. Return only the improved answer." }
];
```

- [ ] **Step 5: Emit trace events**

Emit/store/publish:

- `stage_started`
- `agent_request`
- `agent_response`
- `stage_completed`

- [ ] **Step 6: Run test and commit**

```powershell
dotnet test --filter PipelineExecutorTests
git add src/VirtuaAgent/Orchestration src/VirtuaAgent/Endpoints tests/VirtuaAgent.Tests/PipelineExecutorTests.cs
git commit -m "feat: add single-agent pipeline orchestration"
```

## Task 10: Add Run Query and Detail Endpoints

**Files:**
- Create: `src/VirtuaAgent/Endpoints/OrchestrationRunsEndpoint.cs`
- Modify: `src/VirtuaAgent/Program.cs`
- Test: `tests/VirtuaAgent.Tests/OrchestrationRunsEndpointTests.cs`

- [ ] **Step 1: Write endpoint tests**

Tests:

- `GET /v1/orchestrations/{run_id}` returns run with events.
- `GET /v1/orchestrations?status=completed&client_id=client-a&limit=10` filters runs.

- [ ] **Step 2: Implement endpoints**

Routes:

```csharp
app.MapGet("/v1/orchestrations/{runId}", OrchestrationRunsEndpoint.GetAsync);
app.MapGet("/v1/orchestrations", OrchestrationRunsEndpoint.ListAsync);
```

List defaults:

- `limit=50`
- max `limit=200`

- [ ] **Step 3: Run test and commit**

```powershell
dotnet test --filter OrchestrationRunsEndpointTests
git add src/VirtuaAgent/Endpoints src/VirtuaAgent/Program.cs tests/VirtuaAgent.Tests/OrchestrationRunsEndpointTests.cs
git commit -m "feat: add orchestration run query endpoints"
```

## Task 11: Add Minimal Blazor Trace UI

**Files:**
- Modify: `src/VirtuaAgent/Components/Routes.razor`
- Create: `src/VirtuaAgent/Components/Pages/TraceRuns.razor`
- Create: `src/VirtuaAgent/Components/Pages/TraceRunDetail.razor`
- Create: `src/VirtuaAgent/Components/Services/TraceApiClient.cs`
- Test: `tests/VirtuaAgent.Tests/BlazorUiRouteTests.cs`

- [ ] **Step 1: Write route smoke test**

Test:

- `GET /app` returns success.
- response contains `Virtua Agent runs`.

- [ ] **Step 2: Implement UI client**

`TraceApiClient` calls:

- `GET /v1/orchestrations`
- `GET /v1/orchestrations/{run_id}`
- `GET /v1/orchestrations/{run_id}/events` using browser `EventSource` through a small JS interop helper if needed.

- [ ] **Step 3: Implement run list page**

Page requirements:

- route `/app`
- group/display by `client_id` then request id/run id
- show status, created time, preview, model if available
- click run to detail page

- [ ] **Step 4: Implement run detail page**

Page requirements:

- route `/app/runs/{runId}`
- show request/response JSON blocks
- show event timeline
- append live SSE events while run active

- [ ] **Step 5: Run test and commit**

```powershell
dotnet test --filter BlazorUiRouteTests
git add src/VirtuaAgent/Components tests/VirtuaAgent.Tests/BlazorUiRouteTests.cs
git commit -m "feat: add Blazor trace UI"
```

## Task 12: Add README Quickstart and Manual Check

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Define manual verification**

1. Run `dotnet test`.
2. Start app on `http://localhost:5099`.
3. POST a non-streaming chat request to `/v1/chat/completions`.
4. Assert `Virtua-Agent-Run-Id` header exists.
5. GET `/v1/orchestrations/{run_id}`.
6. GET `/app`.

- [ ] **Step 2: Add README quickstart**

Include:

```powershell
dotnet run --project src/VirtuaAgent
```

Configure upstream in `appsettings.json`:

```json
{
  "Upstream": {
    "BaseUrl": "http://localhost:8080"
  },
  "TraceStore": {
    "ConnectionString": "Data Source=virtua-agent.db"
  }
}
```

- [ ] **Step 3: Commit**

```powershell
git add README.md
git commit -m "docs: add local verification workflow"
```

## Final Verification

- [ ] Run all tests:

```powershell
dotnet test
```

Expected: PASS.

- [ ] Run app:

```powershell
dotnet run --project src/VirtuaAgent
```

Expected: app starts and prints local URLs.

- [ ] Open UI:

```text
http://localhost:<port>/app
```

Expected: run list page loads.

- [ ] Confirm git state:

```powershell
git status --short
```

Expected: clean worktree.

## Self-Review Notes

Spec coverage:

- OpenAI-compatible endpoint: Tasks 2, 5, 6, 7.
- Separate Virtua Agent trace stream: Tasks 4, 8.
- SQLite storage: Task 3.
- Repeated `single_agent`: Task 9.
- Run query/detail endpoints: Task 10.
- Blazor UI: Task 11.
- Local verification: Task 12.

Known intentional MVP exclusions:

- council/voting
- recipes
- named upstreams
- auth
- prompt workbench UI
- inline Virtua Agent stream chunks
