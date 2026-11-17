import { useCallback, useEffect, useMemo, useState } from 'react';
import AppLayout from '../../layouts/AppLayout';
import { Badge, Button, Card, StatCard } from '../../components/ui';
import { hrWorkforceService as service } from '../../services/hrWorkforceService';
import { useAuth } from '../../contexts/AuthContext';
import { Users, ClipboardCheck, Target, Receipt, Laptop, HeartPulse, RefreshCw } from 'lucide-react';

const TABS = [
  { key: 'applicants', label: 'Recruitment', icon: Users },
  { key: 'onboarding', label: 'Onboarding', icon: ClipboardCheck },
  { key: 'goals', label: 'Performance goals', icon: Target },
  { key: 'expenses', label: 'Expenses', icon: Receipt },
  { key: 'assets', label: 'Assets', icon: Laptop },
  { key: 'benefits', label: 'Benefits', icon: HeartPulse },
];

const statusTone = (status) => ({
  pending: 'warning', open: 'warning', applied: 'info', screening: 'info',
  interview: 'info', offer: 'orange', approved: 'success', active: 'success',
  completed: 'success', hired: 'success', rejected: 'danger', declined: 'danger',
  paused: 'warning', assigned: 'orange', available: 'success',
}[String(status || '').toLowerCase()] || 'secondary');

const today = () => new Date().toISOString().slice(0, 10);

export default function HRWorkforceCenter() {
  const { user } = useAuth();
  const isHr = ['HR', 'CEO', 'Admin'].includes(user?.role);
  const [tab, setTab] = useState(isHr ? 'applicants' : 'onboarding');
  const [summary, setSummary] = useState({});
  const [data, setData] = useState({});
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');

  const empty = useMemo(() => ({
    applicant: { candidateName: '', email: '', phone: '', jobTitle: '', source: 'direct', notes: '' },
    onboarding: { employeeUserId: '', title: '', description: '', dueDate: '' },
    goal: { userId: '', title: '', description: '', metricName: '', targetValue: '1', startDate: today(), endDate: today() },
    expense: { category: '', description: '', amount: '', currencyCode: 'EGP', expenseDate: today(), receiptReference: '' },
    asset: { assetTag: '', name: '', category: '', serialNumber: '', notes: '' },
    benefit: { name: '', providerName: '', description: '', employeeContribution: '0', employerContribution: '0', currencyCode: 'EGP', effectiveFrom: today(), effectiveTo: '' },
  }), []);
  const [forms, setForms] = useState(empty);

  const load = useCallback(async () => {
    setLoading(true); setError('');
    try {
      const calls = [
        service.getSummary(),
        service.getOnboarding(),
        service.getGoals(),
        service.getExpenses(),
        service.getBenefitPlans(),
        service.getBenefitEnrollments(),
      ];
      calls.push(service.getAssets());
      if (isHr) calls.push(service.getApplicants());
      const results = await Promise.all(calls);
      const next = {
        summary: results[0].data,
        onboarding: results[1].data.items || [],
        goals: results[2].data.items || [],
        expenses: results[3].data.items || [],
        benefitPlans: results[4].data.items || [],
        enrollments: results[5].data.items || [],
        applicants: isHr ? results[7].data.items || [] : [],
        assets: results[6].data.items || [],
      };
      setSummary(next.summary); setData(next);
    } catch (e) {
      setError(e.response?.data?.message || 'Could not load HR workforce data. Check permissions and database migrations.');
    } finally { setLoading(false); }
  }, [isHr]);

  useEffect(() => { load(); }, [load]);

  const updateForm = (form, field, value) => setForms(prev => ({ ...prev, [form]: { ...prev[form], [field]: value } }));
  const run = async (action, successText) => {
    setBusy(true); setError(''); setMessage('');
    try { await action(); setMessage(successText); await load(); }
    catch (e) { setError(e.response?.data?.message || 'The operation could not be completed.'); }
    finally { setBusy(false); }
  };

  const submit = (event, action, successText) => {
    event.preventDefault();
    return run(action, successText);
  };

  const field = (label, value, onChange, type = 'text', required = false, options = {}) => (
    <label className="block text-sm text-text-secondary">
      <span className="mb-1 block">{label}</span>
      <input type={type} value={value ?? ''} onChange={e => onChange(e.target.value)} required={required}
        min={options.min} max={options.max} step={options.step}
        className="w-full rounded-lg border border-border bg-surface px-3 py-2 text-text-primary outline-none focus:border-orange" />
    </label>
  );

  const status = value => <Badge variant={statusTone(value)}>{value || '—'}</Badge>;
  const canReview = isHr || ['Manager', 'Supervisor'].includes(user?.role);

  return (
    <AppLayout title={isHr ? 'HR Workforce' : 'My People & Benefits'}>
      <div className="mb-5 flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="text-xl font-semibold text-text-primary">People operations workspace</h2>
          <p className="mt-1 text-sm text-text-muted">Recruitment, onboarding, goals, expense claims, assets and benefits — scoped to your company and reporting access.</p>
        </div>
        <Button variant="secondary" onClick={load} disabled={loading}><RefreshCw size={16} className="mr-2" />Refresh</Button>
      </div>

      {error && <div className="mb-4 rounded-lg border border-danger/30 bg-danger/10 p-3 text-sm text-danger">{error}</div>}
      {message && <div className="mb-4 rounded-lg border border-success/30 bg-success/10 p-3 text-sm text-success">{message}</div>}

      <div className="mb-5 grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard label="Open onboarding" value={summary.openOnboardingTasks ?? '—'} icon={ClipboardCheck} />
        <StatCard label="Pending expenses" value={summary.pendingExpenseClaims ?? '—'} icon={Receipt} />
        <StatCard label="Active goals" value={summary.activeGoals ?? '—'} icon={Target} />
        {isHr && <StatCard label="Applicants in pipeline" value={summary.applicantsInPipeline ?? '—'} icon={Users} />}
      </div>

      <div className="mb-4 flex gap-2 overflow-x-auto border-b border-border pb-2">
        {TABS.filter(item => isHr || ['onboarding', 'goals', 'expenses', 'assets', 'benefits'].includes(item.key)).map(item => {
          const Icon = item.icon;
          return <button key={item.key} onClick={() => setTab(item.key)} className={`flex shrink-0 items-center gap-2 rounded-lg px-3 py-2 text-sm ${tab === item.key ? 'bg-orange/15 text-orange' : 'text-text-secondary hover:bg-surface-hover'}`}><Icon size={16} />{item.label}</button>;
        })}
      </div>

      {loading ? <div className="py-16 text-center text-text-secondary">Loading workforce data…</div> : (
        <>
          {tab === 'applicants' && isHr && <div className="grid gap-4 xl:grid-cols-[360px_1fr]">
            <Card><h3 className="mb-3 font-semibold text-text-primary">Add applicant</h3>
              <form className="space-y-3" onSubmit={e => submit(e, () => service.createApplicant(forms.applicant), 'Applicant added.')}>
                {field('Candidate name', forms.applicant.candidateName, v => updateForm('applicant','candidateName',v), 'text', true)}
                {field('Job title', forms.applicant.jobTitle, v => updateForm('applicant','jobTitle',v), 'text', true)}
                {field('Email', forms.applicant.email, v => updateForm('applicant','email',v), 'email')}
                {field('Phone', forms.applicant.phone, v => updateForm('applicant','phone',v))}
                {field('Source', forms.applicant.source, v => updateForm('applicant','source',v))}
                <label className="block text-sm text-text-secondary">Notes<textarea value={forms.applicant.notes} onChange={e => updateForm('applicant','notes',e.target.value)} className="mt-1 w-full rounded-lg border border-border bg-surface p-3 text-text-primary" rows={3} /></label>
                <Button type="submit" disabled={busy}>Create applicant</Button>
              </form>
            </Card>
            <Card><h3 className="mb-3 font-semibold text-text-primary">Applicant pipeline</h3>
              <div className="space-y-3">{(data.applicants || []).map(x => <div key={x.jobApplicantId} className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-border p-3">
                <div><p className="font-medium text-text-primary">{x.candidateName} — {x.jobTitle}</p><p className="text-xs text-text-muted">{x.email} · {x.phone}</p></div>
                <div className="flex items-center gap-2">{status(x.stage)}
                  <select value={x.stage} onChange={e => run(() => service.updateApplicantStage(x.jobApplicantId,e.target.value), 'Applicant stage updated.')} className="rounded border border-border bg-surface px-2 py-1 text-sm text-text-primary">
                    {['applied','screening','interview','offer','hired','rejected'].map(s => <option key={s}>{s}</option>)}
                  </select>
                </div>
              </div>)}{!data.applicants?.length && <p className="py-8 text-center text-sm text-text-muted">No applicants have been added.</p>}</div>
            </Card>
          </div>}

          {tab === 'onboarding' && <div className="grid gap-4 xl:grid-cols-[360px_1fr]">
            {isHr && <Card><h3 className="mb-3 font-semibold text-text-primary">Assign onboarding task</h3><form className="space-y-3" onSubmit={e => submit(e, () => service.createOnboardingTask({ ...forms.onboarding, employeeUserId:Number(forms.onboarding.employeeUserId), dueDate:forms.onboarding.dueDate || null }), 'Onboarding task created.')}>
              {field('Employee user ID',forms.onboarding.employeeUserId,v=>updateForm('onboarding','employeeUserId',v),'number',true,{min:1})}
              {field('Task title',forms.onboarding.title,v=>updateForm('onboarding','title',v),'text',true)}
              {field('Due date',forms.onboarding.dueDate,v=>updateForm('onboarding','dueDate',v),'date')}
              <label className="block text-sm text-text-secondary">Description<textarea value={forms.onboarding.description} onChange={e=>updateForm('onboarding','description',e.target.value)} className="mt-1 w-full rounded-lg border border-border bg-surface p-3 text-text-primary" rows={3}/></label>
              <Button type="submit" disabled={busy}>Assign task</Button>
            </form></Card>}
            <Card><h3 className="mb-3 font-semibold text-text-primary">Onboarding checklist</h3><div className="space-y-2">{(data.onboarding||[]).map(x=><div key={x.onboardingTaskId} className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-border p-3"><div><p className="font-medium text-text-primary">{x.title}</p><p className="text-xs text-text-muted">Employee #{x.employeeUserId} · Due {x.dueDate||'—'}</p></div><div className="flex items-center gap-2">{status(x.status)}{x.status==='open'&&<Button size="sm" variant="secondary" onClick={()=>run(()=>service.completeOnboardingTask(x.onboardingTaskId),'Onboarding task completed.')}>Complete</Button>}</div></div>)}{!data.onboarding?.length&&<p className="py-8 text-center text-sm text-text-muted">No onboarding tasks in your scope.</p>}</div></Card>
          </div>}

          {tab === 'goals' && <div className="grid gap-4 xl:grid-cols-[360px_1fr]">
            <Card><h3 className="mb-3 font-semibold text-text-primary">Create performance goal</h3><form className="space-y-3" onSubmit={e => submit(e, () => service.createGoal({ ...forms.goal, userId: forms.goal.userId ? Number(forms.goal.userId) : null, targetValue:Number(forms.goal.targetValue) }), 'Performance goal created.')}>
              {field('Employee user ID (optional)',forms.goal.userId,v=>updateForm('goal','userId',v),'number')}
              {field('Goal title',forms.goal.title,v=>updateForm('goal','title',v),'text',true)}
              {field('Metric',forms.goal.metricName,v=>updateForm('goal','metricName',v))}
              {field('Target',forms.goal.targetValue,v=>updateForm('goal','targetValue',v),'number',true,{min:0.01,step:0.01})}
              <div className="grid grid-cols-2 gap-2">{field('Start',forms.goal.startDate,v=>updateForm('goal','startDate',v),'date',true)}{field('End',forms.goal.endDate,v=>updateForm('goal','endDate',v),'date',true)}</div>
              <label className="block text-sm text-text-secondary">Description<textarea value={forms.goal.description} onChange={e=>updateForm('goal','description',e.target.value)} className="mt-1 w-full rounded-lg border border-border bg-surface p-3 text-text-primary" rows={2}/></label>
              <Button type="submit" disabled={busy}>Create goal</Button>
            </form></Card>
            <Card><h3 className="mb-3 font-semibold text-text-primary">Goals and progress</h3><div className="space-y-3">{(data.goals||[]).map(x=><div key={x.performanceGoalId} className="rounded-lg border border-border p-3"><div className="flex flex-wrap items-center justify-between gap-2"><div><p className="font-medium text-text-primary">{x.title}</p><p className="text-xs text-text-muted">Employee #{x.userId} · {x.currentValue}/{x.targetValue} {x.metricName}</p></div>{status(x.status)}</div><div className="mt-2 flex items-center gap-2">{field('Update value',x.currentValue,v=>setData(prev=>({...prev,goals:prev.goals.map(g=>g.performanceGoalId===x.performanceGoalId?{...g,currentValue:v}:g)})),'number',false,{min:0,step:0.01})}<Button size="sm" variant="secondary" onClick={()=>run(()=>service.updateGoalProgress(x.performanceGoalId,{currentValue:Number(x.currentValue)}),'Goal progress saved.')}>Save</Button></div></div>)}{!data.goals?.length&&<p className="py-8 text-center text-sm text-text-muted">No goals in your scope.</p>}</div></Card>
          </div>}

          {tab === 'expenses' && <div className="grid gap-4 xl:grid-cols-[360px_1fr]">
            <Card><h3 className="mb-3 font-semibold text-text-primary">Submit expense claim</h3><form className="space-y-3" onSubmit={e=>submit(e,()=>service.createExpense({...forms.expense,amount:Number(forms.expense.amount)}),'Expense claim submitted.')}>
              {field('Category',forms.expense.category,v=>updateForm('expense','category',v),'text',true)}
              {field('Amount',forms.expense.amount,v=>updateForm('expense','amount',v),'number',true,{min:0.01,step:0.01})}
              {field('Currency',forms.expense.currencyCode,v=>updateForm('expense','currencyCode',v),'text',true)}
              {field('Expense date',forms.expense.expenseDate,v=>updateForm('expense','expenseDate',v),'date',true)}
              {field('Receipt reference',forms.expense.receiptReference,v=>updateForm('expense','receiptReference',v))}
              <label className="block text-sm text-text-secondary">Description<textarea value={forms.expense.description} onChange={e=>updateForm('expense','description',e.target.value)} className="mt-1 w-full rounded-lg border border-border bg-surface p-3 text-text-primary" rows={2}/></label>
              <Button type="submit" disabled={busy}>Submit claim</Button>
            </form></Card>
            <Card><h3 className="mb-3 font-semibold text-text-primary">Expense claims</h3><div className="space-y-2">{(data.expenses||[]).map(x=><div key={x.expenseClaimId} className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-border p-3"><div><p className="font-medium text-text-primary">{x.category} · {x.amount} {x.currencyCode}</p><p className="text-xs text-text-muted">Employee #{x.userId} · {x.expenseDate} · {x.description}</p></div><div className="flex items-center gap-2">{status(x.status)}{canReview&&x.status==='pending'&&x.userId!==user?.id&&<><Button size="sm" onClick={()=>run(()=>service.decideExpense(x.expenseClaimId,{decision:'approved'}),'Expense approved.')}>Approve</Button><Button size="sm" color="danger" variant="secondary" onClick={()=>run(()=>service.decideExpense(x.expenseClaimId,{decision:'rejected'}),'Expense rejected.')}>Reject</Button></>}</div></div>)}{!data.expenses?.length&&<p className="py-8 text-center text-sm text-text-muted">No expense claims in your scope.</p>}</div></Card>
          </div>}

          {tab === 'assets' && !isHr && <Card><h3 className="mb-3 font-semibold text-text-primary">My assigned company assets</h3><div className="space-y-2">{(data.assets||[]).map(x=><div key={x.companyAssetId} className="rounded-lg border border-border p-3"><div className="flex flex-wrap items-center justify-between gap-2"><p className="font-medium text-text-primary">{x.name} · {x.assetTag}</p>{status(x.status)}</div><p className="mt-1 text-xs text-text-muted">{x.category} · {x.serialNumber||'No serial number'}</p></div>)}{!data.assets?.length&&<p className="py-8 text-center text-sm text-text-muted">No company assets are assigned to you.</p>}</div></Card>}
          {tab === 'assets' && isHr && <div className="grid gap-4 xl:grid-cols-[360px_1fr]">
            <Card><h3 className="mb-3 font-semibold text-text-primary">Register company asset</h3><form className="space-y-3" onSubmit={e=>submit(e,()=>service.createAsset(forms.asset),'Asset registered.')}>
              {field('Asset tag',forms.asset.assetTag,v=>updateForm('asset','assetTag',v),'text',true)}
              {field('Name',forms.asset.name,v=>updateForm('asset','name',v),'text',true)}
              {field('Category',forms.asset.category,v=>updateForm('asset','category',v))}
              {field('Serial number',forms.asset.serialNumber,v=>updateForm('asset','serialNumber',v))}
              {field('Notes',forms.asset.notes,v=>updateForm('asset','notes',v))}
              <Button type="submit" disabled={busy}>Register asset</Button>
            </form></Card>
            <Card><h3 className="mb-3 font-semibold text-text-primary">Asset register</h3><div className="space-y-2">{(data.assets||[]).map(x=><div key={x.companyAssetId} className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-border p-3"><div><p className="font-medium text-text-primary">{x.name} · {x.assetTag}</p><p className="text-xs text-text-muted">{x.category} · {x.serialNumber||'No serial'} · Employee #{x.assignedToUserId||'—'}</p></div><div className="flex items-center gap-2">{status(x.status)}{x.status==='available'&&<Button size="sm" variant="secondary" onClick={()=>{const employeeUserId=Number(window.prompt('Employee user ID to assign this asset:'));if(employeeUserId>0)run(()=>service.assignAsset(x.companyAssetId,employeeUserId),'Asset assigned.');}}>Assign</Button>}{x.status==='assigned'&&<Button size="sm" variant="secondary" onClick={()=>run(()=>service.returnAsset(x.companyAssetId),'Asset returned.')}>Return</Button>}</div></div>)}{!data.assets?.length&&<p className="py-8 text-center text-sm text-text-muted">No assets registered.</p>}</div></Card>
          </div>}

          {tab === 'benefits' && <div className="grid gap-4 xl:grid-cols-2">
            {isHr&&<Card><h3 className="mb-3 font-semibold text-text-primary">Create benefit plan</h3><form className="grid gap-3 md:grid-cols-2" onSubmit={e=>submit(e,()=>service.createBenefitPlan({...forms.benefit,employeeContribution:Number(forms.benefit.employeeContribution),employerContribution:Number(forms.benefit.employerContribution),effectiveTo:forms.benefit.effectiveTo||null}),'Benefit plan created.')}>
              {field('Plan name',forms.benefit.name,v=>updateForm('benefit','name',v),'text',true)}
              {field('Provider',forms.benefit.providerName,v=>updateForm('benefit','providerName',v))}
              {field('Employee contribution',forms.benefit.employeeContribution,v=>updateForm('benefit','employeeContribution',v),'number',true,{min:0,step:0.01})}
              {field('Employer contribution',forms.benefit.employerContribution,v=>updateForm('benefit','employerContribution',v),'number',true,{min:0,step:0.01})}
              {field('Currency',forms.benefit.currencyCode,v=>updateForm('benefit','currencyCode',v),'text',true)}
              {field('Effective from',forms.benefit.effectiveFrom,v=>updateForm('benefit','effectiveFrom',v),'date',true)}
              {field('Effective to',forms.benefit.effectiveTo,v=>updateForm('benefit','effectiveTo',v),'date')}
              <div className="md:col-span-2"><label className="block text-sm text-text-secondary">Description<textarea value={forms.benefit.description} onChange={e=>updateForm('benefit','description',e.target.value)} className="mt-1 w-full rounded-lg border border-border bg-surface p-3 text-text-primary" rows={2}/></label></div>
              <div className="md:col-span-2"><Button type="submit" disabled={busy}>Create plan</Button></div>
            </form></Card>}
            <Card><h3 className="mb-3 font-semibold text-text-primary">Available benefit plans</h3><div className="space-y-2">{(data.benefitPlans||[]).map(x=><div key={x.benefitPlanId} className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-border p-3"><div><p className="font-medium text-text-primary">{x.name}</p><p className="text-xs text-text-muted">{x.providerName} · Employee {x.employeeContribution} / Employer {x.employerContribution} {x.currencyCode}</p></div><Button size="sm" variant="secondary" onClick={()=>run(()=>service.enrollBenefit(x.benefitPlanId),'Enrollment request submitted.')}>Enroll</Button></div>)}{!data.benefitPlans?.length&&<p className="py-8 text-center text-sm text-text-muted">No active benefit plans are available.</p>}</div></Card>
            <Card><h3 className="mb-3 font-semibold text-text-primary">Benefit enrollments</h3><div className="space-y-2">{(data.enrollments||[]).map(x=><div key={x.benefitEnrollmentId} className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-border p-3"><div><p className="font-medium text-text-primary">{x.planName}</p><p className="text-xs text-text-muted">Employee #{x.userId} · {x.createdAt?new Date(x.createdAt).toLocaleDateString():''}</p></div><div className="flex items-center gap-2">{status(x.status)}{isHr&&x.status==='pending'&&<><Button size="sm" onClick={()=>run(()=>service.decideBenefitEnrollment(x.benefitEnrollmentId,{decision:'active'}),'Enrollment approved.')}>Approve</Button><Button size="sm" variant="secondary" color="danger" onClick={()=>run(()=>service.decideBenefitEnrollment(x.benefitEnrollmentId,{decision:'declined'}),'Enrollment declined.')}>Decline</Button></>}</div></div>)}{!data.enrollments?.length&&<p className="py-8 text-center text-sm text-text-muted">No benefit enrollments found.</p>}</div></Card>
          </div>}
        </>
      )}
    </AppLayout>
  );
}
