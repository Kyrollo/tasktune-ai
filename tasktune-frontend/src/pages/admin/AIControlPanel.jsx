import { useEffect, useMemo, useState } from 'react';
import AppLayout from '../../layouts/AppLayout';
import { Badge, Button, Card } from '../../components/ui';
import api from '../../services/api';
import { Activity, Brain, KeyRound, RefreshCw, Settings, ToggleLeft, ToggleRight } from 'lucide-react';

const FEATURE_DEFS = [
  { key: 'taskGuidance', name: 'Task Guidance', description: 'AI recommendations, guidance, and subtasks for tasks.' },
  { key: 'autoEvaluation', name: 'Auto Evaluation', description: 'AI-assisted evaluation and feedback workflows.' },
  { key: 'riskDetection', name: 'Risk Detection', description: 'Progress analysis and delivery risk detection.' },
  { key: 'performanceInsights', name: 'Performance Insights', description: 'Insights and recommendations from tracked work data.' },
  { key: 'smartSuggestions', name: 'Smart Suggestions', description: 'Structured AI suggestions inside task and work flows.' },
  { key: 'chatAssistant', name: 'Chat Assistant', description: 'TaskTune AI Assistant in the web chat.' },
  { key: 'reportGeneration', name: 'Report Generation', description: 'AI report generation with PDF export.' },
  { key: 'emailAssistant', name: 'Email Assistant', description: 'AI-generated communication recommendations.' },
];

const DEFAULT_AGENT_ORDER = [
  { key: 'chat_assistant', name: 'TaskTune AI Assistant' },
  { key: 'task_guidance', name: 'Task Guidance' },
  { key: 'progress_tracking', name: 'Progress Tracking' },
  { key: 'report_generation', name: 'Report Generation' },
  { key: 'email_assistant', name: 'Email Assistant' },
];

const DEFAULT_CONFIG = {
  companyId: 0,
  enabled: true,
  features: FEATURE_DEFS.reduce((acc, feature) => ({ ...acc, [feature.key]: true }), {}),
  agents: DEFAULT_AGENT_ORDER.map(agent => ({
    key: agent.key,
    name: agent.name,
    provider: 'groq',
    model: 'llama-3.3-70b-versatile',
    apiKey: '',
    baseUrl: '',
    endpoint: '',
    temperature: 0.2,
    maxTokens: 1024,
    enabled: true,
  })),
};

function normalizeConfig(raw) {
  const features = FEATURE_DEFS.reduce((acc, feature) => ({
    ...acc,
    [feature.key]: raw?.features?.[feature.key] ?? DEFAULT_CONFIG.features[feature.key],
  }), {});

  const incomingAgents = new Map((raw?.agents || []).map(agent => [agent.key, agent]));
  const mergedAgents = DEFAULT_AGENT_ORDER.map(agent => {
    const incoming = incomingAgents.get(agent.key) || {};
    return {
      ...DEFAULT_CONFIG.agents.find(item => item.key === agent.key),
      ...incoming,
      key: agent.key,
      name: incoming.name || agent.name,
      apiKey: incoming.apiKey || '',
      baseUrl: incoming.baseUrl || '',
      endpoint: incoming.endpoint || '',
    };
  });

  return {
    companyId: raw?.companyId ?? 0,
    enabled: raw?.enabled ?? true,
    features,
    agents: mergedAgents,
  };
}

function buildPayload(config) {
  return {
    companyId: config.companyId || 0,
    enabled: config.enabled,
    features: config.features,
    agents: Object.fromEntries(
      config.agents.map(agent => [
        agent.key,
        {
          key: agent.key,
          name: agent.name,
          provider: agent.provider,
          model: agent.model,
          apiKey: agent.apiKey || null,
          baseUrl: agent.baseUrl,
          endpoint: agent.endpoint,
          temperature: Number(agent.temperature) || 0,
          maxTokens: Number(agent.maxTokens) || 0,
          enabled: !!agent.enabled,
        },
      ]),
    ),
  };
}

export default function AIControlPanel() {
  const [config, setConfig] = useState(DEFAULT_CONFIG);
  const [actionLogs, setActionLogs] = useState([]);
  const [usageStats, setUsageStats] = useState({ monthActions: 0, totalActions: 0 });
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [saveMessage, setSaveMessage] = useState('');

  const activeAgents = useMemo(() => config.agents.filter(agent => agent.enabled).length, [config.agents]);

  useEffect(() => {
    const fetchData = async () => {
      try {
        const [configRes, logsRes] = await Promise.all([
          api.get('/ai/config').catch(() => ({ data: null })),
          api.get('/ai/logs?page=1&pageSize=20').catch(() => ({ data: { items: [], usage: {} } })),
        ]);

        setConfig(normalizeConfig(configRes.data));

        const logs = logsRes.data?.items || [];
        setUsageStats(logsRes.data?.usage || { monthActions: logs.length, totalActions: logs.length });
        setActionLogs(logs.map(log => ({
          id: log.aiLogId ?? log.id,
          action: log.actionTypeName ?? 'AI Action',
          timestamp: log.timestamp,
          modelVersion: log.modelVersion,
          userName: log.userName,
          taskName: log.taskName,
          outputPreview: log.outputPreview,
        })));
      } catch (error) {
        console.error('Failed to load AI control panel data:', error);
      } finally {
        setLoading(false);
      }
    };

    fetchData();
  }, []);

  const toggleFeature = (featureKey) => {
    setConfig(prev => ({
      ...prev,
      features: {
        ...prev.features,
        [featureKey]: !prev.features[featureKey],
      },
    }));
  };

  const updateAgent = (agentKey, field, value) => {
    setConfig(prev => ({
      ...prev,
      agents: prev.agents.map(agent =>
        agent.key === agentKey ? { ...agent, [field]: value } : agent,
      ),
    }));
  };

  const handleSave = async () => {
    setSaving(true);
    setSaveMessage('');
    try {
      const { data } = await api.put('/ai/config', buildPayload(config));
      setConfig(normalizeConfig(data?.config || config));
      setSaveMessage('Configuration saved successfully.');
    } catch (error) {
      console.error('Failed to save AI configuration:', error);
      setSaveMessage(error.response?.data?.message || 'Failed to save configuration.');
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <AppLayout title="AI Control Panel">
        <div className="flex items-center justify-center h-64">
          <div className="text-text-secondary">Loading AI configuration...</div>
        </div>
      </AppLayout>
    );
  }

  return (
    <AppLayout title="AI Control Panel">
      <div className="grid lg:grid-cols-3 gap-6">
        <div className="lg:col-span-2 space-y-4">
          <Card>
            <div className="flex items-start justify-between gap-4 mb-5">
              <div className="flex items-center gap-2">
                <Brain size={20} className="text-orange" />
                <div>
                  <h3 className="text-lg font-semibold text-text-primary">TaskTune AI Configuration</h3>
                  <p className="text-xs text-text-muted">Each agent can use its own model, API key, endpoint, and limits.</p>
                </div>
              </div>
              <button onClick={() => setConfig(prev => ({ ...prev, enabled: !prev.enabled }))} className="focus:outline-none">
                {config.enabled ? <ToggleRight size={36} className="text-orange" /> : <ToggleLeft size={36} className="text-text-muted" />}
              </button>
            </div>

            <div className="grid md:grid-cols-2 gap-3">
              {FEATURE_DEFS.map(feature => (
                <div key={feature.key} className="flex items-center justify-between p-4 bg-bg rounded-lg border border-border/50">
                  <div className="pr-4">
                    <p className="text-sm font-medium text-text-primary">{feature.name}</p>
                    <p className="text-xs text-text-muted mt-1">{feature.description}</p>
                  </div>
                  <button onClick={() => toggleFeature(feature.key)} className="focus:outline-none">
                    {config.features[feature.key] ? <ToggleRight size={32} className="text-orange" /> : <ToggleLeft size={32} className="text-text-muted" />}
                  </button>
                </div>
              ))}
            </div>
          </Card>

          <Card>
            <div className="flex items-center gap-2 mb-4">
              <Settings size={20} className="text-orange" />
              <h3 className="text-lg font-semibold text-text-primary">Agent Connections</h3>
            </div>

            <div className="space-y-4">
              {config.agents.map(agent => (
                <div key={agent.key} className="p-4 rounded-xl bg-bg border border-border/50">
                  <div className="flex items-center justify-between gap-4 mb-4">
                    <div>
                      <p className="text-sm font-semibold text-text-primary">{agent.name}</p>
                      <p className="text-xs text-text-muted">{agent.key}</p>
                    </div>
                    <button onClick={() => updateAgent(agent.key, 'enabled', !agent.enabled)} className="focus:outline-none">
                      {agent.enabled ? <ToggleRight size={32} className="text-orange" /> : <ToggleLeft size={32} className="text-text-muted" />}
                    </button>
                  </div>

                  <div className="grid md:grid-cols-2 gap-4">
                    <div>
                      <label className="block text-sm text-text-secondary mb-1.5">Provider</label>
                      <input
                        value={agent.provider}
                        onChange={e => updateAgent(agent.key, 'provider', e.target.value)}
                        className="w-full bg-surface border border-border rounded-lg px-4 py-2.5 text-sm text-text-primary focus:outline-none focus:border-orange"
                      />
                    </div>
                    <div>
                      <label className="block text-sm text-text-secondary mb-1.5">Model</label>
                      <input
                        value={agent.model}
                        onChange={e => updateAgent(agent.key, 'model', e.target.value)}
                        className="w-full bg-surface border border-border rounded-lg px-4 py-2.5 text-sm text-text-primary focus:outline-none focus:border-orange"
                      />
                    </div>
                    <div className="md:col-span-2">
                      <label className="block text-sm text-text-secondary mb-1.5">API Key</label>
                      <input
                        type="password"
                        value={agent.apiKey}
                        onChange={e => updateAgent(agent.key, 'apiKey', e.target.value)}
                        placeholder="Enter provider API key"
                        className="w-full bg-surface border border-border rounded-lg px-4 py-2.5 text-sm text-text-primary focus:outline-none focus:border-orange"
                      />
                    </div>
                    <div>
                      <label className="block text-sm text-text-secondary mb-1.5">Base URL</label>
                      <input
                        value={agent.baseUrl}
                        onChange={e => updateAgent(agent.key, 'baseUrl', e.target.value)}
                        className="w-full bg-surface border border-border rounded-lg px-4 py-2.5 text-sm text-text-primary focus:outline-none focus:border-orange"
                      />
                    </div>
                    <div>
                      <label className="block text-sm text-text-secondary mb-1.5">Endpoint</label>
                      <input
                        value={agent.endpoint}
                        onChange={e => updateAgent(agent.key, 'endpoint', e.target.value)}
                        className="w-full bg-surface border border-border rounded-lg px-4 py-2.5 text-sm text-text-primary focus:outline-none focus:border-orange"
                      />
                    </div>
                    <div>
                      <label className="block text-sm text-text-secondary mb-1.5">Temperature</label>
                      <input
                        type="number"
                        min="0"
                        max="2"
                        step="0.1"
                        value={agent.temperature}
                        onChange={e => updateAgent(agent.key, 'temperature', e.target.value)}
                        className="w-full bg-surface border border-border rounded-lg px-4 py-2.5 text-sm text-text-primary focus:outline-none focus:border-orange"
                      />
                    </div>
                    <div>
                      <label className="block text-sm text-text-secondary mb-1.5">Max Tokens</label>
                      <input
                        type="number"
                        min="128"
                        step="128"
                        value={agent.maxTokens}
                        onChange={e => updateAgent(agent.key, 'maxTokens', e.target.value)}
                        className="w-full bg-surface border border-border rounded-lg px-4 py-2.5 text-sm text-text-primary focus:outline-none focus:border-orange"
                      />
                    </div>
                  </div>
                </div>
              ))}
            </div>

            <div className="flex items-center justify-between gap-3 mt-5">
              <div className="text-sm text-text-secondary">{saveMessage || 'Changes are stored in the company AI configuration.'}</div>
              <Button onClick={handleSave} disabled={saving}>
                <span className="flex items-center gap-2">
                  <RefreshCw size={16} className={saving ? 'animate-spin' : ''} />
                  {saving ? 'Saving...' : 'Save Configuration'}
                </span>
              </Button>
            </div>
          </Card>

          <Card>
            <div className="flex items-center gap-2 mb-4">
              <Activity size={20} className="text-orange" />
              <h3 className="text-lg font-semibold text-text-primary">AI Action Logs</h3>
            </div>

            <div className="space-y-2">
              {actionLogs.length === 0 && (
                <p className="text-sm text-text-muted text-center py-4">No AI action logs recorded yet.</p>
              )}

              {actionLogs.map(log => (
                <div key={log.id} className="p-3 bg-bg rounded-lg border border-border/50">
                  <div className="flex items-center justify-between gap-3">
                    <div className="min-w-0">
                      <p className="text-sm text-text-primary">{log.action}</p>
                      <p className="text-xs text-text-muted">{new Date(log.timestamp).toLocaleString()}</p>
                      <p className="text-xs text-text-secondary mt-1 truncate">
                        {[log.userName, log.taskName, log.modelVersion].filter(Boolean).join(' • ')}
                      </p>
                    </div>
                    <Badge variant="orange">Logged</Badge>
                  </div>
                  {log.outputPreview && <p className="text-xs text-text-muted mt-2 truncate">{log.outputPreview}</p>}
                </div>
              ))}
            </div>
          </Card>
        </div>

        <div className="space-y-4">
          <Card>
            <h3 className="text-base font-semibold text-text-primary mb-3">System Status</h3>
            <div className="space-y-3">
              <div className="flex items-center justify-between p-3 bg-bg rounded-lg">
                <span className="text-sm text-text-secondary">AI Platform</span>
                <Badge variant={config.enabled ? 'success' : 'danger'}>{config.enabled ? 'Enabled' : 'Disabled'}</Badge>
              </div>
              <div className="flex items-center justify-between p-3 bg-bg rounded-lg">
                <span className="text-sm text-text-secondary">Active Agents</span>
                <span className="text-sm text-text-primary">{activeAgents}/{config.agents.length}</span>
              </div>
              <div className="flex items-center justify-between p-3 bg-bg rounded-lg">
                <span className="text-sm text-text-secondary">Enabled Features</span>
                <span className="text-sm text-text-primary">{Object.values(config.features).filter(Boolean).length}/{FEATURE_DEFS.length}</span>
              </div>
              <div className="flex items-center justify-between p-3 bg-bg rounded-lg">
                <span className="text-sm text-text-secondary">Keys Configured</span>
                <span className="text-sm text-text-primary">{config.agents.filter(agent => agent.apiKey).length}/{config.agents.length}</span>
              </div>
            </div>
          </Card>

          <Card>
            <div className="flex items-center gap-2 mb-3">
              <KeyRound size={18} className="text-orange" />
              <h3 className="text-base font-semibold text-text-primary">Usage</h3>
            </div>
            <div className="space-y-3">
              <div className="p-3 bg-bg rounded-lg">
                <div className="flex items-center justify-between mb-1">
                  <span className="text-xs text-text-muted">Last 30 Days</span>
                  <span className="text-sm text-text-primary font-medium">{usageStats.monthActions}</span>
                </div>
                <p className="text-xs text-text-muted">AI actions recorded from backend logs.</p>
              </div>
              <div className="p-3 bg-bg rounded-lg">
                <div className="flex items-center justify-between mb-1">
                  <span className="text-xs text-text-muted">Total Logged Actions</span>
                  <span className="text-sm text-text-primary font-medium">{usageStats.totalActions}</span>
                </div>
                <p className="text-xs text-text-muted">Includes chat, guidance, reports, email, and config changes.</p>
              </div>
            </div>
          </Card>
        </div>
      </div>
    </AppLayout>
  );
}
