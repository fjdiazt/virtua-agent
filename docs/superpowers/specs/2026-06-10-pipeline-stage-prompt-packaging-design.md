# Pipeline Stage Prompt Packaging Design

## Goal

Make pipeline stage prompts explicit and predictable without adding hidden behavior. When a chat request uses a pipeline, each stage should receive clearly labeled inputs: the original conversation, the previous stage output when one exists, and the current stage instruction.

Zero-stage chat remains pass-through and is not affected by this design.

## Current Context

`PipelineExecutor` currently builds each stage request by copying the original chat messages. For later stages, it appends the previous answer as an assistant message and appends stage instructions as another user message. If later-stage instructions are empty, it injects a hardcoded revision instruction.

That works for simple cases, but it leaves instruction priority ambiguous. The original user request and the stage instruction are both ordinary chat messages, so a model can blend them or treat the original task as still active when a later stage is meant to transform the previous result.

## Prompt Packaging

Pipeline mode should generate explicit stage prompts.

For expanded execution index `0`, the stage request uses the original conversation as the task. If the first stage has an instruction, append a labeled stage instruction section. If the first stage instruction is empty, send the original conversation unchanged.

For expanded execution index `1+`, the stage request contains a generated user message with these sections:

```text
Original conversation:
<serialized original conversation>

Previous stage output:
<previous answer>

Stage instruction:
<current stage instruction>
```

The executor should not decide whether the stage instruction complements or overrides the original chat. That is up to the user's wording. The labels only package inputs; they are not extra behavioral policy.

## Instruction Validation

The only expanded execution that may omit instructions is execution index `0`.

Examples:

- One stage, repeat `1`, empty instructions: valid. The pipeline first agent answers the original chat.
- One stage, repeat `2`, empty instructions: invalid. The second execution has previous output but no task.
- Two stages where stage 1 instructions are empty and stage 2 instructions are set: valid.
- Two stages where stage 2 instructions are empty: invalid.

This removes the current hidden fallback revision instruction. Later stages must be explicit because the system should not invent transformation semantics.

## Data Flow

The pipeline still executes sequentially:

1. Compile and validate stages.
2. Expand repeats into execution indexes.
3. Build each upstream `ChatCompletionRequest`.
4. Send the request to the selected model.
5. Store the answer as `CurrentAnswer`.
6. Use `CurrentAnswer` as `Previous stage output` for the next execution.

Model override, temperature, max tokens, random agent selection, trace events, and result handling remain unchanged.

## Error Handling

If an expanded execution after index `0` has empty instructions, return a `PipelineValidationException` before sending that stage to the upstream model.

Use a specific parameter path when possible:

- `orchestration.pipeline.stages[0].instructions` for repeated stage 1 execution 2
- `orchestration.pipeline.stages[1].instructions` for stage 2

The error message should explain that only the first pipeline execution may omit instructions.

## Testing

Add or update `PipelineExecutorTests` to cover:

- First stage with empty instructions sends the original conversation unchanged.
- First stage with instructions includes both original conversation and labeled stage instruction.
- Later stage with instructions includes original conversation, previous output, and labeled stage instruction.
- Later stage with empty instructions is rejected.
- A repeated first stage with repeat `2` and empty instructions is rejected.
- The old hardcoded revision fallback is no longer used.
