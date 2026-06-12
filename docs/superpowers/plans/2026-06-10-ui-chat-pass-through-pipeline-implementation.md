# UI Chat Pass-Through Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `/ui/chat` send plain upstream chat by default and use a pipeline only after the user adds stages from a reusable stage settings form.

**Architecture:** Keep the backend API contract unchanged. Update `Chat.razor` so the page has a stage draft form and an initially empty stage list, and update `virtua-agent-chat.js` so it only adds the orchestration wrapper when a pipeline is present. Add low-risk browser `localStorage` persistence for the draft and stage list if the JS interop remains small.

**Tech Stack:** ASP.NET Core, Blazor Server, C#, JavaScript ES modules, xUnit, `Microsoft.AspNetCore.Mvc.Testing`.

---

## File Structure

- Modify `src/VirtuaAgent/Components/Pages/Chat.razor`
  - Owns chat state, the reusable stage draft form, the added stage list, and send-time pipeline construction.
  - Adds small DTOs/classes for saved stage state if local persistence is implemented.
- Modify `src/VirtuaAgent/wwwroot/virtua-agent-chat.js`
  - Builds the chat fetch body.
  - Adds orchestration only when a non-null pipeline is passed.
  - Provides tiny `loadStageState` and `saveStageState` helpers backed by `localStorage`.
- Modify `tests/VirtuaAgent.Tests/BlazorUiRouteTests.cs`
  - Updates the `/ui/chat` smoke test to assert the new `Send` button, visible stage settings form, and empty stage list state.

## Task 1: Update Route Smoke Test

**Files:**
- Modify: `tests/VirtuaAgent.Tests/BlazorUiRouteTests.cs`

- [ ] **Step 1: Write the failing test expectations**

Replace the assertions at the end of `ChatRouteReturnsTransientChatPage` with:

```csharp
Assert.Equal(HttpStatusCode.OK, response.StatusCode);
Assert.Contains("Virtua Agent Pipeline Tester", html);
Assert.Contains("Message Virtua Agent API", html);
Assert.Contains("Pipeline", html);
Assert.Contains("Stage settings", html);
Assert.Contains("No stages configured.", html);
Assert.Contains("Chat passes through to the selected model until you add a stage.", html);
Assert.Contains(">Send</button>", html);
Assert.DoesNotContain("Run pipeline", html);
Assert.Contains("Repeat", html);
Assert.Contains("Instructions", html);
Assert.Contains("No messages yet.", html);
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```bash
dotnet test VirtuaAgent.slnx --filter BlazorUiRouteTests.ChatRouteReturnsTransientChatPage
```

Expected: FAIL because the page still renders `Run pipeline`, starts with a stage, and does not render the new empty state or `Stage settings` heading.

- [ ] **Step 3: Commit is not done yet**

Do not commit after this task. The failing test and implementation should be committed together once the feature passes.

## Task 2: Add Stage Draft Form And Empty Stage List

**Files:**
- Modify: `src/VirtuaAgent/Components/Pages/Chat.razor`

- [ ] **Step 1: Change page state fields**

Replace:

```csharp
private readonly List<PipelineStageEditor> _stages = [new()];
```

with:

```csharp
private readonly List<PipelineStageEditor> _stages = [];
private PipelineStageEditor _stageDraft = new();
```

- [ ] **Step 2: Update the Send button label**

Replace:

```razor
<button class="btn btn-primary" @onclick="SendAsync" disabled="@(!CanSend)">Run pipeline</button>
```

with:

```razor
<button class="btn btn-primary" @onclick="SendAsync" disabled="@(!CanSend)">Send</button>
```

- [ ] **Step 3: Replace the pipeline panel stage markup**

In the `<aside class="pipeline-panel">`, keep the panel header, but change the header button to add from the draft:

```razor
<div class="pipeline-panel-header">
    <h2>Pipeline</h2>
    <button class="btn btn-sm btn-outline-primary" @onclick="AddStage" disabled="@_isSending">Add stage</button>
</div>
```

Immediately after the header, add the draft form:

```razor
<div class="pipeline-stage pipeline-stage-draft">
    <div class="pipeline-stage-header">
        <strong>Stage settings</strong>
        <span class="text-muted">Copied when added</span>
    </div>
    <div class="pipeline-fields">
        <label>
            Repeat
            <InputNumber class="form-control" @bind-Value="_stageDraft.Repeat" min="1" disabled="@_isSending" />
        </label>
        <label>
            Model override
            <select class="form-select" @bind="_stageDraft.Model" disabled="@(_isSending || _models.Count == 0)">
                <option value="">Base model</option>
                @foreach (var model in _models)
                {
                    <option value="@model">@model</option>
                }
            </select>
        </label>
        <label>
            Temperature
            <InputNumber class="form-control" @bind-Value="_stageDraft.Temperature" disabled="@_isSending" />
        </label>
        <label>
            Max tokens
            <InputNumber class="form-control" @bind-Value="_stageDraft.MaxTokens" disabled="@_isSending" />
        </label>
        <label class="pipeline-field-wide">
            Instructions
            <textarea class="form-control"
                      rows="3"
                      @bind="_stageDraft.Instructions"
                      @bind:event="oninput"
                      placeholder="Optional stage instructions"
                      disabled="@_isSending"></textarea>
        </label>
    </div>
</div>
```

Then render an empty state before the existing `@for` loop:

```razor
@if (_stages.Count == 0)
{
    <div class="pipeline-empty">
        <strong>No stages configured.</strong>
        <p class="text-muted">Chat passes through to the selected model until you add a stage.</p>
    </div>
}
```

Keep the existing `@for` loop for added stages, but change the remove button so the last stage can be removed:

```razor
<button class="btn btn-sm btn-outline-danger" @onclick="() => RemoveStage(stage)" disabled="@_isSending">Remove</button>
```

- [ ] **Step 4: Add draft cloning**

Replace:

```csharp
private void AddStage() => _stages.Add(new PipelineStageEditor());
```

with:

```csharp
private void AddStage()
{
    _stages.Add(_stageDraft.Clone());
}
```

Replace:

```csharp
private void RemoveStage(PipelineStageEditor stage)
{
    if (_stages.Count > 1)
    {
        _stages.Remove(stage);
    }
}
```

with:

```csharp
private void RemoveStage(PipelineStageEditor stage)
{
    _stages.Remove(stage);
}
```

Add this method to `PipelineStageEditor`:

```csharp
public PipelineStageEditor Clone() => new()
{
    Repeat = Repeat,
    Model = Model,
    Instructions = Instructions,
    Temperature = Temperature,
    MaxTokens = MaxTokens
};
```

- [ ] **Step 5: Run the route test**

Run:

```bash
dotnet test VirtuaAgent.slnx --filter BlazorUiRouteTests.ChatRouteReturnsTransientChatPage
```

Expected: PASS for the route smoke test.

## Task 3: Send Optional Pipeline From Blazor

**Files:**
- Modify: `src/VirtuaAgent/Components/Pages/Chat.razor`

- [ ] **Step 1: Make `CanSend` work with zero stages**

Keep the `_stages.All(stage => stage.Repeat >= 1)` check. It returns `true` for an empty list, so no code change is needed unless the previous implementation added any `_stages.Count > 0` guard.

- [ ] **Step 2: Build a nullable pipeline**

In `SendAsync`, replace the current `var pipeline = new { ... }` block with:

```csharp
object? pipeline = _stages.Count == 0
    ? null
    : new
    {
        stages = _stages.Select(stage => new
        {
            type = "single_agent",
            repeat = Math.Max(1, stage.Repeat),
            instructions = string.IsNullOrWhiteSpace(stage.Instructions) ? null : stage.Instructions,
            agent = new
            {
                model = string.IsNullOrWhiteSpace(stage.Model) ? null : stage.Model,
                temperature = stage.Temperature,
                max_tokens = stage.MaxTokens
            }
        }).ToArray()
    };
```

Keep the call:

```csharp
await _module.InvokeVoidAsync("sendChat", _selectedModel, payloadMessages, pipeline, _dotNetRef);
```

- [ ] **Step 3: Neutralize status copy**

Replace:

```csharp
_status = "Waiting for pipeline...";
```

with:

```csharp
_status = "Waiting for response...";
```

Replace:

```csharp
_status = "Pipeline running...";
```

with:

```csharp
_status = "Waiting for response...";
```

Replace:

```csharp
_status = "Pipeline stage answered...";
```

with:

```csharp
_status = "Stage answered...";
```

- [ ] **Step 4: Run the route test**

Run:

```bash
dotnet test VirtuaAgent.slnx --filter BlazorUiRouteTests.ChatRouteReturnsTransientChatPage
```

Expected: PASS.

## Task 4: Add Optional-Orchestration JS Request Shaping

**Files:**
- Modify: `src/VirtuaAgent/wwwroot/virtua-agent-chat.js`

- [ ] **Step 1: Change fetch body construction**

Inside `sendChat`, before `fetch`, add:

```javascript
const body = {
    model,
    messages,
    stream: true
};

if (pipeline) {
    body.orchestration = {
        include_virtua_agent: true,
        store: true,
        pipeline
    };
}
```

Then replace the current `body: JSON.stringify({ ... })` object in `fetch` with:

```javascript
body: JSON.stringify(body),
```

Do not remove the existing streamed error handling for `chunk.error`.

- [ ] **Step 2: Add a manual check point**

Run:

```bash
dotnet test VirtuaAgent.slnx --filter BlazorUiRouteTests.ChatRouteReturnsTransientChatPage
```

Expected: PASS. This does not execute the JS path, but verifies the Blazor render still works.

## Task 5: Add Cheap Local Stage Persistence

**Files:**
- Modify: `src/VirtuaAgent/Components/Pages/Chat.razor`
- Modify: `src/VirtuaAgent/wwwroot/virtua-agent-chat.js`

- [ ] **Step 1: Add JS helpers**

Append these exports to `src/VirtuaAgent/wwwroot/virtua-agent-chat.js`:

```javascript
const stageStateKey = "virtua-agent.ui.chat.stageState";

export function loadStageState() {
    const saved = localStorage.getItem(stageStateKey);
    if (!saved) {
        return null;
    }

    try {
        return JSON.parse(saved);
    } catch {
        localStorage.removeItem(stageStateKey);
        return null;
    }
}

export function saveStageState(state) {
    localStorage.setItem(stageStateKey, JSON.stringify(state));
}
```

- [ ] **Step 2: Add saved state classes**

In `Chat.razor`, add these sealed classes near `PipelineStageEditor`:

```csharp
private sealed class SavedStageState
{
    public PipelineStageEditor? Draft { get; set; }
    public List<PipelineStageEditor>? Stages { get; set; }
}
```

- [ ] **Step 3: Load saved state after module import**

In `OnAfterRenderAsync`, immediately after importing `_module`, add:

```csharp
await LoadStageStateAsync();
```

Add this method:

```csharp
private async Task LoadStageStateAsync()
{
    if (_module is null)
    {
        return;
    }

    try
    {
        var saved = await _module.InvokeAsync<SavedStageState?>("loadStageState");
        if (saved?.Draft is not null)
        {
            _stageDraft = saved.Draft;
        }

        if (saved?.Stages is { Count: > 0 } stages)
        {
            _stages.Clear();
            _stages.AddRange(stages);
        }
    }
    catch (JSException)
    {
        // Local persistence is optional POC polish; ignore unavailable browser storage.
    }
}
```

- [ ] **Step 4: Save state after stage add/remove**

Change `AddStage` to:

```csharp
private async Task AddStage()
{
    _stages.Add(_stageDraft.Clone());
    await SaveStageStateAsync();
}
```

The existing markup can keep `@onclick="AddStage"`; Blazor accepts `Task` event handlers.

Change `RemoveStage` to:

```csharp
private async Task RemoveStage(PipelineStageEditor stage)
{
    _stages.Remove(stage);
    await SaveStageStateAsync();
}
```

The existing markup can keep `@onclick="() => RemoveStage(stage)"`.

Add:

```csharp
private async Task SaveStageStateAsync()
{
    if (_module is null)
    {
        return;
    }

    try
    {
        await _module.InvokeVoidAsync("saveStageState", new SavedStageState
        {
            Draft = _stageDraft,
            Stages = _stages
        });
    }
    catch (JSException)
    {
        // Local persistence is optional POC polish; ignore unavailable browser storage.
    }
}
```

- [ ] **Step 5: Save draft field edits without heavy plumbing**

Do not wire every field change in this pass. The draft is persisted when the user adds or removes a stage. This keeps the POC cheap and avoids a larger Blazor form event refactor.

- [ ] **Step 6: Run the route test**

Run:

```bash
dotnet test VirtuaAgent.slnx --filter BlazorUiRouteTests.ChatRouteReturnsTransientChatPage
```

Expected: PASS.

## Task 6: Full Verification And Commit

**Files:**
- Modify: `src/VirtuaAgent/Components/Pages/Chat.razor`
- Modify: `src/VirtuaAgent/wwwroot/virtua-agent-chat.js`
- Modify: `tests/VirtuaAgent.Tests/BlazorUiRouteTests.cs`

- [ ] **Step 1: Run the full test suite**

Run:

```bash
dotnet test VirtuaAgent.slnx
```

Expected: PASS.

- [ ] **Step 2: Inspect the final diff**

Run:

```bash
git diff -- src/VirtuaAgent/Components/Pages/Chat.razor src/VirtuaAgent/wwwroot/virtua-agent-chat.js tests/VirtuaAgent.Tests/BlazorUiRouteTests.cs
```

Expected:

- `Chat.razor` starts with no stages.
- The right panel shows a `Stage settings` form and empty stage state.
- The button label is `Send`.
- `SendAsync` passes `null` pipeline with zero stages.
- `virtua-agent-chat.js` only adds `orchestration` when `pipeline` is truthy.
- Existing streamed error handling is preserved.
- Route test expectations match the new UI.

- [ ] **Step 3: Commit the implementation**

Run:

```bash
git add src/VirtuaAgent/Components/Pages/Chat.razor src/VirtuaAgent/wwwroot/virtua-agent-chat.js tests/VirtuaAgent.Tests/BlazorUiRouteTests.cs
git commit -m "feat: default chat to pass-through"
```

Expected: commit succeeds. Note that `src/VirtuaAgent/wwwroot/virtua-agent-chat.js` had a pre-existing uncommitted streamed-error change before this plan; preserve it and include it only if it is still part of the intended final JS behavior.

## Self-Review

Spec coverage:

- Default zero-stage pass-through: Task 3 and Task 4.
- Visible right panel with empty state: Task 2.
- Reusable stage settings form: Task 2.
- Add stage copies form and does not clear it: Task 2.
- Always `Send`: Task 2.
- Neutral status copy: Task 3.
- Optional cheap persistence: Task 5.
- Tests: Task 1 and Task 6.

Placeholder scan:

- The plan contains no `TBD`, `TODO`, or undefined future work.
- Optional persistence has a concrete implementation and an explicit low-cost boundary.

Type consistency:

- `_stageDraft`, `_stages`, `PipelineStageEditor.Clone`, `SavedStageState`, `LoadStageStateAsync`, and `SaveStageStateAsync` are named consistently across tasks.
- JS exports `loadStageState` and `saveStageState` match the Blazor invocations.
