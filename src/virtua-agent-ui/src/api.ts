import type { VirtuaAgentModel, ModelDto, ModelListResponse } from './types';

async function readJson<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const text = await response.text();
    throw new Error(text || `HTTP ${response.status}`);
  }

  return response.json() as Promise<T>;
}

export async function listModels(): Promise<string[]> {
  const models = await listModelDtos();
  return models.map((model) => model.id).filter(Boolean);
}

export async function listUpstreamModels(): Promise<string[]> {
  const models = await listModelDtos();
  return models
    .filter((model) => model.owned_by !== 'virtua-agent')
    .map((model) => model.id)
    .filter(Boolean);
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
    throw new Error(`HTTP ${response.status}`);
  }
}
