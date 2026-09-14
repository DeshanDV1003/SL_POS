import { apiFetch } from './client';
import type { Branch, Company, Terminal } from './types';

export function getCompanies(): Promise<Company[]> {
  return apiFetch<Company[]>('/api/v1/companies');
}

export function getBranches(companyId: number): Promise<Branch[]> {
  return apiFetch<Branch[]>(`/api/v1/companies/${companyId}/branches`);
}

export function getTerminals(branchId: number): Promise<Terminal[]> {
  return apiFetch<Terminal[]>(`/api/v1/branches/${branchId}/terminals`);
}

/** Proves this terminal is online right now — see docs/architecture.md §10. Called periodically while connected, not on every request. */
export function recordTerminalHeartbeat(branchId: number, terminalId: number): Promise<void> {
  return apiFetch<void>(`/api/v1/branches/${branchId}/terminals/${terminalId}/heartbeat`, { method: 'POST' });
}
