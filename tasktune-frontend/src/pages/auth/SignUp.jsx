import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { authService } from '../../services/authService';
import {
  Zap, Eye, EyeOff, Building2, UserPlus,
  ArrowRight, ArrowLeft, Plus, X, Check,
} from 'lucide-react';

const STEPS_EMPLOYEE = ['type', 'invite', 'details'];
const STEPS_OWNER    = ['type', 'company', 'org', 'account'];

export default function SignUp() {
  const navigate = useNavigate();
  const [accountType, setAccountType] = useState(null);
  const [submitting, setSubmitting]   = useState(false);
  const [apiError, setApiError]       = useState(null);
  const [step, setStep]               = useState('type');
  const [showPw, setShowPw]           = useState(false);

  // Employee fields
  const [inviteCode, setInviteCode] = useState('');
  const [empForm, setEmpForm]       = useState({ name: '', email: '', password: '', confirmPassword: '' });

  // Owner fields
  const [companyForm, setCompanyForm] = useState({ companyName: '', industry: '', size: '', website: '' });
  const [departments, setDepartments] = useState(['']);
  const [positions, setPositions]     = useState(['']);
  const [ownerForm, setOwnerForm]     = useState({ name: '', email: '', password: '', confirmPassword: '' });

  const getSteps          = () => accountType === 'employee' ? STEPS_EMPLOYEE : STEPS_OWNER;
  const getCurrentStepIndex = () => getSteps().indexOf(step);

  const goNext = () => {
    const steps = getSteps();
    const idx = steps.indexOf(step);
    if (idx < steps.length - 1) setStep(steps[idx + 1]);
  };

  const goBack = () => {
    const steps = getSteps();
    const idx = steps.indexOf(step);
    if (idx > 0) setStep(steps[idx - 1]);
    else { setStep('type'); setAccountType(null); }
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setApiError(null);
    setSubmitting(true);
    try {
      const form = accountType === 'employee' ? empForm : ownerForm;
      if (form.password !== form.confirmPassword) {
        setApiError('Passwords do not match.');
        setSubmitting(false);
        return;
      }
      const nameParts = form.name.trim().split(' ');
      const payload = {
        email:           form.email,
        password:        form.password,
        confirmPassword: form.confirmPassword,
        firstName:       nameParts[0] || '',
        lastName:        nameParts.slice(1).join(' ') || '',
        companyName:     accountType === 'owner' ? companyForm.companyName : null,
        inviteCode:      accountType === 'employee' ? inviteCode : null,
      };
      await authService.register(payload);
      navigate('/login');
    } catch (err) {
      setApiError(err.response?.data?.message || 'Registration failed. Please try again.');
    } finally {
      setSubmitting(false);
    }
  };

  const handleSkipOrg = () => setStep('account');

  const addDepartment    = () => setDepartments([...departments, '']);
  const removeDepartment = (i) => setDepartments(departments.filter((_, idx) => idx !== i));
  const updateDepartment = (i, val) => {
    const copy = [...departments]; copy[i] = val; setDepartments(copy);
  };

  const addPosition    = () => setPositions([...positions, '']);
  const removePosition = (i) => setPositions(positions.filter((_, idx) => idx !== i));
  const updatePosition = (i, val) => {
    const copy = [...positions]; copy[i] = val; setPositions(copy);
  };

  const renderStepIndicator = () => {
    const steps = getSteps();
    if (step === 'type') return null;
    return (
      <div className="flex items-center justify-center gap-2 mb-8">
        {steps.map((s, i) => (
          <div key={s} className="flex items-center gap-2">
            <div
              className={`w-8 h-8 rounded-full flex items-center justify-center text-xs font-medium transition-colors ${
                i <= getCurrentStepIndex() ? 'bg-orange text-white' : 'bg-bg border border-border text-text-muted'
              }`}
            >
              {i < getCurrentStepIndex() ? <Check size={14} /> : i + 1}
            </div>
            {i < steps.length - 1 && (
              <div className={`w-8 h-0.5 ${i < getCurrentStepIndex() ? 'bg-orange' : 'bg-border'}`} />
            )}
          </div>
        ))}
      </div>
    );
  };

  const renderTypeStep = () => (
    <div className="space-y-4">
      <h2 className="text-lg font-semibold text-text-primary text-center mb-2">How would you like to sign up?</h2>
      <p className="text-sm text-text-secondary text-center mb-6">Choose your account type to get started</p>
      <button
        onClick={() => { setAccountType('employee'); setStep('invite'); }}
        className="w-full p-5 bg-bg border border-border rounded-xl hover:border-orange/50 hover:bg-surface-hover transition-all text-left group"
      >
        <div className="flex items-start gap-4">
          <div className="w-12 h-12 bg-orange/10 rounded-xl flex items-center justify-center shrink-0 group-hover:bg-orange/20 transition-colors">
            <UserPlus size={24} className="text-orange" />
          </div>
          <div>
            <p className="text-text-primary font-medium mb-1">Join a Company</p>
            <p className="text-sm text-text-secondary">I have an invitation code from my company and want to join as an employee.</p>
          </div>
          <ArrowRight size={20} className="text-text-muted mt-1 shrink-0 group-hover:text-orange transition-colors" />
        </div>
      </button>
      <button
        onClick={() => { setAccountType('owner'); setStep('company'); }}
        className="w-full p-5 bg-bg border border-border rounded-xl hover:border-orange/50 hover:bg-surface-hover transition-all text-left group"
      >
        <div className="flex items-start gap-4">
          <div className="w-12 h-12 bg-orange/10 rounded-xl flex items-center justify-center shrink-0 group-hover:bg-orange/20 transition-colors">
            <Building2 size={24} className="text-orange" />
          </div>
          <div>
            <p className="text-text-primary font-medium mb-1">Create a Company</p>
            <p className="text-sm text-text-secondary">I'm a company owner and want to set up my organization on TaskTune.</p>
          </div>
          <ArrowRight size={20} className="text-text-muted mt-1 shrink-0 group-hover:text-orange transition-colors" />
        </div>
      </button>
    </div>
  );

  const renderInviteStep = () => (
    <div className="space-y-4">
      <h2 className="text-lg font-semibold text-text-primary text-center mb-1">Enter Invitation Code</h2>
      <p className="text-sm text-text-secondary text-center mb-6">Enter the code provided by your company administrator</p>
      <div>
        <label className="block text-sm text-text-secondary mb-1.5">Invitation Code</label>
        <input
          type="text"
          value={inviteCode}
          onChange={(e) => setInviteCode(e.target.value)}
          className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-text-primary placeholder:text-text-muted focus:outline-none focus:border-orange transition-colors tracking-widest text-center text-lg"
          placeholder="XXXX-XXXX-XXXX"
          maxLength={14}
        />
      </div>
      <div className="flex gap-3 pt-2">
        <button onClick={goBack} className="px-4 py-2.5 bg-surface-hover hover:bg-border text-text-primary border border-border rounded-lg text-sm font-medium transition-colors">
          <ArrowLeft size={16} className="inline mr-1" /> Back
        </button>
        <button
          onClick={goNext}
          disabled={!inviteCode.trim()}
          className="flex-1 bg-orange hover:bg-orange-hover disabled:opacity-40 disabled:cursor-not-allowed text-white font-medium py-2.5 rounded-lg transition-colors text-sm"
        >
          Continue <ArrowRight size={16} className="inline ml-1" />
        </button>
      </div>
    </div>
  );

  const renderEmployeeDetails = () => (
    <form onSubmit={handleSubmit} className="space-y-4">
      <h2 className="text-lg font-semibold text-text-primary text-center mb-1">Create Your Account</h2>
      <p className="text-sm text-text-secondary text-center mb-4">Fill in your personal details</p>
      {apiError && (
        <div className="p-3 bg-danger/10 border border-danger/30 rounded-lg text-danger text-sm">{apiError}</div>
      )}
      <div>
        <label className="block text-sm text-text-secondary mb-1.5">Full Name</label>
        <input type="text" value={empForm.name} onChange={(e) => setEmpForm({ ...empForm, name: e.target.value })} required
          className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-text-primary placeholder:text-text-muted focus:outline-none focus:border-orange transition-colors"
          placeholder="John Doe" />
      </div>
      <div>
        <label className="block text-sm text-text-secondary mb-1.5">Email Address</label>
        <input type="email" value={empForm.email} onChange={(e) => setEmpForm({ ...empForm, email: e.target.value })} required
          className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-text-primary placeholder:text-text-muted focus:outline-none focus:border-orange transition-colors"
          placeholder="you@company.com" />
      </div>
      <div>
        <label className="block text-sm text-text-secondary mb-1.5">Password</label>
        <div className="relative">
          <input type={showPw ? 'text' : 'password'} value={empForm.password}
            onChange={(e) => setEmpForm({ ...empForm, password: e.target.value })} required
            className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-text-primary placeholder:text-text-muted focus:outline-none focus:border-orange transition-colors pr-10"
            placeholder="••••••••" />
          <button type="button" onClick={() => setShowPw(!showPw)}
            className="absolute right-3 top-1/2 -translate-y-1/2 text-text-muted hover:text-text-primary">
            {showPw ? <EyeOff size={16} /> : <Eye size={16} />}
          </button>
        </div>
      </div>
      <div>
        <label className="block text-sm text-text-secondary mb-1.5">Confirm Password</label>
        <input type="password" value={empForm.confirmPassword}
          onChange={(e) => setEmpForm({ ...empForm, confirmPassword: e.target.value })} required
          className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-text-primary placeholder:text-text-muted focus:outline-none focus:border-orange transition-colors"
          placeholder="••••••••" />
      </div>
      <div className="flex gap-3 pt-2">
        <button type="button" onClick={goBack}
          className="px-4 py-2.5 bg-surface-hover hover:bg-border text-text-primary border border-border rounded-lg text-sm font-medium transition-colors">
          <ArrowLeft size={16} className="inline mr-1" /> Back
        </button>
        <button type="submit" disabled={submitting}
          className="flex-1 bg-orange hover:bg-orange-hover disabled:opacity-50 text-white font-medium py-2.5 rounded-lg transition-colors text-sm">
          {submitting ? 'Creating...' : 'Create Account'}
        </button>
      </div>
    </form>
  );

  const renderCompanyStep = () => (
    <div className="space-y-4">
      <h2 className="text-lg font-semibold text-text-primary text-center mb-1">Company Information</h2>
      <p className="text-sm text-text-secondary text-center mb-4">Tell us about your company</p>
      <div>
        <label className="block text-sm text-text-secondary mb-1.5">Company Name</label>
        <input type="text" value={companyForm.companyName}
          onChange={(e) => setCompanyForm({ ...companyForm, companyName: e.target.value })}
          className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-text-primary placeholder:text-text-muted focus:outline-none focus:border-orange transition-colors"
          placeholder="Acme Inc." />
      </div>
      <div>
        <label className="block text-sm text-text-secondary mb-1.5">Industry</label>
        <select value={companyForm.industry}
          onChange={(e) => setCompanyForm({ ...companyForm, industry: e.target.value })}
          className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-text-primary focus:outline-none focus:border-orange transition-colors appearance-none">
          <option value="">Select industry...</option>
          {['Technology','Healthcare','Finance','Education','Manufacturing','Retail','Consulting','Other'].map((i) => (
            <option key={i} value={i.toLowerCase()}>{i}</option>
          ))}
        </select>
      </div>
      <div>
        <label className="block text-sm text-text-secondary mb-1.5">Company Size</label>
        <select value={companyForm.size}
          onChange={(e) => setCompanyForm({ ...companyForm, size: e.target.value })}
          className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-text-primary focus:outline-none focus:border-orange transition-colors appearance-none">
          <option value="">Select size...</option>
          {['1-10','11-50','51-200','201-500','500+'].map((s) => (
            <option key={s} value={s}>{s} employees</option>
          ))}
        </select>
      </div>
      <div>
        <label className="block text-sm text-text-secondary mb-1.5">Website <span className="text-text-muted">(optional)</span></label>
        <input type="url" value={companyForm.website}
          onChange={(e) => setCompanyForm({ ...companyForm, website: e.target.value })}
          className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-text-primary placeholder:text-text-muted focus:outline-none focus:border-orange transition-colors"
          placeholder="https://company.com" />
      </div>
      <div className="flex gap-3 pt-2">
        <button onClick={goBack}
          className="px-4 py-2.5 bg-surface-hover hover:bg-border text-text-primary border border-border rounded-lg text-sm font-medium transition-colors">
          <ArrowLeft size={16} className="inline mr-1" /> Back
        </button>
        <button onClick={goNext} disabled={!companyForm.companyName.trim()}
          className="flex-1 bg-orange hover:bg-orange-hover disabled:opacity-40 disabled:cursor-not-allowed text-white font-medium py-2.5 rounded-lg transition-colors text-sm">
          Continue <ArrowRight size={16} className="inline ml-1" />
        </button>
      </div>
    </div>
  );

  const renderOrgStep = () => (
    <div className="space-y-5">
      <h2 className="text-lg font-semibold text-text-primary text-center mb-1">Organization Structure</h2>
      <p className="text-sm text-text-secondary text-center mb-2">
        Set up departments and positions. You can skip this and configure it later from the admin panel.
      </p>
      {/* Departments */}
      <div>
        <div className="flex items-center justify-between mb-2">
          <label className="text-sm font-medium text-text-primary">Departments</label>
          <button onClick={addDepartment} className="text-xs text-orange hover:text-orange-hover flex items-center gap-1 transition-colors">
            <Plus size={14} /> Add
          </button>
        </div>
        <div className="space-y-2">
          {departments.map((dep, i) => (
            <div key={i} className="flex gap-2">
              <input type="text" value={dep} onChange={(e) => updateDepartment(i, e.target.value)}
                className="flex-1 bg-bg border border-border rounded-lg px-4 py-2 text-sm text-text-primary placeholder:text-text-muted focus:outline-none focus:border-orange transition-colors"
                placeholder={`Department ${i + 1}`} />
              {departments.length > 1 && (
                <button onClick={() => removeDepartment(i)} className="p-2 text-text-muted hover:text-danger rounded-lg hover:bg-surface-hover transition-colors">
                  <X size={16} />
                </button>
              )}
            </div>
          ))}
        </div>
      </div>
      {/* Positions */}
      <div>
        <div className="flex items-center justify-between mb-2">
          <label className="text-sm font-medium text-text-primary">Positions</label>
          <button onClick={addPosition} className="text-xs text-orange hover:text-orange-hover flex items-center gap-1 transition-colors">
            <Plus size={14} /> Add
          </button>
        </div>
        <div className="space-y-2">
          {positions.map((pos, i) => (
            <div key={i} className="flex gap-2">
              <input type="text" value={pos} onChange={(e) => updatePosition(i, e.target.value)}
                className="flex-1 bg-bg border border-border rounded-lg px-4 py-2 text-sm text-text-primary placeholder:text-text-muted focus:outline-none focus:border-orange transition-colors"
                placeholder={`Position ${i + 1}`} />
              {positions.length > 1 && (
                <button onClick={() => removePosition(i)} className="p-2 text-text-muted hover:text-danger rounded-lg hover:bg-surface-hover transition-colors">
                  <X size={16} />
                </button>
              )}
            </div>
          ))}
        </div>
      </div>
      <div className="flex gap-3 pt-2">
        <button onClick={goBack}
          className="px-4 py-2.5 bg-surface-hover hover:bg-border text-text-primary border border-border rounded-lg text-sm font-medium transition-colors">
          <ArrowLeft size={16} className="inline mr-1" /> Back
        </button>
        <button onClick={handleSkipOrg}
          className="px-4 py-2.5 text-text-secondary hover:text-text-primary border border-border rounded-lg text-sm font-medium transition-colors">
          Skip for Now
        </button>
        <button onClick={goNext}
          className="flex-1 bg-orange hover:bg-orange-hover text-white font-medium py-2.5 rounded-lg transition-colors text-sm">
          Continue <ArrowRight size={16} className="inline ml-1" />
        </button>
      </div>
    </div>
  );

  const renderOwnerAccount = () => (
    <form onSubmit={handleSubmit} className="space-y-4">
      <h2 className="text-lg font-semibold text-text-primary text-center mb-1">Your Account</h2>
      <p className="text-sm text-text-secondary text-center mb-4">Create your admin account</p>
      {apiError && (
        <div className="p-3 bg-danger/10 border border-danger/30 rounded-lg text-danger text-sm">{apiError}</div>
      )}
      <div>
        <label className="block text-sm text-text-secondary mb-1.5">Full Name</label>
        <input type="text" value={ownerForm.name} onChange={(e) => setOwnerForm({ ...ownerForm, name: e.target.value })} required
          className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-text-primary placeholder:text-text-muted focus:outline-none focus:border-orange transition-colors"
          placeholder="John Doe" />
      </div>
      <div>
        <label className="block text-sm text-text-secondary mb-1.5">Email Address</label>
        <input type="email" value={ownerForm.email} onChange={(e) => setOwnerForm({ ...ownerForm, email: e.target.value })} required
          className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-text-primary placeholder:text-text-muted focus:outline-none focus:border-orange transition-colors"
          placeholder="you@company.com" />
      </div>
      <div>
        <label className="block text-sm text-text-secondary mb-1.5">Password</label>
        <div className="relative">
          <input type={showPw ? 'text' : 'password'} value={ownerForm.password}
            onChange={(e) => setOwnerForm({ ...ownerForm, password: e.target.value })} required
            className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-text-primary placeholder:text-text-muted focus:outline-none focus:border-orange transition-colors pr-10"
            placeholder="••••••••" />
          <button type="button" onClick={() => setShowPw(!showPw)}
            className="absolute right-3 top-1/2 -translate-y-1/2 text-text-muted hover:text-text-primary">
            {showPw ? <EyeOff size={16} /> : <Eye size={16} />}
          </button>
        </div>
      </div>
      <div>
        <label className="block text-sm text-text-secondary mb-1.5">Confirm Password</label>
        <input type="password" value={ownerForm.confirmPassword}
          onChange={(e) => setOwnerForm({ ...ownerForm, confirmPassword: e.target.value })} required
          className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-text-primary placeholder:text-text-muted focus:outline-none focus:border-orange transition-colors"
          placeholder="••••••••" />
      </div>
      <div className="flex gap-3 pt-2">
        <button type="button" onClick={goBack}
          className="px-4 py-2.5 bg-surface-hover hover:bg-border text-text-primary border border-border rounded-lg text-sm font-medium transition-colors">
          <ArrowLeft size={16} className="inline mr-1" /> Back
        </button>
        <button type="submit" disabled={submitting}
          className="flex-1 bg-orange hover:bg-orange-hover disabled:opacity-50 text-white font-medium py-2.5 rounded-lg transition-colors text-sm">
          {submitting ? 'Creating...' : 'Create Company Account'}
        </button>
      </div>
    </form>
  );

  const renderStep = () => {
    switch (step) {
      case 'type':    return renderTypeStep();
      case 'invite':  return renderInviteStep();
      case 'details': return renderEmployeeDetails();
      case 'company': return renderCompanyStep();
      case 'org':     return renderOrgStep();
      case 'account': return renderOwnerAccount();
      default:        return renderTypeStep();
    }
  };

  return (
    <div className="min-h-screen bg-bg flex items-center justify-center px-4 py-8">
      <div className="w-full max-w-lg">
        {/* Logo */}
        <div className="text-center mb-8">
          <div className="w-14 h-14 bg-orange rounded-2xl flex items-center justify-center mx-auto mb-4">
            <Zap size={28} className="text-white" />
          </div>
          <h1 className="text-2xl font-bold text-text-primary">Create an Account</h1>
          <p className="text-sm text-text-secondary mt-1">Get started with TaskTune AI</p>
        </div>

        {/* Step Indicator */}
        {renderStepIndicator()}

        {/* Form Card */}
        <div className="bg-surface border border-border rounded-xl p-6">
          {renderStep()}
        </div>

        {/* Footer */}
        <p className="text-center text-sm text-text-secondary mt-6">
          Already have an account?{' '}
          <button onClick={() => navigate('/login')} className="text-orange hover:text-orange-hover font-medium">
            Sign In
          </button>
        </p>
        <p className="text-center text-xs text-text-muted mt-3">&copy; 2026 TaskTune AI</p>
      </div>
    </div>
  );
}
