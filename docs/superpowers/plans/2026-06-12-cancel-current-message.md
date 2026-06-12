# Cancel Current Message Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let the Chat page cancel the currently streaming assistant response, keep partial text visible, and persist canceled state in SQLite.

**Architecture:** Use a frontend `AbortController` for the active `/v1/chat/completions` fetch. Extend chat message persistence with `metadata_json` so assistant rows can store `{ "canceled": true }`. Keep the backend OpenAI-compatible API unchanged; cancel is normal HTTP request abort.

**Tech Stack:** ASP.NET Core minimal APIs, Microsoft.Data.Sqlite, xUnit, React, TypeScript, Mantine.

---

## File Structure

- Modify `src/virtua-agent-api/VirtuaAgent.Api/ChatSessions/ChatSessionModels.cs`: add message metadata DTO property.
- Modify `src/virtua-agent-api/VirtuaAgent.Api/ChatSessions/SqliteChatSessionStore.cs`: add `metadata_json` column, save/read metadata.
- Modify `src/virtua-agent-api/VirtuaAgent.Tests/ChatSessionsEndpointTests.cs`: cover metadata round trip.
- Modify `src/virtua-agent-app/src/types.ts`: add chat message metadata types.
- Modify `src/virtua-agent-app/src/App.tsx`: add cancel controller, canceled UI state, cancel button.
- Modify `README.md`: document cancel behavior.

## Task 1: Persist Chat Message Metadata

**Files:**
- Modify: `src/virtua-agent-api/VirtuaAgent.Api/ChatSessions/ChatSessionModels.cs`
- Modify: `src/virtua-agent-api/VirtuaAgent.Api/ChatSessions/SqliteChatSessionStore.cs`
- Modify: `src/virtua-agent-api/VirtuaAgent.Tests/ChatSessionsEndpointTests.cs`

- [ ] Add metadata property to `ChatSessionMessageDto` and `SaveChatSessionMessageRequest`:

```csharp
public Dictionary<string, object?>? Metadata { get; init; }
```

- [ ] Extend SQLite schema with `metadata_json TEXT NULL`. Use `ALTER TABLE chat_messages ADD COLUMN metadata_json TEXT NULL;` guarded so existing DBs migrate.
- [ ] Insert and read `metadata_json` using `JsonSerializer.Serialize` / `JsonSerializer.Deserialize<Dictionary<string, object?>>`.
- [ ] Add backend test `MetadataRoundTripsThroughStore`.
- [ ] Add API test `MetadataRoundTripsThroughApi`.
- [ ] Run:

```powershell
dotnet test src\virtua-agent-api\VirtuaAgent.slnx -p:UseAppHost=false
```

Expected: pass.

## Task 2: Add Frontend Metadata Types

**Files:**
- Modify: `src/virtua-agent-app/src/types.ts`

- [ ] Add metadata to saved chat types:

```ts
export type ChatMessageMetadata = {
  canceled?: boolean;
};
```

- [ ] Extend local and saved chat message types with optional metadata.
- [ ] Run:

```powershell
npm run build --prefix src\virtua-agent-app
```

Expected: pass.

## Task 3: Implement Cancel Flow

**Files:**
- Modify: `src/virtua-agent-app/src/App.tsx`

- [ ] Add `AbortController` ref for current send.
- [ ] Add `cancelRequested` ref so `AbortError` can be treated as user cancel, not error.
- [ ] During `send`, create a controller and pass `signal` to `fetch`.
- [ ] Add `cancelChat()` that sets cancel requested and calls `abort()`.
- [ ] While busy, render Cancel button instead of Send button.
- [ ] On cancel, keep partial assistant text visible.
- [ ] Save partial assistant text with `metadata: { canceled: true }`.
- [ ] If no assistant content arrived before cancel, do not save assistant row.
- [ ] Clear controller refs in `finally`.
- [ ] Run:

```powershell
npm run build --prefix src\virtua-agent-app
```

Expected: pass.

## Task 4: Render Canceled Messages

**Files:**
- Modify: `src/virtua-agent-app/src/App.tsx`

- [ ] Load saved metadata into local message state.
- [ ] Render canceled assistant messages with a small `Canceled` badge beside the role label.
- [ ] Clear chat clears canceled state with messages.
- [ ] Run:

```powershell
npm run build --prefix src\virtua-agent-app
```

Expected: pass.

## Task 5: Final Verification And Commit

**Files:**
- Modify: `README.md`

- [ ] Document Cancel button behavior.
- [ ] Run:

```powershell
dotnet test src\virtua-agent-api\VirtuaAgent.slnx -p:UseAppHost=false
npm run build --prefix src\virtua-agent-app
dotnet publish src\virtua-agent-api\VirtuaAgent.Api\VirtuaAgent.Api.csproj -c Release -o .logs\publish-check /p:UseAppHost=false
git diff --check
```

Expected: all pass.

- [ ] Commit:

```powershell
git add README.md docs\superpowers\plans\2026-06-12-cancel-current-message.md docs\superpowers\specs\2026-06-12-cancel-current-message-design.md src\virtua-agent-api\VirtuaAgent.Api src\virtua-agent-api\VirtuaAgent.Tests src\virtua-agent-app\src
git commit -m "feat: cancel current chat message"
```
