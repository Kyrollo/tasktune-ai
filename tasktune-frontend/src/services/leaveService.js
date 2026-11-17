import api from './api';

export const leaveService = {
  getTypes: () => api.get('/leave/types'),
  createType: (data) => api.post('/leave/types', data),
  updateType: (id, data) => api.put(`/leave/types/${id}`, data),
  getMyBalances: (year) => api.get(`/leave/balances/me?year=${year}`),
  getEmployeeBalances: (userId, year) => api.get(`/leave/balances/${userId}?year=${year}`),
  setBalance: (userId, leaveTypeId, year, data) =>
    api.put(`/leave/balances/${userId}/${leaveTypeId}/${year}`, data),
  getMyRequests: (page = 1, pageSize = 50) =>
    api.get(`/leave/requests/me?page=${page}&pageSize=${pageSize}`),
  getApprovalInbox: (page = 1, pageSize = 50) =>
    api.get(`/leave/requests/inbox?page=${page}&pageSize=${pageSize}`),
  createRequest: (data) => api.post('/leave/requests', data),
  decideRequest: (id, data) => api.post(`/leave/requests/${id}/decision`, data),
  cancelRequest: (id, reason) => api.post(`/leave/requests/${id}/cancel`, { reason }),
  getHolidays: (year) => api.get(`/leave/holidays?year=${year}`),
  createHoliday: (data) => api.post('/leave/holidays', data),
};
