import { useState, type FormEvent } from 'react';
import { plainErrorMessage, type ApiErrorBody } from '../../../shared/errorMessages';

const INTERFACE_NAME = 'shopfloor';

/** Signs in to the ShopFloor interface only (Q1-B). */
export function SignInPage({ onSignedIn }: { onSignedIn: (userName: string) => void }) {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const response = await fetch('/api/identity/sign-in', {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username, password, interface: INTERFACE_NAME }),
      });
      if (response.ok) {
        const body = (await response.json()) as { user: { name: string } };
        onSignedIn(body.user.name);
        return;
      }
      const body = (await response.json().catch(() => null)) as ApiErrorBody | null;
      setError(plainErrorMessage(response.status, body));
    } catch {
      setError(plainErrorMessage(503));
    } finally {
      setBusy(false);
    }
  }

  return (
    <main style={{ maxWidth: 360, margin: '64px auto', padding: '0 16px' }}>
      <h1>ShopFloor sign in</h1>
      <form onSubmit={submit}>
        <label>
          User name
          <input value={username} onChange={(e) => setUsername(e.target.value)} autoComplete="username" required />
        </label>
        <label>
          Password
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete="current-password"
            required
          />
        </label>
        {error && <p role="alert">{error}</p>}
        <button type="submit" disabled={busy}>
          Sign in
        </button>
      </form>
    </main>
  );
}
