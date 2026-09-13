import { apiFetch } from './client';

export interface SalesSummary {
  grossSales: number;
  discountTotal: number;
  taxTotal: number;
  serviceChargeTotal: number;
  netSales: number;
  transactionCount: number;
  voidCount: number;
  averageSaleValue: number;
}

export interface SalesByProduct {
  productId: number;
  productName: string;
  quantitySold: number;
  revenue: number;
}

export interface SalesByCategory {
  categoryId: number | null;
  categoryName: string;
  revenue: number;
}

export interface SalesByPaymentMethod {
  method: string;
  total: number;
}

export interface DashboardSummary {
  todayNetSales: number;
  todayTransactionCount: number;
  last7DaysNetSales: number;
  lowStockProductCount: number;
  topProducts: SalesByProduct[];
}

export function getDashboard(branchId: number): Promise<DashboardSummary> {
  return apiFetch<DashboardSummary>(`/api/v1/branches/${branchId}/dashboard`);
}

export function getSalesSummary(branchId: number): Promise<SalesSummary> {
  return apiFetch<SalesSummary>(`/api/v1/branches/${branchId}/reports/sales-summary`);
}

export function getSalesByProduct(branchId: number): Promise<SalesByProduct[]> {
  return apiFetch<SalesByProduct[]>(`/api/v1/branches/${branchId}/reports/sales-by-product`);
}

export function getSalesByCategory(branchId: number): Promise<SalesByCategory[]> {
  return apiFetch<SalesByCategory[]>(`/api/v1/branches/${branchId}/reports/sales-by-category`);
}

export function getSalesByPaymentMethod(branchId: number): Promise<SalesByPaymentMethod[]> {
  return apiFetch<SalesByPaymentMethod[]>(`/api/v1/branches/${branchId}/reports/sales-by-payment-method`);
}
