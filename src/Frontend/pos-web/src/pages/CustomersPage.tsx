import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { ApiError } from '../api/client';
import { createCustomer, getCustomers, getMembershipTiers, type CustomerDto, type MembershipTierDto } from '../api/crm';

export function CustomersPage() {
  const [customers, setCustomers] = useState<CustomerDto[] | null>(null);
  const [tiers, setTiers] = useState<MembershipTierDto[]>([]);
  const [name, setName] = useState('');
  const [phone, setPhone] = useState('');
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    refresh();
    getMembershipTiers().then(setTiers).catch(() => {});
  }, []);

  function refresh() {
    getCustomers().then(setCustomers).catch(() => setError('Could not load customers.'));
  }

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault();
    if (!name.trim()) return;
    setError(null);
    try {
      await createCustomer(name.trim(), phone.trim() || undefined);
      setName('');
      setPhone('');
      refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Could not create customer.');
    }
  }

  const tierName = (id: number | null) => tiers.find((t) => t.id === id)?.name ?? '—';

  return (
    <div className="dashboard">
      <header className="dashboard-header">
        <div>
          <h1>Customers</h1>
          <p>{customers?.length ?? 0} customers</p>
        </div>
        <Link to="/">Back to dashboard</Link>
      </header>

      <form onSubmit={handleCreate} className="header-actions" style={{ marginBottom: '1.5rem' }}>
        <input type="text" placeholder="Name" value={name} onChange={(e) => setName(e.target.value)} className="search-input" style={{ maxWidth: 220 }} />
        <input type="text" placeholder="Phone (optional)" value={phone} onChange={(e) => setPhone(e.target.value)} className="search-input" style={{ maxWidth: 180 }} />
        <button type="submit" className="checkout-button" style={{ padding: '0.6rem 1.2rem' }}>Add Customer</button>
      </form>

      {error && <div className="error-banner">{error}</div>}

      {customers === null ? (
        <p>Loading…</p>
      ) : (
        <table className="product-table">
          <thead>
            <tr><th>Name</th><th>Phone</th><th>Loyalty Points</th><th>Tier</th></tr>
          </thead>
          <tbody>
            {customers.map((c) => (
              <tr key={c.id}>
                <td>{c.name}</td>
                <td>{c.phone ?? '—'}</td>
                <td>{c.loyaltyPointsBalance}</td>
                <td>{tierName(c.membershipTierId)}</td>
              </tr>
            ))}
            {customers.length === 0 && <tr><td colSpan={4} className="empty-cart">No customers yet.</td></tr>}
          </tbody>
        </table>
      )}
    </div>
  );
}
