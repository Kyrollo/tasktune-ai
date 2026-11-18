import api from './api';

export const platformService = {
  getTenants: (params = {}) => api.get('/platform/tenants', { params }),
  getTenant: (companyId) => api.get(`/platform/tenants/${companyId}`),
  getTenantUsage: (companyId, params = {}) => api.get(`/platform/tenants/${companyId}/usage`, { params }),
  suspendTenant: (companyId, reason) => api.post(`/platform/tenants/${companyId}/suspend`, { reason }),
  reactivateTenant: (companyId, reason) => api.post(`/platform/tenants/${companyId}/reactivate`, { reason }),
  getPlans: () => api.get('/platform/plans'),
  createPlan: (payload) => api.post('/platform/plans', payload),
  setPlanFeature: (planId, featureCode, payload) => api.put(`/platform/plans/${planId}/features/${encodeURIComponent(featureCode)}`, payload),
  getSubscription: (companyId) => api.get(`/platform/subscriptions/tenants/${companyId}`),
  assignSubscription: (companyId, payload) => api.put(`/platform/subscriptions/tenants/${companyId}`, payload),
};
