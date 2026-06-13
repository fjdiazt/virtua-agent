import type {
  ModelDto,
  ModelEndpoint,
  ModelListResponse,
  SaveModelEndpointRequest,
  VirtuaAgentModel
} from './types';

async function readJson<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const text = await response.text();
    throw new Error(errorMessageFrom(text, response.status));
  }

  return response.json() as Promise<T>;
}

function errorMessageFrom(text: string, status: number) {
  if (!text) return `HTTP ${status}`;

  try {
    const parsed = JSON.parse(text);
    if (typeof parsed?.error?.message === 'string') {
      return parsed.error.message;
    }
  } catch {
    return text;
  }

  return text;
}

export async function listUpstreamModels(): Promise<string[]> {
  const models = await listModelDtos();
  return models
    .filter((model) => model.owned_by !== 'virtua-agent')
    .map((model) => model.id)
    .filter(Boolean);
}

export async function listEndpointModels(endpointId: string): Promise<string[]> {
  const response = await fetch(`/v1/model-endpoints/${endpointId}/models`);
  const body = await readJson<ModelListResponse>(response);
  return body.data.map((model) => model.id).filter(Boolean);
}

async function listModelDtos(): Promise<ModelDto[]> {
  const response = await fetch('/v1/models');
  const body = await readJson<ModelListResponse>(response);
  return body.data;
}

export async function listVirtuaAgentModels(): Promise<VirtuaAgentModel[]> {
  const response = await fetch('/v1/pipeline-models');
  return readJson<VirtuaAgentModel[]>(response);
}

export async function listModelEndpoints(): Promise<ModelEndpoint[]> {
  const response = await fetch('/v1/model-endpoints');
  return readJson<ModelEndpoint[]>(response);
}

export async function saveModelEndpoint(endpoint: SaveModelEndpointRequest): Promise<ModelEndpoint> {
  const response = await fetch('/v1/model-endpoints', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(endpoint)
  });
  return readJson<ModelEndpoint>(response);
}

export async function deleteModelEndpoint(id: string): Promise<void> {
  const response = await fetch(`/v1/model-endpoints/${id}`, { method: 'DELETE' });
  if (!response.ok) {
    throw new Error(errorMessageFrom(await response.text(), response.status));
  }
}

export async function saveVirtuaAgentModel(model: VirtuaAgentModel): Promise<VirtuaAgentModel> {
  const response = await fetch('/v1/pipeline-models', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(model)
  });
  return readJson<VirtuaAgentModel>(response);
}

export async function deleteVirtuaAgentModel(id: string): Promise<void> {
  const response = await fetch(`/v1/pipeline-models/${id}`, { method: 'DELETE' });
  if (!response.ok) {
    throw new Error(errorMessageFrom(await response.text(), response.status));
  }
}
