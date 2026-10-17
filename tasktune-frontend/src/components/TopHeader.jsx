import { useState, useEffect } from 'react';
import { Bell, Search } from 'lucide-react';
import { useAuth } from '../contexts/AuthContext';
import { useNavigate } from 'react-router-dom';
import { notificationService } from '../services/notificationService';
import { searchService } from '../services/searchService';

export default function TopHeader({ title }) {
  const { user } = useAuth();
  const navigate = useNavigate();
  const [unreadCount, setUnreadCount] = useState(0);
  const [searchQuery, setSearchQuery] = useState('');
  const [searchResults, setSearchResults] = useState([]);
  const [showResults, setShowResults] = useState(false);

  useEffect(() => {
    const fetchCount = async () => {
      try {
        const { data } = await notificationService.getNotifications();
        const items = Array.isArray(data) ? data : data?.items || [];
        setUnreadCount(items.filter(n => !n.isRead && n.isRead !== true && n.isRead !== 1).length);
      } catch {
        setUnreadCount(0);
      }
    };
    if (user) fetchCount();
    const interval = setInterval(() => { if (user) fetchCount(); }, 30000);
    return () => clearInterval(interval);
  }, [user]);

  const handleSearch = async (query) => {
    setSearchQuery(query);
    if (query.trim().length < 2) {
      setSearchResults([]);
      setShowResults(false);
      return;
    }
    try {
      const { data } = await searchService.search(query);
      const results = Array.isArray(data) ? data : data?.items || [];
      setSearchResults(results.slice(0, 8));
      setShowResults(true);
    } catch {
      setSearchResults([]);
    }
  };

  return (
    <header className="h-16 bg-surface border-b border-border flex items-center justify-between px-6 shrink-0">
      <h1 className="text-xl font-semibold text-white">{title}</h1>

      <div className="flex items-center gap-4">
        {/* Search */}
        <div className="relative hidden md:block">
          <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-text-muted" />
          <input
            type="text"
            placeholder="Search..."
            value={searchQuery}
            onChange={(e) => handleSearch(e.target.value)}
            onBlur={() => setTimeout(() => setShowResults(false), 200)}
            onFocus={() => searchQuery.trim().length >= 2 && setShowResults(true)}
            className="bg-bg border border-border rounded-lg pl-9 pr-4 py-2 text-sm text-white placeholder:text-text-muted focus:outline-none focus:border-orange w-64 transition-colors"
          />
          {showResults && searchResults.length > 0 && (
            <div className="absolute top-full left-0 right-0 mt-1 bg-surface border border-border rounded-lg shadow-lg z-50 max-h-64 overflow-y-auto">
              {searchResults.map((r, i) => (
                <button key={i} onClick={() => { setShowResults(false); setSearchQuery(''); navigate(r.url || '#'); }} className="w-full text-left px-4 py-2 hover:bg-surface-hover transition-colors">
                  <p className="text-sm text-white">{r.title || r.name || 'Result'}</p>
                  <p className="text-xs text-text-muted">{r.type || 'item'}</p>
                </button>
              ))}
            </div>
          )}
        </div>

        {/* Notifications */}
        <button
          onClick={() => navigate('/notifications')}
          className="relative p-2 text-text-secondary hover:text-white rounded-lg hover:bg-surface-hover transition-colors"
        >
          <Bell size={20} />
          {unreadCount > 0 && (
            <span className="absolute -top-0.5 -right-0.5 w-4 h-4 bg-orange text-[10px] text-white rounded-full flex items-center justify-center font-medium">
              {unreadCount}
            </span>
          )}
        </button>

        {/* User */}
        {user && (
          <div className="flex items-center gap-3">
            <div className="w-8 h-8 rounded-full bg-orange/20 flex items-center justify-center text-orange text-sm font-medium">
              {user.name.charAt(0)}
            </div>
            <div className="hidden md:block">
              <p className="text-sm font-medium text-white">{user.name}</p>
              <p className="text-xs text-text-muted">{user.role}</p>
            </div>
          </div>
        )}
      </div>
    </header>
  );
}
