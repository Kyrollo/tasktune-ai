import api from './api';

export const evaluationService = {
  getEvaluation: (id) => api.get(`/evaluations/${id}`),
  getTaskEvaluations: (taskId, page = 1, pageSize = 25) =>
    api.get(`/evaluations/task/${taskId}?page=${page}&pageSize=${pageSize}`),
  getUserEvaluations: (userId) => api.get(`/evaluations/user/${userId}`),
  getMyEvaluations: () => api.get('/evaluations/my'),
  createEvaluation: (data) => api.post('/evaluations', data),
};
