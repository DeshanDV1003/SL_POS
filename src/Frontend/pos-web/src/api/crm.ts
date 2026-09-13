import { apiFetch } from './client';

export interface CustomerDto {
  id: number;
  customerGroupId: number | null;
  membershipTierId: number | null;
  name: string;
  phone: string | null;
  email: string | null;
  creditLimit: number;
  outstandingBalance: number;
  loyaltyPointsBalance: number;
  isActive: boolean;
}

export interface MembershipTierDto {
  id: number;
  name: string;
  minimumPoints: number;
  pointsMultiplier: number;
}

export function getCustomers(search?: string): Promise<CustomerDto[]> {
  const query = search ? `?search=${encodeURIComponent(search)}` : '';
  return apiFetch<CustomerDto[]>(`/api/v1/customers${query}`);
}

export function createCustomer(name: string, phone?: string): Promise<CustomerDto> {
  return apiFetch<CustomerDto>('/api/v1/customers', {
    method: 'POST',
    body: JSON.stringify({ name, phone }),
  });
}

export function getMembershipTiers(): Promise<MembershipTierDto[]> {
  return apiFetch<MembershipTierDto[]>('/api/v1/membership-tiers');
}
