# Pipeline Stage Prompt Packaging Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make pipeline stage requests use explicit labeled prompt packaging and require instructions for every expanded execution after the first.

**Architecture:** Keep the public API DTOs unchanged. Update `PipelineExecutor` to validate expanded stage executions during execution, build labeled user prompts for stage calls that need packaging, and remove the hidden fallback revision instruction. Update `PipelineExecutorTests` to lock down first-stage pass-through, first-stage packaging with instructions, later-stage packaging, and instruction validation failures.

**Tech Stack:** ASP.NET Core, C# records/classes, xUnit, in-memory fake upstream client.

---

## File Structure

- Modify `src/VirtuaAgent/Orchestration/PipelineExecutor.cs`
  - Owns stage validation, stage request construction, prompt section formatting, and trace publishing.
- Modify `tests/VirtuaAgent.Tests/PipelineExecutorTests.cs`
  - Owns behavior tests for prompt packaging and validation.
- Create `docs/superpowers/plans/2026-06-10-pipeline-stage-prompt-packaging-implementation.md`
  - Documents this implementation plan.

## Task 1: Replace Revision Fallback Tests With Validation Tests

**Files:**
- Modify: `tests/VirtuaAgent.Tests/PipelineExecutorTests.cs`

- [ ] **Step 1: Replace repeated no-instruction revision test**

Replace the existing `ExecutesRepeatedSingleAgentStagesAndUsesPreviousAnswerForRevision` test with:

```csharp
[Fact]
public async Task RepeatedStageWithoutSecondExecutionInstructionsIsRejected()
{
    var upstream = new RecordingUpstreamClient("first answer");
    var executor = new PipelineExecutor(upstream, new NoopTraceStore(), new ActiveTraceHub());
    var request = new ChatCompletionRequest
    {
        Model = "local-model",
        Messages = [new ChatMessageDto { Role = "user", Content = "write answer" }],
        Orchestration = new OrchestrationRequestDto
        {
            Pipeline = new PipelineRequestDto
            {
                Stages = [new PipelineStageRequestDto { Type = "single_agent", Repeat = 2 }]
            }
        }
    };

    var ex = await Assert.ThrowsAsync<PipelineValidationException>(
        () => executor.ExecuteAsync("run_test", request, store: true));

    Assert.Equal("orchestration.pipeline.stages[0].instructions", ex.Param);
    Assert.Equal("instructions_required", ex.Code);
    Assert.Empty(upstream.Requests);
}
```

- [ ] **Step 2: Replace override/fallback test**

Replace `StageInstructionsOverrideAutomaticRevisionInstruction` with:

```csharp
[Fact]
public async Task LaterStageWithInstructionsReceivesLabeledPromptSections()
{
    var upstream = new RecordingUpstreamClient("draft answer", "corrected answer");
    var executor = new PipelineExecutor(upstream, new NoopTraceStore(), new ActiveTraceHub());
    var request = new ChatCompletionRequest
    {
        Model = "local-model",
        Messages = [new ChatMessageDto { Role = "user", Content = "write answer" }],
        Orchestration = new OrchestrationRequestDto
        {
            Pipeline = new PipelineRequestDto
            {
                Stages =
                [
                    new PipelineStageRequestDto { Type = "single_agent" },
                    new PipelineStageRequestDto
                    {
                        Type = "single_agent",
                        Instructions = "Correct spelling only."
                    }
                ]
            }
        }
    };

    var response = await executor.ExecuteAsync("run_test", request, store: true);

    Assert.Equal("corrected answer", response.Choices[0].Message.Content);
    Assert.Equal(2, upstream.Requests.Count);
    var packaged = Assert.Single(upstream.Requests[1].Messages);
    Assert.Equal("user", packaged.Role);
    Assert.Contains("Original conversation:", packaged.Content, StringComparison.Ordinal);
    Assert.Contains("user: write answer", packaged.Content, StringComparison.Ordinal);
    Assert.Contains("Previous stage output:", packaged.Content, StringComparison.Ordinal);
    Assert.Contains("draft answer", packaged.Content, StringComparison.Ordinal);
    Assert.Contains("Stage instruction:", packaged.Content, StringComparison.Ordinal);
    Assert.Contains("Correct spelling only.", packaged.Content, StringComparison.Ordinal);
    Assert.DoesNotContain("Revise and improve", packaged.Content, StringComparison.Ordinal);
}
```

- [ ] **Step 3: Run tests to verify failures**

Run:

```bash
dotnet test VirtuaAgent.slnx --filter PipelineExecutorTests -p:UseAppHost=false
```

Expected: FAIL. The first new test fails because the executor still accepts repeated no-instruction stages. The second new test fails because the later-stage request still uses original messages plus assistant/user messages instead of a single labeled prompt.

## Task 2: Add First-Stage Prompt Packaging Tests

**Files:**
- Modify: `tests/VirtuaAgent.Tests/PipelineExecutorTests.cs`

- [ ] **Step 1: Add first-stage empty-instruction test**

Add this test before the validation tests:

```csharp
[Fact]
public async Task FirstStageWithoutInstructionsSendsOriginalConversationUnchanged()
{
    var upstream = new RecordingUpstreamClient("answer");
    var executor = new PipelineExecutor(upstream, new NoopTraceStore(), new ActiveTraceHub());
    var request = new ChatCompletionRequest
    {
        Model = "local-model",
        Messages =
        [
            new ChatMessageDto { Role = "system", Content = "Be direct." },
            new ChatMessageDto { Role = "user", Content = "write answer" }
        ],
        Orchestration = new OrchestrationRequestDto
        {
            Pipeline = new PipelineRequestDto
            {
                Stages = [new PipelineStageRequestDto { Type = "single_agent" }]
            }
        }
    };

    await executor.ExecuteAsync("run_test", request, store: true);

    Assert.Single(upstream.Requests);
    Assert.Equal(request.Messages, upstream.Requests[0].Messages);
}
```

- [ ] **Step 2: Add first-stage instruction packaging test**

Add:

```csharp
[Fact]
public async Task FirstStageWithInstructionsReceivesOriginalConversationAndStageInstruction()
{
    var upstream = new RecordingUpstreamClient("answer");
    var executor = new PipelineExecutor(upstream, new NoopTraceStore(), new ActiveTraceHub());
    var request = new ChatCompletionRequest
    {
        Model = "local-model",
        Messages = [new ChatMessageDto { Role = "user", Content = "write answer" }],
        Orchestration = new OrchestrationRequestDto
        {
            Pipeline = new PipelineRequestDto
            {
                Stages =
                [
                    new PipelineStageRequestDto
                    {
                        Type = "single_agent",
                        Instructions = "Use bullet points."
                    }
                ]
            }
        }
    };

    await executor.ExecuteAsync("run_test", request, store: true);

    var packaged = Assert.Single(upstream.Requests[0].Messages);
    Assert.Equal("user", packaged.Role);
    Assert.Contains("Original conversation:", packaged.Content, StringComparison.Ordinal);
    Assert.Contains("user: write answer", packaged.Content, StringComparison.Ordinal);
    Assert.DoesNotContain("Previous stage output:", packaged.Content, StringComparison.Ordinal);
    Assert.Contains("Stage instruction:", packaged.Content, StringComparison.Ordinal);
    Assert.Contains("Use bullet points.", packaged.Content, StringComparison.Ordinal);
}
```

- [ ] **Step 3: Add later empty-instruction stage test**

Add:

```csharp
[Fact]
public async Task LaterStageWithoutInstructionsIsRejected()
{
    var upstream = new RecordingUpstreamClient("first answer");
    var executor = new PipelineExecutor(upstream, new NoopTraceStore(), new ActiveTraceHub());
    var request = new ChatCompletionRequest
    {
        Model = "local-model",
        Messages = [new ChatMessageDto { Role = "user", Content = "write answer" }],
        Orchestration = new OrchestrationRequestDto
        {
            Pipeline = new PipelineRequestDto
            {
                Stages =
                [
                    new PipelineStageRequestDto { Type = "single_agent" },
                    new PipelineStageRequestDto { Type = "single_agent" }
                ]
            }
        }
    };

    var ex = await Assert.ThrowsAsync<PipelineValidationException>(
        () => executor.ExecuteAsync("run_test", request, store: true));

    Assert.Equal("orchestration.pipeline.stages[1].instructions", ex.Param);
    Assert.Equal("instructions_required", ex.Code);
    Assert.Empty(upstream.Requests);
}
```

- [ ] **Step 4: Run tests to verify failures**

Run:

```bash
dotnet test VirtuaAgent.slnx --filter PipelineExecutorTests -p:UseAppHost=false
```

Expected: FAIL until `PipelineExecutor` is updated.

## Task 3: Implement Expanded Instruction Validation

**Files:**
- Modify: `src/VirtuaAgent/Orchestration/PipelineExecutor.cs`

- [ ] **Step 1: Remove hidden fallback constant**

Delete:

```csharp
private const string RevisionInstruction =
    "Revise and improve the previous answer while staying aligned with the original request. Return only the improved answer.";
```

- [ ] **Step 2: Pass stage index and execution index to request builder**

Replace:

```csharp
var stageRequest = BuildSingleAgentRequest(request, context, stage, agent);
```

with:

```csharp
var stageRequest = BuildSingleAgentRequest(request, context, stage, agent, stageIndex: pipeline.Stages.IndexOf(stage), executionIndex);
```

If using `IndexOf` feels brittle during implementation, change the outer loop to a `for` loop over `stageIndex` and assign `var stage = pipeline.Stages[stageIndex];`.

- [ ] **Step 3: Update the builder signature**

Change `BuildSingleAgentRequest` to accept:

```csharp
private static ChatCompletionRequest BuildSingleAgentRequest(
    ChatCompletionRequest originalRequest,
    PipelineContext context,
    PipelineStageDefinition stage,
    AgentRequestDto? agent,
    int stageIndex,
    int executionIndex)
```

- [ ] **Step 4: Validate missing instructions for execution index 1+**

At the top of `BuildSingleAgentRequest`, add:

```csharp
if (executionIndex > 0 && string.IsNullOrWhiteSpace(stage.Instructions))
{
    throw new PipelineValidationException(
        "Only the first pipeline execution may omit stage instructions.",
        $"orchestration.pipeline.stages[{stageIndex}].instructions",
        "instructions_required");
}
```

- [ ] **Step 5: Run targeted tests**

Run:

```bash
dotnet test VirtuaAgent.slnx --filter PipelineExecutorTests -p:UseAppHost=false
```

Expected: Some tests still fail because prompt packaging has not been implemented, but validation tests for missing later instructions should pass.

## Task 4: Implement Labeled Prompt Packaging

**Files:**
- Modify: `src/VirtuaAgent/Orchestration/PipelineExecutor.cs`

- [ ] **Step 1: Replace message construction in `BuildSingleAgentRequest`**

Replace the current message construction logic:

```csharp
var messages = new List<ChatMessageDto>(context.OriginalMessages);
var hasPreviousAnswer = !string.IsNullOrWhiteSpace(context.CurrentAnswer);
var instructions = string.IsNullOrWhiteSpace(stage.Instructions)
    ? hasPreviousAnswer ? RevisionInstruction : null
    : stage.Instructions;

if (hasPreviousAnswer)
{
    messages.Add(new ChatMessageDto { Role = "assistant", Content = context.CurrentAnswer! });
}

if (!string.IsNullOrWhiteSpace(instructions))
{
    messages.Add(new ChatMessageDto { Role = "user", Content = instructions });
}
```

with:

```csharp
var messages = BuildStageMessages(context, stage, executionIndex);
```

- [ ] **Step 2: Add `BuildStageMessages`**

Add this helper below `BuildSingleAgentRequest`:

```csharp
private static List<ChatMessageDto> BuildStageMessages(
    PipelineContext context,
    PipelineStageDefinition stage,
    int executionIndex)
{
    if (executionIndex == 0 && string.IsNullOrWhiteSpace(stage.Instructions))
    {
        return new List<ChatMessageDto>(context.OriginalMessages);
    }

    var sections = new List<string>
    {
        "Original conversation:",
        FormatConversation(context.OriginalMessages)
    };

    if (!string.IsNullOrWhiteSpace(context.CurrentAnswer))
    {
        sections.Add("Previous stage output:");
        sections.Add(context.CurrentAnswer!);
    }

    if (!string.IsNullOrWhiteSpace(stage.Instructions))
    {
        sections.Add("Stage instruction:");
        sections.Add(stage.Instructions);
    }

    return
    [
        new ChatMessageDto
        {
            Role = "user",
            Content = string.Join("\n\n", sections)
        }
    ];
}
```

- [ ] **Step 3: Add `FormatConversation`**

Add:

```csharp
private static string FormatConversation(IEnumerable<ChatMessageDto> messages) =>
    string.Join("\n", messages.Select(message => $"{message.Role}: {message.Content}"));
```

- [ ] **Step 4: Run targeted tests**

Run:

```bash
dotnet test VirtuaAgent.slnx --filter PipelineExecutorTests -p:UseAppHost=false
```

Expected: PASS for all `PipelineExecutorTests`.

## Task 5: Full Verification And Commit

**Files:**
- Modify: `src/VirtuaAgent/Orchestration/PipelineExecutor.cs`
- Modify: `tests/VirtuaAgent.Tests/PipelineExecutorTests.cs`
- Create: `docs/superpowers/plans/2026-06-10-pipeline-stage-prompt-packaging-implementation.md`

- [ ] **Step 1: Run full test suite**

Run:

```bash
dotnet test VirtuaAgent.slnx -p:UseAppHost=false
```

Expected: PASS.

- [ ] **Step 2: Inspect diff**

Run:

```bash
git diff -- src/VirtuaAgent/Orchestration/PipelineExecutor.cs tests/VirtuaAgent.Tests/PipelineExecutorTests.cs docs/superpowers/plans/2026-06-10-pipeline-stage-prompt-packaging-implementation.md
```

Expected:

- No `RevisionInstruction` constant remains.
- Execution index `0` may omit instructions.
- Execution index `1+` rejects empty instructions with `instructions_required`.
- First execution without instructions sends original messages unchanged.
- Packaged prompts use `Original conversation:`, `Previous stage output:`, and `Stage instruction:` labels.
- Existing unrelated CSS changes remain unstaged.

- [ ] **Step 3: Commit scoped changes**

Run:

```bash
git add docs/superpowers/plans/2026-06-10-pipeline-stage-prompt-packaging-implementation.md src/VirtuaAgent/Orchestration/PipelineExecutor.cs tests/VirtuaAgent.Tests/PipelineExecutorTests.cs
git commit -m "feat: package pipeline stage prompts"
```

Expected: commit succeeds.

## Self-Review

Spec coverage:

- Zero-stage pass-through unaffected: no endpoint or UI changes.
- First execution can omit instructions: Task 2 and Task 4.
- First execution with instructions gets labeled packaging: Task 2 and Task 4.
- Later executions require instructions: Task 1, Task 2, and Task 3.
- Later executions include original conversation, previous output, and stage instruction: Task 1 and Task 4.
- Hidden fallback revision instruction removed: Task 1 and Task 3.

Placeholder scan:

- No TBD/TODO placeholders.
- Every task includes exact file paths, code snippets, and commands.

Type consistency:

- `BuildSingleAgentRequest`, `BuildStageMessages`, `FormatConversation`, `stageIndex`, and `executionIndex` are named consistently.
