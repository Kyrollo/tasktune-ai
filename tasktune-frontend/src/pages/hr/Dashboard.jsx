import { useState, useEffect } from 'react';
import AppLayout from '../../layouts/AppLayout';
import { Card, StatCard, Badge } from '../../components/ui';
import { dashboardService } from '../../services/dashboardService';
import { communicationService } from '../../services/communicationService';
import { Users, AlertTriangle, TrendingDown, Activity } from 'lucide-react';
import { BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer } from 'recharts';

export default function HRDashboard() {
  const [stats, setStats] = useState(null);
  const [recentComms, setRecentComms] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchData = async () => {
      try {
        const [dashboardRes, commsRes] = await Promise.all([
          dashboardService.getHRDashboard(),
          communicationService.getCommunications(1, 10).catch(() => ({ data: { items: [] } })),
        ]);
        setStats(dashboardRes.data);
        setRecentComms(commsRes?.data?.items ?? []);
      } catch (err) {
        console.error('Failed to load dashboard:', err);
      } finally {
        setLoading(false);
      }
    };
    fetchData();
  }, []);

  const rawEngagement = stats?.engagementData ?? stats?.departmentEngagement ?? [];
  const engagementData = Array.isArray(rawEngagement) ? rawEngagement.map(d => ({
    dept: d.dept ?? d.department,
    avg: d.avg ?? d.score ?? 0
  })) : [];
  const heatmapData = Array.isArray(stats?.engagementHeatmap) ? stats.engagementHeatmap : [];
  const lowPerformersList = Array.isArray(stats?.lowPerformersList)
    ? stats.lowPerformersList
    : Array.isArray(stats?.lowPerformers) ? stats.lowPerformers : [];

  if (loading) {
    return (
      <AppLayout title="HR Dashboard">
        <div className="flex items-center justify-center h-64">
          <div className="text-text-secondary">Loading...</div>
        </div>
      </AppLayout>
    );
  }

  const activeEmployees = stats?.totalEmployees ?? 0;
  const inactiveEmployees = stats?.inactiveEmployees ?? 0;
  const averageScore = stats?.averageScore ?? 0;
  const warningCount = lowPerformersList?.length ?? stats?.lowPerformers ?? 0;

  return (
    <AppLayout title="HR Dashboard">
      {/* Stats */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
        <StatCard label="Active Employees" value={activeEmployees} icon={Users} trend="+2 new hires" trendUp />
        <StatCard label="Inactive" value={inactiveEmployees} icon={TrendingDown} />
        <StatCard label="Avg Score" value={`${averageScore}%`} icon={Activity} trend="+3% this month" trendUp />
        <StatCard label="Warning Flags" value={warningCount} icon={AlertTriangle} trend="Needs review" trendUp={false} />
      </div>

      <div className="grid lg:grid-cols-3 gap-6">
        <div className="lg:col-span-2 space-y-4">
          {/* Engagement Chart */}
          <Card>
            <h3 className="text-lg font-semibold text-white mb-4">Department Engagement</h3>
            {engagementData?.length > 0 ? (
              <ResponsiveContainer width="100%" height={250}>
                <BarChart data={engagementData}>
                  <XAxis dataKey="dept" stroke="#6B7280" fontSize={12} />
                  <YAxis stroke="#6B7280" fontSize={12} />
                  <Tooltip contentStyle={{ background: '#111', border: '1px solid #1E1E1E', borderRadius: 8 }} />
                  <Bar dataKey="avg" fill="#F97316" radius={[4, 4, 0, 0]} />
                </BarChart>
              </ResponsiveContainer>
            ) : (
              <div className="flex items-center justify-center h-[250px] text-text-muted text-sm">No engagement data available</div>
            )}
          </Card>

          {/* Engagement Heatmap (as table) */}
          <Card>
            <h3 className="text-lg font-semibold text-white mb-4">Weekly Engagement Heatmap</h3>
            {heatmapData?.length > 0 ? (
              <div className="overflow-x-auto">
                <table className="w-full">
                  <thead>
                    <tr className="border-b border-border">
                      <th className="text-left py-2 px-3 text-xs text-text-muted">Department</th>
                      <th className="text-center py-2 px-3 text-xs text-text-muted">Week 1</th>
                      <th className="text-center py-2 px-3 text-xs text-text-muted">Week 2</th>
                      <th className="text-center py-2 px-3 text-xs text-text-muted">Week 3</th>
                      <th className="text-center py-2 px-3 text-xs text-text-muted">Week 4</th>
                    </tr>
                  </thead>
                  <tbody>
                    {heatmapData.map(d => (
                      <tr key={d.dept ?? d.department} className="border-b border-border/50">
                        <td className="py-2 px-3 text-sm text-white">{d.dept ?? d.department}</td>
                        {[d.week1, d.week2, d.week3, d.week4].map((v, i) => (
                          <td key={i} className="py-2 px-3 text-center">
                            <span className={`inline-block px-3 py-1 rounded-md text-xs font-medium ${
                              v >= 90 ? 'bg-success/20 text-success' : v >= 80 ? 'bg-orange/20 text-orange' : 'bg-danger/20 text-danger'
                            }`}>{v}%</span>
                          </td>
                        ))}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : (
              <div className="py-12 text-center text-text-muted text-sm">No heatmap data available</div>
            )}
          </Card>

          {/* Communication Log */}
          <Card>
            <h3 className="text-lg font-semibold text-white mb-4">Recent Communications</h3>
            <div className="space-y-3">
              {recentComms.length > 0 ? (
                recentComms.map((c, idx) => (
                  <div key={c.communicationId ?? c.id ?? idx} className="flex items-center justify-between p-3 bg-bg rounded-lg border border-border/50">
                    <div>
                      <p className="text-sm font-medium text-white">{c.subject ?? c.title ?? '—'}</p>
                      <p className="text-xs text-text-muted">
                        {c.sentByName ?? c.senderName ?? '—'} → {c.recipientUserName ?? c.recipientDepartmentName ?? c.recipient ?? '—'} • {c.sentAt ? new Date(c.sentAt).toLocaleDateString() : '—'}
                      </p>
                    </div>
                    <Badge variant="success">Sent</Badge>
                  </div>
                ))
              ) : (
                <div className="py-8 text-center text-text-muted text-sm">No recent communications</div>
              )}
            </div>
          </Card>
        </div>

        {/* Right sidebar */}
        <div className="space-y-4">
          {/* Warning Flags */}
          <Card>
            <h3 className="text-base font-semibold text-white mb-3">Warning Flags</h3>
            <div className="space-y-2">
              {lowPerformersList?.length > 0 ? (
                lowPerformersList.map((p, i) => (
                  <div key={i} className={`p-3 rounded-lg ${(p.count >= 3 || p.delayCount >= 3) ? 'bg-danger/5 border border-danger/20' : 'bg-warning/5 border border-warning/20'}`}>
                    <p className={`text-sm font-medium ${(p.count >= 3 || p.delayCount >= 3) ? 'text-danger' : 'text-warning'}`}>
                      {p.reason ?? 'Low Performance'}
                    </p>
                    <p className="text-xs text-text-muted">{p.name ?? p.employeeName} — {p.department ?? p.dept ?? ''}</p>
                  </div>
                ))
              ) : (
                <p className="text-sm text-text-muted py-2">No warning flags</p>
              )}
            </div>
          </Card>

          {/* Repeated Delays */}
          <Card>
            <h3 className="text-base font-semibold text-white mb-3">Repeated Delays</h3>
            <div className="space-y-2">
              {lowPerformersList?.filter(p => (p.count ?? p.delayCount ?? 0) > 0)?.length > 0 ? (
                lowPerformersList.filter(p => (p.count ?? p.delayCount ?? 0) > 0).map((d, i) => (
                  <div key={d.name ?? d.employeeName ?? i} className="flex items-center justify-between p-2 rounded-lg">
                    <div>
                      <p className="text-sm text-white">{d.name ?? d.employeeName}</p>
                      <p className="text-xs text-text-muted">{d.dept ?? d.department}</p>
                    </div>
                    <Badge variant={(d.count ?? d.delayCount ?? 0) >= 3 ? 'danger' : 'warning'}>
                      {d.count ?? d.delayCount ?? 0} delays
                    </Badge>
                  </div>
                ))
              ) : (
                <p className="text-sm text-text-muted py-2">No repeated delays detected</p>
              )}
            </div>
          </Card>
        </div>
      </div>
    </AppLayout>
  );
}
