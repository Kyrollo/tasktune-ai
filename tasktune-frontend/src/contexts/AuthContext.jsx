/* eslint-disable react-refresh/only-export-components */
import { createContext, useContext, useState, useEffect, useCallback } from 'react';
import { authService } from '../services/authService';
import { persistAuth, clearAllAuthStorage, getAccessToken } from '../utils/authStorage';
import api from '../services/api';

const AuthContext = createContext(null);

// ── role helpers ──────────────────────────────────────────────────────────────
const ROLE_NAME_MAP = { 0: 'Employee', 1: 'Supervisor', 2: 'Manager', 3: 'HR', 4: 'CEO', 5: 'Admin' };
const ROLE_BYTE_MAP = { Employee: 0, Supervisor: 1, Manager: 2, HR: 3, CEO: 4, Admin: 5 };

const normalizeRole = (role) =>
  typeof role === 'number' ? (ROLE_NAME_MAP[role] || 'Employee') : (role || 'Employee');

const normalizeAuthUser = (u) => {
  if (!u) return null;
  return {
    id:     u.userId ?? u.id,
    name:   u.fullName || u.name || `${u.firstName || ''} ${u.lastName || ''}`.trim() || 'User',
    email:  u.email  || '',
    role:   normalizeRole(u.role),
    avatar: u.avatar || null,
  };
};

// ── nav / route maps ──────────────────────────────────────────────────────────
const PAGE_NAV_MAP = {
  Dashboard:          { Employee: { label: 'Dashboard', path: '/dashboard', icon: 'LayoutDashboard' }, Supervisor: { label: 'Dashboard', path: '/supervisor', icon: 'LayoutDashboard' }, Manager: { label: 'Executive Dashboard', path: '/executive', icon: 'LayoutDashboard' }, CEO: { label: 'Executive Dashboard', path: '/executive', icon: 'LayoutDashboard' }, HR: { label: 'HR Dashboard', path: '/hr', icon: 'LayoutDashboard' }, Admin: { label: 'Admin Dashboard', path: '/admin/users', icon: 'LayoutDashboard' } },
  Tasks:              { _default: { label: 'Tasks Management',    path: '/tasks-management',   icon: 'ListTodo'      } },
  Projects:           { _default: { label: 'Projects',            path: '/projects',            icon: 'FolderKanban'  } },
  Evaluations:        { _default: { label: 'My Evaluations',      path: '/evaluations',         icon: 'Star'          } },
  Reports:            { _default: { label: 'Reports',             path: '/reports',             icon: 'BarChart3'     } },
  'HR Communications':{ _default: { label: 'Communication Center',path: '/hr/communications',  icon: 'MessageSquare' } },
  'Users Management': { _default: { label: 'Users Management',    path: '/admin/users',         icon: 'Users'         } },
  'Role Matrix':      { _default: { label: 'Role Matrix',         path: '/admin/roles',         icon: 'Shield'        } },
  'AI Control':       { _default: { label: 'AI Control Panel',    path: '/admin/ai',            icon: 'Brain'         } },
  'Audit Logs':       { _default: { label: 'Audit Logs',          path: '/admin/audit',         icon: 'FileText'      } },
  Settings:           { _default: { label: 'Settings',            path: '/settings',            icon: 'Settings'      } },
};

const PAGE_ROUTES = {
  Dashboard:          { Employee: ['/dashboard'], Supervisor: ['/supervisor'], Manager: ['/executive'], CEO: ['/executive'], HR: ['/hr'], Admin: ['/admin/users'] },
  Tasks:              { _default: ['/tasks-management'] },
  Projects:           { _default: ['/projects'] },
  Evaluations:        { _default: ['/evaluations'] },
  Reports:            { _default: ['/reports'] },
  'HR Communications':{ _default: ['/hr/communications'] },
  'Users Management': { _default: ['/admin/users'] },
  'Role Matrix':      { _default: ['/admin/roles'] },
  'AI Control':       { _default: ['/admin/ai'] },
  'Audit Logs':       { _default: ['/admin/audit'] },
  Settings:           { _default: ['/settings'] },
};

const ALWAYS_ACCESSIBLE = ['/notifications', '/directory', '/tasks/', '/profile/'];

const DEFAULT_ROUTE = {
  Employee: '/dashboard', Supervisor: '/supervisor', Manager: '/executive',
  CEO: '/executive', HR: '/hr', Admin: '/admin/users',
};

const FALLBACK_NAV = {
  Employee:   [{ label: 'Dashboard',          path: '/dashboard',        icon: 'LayoutDashboard' }, { label: 'My Evaluations', path: '/evaluations', icon: 'Star' }, { label: 'Org Directory', path: '/directory', icon: 'Building2' }, { label: 'Notifications', path: '/notifications', icon: 'Bell' }, { label: 'Settings', path: '/settings', icon: 'Settings' }],
  Supervisor: [{ label: 'Dashboard',          path: '/supervisor',       icon: 'LayoutDashboard' }, { label: 'My Tasks', path: '/dashboard', icon: 'CheckSquare' }, { label: 'Tasks Management', path: '/tasks-management', icon: 'ListTodo' }, { label: 'My Evaluations', path: '/evaluations', icon: 'Star' }, { label: 'Projects', path: '/projects', icon: 'FolderKanban' }, { label: 'Reports', path: '/reports', icon: 'BarChart3' }, { label: 'Role Matrix', path: '/admin/roles', icon: 'Shield' }, { label: 'Org Directory', path: '/directory', icon: 'Building2' }, { label: 'Notifications', path: '/notifications', icon: 'Bell' }, { label: 'Settings', path: '/settings', icon: 'Settings' }],
  Manager:    [{ label: 'Executive Dashboard',path: '/executive',        icon: 'LayoutDashboard' }, { label: 'My Evaluations', path: '/evaluations', icon: 'Star' }, { label: 'Projects', path: '/projects', icon: 'FolderKanban' }, { label: 'Tasks Management', path: '/tasks-management', icon: 'ListTodo' }, { label: 'Reports', path: '/reports', icon: 'BarChart3' }, { label: 'Role Matrix', path: '/admin/roles', icon: 'Shield' }, { label: 'Org Directory', path: '/directory', icon: 'Building2' }, { label: 'Audit Logs', path: '/admin/audit', icon: 'FileText' }, { label: 'Notifications', path: '/notifications', icon: 'Bell' }, { label: 'Settings', path: '/settings', icon: 'Settings' }],
  CEO:        [{ label: 'Executive Dashboard',path: '/executive',        icon: 'LayoutDashboard' }, { label: 'My Evaluations', path: '/evaluations', icon: 'Star' }, { label: 'Projects', path: '/projects', icon: 'FolderKanban' }, { label: 'Tasks Management', path: '/tasks-management', icon: 'ListTodo' }, { label: 'Reports', path: '/reports', icon: 'BarChart3' }, { label: 'Communication Center', path: '/hr/communications', icon: 'MessageSquare' }, { label: 'Users Management', path: '/admin/users', icon: 'Users' }, { label: 'AI Control Panel', path: '/admin/ai', icon: 'Brain' }, { label: 'Role Matrix', path: '/admin/roles', icon: 'Shield' }, { label: 'Org Directory', path: '/directory', icon: 'Building2' }, { label: 'Audit Logs', path: '/admin/audit', icon: 'FileText' }, { label: 'Notifications', path: '/notifications', icon: 'Bell' }, { label: 'Settings', path: '/settings', icon: 'Settings' }],
  HR:         [{ label: 'HR Dashboard',       path: '/hr',               icon: 'LayoutDashboard' }, { label: 'Communication Center', path: '/hr/communications', icon: 'MessageSquare' }, { label: 'Reports', path: '/reports', icon: 'BarChart3' }, { label: 'Role Matrix', path: '/admin/roles', icon: 'Shield' }, { label: 'Org Directory', path: '/directory', icon: 'Building2' }, { label: 'Notifications', path: '/notifications', icon: 'Bell' }, { label: 'Settings', path: '/settings', icon: 'Settings' }],
  Admin:      [{ label: 'Users Management',   path: '/admin/users',      icon: 'Users'          }, { label: 'My Evaluations', path: '/evaluations', icon: 'Star' }, { label: 'Projects', path: '/projects', icon: 'FolderKanban' }, { label: 'Tasks Management', path: '/tasks-management', icon: 'ListTodo' }, { label: 'AI Control Panel', path: '/admin/ai', icon: 'Brain' }, { label: 'Role Matrix', path: '/admin/roles', icon: 'Shield' }, { label: 'Org Directory', path: '/directory', icon: 'Building2' }, { label: 'Audit Logs', path: '/admin/audit', icon: 'FileText' }, { label: 'Notifications', path: '/notifications', icon: 'Bell' }, { label: 'Settings', path: '/settings', icon: 'Settings' }],
};

// ── nav builder ───────────────────────────────────────────────────────────────
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

  function buildNavFromPermissions(role, permissions) {
    const roleByte    = ROLE_BYTE_MAP[role];
    const grantedPages = new Set();
    
    // Always grant Dashboard and Settings for all users
    grantedPages.add('Dashboard'); 
    grantedPages.add('Settings');

    for (const p of permissions)
      if (p.role === roleByte && (p.action === 'access' || p.action === 'read') && p.canPerform) {
        const mappedName = DB_RESOURCE_MAP[p.resource] || p.resource;
        grantedPages.add(mappedName);
      }

  const nav        = [];
  const addedPaths = new Set();
  for (const pageName of Object.keys(PAGE_NAV_MAP)) {
    if (!grantedPages.has(pageName)) continue;
    const item = PAGE_NAV_MAP[pageName][role] || PAGE_NAV_MAP[pageName]._default;
    if (item && !addedPaths.has(item.path)) { nav.push(item); addedPaths.add(item.path); }
  }
  if (!addedPaths.has('/directory'))    nav.push({ label: 'Org Directory',  path: '/directory',    icon: 'Building2' });
  if (!addedPaths.has('/notifications'))nav.push({ label: 'Notifications',  path: '/notifications', icon: 'Bell'      });
  if (!addedPaths.has('/settings') && grantedPages.has('Settings'))
    nav.push({ label: 'Settings', path: '/settings', icon: 'Settings' });
  return nav;
}

function buildAllowedRoutes(role, permissions) {
  const roleByte = ROLE_BYTE_MAP[role];
  const routes   = new Set(ALWAYS_ACCESSIBLE);
  for (const p of permissions) {
      if (p.role === roleByte && (p.action === 'access' || p.action === 'read') && p.canPerform) {
        const mappedName = DB_RESOURCE_MAP[p.resource] || p.resource;
        const routeMap = PAGE_ROUTES[mappedName] || PAGE_ROUTES[p.resource];
      if (routeMap) {
        const pageRoutes = routeMap[role] || routeMap._default || [];
        for (const r of pageRoutes) routes.add(r);
      }
    }
  }
  return routes;
}

// ── storage helpers ───────────────────────────────────────────────────────────
function readStoredUser() {
  const sessUser = sessionStorage.getItem('user');
  const sessTok  = sessionStorage.getItem('accessToken');
  if (sessTok && sessUser) return normalizeAuthUser(JSON.parse(sessUser));
  const locUser = localStorage.getItem('user');
  const locTok  = localStorage.getItem('accessToken');
  if (locTok && locUser) return normalizeAuthUser(JSON.parse(locUser));
  return null;
}

// ── provider ──────────────────────────────────────────────────────────────────
export function AuthProvider({ children }) {
  const [user, setUser]                       = useState(() => readStoredUser());
  const [permissions, setPermissions]         = useState([]);
  const [permissionsLoaded, setPermissionsLoaded] = useState(false);
  const [loading, setLoading]                 = useState(false);
  const [error, setError]                     = useState(null);

  const fetchPermissions = useCallback(async () => {
    try {
      const { data } = await api.get('/role-permissions');
      const items = Array.isArray(data) ? data : data?.items || [];
      setPermissions(items);
    } catch {
      setPermissions([]);
    } finally {
      setPermissionsLoaded(true);
    }
  }, []);

  useEffect(() => {
    const token  = getAccessToken();
    const stored = readStoredUser();
    if (token && stored) {
      setUser(normalizeAuthUser(stored));
      fetchPermissions();
    } else {
      setPermissionsLoaded(true);
    }
  }, [fetchPermissions]);

  const login = useCallback(async (email, password, rememberMe = false) => {
    setLoading(true);
    setError(null);
    try {
      const { data } = await authService.login(email, password);
      const userData = normalizeAuthUser(data.user);
      persistAuth({ accessToken: data.accessToken, refreshToken: data.refreshToken, user: userData }, rememberMe);
      setUser(userData);
      await fetchPermissions();
      return userData;
    } catch (err) {
      const msg = err.response?.data?.message || 'Login failed. Please check your credentials.';
      setError(msg);
      throw new Error(msg);
    } finally {
      setLoading(false);
    }
  }, [fetchPermissions]);

  const logout = useCallback(async () => {
    try { await authService.logout(); } catch { /* ignore */ }
    finally {
      clearAllAuthStorage();
      setUser(null);
      setPermissions([]);
      setPermissionsLoaded(false);
    }
  }, []);

  const getNav = useCallback(() => {
    if (!user) return [];
    if (permissions.length > 0) {
      const nav = buildNavFromPermissions(user.role, permissions);
      const forcedByRole = {
        Supervisor: [{ label: 'Role Matrix', path: '/admin/roles', icon: 'Shield' }],
        Manager: [{ label: 'My Evaluations', path: '/evaluations', icon: 'Star' }, { label: 'Role Matrix', path: '/admin/roles', icon: 'Shield' }],
        CEO: [{ label: 'My Evaluations', path: '/evaluations', icon: 'Star' }, { label: 'Communication Center', path: '/hr/communications', icon: 'MessageSquare' }, { label: 'Users Management', path: '/admin/users', icon: 'Users' }, { label: 'Role Matrix', path: '/admin/roles', icon: 'Shield' }],
        HR: [{ label: 'Role Matrix', path: '/admin/roles', icon: 'Shield' }],
        Admin: [{ label: 'Users Management', path: '/admin/users', icon: 'Users' }, { label: 'My Evaluations', path: '/evaluations', icon: 'Star' }, { label: 'Role Matrix', path: '/admin/roles', icon: 'Shield' }],
      };
      for (const item of forcedByRole[user.role] || []) {
        if (!nav.some((n) => n.path === item.path)) {
          const settingsIdx = nav.findIndex((n) => n.path === '/settings');
          if (settingsIdx >= 0) nav.splice(settingsIdx, 0, item);
          else nav.push(item);
        }
      }
      return nav;
    }
    return FALLBACK_NAV[user.role] || [];
  }, [user, permissions]);

  const getDefaultRoute = useCallback(() =>
    user ? DEFAULT_ROUTE[user.role] || '/dashboard' : '/login',
  [user]);

  // FIX: return false when no route matches instead of always true
  const canAccessRoute = useCallback((pathname) => {
    if (!user) return false;
    if (!permissionsLoaded) return false;
    if (permissions.length === 0) return true;

    const allowedRoutes = buildAllowedRoutes(user.role, permissions);
    const forcedRoutes = {
      Supervisor: ['/admin/roles'],
      Manager: ['/admin/roles', '/evaluations'],
      CEO: ['/evaluations', '/hr/communications', '/admin/users', '/admin/roles'],
      HR: ['/admin/roles'],
      Admin: ['/evaluations', '/admin/users', '/admin/roles'],
    };
    for (const route of forcedRoutes[user.role] || []) {
      allowedRoutes.add(route);
    }
    for (const route of allowedRoutes) {
      if (pathname === route) return true;
      if (route.endsWith('/') && pathname.startsWith(route)) return true;
    }
    return false;
  }, [user, permissions, permissionsLoaded]);

  return (
    <AuthContext.Provider
      value={{ user, login, logout, getNav, getDefaultRoute, canAccessRoute, permissions, permissionsLoaded, loading, error, setError }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export const useAuth = () => {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be inside AuthProvider');
  return ctx;
};
