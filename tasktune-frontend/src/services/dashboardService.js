import api from './api';

export const dashboardService = {
  getEmployeeDashboard: () => api.get('/dashboards/employee'),
  getSupervisorDashboard: () => api.get('/dashboards/supervisor'),
  getExecutiveDashboard: () => api.get('/dashboards/executive'),
  getHRDashboard: () => api.get('/dashboards/hr'),
  getAdminDashboard: () => api.get('/dashboards/admin'),
};
