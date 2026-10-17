import { useNavigate, Link } from 'react-router-dom';
import { Zap, BarChart3, Brain, Users, Shield, CheckCircle, ArrowRight } from 'lucide-react';

const features = [
  { icon: Brain, title: 'AI-Powered Intelligence', desc: 'Smart task guidance, automated evaluations, and predictive insights powered by advanced AI agents.' },
  { icon: BarChart3, title: 'Real-Time Analytics', desc: 'Interactive dashboards with KPIs, performance trends, and department-level analytics.' },
  { icon: Users, title: 'Team Collaboration', desc: 'Streamlined workflows with role-based access for Employees, Supervisors, Managers, HR, and Admins.' },
  { icon: Shield, title: 'Enterprise Security', desc: 'Role-based access control, audit logging, and granular permission management.' },
  { icon: CheckCircle, title: 'Smart Task Management', desc: 'Projects, tasks, subtasks, dependencies, and automated progress tracking all in one place.' },
  { icon: Zap, title: 'HR & Communication Hub', desc: 'Employee engagement monitoring, automated communications, and performance reviews.' },
];

const roles = [
  { name: 'Employee', desc: 'Track tasks, submit progress, view evaluations, get AI guidance.' },
  { name: 'Supervisor', desc: 'Review team work, evaluate performance, report bugs, manage tasks.' },
  { name: 'Manager / CEO', desc: 'Executive dashboards, company KPIs, strategic reports, AI controls.' },
  { name: 'HR', desc: 'Monitor engagement, send communications, track attendance patterns.' },
  { name: 'Admin', desc: 'Manage users, configure AI, audit system activity, control roles.' },
];

export default function Landing() {
  const navigate = useNavigate();

  return (
    <div className="min-h-screen bg-bg">
      {/* Nav */}
      <nav className="fixed top-0 w-full bg-bg/80 backdrop-blur-md border-b border-border z-50">
        <div className="max-w-7xl mx-auto px-6 h-16 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="w-9 h-9 bg-orange rounded-lg flex items-center justify-center">
              <Zap size={20} className="text-white" />
            </div>
            <span className="text-xl font-bold text-white">TaskTune AI</span>
          </div>
          <div className="flex items-center gap-4">
            <Link to="/login" className="px-5 py-2 text-sm text-text-secondary hover:text-white transition-colors">
              Sign In
            </Link>
            <Link to="/signup" className="px-5 py-2 bg-orange hover:bg-orange-hover text-white text-sm font-medium rounded-lg transition-colors inline-block">
              Get Started
            </Link>
          </div>
        </div>
      </nav>

      {/* Hero */}
      <section className="pt-32 pb-20 px-6">
        <div className="max-w-4xl mx-auto text-center">
          <div className="inline-flex items-center gap-2 px-4 py-1.5 bg-orange/10 text-orange text-sm rounded-full mb-6">
            <Zap size={14} /> AI-Powered Work Management
          </div>
          <h1 className="text-5xl md:text-6xl font-bold text-white leading-tight mb-6">
            Manage Work Smarter<br />
            with <span className="text-orange">AI Intelligence</span>
          </h1>
          <p className="text-lg text-text-secondary max-w-2xl mx-auto mb-10">
            TaskTune AI is an integrated Work & HR Management System that combines intelligent task management,
            automated evaluations, and AI-driven insights to supercharge your team's productivity.
          </p>
          <div className="flex items-center gap-4 justify-center">
            <button onClick={() => navigate('/signup')} className="px-8 py-3 bg-orange hover:bg-orange-hover text-white font-medium rounded-lg transition-colors flex items-center gap-2">
              Start Free Trial <ArrowRight size={18} />
            </button>
            <button onClick={() => document.getElementById('features').scrollIntoView({ behavior: 'smooth' })} className="px-8 py-3 border border-border text-white hover:bg-surface-hover rounded-lg transition-colors">
              Learn More
            </button>
          </div>
        </div>
      </section>

      {/* Stats */}
      <section className="py-12 border-y border-border">
        <div className="max-w-5xl mx-auto px-6 grid grid-cols-2 md:grid-cols-4 gap-8 text-center">
          {[{ n: '5', l: 'AI Agents' }, { n: '25', l: 'System Pages' }, { n: '6', l: 'User Roles' }, { n: '25+', l: 'Database Models' }].map(s => (
            <div key={s.l}>
              <p className="text-3xl font-bold text-orange">{s.n}</p>
              <p className="text-sm text-text-secondary mt-1">{s.l}</p>
            </div>
          ))}
        </div>
      </section>

      {/* Features */}
      <section id="features" className="py-20 px-6">
        <div className="max-w-6xl mx-auto">
          <div className="text-center mb-14">
            <h2 className="text-3xl font-bold text-white mb-4">Everything You Need</h2>
            <p className="text-text-secondary max-w-lg mx-auto">A complete platform for managing tasks, teams, and performance with AI at the core.</p>
          </div>
          <div className="grid md:grid-cols-2 lg:grid-cols-3 gap-6">
            {features.map(f => (
              <div key={f.title} className="bg-surface border border-border rounded-xl p-6 hover:border-orange/30 transition-colors">
                <div className="w-12 h-12 bg-orange/10 rounded-lg flex items-center justify-center mb-4">
                  <f.icon size={24} className="text-orange" />
                </div>
                <h3 className="text-lg font-semibold text-white mb-2">{f.title}</h3>
                <p className="text-sm text-text-secondary leading-relaxed">{f.desc}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* Roles */}
      <section className="py-20 px-6 bg-surface/50">
        <div className="max-w-5xl mx-auto">
          <div className="text-center mb-14">
            <h2 className="text-3xl font-bold text-white mb-4">Built for Every Role</h2>
            <p className="text-text-secondary">Tailored dashboards and tools for each team member.</p>
          </div>
          <div className="grid md:grid-cols-2 lg:grid-cols-3 gap-5">
            {roles.map(r => (
              <div key={r.name} className="bg-surface border border-border rounded-xl p-5">
                <div className="w-10 h-10 bg-orange/10 rounded-lg flex items-center justify-center mb-3">
                  <Users size={20} className="text-orange" />
                </div>
                <h3 className="text-base font-semibold text-white mb-1">{r.name}</h3>
                <p className="text-sm text-text-secondary">{r.desc}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* CTA */}
      <section className="py-20 px-6">
        <div className="max-w-3xl mx-auto text-center">
          <h2 className="text-3xl font-bold text-white mb-4">Ready to Transform Your Workflow?</h2>
          <p className="text-text-secondary mb-8">Join teams using TaskTune AI to boost productivity with intelligent management.</p>
          <button onClick={() => navigate('/signup')} className="px-8 py-3 bg-orange hover:bg-orange-hover text-white font-medium rounded-lg transition-colors">
            Get Started Now
          </button>
        </div>
      </section>

      {/* Footer */}
      <footer className="border-t border-border py-8 px-6">
        <div className="max-w-6xl mx-auto flex items-center justify-between">
          <div className="flex items-center gap-2">
            <div className="w-7 h-7 bg-orange rounded-md flex items-center justify-center">
              <Zap size={14} className="text-white" />
            </div>
            <span className="text-sm font-semibold text-white">TaskTune AI</span>
          </div>
          <p className="text-xs text-text-muted">&copy; 2026 TaskTune AI. All rights reserved.</p>
        </div>
      </footer>
    </div>
  );
}
