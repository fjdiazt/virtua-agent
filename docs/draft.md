AI Agent Orchestration System — Implementation Canvas

Purpose

Build a local-first AI orchestration API that exposes an OpenAI-compatible /v1/chat/completions endpoint and can optionally run a configurable orchestration pipeline before returning the final answer.

The system starts with llama.cpp as the first AI backend, accessed through its OpenAI-compatible HTTP API.

The public API must remain OpenAI-compatible so existing OpenAI-compatible clients can call it normally.

Advanced behavior is enabled only through optional orchestration extension parameters.

Core Product Idea

This system is an AI orchestration layer.

It is not a llama.cpp-specific app.

It is not a full agent framework.

It is not a RAG system.

It is not a tool-using autonomous agent.

It does only this:

Accept OpenAI-compatible chat completion requests.
If no orchestration pipeline is provided, behave like a normal chat completion proxy.
If an orchestration pipeline is provided, execute the configured pipeline stages.
Return the final answer in an OpenAI-compatible response.
Optionally return trace/progress information for orchestration-aware clients.

The main architectural abstraction is:

Pipeline = ordered queue of stages.

Each stage receives the current pipeline context and may produce a new current answer.

Repeating stages or chaining different stage types replaces the old concept of a separate “iterative mode.”

There is no special top-level Iterative Mode.

Iteration is achieved by queue composition.

Examples:

single_agent

single_agent → single_agent → single_agent

council

council → chairman

council → chairman → single_agent

single_agent → council → chairman

MVP Scope

The MVP must implement:

OpenAI-compatible /v1/chat/completions endpoint.
Normal single-call chat completion when no orchestration pipeline is provided.
Optional orchestration pipeline object in the request.
Manual sequential pipeline execution.
single_agent stage.
council stage.
llama.cpp provider through IAiClient.
OpenAI-compatible final response.
Optional orchestration trace returned in response when requested.
Deterministic vote counting for council.
Clear separation between public API DTOs, domain models, application orchestration, and infrastructure provider clients.
Basic non-streaming behavior.

MVP should design for streaming, chairman, and early return later, but not implement them unless explicitly requested after MVP.

Out of Scope for MVP

Do not implement:

Memory.
RAG.
Vector search.
File reading.
File writing.
Web browsing.
Shell tools.
MCP.
Email/calendar.
Scheduling.
Autonomous background tasks.
Model downloading.
Hardware model recommendation.
Document workspace.
Multi-user collaboration.
Image generation.
Long-term persistence.
User accounts.
Authentication.
Billing.
Cloud sync.
Agent marketplace.
Prompt library.
Tool calling.
OpenRouter.
Provider marketplace.
Streaming.
Early Return / graceful cancellation.
Hard cancellation endpoint.
Random queue execution.
Conditional branching.
Repeat-until-approved.
Chairman stage implementation, unless trivial and explicitly approved.

Important Design Principle

Keep the MVP narrow, but do not paint the architecture into a corner.

The MVP should expose:

normal OpenAI-compatible chat completion
manual sequential pipeline
single_agent stage
council stage

But the architecture should make it easy to later add:

chairman stage
streaming events
repeated stage execution
random execution strategy
conditional execution
early return
council → chairman
council → chairman → single_agent
single_agent → council
nested/repeated refinement

Public API

Endpoint:

POST /v1/chat/completions

Default behavior:

If the request does not contain orchestration, behave like a normal OpenAI-compatible chat completion endpoint.

Normal request example:

{
"model": "local-model",
"messages": [
{
"role": "user",
"content": "Explain dependency injection."
}
],
"temperature": 0.7,
"max_tokens": 1024
}

Normal response example:

{
"id": "chatcmpl-local-123",
"object": "chat.completion",
"created": 1780850000,
"model": "local-model",
"choices": [
{
"index": 0,
"message": {
"role": "assistant",
"content": "Dependency injection is a design pattern..."
},
"finish_reason": "stop"
}
],
"usage": {
"prompt_tokens": 12,
"completion_tokens": 120,
"total_tokens": 132
}
}

The final answer must always be returned in:

choices[0].message.content

Extended Orchestration Request

Advanced behavior is enabled with an optional root-level orchestration object.

MVP orchestration request example:

{
"model": "local-model",
"messages": [
{
"role": "user",
"content": "Write a high-level architecture for a small AI orchestration system."
}
],
"temperature": 0.7,
"max_tokens": 2048,
"orchestration": {
"return_trace": true,
"pipeline": {
"strategy": "sequential",
"stages": [
{
"id": "stage-1",
"type": "single_agent",
"name": "Initial Draft",
"instruction": "Produce the best initial answer to the user request."
},
{
"id": "stage-2",
"type": "single_agent",
"name": "Refinement Pass",
"instruction": "Improve the current answer while preserving the original request and avoiding unrelated scope."
}
]
}
}
}

Council request example:

{
"model": "local-model",
"messages": [
{
"role": "user",
"content": "Compare SQLite and PostgreSQL for a small desktop app."
}
],
"temperature": 0.7,
"max_tokens": 2048,
"orchestration": {
"return_trace": true,
"pipeline": {
"strategy": "sequential",
"stages": [
{
"id": "stage-1",
"type": "council",
"name": "Council Vote",
"candidate_agent_count": 3,
"supervisor_voter_count": 3,
"run_candidates_in_parallel": true,
"run_supervisors_in_parallel": true,
"use_blind_candidate_ids": true,
"tie_break_mode": "highest_average_score_then_first_candidate"
}
]
}
}
}

Future non-MVP pipeline example:

{
"model": "local-model",
"messages": [
{
"role": "user",
"content": "Write an architecture proposal."
}
],
"orchestration": {
"return_trace": true,
"pipeline": {
"strategy": "sequential",
"stages": [
{
"id": "stage-1",
"type": "council",
"candidate_agent_count": 3,
"supervisor_voter_count": 3
},
{
"id": "stage-2",
"type": "chairman",
"instruction": "Synthesize the winning and useful council content into one final answer."
},
{
"id": "stage-3",
"type": "single_agent",
"instruction": "Refine the final answer for clarity without adding new scope."
}
]
}
}
}

The future example is only a design target. Do not implement chairman unless explicitly included in the MVP task.

Public API DTOs

Create DTOs separate from domain models.

public sealed class ChatCompletionRequest
{
public string Model { get; set; }
public IList Messages { get; set; }
public double? Temperature { get; set; }
public int? MaxTokens { get; set; }
public bool? Stream { get; set; }
public OrchestrationRequestDto Orchestration { get; set; }
}

public sealed class ChatMessageDto
{
public string Role { get; set; }
public string Content { get; set; }
}

public sealed class OrchestrationRequestDto
{
public bool ReturnTrace { get; set; }
public PipelineRequestDto Pipeline { get; set; }
}

public sealed class PipelineRequestDto
{
public PipelineExecutionStrategy Strategy { get; set; } = PipelineExecutionStrategy.Sequential;
public IList Stages { get; set; }
}

public sealed class PipelineStageRequestDto
{
public string Id { get; set; }
public PipelineStageType Type { get; set; }
public string Name { get; set; }
public string Instruction { get; set; }

public int? Repeat { get; set; }

public int? CandidateAgentCount { get; set; }
public int? SupervisorVoterCount { get; set; }
public bool? RunCandidatesInParallel { get; set; }
public bool? RunSupervisorsInParallel { get; set; }
public bool? UseBlindCandidateIds { get; set; }
public CouncilTieBreakMode? TieBreakMode { get; set; }

public AgentConfigDto Agent { get; set; }
public IList<AgentConfigDto> Agents { get; set; }
public IList<SupervisorConfigDto> Supervisors { get; set; }

}

public sealed class AgentConfigDto
{
public string Id { get; set; }
public string Name { get; set; }
public string SystemPrompt { get; set; }
public string Model { get; set; }
public double? Temperature { get; set; }
public int? MaxTokens { get; set; }
}

public sealed class SupervisorConfigDto
{
public string Id { get; set; }
public string Name { get; set; }
public string SystemPrompt { get; set; }
public string Model { get; set; }
public double? Temperature { get; set; }
public int? MaxTokens { get; set; }
}

Use strings in JSON, but map them to enums internally.

Recommended enums:

public enum PipelineExecutionStrategy
{
Sequential
}

public enum PipelineStageType
{
SingleAgent,
Council,
Chairman,
SupervisorReview
}

public enum CouncilTieBreakMode
{
HighestAverageScoreThenFirstCandidate
}

Only Sequential, SingleAgent, and Council need to be implemented in MVP.

Chairman and SupervisorReview may exist in the enum as future placeholders, but requests using unimplemented stage types should return a clear “not implemented” validation error.

Public Response DTOs

public sealed class ChatCompletionResponse
{
public string Id { get; set; }
public string Object { get; set; } = "chat.completion";
public long Created { get; set; }
public string Model { get; set; }
public IList Choices { get; set; }
public UsageDto Usage { get; set; }
public OrchestrationResponseDto Orchestration { get; set; }
}

public sealed class ChatCompletionChoiceDto
{
public int Index { get; set; }
public ChatMessageDto Message { get; set; }
public string FinishReason { get; set; }
}

public sealed class UsageDto
{
public int? PromptTokens { get; set; }
public int? CompletionTokens { get; set; }
public int? TotalTokens { get; set; }
}

public sealed class OrchestrationResponseDto
{
public string RunId { get; set; }
public string Status { get; set; }
public bool IsPipelineRun { get; set; }
public bool IsPartialResult { get; set; }
public OrchestrationTraceDto Trace { get; set; }
}

public sealed class OrchestrationTraceDto
{
public IList Stages { get; set; }
public IList CouncilCandidates { get; set; }
public IList SupervisorVotes { get; set; }
public IList CouncilDecisions { get; set; }
}

Compatibility Rules

No orchestration object means normal chat completion.
orchestration.pipeline means pipeline execution.
The final answer must always be returned in choices[0].message.content.
Trace data must be optional.
Trace data is returned only when orchestration.return_trace = true.
Unknown OpenAI request fields may be ignored for MVP.
stream: true should return a clear “streaming not supported in MVP” error.
Unimplemented stage types should return a clear validation error.
Invalid or empty pipeline should return a clear validation error.
Existing OpenAI-compatible clients should still receive a usable response when no orchestration is used.

Architecture Layers

Use five layers.

API / Presentation Layer

Responsible for:

Exposing /v1/chat/completions.
Receiving OpenAI-compatible DTOs.
Returning OpenAI-compatible DTOs.
Returning validation errors.

Must not:

Call llama.cpp directly.
Run orchestration logic.
Count votes.
Build prompts.
Know provider-specific response details.

Classes:

ChatCompletionsController.
ChatCompletionRequest.
ChatCompletionResponse.
DTOs.
API Adapter Layer

Responsible for mapping public API DTOs to internal domain/application models.

Classes:

OpenAiRequestMapper.
OpenAiResponseMapper.

Responsibilities:

Convert ChatCompletionRequest to AgentRunRequest.
Convert orchestration pipeline DTO to PipelineDefinition.
Apply defaults.
Convert AgentRunResult to ChatCompletionResponse.
Include trace only when requested.

Must not:

Call AI models.
Run pipeline stages.
Count votes.
Application Layer

Responsible for orchestration.

Classes:

AgentOrchestrator.
PipelineCompiler.
PipelineExecutor.
PipelineStageRegistry.
SingleAgentStageHandler.
CouncilStageHandler.
AgentFactory.
SupervisorVotingService.
CouncilVoteCounter.
PromptBuilder.

Responsibilities:

Compile request into executable pipeline.
Execute stage queue sequentially.
Maintain PipelineContext.
Update CurrentAnswer.
Add trace entries.
Run agents.
Run council candidates.
Run supervisor voters.
Count votes deterministically.

Must not:

Contain llama.cpp-specific HTTP code.
Return API DTOs directly.
Depend on controllers.
Domain Layer

Responsible for core models and abstractions.

Classes:

AgentRunRequest.
AgentRunResult.
PipelineDefinition.
PipelineStageDefinition.
PipelineContext.
PipelineStageResult.
PipelineStageTrace.
AgentConfig.
SupervisorConfig.
AgentResponse.
AgentStep.
CouncilCandidate.
SupervisorVote.
CouncilDecision.
AiRequest.
AiMessage.
AiResponse.

No dependency on ASP.NET, HTTP, llama.cpp, or infrastructure.

Infrastructure Layer

Responsible for provider integrations.

MVP classes:

LlamaCppClient.
LlamaCppOptions.

Responsibilities:

Map internal AiRequest to OpenAI-compatible llama.cpp request.
POST to llama.cpp /v1/chat/completions.
Parse response.
Return AiResponse.
Handle timeout/provider errors.

Must not:

Know about pipeline stages.
Know about council voting.
Know about prompt-building rules.

Core Internal Model

AgentRunRequest

public sealed class AgentRunRequest
{
public string RunId { get; init; }
public string Model { get; init; }
public string OriginalPrompt { get; init; }
public IList OriginalMessages { get; init; }
public double? Temperature { get; init; }
public int? MaxTokens { get; init; }
public bool ReturnTrace { get; init; }
public PipelineDefinition Pipeline { get; init; }
}

PipelineDefinition

public sealed class PipelineDefinition
{
public PipelineExecutionStrategy Strategy { get; init; } = PipelineExecutionStrategy.Sequential;
public IList Stages { get; init; } = new List();
}

PipelineStageDefinition

public sealed class PipelineStageDefinition
{
public string Id { get; init; }
public PipelineStageType Type { get; init; }
public string Name { get; init; }
public string Instruction { get; init; }
public int Repeat { get; init; } = 1;

public AgentConfig Agent { get; init; }
public IList<AgentConfig> Agents { get; init; } = new List<AgentConfig>();
public IList<SupervisorConfig> Supervisors { get; init; } = new List<SupervisorConfig>();

public CouncilStageOptions Council { get; init; }

}

CouncilStageOptions

public sealed class CouncilStageOptions
{
public int CandidateAgentCount { get; init; } = 3;
public int SupervisorVoterCount { get; init; } = 3;
public bool RunCandidatesInParallel { get; init; } = true;
public bool RunSupervisorsInParallel { get; init; } = true;
public bool UseBlindCandidateIds { get; init; } = true;
public CouncilTieBreakMode TieBreakMode { get; init; } = CouncilTieBreakMode.HighestAverageScoreThenFirstCandidate;
}

PipelineContext

public sealed class PipelineContext
{
public string RunId { get; init; }
public string OriginalPrompt { get; init; }
public IList OriginalMessages { get; init; }

public string CurrentAnswer { get; set; }

public IList<PipelineStageTrace> StageTraces { get; } = new List<PipelineStageTrace>();
public IList<AgentStep> AgentSteps { get; } = new List<AgentStep>();
public IList<CouncilCandidate> CouncilCandidates { get; } = new List<CouncilCandidate>();
public IList<SupervisorVote> SupervisorVotes { get; } = new List<SupervisorVote>();
public IList<CouncilDecision> CouncilDecisions { get; } = new List<CouncilDecision>();
public IList<string> Errors { get; } = new List<string>();

public IDictionary<string, object> Data { get; } = new Dictionary<string, object>();

}

AgentRunResult

public sealed class AgentRunResult
{
public string RunId { get; init; }
public string Model { get; init; }
public string FinalAnswer { get; init; }
public PipelineExecutionStatus Status { get; init; }
public bool IsPipelineRun { get; init; }
public bool IsPartialResult { get; init; }

public int? PromptTokens { get; init; }
public int? CompletionTokens { get; init; }

public IList<PipelineStageTrace> StageTraces { get; init; }
public IList<AgentStep> AgentSteps { get; init; }
public IList<CouncilCandidate> CouncilCandidates { get; init; }
public IList<SupervisorVote> SupervisorVotes { get; init; }
public IList<CouncilDecision> CouncilDecisions { get; init; }
public IList<string> Errors { get; init; }

}

public enum PipelineExecutionStatus
{
Pending,
Running,
Completed,
Failed
}

Pipeline Execution

AgentOrchestrator should not directly choose between separate “iterative” or “council” runners.

Instead:

OpenAiRequestMapper maps request to AgentRunRequest.
AgentRunRequest contains PipelineDefinition.
If no orchestration was requested, mapper creates a pipeline with one single_agent stage.
PipelineExecutor runs the queue.
Each stage updates PipelineContext.CurrentAnswer.
Final answer comes from PipelineContext.CurrentAnswer.

AgentOrchestrator

public sealed class AgentOrchestrator
{
private readonly IPipelineExecutor \_pipelineExecutor;

public async Task<AgentRunResult> RunAsync(
AgentRunRequest request,
CancellationToken cancellationToken = default)
{
return await \_pipelineExecutor.ExecuteAsync(request, cancellationToken);
}

}

PipelineExecutor

public interface IPipelineExecutor
{
Task ExecuteAsync(
AgentRunRequest request,
CancellationToken cancellationToken = default);
}

public sealed class PipelineExecutor : IPipelineExecutor
{
private readonly IPipelineStageRegistry \_stageRegistry;

public async Task<AgentRunResult> ExecuteAsync(
AgentRunRequest request,
CancellationToken cancellationToken = default)
{
// 1. Create PipelineContext.
// 2. Validate pipeline strategy.
// 3. For MVP, only sequential strategy is supported.
// 4. For each stage:
// - resolve handler
// - execute handler
// - update context
// - record trace
// 5. FinalAnswer = context.CurrentAnswer.
// 6. Return AgentRunResult.
}

}

Pipeline Stage Handler

public interface IPipelineStageHandler
{
PipelineStageType StageType { get; }

Task<PipelineStageResult> ExecuteAsync(
PipelineStageDefinition stage,
PipelineContext context,
CancellationToken cancellationToken = default);

}

PipelineStageResult

public sealed class PipelineStageResult
{
public PipelineContext Context { get; init; }
public bool ShouldStop { get; init; }
}

Stage Type: single_agent

Purpose:

Run one AI agent call using the original prompt and the current answer, if one exists.

Behavior:

If CurrentAnswer is empty, produce an initial answer from the original request.
If CurrentAnswer exists, treat the call as a refinement/transformation stage.
The stage instruction tells the agent what to do.
The result becomes the new CurrentAnswer.
Add stage trace and agent step.

Prompt template for initial answer:

System:

You are an AI agent. Produce the best possible answer to the user request. Follow the user request closely. Do not add unrelated scope.

User:

Original user request:

{{originalPrompt}}

Stage instruction:

{{stageInstruction}}

Return only the answer.

Prompt template for refinement:

System:

You are an AI refinement agent. Your job is to improve or transform the current answer according to the stage instruction while staying aligned with the original user request.

Do not introduce unrelated scope.

User:

Original user request:

{{originalPrompt}}

Current answer:

{{currentAnswer}}

Stage instruction:

{{stageInstruction}}

Return only the improved answer.

Stage Type: council

Purpose:

Generate multiple independent candidate answers, then have supervisor voters select the best candidate. Application code counts the votes deterministically.

Council is a compound stage.

Internally it does:

Candidate generation.
Supervisor voting.
Deterministic vote counting.
Winner selection.
CurrentAnswer = winning candidate response.

Council behavior:

Candidate agents receive the original prompt.
Candidate agents should not see each other’s answers.
Supervisor voters receive the original prompt and all candidate answers.
Supervisor voters select exactly one candidate.
Application code counts votes.
The candidate with the most votes wins.
Tie-breaking is deterministic.
The winning candidate response becomes PipelineContext.CurrentAnswer.

Candidate prompt template:

System:

You are a candidate agent in an AI council.

Produce the strongest possible answer to the original user request.

Work independently.

Do not mention other agents.

Do not add unrelated scope.

User:

Original user request:

{{originalPrompt}}

Stage instruction:

{{stageInstruction}}

Task:

Produce your best answer.

Return only the answer.

Supervisor voting prompt template:

System:

You are a supervisor voter in an AI council.

Your job is to evaluate candidate answers against the original user request and vote for the single best candidate.

You are not producing the final answer.

You are only voting.

Judge based on:

Requirement coverage.
Correctness.
Clarity.
Usefulness.
Constraint-following.
Lack of unnecessary scope.
Overall answer quality.

User:

Original user request:

{{originalPrompt}}

Candidate answers:

{{candidateResponses}}

Task:

Vote for exactly one candidate.

Return JSON only:

{
"selectedCandidateId": "candidate-id",
"score": 1,
"explanation": "short explanation",
"candidateRanking": ["candidate-id-1", "candidate-id-2", "candidate-id-3"]
}

Rules:

Select exactly one candidate.
Do not invent candidate IDs.
Do not rewrite the answer.
Do not create a new answer.
Do not vote for a candidate that fails the original request.
If several candidates are close, choose the one that best satisfies the original request.

Blind Candidate IDs

When use_blind_candidate_ids is true, supervisor voters should see:

Candidate A
Candidate B
Candidate C

They should not see:

agent names
model names
provider names
configured roles

The app internally maps blind candidate IDs to actual candidate IDs.

Vote Counting

Vote counting must be deterministic application logic.

Do not ask an AI to count votes.

Tie-breaking:

Highest vote count wins.
If tied, highest average supervisor score wins.
If still tied, earliest candidate wins.

Tie-break result must be recorded in CouncilDecision.

Invalid Supervisor JSON

Supervisor voting asks for JSON, but models may fail.

Handling:

Try to parse JSON.
If parsing fails, retry once with a JSON repair prompt.
If it still fails, record raw response and mark the vote invalid.
Continue if at least one valid vote exists.
If no valid votes exist, fail the council stage.

Council Failure Rules

If one candidate fails:

Record the error.
Continue if at least one candidate succeeds.

If all candidates fail:

Fail the council stage.

If one supervisor fails:

Record the error.
Continue if at least one valid vote exists.

If no valid supervisor votes exist:

Fail the council stage.

Stage Type: chairman

Chairman is not required for MVP.

Design note only.

A future chairman stage would:

Read original prompt.
Read CurrentAnswer.
Optionally read candidates, votes, and council decisions.
Produce a synthesized or polished final answer.
Update CurrentAnswer.

Chairman differs from council voting:

Council selects an existing candidate answer.
Chairman creates a new synthesized answer.

Do not implement chairman in MVP unless explicitly requested.

llama.cpp Integration

The llama.cpp integration lives only in Infrastructure.

The app calls llama.cpp through its OpenAI-compatible API.

Example server command:

llama-server -m model.gguf --ctx-size 8192 --port 8080

Expected endpoint:

POST http://localhost:8080/v1/chat/completions

LlamaCppOptions

public sealed class LlamaCppOptions
{
public string BaseUrl { get; init; }
public string DefaultModel { get; init; }
public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(5);
}

IAiClient

public interface IAiClient
{
Task ChatAsync(
AiRequest request,
CancellationToken cancellationToken = default);
}

LlamaCppClient

public sealed class LlamaCppClient : IAiClient
{
private readonly HttpClient \_httpClient;
private readonly LlamaCppOptions \_options;

public async Task<AiResponse> ChatAsync(
AiRequest request,
CancellationToken cancellationToken = default)
{
// Map AiRequest to OpenAI-compatible chat completion request.
// POST to /v1/chat/completions.
// Parse choices[0].message.content.
// Parse usage if available.
// Return AiResponse.
}

}

No application-layer class should reference LlamaCppClient directly.

Use dependency injection with IAiClient.

Do not include OpenRouter in MVP.

OpenAI-compatible public API does not mean OpenRouter is required.

OpenRouter may be added later as another IAiClient implementation, but it is out of scope now.

Internal AI Models

AiRequest

public sealed class AiRequest
{
public string Model { get; init; }
public IList Messages { get; init; }
public double? Temperature { get; init; }
public int? MaxTokens { get; init; }
}

AiMessage

public sealed class AiMessage
{
public string Role { get; init; }
public string Content { get; init; }
}

AiResponse

public sealed class AiResponse
{
public string Content { get; init; }
public string Model { get; init; }
public int? PromptTokens { get; init; }
public int? CompletionTokens { get; init; }
public TimeSpan Duration { get; init; }
public string RawResponse { get; init; }
}

Separation of Concerns

ChatCompletionsController

Responsible for:

Receiving HTTP request.
Calling OpenAiRequestMapper.
Calling AgentOrchestrator.
Calling OpenAiResponseMapper.
Returning HTTP response.

Not responsible for:

Calling llama.cpp.
Building prompts.
Running pipeline stages.
Counting votes.

OpenAiRequestMapper

Responsible for:

Mapping public request DTOs to AgentRunRequest.
Creating default single-agent pipeline when no orchestration exists.
Applying default values.
Validating request shape.

Not responsible for:

Calling models.
Executing pipeline.

OpenAiResponseMapper

Responsible for:

Mapping AgentRunResult to ChatCompletionResponse.
Placing final answer in choices[0].message.content.
Including trace when requested.
Omitting trace when not requested.

Not responsible for:

Selecting final answer.
Counting votes.

PipelineExecutor

Responsible for:

Executing pipeline queue.
Running stages in order.
Maintaining PipelineContext.
Returning AgentRunResult.

Not responsible for:

Provider-specific HTTP calls.
API DTO mapping.
Stage-specific logic.

SingleAgentStageHandler

Responsible for:

Building one agent request.
Calling IAiClient.
Updating CurrentAnswer.

Not responsible for:

Running the whole pipeline.
Counting votes.

CouncilStageHandler

Responsible for:

Candidate generation.
Supervisor voting.
Calling CouncilVoteCounter.
Updating CurrentAnswer with winning candidate.

Not responsible for:

Counting votes internally if that logic belongs to CouncilVoteCounter.
Provider-specific HTTP.

CouncilVoteCounter

Responsible for:

Deterministic vote counting.
Tie-breaking.
Producing CouncilDecision.

Not responsible for:

Calling AI.
Modifying candidate answers.

PromptBuilder

Responsible for:

Building messages for single_agent.
Building candidate prompts.
Building supervisor voting prompts.
Building future chairman prompts.

Not responsible for:

Calling AI.
Running stages.

LlamaCppClient

Responsible for:

Provider communication.
HTTP request/response mapping.
Provider error handling.

Not responsible for:

Pipeline behavior.
Council behavior.
Prompt decisions.

Suggested Folder Structure

/src
/Domain
AgentRunRequest.cs
AgentRunResult.cs
PipelineDefinition.cs
PipelineStageDefinition.cs
PipelineContext.cs
PipelineStageResult.cs
PipelineStageTrace.cs
AgentConfig.cs
SupervisorConfig.cs
AgentStep.cs
AgentResponse.cs
CouncilCandidate.cs
SupervisorVote.cs
CouncilDecision.cs
AiRequest.cs
AiResponse.cs
AiMessage.cs
Enums.cs

/Application
/Abstractions
IAiClient.cs
IPipelineExecutor.cs
IPipelineStageHandler.cs
IPipelineStageRegistry.cs
IPromptBuilder.cs
ICouncilVoteCounter.cs

/Orchestration
AgentOrchestrator.cs
PipelineExecutor.cs
PipelineStageRegistry.cs

/Stages
SingleAgentStageHandler.cs
CouncilStageHandler.cs

/Council
CouncilVoteCounter.cs
SupervisorVoteParser.cs

/Prompts
PromptBuilder.cs
PromptTemplates.cs

/Infrastructure
/AiClients
LlamaCppClient.cs
LlamaCppOptions.cs

/Api
/OpenAiCompatibility
ChatCompletionsController.cs
ChatCompletionRequest.cs
ChatCompletionResponse.cs
ChatCompletionChoiceDto.cs
ChatMessageDto.cs
UsageDto.cs
OrchestrationRequestDto.cs
OrchestrationResponseDto.cs
PipelineRequestDto.cs
PipelineStageRequestDto.cs
AgentConfigDto.cs
SupervisorConfigDto.cs
OpenAiRequestMapper.cs
OpenAiResponseMapper.cs

Testing Strategy

Unit Tests

OpenAiRequestMapper tests:

Normal OpenAI-compatible request maps to one-stage single_agent pipeline.
Request with orchestration pipeline maps correctly.
Missing orchestration defaults to normal chat.
Empty pipeline is rejected.
Unsupported strategy is rejected.
Unsupported stage type is rejected.
Defaults are applied for council options.
Original messages are preserved.
Original prompt is extracted from user messages.

OpenAiResponseMapper tests:

Final answer appears in choices[0].message.content.
OpenAI-compatible response shape is valid.
Trace is omitted when ReturnTrace is false.
Trace is included when ReturnTrace is true.
Usage values are mapped when available.
Failed results map to clear error behavior.

PipelineExecutor tests:

Executes stages in order.
Passes CurrentAnswer from one stage to the next.
Stops on failed stage.
Returns final CurrentAnswer.
Records stage traces.
Handles single-stage normal pipeline.

SingleAgentStageHandler tests:

Calls IAiClient once.
Uses original prompt when CurrentAnswer is empty.
Uses CurrentAnswer when refining.
Updates CurrentAnswer.
Records agent step.
Handles provider failure.

CouncilStageHandler tests:

Runs configured number of candidates.
Runs configured number of supervisors.
Supports parallel candidate execution.
Supports parallel supervisor execution.
Uses blind candidate IDs when enabled.
Updates CurrentAnswer with winning candidate.
Records candidates, votes, and decision.
Handles one candidate failure when others succeed.
Fails if all candidates fail.
Handles one supervisor failure when valid votes remain.
Fails if no valid votes exist.

CouncilVoteCounter tests:

Candidate with most votes wins.
Tie uses highest average score.
If still tied, earliest candidate wins.
Invalid candidate IDs are ignored or recorded.
Missing scores do not crash the system.
Tie-break metadata is recorded.

PromptBuilder tests:

Builds single_agent initial prompt correctly.
Builds single_agent refinement prompt correctly.
Builds council candidate prompt correctly.
Builds supervisor voting prompt correctly.
Does not expose real agent/model names when blind IDs are enabled.

LlamaCppClient integration tests:

Sends request to /v1/chat/completions.
Parses response content.
Parses usage when available.
Handles timeout.
Handles empty response.
Handles provider error response.

API integration tests:

POST /v1/chat/completions normal request works.
POST /v1/chat/completions with single_agent pipeline works.
POST /v1/chat/completions with council pipeline works.
Final answer is always in choices[0].message.content.
Trace appears only when requested.
stream true returns MVP “not supported” response.

Error Handling

API validation errors:

Missing messages.
Empty messages.
Empty pipeline.
Unsupported pipeline strategy.
Unsupported stage type.
Invalid candidate count.
Invalid supervisor count.
stream true in MVP.

Single agent errors:

Record provider error.
Mark run failed.
Return clear error.

Council errors:

Candidate failure is recorded.
Continue if at least one candidate succeeds.
Fail if all candidates fail.
Supervisor failure is recorded.
Continue if at least one valid vote exists.
Fail if no valid votes exist.
Invalid JSON vote gets one repair retry.
If repair fails, vote is invalid.

Context Management

Normal chat:

Send original OpenAI messages to provider.
Do not add orchestration instructions unless executing a pipeline stage.
Behave like a normal chat completion.

single_agent pipeline:

Always include original user request.
Include CurrentAnswer only if it exists.
Include stage instruction.

council pipeline:

Candidates receive original prompt and stage instruction.
Candidates do not see other candidate answers.
Supervisors receive original prompt and candidate answers.
Supervisors receive blind IDs if enabled.
Supervisors do not see real model names by default.

MVP Build Order

Create project structure.
Define domain models and enums.
Define public API DTOs.
Define IAiClient.
Implement LlamaCppClient.
Implement OpenAiRequestMapper.
Implement OpenAiResponseMapper.
Implement PromptBuilder.
Implement PipelineStageRegistry.
Implement PipelineExecutor.
Implement SingleAgentStageHandler.
Implement CouncilVoteCounter.
Implement SupervisorVoteParser.
Implement CouncilStageHandler.
Implement AgentOrchestrator.
Implement ChatCompletionsController.
Add unit tests for mappers.
Add unit tests for PipelineExecutor.
Add unit tests for SingleAgentStageHandler.
Add unit tests for CouncilVoteCounter.
Add unit tests for CouncilStageHandler.
Add LlamaCppClient integration test.
Add API integration tests.
Verify with a running llama.cpp server.

MVP Acceptance Criteria

OpenAI Compatibility

Client can call POST /v1/chat/completions.
Normal request works without orchestration fields.
Normal request produces OpenAI-compatible response.
Final answer is returned in choices[0].message.content.
llama.cpp is used through IAiClient.
No controller depends directly on LlamaCppClient.

Normal Chat Completion

System sends one request to llama.cpp.
System returns one assistant answer.
Response shape remains OpenAI-compatible.

Pipeline Execution

Request can include orchestration pipeline.
MVP supports sequential strategy.
Pipeline stages run in order.
CurrentAnswer passes from stage to stage.
Final CurrentAnswer becomes the final API answer.
Trace is returned when requested.
Trace is omitted when not requested.

Single Agent Stage

Stage can produce initial answer.
Stage can refine CurrentAnswer.
Stage uses stage instruction.
Stage records trace.

Council Stage

Council generates multiple independent candidate answers.
Council sends candidates to multiple supervisor voters.
Supervisor voters vote for exactly one candidate.
Application code counts votes.
Answer with most votes wins.
Ties are resolved deterministically.
Winning candidate answer becomes CurrentAnswer.
Council trace includes candidates, votes, vote counts, and decision.
Blind candidate IDs hide real agent names from supervisors when enabled.

Architecture

Public API DTOs are separate from domain models.
API mapper converts public request to domain request.
Response mapper converts domain result to OpenAI-compatible response.
PipelineExecutor is the core orchestration mechanism.
There is no separate hardcoded Iterative Mode.
Repetition/iteration is achieved by pipeline queue composition.
No pipeline or stage handler depends directly on llama.cpp.
No out-of-scope features are implemented.

Future Notes / Nice-to-Haves

These are not MVP requirements.

Streaming

Future streaming should support two layers:

Provider token streaming from llama.cpp.
Orchestration event streaming from the pipeline.

Important future rule:

Only final-answer tokens should be streamed as OpenAI-compatible choices[].delta.content.

Intermediate candidate tokens, reasoning, votes, stage transitions, and trace events should be streamed as orchestration events.

Potential future event types:

run_started
stage_started
agent_started
agent_delta
agent_reasoning_delta
agent_completed
candidate_completed
supervisor_started
supervisor_vote_received
vote_count_updated
stage_completed
stage_failed
pipeline_completed

Future IAiClient streaming shape:

public interface IAiClient
{
Task ChatAsync(
AiRequest request,
CancellationToken cancellationToken = default);

IAsyncEnumerable<AiStreamEvent> StreamChatAsync(
AiRequest request,
CancellationToken cancellationToken = default);

}

Do not implement streaming in MVP unless explicitly requested.

Chairman Stage

Future chairman stage can synthesize or polish the result after council voting.

Council selects an existing answer.

Chairman creates a new synthesized answer.

Potential future pipeline:

council → chairman

or:

council → chairman → single_agent

Do not implement in MVP unless explicitly requested.

Stage Repeat

Future stage definitions may support repeat:

{
"type": "single_agent",
"repeat": 3
}

This would replace old “iteration count” behavior.

For MVP, repeat may be parsed but should either:

support only repeat = 1
or implement simple repeat if trivial

Do not let repeat expand scope.

Early Return

Future Early Return means:

The user can stop a running pipeline and receive the best available result produced so far.

This is different from hard cancellation.

Early Return should return the earliest valid returnable result according to the current stage.

Examples:

single_agent may finish current call and return it.
parallel council may cancel unfinished agents and return first completed candidate.
supervisor voting may count votes already received.
chairman may return previous CurrentAnswer if not finished.

Early Return is not part of MVP.

The MVP does not need:

cancellation endpoint
run control API
partial result recovery
stage-specific cancel policies
early-return UI
Random / Conditional Queue

Future pipeline strategies may include:

random
conditional
repeat until approved
branch and merge

MVP only supports sequential.

Additional Providers

Future providers may include:

OpenAI
OpenRouter
Ollama
LM Studio
vLLM

Each provider should be added as another IAiClient implementation.

Do not implement OpenRouter or provider routing in MVP.

Final Design Reminder

Keep the system narrow.

Build one clean pipeline executor and a small set of stage handlers.

The MVP should feel like:

OpenAI-compatible request
→ optional sequential pipeline
→ single_agent and/or council stages
→ final answer in OpenAI-compatible response

Everything else is future scope.
