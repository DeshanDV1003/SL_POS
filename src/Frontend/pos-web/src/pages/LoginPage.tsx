import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { ApiError } from '../api/client';
import { useAuth } from '../context/AuthContext';

export function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [branchCode, setBranchCode] = useState('');
  const [terminalCode, setTerminalCode] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);
    try {
      await login({
        username,
        password,
        branchCode: branchCode || undefined,
        terminalCode: terminalCode || undefined,
      });
      navigate('/', { replace: true });
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError('Unable to reach the server. Please try again.');
      }
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div className="login-page">
      <form className="login-card" onSubmit={handleSubmit}>
        <h1>Universal POS</h1>
        <p className="subtitle">Sign in to continue</p>

        <label>
          Username
          <input
            type="text"
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            autoFocus
            required
          />
        </label>

        <label>
          Password
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
          />
        </label>

        <details>
          <summary>Terminal (optional)</summary>
          <label>
            Branch code
            <input type="text" value={branchCode} onChange={(e) => setBranchCode(e.target.value)} placeholder="e.g. CS-COL07" />
          </label>
          <label>
            Terminal code
            <input type="text" value={terminalCode} onChange={(e) => setTerminalCode(e.target.value)} placeholder="e.g. T1" />
          </label>
        </details>

        {error && <div className="error-banner" role="alert">{error}</div>}

        <button type="submit" disabled={isSubmitting}>
          {isSubmitting ? 'Signing in…' : 'Sign in'}
        </button>
      </form>
    </div>
  );
}
