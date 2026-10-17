import { Routes, Route, Navigate, useLocation } from 'react-router-dom';
import { useAuth } from './contexts/AuthContext';

import Landing from './pages/Landing';
import Login from './pages/auth/Login';
import SignUp from './pages/auth/SignUp';
import ForgotPassword from './pages/auth/ForgotPassword';
import EmployeeDashboard from './pages/employee/Dashboard';
import TaskDetails from './pages/employee/TaskDetails';
import SubmitProgress from './pages/employee/SubmitProgress';
import MyEvaluations from './pages/employee/MyEvaluations';
import SupervisorDashboard from './pages/supervisor/Dashboard';
import TaskReview from './pages/supervisor/TaskReview';
import ExecutiveDashboard from './pages/executive/Dashboard';
import Reports from './pages/executive/Reports';
import HRDashboard from './pages/hr/Dashboard';
import CommunicationCenter from './pages/hr/CommunicationCenter';
import Projects from './pages/core/Projects';
import ProjectDetails from './pages/core/ProjectDetails';
import TasksManagement from './pages/core/TasksManagement';
import UsersManagement from './pages/admin/UsersManagement';
import AIControlPanel from './pages/admin/AIControlPanel';
import RoleMatrix from './pages/admin/RoleMatrix';
import AuditLogs from './pages/admin/AuditLogs';
import OrgChart from './pages/shared/OrgChart';
import EmployeeProfile from './pages/shared/EmployeeProfile';
import Notifications from './pages/shared/Notifications';
import Settings from './pages/shared/Settings';

function ProtectedRoute({ children, allowedRoles }) {
  const { user, getDefaultRoute, canAccessRoute, permissions, permissionsLoaded } = useAuth();
  const location = useLocation();

  if (!user) return <Navigate to="/login" replace />;

  const hasRoleAccess = !allowedRoles || allowedRoles.includes(user.role);
  const canUsePermissionFallback = !!allowedRoles && !hasRoleAccess;

  if (canUsePermissionFallback && !permissionsLoaded) {
    return (
      <div className="min-h-screen bg-bg flex items-center justify-center text-text-secondary">
        Loading access...
      </div>
    );
  }

  const hasPermissionAccess =
    canUsePermissionFallback &&
    permissions.length > 0 &&
    canAccessRoute(location.pathname);

  if (!hasRoleAccess && !hasPermissionAccess) {
    return <Navigate to={getDefaultRoute()} replace />;
  }

  return children;
}

export default function App() {
  return (
    <Routes>
      {/* Public */}
      <Route path="/" element={<Landing />} />
      <Route path="/login" element={<Login />} />
      <Route path="/signup" element={<SignUp />} />
      <Route path="/forgot-password" element={<ForgotPassword />} />

      {/* Protected */}
      <Route path="/dashboard" element={<ProtectedRoute><EmployeeDashboard /></ProtectedRoute>} />
      <Route path="/tasks/:id" element={<ProtectedRoute><TaskDetails /></ProtectedRoute>} />
      <Route path="/tasks/:id/submit" element={<ProtectedRoute><SubmitProgress /></ProtectedRoute>} />
      <Route path="/evaluations" element={<ProtectedRoute><MyEvaluations /></ProtectedRoute>} />
      <Route path="/supervisor" element={<ProtectedRoute allowedRoles={['Supervisor','Manager','CEO','Admin']}><SupervisorDashboard /></ProtectedRoute>} />
      <Route path="/supervisor/review/:id" element={<ProtectedRoute allowedRoles={['Supervisor','Manager','CEO','Admin']}><TaskReview /></ProtectedRoute>} />
      <Route path="/executive" element={<ProtectedRoute allowedRoles={['Manager','CEO','Admin']}><ExecutiveDashboard /></ProtectedRoute>} />
      <Route path="/reports" element={<ProtectedRoute allowedRoles={['Supervisor','Manager','CEO','HR','Admin']}><Reports /></ProtectedRoute>} />
      <Route path="/hr" element={<ProtectedRoute allowedRoles={['HR','CEO','Admin']}><HRDashboard /></ProtectedRoute>} />
      <Route path="/hr/communications" element={<ProtectedRoute allowedRoles={['HR','CEO','Admin']}><CommunicationCenter /></ProtectedRoute>} />
      <Route path="/projects" element={<ProtectedRoute><Projects /></ProtectedRoute>} />
      <Route path="/projects/:id" element={<ProtectedRoute><ProjectDetails /></ProtectedRoute>} />
      <Route path="/tasks-management" element={<ProtectedRoute><TasksManagement /></ProtectedRoute>} />
      <Route path="/admin/users" element={<ProtectedRoute allowedRoles={['Admin','CEO']}><UsersManagement /></ProtectedRoute>} />
      <Route path="/admin/ai" element={<ProtectedRoute allowedRoles={['Admin','CEO']}><AIControlPanel /></ProtectedRoute>} />
      <Route path="/admin/roles" element={<ProtectedRoute allowedRoles={['Supervisor','Manager','HR','Admin','CEO']}><RoleMatrix /></ProtectedRoute>} />
      <Route path="/admin/audit" element={<ProtectedRoute allowedRoles={['Admin','Manager','CEO']}><AuditLogs /></ProtectedRoute>} />
      <Route path="/directory" element={<ProtectedRoute><OrgChart /></ProtectedRoute>} />
      <Route path="/profile/:id" element={<ProtectedRoute><EmployeeProfile /></ProtectedRoute>} />
      <Route path="/notifications" element={<ProtectedRoute><Notifications /></ProtectedRoute>} />
      <Route path="/settings" element={<ProtectedRoute><Settings /></ProtectedRoute>} />

      {/* Fallback */}
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
