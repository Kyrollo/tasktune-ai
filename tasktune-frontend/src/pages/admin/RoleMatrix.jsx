import { useState, useEffect } from 'react';
import AppLayout from '../../layouts/AppLayout';
import { Card, Button } from '../../components/ui';
import api from '../../services/api';
import { Shield, Save, Check, Minus } from 'lucide-react';

const ROLE_MAP = [
  { byte: 0, name: 'Employee' },
  { byte: 1, name: 'Supervisor' },
  { byte: 2, name: 'Manager' },
  { byte: 3, name: 'HR' },
  { byte: 4, name: 'CEO' },
  { byte: 5, name: 'Admin' },
];

const PAGES = [
  'Dashboard', 'Tasks', 'Projects', 'Evaluations', 'Reports',
  'HR Communications', 'Users Management', 'Role Matrix', 'AI Control', 'Audit Logs', 'Settings',
];

const DEFAULT_ACCESS = {
  Employee:   { Dashboard: true, Tasks: true, Projects: false, Evaluations: true, Reports: false, 'HR Communications': false, 'Users Management': false, 'Role Matrix': false, 'AI Control': false, 'Audit Logs': false, Settings: true },
  Supervisor: { Dashboard: true, Tasks: true, Projects: true, Evaluations: true, Reports: true, 'HR Communications': false, 'Users Management': false, 'Role Matrix': true, 'AI Control': false, 'Audit Logs': false, Settings: true },
  Manager:    { Dashboard: true, Tasks: true, Projects: true, Evaluations: true, Reports: true, 'HR Communications': false, 'Users Management': false, 'Role Matrix': true, 'AI Control': false, 'Audit Logs': true, Settings: true },
  HR:         { Dashboard: true, Tasks: false, Projects: false, Evaluations: false, Reports: true, 'HR Communications': true, 'Users Management': false, 'Role Matrix': true, 'AI Control': false, 'Audit Logs': false, Settings: true },
  CEO:        { Dashboard: true, Tasks: true, Projects: true, Evaluations: true, Reports: true, 'HR Communications': true, 'Users Management': true, 'Role Matrix': true, 'AI Control': true, 'Audit Logs': true, Settings: true },
  Admin:      { Dashboard: true, Tasks: true, Projects: true, Evaluations: true, Reports: true, 'HR Communications': true, 'Users Management': true, 'Role Matrix': true, 'AI Control': true, 'Audit Logs': true, Settings: true },
};

const DB_RESOURCE_MAP = {
  'tasks': 'Tasks',
  'projects': 'Projects',
  'evaluations': 'Evaluations',
  'reports': 'Reports',
  'communications': 'HR Communications',
  'users': 'Users Management',
  'roles': 'Role Matrix',
  'audit': 'Audit Logs',
  'ai': 'AI Control'
};

const UI_TO_DB_MAP = Object.entries(DB_RESOURCE_MAP).reduce((acc, [db, ui]) => {
  acc[ui] = db;
  return acc;
}, {});

function buildMatrix(permissions) {
  const matrix = {};
  for (const role of ROLE_MAP) {
    matrix[role.name] = {};
    for (const page of PAGES) {
      matrix[role.name][page] = DEFAULT_ACCESS[role.name]?.[page] ?? false;
    }
  }
  for (const p of permissions) {
    const role = ROLE_MAP.find(r => r.byte === p.role);
    const uiResourceName = DB_RESOURCE_MAP[p.resource] || p.resource;
    
    if (role && PAGES.includes(uiResourceName) && (p.action === 'access' || p.action === 'read')) {
      matrix[role.name][uiResourceName] = p.canPerform;
    }
  }
  return matrix;
}

function matrixToPermissions(matrix) {
  const perms = [];
  for (const role of ROLE_MAP) {
    for (const page of PAGES) {
      const dbResourceName = UI_TO_DB_MAP[page] || page;
      // Skip hardcoded baseline pages from payload sync to prevent duplications if intended
      if(page === 'Dashboard' || page === 'Settings') continue;

      perms.push({
        role: role.byte,
        resource: dbResourceName,
        action: 'read',
        canPerform: matrix[role.name]?.[page] ?? false,
      });
    }
  }
  return perms;
}

export default function RoleMatrix() {
  const [matrix, setMatrix] = useState(() => buildMatrix([]));
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    (async () => {
      try {
        const { data } = await api.get('/role-permissions');
        const items = Array.isArray(data) ? data : data?.items || [];
        setMatrix(buildMatrix(items));
      } catch (err) {
        console.error('Failed to load permissions:', err);
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  const toggle = (roleName, page) => {
    setMatrix(prev => ({
      ...prev,
      [roleName]: { ...prev[roleName], [page]: !prev[roleName][page] },
    }));
    setSaved(false);
  };

  const handleSave = async () => {
    setSaving(true);
    setError('');
    try {
      await api.put('/role-permissions', { permissions: matrixToPermissions(matrix) });
      setSaved(true);
      setTimeout(() => setSaved(false), 3000);
    } catch (err) {
      setError(err.response?.data?.message || 'Failed to save permissions');
    } finally {
      setSaving(false);
    }
  };

  if (loading) return (
    <AppLayout title="Role Permission Matrix">
      <div className="flex items-center justify-center h-64"><div className="text-text-secondary">Loading...</div></div>
    </AppLayout>
  );

  return (
    <AppLayout title="Role Permission Matrix">
      <Card>
        <div className="flex items-center justify-between mb-6">
          <div className="flex items-center gap-2">
            <Shield size={20} className="text-orange" />
            <h3 className="text-lg font-semibold text-white">Permissions</h3>
          </div>
          <div className="flex items-center gap-3">
            {error && <span className="text-sm text-danger">{error}</span>}
            {saved && <span className="text-sm text-success">Saved!</span>}
            <Button disabled={saving} onClick={handleSave}>
              <span className="flex items-center gap-2"><Save size={16} /> {saving ? 'Saving...' : 'Save Changes'}</span>
            </Button>
          </div>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full border-collapse">
            <thead>
              <tr className="border-b border-border">
                <th className="text-left py-3 px-4 text-xs text-text-muted font-medium w-48">Page / Feature</th>
                {ROLE_MAP.map(role => (
                  <th key={role.name} className="py-3 px-2 text-xs text-text-muted font-medium w-24 text-center">{role.name}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {PAGES.map(page => (
                <tr key={page} className="border-b border-border/50 hover:bg-surface-hover/50 transition-colors">
                  <td className="py-3 px-4 text-sm text-white font-medium">{page}</td>
                  {ROLE_MAP.map(role => {
                    const granted = matrix[role.name]?.[page] ?? false;
                    return (
                      <td key={role.name} className="py-3 px-2">
                        <div className="flex justify-center">
                          <button
                            onClick={() => toggle(role.name, page)}
                            className={`w-9 h-9 rounded-lg flex items-center justify-center transition-all ${
                              granted
                                ? 'bg-success/20 text-success hover:bg-success/30'
                                : 'bg-border/30 text-text-muted hover:bg-border/50'
                            }`}
                          >
                            {granted ? <Check size={16} strokeWidth={3} /> : <Minus size={16} />}
                          </button>
                        </div>
                      </td>
                    );
                  })}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Card>

      <div className="mt-4 flex items-center gap-6">
        <div className="flex items-center gap-2">
          <div className="w-9 h-9 rounded-lg bg-success/20 flex items-center justify-center text-success"><Check size={16} strokeWidth={3} /></div>
          <span className="text-sm text-text-secondary">Full Access</span>
        </div>
        <div className="flex items-center gap-2">
          <div className="w-9 h-9 rounded-lg bg-border/30 flex items-center justify-center text-text-muted"><Minus size={16} /></div>
          <span className="text-sm text-text-secondary">No Access</span>
        </div>
      </div>
    </AppLayout>
  );
}
