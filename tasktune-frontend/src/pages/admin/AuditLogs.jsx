import { useEffect, useMemo, useState } from 'react';
import AppLayout from '../../layouts/AppLayout';
import { Badge, Button, Card } from '../../components/ui';
import { auditService } from '../../services/auditService';
import { Download, FileText, Search } from 'lucide-react';

function getDetailsText(raw) {
  if (!raw) return '';
  if (typeof raw === 'string') {
    try {
      raw = JSON.parse(raw);
    } catch {
      return raw;
    }
  }

  const parts = [];
  if (raw.before) parts.push(`Before: ${JSON.stringify(raw.before)}`);
  if (raw.after) parts.push(`After: ${JSON.stringify(raw.after)}`);
  return parts.length > 0 ? parts.join(' -> ') : JSON.stringify(raw);
}

export default function AuditLogs() {
  const [auditLogs, setAuditLogs] = useState([]);
  const [loading, setLoading] = useState(true);
  const [exporting, setExporting] = useState(false);
  const [search, setSearch] = useState('');
  const [typeFilter, setTypeFilter] = useState('All');

  useEffect(() => {
    const fetchLogs = async () => {
      try {
        const { data } = await auditService.getLogs();
        setAuditLogs(Array.isArray(data) ? data : data?.items || []);
      } catch (error) {
        console.error('Failed to load audit logs:', error);
      } finally {
        setLoading(false);
      }
    };

    fetchLogs();
  }, []);

  const types = useMemo(
    () => ['All', ...new Set(auditLogs.map(log => log.resourceType).filter(Boolean))],
    [auditLogs],
  );

  const filteredLogs = useMemo(() => {
    const query = search.trim().toLowerCase();
    return auditLogs.filter(log => {
      const details = getDetailsText(log.changesJson || log.details).toLowerCase();
      return (
        (typeFilter === 'All' || log.resourceType === typeFilter) &&
        (!query ||
          (log.action || '').toLowerCase().includes(query) ||
          (log.resourceType || '').toLowerCase().includes(query) ||
          (log.userName || '').toLowerCase().includes(query) ||
          details.includes(query))
      );
    });
  }, [auditLogs, search, typeFilter]);

  const handleExport = async () => {
    setExporting(true);
    try {
      const response = await auditService.exportLogs(typeFilter);
      const blob = new Blob([response.data], { type: 'text/csv;charset=utf-8;' });
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.setAttribute('download', `audit-logs-${new Date().toISOString().slice(0, 19).replace(/[:T]/g, '-')}.csv`);
      document.body.appendChild(link);
      link.click();
      link.remove();
      window.URL.revokeObjectURL(url);
    } catch (error) {
      console.error('Failed to export audit logs:', error);
    } finally {
      setExporting(false);
    }
  };

  const typeVariant = {
    User: 'info',
    Task: 'warning',
    Task_Guidance: 'orange',
    Task_Progress: 'warning',
    Task_Progress_AI: 'danger',
    AI_Config: 'orange',
    Audit_Log: 'info',
    Communication: 'info',
    Project: 'default',
  };

  if (loading) {
    return (
      <AppLayout title="Audit Logs">
        <div className="flex items-center justify-center h-64">
          <div className="text-text-secondary">Loading audit logs...</div>
        </div>
      </AppLayout>
    );
  }

  return (
    <AppLayout title="Audit Logs">
      <div className="flex flex-wrap items-center justify-between mb-6 gap-3">
        <div className="flex items-center gap-2 flex-wrap">
          {types.map(type => (
            <button
              key={type}
              onClick={() => setTypeFilter(type)}
              className={`px-3 py-1.5 rounded-lg text-xs ${
                typeFilter === type
                  ? 'bg-orange text-text-primary'
                  : 'bg-surface border border-border text-text-secondary hover:text-text-primary'
              }`}
            >
              {type}
            </button>
          ))}
        </div>

        <div className="flex items-center gap-3">
          <div className="relative">
            <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-text-muted" />
            <input
              value={search}
              onChange={e => setSearch(e.target.value)}
              placeholder="Search audit logs..."
              className="pl-9 pr-4 py-2 bg-surface border border-border rounded-lg text-sm text-text-primary placeholder:text-text-muted focus:outline-none focus:border-orange"
            />
          </div>
          <Button variant="secondary" onClick={handleExport} disabled={exporting}>
            <span className="flex items-center gap-2">
              <Download size={16} />
              {exporting ? 'Exporting...' : 'Export'}
            </span>
          </Button>
        </div>
      </div>

      <Card>
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr className="border-b border-border">
                <th className="text-left py-3 px-4 text-xs text-text-muted font-medium">Timestamp</th>
                <th className="text-left py-3 px-4 text-xs text-text-muted font-medium">User</th>
                <th className="text-left py-3 px-4 text-xs text-text-muted font-medium">Type</th>
                <th className="text-left py-3 px-4 text-xs text-text-muted font-medium">Action</th>
                <th className="text-left py-3 px-4 text-xs text-text-muted font-medium">Details</th>
                <th className="text-left py-3 px-4 text-xs text-text-muted font-medium">IP Address</th>
              </tr>
            </thead>
            <tbody>
              {filteredLogs.map(log => (
                <tr key={log.logId ?? log.id} className="border-b border-border/50 hover:bg-surface-hover transition-colors">
                  <td className="py-3 px-4 text-sm text-text-secondary whitespace-nowrap">{new Date(log.timestamp).toLocaleString()}</td>
                  <td className="py-3 px-4 text-sm text-text-primary">{log.userName || 'Unknown'}</td>
                  <td className="py-3 px-4">
                    <Badge variant={typeVariant[log.resourceType] || 'default'}>
                      {(log.resourceType || 'Unknown').replace(/_/g, ' ')}
                    </Badge>
                  </td>
                  <td className="py-3 px-4 text-sm text-text-primary">{log.action}</td>
                  <td className="py-3 px-4 text-sm text-text-secondary max-w-sm truncate">{getDetailsText(log.changesJson || log.details)}</td>
                  <td className="py-3 px-4 text-sm text-text-muted font-mono">{log.ipAddress || '-'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {filteredLogs.length === 0 && (
          <div className="text-center py-8">
            <FileText size={32} className="text-text-muted mx-auto mb-2" />
            <p className="text-sm text-text-muted">No audit logs match the current filters.</p>
          </div>
        )}
      </Card>
    </AppLayout>
  );
}
