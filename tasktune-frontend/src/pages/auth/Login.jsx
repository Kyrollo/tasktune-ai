import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { Zap, Eye, EyeOff, Loader2 } from 'lucide-react';

export default function Login() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showPw, setShowPw] = useState(false);
  const [rememberMe, setRememberMe] = useState(false);
  const { login, loading, error, setError } = useAuth();
  const navigate = useNavigate();

  const ROLE_ROUTES = {
    Employee: '/dashboard',
    Supervisor: '/supervisor',
    Manager: '/executive',
    CEO: '/executive',
    HR: '/hr',
    Admin: '/admin/users',
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError(null);
    if (!email || !password) {
      setError('Please enter both email and password.');
      return;
    }
    try {
      const userData = await login(email, password, rememberMe);
      navigate(ROLE_ROUTES[userData.role] || '/dashboard');
    } catch {
      // error is already set in AuthContext
    }
  };

  const demoAccounts = [
    { label: 'Employee', email: 'employee@tasktune.ai' },
    { label: 'Supervisor', email: 'supervisor@tasktune.ai' },
    { label: 'Manager', email: 'manager@tasktune.ai' },
    { label: 'CEO', email: 'ceo@tasktune.ai' },
    { label: 'HR', email: 'hr@tasktune.ai' },
    { label: 'Admin', email: 'admin@tasktune.ai' },
  ];

  const fillDemo = (account) => {
    setEmail(account.email);
    setPassword('Password123!');
    setError(null);
  };

  return (
    <div className="min-h-screen bg-bg flex items-center justify-center px-4">
      <div className="w-full max-w-md">
        <div className="text-center mb-8">
          <div className="w-14 h-14 bg-orange rounded-2xl flex items-center justify-center mx-auto mb-4">
            <Zap size={28} className="text-white" />
          </div>
          <h1 className="text-2xl font-bold text-white">Welcome Back</h1>
          <p className="text-sm text-text-secondary mt-1">Sign in to TaskTune AI</p>
        </div>

        <div className="bg-surface border border-border rounded-xl p-6">
          {error && (
            <div className="mb-4 p-3 bg-red-500/10 border border-red-500/30 rounded-lg text-red-400 text-sm">
              {error}
            </div>
          )}

          <form onSubmit={handleSubmit} className="space-y-4">
            <div>
              <label className="block text-sm text-text-secondary mb-1.5">Email</label>
              <input
                type="email"
                value={email}
                onChange={e => setEmail(e.target.value)}
                className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-white placeholder:text-text-muted focus:outline-none focus:border-orange transition-colors"
                placeholder="you@company.com"
              />
            </div>

            <div>
              <label className="block text-sm text-text-secondary mb-1.5">Password</label>
              <div className="relative">
                <input
                  type={showPw ? 'text' : 'password'}
                  value={password}
                  onChange={e => setPassword(e.target.value)}
                  className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-white placeholder:text-text-muted focus:outline-none focus:border-orange transition-colors pr-10"
                  placeholder="••••••••"
                />
                <button type="button" onClick={() => setShowPw(!showPw)} className="absolute right-3 top-1/2 -translate-y-1/2 text-text-muted hover:text-white">
                  {showPw ? <EyeOff size={16} /> : <Eye size={16} />}
                </button>
              </div>
            </div>

            <div className="flex items-center justify-between">
              <label className="flex items-center gap-2 cursor-pointer">
                <input type="checkbox" checked={rememberMe} onChange={(e) => setRememberMe(e.target.checked)} className="w-4 h-4 rounded border-border accent-orange" />
                <span className="text-sm text-text-secondary">Remember me</span>
              </label>
              <button type="button" onClick={() => navigate('/forgot-password')} className="text-sm text-orange hover:text-orange-hover">
                Forgot password?
              </button>
            </div>

            <button
              type="submit"
              disabled={loading}
              className="w-full bg-orange hover:bg-orange-hover text-white font-medium py-2.5 rounded-lg transition-colors disabled:opacity-50 flex items-center justify-center gap-2"
            >
              {loading ? <><Loader2 size={16} className="animate-spin" /> Signing in...</> : 'Sign In'}
            </button>
          </form>

          <div className="mt-5 pt-5 border-t border-border">
            <p className="text-xs text-text-muted mb-3 text-center">Quick login as demo user:</p>
            <div className="grid grid-cols-3 gap-2">
              {demoAccounts.map((acc) => (
                <button
                  key={acc.label}
                  type="button"
                  onClick={() => fillDemo(acc)}
                  className="text-xs bg-bg border border-border rounded-lg py-2 px-2 text-text-secondary hover:text-white hover:border-orange transition-colors"
                >
                  {acc.label}
                </button>
              ))}
            </div>
          </div>
        </div>

        <p className="text-center text-sm text-text-secondary mt-6">
          Don't have an account?{' '}
          <button onClick={() => navigate('/signup')} className="text-orange hover:text-orange-hover font-medium">
            Sign Up
          </button>
        </p>
        <p className="text-center text-xs text-text-muted mt-3">
          &copy; 2026 TaskTune AI
        </p>
      </div>
    </div>
  );
}
