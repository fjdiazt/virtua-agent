export type ModelDto = {
  id: string;
  object?: string;
  owned_by?: string;
};

export type ModelListResponse = {
  object: string;
  data: ModelDto[];
};

export type ChatMessage = {
  role: 'system' | 'user' | 'assistant';
  content: string;
};

export type AgentRequest = {
  model?: string | null;
  temperature?: number | null;
  max_tokens?: number | null;
};

export type PipelineStage = {
  type: 'single_agent';
  name?: string | null;
  repeat: number;
  instructions?: string | null;
  agent?: AgentRequest;
};

export type Pipeline = {
  default_model?: string | null;
  default_temperature?: number | null;
  default_max_tokens?: number | null;
  stages: PipelineStage[];
};

export type VirtuaAgentModel = {
  id: string;
  ownedBy?: string | null;
  pipeline: Pipeline;
};

export type TraceEvent = {
  type: string;
  json: string;
};
