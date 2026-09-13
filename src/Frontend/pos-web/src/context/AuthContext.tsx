import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { login as loginRequest, logout as logoutRequest } from '../api/auth';
import { registerAccessTokenProvider } from '../api/client';
import type { LoginRequest, LoginResult, UserProfile } from '../api/types';

interface Session {
  accessToken: string;
  refreshToken: string;
  user: UserProfile;
}

interface AuthContextValue {
  session: Session | null;
  login: (request: LoginRequest) => Promise<void>;
  logout: () => Promise<void>;
  hasPermission: (code: string) => boolean;
}

const STORAGE_KEY = 'universalpos.session';

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

function loadStoredSession(): Session | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as Session) : null;
  } catch {
    return null;
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | null>(() => loadStoredSession());

  useEffect(() => {
    registerAccessTokenProvider(() => session?.accessToken ?? null);
  }, [session]);

  useEffect(() => {
    if (session) {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
    } else {
      localStorage.removeItem(STORAGE_KEY);
    }
  }, [session]);

  const value = useMemo<AuthContextValue>(
    () => ({
      session,
      login: async (request) => {
        const result: LoginResult = await loginRequest(request);
        setSession({ accessToken: result.accessToken, refreshToken: result.refreshToken, user: result.user });
      },
      logout: async () => {
        if (session) {
          try {
            await logoutRequest(session.refreshToken);
          } catch {
            // Best-effort server-side revocation; the client still clears its own session below.
          }
        }
        setSession(null);
      },
      hasPermission: (code) => session?.user.permissions.includes(code) ?? false,
    }),
    [session],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}
