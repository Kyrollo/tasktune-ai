import api from './api';

export const payrollService = {
  getPeriods: () => api.get('/payroll/periods'),
  createPeriod: (payload) => api.post('/payroll/periods', payload),
  getPeriodPayslips: (periodId) => api.get(`/payroll/periods/${periodId}/payslips`),
  createPayslip: (periodId, payload) => api.post(`/payroll/periods/${periodId}/payslips`, payload),
  approvePeriod: (periodId) => api.post(`/payroll/periods/${periodId}/approve`),
  markPaid: (payslipId) => api.post(`/payroll/payslips/${payslipId}/mark-paid`),
  getMyPayslips: () => api.get('/payroll/me/payslips'),
};
