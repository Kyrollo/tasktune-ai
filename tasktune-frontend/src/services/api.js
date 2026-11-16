import axios from 'axios';
import { getAccessToken, getAuthStorage, clearAllAuthStorage } from '../utils/authStorage';

// Same-origin is the production default. Local development can set VITE_API_BASE_URL.
const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL || '/api').replace(/\/$/, '');

const api = axios.create({
  baseURL: API_BASE_URL,
  headers: { 'Content-Type': 'application/json' },
  timeout: 30000,
});

api.interceptors.request.use((config) => {
  const token = getAccessToken();
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

let refreshPromise = null;

api.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config;
    if (!originalRequest || error.response?.status !== 401 ||
        originalRequest._retry || originalRequest.url?.includes('/auth/')) {
      return Promise.reject(error);
    }

    originalRequest._retry = true;
    try {
      if (!refreshPromise) {
        refreshPromise = (async () => {
          const store = getAuthStorage();
          const refreshToken = store.getItem('refreshToken');
          if (!refreshToken) throw new Error('No refresh token');

          const { data } = await axios.post(
            `${API_BASE_URL}/auth/refresh-token`,
            { refreshToken },
            { timeout: 15000 },
          );
          if (!data?.accessToken || !data?.refreshToken) {
            throw new Error('The refresh endpoint returned an invalid token response.');
          }
          store.setItem('accessToken', data.accessToken);
          store.setItem('refreshToken', data.refreshToken);
          return data.accessToken;
        })().finally(() => {
          refreshPromise = null;
        });
      }

      const accessToken = await refreshPromise;
      originalRequest.headers = originalRequest.headers || {};
      originalRequest.headers.Authorization = `Bearer ${accessToken}`;
      return api(originalRequest);
    } catch (refreshError) {
      clearAllAuthStorage();
      if (window.location.pathname !== '/login') window.location.assign('/login');
      return Promise.reject(refreshError);
    }
  },
);

export default api;
