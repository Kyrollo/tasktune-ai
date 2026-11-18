import api from './api';

export const communicationService = {
  getCommunications: (page = 1, pageSize = 25) => api.get(`/communications?page=${page}&pageSize=${pageSize}`),
  getOverview: () => api.get('/communications/overview'),
  getRecommendation: (data) => api.post('/communications/recommendation', data),
  createCommunication: (data) => api.post('/communications', data),
  getApprovals: (status = 'Pending', page = 1, pageSize = 50) => api.get(`/communications/approvals?status=${encodeURIComponent(status)}&page=${page}&pageSize=${pageSize}`),
  approveCommunication: (approvalId) => api.post(`/communications/approvals/${approvalId}/approve`),
  rejectCommunication: (approvalId, reason) => api.post(`/communications/approvals/${approvalId}/reject`, { reason }),
  getDeadLetterOutbox: (page = 1, pageSize = 50) => api.get(`/communications/outbox/dead-letter?page=${page}&pageSize=${pageSize}`),
  getOutboxMetrics: () => api.get('/communications/outbox/metrics'),
  replayDeadLetter: (outboxId, reason, providerConfirmedNotAccepted = false) => api.post(`/communications/outbox/${outboxId}/replay`, { reason, providerConfirmedNotAccepted }),
};
