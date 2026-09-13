import { apiFetch } from './client';
import type { LoginRequest, LoginResult } from './types';

export function login(request: LoginRequest): Promise<LoginResult> {
  return apiFetch<LoginResult>('/api/v1/auth/login', {
    method: 'POST',
    body: JSON.stringify(request),
  });
}

export function logout(refreshToken: string): Promise<void> {
  return apiFetch<void>('/api/v1/auth/logout', {
    method: 'POST',
    body: JSON.stringify({ refreshToken }),
  });
}
