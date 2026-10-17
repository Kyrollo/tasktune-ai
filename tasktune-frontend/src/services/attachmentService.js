import api from './api';

export const attachmentService = {
  getAttachments: (taskId) => api.get(`/tasks/${taskId}/attachments`),
  uploadAttachment: (taskId, file) => {
    const formData = new FormData();
    formData.append('file', file);
    return api.post(`/tasks/${taskId}/attachments`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
  },
};
