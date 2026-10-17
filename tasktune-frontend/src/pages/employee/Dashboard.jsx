import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import AppLayout from '../../layouts/AppLayout';
import { Card, StatCard, Badge, ProgressBar } from '../../components/ui';
import { dashboardService } from '../../services/dashboardService';
import { taskService } from '../../services/taskService';
import { CheckCircle, Clock, AlertTriangle, Brain } from 'lucide-react';
import { BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer, PieChart, Pie, Cell } from 'recharts';

const TASK_STATUS_MAP = { 0: 'Not Started', 1: 'In Progress', 2: 'Completed', 3: 'In Review' };
const TASK_PRIORITY_MAP = { 0: 'Low', 1: 'Medium', 2: 'High', 3: 'Critical' };
const STATUS_CHART_COLORS = { 'Not Started': '#6B7280', 'In Progress': '#F97316', 'In Review': '#3B82F6', 'Completed': '#22C55E', 'Overdue': '#EF4444' };

const statusBadge = (s) => {
  const map = { 'Completed': 'success', 'In Progress': 'orange', 'In Review': 'info', 'Not Started': 'default', 'Overdue': 'danger' };
  return <Badge variant={map[s] || 'default'}>{s}</Badge>;
};

const priorityBadge = (p) => {
  const map = { High: 'danger', Medium: 'warning', Low: 'default', Critical: 'danger' };
  return <Badge variant={map[p] || 'default'}>{p}</Badge>;
};

function mapTaskFromApi(t) {
  const statusStr = TASK_STATUS_MAP[t.status] ?? 'Not Started';
  const isOverdue = t.dueDate && new Date(t.dueDate) < new Date() && statusStr !== 'Completed';
  const displayStatus = isOverdue ? 'Overdue' : statusStr;
  return {
    id: t.taskId ?? t.id,
    name: t.taskName ?? t.name,
    projectId: t.projectId,
    projectName: t.projectName ?? t.project?.name ?? '',
    priority: TASK_PRIORITY_MAP[t.priority] ?? 'Medium',
    status: displayStatus,
    progress: t.completionPercentage ?? t.progress ?? 0,
    dueDate: t.dueDate ? (typeof t.dueDate === 'string' ? t.dueDate.slice(0, 10) : t.dueDate) : '',
  };
}

export default function EmployeeDashboard() {
  const navigate = useNavigate();
  const [stats, setStats] = useState(null);
  const [tasks, setTasks] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchData = async () => {
      try {
        const [dashboardRes, tasksRes] = await Promise.all([
          dashboardService.getEmployeeDashboard(),
          taskService.getTasks(1, 100),
        ]);
        setStats(dashboardRes.data);
        const items = tasksRes.data?.items ?? tasksRes.data?.Items ?? [];
        setTasks(items.map(mapTaskFromApi));
      } catch (err) {
        console.error('Failed to load dashboard:', err);
      } finally {
        setLoading(false);
      }
    };
    fetchData();
  }, []);

  const completionTrendData = Array.isArray(stats?.completionTrend) ? stats.completionTrend : [];
  const statusDistributionData = (Array.isArray(stats?.taskStatusDistribution) && stats.taskStatusDistribution.length > 0)
    ? stats.taskStatusDistribution
    : Object.entries(
        tasks.reduce((acc, t) => { acc[t.status] = (acc[t.status] ?? 0) + 1; return acc; }, {})
      ).map(([name, value]) => ({ name, value, color: STATUS_CHART_COLORS[name] ?? '#6B7280' }));

  if (loading) {
    return (
      <AppLayout title="Employee Dashboard">
        <div className="flex items-center justify-center h-64">
          <div className="text-text-secondary">Loading...</div>
        </div>
      </AppLayout>
    );
  }

  return (
    <AppLayout title="Employee Dashboard">
      {/* Stats */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
        <StatCard label="Total Tasks" value={stats?.totalTasks ?? 0} icon={CheckCircle} />
        <StatCard label="In Progress" value={stats?.activeTasks ?? 0} icon={Clock} trend="+2 this week" trendUp />
        <StatCard label="Completed" value={stats?.completedTasks ?? 0} icon={CheckCircle} trend={stats?.totalTasks ? `${Math.round((stats.completedTasks ?? 0) / stats.totalTasks * 100)}% rate` : '—'} trendUp />
        <StatCard label="Overdue" value={stats?.overdueTasks ?? 0} icon={AlertTriangle} trend={(stats?.overdueTasks ?? 0) > 0 ? 'Needs attention' : 'All clear'} trendUp={(stats?.overdueTasks ?? 0) === 0} />
      </div>

      <div className="grid lg:grid-cols-3 gap-6">
        {/* Task List */}
        <div className="lg:col-span-2 space-y-4">
          <Card>
            <h3 className="text-lg font-semibold text-white mb-4">My Tasks</h3>
            <div className="space-y-3">
              {tasks.map(task => (
                <div
                  key={task.id}
                  onClick={() => navigate(`/tasks/${task.id}`)}
                  className="flex items-center justify-between p-4 bg-bg rounded-lg border border-border/50 hover:border-orange/30 cursor-pointer transition-colors"
                >
                  <div className="min-w-0 flex-1">
                    <div className="flex items-center gap-3 mb-1">
                      <p className="text-sm font-medium text-white truncate">{task.name}</p>
                      {priorityBadge(task.priority)}
                    </div>
                    <p className="text-xs text-text-muted">{task.projectName || '—'} • Due {task.dueDate || '—'}</p>
                    <div className="mt-2 max-w-xs">
                      <ProgressBar value={task.progress} size="sm" />
                    </div>
                  </div>
                  <div className="ml-4 shrink-0">{statusBadge(task.status)}</div>
                </div>
              ))}
            </div>
          </Card>

          {/* Completion Chart */}
          <Card>
            <h3 className="text-lg font-semibold text-white mb-4">Task Completion Trend</h3>
            <ResponsiveContainer width="100%" height={220}>
              <BarChart data={completionTrendData}>
                <XAxis dataKey="month" stroke="#6B7280" fontSize={12} />
                <YAxis stroke="#6B7280" fontSize={12} />
                <Tooltip contentStyle={{ background: '#111', border: '1px solid #1E1E1E', borderRadius: 8 }} />
                <Bar dataKey="completed" fill="#F97316" radius={[4, 4, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </Card>
        </div>

        {/* Right Column */}
        <div className="space-y-4">
          {/* AI Guidance */}
          <Card>
            <div className="flex items-center gap-2 mb-3">
              <Brain size={18} className="text-orange" />
              <h3 className="text-base font-semibold text-white">AI Guidance</h3>
            </div>
            <div className="p-3 bg-orange/5 border border-orange/20 rounded-lg">
              <p className="text-sm text-text-secondary">AI guidance will appear here when an AI model is connected to your account.</p>
            </div>
          </Card>

          {/* Status Distribution */}
          <Card>
            <h3 className="text-base font-semibold text-white mb-3">Task Distribution</h3>
            <ResponsiveContainer width="100%" height={180}>
              <PieChart>
                <Pie data={statusDistributionData} dataKey="value" cx="50%" cy="50%" innerRadius={40} outerRadius={70} paddingAngle={3}>
                  {statusDistributionData.map((entry, i) => (
                    <Cell key={i} fill={entry.color} />
                  ))}
                </Pie>
                <Tooltip contentStyle={{ background: '#111', border: '1px solid #1E1E1E', borderRadius: 8 }} />
              </PieChart>
            </ResponsiveContainer>
            <div className="flex flex-wrap gap-3 mt-2">
              {statusDistributionData.map(d => (
                <div key={d.name} className="flex items-center gap-1.5">
                  <div className="w-2.5 h-2.5 rounded-full" style={{ background: d.color }} />
                  <span className="text-xs text-text-secondary">{d.name}</span>
                </div>
              ))}
            </div>
          </Card>

          {/* Deadlines */}
          <Card>
            <h3 className="text-base font-semibold text-white mb-3">Upcoming Deadlines</h3>
            <div className="space-y-2">
              {tasks
                .filter(t => t.status !== 'Completed')
                .sort((a, b) => new Date(a.dueDate || 0) - new Date(b.dueDate || 0))
                .slice(0, 4)
                .map(t => {
                  const days = t.dueDate ? Math.ceil((new Date(t.dueDate) - new Date()) / 86400000) : 0;
                  return (
                    <div key={t.id} className="flex items-center justify-between p-2 rounded-lg hover:bg-surface-hover cursor-pointer" onClick={() => navigate(`/tasks/${t.id}`)}>
                      <p className="text-sm text-white truncate">{t.name}</p>
                      <span className={`text-xs font-medium ${days < 0 ? 'text-danger' : days < 7 ? 'text-warning' : 'text-text-secondary'}`}>
                        {days < 0 ? `${Math.abs(days)}d overdue` : `${days}d left`}
                      </span>
                    </div>
                  );
                })}
            </div>
          </Card>
        </div>
      </div>
    </AppLayout>
  );
}
