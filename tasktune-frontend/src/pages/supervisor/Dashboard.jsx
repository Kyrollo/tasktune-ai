import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import AppLayout from '../../layouts/AppLayout';
import { Card, StatCard, Badge, ProgressBar } from '../../components/ui';
import { dashboardService } from '../../services/dashboardService';
import { taskService } from '../../services/taskService';
import { userService } from '../../services/userService';
import { Users, AlertTriangle, CheckCircle, Clock, Brain } from 'lucide-react';
import { BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer } from 'recharts';

const TASK_STATUS_MAP = { 0: 'Not Started', 1: 'In Progress', 2: 'In Review', 3: 'Completed' };
const TASK_PRIORITY_MAP = { 0: 'Low', 1: 'Medium', 2: 'High', 3: 'Critical' };

function mapTaskFromApi(t) {
  const statusByte = t.status ?? t.Status ?? 0;
  const priorityByte = t.priority ?? t.Priority ?? 1;
  const statusStr = TASK_STATUS_MAP[statusByte] ?? 'Not Started';
  const isOverdue = t.dueDate && new Date(t.dueDate) < new Date() && statusStr !== 'Completed';
  const displayStatus = isOverdue ? 'Overdue' : statusStr;
  const rawAssignee = t.assignedTo ?? t.AssignedTo;
  const assignedToUserId =
    t.assignedToUserId ?? t.AssignedToUserId ?? t.assigneeUserId ?? t.AssigneeUserId ??
    (rawAssignee && typeof rawAssignee === 'object' ? (rawAssignee.userId ?? rawAssignee.UserId) : null) ??
    (typeof rawAssignee === 'number' || (typeof rawAssignee === 'string' && rawAssignee && !rawAssignee.includes('@'))
      ? rawAssignee
      : null);
  return {
    id: t.taskId ?? t.TaskId ?? t.id,
    name: t.taskName ?? t.TaskName ?? t.name,
    projectId: t.projectId ?? t.ProjectId,
    projectName: t.projectName ?? t.ProjectName ?? '',
    priority: TASK_PRIORITY_MAP[priorityByte] ?? 'Medium',
    status: displayStatus,
    statusByte,
    progress: t.completionPercentage ?? t.CompletionPercentage ?? t.progress ?? 0,
    dueDate: t.dueDate ? (typeof t.dueDate === 'string' ? t.dueDate.slice(0, 10) : t.dueDate) : '',
    assignedTo: typeof rawAssignee === 'object' && rawAssignee !== null ? rawAssignee : null,
    assignedToUserId,
  };
}

function getAssigneeDisplayName(task, usersMap) {
  const a = task.assignedTo;
  if (a && typeof a === 'object') {
    const n = a.fullName ?? a.FullName ?? a.name ?? (a.firstName || a.lastName ? `${a.firstName ?? ''} ${a.lastName ?? ''}`.trim() : null);
    if (n) return n;
  }
  const id = task.assignedToUserId;
  if (id != null && usersMap[id] != null && usersMap[id] !== '') return usersMap[id];
  return '—';
}

export default function SupervisorDashboard() {
  const navigate = useNavigate();
  const [stats, setStats] = useState(null);
  const [teamMembers, setTeamMembers] = useState([]);
  const [usersMap, setUsersMap] = useState({});
  const [tasks, setTasks] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchData = async () => {
      try {
        const [dashboardRes, tasksRes] = await Promise.all([
          dashboardService.getSupervisorDashboard(),
          taskService.getTasks(1, 100),
        ]);
        const data = dashboardRes.data ?? dashboardRes;
        setStats(data);

        const taskItems = tasksRes.data?.items ?? tasksRes.data?.Items ?? [];
        const reviewQueue = data?.reviewQueue ?? data?.ReviewQueue ?? [];
        const combinedTasks = [...reviewQueue, ...taskItems];
        const uniqueById = [...new Map(combinedTasks.map(t => [t.taskId ?? t.TaskId ?? t.id, t])).values()];
        setTasks(uniqueById.map(mapTaskFromApi));

        let usersRes = { data: { items: [] } };
        try {
          usersRes = await userService.getUsers(1, 50);
        } catch {
          console.warn('Could not load users for supervisor dashboard');
        }
        const userItems = usersRes.data?.items ?? usersRes.data?.Items ?? [];
        const nameById = {};
        (Array.isArray(userItems) ? userItems : []).forEach(u => {
          const id = u.userId ?? u.UserId ?? u.id;
          if (id == null) return;
          const nm = u.fullName ?? u.FullName ?? u.name ?? (u.firstName || u.lastName ? `${u.firstName ?? ''} ${u.lastName ?? ''}`.trim() : null);
          if (nm) nameById[id] = nm;
        });
        if (userItems.length > 0) {
          const employees = userItems.filter(u => {
            const roleName = (u.roleName ?? u.RoleName ?? '').toLowerCase();
            const role = u.role ?? u.Role;
            return roleName === 'employee' || role === 0;
          });
          setTeamMembers(employees.length > 0 ? employees : userItems);
          setUsersMap(nameById);
        } else {
          const seen = new Map();
          uniqueById.forEach(t => {
            const a = t.assignedTo ?? t.AssignedTo;
            const uid = t.assignedToUserId ?? t.AssignedToUserId ?? (a && typeof a === 'object' ? (a.userId ?? a.UserId) : null) ??
              (typeof a === 'number' || (typeof a === 'string' && a && !a.includes('@')) ? a : null);
            if (uid == null) return;
            let displayName = null;
            if (a && typeof a === 'object') {
              displayName = a.fullName ?? a.FullName ?? a.name ?? (a.firstName || a.lastName ? `${a.firstName ?? ''} ${a.lastName ?? ''}`.trim() : null);
            }
            if (!displayName && nameById[uid]) displayName = nameById[uid];
            if (!seen.has(uid)) seen.set(uid, { id: uid, name: displayName || `User ${uid}`, position: (a && typeof a === 'object' ? (a.roleName ?? a.RoleName) : null) || 'Employee' });
          });
          const list = Array.from(seen.values());
          setTeamMembers(list);
          const fallbackMap = { ...nameById };
          list.forEach(m => {
            if (m.name && m.id != null) fallbackMap[m.id] = m.name;
          });
          setUsersMap(fallbackMap);
        }
      } catch (err) {
        console.error('Failed to load dashboard:', err);
      } finally {
        setLoading(false);
      }
    };
    fetchData();
  }, []);

  const totalTasks = stats?.totalTeamTasks ?? stats?.totalTasks ?? 0;
  const pendingReviews = stats?.pendingReviews ?? stats?.PendingReviews ?? 0;
  const statsTeamCount = stats?.teamMembers ?? stats?.TeamMembers;
  const teamMembersCount = Math.max(
    Number(statsTeamCount) || 0,
    teamMembers.length,
  );
  const overdueCount = stats?.overdueTasks ?? stats?.OverdueTasks ?? 0;

  const inReview = tasks.filter(t => t.status === 'In Review' || t.statusByte === 2);
  const overdueTasks = tasks.filter(t => t.status === 'Overdue' || (new Date(t.dueDate) < new Date() && t.status !== 'Completed'));
  const teamTasks = tasks.filter(t => t.status !== 'Completed');
  const workloadData = Array.isArray(stats?.weeklyWorkload) ? stats.weeklyWorkload
    : Array.isArray(stats?.WeeklyWorkload) ? stats.WeeklyWorkload : [];

  if (loading) {
    return (
      <AppLayout title="Supervisor Dashboard">
        <div className="flex items-center justify-center h-64">
          <div className="text-text-secondary">Loading...</div>
        </div>
      </AppLayout>
    );
  }

  return (
    <AppLayout title="Supervisor Dashboard">
      {/* Stats */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
        <StatCard label="Team Members" value={teamMembersCount} icon={Users} />
        <StatCard label="Total Tasks" value={totalTasks} icon={Clock} />
        <StatCard label="Pending Reviews" value={pendingReviews} icon={CheckCircle} trend="Needs attention" trendUp={false} />
        <StatCard label="Overdue" value={overdueTasks.length || overdueCount} icon={AlertTriangle} trend={overdueTasks.length > 0 || overdueCount > 0 ? 'Action required' : 'All on track'} trendUp={overdueTasks.length === 0 && overdueCount === 0} />
      </div>

      <div className="grid lg:grid-cols-3 gap-6">
        <div className="lg:col-span-2 space-y-4">
          {/* Tasks Needing Review */}
          <Card>
            <h3 className="text-lg font-semibold text-white mb-4">Tasks Needing Review</h3>
            {inReview.length === 0 ? (
              <p className="text-sm text-text-muted py-4 text-center">No tasks pending review.</p>
            ) : (
              <div className="space-y-3">
                {inReview.map(task => {
                  const assigneeName = getAssigneeDisplayName(task, usersMap);
                  return (
                    <div key={task.id} onClick={() => navigate(`/supervisor/review/${task.id}`)} className="flex items-center justify-between p-4 bg-bg rounded-lg border border-border/50 hover:border-orange/30 cursor-pointer transition-colors">
                      <div>
                        <p className="text-sm font-medium text-white">{task.name}</p>
                        <p className="text-xs text-text-muted">Submitted by {assigneeName} • Due {task.dueDate}</p>
                      </div>
                      <Badge variant="info">In Review</Badge>
                    </div>
                  );
                })}
              </div>
            )}
          </Card>

          {/* Team Task Overview */}
          <Card>
            <h3 className="text-lg font-semibold text-white mb-4">Team Task Progress</h3>
            <div className="space-y-3">
              {teamTasks.filter(t => t.status !== 'Completed').map(task => {
                const assigneeName = getAssigneeDisplayName(task, usersMap);
                return (
                  <div key={task.id} onClick={() => navigate(`/tasks/${task.id}`)} className="p-3 bg-bg rounded-lg border border-border/50 hover:border-orange/30 cursor-pointer transition-colors">
                    <div className="flex items-center justify-between mb-2">
                      <p className="text-sm font-medium text-white">{task.name}</p>
                      <Badge variant={task.priority === 'High' || task.priority === 'Critical' ? 'danger' : task.priority === 'Medium' ? 'warning' : 'default'}>{task.priority}</Badge>
                    </div>
                    <div className="flex items-center gap-3">
                      <div className="flex-1"><ProgressBar value={task.progress} size="sm" /></div>
                      <span className="text-xs text-text-muted">{task.progress}%</span>
                      <span className="text-xs text-text-muted">{assigneeName}</span>
                    </div>
                  </div>
                );
              })}
              {teamTasks.filter(t => t.status !== 'Completed').length === 0 && (
                <p className="text-sm text-text-muted py-4 text-center">No active tasks.</p>
              )}
            </div>
          </Card>

          {/* Workload Chart */}
          <Card>
            <h3 className="text-lg font-semibold text-white mb-4">Team Workload</h3>
            <ResponsiveContainer width="100%" height={220}>
              <BarChart data={workloadData}>
                <XAxis dataKey="day" stroke="#6B7280" fontSize={12} />
                <YAxis stroke="#6B7280" fontSize={12} />
                <Tooltip contentStyle={{ background: '#111', border: '1px solid #1E1E1E', borderRadius: 8 }} />
                <Bar dataKey="tasks" fill="#F97316" radius={[4, 4, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </Card>
        </div>

        {/* Right sidebar */}
        <div className="space-y-4">
          {/* Alerts */}
          <Card>
            <h3 className="text-base font-semibold text-white mb-3">Alerts</h3>
            <div className="space-y-2">
              {overdueTasks.map(t => (
                <div key={t.id} onClick={() => navigate(`/tasks/${t.id}`)} className="p-3 bg-danger/5 border border-danger/20 rounded-lg cursor-pointer hover:border-danger/40 transition-colors">
                  <p className="text-sm text-danger font-medium">Overdue: {t.name}</p>
                  <p className="text-xs text-text-muted">Due {t.dueDate}</p>
                </div>
              ))}
              {overdueTasks.length === 0 && overdueCount > 0 && (
                <div className="p-3 bg-danger/5 border border-danger/20 rounded-lg">
                  <p className="text-sm text-danger">{overdueCount} task(s) overdue. View tasks for details.</p>
                </div>
              )}
              {overdueTasks.length === 0 && overdueCount === 0 && (
                <div className="p-3 bg-success/5 border border-success/20 rounded-lg">
                  <p className="text-sm text-success">All tasks are on track!</p>
                </div>
              )}
            </div>
          </Card>

          {/* AI Summary */}
          <Card>
            <div className="flex items-center gap-2 mb-3">
              <Brain size={18} className="text-orange" />
              <h3 className="text-base font-semibold text-white">AI Performance Summary</h3>
            </div>
            <div className="p-3 bg-orange/5 border border-orange/20 rounded-lg">
              <p className="text-sm text-text-secondary">AI performance analysis will appear here when an AI model is connected.</p>
            </div>
          </Card>

          {/* Team Members */}
          <Card>
            <h3 className="text-base font-semibold text-white mb-3">Team Members</h3>
            <div className="space-y-2">
              {teamMembers.slice(0, 5).map(m => {
                const id = m.userId ?? m.UserId ?? m.id;
                const name = m.fullName ?? m.FullName ?? m.name ?? (m.firstName && m.lastName ? `${m.firstName} ${m.lastName}` : 'Unknown');
                const position = m.positionTitle ?? m.PositionTitle ?? m.position ?? 'Employee';
                return (
                  <div key={id} onClick={() => navigate(`/profile/${id}`)} className="flex items-center gap-3 p-2 rounded-lg hover:bg-surface-hover cursor-pointer">
                    <div className="w-8 h-8 rounded-full bg-orange/20 flex items-center justify-center text-orange text-sm font-medium">{name.charAt(0)}</div>
                    <div>
                      <p className="text-sm text-white">{name}</p>
                      <p className="text-xs text-text-muted">{position}</p>
                    </div>
                  </div>
                );
              })}
              {teamMembers.length === 0 && (
                <p className="text-sm text-text-muted py-2">No team members loaded.</p>
              )}
            </div>
          </Card>
        </div>
      </div>
    </AppLayout>
  );
}
