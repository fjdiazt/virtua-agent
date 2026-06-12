# Cancel Current Message Design

## Goal

Allow the Chat page user to cancel the currently streaming assistant response while keeping an audit trail in the saved chat transcript.

## Decisions

- Use browser request cancellation with `AbortController`.
- Do not add a backend cancel endpoint in this version.
- Keep current OpenAI-compatible routes unchanged.
- Save the user message normally.
- Save the partial assistant response on cancel.
- Mark the saved assistant message as canceled so reload can show the canceled state.

## User Behavior

When no request is running, the composer shows the existing Send action.

When a request is running:

- the send action becomes Cancel
- clicking Cancel aborts the current fetch/read stream
- partial assistant text stays visible
- busy state stops
- the input remains empty because the prompt was already sent and saved

On page refresh:

- saved user messages load as before
- canceled assistant partials load and show a canceled indicator

## API And Runtime Behavior

The Chat page keeps using `/v1/chat/completions`. Canceling uses `AbortController.abort()` on the browser fetch. This closes the HTTP request. ASP.NET receives request cancellation through the existing request `CancellationToken`, which is already passed into upstream proxy and pipeline execution paths.

This means HTTP/API users can already cancel by closing their request or aborting their client call. No custom `/cancel` route is needed now.

## Persistence Shape

Extend saved chat messages with metadata:

- `metadata_json TEXT NULL` in SQLite
- `metadata?: Record<string, unknown> | null` in API/app DTOs

Canceled assistant messages store:

```json
{ "canceled": true }
```

Reasoning remains separate in `reasoning_json`.

## UI Rendering

Chat message rendering detects assistant messages with `metadata.canceled === true` and shows a compact canceled indicator near the assistant label.

During streaming, local message state can track canceled status without requiring a full saved-message UI refactor. The persisted load path maps saved metadata back into UI message state.

## Error Handling

- Abort errors caused by user cancel are not shown as red error notifications.
- Network or upstream errors still show red notifications.
- If saving the canceled assistant row fails, show a red notification after cancel completes.
- If cancel happens before any assistant content arrives, do not save an empty assistant row.

## Testing

Backend tests:

- metadata round-trips through the chat session store
- metadata round-trips through chat session API

Frontend verification:

- TypeScript build passes
- manual check: start chat, cancel mid-stream, partial assistant remains
- manual check: refresh page, canceled partial reloads with indicator

## Non-Goals

- no backend run cancellation endpoint
- no cancellation of already completed messages
- no session list UI
- no replay of saved chat history as prompt context
