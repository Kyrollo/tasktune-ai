import { useCallback, useEffect, useMemo, useState } from 'react';
import AppLayout from '../../layouts/AppLayout';
import { Badge, Button, Card } from '../../components/ui';
import { platformService } from '../../services/platformService';
import { Search, RefreshCw, Building2, CreditCard, ShieldCheck } from 'lucide-react';

const today = () => new Date().toISOString().slice(0, 10);
const formatBytes = (value) => {
  const bytes = Number(value || 0);
  if (bytes < 1024) return bytes + ' B';
  const units = ['KB', 'MB', 'GB', 'TB'];
  let size = bytes / 1024;
  let unit = 0;
  while (size >= 1024 && unit < units.length - 1) { size /= 1024; unit += 1; }
  return size.toFixed(1) + ' ' + units[unit];
};
const dateToUtc = (value) => value ? new Date(`${value}T00:00:00.000Z`).toISOString() : null;

export default function PlatformConsole() {
  const [tenants, setTenants] = useState([]);
  const [plans, setPlans] = useState([]);
  const [selectedTenantId, setSelectedTenantId] = useState('');
  const [tenantDetails, setTenantDetails] = useState(null);
  const [subscriptionDetails, setSubscriptionDetails] = useState(null);
  const [usageSummary, setUsageSummary] = useState(null);
  const [search, setSearch] = useState('');
  const [loading, setLoading] = useState(true);
  const [detailsLoading, setDetailsLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [lifecycleReason, setLifecycleReason] = useState('');
  const [assignment, setAssignment] = useState({ planId: '', status: 'Trial', startsAt: today(), endsAt: '', reason: '' });
  const [newPlan, setNewPlan] = useState({ code: '', name: '', description: '', maxUsers: '', maxStorageBytes: '', monthlyAiTokenLimit: '' });

  const loadOverview = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const [tenantResult, planResult] = await Promise.all([
        platformService.getTenants({ page: 1, pageSize: 100 }),
        platformService.getPlans(),
      ]);
      const rows = tenantResult.data?.items ?? [];
      setTenants(rows);
      setPlans(planResult.data?.items ?? []);
      setSelectedTenantId((current) => current && rows.some((row) => row.companyId === Number(current))
        ? current : (rows[0]?.companyId ? String(rows[0].companyId) : ''));
    } catch (err) {
      setError(err.response?.data?.message || 'Unable to load platform data. Confirm platform administrator access.');
    } finally {
      setLoading(false);
    }
  }, []);

  const loadTenant = useCallback(async (companyId) => {
    if (!companyId) {
      setTenantDetails(null);
      setSubscriptionDetails(null);
      setUsageSummary(null);
      return;
    }
    setDetailsLoading(true);
    try {
      const [tenantResult, subscriptionResult, usageResult] = await Promise.all([
        platformService.getTenant(companyId),
        platformService.getSubscription(companyId),
        platformService.getTenantUsage(companyId),
      ]);
      setTenantDetails(tenantResult.data);
      setSubscriptionDetails(subscriptionResult.data);
      setUsageSummary(usageResult.data);
      const current = subscriptionResult.data?.subscription;
      setAssignment({
        planId: current?.planId ? String(current.planId) : '',
        status: current?.status || 'Trial',
        startsAt: current?.startsAt ? current.startsAt.slice(0, 10) : today(),
        endsAt: current?.endsAt ? current.endsAt.slice(0, 10) : '',
        reason: '',
      });
    } catch (err) {
      setError(err.response?.data?.message || 'Unable to load the selected tenant.');
    } finally {
      setDetailsLoading(false);
    }
  }, []);

  useEffect(() => { loadOverview(); }, [loadOverview]);
  useEffect(() => { loadTenant(selectedTenantId); }, [selectedTenantId, loadTenant]);

  const filteredTenants = useMemo(() => {
    const term = search.trim().toLowerCase();
    if (!term) return tenants;
    return tenants.filter((tenant) =>
      String(tenant.companyName || '').toLowerCase().includes(term) ||
      String(tenant.companyId).includes(term));
  }, [tenants, search]);

  const runAction = async (action, message) => {
    setSaving(true);
    setError('');
    setNotice('');
    try {
      await action();
      setNotice(message);
      const companyId = selectedTenantId;
      await loadOverview();
      if (companyId) await loadTenant(companyId);
    } catch (err) {
      setError(err.response?.data?.message || 'The operation could not be completed.');
    } finally {
      setSaving(false);
    }
  };

  const handleLifecycle = () => {
    if (!selectedTenantId || lifecycleReason.trim().length < 10) {
      setError('Enter a reason with at least 10 characters.');
      return;
    }
    const suspended = tenantDetails?.company?.status === 'Suspended';
    const reason = lifecycleReason.trim();
    setLifecycleReason('');
    runAction(() => suspended
      ? platformService.reactivateTenant(selectedTenantId, reason)
      : platformService.suspendTenant(selectedTenantId, reason),
    suspended ? 'Tenant reactivated.' : 'Tenant suspended.');
  };

  const handleAssignment = () => {
    if (!selectedTenantId || !assignment.planId || assignment.reason.trim().length < 10 || !assignment.startsAt) {
      setError('Choose a plan, start date and provide a reason of at least 10 characters.');
      return;
    }
    runAction(() => platformService.assignSubscription(selectedTenantId, {
      planId: Number(assignment.planId),
      status: assignment.status,
      startsAt: dateToUtc(assignment.startsAt),
      endsAt: dateToUtc(assignment.endsAt),
      reason: assignment.reason.trim(),
    }), 'Tenant subscription updated.');
  };

  const handleCreatePlan = () => {
    if (!newPlan.code.trim() || !newPlan.name.trim()) {
      setError('Plan code and name are required.');
      return;
    }
    runAction(async () => {
      await platformService.createPlan({
        code: newPlan.code.trim(),
        name: newPlan.name.trim(),
        description: newPlan.description.trim() || null,
        maxUsers: newPlan.maxUsers ? Number(newPlan.maxUsers) : null,
        maxStorageBytes: newPlan.maxStorageBytes ? Number(newPlan.maxStorageBytes) : null,
        monthlyAiTokenLimit: newPlan.monthlyAiTokenLimit ? Number(newPlan.monthlyAiTokenLimit) : null,
      });
      setNewPlan({ code: '', name: '', description: '', maxUsers: '', maxStorageBytes: '', monthlyAiTokenLimit: '' });
    }, 'Subscription plan created.');
  };

  const toggleFeature = (plan, featureCode) => {
    const current = (plan.features || []).find((feature) => feature.featureCode === featureCode);
    runAction(() => platformService.setPlanFeature(plan.planId, featureCode, {
      isEnabled: !current?.isEnabled,
      limitValue: current?.limitValue ?? null,
    }), `${featureCode} updated for ${plan.name}.`);
  };

  if (loading) return <AppLayout title="Platform Console"><div className="flex h-64 items-center justify-center text-text-secondary">Loading platform console…</div></AppLayout>;
  const company = tenantDetails?.company;
  const currentSubscription = subscriptionDetails?.subscription;

  return (
    <AppLayout title="Platform Console">
      <div className="mb-6 flex flex-wrap items-start justify-between gap-3">
        <div>
          <p className="text-sm text-text-muted">Cross-tenant operations</p>
          <h1 className="mt-1 text-2xl font-semibold text-text-primary">Platform Console</h1>
          <p className="mt-1 text-sm text-text-secondary">Manage tenant lifecycle, plans, entitlements and subscriptions.</p>
        </div>
        <Button onClick={loadOverview} disabled={saving}><span className="flex items-center gap-2"><RefreshCw size={15} /> Refresh</span></Button>
      </div>
      {error && <div className="mb-4 rounded-lg border border-danger/30 bg-danger/10 px-4 py-3 text-sm text-danger">{error}</div>}
      {notice && <div className="mb-4 rounded-lg border border-success/30 bg-success/10 px-4 py-3 text-sm text-success">{notice}</div>}

      <div className="grid grid-cols-1 gap-5 xl:grid-cols-[minmax(280px,0.85fr)_minmax(0,2fr)]">
        <Card>
          <div className="mb-4 flex items-center justify-between"><h2 className="font-semibold text-text-primary">Tenants</h2><Badge>{tenants.length} listed</Badge></div>
          <div className="relative mb-3"><Search size={15} className="absolute left-3 top-1/2 -translate-y-1/2 text-text-muted" /><input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search name or ID" className="w-full rounded-lg border border-border bg-bg py-2 pl-9 pr-3 text-sm text-text-primary outline-none focus:border-orange" /></div>
          <div className="max-h-[620px] space-y-2 overflow-y-auto">
            {filteredTenants.map((tenant) => <button key={tenant.companyId} onClick={() => { setSelectedTenantId(String(tenant.companyId)); setError(''); }} className={`w-full rounded-lg border px-3 py-3 text-left transition-colors ${Number(selectedTenantId) === tenant.companyId ? 'border-orange bg-orange/10' : 'border-border hover:bg-surface-hover'}`}>
              <div className="flex items-center justify-between gap-2"><span className="truncate text-sm font-medium text-text-primary">{tenant.companyName}</span><Badge variant={tenant.status === 'Active' ? 'success' : 'danger'}>{tenant.status}</Badge></div>
              <div className="mt-1 flex justify-between text-xs text-text-muted"><span>Tenant #{tenant.companyId}</span><span>{tenant.activeUserCount ?? 0} active users</span></div>
            </button>)}
            {filteredTenants.length === 0 && <p className="py-8 text-center text-sm text-text-muted">No tenants match this search.</p>}
          </div>
        </Card>

        <div className="space-y-5">
          <Card>
            <div className="mb-4 flex items-center gap-2"><Building2 size={18} className="text-orange" /><h2 className="font-semibold text-text-primary">Tenant details</h2>{company && <Badge>{company.status}</Badge>}</div>
            {detailsLoading ? <p className="text-sm text-text-muted">Loading tenant details…</p> : !company ? <p className="text-sm text-text-muted">Select a tenant to inspect lifecycle and subscription.</p> : <>
              <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
                <div><p className="text-xs text-text-muted">Company ID</p><p className="font-medium text-text-primary">{company.companyId}</p></div>
                <div><p className="text-xs text-text-muted">Status</p><p className="font-medium text-text-primary">{company.status}</p></div>
                <div><p className="text-xs text-text-muted">Active users</p><p className="font-medium text-text-primary">{company.activeUserCount}</p></div>
                <div><p className="text-xs text-text-muted">Total users</p><p className="font-medium text-text-primary">{company.totalUserCount}</p></div>
              </div>
              <div className="mt-4 grid gap-3 md:grid-cols-[minmax(0,1fr)_auto]">
                <input value={lifecycleReason} onChange={(event) => setLifecycleReason(event.target.value)} placeholder="Reason (10–1,000 characters)" maxLength={1000} className="w-full rounded-lg border border-border bg-bg px-3 py-2 text-sm text-text-primary outline-none focus:border-orange" />
                <Button disabled={saving || lifecycleReason.trim().length < 10} onClick={handleLifecycle}>{company.status === 'Suspended' ? 'Reactivate tenant' : 'Suspend tenant'}</Button>
              </div>
              <div className="mt-5 border-t border-border pt-4">
                <h3 className="mb-3 text-sm font-semibold text-text-primary">Lifecycle history</h3>
                <div className="max-h-36 space-y-2 overflow-y-auto">
                  {(tenantDetails.lifecycle || []).map((event) => <div key={event.eventId} className="rounded-lg bg-bg px-3 py-2 text-xs">
                    <div className="flex justify-between gap-2"><span className="font-medium text-text-primary">{event.action}</span><span className="text-text-muted">{new Date(event.createdAt).toLocaleString()}</span></div>
                    <p className="mt-1 text-text-secondary">{event.reason}</p>
                  </div>)}
                  {(tenantDetails.lifecycle || []).length === 0 && <p className="text-xs text-text-muted">No lifecycle events recorded.</p>}
                </div>
              </div>
            </>}
          </Card>

          <Card>
            <div className="mb-4 flex items-center gap-2"><CreditCard size={18} className="text-orange" /><h2 className="font-semibold text-text-primary">Tenant subscription</h2></div>
            {currentSubscription ? <p className="mb-3 text-sm text-text-secondary">Current: <span className="font-medium text-text-primary">{currentSubscription.planName}</span> · {currentSubscription.status}</p> : <p className="mb-3 text-sm text-text-muted">No subscription assigned; feature-gated actions are denied.</p>}
            <div className="grid gap-3 md:grid-cols-2">
              <label className="text-xs text-text-muted">Plan<select value={assignment.planId} onChange={(event) => setAssignment({ ...assignment, planId: event.target.value })} className="mt-1 w-full rounded-lg border border-border bg-bg px-3 py-2 text-sm text-text-primary"><option value="">Select plan</option>{plans.filter((plan) => plan.isActive).map((plan) => <option key={plan.planId} value={plan.planId}>{plan.name} ({plan.code})</option>)}</select></label>
              <label className="text-xs text-text-muted">Status<select value={assignment.status} onChange={(event) => setAssignment({ ...assignment, status: event.target.value })} className="mt-1 w-full rounded-lg border border-border bg-bg px-3 py-2 text-sm text-text-primary">{['Trial', 'Active', 'PastDue', 'Cancelled'].map((status) => <option key={status}>{status}</option>)}</select></label>
              <label className="text-xs text-text-muted">Starts<input type="date" value={assignment.startsAt} onChange={(event) => setAssignment({ ...assignment, startsAt: event.target.value })} className="mt-1 w-full rounded-lg border border-border bg-bg px-3 py-2 text-sm text-text-primary" /></label>
              <label className="text-xs text-text-muted">Ends (optional)<input type="date" value={assignment.endsAt} onChange={(event) => setAssignment({ ...assignment, endsAt: event.target.value })} className="mt-1 w-full rounded-lg border border-border bg-bg px-3 py-2 text-sm text-text-primary" /></label>
              <label className="text-xs text-text-muted md:col-span-2">Assignment reason<textarea value={assignment.reason} onChange={(event) => setAssignment({ ...assignment, reason: event.target.value })} minLength={10} maxLength={1000} rows={2} className="mt-1 w-full rounded-lg border border-border bg-bg px-3 py-2 text-sm text-text-primary" /></label>
            </div>
            <div className="mt-3"><Button disabled={saving || !selectedTenantId} onClick={handleAssignment}>Save subscription</Button></div>
            <div className="mt-5 border-t border-border pt-4">
              <h3 className="mb-3 text-sm font-semibold text-text-primary">Subscription history</h3>
              <div className="max-h-40 space-y-2 overflow-y-auto">
                {(subscriptionDetails?.events || []).map((event) => <div key={event.eventId} className="rounded-lg bg-bg px-3 py-2 text-xs">
                  <div className="flex justify-between gap-2"><span className="font-medium text-text-primary">{event.action}</span><span className="text-text-muted">{new Date(event.createdAt).toLocaleString()}</span></div>
                  <p className="mt-1 text-text-secondary">{event.previousStatus || 'Unassigned'} → {event.newStatus}. {event.reason}</p>
                </div>)}
              </div>
            </div>
          </Card>
        </div>
      </div>

          <Card>
            <div className="mb-4 flex items-center justify-between gap-3">
              <h2 className="font-semibold text-text-primary">Current-month usage</h2>
              <Badge>{usageSummary?.hasActiveSubscription ? usageSummary.planCode : 'No active plan'}</Badge>
            </div>
            {!usageSummary ? <p className="text-sm text-text-muted">Usage is not available.</p> : <>
              <div className="grid grid-cols-2 gap-3 md:grid-cols-3">
                <div className="rounded-lg bg-bg p-3">
                  <p className="text-xs text-text-muted">Reserved seats</p>
                  <p className="mt-1 text-lg font-semibold text-text-primary">{usageSummary.seatsReserved}{usageSummary.maxUsers == null ? ' / ∞' : ' / ' + usageSummary.maxUsers}</p>
                  <p className="text-xs text-text-muted">{usageSummary.activeUsers} active · {usageSummary.pendingInvitations} invited</p>
                </div>
                <div className="rounded-lg bg-bg p-3">
                  <p className="text-xs text-text-muted">Attachment storage</p>
                  <p className="mt-1 text-lg font-semibold text-text-primary">{formatBytes(usageSummary.taskAttachmentStorageBytes)}{usageSummary.maxStorageBytes == null ? ' / ∞' : ' / ' + formatBytes(usageSummary.maxStorageBytes)}</p>
                </div>
                <div className="rounded-lg bg-bg p-3">
                  <p className="text-xs text-text-muted">AI actions</p>
                  <p className="mt-1 text-lg font-semibold text-text-primary">{usageSummary.aiActions}</p>
                </div>
                <div className="rounded-lg bg-bg p-3">
                  <p className="text-xs text-text-muted">AI tokens</p>
                  <p className="mt-1 text-lg font-semibold text-text-primary">{usageSummary.aiTokenUsageAvailable ? Number(usageSummary.aiTokensUsed).toLocaleString() : 'Not reported'}</p>
                  <p className="text-xs text-text-muted">{usageSummary.monthlyAiTokenLimit == null ? 'No configured cap' : 'Limit ' + Number(usageSummary.monthlyAiTokenLimit).toLocaleString()}</p>
                </div>
                <div className="rounded-lg bg-bg p-3">
                  <p className="text-xs text-text-muted">Period (UTC)</p>
                  <p className="mt-1 text-lg font-semibold text-text-primary">{String(usageSummary.month).padStart(2, '0')}/{usageSummary.year}</p>
                </div>
              </div>
              {(usageSummary.seatLimitExceeded || usageSummary.storageLimitExceeded || usageSummary.aiTokenLimitExceeded) && <p className="mt-3 rounded-lg border border-danger/30 bg-danger/10 px-3 py-2 text-xs text-danger">One or more measured usage values exceed the assigned plan limit.</p>}
              <p className="mt-3 text-xs text-text-muted">AI tokens are shown only when a provider reports actual token counts. “Not reported” is not zero.</p>
            </>}
          </Card>

      <div className="mt-6 grid grid-cols-1 gap-5 xl:grid-cols-2">
        <Card>
          <div className="mb-4 flex items-center gap-2"><ShieldCheck size={18} className="text-orange" /><h2 className="font-semibold text-text-primary">Plan catalogue and entitlements</h2></div>
          <div className="space-y-3">{plans.map((plan) => <div key={plan.planId} className="rounded-lg border border-border p-3">
            <div className="flex flex-wrap items-start justify-between gap-2"><div><p className="font-medium text-text-primary">{plan.name}</p><p className="text-xs text-text-muted">{plan.code} · {plan.maxUsers ?? 'Unlimited'} seats · {plan.monthlyAiTokenLimit ?? 'No AI cap'}</p></div><Badge variant={plan.isActive ? 'success' : 'danger'}>{plan.isActive ? 'Active' : 'Inactive'}</Badge></div>
            <div className="mt-3 flex flex-wrap gap-2">{['ai.chat', 'ai.task_guidance'].map((featureCode) => {
              const feature = (plan.features || []).find((item) => item.featureCode === featureCode);
              return <button key={featureCode} disabled={saving} onClick={() => toggleFeature(plan, featureCode)} className={`rounded-lg border px-3 py-1.5 text-xs ${feature?.isEnabled ? 'border-success/40 bg-success/10 text-success' : 'border-border text-text-secondary'}`}>{featureCode}: {feature?.isEnabled ? 'On' : 'Off'}</button>;
            })}</div>
          </div>)}</div>
        </Card>
        <Card>
          <h2 className="mb-4 font-semibold text-text-primary">Create a plan</h2>
          <div className="grid gap-3 sm:grid-cols-2">
            <label className="text-xs text-text-muted">Plan code<input value={newPlan.code} onChange={(event) => setNewPlan({ ...newPlan, code: event.target.value })} placeholder="starter" maxLength={64} className="mt-1 w-full rounded-lg border border-border bg-bg px-3 py-2 text-sm text-text-primary" /></label>
            <label className="text-xs text-text-muted">Display name<input value={newPlan.name} onChange={(event) => setNewPlan({ ...newPlan, name: event.target.value })} placeholder="Starter" maxLength={120} className="mt-1 w-full rounded-lg border border-border bg-bg px-3 py-2 text-sm text-text-primary" /></label>
            <label className="text-xs text-text-muted sm:col-span-2">Description<input value={newPlan.description} onChange={(event) => setNewPlan({ ...newPlan, description: event.target.value })} maxLength={500} className="mt-1 w-full rounded-lg border border-border bg-bg px-3 py-2 text-sm text-text-primary" /></label>
            <label className="text-xs text-text-muted">Max users<input type="number" min="1" value={newPlan.maxUsers} onChange={(event) => setNewPlan({ ...newPlan, maxUsers: event.target.value })} className="mt-1 w-full rounded-lg border border-border bg-bg px-3 py-2 text-sm text-text-primary" /></label>
            <label className="text-xs text-text-muted">Monthly AI token cap<input type="number" min="0" value={newPlan.monthlyAiTokenLimit} onChange={(event) => setNewPlan({ ...newPlan, monthlyAiTokenLimit: event.target.value })} className="mt-1 w-full rounded-lg border border-border bg-bg px-3 py-2 text-sm text-text-primary" /></label>
          </div>
          <div className="mt-4"><Button disabled={saving} onClick={handleCreatePlan}>Create plan</Button></div>
          <p className="mt-3 text-xs text-text-muted">No prices or billing provider are configured here. Seat/storage/AI limits are metadata until metering is implemented.</p>
        </Card>
      </div>
    </AppLayout>
  );
}
