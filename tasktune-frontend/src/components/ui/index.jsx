export function Card({ children, className = '', ...props }) {
  return (
    <div className={`bg-surface border border-border rounded-xl p-5 ${className}`} {...props}>
      {children}
    </div>
  );
}

export function StatCard({ label, value, icon: Icon, trend, trendUp }) {
  return (
    <Card className="flex items-start justify-between">
      <div>
        <p className="text-sm text-text-secondary mb-1">{label}</p>
        <p className="text-2xl font-bold text-text-primary">{value}</p>
        {trend && (
          <p className={`text-xs mt-1 ${trendUp ? 'text-success' : 'text-danger'}`}>
            {trendUp ? '↑' : '↓'} {trend}
          </p>
        )}
      </div>
      {Icon && (
        <div className="w-10 h-10 rounded-lg bg-orange-light flex items-center justify-center">
          <Icon size={20} className="text-orange" />
        </div>
      )}
    </Card>
  );
}

export function Badge({ children, variant = 'default' }) {
  const styles = {
    default: 'bg-border text-text-secondary',
    success: 'bg-success/10 text-success',
    warning: 'bg-warning/10 text-warning',
    danger: 'bg-danger/10 text-danger',
    info: 'bg-info/10 text-info',
    orange: 'bg-orange/10 text-orange',
  };
  return (
    <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${styles[variant] || styles.default}`}>
      {children}
    </span>
  );
}

export function Button({ children, variant = 'primary', className = '', ...props }) {
  const styles = {
    primary: 'bg-orange hover:bg-orange-hover text-white',
    secondary: 'bg-surface-hover hover:bg-border text-text-primary border border-border',
    ghost: 'hover:bg-surface-hover text-text-secondary hover:text-text-primary',
    danger: 'bg-danger/10 hover:bg-danger/20 text-danger',
  };
  return (
    <button className={`px-4 py-2 rounded-lg text-sm font-medium transition-colors ${styles[variant]} ${className}`} {...props}>
      {children}
    </button>
  );
}

export function Input({ label, className = '', ...props }) {
  return (
    <div className={className}>
      {label && <label className="block text-sm text-text-secondary mb-1.5">{label}</label>}
      <input
        className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-text-primary placeholder:text-text-muted focus:outline-none focus:border-orange transition-colors"
        {...props}
      />
    </div>
  );
}

export function Select({ label, options, className = '', ...props }) {
  return (
    <div className={className}>
      {label && <label className="block text-sm text-text-secondary mb-1.5">{label}</label>}
      <select
        className="w-full bg-bg border border-border rounded-lg px-4 py-2.5 text-sm text-text-primary focus:outline-none focus:border-orange transition-colors appearance-none"
        {...props}
      >
        {options.map(opt => (
          <option key={opt.value} value={opt.value}>{opt.label}</option>
        ))}
      </select>
    </div>
  );
}

export function DataTable({ columns, data, onRowClick }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full">
        <thead>
          <tr className="border-b border-border">
            {columns.map(col => (
              <th key={col.key} className="text-left py-3 px-4 text-xs font-medium text-text-muted uppercase tracking-wider">
                {col.label}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {data.map((row, i) => (
            <tr
              key={row.id || i}
              onClick={() => onRowClick?.(row)}
              className={`border-b border-border/50 transition-colors ${onRowClick ? 'cursor-pointer hover:bg-surface-hover' : ''}`}
            >
              {columns.map(col => (
                <td key={col.key} className="py-3 px-4 text-sm text-text-secondary">
                  {col.render ? col.render(row[col.key], row) : row[col.key]}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export function ProgressBar({ value, size = 'md' }) {
  const heights = { sm: 'h-1.5', md: 'h-2', lg: 'h-3' };
  const color = value >= 80 ? 'bg-success' : value >= 50 ? 'bg-orange' : value >= 25 ? 'bg-warning' : 'bg-danger';
  return (
    <div className={`w-full bg-border rounded-full ${heights[size]}`}>
      <div className={`${color} ${heights[size]} rounded-full transition-all duration-500`} style={{ width: `${value}%` }} />
    </div>
  );
}

export function Modal({ open, isOpen, onClose, title, children, size = 'md' }) {
  if (!(open || isOpen)) return null;
  const sizes = {
    sm: 'max-w-md',
    md: 'max-w-lg',
    lg: 'max-w-3xl',
    xl: 'max-w-5xl',
    full: 'max-w-[90vw]',
  };
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div className="absolute inset-0 bg-black/60 backdrop-blur-sm" onClick={onClose} />
      <div className={`relative bg-surface border border-border rounded-xl p-6 w-full ${sizes[size] || sizes.md} max-h-[90vh] overflow-y-auto transition-all duration-300`}>
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-lg font-semibold text-text-primary">{title}</h2>
          <button onClick={onClose} className="text-text-muted hover:text-text-primary text-xl leading-none">&times;</button>
        </div>
        {children}
      </div>
    </div>
  );
}

export function Tabs({ tabs, active, onChange }) {
  return (
    <div className="flex gap-1 bg-bg border border-border rounded-lg p-1">
      {tabs.map(tab => (
        <button
          key={tab.value}
          onClick={() => onChange(tab.value)}
          className={`px-4 py-2 rounded-md text-sm font-medium transition-colors ${
            active === tab.value ? 'bg-orange text-white' : 'text-text-secondary hover:text-text-primary'
          }`}
        >
          {tab.label}
        </button>
      ))}
    </div>
  );
}

export function EmptyState({ icon: Icon, title, description }) {
  return (
    <div className="flex flex-col items-center justify-center py-16 text-center">
      {Icon && <Icon size={48} className="text-text-muted mb-4" />}
      <h3 className="text-lg font-medium text-text-primary mb-2">{title}</h3>
      <p className="text-sm text-text-secondary max-w-md">{description}</p>
    </div>
  );
}
