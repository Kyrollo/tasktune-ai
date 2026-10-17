import { useState, useEffect } from 'react';
import AppLayout from '../../layouts/AppLayout';
import { Card, Badge, Button } from '../../components/ui';
import { notificationService } from '../../services/notificationService';
import { Bell, Check, CheckCheck, Trash2 } from 'lucide-react';

export default function Notifications() {
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(true);
  const [filter, setFilter] = useState('All');
  const filters = ['All', 'Unread', 'Read'];

  const normalize = (n) => ({
    id: n.notificationId ?? n.id,
    type: n.notificationType ?? n.type ?? 'info',
    title: n.title ?? '',
    message: n.message ?? '',
    read: n.isRead === true || n.isRead === 1 || n.read === true || n.read === 1,
    date: n.sentAt ?? n.date ?? n.createdAt,
    relatedTaskId: n.relatedTaskId,
    relatedProjectId: n.relatedProjectId,
  });

  useEffect(() => {
    const fetchData = async () => {
      try {
        const { data } = await notificationService.getNotifications();
        const raw = Array.isArray(data) ? data : data?.items || [];
        setItems(raw.map(normalize));
      } catch (err) {
        console.error('Failed to load notifications:', err);
      } finally {
        setLoading(false);
      }
    };
    fetchData();
  }, []);

  const filtered = items.filter(n => {
    if (filter === 'Unread') return !n.read;
    if (filter === 'Read') return n.read;
    return true;
  });

  const markRead = async (id) => {
    try {
      await notificationService.markRead(id);
      setItems(items.map(n => n.id === id ? { ...n, read: true } : n));
    } catch (err) {
      console.error('Failed to mark read:', err);
    }
  };
  const markAllRead = async () => {
    try {
      await notificationService.markAllRead();
      setItems(items.map(n => ({ ...n, read: true })));
    } catch (err) {
      console.error('Failed to mark all read:', err);
    }
  };
  const remove = async (id) => {
    try {
      await notificationService.deleteNotification(id);
      setItems(items.filter(n => n.id !== id));
    } catch (err) {
      console.error('Failed to delete notification:', err);
    }
  };

  const typeIcon = { task: '📋', evaluation: '⭐', system: '⚙️', warning: '⚠️', info: 'ℹ️', reminder: '⏰', overdue: '🔴' };
  const typeVariant = { task: 'info', evaluation: 'orange', system: 'default', warning: 'warning', info: 'info', reminder: 'warning', overdue: 'danger' };

  if (loading) return (
    <AppLayout title="Notifications">
      <div className="flex items-center justify-center h-64"><div className="text-text-secondary">Loading...</div></div>
    </AppLayout>
  );

  return (
    <AppLayout title="Notifications">
      {/* Header */}
      <div className="flex items-center justify-between mb-6">
        <div className="flex items-center gap-3">
          {filters.map(f => (
            <button key={f} onClick={() => setFilter(f)} className={`px-3 py-1.5 rounded-lg text-sm ${filter === f ? 'bg-orange text-white' : 'bg-surface border border-border text-text-secondary hover:text-white'}`}>
              {f} {f === 'Unread' && `(${items.filter(n => !n.read).length})`}
            </button>
          ))}
        </div>
        <Button variant="secondary" onClick={markAllRead}>
          <span className="flex items-center gap-2"><CheckCheck size={16} /> Mark All Read</span>
        </Button>
      </div>

      {/* Notifications List */}
      <div className="space-y-2">
        {filtered.map(n => (
          <Card key={n.id} className={`transition-colors ${!n.read ? 'border-orange/20' : ''}`}>
            <div className="flex items-start gap-4">
              <span className="text-lg">{typeIcon[n.type] || '📌'}</span>
              <div className="flex-1">
                <div className="flex items-center gap-2 mb-1">
                  <p className={`text-sm font-medium ${!n.read ? 'text-white' : 'text-text-secondary'}`}>{n.title}</p>
                  {!n.read && <div className="w-2 h-2 rounded-full bg-orange" />}
                </div>
                <p className="text-sm text-text-muted">{n.message}</p>
                <div className="flex items-center gap-3 mt-2">
                  <span className="text-xs text-text-muted">{new Date(n.date || n.createdAt || 0).toLocaleString()}</span>
                  <Badge variant={typeVariant[n.type]}>{n.type}</Badge>
                </div>
              </div>
              <div className="flex items-center gap-1">
                {!n.read && (
                  <button onClick={() => markRead(n.id)} className="p-1.5 rounded hover:bg-surface-hover text-text-secondary hover:text-success" title="Mark as read">
                    <Check size={14} />
                  </button>
                )}
                <button onClick={() => remove(n.id)} className="p-1.5 rounded hover:bg-surface-hover text-text-secondary hover:text-danger" title="Remove">
                  <Trash2 size={14} />
                </button>
              </div>
            </div>
          </Card>
        ))}
      </div>

      {filtered.length === 0 && (
        <div className="text-center py-12">
          <Bell size={40} className="text-text-muted mx-auto mb-3" />
          <p className="text-text-secondary">{filter === 'Unread' ? 'No unread notifications!' : 'No notifications.'}</p>
        </div>
      )}
    </AppLayout>
  );
}
