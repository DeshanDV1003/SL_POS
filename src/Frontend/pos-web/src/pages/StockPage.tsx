import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { ApiError } from '../api/client';
import { getStockOnHand, type StockOnHand } from '../api/inventory';
import { useAuth } from '../context/AuthContext';

export function StockPage() {
  const { session } = useAuth();
  const [branchId, setBranchId] = useState<number | null>(session?.user.branchIds[0] ?? null);
  const [stock, setStock] = useState<StockOnHand[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!branchId) return;
    getStockOnHand(branchId)
      .then(setStock)
      .catch((err) => setError(err instanceof ApiError ? err.message : 'Could not load stock.'));
  }, [branchId]);

  return (
    <div className="dashboard">
      <header className="dashboard-header">
        <div>
          <h1>Stock On Hand</h1>
          <p>{stock?.length ?? 0} products with recorded stock</p>
        </div>
        <Link to="/">Back to dashboard</Link>
      </header>

      {session && session.user.branchIds.length > 1 && (
        <select value={branchId ?? ''} onChange={(e) => setBranchId(Number(e.target.value))} className="search-input">
          {session.user.branchIds.map((id) => (
            <option key={id} value={id}>Branch #{id}</option>
          ))}
        </select>
      )}

      {error && <div className="error-banner">{error}</div>}

      {stock === null ? (
        <p>Loading…</p>
      ) : stock.length === 0 ? (
        <p>No stock movements recorded for this branch yet.</p>
      ) : (
        <table className="product-table">
          <thead>
            <tr>
              <th>SKU</th>
              <th>Product</th>
              <th>Quantity On Hand</th>
              <th>Reorder Level</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            {stock.map((s) => (
              <tr key={s.productId}>
                <td>{s.sku}</td>
                <td>{s.productName}</td>
                <td>{s.quantityOnHand}</td>
                <td>{s.reorderLevel}</td>
                <td>{s.isBelowReorderLevel ? <span className="badge-warning">Reorder</span> : 'OK'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
