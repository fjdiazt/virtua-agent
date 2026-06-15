export type ModelDto = {
  id: string;
  object?: string;
  owned_by?: string;
};

export type ModelListResponse = {
  object: string;
  data: ModelDto[];
};

export type ModelEndpoint = {
  id: string;
  name: string;
  base_url: string;
  has_api_key: boolean;
};

export type SaveModelEndpointRequest = {
  id?: string | null;
  name: string;
  base_url: string;
  api_key?: string | null;
};

export type AgentRequest = {
  endpoint_id?: string | null;
  model?: string | null;
  temperature?: number | null;
  max_tokens?: number | null;
};

export type OriginalMessagesInput = 'none' | 'text' | 'full';
export type PriorStageOutputInput = 'none' | 'last';

export type PipelineStageInput = {
  original_messages?: OriginalMessagesInput | null;
  prior_stage_output?: PriorStageOutputInput | null;
};

export type PipelineStage = {
  type: 'single_agent';
  name?: string | null;
  repeat: number;
  instructions?: string | null;
  protocol?: string | null;
  input?: PipelineStageInput | null;
  agent?: AgentRequest;
};

export type Pipeline = {
  default_endpoint_id?: string | null;
  default_model?: string | null;
  default_temperature?: number | null;
  default_max_tokens?: number | null;
  protocol?: string | null;
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
