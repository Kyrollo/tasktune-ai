import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import AppLayout from '../../layouts/AppLayout';
import { Card, Badge, Button, Modal } from '../../components/ui';
import { userService } from '../../services/userService';
import { departmentService } from '../../services/departmentService';
import { Search, UserPlus, Edit, Trash2, Shield } from 'lucide-react';

const ROLE_MAP = { 0: 'Employee', 1: 'Supervisor', 2: 'Manager', 3: 'HR', 4: 'CEO', 5: 'Admin' };
const ROLE_TO_BYTE = { Employee: 0, Supervisor: 1, Manager: 2, HR: 3, CEO: 4, Admin: 5 };

const normalizeUser = (u) => ({
  id: u.userId || u.id,
  name: u.fullName || `${u.firstName || ''} ${u.lastName || ''}`.trim() || u.name || 'Unknown',
  email: u.email || '',
  role: typeof u.role === 'number' ? (ROLE_MAP[u.role] || 'Employee') : (u.role || 'Employee'),
  status: u.isActive !== undefined ? (u.isActive ? 'Active' : 'Inactive') : (u.status || 'Active'),
  department: u.departmentName || u.department?.departmentName || u.department || 'N/A',
  position: u.positionTitle || u.position?.positionTitle || u.position || 'N/A',
  avatar: u.avatar || null,
});

export default function UsersManagement() {
  const navigate = useNavigate();
  const [users, setUsers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [roleFilter, setRoleFilter] = useState('All');
  const [showCreate, setShowCreate] = useState(false);
  const [formData, setFormData] = useState({
    firstName: '', lastName: '', email: '', password: '', role: 0, departmentId: '', positionId: '',
  });
  const [departments, setDepartments] = useState([]);
  const [submitting, setSubmitting] = useState(false);
  const [createError, setCreateError] = useState('');
  const [permissionUser, setPermissionUser] = useState(null);
  const [permissionOpen, setPermissionOpen] = useState(false);
  const [permissionLoading, setPermissionLoading] = useState(false);
  const [permissionSaving, setPermissionSaving] = useState(false);
  const [permissionItems, setPermissionItems] = useState([]);
  const [permissionError, setPermissionError] = useState('');
  const [permissionForm, setPermissionForm] = useState({
    resource: 'employees', action: 'read', canPerform: true,
    scope: 'selected', scopeResourceId: '', expiresAt: '', reason: '',
  });

  const roles = ['All', 'Employee', 'Supervisor', 'Manager', 'CEO', 'HR', 'Admin'];
  const statusVariant = { Active: 'success', Inactive: 'danger' };

  useEffect(() => {
    const fetchData = async () => {
      try {
        const [usersRes, deptsRes] = await Promise.all([
          userService.getUsers(),
          departmentService.getDepartments().catch(() => ({ data: [] })),
        ]);
        const raw = Array.isArray(usersRes.data) ? usersRes.data : usersRes.data?.items || [];
        setUsers(raw.map(normalizeUser));
        const depts = Array.isArray(deptsRes.data) ? deptsRes.data : deptsRes.data?.items || [];
        setDepartments(depts.map((d) => ({ id: d.departmentId ?? d.id, name: d.departmentName ?? d.name })));
      } catch (err) {
        console.error('Failed to load users:', err);
      } finally {
        setLoading(false);
      }
    };
    fetchData();
  }, []);

  const handleCreateUser = async () => {
    if (!formData.firstName || !formData.email || !formData.password) {
      setCreateError('First name, email, and password are required.');
      return;
    }
    try {
      setSubmitting(true);
      setCreateError('');
      await userService.createUser({
        firstName: formData.firstName,
        lastName: formData.lastName || '',
        email: formData.email,
        password: formData.password,
        role: Number(formData.role),
        departmentId: formData.departmentId ? Number(formData.departmentId) : null,
        positionId: formData.positionId ? Number(formData.positionId) : null,
      });
      const { data } = await userService.getUsers();
      const raw = Array.isArray(data) ? data : data.items || [];
      setUsers(raw.map(normalizeUser));
      setShowCreate(false);
      setFormData({ firstName: '', lastName: '', email: '', password: '', role: 0, departmentId: '', positionId: '' });
    } catch (err) {
      console.error('Failed to create user:', err);
      setCreateError(err.response?.data?.message || 'Failed to create user. Please check the data and try again.');
    } finally {
      setSubmitting(false);
    }
  };


  const loadPermissionOverrides = async (userId) => {
    const { data } = await userService.getPermissionOverrides(userId);
    setPermissionItems(Array.isArray(data?.items) ? data.items : []);
  };

  const openPermissionManager = async (person) => {
    setPermissionUser(person);
    setPermissionOpen(true);
    setPermissionLoading(true);
    setPermissionError('');
    setPermissionForm({
      resource: 'employees', action: 'read', canPerform: true,
      scope: 'selected', scopeResourceId: '', expiresAt: '', reason: '',
    });
    try {
      await loadPermissionOverrides(person.id);
    } catch (err) {
      setPermissionError(err.response?.data?.message || 'Unable to load individual permissions.');
    } finally {
      setPermissionLoading(false);
    }
  };

  const savePermission = async () => {
    if (!permissionUser) return;
    if (permissionForm.scope === 'selected' && !permissionForm.scopeResourceId) {
      setPermissionError('Choose the employee record this permission applies to.');
      return;
    }
    setPermissionSaving(true);
    setPermissionError('');
    try {
      const expiresAt = permissionForm.expiresAt
        ? new Date(`${permissionForm.expiresAt}T23:59:59.000Z`).toISOString()
        : null;
      await userService.savePermissionOverrides(permissionUser.id, [{
        resource: permissionForm.resource,
        action: permissionForm.action,
        canPerform: permissionForm.canPerform,
        scope: permissionForm.scope,
        scopeResourceId: permissionForm.scope === 'selected' ? Number(permissionForm.scopeResourceId) : null,
        expiresAt,
        reason: permissionForm.reason.trim() || null,
      }]);
      await loadPermissionOverrides(permissionUser.id);
      setPermissionError('');
    } catch (err) {
      setPermissionError(err.response?.data?.message || 'Unable to save the permission.');
    } finally {
      setPermissionSaving(false);
    }
  };

  const removePermission = async (overrideId) => {
    if (!permissionUser) return;
    try {
      await userService.deletePermissionOverride(permissionUser.id, overrideId);
      await loadPermissionOverrides(permissionUser.id);
    } catch (err) {
      setPermissionError(err.response?.data?.message || 'Unable to revoke the permission.');
    }
  };

  const handleDeactivate = async (person) => {
    if (!window.confirm(`Deactivate ${person.name}? Their active sessions will be revoked.`)) return;
    try {
      await userService.deactivateUser(person.id);
      setUsers((current) => current.filter((item) => item.id !== person.id));
    } catch (err) {
      window.alert(err.response?.data?.message || 'Unable to deactivate this user.');
    }
  };

  const filtered = users.filter(
    (u) =>
      (roleFilter === 'All' || u.role === roleFilter) &&
      (u.name || '').toLowerCase().includes(search.toLowerCase()),
  );

  if (loading)
    return (
      <AppLayout title="Users Management">
        <div className="flex items-center justify-center h-64">
          <div className="text-text-secondary">Loading...</div>
        </div>
      </AppLayout>
    );

  return (
    <AppLayout title="Users Management">
      {/* Header */}
      <div className="flex flex-wrap items-center justify-between mb-6 gap-3">
        <div className="flex items-center gap-2 flex-wrap">
          {roles.map((r) => (
            <button
              key={r}
              onClick={() => setRoleFilter(r)}
              className={`px-3 py-1.5 rounded-lg text-xs ${
                roleFilter === r
                  ? 'bg-orange text-white'
                  : 'bg-surface border border-border text-text-secondary hover:text-white'
              }`}
            >
              {r}
            </button>
          ))}
        </div>
        <div className="flex items-center gap-3">
          <div className="relative">
            <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-text-muted" />
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Search users..."
              className="pl-9 pr-4 py-2 bg-surface border border-border rounded-lg text-sm text-text-primary placeholder:text-text-muted focus:outline-none focus:border-orange"
            />
          </div>
          <Button onClick={() => setShowCreate(true)}>
            <span className="flex items-center gap-2">
              <UserPlus size={16} /> Add User
            </span>
          </Button>
        </div>
      </div>

      {/* Users table */}
      <Card>
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr className="border-b border-border">
                <th className="text-left py-3 px-4 text-xs text-text-muted font-medium">User</th>
                <th className="text-left py-3 px-4 text-xs text-text-muted font-medium">Email</th>
                <th className="text-left py-3 px-4 text-xs text-text-muted font-medium">Role</th>
                <th className="text-left py-3 px-4 text-xs text-text-muted font-medium">Department</th>
                <th className="text-left py-3 px-4 text-xs text-text-muted font-medium">Status</th>
                <th className="text-left py-3 px-4 text-xs text-text-muted font-medium">Actions</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((u) => (
                <tr key={u.id} className="border-b border-border/50 hover:bg-surface-hover transition-colors">
                  <td className="py-3 px-4">
                    <div className="flex items-center gap-3">
                      <div className="w-8 h-8 rounded-full bg-orange/20 flex items-center justify-center text-orange text-sm font-medium">
                        {u.name.charAt(0)}
                      </div>
                      <div>
                        <p className="text-sm text-text-primary font-medium">{u.name}</p>
                        <p className="text-xs text-text-muted">{u.position}</p>
                      </div>
                    </div>
                  </td>
                  <td className="py-3 px-4 text-sm text-text-secondary">{u.email}</td>
                  <td className="py-3 px-4"><Badge>{u.role}</Badge></td>
                  <td className="py-3 px-4 text-sm text-text-secondary">{u.department}</td>
                  <td className="py-3 px-4"><Badge variant={statusVariant[u.status]}>{u.status}</Badge></td>
                  <td className="py-3 px-4">
                    <div className="flex items-center gap-2">
                      <button
                        onClick={() => navigate(`/profile/${u.id}`)}
                        className="p-1.5 rounded hover:bg-surface-hover text-text-secondary hover:text-text-primary"
                      >
                        <Edit size={14} />
                      </button>
                      <button
                        onClick={() => openPermissionManager(u)}
                        title="Manage individual permissions"
                        className="p-1.5 rounded hover:bg-surface-hover text-text-secondary hover:text-orange"
                      >
                        <Shield size={14} />
                      </button>
                      <button
                        onClick={() => handleDeactivate(u)}
                        title="Deactivate user"
                        className="p-1.5 rounded hover:bg-surface-hover text-text-secondary hover:text-danger"
                      >
                        <Trash2 size={14} />
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {filtered.length === 0 && (
            <p className="text-sm text-text-muted text-center py-6">No users found.</p>
          )}
        </div>
      </Card>

      {/* Create User Modal */}
      <Modal
        isOpen={showCreate}
        onClose={() => { setShowCreate(false); setCreateError(''); }}
        title="Add New User"
      >
        <div className="space-y-4">
          {createError && (
            <div className="p-3 bg-danger/10 border border-danger/20 rounded-lg text-sm text-danger">
              {createError}
            </div>
          )}
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm text-text-secondary mb-1.5">First Name *</label>
              <input
                value={formData.firstName}
                onChange={(e) => setFormData((prev) => ({ ...prev, firstName: e.target.value }))}
                className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-text-primary placeholder:text-text-muted focus:outline-none focus:border-orange"
                placeholder="First name..."
              />
            </div>
            <div>
              <label className="block text-sm text-text-secondary mb-1.5">Last Name</label>
              <input
                value={formData.lastName}
                onChange={(e) => setFormData((prev) => ({ ...prev, lastName: e.target.value }))}
                className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-text-primary placeholder:text-text-muted focus:outline-none focus:border-orange"
                placeholder="Last name..."
              />
            </div>
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm text-text-secondary mb-1.5">Email *</label>
              <input
                type="email"
                value={formData.email}
                onChange={(e) => setFormData((prev) => ({ ...prev, email: e.target.value }))}
                className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-text-primary placeholder:text-text-muted focus:outline-none focus:border-orange"
                placeholder="Email..."
              />
            </div>
            <div>
              <label className="block text-sm text-text-secondary mb-1.5">Password *</label>
              <input
                type="password"
                value={formData.password}
                onChange={(e) => setFormData((prev) => ({ ...prev, password: e.target.value }))}
                className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-text-primary placeholder:text-text-muted focus:outline-none focus:border-orange"
                placeholder="Password..."
              />
            </div>
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm text-text-secondary mb-1.5">Role</label>
              <select
                value={formData.role}
                onChange={(e) => setFormData((prev) => ({ ...prev, role: e.target.value }))}
                className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-text-primary focus:outline-none focus:border-orange appearance-none"
              >
                {roles
                  .filter((r) => r !== 'All')
                  .map((r) => (
                    <option key={r} value={ROLE_TO_BYTE[r] ?? 0}>
                      {r}
                    </option>
                  ))}
              </select>
            </div>
            <div>
              <label className="block text-sm text-text-secondary mb-1.5">Department</label>
              <select
                value={formData.departmentId}
                onChange={(e) => setFormData((prev) => ({ ...prev, departmentId: e.target.value }))}
                className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-text-primary focus:outline-none focus:border-orange appearance-none"
              >
                <option value="">Select department...</option>
                {departments.map((d) => (
                  <option key={d.id} value={d.id}>
                    {d.name}
                  </option>
                ))}
              </select>
            </div>
          </div>
          <div className="flex justify-end gap-3">
            <Button
              variant="secondary"
              onClick={() => { setShowCreate(false); setCreateError(''); }}
              disabled={submitting}
            >
              Cancel
            </Button>
            <Button onClick={handleCreateUser} disabled={submitting}>
              {submitting ? 'Creating...' : 'Add User'}
            </Button>
          </div>
        </div>
      </Modal>

      <Modal
        isOpen={permissionOpen}
        onClose={() => { setPermissionOpen(false); setPermissionUser(null); setPermissionItems([]); }}
        title={`Individual Permissions — ${permissionUser?.name || ''}`}
        size="lg"
      >
        <div className="space-y-4">
          <p className="text-sm text-text-secondary">
            These overrides apply to this account only. A selected-record override can grant or deny access to one employee page.
            Denials take precedence over role permissions.
          </p>
          {permissionError && (
            <div role="alert" className="p-3 rounded-lg border border-danger/20 bg-danger/10 text-sm text-danger">
              {permissionError}
            </div>
          )}
          {permissionLoading ? (
            <div className="py-6 text-center text-sm text-text-secondary">Loading permissions…</div>
          ) : (
            <>
              <div className="grid md:grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs text-text-secondary mb-1">Module</label>
                  <select value={permissionForm.resource} onChange={(e) => setPermissionForm((v) => ({ ...v, resource: e.target.value }))}
                    className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm">
                    {['employees','tasks','projects','reports','attendance','leave','payroll','hr_cases','documents','org_chart','permissions','integrations'].map((value) => <option key={value} value={value}>{value.replaceAll('_',' ')}</option>)}
                  </select>
                </div>
                <div>
                  <label className="block text-xs text-text-secondary mb-1">Action</label>
                  <select value={permissionForm.action} onChange={(e) => setPermissionForm((v) => ({ ...v, action: e.target.value }))}
                    className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm">
                    {['read','create','update','delete','approve','export','manage'].map((value) => <option key={value}>{value}</option>)}
                  </select>
                </div>
                <div>
                  <label className="block text-xs text-text-secondary mb-1">Decision</label>
                  <select value={String(permissionForm.canPerform)} onChange={(e) => setPermissionForm((v) => ({ ...v, canPerform: e.target.value === 'true' }))}
                    className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm">
                    <option value="true">Grant permission</option>
                    <option value="false">Explicitly deny permission</option>
                  </select>
                </div>
                <div>
                  <label className="block text-xs text-text-secondary mb-1">Access scope</label>
                  <select value={permissionForm.scope} onChange={(e) => setPermissionForm((v) => ({ ...v, scope: e.target.value, scopeResourceId: '' }))}
                    className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm">
                    <option value="self">Own record</option>
                    <option value="team">Reporting team</option>
                    <option value="department">Department</option>
                    <option value="company">Entire company</option>
                    <option value="selected">One selected employee</option>
                  </select>
                </div>
                {permissionForm.scope === 'selected' && (
                  <div>
                    <label className="block text-xs text-text-secondary mb-1">Selected employee</label>
                    <select value={permissionForm.scopeResourceId} onChange={(e) => setPermissionForm((v) => ({ ...v, scopeResourceId: e.target.value }))}
                      className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm">
                      <option value="">Choose employee…</option>
                      {users.filter((candidate) => candidate.id !== permissionUser?.id).map((candidate) => (
                        <option key={candidate.id} value={candidate.id}>{candidate.name} — {candidate.department}</option>
                      ))}
                    </select>
                  </div>
                )}
                <div>
                  <label className="block text-xs text-text-secondary mb-1">Expires on (optional)</label>
                  <input type="date" value={permissionForm.expiresAt} onChange={(e) => setPermissionForm((v) => ({ ...v, expiresAt: e.target.value }))}
                    className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm" />
                </div>
                <div className="md:col-span-2">
                  <label className="block text-xs text-text-secondary mb-1">Reason / approval reference</label>
                  <input value={permissionForm.reason} onChange={(e) => setPermissionForm((v) => ({ ...v, reason: e.target.value }))}
                    maxLength={500} placeholder="Why is this access required?"
                    className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm" />
                </div>
              </div>
              <div className="flex justify-end">
                <Button onClick={savePermission} disabled={permissionSaving}>
                  {permissionSaving ? 'Saving…' : 'Save individual permission'}
                </Button>
              </div>
              <div className="border-t border-border pt-4">
                <h4 className="text-sm font-semibold text-text-primary mb-3">Current overrides ({permissionItems.length})</h4>
                {permissionItems.length === 0 ? (
                  <p className="text-sm text-text-muted">No individual overrides configured. Role-level permissions apply.</p>
                ) : (
                  <div className="space-y-2 max-h-64 overflow-y-auto">
                    {permissionItems.map((item) => (
                      <div key={item.permissionOverrideId} className="flex items-center gap-3 p-3 bg-bg border border-border rounded-lg">
                        <div className="flex-1 min-w-0">
                          <p className="text-sm text-text-primary font-medium">{item.resource} · {item.action}</p>
                          <p className="text-xs text-text-muted">
                            {item.scope}{item.scopeResourceId ? ` · employee #${item.scopeResourceId}` : ''}
                            {item.expiresAt ? ` · expires ${new Date(item.expiresAt).toLocaleDateString()}` : ' · no expiry'}
                          </p>
                        </div>
                        <Badge variant={item.isExpired ? 'default' : item.canPerform ? 'success' : 'danger'}>
                          {item.isExpired ? 'Expired' : item.canPerform ? 'Granted' : 'Denied'}
                        </Badge>
                        <button onClick={() => removePermission(item.permissionOverrideId)} title="Revoke override"
                          className="p-1.5 rounded hover:bg-surface-hover text-text-secondary hover:text-danger">
                          <Trash2 size={14} />
                        </button>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </>
          )}
        </div>
      </Modal>
    </AppLayout>
  );
}
