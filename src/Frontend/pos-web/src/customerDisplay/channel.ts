/**
 * Customer-facing display, built as a real, fully-testable software piece for
 * Phase 12 (see docs/architecture.md §11's ICustomerDisplay) — no special hardware
 * needed: a second monitor just runs a second browser window of the same origin,
 * and BroadcastChannel (a standard browser API) is how same-origin tabs/windows
 * talk to each other without a server round-trip.
 */

export const CUSTOMER_DISPLAY_CHANNEL = 'universalpos-customer-display';

export interface CustomerDisplayLine {
  productName: string;
  quantity: number;
  lineTotal: number;
}

export interface CustomerDisplayState {
  status: 'idle' | 'shopping' | 'completed';
  branchName?: string;
  lines: CustomerDisplayLine[];
  subTotal: number;
  discountTotal: number;
  estimatedTotal: number;
  /** Only set when status is 'completed'. */
  completedGrandTotal?: number;
  completedChangeDue?: number;
}

export function createCustomerDisplayChannel(): BroadcastChannel {
  return new BroadcastChannel(CUSTOMER_DISPLAY_CHANNEL);
}
