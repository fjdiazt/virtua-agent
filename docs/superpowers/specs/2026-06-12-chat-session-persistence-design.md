# Chat Session Persistence Design

## Goal

Persist the Chat page transcript in SQLite so refreshing the page shows saved messages. Saved messages are display history only. Chat requests remain independent and do not use saved database history as context.

## Decisions

- Use session-plus-message schema now, but expose no session picker UI yet.
- Use a fixed current session id, `default`, until multi-session UI exists.
- Keep OpenAI-compatible APIs unchanged. Persistence uses Virtua Agent-only endpoints.
- Clear button deletes messages from the current session only.
- Save reasoning with assistant messages as a JSON object keyed by reasoning label. If a response has no reasoning, store null.

## Backend Shape

Add a chat persistence module backed by the existing SQLite connection string.

Tables:

- `chat_sessions`
  - `id TEXT PRIMARY KEY`
  - `title TEXT NULL`
  - `created_at TEXT NOT NULL`
  - `updated_at TEXT NOT NULL`
- `chat_messages`
  - `id TEXT PRIMARY KEY`
  - `session_id TEXT NOT NULL`
  - `role TEXT NOT NULL`
  - `content TEXT NOT NULL`
  - `reasoning_json TEXT NULL`
  - `created_at TEXT NOT NULL`

Store responsibilities:

- initialize schema
- ensure the fixed `default` session exists
- list current session messages ordered by creation time
- append one message row
- clear current session messages

## API Shape

Virtua Agent-only endpoints:

- `GET /v1/chat-sessions/current/messages`
- `POST /v1/chat-sessions/current/messages`
- `DELETE /v1/chat-sessions/current/messages`

These routes are not OpenAI drop-in routes. They are app support APIs only. `/v1/chat/completions` and `/v1/models` keep their current OpenAI-compatible behavior.

Message DTO:

- `id`
- `role`
- `content`
- `reasoning`
- `created_at`

Append request:

- `role`
- `content`
- optional `reasoning`

## UI Behavior

Chat page load:

- fetch current session messages
- render them in the existing message list
- do not pass them as request context automatically

Send flow:

- save the user message row after the user sends
- send only the current user message to `/v1/chat/completions`; do not include prior saved or visible transcript rows
- save the assistant message row when streaming completes
- save final reasoning buckets with the assistant message

Clear flow:

- add a clear button near the Chat header controls
- call `DELETE /v1/chat-sessions/current/messages`
- clear local message and reasoning state

## Error Handling

- Failed load shows a red notification and leaves the chat empty.
- Failed user-message save shows a red notification but does not block the request.
- Failed assistant-message save shows a red notification after the response completes.
- Failed clear shows a red notification and keeps local state unchanged.

## Testing

Backend tests cover:

- schema initializes and creates current session
- appended messages reload in insertion order
- clear removes current session messages
- API returns saved messages and truncates current messages

Frontend build verifies TypeScript integration. Manual validation checks page refresh after sending and clear button behavior.
