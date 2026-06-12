# UI Chat Pass-Through Pipeline Design

## Goal

Make `/ui/chat` behave like normal chat by default. When the user has not added pipeline stages, sending a message should pass through to the upstream OpenAI-compatible server without a pipeline. The right side of the page should still expose pipeline controls so the user can add stages when needed.

## Current Context

The server already supports this split:

- Requests without `orchestration.pipeline` stream directly through `IOpenAiCompatibleUpstreamClient.StreamChatAsync`.
- Requests with `orchestration.pipeline` execute `PipelineExecutor`.
- Empty pipelines are intentionally rejected by backend validation.

The current UI starts with one stage and `virtua-agent-chat.js` always sends `orchestration.pipeline`, so the UI cannot currently exercise the normal pass-through path.

## User Experience

The chat page keeps the existing two-column workbench:

- Left column: model selector, transcript, reasoning stream, status/error area, composer.
- Right column: visible pipeline panel.

The main action button always says `Send`. It does not switch to `Run pipeline`.

The pipeline panel has a small stage settings form at the top. The form includes:

- repeat count
- model override
- temperature
- max tokens
- instructions

Below the form is the stage list. The list starts empty and shows an empty state explaining that chat passes through when there are no stages.

Pressing `Add stage` copies the current form values into the stage list. The form is not cleared, so the same settings can be reused to add several similar stages quickly. Removing stages can return the list to empty.

The stage list is the actual pipeline used for the next send. Stages in the list may remain editable in place, following the current UI pattern, because that preserves existing capability while adding the reusable composer.

## Request Behavior

When `_stages.Count == 0`, the UI sends:

- `model`
- `messages`
- `stream: true`

It does not send `orchestration` or `orchestration.pipeline`.

When `_stages.Count > 0`, the UI sends the existing orchestration wrapper:

- `orchestration.include_virtua_agent: true`
- `orchestration.store: true`
- `orchestration.pipeline.stages`

Each stage maps from the stage list into the existing `single_agent` request shape.

## Component Boundaries

`Chat.razor` should own the page state:

- `_stageDraft`: reusable top-form values.
- `_stages`: added stages that will be sent as the pipeline.
- `_messages`, `_events`, `_reasoning`, `_runId`, and send status remain as they are today.

`AddStage` clones `_stageDraft` into `_stages`. It must not reuse the same object reference, because later form edits should not mutate already added stages.

`virtua-agent-chat.js` should accept an optional pipeline argument. It builds the fetch body from the common chat fields, then adds `orchestration` only when a pipeline is provided.

The backend API contract should not change for this feature.

## Status, Trace, and Errors

Status text should use neutral chat wording:

- waiting for response
- streaming final answer
- streaming reasoning

Pipeline trace events should still be shown when the response includes a Virtua Agent trace URL. With no pipeline, a run ID and monitor link may still exist because the endpoint adds Virtua Agent headers for all chat requests, but stage events will not exist.

Validation should stay simple:

- Send is disabled while sending.
- Send requires a model and non-empty draft.
- If stages exist, each stage repeat must be at least 1.
- With zero stages, repeat validation should not block sending.

The existing streamed error handling in `virtua-agent-chat.js` should remain intact.

## Persistence

Persistence is optional POC polish. If it is cheap and low-risk, use browser `localStorage` to preserve:

- `_stageDraft`
- `_stages`

This persistence is client-only and should not affect backend state. If adding it requires a large interop harness or makes the implementation brittle, skip it for this pass.

## Testing

Update the Blazor route smoke test to reflect the new visible UI:

- page title still identifies the chat tester
- composer placeholder remains visible
- primary button text is `Send`
- pipeline panel is visible
- stage settings form is visible
- empty stage list state is visible
- stage fields such as Repeat and Instructions are still present

The endpoint tests already cover no-orchestration pass-through behavior and streamed pipeline behavior. Add or adjust tests only if implementation touches server behavior, which this design does not require.

Manual verification should include:

- zero-stage send reaches the upstream streaming path without empty-pipeline validation failure
- adding a stage sends an orchestration pipeline
- removing all stages returns to pass-through behavior
- `Add stage` copies the form while leaving the form unchanged
