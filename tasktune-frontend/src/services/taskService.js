import api from './api';

export const taskService = {
  getTasks: (page = 1, pageSize = 25) => api.get(`/tasks?page=${page}&pageSize=${pageSize}`),
  getTask: (id) => api.get(`/tasks/${id}`),
  createTask: (data) => api.post('/tasks', data),
  updateTask: (id, data) => api.put(`/tasks/${id}`, data),
  deleteTask: (id) => api.delete(`/tasks/${id}`),
  submitProgress: (taskId, data) => api.post(`/tasks/${taskId}/progress`, data),
  getProgress: (taskId) => api.get(`/tasks/${taskId}/progress`),
  getSubtasks: (taskId) => api.get(`/tasks/${taskId}/subtasks`),
  createSubtask: (taskId, data) => api.post(`/tasks/${taskId}/subtasks`, data),
  updateSubtask: (taskId, id, data) => api.put(`/tasks/${taskId}/subtasks/${id}`, data),
  toggleSubtask: (taskId, id) => api.patch(`/tasks/${taskId}/subtasks/${id}/toggle`),
  deleteSubtask: (taskId, id) => api.delete(`/tasks/${taskId}/subtasks/${id}`),

  getGuidance: (taskId) => api.get(`/tasks/${taskId}/guidance`),
  generateGuidance: (taskId) => api.post(`/tasks/${taskId}/guidance/generate`),
  applyGuidanceSubtasks: (taskId, subtasks) => api.post(`/tasks/${taskId}/guidance/apply-subtasks`, { subtasks }),

  getTaskRecommendation: (data) => api.post('/tasks/ai-recommendation', {
    taskName: data.taskName || data.task_name || '',
    description: data.description || '',
    definitionOfDone: data.definitionOfDone || data.definition_of_done || '',
    dueDate: data.dueDate || data.due_date || 'N/A',
    projectName: data.projectName || '',
    subtasks: data.subtasks || [],
    dependencies: data.dependencies || [],
    completionPercentage: data.completionPercentage || 0,
  }),

  analyzeProgress: (taskId, data = {}) => api.post(`/tasks/${taskId}/progress/ai-analyze`, data),
  getLatestProgressAnalysis: (taskId) => api.get(`/tasks/${taskId}/progress/ai-analysis`),
};

