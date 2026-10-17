/** Returns which storage holds the active refresh token (session = not remembered). */
export function getAuthStorage() {
  if (sessionStorage.getItem('refreshToken')) return sessionStorage;
  if (localStorage.getItem('refreshToken')) return localStorage;
  return localStorage;
}

export function getAccessToken() {
  return sessionStorage.getItem('accessToken') || localStorage.getItem('accessToken');
}

export function clearAllAuthStorage() {
  ['accessToken', 'refreshToken', 'user'].forEach((k) => {
    sessionStorage.removeItem(k);
    localStorage.removeItem(k);
  });
}

export function persistAuth({ accessToken, refreshToken, user }, rememberMe) {
  clearAllAuthStorage();
  const store = rememberMe ? localStorage : sessionStorage;
  store.setItem('accessToken', accessToken);
  store.setItem('refreshToken', refreshToken);
  store.setItem('user', JSON.stringify(user));
}
