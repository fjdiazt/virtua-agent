# Virtua Agent API Design

Date: 2026-06-08

## Status

Approved brainstorming design for MVP planning.

`docs/draft.md` was used only as early seed material. Its MVP list is not binding.

## Product Goal

Virtua Agent API is a local-first AI orchestration API for developers who want better AI agent responses through orchestration techniques while keeping OpenAI-compatible clients working as drop-in clients.

The first version prioritizes answer quality and debuggability over breadth.

## Core Principles

- `/v1/chat/completions` remains OpenAI-compatible.
- Existing OpenAI-compatible clients can call the API without custom fields.
- Extra Virtua Agent behavior is exposed through optional request fields, response headers, and separate trace endpoints.
- OpenAI response and stream bodies stay clean by default.
- Full orchestration trace is stored locally and visible in the UI.
- The MVP is backend/API-first with a minimal Blazor trace UI.

## MVP API

### `POST /v1/chat/completions`

Primary OpenAI-compatible endpoint.

Behavior:

- No orchestration: proxy to one configured OpenAI-compatible upstream endpoint.
- `stream: true`: return OpenAI-compatible SSE stream.
- With orchestration: execute the configured pipeline and return the final answer in OpenAI-compatible shape.
- Every request creates a Virtua Agent run.

The endpoint returns metadata headers:

```text
Virtua-Agent-Run-Id: run_...
Link: </v1/orchestrations/run_.../events>; rel="monitor"
```

Optional request extension:

```json
{
  "orchestration": {
    "include_virtua_agent": true
  }
}
```

When enabled for non-streaming responses, the response includes:

```json
{
  "virtua_agent": {
    "run_id": "run_...",
    "trace_url": "/v1/orchestrations/run_.../events"
  }
}
```

By default, response bodies remain OpenAI-compatible and omit this extension.

### `GET /v1/orchestrations/{run_id}`

Returns stored run details:

- run identity
- status
- full request body
- final response body
- upstream request/response bodies
- trace events
- timestamps and errors

### `GET /v1/orchestrations/{run_id}/events`

Returns a Virtua Agent SSE trace stream for one run.

Event examples:

```json
{ "type": "run_started", "run_id": "run_..." }
{ "type": "stage_started", "stage_index": 0 }
{ "type": "agent_delta", "stage_index": 0, "content": "..." }
{ "type": "stage_completed", "stage_index": 0 }
{ "type": "run_completed", "run_id": "run_..." }
```

The trace stream sends one event at a time. It does not resend whole history on every chunk.

### `GET /v1/orchestrations`

Lists runs for UI and debugging.

Initial filters:

- `status`
- `client_id`
- `limit`

Primary grouping in the UI is client/request identity.

### `GET /app`

Serves the Blazor trace UI from the same .NET app.

The UI uses only HTTP and SSE endpoints. It does not call orchestration services directly, so it can be replaced by React later.

## Streaming Design

OpenAI stream and Virtua Agent trace stream are separate.

OpenAI-compatible stream:

- returned by `/v1/chat/completions` when `stream: true`
- preserves OpenAI chunk shape
- no default Virtua Agent fields in stream chunks
- raw proxy mode passes through upstream chunks

Virtua Agent trace stream:

- exposed through `Link rel="monitor"` and `Virtua-Agent-Run-Id`
- streams orchestration and debugging events
- stores events in SQLite by default
- powers the UI

For orchestrated multi-stage runs, the trace stream shows earlier stage progress immediately. The OpenAI stream emits final answer content in OpenAI-compatible shape.

## Run Identity

Each request receives:

- generated `run_id`
- generated server request id
- optional `Virtua-Agent-Client-Id`
- optional `Virtua-Agent-Request-Id`
- optional W3C `traceparent`
- optional W3C `tracestate`

OpenTelemetry-style spans/events should be used internally where practical.

Client identity is optional to preserve drop-in compatibility.

## Persistence

SQLite stores everything locally by default:

- full request body
- full final response body
- upstream request/response bodies
- trace events
- timestamps
- status
- errors
- client/request identity
- model/upstream information
- first user message preview

Request extension:

```json
{
  "orchestration": {
    "store": false
  }
}
```

When `store` is false:

- no persistent SQLite body/event storage
- active run remains visible while running
- live trace stream still works for active subscribers

## Orchestration MVP

The MVP pipeline is an ordered queue. There is no top-level `strategy` field in MVP.

Supported stage type:

- `single_agent`

MVP supports:

- explicit manual stage queue
- `repeat`
- fixed inline agent options
- random agent selection from inline agent list
- optional `seed` for reproducible random selection
- automatic stage framing

Example:

```json
{
  "orchestration": {
    "pipeline": {
      "stages": [
        {
          "type": "single_agent",
          "repeat": 2,
          "agent": {
            "model": "local-model",
            "temperature": 0.7,
            "max_tokens": 1024
          }
        },
        {
          "type": "single_agent",
          "repeat": 4,
          "agent_selection": "random",
          "seed": 123,
          "agents": [
            {
              "model": "local-model",
              "temperature": 0.2
            },
            {
              "model": "local-model",
              "temperature": 0.8
            }
          ]
        }
      ]
    }
  }
}
```

Automatic stage framing:

- First `single_agent` call receives the original OpenAI messages with no revise/improve instruction.
- Later `single_agent` calls receive the original prompt/messages plus the previous answer and an automatic revise/improve instruction.
- Custom stage instructions are not required in MVP.

## Agent and Upstream Model

MVP agents are inline request objects containing model/options only:

- `model`
- `temperature`
- `max_tokens`

MVP does not include:

- named agents
- personas
- system prompt libraries
- provider selection per request

The backend has one configured OpenAI-compatible upstream endpoint.

The `model` field is passed through like OpenAI. The MVP does not silently rewrite model names.

## Error Shape

Errors use an OpenAI-compatible error object with optional Virtua Agent metadata.

Example:

```json
{
  "error": {
    "message": "Council stage is not supported in MVP.",
    "type": "invalid_request_error",
    "param": "orchestration.pipeline.stages[0].type",
    "code": "stage_not_supported",
    "virtua_agent": {
      "run_id": "run_...",
      "stage_type": "council"
    }
  }
}
```

## Tech Stack

Backend:

- .NET/C#
- ASP.NET Core hybrid Minimal API endpoints plus service classes
- SQLite
- OpenAI-compatible upstream HTTP client
- SSE for Virtua Agent trace

Frontend:

- Blazor UI served by the same .NET app
- UI consumes HTTP and SSE endpoints only
- React replacement remains possible later

Authentication:

- none in MVP
- local developer server assumption

## Architecture

Recommended boundaries:

- API endpoints: HTTP routes, request/response handling, headers.
- OpenAI compatibility adapter: OpenAI DTOs, request/response mapping, stream mapping.
- Orchestrator: pipeline execution.
- Stage handlers: `single_agent` now, future stage types later.
- Upstream client: configured OpenAI-compatible HTTP endpoint.
- Trace store: SQLite persistence and active in-memory event streams.
- UI API: run list, run detail, event stream.
- Blazor UI: trace browsing and live run inspection.

Controllers/endpoints must not contain orchestration logic.

Stage handlers must not contain provider-specific HTTP logic.

The upstream client must not know about pipeline semantics.

## Post-MVP Planned Scope

These are documented design targets, not MVP commitments:

- council stage
- voting
- recipes
- named local agents
- named upstreams
- UI prompt runner
- UI model/temperature/max token controls
- recipe comparison
- richer run analytics
- pipeline strategies such as branching, conditional execution, random queue, or independent parallel stages if a valid use case appears

Parallelism in MVP is not a pipeline strategy. Future stage types may perform internal parallel work.

## Out of Scope for MVP

- auth
- billing
- user accounts
- cloud sync
- RAG
- tools
- file access
- web browsing
- autonomous background agents
- provider marketplace
- council/voting
- named recipes
- named agents
- multiple upstreams
- prompt workbench UI
- inline Virtua Agent data in OpenAI stream chunks by default

## Acceptance Criteria

- Existing OpenAI-compatible clients can call `/v1/chat/completions`.
- Non-streaming responses are OpenAI-compatible by default.
- `stream: true` returns OpenAI-compatible SSE chunks.
- Every request creates a run id.
- Response headers expose run id and monitor link.
- Virtua Agent trace events are available over a separate SSE endpoint.
- SQLite stores full request/response bodies and events by default.
- `orchestration.store=false` disables persistent storage for that run.
- MVP pipeline supports repeated `single_agent` stages.
- Later `single_agent` stages revise/improve the prior answer automatically.
- The UI lists active and completed runs and shows live trace events.
- UI groups primarily by client/request identity.
- API/UI boundary stays clean enough to replace Blazor later.
