import { apiFetch } from './client';

export interface StockOnHand {
  productId: number;
  productName: string;
  sku: string;
  quantityOnHand: number;
  reorderLevel: number;
  isBelowReorderLevel: boolean;
}

export function getStockOnHand(branchId: number): Promise<StockOnHand[]> {
  return apiFetch<StockOnHand[]>(`/api/v1/branches/${branchId}/inventory/stock-on-hand`);
}
