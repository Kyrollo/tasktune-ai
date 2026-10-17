import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import AppLayout from '../../layouts/AppLayout';
import { Card, Badge, Button, ProgressBar, Modal } from '../../components/ui';
import { taskService } from '../../services/taskService';
import { projectService } from '../../services/projectService';
import { userService } from '../../services/userService';
import {
  Plus, Search, ListTodo, LayoutGrid, Sparkles, Clock,
  AlertTriangle, CheckCircle, Lightbulb, X, ChevronDown,
  ChevronUp, BookOpen, Target, Shield, ArrowRight, Save,
  Check, Eye, EyeOff
} from 'lucide-react';

const TASK_STATUS_MAP = { 0: 'Not Started', 1: 'In Progress', 2: 'In Review', 3: 'Completed' };
const TASK_PRIORITY_MAP = { 0: 'Low', 1: 'Medium', 2: 'High', 3: 'Critical' };
const PRIORITY_TO_BYTE = { Low: 0, Medium: 1, High: 2, Critical: 3 };

export default function TasksManagement() {
  const navigate = useNavigate();
  const [tasks, setTasks] = useState([]);
  const [projects, setProjects] = useState([]);
  const [users, setUsers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [filter, setFilter] = useState('All');
  const [search, setSearch] = useState('');
  const [view, setView] = useState('list');
  const [showCreate, setShowCreate] = useState(false);
  const [creating, setCreating] = useState(false);
  const [error, setError] = useState('');
  const [newTask, setNewTask] = useState({
    taskName: '', description: '', projectId: '', priority: 1,
    assignedTo: '', startDate: '', dueDate: ''
  });

  // ── AI Recommendation state ──
  const [aiLoading, setAiLoading] = useState(false);
  const [aiRecommendation, setAiRecommendation] = useState(null);
  const [aiError, setAiError] = useState('');
  const [showAiPanel, setShowAiPanel] = useState(false);
  const [selectedSubtasks, setSelectedSubtasks] = useState([]);
  const [expandedSections, setExpandedSections] = useState({
    guidance: true, breakdown: true, subtasks: true, risks: true, resources: true, criteria: true
  });

  // ── Expanded AI detail view for saved tasks ──
  const [expandedAiTaskId, setExpandedAiTaskId] = useState(null);
  const [savingAiForTask, setSavingAiForTask] = useState(null);

  const statuses = ['All', 'Not Started', 'In Progress', 'In Review', 'Completed'];

  useEffect(() => {
    const fetchData = async () => {
      try {
        const [{ data: taskRes }, { data: projRes }] = await Promise.all([
          taskService.getTasks(1, 100),
          projectService.getProjects(1, 100),
        ]);
        const taskItems = (taskRes?.items || taskRes?.Items || [])?.map(t => ({
          id: t.taskId ?? t.id,
          name: t.taskName ?? t.name,
          description: t.description ?? '',
          projectId: t.projectId,
          assigneeIds: t.assignedTo ? [t.assignedTo] : [],
          supervisorId: t.createdBy,
          priority: TASK_PRIORITY_MAP[t.priority] ?? 'Medium',
          status: TASK_STATUS_MAP[t.status] ?? 'Not Started',
          progress: t.completionPercentage ?? t.progress ?? 0,
          startDate: t.startDate ? (typeof t.startDate === 'string' ? t.startDate.slice(0, 10) : t.startDate) : '',
          dueDate: t.dueDate ? (typeof t.dueDate === 'string' ? t.dueDate.slice(0, 10) : t.dueDate) : '',
          submitMethod: t.submitMethod ?? 'File Upload',
          definitionOfDone: t.definitionOfDone ?? '',
          aiRecommendation: t.aiRecommendation ?? null,
        }));
        setTasks(taskItems || []);
        const projItems = (projRes?.items || projRes?.Items || [])?.map(p => ({
          id: p.projectId ?? p.id,
          name: p.projectName ?? p.name
        }));
        setProjects(projItems || []);
        try {
          const { data: userRes } = await userService.getUsers(1, 100);
          const userItems = (userRes?.items || userRes?.Items || [])?.map(u => ({
            id: u.userId ?? u.id,
            name: u.fullName ?? (`${u.firstName || ''} ${u.lastName || ''}`.trim() || u.name)
          }));
          setUsers(userItems || []);
        } catch {
          console.warn('Could not load users');
        }
      } catch (err) {
        console.error('Failed to load tasks:', err);
      } finally {
        setLoading(false);
      }
    };
    fetchData();
  }, []);

  // ── AI Recommend handler ──
  const handleGetRecommendation = async () => {
    if (!newTask.taskName) return;
    setAiLoading(true);
    setAiError('');
    setAiRecommendation(null);
    setShowAiPanel(true);
    try {
      const res = await taskService.getTaskRecommendation({
        taskName: newTask.taskName,
        description: newTask.description || '',
        definitionOfDone: newTask.description || '',
        projectName: projects.find((project) => project.id === Number(newTask.projectId))?.name || '',
        projectId: newTask.projectId ? Number(newTask.projectId) : 0,
        priority: Number(newTask.priority),
        dueDate: newTask.dueDate || new Date().toISOString().slice(0, 10),
      });
      setAiRecommendation(res?.data ?? null);
      if (res?.data?.suggested_subtasks) {
        setSelectedSubtasks(res.data.suggested_subtasks.map((_, i) => i));
      }
    } catch (err) {
      console.error('AI recommendation failed:', err);
      setAiError(
        err.response?.data?.exceptionMessage ||
        err.response?.data?.message ||
        'Failed to get AI recommendation. Make sure the agent is running.'
      );
    } finally {
      setAiLoading(false);
    }
  };

  const toggleSubtaskSelection = (idx) => {
    setSelectedSubtasks(prev =>
      prev.includes(idx) ? prev.filter(i => i !== idx) : [...prev, idx]
    );
  };

  const toggleSection = (key) => {
    setExpandedSections(prev => ({ ...prev, [key]: !prev[key] }));
  };

  const applyAiDescription = () => {
    if (aiRecommendation?.guidance_text) {
      setNewTask(prev => ({ ...prev, description: aiRecommendation.guidance_text }));
    }
  };

  // ── Apply all AI recommendation fields to the task form ──
  const handleApplyAiRecommendation = () => {
    if (!aiRecommendation) return;
    const updates = {};
    if (aiRecommendation.guidance_text && !newTask.description) {
      updates.description = aiRecommendation.guidance_text;
    }
    if (aiRecommendation.estimated_time) {
      const desc = newTask.description || aiRecommendation.guidance_text || '';
      updates.description = desc + '\n\nEstimated Time: ' + aiRecommendation.estimated_time;
    }
    if (Object.keys(updates).length > 0) {
      setNewTask(prev => ({ ...prev, ...updates }));
    }
    // Select all suggested subtasks
    if (aiRecommendation.suggested_subtasks?.length) {
      setSelectedSubtasks(aiRecommendation.suggested_subtasks.map((_, i) => i));
    }
  };

  // ── Toggle expanded AI card for a task ──
  const toggleExpandedAi = (taskId) => {
    setExpandedAiTaskId(prev => prev === taskId ? null : taskId);
  };

  // ── Accept & Save AI recommendation to a task ──
  const handleAcceptAiRecommendation = async (task) => {
    if (!task.aiRecommendation) return;
    setSavingAiForTask(task.id);
    try {
      const ai = task.aiRecommendation;
      // Build description from guidance text
      const descParts = [];
      if (ai.guidance_text) descParts.push(ai.guidance_text);
      if (ai.estimated_time) descParts.push(`\nEstimated Time: ${ai.estimated_time}`);
      if (ai.risk_factors?.length) descParts.push(`\nRisks: ${ai.risk_factors.join('; ')}`);
      if (ai.success_criteria?.completion_checklist?.length) {
        descParts.push(`\nChecklist: ${ai.success_criteria.completion_checklist.join('; ')}`);
      }
      const descStr = descParts.join('\n');

      // Update task with AI data
      await taskService.updateTask(task.id, {
        projectId: task.projectId,
        taskName: task.name,
        description: descStr,
        priority: PRIORITY_TO_BYTE[task.priority] ?? 1,
        assignedTo: task.assigneeIds?.[0] || 0,
        startDate: task.startDate || new Date().toISOString().slice(0, 10),
        dueDate: task.dueDate || new Date().toISOString().slice(0, 10),
        submitMethod: task.submitMethod || 'File Upload',
        definitionOfDone: ai.success_criteria?.completion_checklist?.join('; ') || null,
        aiRecommendation: ai,
      });

      // Create subtasks from AI suggestions
      if (ai.suggested_subtasks?.length) {
        for (let i = 0; i < ai.suggested_subtasks.length; i++) {
          try {
            await taskService.createSubtask(task.id, {
              subtaskName: ai.suggested_subtasks[i],
              description: null,
              orderIndex: i,
            });
          } catch (e) {
            console.warn('Failed to create subtask:', e);
          }
        }
      }

      // Mark the task as having saved AI recommendation
      setTasks(prev => prev.map(t =>
        t.id === task.id
          ? { ...t, aiRecommendation: { ...ai, _saved: true }, description: descStr }
          : t
      ));
    } catch (err) {
      console.error('Failed to save AI recommendation:', err);
    } finally {
      setSavingAiForTask(null);
    }
  };

  const handleCreateTask = async () => {
    setError('');
    // Validate required fields and show error instead of silently returning
    const missing = [];
    if (!newTask.taskName) missing.push('Task Name');
    if (!newTask.projectId) missing.push('Project');
    if (!newTask.assignedTo) missing.push('Assignee');
    if (!newTask.dueDate) missing.push('Due Date');
    if (missing.length > 0) {
      setError(`Please fill in: ${missing.join(', ')}`);
      return;
    }
    setCreating(true);

    // Capture AI recommendation in a local variable before any state resets
    const capturedAiRec = aiRecommendation ? { ...aiRecommendation } : null;
    const capturedTaskName = newTask.taskName;

    try {
      const subtasksPayload = selectedSubtasks.length > 0 && capturedAiRec?.suggested_subtasks
        ? selectedSubtasks.map((idx, orderIndex) => ({
          subtaskName: capturedAiRec.suggested_subtasks[idx],
          description: null,
          orderIndex
        }))
        : undefined;

      // If AI recommendation was accepted, include guidance as description
      const finalDescription = newTask.description || (capturedAiRec?.guidance_text ?? null);

      const createRes = await taskService.createTask({
        projectId: Number(newTask.projectId),
        taskName: newTask.taskName,
        description: finalDescription,
        priority: Number(newTask.priority),
        assignedTo: Number(newTask.assignedTo),
        startDate: newTask.startDate || new Date().toISOString().slice(0, 10),
        dueDate: newTask.dueDate,
        subtasks: subtasksPayload,
        definitionOfDone: capturedAiRec?.success_criteria?.completion_checklist?.join('; ') || null,
        aiRecommendation: capturedAiRec,
      });

      // Save AI recommendation locally with the new task
      const newTaskId = createRes?.data?.taskId ?? createRes?.data?.id;

      // Close modal and reset form first
      setShowCreate(false);
      resetCreateForm();

      const { data: taskRes } = await taskService.getTasks(1, 100);
      let aiAttached = false;
      const taskItems = (taskRes?.items || taskRes?.Items || [])?.map(t => {
        const mapped = {
          id: t.taskId ?? t.id,
          name: t.taskName ?? t.name,
          description: t.description ?? '',
          projectId: t.projectId,
          assigneeIds: t.assignedTo ? [t.assignedTo] : [],
          supervisorId: t.createdBy,
          priority: TASK_PRIORITY_MAP[t.priority] ?? 'Medium',
          status: TASK_STATUS_MAP[t.status] ?? 'Not Started',
          progress: t.completionPercentage ?? t.progress ?? 0,
          startDate: t.startDate ? (typeof t.startDate === 'string' ? t.startDate.slice(0, 10) : t.startDate) : '',
          dueDate: t.dueDate ? (typeof t.dueDate === 'string' ? t.dueDate.slice(0, 10) : t.dueDate) : '',
          submitMethod: t.submitMethod ?? 'File Upload',
          definitionOfDone: t.definitionOfDone ?? '',
          aiRecommendation: t.aiRecommendation ?? null,
        };
        // Attach AI recommendation to the just-created task (use == for type-safe comparison)
        if (capturedAiRec && !aiAttached) {
          // eslint-disable-next-line eqeqeq
          if (newTaskId && mapped.id == newTaskId) {
            mapped.aiRecommendation = capturedAiRec;
            aiAttached = true;
          } else if (!newTaskId && mapped.name === capturedTaskName) {
            // Fallback: match by name if ID wasn't returned
            mapped.aiRecommendation = capturedAiRec;
            aiAttached = true;
          }
        }
        return mapped;
      });

      // Last-resort fallback: attach to the last task if nothing matched
      if (capturedAiRec && !aiAttached && taskItems.length > 0) {
        taskItems[taskItems.length - 1].aiRecommendation = capturedAiRec;
      }

      setTasks(taskItems || []);
    } catch (err) {
      setError(err.response?.data?.message || 'Failed to create task');
    } finally {
      setCreating(false);
    }
  };

  const resetCreateForm = () => {
    setNewTask({ taskName: '', description: '', projectId: '', priority: 1, assignedTo: '', startDate: '', dueDate: '' });
    setAiRecommendation(null);
    setAiError('');
    setShowAiPanel(false);
    setSelectedSubtasks([]);
  };

  const filtered = tasks.filter(t =>
    (filter === 'All' || t.status === filter) &&
    t.name.toLowerCase().includes(search.toLowerCase())
  );

  if (loading) return (
    <AppLayout title="Tasks Management">
      <div className="flex items-center justify-center h-64"><div className="text-text-secondary">Loading...</div></div>
    </AppLayout>
  );

  const statusVariant = { 'Not Started': 'default', 'In Progress': 'warning', 'In Review': 'info', 'Completed': 'success' };
  const priorityVariant = { Low: 'default', Medium: 'warning', High: 'danger', Critical: 'danger' };

  // ── Collapsible Section Component ──
  const CollapsibleSection = ({ id, icon: Icon, title, badge, children }) => (
    <div className="rounded-lg bg-surface border border-border overflow-hidden">
      <button
        onClick={() => toggleSection(id)}
        className="w-full flex items-center justify-between p-3 hover:bg-surface-hover transition-colors"
      >
        <span className="text-xs text-text-muted font-medium flex items-center gap-2">
          {Icon && <Icon size={13} />} {title}
          {badge && <span className="text-[10px] bg-purple-500/20 text-purple-400 px-1.5 py-0.5 rounded-full">{badge}</span>}
        </span>
        {expandedSections[id] ? <ChevronUp size={14} className="text-text-muted" /> : <ChevronDown size={14} className="text-text-muted" />}
      </button>
      {expandedSections[id] && <div className="px-3 pb-3 border-t border-border/50">{children}</div>}
    </div>
  );

  // ── AI Recommendation Panel (reused in modal & view) ──
  const AiRecommendationContent = ({ data, selectable = false }) => (
    <div className="space-y-3">
      {/* Guidance */}
      {data.guidance_text && (
        <CollapsibleSection id="guidance" icon={Lightbulb} title="AI Guidance">
          <p className="text-xs text-text-secondary leading-relaxed mt-2 whitespace-pre-wrap">{data.guidance_text}</p>
          {selectable && (
            <button onClick={applyAiDescription} className="mt-2 text-xs text-purple-400 hover:text-purple-300 transition-colors flex items-center gap-1">
              <ArrowRight size={12} /> Apply as task description
            </button>
          )}
        </CollapsibleSection>
      )}

      {/* Estimated Time */}
      {data.estimated_time && (
        <div className="flex items-center gap-2 px-3 py-2.5 rounded-lg bg-blue-500/10 border border-blue-500/20">
          <Clock size={14} className="text-blue-400 shrink-0" />
          <span className="text-xs text-blue-300 font-medium">Estimated: {data.estimated_time}</span>
        </div>
      )}

      {/* Task Breakdown */}
      {data.task_breakdown?.length > 0 && (
        <CollapsibleSection id="breakdown" icon={Target} title="Task Breakdown" badge={`${data.task_breakdown.length} phases`}>
          <div className="space-y-2 mt-2">
            {data.task_breakdown.map((item, i) => (
              <div key={i} className="p-2.5 rounded-lg bg-bg border border-border/50">
                <div className="flex items-center justify-between mb-1.5">
                  <span className="text-xs text-white font-medium">{item.task}</span>
                  <Badge variant={item.priority === 'high' ? 'danger' : item.priority === 'medium' ? 'warning' : 'default'}>
                    {item.priority}
                  </Badge>
                </div>
                {item.subtasks?.length > 0 && (
                  <ul className="ml-3 space-y-0.5">
                    {item.subtasks.map((st, j) => (
                      <li key={j} className="text-[11px] text-text-muted flex items-start gap-1.5">
                        <span className="text-text-muted/50 mt-0.5 shrink-0">•</span> {st}
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            ))}
          </div>
        </CollapsibleSection>
      )}

      {/* Suggested Subtasks */}
      {data.suggested_subtasks?.length > 0 && (
        <CollapsibleSection id="subtasks" icon={CheckCircle} title="Suggested Subtasks" badge={`${data.suggested_subtasks.length}`}>
          <div className="mt-2">
            {selectable && (
              <div className="flex gap-2 mb-2">
                <button onClick={() => setSelectedSubtasks(data.suggested_subtasks.map((_, i) => i))} className="text-[11px] text-purple-400 hover:text-purple-300">Select all</button>
                <span className="text-text-muted/30">|</span>
                <button onClick={() => setSelectedSubtasks([])} className="text-[11px] text-text-muted hover:text-white">Clear</button>
              </div>
            )}
            <div className="space-y-1">
              {data.suggested_subtasks.map((step, i) => (
                selectable ? (
                  <label key={i} className="flex items-start gap-2 p-1.5 rounded hover:bg-bg cursor-pointer transition-colors">
                    <input
                      type="checkbox"
                      checked={selectedSubtasks.includes(i)}
                      onChange={() => toggleSubtaskSelection(i)}
                      className="mt-0.5 accent-purple-500"
                    />
                    <span className="text-xs text-text-secondary">{step}</span>
                  </label>
                ) : (
                  <div key={i} className="flex items-start gap-2 p-1.5 rounded bg-bg/50">
                    <CheckCircle size={12} className="text-success mt-0.5 shrink-0" />
                    <span className="text-xs text-text-secondary">{step}</span>
                  </div>
                )
              ))}
            </div>
          </div>
        </CollapsibleSection>
      )}

      {/* Risk Factors */}
      {data.risk_factors?.length > 0 && (
        <CollapsibleSection id="risks" icon={AlertTriangle} title="Risk Assessment" badge={`${data.risk_factors.length} risks`}>
          <ul className="space-y-1.5 mt-2">
            {data.risk_factors.map((risk, i) => (
              <li key={i} className="text-xs text-yellow-400/80 flex items-start gap-2 p-1.5 rounded bg-yellow-500/5">
                <AlertTriangle size={12} className="text-yellow-500/50 mt-0.5 shrink-0" />
                <span>{risk}</span>
              </li>
            ))}
          </ul>
        </CollapsibleSection>
      )}

      {/* Resources */}
      {data.resources?.length > 0 && (
        <CollapsibleSection id="resources" icon={BookOpen} title="Recommended Resources" badge={`${data.resources.length}`}>
          <ul className="space-y-1 mt-2">
            {data.resources.map((res, i) => (
              <li key={i} className="text-xs text-text-secondary flex items-start gap-2 p-1">
                <span className="text-purple-400/60 mt-0.5 shrink-0">→</span> {res}
              </li>
            ))}
          </ul>
        </CollapsibleSection>
      )}

      {/* Success Criteria */}
      {data.success_criteria && (Object.values(data.success_criteria).some(v => Array.isArray(v) && v.length > 0)) && (
        <CollapsibleSection id="criteria" icon={Shield} title="Success Criteria">
          <div className="space-y-2 mt-2">
            {data.success_criteria.deliverables?.length > 0 && (
              <div>
                <span className="text-[11px] text-text-muted font-medium uppercase tracking-wider">Deliverables</span>
                <ul className="mt-1 space-y-0.5">
                  {data.success_criteria.deliverables.map((d, i) => (
                    <li key={i} className="text-xs text-text-secondary flex items-start gap-1.5">
                      <span className="text-green-400/60 mt-0.5 shrink-0">✓</span> {d}
                    </li>
                  ))}
                </ul>
              </div>
            )}
            {data.success_criteria.quality_indicators?.length > 0 && (
              <div>
                <span className="text-[11px] text-text-muted font-medium uppercase tracking-wider">Quality Indicators</span>
                <ul className="mt-1 space-y-0.5">
                  {data.success_criteria.quality_indicators.map((q, i) => (
                    <li key={i} className="text-xs text-text-secondary flex items-start gap-1.5">
                      <span className="text-blue-400/60 mt-0.5 shrink-0">◆</span> {q}
                    </li>
                  ))}
                </ul>
              </div>
            )}
            {data.success_criteria.completion_checklist?.length > 0 && (
              <div>
                <span className="text-[11px] text-text-muted font-medium uppercase tracking-wider">Completion Checklist</span>
                <ul className="mt-1 space-y-0.5">
                  {data.success_criteria.completion_checklist.map((c, i) => (
                    <li key={i} className="text-xs text-text-secondary flex items-start gap-1.5">
                      <span className="text-orange/60 mt-0.5 shrink-0">☐</span> {c}
                    </li>
                  ))}
                </ul>
              </div>
            )}
          </div>
        </CollapsibleSection>
      )}
    </div>
  );

  return (
    <AppLayout title="Tasks Management">
      {/* Header */}
      <div className="flex flex-wrap items-center justify-between mb-6 gap-3">
        <div className="flex items-center gap-2">
          {statuses.map(s => (
            <button key={s} onClick={() => setFilter(s)} className={`px-3 py-1.5 rounded-lg text-xs ${filter === s ? 'bg-orange text-white' : 'bg-surface border border-border text-text-secondary hover:text-white'}`}>{s}</button>
          ))}
        </div>
        <div className="flex items-center gap-3">
          <div className="relative">
            <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-text-muted" />
            <input value={search} onChange={e => setSearch(e.target.value)} placeholder="Search tasks..." className="pl-9 pr-4 py-2 bg-surface border border-border rounded-lg text-sm text-white placeholder:text-text-muted focus:outline-none focus:border-orange" />
          </div>
          <div className="flex bg-surface border border-border rounded-lg overflow-hidden">
            <button onClick={() => setView('list')} className={`p-2 ${view === 'list' ? 'bg-orange text-white' : 'text-text-secondary'}`}><ListTodo size={16} /></button>
            <button onClick={() => setView('grid')} className={`p-2 ${view === 'grid' ? 'bg-orange text-white' : 'text-text-secondary'}`}><LayoutGrid size={16} /></button>
          </div>
          <Button onClick={() => { setError(''); setShowCreate(true); }}><span className="flex items-center gap-2"><Plus size={16} /> New Task</span></Button>
        </div>
      </div>

      {/* List View */}
      {view === 'list' ? (
        <Card>
          <div className="overflow-x-auto">
            <table className="w-full">
              <thead>
                <tr className="border-b border-border">
                  <th className="text-left py-3 px-4 text-xs text-text-muted font-medium">Task</th>
                  <th className="text-left py-3 px-4 text-xs text-text-muted font-medium">Project</th>
                  <th className="text-left py-3 px-4 text-xs text-text-muted font-medium">Assignee</th>
                  <th className="text-left py-3 px-4 text-xs text-text-muted font-medium">Status</th>
                  <th className="text-left py-3 px-4 text-xs text-text-muted font-medium">Priority</th>
                  <th className="text-left py-3 px-4 text-xs text-text-muted font-medium">Progress</th>
                  <th className="text-left py-3 px-4 text-xs text-text-muted font-medium">Due</th>
                  <th className="text-left py-3 px-4 text-xs text-text-muted font-medium">AI</th>
                </tr>
              </thead>
              <tbody>
                {filtered.map(t => {
                  const project = projects.find(p => p.id === t.projectId);
                  const assignee = users.find(u => t.assigneeIds.includes(u.id));
                  const isExpanded = expandedAiTaskId === t.id;
                  return (
                    <>
                      <tr key={t.id} className={`border-b ${isExpanded ? 'border-purple-500/30 bg-purple-500/5' : 'border-border/50'} hover:bg-surface-hover cursor-pointer transition-colors group`}>
                        <td className="py-3 px-4 text-sm text-white" onClick={() => navigate(`/tasks/${t.id}`)}>{t.name}</td>
                        <td className="py-3 px-4 text-sm text-text-secondary" onClick={() => navigate(`/tasks/${t.id}`)}>{project?.name}</td>
                        <td className="py-3 px-4" onClick={() => navigate(`/tasks/${t.id}`)}><span className="text-sm text-text-secondary">{assignee?.name}</span></td>
                        <td className="py-3 px-4" onClick={() => navigate(`/tasks/${t.id}`)}><Badge variant={statusVariant[t.status]}>{t.status}</Badge></td>
                        <td className="py-3 px-4" onClick={() => navigate(`/tasks/${t.id}`)}><Badge variant={priorityVariant[t.priority]}>{t.priority}</Badge></td>
                        <td className="py-3 px-4 w-32" onClick={() => navigate(`/tasks/${t.id}`)}><ProgressBar value={t.progress} size="sm" /></td>
                        <td className="py-3 px-4 text-sm text-text-muted whitespace-nowrap" onClick={() => navigate(`/tasks/${t.id}`)}>{t.dueDate}</td>
                        <td className="py-3 px-4">
                          {t.aiRecommendation ? (
                            <button
                              onClick={(e) => { e.stopPropagation(); toggleExpandedAi(t.id); }}
                              className={`flex items-center gap-1 text-xs transition-all px-2 py-1 rounded-full ${t.aiRecommendation._saved
                                  ? 'text-green-400 bg-green-500/10 hover:bg-green-500/20'
                                  : isExpanded
                                    ? 'text-purple-300 bg-purple-500/20 ring-1 ring-purple-500/40'
                                    : 'text-purple-400 hover:text-purple-300 bg-purple-500/10'
                                }`}
                              title={t.aiRecommendation._saved ? 'AI Recommendation Saved' : isExpanded ? 'Collapse AI Panel' : 'Expand AI Recommendation'}
                            >
                              {t.aiRecommendation._saved ? <Check size={12} /> : <Sparkles size={12} />}
                              {t.aiRecommendation._saved ? 'Saved' : isExpanded ? 'Hide' : 'AI'}
                            </button>
                          ) : (
                            <span className="text-text-muted/30 text-xs">—</span>
                          )}
                        </td>
                      </tr>
                      {/* ── Expanded AI Row ── */}
                      {isExpanded && t.aiRecommendation && (
                        <tr key={`${t.id}-ai`}>
                          <td colSpan={8} className="p-0">
                            <div className="border-b border-purple-500/20 bg-gradient-to-b from-purple-500/5 to-transparent">
                              <div className="p-5">
                                {/* Header bar */}
                                <div className="flex items-center justify-between mb-4">
                                  <div className="flex items-center gap-3">
                                    <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-purple-500 to-indigo-500 flex items-center justify-center shadow-lg shadow-purple-500/20">
                                      <Sparkles size={16} className="text-white" />
                                    </div>
                                    <div>
                                      <h4 className="text-sm font-semibold text-white">AI Recommendation</h4>
                                      <p className="text-[11px] text-text-muted">Generated insights for "{t.name}"</p>
                                    </div>
                                  </div>
                                  <div className="flex items-center gap-2">
                                    {!t.aiRecommendation._saved && (
                                      <button
                                        onClick={() => handleAcceptAiRecommendation(t)}
                                        disabled={savingAiForTask === t.id}
                                        className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium bg-gradient-to-r from-green-600 to-emerald-600 hover:from-green-500 hover:to-emerald-500 text-white shadow-lg shadow-green-500/20 transition-all disabled:opacity-50"
                                      >
                                        {savingAiForTask === t.id ? (
                                          <><span className="animate-spin inline-block w-3 h-3 border-2 border-white/30 border-t-white rounded-full" /> Saving...</>
                                        ) : (
                                          <><Save size={12} /> Accept & Save</>
                                        )}
                                      </button>
                                    )}
                                    {t.aiRecommendation._saved && (
                                      <span className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium bg-green-500/10 text-green-400 border border-green-500/20">
                                        <Check size={12} /> Recommendation Saved
                                      </span>
                                    )}
                                    <button
                                      onClick={() => toggleExpandedAi(null)}
                                      className="p-1.5 rounded-lg text-text-muted hover:text-white hover:bg-surface-hover transition-colors"
                                    >
                                      <X size={14} />
                                    </button>
                                  </div>
                                </div>

                                {/* Two-column layout: Task summary + AI details */}
                                <div className="flex gap-5">
                                  {/* Left: Task Summary Card */}
                                  <div className="w-[260px] shrink-0">
                                    <div className="rounded-xl bg-surface border border-border p-4 space-y-3">
                                      <h5 className="text-xs font-semibold text-text-muted uppercase tracking-wider">Task Summary</h5>
                                      <div className="space-y-2.5">
                                        <div>
                                          <span className="text-[10px] text-text-muted uppercase tracking-wider">Name</span>
                                          <p className="text-sm text-white font-medium mt-0.5">{t.name}</p>
                                        </div>
                                        {t.description && (
                                          <div>
                                            <span className="text-[10px] text-text-muted uppercase tracking-wider">Description</span>
                                            <p className="text-xs text-text-secondary mt-0.5 line-clamp-3">{t.description}</p>
                                          </div>
                                        )}
                                        <div className="grid grid-cols-2 gap-2">
                                          <div>
                                            <span className="text-[10px] text-text-muted uppercase tracking-wider">Status</span>
                                            <div className="mt-1"><Badge variant={statusVariant[t.status]}>{t.status}</Badge></div>
                                          </div>
                                          <div>
                                            <span className="text-[10px] text-text-muted uppercase tracking-wider">Priority</span>
                                            <div className="mt-1"><Badge variant={priorityVariant[t.priority]}>{t.priority}</Badge></div>
                                          </div>
                                        </div>
                                        <div className="grid grid-cols-2 gap-2">
                                          <div>
                                            <span className="text-[10px] text-text-muted uppercase tracking-wider">Project</span>
                                            <p className="text-xs text-text-secondary mt-0.5">{project?.name || '—'}</p>
                                          </div>
                                          <div>
                                            <span className="text-[10px] text-text-muted uppercase tracking-wider">Assignee</span>
                                            <p className="text-xs text-text-secondary mt-0.5">{assignee?.name || '—'}</p>
                                          </div>
                                        </div>
                                        <div className="grid grid-cols-2 gap-2">
                                          <div>
                                            <span className="text-[10px] text-text-muted uppercase tracking-wider">Due Date</span>
                                            <p className="text-xs text-text-secondary mt-0.5">{t.dueDate || '—'}</p>
                                          </div>
                                          <div>
                                            <span className="text-[10px] text-text-muted uppercase tracking-wider">Progress</span>
                                            <div className="mt-1"><ProgressBar value={t.progress} size="sm" /></div>
                                          </div>
                                        </div>
                                      </div>
                                      <button
                                        onClick={() => navigate(`/tasks/${t.id}`)}
                                        className="w-full mt-2 flex items-center justify-center gap-1.5 px-3 py-2 rounded-lg text-xs font-medium bg-orange/10 text-orange hover:bg-orange/20 transition-colors"
                                      >
                                        <Eye size={12} /> View Full Task
                                      </button>
                                    </div>
                                  </div>

                                  {/* Right: AI Recommendation Details */}
                                  <div className="flex-1 min-w-0 overflow-y-auto" style={{ maxHeight: '450px' }}>
                                    <AiRecommendationContent data={t.aiRecommendation} selectable={false} />
                                  </div>
                                </div>
                              </div>
                            </div>
                          </td>
                        </tr>
                      )}
                    </>
                  );
                })}
              </tbody>
            </table>
          </div>
          {filtered.length === 0 && <p className="text-sm text-text-muted text-center py-6">No tasks match your filters.</p>}
        </Card>
      ) : (
        /* Grid View */
        <div className="grid md:grid-cols-2 lg:grid-cols-3 gap-4">
          {filtered.map(t => {
            const project = projects.find(p => p.id === t.projectId);
            const assignee = users.find(u => t.assigneeIds.includes(u.id));
            const isExpanded = expandedAiTaskId === t.id;
            return (
              <div key={t.id} className={`transition-all duration-300 ${isExpanded ? 'md:col-span-2 lg:col-span-3' : ''}`}>
                <Card className={`hover:border-orange/30 cursor-pointer transition-all relative ${isExpanded ? 'border-purple-500/30 ring-1 ring-purple-500/20' : ''}`} onClick={() => !isExpanded && navigate(`/tasks/${t.id}`)}>
                  <div className="flex items-center justify-between mb-2">
                    <Badge variant={statusVariant[t.status]}>{t.status}</Badge>
                    <div className="flex items-center gap-2">
                      {t.aiRecommendation && (
                        <button
                          onClick={(e) => { e.stopPropagation(); toggleExpandedAi(t.id); }}
                          className={`flex items-center gap-1 text-[10px] px-2 py-0.5 rounded-full transition-all ${t.aiRecommendation._saved
                              ? 'text-green-400 bg-green-500/10 hover:bg-green-500/20'
                              : isExpanded
                                ? 'text-purple-300 bg-purple-500/20 ring-1 ring-purple-500/40'
                                : 'text-purple-400 hover:text-purple-300 bg-purple-500/10'
                            }`}
                        >
                          {t.aiRecommendation._saved ? <Check size={10} /> : <Sparkles size={10} />}
                          {t.aiRecommendation._saved ? 'Saved' : isExpanded ? 'Hide' : 'AI'}
                        </button>
                      )}
                      <Badge variant={priorityVariant[t.priority]}>{t.priority}</Badge>
                    </div>
                  </div>
                  <h3 className="text-sm font-semibold text-white mb-1">{t.name}</h3>
                  <p className="text-xs text-text-muted mb-3">{project?.name}</p>
                  <ProgressBar value={t.progress} size="sm" />
                  <div className="flex items-center justify-between mt-3 pt-2 border-t border-border/50 text-xs text-text-muted">
                    <span>{assignee?.name}</span>
                    <span>{t.dueDate}</span>
                  </div>

                  {/* ── Expanded AI Panel for Grid ── */}
                  {isExpanded && t.aiRecommendation && (
                    <div className="mt-4 pt-4 border-t border-purple-500/20" onClick={(e) => e.stopPropagation()}>
                      {/* Header */}
                      <div className="flex items-center justify-between mb-4">
                        <div className="flex items-center gap-2">
                          <div className="w-7 h-7 rounded-lg bg-gradient-to-br from-purple-500 to-indigo-500 flex items-center justify-center shadow-lg shadow-purple-500/20">
                            <Sparkles size={14} className="text-white" />
                          </div>
                          <div>
                            <h4 className="text-xs font-semibold text-white">AI Recommendation</h4>
                            <p className="text-[10px] text-text-muted">Generated insights</p>
                          </div>
                        </div>
                        <div className="flex items-center gap-2">
                          {!t.aiRecommendation._saved && (
                            <button
                              onClick={() => handleAcceptAiRecommendation(t)}
                              disabled={savingAiForTask === t.id}
                              className="flex items-center gap-1 px-2.5 py-1 rounded-lg text-[11px] font-medium bg-gradient-to-r from-green-600 to-emerald-600 hover:from-green-500 hover:to-emerald-500 text-white transition-all disabled:opacity-50"
                            >
                              {savingAiForTask === t.id ? (
                                <><span className="animate-spin inline-block w-3 h-3 border-2 border-white/30 border-t-white rounded-full" /> Saving...</>
                              ) : (
                                <><Save size={11} /> Accept</>
                              )}
                            </button>
                          )}
                          {t.aiRecommendation._saved && (
                            <span className="flex items-center gap-1 px-2.5 py-1 rounded-lg text-[11px] font-medium bg-green-500/10 text-green-400 border border-green-500/20">
                              <Check size={11} /> Saved
                            </span>
                          )}
                        </div>
                      </div>

                      {/* AI Content */}
                      <div className="max-h-[400px] overflow-y-auto pr-1">
                        <AiRecommendationContent data={t.aiRecommendation} selectable={false} />
                      </div>

                      {/* Footer: View Task */}
                      <div className="mt-3 pt-3 border-t border-border/50 flex justify-end">
                        <button
                          onClick={() => navigate(`/tasks/${t.id}`)}
                          className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium bg-orange/10 text-orange hover:bg-orange/20 transition-colors"
                        >
                          <Eye size={12} /> View Full Task
                        </button>
                      </div>
                    </div>
                  )}
                </Card>
              </div>
            );
          })}
        </div>
      )}

      {/* ════════════════ Create Task Modal ════════════════ */}
      <Modal
        isOpen={showCreate}
        onClose={() => { setShowCreate(false); resetCreateForm(); }}
        title="Create Task"
        size={showAiPanel ? 'full' : 'lg'}
      >
        <div className={`${showAiPanel ? 'flex gap-6' : ''}`} style={{ maxHeight: '75vh' }}>
          {/* Left: Form */}
          <div className={`space-y-4 overflow-y-auto pr-2 ${showAiPanel ? 'w-[400px] shrink-0' : 'w-full'}`}>
            <div>
              <label className="block text-sm text-text-secondary mb-1.5">Task Name *</label>
              <input value={newTask.taskName} onChange={e => setNewTask(prev => ({ ...prev, taskName: e.target.value }))} className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-white placeholder:text-text-muted focus:outline-none focus:border-orange" placeholder="Enter task name..." />
            </div>
            <div>
              <label className="block text-sm text-text-secondary mb-1.5">Description</label>
              <textarea rows={3} value={newTask.description} onChange={e => setNewTask(prev => ({ ...prev, description: e.target.value }))} className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-white placeholder:text-text-muted focus:outline-none focus:border-orange resize-none" placeholder="Describe the task..." />
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="block text-sm text-text-secondary mb-1.5">Project *</label>
                <select value={newTask.projectId} onChange={e => setNewTask(prev => ({ ...prev, projectId: e.target.value }))} className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-white focus:outline-none focus:border-orange appearance-none">
                  <option value="">Select project...</option>
                  {projects.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}
                </select>
              </div>
              <div>
                <label className="block text-sm text-text-secondary mb-1.5">Priority</label>
                <select value={newTask.priority} onChange={e => setNewTask(prev => ({ ...prev, priority: e.target.value }))} className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-white focus:outline-none focus:border-orange appearance-none">
                  <option value={0}>Low</option><option value={1}>Medium</option><option value={2}>High</option><option value={3}>Critical</option>
                </select>
              </div>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="block text-sm text-text-secondary mb-1.5">Assignee *</label>
                <select value={newTask.assignedTo} onChange={e => setNewTask(prev => ({ ...prev, assignedTo: e.target.value }))} className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-white focus:outline-none focus:border-orange appearance-none">
                  <option value="">Select assignee...</option>
                  {users.map(u => <option key={u.id} value={u.id}>{u.name}</option>)}
                </select>
              </div>
              <div>
                <label className="block text-sm text-text-secondary mb-1.5">Due Date *</label>
                <input type="date" value={newTask.dueDate} onChange={e => setNewTask(prev => ({ ...prev, dueDate: e.target.value }))} className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-white focus:outline-none focus:border-orange" />
              </div>
            </div>
            <div>
              <label className="block text-sm text-text-secondary mb-1.5">Start Date</label>
              <input type="date" value={newTask.startDate} onChange={e => setNewTask(prev => ({ ...prev, startDate: e.target.value }))} className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-white focus:outline-none focus:border-orange" />
            </div>

            {/* AI Subtasks indicator */}
            {selectedSubtasks.length > 0 && aiRecommendation && (
              <div className="flex items-center gap-2 px-3 py-2 rounded-lg bg-purple-500/10 border border-purple-500/20">
                <CheckCircle size={14} className="text-purple-400" />
                <span className="text-xs text-purple-300">{selectedSubtasks.length} AI-suggested subtask{selectedSubtasks.length > 1 ? 's' : ''} will be added</span>
              </div>
            )}

            {error && <p className="text-sm text-danger">{error}</p>}

            {/* Action buttons */}
            <div className="flex items-center justify-between gap-3 pt-2 border-t border-border/50">
              <div className="flex items-center gap-2">
                <button
                  id="ai-recommend-btn"
                  onClick={handleGetRecommendation}
                  disabled={!newTask.taskName || aiLoading}
                  className="flex items-center gap-2 px-4 py-2.5 rounded-lg text-sm font-medium transition-all disabled:opacity-40 disabled:cursor-not-allowed bg-gradient-to-r from-purple-600 to-indigo-600 hover:from-purple-500 hover:to-indigo-500 text-white shadow-lg shadow-purple-500/20"
                >
                  {aiLoading ? (
                    <><span className="animate-spin inline-block w-4 h-4 border-2 border-white/30 border-t-white rounded-full" /> Analyzing...</>
                  ) : (
                    <><Sparkles size={16} /> AI Recommend</>
                  )}
                </button>
                {aiRecommendation && !aiLoading && (
                  <button
                    onClick={handleApplyAiRecommendation}
                    className="flex items-center gap-2 px-4 py-2.5 rounded-lg text-sm font-medium transition-all bg-gradient-to-r from-green-600 to-emerald-600 hover:from-green-500 hover:to-emerald-500 text-white shadow-lg shadow-green-500/20"
                  >
                    <Check size={16} /> Apply AI to Form
                  </button>
                )}
              </div>
              <div className="flex gap-3">
                <Button variant="secondary" onClick={() => { setShowCreate(false); resetCreateForm(); }} disabled={creating}>Cancel</Button>
                <Button onClick={handleCreateTask} disabled={creating}>{creating ? 'Creating...' : 'Create Task'}</Button>
              </div>
            </div>
          </div>

          {/* Right: AI Recommendation Panel */}
          {showAiPanel && (
            <div className="flex-1 overflow-y-auto border-l border-border pl-5 min-w-[350px]">
              {/* Header */}
              <div className="flex items-center justify-between mb-4 sticky top-0 bg-surface pb-3 z-10 border-b border-border/50">
                <h4 className="text-sm font-semibold text-white flex items-center gap-2">
                  <div className="w-7 h-7 rounded-lg bg-gradient-to-br from-purple-500 to-indigo-500 flex items-center justify-center shadow-lg shadow-purple-500/20">
                    <Sparkles size={13} className="text-white" />
                  </div>
                  AI Recommendations
                </h4>
                <button onClick={() => setShowAiPanel(false)} className="text-text-muted hover:text-white transition-colors p-1.5 rounded-lg hover:bg-surface-hover">
                  <X size={14} />
                </button>
              </div>

              {/* Loading */}
              {aiLoading && (
                <div className="flex flex-col items-center justify-center py-20 text-center">
                  <div className="relative mb-5">
                    <div className="w-16 h-16 rounded-full border-[3px] border-purple-500/20 border-t-purple-500 animate-spin" />
                    <Sparkles size={20} className="absolute inset-0 m-auto text-purple-400 animate-pulse" />
                  </div>
                  <p className="text-sm font-semibold text-white">AI Agent Analyzing...</p>
                  <p className="text-xs text-text-muted mt-1.5">Generating task breakdown, risks & subtasks</p>
                  <div className="mt-4 flex items-center gap-1.5">
                    <div className="w-1.5 h-1.5 rounded-full bg-purple-500 animate-bounce" style={{ animationDelay: '0ms' }} />
                    <div className="w-1.5 h-1.5 rounded-full bg-purple-400 animate-bounce" style={{ animationDelay: '150ms' }} />
                    <div className="w-1.5 h-1.5 rounded-full bg-purple-300 animate-bounce" style={{ animationDelay: '300ms' }} />
                  </div>
                  <p className="text-[10px] text-text-muted/60 mt-4">This may take 30-60 seconds</p>
                </div>
              )}

              {/* Error */}
              {aiError && !aiLoading && (
                <div className="p-4 rounded-lg bg-red-500/10 border border-red-500/20">
                  <div className="flex items-center gap-2 mb-1">
                    <AlertTriangle size={14} className="text-red-400" />
                    <p className="text-sm text-red-400 font-medium">Recommendation Failed</p>
                  </div>
                  <p className="text-xs text-red-400/70 mt-1">{aiError}</p>
                  <button onClick={handleGetRecommendation} className="mt-3 text-xs text-purple-400 hover:text-purple-300 transition-colors">
                    ↻ Retry
                  </button>
                </div>
              )}

              {/* Results */}
              {aiRecommendation && !aiLoading && (
                <>
                  {/* Success banner */}
                  <div className="flex items-center gap-2 px-3 py-2 mb-4 rounded-lg bg-green-500/10 border border-green-500/20">
                    <CheckCircle size={14} className="text-green-400" />
                    <span className="text-xs text-green-300 font-medium">AI analysis complete — review below and create your task</span>
                  </div>
                  <AiRecommendationContent data={aiRecommendation} selectable={true} />
                </>
              )}
            </div>
          )}
        </div>
      </Modal>

      {/* AI Recommendation view is now inline - no separate modal needed */}
    </AppLayout>
  );
}
