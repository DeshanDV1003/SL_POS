import { useEffect, useState } from 'react';
import { createCustomerDisplayChannel, type CustomerDisplayState } from '../customerDisplay/channel';

const currency = new Intl.NumberFormat('en-LK', { style: 'currency', currency: 'LKR' });

const idleState: CustomerDisplayState = { status: 'idle', lines: [], subTotal: 0, discountTotal: 0, estimatedTotal: 0 };

/**
 * Meant to be opened as a second browser window on a customer-facing monitor —
 * see the "Open Customer Display" button on CheckoutPage. Never calls the API
 * itself; it only listens to the BroadcastChannel the checkout screen posts to,
 * so it stays in sync with the live cart with no server round-trip.
 */
export function CustomerDisplayPage() {
  const [state, setState] = useState<CustomerDisplayState>(idleState);

  useEffect(() => {
    const channel = createCustomerDisplayChannel();
    channel.onmessage = (event: MessageEvent<CustomerDisplayState>) => setState(event.data);
    return () => channel.close();
  }, []);

  if (state.status === 'idle') {
    return (
      <div className="customer-display customer-display-idle">
        <h1>Welcome</h1>
      </div>
    );
  }

  if (state.status === 'completed') {
    return (
      <div className="customer-display">
        <h1>Thank you!</h1>
        <div className="customer-display-total">{currency.format(state.completedGrandTotal ?? 0)}</div>
        {state.completedChangeDue !== undefined && state.completedChangeDue > 0 && (
          <div className="customer-display-change">Change: {currency.format(state.completedChangeDue)}</div>
        )}
      </div>
    );
  }

  return (
    <div className="customer-display">
      {state.branchName && <h2 className="customer-display-branch">{state.branchName}</h2>}
      <ul className="customer-display-lines">
        {state.lines.map((line, i) => (
          <li key={i}>
            <span>{line.quantity} x {line.productName}</span>
            <span>{currency.format(line.lineTotal)}</span>
          </li>
        ))}
      </ul>
      {state.lines.length === 0 && <p className="customer-display-empty">Ready when you are.</p>}
      <div className="customer-display-total-row">
        <span>Total</span>
        <span>{currency.format(state.estimatedTotal)}</span>
      </div>
    </div>
  );
}
