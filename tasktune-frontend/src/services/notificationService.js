import api from './api';

export const notificationService = {
  getNotifications: () => api.get('/notifications'),
  markRead: (id) => api.put(`/notifications/${id}/mark-read`),
  markAllRead: () => api.post('/notifications/mark-all-read'),
  deleteNotification: (id) => api.delete(`/notifications/${id}`),
};
