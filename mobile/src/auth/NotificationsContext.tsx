import React, {
  createContext, useCallback, useContext, useEffect, useRef, useState,
} from 'react';
import { MeDto, NotificationDto, NotificationsApi, UsersApi } from '../api/endpoints';
import { useAuth } from '../auth/AuthContext';

type NotificationsCtx = {
  /** Totale = notifiche backend non lette + alert account "fissi" non risolti. */
  unreadCount: number;
  /** Quanti alert account "fissi" sono ancora attivi (default email / pwd da cambiare). */
  stickyCount: number;
  notifications: NotificationDto[];
  me: MeDto | null;
  loading: boolean;
  refresh: () => Promise<void>;
  refreshMe: () => Promise<void>;
  /** Segna una singola notifica come letta (sia backend sia stato locale). */
  markRead: (id: string) => Promise<void>;
  markAllRead: () => Promise<void>;
};

const Ctx = createContext<NotificationsCtx>({
  unreadCount: 0,
  stickyCount: 0,
  notifications: [],
  me: null,
  loading: false,
  refresh: async () => {},
  refreshMe: async () => {},
  markRead: async () => {},
  markAllRead: async () => {},
});

export const useNotifications = () => useContext(Ctx);

const POLL_MS = 30_000;

/**
 * Calcola quante "notifiche fisse di sistema" sono ancora attive per l'utente
 * corrente. Queste notifiche restano nel badge campanella finch&eacute; l'utente
 * non risolve la condizione (es. cambia email di default, cambia password).
 */
function computeStickyCount(me: MeDto | null): number {
  if (!me) return 0;
  let n = 0;
  // Email: o pendi conferma di un nuovo indirizzo o stai usando l'email di default.
  if (me.pendingEmail || me.isDefaultEmail) n += 1;
  // Password: o devi cambiarla obbligatoriamente o &egrave; scaduta.
  if (me.passwordChangeRequired || me.passwordExpired) n += 1;
  return n;
}

export const NotificationsProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const { user } = useAuth();
  const [unreadBackend, setUnreadBackend] = useState(0);
  const [me, setMe] = useState<MeDto | null>(null);
  const [notifications, setNotifications] = useState<NotificationDto[]>([]);
  const [loading, setLoading] = useState(false);
  const timer = useRef<ReturnType<typeof setInterval> | null>(null);

  const fetchCount = useCallback(async () => {
    try {
      const n = await NotificationsApi.unreadCount();
      setUnreadBackend(n);
    } catch {
      // silenzioso
    }
  }, []);

  const refreshMe = useCallback(async () => {
    try { setMe(await UsersApi.me()); } catch { /* silent */ }
  }, []);

  const refresh = useCallback(async () => {
    if (!user) return;
    setLoading(true);
    try {
      const list = await NotificationsApi.list();
      setNotifications(list);
      setUnreadBackend(list.filter(n => !n.isRead).length);
    } catch {
      // silenzioso
    } finally {
      setLoading(false);
    }
    refreshMe();
  }, [user, refreshMe]);

  const markRead = useCallback(async (id: string) => {
    setNotifications(prev => prev.map(n => n.id === id && !n.isRead ? { ...n, isRead: true } : n));
    setUnreadBackend(prev => Math.max(0, prev - 1));
    try { await NotificationsApi.markRead(id); } catch { /* silent */ }
  }, []);

  const markAllRead = useCallback(async () => {
    await NotificationsApi.markAllRead();
    setUnreadBackend(0);
    setNotifications(prev => prev.map(n => ({ ...n, isRead: true })));
  }, []);

  useEffect(() => {
    if (!user) {
      setUnreadBackend(0);
      setNotifications([]);
      setMe(null);
      if (timer.current) clearInterval(timer.current);
      return;
    }
    fetchCount();
    refreshMe();
    timer.current = setInterval(() => { fetchCount(); refreshMe(); }, POLL_MS);
    return () => { if (timer.current) clearInterval(timer.current); };
  }, [user, fetchCount, refreshMe]);

  const stickyCount = computeStickyCount(me);
  const unreadCount = unreadBackend + stickyCount;

  return (
    <Ctx.Provider value={{ unreadCount, stickyCount, notifications, me, loading, refresh, refreshMe, markRead, markAllRead }}>
      {children}
    </Ctx.Provider>
  );
};
