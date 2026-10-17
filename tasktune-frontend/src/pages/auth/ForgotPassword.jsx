import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Zap, ArrowLeft, CheckCircle } from 'lucide-react';
import { authService } from '../../services/authService';

export default function ForgotPassword() {
  const [email, setEmail] = useState('');
  const [sent, setSent] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const navigate = useNavigate();

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      await authService.forgotPassword(email);
      setSent(true);
    } catch (err) {
      setError(err?.response?.data?.message || err?.message || 'Failed to send reset link. Please try again.');
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
          <h1 className="text-2xl font-bold text-white">Reset Password</h1>
          <p className="text-sm text-text-secondary mt-1">We'll send a reset link to your email</p>
        </div>

        <div className="bg-surface border border-border rounded-xl p-6">
          {sent ? (
            <div className="text-center py-6">
              <CheckCircle size={48} className="text-success mx-auto mb-4" />
              <h3 className="text-lg font-medium text-white mb-2">Check Your Email</h3>
              <p className="text-sm text-text-secondary mb-6">
                A password reset request has been sent to our technical team. You'll receive instructions shortly.
              </p>
              <button onClick={() => navigate('/login')} className="px-6 py-2.5 bg-orange hover:bg-orange-hover text-white font-medium rounded-lg transition-colors">
                Back to Login
              </button>
            </div>
          ) : (
            <form onSubmit={handleSubmit} className="space-y-4">
              {error && (
                <div className="p-3 bg-error/10 border border-error/30 rounded-lg text-sm text-error">{error}</div>
              )}
              <div>
                <label className="block text-sm text-text-secondary mb-1.5">Email Address</label>
                <input
                  type="email"
                  value={email}
                  onChange={e => setEmail(e.target.value)}
                  required
                  className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-white placeholder:text-text-muted focus:outline-none focus:border-orange transition-colors"
                  placeholder="you@company.com"
                />
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
