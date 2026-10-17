import { useState, useEffect } from 'react';
import AppLayout from '../../layouts/AppLayout';
import { Card, Badge } from '../../components/ui';
import { evaluationService } from '../../services/evaluationService';
import { useAuth } from '../../contexts/AuthContext';
import { Star, Brain, ClipboardCheck, CheckCircle2, Clock3 } from 'lucide-react';
import { LineChart, Line, XAxis, YAxis, Tooltip, ResponsiveContainer, Legend } from 'recharts';

const scoreBadge = (score) => {
  if (score >= 90) return <Badge variant="success">{score}</Badge>;
  if (score >= 75) return <Badge variant="orange">{score}</Badge>;
  if (score >= 60) return <Badge variant="warning">{score}</Badge>;
  return <Badge variant="danger">{score}</Badge>;
};

const MONTH_NAMES = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

function buildPerformanceTrend(evaluations) {
  if (!evaluations?.length) return [];
  const byMonth = {};
  for (const e of evaluations) {
    const d = e.date ? (typeof e.date === 'string' ? e.date.slice(0, 7) : '') : '';
    if (!d) continue;
    const [y, m] = d.split('-').map(Number);
    const key = `${y}-${String(m).padStart(2, '0')}`;
    if (!byMonth[key]) byMonth[key] = { key, overall: [], quality: [], timeliness: [] };
    byMonth[key].overall.push(e.overall ?? 0);
    byMonth[key].quality.push(e.quality ?? 0);
    byMonth[key].timeliness.push(e.timeliness ?? 0);
  }
  return Object.values(byMonth)
    .map(({ key, overall, quality, timeliness }) => {
      const [y, m] = key.split('-').map(Number);
      return {
        key,
        month: `${MONTH_NAMES[m - 1] || ''} ${y}`.trim() || key,
        overall: Math.round(overall.reduce((a, b) => a + b, 0) / overall.length) || 0,
        quality: Math.round(quality.reduce((a, b) => a + b, 0) / quality.length) || 0,
        timeliness: Math.round(timeliness.reduce((a, b) => a + b, 0) / timeliness.length) || 0,
      };
    })
    .sort((a, b) => a.key.localeCompare(b.key));
}

export default function MyEvaluations() {
  const { user } = useAuth();
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchData = async () => {
      try {
        const { data } = await evaluationService.getMyEvaluations();
        const rawItems = Array.isArray(data) ? data : [];
        setItems(rawItems.map((item) => ({
          id: item.evaluationId ?? item.id ?? `task-${item.taskId}`,
          type: item.type ?? 'employee',
          reviewState: item.reviewState ?? 'Reviewed',
          needsReview: Boolean(item.needsReview),
          taskId: item.taskId,
          taskName: item.taskName || `Task #${item.taskId}`,
          employeeName: item.employeeName || user?.name || 'Employee',
          departmentName: item.departmentName || '',
          date: item.createdAt ? (typeof item.createdAt === 'string' ? item.createdAt.slice(0, 10) : item.createdAt) : '',
          overall: item.overallScore ?? item.overall ?? 0,
          quality: item.qualityScore ?? item.quality ?? 0,
          timeliness: item.timelinessScore ?? item.timeliness ?? 0,
          collaboration: item.collaborationScore ?? item.collaboration ?? 0,
          feedback: item.manualFeedback ?? item.feedback ?? '',
          aiReport: item.aiGeneratedFeedback ?? item.aiReport ?? '',
          completionPercentage: item.completionPercentage ?? 0,
        })));
      } catch (err) {
        console.error('Failed to load evaluations:', err);
      } finally {
        setLoading(false);
      }
    };
    fetchData();
  }, [user?.name]);

  const reviewerMode = user?.role === 'CEO' || user?.role === 'Supervisor' || user?.role === 'Manager' || user?.role === 'Admin';
  const performanceTrend = buildPerformanceTrend(items.filter((item) => item.type === 'employee'));
  const pendingReviews = items.filter((item) => item.needsReview);
  const reviewedItems = items.filter((item) => !item.needsReview);

  if (loading) return (
    <AppLayout title="My Evaluations">
      <div className="flex items-center justify-center h-64"><div className="text-text-secondary">Loading...</div></div>
    </AppLayout>
  );

  return (
    <AppLayout title="My Evaluations">
      {!reviewerMode && (
        <Card className="mb-6">
          <h3 className="text-lg font-semibold text-white mb-4">Performance Trend</h3>
          {performanceTrend.length === 0 ? (
            <p className="text-sm text-text-muted py-8 text-center">No performance data available yet</p>
          ) : (
            <ResponsiveContainer width="100%" height={250}>
              <LineChart data={performanceTrend}>
                <XAxis dataKey="month" stroke="#6B7280" fontSize={12} />
                <YAxis stroke="#6B7280" fontSize={12} domain={[0, 100]} />
                <Tooltip contentStyle={{ background: '#111', border: '1px solid #1E1E1E', borderRadius: 8 }} />
                <Legend />
                <Line type="monotone" dataKey="overall" stroke="#F97316" strokeWidth={2} dot={{ fill: '#F97316' }} />
                <Line type="monotone" dataKey="quality" stroke="#22C55E" strokeWidth={2} dot={{ fill: '#22C55E' }} />
                <Line type="monotone" dataKey="timeliness" stroke="#3B82F6" strokeWidth={2} dot={{ fill: '#3B82F6' }} />
              </LineChart>
            </ResponsiveContainer>
          )}
        </Card>
      )}

      {reviewerMode && (
        <div className="grid md:grid-cols-3 gap-4 mb-6">
          <Card>
            <div className="flex items-center gap-3">
              <Clock3 size={18} className="text-warning" />
              <div>
                <p className="text-sm text-text-muted">Pending Review</p>
                <p className="text-2xl font-bold text-white">{pendingReviews.length}</p>
              </div>
            </div>
          </Card>
          <Card>
            <div className="flex items-center gap-3">
              <CheckCircle2 size={18} className="text-success" />
              <div>
                <p className="text-sm text-text-muted">Reviewed</p>
                <p className="text-2xl font-bold text-white">{reviewedItems.length}</p>
              </div>
            </div>
          </Card>
          <Card>
            <div className="flex items-center gap-3">
              <ClipboardCheck size={18} className="text-orange" />
              <div>
                <p className="text-sm text-text-muted">Total In Scope</p>
                <p className="text-2xl font-bold text-white">{items.length}</p>
              </div>
            </div>
          </Card>
        </div>
      )}

      <div className="space-y-4">
        {items.map((ev) => (
          <Card key={ev.id}>
            <div className="flex items-start justify-between mb-4">
              <div>
                <h3 className="text-base font-semibold text-white">{ev.taskName}</h3>
                <p className="text-sm text-text-muted">
                  {reviewerMode ? `${ev.employeeName}${ev.departmentName ? ` • ${ev.departmentName}` : ''}` : `Evaluated on ${ev.date}`}
                </p>
              </div>
              <div className="flex items-center gap-2">
                {reviewerMode ? (
                  <Badge variant={ev.needsReview ? 'warning' : 'success'}>{ev.reviewState}</Badge>
                ) : (
                  <>
                    <Star size={16} className="text-orange" />
                    <span className="text-lg font-bold text-orange">{ev.overall}</span>
                  </>
                )}
              </div>
            </div>

            {reviewerMode && ev.needsReview ? (
              <div className="grid md:grid-cols-2 gap-4">
                <div className="p-3 bg-bg rounded-lg border border-border/50">
                  <p className="text-xs text-text-muted mb-1">Completion</p>
                  <p className="text-sm text-white">{ev.completionPercentage}% complete</p>
                </div>
                <div className="p-3 bg-bg rounded-lg border border-border/50">
                  <p className="text-xs text-text-muted mb-1">Review Status</p>
                  <p className="text-sm text-warning">Awaiting your evaluation</p>
                </div>
              </div>
            ) : (
              <>
                <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-4">
                  <div className="p-3 bg-bg rounded-lg text-center">
                    <p className="text-xs text-text-muted mb-1">Overall</p>
                    {scoreBadge(ev.overall)}
                  </div>
                  <div className="p-3 bg-bg rounded-lg text-center">
                    <p className="text-xs text-text-muted mb-1">Quality</p>
                    {scoreBadge(ev.quality)}
                  </div>
                  <div className="p-3 bg-bg rounded-lg text-center">
                    <p className="text-xs text-text-muted mb-1">Timeliness</p>
                    {scoreBadge(ev.timeliness)}
                  </div>
                  <div className="p-3 bg-bg rounded-lg text-center">
                    <p className="text-xs text-text-muted mb-1">Collaboration</p>
                    {scoreBadge(ev.collaboration)}
                  </div>
                </div>

                <div className="p-3 bg-bg rounded-lg border border-border/50 mb-3">
                  <p className="text-xs text-text-muted mb-1">{reviewerMode ? 'Your Feedback' : 'Supervisor Feedback'}</p>
                  <p className="text-sm text-text-secondary">{ev.feedback || 'No manual feedback recorded.'}</p>
                </div>

                <div className="p-3 bg-orange/5 border border-orange/20 rounded-lg">
                  <div className="flex items-center gap-2 mb-1">
                    <Brain size={14} className="text-orange" />
                    <p className="text-xs text-orange font-medium">AI-Generated Report</p>
                  </div>
                  <p className="text-sm text-text-secondary">{ev.aiReport || 'No AI report recorded yet.'}</p>
                </div>
              </>
            )}
          </Card>
        ))}

        {items.length === 0 && (
          <Card>
            <div className="text-center py-8">
              <Star size={40} className="text-text-muted mx-auto mb-3" />
              <p className="text-white font-medium">{reviewerMode ? 'No Reviews In Scope' : 'No Evaluations Yet'}</p>
              <p className="text-sm text-text-secondary mt-1">
                {reviewerMode
                  ? 'Tasks that need your review or were already reviewed will appear here.'
                  : 'Your evaluations will appear here once your work is reviewed.'}
              </p>
            </div>
          </Card>
        )}
      </div>
    </AppLayout>
  );
}
