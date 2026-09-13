import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  getDashboard,
  getSalesByCategory,
  getSalesByPaymentMethod,
  getSalesByProduct,
  getSalesSummary,
  type DashboardSummary,
  type SalesByCategory,
  type SalesByPaymentMethod,
  type SalesByProduct,
  type SalesSummary,
} from '../api/reporting';
import { useAuth } from '../context/AuthContext';

const currency = new Intl.NumberFormat('en-LK', { style: 'currency', currency: 'LKR' });

export function ReportsPage() {
  const { session } = useAuth();
  const branchId = session?.user.branchIds[0] ?? null;
  const [dashboard, setDashboard] = useState<DashboardSummary | null>(null);
  const [summary, setSummary] = useState<SalesSummary | null>(null);
  const [byProduct, setByProduct] = useState<SalesByProduct[]>([]);
  const [byCategory, setByCategory] = useState<SalesByCategory[]>([]);
  const [byPayment, setByPayment] = useState<SalesByPaymentMethod[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!branchId) return;
    Promise.all([
      getDashboard(branchId),
      getSalesSummary(branchId),
      getSalesByProduct(branchId),
      getSalesByCategory(branchId),
      getSalesByPaymentMethod(branchId),
    ])
      .then(([d, s, p, c, m]) => {
        setDashboard(d);
        setSummary(s);
        setByProduct(p);
        setByCategory(c);
        setByPayment(m);
      })
      .catch(() => setError('Could not load reports. You may not have permission to view them.'));
  }, [branchId]);

  return (
    <div className="dashboard">
      <header className="dashboard-header">
        <div>
          <h1>Reports &amp; Dashboard</h1>
          <p>Last 7 days</p>
        </div>
        <Link to="/">Back to dashboard</Link>
      </header>

      {error && <div className="error-banner">{error}</div>}

      {dashboard && (
        <section>
          <h2>Overview</h2>
          <ul className="card-list">
            <li className="card"><strong>{currency.format(dashboard.todayNetSales)}</strong><span>Today's Net Sales</span></li>
            <li className="card"><strong>{dashboard.todayTransactionCount}</strong><span>Today's Transactions</span></li>
            <li className="card"><strong>{currency.format(dashboard.last7DaysNetSales)}</strong><span>Last 7 Days Net Sales</span></li>
            <li className="card"><strong>{dashboard.lowStockProductCount}</strong><span>Products Below Reorder Level</span></li>
          </ul>
        </section>
      )}

      {summary && (
        <section>
          <h2>Sales Summary (7 days)</h2>
          <div className="totals-box">
            <div className="receipt-line"><span>Gross Sales</span><span>{currency.format(summary.grossSales)}</span></div>
            <div className="receipt-line"><span>Discounts</span><span>-{currency.format(summary.discountTotal)}</span></div>
            <div className="receipt-line"><span>Tax</span><span>{currency.format(summary.taxTotal)}</span></div>
            <div className="receipt-line receipt-total"><span>Net Sales</span><span>{currency.format(summary.netSales)}</span></div>
            <div className="receipt-line"><span>Transactions / Voids</span><span>{summary.transactionCount} / {summary.voidCount}</span></div>
            <div className="receipt-line"><span>Average Sale</span><span>{currency.format(summary.averageSaleValue)}</span></div>
          </div>
        </section>
      )}

      <section>
        <h2>Sales by Product</h2>
        <table className="product-table">
          <thead><tr><th>Product</th><th>Qty Sold</th><th>Revenue</th></tr></thead>
          <tbody>
            {byProduct.map((p) => (
              <tr key={p.productId}><td>{p.productName}</td><td>{p.quantitySold}</td><td>{currency.format(p.revenue)}</td></tr>
            ))}
            {byProduct.length === 0 && <tr><td colSpan={3} className="empty-cart">No sales in this period.</td></tr>}
          </tbody>
        </table>
      </section>

      <section>
        <h2>Sales by Category</h2>
        <table className="product-table">
          <thead><tr><th>Category</th><th>Revenue</th></tr></thead>
          <tbody>
            {byCategory.map((c) => (
              <tr key={c.categoryId ?? 'none'}><td>{c.categoryName}</td><td>{currency.format(c.revenue)}</td></tr>
            ))}
          </tbody>
        </table>
      </section>

      <section>
        <h2>Sales by Payment Method</h2>
        <table className="product-table">
          <thead><tr><th>Method</th><th>Total</th></tr></thead>
          <tbody>
            {byPayment.map((m) => (
              <tr key={m.method}><td>{m.method}</td><td>{currency.format(m.total)}</td></tr>
            ))}
          </tbody>
        </table>
      </section>
    </div>
  );
}
