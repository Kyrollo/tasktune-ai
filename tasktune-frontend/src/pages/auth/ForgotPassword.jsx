import { useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { Zap, ArrowLeft, CheckCircle, LockKeyhole } from 'lucide-react';
import { authService } from '../../services/authService';

export default function ForgotPassword() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const resetToken = searchParams.get('token') || '';
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [sent, setSent] = useState(false);
  const [resetComplete, setResetComplete] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const handleRequest = async (event) => {
    event.preventDefault();
    setError('');
    setLoading(true);
    try {
      await authService.forgotPassword(email.trim().toLowerCase());
      setSent(true);
    } catch {
      // Keep the response generic so the page does not disclose whether an account exists.
      setSent(true);
    } finally {
      setLoading(false);
    }
  };

  const handleReset = async (event) => {
    event.preventDefault();
    setError('');
    if (password.length < 12) {
      setError('Use at least 12 characters for your new password.');
      return;
    }
    if (password !== confirmPassword) {
      setError('The passwords do not match.');
      return;
    }

    setLoading(true);
    try {
      await authService.resetPassword(resetToken, password, confirmPassword);
      setResetComplete(true);
    } catch (err) {
      setError(err?.response?.data?.message || 'The reset link is invalid, expired, or has already been used.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-bg flex items-center justify-center px-4">
      <div className="w-full max-w-md">
        <div className="text-center mb-8">
          <div className="w-14 h-14 bg-orange rounded-2xl flex items-center justify-center mx-auto mb-4">
            <Zap size={28} className="text-white" />
          </div>
          <h1 className="text-2xl font-bold text-white">{resetToken ? 'Choose a New Password' : 'Reset Password'}</h1>
          <p className="text-sm text-text-secondary mt-1">
            {resetToken ? 'Set a new password for your TaskTune account' : "We'll send a one-time reset link to your email"}
          </p>
        </div>

        <div className="bg-surface border border-border rounded-xl p-6">
          {resetComplete ? (
            <div className="text-center py-6">
              <CheckCircle size={48} className="text-success mx-auto mb-4" />
              <h3 className="text-lg font-medium text-white mb-2">Password Updated</h3>
              <p className="text-sm text-text-secondary mb-6">All previous sessions were revoked. Sign in using your new password.</p>
              <button onClick={() => navigate('/login', { replace: true })} className="px-6 py-2.5 bg-orange hover:bg-orange-hover text-white font-medium rounded-lg transition-colors">
                Back to Login
              </button>
            </div>
          ) : sent ? (
            <div className="text-center py-6">
              <CheckCircle size={48} className="text-success mx-auto mb-4" />
              <h3 className="text-lg font-medium text-white mb-2">Check Your Email</h3>
              <p className="text-sm text-text-secondary mb-6">
                When an active account matches that address, an email with a one-time reset link will arrive shortly.
              </p>
              <button onClick={() => navigate('/login')} className="px-6 py-2.5 bg-orange hover:bg-orange-hover text-white font-medium rounded-lg transition-colors">
                Back to Login
              </button>
            </div>
          ) : resetToken ? (
            <form onSubmit={handleReset} className="space-y-4">
              {error && <div className="p-3 bg-error/10 border border-error/30 rounded-lg text-sm text-error">{error}</div>}
              <div>
                <label className="block text-sm text-text-secondary mb-1.5">New Password</label>
                <input type="password" autoComplete="new-password" minLength={12} required value={password}
                  onChange={e => setPassword(e.target.value)}
                  className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-white focus:outline-none focus:border-orange transition-colors" />
              </div>
              <div>
                <label className="block text-sm text-text-secondary mb-1.5">Confirm New Password</label>
                <input type="password" autoComplete="new-password" minLength={12} required value={confirmPassword}
                  onChange={e => setConfirmPassword(e.target.value)}
                  className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-white focus:outline-none focus:border-orange transition-colors" />
              </div>
              <button type="submit" disabled={loading} className="w-full bg-orange hover:bg-orange-hover text-white font-medium py-2.5 rounded-lg transition-colors disabled:opacity-50 disabled:cursor-not-allowed">
                {loading ? 'Updating...' : 'Update Password'}
              </button>
            </form>
          ) : (
            <form onSubmit={handleRequest} className="space-y-4">
              {error && <div className="p-3 bg-error/10 border border-error/30 rounded-lg text-sm text-error">{error}</div>}
              <div>
                <label className="block text-sm text-text-secondary mb-1.5">Email Address</label>
                <input type="email" autoComplete="email" value={email} onChange={e => setEmail(e.target.value)} required
                  className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-white placeholder:text-text-muted focus:outline-none focus:border-orange transition-colors"
                  placeholder="you@company.com" />
              </div>
              <button type="submit" disabled={loading} className="w-full bg-orange hover:bg-orange-hover text-white font-medium py-2.5 rounded-lg transition-colors disabled:opacity-50 disabled:cursor-not-allowed">
                {loading ? 'Sending...' : 'Send Reset Link'}
              </button>
            </form>
          )}
        </div>

        <button onClick={() => navigate('/login')} className="flex items-center gap-2 text-sm text-text-secondary hover:text-white mx-auto mt-6 transition-colors">
          <ArrowLeft size={16} /> Back to Login
        </button>
      </div>
    </div>
  );
}
