# Pipeline Stage Input Routing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add generic per-stage input routing for Virtua Agent pipelines so each stage can choose original request visibility, prior stage output visibility, and protocol framing while keeping `/v1/chat/completions` OpenAI-compatible.

**Architecture:** Add pipeline-level `protocol`, stage-level `protocol`, and stage-level `input` DTOs. Keep multimodal routing structural: `original_messages: "full"` preserves OpenAI content parts, while text prompt sections are built by a deterministic formatter. Update API validation, Swagger, tests, and the model editor UI.

**Tech Stack:** ASP.NET Core minimal API, C# records, System.Text.Json, Swashbuckle, xUnit, React/Vite, Mantine.

---

## File Structure

- Modify `src/virtua-agent-api/VirtuaAgent.Api/PipelineModels/VirtuaAgentDtos.cs`
  - Add `PipelineRequestDto.Protocol`.
  - Add `PipelineStageRequestDto.Protocol`.
  - Add `PipelineStageRequestDto.Input`.
  - Add `PipelineStageInputRequestDto`.

- Modify `src/virtua-agent-api/VirtuaAgent.Api/Orchestration/PipelineModels.cs`
  - Add protocol to `PipelineDefinition`.
  - Add protocol and optional input to `PipelineStageDefinition`.
  - Add `PipelineStageInputDefinition`.
  - Add prior-output label fields to `PipelineContext`.

- Modify `src/virtua-agent-api/VirtuaAgent.Api/Orchestration/PipelineExecutor.cs`
  - Compile and validate input selector values.
  - Resolve effective protocol per stage.
  - Store prior output label after each stage execution.

- Modify `src/virtua-agent-api/VirtuaAgent.Api/Orchestration/PipelinePromptProtocol.cs`
  - Keep the default pipeline protocol text.

- Create `src/virtua-agent-api/VirtuaAgent.Api/Orchestration/PipelineStagePromptComposer.cs`
  - Own effective stage input resolution and selector-driven prompt composition.
  - Preserve OpenAI multimodal messages for `original_messages: "full"`.
  - Render text blocks for `original_messages: "text"` and `prior_stage_output: "last"`.
  - Always render requested prior output with its source label, even when the output is empty.

- Modify `src/virtua-agent-api/VirtuaAgent.Api/OpenAi/ChatMessageContentSchemaFilter.cs`
  - Add `input` and `protocol` schema examples if generated schema is unclear after DTO changes.

- Modify `src/virtua-agent-api/VirtuaAgent.Tests/PipelineExecutorTests.cs`
  - Add failing tests for selectors, protocol override precedence, validation, and prior output labels.
  - Test prompt behavior through pipeline execution, not helper internals.

- Modify `src/virtua-agent-api/VirtuaAgent.Tests/ChatCompletionsEndpointTests.cs`
  - Add endpoint-level test proving saved virtual model pipeline uses selectors.

- Modify `src/virtua-agent-api/VirtuaAgent.Tests/SwaggerRouteTests.cs`
  - Add schema assertion for `original_messages`, `prior_stage_output`, and `protocol`.

- Modify `src/virtua-agent-app/src/types.ts`
  - Add TypeScript types for pipeline protocol and stage input.

- Modify `src/virtua-agent-app/src/App.tsx`
  - Add model-level protocol textarea.
  - Add stage-level input selectors and optional protocol textarea.

---

### Task 1: Add DTO Contract Tests

**Files:**
- Modify: `src/virtua-agent-api/VirtuaAgent.Tests/PipelineExecutorTests.cs`

- [ ] **Step 1: Write failing tests for selector defaults and explicit selector behavior**

Add these tests near the existing prompt packaging tests:

```csharp
[Fact]
public async Task FirstStageDefaultInputPreservesOriginalMultimodalRequest()
{
    var upstream = new RecordingUpstreamClient("observations");
    var executor = CreateExecutor(upstream);
    var request = new ChatCompletionRequest
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
        ],
        Orchestration = new OrchestrationRequestDto
        {
            Pipeline = new PipelineRequestDto
            {
                Stages =
                [
                    new PipelineStageRequestDto
                    {
                        Type = "single_agent",
                        Name = "Analyze image",
                        Instructions = "Analyze the attached image."
                    }
                ]
            }
        }
    };

    await executor.ExecuteAsync("run_test", request, store: true);

    Assert.Equal(2, upstream.Requests[0].Messages.Count);
    Assert.True(upstream.Requests[0].Messages[0].Content.IsParts);
    Assert.Contains(upstream.Requests[0].Messages[0].Content.Parts, part => part.Type == "image_url");
    Assert.Contains("Analyze the attached image.", upstream.Requests[0].Messages[1].Content.AsText(), StringComparison.Ordinal);
}

[Fact]
public async Task LaterStageCanReceiveOnlyPriorStageOutput()
{
    var upstream = new RecordingUpstreamClient("visual observations", "draft description");
    var executor = CreateExecutor(upstream);
    var request = new ChatCompletionRequest
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
        ],
        Orchestration = new OrchestrationRequestDto
        {
            Pipeline = new PipelineRequestDto
            {
                Stages =
                [
                    new PipelineStageRequestDto { Type = "single_agent", Name = "Analyze image" },
                    new PipelineStageRequestDto
                    {
                        Type = "single_agent",
                        Name = "Draft",
                        Instructions = "Use the prior observations to draft a description.",
                        Input = new PipelineStageInputRequestDto
                        {
                            OriginalMessages = "none",
                            PriorStageOutput = "last"
                        }
                    }
                ]
            }
        }
    };

    await executor.ExecuteAsync("run_test", request, store: true);

    var packaged = Assert.Single(upstream.Requests[1].Messages);
    var text = packaged.Content.AsText();
    Assert.Contains("Prior stage output from \"Analyze image\":", text, StringComparison.Ordinal);
    Assert.Contains("visual observations", text, StringComparison.Ordinal);
    Assert.DoesNotContain("Describe this image.", text, StringComparison.Ordinal);
    Assert.DoesNotContain("AAAABASE64", text, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
dotnet test src\virtua-agent-api\VirtuaAgent.slnx --filter FullyQualifiedName~PipelineExecutorTests -p:UseAppHost=false
```

Expected: compile fails because `PipelineStageInputRequestDto`, `Input`, and selector behavior do not exist.

---

### Task 2: Add Pipeline Input DTOs

**Files:**
- Modify: `src/virtua-agent-api/VirtuaAgent.Api/PipelineModels/VirtuaAgentDtos.cs`
- Modify: `src/virtua-agent-api/VirtuaAgent.Api/Orchestration/PipelineModels.cs`

- [ ] **Step 1: Add API DTO fields**

In `PipelineRequestDto`, add:

```csharp
public string? Protocol { get; init; }
```

In `PipelineStageRequestDto`, add:

```csharp
public string? Protocol { get; init; }
public PipelineStageInputRequestDto? Input { get; init; }
```

Below `PipelineStageRequestDto`, add:

```csharp
public sealed record PipelineStageInputRequestDto
{
    [JsonPropertyName("original_messages")]
    public string? OriginalMessages { get; init; }

    [JsonPropertyName("prior_stage_output")]
    public string? PriorStageOutput { get; init; }
}
```

- [ ] **Step 2: Add compiled model fields**

In `PipelineDefinition`, add protocol before `Stages`:

```csharp
string? Protocol,
List<PipelineStageDefinition> Stages);
```

In `PipelineStageDefinition`, add protocol and input before `Agent`:

```csharp
string? Protocol,
PipelineStageInputDefinition? Input,
AgentRequestDto? Agent,
```

Add this record:

```csharp
public sealed record PipelineStageInputDefinition(string? OriginalMessages, string? PriorStageOutput)
{
    public static PipelineStageInputDefinition DefaultForExecution(int executionIndex) =>
        executionIndex == 0
            ? new PipelineStageInputDefinition("full", "none")
            : new PipelineStageInputDefinition("text", "last");

    public static PipelineStageInputDefinition Resolve(PipelineStageInputDefinition? input, int executionIndex)
    {
        var defaults = DefaultForExecution(executionIndex);
        return new PipelineStageInputDefinition(
            input?.OriginalMessages ?? defaults.OriginalMessages,
            input?.PriorStageOutput ?? defaults.PriorStageOutput);
    }
}
```

Update `PipelineContext`:

```csharp
public sealed record PipelineContext(string RunId, List<ChatMessageDto> OriginalMessages)
{
    public string? CurrentAnswer { get; set; }
    public string? CurrentAnswerLabel { get; set; }
}
```

- [ ] **Step 3: Run tests to verify compile moves to executor failures**

Run:

```powershell
dotnet test src\virtua-agent-api\VirtuaAgent.slnx --filter FullyQualifiedName~PipelineExecutorTests -p:UseAppHost=false
```

Expected: compile fails in `PipelineExecutor.cs` constructor calls for `PipelineDefinition` and `PipelineStageDefinition`.

---

### Task 3: Compile And Validate Stage Input

**Files:**
- Modify: `src/virtua-agent-api/VirtuaAgent.Api/Orchestration/PipelineExecutor.cs`
- Modify: `src/virtua-agent-api/VirtuaAgent.Tests/PipelineExecutorTests.cs`

- [ ] **Step 1: Add failing validation tests**

Add:

```csharp
[Theory]
[InlineData("bad", "none", "orchestration.pipeline.stages[0].input.original_messages")]
[InlineData("full", "bad", "orchestration.pipeline.stages[0].input.prior_stage_output")]
public async Task InvalidStageInputSelectorIsRejected(string originalMessages, string priorStageOutput, string param)
{
    var upstream = new RecordingUpstreamClient("answer");
    var executor = CreateExecutor(upstream);
    var request = new ChatCompletionRequest
    {
        Model = "local-model",
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
                        Input = new PipelineStageInputRequestDto
                        {
                            OriginalMessages = originalMessages,
                            PriorStageOutput = priorStageOutput
                        }
                    }
                ]
            }
        }
    };

    var ex = await Assert.ThrowsAsync<PipelineValidationException>(
        () => executor.ExecuteAsync("run_test", request, store: true));

    Assert.Equal("invalid_stage_input", ex.Code);
    Assert.Equal(param, ex.Param);
    Assert.Empty(upstream.Requests);
}

[Fact]
public async Task FirstExecutionCannotRequestPriorStageOutput()
{
    var upstream = new RecordingUpstreamClient("answer");
    var executor = CreateExecutor(upstream);
    var request = new ChatCompletionRequest
    {
        Model = "local-model",
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
                        Input = new PipelineStageInputRequestDto
                        {
                            OriginalMessages = "none",
                            PriorStageOutput = "last"
                        }
                    }
                ]
            }
        }
    };

    var ex = await Assert.ThrowsAsync<PipelineValidationException>(
        () => executor.ExecuteAsync("run_test", request, store: true));

    Assert.Equal("invalid_stage_input", ex.Code);
    Assert.Equal("orchestration.pipeline.stages[0].input.prior_stage_output", ex.Param);
}

[Fact]
public async Task PartialStageInputMergesWithExecutionDefault()
{
    var upstream = new RecordingUpstreamClient("answer");
    var executor = CreateExecutor(upstream);
    var request = new ChatCompletionRequest
    {
        Model = "local-model",
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
                        Instructions = "Answer directly.",
                        Input = new PipelineStageInputRequestDto
                        {
                            OriginalMessages = "text"
                        }
                    }
                ]
            }
        }
    };

    await executor.ExecuteAsync("run_test", request, store: true);

    var text = Assert.Single(upstream.Requests[0].Messages).Content.AsText();
    Assert.Contains("Original conversation:", text, StringComparison.Ordinal);
    Assert.DoesNotContain("Prior stage output", text, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Implement selector compilation**

In `Compile`, when adding `PipelineStageDefinition`, pass `request.Orchestration?.Pipeline?.Protocol`, `stage.Protocol`, and compiled input:

```csharp
definitions.Add(new PipelineStageDefinition(
    stage.Type,
    stage.Name,
    stage.Repeat,
    stage.Instructions,
    stage.Protocol,
    CompileInput(stage.Input, index),
    stage.Agent,
    stage.AgentSelection,
    stage.Seed,
    stage.Agents));
```

Return pipeline with protocol:

```csharp
return new PipelineDefinition(
    request.Orchestration?.Pipeline?.DefaultEndpointId,
    request.Orchestration?.Pipeline?.DefaultModel,
    request.Orchestration?.Pipeline?.DefaultTemperature,
    request.Orchestration?.Pipeline?.DefaultMaxTokens,
    request.Orchestration?.Pipeline?.Protocol,
    definitions);
```

Add helper:

```csharp
private static PipelineStageInputDefinition? CompileInput(PipelineStageInputRequestDto? input, int stageIndex)
{
    if (input is null)
    {
        return null;
    }

    var originalMessages = NormalizeSelector(
        input.OriginalMessages,
        ["none", "text", "full"],
        $"orchestration.pipeline.stages[{stageIndex}].input.original_messages");

    var priorStageOutput = NormalizeSelector(
        input.PriorStageOutput,
        ["none", "last"],
        $"orchestration.pipeline.stages[{stageIndex}].input.prior_stage_output");

    return originalMessages is null && priorStageOutput is null
        ? null
        : new PipelineStageInputDefinition(originalMessages, priorStageOutput);
}

private static string? NormalizeSelector(string? value, string[] allowed, string param)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return null;
    }

    var normalized = value.Trim().ToLowerInvariant();
    if (allowed.Contains(normalized))
    {
        return normalized;
    }

    throw new PipelineValidationException(
        $"{param} must be one of: {string.Join(", ", allowed)}.",
        param,
        "invalid_stage_input");
}
```

In `ValidateExpandedInstructions`, after existing instruction validation, validate first execution input:

```csharp
var input = PipelineStageInputDefinition.Resolve(stage.Input, executionIndex);
if (executionIndex == 0 && input.PriorStageOutput == "last")
{
    throw new PipelineValidationException(
        "The first pipeline execution cannot include prior stage output.",
        $"orchestration.pipeline.stages[{stageIndex}].input.prior_stage_output",
        "invalid_stage_input");
}
```

- [ ] **Step 3: Run validation tests**

Run:

```powershell
dotnet test src\virtua-agent-api\VirtuaAgent.slnx --filter FullyQualifiedName~PipelineExecutorTests -p:UseAppHost=false
```

Expected: selector validation tests pass, prompt behavior tests still fail until formatter changes.

---

### Task 4: Implement Selector-Driven Prompt Composer

**Files:**
- Create: `src/virtua-agent-api/VirtuaAgent.Api/Orchestration/PipelineStagePromptComposer.cs`
- Modify: `src/virtua-agent-api/VirtuaAgent.Api/Orchestration/PipelineExecutor.cs`

- [ ] **Step 1: Create prompt composer**

Create `src/virtua-agent-api/VirtuaAgent.Api/Orchestration/PipelineStagePromptComposer.cs`:

```csharp
using VirtuaAgent.OpenAi;

namespace VirtuaAgent.Orchestration;

public static class PipelineStagePromptComposer
{
    public static List<ChatMessageDto> Compose(
        PipelineContext context,
        PipelineStageDefinition stage,
        int executionIndex,
        string? protocol)
    {
        if (executionIndex == 0
            && stage.Input is null
            && string.IsNullOrWhiteSpace(protocol)
            && string.IsNullOrWhiteSpace(stage.Instructions))
        {
            return new List<ChatMessageDto>(context.OriginalMessages);
        }

        var input = PipelineStageInputDefinition.Resolve(stage.Input, executionIndex);
        var effectiveProtocol = string.IsNullOrWhiteSpace(protocol)
            ? PipelinePromptProtocol.Default.Instructions.Trim()
            : protocol.Trim();

        var messages = new List<ChatMessageDto>();
        var textSections = new List<string>();

        if (input.OriginalMessages == "full")
        {
            messages.AddRange(context.OriginalMessages);
        }
        else if (input.OriginalMessages == "text")
        {
            textSections.Add("Original conversation:");
            textSections.Add(FormatConversation(context.OriginalMessages));
        }

        if (!string.IsNullOrWhiteSpace(effectiveProtocol))
        {
            textSections.Add("Pipeline protocol:");
            textSections.Add(effectiveProtocol);
        }

        if (input.PriorStageOutput == "last")
        {
            var label = string.IsNullOrWhiteSpace(context.CurrentAnswerLabel)
                ? "previous stage"
                : context.CurrentAnswerLabel!;
            textSections.Add($"Prior stage output from \"{label}\":");
            textSections.Add(string.IsNullOrWhiteSpace(context.CurrentAnswer) ? "[empty]" : context.CurrentAnswer!);
        }

        if (!string.IsNullOrWhiteSpace(stage.Instructions))
        {
            textSections.Add("Stage instruction:");
            textSections.Add(stage.Instructions);
        }

        if (textSections.Count > 0)
        {
            messages.Add(new ChatMessageDto
            {
                Role = "user",
                Content = string.Join("\n\n", textSections)
            });
        }

        if (messages.Count == 0)
        {
            messages.Add(new ChatMessageDto { Role = "user", Content = "" });
        }

        return messages;
    }

    private static string FormatConversation(IEnumerable<ChatMessageDto> messages) =>
        string.Join("\n", messages.Select(message => $"{message.Role}: {message.Content.AsText()}"));
}
```

- [ ] **Step 2: Pass effective protocol from executor**

In `BuildSingleAgentRequest`, replace:

```csharp
var messages = PipelinePromptBuilder.BuildStageMessages(context, stage, executionIndex);
```

with:

```csharp
var messages = PipelineStagePromptComposer.Compose(
    context,
    stage,
    executionIndex,
    stage.Protocol ?? pipeline.Protocol);
```

- [ ] **Step 3: Track prior stage labels**

Add helper in `PipelineExecutor`:

```csharp
private static string BuildStageOutputLabel(int stageIndex, int repeatIndex, PipelineStageDefinition stage)
{
    var baseLabel = string.IsNullOrWhiteSpace(stage.Name) ? $"Stage {stageIndex + 1}" : stage.Name;
    return stage.Repeat > 1 ? $"{baseLabel} #{repeatIndex + 1}" : baseLabel;
}
```

After setting `context.CurrentAnswer` in both non-streaming and streaming paths, add:

```csharp
context.CurrentAnswerLabel = BuildStageOutputLabel(stageIndex, repeatIndex, stage);
```

- [ ] **Step 4: Run prompt tests**

Run:

```powershell
dotnet test src\virtua-agent-api\VirtuaAgent.slnx --filter FullyQualifiedName~PipelineExecutorTests -p:UseAppHost=false
```

Expected: prompt routing tests pass.

- [ ] **Step 5: Keep system-role placement deferred**

Leave this comment above `Compose`:

```csharp
// Keep protocol and data blocks in a user message for maximum upstream compatibility.
// If model compliance requires stronger framing, revisit moving protocol or selected
// blocks to system messages as a separate behavior change.
```

---

### Task 5: Add Protocol Precedence Tests

**Files:**
- Modify: `src/virtua-agent-api/VirtuaAgent.Tests/PipelineExecutorTests.cs`

- [ ] **Step 1: Add failing protocol tests**

Add:

```csharp
[Fact]
public async Task PipelineProtocolOverridesBuiltInProtocol()
{
    var upstream = new RecordingUpstreamClient("draft", "final");
    var executor = CreateExecutor(upstream);
    var request = new ChatCompletionRequest
    {
        Model = "local-model",
        Messages = [new ChatMessageDto { Role = "user", Content = "hello" }],
        Orchestration = new OrchestrationRequestDto
        {
            Pipeline = new PipelineRequestDto
            {
                Protocol = "Pipeline-wide protocol.",
                Stages =
                [
                    new PipelineStageRequestDto { Type = "single_agent" },
                    new PipelineStageRequestDto
                    {
                        Type = "single_agent",
                        Instructions = "Finalize.",
                        Input = new PipelineStageInputRequestDto
                        {
                            OriginalMessages = "none",
                            PriorStageOutput = "last"
                        }
                    }
                ]
            }
        }
    };

    await executor.ExecuteAsync("run_test", request, store: true);

    var text = Assert.Single(upstream.Requests[1].Messages).Content.AsText();
    Assert.Contains("Pipeline-wide protocol.", text, StringComparison.Ordinal);
    Assert.DoesNotContain("You are executing one stage in a pipeline.", text, StringComparison.Ordinal);
}

[Fact]
public async Task StageProtocolOverridesPipelineProtocol()
{
    var upstream = new RecordingUpstreamClient("draft", "final");
    var executor = CreateExecutor(upstream);
    var request = new ChatCompletionRequest
    {
        Model = "local-model",
        Messages = [new ChatMessageDto { Role = "user", Content = "hello" }],
        Orchestration = new OrchestrationRequestDto
        {
            Pipeline = new PipelineRequestDto
            {
                Protocol = "Pipeline-wide protocol.",
                Stages =
                [
                    new PipelineStageRequestDto { Type = "single_agent" },
                    new PipelineStageRequestDto
                    {
                        Type = "single_agent",
                        Protocol = "Stage-specific protocol.",
                        Instructions = "Finalize.",
                        Input = new PipelineStageInputRequestDto
                        {
                            OriginalMessages = "none",
                            PriorStageOutput = "last"
                        }
                    }
                ]
            }
        }
    };

    await executor.ExecuteAsync("run_test", request, store: true);

    var text = Assert.Single(upstream.Requests[1].Messages).Content.AsText();
    Assert.Contains("Stage-specific protocol.", text, StringComparison.Ordinal);
    Assert.DoesNotContain("Pipeline-wide protocol.", text, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run protocol tests**

Run:

```powershell
dotnet test src\virtua-agent-api\VirtuaAgent.slnx --filter FullyQualifiedName~PipelineExecutorTests -p:UseAppHost=false
```

Expected: protocol tests pass after Task 4 implementation.

---

### Task 6: Add Endpoint And Swagger Coverage

**Files:**
- Modify: `src/virtua-agent-api/VirtuaAgent.Tests/ChatCompletionsEndpointTests.cs`
- Modify: `src/virtua-agent-api/VirtuaAgent.Tests/SwaggerRouteTests.cs`
- Modify if needed: `src/virtua-agent-api/VirtuaAgent.Api/OpenAi/ChatMessageContentSchemaFilter.cs`

- [ ] **Step 1: Add endpoint test for saved pipeline model selectors**

Add:

```csharp
[Fact]
public async Task PresetPipelineUsesConfiguredStageInputSelectors()
{
    var upstream = new FakeUpstreamClient("observations", "draft");
    await using var factory = new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["PipelinePresets:0:Id"] = "virtua-agent/media-description",
                    ["PipelinePresets:0:Pipeline:Protocol"] = "Configured pipeline protocol.",
                    ["PipelinePresets:0:Pipeline:Stages:0:Type"] = "single_agent",
                    ["PipelinePresets:0:Pipeline:Stages:0:Name"] = "Analyze image",
                    ["PipelinePresets:0:Pipeline:Stages:0:Instructions"] = "Analyze the attached image.",
                    ["PipelinePresets:0:Pipeline:Stages:0:Input:OriginalMessages"] = "full",
                    ["PipelinePresets:0:Pipeline:Stages:0:Input:PriorStageOutput"] = "none",
                    ["PipelinePresets:0:Pipeline:Stages:1:Type"] = "single_agent",
                    ["PipelinePresets:0:Pipeline:Stages:1:Name"] = "Draft",
                    ["PipelinePresets:0:Pipeline:Stages:1:Instructions"] = "Write a draft from observations.",
                    ["PipelinePresets:0:Pipeline:Stages:1:Input:OriginalMessages"] = "none",
                    ["PipelinePresets:0:Pipeline:Stages:1:Input:PriorStageOutput"] = "last"
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
        Model = "virtua-agent/media-description",
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
    Assert.Equal(2, upstream.Requests.Count);
    Assert.True(upstream.Requests[0].Messages[0].Content.IsParts);
    var draftPrompt = Assert.Single(upstream.Requests[1].Messages).Content.AsText();
    Assert.Contains("Prior stage output from \"Analyze image\":", draftPrompt, StringComparison.Ordinal);
    Assert.DoesNotContain("Describe this image.", draftPrompt, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Add Swagger schema assertions**

In `SwaggerJsonShowsOpenAiMultimodalContentShape`, add:

```csharp
var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
var inputSchema = schemas.GetProperty("PipelineStageInputRequestDto");
Assert.True(inputSchema.GetProperty("properties").TryGetProperty("original_messages", out _));
Assert.True(inputSchema.GetProperty("properties").TryGetProperty("prior_stage_output", out _));
var pipelineSchema = schemas.GetProperty("PipelineRequestDto");
Assert.True(pipelineSchema.GetProperty("properties").TryGetProperty("protocol", out _));
```

- [ ] **Step 3: Run endpoint and Swagger tests**

Run:

```powershell
dotnet test src\virtua-agent-api\VirtuaAgent.slnx --filter "FullyQualifiedName~ChatCompletionsEndpointTests|FullyQualifiedName~SwaggerRouteTests" -p:UseAppHost=false
```

Expected: tests pass. If Swagger omits selector names, add a schema filter or XML description only for the new selector DTO; do not change runtime JSON.

---

### Task 7: Update Model Editor UI

**Files:**
- Modify: `src/virtua-agent-app/src/types.ts`
- Modify: `src/virtua-agent-app/src/App.tsx`

- [ ] **Step 1: Update TypeScript pipeline types**

Add:

```ts
export type OriginalMessagesInput = 'none' | 'text' | 'full';
export type PriorStageOutputInput = 'none' | 'last';

export type PipelineStageInput = {
  original_messages?: OriginalMessagesInput | null;
  prior_stage_output?: PriorStageOutputInput | null;
};
```

Update `PipelineStage`:

```ts
export type PipelineStage = {
  type: 'single_agent';
  name?: string | null;
  repeat: number;
  instructions?: string | null;
  protocol?: string | null;
  input?: PipelineStageInput | null;
  agent?: AgentRequest;
};
```

Update `Pipeline`:

```ts
export type Pipeline = {
  default_endpoint_id?: string | null;
  default_model?: string | null;
  default_temperature?: number | null;
  default_max_tokens?: number | null;
  protocol?: string | null;
  stages: PipelineStage[];
};
```

- [ ] **Step 2: Add default stage input**

Update `emptyStage`:

```ts
const emptyStage = (): PipelineStage => ({
  type: 'single_agent',
  repeat: 1,
  name: '',
  instructions: '',
  protocol: null,
  input: null,
  agent: { endpoint_id: null, model: null, temperature: null, max_tokens: null }
});
```

Add data constants near `defaultEndpointValue`:

```ts
const originalMessageInputData = [
  { value: 'full', label: 'Original messages: full' },
  { value: 'text', label: 'Original messages: text only' },
  { value: 'none', label: 'Original messages: none' }
];

const priorStageOutputInputData = [
  { value: 'none', label: 'Prior output: none' },
  { value: 'last', label: 'Prior output: last stage' }
];

function defaultStageInput(index: number): PipelineStageInput {
  return index === 0
    ? { original_messages: 'full', prior_stage_output: 'none' }
    : { original_messages: 'text', prior_stage_output: 'last' };
}
```

- [ ] **Step 3: Add pipeline protocol textarea**

Below the default model/max token controls and before `Divider label="Pipeline"`, add:

```tsx
<Textarea
  label="Pipeline protocol"
  minRows={2}
  value={draft.pipeline.protocol ?? ''}
  onChange={(event) => setDraft({
    ...draft,
    pipeline: { ...draft.pipeline, protocol: event.currentTarget.value || null }
  })}
/>
```

- [ ] **Step 4: Add stage input selectors**

Inside each stage card, before `Instructions`, add:

```tsx
<Group grow>
  <Select
    label="Original messages"
    data={originalMessageInputData}
    value={stage.input?.original_messages ?? defaultStageInput(index).original_messages}
    onChange={(value) => updateStage(index, {
      ...stage,
      input: {
        ...stage.input,
        original_messages: (value ?? defaultStageInput(index).original_messages) as 'none' | 'text' | 'full'
      }
    })}
  />
  <Select
    label="Prior output"
    data={priorStageOutputInputData}
    value={stage.input?.prior_stage_output ?? defaultStageInput(index).prior_stage_output}
    onChange={(value) => updateStage(index, {
      ...stage,
      input: {
        ...stage.input,
        prior_stage_output: (value ?? defaultStageInput(index).prior_stage_output) as 'none' | 'last'
      }
    })}
  />
</Group>
```

- [ ] **Step 5: Add stage protocol textarea**

Below stage input selectors and before instructions, add:

```tsx
<Textarea
  label="Stage protocol"
  minRows={2}
  value={stage.protocol ?? ''}
  onChange={(event) => updateStage(index, { ...stage, protocol: event.currentTarget.value || null })}
/>
```

- [ ] **Step 6: Build the app**

Run:

```powershell
npm run build --prefix src\virtua-agent-app
```

Expected: TypeScript and Vite build pass. Existing chunk-size warning may appear; it is not a failure.

---

### Task 8: Full Verification

**Files:**
- No edits unless verification finds a real defect.

- [ ] **Step 1: Run backend tests**

Run:

```powershell
dotnet test src\virtua-agent-api\VirtuaAgent.slnx -p:UseAppHost=false
```

Expected:

```text
Passed!  - Failed:     0
```

- [ ] **Step 2: Run frontend build**

Run:

```powershell
npm run build --prefix src\virtua-agent-app
```

Expected: build exits 0.

- [ ] **Step 3: Run diff check**

Run:

```powershell
git diff --check
```

Expected: no whitespace errors.

- [ ] **Step 4: Inspect status**

Run:

```powershell
git status --short --branch
```

Expected: only files from this plan are modified.

- [ ] **Step 5: Commit**

Run:

```powershell
git add src\virtua-agent-api\VirtuaAgent.Api\PipelineModels\VirtuaAgentDtos.cs `
  src\virtua-agent-api\VirtuaAgent.Api\Orchestration\PipelineModels.cs `
  src\virtua-agent-api\VirtuaAgent.Api\Orchestration\PipelineExecutor.cs `
  src\virtua-agent-api\VirtuaAgent.Api\Orchestration\PipelinePromptProtocol.cs `
  src\virtua-agent-api\VirtuaAgent.Api\OpenAi\ChatMessageContentSchemaFilter.cs `
  src\virtua-agent-api\VirtuaAgent.Tests\PipelineExecutorTests.cs `
  src\virtua-agent-api\VirtuaAgent.Tests\ChatCompletionsEndpointTests.cs `
  src\virtua-agent-api\VirtuaAgent.Tests\SwaggerRouteTests.cs `
  src\virtua-agent-app\src\types.ts `
  src\virtua-agent-app\src\App.tsx
git commit -m "feat: add pipeline stage input routing"
```

Expected: commit succeeds.

---

## Self-Review

- Spec coverage: covers generic input routing, OpenAI drop-in behavior, multimodal preservation, per-stage relevant instructions, global `protocol`, stage `protocol`, no `context`, no templates, Swagger, backend tests, and model editor controls.
- Placeholder scan: no TBD/TODO/placeholders.
- Type consistency: DTO names use `Protocol`, `Input`, `PipelineStageInputRequestDto`, `OriginalMessages`, `PriorStageOutput`; compiled selector fields are nullable until `PipelineStageInputDefinition.Resolve` merges execution defaults; JSON names are `protocol`, `input`, `original_messages`, and `prior_stage_output`.
- Scope: one coherent subsystem: pipeline stage prompt composition. UI controls are included because saved model editing otherwise has no first-class way to set the new JSON fields.
