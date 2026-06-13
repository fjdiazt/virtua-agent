import { useEffect, useMemo, useState } from 'react';
import {
  ActionIcon,
  Alert,
  AppShell,
  Badge,
  Box,
  Button,
  Card,
  Divider,
  Group,
  Loader,
  NavLink,
  NumberInput,
  Paper,
  Select,
  Stack,
  Text,
  Textarea,
  TextInput,
  Title
} from '@mantine/core';
import { notifications } from '@mantine/notifications';
import {
  IconActivity,
  IconBook2,
  IconBrandOpenai,
  IconChevronRight,
  IconPlus,
  IconSettings,
  IconTrash
} from '@tabler/icons-react';
import { Link, Navigate, Route, Routes, useLocation } from 'react-router-dom';
import logoUrl from '../../../assets/logo.png';
import {
  deleteModelEndpoint,
  deleteVirtuaAgentModel,
  listEndpointModels,
  listModelEndpoints,
  listVirtuaAgentModels,
  listUpstreamModels,
  saveModelEndpoint,
  saveVirtuaAgentModel
} from './api';
import type { ModelEndpoint, SaveModelEndpointRequest, VirtuaAgentModel, PipelineStage } from './types';

const defaultEndpointValue = '__default__';

function endpointSelectData(endpoints: ModelEndpoint[]) {
  return [
    { value: defaultEndpointValue, label: 'Default upstream' },
    ...endpoints.map((endpoint) => ({ value: endpoint.id, label: endpoint.name }))
  ];
}

function endpointValue(endpointId?: string | null) {
  return endpointId ?? defaultEndpointValue;
}

function endpointIdFromValue(value: string | null) {
  return value === defaultEndpointValue ? null : value;
}

const emptyStage = (): PipelineStage => ({
  type: 'single_agent',
  repeat: 1,
  name: '',
  instructions: '',
  agent: { endpoint_id: null, model: null, temperature: null, max_tokens: null }
});

const emptyModel = (baseModel?: string): VirtuaAgentModel => ({
  id: 'virtua-agent/new-model',
  ownedBy: 'virtua-agent',
  pipeline: {
    default_endpoint_id: null,
    default_model: baseModel ?? null,
    default_temperature: 0.2,
    default_max_tokens: 512,
    stages: [emptyStage()]
  }
});

type ModelLoadError = {
  endpointId: string | null;
  label: string;
  message: string;
};

export function App() {
  const location = useLocation();
  const nav = [
    { label: 'Models', icon: IconBrandOpenai, to: '/models' },
    { label: 'Runs', icon: IconActivity, to: '/runs' },
    { label: 'Settings', icon: IconSettings, to: '/settings' }
  ];

  return (
    <AppShell navbar={{ width: 260, breakpoint: 'sm' }} padding="lg" className="shell">
      <AppShell.Navbar p="md">
        <Stack className="brand-block" gap={4} mb="xl">
          <img className="brand-logo" src={logoUrl} alt="Virtua Agent" />
          <Text size="xs" c="dimmed">API workbench</Text>
        </Stack>
        <Stack gap={4}>
          {nav.map((item) => (
            <NavLink
              key={item.to}
              component={Link}
              to={item.to}
              active={location.pathname === item.to}
              label={item.label}
              leftSection={<item.icon size={18} />}
              rightSection={<IconChevronRight size={14} />}
            />
          ))}
          <NavLink component="a" href="/swagger" label="Swagger" leftSection={<IconBook2 size={18} />} />
        </Stack>
      </AppShell.Navbar>
      <AppShell.Main>
        <Routes>
          <Route path="/" element={<Navigate to="/models" replace />} />
          <Route path="/models" element={<ModelsPage />} />
          <Route path="/runs" element={<RunsPage />} />
          <Route path="/settings" element={<SettingsPage />} />
        </Routes>
      </AppShell.Main>
    </AppShell>
  );
}

function ModelsPage() {
  const [endpoints, setEndpoints] = useState<ModelEndpoint[]>([]);
  const [endpointModels, setEndpointModels] = useState<Record<string, string[]>>({});
  const [upstreamModels, setUpstreamModels] = useState<string[]>([]);
  const [modelLoadErrors, setModelLoadErrors] = useState<Record<string, ModelLoadError>>({});
  const [items, setItems] = useState<VirtuaAgentModel[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [draft, setDraft] = useState<VirtuaAgentModel>(() => emptyModel());

  const selected = useMemo(
    () => items.find((item) => item.id === selectedId),
    [items, selectedId]
  );

  useEffect(() => {
    void refresh();
  }, []);

  useEffect(() => {
    if (selected) {
      setDraft(structuredClone(selected));
      void loadModelsForEndpoint(selected.pipeline.default_endpoint_id);
      selected.pipeline.stages.forEach((stage) => {
        void loadModelsForEndpoint(stage.agent?.endpoint_id);
      });
    }
  }, [selected]);

  async function refresh() {
    const savedModelsPromise = listVirtuaAgentModels();
    const savedEndpointsPromise = listModelEndpoints();

    const [VirtuaAgentModels, savedEndpoints] = await Promise.all([savedModelsPromise, savedEndpointsPromise]);
    setItems(VirtuaAgentModels);
    setEndpoints(savedEndpoints);

    if (!selectedId && VirtuaAgentModels[0]) {
      setSelectedId(VirtuaAgentModels[0].id);
    }

    const sourceModels = await loadModelsForEndpoint(null, { force: true });
    if (!selectedId && !VirtuaAgentModels[0]) setDraft(emptyModel(sourceModels[0]));
  }

  async function loadModelsForEndpoint(endpointId?: string | null, options?: { force?: boolean }) {
    const key = endpointValue(endpointId);
    if (!options?.force && endpointModels[key]) return endpointModels[key];

    try {
      const models = endpointId ? await listEndpointModels(endpointId) : await listUpstreamModels();
      setEndpointModels((current) => ({ ...current, [key]: models }));
      if (!endpointId) setUpstreamModels(models);
      setModelLoadErrors((current) => {
        const next = { ...current };
        delete next[key];
        return next;
      });
      return models;
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Failed to load models';
      setModelLoadErrors((current) => ({
        ...current,
        [key]: {
          endpointId: endpointId ?? null,
          label: endpointLabel(endpointId),
          message
        }
      }));
      return endpointModels[key] ?? [];
    }
  }

  function endpointLabel(endpointId?: string | null) {
    if (!endpointId) return 'Default upstream';
    return endpoints.find((endpoint) => endpoint.id === endpointId)?.name ?? endpointId;
  }

  function modelsForEndpoint(endpointId?: string | null, selectedModel?: string | null) {
    const models = endpointModels[endpointValue(endpointId)] ?? (endpointId ? [] : upstreamModels);
    return selectedModel && !models.includes(selectedModel) ? [selectedModel, ...models] : models;
  }

  async function retryModelDiscovery() {
    const errors = Object.values(modelLoadErrors);
    await Promise.all(errors.map((error) => loadModelsForEndpoint(error.endpointId, { force: true })));
  }

  function updateStage(index: number, stage: PipelineStage) {
    setDraft((current) => ({
      ...current,
      pipeline: {
        ...current.pipeline,
        stages: current.pipeline.stages.map((item, itemIndex) => itemIndex === index ? stage : item)
      }
    }));
  }

  async function save() {
    const saved = await saveVirtuaAgentModel(draft);
    notifications.show({ color: 'green', message: `Saved ${saved.id}` });
    await refresh();
    setSelectedId(saved.id);
  }

  async function remove() {
    if (!draft.id) return;
    await deleteVirtuaAgentModel(draft.id);
    notifications.show({ color: 'green', message: `Deleted ${draft.id}` });
    setSelectedId(null);
    setDraft(emptyModel(upstreamModels[0]));
    await refresh();
  }

  return (
    <Stack gap="lg">
      <Group justify="space-between">
        <Box>
          <Title order={2}>Virtua Agent Models</Title>
          <Text c="dimmed">Pipeline-backed models exposed through `/v1/models`.</Text>
        </Box>
        <Button leftSection={<IconPlus size={16} />} onClick={() => { setSelectedId(null); setDraft(emptyModel(upstreamModels[0])); }}>
          New model
        </Button>
      </Group>

      {Object.keys(modelLoadErrors).length > 0 && (
        <Alert color="yellow" title="Model discovery unavailable">
          <Stack gap="xs">
            <Text size="sm">Saved Virtua Agent models are still available. Live model lists could not be loaded.</Text>
            {Object.values(modelLoadErrors).map((error) => (
              <Text key={endpointValue(error.endpointId)} size="sm">
                {error.label}: {error.message}
              </Text>
            ))}
            <Group>
              <Button size="xs" variant="light" onClick={() => void retryModelDiscovery()}>
                Retry
              </Button>
            </Group>
          </Stack>
        </Alert>
      )}

      <Box className="models-grid">
        <Paper withBorder p="sm">
          <Stack gap={4}>
            {items.map((item) => (
              <NavLink
                key={item.id}
                active={item.id === selectedId}
                label={item.id}
                leftSection={<IconBrandOpenai size={18} />}
                onClick={() => setSelectedId(item.id)}
              />
            ))}
            {items.length === 0 && <Text c="dimmed" p="sm">No Virtua Agent Models saved.</Text>}
          </Stack>
        </Paper>

        <Paper withBorder p="lg">
          <Stack>
            <Group grow align="end">
              <TextInput
                label="Model id"
                value={draft.id}
                onChange={(event) => setDraft({ ...draft, id: event.currentTarget.value })}
              />
              <Select
                label="Default endpoint"
                data={endpointSelectData(endpoints)}
                value={endpointValue(draft.pipeline.default_endpoint_id)}
                searchable
                onChange={(value) => {
                  const nextEndpointId = endpointIdFromValue(value);
                  setDraft({ ...draft, pipeline: { ...draft.pipeline, default_endpoint_id: nextEndpointId, default_model: null } });
                  void loadModelsForEndpoint(nextEndpointId);
                }}
              />
              <Select
                label="Default model"
                data={modelsForEndpoint(draft.pipeline.default_endpoint_id, draft.pipeline.default_model)}
                value={draft.pipeline.default_model ?? null}
                searchable
                onChange={(value) => setDraft({ ...draft, pipeline: { ...draft.pipeline, default_model: value } })}
              />
              <NumberInput
                label="Default temperature"
                min={0}
                max={2}
                step={0.1}
                value={draft.pipeline.default_temperature ?? undefined}
                onChange={(value) => setDraft({ ...draft, pipeline: { ...draft.pipeline, default_temperature: value === '' ? null : Number(value) } })}
              />
              <NumberInput
                label="Default max tokens"
                min={1}
                value={draft.pipeline.default_max_tokens ?? undefined}
                onChange={(value) => setDraft({ ...draft, pipeline: { ...draft.pipeline, default_max_tokens: value === '' ? null : Number(value) } })}
              />
            </Group>

            <Divider label="Pipeline" />
            <Stack>
              {draft.pipeline.stages.map((stage, index) => (
                <Card withBorder key={index} radius="sm">
                  <Stack>
                    <Group justify="space-between">
                      <Text fw={600}>Stage {index + 1}</Text>
                      <ActionIcon
                        variant="subtle"
                        color="red"
                        aria-label="Remove stage"
                        onClick={() => setDraft({
                          ...draft,
                          pipeline: {
                            ...draft.pipeline,
                            stages: draft.pipeline.stages.filter((_, stageIndex) => stageIndex !== index)
                          }
                        })}
                      >
                        <IconTrash size={18} />
                      </ActionIcon>
                    </Group>
                    <Group grow align="end">
                      <TextInput
                        label="Stage name"
                        value={stage.name ?? ''}
                        onChange={(event) => updateStage(index, { ...stage, name: event.currentTarget.value })}
                      />
                      <Select
                        label="Stage endpoint"
                        data={endpointSelectData(endpoints)}
                        value={endpointValue(stage.agent?.endpoint_id)}
                        searchable
                        onChange={(value) => {
                          const nextEndpointId = endpointIdFromValue(value);
                          updateStage(index, { ...stage, agent: { ...stage.agent, endpoint_id: nextEndpointId, model: null } });
                          void loadModelsForEndpoint(nextEndpointId);
                        }}
                      />
                      <Select
                        label="Stage model"
                        placeholder="Use default model"
                        clearable
                        searchable
                        data={modelsForEndpoint(stage.agent?.endpoint_id, stage.agent?.model)}
                        value={stage.agent?.model ?? null}
                        onChange={(value) => updateStage(index, { ...stage, agent: { ...stage.agent, model: value } })}
                      />
                      <NumberInput
                        label="Repeat"
                        min={1}
                        value={stage.repeat}
                        onChange={(value) => updateStage(index, { ...stage, repeat: Number(value) || 1 })}
                      />
                    </Group>
                    <Group grow>
                      <NumberInput
                        label="Temperature"
                        min={0}
                        max={2}
                        step={0.1}
                        value={stage.agent?.temperature ?? undefined}
                        onChange={(value) => updateStage(index, { ...stage, agent: { ...stage.agent, temperature: value === '' ? null : Number(value) } })}
                      />
                      <NumberInput
                        label="Max tokens"
                        min={1}
                        value={stage.agent?.max_tokens ?? undefined}
                        onChange={(value) => updateStage(index, { ...stage, agent: { ...stage.agent, max_tokens: value === '' ? null : Number(value) } })}
                      />
                    </Group>
                    <Textarea
                      label="Instructions"
                      minRows={3}
                      value={stage.instructions ?? ''}
                      onChange={(event) => updateStage(index, { ...stage, instructions: event.currentTarget.value })}
                    />
                  </Stack>
                </Card>
              ))}
            </Stack>
            <Group justify="space-between">
              <Button variant="light" leftSection={<IconPlus size={16} />} onClick={() => setDraft({ ...draft, pipeline: { ...draft.pipeline, stages: [...draft.pipeline.stages, emptyStage()] } })}>
                Add stage
              </Button>
              <Group>
                <Button variant="subtle" color="red" leftSection={<IconTrash size={16} />} onClick={() => void remove()}>
                  Delete
                </Button>
                <Button leftSection={<IconSettings size={16} />} onClick={() => void save()}>
                  Save model
                </Button>
              </Group>
            </Group>
          </Stack>
        </Paper>
      </Box>
    </Stack>
  );
}

function SettingsPage() {
  const [endpoints, setEndpoints] = useState<ModelEndpoint[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [draft, setDraft] = useState<SaveModelEndpointRequest>({ name: '', base_url: '', api_key: '' });
  const [models, setModels] = useState<string[]>([]);
  const [loadingModels, setLoadingModels] = useState(false);

  const selected = useMemo(
    () => endpoints.find((endpoint) => endpoint.id === selectedId),
    [endpoints, selectedId]
  );

  useEffect(() => {
    void refresh();
  }, []);

  useEffect(() => {
    if (!selected) return;
    setDraft({ id: selected.id, name: selected.name, base_url: selected.base_url, api_key: '' });
    void refreshModels(selected.id);
  }, [selected]);

  async function refresh() {
    const items = await listModelEndpoints();
    setEndpoints(items);
    if (!selectedId && items[0]) setSelectedId(items[0].id);
  }

  async function refreshModels(endpointId: string) {
    setLoadingModels(true);
    try {
      setModels(await listEndpointModels(endpointId));
    } catch (error) {
      setModels([]);
      notifications.show({ color: 'red', message: error instanceof Error ? error.message : 'Failed to load models' });
    } finally {
      setLoadingModels(false);
    }
  }

  async function saveEndpoint() {
    try {
      const apiKey = draft.api_key?.trim();
      const saved = await saveModelEndpoint({ ...draft, api_key: apiKey ? apiKey : undefined });
      notifications.show({ color: 'green', message: `Saved ${saved.name}` });
      await refresh();
      setSelectedId(saved.id);
    } catch (error) {
      notifications.show({ color: 'red', message: error instanceof Error ? error.message : 'Failed to save endpoint' });
    }
  }

  async function removeEndpoint() {
    if (!selectedId) return;
    try {
      await deleteModelEndpoint(selectedId);
      notifications.show({ color: 'green', message: 'Endpoint deleted' });
      setSelectedId(null);
      setDraft({ name: '', base_url: '', api_key: '' });
      setModels([]);
      await refresh();
    } catch (error) {
      notifications.show({ color: 'red', message: error instanceof Error ? error.message : 'Failed to delete endpoint' });
    }
  }

  return (
    <Stack gap="lg">
      <Group justify="space-between">
        <Box>
          <Title order={2}>Settings</Title>
          <Text c="dimmed">OpenAI-compatible endpoints for pipeline stages.</Text>
        </Box>
        <Button leftSection={<IconPlus size={16} />} onClick={() => { setSelectedId(null); setDraft({ name: '', base_url: '', api_key: '' }); setModels([]); }}>
          New endpoint
        </Button>
      </Group>

      <Box className="models-grid">
        <Paper withBorder p="sm">
          <Stack gap={4}>
            {endpoints.map((endpoint) => (
              <NavLink
                key={endpoint.id}
                active={endpoint.id === selectedId}
                label={endpoint.name}
                description={endpoint.base_url}
                leftSection={<IconSettings size={18} />}
                onClick={() => setSelectedId(endpoint.id)}
              />
            ))}
            {endpoints.length === 0 && <Text c="dimmed" p="sm">No endpoints saved.</Text>}
          </Stack>
        </Paper>

        <Paper withBorder p="lg">
          <Stack>
            <Group grow align="end">
              <TextInput
                label="Name"
                value={draft.name}
                onChange={(event) => setDraft({ ...draft, name: event.currentTarget.value })}
              />
              <TextInput
                label="Base URL"
                placeholder="http://localhost:8080"
                value={draft.base_url}
                onChange={(event) => setDraft({ ...draft, base_url: event.currentTarget.value })}
              />
            </Group>
            <TextInput
              label="API key"
              placeholder={selected?.has_api_key ? 'Leave blank to keep saved key' : 'Optional'}
              value={draft.api_key ?? ''}
              onChange={(event) => setDraft({ ...draft, api_key: event.currentTarget.value })}
            />
            <Group justify="space-between">
              <Button
                variant="light"
                loading={loadingModels}
                disabled={!selectedId}
                onClick={() => selectedId && void refreshModels(selectedId)}
              >
                Refresh models
              </Button>
              <Group>
                <Button variant="subtle" color="red" leftSection={<IconTrash size={16} />} disabled={!selectedId} onClick={() => void removeEndpoint()}>
                  Delete
                </Button>
                <Button leftSection={<IconSettings size={16} />} onClick={() => void saveEndpoint()}>
                  Save endpoint
                </Button>
              </Group>
            </Group>
            <Divider label="Models" />
            <Stack gap={6}>
              {models.map((model) => <Badge key={model} variant="light">{model}</Badge>)}
              {models.length === 0 && <Text c="dimmed">No models loaded.</Text>}
            </Stack>
          </Stack>
        </Paper>
      </Box>
    </Stack>
  );
}

function RunsPage() {
  const [runs, setRuns] = useState<Array<{ runId: string; status: string; preview?: string }>>([]);
  const [loading, setLoading] = useState(true);

  async function load() {
    setLoading(true);
    try {
      const response = await fetch('/v1/orchestrations');
      const body = await response.json();
      setRuns(body.runs ?? body ?? []);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void load();
  }, []);

  async function clear() {
    await fetch('/v1/orchestrations', { method: 'DELETE' });
    await load();
  }

  return (
    <Stack>
      <Group justify="space-between">
        <Box>
          <Title order={2}>Runs</Title>
          <Text c="dimmed">Stored orchestration traces.</Text>
        </Box>
        <Button variant="light" color="red" leftSection={<IconTrash size={16} />} onClick={() => void clear()}>
          Clear runs
        </Button>
      </Group>
      <Paper withBorder>
        {loading ? (
          <Box p="lg"><Loader size="sm" /></Box>
        ) : (
          <Stack gap={0}>
            {runs.map((run) => (
              <Box key={run.runId} className="run-row">
                <Text fw={600}>{run.runId}</Text>
                <Badge variant="light">{run.status}</Badge>
                <Text c="dimmed">{run.preview}</Text>
              </Box>
            ))}
            {runs.length === 0 && <Text c="dimmed" p="lg">No runs stored.</Text>}
          </Stack>
        )}
      </Paper>
    </Stack>
  );
}
