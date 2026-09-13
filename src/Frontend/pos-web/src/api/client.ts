import type { ApiErrorBody } from './types';

export class ApiError extends Error {
  status: number;
  body: ApiErrorBody | null;

  constructor(status: number, body: ApiErrorBody | null, fallbackMessage: string) {
    super(body?.error ?? fallbackMessage);
    this.status = status;
    this.body = body;
  }
}

type TokenProvider = () => string | null;

let getAccessToken: TokenProvider = () => null;

/** Wired up once by AuthContext so the client can attach the current access token without a circular import. */
export function registerAccessTokenProvider(provider: TokenProvider) {
  getAccessToken = provider;
}

export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const token = getAccessToken();
  const headers = new Headers(init?.headers);
  headers.set('Content-Type', 'application/json');
  if (token) {
    headers.set('Authorization', `Bearer ${token}`);
  }

  const response = await fetch(path, { ...init, headers });

  if (response.status === 204) {
    return undefined as T;
  }

  const text = await response.text();
  const data = text ? JSON.parse(text) : null;

  if (!response.ok) {
    throw new ApiError(response.status, data as ApiErrorBody, `Request to ${path} failed with ${response.status}`);
  }

  return data as T;
}
