import api from './api';

export const communicationService = {
  getCommunications: (page = 1, pageSize = 25) => api.get(`/communications?page=${page}&pageSize=${pageSize}`),
  getOverview: () => api.get('/communications/overview'),
  getRecommendation: (data) => api.post('/communications/recommendation', data),
  createCommunication: (data) => api.post('/communications', data),
  getPendingApprovals: (page = 1, pageSize = 50) => api.get(`/communications/approvals/pending?page=${page}&pageSize=${pageSize}`),
  approve: (approvalId, note = null) => api.post(`/communications/approvals/${approvalId}/approve`, { note }),
  reject: (approvalId, note = null) => api.post(`/communications/approvals/${approvalId}/reject`, { note }),
};
