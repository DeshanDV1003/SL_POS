export interface LoginRequest {
  username: string;
  password: string;
  branchCode?: string;
  terminalCode?: string;
}

export interface UserProfile {
  id: number;
  username: string;
  fullName: string;
  companyId: number;
  branchIds: number[];
  roles: string[];
  permissions: string[];
}

export interface LoginResult {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAtUtc: string;
  user: UserProfile;
}

export interface Company {
  id: number;
  name: string;
  legalName: string;
  defaultCurrencyCode: string;
  isVatRegistered: boolean;
  isActive: boolean;
}

export interface Branch {
  id: number;
  companyId: number;
  name: string;
  code: string;
  city: string | null;
  businessTypeFlags: string;
  isActive: boolean;
}

export interface Terminal {
  id: number;
  branchId: number;
  name: string;
  code: string;
  isActive: boolean;
  lastSeenAtUtc: string | null;
}

export interface ApiErrorBody {
  error: string;
  errors: Record<string, string[]> | null;
  correlationId: string;
}
