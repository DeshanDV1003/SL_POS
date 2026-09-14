import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { getKdsTickets, getKitchenStations, getTicketPrintPayload, updateTicketStatus, type KitchenStationInfo, type TicketInfo } from '../api/restaurant';
import { useAuth } from '../context/AuthContext';

const nextStatus: Record<string, string | null> = {
  Sent: 'Accepted',
  Accepted: 'Preparing',
  Preparing: 'Ready',
  Ready: 'Served',
  Served: null,
};

export function KdsPage() {
  const { session } = useAuth();
  const branchId = session?.user.branchIds[0] ?? null;
  const [stations, setStations] = useState<KitchenStationInfo[]>([]);
  const [stationId, setStationId] = useState<number | null>(null);
  const [tickets, setTickets] = useState<TicketInfo[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [printPreview, setPrintPreview] = useState<{ ticketId: number; text: string } | null>(null);

  useEffect(() => {
    if (branchId) getKitchenStations(branchId).then((s) => { setStations(s); if (s.length > 0) setStationId(s[0].id); });
  }, [branchId]);

  useEffect(() => {
    if (branchId && stationId) refresh();
  }, [branchId, stationId]);

  function refresh() {
    if (branchId && stationId) getKdsTickets(branchId, stationId).then(setTickets).catch(() => setError('Could not load tickets.'));
  }

  async function handleAdvance(ticketId: number, currentStatus: string) {
    if (!branchId) return;
    const next = nextStatus[currentStatus];
    if (!next) return;
    try {
      await updateTicketStatus(branchId, ticketId, next);
      refresh();
    } catch {
      setError('Could not update ticket status.');
    }
  }

  async function handlePrint(ticketId: number) {
    if (!branchId) return;
    try {
      const text = await getTicketPrintPayload(branchId, ticketId);
      setPrintPreview({ ticketId, text });
    } catch {
      setError('Could not load the ticket print payload.');
    }
  }

  return (
    <div className="dashboard">
      <header className="dashboard-header">
        <div>
          <h1>Kitchen Display</h1>
        </div>
        <Link to="/">Back to dashboard</Link>
      </header>

      <div className="station-tabs">
        {stations.map((s) => (
          <button
            key={s.id}
            className={s.id === stationId ? 'station-tab active' : 'station-tab'}
            onClick={() => setStationId(s.id)}
          >
            {s.name}
          </button>
        ))}
      </div>

      {error && <div className="error-banner">{error}</div>}

      <div className="kds-grid">
        {tickets.map((ticket) => (
          <div key={ticket.id} className="kds-card">
            <div className="kds-card-header">
              <strong>{ticket.ticketNumber}</strong>
              {ticket.tableId && <span>Table #{ticket.tableId}</span>}
            </div>
            <ul>
              {ticket.lines.map((line, i) => (
                <li key={i}>{line.quantity}x {line.productName}{line.notes ? ` (${line.notes})` : ''}</li>
              ))}
            </ul>
            <div className="kds-card-footer">
              <span className={`badge-warning`}>{ticket.status}</span>
              <button onClick={() => handlePrint(ticket.id)} className="hold-button">Print</button>
              {nextStatus[ticket.status] && (
                <button onClick={() => handleAdvance(ticket.id, ticket.status)} className="hold-button">
                  Mark {nextStatus[ticket.status]}
                </button>
              )}
            </div>
          </div>
        ))}
        {tickets.length === 0 && <p>No active tickets for this station.</p>}
      </div>

      {printPreview && (
        <div className="modal-overlay" onClick={() => setPrintPreview(null)}>
          <div className="receipt-card" onClick={(e) => e.stopPropagation()}>
            <pre className="printed-receipt">{printPreview.text}</pre>
            <button onClick={() => window.print()} className="hold-button">Print</button>
            <button onClick={() => setPrintPreview(null)} className="checkout-button">Close</button>
          </div>
        </div>
      )}
    </div>
  );
}
