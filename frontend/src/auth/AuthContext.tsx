import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react';
import { tokenStore } from '@/lib/api';
import { authApi } from '@/lib/services';
import type { LoginResponse, RegisterRequest, UserProfile } from '@/types/api';
import { UserRole } from '@/lib/enums';

interface AuthState {
  user: UserProfile | null;
  loading: boolean;
  login: (email: string, password: string) => Promise<UserProfile>;
  register: (body: RegisterRequest) => Promise<UserProfile>;
  logout: () => Promise<void>;
  refreshUser: () => Promise<void>;
  /** Optimistically patch the cached user (e.g. after email/phone verification). */
  patchUser: (patch: Partial<UserProfile>) => void;
}

const AuthContext = createContext<AuthState | undefined>(undefined);

function fromLogin(r: LoginResponse): UserProfile {
  return {
    userId: r.userId,
    fullName: r.fullName,
    email: r.email,
    role: r.role,
    isVerified: r.isVerified,
    emailVerified: r.emailVerified,
    phoneVerified: r.phoneVerified,
    tripNestId: r.tripNestId ?? null,
  };
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserProfile | null>(null);
  const [loading, setLoading] = useState(true);

  const refreshUser = useCallback(async () => {
    if (!tokenStore.access) {
      setUser(null);
      return;
    }
    try {
      setUser(await authApi.me());
    } catch {
      setUser(null);
    }
  }, []);

  useEffect(() => {
    refreshUser().finally(() => setLoading(false));
  }, [refreshUser]);

  // Hard logout when the refresh flow gives up.
  useEffect(() => {
    const onUnauth = () => {
      tokenStore.clear();
      setUser(null);
    };
    window.addEventListener('tn:unauthorized', onUnauth);
    return () => window.removeEventListener('tn:unauthorized', onUnauth);
  }, []);

  const login = useCallback(async (email: string, password: string) => {
    const res = await authApi.login(email, password);
    tokenStore.set(res.accessToken, res.refreshToken);
    const u = fromLogin(res);
    setUser(u);
    return u;
  }, []);

  const register = useCallback(async (body: RegisterRequest) => {
    const res = await authApi.register(body);
    tokenStore.set(res.accessToken, res.refreshToken);
    const u = fromLogin(res);
    setUser(u);
    return u;
  }, []);

  const logout = useCallback(async () => {
    try {
      await authApi.logout();
    } catch {
      /* ignore */
    }
    tokenStore.clear();
    setUser(null);
  }, []);

  const patchUser = useCallback((patch: Partial<UserProfile>) => {
    setUser((u) => (u ? { ...u, ...patch } : u));
  }, []);

  const value = useMemo(
    () => ({ user, loading, login, register, logout, refreshUser, patchUser }),
    [user, loading, login, register, logout, refreshUser, patchUser],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
}

export const isHost = (role?: number) => role === UserRole.Landlord;
export const isService = (role?: number) => role === UserRole.Agent || role === UserRole.Caretaker;
export const roleHome = (role?: number): string => {
  switch (role) {
    case UserRole.Landlord:
      return '/host';
    case UserRole.Agent:
      return '/agent';
    case UserRole.Caretaker:
      return '/caretaker';
    case UserRole.Admin:
      return '/admin';
    default:
      return '/dashboard';
  }
};
