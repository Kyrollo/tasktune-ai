import api from './api';

export const projectService = {
  getProjects: (page = 1, pageSize = 10) => api.get(`/projects?page=${page}&pageSize=${pageSize}`),
  getProject: (id) => api.get(`/projects/${id}`),
  createProject: (data) => api.post('/projects', data),
  updateProject: (id, data) => api.put(`/projects/${id}`, data),
  deleteProject: (id) => api.delete(`/projects/${id}`),
};
