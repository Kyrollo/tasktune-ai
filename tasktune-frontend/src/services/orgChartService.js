import api from './api';

export const orgChartService = {
  getOrgChart: () => api.get('/org-chart'),
};
