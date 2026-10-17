import { useCallback, useEffect, useState } from 'react';
import { useLocation } from 'react-router-dom';
import { MessageCircle, Plus, Send, X, Loader2 } from 'lucide-react';
import { chatService } from '../services/chatService';

function getContextPayload(pathname) {
  const taskMatch = pathname.match(/^\/tasks\/(\d+)/);
  if (taskMatch) {
    return { relatedTaskId: Number(taskMatch[1]) };
  }

  const projectMatch = pathname.match(/^\/projects\/(\d+)/);
  if (projectMatch) {
    return { relatedProjectId: Number(projectMatch[1]) };
  }

  return {};
}

export default function ChatbotWidget() {
  const location = useLocation();
  const [open, setOpen] = useState(false);
  const [threads, setThreads] = useState([]);
  const [currentThreadId, setCurrentThreadId] = useState(null);
  const [messages, setMessages] = useState([]);
  const [input, setInput] = useState('');
  const [loadingThreads, setLoadingThreads] = useState(false);
  const [loadingMessages, setLoadingMessages] = useState(false);
  const [sending, setSending] = useState(false);

  const loadThread = useCallback(async (threadId) => {
    setLoadingMessages(true);
    try {
      const { data } = await chatService.getThread(threadId);
      setCurrentThreadId(data.id ?? data.threadId);
      setMessages(data.messages || []);
    } catch (err) {
      console.error('Failed to load chat thread:', err);
    } finally {
      setLoadingMessages(false);
    }
  }, []);

  const loadThreads = useCallback(async () => {
    setLoadingThreads(true);
    try {
      const { data } = await chatService.getThreads();
      const items = data?.items || [];
      setThreads(items);
      if (!currentThreadId && items.length > 0) {
        await loadThread(items[0].id ?? items[0].threadId);
      }
    } catch (err) {
      console.error('Failed to load chat threads:', err);
    } finally {
      setLoadingThreads(false);
    }
  }, [currentThreadId, loadThread]);

  useEffect(() => {
    if (open) {
      loadThreads();
    }
  }, [open, loadThreads]);

  const handleNewThread = async () => {
    try {
      const { data } = await chatService.createThread(getContextPayload(location.pathname));
      const newThread = {
        id: data.id ?? data.threadId,
        title: data.title,
        updatedAt: data.updatedAt,
      };
      setThreads(prev => [newThread, ...prev]);
      setCurrentThreadId(newThread.id);
      setMessages([]);
    } catch (err) {
      console.error('Failed to create chat thread:', err);
    }
  };

  const ensureThread = async () => {
    if (currentThreadId) return currentThreadId;
    const { data } = await chatService.createThread(getContextPayload(location.pathname));
    const id = data.id ?? data.threadId;
    setThreads(prev => [{ id, title: data.title, updatedAt: data.updatedAt }, ...prev]);
    setCurrentThreadId(id);
    setMessages([]);
    return id;
  };

  const handleSend = async () => {
    const content = input.trim();
    if (!content || sending) return;

    setSending(true);
    setInput('');

    try {
      const threadId = await ensureThread();
      const { data } = await chatService.sendMessage(threadId, content);
      setCurrentThreadId(data.threadId);
      setMessages(prev => [
        ...prev,
        data.userMessage,
        data.assistantMessage,
      ]);
      setThreads(prev => {
        const next = [...prev];
        const idx = next.findIndex(t => (t.id ?? t.threadId) === data.threadId);
        if (idx >= 0) {
          next[idx] = {
            ...next[idx],
            title: data.threadTitle,
            lastMessage: data.assistantMessage?.content,
            updatedAt: data.assistantMessage?.sentAt,
          };
          const [thread] = next.splice(idx, 1);
          next.unshift(thread);
          return next;
        }
        return [{ id: data.threadId, title: data.threadTitle, updatedAt: data.assistantMessage?.sentAt }, ...next];
      });
    } catch (err) {
      console.error('Failed to send chat message:', err);
      setMessages(prev => [
        ...prev,
        {
          id: `error-${Date.now()}`,
          role: 'assistant',
          content: 'I could not send that message right now. Please try again.',
          sentAt: new Date().toISOString(),
        },
      ]);
    } finally {
      setSending(false);
    }
  };

  return (
    <>
      <button
        onClick={() => setOpen(prev => !prev)}
        className="fixed bottom-6 right-6 z-50 h-14 w-14 rounded-full bg-orange text-white shadow-lg shadow-orange/20 flex items-center justify-center hover:bg-orange-hover transition-colors"
        aria-label="Open chatbot"
      >
        {open ? <X size={22} /> : <MessageCircle size={22} />}
        <span className="absolute -top-1 -right-1 h-3.5 w-3.5 rounded-full bg-info border-2 border-bg" />
      </button>

      {open && (
        <div className="fixed bottom-24 right-6 z-50 w-[min(92vw,430px)] h-[min(70vh,620px)] bg-surface border border-border rounded-2xl shadow-2xl overflow-hidden flex flex-col">
          <div className="px-4 py-3 border-b border-border flex items-center justify-between">
            <div>
              <h3 className="text-sm font-semibold text-text-primary">TaskTune AI Assistant</h3>
              <p className="text-xs text-text-muted">Role-aware answers from your permitted company data</p>
            </div>
            <button
              onClick={handleNewThread}
              className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-orange/10 text-orange text-xs font-medium hover:bg-orange/15 transition-colors"
            >
              <Plus size={14} />
              New Thread
            </button>
          </div>

          <div className="border-b border-border px-3 py-2 overflow-x-auto">
            <div className="flex gap-2">
              {loadingThreads && <span className="text-xs text-text-muted px-2 py-1">Loading threads...</span>}
              {!loadingThreads && threads.length === 0 && <span className="text-xs text-text-muted px-2 py-1">Start a new conversation.</span>}
              {threads.map(thread => (
                <button
                  key={thread.id ?? thread.threadId}
                  onClick={() => loadThread(thread.id ?? thread.threadId)}
                  className={`shrink-0 max-w-[160px] px-3 py-1.5 rounded-lg text-left text-xs border transition-colors ${
                    (thread.id ?? thread.threadId) === currentThreadId
                      ? 'border-orange/40 bg-orange/10 text-text-primary'
                      : 'border-border bg-bg text-text-secondary hover:text-text-primary'
                  }`}
                >
                  <div className="truncate font-medium">{thread.title || 'New Chat'}</div>
                </button>
              ))}
            </div>
          </div>

          <div className="flex-1 overflow-y-auto px-4 py-3 space-y-3 bg-bg/40">
            {loadingMessages ? (
              <div className="h-full flex items-center justify-center text-text-muted text-sm">
                <Loader2 size={18} className="animate-spin mr-2" />
                Loading conversation...
              </div>
            ) : messages.length === 0 ? (
              <div className="h-full flex items-center justify-center text-center text-sm text-text-muted px-6">
                Ask about tasks, projects, reports, evaluations, or notifications. The assistant only uses data your role can access.
              </div>
            ) : (
              messages.map(message => (
                <div
                  key={message.id}
                  className={`max-w-[88%] rounded-2xl px-3 py-2 text-sm whitespace-pre-line ${
                    message.role === 'user'
                      ? 'ml-auto bg-orange text-white'
                      : 'bg-surface border border-border text-text-primary'
                  }`}
                >
                  {message.content}
                </div>
              ))
            )}
          </div>

          <div className="p-3 border-t border-border bg-surface">
            <div className="flex items-end gap-2">
              <textarea
                value={input}
                onChange={(e) => setInput(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter' && !e.shiftKey) {
                    e.preventDefault();
                    handleSend();
                  }
                }}
                rows={2}
                placeholder="Ask TaskTune AI Assistant..."
                className="flex-1 bg-bg border border-border rounded-xl px-3 py-2 text-sm text-text-primary placeholder:text-text-muted focus:outline-none focus:border-orange resize-none"
              />
              <button
                id="chatbot-send-btn"
                onClick={handleSend}
                disabled={sending || !input.trim()}
                className="h-11 w-11 rounded-xl bg-orange text-white flex items-center justify-center disabled:opacity-50 disabled:cursor-not-allowed hover:bg-orange-hover transition-colors"
              >
                {sending ? <Loader2 size={18} className="animate-spin" /> : <Send size={18} />}
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
