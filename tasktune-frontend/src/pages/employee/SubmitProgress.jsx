import { useState, useEffect, useCallback, useRef } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import AppLayout from '../../layouts/AppLayout';
import { Card, Button, Badge } from '../../components/ui';
import { taskService } from '../../services/taskService';
import { attachmentService } from '../../services/attachmentService';
import { ArrowLeft, Upload, CheckCircle, X, Bot, TrendingUp, AlertTriangle, Calendar, Shield, Lightbulb, Loader2 } from 'lucide-react';

export default function SubmitProgress() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [task, setTask]         = useState(null);
  const [loading, setLoading]   = useState(true);
  const [progress, setProgress] = useState(0);
  const [notes, setNotes]       = useState('');
  const [blockers, setBlockers] = useState('');
  const [submitted, setSubmitted]   = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [history, setHistory] = useState([]);
  const [files, setFiles]     = useState([]);
  const fileRef = useRef(null);

  // AI Progress Analysis state
  const [aiAnalysis, setAiAnalysis] = useState(null);
  const [aiLoading, setAiLoading] = useState(false);
  const [aiError, setAiError] = useState(null);
  const [showAiPanel, setShowAiPanel] = useState(false);

  useEffect(() => {
    const fetchTask = async () => {
      if (!id) return;
      try {
        const { data } = await taskService.getTask(id);
        if (data) {
          setTask({
            id:       data.taskId ?? data.id,
            name:     data.taskName ?? data.name,
            progress: data.completionPercentage ?? data.progress ?? 0,
            dueDate:  data.dueDate ? (typeof data.dueDate === 'string' ? data.dueDate.slice(0, 10) : data.dueDate) : '',
            startDate: data.startDate ? (typeof data.startDate === 'string' ? data.startDate.slice(0, 10) : data.startDate) : '',
            description: data.definitionOfDone ?? data.description ?? '',
          });
          setProgress(data.completionPercentage ?? data.progress ?? 0);
        }
      } catch (err) {
        console.error('Failed to load task:', err);
      } finally {
        setLoading(false);
      }
    };
    fetchTask();
  }, [id]);

  const fetchProgressHistory = useCallback(async () => {
    if (!id) return;
    try {
      const { data } = await taskService.getProgress(id);
      const items = Array.isArray(data) ? data : (data?.items ?? data?.data ?? []);
      setHistory(items);
    } catch (err) {
      console.error('Failed to load progress history:', err);
      setHistory([]);
    }
  }, [id]);

  useEffect(() => { fetchProgressHistory(); }, [fetchProgressHistory]);
  useEffect(() => { if (submitted) fetchProgressHistory(); }, [submitted, fetchProgressHistory]);

  useEffect(() => {
    const fetchLatestAnalysis = async () => {
      if (!id) return;
      try {
        const { data } = await taskService.getLatestProgressAnalysis(id);
        if (data && Object.keys(data).length > 0) {
          setAiAnalysis(data);
          setShowAiPanel(true);
        }
      } catch (err) {
        console.error('Failed to load latest AI analysis:', err);
      }
    };
    fetchLatestAnalysis();
  }, [id]);

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!id) return;
    setSubmitting(true);
    try {
      await taskService.submitProgress(id, {
        completionPercentage: progress,
        notes:    notes    || null,
        blockers: blockers || null,
      });
      for (const file of files) {
        await attachmentService.uploadAttachment(id, file).catch((err) =>
          console.error(`Failed to upload ${file.name}:`, err)
        );
      }
      setFiles([]);
      setSubmitted(true);
    } catch (err) {
      console.error('Failed to submit progress:', err);
    } finally {
      setSubmitting(false);
    }
  };

  // AI Progress Analysis handler
  const handleAiAnalyze = async () => {
    if (!id) return;
    setAiLoading(true);
    setAiError(null);
    setAiAnalysis(null);
    setShowAiPanel(true);
    try {
      const { data } = await taskService.analyzeProgress(id, {
        completionPercentage: progress,
        notes: notes || undefined,
        blockers: blockers || undefined,
      });
      setAiAnalysis(data);
    } catch (err) {
      console.error('AI analysis failed:', err);
      setAiError(err?.response?.data?.message || err?.response?.data?.exceptionMessage || 'Failed to analyze progress. Please make sure the AI agent is running.');
    } finally {
      setAiLoading(false);
    }
  };

  const getRiskColor = (level) => {
    switch (level?.toLowerCase()) {
      case 'low': return 'text-emerald-400';
      case 'medium': return 'text-amber-400';
      case 'high': return 'text-red-400';
      default: return 'text-text-secondary';
    }
  };

  const getRiskBg = (level) => {
    switch (level?.toLowerCase()) {
      case 'low': return 'bg-emerald-500/10 border-emerald-500/20';
      case 'medium': return 'bg-amber-500/10 border-amber-500/20';
      case 'high': return 'bg-red-500/10 border-red-500/20';
      default: return 'bg-bg border-border';
    }
  };

  const getSummaryIcon = (summary) => {
    switch (summary) {
      case 'On track': return <TrendingUp size={18} className="text-emerald-400" />;
      case 'At risk': return <AlertTriangle size={18} className="text-amber-400" />;
      case 'Delayed': return <AlertTriangle size={18} className="text-red-400" />;
      default: return <TrendingUp size={18} className="text-text-secondary" />;
    }
  };

  const getSummaryColor = (summary) => {
    switch (summary) {
      case 'On track': return 'text-emerald-400';
      case 'At risk': return 'text-amber-400';
      case 'Delayed': return 'text-red-400';
      default: return 'text-text-secondary';
    }
  };

  if (loading)
    return (
      <AppLayout title="Submit Progress">
        <div className="flex items-center justify-center h-64">
          <div className="text-text-secondary">Loading...</div>
        </div>
      </AppLayout>
    );

  if (!task)
    return (
      <AppLayout title="Task Not Found">
        <Card><p className="text-text-secondary">Task not found.</p></Card>
      </AppLayout>
    );

  return (
    <AppLayout title="Submit Progress">
      <button
        onClick={() => navigate(-1)}
        className="flex items-center gap-2 text-sm text-text-secondary hover:text-text-primary mb-4 transition-colors"
      >
        <ArrowLeft size={16} /> Back to Task
      </button>

      <div className={`grid gap-6 ${showAiPanel ? 'lg:grid-cols-5' : 'lg:grid-cols-3'}`}>
        <div className={showAiPanel ? 'lg:col-span-2' : 'lg:col-span-2'}>
          {submitted ? (
            <Card>
              <div className="text-center py-8">
                <CheckCircle size={48} className="text-success mx-auto mb-4" />
                <h3 className="text-lg font-semibold text-text-primary mb-2">Progress Submitted!</h3>
                <p className="text-sm text-text-secondary mb-6">
                  Your supervisor has been notified and will review your submission.
                </p>
                <div className="flex items-center gap-3 justify-center">
                  <Button onClick={() => navigate(`/tasks/${id}`)}>Back to Task</Button>
                  <Button variant="secondary" onClick={() => navigate('/dashboard')}>Go to Dashboard</Button>
                </div>
              </div>
            </Card>
          ) : (
            <Card>
              <h2 className="text-lg font-semibold text-text-primary mb-1">Submit Progress for: {task.name}</h2>
              <p className="text-sm text-text-muted mb-6">Task #{task.id}</p>
              <form onSubmit={handleSubmit} className="space-y-5">
                {/* Progress Slider */}
                <div>
                  <div className="flex items-center justify-between mb-2">
                    <label className="text-sm text-text-secondary">Completion Percentage</label>
                    <span className="text-lg font-bold text-orange">{progress}%</span>
                  </div>
                  <input
                    type="range" min={0} max={100} value={progress}
                    onChange={(e) => setProgress(Number(e.target.value))}
                    className="w-full h-2 bg-border rounded-full appearance-none cursor-pointer accent-orange"
                  />
                  <div className="flex justify-between text-xs text-text-muted mt-1">
                    {['0%','25%','50%','75%','100%'].map((l) => <span key={l}>{l}</span>)}
                  </div>
                </div>
                {/* Notes */}
                <div>
                  <label className="block text-sm text-text-secondary mb-1.5">Progress Notes</label>
                  <textarea
                    value={notes} onChange={(e) => setNotes(e.target.value)}
                    rows={4} placeholder="Describe what you've accomplished..."
                    className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-text-primary placeholder:text-text-muted focus:outline-none focus:border-orange resize-none"
                  />
                </div>
                {/* Blockers */}
                <div>
                  <label className="block text-sm text-text-secondary mb-1.5">Blockers (optional)</label>
                  <textarea
                    value={blockers} onChange={(e) => setBlockers(e.target.value)}
                    rows={3} placeholder="Any blockers or issues..."
                    className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-text-primary placeholder:text-text-muted focus:outline-none focus:border-orange resize-none"
                  />
                </div>
                {/* File Upload */}
                <div>
                  <label className="block text-sm text-text-secondary mb-1.5">Attachments</label>
                  <input
                    ref={fileRef} type="file" multiple className="hidden"
                    onChange={(e) => {
                      if (e.target.files?.length) {
                        setFiles((prev) => [...prev, ...Array.from(e.target.files)]);
                        e.target.value = '';
                      }
                    }}
                  />
                  <div
                    onClick={() => fileRef.current?.click()}
                    className="border-2 border-dashed border-border rounded-lg p-8 text-center hover:border-orange/50 transition-colors cursor-pointer"
                  >
                    <Upload size={24} className="text-text-muted mx-auto mb-2" />
                    <p className="text-sm text-text-secondary">Drag files here or click to upload</p>
                    <p className="text-xs text-text-muted mt-1">PDF, ZIP, PNG, JPG up to 10MB</p>
                  </div>
                  {files.length > 0 && (
                    <div className="mt-2 space-y-1">
                      {files.map((f, i) => (
                        <div key={`${f.name}-${i}`} className="flex items-center justify-between p-2 bg-bg rounded-lg border border-border/50">
                          <span className="text-sm text-text-primary truncate">{f.name}</span>
                          <button
                            type="button"
                            onClick={() => setFiles((prev) => prev.filter((_, idx) => idx !== i))}
                            className="text-text-muted hover:text-danger shrink-0 ml-2"
                          >
                            <X size={14} />
                          </button>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
                <div className="flex items-center gap-3 pt-2">
                  <Button type="submit" disabled={submitting}>
                    {submitting ? 'Submitting...' : 'Submit Progress'}
                  </Button>
                  <Button variant="secondary" onClick={() => navigate(-1)}>Cancel</Button>
                </div>
              </form>
            </Card>
          )}
        </div>

        {/* Submission History */}
        <div>
          <Card>
            <h3 className="text-base font-semibold text-text-primary mb-3">Submission History</h3>
            <div className="space-y-3">
              {history.length === 0 ? (
                <p className="text-sm text-text-muted">No previous submissions.</p>
              ) : (
                history.map((s) => (
                  <div
                    key={s.progressId ?? s.submittedAt}
                    className="p-3 bg-bg rounded-lg border border-border/50"
                  >
                    <div className="flex items-center justify-between mb-1">
                      <span className="text-xs text-text-muted">
                        {s.submittedAt
                          ? typeof s.submittedAt === 'string'
                            ? s.submittedAt.slice(0, 10)
                            : s.submittedAt
                          : ''}
                      </span>
                      <Badge variant="orange">{s.completionPercentage ?? 0}%</Badge>
                    </div>
                    <p className="text-sm text-text-secondary">{s.notes || '(no notes)'}</p>
                    {s.blockers && (
                      <p className="text-xs text-text-muted mt-1">Blockers: {s.blockers}</p>
                    )}
                  </div>
                ))
              )}
            </div>

            {/* AI Analyze Button */}
            <div className="mt-4 pt-4 border-t border-border">
              <button
                onClick={handleAiAnalyze}
                disabled={aiLoading}
                className="w-full flex items-center justify-center gap-2.5 px-4 py-3 rounded-xl text-sm font-semibold transition-all duration-300 relative overflow-hidden group"
                style={{
                  background: 'linear-gradient(135deg, #6366f1 0%, #8b5cf6 50%, #a855f7 100%)',
                  color: 'white',
                  boxShadow: '0 4px 15px rgba(99, 102, 241, 0.3)',
                }}
              >
                <span className="absolute inset-0 bg-white/10 opacity-0 group-hover:opacity-100 transition-opacity duration-300" />
                {aiLoading ? (
                  <>
                    <Loader2 size={18} className="animate-spin" />
                    Analyzing with AI...
                  </>
                ) : (
                  <>
                    <Bot size={18} />
                    <span>AI Progress Analysis</span>
                    <span className="ml-auto text-xs bg-white/20 px-2 py-0.5 rounded-full">Agent 3</span>
                  </>
                )}
              </button>
              <p className="text-xs text-text-muted mt-2 text-center">
                Get AI-powered insights about your task progress
              </p>
            </div>
          </Card>
        </div>

        {/* AI Analysis Sidebar Panel */}
        {showAiPanel && (
          <div className="lg:col-span-2">
            <Card>
              <div className="flex items-center justify-between mb-4">
                <div className="flex items-center gap-2">
                  <div className="w-8 h-8 rounded-lg flex items-center justify-center" style={{ background: 'linear-gradient(135deg, #6366f1, #a855f7)' }}>
                    <Bot size={18} className="text-white" />
                  </div>
                  <div>
                    <h3 className="text-base font-semibold text-text-primary">AI Progress Analysis</h3>
                    <p className="text-xs text-text-muted">Powered by Llama 3.3 70B</p>
                  </div>
                </div>
                <button
                  onClick={() => { setShowAiPanel(false); setAiAnalysis(null); setAiError(null); }}
                  className="text-text-muted hover:text-text-primary transition-colors p-1 rounded-lg hover:bg-border/50"
                >
                  <X size={16} />
                </button>
              </div>

              {aiLoading && (
                <div className="flex flex-col items-center justify-center py-12 space-y-4">
                  <div className="relative">
                    <div className="w-16 h-16 rounded-full border-4 border-border border-t-purple-500 animate-spin" />
                    <Bot size={24} className="absolute inset-0 m-auto text-purple-400" />
                  </div>
                  <div className="text-center">
                    <p className="text-sm font-medium text-text-primary">Analyzing your progress...</p>
                    <p className="text-xs text-text-muted mt-1">Our AI is evaluating status, predicting completion & assessing risks</p>
                  </div>
                </div>
              )}

              {aiError && (
                <div className="p-4 rounded-xl bg-red-500/10 border border-red-500/20">
                  <div className="flex items-start gap-3">
                    <AlertTriangle size={18} className="text-red-400 shrink-0 mt-0.5" />
                    <div>
                      <p className="text-sm font-medium text-red-400">Analysis Failed</p>
                      <p className="text-xs text-text-muted mt-1">{aiError}</p>
                      <button
                        onClick={handleAiAnalyze}
                        className="mt-3 text-xs font-medium text-purple-400 hover:text-purple-300 transition-colors"
                      >
                        Try again →
                      </button>
                    </div>
                  </div>
                </div>
              )}

              {aiAnalysis && !aiLoading && (
                <div className="space-y-4">
                  {/* Progress Summary */}
                  <div className={`p-4 rounded-xl border ${
                    aiAnalysis.progress_summary === 'On track' ? 'bg-emerald-500/10 border-emerald-500/20' :
                    aiAnalysis.progress_summary === 'At risk' ? 'bg-amber-500/10 border-amber-500/20' :
                    'bg-red-500/10 border-red-500/20'
                  }`}>
                    <div className="flex items-center gap-2 mb-2">
                      {getSummaryIcon(aiAnalysis.progress_summary)}
                      <span className={`text-sm font-semibold ${getSummaryColor(aiAnalysis.progress_summary)}`}>
                        {aiAnalysis.progress_summary}
                      </span>
                    </div>
                    <p className="text-xs text-text-muted">Task health assessment based on current progress, velocity, and blockers</p>
                  </div>

                  {/* Completion Prediction */}
                  {aiAnalysis.completion_prediction && (
                    <div className="p-4 rounded-xl bg-blue-500/10 border border-blue-500/20">
                      <div className="flex items-center gap-2 mb-2">
                        <Calendar size={16} className="text-blue-400" />
                        <span className="text-sm font-semibold text-blue-400">Predicted Completion</span>
                      </div>
                      <p className="text-lg font-bold text-text-primary">{aiAnalysis.completion_prediction}</p>
                    </div>
                  )}

                  {/* Risk Level */}
                  <div className={`p-4 rounded-xl border ${getRiskBg(aiAnalysis.risk_level)}`}>
                    <div className="flex items-center gap-2 mb-2">
                      <Shield size={16} className={getRiskColor(aiAnalysis.risk_level)} />
                      <span className={`text-sm font-semibold ${getRiskColor(aiAnalysis.risk_level)}`}>
                        Risk Level: {aiAnalysis.risk_level?.charAt(0).toUpperCase() + aiAnalysis.risk_level?.slice(1)}
                      </span>
                    </div>
                  </div>

                  {/* Blockers Analysis */}
                  {aiAnalysis.blockers_analysis && (
                    <div className="p-4 rounded-xl bg-bg border border-border">
                      <div className="flex items-center gap-2 mb-2">
                        <AlertTriangle size={16} className="text-amber-400" />
                        <span className="text-sm font-semibold text-text-primary">Blockers Analysis</span>
                      </div>
                      <p className="text-sm text-text-secondary leading-relaxed">{aiAnalysis.blockers_analysis}</p>
                    </div>
                  )}

                  {/* Recommendations */}
                  {aiAnalysis.recommendations?.length > 0 && (
                    <div className="p-4 rounded-xl bg-bg border border-border">
                      <div className="flex items-center gap-2 mb-3">
                        <Lightbulb size={16} className="text-purple-400" />
                        <span className="text-sm font-semibold text-text-primary">AI Recommendations</span>
                      </div>
                      <div className="space-y-2">
                        {aiAnalysis.recommendations.map((rec, idx) => (
                          <div key={idx} className="flex items-start gap-2.5 p-2.5 rounded-lg bg-purple-500/5 border border-purple-500/10">
                            <span className="w-5 h-5 rounded-full bg-purple-500/20 flex items-center justify-center shrink-0 mt-0.5">
                              <span className="text-xs font-bold text-purple-400">{idx + 1}</span>
                            </span>
                            <p className="text-sm text-text-secondary leading-relaxed">{rec}</p>
                          </div>
                        ))}
                      </div>
                    </div>
                  )}

                  {/* Re-analyze button */}
                  <button
                    onClick={handleAiAnalyze}
                    disabled={aiLoading}
                    className="w-full flex items-center justify-center gap-2 px-4 py-2.5 rounded-lg text-xs font-medium text-purple-400 bg-purple-500/10 border border-purple-500/20 hover:bg-purple-500/20 transition-colors"
                  >
                    <Bot size={14} />
                    Re-analyze Progress
                  </button>
                </div>
              )}
            </Card>
          </div>
        )}
      </div>
    </AppLayout>
  );
}
