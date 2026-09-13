import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { ApiError } from '../api/client';
import { getCategories, getProducts, type Category, type ProductSummary } from '../api/catalog';

const currencyFormatter = new Intl.NumberFormat('en-LK', { style: 'currency', currency: 'LKR' });

export function ProductsPage() {
  const [products, setProducts] = useState<ProductSummary[] | null>(null);
  const [categories, setCategories] = useState<Category[]>([]);
  const [search, setSearch] = useState('');
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    getCategories().then(setCategories).catch(() => {});
  }, []);

  useEffect(() => {
    const handle = setTimeout(() => {
      getProducts(search || undefined)
        .then(setProducts)
        .catch((err) => setError(err instanceof ApiError ? err.message : 'Could not load products.'));
    }, 250);
    return () => clearTimeout(handle);
  }, [search]);

  const categoryNameById = useMemo(() => {
    const map = new Map<number, string>();
    categories.forEach((c) => map.set(c.id, c.name));
    return map;
  }, [categories]);

  return (
    <div className="dashboard">
      <header className="dashboard-header">
        <div>
          <h1>Product Catalog</h1>
          <p>{products?.length ?? 0} products</p>
        </div>
        <Link to="/">Back to dashboard</Link>
      </header>

      <input
        type="search"
        placeholder="Search by name or SKU…"
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        className="search-input"
      />

      {error && <div className="error-banner">{error}</div>}

      {products === null ? (
        <p>Loading…</p>
      ) : (
        <table className="product-table">
          <thead>
            <tr>
              <th>SKU</th>
              <th>Name</th>
              <th>Category</th>
              <th>Barcode</th>
              <th>Cost</th>
              <th>Selling Price</th>
            </tr>
          </thead>
          <tbody>
            {products.map((p) => (
              <tr key={p.id}>
                <td>{p.sku}</td>
                <td>{p.name}</td>
                <td>{p.categoryId ? categoryNameById.get(p.categoryId) ?? '—' : '—'}</td>
                <td>{p.barcodes[0] ?? '—'}</td>
                <td>{currencyFormatter.format(p.costPrice)}</td>
                <td>{currencyFormatter.format(p.sellingPrice)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
