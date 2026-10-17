import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import AppLayout from '../../layouts/AppLayout';
import { Card, Badge, Button, ProgressBar } from '../../components/ui';
import { taskService } from '../../services/taskService';
import { evaluationService } from '../../services/evaluationService';
import { ArrowLeft, CheckCircle, RotateCcw, Bug, Brain } from 'lucide-react';

const TASK_STATUS_MAP = { 0: 'Not Started', 1: 'In Progress', 2: 'In Review', 3: 'Completed' };
const TASK_PRIORITY_MAP = { 0: 'Low', 1: 'Medium', 2: 'High', 3: 'Critical' };
const RISK_MAP = { Low: 0, Medium: 1, High: 2, Critical: 3 };

export default function TaskReview() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [task, setTask] = useState(null);
  const [loading, setLoading] = useState(true);
  const [scores, setScores] = useState({ overall: 0, quality: 0, timeliness: 0, collaboration: 0 });
  const [feedback, setFeedback] = useState('');
  const [bugDesc, setBugDesc] = useState('');
  const [bugRisk, setBugRisk] = useState('Low');
  const [showBug, setShowBug] = useState(false);
  const [submitted, setSubmitted] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [progress, setProgress] = useState([]);
  const [attachments, setAttachments] = useState([]);
  const [evaluations, setEvaluations] = useState([]);

  useEffect(() => {
    const fetchTask = async () => {
      if (!id) return;
      try {
        const { data } = await taskService.getTask(id);
        if (data) {
          setAttachments(data.attachments ?? []);
          setTask({
            id: data.taskId ?? data.id,
            name: data.taskName ?? data.name,
            projectId: data.projectId,
            assigneeIds: data.assignedTo ? [data.assignedTo] : [],
            assignedToName: data.assignedTo?.fullName ?? data.assignedTo?.FullName ?? data.assignedToName,
            priority: TASK_PRIORITY_MAP[data.priority] ?? 'Medium',
            status: TASK_STATUS_MAP[data.status] ?? 'In Review',
            progress: data.completionPercentage ?? data.progress ?? 0,
            dueDate: data.dueDate ? (typeof data.dueDate === 'string' ? data.dueDate.slice(0, 10) : data.dueDate) : ''
          });
        }
      } catch (err) {
        console.error('Failed to load task:', err);
      } finally {
        setLoading(false);
      }
    };
    fetchTask();
  }, [id]);

  useEffect(() => {
    const fetchProgress = async () => {
      if (!id) return;
      try {
        const { data } = await taskService.getProgress(id);
        setProgress(Array.isArray(data) ? data : []);
      } catch {
        setProgress([]);
      }
    };
    fetchProgress();
  }, [id]);

  useEffect(() => {
    const fetchEvaluations = async () => {
      if (!id) return;
      try {
        const { data } = await evaluationService.getTaskEvaluations(id);
        setEvaluations(data?.items ?? []);
      } catch {
        setEvaluations([]);
      }
    };
    fetchEvaluations();
  }, [id]);

  const handleApprove = async () => {
    if (!task || !id) return;
    setSubmitting(true);
    try {
      await evaluationService.createEvaluation({
        taskId: Number(id),
        evaluatedUserId: (typeof task.assigneeIds?.[0] === 'object' ? (task.assigneeIds[0]?.userId ?? task.assigneeIds[0]?.UserId) : task.assigneeIds?.[0]) ?? 0,
        overallScore: scores.overall,
        qualityScore: scores.quality,
        timelinessScore: scores.timeliness,
        collaborationScore: scores.collaboration,
        riskLevel: RISK_MAP[bugRisk] ?? 0,
        manualFeedback: feedback || null
      });
      const { data } = await evaluationService.getTaskEvaluations(id);
      setEvaluations(data?.items ?? []);
      setSubmitted(true);
    } catch (err) {
      console.error('Failed to submit evaluation:', err);
    } finally {
      setSubmitting(false);
    }
  };

  const handleRework = async () => {
    if (!task || !id) return;
    setSubmitting(true);
    try {
      await evaluationService.createEvaluation({
        taskId: Number(id),
        evaluatedUserId: (typeof task.assigneeIds?.[0] === 'object' ? (task.assigneeIds[0]?.userId ?? task.assigneeIds[0]?.UserId) : task.assigneeIds?.[0]) ?? 0,
        overallScore: scores.overall,
        qualityScore: scores.quality,
        timelinessScore: scores.timeliness,
        collaborationScore: scores.collaboration,
        riskLevel: 2,
        manualFeedback: (feedback || '') + (feedback ? '\n\n' : '') + 'Rework requested.'
      });
      const { data } = await evaluationService.getTaskEvaluations(id);
      setEvaluations(data?.items ?? []);
      setSubmitted(true);
    } catch (err) {
      console.error('Failed to submit evaluation:', err);
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) return (
    <AppLayout title="Task Review">
      <div className="flex items-center justify-center h-64"><div className="text-text-secondary">Loading...</div></div>
    </AppLayout>
  );

  if (!task) {
    return <AppLayout title="Review Not Found"><Card><p className="text-text-secondary">Task not found.</p></Card></AppLayout>;
  }

  const assignees = task.assigneeIds?.length ? [{ name: task.assignedToName }] : [];

  return (
    <AppLayout title="Task Review">
      <button onClick={() => navigate(-1)} className="flex items-center gap-2 text-sm text-text-secondary hover:text-white mb-4 transition-colors">
        <ArrowLeft size={16} /> Back
      </button>

      {submitted ? (
        <Card>
          <div className="text-center py-8">
            <CheckCircle size={48} className="text-success mx-auto mb-4" />
            <h3 className="text-lg font-semibold text-white mb-2">Review Submitted!</h3>
            <p className="text-sm text-text-secondary mb-4">The employee has been notified of the evaluation.</p>
            <Button onClick={() => navigate('/supervisor')}>Back to Dashboard</Button>
          </div>
        </Card>
      ) : (
        <div className="grid lg:grid-cols-3 gap-6">
          <div className="lg:col-span-2 space-y-4">
            {/* Task Summary */}
            <Card>
              <h2 className="text-lg font-semibold text-white mb-2">{task.name}</h2>
              <div className="flex items-center gap-3 mb-4">
                <Badge variant="info">In Review</Badge>
                <Badge variant={task.priority === 'High' ? 'danger' : 'warning'}>{task.priority}</Badge>
              </div>
              <div className="flex items-center justify-between mb-2">
                <span className="text-sm text-text-secondary">Progress</span>
                <span className="text-sm font-medium text-white">{task.progress}%</span>
              </div>
              <ProgressBar value={task.progress} size="md" />
              <div className="mt-4 grid grid-cols-2 gap-4 text-sm">
                <div><p className="text-text-muted text-xs">Assigned To</p><p className="text-white">{assignees.map(a => a.name).join(', ')}</p></div>
                <div><p className="text-text-muted text-xs">Due Date</p><p className="text-white">{task.dueDate}</p></div>
              </div>
            </Card>

            {/* Submitted Work */}
            <Card>
              <h3 className="text-base font-semibold text-white mb-3">Submitted Work</h3>
              {progress.length > 0 ? (
                <>
                  <div className="space-y-3 mb-3">
                    {progress.slice(0, 3).map((p) => (
                      <div key={p.progressId ?? p.ProgressId} className="p-4 bg-bg rounded-lg border border-border/50">
                        <div className="flex items-center justify-between mb-2">
                          <span className="text-xs text-text-muted">
                            {(p.submittedAt || p.SubmittedAt) ? new Date(p.submittedAt || p.SubmittedAt).toLocaleDateString() : ''}
                            {(p.submittedByName || p.SubmittedByName) && ` — ${p.submittedByName || p.SubmittedByName}`}
                          </span>
                          <span className="text-xs font-medium text-orange">{(p.completionPercentage ?? p.CompletionPercentage) ?? 0}%</span>
                        </div>
                        <p className="text-sm text-text-secondary">{p.notes || p.Notes || '—'}</p>
                        {(p.blockers || p.Blockers) && <p className="text-sm text-text-muted mt-1">Blockers: {p.blockers || p.Blockers}</p>}
                      </div>
                    ))}
                  </div>
                  {attachments.length > 0 && (
                    <div className="space-y-2">
                      {attachments.map((a) => (
                        <div key={a.attachmentId ?? a.AttachmentId} className="flex items-center gap-3 p-2 bg-bg rounded-lg border border-border/50">
                          <span className="text-sm text-white">{a.fileName || a.FileName}</span>
                          <span className="text-xs text-text-muted ml-auto">
                          {(() => {
                            const sz = a.fileSize ?? a.FileSize;
                            if (!sz) return '—';
                            if (sz >= 1024 * 1024) return `${(sz / 1024 / 1024).toFixed(1)} MB`;
                            return `${(sz / 1024).toFixed(1)} KB`;
                          })()}
                        </span>
                        </div>
                      ))}
                    </div>
                  )}
                </>
              ) : (
                <p className="text-sm text-text-secondary p-4 bg-bg rounded-lg border border-border/50">No work submitted yet.</p>
              )}
            </Card>

            {/* Scoring */}
            <Card>
              <h3 className="text-base font-semibold text-white mb-4">Evaluation Scores</h3>
              <div className="grid grid-cols-2 gap-4">
                {Object.entries(scores).map(([key, val]) => (
                  <div key={key}>
                    <div className="flex items-center justify-between mb-1">
                      <label className="text-sm text-text-secondary capitalize">{key}</label>
                      <span className="text-sm font-medium text-orange">{val}</span>
                    </div>
                    <input
                      type="range" min="0" max="100" value={val}
                      onChange={e => setScores(s => ({ ...s, [key]: Number(e.target.value) }))}
                      className="w-full h-2 bg-border rounded-full appearance-none cursor-pointer accent-orange"
                    />
                  </div>
                ))}
              </div>
            </Card>

            {/* Feedback */}
            <Card>
              <h3 className="text-base font-semibold text-white mb-3">Manual Feedback</h3>
              <textarea
                value={feedback} onChange={e => setFeedback(e.target.value)}
                rows={4} placeholder="Provide detailed feedback..."
                className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-white placeholder:text-text-muted focus:outline-none focus:border-orange resize-none"
              />
            </Card>

            {/* Bug Report */}
            {showBug && (
              <Card>
                <div className="flex items-center gap-2 mb-3">
                  <Bug size={16} className="text-danger" />
                  <h3 className="text-base font-semibold text-white">Report Bug</h3>
                </div>
                <div className="space-y-3">
                  <textarea
                    value={bugDesc} onChange={e => setBugDesc(e.target.value)}
                    rows={3} placeholder="Describe the bug..."
                    className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-white placeholder:text-text-muted focus:outline-none focus:border-orange resize-none"
                  />
                  <div>
                    <label className="text-sm text-text-secondary mb-1 block">Risk Level</label>
                    <div className="flex gap-2">
                      {['Low', 'Medium', 'High', 'Critical'].map(r => (
                        <button key={r} onClick={() => setBugRisk(r)} className={`px-3 py-1.5 rounded-md text-xs font-medium ${bugRisk === r ? 'bg-danger text-white' : 'bg-bg border border-border text-text-secondary'}`}>{r}</button>
                      ))}
                    </div>
                  </div>
                </div>
              </Card>
            )}

            {/* Actions */}
            <div className="flex items-center gap-3">
              <Button onClick={handleApprove} disabled={submitting}>
                <span className="flex items-center gap-2"><CheckCircle size={16} /> {submitting ? 'Submitting...' : 'Approve'}</span>
              </Button>
              <Button variant="danger" onClick={handleRework} disabled={submitting}>
                <span className="flex items-center gap-2"><RotateCcw size={16} /> Request Rework</span>
              </Button>
              <Button variant="secondary" onClick={() => setShowBug(!showBug)}>
                <span className="flex items-center gap-2"><Bug size={16} /> {showBug ? 'Hide Bug Form' : 'Report Bug'}</span>
              </Button>
            </div>
          </div>

          {/* Right sidebar */}
          <div className="space-y-4">
            <Card>
              <div className="flex items-center gap-2 mb-3">
                <Brain size={18} className="text-orange" />
                <h3 className="text-base font-semibold text-white">AI Evaluation</h3>
              </div>
              <Button variant="secondary" className="w-full mb-3">Trigger AI Evaluation</Button>
              <div className="p-3 bg-orange/5 border border-orange/20 rounded-lg">
                <p className="text-xs text-text-secondary">AI evaluation will appear here when an AI model is connected.</p>
              </div>
            </Card>

            <Card>
              <h3 className="text-base font-semibold text-white mb-3">Previous Reviews</h3>
              <div className="space-y-2">
                {evaluations.length > 0 ? (
                  evaluations.map((e) => (
                    <div key={e.evaluationId ?? e.EvaluationId} className="p-3 bg-bg rounded-lg border border-border/50">
                      <div className="flex items-center justify-between mb-1">
                        <span className="text-xs text-text-muted">
                          {(e.submittedAt || e.SubmittedAt) ? new Date(e.submittedAt || e.SubmittedAt).toLocaleDateString() : ''}
                        </span>
                        <Badge variant={(e.status ?? e.Status) === 1 ? 'success' : 'warning'}>
                          {(e.status ?? e.Status) === 1 ? 'Approved' : 'Rework'}
                        </Badge>
                      </div>
                      <p className="text-sm text-text-secondary">{e.manualFeedback || e.ManualFeedback || `Overall: ${e.overallScore ?? e.OverallScore}/100`}</p>
                    </div>
                  ))
                ) : (
                  <p className="text-sm text-text-secondary p-3 bg-bg rounded-lg border border-border/50">No previous reviews.</p>
                )}
              </div>
            </Card>
          </div>
        </div>
      )}
    </AppLayout>
  );
}
