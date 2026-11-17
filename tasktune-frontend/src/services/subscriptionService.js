import api from './api';

export const subscriptionService = {
  getCurrent: () => api.get('/subscription/current'),
  getUsage: () => api.get('/subscription/usage'),
};
