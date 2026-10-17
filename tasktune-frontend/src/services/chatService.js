import api from './api';

export const chatService = {
  getThreads: () => api.get('/chatbot/threads'),
  createThread: (data = {}) => api.post('/chatbot/threads', data),
  getThread: (threadId) => api.get(`/chatbot/threads/${threadId}`),
  sendMessage: (threadId, content) => api.post(`/chatbot/threads/${threadId}/messages`, { content }),
};
