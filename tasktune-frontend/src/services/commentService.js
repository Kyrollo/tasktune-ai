import api from './api';

export const commentService = {
  getComments: (taskId) => api.get(`/tasks/${taskId}/comments`),
  addComment: (taskId, content) => api.post(`/tasks/${taskId}/comments`, { content }),
};
