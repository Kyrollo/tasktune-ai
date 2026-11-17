import api from './api';

export const attendanceService = {
  getToday: () => api.get('/attendance/me/today'),
  clockIn: () => api.post('/attendance/clock-in'),
  clockOut: () => api.post('/attendance/clock-out'),
  generateSchedule: (from, to) => api.post('/attendance/schedule/generate', { from, to }),
  reconcileMissed: (from, to) => api.post('/attendance/schedule/reconcile-missed', { from, to }),
  getMyTimesheet: (from, to) => api.get('/attendance/me/timesheet', { params: { from, to } }),
  getTeamAttendance: (from, to, employeeUserId) =>
    api.get('/attendance/team', { params: { from, to, employeeUserId } }),
  getShifts: () => api.get('/attendance/shifts'),
  createShift: (data) => api.post('/attendance/shifts', data),
  updateShift: (id, data) => api.put(`/attendance/shifts/${id}`, data),
  getMyAssignments: () => api.get('/attendance/assignments/me'),
  getEmployeeAssignments: (userId) => api.get(`/attendance/assignments/${userId}`),
  assignShift: (data) => api.post('/attendance/assignments', data),
  closeAssignment: (id, effectiveTo) =>
    api.post(`/attendance/assignments/${id}/close`, { effectiveTo }),
  adjustAttendance: (id, data) => api.post(`/attendance/records/${id}/adjust`, data),
};
