<p align="center">
  <img src="assets/logo.png" alt="Virtua Agent logo" width="480">
</p>

# Virtua Agent

Local-first OpenAI-compatible orchestration API with an ASP.NET Core backend and React/Vite workbench UI.

Virtua Agent exposes `/v1/chat/completions` as a drop-in OpenAI-compatible proxy to one configured upstream. Normal chat completion requests pass through unchanged. Requests can also opt into Virtua Agent orchestration, or select a saved Virtua Agent Pipeline model from `/v1/models`, to run staged agent calls, stream reasoning, persist traces in SQLite, and inspect runs through the built-in UI.

Current direction is narrow and inspectable: improve AI responses through explicit pipeline stages, preserve OpenAI-compatible request/response shapes, record enough trace data to debug behavior, and keep everything local-first. Out of scope for now: auth, billing, cloud sync, RAG, tool execution, provider marketplace, autonomous background agents, and multi-user workflow.

## Features

- OpenAI-compatible `/v1/chat/completions` proxy for streaming and non-streaming requests.
- OpenAI-compatible `/v1/models` list that merges upstream models with saved Virtua Agent Pipeline-backed models.
- Pipeline orchestration with `single_agent` stages, per-stage instructions, repeats, stage names, default model/temperature/max tokens, per-stage agent overrides, and random agent selection.
- Saved pipeline model CRUD at `/v1/pipeline-models`.
- Saved OpenAI-compatible endpoint CRUD at `/v1/model-endpoints`.
- Built-in `virtua-agent-test` saved model fixture for visible `Draft -> Tighten -> Apply rules` mutation with repeat counts `1, 2, 1`.
- SQLite trace storage for runs, trace events, request JSON, response JSON, and stage reasoning.
- Live orchestration event stream at `/v1/orchestrations/{runId}/events`.
- Stored run APIs: list/get/clear at `/v1/orchestrations`.
- Response headers for every chat request: `Virtua-Agent-Run-Id` plus `Link: </v1/orchestrations/{runId}/events>; rel="monitor"`.
- Optional response metadata with `orchestration.include_virtua_agent: true`.
- Optional trace persistence disable with `orchestration.store: false`.
- Streaming reasoning support from upstream reasoning fields and `<think>...</think>` content extraction.
- React workbench UI for Chat, Virtua Agent Models, Runs, Settings, and Swagger.

## Run

```powershell
dotnet restore src/virtua-agent-api/VirtuaAgent.slnx
dotnet run --project src/virtua-agent-api/VirtuaAgent.Api
```

Open:

```text
http://localhost:<port>/app/chat
http://localhost:<port>/app/models
http://localhost:<port>/app/runs
http://localhost:<port>/app/settings
http://localhost:<port>/swagger
```

Root `/` redirects to `/app/chat`.

## Configure

Configure upstream and storage in `src/virtua-agent-api/VirtuaAgent.Api/appsettings.json` or `src/virtua-agent-api/VirtuaAgent.Api/appsettings.Development.json`.

```json
{
  "Upstream": {
    "BaseUrl": "http://localhost:8080",
    "RequestTimeoutSeconds": 100
  },
  "TraceStore": {
    "ConnectionString": "Data Source=virtua-agent.db"
  },
  "PipelinePresets": []
}
```

Upstream must expose OpenAI-compatible `/v1/models` and `/v1/chat/completions`.

Additional OpenAI-compatible endpoints can be saved from `/app/settings`. Saved endpoints are stored in SQLite and can be selected in chat and Virtua Agent stage forms.

`TraceStore:ConnectionString` is shared by run traces, saved pipeline models, and saved model endpoints. Local SQLite files are ignored by git.

## App Development

The React app lives in `src/virtua-agent-app`. Vite dev server proxies API calls to the ASP.NET Core API.

```powershell
npm install --prefix src/virtua-agent-app
npm run dev --prefix src/virtua-agent-app
```

Build static app files into `src/virtua-agent-api/VirtuaAgent.Api/wwwroot/app`:

```powershell
npm run build --prefix src/virtua-agent-app
```

## Docker

Build and run the API with the bundled UI:

```powershell
docker compose up -d --build
```

Defaults:

- Host port: `4000`
- Container port: `8080`
- Upstream URL: `http://192.168.100.101:8080`
- SQLite database: `/data/virtua-agent.db` in the `virtua_agent_data` volume

Override with `.env` or shell variables:

```text
VIRTUA_AGENT_PORT=4000
UPSTREAM_BASE_URL=http://192.168.100.101:8080
UPSTREAM_REQUEST_TIMEOUT_SECONDS=100
ASPNETCORE_ENVIRONMENT=Production
```

Gitea Actions deploy from `.gitea/workflows/deploy.yaml` on pushes to `main` and uses the same variables when configured in the repository.

## Chat Examples

Plain OpenAI-compatible proxy:

```json
{
  "model": "local-model",
  "stream": true,
  "messages": [{ "role": "user", "content": "Write a concise summary." }]
}
```

Inline Virtua Agent Pipeline:

```json
{
  "model": "local-model",
  "stream": true,
  "messages": [{ "role": "user", "content": "Improve this paragraph." }],
  "orchestration": {
    "include_virtua_agent": true,
    "store": true,
    "pipeline": {
      "default_model": "local-model",
      "default_temperature": 0.2,
      "default_max_tokens": 512,
      "stages": [
        {
          "type": "single_agent",
          "name": "Draft",
          "instructions": "Write a direct first draft."
        },
        {
          "type": "single_agent",
          "name": "Tighten",
          "repeat": 2,
          "instructions": "Rewrite the previous output to be shorter and sharper."
        },
        {
          "type": "single_agent",
          "name": "Apply rules",
          "instructions": "Apply final constraints and return the final answer."
        }
      ]
    }
  }
}
```

Saved Virtua Agent model call:

```json
{
  "model": "virtua-agent-test",
  "stream": true,
  "messages": [{ "role": "user", "content": "Explain trace storage." }],
  "orchestration": {
    "include_virtua_agent": true
  }
}
```

When `model` matches a saved pipeline model or configured preset, Virtua Agent resolves that model to its pipeline and executes it through `/v1/chat/completions`.

## Pipeline Models

List saved Virtua Agent Models:

```powershell
Invoke-RestMethod http://localhost:<port>/v1/pipeline-models
```

Save a pipeline-backed model:

```json
{
  "id": "virtua-agent/editor",
  "ownedBy": "virtua-agent",
  "pipeline": {
    "default_model": "local-model",
    "stages": [
      {
        "type": "single_agent",
        "name": "Draft",
        "instructions": "Write an initial answer."
      },
      {
        "type": "single_agent",
        "name": "Edit",
        "instructions": "Improve clarity and fix errors."
      }
    ]
  }
}
```

Delete by id:

```powershell
Invoke-RestMethod -Method Delete http://localhost:<port>/v1/pipeline-models/virtua-agent/editor
```

Virtua Agent rejects nested pipeline model references inside pipeline stage agents to avoid recursive orchestration.

## Runs And Traces

Every chat request gets a run id. Stored runs can be queried:

```text
GET    /v1/orchestrations?status=completed&client_id=<id>&limit=50
GET    /v1/orchestrations/{runId}
DELETE /v1/orchestrations
GET    /v1/orchestrations/{runId}/events
```

Use `Virtua-Agent-Request-Id` and `Virtua-Agent-Client-Id` request headers to make stored runs easier to correlate.

For streaming pipeline runs, Virtua Agent forwards reasoning chunks as OpenAI-style SSE deltas with `delta.reasoning` and `delta.virtua_agent` metadata. Final chat/reasoning persistence happens when pipeline execution completes; streaming reasoning remains live output plus final trace records, not per-token archival beyond stored reasoning chunks.

## Verify

```powershell
dotnet build src/virtua-agent-api/VirtuaAgent.slnx
dotnet test src/virtua-agent-api/VirtuaAgent.slnx -p:UseAppHost=false
npm run build --prefix src/virtua-agent-app
docker compose build
```
