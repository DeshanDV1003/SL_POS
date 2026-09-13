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
