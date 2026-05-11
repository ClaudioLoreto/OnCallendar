import React, { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import AsyncStorage from '@react-native-async-storage/async-storage';
import apiClient, { setAuthToken, setOnUnauthorized } from '../api/apiClient';
import { registerForPushNotificationsAsync, unregisterPushAsync } from '../notifications/pushRegistration';

export type AuthUser = {
  userId: string;
  email: string;
  fullName: string;
  role: 'SuperAdmin' | 'Medico';
  tenantId: string | null;
};

type AuthState = {
  user: AuthUser | null;
  token: string | null;
  loading: boolean;
  login: (email: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
};

const STORAGE_KEY = 'oncallendar.auth';
const AuthCtx = createContext<AuthState | null>(null);

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [token, setToken] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const pushTokenRef = useRef<string | null>(null);

  const logout = useCallback(async () => {
    // best-effort unregister del device token prima di pulire l'auth
    await unregisterPushAsync(pushTokenRef.current);
    pushTokenRef.current = null;
    setAuthToken(null);
    setToken(null);
    setUser(null);
    await AsyncStorage.removeItem(STORAGE_KEY);
  }, []);

  // Registra il callback di auto-logout sull'apiClient (gestione 401)
  useEffect(() => {
    setOnUnauthorized(() => { void logout(); });
    return () => setOnUnauthorized(null);
  }, [logout]);

  useEffect(() => {
    (async () => {
      try {
        const raw = await AsyncStorage.getItem(STORAGE_KEY);
        if (raw) {
          const parsed = JSON.parse(raw) as { token: string; user: AuthUser };
          setAuthToken(parsed.token);
          setToken(parsed.token);
          setUser(parsed.user);
          // Refresh push token al riavvio (best effort).
          void registerForPushNotificationsAsync().then(t => { pushTokenRef.current = t; });
        }
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  const login = useCallback(async (email: string, password: string) => {
    const { data } = await apiClient.post('/api/auth/login', { email, password });
    const u: AuthUser = {
      userId: data.userId,
      email: data.email,
      fullName: data.fullName,
      role: data.role,
      tenantId: data.tenantId,
    };
    setAuthToken(data.token);
    setToken(data.token);
    setUser(u);
    await AsyncStorage.setItem(STORAGE_KEY, JSON.stringify({ token: data.token, user: u }));
    // Registra il device per le push (best effort).
    void registerForPushNotificationsAsync().then(t => { pushTokenRef.current = t; });
  }, []);

  const value = useMemo<AuthState>(
    () => ({ user, token, loading, login, logout }),
    [user, token, loading, login, logout],
  );

  return <AuthCtx.Provider value={value}>{children}</AuthCtx.Provider>;
};

export const useAuth = (): AuthState => {
  const ctx = useContext(AuthCtx);
  if (!ctx) throw new Error('useAuth must be inside AuthProvider');
  return ctx;
};
