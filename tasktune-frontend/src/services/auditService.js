import api from './api';

export const auditService = {
  getLogs: (page = 1, pageSize = 50, resourceType) => {
    let url = `/audit-logs?page=${page}&pageSize=${pageSize}`;
    if (resourceType) url += `&resourceType=${resourceType}`;
    return api.get(url);
  },
  exportLogs: (resourceType, action) => {
    const params = new URLSearchParams();
    if (resourceType && resourceType !== 'All') params.append('resourceType', resourceType);
    if (action) params.append('action', action);
    const query = params.toString();
    return api.get(`/audit-logs/export${query ? `?${query}` : ''}`, { responseType: 'blob' });
  },
};
