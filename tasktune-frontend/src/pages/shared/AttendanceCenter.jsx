import { useCallback, useEffect, useMemo, useState } from 'react';
import AppLayout from '../../layouts/AppLayout';
import { Badge, Button, Card, StatCard } from '../../components/ui';
import { useAuth } from '../../contexts/AuthContext';
import { attendanceService } from '../../services/attendanceService';
import { userService } from '../../services/userService';
import {
  Clock3, CalendarDays, LogIn, LogOut, RefreshCw, Users,
  Settings2, Plus, AlertCircle, CheckCircle2, Timer, CalendarClock
} from 'lucide-react';

const REVIEW_ROLES = ['Supervisor', 'Manager', 'HR', 'CEO', 'Admin'];
const CONFIG_ROLES = ['HR', 'CEO', 'Admin'];
const WEEK_DAYS = [
  { value: 0, label: 'Sun' },
  { value: 1, label: 'Mon' },
  { value: 2, label: 'Tue' },
  { value: 3, label: 'Wed' },
  { value: 4, label: 'Thu' },
  { value: 5, label: 'Fri' },
  { value: 6, label: 'Sat' },
];

const toDateInput = (date) => {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
};

const shiftRange = () => {
  const end = new Date();
  const start = new Date();
  start.setDate(start.getDate() - 29);
  return { from: toDateInput(start), to: toDateInput(end) };
};

const formatTime = (value, timeZone) => {
  if (!value) return '—';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '—';
  return date.toLocaleTimeString([], {
    hour: '2-digit',
    minute: '2-digit',
    timeZone: timeZone || undefined,
  });
};

const formatDateTime = (value, timeZone) => {
  if (!value) return '—';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '—';
  return date.toLocaleString([], { timeZone: timeZone || undefined, dateStyle: 'medium', timeStyle: 'short' });
};

const formatDate = (value) => {
  if (!value) return '—';
  return new Date(`${value}T00:00:00`).toLocaleDateString();
};

const statusVariant = (status) => ({
  present: 'success',
  late: 'warning',
  early_leave: 'warning',
  late_and_early: 'danger',
  scheduled: 'default',
  missed: 'danger',
  on_leave: 'info',
  holiday: 'info',
}[String(status || '').toLowerCase()] || 'default');

const statusLabel = (status) => String(status || 'unknown').replaceAll('_', ' ');

export default function AttendanceCenter() {
  const { user } = useAuth();
  const role = user?.role || 'Employee';
  const canReview = REVIEW_ROLES.includes(role);
  const canConfigure = CONFIG_ROLES.includes(role);

  const [today, setToday] = useState(null);
  const [timesheet, setTimesheet] = useState({ items: [], summary: {} });
  const [team, setTeam] = useState({ items: [], summary: {} });
  const [shifts, setShifts] = useState([]);
  const [employees, setEmployees] = useState([]);
  const [range, setRange] = useState(shiftRange);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [showSetup, setShowSetup] = useState(false);

  const [shiftForm, setShiftForm] = useState({
    name: '',
    startTime: '09:00',
    endTime: '17:00',
    breakMinutes: '60',
    graceMinutes: '5',
  });
  const [assignmentForm, setAssignmentForm] = useState({
    userId: '',
    workShiftId: '',
    days: [1, 2, 3, 4, 5],
    effectiveFrom: toDateInput(new Date()),
    effectiveTo: '',
  });

  const load = useCallback(async (silent = false) => {
    if (silent) setRefreshing(true);
    else setLoading(true);
    setError('');

    const jobs = [
      attendanceService.getToday(),
      attendanceService.getMyTimesheet(range.from, range.to),
    ];
    if (canReview) {
      jobs.push(attendanceService.getTeamAttendance(range.from, range.to));
      jobs.push(userService.getUsers(1, 100));
    }
    if (canConfigure) jobs.push(attendanceService.getShifts());

    try {
      const results = await Promise.all(jobs);
      setToday(results[0].data);
      setTimesheet(results[1].data || { items: [], summary: {} });

      let cursor = 2;
      if (canReview) {
        setTeam(results[cursor++].data || { items: [], summary: {} });
        const directory = results[cursor++].data;
        const people = Array.isArray(directory) ? directory : directory?.items || [];
        setEmployees(people.map((person) => ({
          id: person.userId ?? person.id,
          name: person.fullName || [person.firstName, person.lastName].filter(Boolean).join(' '),
          department: person.departmentName || person.department || 'Unassigned',
        })));
      } else {
        setTeam({ items: [], summary: {} });
        setEmployees([]);
      }
      if (canConfigure) {
        setShifts(results[cursor]?.data?.items || []);
      } else {
        setShifts([]);
      }
    } catch (err) {
      setError(err.response?.data?.message || 'Unable to load attendance. Check your connection and retry.');
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [range.from, range.to, canReview, canConfigure]);

  useEffect(() => { load(); }, [load]);

  useEffect(() => {
    if (!success) return undefined;
    const timeout = window.setTimeout(() => setSuccess(''), 4000);
    return () => window.clearTimeout(timeout);
  }, [success]);

  const shift = today?.shift;
  const attendance = today?.attendance;
  const canClockIn = Boolean(today?.canClockIn && shift);
  const canClockOut = Boolean(today?.canClockOut);

  const totalWorkedHours = useMemo(
    () => (Number(timesheet.summary?.totalWorkedMinutes || 0) / 60).toFixed(1),
    [timesheet.summary?.totalWorkedMinutes],
  );

  const clock = async (action) => {
    setSubmitting(true);
    setError('');
    setSuccess('');
    try {
      const result = action === 'in'
        ? await attendanceService.clockIn()
        : await attendanceService.clockOut();
      setSuccess(result.data?.message || (action === 'in' ? 'Clock-in recorded.' : 'Clock-out recorded.'));
      await load(true);
    } catch (err) {
      setError(err.response?.data?.message || 'The attendance action could not be completed.');
    } finally {
      setSubmitting(false);
    }
  };

  const createShift = async (event) => {
    event.preventDefault();
    setSubmitting(true);
    setError('');
    try {
      await attendanceService.createShift({
        name: shiftForm.name.trim(),
        startTime: `${shiftForm.startTime}:00`,
        endTime: `${shiftForm.endTime}:00`,
        breakMinutes: Number(shiftForm.breakMinutes),
        graceMinutes: Number(shiftForm.graceMinutes),
        isActive: true,
      });
      setShiftForm({
        name: '', startTime: '09:00', endTime: '17:00',
        breakMinutes: '60', graceMinutes: '5',
      });
      setSuccess('Work shift created.');
      await load(true);
    } catch (err) {
      setError(err.response?.data?.message || 'Unable to create the work shift.');
    } finally {
      setSubmitting(false);
    }
  };

  const assignShift = async (event) => {
    event.preventDefault();
    if (assignmentForm.days.length === 0) {
      setError('Select at least one scheduled day.');
      return;
    }
    setSubmitting(true);
    setError('');
    try {
      const daysOfWeekMask = assignmentForm.days.reduce((mask, day) => mask | (1 << day), 0);
      await attendanceService.assignShift({
        userId: Number(assignmentForm.userId),
        workShiftId: Number(assignmentForm.workShiftId),
        daysOfWeekMask,
        effectiveFrom: assignmentForm.effectiveFrom,
        effectiveTo: assignmentForm.effectiveTo || null,
      });
      setSuccess('Employee shift assignment saved.');
      setAssignmentForm((current) => ({
        ...current,
        userId: '',
        workShiftId: '',
        effectiveFrom: toDateInput(new Date()),
        effectiveTo: '',
      }));
      await load(true);
    } catch (err) {
      setError(err.response?.data?.message || 'Unable to assign this shift.');
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) {
    return (
      <AppLayout title="Attendance & Shifts">
        <div className="h-64 flex items-center justify-center text-sm text-text-secondary">Loading attendance…</div>
      </AppLayout>
    );
  }

  const timeZoneId = today?.timeZoneId || 'UTC';

  return (
    <AppLayout title="Attendance & Shifts">
      <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-3 mb-5">
        <p className="text-sm text-text-secondary">Time records are stored in UTC and displayed using the company time zone.</p>
        <Button variant="secondary" onClick={() => load(true)} disabled={refreshing}>
          <span className="inline-flex items-center gap-2"><RefreshCw size={14} className={refreshing ? 'animate-spin' : ''} /> Refresh</span>
        </Button>
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

      <div className="grid lg:grid-cols-3 gap-4 mb-5">
        <Card>
          <div className="flex items-center justify-between gap-3 mb-3">
            <div>
              <p className="text-xs text-text-muted">Company local date</p>
              <p className="text-lg font-semibold text-text-primary">{today?.workDate || '—'}</p>
            </div>
            <CalendarDays size={22} className="text-orange" />
          </div>
          <p className="text-xs text-text-muted">Time zone: {timeZoneId}</p>
          <p className="text-xs text-text-secondary mt-1">{today?.localNow ? formatDateTime(today.localNow, timeZoneId) : ''}</p>
        </Card>

        <Card>
          <div className="flex items-center gap-2 mb-3">
            <CalendarClock size={18} className="text-orange" />
            <h2 className="font-semibold text-text-primary">Today's shift</h2>
          </div>
          {shift ? (
            <>
              <p className="font-medium text-text-primary">{shift.shiftName}</p>
              <p className="text-sm text-text-secondary mt-1">
                {shift.startTime} – {shift.endTime}
                {shift.endTime <= shift.startTime ? ' · Overnight' : ''}
              </p>
              <p className="text-xs text-text-muted mt-1">Grace period: {shift.graceMinutes} min · Break: {shift.breakMinutes} min</p>
            </>
          ) : (
            <p className="text-sm text-text-muted">No active shift assignment for today.</p>
          )}
        </Card>

        <Card>
          <div className="flex items-center gap-2 mb-3">
            <Clock3 size={18} className="text-orange" />
            <h2 className="font-semibold text-text-primary">Today's punches</h2>
            {attendance && <Badge variant={statusVariant(attendance.status)}>{statusLabel(attendance.status)}</Badge>}
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <p className="text-xs text-text-muted">Clock-in</p>
              <p className="text-sm font-medium text-text-primary">{formatTime(attendance?.checkInUtc, timeZoneId)}</p>
            </div>
            <div>
              <p className="text-xs text-text-muted">Clock-out</p>
              <p className="text-sm font-medium text-text-primary">{formatTime(attendance?.checkOutUtc, timeZoneId)}</p>
            </div>
          </div>
          {attendance && (
            <p className="text-xs text-text-muted mt-2">
              Late: {attendance.lateMinutes || 0} min · Worked: {((attendance.workedMinutes || 0) / 60).toFixed(1)} h
            </p>
          )}
        </Card>
      </div>

      <Card className="mb-5">
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
          <div>
            <h2 className="font-semibold text-text-primary">Time clock</h2>
            <p className="text-xs text-text-muted mt-1">Clock actions use the current server time and the assigned work shift.</p>
          </div>
          <div className="flex flex-wrap gap-2">
            <Button onClick={() => clock('in')} disabled={!canClockIn || submitting}>
              <span className="inline-flex items-center gap-2"><LogIn size={16} /> {submitting ? 'Processing…' : 'Clock in'}</span>
            </Button>
            <Button variant="secondary" onClick={() => clock('out')} disabled={!canClockOut || submitting}>
              <span className="inline-flex items-center gap-2"><LogOut size={16} /> Clock out</span>
            </Button>
          </div>
        </div>
        {!shift && <p className="text-xs text-warning mt-3">HR must assign a shift before you can clock in.</p>}
        {attendance?.checkInUtc && !attendance?.checkOutUtc && <p className="text-xs text-success mt-3">You have an open attendance session.</p>}
      </Card>

      <div className="grid md:grid-cols-3 gap-4 mb-5">
        <StatCard label="Recorded days" value={timesheet.summary?.recordedDays || 0} icon={CalendarDays} />
        <StatCard label="Late days" value={timesheet.summary?.lateDays || 0} icon={Timer} />
        <StatCard label="Worked hours" value={totalWorkedHours} icon={Clock3} />
      </div>

      <Card className="mb-5">
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-3 mb-4">
          <h2 className="font-semibold text-text-primary">My timesheet</h2>
          <div className="flex flex-wrap items-center gap-2">
            <label className="text-xs text-text-muted">From</label>
            <input type="date" value={range.from} onChange={(event) => setRange((current) => ({ ...current, from: event.target.value }))}
              className="bg-bg border border-border rounded-lg px-2 py-1.5 text-xs text-text-primary" />
            <label className="text-xs text-text-muted">To</label>
            <input type="date" value={range.to} onChange={(event) => setRange((current) => ({ ...current, to: event.target.value }))}
              className="bg-bg border border-border rounded-lg px-2 py-1.5 text-xs text-text-primary" />
            <Button size="sm" variant="secondary" onClick={() => load(true)} disabled={refreshing}>Apply</Button>
          </div>
        </div>
        {(timesheet.items || []).length === 0 ? (
          <p className="py-8 text-center text-sm text-text-muted">No attendance records are available for this period.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[680px]">
              <thead>
                <tr className="text-left border-b border-border text-xs text-text-muted">
                  <th className="py-2 pr-3">Work date</th><th className="py-2 pr-3">Clock-in</th><th className="py-2 pr-3">Clock-out</th>
                  <th className="py-2 pr-3">Status</th><th className="py-2 pr-3">Late</th><th className="py-2">Worked</th>
                </tr>
              </thead>
              <tbody>
                {timesheet.items.map((item) => (
                  <tr key={item.attendanceRecordId} className="border-b border-border/50 text-sm">
                    <td className="py-3 pr-3 text-text-primary">{formatDate(item.workDate)}</td>
                    <td className="py-3 pr-3 text-text-secondary">{formatTime(item.checkInUtc, timeZoneId)}</td>
                    <td className="py-3 pr-3 text-text-secondary">{formatTime(item.checkOutUtc, timeZoneId)}</td>
                    <td className="py-3 pr-3"><Badge variant={statusVariant(item.status)}>{statusLabel(item.status)}</Badge></td>
                    <td className="py-3 pr-3 text-text-secondary">{item.lateMinutes || 0} min</td>
                    <td className="py-3 text-text-secondary">{((item.workedMinutes || 0) / 60).toFixed(1)} h</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      {canReview && (
        <Card className="mb-5">
          <div className="flex items-center justify-between gap-3 mb-4">
            <div className="flex items-center gap-2">
              <Users size={18} className="text-orange" />
              <h2 className="font-semibold text-text-primary">Team attendance</h2>
            </div>
            <Badge>{team.summary?.recordedSessions || 0} records</Badge>
          </div>
          {(team.items || []).length === 0 ? (
            <p className="py-6 text-center text-sm text-text-muted">No team attendance records in this period.</p>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full min-w-[760px]">
                <thead>
                  <tr className="text-left border-b border-border text-xs text-text-muted">
                    <th className="py-2 pr-3">Employee</th><th className="py-2 pr-3">Date</th><th className="py-2 pr-3">In</th>
                    <th className="py-2 pr-3">Out</th><th className="py-2 pr-3">Status</th><th className="py-2 pr-3">Late</th><th className="py-2">Worked</th>
                  </tr>
                </thead>
                <tbody>
                  {team.items.map((item) => (
                    <tr key={item.attendanceRecordId} className="border-b border-border/50 text-sm">
                      <td className="py-3 pr-3">
                        <p className="text-text-primary">{item.employeeName}</p>
                        <p className="text-[11px] text-text-muted">{item.department || 'Unassigned'}</p>
                      </td>
                      <td className="py-3 pr-3 text-text-secondary">{formatDate(item.workDate)}</td>
                      <td className="py-3 pr-3 text-text-secondary">{formatTime(item.checkInUtc, timeZoneId)}</td>
                      <td className="py-3 pr-3 text-text-secondary">{formatTime(item.checkOutUtc, timeZoneId)}</td>
                      <td className="py-3 pr-3"><Badge variant={statusVariant(item.status)}>{statusLabel(item.status)}</Badge></td>
                      <td className="py-3 pr-3 text-text-secondary">{item.lateMinutes || 0} min</td>
                      <td className="py-3 text-text-secondary">{((item.workedMinutes || 0) / 60).toFixed(1)} h</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </Card>
      )}

      {canConfigure && (
        <Card>
          <button onClick={() => setShowSetup((value) => !value)} className="w-full flex items-center justify-between gap-3 text-left">
            <span className="inline-flex items-center gap-2 font-semibold text-text-primary"><Settings2 size={18} className="text-orange" /> HR shift configuration</span>
            <span className="text-xs text-text-muted">{showSetup ? 'Hide' : 'Show setup'}</span>
          </button>
          {showSetup && (
            <div className="grid lg:grid-cols-2 gap-5 mt-5">
              <form className="space-y-3" onSubmit={createShift}>
                <h3 className="text-sm font-semibold text-text-primary">Create work shift</h3>
                <input required maxLength={120} placeholder="Shift name (e.g. Morning)" value={shiftForm.name}
                  onChange={(event) => setShiftForm((current) => ({ ...current, name: event.target.value }))}
                  className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm text-text-primary" />
                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="block text-xs text-text-muted mb-1">Start time</label>
                    <input required type="time" value={shiftForm.startTime}
                      onChange={(event) => setShiftForm((current) => ({ ...current, startTime: event.target.value }))}
                      className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm text-text-primary" />
                  </div>
                  <div>
                    <label className="block text-xs text-text-muted mb-1">End time</label>
                    <input required type="time" value={shiftForm.endTime}
                      onChange={(event) => setShiftForm((current) => ({ ...current, endTime: event.target.value }))}
                      className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm text-text-primary" />
                  </div>
                  <div>
                    <label className="block text-xs text-text-muted mb-1">Break minutes</label>
                    <input required type="number" min="0" max="720" value={shiftForm.breakMinutes}
                      onChange={(event) => setShiftForm((current) => ({ ...current, breakMinutes: event.target.value }))}
                      className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm text-text-primary" />
                  </div>
                  <div>
                    <label className="block text-xs text-text-muted mb-1">Grace minutes</label>
                    <input required type="number" min="0" max="180" value={shiftForm.graceMinutes}
                      onChange={(event) => setShiftForm((current) => ({ ...current, graceMinutes: event.target.value }))}
                      className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm text-text-primary" />
                  </div>
                </div>
                <Button type="submit" disabled={submitting}>{submitting ? 'Saving…' : 'Create shift'}</Button>
                <div className="border-t border-border pt-3">
                  <h4 className="text-xs font-semibold text-text-primary mb-2">Active shifts</h4>
                  {shifts.length === 0 ? (
                    <p className="text-xs text-text-muted">No shifts configured.</p>
                  ) : (
                    <div className="space-y-2">
                      {shifts.map((item) => (
                        <div key={item.workShiftId} className="flex items-center justify-between gap-2 text-sm">
                          <span className="text-text-primary">{item.name}</span>
                          <span className="text-xs text-text-muted">{item.startTime}–{item.endTime}{item.isOvernight ? ' · Overnight' : ''}</span>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              </form>

              <form className="space-y-3" onSubmit={assignShift}>
                <h3 className="text-sm font-semibold text-text-primary">Assign shift to employee</h3>
                <select required value={assignmentForm.userId} onChange={(event) => setAssignmentForm((current) => ({ ...current, userId: event.target.value }))}
                  className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm text-text-primary">
                  <option value="">Select employee…</option>
                  {employees.map((person) => <option key={person.id} value={person.id}>{person.name} · {person.department}</option>)}
                </select>
                <select required value={assignmentForm.workShiftId} onChange={(event) => setAssignmentForm((current) => ({ ...current, workShiftId: event.target.value }))}
                  className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm text-text-primary">
                  <option value="">Select shift…</option>
                  {shifts.map((item) => <option key={item.workShiftId} value={item.workShiftId}>{item.name} · {item.startTime}–{item.endTime}</option>)}
                </select>
                <div>
                  <label className="block text-xs text-text-muted mb-2">Scheduled days</label>
                  <div className="flex flex-wrap gap-2">
                    {WEEK_DAYS.map((day) => (
                      <label key={day.value} className="flex items-center gap-1.5 text-xs text-text-secondary px-2 py-1.5 bg-bg border border-border rounded-lg">
                        <input type="checkbox" checked={assignmentForm.days.includes(day.value)}
                          onChange={(event) => setAssignmentForm((current) => ({
                            ...current,
                            days: event.target.checked
                              ? [...current.days, day.value].sort((a, b) => a - b)
                              : current.days.filter((value) => value !== day.value),
                          }))} />
                        {day.label}
                      </label>
                    ))}
                  </div>
                </div>
                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="block text-xs text-text-muted mb-1">Effective from</label>
                    <input required type="date" value={assignmentForm.effectiveFrom}
                      onChange={(event) => setAssignmentForm((current) => ({ ...current, effectiveFrom: event.target.value }))}
                      className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm text-text-primary" />
                  </div>
                  <div>
                    <label className="block text-xs text-text-muted mb-1">Effective to (optional)</label>
                    <input type="date" min={assignmentForm.effectiveFrom} value={assignmentForm.effectiveTo}
                      onChange={(event) => setAssignmentForm((current) => ({ ...current, effectiveTo: event.target.value }))}
                      className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm text-text-primary" />
                  </div>
                </div>
                <Button type="submit" disabled={submitting || shifts.length === 0 || employees.length === 0 || assignmentForm.days.length === 0}>
                  {submitting ? 'Saving…' : 'Assign shift'}
                </Button>
                <p className="text-xs text-text-muted">Overlapping assignments on the same weekday are rejected by the API.</p>
              </form>
            </div>
          )}
        </Card>
      )}
    </AppLayout>
  );
}
