import { apiFetch } from './client';

export interface ProductSummary {
  id: number;
  sku: string;
  name: string;
  sellingPrice: number;
  costPrice: number;
  categoryId: number | null;
  brandId: number | null;
  isActive: boolean;
  barcodes: string[];
}

export interface Category {
  id: number;
  parentCategoryId: number | null;
  name: string;
  defaultTaxRateId: number | null;
  isActive: boolean;
}

export function getProducts(search?: string): Promise<ProductSummary[]> {
  const query = search ? `?search=${encodeURIComponent(search)}` : '';
  return apiFetch<ProductSummary[]>(`/api/v1/products${query}`);
}

export function getCategories(): Promise<Category[]> {
  return apiFetch<Category[]>('/api/v1/categories');
}
