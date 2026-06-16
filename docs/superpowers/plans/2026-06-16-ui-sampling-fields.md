# UI Sampling Fields Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add typed UI support for pipeline and per-stage sampling options already supported by the API.

**Architecture:** Extend the existing React model editor DTO types and form bindings. Keep advanced sampling as individual nullable `NumberInput` fields so import/export JSON remains typed and no ad hoc JSON blob is introduced.

**Tech Stack:** React, TypeScript, Mantine, Vite.

---

## File Structure

- Modify `src/virtua-agent-app/src/types.ts`
  - Add nullable fields to `AgentRequest` and `Pipeline`.
- Modify `src/virtua-agent-app/src/App.tsx`
  - Initialize new fields in `emptyStage` and `emptyModel`.
  - Add helper for nullable number conversion.
  - Add pipeline default sampling fields.
  - Add stage sampling fields.
- Modify `src/virtua-agent-app/src/styles.css`
  - Adjust stage sampling grid to fit six controls on desktop and two columns on mobile.

---

### Task 1: Extend Frontend Types

**Files:**
- Modify: `src/virtua-agent-app/src/types.ts`

- [x] **Step 1: Add typed sampling fields**

Add these properties to `AgentRequest` after `temperature`:

```ts
top_p?: number | null;
top_k?: number | null;
min_p?: number | null;
repeat_penalty?: number | null;
```

Add these properties to `Pipeline` after `default_temperature`:

```ts
default_top_p?: number | null;
default_top_k?: number | null;
default_min_p?: number | null;
default_repeat_penalty?: number | null;
```

- [x] **Step 2: Run TypeScript build to verify current missing bindings are not introduced**

Run: `npm run build --prefix src/virtua-agent-app`

Expected: build still passes.

---

### Task 2: Bind Pipeline Default Sampling Fields

**Files:**
- Modify: `src/virtua-agent-app/src/App.tsx`

- [x] **Step 1: Add nullable number helper**

Add near the other small helpers:

```ts
function nullableNumber(value: string | number | null | undefined) {
  return value === '' || value === null || value === undefined ? null : Number(value);
}
```

- [x] **Step 2: Initialize pipeline defaults**

Update `emptyModel` pipeline object:

```ts
default_temperature: 0.2,
default_top_p: null,
default_top_k: null,
default_min_p: null,
default_repeat_penalty: null,
default_max_tokens: 512,
```

- [x] **Step 3: Add default sampling controls**

Replace the current default `Group grow align="end"` contents so the first row remains identity/model controls and sampling moves to a grid:

```tsx
<Group grow align="end">
  <TextInput ... />
  <Select label="Default endpoint" ... />
  <Select label="Default model" ... />
</Group>
<Box className="sampling-grid">
  <NumberInput label="Default temperature" min={0} max={2} step={0.1} ... />
  <NumberInput label="Default top P" min={0} max={1} step={0.05} ... />
  <NumberInput label="Default top K" min={0} step={1} ... />
  <NumberInput label="Default min P" min={0} max={1} step={0.01} ... />
  <NumberInput label="Default repeat penalty" min={0} step={0.05} ... />
  <NumberInput label="Default max tokens" min={1} ... />
</Box>
```

Each `onChange` must update `draft.pipeline.<field>` using `nullableNumber(value)`.

- [x] **Step 4: Run app build**

Run: `npm run build --prefix src/virtua-agent-app`

Expected: TypeScript and Vite build pass.

---

### Task 3: Bind Stage Sampling Fields

**Files:**
- Modify: `src/virtua-agent-app/src/App.tsx`
- Modify: `src/virtua-agent-app/src/styles.css`

- [x] **Step 1: Initialize empty stage agent**

Update `emptyStage` agent:

```ts
agent: {
  endpoint_id: null,
  model: null,
  temperature: null,
  top_p: null,
  top_k: null,
  min_p: null,
  repeat_penalty: null,
  max_tokens: null
}
```

- [x] **Step 2: Replace stage secondary grid controls**

Keep the existing `Box className="stage-secondary-grid"`, but include six controls:

```tsx
<NumberInput label="Temperature" min={0} max={2} step={0.1} ... />
<NumberInput label="Top P" min={0} max={1} step={0.05} ... />
<NumberInput label="Top K" min={0} step={1} ... />
<NumberInput label="Min P" min={0} max={1} step={0.01} ... />
<NumberInput label="Repeat penalty" min={0} step={0.05} ... />
<NumberInput label="Max tokens" min={1} ... />
```

Each `onChange` must update `stage.agent.<field>` using `nullableNumber(value)`.

- [x] **Step 3: Adjust grid CSS**

Change `.stage-secondary-grid` to three columns on desktop:

```css
.sampling-grid,
.stage-secondary-grid {
  display: grid;
  gap: 12px;
  grid-template-columns: repeat(3, minmax(0, 1fr));
}
```

Keep mobile at two columns by adding `.sampling-grid` to the existing mobile rule:

```css
.sampling-grid,
.stage-primary-grid,
.stage-secondary-grid {
  grid-template-columns: repeat(2, minmax(0, 1fr));
}
```

- [x] **Step 4: Run app build**

Run: `npm run build --prefix src/virtua-agent-app`

Expected: TypeScript and Vite build pass.

---

### Task 4: Final Verification And Commit

**Files:**
- Verify all modified files.

- [x] **Step 1: Run frontend build**

Run: `npm run build --prefix src/virtua-agent-app`

Expected: build passes.

- [x] **Step 2: Run backend tests**

Run: `$out = Join-Path $env:TEMP 'virtua-agent-test-bin\'; dotnet test src/virtua-agent-api/VirtuaAgent.slnx -p:UseAppHost=false -p:OutDir=$out`

Expected: all tests pass.

- [x] **Step 3: Commit UI work**

Run:

```powershell
git add docs/superpowers/plans/2026-06-16-ui-sampling-fields.md src/virtua-agent-app/src/types.ts src/virtua-agent-app/src/App.tsx src/virtua-agent-app/src/styles.css
git commit -m "feat: add sampling controls to model editor"
```

Expected: commit created on current branch.

---

## Self-Review

- Spec coverage: typed UI fields cover pipeline defaults and stage agent overrides; root chat UI is intentionally out of scope.
- Placeholder scan: no TODO/TBD placeholders.
- Type consistency: frontend names match API JSON field names already added: `top_p`, `top_k`, `min_p`, `repeat_penalty`, plus `default_` pipeline variants.
