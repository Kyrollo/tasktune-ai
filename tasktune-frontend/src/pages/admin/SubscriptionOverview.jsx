import { useCallback, useEffect, useState } from 'react';
import AppLayout from '../../layouts/AppLayout';
import { Card, Badge, Button } from '../../components/ui';
import { subscriptionService } from '../../services/subscriptionService';
import { RefreshCw, Users, HardDrive, Brain, Layers3, AlertCircle, ShieldCheck } from 'lucide-react';

const formatBytes = (value) => {
  const bytes = Number(value || 0);
  if (bytes <= 0) return '0 B';
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  const tier = Math.min(Math.floor(Math.log(bytes) / Math.log(1024)), units.length - 1);
  return `${(bytes / (1024 ** tier)).toFixed(tier === 0 ? 0 : 1)} ${units[tier]}`;
};

const formatDate = (value) => value ? new Date(value).toLocaleDateString() : '—';

const quotaLabel = (used, limit, suffix = '') => {
  if (limit == null) return `${used}${suffix} used · Unlimited`;
  return `${used}${suffix} of ${limit}${suffix}`;
};

function Meter({ title, detail, used, limit, icon: Icon, format = (v) => String(v) }) {
  const percentage = limit == null || limit <= 0 ? 0 : Math.min(100, Math.max(0, (Number(used || 0) / Number(limit)) * 100));
  return (
    <div className="p-4 rounded-xl border border-border bg-bg">
      <div className="flex items-start justify-between gap-3 mb-3">
        <div>
          <p className="text-sm font-medium text-text-primary">{title}</p>
          <p className="text-xs text-text-muted mt-1">{detail}</p>
        </div>
        <Icon size={18} className="text-orange" />
      </div>
      <p className="text-xl font-semibold text-text-primary">{format(used)} <span className="text-sm font-normal text-text-muted">/ {limit == null ? 'Unlimited' : format(limit)}</span></p>
      {limit != null && (
        <div className="h-2 rounded-full bg-surface-tertiary overflow-hidden mt-3">
          <div className={`h-full rounded-full ${percentage >= 90 ? 'bg-danger' : 'bg-orange'}`} style={{ width: `${percentage}%` }} />
        </div>
      )}
    </div>
  );
}

export default function SubscriptionOverview() {
  const [snapshot, setSnapshot] = useState(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState('');

  const load = useCallback(async (silent = false) => {
    if (silent) setRefreshing(true);
    else setLoading(true);
    setError('');
    try {
      const { data } = await subscriptionService.getCurrent();
      setSnapshot(data);
    } catch (err) {
      setError(err.response?.data?.message || 'Unable to load the current subscription and usage.');
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  if (loading) {
    return (
      <AppLayout title="Subscription & Usage">
        <div className="h-64 flex items-center justify-center text-sm text-text-secondary">Loading subscription information…</div>
      </AppLayout>
    );
  }

  const subscription = snapshot?.subscription;
  const limits = snapshot?.limits || {};
  const usage = snapshot?.usage || {};
  const modules = (() => {
    try { return JSON.parse(snapshot?.plan?.enabledModulesJson || '[]'); }
    catch { return []; }
  })();

  return (
    <AppLayout title="Subscription & Usage">
      <div className="flex items-center justify-between gap-3 mb-5">
        <div>
          <p className="text-sm text-text-secondary">Your company's plan, entitlements and measured platform usage</p>
        </div>
        <Button variant="secondary" onClick={() => load(true)} disabled={refreshing}>
          <span className="inline-flex items-center gap-2"><RefreshCw size={14} className={refreshing ? 'animate-spin' : ''} /> Refresh</span>
        </Button>
      </div>

      {error && (
        <div role="alert" className="flex gap-2 items-start rounded-lg border border-danger/20 bg-danger/10 text-danger p-3 mb-5 text-sm">
          <AlertCircle size={16} className="mt-0.5 shrink-0" /> {error}
        </div>
      )}

      {!snapshot ? (
        <Card>
          <div className="py-8 text-center">
            <ShieldCheck size={30} className="mx-auto text-text-muted mb-3" />
            <h2 className="font-semibold text-text-primary">No subscription information available</h2>
            <p className="text-sm text-text-muted mt-2">The platform administrator must provision a subscription for this company.</p>
          </div>
        </Card>
      ) : (
        <>
          <Card>
            <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-4">
              <div>
                <div className="flex items-center gap-2 mb-2">
                  <Layers3 size={19} className="text-orange" />
                  <h2 className="text-xl font-semibold text-text-primary">{subscription?.plan || 'Current plan'}</h2>
                  <Badge variant={subscription?.status === 'active' ? 'success' : subscription?.status === 'trialing' ? 'warning' : 'danger'}>
                    {subscription?.status || 'unknown'}
                  </Badge>
                </div>
                <p className="text-sm text-text-secondary">
                  Billing mode: {subscription?.billingMode === 'free' ? 'Free' : 'Managed by sales / billing provider'}
                </p>
                <p className="text-xs text-text-muted mt-2">
                  Current period: {formatDate(subscription?.currentPeriodStart)} – {formatDate(subscription?.currentPeriodEnd)}
                  {subscription?.trialEndsAt ? ` · Trial ends ${formatDate(subscription.trialEndsAt)}` : ''}
                </p>
              </div>
              <div className="lg:text-right">
                <p className="text-xs text-text-muted">Plan code</p>
                <p className="font-mono text-sm text-text-primary">{subscription?.planCode || '—'}</p>
                <p className="text-xs text-text-muted mt-2">{subscription?.cancelAtPeriodEnd ? 'Cancellation scheduled at period end' : 'No cancellation scheduled'}</p>
              </div>
            </div>
          </Card>

          <div className="grid md:grid-cols-3 gap-4 mt-5">
            <Meter
              title="Active employees"
              detail="Active accounts in this company"
              used={usage.activeEmployees || 0}
              limit={limits.maxActiveEmployees}
              icon={Users}
            />
            <Meter
              title="Storage"
              detail="Measured file storage usage"
              used={usage.storageBytes || 0}
              limit={limits.maxStorageBytes}
              icon={HardDrive}
              format={formatBytes}
            />
            <Meter
              title="AI requests"
              detail="Current calendar-month request count"
              used={usage.aiRequests || 0}
              limit={limits.maxMonthlyAiRequests}
              icon={Brain}
            />
          </div>

          <Card className="mt-5">
            <div className="flex items-center gap-2 mb-4">
              <Layers3 size={18} className="text-orange" />
              <h3 className="font-semibold text-text-primary">Enabled modules</h3>
            </div>
            <div className="flex flex-wrap gap-2">
              {modules.includes('*') ? (
                <Badge variant="success">All modules</Badge>
              ) : modules.length ? modules.map((module) => (
                <Badge key={module} variant="default">{module.replaceAll('_', ' ')}</Badge>
              )) : <p className="text-sm text-text-muted">No module entitlements configured.</p>}
            </div>
          </Card>

          <Card className="mt-5">
            <h3 className="font-semibold text-text-primary mb-2">Billing and upgrades</h3>
            <p className="text-sm text-text-secondary">
              This page shows the subscription assigned by the TaskTune platform. Online checkout, invoices, payment-provider webhooks and self-service upgrades are not connected yet. Contact your platform administrator to change the assigned plan.
            </p>
          </Card>

          <p className="text-xs text-text-muted mt-4">
            Usage snapshots are based on events recorded by enabled meters. Storage and AI totals may remain at zero until the corresponding upload and AI operations are wired to the meter.
          </p>
        </>
      )}
    </AppLayout>
  );
}
