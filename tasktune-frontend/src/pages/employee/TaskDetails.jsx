import { useState, useEffect, useRef } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import AppLayout from '../../layouts/AppLayout';
import { Card, Badge, ProgressBar, Button } from '../../components/ui';
import { taskService } from '../../services/taskService';
import { commentService } from '../../services/commentService';
import { attachmentService } from '../../services/attachmentService';
import {
  CheckCircle, Circle, Brain, Paperclip, MessageSquare, Clock,
  ArrowLeft, Upload, Download, Sparkles, Loader2, AlertTriangle,
  Target, ListChecks, Timer, BookOpen, ShieldAlert, Trophy,
  ChevronDown, ChevronUp, RefreshCw
} from 'lucide-react';

const TASK_STATUS_MAP = { 0: 'Not Started', 1: 'In Progress', 2: 'Completed', 3: 'In Review' };
const TASK_PRIORITY_MAP = { 0: 'Low', 1: 'Medium', 2: 'High', 3: 'Critical' };
const SUBTASK_STATUS_MAP = { 0: false, 1: false, 2: true }; // 2 = done

function formatTimeAgo(date) {
  const now = new Date();
  const diffMs = now - date;
  const diffMins = Math.floor(diffMs / 60000);
  const diffHours = Math.floor(diffMs / 3600000);
  const diffDays = Math.floor(diffMs / 86400000);
  if (diffMins < 1) return 'Just now';
  if (diffMins < 60) return `${diffMins} minutes ago`;
  if (diffHours < 24) return `${diffHours} hours ago`;
  if (diffDays < 7) return `${diffDays} days ago`;
  return date.toLocaleDateString();
}

/* ── Collapsible Section wrapper ──────────────────────────────────── */
function GuidanceSection({ icon: Icon, title, children, defaultOpen = true }) {
  const [open, setOpen] = useState(defaultOpen);
  return (
    <div className="border border-border/40 rounded-lg overflow-hidden">
      <button
        onClick={() => setOpen(o => !o)}
        className="w-full flex items-center justify-between px-3 py-2.5 bg-surface-hover/50 hover:bg-surface-hover transition-colors"
      >
        <span className="flex items-center gap-2 text-sm font-medium text-white">
          {Icon && <Icon size={15} className="text-orange" />}
          {title}
        </span>
        {open ? <ChevronUp size={14} className="text-text-muted" /> : <ChevronDown size={14} className="text-text-muted" />}
      </button>
      {open && <div className="px-3 py-3 text-sm text-text-secondary space-y-2">{children}</div>}
    </div>
  );
}

export default function TaskDetails() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [task, setTask] = useState(null);
  const [subtasks, setSubtasks] = useState([]);
  const [progressHistory, setProgressHistory] = useState([]);
  const [loading, setLoading] = useState(true);
  const [comments, setComments] = useState([]);
  const [newComment, setNewComment] = useState('');
  const [attachments, setAttachments] = useState([]);
  const [uploading, setUploading] = useState(false);
  const fileInputRef = useRef(null);

  // ── AI Guidance state ──
  const [guidance, setGuidance] = useState(null);
  const [guidanceLoading, setGuidanceLoading] = useState(false);
  const [guidanceError, setGuidanceError] = useState('');

  useEffect(() => {
    const fetchData = async () => {
      if (!id) return;
      try {
        const [taskRes, subtaskRes, progressRes, commentsRes, attachmentsRes] = await Promise.all([
          taskService.getTask(id),
          taskService.getSubtasks(id),
          taskService.getProgress(id).catch(() => ({ data: [] })),
          commentService.getComments(id).catch(() => ({ data: [] })),
          attachmentService.getAttachments(id).catch(() => ({ data: [] }))
        ]);
        const t = taskRes?.data;
        if (t) {
          setTask({
            id: t.taskId ?? t.id,
            name: t.taskName ?? t.name,
            projectId: t.projectId,
            projectName: t.projectName,
            assigneeIds: t.assignedTo ? [t.assignedTo] : [],
            assignedToName: t.assignedToName,
            supervisorId: t.createdBy,
            priority: TASK_PRIORITY_MAP[t.priority] ?? 'Medium',
            status: TASK_STATUS_MAP[t.status] ?? 'Not Started',
            progress: t.completionPercentage ?? t.progress ?? 0,
            startDate: t.startDate ? (typeof t.startDate === 'string' ? t.startDate.slice(0, 10) : t.startDate) : '',
            dueDate: t.dueDate ? (typeof t.dueDate === 'string' ? t.dueDate.slice(0, 10) : t.dueDate) : '',
            submitMethod: t.submitMethod ?? 'File Upload',
            definitionOfDone: t.definitionOfDone ?? ''
          });
        }
        const subItems = (subtaskRes?.data?.items || subtaskRes?.data || [])?.map(s => ({
          id: s.subtaskId ?? s.id,
          text: s.subtaskName ?? s.text,
          done: SUBTASK_STATUS_MAP[s.status] ?? (s.status === 2)
        }));
        setSubtasks(subItems || []);

        const history = Array.isArray(progressRes?.data) ? progressRes.data : [];
        setProgressHistory(history);

        const cmts = Array.isArray(commentsRes?.data) ? commentsRes.data : [];
        setComments(cmts);

        const atts = Array.isArray(attachmentsRes?.data) ? attachmentsRes.data : [];
        setAttachments(atts);

        // Fetch any previously cached guidance
        try {
          const guidanceRes = await taskService.getGuidance(id);
          if (guidanceRes?.data) setGuidance(guidanceRes.data);
        } catch { /* no cached guidance — that's fine */ }
      } catch (err) {
        console.error('Failed to load task:', err);
      } finally {
        setLoading(false);
      }
    };
    fetchData();
  }, [id]);

  // ── Generate Guidance handler ──
  const handleGenerateGuidance = async () => {
    setGuidanceLoading(true);
    setGuidanceError('');
    try {
      const res = await taskService.generateGuidance(id, { ...task, subtasks });
      setGuidance(res?.data ?? null);
    } catch (err) {
      console.error('Guidance generation failed:', err);
      setGuidanceError(
        err?.response?.data?.ExceptionMessage ||
        err?.response?.data?.Message ||
        err?.response?.data?.exceptionMessage ||
        err?.response?.data?.message ||
        (typeof err?.response?.data === 'string' ? err.response.data : null) ||
        err?.message ||
        'Failed to generate guidance. Please try again.'
      );
    } finally {
      setGuidanceLoading(false);
    }
  };

  if (loading) return (
    <AppLayout title="Task Details">
      <div className="flex items-center justify-center h-64"><div className="text-text-secondary">Loading...</div></div>
    </AppLayout>
  );

  if (!task) {
    return (
      <AppLayout title="Task Not Found">
        <Card>
          <p className="text-text-secondary">Task not found.</p>
          <Button className="mt-4" onClick={() => navigate(-1)}>Go Back</Button>
        </Card>
      </AppLayout>
    );
  }

  const project = { name: task.projectName };
  const supervisor = task.supervisorId ? { name: 'Supervisor' } : null;
  const assignees = task.assigneeIds?.length ? [{ id: task.assigneeIds[0], name: task.assignedToName ?? 'Assignee', position: '' }] : [];
  const completedSubtasks = subtasks.filter(s => s.done).length;

  const statusVariant = { 'Completed': 'success', 'In Progress': 'orange', 'In Review': 'info', 'Not Started': 'default' };
  const priorityVariant = { High: 'danger', Medium: 'warning', Low: 'default' };

  const PRIORITY_COLORS = { high: 'text-red-400', medium: 'text-yellow-400', low: 'text-green-400' };

  return (
    <AppLayout title="Task Details">
      {/* Back button */}
      <button onClick={() => navigate(-1)} className="flex items-center gap-2 text-sm text-text-secondary hover:text-white mb-4 transition-colors">
        <ArrowLeft size={16} /> Back
      </button>

      <div className="grid lg:grid-cols-3 gap-6">
        {/* Main content */}
        <div className="lg:col-span-2 space-y-4">
          {/* Task info */}
          <Card>
            <div className="flex items-start justify-between mb-4">
              <div>
                <h2 className="text-xl font-bold text-white mb-2">{task.name}</h2>
                <div className="flex items-center gap-3 flex-wrap">
                  <Badge variant={statusVariant[task.status]}>{task.status}</Badge>
                  <Badge variant={priorityVariant[task.priority]}>{task.priority} Priority</Badge>
                  <span className="text-xs text-text-muted">{project?.name}</span>
                </div>
              </div>
              <Button onClick={() => navigate(`/tasks/${task.id}/submit`)}>Submit Progress</Button>
            </div>

            <div className="mb-4">
              <div className="flex items-center justify-between mb-1">
                <span className="text-sm text-text-secondary">Progress</span>
                <span className="text-sm font-medium text-white">{task.progress}%</span>
              </div>
              <ProgressBar value={task.progress} size="lg" />
            </div>

            <div className="grid grid-cols-2 md:grid-cols-4 gap-4 text-sm">
              <div>
                <p className="text-text-muted text-xs">Start Date</p>
                <p className="text-white">{task.startDate}</p>
              </div>
              <div>
                <p className="text-text-muted text-xs">Due Date</p>
                <p className="text-white">{task.dueDate}</p>
              </div>
              <div>
                <p className="text-text-muted text-xs">Submit Method</p>
                <p className="text-white">{task.submitMethod}</p>
              </div>
              <div>
                <p className="text-text-muted text-xs">Supervisor</p>
                <p className="text-white">{supervisor?.name || '—'}</p>
              </div>
            </div>
          </Card>

          {/* Definition of Done */}
          <Card>
            <h3 className="text-base font-semibold text-white mb-3">Definition of Done</h3>
            <p className="text-sm text-text-secondary">{task.definitionOfDone}</p>
          </Card>

          {/* Subtasks */}
          <Card>
            <div className="flex items-center justify-between mb-3">
              <h3 className="text-base font-semibold text-white">Subtasks</h3>
              <span className="text-xs text-text-secondary">{completedSubtasks}/{subtasks.length} completed</span>
            </div>
            {subtasks.length === 0 ? (
              <p className="text-sm text-text-muted">No subtasks defined.</p>
            ) : (
              <div className="space-y-2">
                {subtasks.map(st => (
                  <div key={st.id} className="flex items-center gap-3 p-2 rounded-lg hover:bg-surface-hover">
                    {st.done ? <CheckCircle size={18} className="text-success shrink-0" /> : <Circle size={18} className="text-text-muted shrink-0" />}
                    <span className={`text-sm ${st.done ? 'text-text-muted line-through' : 'text-white'}`}>{st.text}</span>
                  </div>
                ))}
              </div>
            )}
          </Card>

          {/* ═══════════════ AI Guidance (full-width, main area) ═══════════════ */}
          <Card>
            <div className="flex items-center justify-between mb-4">
              <div className="flex items-center gap-2">
                <Brain size={20} className="text-orange" />
                <h3 className="text-lg font-semibold text-white">AI Task Guidance</h3>
              </div>
              <button
                id="generate-guidance-btn"
                onClick={handleGenerateGuidance}
                disabled={guidanceLoading}
                className="flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-medium transition-all
                  bg-gradient-to-r from-orange to-orange/80 text-white hover:shadow-lg hover:shadow-orange/25
                  disabled:opacity-60 disabled:cursor-not-allowed active:scale-95"
              >
                {guidanceLoading ? (
                  <><Loader2 size={16} className="animate-spin" /> Generating…</>
                ) : guidance ? (
                  <><RefreshCw size={15} /> Regenerate</>
                ) : (
                  <><Sparkles size={15} /> Generate Guidance</>
                )}
              </button>
            </div>

            {/* Loading state */}
            {guidanceLoading && (
              <div className="flex flex-col items-center justify-center py-12 gap-4">
                <div className="relative">
                  <div className="w-16 h-16 rounded-full border-4 border-orange/20 border-t-orange animate-spin" />
                  <Brain size={24} className="text-orange absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2" />
                </div>
                <div className="text-center">
                  <p className="text-white font-medium">Analyzing your task…</p>
                  <p className="text-sm text-text-muted mt-1">The AI agent is generating personalized guidance</p>
                </div>
              </div>
            )}

            {/* Error state */}
            {guidanceError && !guidanceLoading && (
              <div className="p-4 bg-red-500/10 border border-red-500/30 rounded-lg flex items-start gap-3">
                <AlertTriangle size={18} className="text-red-400 shrink-0 mt-0.5" />
                <div>
                  <p className="text-sm font-medium text-red-400">Generation Failed</p>
                  <p className="text-sm text-red-400/80 mt-1">{guidanceError}</p>
                </div>
              </div>
            )}

            {/* Empty state */}
            {!guidance && !guidanceLoading && !guidanceError && (
              <div className="flex flex-col items-center justify-center py-10 gap-3">
                <div className="w-14 h-14 rounded-full bg-orange/10 flex items-center justify-center">
                  <Sparkles size={26} className="text-orange" />
                </div>
                <p className="text-white font-medium">Get AI-powered guidance</p>
                <p className="text-sm text-text-muted text-center max-w-sm">
                  Click <strong>Generate Guidance</strong> to get a detailed breakdown, risk assessment, and actionable steps for this task.
                </p>
              </div>
            )}

            {/* ── Guidance Content ── */}
            {guidance && !guidanceLoading && (
              <div className="space-y-3">
                {/* Guidance Text */}
                {guidance.guidance_text && (
                  <GuidanceSection icon={Target} title="Guidance Overview">
                    <p className="leading-relaxed whitespace-pre-line">{guidance.guidance_text}</p>
                  </GuidanceSection>
                )}

                {/* Suggested Steps */}
                {guidance.suggested_subtasks?.length > 0 && (
                  <GuidanceSection icon={ListChecks} title={`Suggested Steps (${guidance.suggested_subtasks.length})`}>
                    <ol className="list-decimal list-inside space-y-1.5">
                      {guidance.suggested_subtasks.map((step, i) => (
                        <li key={i} className="leading-relaxed">{step}</li>
                      ))}
                    </ol>
                  </GuidanceSection>
                )}

                {/* Task Breakdown */}
                {guidance.task_breakdown?.length > 0 && (
                  <GuidanceSection icon={ListChecks} title="Task Breakdown">
                    <div className="space-y-3">
                      {guidance.task_breakdown.map((area, i) => (
                        <div key={i} className="p-2.5 bg-bg rounded-lg border border-border/30">
                          <div className="flex items-center gap-2 mb-1.5">
                            <span className="text-white font-medium text-sm">{i + 1}. {area.task}</span>
                            <span className={`text-xs font-medium uppercase ${PRIORITY_COLORS[area.priority?.toLowerCase()] ?? 'text-text-muted'}`}>
                              [{area.priority}]
                            </span>
                          </div>
                          {area.subtasks?.length > 0 && (
                            <ul className="ml-5 space-y-1">
                              {area.subtasks.map((sub, j) => (
                                <li key={j} className="flex items-start gap-2">
                                  <Circle size={8} className="text-text-muted shrink-0 mt-1.5" />
                                  <span>{sub}</span>
                                </li>
                              ))}
                            </ul>
                          )}
                        </div>
                      ))}
                    </div>
                  </GuidanceSection>
                )}

                {/* Estimated Time */}
                {guidance.estimated_time && (
                  <GuidanceSection icon={Timer} title="Estimated Time">
                    <p className="text-white font-medium">{guidance.estimated_time}</p>
                  </GuidanceSection>
                )}

                {/* Resources */}
                {guidance.resources?.length > 0 && (
                  <GuidanceSection icon={BookOpen} title={`Resources (${guidance.resources.length})`} defaultOpen={false}>
                    <ul className="space-y-1.5">
                      {guidance.resources.map((res, i) => (
                        <li key={i} className="flex items-start gap-2">
                          <span className="text-orange shrink-0">•</span>
                          <span>{res}</span>
                        </li>
                      ))}
                    </ul>
                  </GuidanceSection>
                )}

                {/* Risk Factors */}
                {guidance.risk_factors?.length > 0 && (
                  <GuidanceSection icon={ShieldAlert} title={`Risk Factors (${guidance.risk_factors.length})`} defaultOpen={false}>
                    <ul className="space-y-2">
                      {guidance.risk_factors.map((risk, i) => (
                        <li key={i} className="flex items-start gap-2 p-2 bg-red-500/5 border border-red-500/15 rounded-md">
                          <AlertTriangle size={14} className="text-red-400 shrink-0 mt-0.5" />
                          <span>{risk}</span>
                        </li>
                      ))}
                    </ul>
                  </GuidanceSection>
                )}

                {/* Success Criteria */}
                {guidance.success_criteria && Object.keys(guidance.success_criteria).length > 0 && (
                  <GuidanceSection icon={Trophy} title="Success Criteria" defaultOpen={false}>
                    {guidance.success_criteria.deliverables?.length > 0 && (
                      <div className="mb-3">
                        <p className="text-white font-medium text-xs uppercase tracking-wide mb-1.5">Deliverables</p>
                        <ul className="space-y-1">
                          {guidance.success_criteria.deliverables.map((d, i) => (
                            <li key={i} className="flex items-start gap-2">
                              <CheckCircle size={13} className="text-green-400 shrink-0 mt-0.5" />
                              <span>{d}</span>
                            </li>
                          ))}
                        </ul>
                      </div>
                    )}
                    {guidance.success_criteria.quality_indicators?.length > 0 && (
                      <div className="mb-3">
                        <p className="text-white font-medium text-xs uppercase tracking-wide mb-1.5">Quality Indicators</p>
                        <ul className="space-y-1">
                          {guidance.success_criteria.quality_indicators.map((q, i) => (
                            <li key={i} className="flex items-start gap-2">
                              <Target size={13} className="text-blue-400 shrink-0 mt-0.5" />
                              <span>{q}</span>
                            </li>
                          ))}
                        </ul>
                      </div>
                    )}
                    {guidance.success_criteria.completion_checklist?.length > 0 && (
                      <div>
                        <p className="text-white font-medium text-xs uppercase tracking-wide mb-1.5">Completion Checklist</p>
                        <ul className="space-y-1">
                          {guidance.success_criteria.completion_checklist.map((c, i) => (
                            <li key={i} className="flex items-start gap-2">
                              <Circle size={13} className="text-text-muted shrink-0 mt-0.5" />
                              <span>{c}</span>
                            </li>
                          ))}
                        </ul>
                      </div>
                    )}
                  </GuidanceSection>
                )}
              </div>
            )}
          </Card>

          {/* Comments */}
          <Card>
            <div className="flex items-center gap-2 mb-3">
              <MessageSquare size={16} className="text-orange" />
              <h3 className="text-base font-semibold text-white">Comments</h3>
            </div>
            <div className="space-y-3">
              {comments.length === 0 ? (
                <p className="text-sm text-text-muted">No comments yet.</p>
              ) : (
                comments.map((c) => {
                  const name = c.userName ?? c.userFullName ?? 'User';
                  const time = c.createdAt ? formatTimeAgo(new Date(c.createdAt)) : '';
                  return (
                    <div key={c.commentId ?? c.id ?? c.createdAt} className="p-3 bg-bg rounded-lg border border-border/50">
                      <div className="flex items-center justify-between mb-1">
                        <span className="text-sm font-medium text-white">{name}</span>
                        <span className="text-xs text-text-muted">{time}</span>
                      </div>
                      <p className="text-sm text-text-secondary">{c.content ?? c.text}</p>
                    </div>
                  );
                })
              )}
            </div>
            <div className="mt-3 flex gap-2">
              <input
                value={newComment}
                onChange={(e) => setNewComment(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter' && !e.shiftKey && newComment.trim()) {
                    e.preventDefault();
                    commentService.addComment(id, newComment.trim()).then(() => {
                      setNewComment('');
                      commentService.getComments(id).then((res) => setComments(Array.isArray(res?.data) ? res.data : []));
                    });
                  }
                }}
                placeholder="Add a comment..."
                className="flex-1 bg-bg border border-border rounded-lg px-4 py-2 text-sm text-white placeholder:text-text-muted focus:outline-none focus:border-orange"
              />
              <Button
                onClick={() => {
                  if (!newComment.trim()) return;
                  commentService.addComment(id, newComment.trim()).then(() => {
                    setNewComment('');
                    commentService.getComments(id).then((res) => setComments(Array.isArray(res?.data) ? res.data : []));
                  });
                }}
              >
                Send
              </Button>
            </div>
          </Card>
        </div>

        {/* Right sidebar */}
        <div className="space-y-4">
          {/* Assignees */}
          <Card>
            <h3 className="text-base font-semibold text-white mb-3">Assignees</h3>
            <div className="space-y-2">
              {assignees.map(a => (
                <div key={a.id} onClick={() => navigate(`/profile/${a.id}`)} className="flex items-center gap-3 p-2 rounded-lg hover:bg-surface-hover cursor-pointer">
                  <div className="w-8 h-8 rounded-full bg-orange/20 flex items-center justify-center text-orange text-sm font-medium">{a.name.charAt(0)}</div>
                  <div>
                    <p className="text-sm text-white">{a.name}</p>
                    <p className="text-xs text-text-muted">{a.position}</p>
                  </div>
                </div>
              ))}
            </div>
          </Card>

          {/* Attachments */}
          <Card>
            <div className="flex items-center justify-between mb-3">
              <div className="flex items-center gap-2">
                <Paperclip size={16} className="text-orange" />
                <h3 className="text-base font-semibold text-white">Attachments</h3>
              </div>
              <button
                onClick={() => fileInputRef.current?.click()}
                disabled={uploading}
                className="flex items-center gap-1.5 text-xs text-orange hover:text-orange/80 transition-colors disabled:opacity-50"
              >
                <Upload size={14} />
                {uploading ? 'Uploading...' : 'Upload'}
              </button>
              <input
                ref={fileInputRef}
                type="file"
                className="hidden"
                onChange={async (e) => {
                  const file = e.target.files?.[0];
                  if (!file) return;
                  setUploading(true);
                  try {
                    await attachmentService.uploadAttachment(id, file);
                    const res = await attachmentService.getAttachments(id);
                    setAttachments(Array.isArray(res?.data) ? res.data : []);
                  } catch (err) {
                    console.error('Upload failed:', err);
                  } finally {
                    setUploading(false);
                    e.target.value = '';
                  }
                }}
              />
            </div>
            <div className="space-y-2">
              {attachments.length === 0 ? (
                <p className="text-sm text-text-muted">No attachments uploaded yet.</p>
              ) : (
                attachments.map((att) => {
                  const name = att.fileName ?? att.name ?? 'File';
                  const size = att.fileSize ?? att.size;
                  const sizeLabel = size ? (size < 1024 ? `${size} B` : size < 1048576 ? `${(size / 1024).toFixed(1)} KB` : `${(size / 1048576).toFixed(1)} MB`) : '';
                  const uploader = att.uploadedByName ?? att.userName ?? '';
                  const url = att.fileUrl ?? att.url ?? att.downloadUrl;
                  return (
                    <div key={att.attachmentId ?? att.id ?? name} className="flex items-center justify-between p-2 rounded-lg hover:bg-surface-hover group">
                      <div className="flex items-center gap-2 min-w-0">
                        <Paperclip size={14} className="text-text-muted shrink-0" />
                        <div className="min-w-0">
                          <p className="text-sm text-white truncate">{name}</p>
                          <p className="text-xs text-text-muted">{[sizeLabel, uploader].filter(Boolean).join(' · ')}</p>
                        </div>
                      </div>
                      {url && (
                        <a href={url} target="_blank" rel="noopener noreferrer" className="text-orange hover:text-orange/80 shrink-0">
                          <Download size={14} />
                        </a>
                      )}
                    </div>
                  );
                })
              )}
            </div>
          </Card>

          {/* Activity Timeline */}
          <Card>
            <div className="flex items-center gap-2 mb-3">
              <Clock size={16} className="text-orange" />
              <h3 className="text-base font-semibold text-white">Activity</h3>
            </div>
            <div className="space-y-3">
              {progressHistory.length === 0 ? (
                <p className="text-sm text-text-muted">No activity recorded yet.</p>
              ) : (
                progressHistory.map((ev) => {
                  const pct = ev.completionPercentage ?? ev.CompletionPercentage ?? 0;
                  const notes = ev.notes ?? ev.Notes;
                  const text = notes ? `Progress updated to ${pct}% — ${notes}` : `Progress updated to ${pct}%`;
                  const submittedAt = ev.submittedAt ?? ev.SubmittedAt;
                  const submittedByName = ev.submittedByName ?? ev.SubmittedByName;
                  const submittedDate = submittedAt ? (typeof submittedAt === 'string' ? new Date(submittedAt) : submittedAt) : null;
                  const timeAgo = submittedDate ? formatTimeAgo(submittedDate) : '';
                  return (
                    <div key={ev.progressId ?? ev.ProgressId ?? ev.submittedAt} className="flex items-start gap-3">
                      <div className="w-1.5 h-1.5 rounded-full bg-orange mt-2 shrink-0" />
                      <div>
                        <p className="text-sm text-text-secondary">{text}</p>
                        <p className="text-xs text-text-muted">{submittedByName ? `${submittedByName} · ` : ''}{timeAgo}</p>
                      </div>
                    </div>
                  );
                })
              )}
            </div>
          </Card>
        </div>
      </div>
    </AppLayout>
  );
}
