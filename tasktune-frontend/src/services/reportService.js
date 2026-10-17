import axios from 'axios';
import api from './api';

const reportAgent = axios.create({
  baseURL: 'http://localhost:8004',
  headers: { 'Content-Type': 'application/json' },
  timeout: 10000,
});

export const reportService = {
  getReports: (page = 1, pageSize = 25) => api.get(`/reports?page=${page}&pageSize=${pageSize}`),
  getReport: (id) => api.get(`/reports/${id}`),
  createReport: (data) => api.post('/reports', data),
  generateReport: (data) => api.post('/reports/generate', data),
  generateAIReport: (data) => api.post('/reports/ai-generate', data),
  checkAgentHealth: async () => {
    try {
      const response = await reportAgent.get('/api/health');
      return response.data?.status === 'ok';
    } catch {
      return false;
    }
  },
};
