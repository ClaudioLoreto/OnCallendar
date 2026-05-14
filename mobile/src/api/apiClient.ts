/**
 * apiClient.ts
 *
 * Configura l'IP del backend .NET (in esecuzione sul PC Windows) in modo
 * trasparente per Expo Go, simulatore iOS, simulatore Android e device fisici.
 *
 * COME FUNZIONA L'IP:
 * - Su simulatore iOS si può usare 'localhost'.
 * - Su emulatore Android serve '10.0.2.2'.
 * - Su iPhone fisico (Expo Go) NON funziona localhost: serve l'IP LAN del PC
 *   (es. 192.168.1.42). Lo leggiamo automaticamente da `Constants.expoConfig.hostUri`,
 *   che è l'host del Metro bundler scansionato dal QR code (stesso PC, stessa rete).
 *
 * OVERRIDE MANUALE:
 * - Imposta in `app.config.ts` (extra.apiBaseUrl) o via env EXPO_PUBLIC_API_BASE_URL.
 *
 * GESTIONE 401:
 * - Quando il backend risponde 401 (token scaduto o invalidato — es. dopo un
 *   reset DB) viene chiamata l'`onUnauthorized` registrata da AuthContext, che
 *   svuota lo storage e riporta sulla schermata di login. Questo evita la
 *   "schermata vuota" che si verificava finché il client tentava di usare un
 *   JWT vecchio incompatibile col nuovo schema.
 */

import axios, { AxiosInstance, AxiosError } from 'axios';
import Constants from 'expo-constants';
import { Platform } from 'react-native';

const API_PORT = 5000;

function resolveBaseUrl(): string {
  // 1) Override esplicito da env (build EAS / .env)
  const fromEnv = process.env.EXPO_PUBLIC_API_BASE_URL;
  if (fromEnv) return fromEnv;

  // 2) Su web la WebApp è servita dallo stesso dominio dell'API (Railway):
  //    usiamo path relativo via window.location.origin → niente CORS, niente
  //    URL hardcoded da aggiornare ad ogni cambio di dominio.
  if (Platform.OS === 'web' && typeof window !== 'undefined' && window.location?.origin) {
    return window.location.origin;
  }

  // 3) Override da app.config (extra) – utile per APK release
  const fromExtra =
    (Constants.expoConfig?.extra as Record<string, string> | undefined)?.apiBaseUrl;
  if (fromExtra) return fromExtra;

  // 4) Auto-detect via Metro bundler host (Expo Go in dev)
  //    hostUri es: "192.168.1.42:8081"
  const hostUri =
    Constants.expoConfig?.hostUri ??
    // @ts-ignore - manifest legacy fallback per Expo SDK precedenti
    (Constants as any).manifest?.debuggerHost ??
    '';

  const lanIp = hostUri.split(':')[0];
  if (lanIp && lanIp !== 'localhost' && lanIp !== '127.0.0.1') {
    return `http://${lanIp}:${API_PORT}`;
  }

  // 5) Fallback per simulatori
  if (Platform.OS === 'android') return `http://10.0.2.2:${API_PORT}`;
  return `http://localhost:${API_PORT}`;
}

export const API_BASE_URL = resolveBaseUrl();

if (__DEV__) {
  // eslint-disable-next-line no-console
  console.log('[OnCallendar] API_BASE_URL =', API_BASE_URL);
}

let authToken: string | null = null;
export const setAuthToken = (token: string | null) => {
  authToken = token;
};
export const getAuthToken = () => authToken;

let onUnauthorized: (() => void | Promise<void>) | null = null;
/** Registrato da AuthContext per fare auto-logout su 401. */
export const setOnUnauthorized = (cb: (() => void | Promise<void>) | null) => {
  onUnauthorized = cb;
};

export const apiClient: AxiosInstance = axios.create({
  baseURL: API_BASE_URL,
  timeout: 15_000,
  headers: {
    'Content-Type': 'application/json',
    Accept: 'application/json',
    // bypass localtunnel reminder page when tunneling backend in dev
    'bypass-tunnel-reminder': 'true',
    // User-Agent: solo su native (i browser bloccano l'header → errore in console)
    ...(Platform.OS !== 'web' ? { 'User-Agent': 'OnCallendarMobile' } : {}),
  },
});

apiClient.interceptors.request.use((config) => {
  if (authToken) {
    config.headers.Authorization = `Bearer ${authToken}`;
  }
  return config;
});

apiClient.interceptors.response.use(
  (res) => res,
  async (error: AxiosError) => {
    const status = error.response?.status;
    if (__DEV__) {
      // eslint-disable-next-line no-console
      console.warn(
        '[OnCallendar] API error',
        error.config?.method?.toUpperCase(),
        error.config?.url,
        '→',
        status ?? error.message,
      );
    }
    // 401 = token mancante/scaduto/non valido → forza logout (fix "schermata vuota")
    if (status === 401 && onUnauthorized) {
      try { await onUnauthorized(); } catch { /* ignore */ }
    }
    return Promise.reject(error);
  },
);

export default apiClient;
