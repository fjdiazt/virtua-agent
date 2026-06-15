# Single Public Reasoning Stream Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make pipeline streaming expose reasoning as one OpenAI-like public stream while keeping stage-separated reasoning in stored traces.

**Architecture:** Keep stage metadata inside `ReasoningRecord` persistence only. Public SSE reasoning chunks should contain `delta.reasoning` without Virtua Agent stage metadata, so `/v1/chat/completions` remains closer to a drop-in OpenAI-compatible response.

**Tech Stack:** ASP.NET Core minimal APIs, C# records, xUnit, `Microsoft.AspNetCore.Mvc.Testing`.

---

## File Structure

- Modify `src/virtua-agent-api/VirtuaAgent.Api/Orchestration/PipelineExecutor.cs`
  - Remove custom `virtua_agent` metadata from public reasoning SSE chunks.
  - Continue writing stage-separated `ReasoningRecord` entries to `ITraceStore`.
- Modify `src/virtua-agent-api/VirtuaAgent.Tests/ChatCompletionsEndpointTests.cs`
  - Update streaming reasoning tests to assert public SSE compatibility and trace separation.
- Modify `README.md`
  - Replace the old claim that public pipeline SSE includes `delta.virtua_agent` metadata.

## Task 1: Update Streaming Tests First

**Files:**
- Modify: `src/virtua-agent-api/VirtuaAgent.Tests/ChatCompletionsEndpointTests.cs`

- [ ] **Step 1: Change the public reasoning stream assertion**

Replace the old metadata assertion in `StreamTruePipelineStreamsStageReasoningBeforeFinalAnswer`:

```csharp
Assert.Contains("\"reasoning\":\"stage reasoning\"", body);
Assert.DoesNotContain("\"virtua_agent\"", body);
Assert.DoesNotContain("\"stage_index\"", body);
Assert.DoesNotContain("\"execution_index\"", body);
Assert.DoesNotContain("\"iteration_index\"", body);
Assert.DoesNotContain("\"stage_name\"", body);
Assert.DoesNotContain("\"label\"", body);
Assert.Contains("\"content\":\"final answer\"", body);
Assert.Contains("data: [DONE]", body);
```

- [ ] **Step 2: Keep trace persistence assertions**

Keep the existing trace assertions in the same test:

```csharp
var reasoning = Assert.Single(traceStore.Reasonings);
Assert.Equal("Stage 1", reasoning.Label);
Assert.Equal("stage reasoning", reasoning.Content);
```

- [ ] **Step 3: Change the think extraction public stream assertion**

Replace the stage metadata assertions in `StreamTruePipelineTurnsThinkContentIntoStageReasoning`:

```csharp
Assert.Contains("\"reasoning\":\"hidden thought\"", body);
Assert.DoesNotContain("\"virtua_agent\"", body);
Assert.DoesNotContain("\"stage_index\"", body);
Assert.DoesNotContain("\"execution_index\"", body);
Assert.DoesNotContain("\"iteration_index\"", body);
Assert.DoesNotContain("\"stage_name\"", body);
Assert.DoesNotContain("\"label\"", body);
Assert.Contains("\"content\":\"final answer\"", body);
Assert.DoesNotContain("<think>", body);
```

- [ ] **Step 4: Run focused failing tests**

Run:

```powershell
dotnet test src/virtua-agent-api/VirtuaAgent.slnx --filter "FullyQualifiedName~ChatCompletionsEndpointTests.StreamTruePipelineStreamsStageReasoningBeforeFinalAnswer|FullyQualifiedName~ChatCompletionsEndpointTests.StreamTruePipelineTurnsThinkContentIntoStageReasoning"
```

Expected: FAIL because public SSE still includes `virtua_agent` metadata.

## Task 2: Remove Public Reasoning Metadata

**Files:**
- Modify: `src/virtua-agent-api/VirtuaAgent.Api/Orchestration/PipelineExecutor.cs`

- [ ] **Step 1: Keep metadata for storage, not public stream**

Change `AppendAndWriteReasoningAsync` so `ReasoningMetadata` is still used for `ReasoningRecord.Create(...)`, but not passed to `WriteReasoningChunkAsync`.

Target shape:

```csharp
await WriteReasoningChunkAsync(output, id, created, model, reasoning, cancellationToken);
```

- [ ] **Step 2: Narrow `WriteReasoningChunkAsync`**

Change the method signature to remove `ReasoningMetadata virtuaAgent`:

```csharp
private static async Task WriteReasoningChunkAsync(
    Stream output,
    string id,
    long created,
    string model,
    string reasoning,
    CancellationToken cancellationToken)
```

- [ ] **Step 3: Emit only model-level reasoning**

Change the public chunk delta to:

```csharp
delta = new { reasoning },
```

The resulting public SSE chunk should look like:

```json
{
  "choices": [
    {
      "delta": {
        "reasoning": "..."
      }
    }
  ]
}
```

- [ ] **Step 4: Run focused tests**

Run:

```powershell
dotnet test src/virtua-agent-api/VirtuaAgent.slnx --filter "FullyQualifiedName~ChatCompletionsEndpointTests.StreamTruePipelineStreamsStageReasoningBeforeFinalAnswer|FullyQualifiedName~ChatCompletionsEndpointTests.StreamTruePipelineTurnsThinkContentIntoStageReasoning"
```

Expected: PASS.

## Task 3: Update README

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Update the streaming reasoning description**

Replace the sentence saying pipeline runs forward `delta.virtua_agent` metadata with:

```markdown
For streaming pipeline runs, Virtua Agent forwards reasoning chunks as OpenAI-style SSE deltas with `delta.reasoning` only. Stage-separated reasoning is still stored in trace records for debugging and displayed through the Runs UI when storage is enabled.
```

- [ ] **Step 2: Verify docs no longer claim public metadata**

Run:

```powershell
rg -n "delta\\.virtua_agent|virtua_agent metadata" README.md docs --glob "!docs/superpowers/plans/2026-06-15-single-public-reasoning-stream.md"
```

Expected: no README hit claiming public streaming metadata.

## Task 4: Full Verification

**Files:**
- No code edits.

- [ ] **Step 1: Run backend tests**

Run:

```powershell
dotnet test src/virtua-agent-api/VirtuaAgent.slnx
```

Expected: PASS.

- [ ] **Step 2: Inspect final diff**

Run:

```powershell
git diff -- src/virtua-agent-api/VirtuaAgent.Api/Orchestration/PipelineExecutor.cs src/virtua-agent-api/VirtuaAgent.Tests/ChatCompletionsEndpointTests.cs README.md docs/superpowers/plans/2026-06-15-single-public-reasoning-stream.md
```

Expected: only public SSE metadata removal, matching test updates, README sentence, and this plan.
