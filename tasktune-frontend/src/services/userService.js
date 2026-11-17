import api from './api';

export const userService = {
  getUsers: (page = 1, pageSize = 25) => api.get(`/users?page=${page}&pageSize=${pageSize}`),
  getUser: (id) => api.get(`/users/${id}`),
  createUser: (data) => api.post('/users', data),
  createInvitation: (data) => api.post('/users/invitations', data),
  updateUser: (id, data) => api.put(`/users/${id}`, data),
  deactivateUser: (id) => api.delete(`/users/${id}`),
};
