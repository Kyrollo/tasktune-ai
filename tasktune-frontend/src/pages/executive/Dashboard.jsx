import { useState, useEffect } from 'react';
import AppLayout from '../../layouts/AppLayout';
import { Card, StatCard, Badge } from '../../components/ui';
import { dashboardService } from '../../services/dashboardService';
import { projectService } from '../../services/projectService';
import { userService } from '../../services/userService';
import { TrendingUp, FolderKanban, AlertTriangle, Users, Brain } from 'lucide-react';
import { BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer, PieChart, Pie, Cell, LineChart, Line, Legend } from 'recharts';

const STATUS_MAP = { 0: 'Planning', 1: 'Active', 2: 'Completed' };
const ROLE_NAMES = { 0: 'Employee', 1: 'Supervisor', 2: 'Manager', 3: 'HR', 4: 'CEO', 5: 'Admin' };

export default function ExecutiveDashboard() {
  const [stats, setStats] = useState(null);
  const [projects, setProjects] = useState([]);
  const [usersMap, setUsersMap] = useState({});
  const [deptPerformanceData, setDeptPerformanceData] = useState([]);
  const [topPerformers, setTopPerformers] = useState([]);
  const [performanceTrendData, setPerformanceTrendData] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchData = async () => {
      try {
        const [dashboardRes, projectsRes] = await Promise.all([
          dashboardService.getExecutiveDashboard(),
          projectService.getProjects(1, 200),
        ]);

        // Fetch users
        let allUsers = [];
        try {
          const usersRes = await userService.getUsers(1, 200);
          allUsers = usersRes?.data?.items ?? usersRes?.data?.Items ?? [];
        } catch {
          console.warn('Could not load users for dashboard');
        }

        const d = dashboardRes?.data ?? {};
        setStats(d);

        // ─── Build Projects ───
        const projItems = (projectsRes?.data?.items ?? projectsRes?.data?.Items ?? []).map(p => ({
          id: p.projectId ?? p.id,
          name: p.projectName ?? p.name,
          managerId: p.managerId,
          managerName: p.managerName ?? null,
          status: p.status,
          statusLabel: STATUS_MAP[p.status] ?? 'Active',
          taskCount: p.taskCount ?? 0,
          completedTaskCount: p.completedTaskCount ?? 0,
          completionPct: p.taskCount ? Math.round(((p.completedTaskCount ?? 0) / p.taskCount) * 100) : 0,
        }));
        setProjects(projItems);

        // ─── Users Map ───
        const map = {};
        allUsers.forEach(u => {
          const id = u.userId ?? u.id;
          map[id] = u.fullName ?? u.name ?? 'Unknown';
        });
        setUsersMap(map);

        setDeptPerformanceData((d.departments || []).map(item => ({
          dept: item.name ?? item.dept,
          score: item.score ?? 0,
        })));
        setTopPerformers((d.topPerformers || []).map(item => ({
          userId: item.userId,
          fullName: item.fullName,
          department: item.department,
          role: ROLE_NAMES[item.role] || 'Employee',
          overallScore: item.overallScore,
        })));
        setPerformanceTrendData(d.performanceTrend || []);

      } catch (err) {
        console.error('Failed to load dashboard:', err);
      } finally {
        setLoading(false);
      }
    };
    fetchData();
  }, []);

  const totalTasks = stats?.totalTasks ?? 0;
  const completedTasks = stats?.completedTasks ?? 0;
  const overdueTasks = stats?.overdueTasks ?? 0;

  const projectStatusData = (() => {
    const activeP = stats?.activeProjects ?? 0;
    const completedP = stats?.completedProjects ?? 0;
    const totalP = stats?.totalProjects ?? 0;
    const planningP = Math.max(0, totalP - activeP - completedP);
    return [
      { name: 'Active', value: activeP, color: '#F97316' },
      { name: 'Planning', value: planningP, color: '#3B82F6' },
      { name: 'Completed', value: completedP, color: '#22C55E' },
    ];
  })();

  const riskAlerts = Array.isArray(stats?.riskAlerts) ? stats.riskAlerts : (overdueTasks > 0 ? [{ type: 'overdue', message: `${overdueTasks} overdue task${overdueTasks !== 1 ? 's' : ''}`, detail: 'Tasks past deadline' }] : []);

  if (loading) {
    return (
      <AppLayout title="Executive Dashboard">
        <div className="flex items-center justify-center h-64">
          <div className="text-text-secondary">Loading...</div>
        </div>
      </AppLayout>
    );
  }

  return (
    <AppLayout title="Executive Dashboard">
      {/* KPIs */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
        <StatCard label="Active Projects" value={stats?.activeProjects ?? 0} icon={FolderKanban} trend="+1 this month" trendUp />
        <StatCard label="Task Completion" value={totalTasks ? `${Math.round(completedTasks / totalTasks * 100)}%` : '0%'} icon={TrendingUp} trend={`${completedTasks} of ${totalTasks} tasks`} trendUp />
        <StatCard label="Total Users" value={stats?.totalUsers ?? 0} icon={Users} trend="Above target" trendUp />
        <StatCard label="Overdue Tasks" value={overdueTasks} icon={AlertTriangle} trend={overdueTasks > 0 ? 'Needs attention' : 'All clear'} trendUp={overdueTasks === 0} />
      </div>

      <div className="grid lg:grid-cols-3 gap-6">
        <div className="lg:col-span-2 space-y-4">
          {/* Dept Performance */}
          <Card>
            <h3 className="text-lg font-semibold text-white mb-4">Department Performance</h3>
            {deptPerformanceData.length > 0 ? (
              <ResponsiveContainer width="100%" height={250}>
                <BarChart data={deptPerformanceData}>
                  <XAxis dataKey="dept" stroke="#6B7280" fontSize={12} />
                  <YAxis stroke="#6B7280" fontSize={12} domain={[0, 100]} />
                  <Tooltip contentStyle={{ background: '#111', border: '1px solid #1E1E1E', borderRadius: 8 }} />
                  <Bar dataKey="score" fill="#F97316" radius={[4, 4, 0, 0]} />
                </BarChart>
              </ResponsiveContainer>
            ) : (
              <div className="h-[250px] flex items-center justify-center text-text-muted text-sm">No data available</div>
            )}
          </Card>

          {/* Trend Lines */}
          <Card>
            <h3 className="text-lg font-semibold text-white mb-4">Performance Trend</h3>
            {performanceTrendData.length > 0 ? (
              <ResponsiveContainer width="100%" height={250}>
                <LineChart data={performanceTrendData}>
                  <XAxis dataKey="month" stroke="#6B7280" fontSize={12} />
                  <YAxis stroke="#6B7280" fontSize={12} domain={[40, 100]} />
                  <Tooltip contentStyle={{ background: '#111', border: '1px solid #1E1E1E', borderRadius: 8 }} />
                  <Legend />
                  <Line type="monotone" dataKey="overall" stroke="#F97316" strokeWidth={2} name="Overall" />
                  <Line type="monotone" dataKey="quality" stroke="#22C55E" strokeWidth={2} name="Quality" />
                  <Line type="monotone" dataKey="timeliness" stroke="#3B82F6" strokeWidth={2} name="Timeliness" />
                </LineChart>
              </ResponsiveContainer>
            ) : (
              <div className="h-[250px] flex items-center justify-center text-text-muted text-sm">No data available</div>
            )}
          </Card>

          {/* Project Summary */}
          <Card>
            <h3 className="text-lg font-semibold text-white mb-4">Project Status</h3>
            <div className="space-y-3">
              {projects.length === 0 ? (
                <div className="text-text-muted text-sm py-4">No projects available</div>
              ) : (
                projects.map(p => {
                  const managerName = p.managerName ?? usersMap[p.managerId] ?? '—';
                  const statusVar = { Active: 'orange', Planning: 'info', Completed: 'success' };
                  return (
                    <div key={p.id} className="flex items-center justify-between p-4 bg-bg rounded-lg border border-border/50">
                      <div>
                        <p className="text-sm font-medium text-white">{p.name}</p>
                        <p className="text-xs text-text-muted">{managerName} • {p.taskCount} tasks</p>
                      </div>
                      <div className="flex items-center gap-3">
                        <span className="text-sm text-text-secondary">{p.completionPct}%</span>
                        <Badge variant={statusVar[p.statusLabel] ?? 'info'}>{p.statusLabel}</Badge>
                      </div>
                    </div>
                  );
                })
              )}
            </div>
          </Card>
        </div>

        {/* Right sidebar */}
        <div className="space-y-4">
          {/* Project Distribution */}
          <Card>
            <h3 className="text-base font-semibold text-white mb-3">Project Distribution</h3>
            <ResponsiveContainer width="100%" height={180}>
              <PieChart>
                <Pie data={projectStatusData} dataKey="value" cx="50%" cy="50%" innerRadius={40} outerRadius={70} paddingAngle={5}>
                  {projectStatusData.map((entry, i) => <Cell key={i} fill={entry.color} />)}
                </Pie>
                <Tooltip contentStyle={{ background: '#111', border: '1px solid #1E1E1E', borderRadius: 8 }} />
              </PieChart>
            </ResponsiveContainer>
            <div className="flex flex-wrap gap-3 mt-2">
              {projectStatusData.map(d => (
                <div key={d.name} className="flex items-center gap-1.5">
                  <div className="w-2.5 h-2.5 rounded-full" style={{ background: d.color }} />
                  <span className="text-xs text-text-secondary">{d.name} ({d.value})</span>
                </div>
              ))}
            </div>
          </Card>

          {/* AI Summary */}
          <Card>
            <div className="flex items-center gap-2 mb-3">
              <Brain size={18} className="text-orange" />
              <h3 className="text-base font-semibold text-white">AI Executive Summary</h3>
            </div>
            <div className="space-y-2 text-sm">
              <div className="p-3 bg-bg rounded-lg border border-border/50">
                {totalTasks > 0 ? (
                  <div className="space-y-2">
                    <p className="text-xs text-text-secondary">
                      <span className="text-orange font-medium">{Math.round(completedTasks / totalTasks * 100)}%</span> overall task completion across <span className="text-white font-medium">{stats?.totalProjects ?? 0}</span> projects.
                    </p>
                    {overdueTasks > 0 && (
                      <p className="text-xs text-red-400">
                        ⚠️ {overdueTasks} overdue tasks require immediate attention.
                      </p>
                    )}
                    <p className="text-xs text-text-secondary">
                      {stats?.totalUsers ?? 0} team members across {typeof stats?.departments === 'number' ? stats.departments : deptPerformanceData.length} departments.
                    </p>
                  </div>
                ) : (
                  <p className="text-xs text-text-muted">AI executive analysis will appear here when data is available.</p>
                )}
              </div>
            </div>
          </Card>

          {/* Top Performers */}
          <Card>
            <h3 className="text-base font-semibold text-white mb-3">Top Performers</h3>
            <div className="space-y-2">
              {topPerformers.length === 0 ? (
                <div className="text-text-muted text-sm py-2">No performance data available yet</div>
              ) : (
                topPerformers.map((p, i) => (
                  <div key={p.userId ?? p.id ?? i} className="flex items-center gap-3 p-2 rounded-lg">
                    <span className="text-lg font-bold text-orange w-6">#{i + 1}</span>
                    <div className="flex-1">
                      <p className="text-sm text-white">{p.fullName ?? p.name ?? 'Unknown'}</p>
                      <p className="text-xs text-text-muted">{p.department ?? p.dept ?? '—'}</p>
                    </div>
                    <Badge variant="success">{p.overallScore ?? p.score ?? p.averageScore ?? '—'}%</Badge>
                  </div>
                ))
              )}
            </div>
          </Card>

          {/* Risk Alerts */}
          <Card>
            <h3 className="text-base font-semibold text-white mb-3">Risk Alerts</h3>
            <div className="space-y-2">
              {riskAlerts.length === 0 ? (
                <div className="text-text-muted text-sm py-2">No risk alerts</div>
              ) : (
                riskAlerts.map((alert, i) => (
                  <div
                    key={i}
                    className={`p-3 rounded-lg ${
                      alert.type === 'overdue' || alert.severity === 'high'
                        ? 'bg-danger/5 border border-danger/20'
                        : 'bg-warning/5 border border-warning/20'
                    }`}
                  >
                    <p className={`text-sm font-medium ${alert.type === 'overdue' || alert.severity === 'high' ? 'text-danger' : 'text-warning'}`}>
                      {alert.message ?? alert.title ?? 'Alert'}
                    </p>
                    <p className="text-xs text-text-muted">{alert.detail ?? alert.description ?? ''}</p>
                  </div>
                ))
              )}
            </div>
          </Card>
        </div>
      </div>
    </AppLayout>
  );
}
