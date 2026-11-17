import { useCallback, useEffect, useState } from 'react';
import AppLayout from '../../layouts/AppLayout';
import { Badge, Button, Card, StatCard } from '../../components/ui';
import { payrollService as service } from '../../services/payrollService';
import { useAuth } from '../../contexts/AuthContext';
import { CreditCard, FileText, Wallet, RefreshCw } from 'lucide-react';

const today = new Date().toISOString().slice(0, 10);
const statusTone = (status) => ({ draft: 'warning', approved: 'info', paid: 'success' }[status] || 'default');
const money = (amount, currency) => `${Number(amount || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })} ${currency || ''}`;

export default function PayrollCenter() {
  const { user } = useAuth();
  const isHr = ['HR', 'CEO', 'Admin'].includes(user?.role);
  const [periods, setPeriods] = useState([]);
  const [selectedPeriod, setSelectedPeriod] = useState('');
  const [slips, setSlips] = useState([]);
  const [mySlips, setMySlips] = useState([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [periodForm, setPeriodForm] = useState({ periodStart: today.slice(0, 8) + '01', periodEnd: today, currencyCode: 'EGP' });
  const [payForm, setPayForm] = useState({ userId: '', baseSalary: '', allowances: '0', bonus: '0', incomeTaxDeduction: '0', socialInsuranceDeduction: '0', otherDeductions: '0', currencyCode: 'EGP', notes: '' });

  const load = useCallback(async () => {
    setLoading(true); setError('');
    try {
      if (isHr) {
        const { data } = await service.getPeriods();
        setPeriods(data.items || []);
        if (!selectedPeriod && data.items?.length) setSelectedPeriod(String(data.items[0].payrollPeriodId));
      } else {
        const { data } = await service.getMyPayslips();
        setMySlips(data.items || []);
      }
    } catch (e) {
      setError(e.response?.data?.message || 'Payroll data could not be loaded. Confirm the payroll migration and your plan permissions.');
    } finally { setLoading(false); }
  }, [isHr, selectedPeriod]);

  useEffect(() => { load(); }, [load]);

  const loadSlips = useCallback(async () => {
    if (!isHr || !selectedPeriod) { setSlips([]); return; }
    try {
      const { data } = await service.getPeriodPayslips(Number(selectedPeriod));
      setSlips(data.items || []);
    } catch (e) { setError(e.response?.data?.message || 'Could not load payslips.'); }
  }, [isHr, selectedPeriod]);

  useEffect(() => { loadSlips(); }, [loadSlips]);

  const run = async (action, success) => {
    setBusy(true); setError(''); setNotice('');
    try { await action(); setNotice(success); await load(); await loadSlips(); }
    catch (e) { setError(e.response?.data?.message || 'Payroll action failed.'); }
    finally { setBusy(false); }
  };

  const totalCurrency = isHr ? selected?.currencyCode : mySlips[0]?.currencyCode;
  const totalNet = isHr
    ? slips.reduce((sum, slip) => sum + Number(slip.netPay || 0), 0)
    : mySlips.filter(slip => slip.currencyCode === totalCurrency).reduce((sum, slip) => sum + Number(slip.netPay || 0), 0);
  const selected = periods.find(x => String(x.payrollPeriodId) === selectedPeriod);

  return (
    <AppLayout title={isHr ? 'Payroll Administration' : 'My Payslips'}>
      <div className="mb-5 flex flex-wrap items-start justify-between gap-3">
        <div><h2 className="text-xl font-semibold text-text-primary">{isHr ? 'Payroll workspace' : 'My payslips'}</h2>
          <p className="mt-1 text-sm text-text-muted">Pay components and statutory deductions are entered by authorised payroll staff; TaskTune calculates gross and net totals only.</p></div>
        <Button variant="secondary" onClick={load} disabled={loading}><RefreshCw size={16} className="mr-2" />Refresh</Button>
      </div>
      {error && <div className="mb-4 rounded-lg border border-danger/30 bg-danger/10 p-3 text-sm text-danger">{error}</div>}
      {notice && <div className="mb-4 rounded-lg border border-success/30 bg-success/10 p-3 text-sm text-success">{notice}</div>}
      <div className="mb-5 grid gap-3 sm:grid-cols-3">
        <StatCard label={isHr ? 'Payslips in period' : 'My payslips'} value={isHr ? slips.length : mySlips.length} icon={FileText} />
        <StatCard label="Net pay total" value={money(totalNet, selected?.currencyCode || mySlips[0]?.currencyCode || 'EGP')} icon={Wallet} />
        <StatCard label={isHr ? 'Payroll periods' : 'Paid slips'} value={isHr ? periods.length : mySlips.filter(x => x.status === 'paid').length} icon={CreditCard} />
      </div>

      {loading ? <div className="py-16 text-center text-text-secondary">Loading payroll…</div> : isHr ? (
        <div className="grid gap-4 xl:grid-cols-[340px_1fr]">
          <div className="space-y-4">
            <Card><h3 className="mb-3 font-semibold text-text-primary">Create payroll period</h3>
              <form className="space-y-3" onSubmit={e => { e.preventDefault(); run(() => service.createPeriod(periodForm), 'Payroll period created.'); }}>
                <label className="block text-sm text-text-secondary">Period start<input type="date" required value={periodForm.periodStart} onChange={e => setPeriodForm(p => ({...p, periodStart:e.target.value}))} className="mt-1 w-full rounded-lg border border-border bg-bg px-3 py-2 text-text-primary" /></label>
                <label className="block text-sm text-text-secondary">Period end<input type="date" required value={periodForm.periodEnd} onChange={e => setPeriodForm(p => ({...p, periodEnd:e.target.value}))} className="mt-1 w-full rounded-lg border border-border bg-bg px-3 py-2 text-text-primary" /></label>
                <label className="block text-sm text-text-secondary">Currency<input required maxLength={3} value={periodForm.currencyCode} onChange={e => setPeriodForm(p => ({...p, currencyCode:e.target.value.toUpperCase()}))} className="mt-1 w-full rounded-lg border border-border bg-bg px-3 py-2 text-text-primary" /></label>
                <Button type="submit" disabled={busy}>Create period</Button>
              </form>
            </Card>
            <Card><h3 className="mb-3 font-semibold text-text-primary">Payroll periods</h3>
              <div className="space-y-2">{periods.map(p => <button key={p.payrollPeriodId} onClick={() => setSelectedPeriod(String(p.payrollPeriodId))} className={`w-full rounded-lg border p-3 text-left ${selectedPeriod===String(p.payrollPeriodId)?'border-orange bg-orange/10':'border-border hover:bg-surface-hover'}`}>
                <div className="flex items-center justify-between gap-2"><span className="font-medium text-text-primary">#{p.payrollPeriodId} · {p.periodStart} – {p.periodEnd}</span><Badge variant={statusTone(p.status)}>{p.status}</Badge></div>
                <p className="mt-1 text-xs text-text-muted">{p.currencyCode}</p>
              </button>)}{!periods.length&&<p className="py-6 text-center text-sm text-text-muted">No payroll periods created.</p>}</div>
            </Card>
          </div>
          <div className="space-y-4">
            {selected && <Card><div className="mb-4 flex flex-wrap items-center justify-between gap-3"><div><h3 className="font-semibold text-text-primary">Period #{selected.payrollPeriodId}</h3><p className="text-xs text-text-muted">{selected.periodStart} – {selected.periodEnd} · {selected.currencyCode}</p></div>
              {selected.status === 'draft' && <Button disabled={busy || slips.length===0} onClick={() => run(() => service.approvePeriod(selected.payrollPeriodId), 'Payroll period approved.')}>Approve period</Button>}
            </div>
            {selected.status==='draft' && <form className="mb-5 grid gap-3 rounded-lg border border-border p-4 sm:grid-cols-2 lg:grid-cols-3" onSubmit={e => { e.preventDefault(); run(() => service.createPayslip(selected.payrollPeriodId, {...payForm,userId:Number(payForm.userId),baseSalary:Number(payForm.baseSalary),allowances:Number(payForm.allowances),bonus:Number(payForm.bonus),incomeTaxDeduction:Number(payForm.incomeTaxDeduction),socialInsuranceDeduction:Number(payForm.socialInsuranceDeduction),otherDeductions:Number(payForm.otherDeductions),currencyCode:selected.currencyCode}), 'Payslip added.'); }}>
              <h4 className="sm:col-span-2 lg:col-span-3 font-medium text-text-primary">Add employee payslip (enter employee user ID)</h4>
              {Object.entries({userId:'Employee ID',baseSalary:'Base salary',allowances:'Allowances',bonus:'Bonus',incomeTaxDeduction:'Income tax deduction',socialInsuranceDeduction:'Social insurance deduction',otherDeductions:'Other deductions'}).map(([key,label]) => <label key={key} className="block text-sm text-text-secondary">{label}<input type="number" min={key==='userId'?1:0} step={key==='userId'?1:'0.01'} required value={payForm[key]} onChange={e=>setPayForm(p=>({...p,[key]:e.target.value}))} className="mt-1 w-full rounded-lg border border-border bg-bg px-3 py-2 text-text-primary" /></label>)}
              <label className="block text-sm text-text-secondary">Currency<input maxLength={3} value={payForm.currencyCode} onChange={e=>setPayForm(p=>({...p,currencyCode:e.target.value.toUpperCase()}))} className="mt-1 w-full rounded-lg border border-border bg-bg px-3 py-2 text-text-primary" /></label>
              <label className="block text-sm text-text-secondary sm:col-span-2">Notes<input value={payForm.notes} onChange={e=>setPayForm(p=>({...p,notes:e.target.value}))} className="mt-1 w-full rounded-lg border border-border bg-bg px-3 py-2 text-text-primary" /></label>
              <div className="sm:col-span-2 lg:col-span-3"><Button type="submit" disabled={busy}>Add payslip</Button></div>
            </form>}
            <div className="space-y-2">{slips.map(s => <div key={s.payslipId} className="rounded-lg border border-border p-3">
              <div className="flex flex-wrap items-start justify-between gap-3"><div><p className="font-medium text-text-primary">{s.employeeName} · Employee #{s.userId}</p><p className="mt-1 text-xs text-text-muted">Gross {money(s.grossPay,s.currencyCode)} · Tax {money(s.incomeTaxDeduction,s.currencyCode)} · Insurance {money(s.socialInsuranceDeduction,s.currencyCode)} · Other {money(s.otherDeductions,s.currencyCode)}</p><p className="mt-1 text-sm font-semibold text-text-primary">Net pay: {money(s.netPay,s.currencyCode)}</p></div><div className="flex items-center gap-2"><Badge variant={statusTone(s.status)}>{s.status}</Badge>{s.status==='approved'&&<Button size="sm" onClick={()=>run(()=>service.markPaid(s.payslipId),'Payslip marked paid.')}>Mark paid</Button>}</div></div>
            </div>)}{!slips.length&&<p className="py-8 text-center text-sm text-text-muted">No payslips have been added for this period.</p>}</div>
            </Card>}
          </div>
        </div>
      ) : (
        <Card><h3 className="mb-3 font-semibold text-text-primary">Payslip history</h3><div className="space-y-2">{mySlips.map(s=><div key={s.payslipId} className="rounded-lg border border-border p-4">
          <div className="flex flex-wrap items-center justify-between gap-2"><p className="font-medium text-text-primary">{s.periodStart} – {s.periodEnd}</p><Badge variant={statusTone(s.status)}>{s.status}</Badge></div>
          <div className="mt-3 grid gap-2 text-sm sm:grid-cols-2 lg:grid-cols-4"><p className="text-text-secondary">Base: {money(s.baseSalary,s.currencyCode)}</p><p className="text-text-secondary">Allowances: {money(s.allowances,s.currencyCode)}</p><p className="text-text-secondary">Bonus: {money(s.bonus,s.currencyCode)}</p><p className="text-text-secondary">Gross: {money(s.grossPay,s.currencyCode)}</p><p className="text-text-secondary">Tax: {money(s.incomeTaxDeduction,s.currencyCode)}</p><p className="text-text-secondary">Insurance: {money(s.socialInsuranceDeduction,s.currencyCode)}</p><p className="text-text-secondary">Other: {money(s.otherDeductions,s.currencyCode)}</p><p className="font-semibold text-text-primary">Net: {money(s.netPay,s.currencyCode)}</p></div>
        </div>)}{!mySlips.length&&<p className="py-8 text-center text-sm text-text-muted">No payslips are available yet.</p>}</div></Card>
      )}
    </AppLayout>
  );
}
