import api from './api';

export const settingsService = {
  getSettings: () => api.get('/settings'),
  saveSettings: (settings) => api.put('/settings', { settingsJson: JSON.stringify(settings) }),
};
