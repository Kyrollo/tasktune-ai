import { useAuth } from '../../contexts/AuthContext';
import { useTheme } from '../../contexts/ThemeContext';
import { Sun, Moon, Monitor, Globe, Bell, Lock, User, Palette } from 'lucide-react';
import AppLayout from '../../layouts/AppLayout';
import { useState, useEffect } from 'react';
import { settingsService } from '../../services/settingsService';

export default function Settings() {
  const { user }            = useAuth();
  const { theme, setTheme } = useTheme();
  const [language, setLanguage]         = useState('en');
  const [notifications, setNotifications] = useState({ email: true, push: true, digest: false });
  const [saved, setSaved]         = useState(false);
  const [saveError, setSaveError] = useState('');

  useEffect(() => {
    settingsService
      .getSettings()
      .then(({ data }) => {
        const s = typeof data.settingsJson === 'string' ? JSON.parse(data.settingsJson) : data;
        if (s.theme)         setTheme(s.theme);
        if (s.language)      setLanguage(s.language);
        if (s.notifications) setNotifications(s.notifications);
      })
      .catch(() => {});
  }, [setTheme]);

  const handleSave = async () => {
    setSaveError('');
    try {
      await settingsService.saveSettings({ theme, language, notifications });
      setSaved(true);
      setTimeout(() => setSaved(false), 2000);
    } catch {
      setSaveError('Failed to save');
      setTimeout(() => setSaveError(''), 3000);
    }
  };

  const themeOptions = [
    { value: 'dark',   label: 'Dark',   icon: Moon,    desc: 'Easy on the eyes in low light' },
    { value: 'light',  label: 'Light',  icon: Sun,     desc: 'Classic bright appearance'     },
    { value: 'system', label: 'System', icon: Monitor, desc: 'Follows your OS preference'    },
  ];

  return (
    <AppLayout title="Settings">
      <div className="max-w-2xl space-y-6">

        {/* Appearance */}
        <div className="bg-surface border border-border rounded-xl p-5">
          <div className="flex items-center gap-3 mb-4">
            <Palette size={18} className="text-orange" />
            <div>
              <h2 className="text-sm font-semibold text-text-primary">Appearance</h2>
              <p className="text-xs text-text-muted">Customize how TaskTune looks</p>
            </div>
          </div>
          <p className="text-xs text-text-secondary mb-3">Theme</p>
          <div className="grid grid-cols-3 gap-3">
            {themeOptions.map((opt) => {
              const Icon = opt.icon;
              return (
                <button
                  key={opt.value}
                  onClick={() => setTheme(opt.value)}
                  className={`p-4 rounded-xl border text-left transition-all ${
                    theme === opt.value
                      ? 'border-orange bg-orange/5'
                      : 'border-border bg-bg hover:border-border-light hover:bg-surface-hover'
                  }`}
                >
                  <Icon size={20} className={theme === opt.value ? 'text-orange mb-2' : 'text-text-muted mb-2'} />
                  <p className="text-sm font-medium text-text-primary">{opt.label}</p>
                  <p className="text-xs text-text-muted mt-0.5">{opt.desc}</p>
                </button>
              );
            })}
          </div>
        </div>

        {/* Language */}
        <div className="bg-surface border border-border rounded-xl p-5">
          <div className="flex items-center gap-3 mb-4">
            <Globe size={18} className="text-orange" />
            <div>
              <h2 className="text-sm font-semibold text-text-primary">Language &amp; Region</h2>
              <p className="text-xs text-text-muted">Set your preferred language</p>
            </div>
          </div>
          <label className="block text-xs text-text-secondary mb-1.5">Language</label>
          <select
            value={language}
            onChange={(e) => setLanguage(e.target.value)}
            className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-text-primary focus:outline-none focus:border-orange transition-colors appearance-none"
          >
            <option value="en">English</option>
            <option value="ar">Arabic</option>
            <option value="fr">French</option>
            <option value="de">German</option>
            <option value="es">Spanish</option>
          </select>
        </div>

        {/* Notifications */}
        <div className="bg-surface border border-border rounded-xl p-5">
          <div className="flex items-center gap-3 mb-4">
            <Bell size={18} className="text-orange" />
            <div>
              <h2 className="text-sm font-semibold text-text-primary">Notifications</h2>
              <p className="text-xs text-text-muted">Manage how you receive notifications</p>
            </div>
          </div>
          <div className="space-y-4">
            {[
              { key: 'email',  label: 'Email Notifications',  desc: 'Receive updates via email'          },
              { key: 'push',   label: 'Push Notifications',   desc: 'Get browser push notifications'     },
              { key: 'digest', label: 'Weekly Digest',        desc: 'Summary of your weekly activity'    },
            ].map((item) => (
              <div key={item.key} className="flex items-center justify-between">
                <div>
                  <p className="text-sm text-text-primary">{item.label}</p>
                  <p className="text-xs text-text-muted">{item.desc}</p>
                </div>
                <button
                  onClick={() => setNotifications((prev) => ({ ...prev, [item.key]: !prev[item.key] }))}
                  className={`relative inline-flex items-center w-11 h-6 rounded-full transition-colors ${
                    notifications[item.key] ? 'bg-orange' : 'bg-border'
                  }`}
                >
                  <span
                    className={`absolute left-1 h-4 w-4 rounded-full bg-white shadow transition-transform ${
                      notifications[item.key] ? 'translate-x-5' : 'translate-x-0'
                    }`}
                  />
                </button>
              </div>
            ))}
          </div>
        </div>

        {/* Account */}
        <div className="bg-surface border border-border rounded-xl p-5">
          <div className="flex items-center gap-3 mb-4">
            <User size={18} className="text-orange" />
            <div>
              <h2 className="text-sm font-semibold text-text-primary">Account</h2>
              <p className="text-xs text-text-muted">Your account information</p>
            </div>
          </div>
          {user && (
            <div className="space-y-3">
              {[
                { label: 'Name',  value: user.name  },
                { label: 'Email', value: user.email },
                { label: 'Role',  value: user.role  },
              ].map((row) => (
                <div key={row.label} className="flex items-center justify-between py-2 border-b border-border last:border-0">
                  <span className="text-xs text-text-muted">{row.label}</span>
                  <span className="text-sm text-text-primary">{row.value}</span>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Save */}
        <div className="flex items-center gap-3">
          {saveError && <p className="text-sm text-danger">{saveError}</p>}
          <button
            onClick={handleSave}
            className={`ml-auto px-6 py-2.5 rounded-lg text-sm font-medium transition-colors ${
              saved
                ? 'bg-success/20 text-success border border-success/30'
                : 'bg-orange hover:bg-orange-hover text-white'
            }`}
          >
            {saved ? '✓ Saved' : 'Save Settings'}
          </button>
        </div>

      </div>
    </AppLayout>
  );
}
