import { useCallback, useEffect, useMemo, useState } from 'react';
import AppLayout from '../../layouts/AppLayout';
import { Badge, Button, Card, StatCard } from '../../components/ui';
import { useAuth } from '../../contexts/AuthContext';
import { leaveService } from '../../services/leaveService';
import { userService } from '../../services/userService';
import {
  CalendarDays, CheckCircle2, Clock3, XCircle, Plus, RefreshCw,
  Umbrella, Users, Settings2, CalendarCheck, AlertCircle
} from 'lucide-react';

const MANAGER_ROLES = ['Supervisor', 'Manager', 'HR', 'CEO', 'Admin'];
const HR_ROLES = ['HR', 'CEO', 'Admin'];
const currentYear = () => new Date().getFullYear();

const formatDate = (value) => {
  if (!value) return '—';
  return new Date(`${value}T00:00:00`).toLocaleDateString();
};

const statusVariant = (status) => ({
  pending: 'warning',
  approved: 'success',
  rejected: 'danger',
  cancelled: 'default',
}[String(status || '').toLowerCase()] || 'default');

export default function LeaveCenter() {
  const { user } = useAuth();
  const role = user?.role || 'Employee';
  const canReview = MANAGER_ROLES.includes(role);
  const canManage = HR_ROLES.includes(role);

  const [year, setYear] = useState(currentYear());
  const [types, setTypes] = useState([]);
  const [balances, setBalances] = useState([]);
  const [requests, setRequests] = useState([]);
  const [inbox, setInbox] = useState([]);
  const [holidays, setHolidays] = useState([]);
  const [employees, setEmployees] = useState([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const [requestForm, setRequestForm] = useState({
    leaveTypeId: '',
    startDate: '',
    endDate: '',
    reason: '',
  });
  const [decisionComments, setDecisionComments] = useState({});

  const [showSetup, setShowSetup] = useState(false);
  const [typeForm, setTypeForm] = useState({
    code: '',
    name: '',
    description: '',
    isPaid: true,
    requiresApproval: true,
    annualAllowanceDays: '0',
    maximumConsecutiveDays: '0',
  });
  const [balanceForm, setBalanceForm] = useState({
    userId: '',
    leaveTypeId: '',
    allocatedDays: '',
    carriedOverDays: '0',
  });
  const [holidayForm, setHolidayForm] = useState({ name: '', holidayDate: '', isRecurring: false });

  const load = useCallback(async (silent = false) => {
    if (silent) setRefreshing(true);
    else setLoading(true);
    setError('');
    try {
      const baseCalls = [
        leaveService.getTypes(),
        leaveService.getMyBalances(year),
        leaveService.getMyRequests(1, 100),
        leaveService.getHolidays(year),
      ];
      if (canReview) baseCalls.push(leaveService.getApprovalInbox(1, 100));
      if (canManage) baseCalls.push(userService.getUsers(1, 100));

      const results = await Promise.all(baseCalls);
      setTypes(results[0].data?.items || []);
      setBalances(results[1].data?.items || []);
      setRequests(results[2].data?.items || []);
      setHolidays(results[3].data?.items || []);
      let cursor = 4;
      setInbox(canReview ? (results[cursor++].data?.items || []) : []);
      const directory = canManage ? results[cursor]?.data : null;
      const directoryItems = Array.isArray(directory) ? directory : directory?.items || [];
      setEmployees(directoryItems.map((person) => ({
        id: person.userId ?? person.id,
        name: person.fullName || [person.firstName, person.lastName].filter(Boolean).join(' '),
        department: person.departmentName || person.department || 'Unassigned',
      })));
    } catch (err) {
      setError(err.response?.data?.message || 'Unable to load leave information. Check your connection and try again.');
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [year, canReview, canManage]);

  useEffect(() => {
    load();
  }, [load]);

  useEffect(() => {
    if (success) {
      const handle = window.setTimeout(() => setSuccess(''), 4000);
      return () => window.clearTimeout(handle);
    }
  }, [success]);

  const availableDays = useMemo(
    () => balances.reduce((sum, item) => sum + Number(item.availableDays || 0), 0),
    [balances],
  );
  const pendingDays = useMemo(
    () => balances.reduce((sum, item) => sum + Number(item.pendingDays || 0), 0),
    [balances],
  );
  const approvedRequests = useMemo(
    () => requests.filter((item) => item.status === 'approved').length,
    [requests],
  );

  const createLeaveRequest = async (event) => {
    event.preventDefault();
    setSubmitting(true);
    setError('');
    try {
      await leaveService.createRequest({
        leaveTypeId: Number(requestForm.leaveTypeId),
        startDate: requestForm.startDate,
        endDate: requestForm.endDate,
        reason: requestForm.reason.trim(),
      });
      setRequestForm({ leaveTypeId: '', startDate: '', endDate: '', reason: '' });
      setSuccess('Leave request submitted.');
      await load(true);
    } catch (err) {
      setError(err.response?.data?.message || 'Unable to submit this leave request.');
    } finally {
      setSubmitting(false);
    }
  };

  const cancelRequest = async (requestId) => {
    const reason = window.prompt('Optional cancellation note:') || '';
    try {
      await leaveService.cancelRequest(requestId, reason);
      setSuccess('Pending leave request cancelled.');
      await load(true);
    } catch (err) {
      setError(err.response?.data?.message || 'Unable to cancel this request.');
    }
  };

  const decideRequest = async (requestId, approve) => {
    setError('');
    try {
      await leaveService.decideRequest(requestId, {
        approve,
        comment: decisionComments[requestId] || '',
      });
      setSuccess(approve ? 'Leave request approved.' : 'Leave request rejected.');
      setDecisionComments((current) => ({ ...current, [requestId]: '' }));
      await load(true);
    } catch (err) {
      setError(err.response?.data?.message || 'Unable to process this leave request.');
    }
  };

  const saveLeaveType = async (event) => {
    event.preventDefault();
    setSubmitting(true);
    setError('');
    try {
      await leaveService.createType({
        ...typeForm,
        annualAllowanceDays: Number(typeForm.annualAllowanceDays),
        maximumConsecutiveDays: Number(typeForm.maximumConsecutiveDays),
        requiresAttachment: false,
        isActive: true,
      });
      setTypeForm({
        code: '', name: '', description: '', isPaid: true, requiresApproval: true,
        annualAllowanceDays: '0', maximumConsecutiveDays: '0',
      });
      setSuccess('Leave type created. Allocate employee balances before accepting requests.');
      await load(true);
    } catch (err) {
      setError(err.response?.data?.message || 'Unable to create the leave type.');
    } finally {
      setSubmitting(false);
    }
  };

  const saveBalance = async (event) => {
    event.preventDefault();
    setSubmitting(true);
    setError('');
    try {
      await leaveService.setBalance(
        Number(balanceForm.userId),
        Number(balanceForm.leaveTypeId),
        year,
        {
          allocatedDays: Number(balanceForm.allocatedDays),
          carriedOverDays: Number(balanceForm.carriedOverDays || 0),
        },
      );
      setBalanceForm({ userId: '', leaveTypeId: '', allocatedDays: '', carriedOverDays: '0' });
      setSuccess('Employee leave balance saved.');
      await load(true);
    } catch (err) {
      setError(err.response?.data?.message || 'Unable to save the leave balance.');
    } finally {
      setSubmitting(false);
    }
  };

  const saveHoliday = async (event) => {
    event.preventDefault();
    setSubmitting(true);
    setError('');
    try {
      await leaveService.createHoliday(holidayForm);
      setHolidayForm({ name: '', holidayDate: '', isRecurring: false });
      setSuccess('Company holiday added.');
      await load(true);
    } catch (err) {
      setError(err.response?.data?.message || 'Unable to add the company holiday.');
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) {
    return (
      <AppLayout title="Leave Management">
        <div className="flex items-center justify-center h-64 text-text-secondary">Loading leave information…</div>
      </AppLayout>
    );
  }

  return (
    <AppLayout title="Leave Management">
      <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-3 mb-5">
        <div>
          <p className="text-sm text-text-secondary">Requests, balances, approvals and company holiday calendar</p>
        </div>
        <div className="flex items-center gap-2">
          <label htmlFor="leave-year" className="text-xs text-text-muted">Leave year</label>
          <select id="leave-year" value={year} onChange={(event) => setYear(Number(event.target.value))}
            className="bg-bg border border-border rounded-lg px-3 py-2 text-sm text-text-primary">
            {[currentYear() - 1, currentYear(), currentYear() + 1].map((value) => <option key={value}>{value}</option>)}
          </select>
          <Button variant="secondary" onClick={() => load(true)} disabled={refreshing}>
            <span className="inline-flex items-center gap-2"><RefreshCw size={14} className={refreshing ? 'animate-spin' : ''} /> Refresh</span>
          </Button>
        </div>
      </div>

      {error && (
        <div role="alert" className="flex items-start gap-2 p-3 mb-4 rounded-lg border border-danger/20 bg-danger/10 text-sm text-danger">
          <AlertCircle size={16} className="mt-0.5 shrink-0" /> {error}
        </div>
      )}
      {success && (
        <div role="status" className="flex items-start gap-2 p-3 mb-4 rounded-lg border border-success/20 bg-success/10 text-sm text-success">
          <CheckCircle2 size={16} className="mt-0.5 shrink-0" /> {success}
        </div>
      )}

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-6">
        <StatCard label="Available days" value={availableDays.toFixed(1)} icon={Umbrella} />
        <StatCard label="Pending days" value={pendingDays.toFixed(1)} icon={Clock3} />
        <StatCard label="Approved requests" value={approvedRequests} icon={CalendarCheck} />
      </div>

      <div className="grid xl:grid-cols-3 gap-5">
        <div className="xl:col-span-1 space-y-5">
          <Card>
            <div className="flex items-center gap-2 mb-4">
              <Plus size={18} className="text-orange" />
              <h2 className="font-semibold text-text-primary">Request leave</h2>
            </div>
            <form className="space-y-3" onSubmit={createLeaveRequest}>
              <div>
                <label className="block text-xs text-text-secondary mb-1">Leave type</label>
                <select required value={requestForm.leaveTypeId}
                  onChange={(event) => setRequestForm((current) => ({ ...current, leaveTypeId: event.target.value }))}
                  className="w-full bg-bg border border-border rounded-lg px-3 py-2.5 text-sm text-text-primary">
                  <option value="">Choose leave type…</option>
                  {types.map((type) => <option key={type.leaveTypeId} value={type.leaveTypeId}>{type.name}{type.isPaid ? ' · Paid' : ' · Unpaid'}</option>)}
                </select>
              </div>
              <div>
                <label className="block text-xs text-text-secondary mb-1">Start date</label>
                <input required type="date" value={requestForm.startDate}
                  onChange={(event) => setRequestForm((current) => ({ ...current, startDate: event.target.value }))}
                  className="w-full bg-bg border border-border rounded-lg px-3 py-2.5 text-sm text-text-primary" />
              </div>
              <div>
                <label className="block text-xs text-text-secondary mb-1">End date</label>
                <input required type="date" min={requestForm.startDate || undefined} value={requestForm.endDate}
                  onChange={(event) => setRequestForm((current) => ({ ...current, endDate: event.target.value }))}
                  className="w-full bg-bg border border-border rounded-lg px-3 py-2.5 text-sm text-text-primary" />
              </div>
              <div>
                <label className="block text-xs text-text-secondary mb-1">Reason</label>
                <textarea required rows={3} maxLength={2000} value={requestForm.reason}
                  onChange={(event) => setRequestForm((current) => ({ ...current, reason: event.target.value }))}
                  placeholder="Add a short explanation for your request"
                  className="w-full bg-bg border border-border rounded-lg px-3 py-2.5 text-sm text-text-primary resize-y" />
              </div>
              <Button type="submit" disabled={submitting || types.length === 0} className="w-full">
                {submitting ? 'Submitting…' : 'Submit request'}
              </Button>
              <p className="text-xs text-text-muted">Working days exclude Saturday, Sunday and configured company holidays. Requests spanning calendar years are not yet supported.</p>
            </form>
          </Card>

          <Card>
            <div className="flex items-center gap-2 mb-3">
              <CalendarDays size={18} className="text-orange" />
              <h2 className="font-semibold text-text-primary">Company holidays</h2>
            </div>
            {holidays.length === 0 ? (
              <p className="text-sm text-text-muted">No holidays configured for {year}.</p>
            ) : (
              <div className="space-y-2 max-h-64 overflow-y-auto">
                {holidays.map((holiday) => (
                  <div key={holiday.companyHolidayId} className="flex items-center justify-between gap-3 text-sm">
                    <span className="text-text-primary">{holiday.name}</span>
                    <span className="text-xs text-text-muted">{formatDate(holiday.holidayDate)}</span>
                  </div>
                ))}
              </div>
            )}
          </Card>
        </div>

        <div className="xl:col-span-2 space-y-5">
          <Card>
            <div className="flex items-center justify-between gap-3 mb-4">
              <h2 className="font-semibold text-text-primary">My leave balances</h2>
              <Badge>{year}</Badge>
            </div>
            {balances.length === 0 ? (
              <div className="text-sm text-text-muted py-5 text-center">No leave balances have been allocated. Contact HR.</div>
            ) : (
              <div className="grid md:grid-cols-2 gap-3">
                {balances.map((balance) => (
                  <div key={balance.leaveBalanceId} className="p-4 rounded-lg border border-border bg-bg">
                    <div className="flex items-center justify-between gap-2 mb-3">
                      <div>
                        <p className="font-medium text-text-primary">{balance.leaveType}</p>
                        <p className="text-xs text-text-muted">{balance.code}</p>
                      </div>
                      <Badge variant="success">{Number(balance.availableDays || 0).toFixed(1)} available</Badge>
                    </div>
                    <div className="grid grid-cols-3 gap-2 text-center">
                      <div><p className="font-semibold text-text-primary">{Number(balance.allocatedDays || 0) + Number(balance.carriedOverDays || 0)}</p><p className="text-[11px] text-text-muted">Allocated</p></div>
                      <div><p className="font-semibold text-text-primary">{Number(balance.usedDays || 0)}</p><p className="text-[11px] text-text-muted">Used</p></div>
                      <div><p className="font-semibold text-text-primary">{Number(balance.pendingDays || 0)}</p><p className="text-[11px] text-text-muted">Pending</p></div>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </Card>

          <Card>
            <div className="flex items-center justify-between mb-4">
              <h2 className="font-semibold text-text-primary">My requests</h2>
              <Badge>{requests.length}</Badge>
            </div>
            {requests.length === 0 ? (
              <p className="text-sm text-text-muted py-5 text-center">You have not submitted any leave requests for this company.</p>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full min-w-[620px]">
                  <thead>
                    <tr className="text-left border-b border-border text-xs text-text-muted">
                      <th className="py-2 pr-3">Type</th><th className="py-2 pr-3">Dates</th><th className="py-2 pr-3">Days</th><th className="py-2 pr-3">Status</th><th className="py-2">Action</th>
                    </tr>
                  </thead>
                  <tbody>
                    {requests.map((request) => (
                      <tr key={request.leaveRequestId} className="border-b border-border/50 text-sm">
                        <td className="py-3 pr-3 text-text-primary">{request.leaveType}</td>
                        <td className="py-3 pr-3 text-text-secondary">{formatDate(request.startDate)} – {formatDate(request.endDate)}</td>
                        <td className="py-3 pr-3 text-text-secondary">{Number(request.requestedDays).toFixed(1)}</td>
                        <td className="py-3 pr-3"><Badge variant={statusVariant(request.status)}>{request.status}</Badge></td>
                        <td className="py-3">
                          {request.status === 'pending' ? (
                            <button onClick={() => cancelRequest(request.leaveRequestId)} className="text-xs text-danger hover:underline">Cancel</button>
                          ) : <span className="text-xs text-text-muted">—</span>}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </Card>

          {canReview && (
            <Card>
              <div className="flex items-center justify-between gap-3 mb-4">
                <div className="flex items-center gap-2">
                  <Users size={18} className="text-orange" />
                  <h2 className="font-semibold text-text-primary">Approval inbox</h2>
                </div>
                <Badge>{inbox.length} pending</Badge>
              </div>
              {inbox.length === 0 ? (
                <p className="text-sm text-text-muted py-5 text-center">No leave requests are awaiting your decision.</p>
              ) : (
                <div className="space-y-3">
                  {inbox.map((item) => (
                    <div key={item.leaveRequestId} className="p-4 rounded-lg border border-border bg-bg">
                      <div className="flex flex-wrap items-start justify-between gap-3">
                        <div>
                          <p className="font-medium text-text-primary">{item.employeeName}</p>
                          <p className="text-xs text-text-muted">{item.leaveType} · {Number(item.requestedDays).toFixed(1)} working days</p>
                        </div>
                        <Badge variant="warning">Pending</Badge>
                      </div>
                      <p className="text-sm text-text-secondary mt-2">{formatDate(item.startDate)} – {formatDate(item.endDate)}</p>
                      <p className="text-sm text-text-secondary mt-2 whitespace-pre-wrap">{item.reason}</p>
                      <textarea rows={2} maxLength={2000} placeholder="Decision comment (optional)"
                        value={decisionComments[item.leaveRequestId] || ''}
                        onChange={(event) => setDecisionComments((current) => ({ ...current, [item.leaveRequestId]: event.target.value }))}
                        className="w-full mt-3 bg-surface border border-border rounded-lg px-3 py-2 text-sm text-text-primary" />
                      <div className="flex flex-wrap justify-end gap-2 mt-3">
                        <Button variant="danger" onClick={() => decideRequest(item.leaveRequestId, false)}>Reject</Button>
                        <Button onClick={() => decideRequest(item.leaveRequestId, true)}>Approve</Button>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </Card>
          )}
        </div>
      </div>

      {canManage && (
        <div className="mt-5">
          <Card>
            <button onClick={() => setShowSetup((value) => !value)} className="w-full flex items-center justify-between gap-3 text-left">
              <span className="inline-flex items-center gap-2 font-semibold text-text-primary"><Settings2 size={18} className="text-orange" /> HR leave configuration</span>
              <span className="text-xs text-text-muted">{showSetup ? 'Hide' : 'Show setup'}</span>
            </button>
            {showSetup && (
              <div className="grid lg:grid-cols-3 gap-5 mt-5">
                <form className="space-y-3" onSubmit={saveLeaveType}>
                  <h3 className="text-sm font-semibold text-text-primary">Create leave type</h3>
                  <input required maxLength={50} placeholder="Code (e.g. parental)" value={typeForm.code}
                    onChange={(event) => setTypeForm((current) => ({ ...current, code: event.target.value }))}
                    className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm text-text-primary" />
                  <input required maxLength={120} placeholder="Name" value={typeForm.name}
                    onChange={(event) => setTypeForm((current) => ({ ...current, name: event.target.value }))}
                    className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm text-text-primary" />
                  <input maxLength={1000} placeholder="Description" value={typeForm.description}
                    onChange={(event) => setTypeForm((current) => ({ ...current, description: event.target.value }))}
                    className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm text-text-primary" />
                  <input required min="0" step="0.5" type="number" placeholder="Default annual allowance" value={typeForm.annualAllowanceDays}
                    onChange={(event) => setTypeForm((current) => ({ ...current, annualAllowanceDays: event.target.value }))}
                    className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm text-text-primary" />
                  <input required min="0" step="0.5" type="number" placeholder="Maximum consecutive days (0 = no limit)" value={typeForm.maximumConsecutiveDays}
                    onChange={(event) => setTypeForm((current) => ({ ...current, maximumConsecutiveDays: event.target.value }))}
                    className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm text-text-primary" />
                  <label className="flex items-center gap-2 text-sm text-text-secondary"><input type="checkbox" checked={typeForm.isPaid} onChange={(event) => setTypeForm((current) => ({ ...current, isPaid: event.target.checked }))} /> Paid leave</label>
                  <label className="flex items-center gap-2 text-sm text-text-secondary"><input type="checkbox" checked={typeForm.requiresApproval} onChange={(event) => setTypeForm((current) => ({ ...current, requiresApproval: event.target.checked }))} /> Requires approval</label>
                  <Button type="submit" disabled={submitting}>Create type</Button>
                </form>

                <form className="space-y-3" onSubmit={saveBalance}>
                  <h3 className="text-sm font-semibold text-text-primary">Allocate employee balance</h3>
                  <select required value={balanceForm.userId} onChange={(event) => setBalanceForm((current) => ({ ...current, userId: event.target.value }))}
                    className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm text-text-primary">
                    <option value="">Select employee…</option>
                    {employees.map((person) => <option key={person.id} value={person.id}>{person.name} · {person.department}</option>)}
                  </select>
                  <select required value={balanceForm.leaveTypeId} onChange={(event) => setBalanceForm((current) => ({ ...current, leaveTypeId: event.target.value }))}
                    className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm text-text-primary">
                    <option value="">Select leave type…</option>
                    {types.map((type) => <option key={type.leaveTypeId} value={type.leaveTypeId}>{type.name}</option>)}
                  </select>
                  <input required min="0" step="0.5" type="number" placeholder="Allocated days" value={balanceForm.allocatedDays}
                    onChange={(event) => setBalanceForm((current) => ({ ...current, allocatedDays: event.target.value }))}
                    className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm text-text-primary" />
                  <input min="0" step="0.5" type="number" placeholder="Carried over days" value={balanceForm.carriedOverDays}
                    onChange={(event) => setBalanceForm((current) => ({ ...current, carriedOverDays: event.target.value }))}
                    className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm text-text-primary" />
                  <Button type="submit" disabled={submitting || employees.length === 0}>Save balance</Button>
                </form>

                <form className="space-y-3" onSubmit={saveHoliday}>
                  <h3 className="text-sm font-semibold text-text-primary">Add company holiday</h3>
                  <input required maxLength={160} placeholder="Holiday name" value={holidayForm.name}
                    onChange={(event) => setHolidayForm((current) => ({ ...current, name: event.target.value }))}
                    className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm text-text-primary" />
                  <input required type="date" value={holidayForm.holidayDate}
                    onChange={(event) => setHolidayForm((current) => ({ ...current, holidayDate: event.target.value }))}
                    className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm text-text-primary" />
                  <label className="flex items-center gap-2 text-sm text-text-secondary"><input type="checkbox" checked={holidayForm.isRecurring} onChange={(event) => setHolidayForm((current) => ({ ...current, isRecurring: event.target.checked }))} /> Recurring holiday (metadata only)</label>
                  <Button type="submit" disabled={submitting}>Add holiday</Button>
                </form>
              </div>
            )}
          </Card>
        </div>
      )}
    </AppLayout>
  );
}
