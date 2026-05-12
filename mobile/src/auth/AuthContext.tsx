import React, { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import AsyncStorage from '@react-native-async-storage/async-storage';
import * as SecureStore from 'expo-secure-store';
import { Platform } from 'react-native';
import apiClient, { setAuthToken, setOnUnauthorized } from '../api/apiClient';
import { registerForPushNotificationsAsync, unregisterPushAsync } from '../notifications/pushRegistration';

// Token JWT in SecureStore (cifrato) su device nativi, AsyncStorage su web.
const TOKEN_KEY = 'oncallendar.token';
const USER_KEY = 'oncallendar.user';

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

const isNative = Platform.OS !== 'web';

async function saveToken(token: string) {
  if (isNative) await SecureStore.setItemAsync(TOKEN_KEY, token);
  else await AsyncStorage.setItem(TOKEN_KEY, token);
}
async function loadToken(): Promise<string | null> {
  if (isNative) return SecureStore.getItemAsync(TOKEN_KEY);
  return AsyncStorage.getItem(TOKEN_KEY);
}
async function deleteToken() {
  if (isNative) await SecureStore.deleteItemAsync(TOKEN_KEY);
  else await AsyncStorage.removeItem(TOKEN_KEY);
}

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
    await deleteToken();
    await AsyncStorage.removeItem(USER_KEY);
  }, []);

  // Registra il callback di auto-logout sull'apiClient (gestione 401)
  useEffect(() => {
    setOnUnauthorized(() => { void logout(); });
    return () => setOnUnauthorized(null);
  }, [logout]);

  useEffect(() => {
    (async () => {
      try {
        const savedToken = await loadToken();
        const rawUser = await AsyncStorage.getItem(USER_KEY);
        if (savedToken && rawUser) {
          const parsed = JSON.parse(rawUser) as AuthUser;
          setAuthToken(savedToken);
          setToken(savedToken);
          setUser(parsed);
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
    await saveToken(data.token);
    await AsyncStorage.setItem(USER_KEY, JSON.stringify(u));
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
