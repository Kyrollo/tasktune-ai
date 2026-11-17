import api from './api';

export const userService = {
  getUsers: (page = 1, pageSize = 25) => api.get(`/users?page=${page}&pageSize=${pageSize}`),
  getUser: (id) => api.get(`/users/${id}`),
  createUser: (data) => api.post('/users', data),
  updateUser: (id, data) => api.put(`/users/${id}`, data),
  deactivateUser: (id) => api.delete(`/users/${id}`),
  getPermissionOverrides: (id) => api.get(`/user-permissions/users/${id}`),
  savePermissionOverrides: (id, permissions) =>
    api.put(`/user-permissions/users/${id}`, { permissions }),
  deletePermissionOverride: (id, overrideId) =>
    api.delete(`/user-permissions/users/${id}/${overrideId}`),
};
