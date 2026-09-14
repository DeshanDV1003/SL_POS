import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { ApiError } from '../api/client';
import { findProductByBarcode, getProducts, type ProductSummary } from '../api/catalog';
import { getTerminals, recordTerminalHeartbeat } from '../api/organization';
import { checkout, deleteHeldBill, getHeldBills, getReceiptText, holdBill, recallBill, type HeldBillDto, type SaleReceipt } from '../api/sales';
import { OfflineStatusBadge } from '../components/OfflineStatusBadge';
import { useAuth } from '../context/AuthContext';
import { createCustomerDisplayChannel } from '../customerDisplay/channel';
import { enqueueOfflineSale } from '../offline/offlineSalesQueue';
import { offlineSyncManager } from '../offline/syncManager';

const HEARTBEAT_INTERVAL_MS = 60_000;

/** True for a transport-level failure (offline, DNS, connection reset) — apiFetch only throws ApiError for an actual HTTP response, so anything else reaching here means the request never got one. */
function isNetworkFailure(err: unknown): boolean {
  return !(err instanceof ApiError);
}

const currency = new Intl.NumberFormat('en-LK', { style: 'currency', currency: 'LKR' });

interface CartLine {
  product: ProductSummary;
  quantity: number;
  discountPercentage: number;
}

export function CheckoutPage() {
  const { session } = useAuth();
  const branchId = session?.user.branchIds[0] ?? null;

  const [terminalId, setTerminalId] = useState<number | null>(null);
  const [scanValue, setScanValue] = useState('');
  const [searchResults, setSearchResults] = useState<ProductSummary[]>([]);
  const [cart, setCart] = useState<CartLine[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [cashTendered, setCashTendered] = useState('');
  const [receipt, setReceipt] = useState<SaleReceipt | null>(null);
  const [printedReceipt, setPrintedReceipt] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [heldBills, setHeldBills] = useState<HeldBillDto[]>([]);
  const [offlineNotice, setOfflineNotice] = useState<string | null>(null);
  const [customerDisplayShowingReceipt, setCustomerDisplayShowingReceipt] = useState(false);
  const scanInputRef = useRef<HTMLInputElement>(null);
  const customerDisplayChannelRef = useRef<BroadcastChannel | null>(null);

  useEffect(() => {
    const channel = createCustomerDisplayChannel();
    customerDisplayChannelRef.current = channel;
    return () => channel.close();
  }, []);

  useEffect(() => {
    if (!branchId) return;
    getTerminals(branchId).then((terminals) => {
      if (terminals.length > 0) setTerminalId(terminals[0].id);
    });
    refreshHeldBills();
  }, [branchId]);

  // Proves this terminal is online so a manager reviewing terminal status isn't
  // guessing from the last login — see docs/architecture.md §10.
  useEffect(() => {
    if (!branchId || !terminalId) return;
    const beat = () => {
      if (navigator.onLine) recordTerminalHeartbeat(branchId, terminalId).catch(() => {});
    };
    beat();
    const handle = setInterval(beat, HEARTBEAT_INTERVAL_MS);
    return () => clearInterval(handle);
  }, [branchId, terminalId]);

  function refreshHeldBills() {
    if (branchId) getHeldBills(branchId).then(setHeldBills).catch(() => {});
  }

  const subTotal = cart.reduce((sum, l) => sum + l.product.sellingPrice * l.quantity, 0);
  const discountTotal = cart.reduce((sum, l) => sum + l.product.sellingPrice * l.quantity * (l.discountPercentage / 100), 0);
  // Server computes the authoritative tax/total; this is an estimate shown before checkout.
  const estimatedTotal = Math.round((subTotal - discountTotal) * 100) / 100;

  // Mirrors the live cart to a customer-facing display window, if one is open —
  // see src/customerDisplay/ and docs/architecture.md §11 (ICustomerDisplay).
  // Suppressed right after a checkout completes: setCart([]) fires this same
  // effect with an empty cart, which would otherwise immediately overwrite the
  // "Thank you" / change-due message with a blank "shopping" screen before the
  // cashier ever gets to New Sale.
  useEffect(() => {
    if (customerDisplayShowingReceipt) return;
    customerDisplayChannelRef.current?.postMessage({
      status: 'shopping',
      lines: cart.map((l) => ({ productName: l.product.name, quantity: l.quantity, lineTotal: l.product.sellingPrice * l.quantity * (1 - l.discountPercentage / 100) })),
      subTotal,
      discountTotal,
      estimatedTotal,
    });
  }, [cart, subTotal, discountTotal, estimatedTotal, customerDisplayShowingReceipt]);

  function openCustomerDisplay() {
    window.open('/customer-display', 'universalpos-customer-display', 'width=480,height=760');
  }

  function addProductToCart(product: ProductSummary) {
    setCart((prev) => {
      const existing = prev.find((l) => l.product.id === product.id);
      if (existing) {
        return prev.map((l) => (l.product.id === product.id ? { ...l, quantity: l.quantity + 1 } : l));
      }
      return [...prev, { product, quantity: 1, discountPercentage: 0 }];
    });
    setScanValue('');
    setSearchResults([]);
    scanInputRef.current?.focus();
  }

  async function handleScanSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!scanValue.trim()) return;
    setError(null);
    try {
      const product = await findProductByBarcode(scanValue.trim());
      addProductToCart(product);
    } catch (err) {
      if (err instanceof ApiError && err.status === 404) {
        const results = await getProducts(scanValue.trim());
        setSearchResults(results);
        if (results.length === 0) setError(`No product found for "${scanValue}".`);
      } else {
        setError('Lookup failed. Please try again.');
      }
    }
  }

  function updateQuantity(productId: number, quantity: number) {
    if (quantity <= 0) {
      setCart((prev) => prev.filter((l) => l.product.id !== productId));
      return;
    }
    setCart((prev) => prev.map((l) => (l.product.id === productId ? { ...l, quantity } : l)));
  }

  function updateDiscount(productId: number, discountPercentage: number) {
    setCart((prev) => prev.map((l) => (l.product.id === productId ? { ...l, discountPercentage } : l)));
  }

  async function handleCheckout() {
    if (!branchId || !terminalId || cart.length === 0) return;
    setError(null);
    setOfflineNotice(null);
    setIsSubmitting(true);

    const tendered = Number(cashTendered || estimatedTotal);
    const saleRequest = {
      terminalId,
      lines: cart.map((l) => ({ productId: l.product.id, quantity: l.quantity, discountPercentage: l.discountPercentage })),
      payments: [{ method: 'Cash' as const, amount: tendered }],
    };

    try {
      // Card/digital payments require the gateway to be reachable and so are
      // always online-required (docs/architecture.md §10); this screen only ever
      // charges Cash, so every checkout here is eligible to queue offline if the
      // network is down — never attempt a live request when we already know
      // we're offline.
      if (!navigator.onLine) {
        await queueOffline(branchId, saleRequest);
        return;
      }

      const result = await checkout(branchId, saleRequest);
      setReceipt(result);
      setCart([]);
      setCashTendered('');
      setCustomerDisplayShowingReceipt(true);
      customerDisplayChannelRef.current?.postMessage({
        status: 'completed',
        lines: [],
        subTotal: 0,
        discountTotal: 0,
        estimatedTotal: 0,
        completedGrandTotal: result.grandTotal,
        completedChangeDue: result.changeDue,
      });
    } catch (err) {
      if (isNetworkFailure(err)) {
        await queueOffline(branchId, saleRequest);
        return;
      }
      setError(err instanceof ApiError ? err.message : 'Checkout failed.');
    } finally {
      setIsSubmitting(false);
    }
  }

  async function queueOffline(branchId: number, saleRequest: { terminalId: number; lines: { productId: number; quantity: number; discountPercentage: number }[]; payments: { method: 'Cash'; amount: number }[] }) {
    try {
      await enqueueOfflineSale(branchId, saleRequest);
      await offlineSyncManager.notifyEnqueued();
      setCart([]);
      setCashTendered('');
      setOfflineNotice('Sale saved on this device — it will sync to the server automatically once the connection is back.');
    } catch {
      setError('Could not save this sale offline either. Please try again.');
    }
  }

  async function handleViewReceipt() {
    if (!branchId || !receipt) return;
    try {
      const text = await getReceiptText(branchId, receipt.id);
      setPrintedReceipt(text);
    } catch {
      setError('Could not load the receipt.');
    }
  }

  async function handleHold() {
    if (!branchId || !terminalId || cart.length === 0) return;
    try {
      await holdBill(branchId, terminalId, undefined, cart.map((l) => ({ productId: l.product.id, quantity: l.quantity, discountPercentage: l.discountPercentage })));
      setCart([]);
      refreshHeldBills();
    } catch {
      setError('Could not hold this bill.');
    }
  }

  async function handleRecall(heldBillId: number) {
    if (!branchId) return;
    try {
      const bill = await recallBill(branchId, heldBillId);
      const products = await getProducts();
      const lines: CartLine[] = bill.lines
        .map((l) => {
          const product = products.find((p) => p.id === l.productId);
          return product ? { product, quantity: l.quantity, discountPercentage: l.discountPercentage } : null;
        })
        .filter((l): l is CartLine => l !== null);
      setCart(lines);
      await deleteHeldBill(branchId, heldBillId);
      refreshHeldBills();
    } catch {
      setError('Could not recall this bill.');
    }
  }

  if (offlineNotice) {
    return (
      <div className="dashboard">
        <header className="dashboard-header">
          <h1>Sale Saved Offline</h1>
          <Link to="/">Back to dashboard</Link>
        </header>
        <div className="receipt-card">
          <p>{offlineNotice}</p>
          <p style={{ color: 'var(--color-muted)', fontSize: '0.9rem' }}>
            No invoice number is assigned yet — the server assigns it only once this sale syncs, so the invoice sequence never has a gap.
          </p>
          <OfflineStatusBadge />
          <button onClick={() => setOfflineNotice(null)} className="checkout-button">New Sale</button>
        </div>
      </div>
    );
  }

  if (receipt) {
    return (
      <div className="dashboard">
        <header className="dashboard-header">
          <h1>Sale Completed</h1>
          <Link to="/">Back to dashboard</Link>
        </header>
        <div className="receipt-card">
          <p className="receipt-invoice">{receipt.invoiceNumber}</p>
          {receipt.lines.map((l) => (
            <div key={l.productId} className="receipt-line">
              <span>{l.productName} x{l.quantity}</span>
              <span>{currency.format(l.lineTotal)}</span>
            </div>
          ))}
          <hr />
          <div className="receipt-line"><span>Subtotal</span><span>{currency.format(receipt.subTotal)}</span></div>
          <div className="receipt-line"><span>Discount</span><span>-{currency.format(receipt.discountTotal)}</span></div>
          <div className="receipt-line"><span>Tax</span><span>{currency.format(receipt.taxTotal)}</span></div>
          <div className="receipt-line receipt-total"><span>Total</span><span>{currency.format(receipt.grandTotal)}</span></div>
          <div className="receipt-line"><span>Change Due</span><span>{currency.format(receipt.changeDue)}</span></div>
          {printedReceipt ? (
            <pre className="printed-receipt">{printedReceipt}</pre>
          ) : (
            <button onClick={handleViewReceipt} className="hold-button">View / Print Receipt</button>
          )}
          {printedReceipt && (
            <button onClick={() => window.print()} className="hold-button">Print</button>
          )}
          <button onClick={() => { setReceipt(null); setPrintedReceipt(null); setCustomerDisplayShowingReceipt(false); }} className="checkout-button">New Sale</button>
        </div>
      </div>
    );
  }

  return (
    <div className="pos-screen">
      <div className="pos-main">
        <header className="dashboard-header">
          <h1>Checkout</h1>
          <div style={{ display: 'flex', gap: '0.75rem', alignItems: 'center' }}>
            <button onClick={openCustomerDisplay} className="hold-button">Open Customer Display</button>
            <Link to="/">Back to dashboard</Link>
          </div>
        </header>

        <OfflineStatusBadge />

        <form onSubmit={handleScanSubmit} className="scan-form">
          <input
            ref={scanInputRef}
            type="text"
            placeholder="Scan barcode or type product name/SKU, then Enter"
            value={scanValue}
            onChange={(e) => setScanValue(e.target.value)}
            autoFocus
            className="search-input"
          />
        </form>

        {searchResults.length > 0 && (
          <ul className="card-list">
            {searchResults.map((p) => (
              <li key={p.id} className="card search-result" onClick={() => addProductToCart(p)}>
                <strong>{p.name}</strong>
                <span>{p.sku} · {currency.format(p.sellingPrice)}</span>
              </li>
            ))}
          </ul>
        )}

        {error && <div className="error-banner">{error}</div>}

        <table className="product-table">
          <thead>
            <tr>
              <th>Product</th>
              <th>Qty</th>
              <th>Price</th>
              <th>Disc %</th>
              <th>Line Total</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {cart.map((l) => (
              <tr key={l.product.id}>
                <td>{l.product.name}</td>
                <td>
                  <input
                    type="number"
                    min={0}
                    value={l.quantity}
                    onChange={(e) => updateQuantity(l.product.id, Number(e.target.value))}
                    className="qty-input"
                  />
                </td>
                <td>{currency.format(l.product.sellingPrice)}</td>
                <td>
                  <input
                    type="number"
                    min={0}
                    max={100}
                    value={l.discountPercentage}
                    onChange={(e) => updateDiscount(l.product.id, Number(e.target.value))}
                    className="qty-input"
                  />
                </td>
                <td>{currency.format(l.product.sellingPrice * l.quantity * (1 - l.discountPercentage / 100))}</td>
                <td><button onClick={() => updateQuantity(l.product.id, 0)} className="link-button">Remove</button></td>
              </tr>
            ))}
            {cart.length === 0 && (
              <tr><td colSpan={6} className="empty-cart">Cart is empty — scan or search a product above.</td></tr>
            )}
          </tbody>
        </table>

        {heldBills.length > 0 && (
          <section>
            <h2>Held Bills</h2>
            <ul className="card-list">
              {heldBills.map((h) => (
                <li key={h.id} className="card search-result" onClick={() => handleRecall(h.id)}>
                  <strong>Hold #{h.id}</strong>
                  <span>{h.notes ?? `${h.lines.length} item(s)`}</span>
                </li>
              ))}
            </ul>
          </section>
        )}
      </div>

      <aside className="pos-sidebar">
        <h2>Total</h2>
        <div className="totals-box">
          <div className="receipt-line"><span>Estimated Subtotal</span><span>{currency.format(subTotal - discountTotal)}</span></div>
          <p className="hint">Tax is computed by the server at checkout.</p>
          <div className="receipt-line receipt-total"><span>Estimated Total</span><span>{currency.format(estimatedTotal)}</span></div>
        </div>

        <label>
          Cash Tendered
          <input
            type="number"
            value={cashTendered}
            onChange={(e) => setCashTendered(e.target.value)}
            placeholder={estimatedTotal.toFixed(2)}
            className="search-input"
          />
        </label>

        <button onClick={handleCheckout} disabled={cart.length === 0 || isSubmitting} className="checkout-button">
          {isSubmitting ? 'Processing…' : 'Charge & Complete Sale'}
        </button>
        <button onClick={handleHold} disabled={cart.length === 0} className="hold-button">
          Hold Bill
        </button>
      </aside>
    </div>
  );
}
