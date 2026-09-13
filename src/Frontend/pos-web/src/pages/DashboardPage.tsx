import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { getBranches, getCompanies } from '../api/organization';
import type { Branch, Company } from '../api/types';
import { useAuth } from '../context/AuthContext';

export function DashboardPage() {
  const { session, logout, hasPermission } = useAuth();
  const [companies, setCompanies] = useState<Company[] | null>(null);
  const [branches, setBranches] = useState<Branch[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);

  useEffect(() => {
    if (!session) return;

    if (hasPermission('company.manage')) {
      getCompanies()
        .then(setCompanies)
        .catch(() => setLoadError('Could not load companies.'));
    }

    getBranches(session.user.companyId)
      .then(setBranches)
      .catch(() => setLoadError('Could not load branches.'));
  }, [session, hasPermission]);

  if (!session) return null;

  return (
    <div className="dashboard">
      <header className="dashboard-header">
        <div>
          <h1>Universal POS</h1>
          <p>
            Signed in as <strong>{session.user.fullName}</strong> ({session.user.roles.join(', ')})
          </p>
        </div>
        <div className="header-actions">
          <Link to="/checkout" className="nav-link">Checkout</Link>
          <Link to="/restaurant" className="nav-link">Floor Plan</Link>
          <Link to="/kds" className="nav-link">Kitchen Display</Link>
          <Link to="/products" className="nav-link">Product Catalog</Link>
          <Link to="/stock" className="nav-link">Stock On Hand</Link>
          <button onClick={() => logout()}>Sign out</button>
        </div>
      </header>

      {loadError && <div className="error-banner">{loadError}</div>}

      <section>
        <h2>Your branches</h2>
        {branches === null ? (
          <p>Loading…</p>
        ) : (
          <ul className="card-list">
            {branches.map((b) => (
              <li key={b.id} className="card">
                <strong>{b.name}</strong>
                <span>{b.code}</span>
                <span>{b.businessTypeFlags}</span>
              </li>
            ))}
          </ul>
        )}
      </section>

      {companies && (
        <section>
          <h2>Companies (admin view)</h2>
          <ul className="card-list">
            {companies.map((c) => (
              <li key={c.id} className="card">
                <strong>{c.name}</strong>
                <span>{c.defaultCurrencyCode}{c.isVatRegistered ? ' · VAT registered' : ''}</span>
              </li>
            ))}
          </ul>
        </section>
      )}

      <section>
        <h2>Your permissions</h2>
        <div className="permission-grid">
          {session.user.permissions.map((p) => (
            <span key={p} className="permission-chip">{p}</span>
          ))}
        </div>
      </section>
    </div>
  );
}
