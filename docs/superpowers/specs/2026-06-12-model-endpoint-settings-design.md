# Model Endpoint Settings Design

## Goal

Add a Settings page where users can manage OpenAI-compatible model endpoints, then select endpoint and model pairs in Virtua Agent stage forms.

## Decisions

- Store endpoint definitions in SQLite so settings persist across app restarts and are shared by API and app.
- Support optional API keys. `llama.cpp` usually runs without one, but hosted or proxied endpoints often need bearer auth.
- Store stage model selection as `endpoint_id` plus `model`, not as a combined string. This avoids ambiguity when multiple endpoints expose the same model id.
- Use live `/v1/models` calls when the Settings page opens or an endpoint is selected. No cached model list in this version.
- Apply endpoint selection to Virtua Agent stage forms.

## Backend Shape

Add a model endpoint module with:

- `ModelEndpointDefinition`: internal stored row, including optional API key.
- `ModelEndpointDto`: API response shape, excluding API key.
- `SaveModelEndpointRequest`: API input, including optional API key.
- `IModelEndpointStore`: persistence boundary.
- `SqliteModelEndpointStore`: SQLite implementation using the existing trace store connection string.

Add endpoints:

- `GET /v1/model-endpoints`: list configured endpoints without API keys.
- `POST /v1/model-endpoints`: create or update one endpoint.
- `DELETE /v1/model-endpoints/{id}`: delete one endpoint.
- `GET /v1/model-endpoints/{id}/models`: call that endpoint's `/v1/models` live.

The existing configured `Upstream` remains available as a default endpoint for backward compatibility. Pipeline requests without `endpoint_id` continue to use the default upstream.

## Request Model Changes

`AgentRequestDto` gains `endpoint_id`.

Pipeline execution resolves stage calls in this order:

1. stage agent `endpoint_id`
2. pipeline default `endpoint_id`
3. configured default upstream

Pipeline execution resolves model in this order:

1. stage agent `model`
2. pipeline default `model`
3. validation error if neither exists

Top-level OpenAI-compatible requests may also carry an optional endpoint id through an extension field so callers can target a selected endpoint directly.

## UI Shape

Add a Settings nav item and page:

- list saved endpoints
- edit `Name`, `Base URL`, and optional `API key`
- save/delete endpoints
- refresh/view live models for selected endpoint

Update Models:

- default pipeline settings include default endpoint plus default model
- each stage has endpoint selector plus model selector
- model selector reloads from selected endpoint

## Error Handling

- Invalid endpoint id returns OpenAI-compatible error with `invalid_endpoint`.
- Failed live model fetch returns normal HTTP error text to the UI notification.
- Saved endpoint responses never include API keys.
- Delete removes endpoint row only; existing saved pipeline models can still reference that endpoint id and will fail validation/execution until edited.

## Testing

Backend tests cover:

- endpoint CRUD omits API keys from responses
- endpoint model listing calls the selected base URL
- top-level OpenAI-compatible requests can route to a selected endpoint
- pipeline stage can route to a selected endpoint
- missing endpoint id returns a clear error

Frontend build verifies TypeScript and route integration.
