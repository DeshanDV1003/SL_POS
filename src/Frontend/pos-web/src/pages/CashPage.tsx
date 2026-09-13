import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { closeShift, finalizeDayEndReport, getDayEndReport, getOpenShift, openShift, recordMovement, type DayEndReportDto, type ShiftDto } from '../api/cash';
import { ApiError } from '../api/client';
import { getTerminals } from '../api/organization';
import { useAuth } from '../context/AuthContext';

const currency = new Intl.NumberFormat('en-LK', { style: 'currency', currency: 'LKR' });

export function CashPage() {
  const { session } = useAuth();
  const branchId = session?.user.branchIds[0] ?? null;
  const [terminalId, setTerminalId] = useState<number | null>(null);
  const [shift, setShift] = useState<ShiftDto | null>(null);
  const [openingFloat, setOpeningFloat] = useState('');
  const [movementAmount, setMovementAmount] = useState('');
  const [movementReason, setMovementReason] = useState('');
  const [closingFloat, setClosingFloat] = useState('');
  const [report, setReport] = useState<DayEndReportDto | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!branchId) return;
    getTerminals(branchId).then((terminals) => { if (terminals.length > 0) setTerminalId(terminals[0].id); });
  }, [branchId]);

  useEffect(() => {
    if (branchId && terminalId) getOpenShift(branchId, terminalId).then(setShift);
  }, [branchId, terminalId]);

  async function handleOpenShift() {
    if (!branchId || !terminalId) return;
    setError(null);
    try {
      const result = await openShift(branchId, terminalId, Number(openingFloat || 0));
      setShift(result);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Could not open shift.');
    }
  }

  async function handleMovement(movementType: string) {
    if (!branchId || !shift) return;
    setError(null);
    try {
      await recordMovement(branchId, shift.id, movementType, Number(movementAmount || 0), movementReason || undefined);
      setMovementAmount('');
      setMovementReason('');
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Could not record movement.');
    }
  }

  async function handleCloseShift() {
    if (!branchId || !shift) return;
    setError(null);
    try {
      const result = await closeShift(branchId, shift.id, Number(closingFloat || 0));
      setShift(result);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Could not close shift.');
    }
  }

  async function handleGenerateReport() {
    if (!branchId) return;
    setError(null);
    try {
      const today = new Date().toISOString().slice(0, 10);
      const result = await getDayEndReport(branchId, today);
      setReport(result);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Could not generate report.');
    }
  }

  async function handleFinalizeReport() {
    if (!branchId || !report) return;
    try {
      const result = await finalizeDayEndReport(branchId, report.id);
      setReport(result);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Could not finalize report.');
    }
  }

  return (
    <div className="dashboard">
      <header className="dashboard-header">
        <h1>Cash Management</h1>
        <Link to="/">Back to dashboard</Link>
      </header>

      {error && <div className="error-banner">{error}</div>}

      <section>
        <h2>Cashier Shift</h2>
        {!shift || shift.status === 'Closed' ? (
          <div className="totals-box">
            <label>
              Opening Float
              <input type="number" value={openingFloat} onChange={(e) => setOpeningFloat(e.target.value)} className="search-input" />
            </label>
            <button onClick={handleOpenShift} className="checkout-button">Open Shift</button>
            {shift?.status === 'Closed' && (
              <p className="hint">
                Last shift closed. Expected {currency.format(shift.expectedCash ?? 0)}, counted {currency.format(shift.closingFloatCounted ?? 0)}
                , variance {currency.format(shift.varianceAmount ?? 0)}.
              </p>
            )}
          </div>
        ) : (
          <div className="totals-box">
            <p>Shift #{shift.id} open since {new Date(shift.openedAtUtc).toLocaleTimeString()} — opening float {currency.format(shift.openingFloat)}</p>
            <div className="header-actions">
              <input type="number" placeholder="Amount" value={movementAmount} onChange={(e) => setMovementAmount(e.target.value)} className="qty-input" />
              <input type="text" placeholder="Reason" value={movementReason} onChange={(e) => setMovementReason(e.target.value)} className="search-input" style={{ maxWidth: 200 }} />
              <button onClick={() => handleMovement('CashIn')} className="hold-button">Cash In</button>
              <button onClick={() => handleMovement('CashOut')} className="hold-button">Cash Out</button>
              <button onClick={() => handleMovement('Petty')} className="hold-button">Petty</button>
            </div>
            <label>
              Closing Float Counted
              <input type="number" value={closingFloat} onChange={(e) => setClosingFloat(e.target.value)} className="search-input" />
            </label>
            <button onClick={handleCloseShift} className="checkout-button">Close Shift</button>
          </div>
        )}
      </section>

      <section>
        <h2>Day-End (Z) Report</h2>
        <button onClick={handleGenerateReport} className="hold-button">Generate Today's Report</button>
        {report && (
          <div className="receipt-card" style={{ margin: '1rem 0' }}>
            <p className="receipt-invoice">{report.businessDate} — {report.status}</p>
            <div className="receipt-line"><span>Gross Sales</span><span>{currency.format(report.grossSales)}</span></div>
            <div className="receipt-line"><span>Discounts</span><span>-{currency.format(report.discountTotal)}</span></div>
            <div className="receipt-line"><span>Tax</span><span>{currency.format(report.taxTotal)}</span></div>
            <div className="receipt-line"><span>Service Charge</span><span>{currency.format(report.serviceChargeTotal)}</span></div>
            <div className="receipt-line receipt-total"><span>Net Sales</span><span>{currency.format(report.netSales)}</span></div>
            <div className="receipt-line"><span>Cash</span><span>{currency.format(report.cashSalesTotal)}</span></div>
            <div className="receipt-line"><span>Card</span><span>{currency.format(report.cardSalesTotal)}</span></div>
            <div className="receipt-line"><span>Other</span><span>{currency.format(report.otherPaymentTotal)}</span></div>
            <div className="receipt-line"><span>Transactions / Voids</span><span>{report.transactionCount} / {report.voidCount}</span></div>
            {report.status === 'Draft' && (
              <button onClick={handleFinalizeReport} className="checkout-button" style={{ marginTop: '0.8rem' }}>Finalize Report</button>
            )}
          </div>
        )}
      </section>
    </div>
  );
}
