import { useState } from 'react';
import { NavLink, useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import {
  LayoutDashboard, Star, Building2, Bell, CheckSquare, ListTodo,
  FolderKanban, BarChart3, Brain, Shield, Users, FileText,
  MessageSquare, ChevronLeft, ChevronRight, LogOut, Zap, Settings
} from 'lucide-react';

const iconMap = {
  LayoutDashboard, Star, Building2, Bell, CheckSquare, ListTodo,
  FolderKanban, BarChart3, Brain, Shield, Users, FileText, MessageSquare, Settings,
};

export default function Sidebar() {
  const [collapsed, setCollapsed] = useState(false);
  const { user, logout, getNav } = useAuth();
  const navigate = useNavigate();
  const navItems = getNav();

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  return (
    <aside className={`fixed top-0 left-0 h-screen bg-surface border-r border-border flex flex-col z-40 transition-all duration-300 ${collapsed ? 'w-[72px]' : 'w-[260px]'}`}>
      {/* Logo */}
      <div className="h-16 flex items-center gap-3 px-5 border-b border-border shrink-0">
        <div className="w-8 h-8 bg-orange rounded-lg flex items-center justify-center shrink-0">
          <Zap size={18} className="text-white" />
        </div>
        {!collapsed && <span className="text-lg font-bold text-white tracking-tight">TaskTune AI</span>}
      </div>

      {/* Navigation */}
      <nav className="flex-1 overflow-y-auto py-4 px-3">
        <div className="space-y-1">
          {navItems.map((item) => {
            const Icon = iconMap[item.icon] || LayoutDashboard;
            return (
              <NavLink
                key={item.path}
                to={item.path}
                className={({ isActive }) =>
                  `flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm transition-colors ${
                    isActive
                      ? 'bg-orange-light text-orange font-medium'
                      : 'text-text-secondary hover:bg-surface-hover hover:text-white'
                  }`
                }
              >
                <Icon size={20} className="shrink-0" />
                {!collapsed && <span>{item.label}</span>}
              </NavLink>
            );
          })}
        </div>
      </nav>

      {/* User & Collapse */}
      <div className="border-t border-border p-3 space-y-2">
        {!collapsed && user && (
          <div className="flex items-center gap-3 px-3 py-2">
            <div className="w-8 h-8 rounded-full bg-orange/20 flex items-center justify-center text-orange text-sm font-medium">
              {user.name.charAt(0)}
            </div>
            <div className="min-w-0">
              <p className="text-sm font-medium text-white truncate">{user.name}</p>
              <p className="text-xs text-text-muted truncate">{user.role}</p>
            </div>
          </div>
        )}
        <div className="flex items-center gap-2">
          <button
            onClick={handleLogout}
            className="flex items-center gap-2 px-3 py-2 text-sm text-text-secondary hover:text-danger rounded-lg hover:bg-surface-hover transition-colors flex-1"
          >
            <LogOut size={18} />
            {!collapsed && <span>Logout</span>}
          </button>
          <button
            onClick={() => setCollapsed(!collapsed)}
            className="p-2 text-text-secondary hover:text-white rounded-lg hover:bg-surface-hover transition-colors"
          >
            {collapsed ? <ChevronRight size={18} /> : <ChevronLeft size={18} />}
          </button>
        </div>
      </div>
    </aside>
  );
}
