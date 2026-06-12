# Chat Session Persistence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Persist the Chat page transcript in SQLite, reload it after refresh, and clear it from the Chat page without using saved history as model context.

**Architecture:** Add a focused `ChatSessions` backend module with SQLite tables for sessions and messages. Expose Virtua Agent-only support routes under `/v1/chat-sessions/current/messages`. Update the React Chat page to load, append, and clear saved messages while keeping `/v1/chat/completions` requests limited to the current user message.

**Tech Stack:** ASP.NET Core minimal APIs, Microsoft.Data.Sqlite, xUnit, React, TypeScript, Mantine.

---

## File Structure

- Create `src/virtua-agent-api/VirtuaAgent.Api/ChatSessions/ChatSessionModels.cs` for stored message/session DTOs.
- Create `src/virtua-agent-api/VirtuaAgent.Api/ChatSessions/IChatSessionStore.cs` for persistence boundary.
- Create `src/virtua-agent-api/VirtuaAgent.Api/ChatSessions/SqliteChatSessionStore.cs` for SQLite schema and operations.
- Create `src/virtua-agent-api/VirtuaAgent.Api/ChatSessions/ChatSessionsEndpoint.cs` for API handlers.
- Modify `src/virtua-agent-api/VirtuaAgent.Api/Program.cs` to register and map chat session services.
- Add `src/virtua-agent-api/VirtuaAgent.Tests/ChatSessionsEndpointTests.cs` for API/store behavior.
- Modify `src/virtua-agent-app/src/types.ts` for saved message types.
- Modify `src/virtua-agent-app/src/api.ts` for chat session API functions.
- Modify `src/virtua-agent-app/src/App.tsx` to load/save/clear chat rows.
- Update `README.md` feature/config notes.

## Task 1: Backend Store

**Files:**
- Create `src/virtua-agent-api/VirtuaAgent.Api/ChatSessions/ChatSessionModels.cs`
- Create `src/virtua-agent-api/VirtuaAgent.Api/ChatSessions/IChatSessionStore.cs`
- Create `src/virtua-agent-api/VirtuaAgent.Api/ChatSessions/SqliteChatSessionStore.cs`
- Add `src/virtua-agent-api/VirtuaAgent.Tests/ChatSessionsEndpointTests.cs`

- [ ] Create models:

```csharp
public sealed record ChatSessionMessageDto
{
    public string Id { get; init; } = "";
    public string Role { get; init; } = "";
    public string Content { get; init; } = "";
    public Dictionary<string, string>? Reasoning { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed record SaveChatSessionMessageRequest
{
    public string Role { get; init; } = "";
    public string Content { get; init; } = "";
    public Dictionary<string, string>? Reasoning { get; init; }
}
```

- [ ] Create store interface with `ListCurrentMessagesAsync`, `AppendCurrentMessageAsync`, and `ClearCurrentMessagesAsync`.
- [ ] Implement SQLite schema:

```sql
CREATE TABLE IF NOT EXISTS chat_sessions (
  id TEXT PRIMARY KEY,
  title TEXT NULL,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS chat_messages (
  id TEXT PRIMARY KEY,
  session_id TEXT NOT NULL,
  role TEXT NOT NULL,
  content TEXT NOT NULL,
  reasoning_json TEXT NULL,
  created_at TEXT NOT NULL,
  FOREIGN KEY(session_id) REFERENCES chat_sessions(id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS ix_chat_messages_session_created
ON chat_messages(session_id, created_at);
```

- [ ] Add test `AppendCurrentMessageAsyncPersistsRowsInOrder`.
- [ ] Add test `ClearCurrentMessagesAsyncDeletesOnlyCurrentRows`.
- [ ] Run:

```powershell
dotnet test src\virtua-agent-api\VirtuaAgent.slnx -p:UseAppHost=false
```

Expected: pass.

## Task 2: Backend API

**Files:**
- Create `src/virtua-agent-api/VirtuaAgent.Api/ChatSessions/ChatSessionsEndpoint.cs`
- Modify `src/virtua-agent-api/VirtuaAgent.Api/Program.cs`
- Add tests in `src/virtua-agent-api/VirtuaAgent.Tests/ChatSessionsEndpointTests.cs`

- [ ] Add handlers:

```csharp
GET /v1/chat-sessions/current/messages
POST /v1/chat-sessions/current/messages
DELETE /v1/chat-sessions/current/messages
```

- [ ] Validate append requests:
  - `role` must be `user` or `assistant`
  - `content` must not be empty
- [ ] Register `IChatSessionStore` using `TraceStore:ConnectionString`.
- [ ] Add API test `GetMessagesReturnsSavedMessages`.
- [ ] Add API test `DeleteMessagesClearsSavedMessages`.
- [ ] Run:

```powershell
dotnet test src\virtua-agent-api\VirtuaAgent.slnx -p:UseAppHost=false
```

Expected: pass.

## Task 3: App API Client

**Files:**
- Modify `src/virtua-agent-app/src/types.ts`
- Modify `src/virtua-agent-app/src/api.ts`

- [ ] Add TypeScript types:

```ts
export type SavedChatMessage = ChatMessage & {
  id: string;
  reasoning?: Record<string, string> | null;
  created_at: string;
};

export type SaveChatMessageRequest = {
  role: 'user' | 'assistant';
  content: string;
  reasoning?: Record<string, string> | null;
};
```

- [ ] Add API functions:

```ts
listSavedChatMessages()
saveChatMessage(message)
clearSavedChatMessages()
```

- [ ] Run:

```powershell
npm run build --prefix src\virtua-agent-app
```

Expected: pass.

## Task 4: Chat Page Persistence

**Files:**
- Modify `src/virtua-agent-app/src/App.tsx`

- [ ] On Chat page mount, load saved messages and set `messages`.
- [ ] Add Clear button near chat controls.
- [ ] Clear button calls API, clears local messages, reasoning buckets, and run id.
- [ ] On send, save user row but send only `[current user message]` to `/v1/chat/completions`.
- [ ] On final assistant response, save assistant row with final reasoning buckets.
- [ ] On save/load/clear failures, show red notification.
- [ ] Run:

```powershell
npm run build --prefix src\virtua-agent-app
dotnet test src\virtua-agent-api\VirtuaAgent.slnx -p:UseAppHost=false
```

Expected: both pass.

## Task 5: Docs And Final Verification

**Files:**
- Modify `README.md`

- [ ] Document chat transcript persistence and clear behavior.
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
git add README.md docs\superpowers\plans\2026-06-12-chat-session-persistence.md docs\superpowers\specs\2026-06-12-chat-session-persistence-design.md src\virtua-agent-api\VirtuaAgent.Api src\virtua-agent-api\VirtuaAgent.Tests src\virtua-agent-app\src
git commit -m "feat: persist chat transcript"
```
