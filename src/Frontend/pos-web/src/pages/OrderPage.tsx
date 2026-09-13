import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { ApiError } from '../api/client';
import { getProducts, type ProductSummary } from '../api/catalog';
import { getTerminals } from '../api/organization';
import { addOrderLines, billOrder, getOrder, sendToKitchen, type OrderInfo } from '../api/restaurant';
import { useAuth } from '../context/AuthContext';

const currency = new Intl.NumberFormat('en-LK', { style: 'currency', currency: 'LKR' });

export function OrderPage() {
  const { orderId } = useParams<{ orderId: string }>();
  const { session } = useAuth();
  const branchId = session?.user.branchIds[0] ?? null;
  const navigate = useNavigate();

  const [order, setOrder] = useState<OrderInfo | null>(null);
  const [products, setProducts] = useState<ProductSummary[]>([]);
  const [search, setSearch] = useState('');
  const [terminalId, setTerminalId] = useState<number | null>(null);
  const [cashTendered, setCashTendered] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  useEffect(() => {
    if (!branchId || !orderId) return;
    refreshOrder();
    getProducts().then(setProducts);
    getTerminals(branchId).then((terminals) => terminals.length > 0 && setTerminalId(terminals[0].id));
  }, [branchId, orderId]);

  function refreshOrder() {
    if (branchId && orderId) getOrder(branchId, Number(orderId)).then(setOrder).catch(() => setError('Could not load order.'));
  }

  const filteredProducts = search
    ? products.filter((p) => p.name.toLowerCase().includes(search.toLowerCase()) || p.sku.toLowerCase().includes(search.toLowerCase()))
    : [];

  async function handleAddItem(productId: number) {
    if (!branchId || !order) return;
    setError(null);
    try {
      await addOrderLines(branchId, order.id, [{ productId, quantity: 1 }]);
      setSearch('');
      refreshOrder();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Could not add item.');
    }
  }

  async function handleSendToKitchen() {
    if (!branchId || !order) return;
    setError(null);
    try {
      const tickets = await sendToKitchen(branchId, order.id);
      setMessage(tickets.length > 0 ? `Sent ${tickets.length} ticket(s) to the kitchen/bar.` : 'Nothing new to send.');
      refreshOrder();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Could not send to kitchen.');
    }
  }

  async function handleBill() {
    if (!branchId || !order || !terminalId) return;
    setError(null);
    try {
      await billOrder(branchId, order.id, terminalId, [{ method: 'Cash', amount: Number(cashTendered || 0) }]);
      navigate('/restaurant');
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Could not bill this order.');
    }
  }

  if (!order) {
    return <div className="dashboard"><p>Loading order…</p></div>;
  }

  return (
    <div className="pos-screen">
      <div className="pos-main">
        <header className="dashboard-header">
          <h1>Order #{order.id}</h1>
          <button onClick={() => navigate('/restaurant')}>Back to floor plan</button>
        </header>

        <input
          type="text"
          placeholder="Search menu item…"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className="search-input"
        />

        {filteredProducts.length > 0 && (
          <ul className="card-list">
            {filteredProducts.map((p) => (
              <li key={p.id} className="card search-result" onClick={() => handleAddItem(p.id)}>
                <strong>{p.name}</strong>
                <span>{currency.format(p.sellingPrice)}</span>
              </li>
            ))}
          </ul>
        )}

        {error && <div className="error-banner">{error}</div>}
        {message && <div className="totals-box">{message}</div>}

        <table className="product-table">
          <thead>
            <tr><th>Item</th><th>Qty</th><th>KOT Status</th></tr>
          </thead>
          <tbody>
            {order.lines.map((line) => (
              <tr key={line.id}>
                <td>{line.productName}</td>
                <td>{line.quantity}</td>
                <td>{line.kotStatus}</td>
              </tr>
            ))}
            {order.lines.length === 0 && <tr><td colSpan={3} className="empty-cart">No items yet — search above to add.</td></tr>}
          </tbody>
        </table>
      </div>

      <aside className="pos-sidebar">
        <h2>Actions</h2>
        <button onClick={handleSendToKitchen} disabled={order.lines.length === 0} className="hold-button">
          Send to Kitchen
        </button>

        <h2>Bill</h2>
        <label>
          Cash Tendered
          <input type="number" value={cashTendered} onChange={(e) => setCashTendered(e.target.value)} className="search-input" />
        </label>
        <button onClick={handleBill} disabled={order.lines.length === 0} className="checkout-button">
          Bill & Close Table
        </button>
      </aside>
    </div>
  );
}
