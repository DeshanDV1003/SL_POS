import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ApiError } from '../api/client';
import { getFloors, openTable, type FloorInfo } from '../api/restaurant';
import { useAuth } from '../context/AuthContext';

const statusClass: Record<string, string> = {
  Available: 'table-available',
  Occupied: 'table-occupied',
  Reserved: 'table-reserved',
  Cleaning: 'table-cleaning',
  Billing: 'table-billing',
};

export function FloorPlanPage() {
  const { session } = useAuth();
  const branchId = session?.user.branchIds[0] ?? null;
  const [floors, setFloors] = useState<FloorInfo[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const navigate = useNavigate();

  useEffect(() => {
    if (branchId) refresh();
  }, [branchId]);

  function refresh() {
    if (branchId) getFloors(branchId).then(setFloors).catch(() => setError('Could not load floor plan.'));
  }

  async function handleTableClick(tableId: number, status: string, openOrderId: number | null) {
    if (!branchId) return;
    setError(null);
    if (status === 'Occupied' && openOrderId) {
      navigate(`/restaurant/orders/${openOrderId}`);
      return;
    }
    if (status !== 'Available') return;
    try {
      const order = await openTable(branchId, tableId);
      navigate(`/restaurant/orders/${order.id}`);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Could not open this table.');
    }
  }

  return (
    <div className="dashboard">
      <header className="dashboard-header">
        <div>
          <h1>Floor Plan</h1>
          <p>Tap an available table to seat, or an occupied table to view its order.</p>
        </div>
        <Link to="/">Back to dashboard</Link>
      </header>

      {error && <div className="error-banner">{error}</div>}

      {floors === null ? (
        <p>Loading…</p>
      ) : floors.length === 0 ? (
        <p>No floors configured for this branch.</p>
      ) : (
        floors.map((floor) => (
          <section key={floor.id}>
            <h2>{floor.name}</h2>
            <div className="table-grid">
              {floor.tables.map((table) => (
                <button
                  key={table.id}
                  className={`table-card ${statusClass[table.status] ?? ''}`}
                  onClick={() => handleTableClick(table.id, table.status, table.openOrderId)}
                >
                  <strong>{table.name}</strong>
                  <span>{table.capacity} seats</span>
                  <span className="table-status">{table.status}</span>
                </button>
              ))}
            </div>
          </section>
        ))
      )}
    </div>
  );
}
