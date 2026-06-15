# Pipeline Protocol Settings Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the pipeline protocol visible and editable from Settings, while keeping per-model and per-stage `protocol` as overrides.

**Architecture:** Store a nullable `pipeline_protocol` setting in SQLite through a focused settings store and endpoint. `PipelineExecutor` resolves protocol precedence as stage, pipeline, settings, built-in fallback. The React Settings page edits the setting, while model/stage editors label their existing `protocol` fields as overrides.

**Tech Stack:** ASP.NET Core minimal APIs, Microsoft.Data.Sqlite, xUnit/WebApplicationFactory, React/Vite/Mantine.

---

### Task 1: Backend Settings API

**Files:**
- Create: `src/virtua-agent-api/VirtuaAgent.Api/Settings/PipelineSettingsModels.cs`
- Create: `src/virtua-agent-api/VirtuaAgent.Api/Settings/IPipelineSettingsStore.cs`
- Create: `src/virtua-agent-api/VirtuaAgent.Api/Settings/SqlitePipelineSettingsStore.cs`
- Create: `src/virtua-agent-api/VirtuaAgent.Api/Endpoints/PipelineSettingsEndpoint.cs`
- Modify: `src/virtua-agent-api/VirtuaAgent.Api/Program.cs`
- Test: `src/virtua-agent-api/VirtuaAgent.Tests/PipelineSettingsEndpointTests.cs`

- [ ] **Step 1: Write failing endpoint test**

Add a WebApplicationFactory test that calls `GET /v1/settings`, sees `built_in_pipeline_protocol`, saves `pipeline_protocol`, gets it back, then clears it with whitespace/null.

- [ ] **Step 2: Run endpoint test red**

Run: `dotnet test src\virtua-agent-api\VirtuaAgent.slnx --filter FullyQualifiedName~PipelineSettingsEndpointTests -p:UseAppHost=false`

Expected: fail because route/types do not exist.

- [ ] **Step 3: Implement settings store and endpoint**

Use a generic SQLite table named `app_settings` with key/value rows. Persist only `pipeline_protocol`; return built-in protocol for UI display.

- [ ] **Step 4: Run endpoint test green**

Run: `dotnet test src\virtua-agent-api\VirtuaAgent.slnx --filter FullyQualifiedName~PipelineSettingsEndpointTests -p:UseAppHost=false`

Expected: pass.

### Task 2: Executor Protocol Precedence

**Files:**
- Modify: `src/virtua-agent-api/VirtuaAgent.Api/Orchestration/PipelineExecutor.cs`
- Test: `src/virtua-agent-api/VirtuaAgent.Tests/PipelineExecutorTests.cs`

- [ ] **Step 1: Write failing precedence tests**

Add tests proving settings protocol is used when model/stage protocol is empty, pipeline protocol overrides settings, and stage protocol overrides pipeline/settings.

- [ ] **Step 2: Run executor tests red**

Run: `dotnet test src\virtua-agent-api\VirtuaAgent.slnx --filter FullyQualifiedName~PipelineExecutorTests -p:UseAppHost=false`

Expected: at least settings-protocol test fails.

- [ ] **Step 3: Implement precedence**

Inject `IPipelineSettingsStore` into `PipelineExecutor`, load settings once per execution, and pass `stage.Protocol ?? pipeline.Protocol ?? settings.PipelineProtocol` to `PipelineStagePromptComposer`.

- [ ] **Step 4: Run executor tests green**

Run: `dotnet test src\virtua-agent-api\VirtuaAgent.slnx --filter FullyQualifiedName~PipelineExecutorTests -p:UseAppHost=false`

Expected: pass.

### Task 3: App UX

**Files:**
- Modify: `src/virtua-agent-app/src/types.ts`
- Modify: `src/virtua-agent-app/src/api.ts`
- Modify: `src/virtua-agent-app/src/App.tsx`

- [ ] **Step 1: Add settings API types/client**

Add `PipelineSettings`, `SavePipelineSettingsRequest`, `getPipelineSettings`, and `savePipelineSettings`.

- [ ] **Step 2: Update labels and Settings page**

Use `Pipeline protocol` in Settings. Use `Protocol override` in model/stage forms. Show source text so empty fields are not hidden behavior.

- [ ] **Step 3: Build app**

Run: `npm run build --prefix src/virtua-agent-app`

Expected: pass.

### Task 4: Full Verification

**Files:** all touched files.

- [ ] **Step 1: Run full backend tests**

Run: `dotnet test src\virtua-agent-api\VirtuaAgent.slnx -p:UseAppHost=false`

Expected: pass.

- [ ] **Step 2: Run frontend build**

Run: `npm run build --prefix src/virtua-agent-app`

Expected: pass.

- [ ] **Step 3: Check diff**

Run: `git diff --check`

Expected: exit code 0.
