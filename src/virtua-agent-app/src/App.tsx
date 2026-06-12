import { useEffect, useMemo, useRef, useState } from 'react';
import {
  ActionIcon,
  Accordion,
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
  ScrollArea,
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
  IconChevronDown,
  IconChevronUp,
  IconMessageCircle,
  IconPlus,
  IconSend,
  IconSettings,
  IconTrash
} from '@tabler/icons-react';
import { Link, Navigate, Route, Routes, useLocation } from 'react-router-dom';
import logoUrl from '../../../assets/logo.png';
import {
  deleteVirtuaAgentModel,
  listVirtuaAgentModels,
  listModels,
  listUpstreamModels,
  saveVirtuaAgentModel
} from './api';
import type { ChatMessage, VirtuaAgentModel, PipelineStage } from './types';

function splitThinkBlocks(raw: string) {
  let answer = '';
  let reasoning = '';
  let remaining = raw;

  while (remaining.length > 0) {
    const openMatch = remaining.match(/<think>/i);
    if (!openMatch || openMatch.index === undefined) {
      answer += remaining;
      break;
    }

    answer += remaining.slice(0, openMatch.index);
    const afterOpenIndex = openMatch.index + openMatch[0].length;
    const afterOpen = remaining.slice(afterOpenIndex);
    const closeMatch = afterOpen.match(/<\/think>/i);
    if (!closeMatch || closeMatch.index === undefined) {
      reasoning += afterOpen;
      break;
    }

    reasoning += afterOpen.slice(0, closeMatch.index);
    remaining = afterOpen.slice(closeMatch.index + closeMatch[0].length);
  }

  return { answer: answer.trimStart(), reasoning };
}

type ReasoningBuckets = Record<string, string>;

const reasoningOpenKey = 'virtua-agent.chat.reasoning.open';
const reasoningPanelsKey = 'virtua-agent.chat.reasoning.panels';
const chatModelSessionKey = 'virtua-agent.chat.model';

function readSessionBool(key: string, fallback: boolean) {
  const value = sessionStorage.getItem(key);
  return value === null ? fallback : value === 'true';
}

function readSessionStringArray(key: string) {
  const value = sessionStorage.getItem(key);
  if (!value) return [];

  try {
    const parsed = JSON.parse(value);
    return Array.isArray(parsed) ? parsed.filter((item): item is string => typeof item === 'string') : [];
  } catch {
    return [];
  }
}

const emptyStage = (): PipelineStage => ({
  type: 'single_agent',
  repeat: 1,
  name: '',
  instructions: '',
  agent: { model: null, temperature: null, max_tokens: null }
});

const emptyModel = (baseModel?: string): VirtuaAgentModel => ({
  id: 'virtua-agent/new-model',
  ownedBy: 'virtua-agent',
  pipeline: {
    default_model: baseModel ?? null,
    default_temperature: 0.2,
    default_max_tokens: 512,
    stages: [emptyStage()]
  }
});

export function App() {
  const location = useLocation();
  const nav = [
    { label: 'Chat', icon: IconMessageCircle, to: '/chat' },
    { label: 'Models', icon: IconBrandOpenai, to: '/models' },
    { label: 'Runs', icon: IconActivity, to: '/runs' }
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
          <Route path="/" element={<Navigate to="/chat" replace />} />
          <Route path="/chat" element={<ChatPage />} />
          <Route path="/models" element={<ModelsPage />} />
          <Route path="/runs" element={<RunsPage />} />
        </Routes>
      </AppShell.Main>
    </AppShell>
  );
}

function ChatPage() {
  const [models, setModels] = useState<string[]>([]);
  const [model, setModel] = useState<string | null>(null);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [input, setInput] = useState('');
  const [reasoningBuckets, setReasoningBuckets] = useState<ReasoningBuckets>({});
  const [reasoningOpen, setReasoningOpen] = useState(() => readSessionBool(reasoningOpenKey, true));
  const [openReasoningPanels, setOpenReasoningPanels] = useState<string[]>(() => readSessionStringArray(reasoningPanelsKey));
  const [runId, setRunId] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const viewportRef = useRef<HTMLDivElement>(null);
  const previousReasoningLabelsRef = useRef<string[]>([]);
  const reasoningEntries = Object.entries(reasoningBuckets).filter(([, value]) => value.trim().length > 0);

  useEffect(() => {
    listModels()
      .then((items) => {
        setModels(items);
        const savedModel = sessionStorage.getItem(chatModelSessionKey);
        setModel((current) => current ?? (savedModel && items.includes(savedModel) ? savedModel : items[0] ?? null));
      })
      .catch((error) => notifications.show({ color: 'red', message: error.message }));
  }, []);

  useEffect(() => {
    if (model) {
      sessionStorage.setItem(chatModelSessionKey, model);
    }
  }, [model]);

  useEffect(() => {
    viewportRef.current?.scrollTo({ top: viewportRef.current.scrollHeight });
  }, [messages, reasoningBuckets]);

  useEffect(() => {
    const previousLabels = previousReasoningLabelsRef.current;
    setOpenReasoningPanels((current) => {
      const labels = reasoningEntries.map(([label]) => label);
      const newLabels = labels.filter((label) => !previousLabels.includes(label));
      return Array.from(new Set([...current.filter((label) => labels.includes(label)), ...newLabels]));
    });
    previousReasoningLabelsRef.current = reasoningEntries.map(([label]) => label);
  }, [reasoningEntries.map(([label]) => label).join('|')]);

  useEffect(() => {
    sessionStorage.setItem(reasoningOpenKey, String(reasoningOpen));
  }, [reasoningOpen]);

  useEffect(() => {
    sessionStorage.setItem(reasoningPanelsKey, JSON.stringify(openReasoningPanels));
  }, [openReasoningPanels]);

  async function send() {
    const content = input.trim();
    if (!content || !model || busy) return;

    const nextMessages: ChatMessage[] = [...messages, { role: 'user', content }];
    setMessages([...nextMessages, { role: 'assistant', content: '' }]);
    setInput('');
    setReasoningBuckets({});
    previousReasoningLabelsRef.current = [];
    setOpenReasoningPanels([]);
    setRunId(null);
    setBusy(true);

    try {
      const response = await fetch('/v1/chat/completions', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ model, stream: true, messages: nextMessages })
      });

      if (!response.ok || !response.body) {
        throw new Error(await response.text());
      }

      setRunId(response.headers.get('Virtua-Agent-Run-Id'));
      const reader = response.body.getReader();
      const decoder = new TextDecoder();
      let buffer = '';
      let rawAssistant = '';
      const streamReasoningBuckets: ReasoningBuckets = {};

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });
        const parts = buffer.split('\n\n');
        buffer = parts.pop() ?? '';

        for (const part of parts) {
          const line = part.split('\n').find((item) => item.startsWith('data: '));
          if (!line) continue;
          const data = line.slice(6).trim();
          if (data === '[DONE]') continue;

          const chunk = JSON.parse(data);
          const delta = chunk.choices?.[0]?.delta ?? {};
          const contentDelta = delta.content ?? '';
          const reasoningDelta =
            delta.reasoning ??
            delta.reasoning_content ??
            delta.reasoning_text ??
            delta.thinking ??
            '';
          const virtuaAgentLabel = delta.virtua_agent?.label;
          const reasoningLabel = typeof virtuaAgentLabel === 'string' && virtuaAgentLabel.trim().length > 0
            ? virtuaAgentLabel
            : 'Model reasoning';

          if (reasoningDelta) {
            streamReasoningBuckets[reasoningLabel] = (streamReasoningBuckets[reasoningLabel] ?? '') + reasoningDelta;
          }

          if (contentDelta) {
            rawAssistant += contentDelta;
          }

          if (reasoningDelta || contentDelta) {
            const parsed = splitThinkBlocks(rawAssistant);
            const nextReasoningBuckets = { ...streamReasoningBuckets };
            if (parsed.reasoning.trim().length > 0) {
              nextReasoningBuckets['Model reasoning'] = parsed.reasoning;
            }

            setReasoningBuckets(nextReasoningBuckets);
            setMessages([...nextMessages, { role: 'assistant', content: parsed.answer }]);
          }
        }
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Request failed';
      notifications.show({ color: 'red', message });
      setMessages(nextMessages);
    } finally {
      setBusy(false);
    }
  }

  return (
    <Stack className="chat-page" gap="md">
      <Group justify="space-between" align="center">
        <Box>
          <Title order={2}>Chat</Title>
          <Text c="dimmed">Transient API test chat. Session memory only.</Text>
        </Box>
        <Select
          w={360}
          searchable
          label="Model"
          data={models}
          value={model}
          onChange={setModel}
          placeholder="Select model"
        />
      </Group>

      <Paper className={`chat-surface ${reasoningEntries.length > 0 && reasoningOpen ? 'has-reasoning' : ''}`} withBorder>
        <ScrollArea viewportRef={viewportRef} className="chat-scroll">
          <Stack gap="md" p="md">
            {messages.length === 0 && (
              <Box className="empty-state">
                <Title order={3}>No messages yet.</Title>
                <Text c="dimmed">Pick upstream or Virtua Agent model, send prompt, inspect response.</Text>
              </Box>
            )}
            {messages.map((message, index) => (
              <Box key={`${message.role}-${index}`} className={`message ${message.role}`}>
                <Text size="xs" c="dimmed" tt="uppercase">{message.role}</Text>
                <Text className="message-text">{message.content}</Text>
              </Box>
            ))}
            {busy && <Loader size="sm" />}
          </Stack>
        </ScrollArea>

        {reasoningEntries.length > 0 && (
          <Box className="reasoning">
            <Group justify="space-between">
              <Text fw={600}>Reasoning streams</Text>
              <Group gap="xs">
                {runId && <Badge variant="light">{runId}</Badge>}
                <ActionIcon
                  variant="subtle"
                  aria-label={reasoningOpen ? 'Collapse reasoning streams' : 'Expand reasoning streams'}
                  onClick={() => setReasoningOpen((value) => !value)}
                >
                  {reasoningOpen ? <IconChevronUp size={18} /> : <IconChevronDown size={18} />}
                </ActionIcon>
              </Group>
            </Group>
            {reasoningOpen && (
              <Accordion
                className="reasoning-accordion"
                classNames={{ content: 'reasoning-content' }}
                variant="contained"
                multiple
                value={openReasoningPanels}
                onChange={setOpenReasoningPanels}
                mt="sm"
              >
                {reasoningEntries.map(([label, value]) => (
                  <Accordion.Item
                    className={`reasoning-item ${openReasoningPanels.includes(label) ? 'open' : ''}`}
                    key={label}
                    value={label}
                  >
                    <Accordion.Control>{label}</Accordion.Control>
                    <Accordion.Panel className="reasoning-panel">
                      <Text size="sm" className="reasoning-text">{value}</Text>
                    </Accordion.Panel>
                  </Accordion.Item>
                ))}
              </Accordion>
            )}
          </Box>
        )}

        <Group p="md" align="end">
          <Textarea
            className="composer"
            autosize
            minRows={2}
            maxRows={8}
            placeholder="Message Virtua Agent API"
            value={input}
            onChange={(event) => setInput(event.currentTarget.value)}
            onKeyDown={(event) => {
              if (event.key === 'Enter' && !event.shiftKey) {
                event.preventDefault();
                void send();
              }
            }}
          />
          <ActionIcon size={44} radius="md" variant="filled" onClick={() => void send()} loading={busy} aria-label="Send">
            <IconSend size={20} />
          </ActionIcon>
        </Group>
      </Paper>
    </Stack>
  );
}

function ModelsPage() {
  const [upstreamModels, setUpstreamModels] = useState<string[]>([]);
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
    if (selected) setDraft(structuredClone(selected));
  }, [selected]);

  async function refresh() {
    const [sourceModels, VirtuaAgentModels] = await Promise.all([listUpstreamModels(), listVirtuaAgentModels()]);
    setUpstreamModels(sourceModels);
    setItems(VirtuaAgentModels);
    if (!selectedId && VirtuaAgentModels[0]) setSelectedId(VirtuaAgentModels[0].id);
    if (!selectedId && !VirtuaAgentModels[0]) setDraft(emptyModel(sourceModels[0]));
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
                label="Default model"
                data={upstreamModels}
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
                        label="Stage model"
                        placeholder="Use default model"
                        clearable
                        searchable
                        data={upstreamModels}
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
