# Model Endpoint Settings Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add persisted OpenAI-compatible endpoint settings and endpoint-aware model selection for chat and pipeline stages.

**Architecture:** Add a focused `ModelEndpoints` backend module backed by SQLite. Refactor upstream calls through a resolver so existing default upstream behavior stays intact while requests can specify `endpoint_id`. Update React app selectors to choose endpoint first, then load live models from that endpoint.

**Tech Stack:** ASP.NET Core minimal APIs, SQLite via `Microsoft.Data.Sqlite`, xUnit, React, TypeScript, Mantine.

---

## Files

- Create `src/virtua-agent-api/VirtuaAgent.Api/ModelEndpoints/ModelEndpointModels.cs`: endpoint domain and DTO records.
- Create `src/virtua-agent-api/VirtuaAgent.Api/ModelEndpoints/IModelEndpointStore.cs`: persistence interface.
- Create `src/virtua-agent-api/VirtuaAgent.Api/ModelEndpoints/SqliteModelEndpointStore.cs`: SQLite store.
- Create `src/virtua-agent-api/VirtuaAgent.Api/ModelEndpoints/ModelEndpointsEndpoint.cs`: HTTP handlers.
- Modify `src/virtua-agent-api/VirtuaAgent.Api/Upstream/IOpenAiCompatibleUpstreamClient.cs`: add endpoint-aware methods or resolver boundary.
- Modify `src/virtua-agent-api/VirtuaAgent.Api/Upstream/OpenAiCompatibleUpstreamClient.cs`: support per-call endpoint config.
- Modify `src/virtua-agent-api/VirtuaAgent.Api/Program.cs`: register store and map endpoints.
- Modify `src/virtua-agent-api/VirtuaAgent.Api/PipelineModels/VirtuaAgentDtos.cs`: add `endpoint_id` to agents and pipeline defaults.
- Modify `src/virtua-agent-api/VirtuaAgent.Api/Orchestration/PipelineModels.cs`: carry default endpoint.
- Modify `src/virtua-agent-api/VirtuaAgent.Api/Orchestration/PipelineExecutor.cs`: route stages through selected endpoint.
- Modify `src/virtua-agent-api/VirtuaAgent.Api/Endpoints/ChatCompletionsEndpoint.cs`: route top-level chat through selected endpoint.
- Modify `src/virtua-agent-app/src/types.ts`: endpoint and endpoint id types.
- Modify `src/virtua-agent-app/src/api.ts`: endpoint CRUD/model APIs.
- Modify `src/virtua-agent-app/src/App.tsx`: Settings page, endpoint/model selectors.
- Add tests in `src/virtua-agent-api/VirtuaAgent.Tests/ModelEndpointsEndpointTests.cs`.
- Update existing backend tests for new interface shape.

## Tasks

### Task 1: Backend Endpoint Store

- [ ] Add endpoint model records.
- [ ] Add SQLite store with schema creation.
- [ ] Add CRUD/list tests proving API keys are not returned.
- [ ] Run `dotnet test src/virtua-agent-api/VirtuaAgent.slnx -p:UseAppHost=false`.
- [ ] Commit `feat: add model endpoint store`.

### Task 2: Endpoint HTTP API

- [ ] Map `GET/POST/DELETE /v1/model-endpoints`.
- [ ] Map `GET /v1/model-endpoints/{id}/models`.
- [ ] Add selected-endpoint model listing tests.
- [ ] Run `dotnet test src/virtua-agent-api/VirtuaAgent.slnx -p:UseAppHost=false`.
- [ ] Commit `feat: add model endpoint api`.

### Task 3: Endpoint-Aware Upstream Routing

- [ ] Add `endpoint_id` to chat and pipeline DTOs.
- [ ] Refactor upstream client to accept endpoint config per call.
- [ ] Preserve current default upstream when `endpoint_id` is absent.
- [ ] Add chat and pipeline routing tests.
- [ ] Run `dotnet test src/virtua-agent-api/VirtuaAgent.slnx -p:UseAppHost=false`.
- [ ] Commit `feat: route requests by model endpoint`.

### Task 4: Settings App Page

- [ ] Add endpoint types and API client functions.
- [ ] Add Settings nav item and page.
- [ ] Implement add/edit/delete endpoint form.
- [ ] Implement live model refresh for selected endpoint.
- [ ] Run `npm run build --prefix src/virtua-agent-app`.
- [ ] Commit `feat: add endpoint settings page`.

### Task 5: Endpoint Selectors In Chat And Stage Forms

- [ ] Update Chat page to select endpoint then model.
- [ ] Update pipeline default form to select endpoint then model.
- [ ] Update stage form to select endpoint then model.
- [ ] Send `endpoint_id` in chat and pipeline save payloads.
- [ ] Run `npm run build --prefix src/virtua-agent-app`.
- [ ] Run `dotnet test src/virtua-agent-api/VirtuaAgent.slnx -p:UseAppHost=false`.
- [ ] Commit `feat: select endpoint per model`.

### Task 6: Final Verification

- [ ] Run `dotnet test src/virtua-agent-api/VirtuaAgent.slnx -p:UseAppHost=false`.
- [ ] Run `npm run build --prefix src/virtua-agent-app`.
- [ ] Run `dotnet publish src/virtua-agent-api/VirtuaAgent.Api/VirtuaAgent.Api.csproj -c Release -o .logs/publish-check /p:UseAppHost=false`.
- [ ] Run `git diff --check`.
