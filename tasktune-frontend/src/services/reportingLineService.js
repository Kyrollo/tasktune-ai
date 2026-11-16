import api from './api';

export const reportingLineService = {
  getForEmployee: (employeeUserId) => api.get(`/reporting-lines/employee/${employeeUserId}`),
  getMyDirectReports: () => api.get('/reporting-lines/me/direct-reports'),
  create: (data) => api.post('/reporting-lines', data),
  close: (reportingLineId, effectiveTo) =>
    api.post(`/reporting-lines/${reportingLineId}/close`, { effectiveTo }),
};
